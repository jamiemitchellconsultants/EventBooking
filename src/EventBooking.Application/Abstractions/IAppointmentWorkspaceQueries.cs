using EventBooking.Application.Appointments;

namespace EventBooking.Application.Abstractions;

/// <summary>Projects only appointment-delivery data inside a trusted appointment-type scope.</summary>
public interface IAppointmentWorkspaceQueries
{
    /// <summary>Lists recent-past, current, and future active slots containing active bookings in trusted scope.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    /// <param name="onOrAfter">The on or after.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AppointmentWorkspaceSlotList> ListSlotsAsync(
        Guid appointmentTypeId,
        DateOnly onOrAfter,
        CancellationToken cancellationToken);

    /// <summary>Gets one active slot's minimum appointment rows inside trusted scope.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    /// <param name="confirmedSlotId">The confirmed slot id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AppointmentSlotDetail?> GetSlotAsync(
        Guid appointmentTypeId,
        Guid confirmedSlotId,
        CancellationToken cancellationToken);
}
