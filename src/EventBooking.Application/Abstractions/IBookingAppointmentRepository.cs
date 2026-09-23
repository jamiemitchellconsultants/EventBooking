using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Abstractions;

/// <summary>Identifies the lifecycle owners of one appointment without granting authority.</summary>
/// <param name="CandidateId">The candidate owning the parent Booking.</param>
/// <param name="OriginalBookingId">The journey-root Booking identifier.</param>
/// <param name="BookingId">The parent booking identifier.</param>
/// <param name="ConfirmedSlotId">The confirmed slot selected by that booking.</param>
/// <param name="AppointmentTypeId">The appointment type scoping the lookup.</param>
public sealed record BookingAppointmentLocator(
    Guid CandidateId,
    Guid OriginalBookingId,
    Guid BookingId,
    Guid ConfirmedSlotId,
    Guid AppointmentTypeId);

/// <summary>Persists and locks booking appointments without widening appointment-type scope.</summary>
public interface IBookingAppointmentRepository
{
    /// <summary>Adds one appointment to the current unit of work.</summary>
    /// <param name="appointment">The appointment to track.</param>
    void Add(BookingAppointment appointment);

    /// <summary>Finds immutable parent identifiers only when record and trusted type both match.</summary>
    /// <param name="id">The stable appointment-record identifier.</param>
    /// <param name="appointmentTypeId">The trusted appointment-type scope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The immutable parent identifiers, or null when out of scope.</returns>
    Task<BookingAppointmentLocator?> FindLocatorInScopeAsync(
        Guid id,
        Guid appointmentTypeId,
        CancellationToken cancellationToken);

    /// <summary>Locks and returns one appointment only when record and trusted type both match.</summary>
    /// <param name="id">The stable appointment-record identifier.</param>
    /// <param name="appointmentTypeId">The trusted appointment-type scope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The locked appointment, or null when out of scope.</returns>
    Task<BookingAppointment?> LockForUpdateAsync(
        Guid id,
        Guid appointmentTypeId,
        CancellationToken cancellationToken);

    /// <summary>Locks all appointments for one Booking ordered by stable ID.</summary>
    /// <param name="bookingId">The parent booking identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The locked appointments in stable ID order.</returns>
    Task<IReadOnlyList<BookingAppointment>> LockForBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken);

    /// <summary>Lists the immutable Appointment Type snapshot owned by one Booking.</summary>
    /// <param name="bookingId">The booking id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<BookingAppointment>> ListForBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists the immutable Appointment Type snapshots owned by a whole Booking journey in stable
    /// ID order, so recovery eligibility reads every attempt deterministically in one round trip.
    /// </summary>
    /// <param name="bookingIds">The booking ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<BookingAppointment>> ListForBookingsAsync(
        IReadOnlyCollection<Guid> bookingIds,
        CancellationToken cancellationToken);
}
