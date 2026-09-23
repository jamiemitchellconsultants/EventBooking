using EventBooking.Domain.Slots;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines iconfirmed slot repository for the current use case.</summary>
public interface IConfirmedSlotRepository
{
    /// <summary>Provides get async within this contract.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<ConfirmedSlot?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Takes the transactional write guard for a confirmed slot and returns its current state.
    /// Confirmation and every cancellation path that can change bookings on the slot must take
    /// this guard before reading booking or capacity state for that slot.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<ConfirmedSlot?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Active slots whose window falls on or after the given date, capacities loaded.</summary>
    /// <param name="onOrAfter">The on or after.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<ConfirmedSlot>> ListActiveAsync(DateOnly onOrAfter, CancellationToken cancellationToken);

    /// <summary>Every slot, cancelled ones included — for the coordinator's slots overview.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<ConfirmedSlot>> ListAllAsync(CancellationToken cancellationToken);

    /// <summary>Provides add within this contract.</summary>
    /// <param name="slot">The slot.</param>
    void Add(ConfirmedSlot slot);
}
