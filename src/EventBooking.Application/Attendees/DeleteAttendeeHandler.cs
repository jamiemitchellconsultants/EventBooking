using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Bookings;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Attendees;

/// <summary>Defines delete attendee command for the current use case.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="AttendeeId">The attendee id.</param>
/// <param name="ConfirmCascade">The confirm cascade.</param>
public sealed record DeleteAttendeeCommand(Guid StaffUserId, Guid AttendeeId, bool ConfirmCascade);

/// <summary>Deletes a attendee only after serializing and reconciling their current lifecycle rows.</summary>
/// <param name="attendees">The attendees.</param>
/// <param name="invites">The invites.</param>
/// <param name="bookings">The bookings.</param>
/// <param name="events">The events.</param>
/// <param name="access">The access.</param>
/// <param name="bookingCanceller">The booking canceller.</param>
/// <param name="audit">The audit.</param>
/// <param name="unitOfWork">The unit of work.</param>
public sealed class DeleteAttendeeHandler(
    IAttendeeRepository attendees,
    IInviteRepository invites,
    IBookingRepository bookings,
    IEventRepository events,
    IStaffAccessAuthorizer access,
    BookingCanceller bookingCanceller,
    IAuditLogger audit,
    IUnitOfWork unitOfWork)
{
    /// <summary>Deletes the attendee and releases their active booking after confirmed cascade authorization.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result> HandleAsync(
        DeleteAttendeeCommand command,
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

        // Attendee is the lifecycle root. Every authoritative cascade read occurs only after its
        // row lock is held, then follows Attendee -> Invite -> Booking.
        var attendee = await attendees.LockForUpdateAsync(command.AttendeeId, cancellationToken);
        if (attendee is null)
        {
            return Result.Failure(Error.NotFound("No such attendee."));
        }

        var invite = await invites.LockPendingForAttendeeAsync(attendee.Id, cancellationToken);
        var booking = await bookings.LockActiveOriginalForAttendeeAsync(attendee.Id, cancellationToken);

        // An active recovery booking holds its own event capacity and is invisible to the
        // original-only lookup above, so it is locked and cascaded here as well.
        var activeRecovery = booking is not null && booking.IsOriginal
            ? await bookings.LockActiveRecoveryAsync(booking.Id, cancellationToken)
            : null;

        var bookingCount = (booking is null ? 0 : 1) + (activeRecovery is null ? 0 : 1);
        var inviteCount = invite is null ? 0 : 1;

        if (!command.ConfirmCascade && bookingCount + inviteCount > 0)
        {
            return Result.Failure(Error.Conflict(
                $"Deleting this attendee will cancel {bookingCount} booking and {inviteCount} pending invite, "
                + "and free the capacity they hold. Confirm to proceed."));
        }

        var actorId = command.StaffUserId.ToString();

        try
        {
            // Both events are locked in ascending id order before either booking is cancelled,
            // the same order staff cancellation takes them in. Locking the recovery event first
            // would acquire the same two rows in the opposite order and deadlock against it.
            var eventIds = new List<Guid>();
            if (booking is not null)
            {
                eventIds.Add(booking.EventId);
                if (activeRecovery is not null && activeRecovery.EventId != booking.EventId)
                {
                    eventIds.Add(activeRecovery.EventId);
                }
            }

            eventIds.Sort();
            var lockedEvents = new Dictionary<Guid, Event>();
            foreach (var eventId in eventIds)
            {
                var locked = await events.LockForUpdateAsync(eventId, cancellationToken);
                if (locked is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure(Error.NotFound("No such eventItem."));
                }

                lockedEvents[eventId] = locked;
            }

            if (activeRecovery is not null)
            {
                var releasedRecovery = await bookingCanceller.CancelLockedAsync(
                    activeRecovery,
                    lockedEvents[activeRecovery.EventId],
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
                var released = await bookingCanceller.CancelLockedAsync(
                    booking,
                    lockedEvents[booking.EventId],
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
                AuditEntityTypes.Attendee,
                attendee.Id,
                AuditAction.AttendeeDeleted,
                ActorType.Staff,
                actorId,
                null);

            attendees.Remove(attendee);
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
