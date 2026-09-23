using EventBooking.Domain.Attendees;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines iattendee repository for the current use case.</summary>
public interface IAttendeeRepository
{
    /// <summary>Provides get async within this contract.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Attendee?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Takes the attendee lifecycle write lock and loads the attendee's requirements. Invite
    /// issuance, booking confirmation, cancellation/rebooking, and deletion take this lock first
    /// inside their transactions so one attendee cannot transition through two lifecycles at once.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Attendee?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Provides get by email async within this contract.</summary>
    /// <param name="email">The email.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Attendee?> GetByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>All attendees, or only those in one status when a status is supplied.</summary>
    /// <param name="status">The status.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Attendee>> ListAsync(AttendeeStatus? status, CancellationToken cancellationToken);

    /// <summary>Provides add within this contract.</summary>
    /// <param name="attendee">The attendee.</param>
    void Add(Attendee attendee);

    /// <summary>Provides remove within this contract.</summary>
    /// <param name="attendee">The attendee.</param>
    void Remove(Attendee attendee);

    /// <summary>
    /// Takes every member's lifecycle lock in ascending id order and returns the members
    /// ordered by id. The group-requirement replacement calls this first.
    /// </summary>
    /// <param name="groupId">The group id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Attendee>> LockByGroupForUpdateAsync(Guid groupId, CancellationToken cancellationToken);
}
