using System.Net.Http.Json;

namespace EventBooking.Web.Services;

public sealed record AttendeeDto(
    Guid AttendeeId, string Name, string Email, string Status, string StatusDisplay,
    string GroupCode, string Readiness, IReadOnlyList<string> RequiredTypeCodes,
    string? LatestDeliveryStatus, string Cursor, Guid? LatestDeliveryId,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
    IReadOnlyDictionary<string, ApiLink> Links);
public sealed record CoordinatorLocationDto(Guid Id, string Name, string ZoneAbbreviation);
public sealed record CoordinatorGroupDto(
    Guid Id, string Code, string Name, IReadOnlyList<Guid> RequirementTypeIds);
public sealed record CoordinatorReferenceData(
    IReadOnlyList<CoordinatorLocationDto> Locations,
    IReadOnlyList<CoordinatorGroupDto> Groups,
    IReadOnlyList<TypeSummaryDto> AppointmentTypes,
    IReadOnlyDictionary<string, ApiLink> CollectionLinks);
public sealed record EligibleEventCountDto(int Count, int RequiredOptionCount);
public sealed record ImportErrorDto(int LineNumber, string Message);
public sealed record ImportOutcomeDto(bool Accepted, int ImportedCount, IReadOnlyList<ImportErrorDto> Errors);
public sealed record InviteOutcomeDto(Guid? InviteId, string Status);
public sealed record EmailRetryDto(Guid EmailLogId);
public sealed record AttendeeBookingDto(
    Guid BookingId, bool IsOriginal, DateOnly EventDate, TimeOnly EventStartTime, TimeOnly EventEndTime,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
    IReadOnlyDictionary<string, ApiLink> Links);
public sealed record CancelAttendeeBookingDto(
    bool ConfirmationRequired, int ActiveBookingCount, Guid? CancelledBookingId,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
    IReadOnlyDictionary<string, ApiLink> Links);
public sealed record AttendeeReadinessDto(
    Guid AttendeeId, string Code, string Display,
    IReadOnlyList<OutstandingAppointmentTypeDto> OutstandingAppointmentTypes,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
    IReadOnlyDictionary<string, ApiLink> Links);
public sealed record OutstandingAppointmentTypeDto(string Code, string Name, bool IsRecoverable);
public sealed record RecoveryInviteOutcomeDto(
    Guid RecoveryInviteId, IReadOnlyList<Guid> LocationIds, IReadOnlyList<Guid> RecoverableTypeIds,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
    IReadOnlyDictionary<string, ApiLink> Links);

public interface IAttendeesClient
{
    Task<ApiOutcome<CoordinatorReferenceData>> GetReferenceDataAsync(CancellationToken ct);
    Task<ApiOutcome<PageDto<AttendeeDto>>> ListAsync(
        string? status, Guid? groupId, string? readiness, string? search, string? cursor,
        CancellationToken ct);
    Task<ApiOutcome<Guid>> CreateAsync(
        string name, string email, Guid? groupId, IdempotencySubmission submission,
        CancellationToken ct);
    Task<ApiOutcome<bool>> UpdateAsync(
        Guid id, string name, string email, Guid? groupId, CancellationToken ct);
    Task<ApiOutcome<bool>> DeleteAsync(Guid id, bool confirm, CancellationToken ct);
    Task<ApiOutcome<ImportOutcomeDto>> ImportAsync(
        Stream csv, string fileName, IdempotencySubmission submission, CancellationToken ct);
    Task<ApiOutcome<EligibleEventCountDto>> CountEligibleAsync(
        Guid id, IReadOnlyList<Guid> locationIds, CancellationToken ct);
    Task<ApiOutcome<InviteOutcomeDto>> InviteAsync(
        Guid id, IReadOnlyList<Guid> locationIds, IdempotencySubmission submission,
        CancellationToken ct);
    Task<ApiOutcome<EmailRetryDto>> RetryEmailAsync(Guid id, CancellationToken ct);
    Task<ApiOutcome<PageDto<AttendeeBookingDto>>> GetBookingsAsync(
        Guid attendeeId, CancellationToken ct);
    Task<ApiOutcome<CancelAttendeeBookingDto>> CancelBookingAsync(
        Guid attendeeId, Guid bookingId, bool confirm, CancellationToken ct);
    Task<ApiOutcome<RecoveryInviteOutcomeDto>> StartRecoveryAsync(
        Guid attendeeId, CancellationToken ct);
    Task<ApiOutcome<bool>> CancelRecoveryAsync(
        Guid attendeeId, Guid inviteId, CancellationToken ct);
    Task<ApiOutcome<AttendeeReadinessDto>> GetReadinessAsync(
        Guid attendeeId, CancellationToken ct);
}

