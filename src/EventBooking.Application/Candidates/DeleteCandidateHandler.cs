using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Bookings;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;

namespace EventBooking.Application.Candidates;

/// <summary>Defines delete candidate command for the current use case.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="CandidateId">The candidate id.</param>
/// <param name="ConfirmCascade">The confirm cascade.</param>
public sealed record DeleteCandidateCommand(Guid StaffUserId, Guid CandidateId, bool ConfirmCascade);

/// <summary>Deletes a candidate only after serializing and reconciling their current lifecycle rows.</summary>
/// <param name="candidates">The candidates.</param>
/// <param name="invites">The invites.</param>
/// <param name="bookings">The bookings.</param>
/// <param name="slots">The slots.</param>
/// <param name="access">The access.</param>
/// <param name="bookingCanceller">The booking canceller.</param>
/// <param name="audit">The audit.</param>
/// <param name="unitOfWork">The unit of work.</param>
public sealed class DeleteCandidateHandler(
    ICandidateRepository candidates,
    IInviteRepository invites,
    IBookingRepository bookings,
    IConfirmedSlotRepository slots,
    IStaffAccessAuthorizer access,
    BookingCanceller bookingCanceller,
    IAuditLogger audit,
    IUnitOfWork unitOfWork)
{
    /// <summary>Deletes the candidate and releases their active booking after confirmed cascade authorization.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result> HandleAsync(
        DeleteCandidateCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageCandidates,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result.Failure(authorized.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        // Candidate is the lifecycle root. Every authoritative cascade read occurs only after its
        // row lock is held, then follows Candidate -> Invite -> Booking.
        var candidate = await candidates.LockForUpdateAsync(command.CandidateId, cancellationToken);
        if (candidate is null)
        {
            return Result.Failure(Error.NotFound("No such candidate."));
        }

        var invite = await invites.LockPendingForCandidateAsync(candidate.Id, cancellationToken);
        var booking = await bookings.LockActiveOriginalForCandidateAsync(candidate.Id, cancellationToken);

        // An active recovery booking holds its own slot capacity and is invisible to the
        // original-only lookup above, so it is locked and cascaded here as well.
        var activeRecovery = booking is not null && booking.IsOriginal
            ? await bookings.LockActiveRecoveryAsync(booking.Id, cancellationToken)
            : null;

        var bookingCount = (booking is null ? 0 : 1) + (activeRecovery is null ? 0 : 1);
        var inviteCount = invite is null ? 0 : 1;

        if (!command.ConfirmCascade && bookingCount + inviteCount > 0)
        {
            return Result.Failure(Error.Conflict(
                $"Deleting this candidate will cancel {bookingCount} booking and {inviteCount} pending invite, "
                + "and free the capacity they hold. Confirm to proceed."));
        }

        var actorId = command.StaffUserId.ToString();

        try
        {
            if (activeRecovery is not null)
            {
                var recoverySlot = activeRecovery.ConfirmedSlotId == booking!.ConfirmedSlotId
                    ? await slots.LockForUpdateAsync(booking.ConfirmedSlotId, cancellationToken)
                    : await slots.LockForUpdateAsync(activeRecovery.ConfirmedSlotId, cancellationToken);
                if (recoverySlot is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure(Error.NotFound("No such slot."));
                }

                var releasedRecovery = await bookingCanceller.CancelLockedAsync(
                    activeRecovery,
                    recoverySlot,
                    ActorType.Staff,
                    actorId,
                    cancellationToken);
                if (releasedRecovery.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure(releasedRecovery.Error);
                }
            }

            if (booking is not null)
            {
                var slot = await slots.LockForUpdateAsync(booking.ConfirmedSlotId, cancellationToken);
                if (slot is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure(Error.NotFound("No such slot."));
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
                    return Result.Failure(released.Error);
                }
            }

            if (invite is not null)
            {
                invite.MarkSuperseded();
            }

            audit.Record(
                AuditEntityTypes.Candidate,
                candidate.Id,
                AuditAction.CandidateDeleted,
                ActorType.Staff,
                actorId,
                null);

            candidates.Remove(candidate);
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict(ex.Message));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
