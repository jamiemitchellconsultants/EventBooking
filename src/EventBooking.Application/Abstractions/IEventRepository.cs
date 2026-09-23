using EventBooking.Domain.Events;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines ievent repository for the current use case.</summary>
public interface IEventRepository
{
    /// <summary>Provides get async within this contract.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Event?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Takes the transactional write guard for a event and returns its current state.
    /// Confirmation and every cancellation path that can change bookings on the event must take
    /// this guard before reading booking or capacity state for that eventItem.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Event?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Active events whose window falls on or after the given date, capacities loaded.</summary>
    /// <param name="onOrAfter">The on or after.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Event>> ListActiveAsync(DateOnly onOrAfter, CancellationToken cancellationToken);

    /// <summary>Every eventItem, cancelled ones included — for the coordinator's events overview.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Event>> ListAllAsync(CancellationToken cancellationToken);

    /// <summary>The events with these identifiers, capacities loaded, in no particular order.</summary>
    /// <param name="ids">The event ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Event>> ListByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken);

    /// <summary>
    /// Adds a eventItem, computing its derived start instant from the window and the location's
    /// zone so the row is written complete in the transaction that inserts it (design 04 — invite
    /// selection). Asynchronous because the zone is a property of the location row.
    /// </summary>
    /// <param name="eventItem">The eventItem.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task AddAsync(Event eventItem, CancellationToken cancellationToken);
}
