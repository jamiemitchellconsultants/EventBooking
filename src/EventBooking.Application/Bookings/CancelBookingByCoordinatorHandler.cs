using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;
using EventBooking.Domain.Time;

namespace EventBooking.Application.Bookings;

/// <summary>Cancels one attendee booking in two steps: preview, then confirm.</summary>
/// <param name="StaffUserId">The coordinator performing the cancellation.</param>
/// <param name="AttendeeId">The attendee the booking must belong to.</param>
/// <param name="BookingId">The booking to cancel.</param>
/// <param name="Confirm">Whether this call carries the confirmation.</param>
public sealed record CancelBookingByCoordinatorCommand(
    Guid StaffUserId, Guid AttendeeId, Guid BookingId, bool Confirm);

/// <summary>Preview consequence or confirmed cancellation, never both.</summary>
/// <param name="ConfirmationRequired">True on the preview call, with the active-booking count.</param>
/// <param name="ActiveBookingCount">How many active bookings the attendee holds (preview only).</param>
/// <param name="CancelledBookingId">The cancelled booking (confirmed call only).</param>
public sealed record CoordinatorCancelOutcome(
    bool ConfirmationRequired, int ActiveBookingCount, Guid? CancelledBookingId);

/// <summary>Cancels one attendee booking for a coordinator, in a preview then confirm two-step.</summary>
/// <param name="bookings">The bookings.</param>
/// <param name="attendees">The attendees.</param>
/// <param name="invites">The invites.</param>
/// <param name="events">The events.</param>
/// <param name="capacities">The capacities.</param>
/// <param name="locations">The locations.</param>
/// <param name="access">The staff access authorizer.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
/// <param name="clock">The clock.</param>
/// <param name="zones">The zone abstraction the window's start instant is read in.</param>
public sealed class CancelBookingByCoordinatorHandler(
    IBookingRepository bookings,
    IAttendeeRepository attendees,
    IInviteRepository invites,
    IEventRepository events,
    IEventCapacityRepository capacities,
    ILocationRepository locations,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock,
    IEventWindowZones zones)
{
    /// <summary>Handles the command.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<CoordinatorCancelOutcome>> HandleAsync(
        CancelBookingByCoordinatorCommand command, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId, StaffCapability.ManageAttendees, null, ct);
        if (authorized.IsFailure) return Result<CoordinatorCancelOutcome>.Failure(authorized.Error);

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var attendee = await attendees.LockForUpdateAsync(command.AttendeeId, ct);
        if (attendee is null)
            return Result<CoordinatorCancelOutcome>.Failure(Error.NotFound("No such attendee."));

        // The invite id comes from a port read, so the invite lock precedes the booking
        // lock in canonical order. The booking is re-validated under its own lock below.
        var portBooking = await bookings.GetAsync(command.BookingId, ct);
        if (portBooking is null || portBooking.AttendeeId != attendee.Id)
            return Result<CoordinatorCancelOutcome>.Failure(Error.NotFound("No such booking."));

        var invite = await invites.LockForUpdateAsync(portBooking.InviteId, ct);

        var booking = await bookings.LockByIdForAttendeeAsync(command.BookingId, command.AttendeeId, ct);
        if (booking is null)
            return Result<CoordinatorCancelOutcome>.Failure(Error.NotFound("No such booking."));
        if (booking.Status != BookingStatus.Active)
            return Result<CoordinatorCancelOutcome>.Failure(
                Error.Conflict($"The booking is {booking.Status} and cannot be cancelled."));

        if (!command.Confirm)
        {
            var activeCount = await bookings.CountActiveForAttendeeAsync(attendee.Id, ct);
            await transaction.CommitAsync(ct);
            return Result<CoordinatorCancelOutcome>.Success(
                new CoordinatorCancelOutcome(true, activeCount, null));
        }

        var eventItem = await events.LockForUpdateAsync(booking.EventId, ct);
        if (eventItem is null)
            return Result<CoordinatorCancelOutcome>.Failure(Error.NotFound("No such event."));
        var location = await locations.GetAsync(eventItem.LocationId, ct);
        if (location is not null && eventItem.Window.HasStarted(zones, location.TimeZoneId, clock.UtcNow))
            return Result<CoordinatorCancelOutcome>.Failure(
                Error.WindowStarted("This event has started and can no longer be cancelled."));

        var required = invite?.RequiredAppointmentTypeIds
            ?? attendee.RequiredAppointmentTypeIds;
        await capacities.LockForUpdateAsync(eventItem.Id, required, ct);

        booking.Cancel();
        eventItem.ReleaseTypes(required);
        attendee.ResetToNotYetInvited(clock.UtcNow);
        audit.Record(AuditEntityTypes.Booking, booking.Id, AuditAction.BookingCancelled,
            ActorType.Staff, command.StaffUserId.ToString(), "by coordinator");
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result<CoordinatorCancelOutcome>.Success(
            new CoordinatorCancelOutcome(false, 0, booking.Id));
    }
}
