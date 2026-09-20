# 00a — Port source 10 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## src/EventBooking.Application/Notifications/CandidateEmailComposer.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Notifications/CandidateEmailComposer.cs","encoding":"utf8","sha256":"b5d4b24ce607a0a38a6d1234ff92708142ae16db05e42bfbd7a8a65844cddcec","parts":1,"part":1} -->

`````csharp
using System.Globalization;
using System.Text;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Notifications;

/// <summary>
/// Pure text composition for the 4 candidate emails. No clock, no repository, no mail service —
/// every output is a function of the arguments, so the wording can be asserted in a unit test.
/// </summary>
public static class CandidateEmailComposer
{
    /// <summary>Formats a four-hour slot window using invariant, human-readable wording.</summary>
    /// <param name="window">The window.</param>
    public static string FormatWindow(SlotWindow window) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{0:dddd d MMM yyyy}, {1:HH\\:mm}-{2:HH\\:mm}",
            window.Date,
            window.StartTime,
            window.EndTime);

    /// <summary>Composes an initial, reminder, or recovery Invite from persisted type IDs.</summary>
    /// <param name="candidate">The candidate.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    /// <param name="options">The options.</param>
    /// <param name="bookingUrl">The booking url.</param>
    /// <param name="isReinvite">The is reinvite.</param>
    /// <param name="isRecovery">The is recovery.</param>
    public static EmailMessage Invite(
        Candidate candidate,
        IReadOnlyCollection<Guid> appointmentTypeIds,
        IReadOnlyList<ConfirmedSlot> options,
        string bookingUrl,
        bool isReinvite,
        bool isRecovery)
    {
        var types = FormatTypes(appointmentTypeIds);

        var text = new StringBuilder();
        text.AppendLine($"Hi {candidate.Name},");
        text.AppendLine();

        if (isRecovery)
        {
            text.AppendLine(
                appointmentTypeIds.Count == 1
                    ? "You have a missed appointment, so here are new times to complete it."
                    : "You have missed appointments, so here are new times to complete them.");
        }
        else if (isReinvite)
        {
            text.AppendLine(
                "We have not heard back about your appointments, so here are the latest available times.");
        }
        else
        {
            text.AppendLine("Please choose one of the following times for your appointments.");
        }

        text.AppendLine();
        text.AppendLine($"Appointments: {types}");
        text.AppendLine();

        foreach (var option in options)
        {
            text.AppendLine($"  - {FormatWindow(option.Window)}");
        }

        text.AppendLine();
        text.AppendLine("Choose your time here:");
        text.AppendLine(bookingUrl);

        return new EmailMessage(
            candidate.Id,
            candidate.Email,
            candidate.Name,
            isReinvite ? EmailTemplate.CandidateReinvite : EmailTemplate.CandidateInvite,
            isReinvite
                ? "Reminder: choose a time for your appointments"
                : "Choose a time for your appointments",
            text.ToString(),
            AsHtml(text.ToString(), bookingUrl, "Choose your time"));
    }

    /// <summary>Composes confirmation and names every booked snapshot type.</summary>
    /// <param name="candidate">The candidate.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    /// <param name="slot">The slot.</param>
    /// <param name="manageUrl">The manage url.</param>
    /// <param name="portal">The portal.</param>
    public static EmailMessage BookingConfirmation(
        Candidate candidate,
        IReadOnlyCollection<Guid> appointmentTypeIds,
        ConfirmedSlot slot,
        string manageUrl,
        CandidatePortalOptions portal)
    {
        var types = FormatTypes(appointmentTypeIds);

        var text = new StringBuilder();
        text.AppendLine($"Hi {candidate.Name},");
        text.AppendLine();
        text.AppendLine("Your appointments are confirmed for:");
        text.AppendLine($"  {FormatWindow(slot.Window)}");
        text.AppendLine($"  {portal.HeadOfficeAddress}");
        text.AppendLine();
        text.AppendLine($"Appointments: {types}");
        text.AppendLine();
        text.AppendLine("Need to change or cancel? Use this link:");
        text.AppendLine(manageUrl);
        text.AppendLine();
        text.AppendLine($"Any questions, contact {portal.CoordinatorContact}.");

        return new EmailMessage(
            candidate.Id,
            candidate.Email,
            candidate.Name,
            EmailTemplate.BookingConfirmation,
            "Your appointment is confirmed",
            text.ToString(),
            AsHtml(text.ToString(), manageUrl, "Cancel or reschedule"));
    }

    /// <summary>
    /// Composes a cancellation notice whose recovery wording reflects whether a replacement invite
    /// was actually delivered. Pending or failed replacement delivery receives neutral wording.
    /// </summary>
    /// <param name="candidate">The candidate.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    /// <param name="slot">The slot.</param>
    /// <param name="replacementInviteSent">The replacement invite sent.</param>
    public static EmailMessage SlotCancelled(
        Candidate candidate,
        IReadOnlyCollection<Guid> appointmentTypeIds,
        ConfirmedSlot slot,
        bool replacementInviteSent = false)
    {
        var types = FormatTypes(appointmentTypeIds);

        var text = new StringBuilder();
        text.AppendLine($"Hi {candidate.Name},");
        text.AppendLine();
        text.AppendLine(
            $"We are sorry — your {AppointmentNoun(appointmentTypeIds.Count)} on {FormatWindow(slot.Window)} has had to be cancelled.");
        text.AppendLine();
        text.AppendLine($"Affected appointments: {types}");
        text.AppendLine();
        if (replacementInviteSent)
        {
            text.AppendLine("A new invitation with fresh times is on its way to you.");
        }
        else
        {
            text.AppendLine("The recruitment team will contact you with the next available times.");
        }

        return new EmailMessage(
            candidate.Id,
            candidate.Email,
            candidate.Name,
            EmailTemplate.SlotCancelledRebookingNeeded,
            "Your appointment time has been cancelled",
            text.ToString(),
            AsHtml(text.ToString(), null, null));
    }

    /// <summary>Names snapshot types in deterministic code order for every template.</summary>
    private static string FormatTypes(IReadOnlyCollection<Guid> appointmentTypeIds) =>
        string.Join(
            ", ",
            appointmentTypeIds
                .Select(id => (Code: AppointmentTypeIds.CodeOf(id), Name: AppointmentTypeIds.NameOf(id)))
                .OrderBy(entry => entry.Code, StringComparer.Ordinal)
                .Select(entry => entry.Name));

    /// <summary>Uses one/appointments grammar shared by every template.</summary>
    private static string AppointmentNoun(int count) =>
        count == 1 ? "appointment" : "appointments";

    private static string AsHtml(string text, string? actionUrl, string? actionLabel)
    {
        var body = new StringBuilder();
        body.AppendLine("<html><body style=\"font-family:sans-serif;font-size:15px\">");

        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            body.AppendLine(line.Length == 0 ? "<br />" : $"<p>{System.Net.WebUtility.HtmlEncode(line)}</p>");
        }

        if (actionUrl is not null && actionLabel is not null)
        {
            body.AppendLine($"<p><a href=\"{actionUrl}\">{actionLabel}</a></p>");
        }

        body.AppendLine("</body></html>");
        return body.ToString();
    }
}
`````

