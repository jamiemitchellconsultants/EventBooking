using System.Net.Http.Json;

namespace EventBooking.Web.Services;

public sealed record LocationDto(Guid Id, string Code, string Name, string Address, string TimeZoneId,
    bool IsActive, long Version, [property: System.Text.Json.Serialization.JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);
public sealed record AppointmentTypeDto(Guid Id, string Code, string Name, bool IsActive, long Version,
    bool HasManager, string? ManagerDisplayName, [property: System.Text.Json.Serialization.JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);
public sealed record AttendeeGroupDto(Guid Id, string Code, string Name, bool IsActive, long Version,
    IReadOnlyList<Guid> RequirementTypeIds, int MemberCount,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);
public sealed record SettingsDto(int InviteExpiryDays, int MaxAutoRetryCount, int InviteOptionCount,
    long Version, [property: System.Text.Json.Serialization.JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

public sealed class AdminClient(HttpClient http)
{
    public Task<ApiOutcome<PageDto<LocationDto>>> ListLocationsAsync(bool inactive, CancellationToken ct) => Get<PageDto<LocationDto>>($"/api/locations?includeInactive={inactive.ToString().ToLowerInvariant()}", ct);
    public Task<ApiOutcome<PageDto<AppointmentTypeDto>>> ListAppointmentTypesAsync(bool inactive, CancellationToken ct) => Get<PageDto<AppointmentTypeDto>>($"/api/appointment-types?includeInactive={inactive.ToString().ToLowerInvariant()}", ct);
    public Task<ApiOutcome<PageDto<AttendeeGroupDto>>> ListAttendeeGroupsAsync(bool inactive, CancellationToken ct) => Get<PageDto<AttendeeGroupDto>>($"/api/attendee-groups?includeInactive={inactive.ToString().ToLowerInvariant()}", ct);
    public Task<ApiOutcome<SettingsDto>> GetSettingsAsync(CancellationToken ct) => Get<SettingsDto>("/api/settings", ct);

    public Task<ApiOutcome<LocationDto>> CreateLocationAsync(string code, string name, string address, string zone, IdempotencySubmission submission, CancellationToken ct) => Send<LocationDto>(HttpMethod.Post, "/api/locations", new { code, name, address, timeZoneId = zone }, submission, ct);
    public Task<ApiOutcome<LocationDto>> UpdateLocationAsync(LocationDto value, CancellationToken ct) => Send<LocationDto>(HttpMethod.Put, $"/api/locations/{value.Id}", new { value.Name, value.Address, value.TimeZoneId, value.IsActive, expectedVersion = value.Version }, null, ct);
    public Task<ApiOutcome<AppointmentTypeDto>> CreateAppointmentTypeAsync(string code, string name, IdempotencySubmission submission, CancellationToken ct) => Send<AppointmentTypeDto>(HttpMethod.Post, "/api/appointment-types", new { code, name }, submission, ct);
    public Task<ApiOutcome<AppointmentTypeDto>> UpdateAppointmentTypeAsync(AppointmentTypeDto value, CancellationToken ct) => Send<AppointmentTypeDto>(HttpMethod.Put, $"/api/appointment-types/{value.Id}", new { value.Name, value.IsActive, expectedVersion = value.Version }, null, ct);
    public Task<ApiOutcome<AttendeeGroupDto>> CreateAttendeeGroupAsync(string code, string name, IReadOnlyList<Guid> ids, IdempotencySubmission submission, CancellationToken ct) => Send<AttendeeGroupDto>(HttpMethod.Post, "/api/attendee-groups", new { code, name, appointmentTypeIds = ids }, submission, ct);
    public Task<ApiOutcome<AttendeeGroupDto>> UpdateAttendeeGroupAsync(AttendeeGroupDto value, CancellationToken ct) => Send<AttendeeGroupDto>(HttpMethod.Put, $"/api/attendee-groups/{value.Id}", new { value.Name, appointmentTypeIds = value.RequirementTypeIds, value.IsActive, expectedVersion = value.Version }, null, ct);
    public Task<ApiOutcome<SettingsDto>> UpdateSettingsAsync(SettingsDto value, CancellationToken ct) => Send<SettingsDto>(HttpMethod.Put, "/api/settings", new { value.InviteExpiryDays, value.MaxAutoRetryCount, value.InviteOptionCount, expectedVersion = value.Version }, null, ct);

    private async Task<ApiOutcome<T>> Get<T>(string path, CancellationToken ct) { using var response = await http.GetAsync(path, ct); return await ApiCall.ReadAsync<T>(response, ct); }
    private async Task<ApiOutcome<T>> Send<T>(HttpMethod method, string path, object body, IdempotencySubmission? submission, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        if (submission is not null) request.Headers.Add("Idempotency-Key", submission.Key);
        using var response = await http.SendAsync(request, ct);
        return await ApiCall.ReadAsync<T>(response, ct);
    }
}
