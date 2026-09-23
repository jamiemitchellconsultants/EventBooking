using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;
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
/// <param name="capacities">The capacities.</param>
/// <param name="locations">The locations.</param>
/// <param name="tokens">The tokens.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
/// <param name="clock">The clock.</param>
/// <param name="zones">The zone abstraction the window's start instant is read in.</param>
/// <param name="eligibility">The event eligibility query.</param>
/// <param name="settings">The system settings repository.</param>
/// <param name="issuer">The invite issuer.</param>
public sealed class CancelBookingByAttendeeHandler(
    IBookingRepository bookings,
    IAttendeeRepository attendees,
    IInviteRepository invites,
    IEventRepository events,
    IEventCapacityRepository capacities,
    ILocationRepository locations,
    ITokenService tokens,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock,
    IEventWindowZones zones,
    IEventEligibilityQuery eligibility,
    ISystemSettingsRepository settings,
    IInviteIssuer issuer)
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

        var eventItem = await events.LockForUpdateAsync(booking.EventId, ct);
        if (eventItem is null)
            return Result<AttendeeCancelOutcome>.Failure(Error.NotFound("No such event."));
        var location = await locations.GetAsync(eventItem.LocationId, ct);
        if (location is not null && eventItem.Window.HasStarted(zones, location.TimeZoneId, clock.UtcNow))
            return Result<AttendeeCancelOutcome>.Failure(
                Error.WindowStarted("This event has started and can no longer be cancelled."));

        var required = invite?.RequiredAppointmentTypeIds
            ?? attendee.RequiredAppointmentTypeIds;
        await capacities.LockForUpdateAsync(eventItem.Id, required, ct);

        if (!command.RequestNewTime)
        {
            booking.Cancel();
            eventItem.ReleaseTypes(required);
            attendee.ResetToNotYetInvited(clock.UtcNow);
            audit.Record(AuditEntityTypes.Booking, booking.Id, AuditAction.BookingCancelled,
                ActorType.AttendeeToken, booking.Id.ToString(), "by attendee");
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Result<AttendeeCancelOutcome>.Success(new AttendeeCancelOutcome("cancelled", null));
        }

        var configuration = await settings.GetAsync(ct);
        var originalLocations = invite?.LocationIds ?? [eventItem.LocationId];
        var eligible = await eligibility.CountEligibleEventsAsync(
            required, originalLocations, [eventItem.Id], clock.UtcNow, ct);
        var recoveryWasPending = pendingRecovery?.RecoveryOfBookingId is not null;

        if (eligible < configuration.InviteOptionCount)
        {
            if (recoveryWasPending)
                return Result<AttendeeCancelOutcome>.Success(
                    new AttendeeCancelOutcome("reinvitePending", pendingRecovery!.Id));

            booking.Cancel();
            eventItem.ReleaseTypes(required);
            attendee.ResetToNotYetInvited(clock.UtcNow);
            audit.Record(AuditEntityTypes.Booking, booking.Id, AuditAction.BookingCancelled,
                ActorType.AttendeeToken, booking.Id.ToString(), "by attendee; no eligible events");
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Result<AttendeeCancelOutcome>.Success(new AttendeeCancelOutcome("noEligibleEvents", null));
        }

        booking.Cancel();
        eventItem.ReleaseTypes(required);
        pendingRecovery?.MarkSuperseded();
        var freshEvents = await eligibility.FindEligibleEventsAsync(
            required, originalLocations, [eventItem.Id], configuration.InviteOptionCount,
            clock.UtcNow, ct);
        var issued = await issuer.IssueRecoveryAsync(attendee, booking.Id, required,
            originalLocations, freshEvents, ActorType.AttendeeToken, booking.Id.ToString(), ct);
        if (issued.IsFailure)
        {
            await transaction.RollbackAsync(ct);
            return Result<AttendeeCancelOutcome>.Failure(issued.Error);
        }

        audit.Record(AuditEntityTypes.Booking, booking.Id, AuditAction.BookingCancelled,
            ActorType.AttendeeToken, booking.Id.ToString(), "by attendee; rebooked");
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result<AttendeeCancelOutcome>.Success(new AttendeeCancelOutcome("reinvited", issued.Value.InviteId));
    }
}
