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
public sealed record RosterCsvFile(string FileName, string Content);

public interface IAppointmentsClient
{
    Task<ApiOutcome<WorkspaceContextDto>> GetContextAsync(CancellationToken ct);
    Task<ApiOutcome<PageDto<WorkspaceEventDto>>> ListEventsAsync(Guid? locationId, CancellationToken ct);
    Task<ApiOutcome<PageDto<WorkspaceRosterRowDto>>> GetRosterAsync(Guid eventId, CancellationToken ct);
    Task<ApiOutcome<object>> SetStatusAsync(
        Guid appointmentId, string targetStatus, long expectedVersion, CancellationToken ct);
    Task<ApiOutcome<RosterCsvFile>> DownloadRosterCsvAsync(Guid eventId, CancellationToken ct);
}

public sealed class AppointmentsClient(HttpClient http, IMeClient me) : IAppointmentsClient
{
    public AppointmentsClient(HttpClient http) : this(http, new MeClient(http)) { }

    public async Task<ApiOutcome<WorkspaceContextDto>> GetContextAsync(CancellationToken ct)
    {
        var current = await me.GetAsync(ct);
        if (!current.IsSuccess || current.Value is null)
            return ApiOutcome<WorkspaceContextDto>.Failure(current.Problem ?? Unexpected());
        if (current.Value.ScopeAppointmentTypeCode is null || current.Value.ScopeAppointmentTypeName is null)
            return ApiOutcome<WorkspaceContextDto>.Failure(ApiProblem.FromSlug(
                "unexpected", "Your staff profile has no appointment-type scope."));
        return ApiOutcome<WorkspaceContextDto>.Success(new WorkspaceContextDto(
            current.Value.ScopeAppointmentTypeCode, current.Value.ScopeAppointmentTypeName));
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

    // Fetched through the staff HttpClient, which is the only path that carries the bearer
    // token; a plain link to the route would navigate unauthenticated and receive a 401.
    public async Task<ApiOutcome<RosterCsvFile>> DownloadRosterCsvAsync(Guid eventId, CancellationToken ct)
    {
        using var response = await http.GetAsync(
            $"/api/appointment-workspace/events/{eventId}/roster.csv", ct);
        var disposition = response.Content.Headers.ContentDisposition;
        var fileName = (disposition?.FileNameStar ?? disposition?.FileName)?.Trim('"');
        return await ApiCall.ReadTextAsync(
            response,
            text => new RosterCsvFile(
                string.IsNullOrWhiteSpace(fileName) ? $"roster-{eventId}.csv" : fileName, text),
            ct);
    }

    private static ApiProblem Unexpected() =>
        ApiProblem.FromSlug("unexpected", "Something went wrong. Please try again.");

    private async Task<ApiOutcome<T>> Get<T>(string path, CancellationToken ct)
    {
        using var response = await http.GetAsync(path, ct);
        return await ApiCall.ReadAsync<T>(response, ct);
    }
}
