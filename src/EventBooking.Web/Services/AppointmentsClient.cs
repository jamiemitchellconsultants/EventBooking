using System.Net.Http.Json;

namespace EventBooking.Web.Services;

public sealed record WorkspaceEventDto(
    Guid EventId, Guid LocationId, string LocationName, EventTimeDto Time, string Status,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
    IReadOnlyDictionary<string, ApiLink> Links);
public sealed record WorkspaceRosterRowDto(
    Guid AppointmentId, string Name, string Email, string ScopeTypeCode,
    string AppointmentStatus, DateTimeOffset? CheckedInAt, long Version,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
    IReadOnlyDictionary<string, ApiLink> Links);
public sealed record WorkspaceContextDto(string ScopeTypeCode, string ScopeTypeName);

public interface IAppointmentsClient
{
    Task<ApiOutcome<WorkspaceContextDto>> GetContextAsync(CancellationToken ct);
    Task<ApiOutcome<PageDto<WorkspaceEventDto>>> ListEventsAsync(Guid? locationId, CancellationToken ct);
    Task<ApiOutcome<PageDto<WorkspaceRosterRowDto>>> GetRosterAsync(Guid eventId, CancellationToken ct);
    Task<ApiOutcome<object>> SetStatusAsync(
        Guid appointmentId, string targetStatus, long expectedVersion, CancellationToken ct);
    Uri RosterCsvUri(Guid eventId);
}

public sealed class AppointmentsClient(HttpClient http) : IAppointmentsClient
{
    public async Task<ApiOutcome<WorkspaceContextDto>> GetContextAsync(CancellationToken ct)
    {
        using var response = await http.GetAsync("/api/me", ct);
        var me = await ApiCall.ReadAsync<MeDto>(response, ct);
        if (!me.IsSuccess || me.Value is null)
            return ApiOutcome<WorkspaceContextDto>.Failure(me.Problem ?? Unexpected());
        if (me.Value.ScopeAppointmentTypeCode is null || me.Value.ScopeAppointmentTypeName is null)
            return ApiOutcome<WorkspaceContextDto>.Failure(ApiProblem.FromSlug(
                "unexpected", "Your staff profile has no appointment-type scope."));
        return ApiOutcome<WorkspaceContextDto>.Success(new WorkspaceContextDto(
            me.Value.ScopeAppointmentTypeCode, me.Value.ScopeAppointmentTypeName));
    }

    public Task<ApiOutcome<PageDto<WorkspaceEventDto>>> ListEventsAsync(
        Guid? locationId, CancellationToken ct) =>
        Get<PageDto<WorkspaceEventDto>>(
            "/api/appointment-workspace/events" + (locationId is null ? "" : $"?locationId={locationId:D}"),
            ct);

    public Task<ApiOutcome<PageDto<WorkspaceRosterRowDto>>> GetRosterAsync(
        Guid eventId, CancellationToken ct) =>
        Get<PageDto<WorkspaceRosterRowDto>>($"/api/appointment-workspace/events/{eventId}", ct);

    public async Task<ApiOutcome<object>> SetStatusAsync(
        Guid appointmentId, string targetStatus, long expectedVersion, CancellationToken ct)
    {
        using var response = await http.PutAsJsonAsync(
            $"/api/appointment-workspace/appointments/{appointmentId}/status",
            new { targetStatus, expectedVersion }, ct);
        return await ApiCall.ReadAsync<object>(response, ct);
    }

    public Uri RosterCsvUri(Guid eventId) =>
        new(http.BaseAddress ?? throw new InvalidOperationException("The API base address is not configured."),
            $"/api/appointment-workspace/events/{eventId}/roster.csv");

    private static ApiProblem Unexpected() =>
        ApiProblem.FromSlug("unexpected", "Something went wrong. Please try again.");

    private async Task<ApiOutcome<T>> Get<T>(string path, CancellationToken ct)
    {
        using var response = await http.GetAsync(path, ct);
        return await ApiCall.ReadAsync<T>(response, ct);
    }
}
