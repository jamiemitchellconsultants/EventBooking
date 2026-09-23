using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Invites;

/// <summary>Counts the events an invite dialog could offer an attendee.</summary>
/// <param name="StaffUserId">The asking coordinator.</param>
/// <param name="AttendeeId">The attendee to invite.</param>
/// <param name="LocationIds">The locations the Coordinator opened for this invite.</param>
public sealed record CountEligibleEventsQuery(
    Guid StaffUserId,
    Guid AttendeeId,
    IReadOnlyList<Guid> LocationIds);

/// <summary>Counts eligible events so the invite dialog can warn before issuing.</summary>
/// <param name="attendees">The attendee repository.</param>
/// <param name="locations">The location repository.</param>
/// <param name="eligibility">The event eligibility query.</param>
/// <param name="access">The staff access authorizer.</param>
public sealed class CountEligibleEventsHandler(
    IAttendeeRepository attendees,
    ILocationRepository locations,
    IEventEligibilityQuery eligibility,
    IStaffAccessAuthorizer access)
{
    /// <summary>Handles the query.</summary>
    /// <param name="query">The query.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<int>> HandleAsync(CountEligibleEventsQuery query, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId, StaffCapability.ManageAttendees, null, ct);
        if (authorized.IsFailure) return Result<int>.Failure(authorized.Error);

        var attendee = await attendees.GetAsync(query.AttendeeId, ct);
        if (attendee is null) return Result<int>.Failure(Error.NotFound("No such attendee."));

        var matched = (await locations.ListAsync(ct))
            .Where(location => query.LocationIds.Contains(location.Id)).ToList();
        if (matched.Count != query.LocationIds.Count)
            return Result<int>.Failure(Error.Validation("Unknown location."));
        var inactive = matched.FirstOrDefault(l => !l.IsActive);
        if (inactive is not null)
            return Result<int>.Failure(Error.Validation($"Location {inactive.Code} is not active."));

        return Result<int>.Success(await eligibility.CountEligibleEventsAsync(
            attendee.RequiredAppointmentTypeIds, query.LocationIds, [],
            DateTimeOffset.UtcNow, ct));
    }
}
