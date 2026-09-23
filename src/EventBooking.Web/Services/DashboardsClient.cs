namespace EventBooking.Web.Services;

public sealed record AwaitingRowDto(
    Guid AttendeeId,
    string Name,
    string Email,
    IReadOnlyList<string> RequiredCodes,
    DateOnly WaitingSince,
    int DaysWaiting);

public sealed record NoResponseRowDto(
    Guid AttendeeId,
    string Name,
    string Email,
    IReadOnlyList<string> RequiredCodes,
    DateOnly GaveUpOn);

public sealed record EventCapacityRowDto(string Code, int TotalHeadcount, int RemainingCapacity);

public sealed record EventRowDto(
    Guid EventId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyList<EventCapacityRowDto> Capacities,
    int ActiveBookings);

/// <summary>The latest attendee delivery status projected for staff pages.</summary>
/// <param name="AttendeeId">The attendee whose delivery is shown.</param>
/// <param name="TemplateDisplay">Human-readable template name.</param>
/// <param name="SentAt">The latest attempt or pending timestamp.</param>
/// <param name="Status">The durable delivery status.</param>
/// <param name="CanRetry">Whether current server-side state permits a retry.</param>
public sealed record AttendeeEmailStatusDto(
    Guid AttendeeId,
    string TemplateDisplay,
    DateTimeOffset SentAt,
    string Status,
    bool CanRetry);

public sealed record DashboardsDto(
    IReadOnlyList<AwaitingRowDto> AwaitingAvailability,
    IReadOnlyList<NoResponseRowDto> NoResponse,
    IReadOnlyList<EventRowDto> Events,
    IReadOnlyList<AttendeeEmailStatusDto> EmailStatuses);

public sealed class DashboardsClient(HttpClient http)
{
    public async Task<ApiOutcome<DashboardsDto>> GetAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("/api/dashboards", cancellationToken);
        return await ApiCall.ReadAsync<DashboardsDto>(response, cancellationToken);
    }
}
