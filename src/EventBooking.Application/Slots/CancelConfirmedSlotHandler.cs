using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Bookings;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Slots;

/// <summary>Requests cancellation of a confirmed slot and, when authorized, its bookings.</summary>
/// <param name="StaffUserId">The staff identity performing the cancellation.</param>
/// <param name="ConfirmedSlotId">The confirmed slot to cancel.</param>
/// <param name="ConfirmCascade">Whether cancellation of active bookings is authorized.</param>
public sealed record CancelConfirmedSlotCommand(
    Guid StaffUserId,
    Guid ConfirmedSlotId,
    bool ConfirmCascade);

/// <summary>Reports the durable booking and reinvite changes made by slot cancellation.</summary>
/// <param name="BookingsVoided">The number of active bookings transitioned to cancelled.</param>
/// <param name="CandidatesReinvited">The number of affected candidates issued a replacement invite.</param>
public sealed record CancelSlotOutcome(int BookingsVoided, int CandidatesReinvited);

/// <summary>Cancels a confirmed slot while serializing the candidate lifecycle before slot state.</summary>
/// <param name="slots">Loads and locks confirmed-slot rows.</param>
/// <param name="bookings">Reads the affected bookings and candidate-ID worklist.</param>
/// <param name="invites">Locks pending invites before booking rows.</param>
/// <param name="candidates">Locks candidate lifecycle roots and returns tracked candidates.</param>
/// 
/// <param name="bookingCanceller">Releases booking capacity under the slot lock.</param>
/// <param name="issuer">Creates replacement invites after cancellation.</param>
/// <param name="deliveries">Stages and dispatches cancellation and replacement notifications.</param>
/// <param name="audit">Records cancellation and capacity changes.</param>
/// <param name="unitOfWork">Owns the transaction enclosing the lifecycle transitions.</param>
/// <param name="access">The access.</param>
/// <param name="slotFinder">The slot finder.</param>
/// <param name="appointments">The appointments.</param>
/// <param name="clock">The clock.</param>
public sealed class CancelConfirmedSlotHandler(
    IConfirmedSlotRepository slots,
    IBookingRepository bookings,
    IInviteRepository invites,
    ICandidateRepository candidates,
    IStaffAccessAuthorizer access,
    BookingCanceller bookingCanceller,
    InviteIssuer issuer,
    EligibleSlotFinder slotFinder,
    IBookingAppointmentRepository appointments,
    EmailDeliveryService deliveries,
    IAuditLogger audit,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Cancels the slot and its current active bookings, preserving notification and reinvite
    /// behavior while taking candidate lifecycle locks before the confirmed-slot lock.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<CancelSlotOutcome>> HandleAsync(
        CancelConfirmedSlotCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.CancelConfirmedSlot,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<CancelSlotOutcome>.Failure(authorized.Error);
        }

        // This read is only a candidate-id worklist. It is deliberately non-authoritative and
        // no returned booking entity is ever mutated; each identifier is re-read under the
        // candidate lifecycle lock below.
        var affectedCandidateIds = (await bookings.ListActiveCandidateIdsForSlotAsync(
                command.ConfirmedSlotId,
                cancellationToken))
            .Distinct()
            .OrderBy(candidateId => candidateId)
            .ToArray();

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var lockedCandidates = new Dictionary<Guid, Candidate>();
        var lockedPending = new Dictionary<Guid, IReadOnlyList<Invite>>();
        var lockedOriginals = new Dictionary<Guid, Booking?>();
        var lockedRecoveries = new Dictionary<Guid, Booking?>();

        // Every candidate lifecycle transition uses Candidate -> Invite -> Booking (original,
        // then active recovery). Candidate identifiers are sorted so a slot cancellation
        // touching multiple candidates cannot deadlock another slot cancellation that touches
        // the same set in a different order.
        foreach (var candidateId in affectedCandidateIds)
        {
            var candidate = await candidates.LockForUpdateAsync(candidateId, cancellationToken);
            if (candidate is null)
            {
                continue;
            }

            lockedCandidates[candidate.Id] = candidate;
            lockedPending[candidate.Id] = await invites.LockPendingListForCandidateAsync(
                candidate.Id,
                cancellationToken);
            var original = await bookings.LockActiveOriginalForCandidateAsync(
                candidate.Id,
                cancellationToken);
            lockedOriginals[candidate.Id] = original;
            lockedRecoveries[candidate.Id] = original is null
                ? null
                : await bookings.LockActiveRecoveryAsync(original.Id, cancellationToken);
        }

        var slot = await slots.LockForUpdateAsync(command.ConfirmedSlotId, cancellationToken);
        if (slot is null)
        {
            return Result<CancelSlotOutcome>.Failure(Error.NotFound("No such slot."));
        }

        if (slot.Status == ConfirmedSlotStatus.Cancelled)
        {
            return Result<CancelSlotOutcome>.Failure(
                Error.Conflict("This slot has already been cancelled."));
        }

        // A slot can no longer be cancelled once its date has started.
        if (slot.Window.Date < clock.TodayAtHeadOffice)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CancelSlotOutcome>.Failure(Error.Conflict(
                "This appointment has already taken place and can no longer be cancelled."));
        }

        // Re-read after all lifecycle locks and the slot guard. Only the entities returned by the
        // candidate/booking lock calls are eligible for mutation, preventing a stale pre-lock
        // booking instance from being changed.
        var authoritativeBookings = await bookings.ListActiveForSlotAsync(slot.Id, cancellationToken);
        var affected = new List<AffectedJourneyBooking>(authoritativeBookings.Count);
        foreach (var authoritativeBooking in authoritativeBookings)
        {
            if (!lockedCandidates.TryGetValue(authoritativeBooking.CandidateId, out var candidate))
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<CancelSlotOutcome>.Failure(Error.Conflict(
                    "The slot changed while it was being cancelled. Please retry."));
            }

            Booking? locked = null;
            if (lockedOriginals[candidate.Id]?.Id == authoritativeBooking.Id)
            {
                locked = lockedOriginals[candidate.Id];
            }
            else if (lockedRecoveries[candidate.Id]?.Id == authoritativeBooking.Id)
            {
                locked = lockedRecoveries[candidate.Id];
            }

            if (locked is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<CancelSlotOutcome>.Failure(Error.Conflict(
                    "The slot changed while it was being cancelled. Please retry."));
            }

            affected.Add(new AffectedJourneyBooking(candidate, locked));
        }

        affected.Sort((left, right) => left.Booking.Id.CompareTo(right.Booking.Id));

        if (!command.ConfirmCascade && affected.Count > 0)
        {
            return Result<CancelSlotOutcome>.Failure(Error.Conflict(
                $"Cancelling this slot will cancel {affected.Count} confirmed bookings. "
                + "Affected candidates will be notified and re-invited. Confirm to proceed."));
        }

        var actorId = command.StaffUserId.ToString();
        var reinvited = 0;
        var dispatches = new List<SlotCancellationDispatch>();

        try
        {
            // Cancel first: the re-invites below must not be able to offer this slot back.
            slot.Cancel();

            audit.Record(
                AuditEntityTypes.ConfirmedSlot,
                slot.Id,
                AuditAction.SlotCancelled,
                ActorType.Staff,
                actorId,
                $"{affected.Count} bookings voided");

            foreach (var (candidate, booking) in affected)
            {
                if (booking.IsOriginal)
                {
                    var released = await bookingCanceller.CancelLockedAsync(
                        booking,
                        slot,
                        ActorType.Staff,
                        actorId,
                        cancellationToken);
                    if (released.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelSlotOutcome>.Failure(released.Error);
                    }

                    candidate.ResetToNotYetInvited();

                    var issueResult = await issuer.IssueInitialAsync(
                        candidate, 0, ActorType.Staff, actorId, isReinvite: false, cancellationToken);
                    if (issueResult.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelSlotOutcome>.Failure(issueResult.Error);
                    }

                    var issued = issueResult.Value;
                    if (issued.Invited)
                    {
                        reinvited++;
                    }

                    var cancellation = deliveries.StagePending(
                        candidate.Id,
                        EmailTemplate.SlotCancelledRebookingNeeded,
                        bookingId: booking.Id,
                        confirmedSlotId: slot.Id);
                    deliveries.ClaimForDispatch(cancellation);
                    dispatches.Add(new SlotCancellationDispatch(
                        candidate,
                        released.Value,
                        slot,
                        cancellation.Id,
                        issued.DispatchPlan));
                }
                else
                {
                    var releasedRecovery = await bookingCanceller.CancelLockedAsync(
                        booking,
                        slot,
                        ActorType.Staff,
                        actorId,
                        cancellationToken);
                    if (releasedRecovery.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelSlotOutcome>.Failure(releasedRecovery.Error);
                    }

                    var replacementPlan = await TryIssueReplacementRecoveryAsync(
                        candidate,
                        booking,
                        lockedPending[candidate.Id],
                        actorId,
                        cancellationToken);
                    if (replacementPlan.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelSlotOutcome>.Failure(replacementPlan.Error);
                    }

                    if (replacementPlan.Value is not null)
                    {
                        reinvited++;
                    }

                    var recoveryCancellation = deliveries.StagePending(
                        candidate.Id,
                        EmailTemplate.SlotCancelledRebookingNeeded,
                        bookingId: booking.Id,
                        confirmedSlotId: slot.Id);
                    deliveries.ClaimForDispatch(recoveryCancellation);
                    dispatches.Add(new SlotCancellationDispatch(
                        candidate,
                        releasedRecovery.Value,
                        slot,
                        recoveryCancellation.Id,
                        replacementPlan.Value));
                }
            }
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CancelSlotOutcome>.Failure(Error.Conflict(ex.Message));
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

        foreach (var dispatch in dispatches)
        {
            var replacementSent = false;
            if (dispatch.InvitePlan is { } invitePlan)
            {
                var inviteStatus = await deliveries.DispatchClaimedAsync(
                    invitePlan.DeliveryId, invitePlan.Message, cancellationToken, invitePlan.OnSent);
                replacementSent = inviteStatus == EmailStatus.Sent;
            }

            await deliveries.DispatchClaimedAsync(
                dispatch.CancellationDeliveryId,
                CandidateEmailComposer.SlotCancelled(
                    dispatch.Candidate,
                    dispatch.AppointmentTypeIds,
                    dispatch.Slot,
                    replacementSent),
                cancellationToken);
        }

        return Result<CancelSlotOutcome>.Success(new CancelSlotOutcome(affected.Count, reinvited));
    }

    /// <summary>
    /// Issues a replacement recovery Invite for the cancelled recovery's still-outstanding types,
    /// or returns no plan when three options are unavailable so the Coordinator can act later.
    /// </summary>
    private async Task<Result<EmailDispatchPlan?>> TryIssueReplacementRecoveryAsync(
        Candidate candidate,
        Booking booking,
        IReadOnlyList<Invite> pending,
        string actorId,
        CancellationToken cancellationToken)
    {
        var rootId = booking.RecoveryOfBookingId!.Value;
        var journey = await bookings.ListJourneyAsync(rootId, cancellationToken);
        var rows = await appointments.ListForBookingsAsync(
            journey.Select(entry => entry.Id).ToList(),
            cancellationToken);
        var covered = pending
            .Where(invite => invite.RecoveryOfBookingId.HasValue)
            .SelectMany(invite => invite.RequiredAppointmentTypeIds)
            .Distinct()
            .ToList();
        var selected = new RecoveryRequirementSelector().Select(
            candidate.RequiredAppointmentTypeIds,
            RecoveryConfirmationValidator.BuildAttempts(journey, rows),
            covered);

        if (selected.Count == 0)
        {
            return Result<EmailDispatchPlan?>.Success(null);
        }

        var options = await slotFinder.FindAsync(
            selected,
            Invite.RequiredOptionCount,
            [],
            cancellationToken);
        if (options.Count < Invite.RequiredOptionCount)
        {
            return Result<EmailDispatchPlan?>.Success(null);
        }

        var replacement = await issuer.IssueRecoveryAsync(
            candidate,
            rootId,
            selected,
            options,
            ActorType.Staff,
            actorId,
            cancellationToken);
        if (replacement.IsFailure)
        {
            return Result<EmailDispatchPlan?>.Failure(replacement.Error);
        }

        return Result<EmailDispatchPlan?>.Success(replacement.Value.DispatchPlan);
    }
}

/// <summary>One locked candidate and the locked journey Booking cancelled with the slot.</summary>
/// <param name="Candidate">The lifecycle-locked candidate.</param>
/// <param name="Booking">The locked original or recovery Booking on the cancelled slot.</param>
internal sealed record AffectedJourneyBooking(Candidate Candidate, Booking Booking);

internal sealed record SlotCancellationDispatch(
    Candidate Candidate,
    IReadOnlyList<Guid> AppointmentTypeIds,
    ConfirmedSlot Slot,
    Guid CancellationDeliveryId,
    EmailDispatchPlan? InvitePlan);
