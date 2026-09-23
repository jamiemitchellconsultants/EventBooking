namespace EventBooking.Application.Abstractions;

/// <summary>A single row of audit history for one state change recorded in the audit log.</summary>
/// <param name="Timestamp">When the audited state change was recorded.</param>
/// <param name="EntityType">The audited entity type the row describes.</param>
/// <param name="EntityId">The identifier of the audited entity instance.</param>
/// <param name="Action">The recorded audit action name.</param>
/// <param name="ActorType">Who caused the change: staff, candidate token, or system.</param>
/// <param name="ActorId">The actor identifier, or null for a system actor.</param>
/// <param name="Details">Fixed identifiers, codes, and statuses only; never personal data.</param>
public sealed record AuditHistoryRow(
    DateTimeOffset Timestamp,
    string EntityType,
    Guid EntityId,
    string Action,
    string ActorType,
    string? ActorId,
    string? Details);

/// <summary>Cross-cutting audit search criteria. AllowedEntityTypes is set by the handler, never by the caller.</summary>
/// <param name="From">Inclusive lower bound on the recorded timestamp, or null for no bound.</param>
/// <param name="To">Inclusive upper bound on the recorded timestamp, or null for no bound.</param>
/// <param name="ActorType">Actor type name to match, or null for any.</param>
/// <param name="Action">Audit action name to match, or null for any.</param>
/// <param name="Identifier">Free-text identifier matched exactly against entity id or actor id, or null.</param>
/// <param name="AllowedEntityTypes">Entity types the caller may see, computed from granted capabilities.</param>
/// <param name="EntityType">Optional single entity type requested within the allowed bucket.</param>
/// <param name="Cursor">Opaque keyset cursor for the next page, or null for the newest page.</param>
/// <param name="PageSize">Rows per page; clamped to 200 by the implementation.</param>
public sealed record AuditSearchFilter(
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? ActorType,
    string? Action,
    string? Identifier,
    IReadOnlyList<string> AllowedEntityTypes,
    string? EntityType,
    string? Cursor,
    int PageSize = 50);

/// <summary>One page of audit search results, newest first.</summary>
/// <param name="Rows">The result rows in newest-first order.</param>
/// <param name="NextCursor">Opaque cursor for the following page, or null when exhausted.</param>
public sealed record AuditSearchPage(
    IReadOnlyList<AuditHistoryRow> Rows,
    string? NextCursor);

/// <summary>Defines iaudit queries for the current use case.</summary>
public interface IAuditQueries
{
    /// <summary>Provides for entity async within this contract.</summary>
    /// <param name="entityType">The entity type.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<AuditHistoryRow>> ForEntityAsync(
        string entityType, Guid entityId, CancellationToken cancellationToken);

    /// <summary>
    /// Every entry recorded against the candidate record itself and against their invites, bookings,
    /// and booking appointments, newest first. The caller must already hold candidate-audit access.
    /// </summary>
    /// <param name="candidateId">The candidate id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<AuditHistoryRow>> ForCandidateAsync(
        Guid candidateId, CancellationToken cancellationToken);

    /// <summary>Cross-cutting newest-first keyset-paginated search over the audit log.</summary>
    /// <param name="filter">The filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AuditSearchPage> SearchAsync(AuditSearchFilter filter, CancellationToken cancellationToken);
}
