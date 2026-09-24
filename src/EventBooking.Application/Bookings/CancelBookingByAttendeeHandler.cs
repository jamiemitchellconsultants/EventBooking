using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Time;

namespace EventBooking.Application.Bookings;

/// <summary>Cancels the booking the manage token names, optionally asking for a new time.</summary>
/// <param name="ManageToken">The attendee's booking management link.</param>
/// <param name="RequestNewTime">Whether to issue a fresh invite after cancelling.</param>
public sealed record CancelBookingByAttendeeCommand(string ManageToken, bool RequestNewTime);

/// <summary>The truthful outcome: cancelled, reinvited, noEligibleEvents or reinvitePending.</summary>
/// <param name="Outcome">Which of the four outcomes happened.</param>
/// <param name="InviteId">The fresh or still-pending invite, when there is one.</param>
public sealed record AttendeeCancelOutcome(string Outcome, Guid? InviteId);

/// <summary>
/// Cancels the managed booking on the anonymous pipeline, with the four truthful outcomes.
/// The attendee id is port-read first so locks follow the canonical order; with a new time,
/// eligibility is counted before anything mutates (FR-9.6).
/// </summary>
/// <param name="bookings">The bookings.</param>
/// <param name="attendees">The attendees.</param>
/// <param name="invites">The invites.</param>
/// <param name="events">The events.</param>
/// <param name="locations">The locations.</param>
/// <param name="tokens">The tokens.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="clock">The clock.</param>
/// <param name="zones">The zone abstraction the window's start instant is read in.</param>
/// <param name="eligibility">The event eligibility query.</param>
/// <param name="settings">The system settings repository.</param>
/// <param name="issuer">The invite issuer.</param>
/// <param name="bookingCanceller">Cancels a locked booking and releases its own appointment snapshot.</param>
public sealed class CancelBookingByAttendeeHandler(
    IBookingRepository bookings,
    IAttendeeRepository attendees,
    IInviteRepository invites,
    IEventRepository events,
    ILocationRepository locations,
    ITokenService tokens,
    IUnitOfWork unitOfWork,
    IClock clock,
    IEventWindowZones zones,
    IEventEligibilityQuery eligibility,
    ISystemSettingsRepository settings,
    IInviteIssuer issuer,
    BookingCanceller bookingCanceller)
{
    /// <summary>Handles the command.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<AttendeeCancelOutcome>> HandleAsync(
        CancelBookingByAttendeeCommand command, CancellationToken ct)
    {
        if (!tokens.TryRead(command.ManageToken, out var reference)
            || reference.Purpose != TokenPurpose.Manage
            || reference.Version < Booking.InitialManageTokenVersion)
            return Result<AttendeeCancelOutcome>.Failure(
                Error.Validation("This link cannot be used to manage a booking."));

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var portId = await bookings.GetAttendeeIdAsync(reference.EntityId, ct);
        if (portId is null)
            return Result<AttendeeCancelOutcome>.Failure(Error.NotFound("No such booking."));

        var attendee = await attendees.LockForUpdateAsync(portId.Value, ct);
        if (attendee is null)
            return Result<AttendeeCancelOutcome>.Failure(Error.NotFound("No such attendee."));

        // The invite id comes from a second port read, so the invite lock precedes the
        // booking lock in canonical order. Nothing read here is authority: the booking is
        // re-validated under its own lock below.
        var portBooking = await bookings.GetAsync(reference.EntityId, ct);
        if (portBooking is null || portBooking.AttendeeId != attendee.Id)
            return Result<AttendeeCancelOutcome>.Failure(Error.NotFound("No such booking."));

        var invite = await invites.LockForUpdateAsync(portBooking.InviteId, ct);
        var pendingRecovery = await invites.LockPendingForAttendeeAsync(attendee.Id, ct);

        var booking = await bookings.LockForUpdateAsync(reference.EntityId, ct);
        if (booking is null || booking.AttendeeId != attendee.Id)
            return Result<AttendeeCancelOutcome>.Failure(Error.NotFound("No such booking."));
        if (booking.ManageTokenVersion != reference.Version)
            return Result<AttendeeCancelOutcome>.Failure(
                Error.Conflict("This link has been replaced."));
        if (booking.Status != BookingStatus.Active)
            return Result<AttendeeCancelOutcome>.Failure(
                Error.Conflict($"The booking is {booking.Status} and cannot be cancelled."));

        // An original booking's active recovery booking is part of the same journey: it is
        // locked with the original so cancelling the original can never strand its capacity.
        var activeRecovery = booking.IsOriginal
            ? await bookings.LockActiveRecoveryAsync(booking.Id, ct)
            : null;

        var eventIds = new SortedSet<Guid> { booking.EventId };
        if (activeRecovery is not null) eventIds.Add(activeRecovery.EventId);
        var lockedEvents = new Dictionary<Guid, Event>();
        foreach (var lockedEventId in eventIds)
        {
            var locked = await events.LockForUpdateAsync(lockedEventId, ct);
            if (locked is null)
                return Result<AttendeeCancelOutcome>.Failure(Error.NotFound("No such event."));
            lockedEvents[lockedEventId] = locked;
        }

        var eventItem = lockedEvents[booking.EventId];
        foreach (var locked in lockedEvents.Values)
        {
            var lockedLocation = await locations.GetAsync(locked.LocationId, ct);
            if (lockedLocation is not null
                && locked.Window.HasStarted(zones, lockedLocation.TimeZoneId, clock.UtcNow))
                return Result<AttendeeCancelOutcome>.Failure(
                    Error.WindowStarted("This event has started and can no longer be cancelled."));
        }

        // Cancelling a recovery booking leaves the original live: release only its own
        // appointments and keep the attendee Booked.
        if (!booking.IsOriginal)
        {
            var releasedRecovery = await bookingCanceller.CancelLockedAsync(
                booking, eventItem, ActorType.AttendeeToken, booking.Id.ToString(), ct);
            if (releasedRecovery.IsFailure)
                return Result<AttendeeCancelOutcome>.Failure(releasedRecovery.Error);
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Result<AttendeeCancelOutcome>.Success(new AttendeeCancelOutcome("cancelled", null));
        }

        var required = invite?.RequiredAppointmentTypeIds ?? attendee.RequiredAppointmentTypeIds;
        var recoveryWasPending = pendingRecovery?.RecoveryOfBookingId is not null;
        var reinvite = false;
        IReadOnlyList<Guid> originalLocations = invite?.LocationIds ?? [eventItem.LocationId];
        if (command.RequestNewTime)
        {
            var configuration = await settings.GetAsync(ct);
            var eligible = await eligibility.CountEligibleEventsAsync(
                required, originalLocations, [eventItem.Id], clock.UtcNow, ct);
            if (eligible >= configuration.InviteOptionCount)
                reinvite = true;
            else if (recoveryWasPending)
                return Result<AttendeeCancelOutcome>.Success(
                    new AttendeeCancelOutcome("reinvitePending", pendingRecovery!.Id));
        }

        // The issuer supersedes a pending invite itself, so it is superseded here only when
        // no fresh invite follows.
        if (!reinvite && recoveryWasPending) pendingRecovery!.MarkSuperseded();

        if (activeRecovery is not null)
        {
            var releasedActive = await bookingCanceller.CancelLockedAsync(
                activeRecovery, lockedEvents[activeRecovery.EventId],
                ActorType.AttendeeToken, booking.Id.ToString(), ct);
            if (releasedActive.IsFailure)
                return Result<AttendeeCancelOutcome>.Failure(releasedActive.Error);
        }

        var released = await bookingCanceller.CancelLockedAsync(
            booking, eventItem, ActorType.AttendeeToken, booking.Id.ToString(), ct);
        if (released.IsFailure)
            return Result<AttendeeCancelOutcome>.Failure(released.Error);
        attendee.ResetToNotYetInvited(clock.UtcNow);

        if (!reinvite)
        {
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Result<AttendeeCancelOutcome>.Success(new AttendeeCancelOutcome(
                command.RequestNewTime ? "noEligibleEvents" : "cancelled", null));
        }

        // The cancelled booking can never root a recovery (Booking.CreateRecovery needs an
        // active original), so a new time is a fresh initial invite on the same locations.
        var issued = await issuer.IssueInitialAsync(attendee, originalLocations,
            EmailTemplate.AttendeeInvite, ActorType.AttendeeToken, booking.Id.ToString(), ct,
            lockedPending: pendingRecovery is null ? [] : [pendingRecovery]);
        if (issued.IsFailure)
        {
            await transaction.RollbackAsync(ct);
            return Result<AttendeeCancelOutcome>.Failure(issued.Error);
        }

        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result<AttendeeCancelOutcome>.Success(new AttendeeCancelOutcome("reinvited", issued.Value.InviteId));
    }
}