## src/EventBooking.Application/Notifications/CandidatePortalOptions.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Notifications/CandidatePortalOptions.cs","encoding":"utf8","sha256":"dafc65ead2446dcede5277990d1189e62f4f27eb42e5771b81823ec10c5d0253","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Application.Notifications;

/// <summary>
/// The handful of deployment-specific strings the candidate-facing emails and pages need. Bound
/// from configuration in Task 55 and injected as a singleton.
/// </summary>
/// <param name="BaseUrl">The base url.</param>
/// <param name="HeadOfficeAddress">The head office address.</param>
/// <param name="CoordinatorContact">The coordinator contact.</param>
public sealed record CandidatePortalOptions(
    string BaseUrl,
    string HeadOfficeAddress,
    string CoordinatorContact);
`````

## src/EventBooking.Application/Notifications/EmailDeliveryService.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Notifications/EmailDeliveryService.cs","encoding":"utf8","sha256":"f66bca5ee4e834fadb79d4864537aaef0ae8a38c904e8465515b0daa317189fe","parts":1,"part":1} -->

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
    /// <param name="candidateId">The candidate id.</param>
    /// <param name="template">The template.</param>
    /// <param name="inviteId">The invite id.</param>
    /// <param name="bookingId">The booking id.</param>
    /// <param name="confirmedSlotId">The confirmed slot id.</param>
    /// <param name="after">The after.</param>
    public EmailLog StagePending(
        Guid candidateId,
        EmailTemplate template,
        Guid? inviteId = null,
        Guid? bookingId = null,
        Guid? confirmedSlotId = null,
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
            candidateId,
            template,
            createdAt,
            inviteId,
            bookingId,
            confirmedSlotId);

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

## src/EventBooking.Application/Notifications/RetryEmailHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Notifications/RetryEmailHandler.cs","encoding":"utf8","sha256":"1080adcf12a27c2338c88c63c320caf39497425dc5fc21d9ebf5ba4a5b5d3010","parts":1,"part":1} -->

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

## src/EventBooking.Application/Properties/AssemblyInfo.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Properties/AssemblyInfo.cs","encoding":"utf8","sha256":"f7bda8a0af1996670e833e2119d5895c0ae43e97680fa7f9e046e7e5cba398e4","parts":1,"part":1} -->

`````csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("EventBooking.Application.Tests")]
`````

## src/EventBooking.Application/Settings/AdminSettingsHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Settings/AdminSettingsHandler.cs","encoding":"utf8","sha256":"094b23d12e690ad265089e94b6e78688437e8b86b561b636a56d95883dfec19d","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Common;

namespace EventBooking.Application.Settings;

/// <summary>Projects one appointment type with the manager assigned to it, when there is one.</summary>
/// <param name="Id">The appointment type identifier.</param>
/// <param name="Code">The canonical appointment-type code.</param>
/// <param name="Name">The canonical appointment-type name.</param>
/// <param name="ManagerUserId">The assigned manager's provider identity, or null when unassigned.</param>
/// <param name="ManagerStaffId">The manager's enterprise staff number; null until an identity is recorded.</param>
/// <param name="ManagerDisplayName">
/// The manager's human-readable name mirrored from the identity provider; null when the identity
/// carries none. Presentation data only.
/// </param>
public sealed record AppointmentTypeView(
    Guid Id,
    string Code,
    string Name,
    Guid? ManagerUserId,
    string? ManagerStaffId = null,
    string? ManagerDisplayName = null);

/// <summary>Defines settings view for the current use case.</summary>
/// <param name="InviteExpiryDays">The invite expiry days.</param>
/// <param name="MaxAutoRetryCount">The max auto retry count.</param>
/// <param name="AppointmentTypes">The appointment types.</param>
public sealed record SettingsView(
    int InviteExpiryDays,
    int MaxAutoRetryCount,
    IReadOnlyList<AppointmentTypeView> AppointmentTypes);

/// <summary>Defines update settings command for the current use case.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="InviteExpiryDays">The invite expiry days.</param>
/// <param name="MaxAutoRetryCount">The max auto retry count.</param>
public sealed record UpdateSettingsCommand(Guid StaffUserId, int InviteExpiryDays, int MaxAutoRetryCount);

/// <summary>Defines admin settings handler for the current use case.</summary>
/// <param name="settings">The settings.</param>
/// <param name="appointmentTypes">The appointment types.</param>
/// <param name="profiles">The profiles.</param>
/// <param name="identities">The identities.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
public sealed class AdminSettingsHandler(
    ISystemSettingsRepository settings,
    IAppointmentTypeRepository appointmentTypes,
    IStaffAccessProfileRepository profiles,
    IStaffIdentityRepository identities,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork)
{
    /// <summary>Defines get async for the current use case.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<SettingsView>> GetAsync(Guid staffUserId, CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            staffUserId,
            StaffCapability.ManageSettings,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<SettingsView>.Failure(authorized.Error);
        }

        var current = await settings.GetAsync(cancellationToken);
        var types = await appointmentTypes.ListAsync(cancellationToken);

        var managerByType = (await profiles.ListAsync(cancellationToken))
            .Where(profile => profile.IsManager && profile.AppointmentTypeId is not null)
            .ToDictionary(
                profile => profile.AppointmentTypeId!.Value,
                profile => profile.StaffUserId);

        // One listing serves both projections; the name never costs an extra query.
        var identityByUserId = (await identities.ListAsync(cancellationToken))
            .ToDictionary(identity => identity.StaffUserId);

        return Result<SettingsView>.Success(new SettingsView(
            current.InviteExpiryDays,
            current.MaxAutoRetryCount,
            types.Select(type =>
            {
                Guid? managerUserId = managerByType.TryGetValue(type.Id, out var found) ? found : null;
                var identity = managerUserId is null
                    ? null
                    : identityByUserId.GetValueOrDefault(managerUserId.Value);
                return new AppointmentTypeView(
                    type.Id,
                    type.Code,
                    type.Name,
                    managerUserId,
                    identity?.StaffId.Value,
                    identity?.DisplayName);
            }).ToList()));
    }

    /// <summary>Defines update async for the current use case.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result> UpdateAsync(UpdateSettingsCommand command, CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageSettings,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result.Failure(authorized.Error);
        }

        var current = await settings.GetAsync(cancellationToken);

        try
        {
            current.Update(command.InviteExpiryDays, command.MaxAutoRetryCount);
        }
        catch (DomainException ex)
        {
            return Result.Failure(Error.Validation(ex.Message));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
`````

## src/EventBooking.Application/Slots/AcceptProposalHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Slots/AcceptProposalHandler.cs","encoding":"utf8","sha256":"df2bb83d0d8b1d0228e9323ae59a0c9f2d949c8eac14ebb2a762ebe31b732fdd","parts":1,"part":1} -->

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

## src/EventBooking.Application/Slots/AdjustConfirmedSlotCapacityHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Slots/AdjustConfirmedSlotCapacityHandler.cs","encoding":"utf8","sha256":"1a470bc5c174250ddf85d75b008bec37474d2efe66dab051943c4d40bffe10b5","parts":1,"part":1} -->

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
