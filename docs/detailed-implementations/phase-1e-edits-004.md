# 01e — Location-restricted invites and closed attendee transitions, edits 4 (Task 8)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — src/EventBooking.Application/Invites/ExpireInvitesHandler.cs — 1/1

<!-- retirement-file: {"id":7,"file":"src/EventBooking.Application/Invites/ExpireInvitesHandler.cs","beforeSha":"6d21162fed9b40e51e20b306a3c04151382732c81da74bd4a29a3794ba4ca815","afterSha":"99c512905ae8000f19bbd0135d528a9f908693b77df20d3f5a927c835659d6b8","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Audit;

namespace EventBooking.Application.Invites;

/// <summary>Defines invite sweep summary for the current use case.</summary>
/// <param name="Expired">The expired.</param>
/// <param name="ReIssued">The re issued.</param>
/// <param name="FlaggedForFollowUp">The flagged for follow up.</param>
public sealed record InviteSweepSummary(int Expired, int ReIssued, int FlaggedForFollowUp);

/// <summary>
/// The scheduled half of the invite engine. Expiry is never triggered by a attendee opening a
/// link — an invite nobody ever opens has to expire too.
/// </summary>
/// <param name="deliveries">Dispatches staged re-invites after each commit.</param>
/// <param name="invites">The invites.</param>
/// <param name="attendees">The attendees.</param>
/// <param name="settings">The settings.</param>
/// <param name="issuer">The issuer.</param>
/// <param name="audit">The audit.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="clock">The clock.</param>
public sealed class ExpireInvitesHandler(
    IInviteRepository invites,
    IAttendeeRepository attendees,
    ISystemSettingsRepository settings,
    InviteIssuer issuer,
    EmailDeliveryService deliveries,
    IAuditLogger audit,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    /// <summary>Expires and optionally replaces each due invite under its attendee lifecycle lock.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<InviteSweepSummary> HandleAsync(CancellationToken cancellationToken)
    {
        var configuration = await settings.GetAsync(cancellationToken);
        var due = await invites.ListPendingExpiredAsync(clock.UtcNow, cancellationToken);

        var expired = 0;
        var reIssued = 0;
        var flagged = 0;

        foreach (var dueInvite in due)
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

            // The attendee is the lifecycle root. The due-list row is only a work hint and is
            // re-read under the attendee and invite locks before any transition is made.
            var attendee = await attendees.LockForUpdateAsync(dueInvite.AttendeeId, cancellationToken);
            if (attendee is null)
            {
                continue;
            }

            var invite = await invites.LockForUpdateAsync(dueInvite.Id, cancellationToken);
            if (invite is null || !invite.IsUsableAt(clock.UtcNow) && invite.Status != Domain.Invites.InviteStatus.Pending)
            {
                await transaction.CommitAsync(cancellationToken);
                continue;
            }

            if (invite.RecoveryOfBookingId.HasValue)
            {
                if (invite.ExpiresAt > clock.UtcNow)
                {
                    await transaction.CommitAsync(cancellationToken);
                    continue;
                }

                invite.MarkExpired();
                expired++;

                audit.Record(
                    AuditEntityTypes.Invite,
                    invite.Id,
                    AuditAction.InviteExpired,
                    ActorType.System,
                    null,
                    $"retry {invite.RetryCount}");

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

                continue;
            }

            if (invite.ExpiresAt > clock.UtcNow || attendee.Status == Domain.Attendees.AttendeeStatus.Booked)
            {
                await transaction.CommitAsync(cancellationToken);
                continue;
            }

            invite.MarkExpired();
            expired++;

            audit.Record(
                AuditEntityTypes.Invite,
                invite.Id,
                AuditAction.InviteExpired,
                ActorType.System,
                null,
                $"retry {invite.RetryCount}");

            if (invite.RetryCount >= configuration.MaxAutoRetryCount)
            {
                attendee.MarkNoResponse();
                flagged++;
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                continue;
            }

            var issueResult = await issuer.IssueInitialAsync(
                attendee,
                invite.RetryCount + 1,
                ActorType.System,
                null,
                isReinvite: true,
                cancellationToken);

            if (issueResult.IsFailure)
            {
                if (attendee.Status == Domain.Attendees.AttendeeStatus.Invited)
                {
                    attendee.MarkNoResponse();
                    flagged++;
                    audit.Record(
                        AuditEntityTypes.Invite,
                        invite.Id,
                        AuditAction.InviteExpired,
                        ActorType.System,
                        null,
                        "re-issue failed");
                }

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

                continue;
            }

            var issued = issueResult.Value;
            if (issued.Invited)
            {
                reIssued++;
            }

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

            if (issued.DispatchPlan is { } plan)
            {
                await deliveries.DispatchClaimedAsync(
                    plan.DeliveryId, plan.Message, cancellationToken, plan.OnSent);
            }
        }

        return new InviteSweepSummary(expired, reIssued, flagged);
    }
}
`````

## after — src/EventBooking.Application/Invites/ExpireInvitesHandler.cs — 1/1

<!-- retirement-file: {"id":7,"file":"src/EventBooking.Application/Invites/ExpireInvitesHandler.cs","beforeSha":"6d21162fed9b40e51e20b306a3c04151382732c81da74bd4a29a3794ba4ca815","afterSha":"99c512905ae8000f19bbd0135d528a9f908693b77df20d3f5a927c835659d6b8","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Audit;

namespace EventBooking.Application.Invites;

/// <summary>Defines invite sweep summary for the current use case.</summary>
/// <param name="Expired">The expired.</param>
/// <param name="ReIssued">The re issued.</param>
/// <param name="FlaggedForFollowUp">The flagged for follow up.</param>
public sealed record InviteSweepSummary(int Expired, int ReIssued, int FlaggedForFollowUp);

/// <summary>
/// The scheduled half of the invite engine. Expiry is never triggered by a attendee opening a
/// link — an invite nobody ever opens has to expire too.
/// </summary>
/// <param name="deliveries">Dispatches staged re-invites after each commit.</param>
/// <param name="invites">The invites.</param>
/// <param name="attendees">The attendees.</param>
/// <param name="settings">The settings.</param>
/// <param name="issuer">The issuer.</param>
/// <param name="audit">The audit.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="clock">The clock.</param>
public sealed class ExpireInvitesHandler(
    IInviteRepository invites,
    IAttendeeRepository attendees,
    ISystemSettingsRepository settings,
    InviteIssuer issuer,
    EmailDeliveryService deliveries,
    IAuditLogger audit,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    /// <summary>Expires and optionally replaces each due invite under its attendee lifecycle lock.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<InviteSweepSummary> HandleAsync(CancellationToken cancellationToken)
    {
        var configuration = await settings.GetAsync(cancellationToken);
        var due = await invites.ListPendingExpiredAsync(clock.UtcNow, cancellationToken);

        var expired = 0;
        var reIssued = 0;
        var flagged = 0;

        foreach (var dueInvite in due)
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

            // The attendee is the lifecycle root. The due-list row is only a work hint and is
            // re-read under the attendee and invite locks before any transition is made.
            var attendee = await attendees.LockForUpdateAsync(dueInvite.AttendeeId, cancellationToken);
            if (attendee is null)
            {
                continue;
            }

            var invite = await invites.LockForUpdateAsync(dueInvite.Id, cancellationToken);
            if (invite is null || !invite.IsUsableAt(clock.UtcNow) && invite.Status != Domain.Invites.InviteStatus.Pending)
            {
                await transaction.CommitAsync(cancellationToken);
                continue;
            }

            if (invite.RecoveryOfBookingId.HasValue)
            {
                if (invite.ExpiresAt > clock.UtcNow)
                {
                    await transaction.CommitAsync(cancellationToken);
                    continue;
                }

                invite.MarkExpired();
                expired++;

                audit.Record(
                    AuditEntityTypes.Invite,
                    invite.Id,
                    AuditAction.InviteExpired,
                    ActorType.System,
                    null,
                    $"retry {invite.RetryCount}");

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

                continue;
            }

            if (invite.ExpiresAt > clock.UtcNow || attendee.Status == Domain.Attendees.AttendeeStatus.Booked)
            {
                await transaction.CommitAsync(cancellationToken);
                continue;
            }

            invite.MarkExpired();
            expired++;

            audit.Record(
                AuditEntityTypes.Invite,
                invite.Id,
                AuditAction.InviteExpired,
                ActorType.System,
                null,
                $"retry {invite.RetryCount}");

            if (invite.RetryCount >= configuration.MaxAutoRetryCount)
            {
                attendee.MarkNoResponse(clock.UtcNow);
                flagged++;
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                continue;
            }

            var issueResult = await issuer.IssueInitialAsync(
                attendee,
                invite.RetryCount + 1,
                ActorType.System,
                null,
                isReinvite: true,
                cancellationToken);

            if (issueResult.IsFailure)
            {
                if (attendee.Status == Domain.Attendees.AttendeeStatus.Invited)
                {
                    attendee.MarkNoResponse(clock.UtcNow);
                    flagged++;
                    audit.Record(
                        AuditEntityTypes.Invite,
                        invite.Id,
                        AuditAction.InviteExpired,
                        ActorType.System,
                        null,
                        "re-issue failed");
                }

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

                continue;
            }

            var issued = issueResult.Value;
            if (issued.Invited)
            {
                reIssued++;
            }

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

            if (issued.DispatchPlan is { } plan)
            {
                await deliveries.DispatchClaimedAsync(
                    plan.DeliveryId, plan.Message, cancellationToken, plan.OnSent);
            }
        }

        return new InviteSweepSummary(expired, reIssued, flagged);
    }
}
`````

