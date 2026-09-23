using EventBooking.Application.Candidates;

namespace EventBooking.Application.Abstractions;

/// <summary>Loads the readiness journey projection for one candidate.</summary>
public interface ICandidateReadinessQueries
{
    /// <summary>Loads the full original/recovery journey, or null when the Candidate does not exist.</summary>
    /// <param name="candidateId">The candidate identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The snapshot, or null for an unknown candidate.</returns>
    Task<CandidateReadinessSnapshot?> GetSnapshotAsync(
        Guid candidateId,
        CancellationToken cancellationToken);
}
