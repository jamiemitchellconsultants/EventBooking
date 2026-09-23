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
    /// offered slots. Candidate deletion and expiry processing use it after the candidate lock.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Invite?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Provides get by token hash async within this contract.</summary>
    /// <param name="tokenHash">The token hash.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Invite?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>
    /// Takes a row-level write lock on the invite identified by its token hash and returns it.
    /// Must be called inside the booking-confirmation transaction before checking whether the
    /// invite remains usable or offers the selected slot.
    /// </summary>
    /// <param name="tokenHash">The token hash.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Invite?> LockByTokenHashForUpdateAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>
    /// Takes a row-level write lock on the candidate's current pending invite and loads its
    /// offered slots. Callers hold the candidate lifecycle lock before calling this method.
    /// </summary>
    /// <param name="candidateId">The candidate id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Invite?> LockPendingForCandidateAsync(Guid candidateId, CancellationToken cancellationToken);

    /// <summary>Provides get pending for candidate async within this contract.</summary>
    /// <param name="candidateId">The candidate id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Invite?> GetPendingForCandidateAsync(Guid candidateId, CancellationToken cancellationToken);

    /// <summary>Locks the Candidate's pending initial Invite after the Candidate lifecycle lock.</summary>
    /// <param name="candidateId">The candidate id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Invite?> LockPendingInitialForCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Takes row-level write locks on every pending Invite for the candidate, ordered by ID, and
    /// loads their offered slots. Callers hold the candidate lifecycle lock before calling this
    /// method; recovery issuance reads the set authoritatively exactly once.
    /// </summary>
    /// <param name="candidateId">The candidate id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Invite>> LockPendingListForCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken);

    /// <summary>Pending invites whose expiry has passed — the input to the sweep in Task 37.</summary>
    /// <param name="asAt">The as at.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Invite>> ListPendingExpiredAsync(DateTimeOffset asAt, CancellationToken cancellationToken);

    /// <summary>Provides add within this contract.</summary>
    /// <param name="invite">The invite.</param>
    void Add(Invite invite);
}
