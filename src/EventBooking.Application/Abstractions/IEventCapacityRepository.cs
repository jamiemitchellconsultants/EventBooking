using EventBooking.Domain.Events;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines ievent capacity repository for the current use case.</summary>
public interface IEventCapacityRepository
{
    /// <summary>
    /// Takes a row-level write lock on the capacity rows for one event and the given appointment
    /// types, and returns them. Must be called inside a transaction. Rows are locked in
    /// appointment-type order so two concurrent callers can never deadlock against each other.
    /// </summary>
    /// <param name="eventId">The event id.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<EventCapacity>> LockForUpdateAsync(
        Guid eventId,
        IReadOnlyCollection<Guid> appointmentTypeIds,
        CancellationToken cancellationToken);
}
