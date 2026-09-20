# 02a — Deterministic attendee links and the token version counter, edits 3 (Task 9a)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — src/EventBooking.Application/Invites/InviteIssuer.cs — 1/1

<!-- retirement-file: {"id":8,"file":"src/EventBooking.Application/Invites/InviteIssuer.cs","beforeSha":"e17b2c0047d3f3ba22f461baae0fe14ac69a9e70caeca504534258808b2d7113","afterSha":"3515c7224644a3345514348cdb2a024fa0caf4f6ff00b2d661cc65d8b9b45357","side":"before","part":1,"parts":1} -->

`````csharp
using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Application.Events;
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Invites;

/// <summary>Reports invite creation and the durable delivery state visible to callers.</summary>
/// <param name="Invited">Whether a new pending invite was created.</param>
/// <param name="InviteId">The new invite identifier, when one was created.</param>
/// <param name="EmailSent">Whether the post-commit provider attempt completed successfully.</param>
/// <param name="DeliveryStatus">The durable delivery status, or <c>Unavailable</c> when no invite exists.</param>
/// <param name="DeliveryId">The durable delivery identifier, when one was staged.</param>
public sealed record InviteIssueResult(
    bool Invited,
    Guid? InviteId,
    bool EmailSent,
    string DeliveryStatus = "Unavailable",
    Guid? DeliveryId = null)
{
    internal EmailDispatchPlan? DispatchPlan { get; init; }
}

internal sealed record EmailDispatchPlan(Guid DeliveryId, EmailMessage Message, Action? OnSent = null);

/// <summary>
/// Creates one invite and stages its delivery, or records that there is nothing to offer. Shared by the
/// coordinator trigger (Task 37), the expiry sweep (Task 38) and event cancellation (Task 42).
/// Never saves or calls a provider — the caller owns the unit of work and dispatches only after commit.
/// </summary>
/// <param name="invites">The invites.</param>
/// <param name="groups">The groups.</param>
/// <param name="eventFinder">The event finder.</param>
/// <param name="settings">The settings.</param>
/// <param name="tokens">The tokens.</param>
/// <param name="deliveries">The deliveries.</param>
/// <param name="audit">The audit.</param>
/// <param name="clock">The clock.</param>
/// <param name="portal">The portal.</param>
public sealed class InviteIssuer(
    IInviteRepository invites,
    IAttendeeGroupRepository groups,
    EligibleEventFinder eventFinder,
    ISystemSettingsRepository settings,
    ITokenService tokens,
    EmailDeliveryService deliveries,
    IAuditLogger audit,
    IClock clock,
    AttendeePortalOptions portal)
{
    /// <summary>Issues an initial Invite from a locked, validated Attendee requirement set.</summary>
    /// <param name="attendee">The attendee whose lifecycle is already locked by the caller.</param>
    /// <param name="retryCount">The automated retry number to persist on the new invite.</param>
    /// <param name="actorType">The actor recorded for invite creation.</param>
    /// <param name="actorId">The actor identifier, when a staff identity caused the change.</param>
    /// <param name="isReinvite">Whether the reminder template should be used.</param>
    /// <param name="cancellationToken">Cancels repository and event reads.</param>
    /// <returns>A pending delivery plan the caller dispatches after commit.</returns>
    public async Task<Result<InviteIssueResult>> IssueInitialAsync(
        Attendee attendee,
        int retryCount,
        ActorType actorType,
        string? actorId,
        bool isReinvite,
        CancellationToken cancellationToken)
    {
        var group = await groups.GetAsync(attendee.AttendeeGroupId, cancellationToken);
        var mapping = group?.RequiredAppointmentTypeIds
            .Order()
            .ToList();
        var current = attendee.RequiredAppointmentTypeIds
            .Order()
            .ToList();
        if (group is null || !group.IsActive || mapping!.Count == 0 || !mapping.SequenceEqual(current))
        {
            return Result<InviteIssueResult>.Failure(Error.AttendeeRequirementSnapshotMismatch(
                "The attendee requirements do not match their attendee group."));
        }

        var pending = await invites.GetPendingForAttendeeAsync(attendee.Id, cancellationToken);
        if (pending?.Status == Domain.Invites.InviteStatus.Pending)
        {
            pending.MarkSuperseded();
        }

        InviteIssueResult issued;
        try
        {
            var options = await eventFinder.FindAsync(
                mapping,
                Invite.RequiredOptionCount,
                [],
                cancellationToken);

            if (options.Count < Invite.RequiredOptionCount)
            {
                // FR-5.4 parks a not-yet-invited attendee on AwaitingAvailability. FR-5.7 says a
                // failed automatic re-issue ends at NoResponseNeedsFollowUp instead, and the
                // closed status table is what tells the two apart.
                if (Attendee.IsLegalTransition(attendee.Status, AttendeeStatus.AwaitingAvailability))
                {
                    attendee.MarkAwaitingAvailability(clock.UtcNow);
                }
                else
                {
                    attendee.MarkNoResponse(clock.UtcNow);
                }

                return Result<InviteIssueResult>.Success(new InviteIssueResult(false, null, false));
            }

            var configuration = await settings.GetAsync(cancellationToken);

            var inviteId = Guid.NewGuid();
            var token = tokens.Issue(inviteId);

            var invite = Invite.CreateInitial(
                inviteId,
                attendee.Id,
                token.TokenHash,
                clock.UtcNow.AddDays(configuration.InviteExpiryDays),
                // The Coordinator cannot choose locations until Task 12 puts them on the command,
                // so every invite is restricted to the transitional site.
                [TransitionalLocation.Id],
                options.Select(o => o.Id),
                mapping,
                retryCount);

            invites.Add(invite);
            attendee.MarkInvited(clock.UtcNow);

            audit.Record(
                AuditEntityTypes.Invite,
                inviteId,
                AuditAction.InviteCreated,
                actorType,
                actorId,
                $"retry {retryCount}");

            var message = AttendeeEmailComposer.Invite(
                attendee,
                mapping,
                options,
                $"{portal.BaseUrl}/book/{token.Token}",
                isReinvite,
                isRecovery: false);

            var delivery = deliveries.StagePending(attendee.Id, message.Template, inviteId: inviteId);
            deliveries.ClaimForDispatch(delivery);
            var plan = new EmailDispatchPlan(
                delivery.Id,
                message,
                () => audit.Record(
                    AuditEntityTypes.Invite,
                    inviteId,
                    AuditAction.InviteSent,
                    actorType,
                    actorId,
                    $"invite {inviteId}"));

            issued = new InviteIssueResult(true, inviteId, false, EmailStatus.Pending.ToString(), delivery.Id)
            {
                DispatchPlan = plan,
            };
        }
        catch (DomainException ex)
        {
            return Result<InviteIssueResult>.Failure(Error.Validation(ex.Message));
        }

        return Result<InviteIssueResult>.Success(issued);
    }

    /// <summary>Issues a recovery Invite for already-selected no-show types from locked journey state.</summary>
    /// <param name="attendee">The attendee whose lifecycle is already locked by the caller.</param>
    /// <param name="rootBookingId">The original journey-root Booking the recovery belongs to.</param>
    /// <param name="selectedTypeIds">The recoverable snapshot, already revalidated under lock.</param>
    /// <param name="options">Exactly three future events with capacity for every selected type.</param>
    /// <param name="actorType">The actor recorded for invite creation.</param>
    /// <param name="actorId">The actor identifier, when a staff identity caused the change.</param>
    /// <param name="cancellationToken">Cancels repository reads.</param>
    /// <returns>A pending delivery plan the caller dispatches after commit.</returns>
    public async Task<Result<InviteIssueResult>> IssueRecoveryAsync(
        Attendee attendee,
        Guid rootBookingId,
        IReadOnlyList<Guid> selectedTypeIds,
        IReadOnlyList<Event> options,
        ActorType actorType,
        string? actorId,
        CancellationToken cancellationToken)
    {
        if (options.Count != Invite.RequiredOptionCount)
        {
            return Result<InviteIssueResult>.Failure(Error.Validation(
                "A recovery invite must offer exactly three event options."));
        }

        InviteIssueResult issued;
        try
        {
            var configuration = await settings.GetAsync(cancellationToken);

            var inviteId = Guid.NewGuid();
            var token = tokens.Issue(inviteId);

            var invite = Invite.CreateRecovery(
                inviteId,
                attendee.Id,
                rootBookingId,
                token.TokenHash,
                clock.UtcNow.AddDays(configuration.InviteExpiryDays),
                TransitionalLocation.Id,
                null,
                options.Select(o => o.Id),
                selectedTypeIds);

            invites.Add(invite);

            var codes = selectedTypeIds
                .Select(AppointmentTypeIds.CodeOf)
                .Order(StringComparer.Ordinal)
                .ToList();
            audit.Record(
                AuditEntityTypes.Invite,
                inviteId,
                AuditAction.RecoveryInviteCreated,
                actorType,
                actorId,
                JsonSerializer.Serialize(
                    new RecoveryInviteAudit(rootBookingId, codes),
                    RecoveryAuditJson));

            var message = AttendeeEmailComposer.Invite(
                attendee,
                selectedTypeIds,
                options,
                $"{portal.BaseUrl}/book/{token.Token}",
                isReinvite: false,
                isRecovery: true);

            var delivery = deliveries.StagePending(attendee.Id, message.Template, inviteId: inviteId);
            deliveries.ClaimForDispatch(delivery);
            var plan = new EmailDispatchPlan(
                delivery.Id,
                message,
                () => audit.Record(
                    AuditEntityTypes.Invite,
                    inviteId,
                    AuditAction.InviteSent,
                    actorType,
                    actorId,
                    $"invite {inviteId}"));

            issued = new InviteIssueResult(true, inviteId, false, EmailStatus.Pending.ToString(), delivery.Id)
            {
                DispatchPlan = plan,
            };
        }
        catch (DomainException ex)
        {
            return Result<InviteIssueResult>.Failure(Error.Validation(ex.Message));
        }

        return Result<InviteIssueResult>.Success(issued);
    }

    private static readonly JsonSerializerOptions RecoveryAuditJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed record RecoveryInviteAudit(Guid RootBookingId, IReadOnlyList<string> RequirementCodes);
}
`````

