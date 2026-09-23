using EventBooking.Application.Appointments;

namespace EventBooking.Application.Abstractions;

/// <summary>Projects only appointment-delivery data inside a trusted appointment-type scope.</summary>
public interface IAppointmentWorkspaceQueries
{
    /// <summary>Lists recent-past, current, and future active events containing active bookings in trusted scope.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    /// <param name="onOrAfter">The on or after.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AppointmentWorkspaceEventList> ListEventsAsync(
        Guid appointmentTypeId,
        DateOnly onOrAfter,
        CancellationToken cancellationToken);

    /// <summary>Gets one active event's minimum appointment rows inside trusted scope.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    /// <param name="eventId">The event id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AppointmentEventDetail?> GetEventAsync(
        Guid appointmentTypeId,
        Guid eventId,
        CancellationToken cancellationToken);
}
