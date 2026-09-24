namespace EventBooking.Web.Services;

public sealed record AuditRowDto(
    Guid Id, DateTimeOffset Timestamp, string EntityType, Guid EntityId, string Action,
    string ActorType, string? ActorId, string? ActorDisplay, string? Details,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
    IReadOnlyDictionary<string, ApiLink> Links);
public sealed record AuditFilters(
    DateTimeOffset? From, DateTimeOffset? To, string? ActorType, string? Action,
    string? EntityType, Guid? EntityId, string? ActorId);

public interface IAuditClient
{
    Task<ApiOutcome<PageDto<AuditRowDto>>> SearchAsync(
        AuditFilters filter, string? cursor, CancellationToken ct);
    Task<ApiOutcome<PageDto<AuditRowDto>>> ForAttendeeAsync(
        Guid attendeeId, string? cursor, CancellationToken ct);
    Task<ApiOutcome<PageDto<AuditRowDto>>> ForEventAsync(
        Guid eventId, string? cursor, CancellationToken ct);
}

public sealed class AuditClient(HttpClient http) : IAuditClient
{
    public Task<ApiOutcome<PageDto<AuditRowDto>>> SearchAsync(
        AuditFilters filter, string? cursor, CancellationToken ct)
    {
        var query = new List<string>();
        if (filter.From is not null)
            query.Add($"from={Uri.EscapeDataString(filter.From.Value.ToString("O"))}");
        if (filter.To is not null)
            query.Add($"to={Uri.EscapeDataString(filter.To.Value.ToString("O"))}");
        if (filter.ActorType is not null)
            query.Add($"actorType={Uri.EscapeDataString(filter.ActorType)}");
        if (filter.Action is not null)
            query.Add($"action={Uri.EscapeDataString(filter.Action)}");
        if (filter.EntityType is not null)
            query.Add($"entityType={Uri.EscapeDataString(filter.EntityType)}");
        if (filter.EntityId is not null) query.Add($"entityId={filter.EntityId:D}");
        if (filter.ActorId is not null)
            query.Add($"actorId={Uri.EscapeDataString(filter.ActorId)}");
        if (cursor is not null) query.Add($"cursor={Uri.EscapeDataString(cursor)}");
        var suffix = query.Count == 0 ? "" : "?" + string.Join("&", query);
        return Get<PageDto<AuditRowDto>>("/api/audit" + suffix, ct);
    }

    public Task<ApiOutcome<PageDto<AuditRowDto>>> ForAttendeeAsync(
        Guid attendeeId, string? cursor, CancellationToken ct) =>
        Get<PageDto<AuditRowDto>>(
            $"/api/audit/attendees/{attendeeId}" + CursorQuery(cursor), ct);

    public Task<ApiOutcome<PageDto<AuditRowDto>>> ForEventAsync(
        Guid eventId, string? cursor, CancellationToken ct) =>
        Get<PageDto<AuditRowDto>>(
            $"/api/audit/events/{eventId}" + CursorQuery(cursor), ct);

    private static string CursorQuery(string? cursor) =>
        cursor is null ? "" : $"?cursor={Uri.EscapeDataString(cursor)}";

    private async Task<ApiOutcome<T>> Get<T>(string path, CancellationToken ct)
    {
        using var response = await http.GetAsync(path, ct);
        return await ApiCall.ReadAsync<T>(response, ct);
    }
}
