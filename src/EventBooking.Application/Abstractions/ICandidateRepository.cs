using EventBooking.Domain.Candidates;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines icandidate repository for the current use case.</summary>
public interface ICandidateRepository
{
    /// <summary>Provides get async within this contract.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Candidate?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Takes the candidate lifecycle write lock and loads the candidate's requirements. Invite
    /// issuance, booking confirmation, cancellation/rebooking, and deletion take this lock first
    /// inside their transactions so one candidate cannot transition through two lifecycles at once.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Candidate?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Provides get by email async within this contract.</summary>
    /// <param name="email">The email.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Candidate?> GetByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>All candidates, or only those in one status when a status is supplied.</summary>
    /// <param name="status">The status.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Candidate>> ListAsync(CandidateStatus? status, CancellationToken cancellationToken);

    /// <summary>Provides add within this contract.</summary>
    /// <param name="candidate">The candidate.</param>
    void Add(Candidate candidate);

    /// <summary>Provides remove within this contract.</summary>
    /// <param name="candidate">The candidate.</param>
    void Remove(Candidate candidate);
}
