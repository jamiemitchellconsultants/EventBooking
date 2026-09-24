using EventBooking.Application.Attendees;
using EventBooking.Application.ReadModels;

namespace EventBooking.Application.Abstractions;

/// <summary>The keyset-paginated attendee list. Refuses an Admin-shaped caller itself.</summary>
public interface IAttendeeListQueries
{
    /// <summary>Lists one keyset page of attendees.</summary>
    /// <param name="shape">The caller shape.</param>
    /// <param name="cursor">The opaque page cursor, or null for the first page.</param>
    /// <param name="limit">The page size.</param>
    /// <param name="status">The status filter.</param>
    /// <param name="attendeeGroupId">The Attendee Group filter.</param>
    /// <param name="readiness">The readiness filter.</param>
    /// <param name="nameOrEmailPrefix">The name-or-email prefix filter.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<AttendeeListView> ListAttendeesAsync(
        CallerShape shape, string? cursor, int limit, string? status,
        Guid? attendeeGroupId, string? readiness, string? nameOrEmailPrefix,
        CancellationToken ct);
}
