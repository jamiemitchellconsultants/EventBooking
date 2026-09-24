using System.Globalization;
using System.Net.Http.Json;

namespace EventBooking.Web.Services;

public sealed record EventProposalDto(
    Guid Id, Guid LocationId, string LocationCode, string LocationName, EventTimeDto Time,
    string Status, int ListedTypeCount, int AcceptedTypeCount, int? MyAcceptedHeadcount,
    bool AcceptedByMe, bool CreatedByMe, IReadOnlyList<ProposalTypeDto> Types,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
    IReadOnlyDictionary<string, ApiLink> Links);
public sealed record ProposalTypeDto(string Code, string Name);
public sealed record TypeSummaryDto(Guid Id, string Code, string Name, bool IsActive, bool HasManager);
public sealed record LocationSummaryDto(
    Guid Id, string Name, string Address, string TimeZoneId, string ZoneAbbreviation, bool IsActive);
public sealed record NegotiationReferenceData(
    IReadOnlyList<LocationSummaryDto> Locations,
    IReadOnlyList<TypeSummaryDto> AppointmentTypes,
    Guid CallerAppointmentTypeId,
    IReadOnlyDictionary<string, ApiLink> CollectionLinks);
public sealed record EventCapacityDto(
    Guid AppointmentTypeId, string Code, string Name, int TotalHeadcount, int RemainingCapacity,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
    IReadOnlyDictionary<string, ApiLink> Links);
public sealed record EventDto(
    Guid Id, Guid ProposalId, Guid LocationId, string LocationCode, string LocationName,
    EventTimeDto Time, string Status, IReadOnlyList<EventCapacityDto> Capacities,
    int ActiveBookings,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
    IReadOnlyDictionary<string, ApiLink> Links);
public sealed record ProposeEventRequest(
    Guid LocationId, DateOnly Date, TimeOnly StartTime, int DurationMinutes,
    IReadOnlyList<Guid> AppointmentTypeIds, int Headcount);
public sealed record ProposeEventOutcome(Guid ProposalId, string Status, Guid? EventId);
public sealed record RecordAcceptanceOutcome(
    Guid ProposalId, string Status, Guid? EventId, bool Changed);
public sealed record AdjustEventCapacityOutcome(
    Guid EventId, int TotalHeadcount, int RemainingCapacity, bool Changed);
public sealed record CancelEventOutcome(
    int CancelledCount, int ReinvitedCount, int AwaitingAvailabilityCount);

public interface IEventsClient
{
    Task<ApiOutcome<NegotiationReferenceData>> GetReferenceDataAsync(CancellationToken ct);
    Task<ApiOutcome<PageDto<EventProposalDto>>> ListProposalsAsync(string? cursor, CancellationToken ct);
    Task<ApiOutcome<ProposeEventOutcome>> ProposeAsync(
        ProposeEventRequest request, IdempotencySubmission submission, CancellationToken ct);
    Task<ApiOutcome<RecordAcceptanceOutcome>> RecordAcceptanceAsync(
        Guid proposalId, int headcount, CancellationToken ct);
    Task<ApiOutcome<object>> WithdrawAcceptanceAsync(Guid proposalId, CancellationToken ct);
    Task<ApiOutcome<object>> WithdrawProposalAsync(Guid proposalId, CancellationToken ct);
    Task<ApiOutcome<PageDto<EventDto>>> ListEventsAsync(
        Guid? locationId, DateOnly? from, DateOnly? to, string? cursor, CancellationToken ct);
    Task<ApiOutcome<AdjustEventCapacityOutcome>> AdjustCapacityAsync(
        Guid eventId, Guid appointmentTypeId, int totalHeadcount, CancellationToken ct);
    Task<ApiOutcome<CancelEventOutcome>> CancelAsync(Guid eventId, bool confirm, CancellationToken ct);
}

public sealed class EventsClient(HttpClient http) : IEventsClient
{
    public async Task<ApiOutcome<NegotiationReferenceData>> GetReferenceDataAsync(CancellationToken ct)
    {
        using var meResponse = await http.GetAsync("/api/me", ct);
        var me = await ApiCall.ReadAsync<MeDto>(meResponse, ct);
        if (!me.IsSuccess || me.Value is null)
            return ApiOutcome<NegotiationReferenceData>.Failure(me.Problem ?? Unexpected());
        if (me.Value.ScopeAppointmentTypeId is null)
            return ApiOutcome<NegotiationReferenceData>.Failure(ApiProblem.FromSlug(
                "unexpected", "Your staff profile has no appointment-type scope."));
        using var locationsResponse = await http.GetAsync("/api/locations?includeInactive=false", ct);
        var locations = await ApiCall.ReadAsync<PageDto<LocationDto>>(locationsResponse, ct);
        if (!locations.IsSuccess || locations.Value is null)
            return ApiOutcome<NegotiationReferenceData>.Failure(locations.Problem ?? Unexpected());
        using var typesResponse = await http.GetAsync("/api/appointment-types?includeInactive=false", ct);
        var types = await ApiCall.ReadAsync<PageDto<AppointmentTypeDto>>(typesResponse, ct);
        if (!types.IsSuccess || types.Value is null)
            return ApiOutcome<NegotiationReferenceData>.Failure(types.Problem ?? Unexpected());
        var today = DateOnly.FromDateTime(DateTime.Today);
        return ApiOutcome<NegotiationReferenceData>.Success(new NegotiationReferenceData(
            locations.Value.Items.Select(x => new LocationSummaryDto(
                x.Id, x.Name, x.Address, x.TimeZoneId, ZoneAbbreviation(x.TimeZoneId, today),
                x.IsActive)).ToArray(),
            types.Value.Items.Select(x => new TypeSummaryDto(
                x.Id, x.Code, x.Name, x.IsActive, x.HasManager)).ToArray(),
            me.Value.ScopeAppointmentTypeId.Value,
            me.Value.Links));
    }

