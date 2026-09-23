using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Bookings;

/// <summary>Defines cancel booking command for the current use case.</summary>
/// <param name="ManageToken">The manage token.</param>
/// <param name="Rebook">The rebook.</param>
public sealed record CancelBookingCommand(string? ManageToken, bool Rebook);

/// <summary>Reports cancellation and any replacement-invite delivery outcome.</summary>
/// <param name="Reinvited">Whether a replacement invite was created.</param>
/// <param name="InviteCreated">The explicit replacement-invite creation state.</param>
/// <param name="DeliveryStatus">The provider outcome, or <c>Unavailable</c> when no invite exists.</param>
/// <param name="DeliveryId">The durable replacement delivery identifier, when one was staged.</param>
public sealed record CancelBookingOutcome(
    bool Reinvited,
    bool InviteCreated = false,
    string? DeliveryStatus = null,
    Guid? DeliveryId = null);

/// <summary>Cancels or rebooks under the attendee lifecycle lock before releasing event capacity.</summary>
/// <param name="deliveries">Stages and dispatches replacement invites after commit.</param>
/// <param name="bookings">The bookings.</param>
/// <param name="events">The events.</param>
/// <param name="attendees">The attendees.</param>
/// <param name="invites">The invites.</param>
/// <param name="bookingCanceller">The booking canceller.</param>
/// <param name="issuer">The issuer.</param>
/// <param name="tokens">The tokens.</param>
/// <param name="clock">The clock.</param>
/// <param name="unitOfWork">The unit of work.</param>
public sealed class CancelBookingHandler(
    IBookingRepository bookings,
    IEventRepository events,
    IAttendeeRepository attendees,
    IInviteRepository invites,
    BookingCanceller bookingCanceller,
    LegacyInviteIssuer issuer,
    EmailDeliveryService deliveries,
    ITokenService tokens,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    /// <summary>Cancels the managed booking and optionally issues a replacement invite atomically.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<CancelBookingOutcome>> HandleAsync(
        CancelBookingCommand command,
        CancellationToken cancellationToken)
    {
        if (!tokens.TryRead(command.ManageToken, out var link) || link.Purpose != TokenPurpose.Manage)
        {
            return Result<CancelBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        var eventId = await bookings.GetEventIdAsync(link.EntityId, cancellationToken);
        var attendeeId = await bookings.GetAttendeeIdAsync(link.EntityId, cancellationToken);
        if (eventId is null || attendeeId is null)
        {
            return Result<CancelBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var attendee = await attendees.LockForUpdateAsync(attendeeId.Value, cancellationToken);
        if (attendee is null)
        {
            return Result<CancelBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        var pending = await invites.LockPendingListForAttendeeAsync(attendee.Id, cancellationToken);

        var booking = await bookings.LockForUpdateAsync(link.EntityId, cancellationToken);

        if (booking is null
            || booking.ManageTokenVersion != link.Version
            || booking.Status != BookingStatus.Active)
        {
            return Result<CancelBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        if (booking.AttendeeId != attendee.Id)
        {
            return Result<CancelBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        Booking? activeRecovery = null;
        if (booking.IsOriginal)
        {
            activeRecovery = await bookings.LockActiveRecoveryAsync(booking.Id, cancellationToken);
        }

        var eventIds = new List<Guid> { eventId.Value };
        if (activeRecovery is not null && activeRecovery.EventId != eventId.Value)
        {
            eventIds.Add(activeRecovery.EventId);
        }

        eventIds.Sort();
        var lockedEvents = new Dictionary<Guid, Event>();
        foreach (var lockedEventId in eventIds)
        {
            var locked = await events.LockForUpdateAsync(lockedEventId, cancellationToken);
            if (locked is null)
            {
                return Result<CancelBookingOutcome>.Failure(
                    Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
            }

            lockedEvents[lockedEventId] = locked;
        }

        var eventItem = lockedEvents[eventId.Value];

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
                    ActorType.AttendeeToken,
                    booking.Id.ToString(),
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
                        ActorType.AttendeeToken,
                        booking.Id.ToString(),
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
                    ActorType.AttendeeToken,
                    booking.Id.ToString(),
                    cancellationToken);
                if (released.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<CancelBookingOutcome>.Failure(released.Error);
                }

                attendee.ResetToNotYetInvited(clock.UtcNow);

                if (command.Rebook)
                {
                    var issueResult = await issuer.IssueInitialAsync(
                        attendee,
                        0,
                        ActorType.AttendeeToken,
                        booking.Id.ToString(),
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
