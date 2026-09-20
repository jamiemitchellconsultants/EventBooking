# 00b — Vocabulary edits 15 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — src/EventBooking.Application/Invites/StartRecoveryHandler.cs — 1/1

<!-- vocabulary-file: {"id":79,"oldPath":"src/EventBooking.Application/Invites/StartRecoveryHandler.cs","newPath":"src/EventBooking.Application/Invites/StartRecoveryHandler.cs","beforeSha":"4acb26446c4d20db04d6b667292d9537803a99edcfbe157ad7e7f95cca2929e6","afterSha":"26b63f6c1e1822244fe47dd0c1579087ecece23112ffdee2d1231d5390e76eaf","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Bookings;
using EventBooking.Application.Common;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Invites;

/// <summary>Starts one recovery Invite for a candidate with missed appointments.</summary>
/// <param name="StaffUserId">The Coordinator starting the recovery.</param>
/// <param name="CandidateId">The booked candidate whose no-shows are recovered.</param>
public sealed record StartRecoveryCommand(Guid StaffUserId, Guid CandidateId);

/// <summary>Reports recovery Invite creation, or the awaiting-availability outcome.</summary>
/// <param name="InviteId">The new recovery Invite identifier, or empty when no slots exist.</param>
/// <param name="AppointmentTypeIds">The recoverable snapshot offered, or awaiting availability.</param>
/// <param name="EmailSent">Whether the post-commit provider attempt completed successfully.</param>
public sealed record StartRecoveryResult(
    Guid InviteId,
    IReadOnlyList<Guid> AppointmentTypeIds,
    bool EmailSent);

/// <summary>
/// Gathers every currently recoverable no-show type into one recovery Invite under the
/// Candidate-first lifecycle lock order, revalidating eligibility after the locks because
/// preflight reads are never authoritative.
/// </summary>
/// <param name="candidates">The candidates.</param>
/// <param name="access">The access.</param>
/// <param name="invites">The invites.</param>
/// <param name="bookings">The bookings.</param>
/// <param name="appointments">The appointments.</param>
/// <param name="issuer">The issuer.</param>
/// <param name="slotFinder">The slot finder.</param>
/// <param name="deliveries">The deliveries.</param>
/// <param name="unitOfWork">The unit of work.</param>
public sealed class StartRecoveryHandler(
    ICandidateRepository candidates,
    IStaffAccessAuthorizer access,
    IInviteRepository invites,
    IBookingRepository bookings,
    IBookingAppointmentRepository appointments,
    InviteIssuer issuer,
    EligibleSlotFinder slotFinder,
    EmailDeliveryService deliveries,
    IUnitOfWork unitOfWork)
{
    /// <summary>Creates one recovery Invite after revalidating recoverability under lifecycle locks.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<StartRecoveryResult>> HandleAsync(
        StartRecoveryCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageCandidates,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<StartRecoveryResult>.Failure(authorized.Error);
        }

        var located = await candidates.GetAsync(command.CandidateId, cancellationToken);
        if (located is null)
        {
            return Result<StartRecoveryResult>.Failure(Error.NotFound("No such candidate."));
        }

        var preflight = await SelectRecoverableAsync(located, null, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var candidate = await candidates.LockForUpdateAsync(command.CandidateId, cancellationToken);
        if (candidate is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<StartRecoveryResult>.Failure(Error.NotFound("No such candidate."));
        }

        var pending = await invites.LockPendingListForCandidateAsync(candidate.Id, cancellationToken);
        var pendingRecoveries = pending
            .Where(invite => invite.RecoveryOfBookingId.HasValue)
            .ToList();

        var original = await bookings.LockActiveOriginalForCandidateAsync(candidate.Id, cancellationToken);
        if (original is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<StartRecoveryResult>.Failure(Error.RecoveryNotAvailable(
                "No active booking has a recoverable missed appointment."));
        }

        var activeRecovery = await bookings.LockActiveRecoveryAsync(original.Id, cancellationToken);
        if (pendingRecoveries.Count > 0 || activeRecovery is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<StartRecoveryResult>.Failure(Error.RecoveryAlreadyPending(
                "A recovery is already pending for this candidate."));
        }

        var journey = await bookings.ListJourneyAsync(original.Id, cancellationToken);
        var selected = await SelectRecoverableAsync(candidate, journey, pendingRecoveries, cancellationToken);
        if (!selected.SequenceEqual(preflight))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<StartRecoveryResult>.Failure(Error.RecoveryStateChanged(
                "Recovery eligibility changed while starting the recovery."));
        }

        if (selected.Count == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<StartRecoveryResult>.Failure(Error.RecoveryNotAvailable(
                "No current requirement has a recoverable missed appointment."));
        }

        var options = await slotFinder.FindAsync(
            selected,
            Invite.RequiredOptionCount,
            [],
            cancellationToken);
        if (options.Count < Invite.RequiredOptionCount)
        {
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

            return Result<StartRecoveryResult>.Success(new StartRecoveryResult(Guid.Empty, selected, false));
        }

        InviteIssueResult issued;
        try
        {
            var issueResult = await issuer.IssueRecoveryAsync(
                candidate,
                original.Id,
                selected,
                options,
                ActorType.Staff,
                command.StaffUserId.ToString(),
                cancellationToken);
            if (issueResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<StartRecoveryResult>.Failure(issueResult.Error);
            }

            issued = issueResult.Value;
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<StartRecoveryResult>.Failure(Error.Validation(ex.Message));
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<StartRecoveryResult>.Failure(Error.RecoveryAlreadyPending(
                "A recovery is already pending for this candidate."));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var emailSent = false;
        if (issued.DispatchPlan is { } plan)
        {
            var status = await deliveries.DispatchClaimedAsync(
                plan.DeliveryId, plan.Message, cancellationToken, plan.OnSent);
            emailSent = status == EmailStatus.Sent;
        }

        return Result<StartRecoveryResult>.Success(
            new StartRecoveryResult(issued.InviteId!.Value, selected, emailSent));
    }

    private async Task<IReadOnlyList<Guid>> SelectRecoverableAsync(
        Candidate candidate,
        IReadOnlyList<Booking>? journey,
        CancellationToken cancellationToken) =>
        await SelectRecoverableAsync(candidate, journey, [], cancellationToken);

    private async Task<IReadOnlyList<Guid>> SelectRecoverableAsync(
        Candidate candidate,
        IReadOnlyList<Booking>? journey,
        IReadOnlyList<Invite> pendingRecoveries,
        CancellationToken cancellationToken)
    {
        if (journey is null or { Count: 0 })
        {
            var rootId = await LocateRootBookingIdAsync(candidate.Id, cancellationToken);
            if (rootId is null)
            {
                return [];
            }

            journey = await bookings.ListJourneyAsync(rootId.Value, cancellationToken);
        }

        var ids = journey.Select(booking => booking.Id).ToList();
        var rows = ids.Count == 0
            ? []
            : await appointments.ListForBookingsAsync(ids, cancellationToken);

        var attempts = RecoveryConfirmationValidator.BuildAttempts(journey, rows);

        var covered = pendingRecoveries
            .SelectMany(invite => invite.RequiredAppointmentTypeIds)
            .Distinct()
            .ToList();

        return new RecoveryRequirementSelector().Select(
            candidate.RequiredAppointmentTypeIds, attempts, covered);
    }

    private async Task<Guid?> LocateRootBookingIdAsync(Guid candidateId, CancellationToken cancellationToken)
    {
        var active = await bookings.GetActiveForCandidateAsync(candidateId, cancellationToken);
        return active is null ? null : active.RecoveryOfBookingId ?? active.Id;
    }
}
`````

## after — src/EventBooking.Application/Invites/StartRecoveryHandler.cs — 1/1

<!-- vocabulary-file: {"id":79,"oldPath":"src/EventBooking.Application/Invites/StartRecoveryHandler.cs","newPath":"src/EventBooking.Application/Invites/StartRecoveryHandler.cs","beforeSha":"4acb26446c4d20db04d6b667292d9537803a99edcfbe157ad7e7f95cca2929e6","afterSha":"26b63f6c1e1822244fe47dd0c1579087ecece23112ffdee2d1231d5390e76eaf","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Bookings;
using EventBooking.Application.Common;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Invites;

/// <summary>Starts one recovery Invite for a attendee with missed appointments.</summary>
/// <param name="StaffUserId">The Coordinator starting the recovery.</param>
/// <param name="AttendeeId">The booked attendee whose no-shows are recovered.</param>
public sealed record StartRecoveryCommand(Guid StaffUserId, Guid AttendeeId);

/// <summary>Reports recovery Invite creation, or the awaiting-availability outcome.</summary>
/// <param name="InviteId">The new recovery Invite identifier, or empty when no events exist.</param>
/// <param name="AppointmentTypeIds">The recoverable snapshot offered, or awaiting availability.</param>
/// <param name="EmailSent">Whether the post-commit provider attempt completed successfully.</param>
public sealed record StartRecoveryResult(
    Guid InviteId,
    IReadOnlyList<Guid> AppointmentTypeIds,
    bool EmailSent);

/// <summary>
/// Gathers every currently recoverable no-show type into one recovery Invite under the
/// Attendee-first lifecycle lock order, revalidating eligibility after the locks because
/// preflight reads are never authoritative.
/// </summary>
/// <param name="attendees">The attendees.</param>
/// <param name="access">The access.</param>
/// <param name="invites">The invites.</param>
/// <param name="bookings">The bookings.</param>
/// <param name="appointments">The appointments.</param>
/// <param name="issuer">The issuer.</param>
/// <param name="eventFinder">The event finder.</param>
/// <param name="deliveries">The deliveries.</param>
/// <param name="unitOfWork">The unit of work.</param>
public sealed class StartRecoveryHandler(
    IAttendeeRepository attendees,
    IStaffAccessAuthorizer access,
    IInviteRepository invites,
    IBookingRepository bookings,
    IBookingAppointmentRepository appointments,
    InviteIssuer issuer,
    EligibleEventFinder eventFinder,
    EmailDeliveryService deliveries,
    IUnitOfWork unitOfWork)
{
    /// <summary>Creates one recovery Invite after revalidating recoverability under lifecycle locks.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<StartRecoveryResult>> HandleAsync(
        StartRecoveryCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageAttendees,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<StartRecoveryResult>.Failure(authorized.Error);
        }

        var located = await attendees.GetAsync(command.AttendeeId, cancellationToken);
        if (located is null)
        {
            return Result<StartRecoveryResult>.Failure(Error.NotFound("No such attendee."));
        }

        var preflight = await SelectRecoverableAsync(located, null, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var attendee = await attendees.LockForUpdateAsync(command.AttendeeId, cancellationToken);
        if (attendee is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<StartRecoveryResult>.Failure(Error.NotFound("No such attendee."));
        }

        var pending = await invites.LockPendingListForAttendeeAsync(attendee.Id, cancellationToken);
        var pendingRecoveries = pending
            .Where(invite => invite.RecoveryOfBookingId.HasValue)
            .ToList();

        var original = await bookings.LockActiveOriginalForAttendeeAsync(attendee.Id, cancellationToken);
        if (original is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<StartRecoveryResult>.Failure(Error.RecoveryNotAvailable(
                "No active booking has a recoverable missed appointment."));
        }

        var activeRecovery = await bookings.LockActiveRecoveryAsync(original.Id, cancellationToken);
        if (pendingRecoveries.Count > 0 || activeRecovery is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<StartRecoveryResult>.Failure(Error.RecoveryAlreadyPending(
                "A recovery is already pending for this attendee."));
        }

        var journey = await bookings.ListJourneyAsync(original.Id, cancellationToken);
        var selected = await SelectRecoverableAsync(attendee, journey, pendingRecoveries, cancellationToken);
        if (!selected.SequenceEqual(preflight))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<StartRecoveryResult>.Failure(Error.RecoveryStateChanged(
                "Recovery eligibility changed while starting the recovery."));
        }

        if (selected.Count == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<StartRecoveryResult>.Failure(Error.RecoveryNotAvailable(
                "No current requirement has a recoverable missed appointment."));
        }

        var options = await eventFinder.FindAsync(
            selected,
            Invite.RequiredOptionCount,
            [],
            cancellationToken);
        if (options.Count < Invite.RequiredOptionCount)
        {
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

            return Result<StartRecoveryResult>.Success(new StartRecoveryResult(Guid.Empty, selected, false));
        }

        InviteIssueResult issued;
        try
        {
            var issueResult = await issuer.IssueRecoveryAsync(
                attendee,
                original.Id,
                selected,
                options,
                ActorType.Staff,
                command.StaffUserId.ToString(),
                cancellationToken);
            if (issueResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<StartRecoveryResult>.Failure(issueResult.Error);
            }

            issued = issueResult.Value;
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<StartRecoveryResult>.Failure(Error.Validation(ex.Message));
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<StartRecoveryResult>.Failure(Error.RecoveryAlreadyPending(
                "A recovery is already pending for this attendee."));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var emailSent = false;
        if (issued.DispatchPlan is { } plan)
        {
            var status = await deliveries.DispatchClaimedAsync(
                plan.DeliveryId, plan.Message, cancellationToken, plan.OnSent);
            emailSent = status == EmailStatus.Sent;
        }

        return Result<StartRecoveryResult>.Success(
            new StartRecoveryResult(issued.InviteId!.Value, selected, emailSent));
    }

    private async Task<IReadOnlyList<Guid>> SelectRecoverableAsync(
        Attendee attendee,
        IReadOnlyList<Booking>? journey,
        CancellationToken cancellationToken) =>
        await SelectRecoverableAsync(attendee, journey, [], cancellationToken);

    private async Task<IReadOnlyList<Guid>> SelectRecoverableAsync(
        Attendee attendee,
        IReadOnlyList<Booking>? journey,
        IReadOnlyList<Invite> pendingRecoveries,
        CancellationToken cancellationToken)
    {
        if (journey is null or { Count: 0 })
        {
            var rootId = await LocateRootBookingIdAsync(attendee.Id, cancellationToken);
            if (rootId is null)
            {
                return [];
            }

            journey = await bookings.ListJourneyAsync(rootId.Value, cancellationToken);
        }

        var ids = journey.Select(booking => booking.Id).ToList();
        var rows = ids.Count == 0
            ? []
            : await appointments.ListForBookingsAsync(ids, cancellationToken);

        var attempts = RecoveryConfirmationValidator.BuildAttempts(journey, rows);

        var covered = pendingRecoveries
            .SelectMany(invite => invite.RequiredAppointmentTypeIds)
            .Distinct()
            .ToList();

        return new RecoveryRequirementSelector().Select(
            attendee.RequiredAppointmentTypeIds, attempts, covered);
    }

    private async Task<Guid?> LocateRootBookingIdAsync(Guid attendeeId, CancellationToken cancellationToken)
    {
        var active = await bookings.GetActiveForAttendeeAsync(attendeeId, cancellationToken);
        return active is null ? null : active.RecoveryOfBookingId ?? active.Id;
    }
}
`````

