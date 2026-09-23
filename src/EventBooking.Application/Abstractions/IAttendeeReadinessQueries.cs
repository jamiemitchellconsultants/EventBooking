using EventBooking.Application.Attendees;

namespace EventBooking.Application.Abstractions;

/// <summary>Loads the readiness journey projection for one attendee.</summary>
public interface IAttendeeReadinessQueries
{
    /// <summary>Loads the full original/recovery journey, or null when the Attendee does not exist.</summary>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The snapshot, or null for an unknown attendee.</returns>
    Task<AttendeeReadinessSnapshot?> GetSnapshotAsync(
        Guid attendeeId,
        CancellationToken cancellationToken);
}
