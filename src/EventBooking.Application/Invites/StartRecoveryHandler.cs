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
