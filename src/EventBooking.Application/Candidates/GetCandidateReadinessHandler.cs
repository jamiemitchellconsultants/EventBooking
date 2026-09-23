using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;

namespace EventBooking.Application.Candidates;

/// <summary>Requests internal readiness for one candidate.</summary>
/// <param name="StaffUserId">The staff member asking for readiness.</param>
/// <param name="CandidateId">The candidate identifier.</param>
public sealed record GetCandidateReadinessQuery(Guid StaffUserId, Guid CandidateId);

/// <summary>Authorizes a Coordinator before loading or returning Candidate-linked state.</summary>
/// <param name="access">Authorizes candidate management.</param>
/// <param name="queries">Loads the readiness journey projection.</param>
/// <param name="calculator">Calculates readiness from the projection.</param>
public sealed class GetCandidateReadinessHandler(
    IStaffAccessAuthorizer access,
    ICandidateReadinessQueries queries,
    CandidateReadinessCalculator calculator)
{
    /// <summary>Authorizes a Coordinator before loading or returning Candidate-linked state.</summary>
    /// <param name="query">The staff readiness request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The calculated readiness.</returns>
    public async Task<Result<CandidateReadiness>> HandleAsync(
        GetCandidateReadinessQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId,
            StaffCapability.ManageCandidates,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<CandidateReadiness>.Failure(authorized.Error);
        }

        var snapshot = await queries.GetSnapshotAsync(query.CandidateId, cancellationToken);
        if (snapshot is null)
        {
            return Result<CandidateReadiness>.Failure(Error.NotFound("No such candidate."));
        }

        return Result<CandidateReadiness>.Success(calculator.Calculate(snapshot));
    }
}