## after — src/EventBooking.Application/Invites/InviteIssuer.cs — 1/1

<!-- retirement-file: {"id":8,"file":"src/EventBooking.Application/Invites/InviteIssuer.cs","beforeSha":"e17b2c0047d3f3ba22f461baae0fe14ac69a9e70caeca504534258808b2d7113","afterSha":"3515c7224644a3345514348cdb2a024fa0caf4f6ff00b2d661cc65d8b9b45357","side":"after","part":1,"parts":1} -->

`````csharp
using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Application.Events;
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Invites;

/// <summary>Reports invite creation and the durable delivery state visible to callers.</summary>
/// <param name="Invited">Whether a new pending invite was created.</param>
/// <param name="InviteId">The new invite identifier, when one was created.</param>
/// <param name="EmailSent">Whether the post-commit provider attempt completed successfully.</param>
/// <param name="DeliveryStatus">The durable delivery status, or <c>Unavailable</c> when no invite exists.</param>
/// <param name="DeliveryId">The durable delivery identifier, when one was staged.</param>
public sealed record InviteIssueResult(
    bool Invited,
    Guid? InviteId,
    bool EmailSent,
    string DeliveryStatus = "Unavailable",
    Guid? DeliveryId = null)
{
    internal EmailDispatchPlan? DispatchPlan { get; init; }
}

internal sealed record EmailDispatchPlan(Guid DeliveryId, EmailMessage Message, Action? OnSent = null);

/// <summary>
/// Creates one invite and stages its delivery, or records that there is nothing to offer. Shared by the
/// coordinator trigger (Task 37), the expiry sweep (Task 38) and event cancellation (Task 42).
/// Never saves or calls a provider — the caller owns the unit of work and dispatches only after commit.
/// </summary>
/// <param name="invites">The invites.</param>
/// <param name="groups">The groups.</param>
/// <param name="eventFinder">The event finder.</param>
/// <param name="settings">The settings.</param>
/// <param name="tokens">The tokens.</param>
/// <param name="deliveries">The deliveries.</param>
/// <param name="audit">The audit.</param>
/// <param name="clock">The clock.</param>
/// <param name="portal">The portal.</param>
public sealed class InviteIssuer(
    IInviteRepository invites,
    IAttendeeGroupRepository groups,
    EligibleEventFinder eventFinder,
    ISystemSettingsRepository settings,
    ITokenService tokens,
    EmailDeliveryService deliveries,
    IAuditLogger audit,
    IClock clock,
    AttendeePortalOptions portal)
{
    /// <summary>Issues an initial Invite from a locked, validated Attendee requirement set.</summary>
    /// <param name="attendee">The attendee whose lifecycle is already locked by the caller.</param>
    /// <param name="retryCount">The automated retry number to persist on the new invite.</param>
    /// <param name="actorType">The actor recorded for invite creation.</param>
    /// <param name="actorId">The actor identifier, when a staff identity caused the change.</param>
    /// <param name="isReinvite">Whether the reminder template should be used.</param>
    /// <param name="cancellationToken">Cancels repository and event reads.</param>
    /// <returns>A pending delivery plan the caller dispatches after commit.</returns>
    public async Task<Result<InviteIssueResult>> IssueInitialAsync(
        Attendee attendee,
        int retryCount,
        ActorType actorType,
        string? actorId,
        bool isReinvite,
        CancellationToken cancellationToken)
    {
        var group = await groups.GetAsync(attendee.AttendeeGroupId, cancellationToken);
        var mapping = group?.RequiredAppointmentTypeIds
            .Order()
            .ToList();
        var current = attendee.RequiredAppointmentTypeIds
            .Order()
            .ToList();
        if (group is null || !group.IsActive || mapping!.Count == 0 || !mapping.SequenceEqual(current))
        {
            return Result<InviteIssueResult>.Failure(Error.AttendeeRequirementSnapshotMismatch(
                "The attendee requirements do not match their attendee group."));
        }

        var pending = await invites.GetPendingForAttendeeAsync(attendee.Id, cancellationToken);
        if (pending?.Status == Domain.Invites.InviteStatus.Pending)
        {
            pending.MarkSuperseded();
        }

        InviteIssueResult issued;
        try
        {
            var options = await eventFinder.FindAsync(
                mapping,
                Invite.RequiredOptionCount,
                [],
                cancellationToken);

            if (options.Count < Invite.RequiredOptionCount)
            {
                // FR-5.4 parks a not-yet-invited attendee on AwaitingAvailability. FR-5.7 says a
                // failed automatic re-issue ends at NoResponseNeedsFollowUp instead, and the
                // closed status table is what tells the two apart.
                if (Attendee.IsLegalTransition(attendee.Status, AttendeeStatus.AwaitingAvailability))
                {
                    attendee.MarkAwaitingAvailability(clock.UtcNow);
                }
                else
                {
                    attendee.MarkNoResponse(clock.UtcNow);
                }

                return Result<InviteIssueResult>.Success(new InviteIssueResult(false, null, false));
            }

            var configuration = await settings.GetAsync(cancellationToken);

            var inviteId = Guid.NewGuid();
            var token = tokens.Issue(TokenPurpose.Book, inviteId, Invite.InitialTokenVersion);

            var invite = Invite.CreateInitial(
                inviteId,
                attendee.Id,
                clock.UtcNow.AddDays(configuration.InviteExpiryDays),
                // The Coordinator cannot choose locations until Task 12 puts them on the command,
                // so every invite is restricted to the transitional site.
                [TransitionalLocation.Id],
                options.Select(o => o.Id),
                mapping,
                retryCount);

            invites.Add(invite);
            attendee.MarkInvited(clock.UtcNow);

            audit.Record(
                AuditEntityTypes.Invite,
                inviteId,
                AuditAction.InviteCreated,
                actorType,
                actorId,
                $"retry {retryCount}");

            var message = AttendeeEmailComposer.Invite(
                attendee,
                mapping,
                options,
                $"{portal.BaseUrl}/book/{token}",
                isReinvite,
                isRecovery: false);

            var delivery = deliveries.StagePending(attendee.Id, message.Template, inviteId: inviteId);
            deliveries.ClaimForDispatch(delivery);
            var plan = new EmailDispatchPlan(
                delivery.Id,
                message,
                () => audit.Record(
                    AuditEntityTypes.Invite,
                    inviteId,
                    AuditAction.InviteSent,
                    actorType,
                    actorId,
                    $"invite {inviteId}"));

            issued = new InviteIssueResult(true, inviteId, false, EmailStatus.Pending.ToString(), delivery.Id)
            {
                DispatchPlan = plan,
            };
        }
        catch (DomainException ex)
        {
            return Result<InviteIssueResult>.Failure(Error.Validation(ex.Message));
        }

        return Result<InviteIssueResult>.Success(issued);
    }

    /// <summary>Issues a recovery Invite for already-selected no-show types from locked journey state.</summary>
    /// <param name="attendee">The attendee whose lifecycle is already locked by the caller.</param>
    /// <param name="rootBookingId">The original journey-root Booking the recovery belongs to.</param>
    /// <param name="selectedTypeIds">The recoverable snapshot, already revalidated under lock.</param>
    /// <param name="options">Exactly three future events with capacity for every selected type.</param>
    /// <param name="actorType">The actor recorded for invite creation.</param>
    /// <param name="actorId">The actor identifier, when a staff identity caused the change.</param>
    /// <param name="cancellationToken">Cancels repository reads.</param>
    /// <returns>A pending delivery plan the caller dispatches after commit.</returns>
    public async Task<Result<InviteIssueResult>> IssueRecoveryAsync(
        Attendee attendee,
        Guid rootBookingId,
        IReadOnlyList<Guid> selectedTypeIds,
        IReadOnlyList<Event> options,
        ActorType actorType,
        string? actorId,
        CancellationToken cancellationToken)
    {
        if (options.Count != Invite.RequiredOptionCount)
        {
            return Result<InviteIssueResult>.Failure(Error.Validation(
                "A recovery invite must offer exactly three event options."));
        }

        InviteIssueResult issued;
        try
        {
            var configuration = await settings.GetAsync(cancellationToken);

            var inviteId = Guid.NewGuid();
            var token = tokens.Issue(TokenPurpose.Book, inviteId, Invite.InitialTokenVersion);

            var invite = Invite.CreateRecovery(
                inviteId,
                attendee.Id,
                rootBookingId,
                clock.UtcNow.AddDays(configuration.InviteExpiryDays),
                TransitionalLocation.Id,
                null,
                options.Select(o => o.Id),
                selectedTypeIds);

            invites.Add(invite);

            var codes = selectedTypeIds
                .Select(AppointmentTypeIds.CodeOf)
                .Order(StringComparer.Ordinal)
                .ToList();
            audit.Record(
                AuditEntityTypes.Invite,
                inviteId,
                AuditAction.RecoveryInviteCreated,
                actorType,
                actorId,
                JsonSerializer.Serialize(
                    new RecoveryInviteAudit(rootBookingId, codes),
                    RecoveryAuditJson));

            var message = AttendeeEmailComposer.Invite(
                attendee,
                selectedTypeIds,
                options,
                $"{portal.BaseUrl}/book/{token}",
                isReinvite: false,
                isRecovery: true);

            var delivery = deliveries.StagePending(attendee.Id, message.Template, inviteId: inviteId);
            deliveries.ClaimForDispatch(delivery);
            var plan = new EmailDispatchPlan(
                delivery.Id,
                message,
                () => audit.Record(
                    AuditEntityTypes.Invite,
                    inviteId,
                    AuditAction.InviteSent,
                    actorType,
                    actorId,
                    $"invite {inviteId}"));

            issued = new InviteIssueResult(true, inviteId, false, EmailStatus.Pending.ToString(), delivery.Id)
            {
                DispatchPlan = plan,
            };
        }
        catch (DomainException ex)
        {
            return Result<InviteIssueResult>.Failure(Error.Validation(ex.Message));
        }

        return Result<InviteIssueResult>.Success(issued);
    }

    private static readonly JsonSerializerOptions RecoveryAuditJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed record RecoveryInviteAudit(Guid RootBookingId, IReadOnlyList<string> RequirementCodes);
}
`````

