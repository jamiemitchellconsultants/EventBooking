using EventBooking.Application.Candidates;

namespace EventBooking.Application.Abstractions;

/// <summary>Loads a candidate's active bookings for the staff cancellation workflow.</summary>
public interface ICandidateBookingQueries
{
    /// <summary>Lists active bookings with slot windows, or null when the candidate does not exist.</summary>
    /// <param name="candidateId">The candidate identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Active booking summaries, or null for an unknown candidate.</returns>
    Task<IReadOnlyList<CandidateBookingSummary>?> ListActiveForCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken);
}
