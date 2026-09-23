using EventBooking.Application.Abstractions;
using EventBooking.Application.Candidates;

namespace EventBooking.Application.Tests.Fakes;

/// <summary>Serves one canned readiness snapshot while counting query executions.</summary>
public sealed class InMemoryQueries : ICandidateReadinessQueries
{
    /// <summary>Gets the snapshot returned for any candidate.</summary>
    public CandidateReadinessSnapshot? Snapshot { get; set; }

    /// <summary>Gets how many times the snapshot was requested.</summary>
    public int QueryCount { get; private set; }

    /// <inheritdoc />
    public Task<CandidateReadinessSnapshot?> GetSnapshotAsync(
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        QueryCount++;
        return Task.FromResult(Snapshot);
    }
}


/// <summary>Returns a fixed active-booking listing for the staff cancellation workflow.</summary>
public sealed class InMemoryCandidateBookingQueries : ICandidateBookingQueries
{
    /// <summary>Gets the rows returned for any candidate; null stands for an unknown candidate.</summary>
    public IReadOnlyList<CandidateBookingSummary>? Rows { get; set; } = [];

    /// <summary>Gets how many times the listing was requested.</summary>
    public int QueryCount { get; private set; }

    /// <inheritdoc />
    public Task<IReadOnlyList<CandidateBookingSummary>?> ListActiveForCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        QueryCount++;
        return Task.FromResult(Rows);
    }
}
