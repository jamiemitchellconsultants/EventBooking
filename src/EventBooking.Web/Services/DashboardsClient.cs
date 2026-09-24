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
    Guid LocationId,
    string LocationName,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyList<EventCapacityRowDto> Capacities,
    int ActiveBookings);

public sealed record DashboardTabDto<T>(int Count, IReadOnlyList<T> Rows);

public sealed record DashboardsDto(
    DashboardTabDto<AwaitingRowDto> AwaitingAvailability,
    DashboardTabDto<NoResponseRowDto> NoResponse,
    DashboardTabDto<EventRowDto> Events,
    int FailedEmails,
    int PendingEmails);

public sealed class DashboardsClient(HttpClient http)
{
    public async Task<ApiOutcome<DashboardsDto>> GetAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("/api/dashboards", cancellationToken);
        return await ApiCall.ReadAsync<DashboardsDto>(response, cancellationToken);
    }
}
