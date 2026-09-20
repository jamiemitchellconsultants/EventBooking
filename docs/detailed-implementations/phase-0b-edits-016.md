# 00b — Vocabulary edits 16 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — src/EventBooking.Application/Notifications/EmailDeliveryService.cs — 1/1

<!-- vocabulary-file: {"id":83,"oldPath":"src/EventBooking.Application/Notifications/EmailDeliveryService.cs","newPath":"src/EventBooking.Application/Notifications/EmailDeliveryService.cs","beforeSha":"f66bca5ee4e834fadb79d4864537aaef0ae8a38c904e8465515b0daa317189fe","afterSha":"e9c1e75743b51751ae456f3a50db948ff9e8f51291ed54c105af3194b3ebd91c","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace EventBooking.Application.Notifications;

/// <summary>
/// Coordinates durable pending deliveries and provider attempts. Staging is intentionally
/// synchronous so the caller can save it beside its business state; dispatch starts only after
/// that transaction has committed.
/// </summary>
/// <param name="deliveries">Persists and locks durable delivery rows.</param>
/// <param name="sender">Calls the configured provider transport.</param>
/// <param name="unitOfWork">Owns claim and outcome transactions.</param>
/// <param name="clock">Supplies deterministic claim and outcome timestamps.</param>
/// <param name="logger">Records failures in non-critical post-send audit callbacks.</param>
public sealed class EmailDeliveryService(
    IEmailDeliveryRepository deliveries,
    IEmailSender sender,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<EmailDeliveryService> logger)
{
    private static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(5);
    private DateTimeOffset _lastStagedAt = DateTimeOffset.MinValue;

    /// <summary>
    /// Stages a pending delivery containing only safe context identifiers. The caller owns the
    /// enclosing transaction and must save it before calling <see cref="DispatchAsync"/>.
    /// </summary>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="template">The template.</param>
    /// <param name="inviteId">The invite id.</param>
    /// <param name="bookingId">The booking id.</param>
    /// <param name="eventId">The event id.</param>
    /// <param name="after">The after.</param>
    public EmailLog StagePending(
        Guid attendeeId,
        EmailTemplate template,
        Guid? inviteId = null,
        Guid? bookingId = null,
        Guid? eventId = null,
        DateTimeOffset? after = null)
    {
        var createdAt = clock.UtcNow;
        if (after is not null && createdAt <= after.Value)
        {
            createdAt = after.Value.AddTicks(1);
        }

        if (createdAt <= _lastStagedAt)
        {
            createdAt = _lastStagedAt.AddTicks(1);
        }

        _lastStagedAt = createdAt;
        var delivery = EmailLog.RecordPending(
            Guid.NewGuid(),
            attendeeId,
            template,
            createdAt,
            inviteId,
            bookingId,
            eventId);

        deliveries.Add(delivery);
        return delivery;
    }

    /// <summary>Claims a newly staged row for the current business transaction's post-commit plan.</summary>
    /// <param name="delivery">The delivery.</param>
    public void ClaimForDispatch(EmailLog delivery)
    {
        if (!delivery.TryClaim(clock.UtcNow, ClaimLease))
        {
            throw new InvalidOperationException("The email delivery is already claimed.");
        }
    }

    /// <summary>
    /// Claims one pending delivery, sends it outside the claim transaction, and records Sent or
    /// Failed in a second durable transaction. A fresh claim held by another worker returns
    /// Pending without calling the transport.
    /// </summary>
    /// <param name="onSent">Optional audit callback staged with the Sent outcome.</param>
    /// <param name="deliveryId">The delivery id.</param>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<EmailStatus> DispatchAsync(
        Guid deliveryId,
        EmailMessage message,
        CancellationToken cancellationToken,
        Action? onSent = null)
    {
        await using (var claimTransaction = await unitOfWork.BeginTransactionAsync(cancellationToken))
        {
            var delivery = await deliveries.LockForUpdateAsync(deliveryId, cancellationToken);
            if (delivery is null)
            {
                await claimTransaction.RollbackAsync(cancellationToken);
                return EmailStatus.Failed;
            }

            if (delivery.Status is EmailStatus.Sent or EmailStatus.Resolved)
            {
                await claimTransaction.CommitAsync(cancellationToken);
                return delivery.Status;
            }

            if (!delivery.TryClaim(clock.UtcNow, ClaimLease))
            {
                await claimTransaction.CommitAsync(cancellationToken);
                return EmailStatus.Pending;
            }

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await claimTransaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await claimTransaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        return await DispatchClaimedAsync(deliveryId, message, cancellationToken, onSent);
    }

    /// <summary>
    /// Sends a row claimed by the current business transaction and records its terminal outcome.
    /// The claim must be committed before this method is called; an uncompleted claim remains
    /// Pending and can be reclaimed after its lease expires.
    /// </summary>
    /// <param name="onSent">Optional audit callback staged with the Sent outcome.</param>
    /// <param name="deliveryId">The delivery id.</param>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<EmailStatus> DispatchClaimedAsync(
        Guid deliveryId,
        EmailMessage message,
        CancellationToken cancellationToken,
        Action? onSent = null)
    {
        bool sent;
        try
        {
            sent = await sender.SendAsync(message with { DeliveryId = deliveryId }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            sent = false;
        }

        await using var resultTransaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var completed = await deliveries.LockForUpdateAsync(deliveryId, cancellationToken);
        if (completed is not null)
        {
            if (sent)
            {
                completed.MarkSent(clock.UtcNow);
                try
                {
                    onSent?.Invoke();
                }
                catch (Exception exception)
                {
                    // A provider result is already real; an audit callback must not leave the
                    // durable delivery pending and invite a duplicate provider attempt.
                    logger.LogError(
                        exception,
                        "The post-send audit callback failed for email delivery {DeliveryId}.",
                        deliveryId);
                }
            }
            else
            {
                completed.MarkFailed(clock.UtcNow);
            }

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await resultTransaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await resultTransaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        else
        {
            await resultTransaction.RollbackAsync(cancellationToken);
        }

        return sent ? EmailStatus.Sent : EmailStatus.Failed;
    }
}
`````

## before — src/EventBooking.Application/Notifications/RetryEmailHandler.cs — 1/1

<!-- vocabulary-file: {"id":84,"oldPath":"src/EventBooking.Application/Notifications/RetryEmailHandler.cs","newPath":"src/EventBooking.Application/Notifications/RetryEmailHandler.cs","beforeSha":"1080adcf12a27c2338c88c63c320caf39497425dc5fc21d9ebf5ba4a5b5d3010","afterSha":"27cccceeefb438ac62995ca196f58c5d474578fc060cdc90db7146203fd6ab0d","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Notifications;

/// <summary>Requests a staff-authorized retry of the candidate's latest failed or pending email.</summary>
/// <param name="StaffUserId">The coordinator requesting the retry.</param>
/// <param name="CandidateId">The candidate whose latest delivery should be retried.</param>
public sealed record RetryEmailCommand(Guid StaffUserId, Guid CandidateId);

/// <summary>Reports the durable result of a template-aware email retry.</summary>
/// <param name="DeliveryStatus">The provider outcome of the new attempt.</param>
/// <param name="DeliveryId">The new durable delivery identifier.</param>
public sealed record RetryEmailOutcome(string DeliveryStatus, Guid DeliveryId);

/// <summary>
/// Regenerates the latest delivery from safe persisted context. Token-bearing templates rotate
/// their hash before a fresh raw token is placed in the in-memory provider message.
/// </summary>
/// <param name="access">Authorizes candidate-management access from the caller's complete profile.</param>
/// <param name="candidates">Locks the candidate lifecycle root.</param>
/// <param name="invites">Loads and rotates pending invite hashes.</param>
/// <param name="bookings">Loads and rotates active booking hashes.</param>
/// <param name="slots">Loads template slot context.</param>
/// <param name="deliveryRepository">Loads the latest delivery server-side.</param>
/// <param name="deliveries">Stages and dispatches the replacement attempt.</param>
/// <param name="tokens">Issues fresh raw tokens and their hashes.</param>
/// <param name="unitOfWork">Owns the replacement transaction.</param>
/// <param name="clock">Supplies claim and expiry times.</param>
/// <param name="portal">Provides candidate portal links and copy settings.</param>
/// <param name="appointments">The appointments.</param>
public sealed class RetryEmailHandler(
    IStaffAccessAuthorizer access,
    ICandidateRepository candidates,
    IInviteRepository invites,
    IBookingRepository bookings,
    IConfirmedSlotRepository slots,
    IBookingAppointmentRepository appointments,
    IEmailDeliveryRepository deliveryRepository,
    EmailDeliveryService deliveries,
    ITokenService tokens,
    IUnitOfWork unitOfWork,
    IClock clock,
    CandidatePortalOptions portal)
{
    private static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(5);

    /// <summary>Retries the newest unresolved delivery and returns its post-commit provider outcome.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<RetryEmailOutcome>> HandleAsync(
        RetryEmailCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageCandidates,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<RetryEmailOutcome>.Failure(authorized.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var candidate = await candidates.LockForUpdateAsync(command.CandidateId, cancellationToken);
        if (candidate is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<RetryEmailOutcome>.Failure(Error.NotFound("No such candidate."));
        }

        var previous = await deliveryRepository.LockLatestForCandidateAsync(
            command.CandidateId, cancellationToken);
        if (previous is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<RetryEmailOutcome>.Failure(Error.NotFound("This candidate has no email delivery to retry."));
        }

        if (previous.Status is not EmailStatus.Failed and not EmailStatus.Pending)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<RetryEmailOutcome>.Failure(
                Error.Conflict("The latest email is no longer outstanding."));
        }

        if (previous.ClaimedAt is not null && clock.UtcNow - previous.ClaimedAt.Value < ClaimLease)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<RetryEmailOutcome>.Failure(
                Error.Conflict("The latest email is already being delivered."));
        }

        EmailMessage message;
        try
        {
            message = previous.TemplateName switch
            {
                EmailTemplate.CandidateInvite or EmailTemplate.CandidateReinvite =>
                    await RegenerateInviteAsync(candidate, previous, cancellationToken),
                EmailTemplate.BookingConfirmation =>
                    await RegenerateBookingAsync(candidate, previous, cancellationToken),
                EmailTemplate.SlotCancelledRebookingNeeded =>
                    await RegenerateCancellationAsync(candidate, previous, cancellationToken),
                _ => throw new DomainException("This email template cannot be retried."),
            };
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<RetryEmailOutcome>.Failure(Error.Conflict(ex.Message));
        }

        previous.MarkResolved(clock.UtcNow);
        var replacement = deliveries.StagePending(
            candidate.Id,
            previous.TemplateName,
            previous.InviteId,
            previous.BookingId,
            previous.ConfirmedSlotId,
            after: previous.SentAt);
        deliveries.ClaimForDispatch(replacement);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var status = await deliveries.DispatchClaimedAsync(replacement.Id, message, cancellationToken);
        return Result<RetryEmailOutcome>.Success(
            new RetryEmailOutcome(status.ToString(), replacement.Id));
    }

    private async Task<EmailMessage> RegenerateInviteAsync(
        Domain.Candidates.Candidate candidate,
        EmailLog previous,
        CancellationToken cancellationToken)
    {
        var invite = previous.InviteId is { } inviteId
            ? await invites.LockForUpdateAsync(inviteId, cancellationToken)
            : await invites.LockPendingForCandidateAsync(candidate.Id, cancellationToken);

        if (invite is null || !invite.IsUsableAt(clock.UtcNow) || invite.CandidateId != candidate.Id)
        {
            throw new DomainException("The invite is no longer available for email retry.");
        }

        var options = new List<Domain.Slots.ConfirmedSlot>();
        foreach (var slotId in invite.OfferedSlotIds)
        {
            var slot = await slots.GetAsync(slotId, cancellationToken);
            if (slot is not null)
            {
                options.Add(slot);
            }
        }

        if (options.Count != Invite.RequiredOptionCount)
        {
            throw new DomainException("The invite no longer has three appointment options.");
        }

        var issued = tokens.Issue(invite.Id);
        invite.RotateTokenHash(issued.TokenHash);
        return CandidateEmailComposer.Invite(
            candidate,
            invite.RequiredAppointmentTypeIds,
            options,
            $"{portal.BaseUrl}/book/{issued.Token}",
            previous.TemplateName == EmailTemplate.CandidateReinvite,
            invite.RecoveryOfBookingId.HasValue);
    }

    private async Task<EmailMessage> RegenerateBookingAsync(
        Domain.Candidates.Candidate candidate,
        EmailLog previous,
        CancellationToken cancellationToken)
    {
        var booking = previous.BookingId is { } bookingId
            ? await bookings.LockForUpdateAsync(bookingId, cancellationToken)
            : await bookings.LockActiveForCandidateAsync(candidate.Id, cancellationToken);
        if (booking is null || booking.Status != BookingStatus.Active || booking.CandidateId != candidate.Id)
        {
            throw new DomainException("The booking is no longer available for email retry.");
        }

        var slot = await slots.GetAsync(booking.ConfirmedSlotId, cancellationToken);
        if (slot is null)
        {
            throw new DomainException("The booking slot is no longer available for email retry.");
        }

        var snapshot = await BookingSnapshotAsync(booking.Id, cancellationToken);

        var issued = tokens.Issue(booking.Id);
        booking.RotateManageTokenHash(issued.TokenHash);
        return CandidateEmailComposer.BookingConfirmation(
            candidate,
            snapshot,
            slot,
            $"{portal.BaseUrl}/manage/{issued.Token}",
            portal);
    }

    private async Task<EmailMessage> RegenerateCancellationAsync(
        Domain.Candidates.Candidate candidate,
        EmailLog previous,
        CancellationToken cancellationToken)
    {
        if (candidate.Status is not CandidateStatus.Invited
            and not CandidateStatus.AwaitingAvailability)
        {
            throw new DomainException("The cancellation notice is no longer actionable.");
        }

        if (previous.ConfirmedSlotId is not { } slotId)
        {
            throw new DomainException("The cancelled slot is not available for email retry.");
        }

        var slot = await slots.GetAsync(slotId, cancellationToken);
        if (slot is null || slot.Status != ConfirmedSlotStatus.Cancelled)
        {
            throw new DomainException("The cancelled slot is no longer available for email retry.");
        }

        var booking = previous.BookingId is { } bookingId
            ? await bookings.LockForUpdateAsync(bookingId, cancellationToken)
            : null;
        if (booking is null || booking.CandidateId != candidate.Id)
        {
            throw new DomainException("The booking is no longer available for email retry.");
        }

        var snapshot = await BookingSnapshotAsync(booking.Id, cancellationToken);

        return CandidateEmailComposer.SlotCancelled(candidate, snapshot, slot);
    }

    private async Task<IReadOnlyList<Guid>> BookingSnapshotAsync(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        var snapshot = (await appointments.ListForBookingAsync(bookingId, cancellationToken))
            .Select(appointment => appointment.AppointmentTypeId)
            .ToList();

        if (snapshot.Count == 0)
        {
            throw new DomainException("The booking has no appointments to name.");
        }

        foreach (var appointmentTypeId in snapshot)
        {
            AppointmentTypeIds.EnsureKnown(appointmentTypeId);
        }

        return snapshot;
    }
}
`````

## after — src/EventBooking.Application/Notifications/RetryEmailHandler.cs — 1/1

<!-- vocabulary-file: {"id":84,"oldPath":"src/EventBooking.Application/Notifications/RetryEmailHandler.cs","newPath":"src/EventBooking.Application/Notifications/RetryEmailHandler.cs","beforeSha":"1080adcf12a27c2338c88c63c320caf39497425dc5fc21d9ebf5ba4a5b5d3010","afterSha":"27cccceeefb438ac62995ca196f58c5d474578fc060cdc90db7146203fd6ab0d","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Notifications;

/// <summary>Requests a staff-authorized retry of the attendee's latest failed or pending email.</summary>
/// <param name="StaffUserId">The coordinator requesting the retry.</param>
/// <param name="AttendeeId">The attendee whose latest delivery should be retried.</param>
public sealed record RetryEmailCommand(Guid StaffUserId, Guid AttendeeId);

/// <summary>Reports the durable result of a template-aware email retry.</summary>
/// <param name="DeliveryStatus">The provider outcome of the new attempt.</param>
/// <param name="DeliveryId">The new durable delivery identifier.</param>
public sealed record RetryEmailOutcome(string DeliveryStatus, Guid DeliveryId);

/// <summary>
/// Regenerates the latest delivery from safe persisted context. Token-bearing templates rotate
/// their hash before a fresh raw token is placed in the in-memory provider message.
/// </summary>
/// <param name="access">Authorizes attendee-management access from the caller's complete profile.</param>
/// <param name="attendees">Locks the attendee lifecycle root.</param>
/// <param name="invites">Loads and rotates pending invite hashes.</param>
/// <param name="bookings">Loads and rotates active booking hashes.</param>
/// <param name="events">Loads template event context.</param>
/// <param name="deliveryRepository">Loads the latest delivery server-side.</param>
/// <param name="deliveries">Stages and dispatches the replacement attempt.</param>
/// <param name="tokens">Issues fresh raw tokens and their hashes.</param>
/// <param name="unitOfWork">Owns the replacement transaction.</param>
/// <param name="clock">Supplies claim and expiry times.</param>
/// <param name="portal">Provides attendee portal links and copy settings.</param>
/// <param name="appointments">The appointments.</param>
public sealed class RetryEmailHandler(
    IStaffAccessAuthorizer access,
    IAttendeeRepository attendees,
    IInviteRepository invites,
    IBookingRepository bookings,
    IEventRepository events,
    IBookingAppointmentRepository appointments,
    IEmailDeliveryRepository deliveryRepository,
    EmailDeliveryService deliveries,
    ITokenService tokens,
    IUnitOfWork unitOfWork,
    IClock clock,
    AttendeePortalOptions portal)
{
    private static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(5);

    /// <summary>Retries the newest unresolved delivery and returns its post-commit provider outcome.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<RetryEmailOutcome>> HandleAsync(
        RetryEmailCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageAttendees,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<RetryEmailOutcome>.Failure(authorized.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var attendee = await attendees.LockForUpdateAsync(command.AttendeeId, cancellationToken);
        if (attendee is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<RetryEmailOutcome>.Failure(Error.NotFound("No such attendee."));
        }

        var previous = await deliveryRepository.LockLatestForAttendeeAsync(
            command.AttendeeId, cancellationToken);
        if (previous is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<RetryEmailOutcome>.Failure(Error.NotFound("This attendee has no email delivery to retry."));
        }

        if (previous.Status is not EmailStatus.Failed and not EmailStatus.Pending)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<RetryEmailOutcome>.Failure(
                Error.Conflict("The latest email is no longer outstanding."));
        }

        if (previous.ClaimedAt is not null && clock.UtcNow - previous.ClaimedAt.Value < ClaimLease)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<RetryEmailOutcome>.Failure(
                Error.Conflict("The latest email is already being delivered."));
        }

        EmailMessage message;
        try
        {
            message = previous.TemplateName switch
            {
                EmailTemplate.AttendeeInvite or EmailTemplate.AttendeeReinvite =>
                    await RegenerateInviteAsync(attendee, previous, cancellationToken),
                EmailTemplate.BookingConfirmation =>
                    await RegenerateBookingAsync(attendee, previous, cancellationToken),
                EmailTemplate.EventCancelledRebookingNeeded =>
                    await RegenerateCancellationAsync(attendee, previous, cancellationToken),
                _ => throw new DomainException("This email template cannot be retried."),
            };
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<RetryEmailOutcome>.Failure(Error.Conflict(ex.Message));
        }

        previous.MarkResolved(clock.UtcNow);
        var replacement = deliveries.StagePending(
            attendee.Id,
            previous.TemplateName,
            previous.InviteId,
            previous.BookingId,
            previous.EventId,
            after: previous.SentAt);
        deliveries.ClaimForDispatch(replacement);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var status = await deliveries.DispatchClaimedAsync(replacement.Id, message, cancellationToken);
        return Result<RetryEmailOutcome>.Success(
            new RetryEmailOutcome(status.ToString(), replacement.Id));
    }

    private async Task<EmailMessage> RegenerateInviteAsync(
        Domain.Attendees.Attendee attendee,
        EmailLog previous,
        CancellationToken cancellationToken)
    {
        var invite = previous.InviteId is { } inviteId
            ? await invites.LockForUpdateAsync(inviteId, cancellationToken)
            : await invites.LockPendingForAttendeeAsync(attendee.Id, cancellationToken);

        if (invite is null || !invite.IsUsableAt(clock.UtcNow) || invite.AttendeeId != attendee.Id)
        {
            throw new DomainException("The invite is no longer available for email retry.");
        }

        var options = new List<Domain.Events.Event>();
        foreach (var eventId in invite.OfferedEventIds)
        {
            var eventItem = await events.GetAsync(eventId, cancellationToken);
            if (eventItem is not null)
            {
                options.Add(eventItem);
            }
        }

        if (options.Count != Invite.RequiredOptionCount)
        {
            throw new DomainException("The invite no longer has three appointment options.");
        }

        var issued = tokens.Issue(invite.Id);
        invite.RotateTokenHash(issued.TokenHash);
        return AttendeeEmailComposer.Invite(
            attendee,
            invite.RequiredAppointmentTypeIds,
            options,
            $"{portal.BaseUrl}/book/{issued.Token}",
            previous.TemplateName == EmailTemplate.AttendeeReinvite,
            invite.RecoveryOfBookingId.HasValue);
    }

    private async Task<EmailMessage> RegenerateBookingAsync(
        Domain.Attendees.Attendee attendee,
        EmailLog previous,
        CancellationToken cancellationToken)
    {
        var booking = previous.BookingId is { } bookingId
            ? await bookings.LockForUpdateAsync(bookingId, cancellationToken)
            : await bookings.LockActiveForAttendeeAsync(attendee.Id, cancellationToken);
        if (booking is null || booking.Status != BookingStatus.Active || booking.AttendeeId != attendee.Id)
        {
            throw new DomainException("The booking is no longer available for email retry.");
        }

        var eventItem = await events.GetAsync(booking.EventId, cancellationToken);
        if (eventItem is null)
        {
            throw new DomainException("The booking eventItem is no longer available for email retry.");
        }

        var snapshot = await BookingSnapshotAsync(booking.Id, cancellationToken);

        var issued = tokens.Issue(booking.Id);
        booking.RotateManageTokenHash(issued.TokenHash);
        return AttendeeEmailComposer.BookingConfirmation(
            attendee,
            snapshot,
            eventItem,
            $"{portal.BaseUrl}/manage/{issued.Token}",
            portal);
    }

    private async Task<EmailMessage> RegenerateCancellationAsync(
        Domain.Attendees.Attendee attendee,
        EmailLog previous,
        CancellationToken cancellationToken)
    {
        if (attendee.Status is not AttendeeStatus.Invited
            and not AttendeeStatus.AwaitingAvailability)
        {
            throw new DomainException("The cancellation notice is no longer actionable.");
        }

        if (previous.EventId is not { } eventId)
        {
            throw new DomainException("The cancelled eventItem is not available for email retry.");
        }

        var eventItem = await events.GetAsync(eventId, cancellationToken);
        if (eventItem is null || eventItem.Status != EventStatus.Cancelled)
        {
            throw new DomainException("The cancelled eventItem is no longer available for email retry.");
        }

        var booking = previous.BookingId is { } bookingId
            ? await bookings.LockForUpdateAsync(bookingId, cancellationToken)
            : null;
        if (booking is null || booking.AttendeeId != attendee.Id)
        {
            throw new DomainException("The booking is no longer available for email retry.");
        }

        var snapshot = await BookingSnapshotAsync(booking.Id, cancellationToken);

        return AttendeeEmailComposer.EventCancelled(attendee, snapshot, eventItem);
    }

    private async Task<IReadOnlyList<Guid>> BookingSnapshotAsync(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        var snapshot = (await appointments.ListForBookingAsync(bookingId, cancellationToken))
            .Select(appointment => appointment.AppointmentTypeId)
            .ToList();

        if (snapshot.Count == 0)
        {
            throw new DomainException("The booking has no appointments to name.");
        }

        foreach (var appointmentTypeId in snapshot)
        {
            AppointmentTypeIds.EnsureKnown(appointmentTypeId);
        }

        return snapshot;
    }
}
`````

## before — src/EventBooking.Application/Slots/AcceptProposalHandler.cs — 1/1

<!-- vocabulary-file: {"id":85,"oldPath":"src/EventBooking.Application/Slots/AcceptProposalHandler.cs","newPath":"src/EventBooking.Application/Events/AcceptProposalHandler.cs","beforeSha":"df2bb83d0d8b1d0228e9323ae59a0c9f2d949c8eac14ebb2a762ebe31b732fdd","afterSha":"1114eb286da051e3655baf73c94558fa3e4826bfda94cb73646dff92f2ff1de0","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Slots;

/// <summary>Defines accept proposal command for the current use case.</summary>
/// <param name="ManagerUserId">The manager user id.</param>
/// <param name="ProposalId">The proposal id.</param>
/// <param name="Headcount">The headcount.</param>
public sealed record AcceptProposalCommand(Guid ManagerUserId, Guid ProposalId, int Headcount);

/// <summary>The confirmed slot identifier is null unless this accept was the third.</summary>
/// <param name="ProposalId">The proposal id.</param>
/// <param name="ConfirmedSlotId">The confirmed slot id.</param>
public sealed record AcceptProposalOutcome(Guid ProposalId, Guid? ConfirmedSlotId);

/// <summary>Records an acceptance under the proposal row lock and confirms at most one slot.</summary>
/// <param name="proposals">The proposals.</param>
/// <param name="confirmedSlots">The confirmed slots.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
public sealed class AcceptProposalHandler(
    ISlotProposalRepository proposals,
    IConfirmedSlotRepository confirmedSlots,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit)
{
    /// <summary>Records or revises the appointment-type acceptance while preserving confirmation atomicity.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<AcceptProposalOutcome>> HandleAsync(
        AcceptProposalCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ManagerUserId,
            StaffCapability.ManageSlotNegotiation,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<AcceptProposalOutcome>.Failure(authorized.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var proposal = await proposals.LockForUpdateAsync(command.ProposalId, cancellationToken);
        if (proposal is null)
        {
            return Result<AcceptProposalOutcome>.Failure(Error.NotFound("No such proposal."));
        }

        var appointmentTypeId = authorized.Value.AppointmentTypeId!.Value;

        var previousHeadcount = proposal.Acceptances
            .SingleOrDefault(acceptance => acceptance.AppointmentTypeId == appointmentTypeId)
            ?.Headcount;

        bool changed;
        try
        {
            changed = proposal.Accept(
                appointmentTypeId,
                command.ManagerUserId,
                command.Headcount);
        }
        catch (DomainException ex)
        {
            return Result<AcceptProposalOutcome>.Failure(Error.Validation(ex.Message));
        }

        if (!changed)
        {
            await transaction.CommitAsync(cancellationToken);
            return Result<AcceptProposalOutcome>.Success(
                new AcceptProposalOutcome(proposal.Id, null));
        }

        var appointmentTypeName = AppointmentTypeIdsName(appointmentTypeId);
        var auditDetails = previousHeadcount is null
            ? $"{appointmentTypeName} headcount {command.Headcount}"
            : $"{appointmentTypeName} headcount {previousHeadcount} -> {command.Headcount}";

        audit.Record(
            AuditEntityTypes.SlotProposal,
            proposal.Id,
            AuditAction.AcceptanceRecorded,
            ActorType.Staff,
            command.ManagerUserId.ToString(),
            auditDetails);

        Guid? confirmedSlotId = null;

        if (proposal.IsFullyAccepted)
        {
            var slotId = Guid.NewGuid();

            ConfirmedSlot slot;
            try
            {
                slot = ConfirmedSlot.CreateFrom(slotId, proposal);
            }
            catch (DomainException ex)
            {
                return Result<AcceptProposalOutcome>.Failure(Error.Validation(ex.Message));
            }

            confirmedSlots.Add(slot);
            confirmedSlotId = slotId;

            audit.Record(
                AuditEntityTypes.ConfirmedSlot,
                slotId,
                AuditAction.SlotConfirmed,
                ActorType.Staff,
                command.ManagerUserId.ToString(),
                slot.Window.ToString());
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<AcceptProposalOutcome>.Success(
            new AcceptProposalOutcome(proposal.Id, confirmedSlotId));
    }

    private static string AppointmentTypeIdsName(Guid appointmentTypeId) =>
        Domain.AppointmentTypes.AppointmentTypeIds.NameOf(appointmentTypeId);
}
`````

## after — src/EventBooking.Application/Events/AcceptProposalHandler.cs — 1/1

<!-- vocabulary-file: {"id":85,"oldPath":"src/EventBooking.Application/Slots/AcceptProposalHandler.cs","newPath":"src/EventBooking.Application/Events/AcceptProposalHandler.cs","beforeSha":"df2bb83d0d8b1d0228e9323ae59a0c9f2d949c8eac14ebb2a762ebe31b732fdd","afterSha":"1114eb286da051e3655baf73c94558fa3e4826bfda94cb73646dff92f2ff1de0","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Events;

/// <summary>Defines accept proposal command for the current use case.</summary>
/// <param name="ManagerUserId">The manager user id.</param>
/// <param name="ProposalId">The proposal id.</param>
/// <param name="Headcount">The headcount.</param>
public sealed record AcceptProposalCommand(Guid ManagerUserId, Guid ProposalId, int Headcount);

/// <summary>The event identifier is null unless this accept was the third.</summary>
/// <param name="ProposalId">The proposal id.</param>
/// <param name="EventId">The event id.</param>
public sealed record AcceptProposalOutcome(Guid ProposalId, Guid? EventId);

/// <summary>Records an acceptance under the proposal row lock and confirms at most one eventItem.</summary>
/// <param name="proposals">The proposals.</param>
/// <param name="events">The events.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
public sealed class AcceptProposalHandler(
    IEventProposalRepository proposals,
    IEventRepository events,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit)
{
    /// <summary>Records or revises the appointment-type acceptance while preserving confirmation atomicity.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<AcceptProposalOutcome>> HandleAsync(
        AcceptProposalCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ManagerUserId,
            StaffCapability.ManageEventNegotiation,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<AcceptProposalOutcome>.Failure(authorized.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var proposal = await proposals.LockForUpdateAsync(command.ProposalId, cancellationToken);
        if (proposal is null)
        {
            return Result<AcceptProposalOutcome>.Failure(Error.NotFound("No such proposal."));
        }

        var appointmentTypeId = authorized.Value.AppointmentTypeId!.Value;

        var previousHeadcount = proposal.Acceptances
            .SingleOrDefault(acceptance => acceptance.AppointmentTypeId == appointmentTypeId)
            ?.Headcount;

        bool changed;
        try
        {
            changed = proposal.Accept(
                appointmentTypeId,
                command.ManagerUserId,
                command.Headcount);
        }
        catch (DomainException ex)
        {
            return Result<AcceptProposalOutcome>.Failure(Error.Validation(ex.Message));
        }

        if (!changed)
        {
            await transaction.CommitAsync(cancellationToken);
            return Result<AcceptProposalOutcome>.Success(
                new AcceptProposalOutcome(proposal.Id, null));
        }

        var appointmentTypeName = AppointmentTypeIdsName(appointmentTypeId);
        var auditDetails = previousHeadcount is null
            ? $"{appointmentTypeName} headcount {command.Headcount}"
            : $"{appointmentTypeName} headcount {previousHeadcount} -> {command.Headcount}";

        audit.Record(
            AuditEntityTypes.EventProposal,
            proposal.Id,
            AuditAction.AcceptanceRecorded,
            ActorType.Staff,
            command.ManagerUserId.ToString(),
            auditDetails);

        Guid? eventId = null;

        if (proposal.IsFullyAccepted)
        {
            var createdEventId = Guid.NewGuid();

            Event eventItem;
            try
            {
                eventItem = Event.CreateFrom(createdEventId, proposal);
            }
            catch (DomainException ex)
            {
                return Result<AcceptProposalOutcome>.Failure(Error.Validation(ex.Message));
            }

            events.Add(eventItem);
            eventId = createdEventId;

            audit.Record(
                AuditEntityTypes.Event,
                createdEventId,
                AuditAction.EventConfirmed,
                ActorType.Staff,
                command.ManagerUserId.ToString(),
                eventItem.Window.ToString());
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<AcceptProposalOutcome>.Success(
            new AcceptProposalOutcome(proposal.Id, eventId));
    }

    private static string AppointmentTypeIdsName(Guid appointmentTypeId) =>
        Domain.AppointmentTypes.AppointmentTypeIds.NameOf(appointmentTypeId);
}
`````

## before — src/EventBooking.Application/Slots/AdjustConfirmedSlotCapacityHandler.cs — 1/1

<!-- vocabulary-file: {"id":86,"oldPath":"src/EventBooking.Application/Slots/AdjustConfirmedSlotCapacityHandler.cs","newPath":"src/EventBooking.Application/Events/AdjustEventCapacityHandler.cs","beforeSha":"1a470bc5c174250ddf85d75b008bec37474d2efe66dab051943c4d40bffe10b5","afterSha":"64702f7961dc4241dc95379abb9c31b210809571ce20428de60dc25ef8723774","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Slots;

/// <summary>Defines adjust confirmed slot capacity command for the current use case.</summary>
/// <param name="ManagerUserId">The manager user id.</param>
/// <param name="ConfirmedSlotId">The confirmed slot id.</param>
/// <param name="TotalHeadcount">The total headcount.</param>
public sealed record AdjustConfirmedSlotCapacityCommand(
    Guid ManagerUserId,
    Guid ConfirmedSlotId,
    int TotalHeadcount);

/// <summary>Defines adjust confirmed slot capacity outcome for the current use case.</summary>
/// <param name="ConfirmedSlotId">The confirmed slot id.</param>
/// <param name="TotalHeadcount">The total headcount.</param>
/// <param name="RemainingCapacity">The remaining capacity.</param>
public sealed record AdjustConfirmedSlotCapacityOutcome(
    Guid ConfirmedSlotId,
    int TotalHeadcount,
    int RemainingCapacity);

/// <summary>Defines adjust confirmed slot capacity handler for the current use case.</summary>
/// <param name="slots">The slots.</param>
/// <param name="capacities">The capacities.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
public sealed class AdjustConfirmedSlotCapacityHandler(
    IConfirmedSlotRepository slots,
    ISlotCapacityRepository capacities,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit)
{
    /// <summary>Defines handle async for the current use case.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<AdjustConfirmedSlotCapacityOutcome>> HandleAsync(
        AdjustConfirmedSlotCapacityCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ManagerUserId,
            StaffCapability.ManageSlotNegotiation,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<AdjustConfirmedSlotCapacityOutcome>.Failure(authorized.Error);
        }

        if (command.TotalHeadcount <= 0)
        {
            return Result<AdjustConfirmedSlotCapacityOutcome>.Failure(
                Error.Validation("totalHeadcount must be greater than zero."));
        }

        await using var transaction =
            await unitOfWork.BeginTransactionAsync(cancellationToken);

        var appointmentTypeId = authorized.Value.AppointmentTypeId!.Value;
        var locked = await capacities.LockForUpdateAsync(
            command.ConfirmedSlotId,
            [appointmentTypeId],
            cancellationToken);
        var capacity = locked.SingleOrDefault();

        if (capacity is null)
        {
            return Result<AdjustConfirmedSlotCapacityOutcome>.Failure(
                Error.NotFound("No such slot or capacity for the manager's appointment type."));
        }

        var slot = await slots.GetAsync(command.ConfirmedSlotId, cancellationToken);
        if (slot is null)
        {
            return Result<AdjustConfirmedSlotCapacityOutcome>.Failure(
                Error.NotFound("No such slot."));
        }

        if (slot.Status != ConfirmedSlotStatus.Active)
        {
            return Result<AdjustConfirmedSlotCapacityOutcome>.Failure(
                Error.Conflict("A cancelled slot cannot have its capacity adjusted."));
        }

        if (command.TotalHeadcount < capacity.OccupiedCapacity)
        {
            return Result<AdjustConfirmedSlotCapacityOutcome>.Failure(
                Error.Conflict(
                    "Headcount cannot be lower than the active-booking count of "
                    + $"{capacity.OccupiedCapacity}."));
        }

        var previousTotal = capacity.TotalHeadcount;
        var previousRemaining = capacity.RemainingCapacity;

        bool changed;
        try
        {
            changed = capacity.AdjustTotalHeadcount(command.TotalHeadcount);
        }
        catch (DomainException exception)
        {
            return Result<AdjustConfirmedSlotCapacityOutcome>.Failure(
                Error.Validation(exception.Message));
        }

        if (!changed)
        {
            await transaction.CommitAsync(cancellationToken);
            return Success(slot.Id, capacity);
        }

        audit.Record(
            AuditEntityTypes.ConfirmedSlot,
            slot.Id,
            AuditAction.CapacityAdjusted,
            ActorType.Staff,
            command.ManagerUserId.ToString(),
            $"{AppointmentTypeName(appointmentTypeId)} total "
            + $"{previousTotal} -> {capacity.TotalHeadcount}; remaining "
            + $"{previousRemaining} -> {capacity.RemainingCapacity}");

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Success(slot.Id, capacity);
    }

    private static Result<AdjustConfirmedSlotCapacityOutcome> Success(
        Guid slotId,
        SlotCapacity capacity) =>
        Result<AdjustConfirmedSlotCapacityOutcome>.Success(
            new AdjustConfirmedSlotCapacityOutcome(
                slotId,
                capacity.TotalHeadcount,
                capacity.RemainingCapacity));

    private static string AppointmentTypeName(Guid appointmentTypeId) =>
        Domain.AppointmentTypes.AppointmentTypeIds.NameOf(appointmentTypeId);
}
`````

## after — src/EventBooking.Application/Events/AdjustEventCapacityHandler.cs — 1/1

<!-- vocabulary-file: {"id":86,"oldPath":"src/EventBooking.Application/Slots/AdjustConfirmedSlotCapacityHandler.cs","newPath":"src/EventBooking.Application/Events/AdjustEventCapacityHandler.cs","beforeSha":"1a470bc5c174250ddf85d75b008bec37474d2efe66dab051943c4d40bffe10b5","afterSha":"64702f7961dc4241dc95379abb9c31b210809571ce20428de60dc25ef8723774","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Events;

/// <summary>Defines adjust event capacity command for the current use case.</summary>
/// <param name="ManagerUserId">The manager user id.</param>
/// <param name="EventId">The event id.</param>
/// <param name="TotalHeadcount">The total headcount.</param>
public sealed record AdjustEventCapacityCommand(
    Guid ManagerUserId,
    Guid EventId,
    int TotalHeadcount);

/// <summary>Defines adjust event capacity outcome for the current use case.</summary>
/// <param name="EventId">The event id.</param>
/// <param name="TotalHeadcount">The total headcount.</param>
/// <param name="RemainingCapacity">The remaining capacity.</param>
public sealed record AdjustEventCapacityOutcome(
    Guid EventId,
    int TotalHeadcount,
    int RemainingCapacity);

/// <summary>Defines adjust event capacity handler for the current use case.</summary>
/// <param name="events">The events.</param>
/// <param name="capacities">The capacities.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
public sealed class AdjustEventCapacityHandler(
    IEventRepository events,
    IEventCapacityRepository capacities,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit)
{
    /// <summary>Defines handle async for the current use case.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<AdjustEventCapacityOutcome>> HandleAsync(
        AdjustEventCapacityCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ManagerUserId,
            StaffCapability.ManageEventNegotiation,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(authorized.Error);
        }

        if (command.TotalHeadcount <= 0)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.Validation("totalHeadcount must be greater than zero."));
        }

        await using var transaction =
            await unitOfWork.BeginTransactionAsync(cancellationToken);

        var appointmentTypeId = authorized.Value.AppointmentTypeId!.Value;
        var locked = await capacities.LockForUpdateAsync(
            command.EventId,
            [appointmentTypeId],
            cancellationToken);
        var capacity = locked.SingleOrDefault();

        if (capacity is null)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.NotFound("No such event or capacity for the manager's appointment type."));
        }

        var eventItem = await events.GetAsync(command.EventId, cancellationToken);
        if (eventItem is null)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.NotFound("No such eventItem."));
        }

        if (eventItem.Status != EventStatus.Active)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.Conflict("A cancelled event cannot have its capacity adjusted."));
        }

        if (command.TotalHeadcount < capacity.OccupiedCapacity)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.Conflict(
                    "Headcount cannot be lower than the active-booking count of "
                    + $"{capacity.OccupiedCapacity}."));
        }

        var previousTotal = capacity.TotalHeadcount;
        var previousRemaining = capacity.RemainingCapacity;

        bool changed;
        try
        {
            changed = capacity.AdjustTotalHeadcount(command.TotalHeadcount);
        }
        catch (DomainException exception)
        {
            return Result<AdjustEventCapacityOutcome>.Failure(
                Error.Validation(exception.Message));
        }

        if (!changed)
        {
            await transaction.CommitAsync(cancellationToken);
            return Success(eventItem.Id, capacity);
        }

        audit.Record(
            AuditEntityTypes.Event,
            eventItem.Id,
            AuditAction.CapacityAdjusted,
            ActorType.Staff,
            command.ManagerUserId.ToString(),
            $"{AppointmentTypeName(appointmentTypeId)} total "
            + $"{previousTotal} -> {capacity.TotalHeadcount}; remaining "
            + $"{previousRemaining} -> {capacity.RemainingCapacity}");

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Success(eventItem.Id, capacity);
    }

    private static Result<AdjustEventCapacityOutcome> Success(
        Guid eventId,
        EventCapacity capacity) =>
        Result<AdjustEventCapacityOutcome>.Success(
            new AdjustEventCapacityOutcome(
                eventId,
                capacity.TotalHeadcount,
                capacity.RemainingCapacity));

    private static string AppointmentTypeName(Guid appointmentTypeId) =>
        Domain.AppointmentTypes.AppointmentTypeIds.NameOf(appointmentTypeId);
}
`````
