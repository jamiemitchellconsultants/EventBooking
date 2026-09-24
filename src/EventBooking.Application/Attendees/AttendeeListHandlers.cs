using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Application.ReadModels;

namespace EventBooking.Application.Attendees;

/// <summary>Requests one keyset page of the attendee list.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="Cursor">The opaque page cursor, or null for the first page.</param>
/// <param name="Limit">The page size, between 1 and 200.</param>
/// <param name="Status">The status filter, or null for every status.</param>
/// <param name="AttendeeGroupId">The Attendee Group filter, or null for every group.</param>
/// <param name="Readiness">The readiness filter, or null for every readiness.</param>
/// <param name="NameOrEmailPrefix">The name-or-email prefix filter, or null for every attendee.</param>
public sealed record ListAttendeesQuery(
    Guid StaffUserId, string? Cursor, int Limit, string? Status,
    Guid? AttendeeGroupId, string? Readiness, string? NameOrEmailPrefix);

/// <summary>One attendee row: identity and lifecycle state, never another aggregate's identifiers.</summary>
/// <param name="AttendeeId">The attendee id.</param>
/// <param name="Name">The name.</param>
/// <param name="Email">The email.</param>
/// <param name="Status">The status name.</param>
/// <param name="GroupCode">The Attendee Group code.</param>
/// <param name="Readiness">The readiness label.</param>
/// <param name="RequiredTypeCodes">The required type codes, on awaiting-availability rows only.</param>
/// <param name="LatestDeliveryStatus">The latest delivery status, or null when never invited.</param>
/// <param name="Cursor">The row's page cursor.</param>
public sealed record AttendeeListItem(
    Guid AttendeeId,
    string Name, string Email, string Status, string GroupCode, string Readiness,
    IReadOnlyList<string> RequiredTypeCodes, string? LatestDeliveryStatus, string Cursor);

/// <summary>One keyset page of the attendee list.</summary>
/// <param name="Items">The rows.</param>
/// <param name="NextCursor">The next page cursor, or null when exhausted.</param>
public sealed record AttendeeListView(IReadOnlyList<AttendeeListItem> Items, string? NextCursor);

/// <summary>Lists one keyset page of attendees after a Coordinator-profile check.</summary>
/// <param name="queries">The queries.</param>
/// <param name="profiles">The access profiles.</param>
public sealed class ListAttendeesHandler(
    IAttendeeListQueries queries,
    IStaffAccessProfileRepository profiles)
{
    /// <summary>Handles the query.</summary>
    /// <param name="query">The query.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<AttendeeListView>> HandleAsync(
        ListAttendeesQuery query, CancellationToken ct)
    {
        if (query.Limit is < 1 or > 200)
            return Result<AttendeeListView>.Failure(
                Error.Validation("Limit must be between 1 and 200."));
        if (query.Cursor is not null
            && !AttendeeCursor.TryDecode(query.Cursor, out _, out _))
            return Result<AttendeeListView>.Failure(Error.Validation("The cursor is invalid."));

        var profile = await profiles.GetAsync(query.StaffUserId, ct);
        if (profile is null || !profile.IsCoordinator)
            return Result<AttendeeListView>.Failure(
                Error.Forbidden("The attendee list needs a Coordinator profile."));
        var shape = new CallerShape(
            profile.StaffUserId, profile.IsAdmin, new HashSet<string>());

        var page = await queries.ListAttendeesAsync(shape, query.Cursor, query.Limit,
            query.Status, query.AttendeeGroupId, query.Readiness, query.NameOrEmailPrefix, ct);
        return Result<AttendeeListView>.Success(page);
    }
}