public sealed class AttendeesClient(HttpClient http, IMeClient me) : IAttendeesClient
{
    public AttendeesClient(HttpClient http) : this(http, new MeClient(http)) { }

    public async Task<ApiOutcome<CoordinatorReferenceData>> GetReferenceDataAsync(CancellationToken ct)
    {
        var current = await me.GetAsync(ct);
        if (!current.IsSuccess || current.Value is null)
            return ApiOutcome<CoordinatorReferenceData>.Failure(current.Problem ?? Unexpected());
        using var locationsResponse = await http.GetAsync("/api/locations?includeInactive=false", ct);
        var locations = await ApiCall.ReadAsync<PageDto<LocationDto>>(locationsResponse, ct);
        if (!locations.IsSuccess || locations.Value is null)
            return ApiOutcome<CoordinatorReferenceData>.Failure(locations.Problem ?? Unexpected());
        using var groupsResponse = await http.GetAsync("/api/attendee-groups?includeInactive=false", ct);
        var groups = await ApiCall.ReadAsync<PageDto<AttendeeGroupDto>>(groupsResponse, ct);
        if (!groups.IsSuccess || groups.Value is null)
            return ApiOutcome<CoordinatorReferenceData>.Failure(groups.Problem ?? Unexpected());
        using var typesResponse = await http.GetAsync("/api/appointment-types?includeInactive=false", ct);
        var types = await ApiCall.ReadAsync<PageDto<AppointmentTypeDto>>(typesResponse, ct);
        if (!types.IsSuccess || types.Value is null)
            return ApiOutcome<CoordinatorReferenceData>.Failure(types.Problem ?? Unexpected());
        var today = DateOnly.FromDateTime(DateTime.Today);
        return ApiOutcome<CoordinatorReferenceData>.Success(new CoordinatorReferenceData(
            locations.Value.Items.Select(x => new CoordinatorLocationDto(
                x.Id, x.Name, EventsClient.ZoneAbbreviation(x.TimeZoneId, today))).ToArray(),
            groups.Value.Items.Select(x => new CoordinatorGroupDto(
                x.Id, x.Code, x.Name, x.RequirementTypeIds)).ToArray(),
            types.Value.Items.Select(x => new TypeSummaryDto(
                x.Id, x.Code, x.Name, x.IsActive, x.HasManager)).ToArray(),
            current.Value.Links));
    }

    public Task<ApiOutcome<PageDto<AttendeeDto>>> ListAsync(
        string? status, Guid? groupId, string? readiness, string? search, string? cursor,
        CancellationToken ct)
    {
        var query = new List<string>();
        if (status is not null) query.Add($"status={Uri.EscapeDataString(status)}");
        if (groupId is not null) query.Add($"groupId={groupId:D}");
        if (readiness is not null) query.Add($"readiness={Uri.EscapeDataString(readiness)}");
        if (search is not null) query.Add($"search={Uri.EscapeDataString(search)}");
        if (cursor is not null) query.Add($"cursor={Uri.EscapeDataString(cursor)}");
        var suffix = query.Count == 0 ? "" : "?" + string.Join("&", query);
        return Get<PageDto<AttendeeDto>>("/api/attendees" + suffix, ct);
    }

    public async Task<ApiOutcome<Guid>> CreateAsync(
        string name, string email, Guid? groupId, IdempotencySubmission submission,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/attendees")
        {
            Content = JsonContent.Create(new { name, email, attendeeGroupId = groupId }),
        };
        request.Headers.Add("Idempotency-Key", submission.Key);
        using var response = await http.SendAsync(request, ct);
        return await ApiCall.ReadAsync<Guid>(response, ct);
    }

    public async Task<ApiOutcome<bool>> UpdateAsync(
        Guid id, string name, string email, Guid? groupId, CancellationToken ct)
    {
        using var response = await http.PutAsJsonAsync(
            $"/api/attendees/{id}", new { name, email, attendeeGroupId = groupId }, ct);
        return await ApiCall.ReadNoContentAsync(response, ct);
    }

