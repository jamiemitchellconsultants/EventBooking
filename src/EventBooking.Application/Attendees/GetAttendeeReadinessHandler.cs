using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;

namespace EventBooking.Application.Attendees;

/// <summary>Requests internal readiness for one attendee.</summary>
/// <param name="StaffUserId">The staff member asking for readiness.</param>
/// <param name="AttendeeId">The attendee identifier.</param>
public sealed record GetAttendeeReadinessQuery(Guid StaffUserId, Guid AttendeeId);

/// <summary>Authorizes a Coordinator before loading or returning Attendee-linked state.</summary>
/// <param name="access">Authorizes attendee management.</param>
/// <param name="queries">Loads the readiness journey projection.</param>
/// <param name="calculator">Calculates readiness from the projection.</param>
public sealed class GetAttendeeReadinessHandler(
    IStaffAccessAuthorizer access,
    IAttendeeReadinessQueries queries,
    AttendeeReadinessCalculator calculator)
{
    /// <summary>Authorizes a Coordinator before loading or returning Attendee-linked state.</summary>
    /// <param name="query">The staff readiness request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The calculated readiness.</returns>
    public async Task<Result<AttendeeReadiness>> HandleAsync(
        GetAttendeeReadinessQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId,
            StaffCapability.ManageAttendees,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<AttendeeReadiness>.Failure(authorized.Error);
        }

        var snapshot = await queries.GetSnapshotAsync(query.AttendeeId, cancellationToken);
        if (snapshot is null)
        {
            return Result<AttendeeReadiness>.Failure(Error.NotFound("No such attendee."));
        }

        return Result<AttendeeReadiness>.Success(calculator.Calculate(snapshot));
    }
}
