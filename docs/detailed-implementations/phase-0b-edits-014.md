# 00b — Vocabulary edits 14 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — src/EventBooking.Application/Invites/CancelRecoveryInviteHandler.cs — 1/1

<!-- vocabulary-file: {"id":74,"oldPath":"src/EventBooking.Application/Invites/CancelRecoveryInviteHandler.cs","newPath":"src/EventBooking.Application/Invites/CancelRecoveryInviteHandler.cs","beforeSha":"9733af18cec087533b1c8960d65ecce810f990710df081fcaf856bf1da707947","afterSha":"e2dbb458e31f1845935dcc751092faebcb638ec707a03fde002ed9d0197309a5","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Application.Invites;

/// <summary>Cancels one pending recovery Invite without touching capacity or appointments.</summary>
/// <param name="StaffUserId">The Coordinator cancelling the recovery Invite.</param>
/// <param name="AttendeeId">The attendee route the Invite must belong to.</param>
/// <param name="InviteId">The pending recovery Invite to cancel.</param>
public sealed record CancelRecoveryInviteCommand(Guid StaffUserId, Guid AttendeeId, Guid InviteId);

/// <summary>Cancels one Pending recovery Invite without changing capacity or Attendee status.</summary>
/// <param name="attendees">The attendees.</param>
/// <param name="access">The access.</param>
/// <param name="invites">The invites.</param>
/// <param name="bookings">The bookings.</param>
/// <param name="audit">The audit.</param>
/// <param name="unitOfWork">The unit of work.</param>
public sealed class CancelRecoveryInviteHandler(
    IAttendeeRepository attendees,
    IStaffAccessAuthorizer access,
    IInviteRepository invites,
    IBookingRepository bookings,
    IAuditLogger audit,
    IUnitOfWork unitOfWork)
{
    /// <summary>Cancels one Pending recovery Invite without changing capacity or Attendee status.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result> HandleAsync(
        CancelRecoveryInviteCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageAttendees,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result.Failure(authorized.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var attendee = await attendees.LockForUpdateAsync(command.AttendeeId, cancellationToken);
        if (attendee is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.NotFound("No such attendee."));
        }

        var invite = await invites.LockForUpdateAsync(command.InviteId, cancellationToken);
        if (invite is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.NotFound("No such invite."));
        }

        if (invite.RecoveryOfBookingId is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict("Only a recovery invite can be cancelled."));
        }

        if (invite.AttendeeId != command.AttendeeId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict("This invite does not belong to this attendee."));
        }

        var root = await bookings.LockForUpdateAsync(invite.RecoveryOfBookingId.Value, cancellationToken);
        if (root is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.NotFound("The original booking no longer exists."));
        }

        if (root.AttendeeId != command.AttendeeId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict("This invite does not belong to this attendee."));
        }

        if (invite.Status != InviteStatus.Pending)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict("This recovery invite is no longer pending."));
        }

        try
        {
            invite.RotateTokenHash(Guid.NewGuid().ToString("N"));
            invite.CancelRecovery();
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Validation(ex.Message));
        }

        audit.Record(
            AuditEntityTypes.Invite,
            invite.Id,
            AuditAction.RecoveryInviteCancelled,
            ActorType.Staff,
            command.StaffUserId.ToString(),
            $"root {root.Id}");

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

        return Result.Success();
    }
}
`````

## before — src/EventBooking.Application/Invites/EligibleSlotFinder.cs — 1/1

<!-- vocabulary-file: {"id":75,"oldPath":"src/EventBooking.Application/Invites/EligibleSlotFinder.cs","newPath":"src/EventBooking.Application/Invites/EligibleEventFinder.cs","beforeSha":"c8aef4dccab2e3b09e48079867e06ea2ca312c7dc042e9a11ceb7d9e6791540e","afterSha":"02b647682c72ec1b80e809ee4b3c225c552657d7e7e32b4ce8ebba54dec204ba","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Invites;

/// <summary>
/// The one place the "which slots may a candidate be offered" rule lives. Task 36 uses it to build
/// an invite; Task 40 uses it to find a single replacement when an option fills up.
/// </summary>
/// <param name="slots">The slots.</param>
/// <param name="clock">The clock.</param>
public sealed class EligibleSlotFinder(IConfirmedSlotRepository slots, IClock clock)
{
    /// <summary>Defines find async for the current use case.</summary>
    /// <param name="requiredAppointmentTypeIds">The required appointment type ids.</param>
    /// <param name="take">The take.</param>
    /// <param name="excludeSlotIds">The exclude slot ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<IReadOnlyList<ConfirmedSlot>> FindAsync(
        IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
        int take,
        IReadOnlyCollection<Guid> excludeSlotIds,
        CancellationToken cancellationToken)
    {
        var today = clock.TodayAtHeadOffice;

        var candidates = await slots.ListActiveAsync(today, cancellationToken);

        return candidates
            .Where(s => !excludeSlotIds.Contains(s.Id))
            .Where(s => s.Window.StartsAfter(today))
            .Where(s => s.HasSpareCapacityForAll(requiredAppointmentTypeIds))
            .OrderBy(s => s.Window)
            .Take(take)
            .ToList();
    }
}
`````