    public Task<ApiOutcome<PageDto<EventProposalDto>>> ListProposalsAsync(string? cursor, CancellationToken ct) =>
        Get<PageDto<EventProposalDto>>("/api/event-proposals" + CursorQuery(cursor), ct);

    public Task<ApiOutcome<ProposeEventOutcome>> ProposeAsync(
        ProposeEventRequest request, IdempotencySubmission submission, CancellationToken ct) =>
        Send<ProposeEventOutcome>(HttpMethod.Post, "/api/event-proposals",
            new
            {
                locationId = request.LocationId, date = request.Date, startTime = request.StartTime,
                durationMinutes = request.DurationMinutes,
                appointmentTypeIds = request.AppointmentTypeIds, headcount = request.Headcount,
            },
            submission, ct);

    public Task<ApiOutcome<RecordAcceptanceOutcome>> RecordAcceptanceAsync(
        Guid proposalId, int headcount, CancellationToken ct) =>
        Send<RecordAcceptanceOutcome>(HttpMethod.Put, $"/api/event-proposals/{proposalId}/acceptance",
            new { headcount }, null, ct);

    public async Task<ApiOutcome<object>> WithdrawAcceptanceAsync(Guid proposalId, CancellationToken ct)
    {
        using var response = await http.DeleteAsync(
            $"/api/event-proposals/{proposalId}/acceptance", ct);
        return await NoContent(response, ct);
    }

    public async Task<ApiOutcome<object>> WithdrawProposalAsync(Guid proposalId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/event-proposals/{proposalId}/withdraw");
        using var response = await http.SendAsync(request, ct);
        return await NoContent(response, ct);
    }

    public Task<ApiOutcome<PageDto<EventDto>>> ListEventsAsync(
        Guid? locationId, DateOnly? from, DateOnly? to, string? cursor, CancellationToken ct)
    {
        var query = new List<string>();
        if (locationId is not null) query.Add($"locationId={locationId:D}");
        if (from is not null) query.Add($"from={from.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        if (to is not null) query.Add($"to={to.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        if (cursor is not null) query.Add($"cursor={Uri.EscapeDataString(cursor)}");
        var suffix = query.Count == 0 ? "" : "?" + string.Join("&", query);
        return Get<PageDto<EventDto>>("/api/events" + suffix, ct);
    }

    public Task<ApiOutcome<AdjustEventCapacityOutcome>> AdjustCapacityAsync(
        Guid eventId, Guid appointmentTypeId, int totalHeadcount, CancellationToken ct) =>
        Send<AdjustEventCapacityOutcome>(HttpMethod.Put,
            $"/api/events/{eventId}/capacities/{appointmentTypeId}",
            new { totalHeadcount }, null, ct);

    public async Task<ApiOutcome<CancelEventOutcome>> CancelAsync(
        Guid eventId, bool confirm, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/events/{eventId}/cancel?confirm={confirm.ToString().ToLowerInvariant()}");
        using var response = await http.SendAsync(request, ct);
        return await ApiCall.ReadAsync<CancelEventOutcome>(response, ct);
    }

    // Best-effort display text for the unsaved proposal preview only. Once the API
    // returns a proposal or event, its EventTimeDto abbreviation is rendered instead.
    // TimeZoneInfo carries only long names ("British Summer Time"), so the short names
    // for the zones the product uses are spelled out; anything else falls back to its
    // UTC offset, which is always truthful if less familiar.
    public static string ZoneAbbreviation(string timeZoneId, DateOnly date)
    {
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var sample = date.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Unspecified);
            var daylight = zone.IsDaylightSavingTime(sample);
            return (timeZoneId, daylight) switch
            {
                ("Europe/London", false) => "GMT",
                ("Europe/London", true) => "BST",
                ("Europe/Dublin", false) => "GMT",
                ("Europe/Dublin", true) => "IST",
                ("Asia/Tokyo", _) => "JST",
                ("Etc/UTC", _) => "UTC",
                ("UTC", _) => "UTC",
                _ => FormatOffset(zone.GetUtcOffset(sample)),
            };
        }
        catch (Exception)
        {
            return timeZoneId;
        }
    }

    private static string FormatOffset(TimeSpan offset) =>
        (offset < TimeSpan.Zero ? "-" : "+") + offset.Duration().ToString("hh\\:mm");

    private static string CursorQuery(string? cursor) =>
        cursor is null ? "" : $"?cursor={Uri.EscapeDataString(cursor)}";

    private static ApiProblem Unexpected() =>
        ApiProblem.FromSlug("unexpected", "Something went wrong. Please try again.");

    private async Task<ApiOutcome<T>> Get<T>(string path, CancellationToken ct)
    {
        using var response = await http.GetAsync(path, ct);
        return await ApiCall.ReadAsync<T>(response, ct);
    }

    private async Task<ApiOutcome<T>> Send<T>(
        HttpMethod method, string path, object body, IdempotencySubmission? submission,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        if (submission is not null) request.Headers.Add("Idempotency-Key", submission.Key);
        using var response = await http.SendAsync(request, ct);
        return await ApiCall.ReadAsync<T>(response, ct);
    }

    private static async Task<ApiOutcome<object>> NoContent(
        HttpResponseMessage response, CancellationToken ct)
    {
        var outcome = await ApiCall.ReadNoContentAsync(response, ct);
        return outcome.IsSuccess
            ? ApiOutcome<object>.Success(new object(), outcome.StatusCode)
            : ApiOutcome<object>.Failure(
                outcome.Problem ?? ApiProblem.FromSlug("unexpected", "Something went wrong. Please try again."));
    }
}
