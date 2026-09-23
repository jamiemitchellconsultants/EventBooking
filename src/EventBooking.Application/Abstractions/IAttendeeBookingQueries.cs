using EventBooking.Application.Attendees;

namespace EventBooking.Application.Abstractions;

/// <summary>Loads a attendee's active bookings for the staff cancellation workflow.</summary>
public interface IAttendeeBookingQueries
{
    /// <summary>Lists active bookings with event windows, or null when the attendee does not exist.</summary>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Active booking summaries, or null for an unknown attendee.</returns>
    Task<IReadOnlyList<AttendeeBookingSummary>?> ListActiveForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken);
}