## before — src/EventBooking.Application/Notifications/RetryEmailHandler.cs — 1/1

<!-- retirement-file: {"id":9,"file":"src/EventBooking.Application/Notifications/RetryEmailHandler.cs","beforeSha":"27cccceeefb438ac62995ca196f58c5d474578fc060cdc90db7146203fd6ab0d","afterSha":"e86bdab4dbeef9bb74f49b7218aec2e180a0aaa4ece11d3703fb6c23a82c39f6","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Application/Notifications/RetryEmailHandler.cs — 1/1

<!-- retirement-file: {"id":9,"file":"src/EventBooking.Application/Notifications/RetryEmailHandler.cs","beforeSha":"27cccceeefb438ac62995ca196f58c5d474578fc060cdc90db7146203fd6ab0d","afterSha":"e86bdab4dbeef9bb74f49b7218aec2e180a0aaa4ece11d3703fb6c23a82c39f6","side":"after","part":1,"parts":1} -->

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

        // A retry reuses the current link rather than minting a new one: the token is derived
        // from the invite and its version, so it is reproducible without being stored (design 06).
        var issued = tokens.Issue(TokenPurpose.Book, invite.Id, invite.TokenVersion);
        return AttendeeEmailComposer.Invite(
            attendee,
            invite.RequiredAppointmentTypeIds,
            options,
            $"{portal.BaseUrl}/book/{issued}",
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

        var issued = tokens.Issue(TokenPurpose.Manage, booking.Id, booking.ManageTokenVersion);
        return AttendeeEmailComposer.BookingConfirmation(
            attendee,
            snapshot,
            eventItem,
            $"{portal.BaseUrl}/manage/{issued}",
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

## before — src/EventBooking.Domain/Bookings/Booking.cs — 1/1

<!-- retirement-file: {"id":10,"file":"src/EventBooking.Domain/Bookings/Booking.cs","beforeSha":"54e28a7e769f7397f2e81ca36d2c5c228a65ec72d9ae9db7e0b8a424d3130714","afterSha":"37d6564c1d12dec06d89d75a1b8d4fc5747def51b0868b2d79ab7d8e9ebd9941","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Bookings;

/// <summary>Defines booking for the current use case.</summary>
public sealed class Booking
{
    private Booking()
    {
        // Required by the persistence layer's constructor binding.
        ManageTokenHash = string.Empty;
    }

    /// <summary>Defines id for the current use case.</summary>
    public Guid Id { get; private set; }

    /// <summary>Defines attendee id for the current use case.</summary>
    public Guid AttendeeId { get; private set; }

    /// <summary>Defines event id for the current use case.</summary>
    public Guid EventId { get; private set; }

    /// <summary>Defines invite id for the current use case.</summary>
    public Guid InviteId { get; private set; }

    /// <summary>Defines created at for the current use case.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Defines status for the current use case.</summary>
    public BookingStatus Status { get; private set; } = BookingStatus.Active;

    /// <summary>Gets the original active Booking ID, or null for the journey root.</summary>
    public Guid? RecoveryOfBookingId { get; private set; }

    /// <summary>Gets whether this Booking is the original journey root.</summary>
    public bool IsOriginal => RecoveryOfBookingId is null;

    /// <summary>Hash of the single-use token behind the cancel/reschedule link.</summary>
    public string ManageTokenHash { get; private set; }

    /// <summary>Replaces the persisted management-link hash after issuing a fresh raw token.</summary>
    /// <param name="manageTokenHash">The manage token hash.</param>
    public void RotateManageTokenHash(string? manageTokenHash)
    {
        Guard.Against(Status != BookingStatus.Active, "Only an active booking token can be rotated.");
        ManageTokenHash = Guard.NotBlank(manageTokenHash, "manageTokenHash");
    }

    /// <summary>Defines create for the current use case.</summary>
    /// <param name="id">The id.</param>
    /// <param name="invite">The invite.</param>
    /// <param name="eventId">The event id.</param>
    /// <param name="manageTokenHash">The manage token hash.</param>
    /// <param name="createdAt">The created at.</param>
    public static Booking Create(
        Guid id,
        Invite invite,
        Guid eventId,
        string? manageTokenHash,
        DateTimeOffset createdAt)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(invite is null, "invite must be supplied.");
        Guard.Against(invite!.Status != InviteStatus.Pending, "This invite can no longer be used.");
        Guard.Against(
            !invite.Offers(eventId),
            "The chosen eventItem is not one of this invite's options.");

        return new Booking
        {
            Id = id,
            AttendeeId = invite.AttendeeId,
            EventId = eventId,
            InviteId = invite.Id,
            CreatedAt = createdAt,
            Status = BookingStatus.Active,
            ManageTokenHash = Guard.NotBlank(manageTokenHash, "manageTokenHash"),
        };
    }

    /// <summary>Defines cancel for the current use case.</summary>
    public void Cancel()
    {
        Guard.Against(Status == BookingStatus.Cancelled, "This booking has already been cancelled.");
        Status = BookingStatus.Cancelled;
    }

    /// <summary>Creates a recovery Booking directly linked to the original Booking.</summary>
    /// <param name="id">The stable recovery booking identifier.</param>
    /// <param name="recoveryInvite">The pending recovery invite issued for the original Booking.</param>
    /// <param name="originalBooking">The active original journey root being recovered.</param>
    /// <param name="eventId">The recovery event offered by the invite.</param>
    /// <param name="manageTokenHash">The management-link hash for the recovery booking.</param>
    /// <param name="createdAt">When the recovery booking is created.</param>
    /// <returns>An active recovery Booking pointing at the original root.</returns>
    public static Booking CreateRecovery(
        Guid id,
        Invite recoveryInvite,
        Booking originalBooking,
        Guid eventId,
        string? manageTokenHash,
        DateTimeOffset createdAt)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(recoveryInvite is null, "recoveryInvite must be supplied.");
        Guard.Against(originalBooking is null, "originalBooking must be supplied.");
        Guard.Against(!originalBooking!.IsOriginal, "A recovery booking cannot point at another recovery.");
        Guard.Against(
            originalBooking.Status != BookingStatus.Active,
            "A recovery booking requires an active original booking.");
        Guard.Against(
            recoveryInvite!.RecoveryOfBookingId != originalBooking.Id,
            "The recovery invite must point at the supplied original booking.");
        Guard.Against(
            recoveryInvite.AttendeeId != originalBooking.AttendeeId,
            "The recovery invite must belong to the original booking attendee.");
        Guard.Against(
            recoveryInvite.Status != InviteStatus.Pending,
            "This invite can no longer be used.");
        Guard.Against(
            !recoveryInvite.Offers(eventId),
            "The chosen eventItem is not one of this invite's options.");

        return new Booking
        {
            Id = id,
            AttendeeId = originalBooking.AttendeeId,
            EventId = eventId,
            InviteId = recoveryInvite.Id,
            CreatedAt = createdAt,
            Status = BookingStatus.Active,
            RecoveryOfBookingId = originalBooking.Id,
            ManageTokenHash = Guard.NotBlank(manageTokenHash, "manageTokenHash"),
        };
    }

    /// <summary>Concludes an Active recovery Booking after all of its appointments are terminal.</summary>
    public void Conclude()
    {
        Guard.Against(IsOriginal, "Only a recovery booking can conclude.");
        Guard.Against(Status != BookingStatus.Active, "Only an active recovery booking can conclude.");
        Status = BookingStatus.Concluded;
    }

    /// <summary>Reopens a Concluded recovery Booking after an allowed outcome correction.</summary>
    public void Reopen()
    {
        Guard.Against(IsOriginal, "Only a recovery booking can reopen.");
        Guard.Against(Status != BookingStatus.Concluded, "Only a concluded recovery booking can reopen.");
        Status = BookingStatus.Active;
    }
}
`````
