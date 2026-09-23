using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Bookings;

/// <summary>
/// Voids one booking and returns the capacity it held, under a row lock on the capacity rows.
/// Shared by attendee deletion (Task 32), attendee cancellation (Task 41) and event cancellation
/// (Task 42) — all three release capacity in exactly the same way, and a second implementation
/// would be a second chance to get the locking wrong.
/// </summary>
/// <param name="appointments">The appointments.</param>
/// <param name="capacities">The capacities.</param>
/// <param name="audit">The audit.</param>
public sealed class BookingCanceller(
    IBookingAppointmentRepository appointments,
    IEventCapacityRepository capacities,
    IAuditLogger audit)
{
    /// <summary>Cancels one locked Booking and returns capacity for its own Appointment snapshot.</summary>
    /// <param name="booking">The booking.</param>
    /// <param name="eventItem">The eventItem.</param>
    /// <param name="actorType">The actor type.</param>
    /// <param name="actorId">The actor id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<IReadOnlyList<Guid>>> CancelLockedAsync(
        Booking booking,
        Event eventItem,
        ActorType actorType,
        string? actorId,
        CancellationToken cancellationToken)
    {
        var snapshot = (await appointments.ListForBookingAsync(booking.Id, cancellationToken))
            .Select(appointment => appointment.AppointmentTypeId)
            .Distinct()
            .Order()
            .ToList();

        if (snapshot.Count == 0)
        {
            return Result<IReadOnlyList<Guid>>.Failure(
                Error.Conflict("The booking has no appointments to release."));
        }

        IReadOnlyList<EventCapacity> locked;
        try
        {
            foreach (var appointmentTypeId in snapshot)
            {
                eventItem.CapacityFor(appointmentTypeId);
            }

            locked = (await capacities.LockForUpdateAsync(eventItem.Id, snapshot, cancellationToken))
                .OrderBy(capacity => capacity.AppointmentTypeId)
                .ToList();
        }
        catch (DomainException ex)
        {
            return Result<IReadOnlyList<Guid>>.Failure(Error.Conflict(ex.Message));
        }

        if (locked.Count != snapshot.Count)
        {
            return Result<IReadOnlyList<Guid>>.Failure(
                Error.Conflict("The eventItem is missing capacity counters for the booking."));
        }

        booking.Cancel();

        foreach (var capacity in locked)
        {
            capacity.Increment();

            audit.Record(
                AuditEntityTypes.Event,
                eventItem.Id,
                AuditAction.CapacityIncremented,
                actorType,
                actorId,
                $"{capacity.AppointmentTypeId} now {capacity.RemainingCapacity}");
        }

        audit.Record(
            AuditEntityTypes.Booking,
            booking.Id,
            AuditAction.BookingCancelled,
            actorType,
            actorId,
            null);

        return Result<IReadOnlyList<Guid>>.Success(snapshot);
    }
}
