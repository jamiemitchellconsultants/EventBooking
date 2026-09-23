using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Bookings;

/// <summary>Requests staff cancellation of one candidate's booking.</summary>
/// <param name="StaffUserId">The coordinator performing the cancellation; recorded as the audit actor.</param>
/// <param name="CandidateId">The candidate the booking must belong to.</param>
/// <param name="BookingId">The booking to cancel.</param>
/// <param name="Rebook">
/// Whether to issue a replacement invite. Applies only to an original booking; rebooking a
/// cancelled recovery booking is the recovery path's job and is refused here.
/// </param>
public sealed record CancelCandidateBookingCommand(
    Guid StaffUserId,
    Guid CandidateId,
    Guid BookingId,
    bool Rebook);

/// <summary>
/// Cancels one candidate booking on a coordinator's behalf, reusing the candidate self-service
/// cancellation path so capacity release, recovery cascade, and audit semantics stay identical.
/// </summary>
/// <param name="access">Authorizes candidate management before anything is read or locked.</param>
/// <param name="bookings">Locks and re-reads the targeted booking and any active recovery.</param>
/// <param name="slots">Locks every confirmed slot whose capacity is released.</param>
/// <param name="candidates">Locks the candidate lifecycle root.</param>
/// <param name="invites">Locks pending invites so recovery invites can be superseded.</param>
/// <param name="bookingCanceller">Performs the cancellation, capacity release, and audit write.</param>
/// <param name="issuer">Issues the optional replacement invite.</param>
/// <param name="deliveries">Dispatches a staged replacement invite after commit.</param>
/// <param name="unitOfWork">Owns the transaction enclosing the lifecycle transitions.</param>
/// <param name="clock">The clock.</param>
public sealed class CancelCandidateBookingHandler(
    IStaffAccessAuthorizer access,
    IBookingRepository bookings,
    IConfirmedSlotRepository slots,
    ICandidateRepository candidates,
    IInviteRepository invites,
    BookingCanceller bookingCanceller,
    InviteIssuer issuer,
    EmailDeliveryService deliveries,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    private const string NoSuchBooking = "No such active booking for this candidate.";

    /// <summary>
    /// Cancels the targeted booking under the candidate lifecycle lock order, cascading onto an
    /// active recovery when the target is the original, and optionally re-inviting the candidate.
    /// </summary>
    /// <param name="command">The coordinator's cancellation request.</param>
    /// <param name="cancellationToken">Cancels the authorization, locks, and dispatch.</param>
    /// <returns>
    /// The cancellation outcome, a forbidden failure when the caller lacks candidate management,
    /// a not-found failure for an unknown or already-inactive booking, or a conflict when a
    /// replacement invite is requested for a recovery booking.
    /// </returns>
    public async Task<Result<CancelBookingOutcome>> HandleAsync(
        CancelCandidateBookingCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageCandidates,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<CancelBookingOutcome>.Failure(authorized.Error);
        }

        var actorId = command.StaffUserId.ToString();

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var candidate = await candidates.LockForUpdateAsync(command.CandidateId, cancellationToken);
        if (candidate is null)
        {
            return Result<CancelBookingOutcome>.Failure(Error.NotFound(NoSuchBooking));
        }

        var pending = await invites.LockPendingListForCandidateAsync(candidate.Id, cancellationToken);

        var booking = await bookings.LockByIdForCandidateAsync(
            command.BookingId, candidate.Id, cancellationToken);
        if (booking is null || booking.Status != BookingStatus.Active)
        {
            return Result<CancelBookingOutcome>.Failure(Error.NotFound(NoSuchBooking));
        }

        if (!booking.IsOriginal && command.Rebook)
        {
            return Result<CancelBookingOutcome>.Failure(Error.Conflict(
                "A recovery booking cannot be cancelled and rebooked. "
                + "Cancel it, then arrange the missed appointments again."));
        }

        Booking? activeRecovery = null;
        if (booking.IsOriginal)
        {
            activeRecovery = await bookings.LockActiveRecoveryAsync(booking.Id, cancellationToken);
        }

        var slotIds = new List<Guid> { booking.ConfirmedSlotId };
        if (activeRecovery is not null && activeRecovery.ConfirmedSlotId != booking.ConfirmedSlotId)
        {
            slotIds.Add(activeRecovery.ConfirmedSlotId);
        }

        slotIds.Sort();
        var lockedSlots = new Dictionary<Guid, ConfirmedSlot>();
        foreach (var slotId in slotIds)
        {
            var locked = await slots.LockForUpdateAsync(slotId, cancellationToken);
            if (locked is null)
            {
                return Result<CancelBookingOutcome>.Failure(Error.NotFound(NoSuchBooking));
            }

            lockedSlots[slotId] = locked;
        }

        var slot = lockedSlots[booking.ConfirmedSlotId];

        // A booking can no longer be cancelled once its slot date has started.
        if (lockedSlots.Values.Any(locked => locked.Window.Date < clock.TodayAtHeadOffice))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CancelBookingOutcome>.Failure(Error.Conflict(
                "This appointment has already taken place and can no longer be cancelled."));
        }

        var reinvited = false;
        InviteIssueResult? issued = null;

        try
        {
            if (!booking.IsOriginal)
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
                    return Result<CancelBookingOutcome>.Failure(releasedRecovery.Error);
                }
            }
            else
            {
                foreach (var pendingRecovery in pending.Where(invite => invite.RecoveryOfBookingId.HasValue && invite.Status == Domain.Invites.InviteStatus.Pending))
                {
                    pendingRecovery.MarkSuperseded();
                }

                if (activeRecovery is not null)
                {
                    var releasedActiveRecovery = await bookingCanceller.CancelLockedAsync(
                        activeRecovery,
                        lockedSlots[activeRecovery.ConfirmedSlotId],
                        ActorType.Staff,
                        actorId,
                        cancellationToken);
                    if (releasedActiveRecovery.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelBookingOutcome>.Failure(releasedActiveRecovery.Error);
                    }
                }

                var released = await bookingCanceller.CancelLockedAsync(
                    booking,
                    slot,
                    ActorType.Staff,
                    actorId,
                    cancellationToken);
                if (released.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<CancelBookingOutcome>.Failure(released.Error);
                }

                candidate.ResetToNotYetInvited();

                if (command.Rebook)
                {
                    var issueResult = await issuer.IssueInitialAsync(
                        candidate,
                        0,
                        ActorType.Staff,
                        actorId,
                        isReinvite: false,
                        cancellationToken);
                    if (issueResult.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelBookingOutcome>.Failure(issueResult.Error);
                    }

                    issued = issueResult.Value;
                    reinvited = issued.Invited;
                }
            }
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CancelBookingOutcome>.Failure(Error.Conflict(ex.Message));
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

        if (issued?.DispatchPlan is { } plan)
        {
            var status = await deliveries.DispatchClaimedAsync(
                plan.DeliveryId, plan.Message, cancellationToken, plan.OnSent);

            return Result<CancelBookingOutcome>.Success(
                new CancelBookingOutcome(
                    reinvited,
                    InviteCreated: reinvited,
                    status.ToString(),
                    plan.DeliveryId));
        }

        return Result<CancelBookingOutcome>.Success(
            new CancelBookingOutcome(reinvited, InviteCreated: reinvited, "Unavailable"));
    }
}