## before — src/EventBooking.Application/Invites/InviteIssuer.cs — 1/1

<!-- retirement-file: {"id":8,"file":"src/EventBooking.Application/Invites/InviteIssuer.cs","beforeSha":"5f3d4645294b0026ef28705a46116e39e7a07f9fb618d1a9210edb465a14776d","afterSha":"e17b2c0047d3f3ba22f461baae0fe14ac69a9e70caeca504534258808b2d7113","side":"before","part":1,"parts":1} -->

`````csharp
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
`````

## after — src/EventBooking.Application/Invites/InviteIssuer.cs — 1/1

<!-- retirement-file: {"id":8,"file":"src/EventBooking.Application/Invites/InviteIssuer.cs","beforeSha":"5f3d4645294b0026ef28705a46116e39e7a07f9fb618d1a9210edb465a14776d","afterSha":"e17b2c0047d3f3ba22f461baae0fe14ac69a9e70caeca504534258808b2d7113","side":"after","part":1,"parts":1} -->

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

## before — src/EventBooking.Domain/Attendees/Attendee.cs — 1/1

<!-- retirement-file: {"id":9,"file":"src/EventBooking.Domain/Attendees/Attendee.cs","beforeSha":"dcda95b3b8c5124cc674a93b7a44d170c4c3c80ada4c59ca46c1a725c565ca41","afterSha":"a53682ffa89b8fb02de8731aca428f0d9b06d314dda6cdff2bbab05016b57ace","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net.Mail;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Domain.Attendees;

/// <summary>A person invited to attend appointments, whose requirements derive from one attendee group.</summary>
public sealed class Attendee
{
    private readonly List<AttendeeRequirement> _requirements = [];

    private Attendee()
    {
        // Required by the persistence layer's constructor binding.
        Name = string.Empty;
        Email = string.Empty;
    }

    /// <summary>Gets the attendee identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the attendee display name.</summary>
    public string Name { get; private set; }

    /// <summary>Gets the normalized attendee email address.</summary>
    public string Email { get; private set; }

    /// <summary>Gets the required assigned Attendee Group identifier.</summary>
    public Guid AttendeeGroupId { get; private set; }

    /// <summary>Gets where the attendee sits in the invite and booking lifecycle.</summary>
    public AttendeeStatus Status { get; private set; } = AttendeeStatus.NotYetInvited;

    /// <summary>Gets the materialized appointment types the attendee currently requires.</summary>
    public IReadOnlyList<AttendeeRequirement> Requirements => _requirements;

    /// <summary>Gets the identifiers of the appointment types the attendee currently requires.</summary>
    public IReadOnlyList<Guid> RequiredAppointmentTypeIds =>
        _requirements.Select(r => r.AppointmentTypeId).ToList();

    /// <summary>Creates a Attendee and derives every requirement from the active mapped group.</summary>
    /// <param name="id">The id.</param>
    /// <param name="name">The name.</param>
    /// <param name="email">The email.</param>
    /// <param name="attendeeGroup">The attendee group.</param>
    public static Attendee Create(Guid id, string? name, string? email, AttendeeGroup attendeeGroup)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");

        var attendee = new Attendee
        {
            Id = id,
            Name = Guard.NotBlank(name, "name"),
            Email = NormaliseEmail(email),
            Status = AttendeeStatus.NotYetInvited,
        };

        attendee.AssignAttendeeGroup(attendeeGroup);

        return attendee;
    }

    /// <summary>Replaces the attendee name and email after validating both.</summary>
    /// <param name="name">The name.</param>
    /// <param name="email">The email.</param>
    public void UpdateDetails(string? name, string? email)
    {
        // Validate both before mutating either.
        var newName = Guard.NotBlank(name, "name");
        var newEmail = NormaliseEmail(email);

        Name = newName;
        Email = newEmail;
    }

    /// <summary>Assigns a group and derives its complete set; returns whether that set changed.</summary>
    /// <param name="attendeeGroup">The attendee group.</param>
    public bool AssignAttendeeGroup(AttendeeGroup attendeeGroup)
    {
        ArgumentNullException.ThrowIfNull(attendeeGroup);
        Guard.Against(!attendeeGroup.IsActive, "An inactive attendee group cannot be assignment authority.");

        var mapping = attendeeGroup.RequiredAppointmentTypeIds.ToList();

        Guard.Against(mapping.Count == 0, "An attendee group must map at least one appointment type.");

        foreach (var appointmentTypeId in mapping)
        {
            AppointmentTypeIds.EnsureKnown(appointmentTypeId);
        }

        AttendeeGroupId = attendeeGroup.Id;

        if (_requirements.Select(r => r.AppointmentTypeId).Order().SequenceEqual(mapping.Order()))
        {
            return false;
        }

        _requirements.Clear();
        foreach (var appointmentTypeId in mapping.Order())
        {
            _requirements.Add(AttendeeRequirement.For(Id, appointmentTypeId));
        }

        return true;
    }

    /// <summary>Moves the attendee to Invited from a pre-booking lifecycle state.</summary>
    public void MarkInvited() => TransitionTo(
        AttendeeStatus.Invited,
        AttendeeStatus.NotYetInvited,
        AttendeeStatus.AwaitingAvailability,
        AttendeeStatus.Invited,
        AttendeeStatus.NoResponseNeedsFollowUp);

    /// <summary>Moves the attendee to AwaitingAvailability from a pre-booking lifecycle state.</summary>
    public void MarkAwaitingAvailability() => TransitionTo(
        AttendeeStatus.AwaitingAvailability,
        AttendeeStatus.NotYetInvited,
        AttendeeStatus.AwaitingAvailability,
        AttendeeStatus.Invited,
        AttendeeStatus.NoResponseNeedsFollowUp);

    /// <summary>Moves an invited attendee to Booked.</summary>
    public void MarkBooked() => TransitionTo(AttendeeStatus.Booked, AttendeeStatus.Invited);

    /// <summary>Moves an invited attendee to NoResponseNeedsFollowUp.</summary>
    public void MarkNoResponse() => TransitionTo(
        AttendeeStatus.NoResponseNeedsFollowUp,
        AttendeeStatus.Invited);

    /// <summary>Returns an invited or booked attendee to NotYetInvited.</summary>
    public void ResetToNotYetInvited() => TransitionTo(
        AttendeeStatus.NotYetInvited,
        AttendeeStatus.Invited,
        AttendeeStatus.Booked);

    /// <summary>Resets an unbooked Attendee after a derived requirement-set change.</summary>
    public void ResetAfterRequirementChange()
    {
        if (Status is AttendeeStatus.NotYetInvited)
        {
            return;
        }

        TransitionTo(
            AttendeeStatus.NotYetInvited,
            AttendeeStatus.AwaitingAvailability,
            AttendeeStatus.NoResponseNeedsFollowUp,
            AttendeeStatus.Invited);
    }

    private void TransitionTo(AttendeeStatus target, params AttendeeStatus[] allowedOrigins)
    {
        Guard.Against(
            !allowedOrigins.Contains(Status),
            $"A attendee cannot move from {Status} to {target}.");

        Status = target;
    }

    private static string NormaliseEmail(string? email)
    {
        var trimmed = email?.Trim() ?? string.Empty;

        var valid =
            trimmed.Length > 0
            && !trimmed.Any(char.IsWhiteSpace)
            && MailAddress.TryCreate(trimmed, out var parsed)
            && parsed!.Host.Contains('.');

        Guard.Against(!valid, "email is not a valid email address.");

        return trimmed.ToLowerInvariant();
    }
}
`````

## after — src/EventBooking.Domain/Attendees/Attendee.cs — 1/1

<!-- retirement-file: {"id":9,"file":"src/EventBooking.Domain/Attendees/Attendee.cs","beforeSha":"dcda95b3b8c5124cc674a93b7a44d170c4c3c80ada4c59ca46c1a725c565ca41","afterSha":"a53682ffa89b8fb02de8731aca428f0d9b06d314dda6cdff2bbab05016b57ace","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net.Mail;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Domain.Attendees;

/// <summary>A person invited to attend appointments, whose requirements derive from one attendee group.</summary>
public sealed class Attendee
{
    /// <summary>
    /// Every legal move, one entry per row of design 01's table. Anything absent is a defect
    /// (decision D15), so the set is the rule rather than a comment beside it.
    /// </summary>
    private static readonly HashSet<(AttendeeStatus From, AttendeeStatus To)> Legal =
    [
        (AttendeeStatus.NotYetInvited, AttendeeStatus.Invited),
        (AttendeeStatus.NotYetInvited, AttendeeStatus.AwaitingAvailability),
        (AttendeeStatus.AwaitingAvailability, AttendeeStatus.Invited),
        (AttendeeStatus.AwaitingAvailability, AttendeeStatus.NotYetInvited),
        (AttendeeStatus.Invited, AttendeeStatus.Invited),
        (AttendeeStatus.Invited, AttendeeStatus.Booked),
        (AttendeeStatus.Invited, AttendeeStatus.NoResponseNeedsFollowUp),
        (AttendeeStatus.Invited, AttendeeStatus.NotYetInvited),
        (AttendeeStatus.NoResponseNeedsFollowUp, AttendeeStatus.Invited),
        (AttendeeStatus.NoResponseNeedsFollowUp, AttendeeStatus.AwaitingAvailability),
        (AttendeeStatus.NoResponseNeedsFollowUp, AttendeeStatus.NotYetInvited),
        (AttendeeStatus.Booked, AttendeeStatus.Invited),
        (AttendeeStatus.Booked, AttendeeStatus.AwaitingAvailability),
        (AttendeeStatus.Booked, AttendeeStatus.NotYetInvited),
    ];

    private readonly List<AttendeeRequirement> _requirements = [];

    private Attendee()
    {
        // Required by the persistence layer's constructor binding.
        Name = string.Empty;
        Email = string.Empty;
    }

    /// <summary>Gets the attendee identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the attendee display name.</summary>
    public string Name { get; private set; }

    /// <summary>Gets the normalized attendee email address.</summary>
    public string Email { get; private set; }

    /// <summary>Gets the required assigned Attendee Group identifier.</summary>
    public Guid AttendeeGroupId { get; private set; }

    /// <summary>Gets where the attendee sits in the invite and booking lifecycle.</summary>
    public AttendeeStatus Status { get; private set; } = AttendeeStatus.NotYetInvited;

    /// <summary>Gets when the status was last written. Stamped on creation and on every change.</summary>
    public DateTimeOffset StatusChangedAt { get; private set; }

    /// <summary>Gets the materialized appointment types the attendee currently requires.</summary>
    public IReadOnlyList<AttendeeRequirement> Requirements => _requirements;

    /// <summary>Gets the identifiers of the appointment types the attendee currently requires.</summary>
    public IReadOnlyList<Guid> RequiredAppointmentTypeIds =>
        _requirements.Select(r => r.AppointmentTypeId).ToList();

    /// <summary>Creates a Attendee and derives every requirement from the active mapped group.</summary>
    /// <param name="id">The id.</param>
    /// <param name="name">The name.</param>
    /// <param name="email">The email.</param>
    /// <param name="attendeeGroup">The attendee group.</param>
    /// <param name="now">The instant the initial status is stamped with.</param>
    public static Attendee Create(
        Guid id, string? name, string? email, AttendeeGroup attendeeGroup, DateTimeOffset now)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");

        var attendee = new Attendee
        {
            Id = id,
            Name = Guard.NotBlank(name, "name"),
            Email = NormaliseEmail(email),
            Status = AttendeeStatus.NotYetInvited,
            StatusChangedAt = now,
        };

        attendee.AssignAttendeeGroup(attendeeGroup);

        return attendee;
    }

    /// <summary>Replaces the attendee name and email after validating both.</summary>
    /// <param name="name">The name.</param>
    /// <param name="email">The email.</param>
    public void UpdateDetails(string? name, string? email)
    {
        // Validate both before mutating either.
        var newName = Guard.NotBlank(name, "name");
        var newEmail = NormaliseEmail(email);

        Name = newName;
        Email = newEmail;
    }

    /// <summary>Assigns a group and derives its complete set; returns whether that set changed.</summary>
    /// <param name="attendeeGroup">The attendee group.</param>
    public bool AssignAttendeeGroup(AttendeeGroup attendeeGroup)
    {
        ArgumentNullException.ThrowIfNull(attendeeGroup);
        Guard.Against(!attendeeGroup.IsActive, "An inactive attendee group cannot be assignment authority.");

        var mapping = attendeeGroup.RequiredAppointmentTypeIds.ToList();

        Guard.Against(mapping.Count == 0, "An attendee group must map at least one appointment type.");

        foreach (var appointmentTypeId in mapping)
        {
            AppointmentTypeIds.EnsureKnown(appointmentTypeId);
        }

        AttendeeGroupId = attendeeGroup.Id;

        if (_requirements.Select(r => r.AppointmentTypeId).Order().SequenceEqual(mapping.Order()))
        {
            return false;
        }

        _requirements.Clear();
        foreach (var appointmentTypeId in mapping.Order())
        {
            _requirements.Add(AttendeeRequirement.For(Id, appointmentTypeId));
        }

        return true;
    }

    /// <summary>Whether design 01's table lists this move. Pure, so a caller can ask before acting.</summary>
    /// <param name="from">The current status.</param>
    /// <param name="to">The wanted status.</param>
    public static bool IsLegalTransition(AttendeeStatus from, AttendeeStatus to) =>
        Legal.Contains((from, to));

    /// <summary>Moves the attendee to Invited, from any status the table allows.</summary>
    /// <param name="now">The instant to stamp.</param>
    public void MarkInvited(DateTimeOffset now) => TransitionTo(AttendeeStatus.Invited, now);

    /// <summary>Moves the attendee to AwaitingAvailability, from any status the table allows.</summary>
    /// <param name="now">The instant to stamp.</param>
    public void MarkAwaitingAvailability(DateTimeOffset now) =>
        TransitionTo(AttendeeStatus.AwaitingAvailability, now);

    /// <summary>Moves an invited attendee to Booked.</summary>
    /// <param name="now">The instant to stamp.</param>
    public void MarkBooked(DateTimeOffset now) => TransitionTo(AttendeeStatus.Booked, now);

    /// <summary>Moves an invited attendee to NoResponseNeedsFollowUp.</summary>
    /// <param name="now">The instant to stamp.</param>
    public void MarkNoResponse(DateTimeOffset now) =>
        TransitionTo(AttendeeStatus.NoResponseNeedsFollowUp, now);

    /// <summary>Returns the attendee to NotYetInvited, which the table allows from anywhere.</summary>
    /// <param name="now">The instant to stamp.</param>
    public void ResetToNotYetInvited(DateTimeOffset now) =>
        TransitionTo(AttendeeStatus.NotYetInvited, now);

    /// <summary>Resets an unbooked Attendee after a derived requirement-set change.</summary>
    /// <param name="now">The instant to stamp.</param>
    public void ResetAfterRequirementChange(DateTimeOffset now)
    {
        if (Status is AttendeeStatus.NotYetInvited)
        {
            return;
        }

        // The table allows Booked to NotYetInvited, but only when the attendee themselves cancels.
        // A group reassignment must not silently discard a booking, so it is refused here.
        Guard.Against(
            Status == AttendeeStatus.Booked,
            $"A attendee cannot move from {Status} to {AttendeeStatus.NotYetInvited}.");

        TransitionTo(AttendeeStatus.NotYetInvited, now);
    }

    private void TransitionTo(AttendeeStatus target, DateTimeOffset now)
    {
        Guard.Against(
            !IsLegalTransition(Status, target),
            $"A attendee cannot move from {Status} to {target}.");

        Status = target;
        StatusChangedAt = now;
    }

    private static string NormaliseEmail(string? email)
    {
        var trimmed = email?.Trim() ?? string.Empty;

        var valid =
            trimmed.Length > 0
            && !trimmed.Any(char.IsWhiteSpace)
            && MailAddress.TryCreate(trimmed, out var parsed)
            && parsed!.Host.Contains('.');

        Guard.Against(!valid, "email is not a valid email address.");

        return trimmed.ToLowerInvariant();
    }
}
`````
