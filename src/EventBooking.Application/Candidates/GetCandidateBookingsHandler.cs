using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Candidates;

/// <summary>One active booking a coordinator may cancel, without any management token.</summary>
/// <param name="BookingId">The booking identifier used to target a cancellation.</param>
/// <param name="IsOriginal">True for the original booking; false for an active recovery booking.</param>
/// <param name="SlotDate">The date of the confirmed window the booking holds.</param>
/// <param name="SlotStartTime">The start of the confirmed window the booking holds.</param>
/// <param name="SlotEndTime">The end of the confirmed window the booking holds.</param>
public sealed record CandidateBookingSummary(
    Guid BookingId,
    bool IsOriginal,
    DateOnly SlotDate,
    TimeOnly SlotStartTime,
    TimeOnly SlotEndTime);

/// <summary>Requests the active bookings a coordinator may cancel for one candidate.</summary>
/// <param name="StaffUserId">The staff member asking for the listing.</param>
/// <param name="CandidateId">The candidate whose bookings are listed.</param>
public sealed record GetCandidateBookingsQuery(Guid StaffUserId, Guid CandidateId);

/// <summary>Authorizes a coordinator before listing a candidate's active bookings.</summary>
/// <param name="access">Authorizes candidate management.</param>
/// <param name="queries">Loads the active-booking projection.</param>
public sealed class GetCandidateBookingsHandler(
    IStaffAccessAuthorizer access,
    ICandidateBookingQueries queries)
{
    /// <summary>Authorizes a coordinator before loading or returning candidate-linked state.</summary>
    /// <param name="query">The staff listing request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The candidate's active bookings, a forbidden failure when the caller cannot manage
    /// candidates, or a not-found failure for an unknown candidate.
    /// </returns>
    public async Task<Result<IReadOnlyList<CandidateBookingSummary>>> HandleAsync(
        GetCandidateBookingsQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId,
            StaffCapability.ManageCandidates,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<IReadOnlyList<CandidateBookingSummary>>.Failure(authorized.Error);
        }

        var rows = await queries.ListActiveForCandidateAsync(query.CandidateId, cancellationToken);
        if (rows is null)
        {
            return Result<IReadOnlyList<CandidateBookingSummary>>.Failure(
                Error.NotFound("No such candidate."));
        }

        return Result<IReadOnlyList<CandidateBookingSummary>>.Success(rows);
    }
}
