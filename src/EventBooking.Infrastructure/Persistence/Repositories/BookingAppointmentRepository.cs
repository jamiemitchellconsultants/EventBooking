using EventBooking.Application.Abstractions;
using EventBooking.Domain.Bookings;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Repositories;

/// <summary>Persists and row-locks booking appointments inside trusted appointment-type scope.</summary>
/// <param name="context">The database context.</param>
public sealed class BookingAppointmentRepository(EventBookingDbContext context)
    : IBookingAppointmentRepository
{
    /// <summary>Adds one appointment to the current unit of work.</summary>
    /// <param name="appointment">The appointment to track.</param>
    public void Add(BookingAppointment appointment) => context.BookingAppointments.Add(appointment);

    /// <summary>Finds lifecycle owner identifiers only when record and trusted type both match.</summary>
    /// <param name="id">The stable appointment-record identifier.</param>
    /// <param name="appointmentTypeId">The trusted appointment-type scope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The lifecycle owner identifiers, or null when out of scope.</returns>
    public Task<BookingAppointmentLocator?> FindLocatorInScopeAsync(
        Guid id,
        Guid appointmentTypeId,
        CancellationToken cancellationToken) =>
        context.BookingAppointments
            .AsNoTracking()
            .Where(value => value.Id == id && value.AppointmentTypeId == appointmentTypeId)
            .Join(
                context.Bookings,
                value => value.BookingId,
                booking => booking.Id,
                (value, booking) => new BookingAppointmentLocator(
                    booking.AttendeeId,
                    booking.RecoveryOfBookingId ?? booking.Id,
                    booking.Id,
                    booking.EventId,
                    value.AppointmentTypeId))
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>Locks and returns one appointment only when record and trusted type both match.</summary>
    /// <param name="id">The stable appointment-record identifier.</param>
    /// <param name="appointmentTypeId">The trusted appointment-type scope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The locked appointment, or null when out of scope.</returns>
    public Task<BookingAppointment?> LockForUpdateAsync(
        Guid id,
        Guid appointmentTypeId,
        CancellationToken cancellationToken) =>
        context.BookingAppointments
            .FromSqlInterpolated(
                $"""
                SELECT * FROM booking_appointment
                WHERE id = {id} AND appointment_type_id = {appointmentTypeId}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>Lists the immutable Appointment Type snapshot owned by one Booking.</summary>
    public async Task<IReadOnlyList<BookingAppointment>> ListForBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken) =>
        await context.BookingAppointments
            .AsNoTracking()
            .Where(value => value.BookingId == bookingId)
            .OrderBy(value => value.AppointmentTypeId)
            .ToListAsync(cancellationToken);

    /// <summary>Lists the snapshots owned by a whole Booking journey in stable ID order.</summary>
    public async Task<IReadOnlyList<BookingAppointment>> ListForBookingsAsync(
        IReadOnlyCollection<Guid> bookingIds,
        CancellationToken cancellationToken) =>
        await context.BookingAppointments
            .AsNoTracking()
            .Where(value => bookingIds.Contains(value.BookingId))
            .OrderBy(value => value.Id)
            .ToListAsync(cancellationToken);

    /// <summary>Locks every appointment for one Booking in stable ID order.</summary>
    public async Task<IReadOnlyList<BookingAppointment>> LockForBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken) =>
        await context.BookingAppointments
            .FromSqlInterpolated(
                $"""
                SELECT * FROM booking_appointment
                WHERE booking_id = {bookingId}
                ORDER BY id FOR UPDATE
                """)
            .ToListAsync(cancellationToken);
}