## after — src/EventBooking.Application/Invites/EligibleEventFinder.cs — 1/1

<!-- vocabulary-file: {"id":75,"oldPath":"src/EventBooking.Application/Invites/EligibleSlotFinder.cs","newPath":"src/EventBooking.Application/Invites/EligibleEventFinder.cs","beforeSha":"c8aef4dccab2e3b09e48079867e06ea2ca312c7dc042e9a11ceb7d9e6791540e","afterSha":"02b647682c72ec1b80e809ee4b3c225c552657d7e7e32b4ce8ebba54dec204ba","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Invites;

/// <summary>
/// The one place the "which events may a attendee be offered" rule lives. Task 36 uses it to build
/// an invite; Task 40 uses it to find a single replacement when an option fills up.
/// </summary>
/// <param name="events">The events.</param>
/// <param name="clock">The clock.</param>
public sealed class EligibleEventFinder(IEventRepository events, IClock clock)
{
    /// <summary>Defines find async for the current use case.</summary>
    /// <param name="requiredAppointmentTypeIds">The required appointment type ids.</param>
    /// <param name="take">The take.</param>
    /// <param name="excludeEventIds">The exclude event ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<IReadOnlyList<Event>> FindAsync(
        IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
        int take,
        IReadOnlyCollection<Guid> excludeEventIds,
        CancellationToken cancellationToken)
    {
        var today = clock.TodayAtTransitionalLocation;

        var attendees = await events.ListActiveAsync(today, cancellationToken);

        return attendees
            .Where(s => !excludeEventIds.Contains(s.Id))
            .Where(s => s.Window.StartsAfter(today))
            .Where(s => s.HasSpareCapacityForAll(requiredAppointmentTypeIds))
            .OrderBy(s => s.Window)
            .Take(take)
            .ToList();
    }
}
`````

## before — src/EventBooking.Application/Invites/ExpireInvitesHandler.cs — 1/1

<!-- vocabulary-file: {"id":76,"oldPath":"src/EventBooking.Application/Invites/ExpireInvitesHandler.cs","newPath":"src/EventBooking.Application/Invites/ExpireInvitesHandler.cs","beforeSha":"087520679fc8b186814729a62072cd491d5cca94517467e10a1d1e7d82e7d08a","afterSha":"6d21162fed9b40e51e20b306a3c04151382732c81da74bd4a29a3794ba4ca815","side":"before","part":1,"parts":1} -->

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
/// The scheduled half of the invite engine. Expiry is never triggered by a candidate opening a
/// link — an invite nobody ever opens has to expire too.
/// </summary>
/// <param name="deliveries">Dispatches staged re-invites after each commit.</param>
/// <param name="invites">The invites.</param>
/// <param name="candidates">The candidates.</param>
/// <param name="settings">The settings.</param>
/// <param name="issuer">The issuer.</param>
/// <param name="audit">The audit.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="clock">The clock.</param>
public sealed class ExpireInvitesHandler(
    IInviteRepository invites,
    ICandidateRepository candidates,
    ISystemSettingsRepository settings,
    InviteIssuer issuer,
    EmailDeliveryService deliveries,
    IAuditLogger audit,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    /// <summary>Expires and optionally replaces each due invite under its candidate lifecycle lock.</summary>
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

            // The candidate is the lifecycle root. The due-list row is only a work hint and is
            // re-read under the candidate and invite locks before any transition is made.
            var candidate = await candidates.LockForUpdateAsync(dueInvite.CandidateId, cancellationToken);
            if (candidate is null)
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

            if (invite.ExpiresAt > clock.UtcNow || candidate.Status == Domain.Candidates.CandidateStatus.Booked)
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
                candidate.MarkNoResponse();
                flagged++;
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                continue;
            }

            var issueResult = await issuer.IssueInitialAsync(
                candidate,
                invite.RetryCount + 1,
                ActorType.System,
                null,
                isReinvite: true,
                cancellationToken);

            if (issueResult.IsFailure)
            {
                if (candidate.Status == Domain.Candidates.CandidateStatus.Invited)
                {
                    candidate.MarkNoResponse();
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

<!-- vocabulary-file: {"id":76,"oldPath":"src/EventBooking.Application/Invites/ExpireInvitesHandler.cs","newPath":"src/EventBooking.Application/Invites/ExpireInvitesHandler.cs","beforeSha":"087520679fc8b186814729a62072cd491d5cca94517467e10a1d1e7d82e7d08a","afterSha":"6d21162fed9b40e51e20b306a3c04151382732c81da74bd4a29a3794ba4ca815","side":"after","part":1,"parts":1} -->

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

## before — src/EventBooking.Application/Invites/InviteIssuer.cs — 1/1

<!-- vocabulary-file: {"id":77,"oldPath":"src/EventBooking.Application/Invites/InviteIssuer.cs","newPath":"src/EventBooking.Application/Invites/InviteIssuer.cs","beforeSha":"0767d03c0c368fbd8e385490df63eadd160d178e6b1e5a27d13bf43f66071a86","afterSha":"6021fcde351a824e150276dbc1ae8a1dc92f28bee8879e5d2069a3eb73c4c3f3","side":"before","part":1,"parts":1} -->

`````csharp
using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Slots;

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
/// coordinator trigger (Task 37), the expiry sweep (Task 38) and slot cancellation (Task 42).
/// Never saves or calls a provider — the caller owns the unit of work and dispatches only after commit.
/// </summary>
/// <param name="invites">The invites.</param>
/// <param name="groups">The groups.</param>
/// <param name="slotFinder">The slot finder.</param>
/// <param name="settings">The settings.</param>
/// <param name="tokens">The tokens.</param>
/// <param name="deliveries">The deliveries.</param>
/// <param name="audit">The audit.</param>
/// <param name="clock">The clock.</param>
/// <param name="portal">The portal.</param>
public sealed class InviteIssuer(
    IInviteRepository invites,
    IEmployeeGroupRepository groups,
    EligibleSlotFinder slotFinder,
    ISystemSettingsRepository settings,
    ITokenService tokens,
    EmailDeliveryService deliveries,
    IAuditLogger audit,
    IClock clock,
    CandidatePortalOptions portal)
{
    /// <summary>Issues an initial Invite from a locked, validated Candidate requirement set.</summary>
    /// <param name="candidate">The candidate whose lifecycle is already locked by the caller.</param>
    /// <param name="retryCount">The automated retry number to persist on the new invite.</param>
    /// <param name="actorType">The actor recorded for invite creation.</param>
    /// <param name="actorId">The actor identifier, when a staff identity caused the change.</param>
    /// <param name="isReinvite">Whether the reminder template should be used.</param>
    /// <param name="cancellationToken">Cancels repository and slot reads.</param>
    /// <returns>A pending delivery plan the caller dispatches after commit.</returns>
    public async Task<Result<InviteIssueResult>> IssueInitialAsync(
        Candidate candidate,
        int retryCount,
        ActorType actorType,
        string? actorId,
        bool isReinvite,
        CancellationToken cancellationToken)
    {
        if (!candidate.EmployeeGroupId.HasValue)
        {
            return Result<InviteIssueResult>.Failure(Error.CandidateReconciliationRequired(
                "Assign an employee group before issuing an invite."));
        }

        var group = await groups.GetAsync(candidate.EmployeeGroupId.Value, cancellationToken);
        var mapping = group?.RequiredAppointmentTypeIds
            .Order()
            .ToList();
        var current = candidate.RequiredAppointmentTypeIds
            .Order()
            .ToList();
        if (group is null || !group.IsActive || mapping!.Count == 0 || !mapping.SequenceEqual(current))
        {
            return Result<InviteIssueResult>.Failure(Error.CandidateRequirementSnapshotMismatch(
                "The candidate requirements do not match their employee group."));
        }

        var pending = await invites.GetPendingForCandidateAsync(candidate.Id, cancellationToken);
        if (pending?.Status == Domain.Invites.InviteStatus.Pending)
        {
            pending.MarkSuperseded();
        }

        InviteIssueResult issued;
        try
        {
            var options = await slotFinder.FindAsync(
                mapping,
                Invite.RequiredOptionCount,
                [],
                cancellationToken);

            if (options.Count < Invite.RequiredOptionCount)
            {
                candidate.MarkAwaitingAvailability();
                return Result<InviteIssueResult>.Success(new InviteIssueResult(false, null, false));
            }

            var configuration = await settings.GetAsync(cancellationToken);

            var inviteId = Guid.NewGuid();
            var token = tokens.Issue(inviteId);

            var invite = Invite.CreateInitial(
                inviteId,
                candidate.Id,
                token.TokenHash,
                clock.UtcNow.AddDays(configuration.InviteExpiryDays),
                options.Select(o => o.Id),
                mapping,
                retryCount);

            invites.Add(invite);
            candidate.MarkInvited();

            audit.Record(
                AuditEntityTypes.Invite,
                inviteId,
                AuditAction.InviteCreated,
                actorType,
                actorId,
                $"retry {retryCount}");

            var message = CandidateEmailComposer.Invite(
                candidate,
                mapping,
                options,
                $"{portal.BaseUrl}/book/{token.Token}",
                isReinvite,
                isRecovery: false);

            var delivery = deliveries.StagePending(candidate.Id, message.Template, inviteId: inviteId);
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
    /// <param name="candidate">The candidate whose lifecycle is already locked by the caller.</param>
    /// <param name="rootBookingId">The original journey-root Booking the recovery belongs to.</param>
    /// <param name="selectedTypeIds">The recoverable snapshot, already revalidated under lock.</param>
    /// <param name="options">Exactly three future slots with capacity for every selected type.</param>
    /// <param name="actorType">The actor recorded for invite creation.</param>
    /// <param name="actorId">The actor identifier, when a staff identity caused the change.</param>
    /// <param name="cancellationToken">Cancels repository reads.</param>
    /// <returns>A pending delivery plan the caller dispatches after commit.</returns>
    public async Task<Result<InviteIssueResult>> IssueRecoveryAsync(
        Candidate candidate,
        Guid rootBookingId,
        IReadOnlyList<Guid> selectedTypeIds,
        IReadOnlyList<ConfirmedSlot> options,
        ActorType actorType,
        string? actorId,
        CancellationToken cancellationToken)
    {
        if (options.Count != Invite.RequiredOptionCount)
        {
            return Result<InviteIssueResult>.Failure(Error.Validation(
                "A recovery invite must offer exactly three slot options."));
        }

        InviteIssueResult issued;
        try
        {
            var configuration = await settings.GetAsync(cancellationToken);

            var inviteId = Guid.NewGuid();
            var token = tokens.Issue(inviteId);

            var invite = Invite.CreateRecovery(
                inviteId,
                candidate.Id,
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

            var message = CandidateEmailComposer.Invite(
                candidate,
                selectedTypeIds,
                options,
                $"{portal.BaseUrl}/book/{token.Token}",
                isReinvite: false,
                isRecovery: true);

            var delivery = deliveries.StagePending(candidate.Id, message.Template, inviteId: inviteId);
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

<!-- vocabulary-file: {"id":77,"oldPath":"src/EventBooking.Application/Invites/InviteIssuer.cs","newPath":"src/EventBooking.Application/Invites/InviteIssuer.cs","beforeSha":"0767d03c0c368fbd8e385490df63eadd160d178e6b1e5a27d13bf43f66071a86","afterSha":"6021fcde351a824e150276dbc1ae8a1dc92f28bee8879e5d2069a3eb73c4c3f3","side":"after","part":1,"parts":1} -->

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
        if (!attendee.AttendeeGroupId.HasValue)
        {
            return Result<InviteIssueResult>.Failure(Error.AttendeeReconciliationRequired(
                "Assign an attendee group before issuing an invite."));
        }

        var group = await groups.GetAsync(attendee.AttendeeGroupId.Value, cancellationToken);
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

## before — src/EventBooking.Application/Invites/RecoveryRequirementSelector.cs — 1/1

<!-- vocabulary-file: {"id":78,"oldPath":"src/EventBooking.Application/Invites/RecoveryRequirementSelector.cs","newPath":"src/EventBooking.Application/Invites/RecoveryRequirementSelector.cs","beforeSha":"8bbbf60061fb46b6d287f126bb5d536cc3f120b23b4920e837a50ee9e816941e","afterSha":"0b4cd53ca13ec1f338723e402d59a8c528434d8d5c4db55de249c23a5534e75d","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Invites;

/// <summary>One non-cancelled Booking Appointment considered for recovery eligibility.</summary>
/// <param name="BookingAppointmentId">The stable appointment-record identifier.</param>
/// <param name="AppointmentTypeId">The required appointment type delivered by the attempt.</param>
/// <param name="Status">The attempt's independent operational status.</param>
/// <param name="BookingCreatedAt">When the parent Booking was created, ordering repeat attempts.</param>
public sealed record RecoveryAttempt(
    Guid BookingAppointmentId,
    Guid AppointmentTypeId,
    BookingAppointmentStatus Status,
    DateTimeOffset BookingCreatedAt);

/// <summary>Selects the current requirement types whose latest attempt is an unsatisfied no-show.</summary>
public sealed class RecoveryRequirementSelector
{
    /// <summary>Returns every current type whose latest attempt is NoShow and none is Completed.</summary>
    /// <param name="currentRequirementTypeIds">The candidate's current derived requirement set.</param>
    /// <param name="attempts">Non-cancelled attempts across the journey, in any order.</param>
    /// <param name="typesAlreadyPendingRecovery">Types a pending recovery already covers.</param>
    /// <returns>The recoverable type identifiers in stable order.</returns>
    public IReadOnlyList<Guid> Select(
        IReadOnlyCollection<Guid> currentRequirementTypeIds,
        IReadOnlyCollection<RecoveryAttempt> attempts,
        IReadOnlyCollection<Guid> typesAlreadyPendingRecovery)
    {
        var current = currentRequirementTypeIds.ToHashSet();
        var pending = typesAlreadyPendingRecovery.ToHashSet();

        return attempts
            .Where(attempt => current.Contains(attempt.AppointmentTypeId))
            .Where(attempt => !pending.Contains(attempt.AppointmentTypeId))
            .GroupBy(attempt => attempt.AppointmentTypeId)
            .Where(group => !group.Any(attempt => attempt.Status == BookingAppointmentStatus.Completed))
            .Where(group => Latest(group).Status == BookingAppointmentStatus.NoShow)
            .Select(group => group.Key)
            .Order()
            .ToList();
    }

    private static RecoveryAttempt Latest(IEnumerable<RecoveryAttempt> attempts) =>
        attempts
            .OrderBy(attempt => attempt.BookingCreatedAt)
            .ThenBy(attempt => attempt.BookingAppointmentId)
            .Last();
}
`````

## after — src/EventBooking.Application/Invites/RecoveryRequirementSelector.cs — 1/1

<!-- vocabulary-file: {"id":78,"oldPath":"src/EventBooking.Application/Invites/RecoveryRequirementSelector.cs","newPath":"src/EventBooking.Application/Invites/RecoveryRequirementSelector.cs","beforeSha":"8bbbf60061fb46b6d287f126bb5d536cc3f120b23b4920e837a50ee9e816941e","afterSha":"0b4cd53ca13ec1f338723e402d59a8c528434d8d5c4db55de249c23a5534e75d","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Invites;

/// <summary>One non-cancelled Booking Appointment considered for recovery eligibility.</summary>
/// <param name="BookingAppointmentId">The stable appointment-record identifier.</param>
/// <param name="AppointmentTypeId">The required appointment type delivered by the attempt.</param>
/// <param name="Status">The attempt's independent operational status.</param>
/// <param name="BookingCreatedAt">When the parent Booking was created, ordering repeat attempts.</param>
public sealed record RecoveryAttempt(
    Guid BookingAppointmentId,
    Guid AppointmentTypeId,
    BookingAppointmentStatus Status,
    DateTimeOffset BookingCreatedAt);

/// <summary>Selects the current requirement types whose latest attempt is an unsatisfied no-show.</summary>
public sealed class RecoveryRequirementSelector
{
    /// <summary>Returns every current type whose latest attempt is NoShow and none is Completed.</summary>
    /// <param name="currentRequirementTypeIds">The attendee's current derived requirement set.</param>
    /// <param name="attempts">Non-cancelled attempts across the journey, in any order.</param>
    /// <param name="typesAlreadyPendingRecovery">Types a pending recovery already covers.</param>
    /// <returns>The recoverable type identifiers in stable order.</returns>
    public IReadOnlyList<Guid> Select(
        IReadOnlyCollection<Guid> currentRequirementTypeIds,
        IReadOnlyCollection<RecoveryAttempt> attempts,
        IReadOnlyCollection<Guid> typesAlreadyPendingRecovery)
    {
        var current = currentRequirementTypeIds.ToHashSet();
        var pending = typesAlreadyPendingRecovery.ToHashSet();

        return attempts
            .Where(attempt => current.Contains(attempt.AppointmentTypeId))
            .Where(attempt => !pending.Contains(attempt.AppointmentTypeId))
            .GroupBy(attempt => attempt.AppointmentTypeId)
            .Where(group => !group.Any(attempt => attempt.Status == BookingAppointmentStatus.Completed))
            .Where(group => Latest(group).Status == BookingAppointmentStatus.NoShow)
            .Select(group => group.Key)
            .Order()
            .ToList();
    }

    private static RecoveryAttempt Latest(IEnumerable<RecoveryAttempt> attempts) =>
        attempts
            .OrderBy(attempt => attempt.BookingCreatedAt)
            .ThenBy(attempt => attempt.BookingAppointmentId)
            .Last();
}
`````
