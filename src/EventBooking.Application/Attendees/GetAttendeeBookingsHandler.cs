using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Attendees;

/// <summary>One active booking a coordinator may cancel, without any management token.</summary>
/// <param name="BookingId">The booking identifier used to target a cancellation.</param>
/// <param name="IsOriginal">True for the original booking; false for an active recovery booking.</param>
/// <param name="EventDate">The date of the confirmed window the booking holds.</param>
/// <param name="EventStartTime">The start of the confirmed window the booking holds.</param>
/// <param name="EventEndTime">The end of the confirmed window the booking holds.</param>
public sealed record AttendeeBookingSummary(
    Guid BookingId,
    bool IsOriginal,
    DateOnly EventDate,
    TimeOnly EventStartTime,
    TimeOnly EventEndTime);

/// <summary>Requests the active bookings a coordinator may cancel for one attendee.</summary>
/// <param name="StaffUserId">The staff member asking for the listing.</param>
/// <param name="AttendeeId">The attendee whose bookings are listed.</param>
public sealed record GetAttendeeBookingsQuery(Guid StaffUserId, Guid AttendeeId);

/// <summary>Authorizes a coordinator before listing a attendee's active bookings.</summary>
/// <param name="access">Authorizes attendee management.</param>
/// <param name="queries">Loads the active-booking projection.</param>
public sealed class GetAttendeeBookingsHandler(
    IStaffAccessAuthorizer access,
    IAttendeeBookingQueries queries)
{
    /// <summary>Authorizes a coordinator before loading or returning attendee-linked state.</summary>
    /// <param name="query">The staff listing request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The attendee's active bookings, a forbidden failure when the caller cannot manage
    /// attendees, or a not-found failure for an unknown attendee.
    /// </returns>
    public async Task<Result<IReadOnlyList<AttendeeBookingSummary>>> HandleAsync(
        GetAttendeeBookingsQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId,
            StaffCapability.ManageAttendees,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<IReadOnlyList<AttendeeBookingSummary>>.Failure(authorized.Error);
        }

        var rows = await queries.ListActiveForAttendeeAsync(query.AttendeeId, cancellationToken);
        if (rows is null)
        {
            return Result<IReadOnlyList<AttendeeBookingSummary>>.Failure(
                Error.NotFound("No such attendee."));
        }

        return Result<IReadOnlyList<AttendeeBookingSummary>>.Success(rows);
    }
}
