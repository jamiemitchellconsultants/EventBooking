using EventBooking.Domain.EventGroups;
using EventBooking.Domain.SelfRegistrations;

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

    /// <summary>Stages a new pending registration for the next save.</summary>
    /// <param name="registration">The pending registration.</param>
    void AddRegistration(PendingRegistration registration);

    /// <summary>Finds the pending registration for one event and email address.</summary>
    /// <param name="eventId">The event id.</param>
    /// <param name="email">The normalized email address.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<PendingRegistration?> FindInFlightAsync(
        Guid eventId, string email, CancellationToken cancellationToken);

    /// <summary>Gets one pending registration by its request identifier.</summary>
    /// <param name="requestId">The request id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<PendingRegistration?> GetRegistrationAsync(
        Guid requestId, CancellationToken cancellationToken);
}
