using EventBooking.Application.Abstractions;
using EventBooking.Application.Attendees;

namespace EventBooking.Application.Tests.Fakes;

/// <summary>Serves one canned readiness snapshot while counting query executions.</summary>
public sealed class InMemoryQueries : IAttendeeReadinessQueries
{
    /// <summary>Gets the snapshot returned for any attendee.</summary>
    public AttendeeReadinessSnapshot? Snapshot { get; set; }

    /// <summary>Gets how many times the snapshot was requested.</summary>
    public int QueryCount { get; private set; }

    /// <inheritdoc />
    public Task<AttendeeReadinessSnapshot?> GetSnapshotAsync(
        Guid attendeeId,
        CancellationToken cancellationToken)
    {
        QueryCount++;
        return Task.FromResult(Snapshot);
    }
}


/// <summary>Returns a fixed active-booking listing for the staff cancellation workflow.</summary>
public sealed class InMemoryAttendeeBookingQueries : IAttendeeBookingQueries
{
    /// <summary>Gets the rows returned for any attendee; null stands for an unknown attendee.</summary>
    public IReadOnlyList<AttendeeBookingSummary>? Rows { get; set; } = [];

    /// <summary>Gets how many times the listing was requested.</summary>
    public int QueryCount { get; private set; }

    /// <inheritdoc />
    public Task<IReadOnlyList<AttendeeBookingSummary>?> ListActiveForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken)
    {
        QueryCount++;
        return Task.FromResult(Rows);
    }
}
