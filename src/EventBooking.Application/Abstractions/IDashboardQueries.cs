using EventBooking.Application.Dashboards;
using EventBooking.Application.ReadModels;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Time;

namespace EventBooking.Application.Abstractions;

/// <summary>The latest delivery projection for one attendee, including whether its context remains retryable.</summary>
/// <param name="AttendeeId">The attendee whose latest delivery is projected.</param>
/// <param name="TemplateName">The server-owned template of the delivery.</param>
/// <param name="SentAt">The latest attempt or pending timestamp.</param>
/// <param name="Status">The durable delivery status.</param>
/// <param name="CanRetry">Whether current domain state still permits this delivery to be regenerated.</param>
public sealed record AttendeeEmailStatusRow(
    Guid AttendeeId,
    EmailTemplate TemplateName,
    DateTimeOffset SentAt,
    EmailStatus Status,
    bool CanRetry);

/// <summary>Defines awaiting availability row for the current use case.</summary>
/// <param name="AttendeeId">The attendee id.</param>
/// <param name="Name">The name.</param>
/// <param name="Email">The email.</param>
/// <param name="RequiredCodes">The required codes.</param>
/// <param name="WaitingSince">The waiting since.</param>
/// <param name="DaysWaiting">The days waiting.</param>
public sealed record AwaitingAvailabilityRow(
    Guid AttendeeId,
    string Name,
    string Email,
    IReadOnlyList<string> RequiredCodes,
    DateOnly WaitingSince,
    int DaysWaiting);

/// <summary>Defines no response row for the current use case.</summary>
/// <param name="AttendeeId">The attendee id.</param>
/// <param name="Name">The name.</param>
/// <param name="Email">The email.</param>
/// <param name="RequiredCodes">The required codes.</param>
/// <param name="GaveUpOn">The gave up on.</param>
public sealed record NoResponseRow(
    Guid AttendeeId,
    string Name,
    string Email,
    IReadOnlyList<string> RequiredCodes,
    DateOnly GaveUpOn);

/// <summary>Defines event capacity row for the current use case.</summary>
/// <param name="Code">The code.</param>
/// <param name="TotalHeadcount">The total headcount.</param>
/// <param name="RemainingCapacity">The remaining capacity.</param>
public sealed record EventCapacityRow(string Code, int TotalHeadcount, int RemainingCapacity);

/// <summary>Defines event overview row for the current use case.</summary>
/// <param name="EventId">The event id.</param>
/// <param name="LocationId">The location id.</param>
/// <param name="LocationName">The location name.</param>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
/// <param name="EndTime">The end time.</param>
/// <param name="Capacities">The capacities.</param>
/// <param name="ActiveBookings">The active bookings.</param>
/// <param name="TimeZoneId">The location's IANA zone identifier.</param>
/// <param name="DurationMinutes">The window length.</param>
public sealed record EventOverviewRow(
    Guid EventId, Guid LocationId, string LocationName, DateOnly Date,
    TimeOnly StartTime, TimeOnly EndTime,
    IReadOnlyList<EventCapacityRow> Capacities, int ActiveBookings,
    string TimeZoneId, int DurationMinutes);

/// <summary>
/// The dashboard read side. Implementations query and project directly, returning no entities and
/// offering no writes.
/// </summary>
public interface IDashboardQueries
{
    /// <summary>Provides awaiting availability async within this contract.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<AwaitingAvailabilityRow>> AwaitingAvailabilityAsync(
        CancellationToken cancellationToken);

    /// <summary>Provides no response async within this contract.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<NoResponseRow>> NoResponseAsync(CancellationToken cancellationToken);

    /// <summary>Provides events overview async within this contract.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<EventOverviewRow>> EventsOverviewAsync(CancellationToken cancellationToken);

    /// <summary>The latest EmailLog row per attendee that has ever had one written.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<AttendeeEmailStatusRow>> LatestEmailStatusAsync(CancellationToken cancellationToken);

    /// <summary>
    /// FR-13.1's three tabs, each with its row count, plus FR-13.2's email counts. Takes
    /// the caller shape and refuses Admin-shaped callers itself.
    /// </summary>
    /// <param name="shape">The caller shape.</param>
    /// <param name="locationId">The location id, or null for every location.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="zones">The zone abstraction.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<DashboardsView> GetDashboardsAsync(
        CallerShape shape, Guid? locationId, DateTimeOffset now,
        IEventWindowZones zones, CancellationToken ct);
}
