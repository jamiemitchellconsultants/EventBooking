using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
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
                attendee.MarkAwaitingAvailability();
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
                options.Select(o => o.Id),
                mapping,
                retryCount);

            invites.Add(invite);
            attendee.MarkInvited();

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
