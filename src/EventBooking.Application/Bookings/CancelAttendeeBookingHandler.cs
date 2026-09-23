using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Bookings;

/// <summary>Requests staff cancellation of one attendee's booking.</summary>
/// <param name="StaffUserId">The coordinator performing the cancellation; recorded as the audit actor.</param>
/// <param name="AttendeeId">The attendee the booking must belong to.</param>
/// <param name="BookingId">The booking to cancel.</param>
/// <param name="Rebook">
/// Whether to issue a replacement invite. Applies only to an original booking; rebooking a
/// cancelled recovery booking is the recovery path's job and is refused here.
/// </param>
public sealed record CancelAttendeeBookingCommand(
    Guid StaffUserId,
    Guid AttendeeId,
    Guid BookingId,
    bool Rebook);

/// <summary>
/// Cancels one attendee booking on a coordinator's behalf, reusing the attendee self-service
/// cancellation path so capacity release, recovery cascade, and audit semantics stay identical.
/// </summary>
/// <param name="access">Authorizes attendee management before anything is read or locked.</param>
/// <param name="bookings">Locks and re-reads the targeted booking and any active recovery.</param>
/// <param name="events">Locks every event whose capacity is released.</param>
/// <param name="attendees">Locks the attendee lifecycle root.</param>
/// <param name="invites">Locks pending invites so recovery invites can be superseded.</param>
/// <param name="bookingCanceller">Performs the cancellation, capacity release, and audit write.</param>
/// <param name="issuer">Issues the optional replacement invite.</param>
/// <param name="deliveries">Dispatches a staged replacement invite after commit.</param>
/// <param name="unitOfWork">Owns the transaction enclosing the lifecycle transitions.</param>
/// <param name="clock">The clock.</param>
public sealed class CancelAttendeeBookingHandler(
    IStaffAccessAuthorizer access,
    IBookingRepository bookings,
    IEventRepository events,
    IAttendeeRepository attendees,
    IInviteRepository invites,
    BookingCanceller bookingCanceller,
    InviteIssuer issuer,
    EmailDeliveryService deliveries,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    private const string NoSuchBooking = "No such active booking for this attendee.";

    /// <summary>
    /// Cancels the targeted booking under the attendee lifecycle lock order, cascading onto an
    /// active recovery when the target is the original, and optionally re-inviting the attendee.
    /// </summary>
    /// <param name="command">The coordinator's cancellation request.</param>
    /// <param name="cancellationToken">Cancels the authorization, locks, and dispatch.</param>
    /// <returns>
    /// The cancellation outcome, a forbidden failure when the caller lacks attendee management,
    /// a not-found failure for an unknown or already-inactive booking, or a conflict when a
    /// replacement invite is requested for a recovery booking.
    /// </returns>
    public async Task<Result<CancelBookingOutcome>> HandleAsync(
        CancelAttendeeBookingCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageAttendees,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<CancelBookingOutcome>.Failure(authorized.Error);
        }

        var actorId = command.StaffUserId.ToString();

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var attendee = await attendees.LockForUpdateAsync(command.AttendeeId, cancellationToken);
        if (attendee is null)
        {
            return Result<CancelBookingOutcome>.Failure(Error.NotFound(NoSuchBooking));
        }

        var pending = await invites.LockPendingListForAttendeeAsync(attendee.Id, cancellationToken);

        var booking = await bookings.LockByIdForAttendeeAsync(
            command.BookingId, attendee.Id, cancellationToken);
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

        var eventIds = new List<Guid> { booking.EventId };
        if (activeRecovery is not null && activeRecovery.EventId != booking.EventId)
        {
            eventIds.Add(activeRecovery.EventId);
        }

        eventIds.Sort();
        var lockedEvents = new Dictionary<Guid, Event>();
        foreach (var eventId in eventIds)
        {
            var locked = await events.LockForUpdateAsync(eventId, cancellationToken);
            if (locked is null)
            {
                return Result<CancelBookingOutcome>.Failure(Error.NotFound(NoSuchBooking));
            }

            lockedEvents[eventId] = locked;
        }

        var eventItem = lockedEvents[booking.EventId];

        // A booking can no longer be cancelled once its event date has started.
        if (lockedEvents.Values.Any(locked => locked.Window.Date < clock.TodayAtTransitionalLocation))
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
                    eventItem,
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
                        lockedEvents[activeRecovery.EventId],
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
                    eventItem,
                    ActorType.Staff,
                    actorId,
                    cancellationToken);
                if (released.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<CancelBookingOutcome>.Failure(released.Error);
                }

                attendee.ResetToNotYetInvited();

                if (command.Rebook)
                {
                    var issueResult = await issuer.IssueInitialAsync(
                        attendee,
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
