namespace EventBooking.Web.Services;

public sealed record AwaitingAvailabilityDto(
    Guid AttendeeId, string Name, string Email, IReadOnlyList<string> RequiredCodes,
    DateOnly WaitingSince, int DaysWaiting);
public sealed record NoResponseDto(
    Guid AttendeeId, string Name, string Email, IReadOnlyList<string> RequiredCodes,
    DateOnly GaveUpOn);
public sealed record DashboardCapacityDto(string Code, int TotalHeadcount, int RemainingCapacity);
public sealed record EventOverviewDto(
    Guid EventId, Guid LocationId, string LocationName, EventTimeDto Time,
    IReadOnlyList<DashboardCapacityDto> Capacities, int ActiveBookings,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
    IReadOnlyDictionary<string, ApiLink> Links);
public sealed record DashboardCountedTab<T>(int Count, IReadOnlyList<T> Rows);
public sealed record DashboardsDto(
    DashboardCountedTab<AwaitingAvailabilityDto> AwaitingAvailability,
    DashboardCountedTab<NoResponseDto> NoResponse,
    DashboardCountedTab<EventOverviewDto> Events,
    int FailedEmails, int PendingEmails,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
    IReadOnlyDictionary<string, ApiLink> Links);

public interface IDashboardsClient
{
    Task<ApiOutcome<DashboardsDto>> GetAsync(Guid? locationId, CancellationToken ct);
}

public sealed class DashboardsClient(HttpClient http) : IDashboardsClient
{
    public async Task<ApiOutcome<DashboardsDto>> GetAsync(Guid? locationId, CancellationToken ct)
    {
        using var response = await http.GetAsync(
            "/api/dashboards" + (locationId is null ? "" : $"?locationId={locationId:D}"), ct);
        return await ApiCall.ReadAsync<DashboardsDto>(response, ct);
    }
}