    public async Task<ApiOutcome<bool>> DeleteAsync(Guid id, bool confirm, CancellationToken ct)
    {
        using var response = await http.DeleteAsync(
            $"/api/attendees/{id}?confirm={confirm.ToString().ToLowerInvariant()}", ct);
        return await ApiCall.ReadNoContentAsync(response, ct);
    }

    public async Task<ApiOutcome<ImportOutcomeDto>> ImportAsync(
        Stream csv, string fileName, IdempotencySubmission submission, CancellationToken ct)
    {
        using var content = new MultipartFormDataContent();
        using var file = new StreamContent(csv);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        content.Add(file, "file", fileName);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/attendees/import")
        {
            Content = content,
        };
        request.Headers.Add("Idempotency-Key", submission.Key);
        using var response = await http.SendAsync(request, ct);
        return await ApiCall.ReadAsync<ImportOutcomeDto>(response, ct);
    }

    public Task<ApiOutcome<EligibleEventCountDto>> CountEligibleAsync(
        Guid id, IReadOnlyList<Guid> locationIds, CancellationToken ct) =>
        Get<EligibleEventCountDto>(
            $"/api/attendees/{id}/eligible-event-count" +
            (locationIds.Count == 0
                ? ""
                : "?" + string.Join("&", locationIds.Select(x => $"locationIds={x:D}"))),
            ct);

    public async Task<ApiOutcome<InviteOutcomeDto>> InviteAsync(
        Guid id, IReadOnlyList<Guid> locationIds, IdempotencySubmission submission,
        CancellationToken ct)
    {
        // An empty selection means every active location: the request carries null
        // rather than an empty array, which the handler would read as no locations.
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/attendees/{id}/invites")
        {
            Content = JsonContent.Create(new
            {
                locationIds = locationIds.Count == 0 ? null : locationIds,
            }),
        };
        request.Headers.Add("Idempotency-Key", submission.Key);
        using var response = await http.SendAsync(request, ct);
        return await ApiCall.ReadAsync<InviteOutcomeDto>(response, ct);
    }

    public async Task<ApiOutcome<EmailRetryDto>> RetryEmailAsync(Guid id, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/attendees/{id}/email-retry");
        using var response = await http.SendAsync(request, ct);
        return await ApiCall.ReadAsync<EmailRetryDto>(response, ct);
    }

    public Task<ApiOutcome<PageDto<AttendeeBookingDto>>> GetBookingsAsync(
        Guid attendeeId, CancellationToken ct) =>
        Get<PageDto<AttendeeBookingDto>>($"/api/attendees/{attendeeId}/bookings", ct);

    public async Task<ApiOutcome<CancelAttendeeBookingDto>> CancelBookingAsync(
        Guid attendeeId, Guid bookingId, bool confirm, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/attendees/{attendeeId}/bookings/{bookingId}/cancel?confirm={confirm.ToString().ToLowerInvariant()}");
        using var response = await http.SendAsync(request, ct);
        return await ApiCall.ReadAsync<CancelAttendeeBookingDto>(response, ct);
    }

    public async Task<ApiOutcome<RecoveryInviteOutcomeDto>> StartRecoveryAsync(
        Guid attendeeId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/attendees/{attendeeId}/recovery-invites");
        using var response = await http.SendAsync(request, ct);
        return await ApiCall.ReadAsync<RecoveryInviteOutcomeDto>(response, ct);
    }

    public async Task<ApiOutcome<bool>> CancelRecoveryAsync(
        Guid attendeeId, Guid inviteId, CancellationToken ct)
    {
        using var response = await http.DeleteAsync(
            $"/api/attendees/{attendeeId}/recovery-invites/{inviteId}", ct);
        return await ApiCall.ReadNoContentAsync(response, ct);
    }

    public Task<ApiOutcome<AttendeeReadinessDto>> GetReadinessAsync(
        Guid attendeeId, CancellationToken ct) =>
        Get<AttendeeReadinessDto>($"/api/attendees/{attendeeId}/readiness", ct);

    private static ApiProblem Unexpected() =>
        ApiProblem.FromSlug("unexpected", "Something went wrong. Please try again.");

    private async Task<ApiOutcome<T>> Get<T>(string path, CancellationToken ct)
    {
        using var response = await http.GetAsync(path, ct);
        return await ApiCall.ReadAsync<T>(response, ct);
    }
}
