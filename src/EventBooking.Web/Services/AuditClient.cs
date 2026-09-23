namespace EventBooking.Web.Services;

public sealed record AuditRowDto(
    DateTimeOffset Timestamp,
    string EntityType,
    Guid EntityId,
    string Action,
    string ActorType,
    string? ActorId,
    string? Details);

/// <summary>Filter criteria for the cross-cutting audit search.</summary>
/// <param name="From">Inclusive lower bound on the recorded timestamp, or null.</param>
/// <param name="To">Inclusive upper bound on the recorded timestamp, or null.</param>
/// <param name="ActorType">Actor type name to match, or null for any.</param>
/// <param name="Action">Audit action name to match, or null for any.</param>
/// <param name="Identifier">Free-text identifier matched exactly against entity id or actor id.</param>
/// <param name="EntityType">Optional single entity type within the caller's allowed bucket.</param>
/// <param name="Cursor">Opaque keyset cursor, or null for the newest page.</param>
/// <param name="PageSize">Rows per page.</param>
public sealed record AuditSearchFilterDto(
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? ActorType,
    string? Action,
    string? Identifier,
    string? EntityType,
    string? Cursor,
    int PageSize);

/// <summary>One page of audit search results, newest first.</summary>
/// <param name="Rows">The result rows in newest-first order.</param>
/// <param name="NextCursor">Opaque cursor for the following page, or null when exhausted.</param>
public sealed record AuditSearchPageDto(
    List<AuditRowDto> Rows,
    string? NextCursor);

public sealed class AuditClient(HttpClient http)
{
    public async Task<ApiOutcome<List<AuditRowDto>>> ForEventAsync(
        Guid eventId, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync($"/api/audit/event/{eventId}", cancellationToken);
        return await ApiCall.ReadAsync<List<AuditRowDto>>(response, cancellationToken);
    }

    public async Task<ApiOutcome<List<AuditRowDto>>> ForAttendeeAsync(
        Guid attendeeId, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync($"/api/audit/attendee/{attendeeId}", cancellationToken);
        return await ApiCall.ReadAsync<List<AuditRowDto>>(response, cancellationToken);
    }

    /// <summary>Searches the audit log newest-first with keyset pagination.</summary>
    /// <param name="filter">The search criteria; absent criteria are omitted from the query string.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The requested page, or the failure the API reported.</returns>
    public async Task<ApiOutcome<AuditSearchPageDto>> SearchAsync(
        AuditSearchFilterDto filter, CancellationToken cancellationToken)
    {
        var query = new List<string>();
        if (filter.From is not null)
        {
            query.Add($"from={Uri.EscapeDataString(filter.From.Value.ToString("O"))}");
        }

        if (filter.To is not null)
        {
            query.Add($"to={Uri.EscapeDataString(filter.To.Value.ToString("O"))}");
        }

        if (filter.ActorType is not null)
        {
            query.Add($"actorType={Uri.EscapeDataString(filter.ActorType)}");
        }

        if (filter.Action is not null)
        {
            query.Add($"action={Uri.EscapeDataString(filter.Action)}");
        }

        if (filter.Identifier is not null)
        {
            query.Add($"identifier={Uri.EscapeDataString(filter.Identifier)}");
        }

        if (filter.EntityType is not null)
        {
            query.Add($"entityType={Uri.EscapeDataString(filter.EntityType)}");
        }

        if (filter.Cursor is not null)
        {
            query.Add($"cursor={Uri.EscapeDataString(filter.Cursor)}");
        }

        query.Add($"pageSize={filter.PageSize}");

        using var response = await http.GetAsync(
            $"/api/audit/search?{string.Join("&", query)}", cancellationToken);
        return await ApiCall.ReadAsync<AuditSearchPageDto>(response, cancellationToken);
    }
}
