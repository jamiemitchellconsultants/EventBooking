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
