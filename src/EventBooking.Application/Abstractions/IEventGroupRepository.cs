using EventBooking.Domain.EventGroups;

namespace EventBooking.Application.Abstractions;

/// <summary>Persists Event Group publication aggregates with their membership collections.</summary>
public interface IEventGroupRepository
{
    /// <summary>Gets a group with both membership collections.</summary>
    /// <param name="id">The event group id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<EventGroup?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Locks the parent row before any gate or mapping mutation.</summary>
    /// <param name="id">The event group id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<EventGroup?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lists every group with both membership collections.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<EventGroup>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Lists every group selecting the attendee group, with both membership collections.</summary>
    /// <param name="attendeeGroupId">The attendee group id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<EventGroup>> ListContainingAttendeeGroupAsync(
        Guid attendeeGroupId, CancellationToken cancellationToken);

    /// <summary>Stages a new event group for the next save.</summary>
    /// <param name="group">The event group.</param>
    void Add(EventGroup group);
}
