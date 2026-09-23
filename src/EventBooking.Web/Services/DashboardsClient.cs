namespace EventBooking.Web.Services;

public sealed record AwaitingRowDto(
    Guid CandidateId,
    string Name,
    string Email,
    IReadOnlyList<string> RequiredCodes,
    DateOnly WaitingSince,
    int DaysWaiting);

public sealed record NoResponseRowDto(
    Guid CandidateId,
    string Name,
    string Email,
    IReadOnlyList<string> RequiredCodes,
    DateOnly GaveUpOn);

public sealed record SlotCapacityRowDto(string Code, int TotalHeadcount, int RemainingCapacity);

public sealed record SlotRowDto(
    Guid ConfirmedSlotId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyList<SlotCapacityRowDto> Capacities,
    int ActiveBookings);

/// <summary>The latest candidate delivery status projected for staff pages.</summary>
/// <param name="CandidateId">The candidate whose delivery is shown.</param>
/// <param name="TemplateDisplay">Human-readable template name.</param>
/// <param name="SentAt">The latest attempt or pending timestamp.</param>
/// <param name="Status">The durable delivery status.</param>
/// <param name="CanRetry">Whether current server-side state permits a retry.</param>
public sealed record CandidateEmailStatusDto(
    Guid CandidateId,
    string TemplateDisplay,
    DateTimeOffset SentAt,
    string Status,
    bool CanRetry);

public sealed record DashboardsDto(
    IReadOnlyList<AwaitingRowDto> AwaitingAvailability,
    IReadOnlyList<NoResponseRowDto> NoResponse,
    IReadOnlyList<SlotRowDto> Slots,
    IReadOnlyList<CandidateEmailStatusDto> EmailStatuses);

public sealed class DashboardsClient(HttpClient http)
{
    public async Task<ApiOutcome<DashboardsDto>> GetAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("/api/dashboards", cancellationToken);
        return await ApiCall.ReadAsync<DashboardsDto>(response, cancellationToken);
    }
}
