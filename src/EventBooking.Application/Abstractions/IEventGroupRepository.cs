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

    /// <summary>Reads a group with both collections without tracking, ahead of a locked re-read.</summary>
    /// <param name="id">The group id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<EventGroup?> GetUntrackedAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Stages a new pending registration for the next save.</summary>
    /// <param name="registration">The pending registration.</param>
    void AddRegistration(PendingRegistration registration);

    /// <summary>Finds the pending registration for one event, email address and selection.</summary>
    /// <param name="eventId">The event id.</param>
    /// <param name="email">The normalized email address.</param>
    /// <param name="eventGroupId">The event group the request came through.</param>
    /// <param name="attendeeGroupId">The selected attendee group.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<PendingRegistration?> FindInFlightAsync(
        Guid eventId, string email, Guid eventGroupId, Guid attendeeGroupId,
        CancellationToken cancellationToken);

    /// <summary>Serialises concurrent submissions for one normalized email address until commit.</summary>
    /// <param name="email">The normalized email address.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task LockEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// The instant the newest confirmation-link email for the address is due to go out, or null
    /// when none was staged at or after <paramref name="since"/>.
    /// </summary>
    /// <param name="email">The normalized email address.</param>
    /// <param name="since">The earliest staging instant to consider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<DateTimeOffset?> LatestLinkDueAsync(
        string email, DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>Gets one pending registration by its request identifier.</summary>
    /// <param name="requestId">The request id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<PendingRegistration?> GetRegistrationAsync(
        Guid requestId, CancellationToken cancellationToken);

    /// <summary>Re-reads one pending registration without tracking, for post-lock validation.</summary>
    /// <param name="requestId">The request id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<PendingRegistration?> GetRegistrationUntrackedAsync(
        Guid requestId, CancellationToken cancellationToken);

    /// <summary>Lists the pending registrations at or past their expiry.</summary>
    /// <param name="now">The current instant.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<PendingRegistration>> ListExpiredPendingAsync(
        DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes one bounded batch of terminal requests at or past retention, with their
    /// confirmation email rows. Returns the deleted request count, zero when none remain.
    /// </summary>
    /// <param name="cutoff">The oldest terminal instant to keep.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<int> DeleteTerminalBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);
}
