using EventBooking.Domain.Invites;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines iinvite repository for the current use case.</summary>
public interface IInviteRepository
{
    /// <summary>Provides get async within this contract.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Invite?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Takes a row-level write lock on the invite identified by its identifier and loads its
    /// offered events. Attendee deletion and expiry processing use it after the attendee lock.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Invite?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Takes a row-level write lock on the attendee's current pending invite and loads its
    /// offered events. Callers hold the attendee lifecycle lock before calling this method.
    /// </summary>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Invite?> LockPendingForAttendeeAsync(Guid attendeeId, CancellationToken cancellationToken);

    /// <summary>Provides get pending for attendee async within this contract.</summary>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Invite?> GetPendingForAttendeeAsync(Guid attendeeId, CancellationToken cancellationToken);

    /// <summary>Locks the Attendee's pending initial Invite after the Attendee lifecycle lock.</summary>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Invite?> LockPendingInitialForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Takes row-level write locks on every pending Invite for the attendee, ordered by ID, and
    /// loads their offered events. Callers hold the attendee lifecycle lock before calling this
    /// method; recovery issuance reads the set authoritatively exactly once.
    /// </summary>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Invite>> LockPendingListForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken);

    /// <summary>Pending invites whose expiry has passed — the input to the sweep in Task 37.</summary>
    /// <param name="asAt">The as at.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Invite>> ListPendingExpiredAsync(DateTimeOffset asAt, CancellationToken cancellationToken);

    /// <summary>Provides add within this contract.</summary>
    /// <param name="invite">The invite.</param>
    void Add(Invite invite);
}
