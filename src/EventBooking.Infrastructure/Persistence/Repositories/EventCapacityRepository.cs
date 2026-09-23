using EventBooking.Application.Abstractions;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence.Locking;

namespace EventBooking.Infrastructure.Persistence.Repositories;

/// <summary>
/// Capacity rows locked through the shared row-lock helpers, so the lock-order guard sees them
/// and every caller takes them in the domain's own key order. Takes only the helpers: it reads
/// nothing except through them.
/// </summary>
/// <param name="rowLocks">The shared row locks for the current transaction.</param>
public sealed class EventCapacityRepository(RowLocks rowLocks) : IEventCapacityRepository
{
    public Task<IReadOnlyList<EventCapacity>> LockForUpdateAsync(
        Guid eventId,
        IReadOnlyCollection<Guid> appointmentTypeIds,
        CancellationToken cancellationToken) =>
        rowLocks.LockCapacitiesAsync(eventId, appointmentTypeIds, cancellationToken);
}
