using EventBooking.Domain.Slots;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines islot capacity repository for the current use case.</summary>
public interface ISlotCapacityRepository
{
    /// <summary>
    /// Takes a row-level write lock on the capacity rows for one slot and the given appointment
    /// types, and returns them. Must be called inside a transaction. Rows are locked in
    /// appointment-type order so two concurrent callers can never deadlock against each other.
    /// </summary>
    /// <param name="confirmedSlotId">The confirmed slot id.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<SlotCapacity>> LockForUpdateAsync(
        Guid confirmedSlotId,
        IReadOnlyCollection<Guid> appointmentTypeIds,
        CancellationToken cancellationToken);
}