## before — src/EventBooking.Application/Invites/TriggerInviteHandler.cs — 1/1

<!-- vocabulary-file: {"id":80,"oldPath":"src/EventBooking.Application/Invites/TriggerInviteHandler.cs","newPath":"src/EventBooking.Application/Invites/TriggerInviteHandler.cs","beforeSha":"10c563fa4ed90355c038e1fc67eb9bb9aab1514bb69c78d71d793168679646d3","afterSha":"20ad137e914004f504a3874ca7afec361e993603c00b66880dfdea3d5c80eea0","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Common;
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Invites;

/// <summary>A null staff identity means the system triggered this, not a person.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="CandidateId">The candidate id.</param>
public sealed record TriggerInviteCommand(Guid? StaffUserId, Guid CandidateId);

/// <summary>Issues an invite while serializing all transitions for the candidate lifecycle.</summary>
/// <param name="deliveries">Dispatches the staged invite after commit.</param>
/// <param name="candidates">The candidates.</param>
/// <param name="access">The access.</param>
/// <param name="issuer">The issuer.</param>
/// <param name="unitOfWork">The unit of work.</param>
public sealed class TriggerInviteHandler(
    ICandidateRepository candidates,
    IStaffAccessAuthorizer access,
    InviteIssuer issuer,
    EmailDeliveryService deliveries,
    IUnitOfWork unitOfWork)
{
    /// <summary>Issues or replaces a candidate invite under the candidate row lock.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<InviteIssueResult>> HandleAsync(
        TriggerInviteCommand command,
        CancellationToken cancellationToken)
    {
        var actorType = ActorType.System;
        string? actorId = null;

        if (command.StaffUserId is not null)
        {
            var authorized = await access.AuthorizeAsync(
                command.StaffUserId.Value,
                StaffCapability.ManageCandidates,
                null,
                cancellationToken);
            if (authorized.IsFailure)
            {
                return Result<InviteIssueResult>.Failure(authorized.Error);
            }

            actorType = ActorType.Staff;
            actorId = command.StaffUserId.Value.ToString();
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var candidate = await candidates.LockForUpdateAsync(command.CandidateId, cancellationToken);
        if (candidate is null)
        {
            return Result<InviteIssueResult>.Failure(Error.NotFound("No such candidate."));
        }

        if (candidate.Status == CandidateStatus.Booked)
        {
            return Result<InviteIssueResult>.Failure(Error.Conflict("This candidate is already booked."));
        }

        InviteIssueResult issued;
        try
        {
            // A manually triggered invite starts the retry count again: a coordinator pressing
            // "Re-invite Now" means start over, not continue chasing.
            var issueResult = await issuer.IssueInitialAsync(
                candidate, 0, actorType, actorId, isReinvite: false, cancellationToken);
            if (issueResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<InviteIssueResult>.Failure(issueResult.Error);
            }

            issued = issueResult.Value;
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<InviteIssueResult>.Failure(Error.Validation(ex.Message));
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<InviteIssueResult>.Failure(
                Error.Conflict("This candidate already has a pending invite."));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        if (issued.DispatchPlan is { } plan)
        {
            var status = await deliveries.DispatchClaimedAsync(
                plan.DeliveryId, plan.Message, cancellationToken, plan.OnSent);

            issued = issued with
            {
                EmailSent = status == EmailStatus.Sent,
                DeliveryStatus = status.ToString(),
            };
        }

        return Result<InviteIssueResult>.Success(issued);
    }
}
`````

## after — src/EventBooking.Application/Invites/TriggerInviteHandler.cs — 1/1

<!-- vocabulary-file: {"id":80,"oldPath":"src/EventBooking.Application/Invites/TriggerInviteHandler.cs","newPath":"src/EventBooking.Application/Invites/TriggerInviteHandler.cs","beforeSha":"10c563fa4ed90355c038e1fc67eb9bb9aab1514bb69c78d71d793168679646d3","afterSha":"20ad137e914004f504a3874ca7afec361e993603c00b66880dfdea3d5c80eea0","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Common;
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Invites;

/// <summary>A null staff identity means the system triggered this, not a person.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="AttendeeId">The attendee id.</param>
public sealed record TriggerInviteCommand(Guid? StaffUserId, Guid AttendeeId);

/// <summary>Issues an invite while serializing all transitions for the attendee lifecycle.</summary>
/// <param name="deliveries">Dispatches the staged invite after commit.</param>
/// <param name="attendees">The attendees.</param>
/// <param name="access">The access.</param>
/// <param name="issuer">The issuer.</param>
/// <param name="unitOfWork">The unit of work.</param>
public sealed class TriggerInviteHandler(
    IAttendeeRepository attendees,
    IStaffAccessAuthorizer access,
    InviteIssuer issuer,
    EmailDeliveryService deliveries,
    IUnitOfWork unitOfWork)
{
    /// <summary>Issues or replaces a attendee invite under the attendee row lock.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<InviteIssueResult>> HandleAsync(
        TriggerInviteCommand command,
        CancellationToken cancellationToken)
    {
        var actorType = ActorType.System;
        string? actorId = null;

        if (command.StaffUserId is not null)
        {
            var authorized = await access.AuthorizeAsync(
                command.StaffUserId.Value,
                StaffCapability.ManageAttendees,
                null,
                cancellationToken);
            if (authorized.IsFailure)
            {
                return Result<InviteIssueResult>.Failure(authorized.Error);
            }

            actorType = ActorType.Staff;
            actorId = command.StaffUserId.Value.ToString();
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var attendee = await attendees.LockForUpdateAsync(command.AttendeeId, cancellationToken);
        if (attendee is null)
        {
            return Result<InviteIssueResult>.Failure(Error.NotFound("No such attendee."));
        }

        if (attendee.Status == AttendeeStatus.Booked)
        {
            return Result<InviteIssueResult>.Failure(Error.Conflict("This attendee is already booked."));
        }

        InviteIssueResult issued;
        try
        {
            // A manually triggered invite starts the retry count again: a coordinator pressing
            // "Re-invite Now" means start over, not continue chasing.
            var issueResult = await issuer.IssueInitialAsync(
                attendee, 0, actorType, actorId, isReinvite: false, cancellationToken);
            if (issueResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<InviteIssueResult>.Failure(issueResult.Error);
            }

            issued = issueResult.Value;
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<InviteIssueResult>.Failure(Error.Validation(ex.Message));
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<InviteIssueResult>.Failure(
                Error.Conflict("This attendee already has a pending invite."));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        if (issued.DispatchPlan is { } plan)
        {
            var status = await deliveries.DispatchClaimedAsync(
                plan.DeliveryId, plan.Message, cancellationToken, plan.OnSent);

            issued = issued with
            {
                EmailSent = status == EmailStatus.Sent,
                DeliveryStatus = status.ToString(),
            };
        }

        return Result<InviteIssueResult>.Success(issued);
    }
}
`````

## before — src/EventBooking.Application/Notifications/CandidateEmailComposer.cs — 1/1

<!-- vocabulary-file: {"id":81,"oldPath":"src/EventBooking.Application/Notifications/CandidateEmailComposer.cs","newPath":"src/EventBooking.Application/Notifications/AttendeeEmailComposer.cs","beforeSha":"b5d4b24ce607a0a38a6d1234ff92708142ae16db05e42bfbd7a8a65844cddcec","afterSha":"0c2d5c34cba4c8c83218eb7e00934f7377f75bf6bb7113ddb55332bbf63ef326","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Application/Notifications/AttendeeEmailComposer.cs — 1/1

<!-- vocabulary-file: {"id":81,"oldPath":"src/EventBooking.Application/Notifications/CandidateEmailComposer.cs","newPath":"src/EventBooking.Application/Notifications/AttendeeEmailComposer.cs","beforeSha":"b5d4b24ce607a0a38a6d1234ff92708142ae16db05e42bfbd7a8a65844cddcec","afterSha":"0c2d5c34cba4c8c83218eb7e00934f7377f75bf6bb7113ddb55332bbf63ef326","side":"after","part":1,"parts":1} -->

`````csharp
using System.Globalization;
using System.Text;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Notifications;

/// <summary>
/// Pure text composition for the 4 attendee emails. No clock, no repository, no mail service —
/// every output is a function of the arguments, so the wording can be asserted in a unit test.
/// </summary>
public static class AttendeeEmailComposer
{
    /// <summary>Formats a four-hour event window using invariant, human-readable wording.</summary>
    /// <param name="window">The window.</param>
    public static string FormatWindow(EventWindow window) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{0:dddd d MMM yyyy}, {1:HH\\:mm}-{2:HH\\:mm}",
            window.Date,
            window.StartTime,
            window.EndTime);

    /// <summary>Composes an initial, reminder, or recovery Invite from persisted type IDs.</summary>
    /// <param name="attendee">The attendee.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    /// <param name="options">The options.</param>
    /// <param name="bookingUrl">The booking url.</param>
    /// <param name="isReinvite">The is reinvite.</param>
    /// <param name="isRecovery">The is recovery.</param>
    public static EmailMessage Invite(
        Attendee attendee,
        IReadOnlyCollection<Guid> appointmentTypeIds,
        IReadOnlyList<Event> options,
        string bookingUrl,
        bool isReinvite,
        bool isRecovery)
    {
        var types = FormatTypes(appointmentTypeIds);

        var text = new StringBuilder();
        text.AppendLine($"Hi {attendee.Name},");
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
            attendee.Id,
            attendee.Email,
            attendee.Name,
            isReinvite ? EmailTemplate.AttendeeReinvite : EmailTemplate.AttendeeInvite,
            isReinvite
                ? "Reminder: choose a time for your appointments"
                : "Choose a time for your appointments",
            text.ToString(),
            AsHtml(text.ToString(), bookingUrl, "Choose your time"));
    }

    /// <summary>Composes confirmation and names every booked snapshot type.</summary>
    /// <param name="attendee">The attendee.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    /// <param name="eventItem">The eventItem.</param>
    /// <param name="manageUrl">The manage url.</param>
    /// <param name="portal">The portal.</param>
    public static EmailMessage BookingConfirmation(
        Attendee attendee,
        IReadOnlyCollection<Guid> appointmentTypeIds,
        Event eventItem,
        string manageUrl,
        AttendeePortalOptions portal)
    {
        var types = FormatTypes(appointmentTypeIds);

        var text = new StringBuilder();
        text.AppendLine($"Hi {attendee.Name},");
        text.AppendLine();
        text.AppendLine("Your appointments are confirmed for:");
        text.AppendLine($"  {FormatWindow(eventItem.Window)}");
        text.AppendLine($"  {portal.TransitionalLocationAddress}");
        text.AppendLine();
        text.AppendLine($"Appointments: {types}");
        text.AppendLine();
        text.AppendLine("Need to change or cancel? Use this link:");
        text.AppendLine(manageUrl);
        text.AppendLine();
        text.AppendLine($"Any questions, contact {portal.CoordinatorContact}.");

        return new EmailMessage(
            attendee.Id,
            attendee.Email,
            attendee.Name,
            EmailTemplate.BookingConfirmation,
            "Your appointment is confirmed",
            text.ToString(),
            AsHtml(text.ToString(), manageUrl, "Cancel or reschedule"));
    }

    /// <summary>
    /// Composes a cancellation notice whose recovery wording reflects whether a replacement invite
    /// was actually delivered. Pending or failed replacement delivery receives neutral wording.
    /// </summary>
    /// <param name="attendee">The attendee.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    /// <param name="eventItem">The eventItem.</param>
    /// <param name="replacementInviteSent">The replacement invite sent.</param>
    public static EmailMessage EventCancelled(
        Attendee attendee,
        IReadOnlyCollection<Guid> appointmentTypeIds,
        Event eventItem,
        bool replacementInviteSent = false)
    {
        var types = FormatTypes(appointmentTypeIds);

        var text = new StringBuilder();
        text.AppendLine($"Hi {attendee.Name},");
        text.AppendLine();
        text.AppendLine(
            $"We are sorry — your {AppointmentNoun(appointmentTypeIds.Count)} on {FormatWindow(eventItem.Window)} has had to be cancelled.");
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
            attendee.Id,
            attendee.Email,
            attendee.Name,
            EmailTemplate.EventCancelledRebookingNeeded,
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

## before — src/EventBooking.Application/Notifications/CandidatePortalOptions.cs — 1/1

<!-- vocabulary-file: {"id":82,"oldPath":"src/EventBooking.Application/Notifications/CandidatePortalOptions.cs","newPath":"src/EventBooking.Application/Notifications/AttendeePortalOptions.cs","beforeSha":"dafc65ead2446dcede5277990d1189e62f4f27eb42e5771b81823ec10c5d0253","afterSha":"974954bce97659c98e38f7f37e8ca31763d46b30ea1c9e756989cdd6f0a18696","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Application/Notifications/AttendeePortalOptions.cs — 1/1

<!-- vocabulary-file: {"id":82,"oldPath":"src/EventBooking.Application/Notifications/CandidatePortalOptions.cs","newPath":"src/EventBooking.Application/Notifications/AttendeePortalOptions.cs","beforeSha":"dafc65ead2446dcede5277990d1189e62f4f27eb42e5771b81823ec10c5d0253","afterSha":"974954bce97659c98e38f7f37e8ca31763d46b30ea1c9e756989cdd6f0a18696","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Application.Notifications;

/// <summary>
/// The handful of deployment-specific strings the attendee-facing emails and pages need. Bound
/// from configuration in Task 55 and injected as a singleton.
/// </summary>
/// <param name="BaseUrl">The base url.</param>
/// <param name="TransitionalLocationAddress">The transitional location address.</param>
/// <param name="CoordinatorContact">The coordinator contact.</param>
public sealed record AttendeePortalOptions(
    string BaseUrl,
    string TransitionalLocationAddress,
    string CoordinatorContact);
`````

## before — src/EventBooking.Application/Notifications/EmailDeliveryService.cs — 1/1

<!-- vocabulary-file: {"id":83,"oldPath":"src/EventBooking.Application/Notifications/EmailDeliveryService.cs","newPath":"src/EventBooking.Application/Notifications/EmailDeliveryService.cs","beforeSha":"f66bca5ee4e834fadb79d4864537aaef0ae8a38c904e8465515b0daa317189fe","afterSha":"e9c1e75743b51751ae456f3a50db948ff9e8f51291ed54c105af3194b3ebd91c","side":"before","part":1,"parts":1} -->

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
