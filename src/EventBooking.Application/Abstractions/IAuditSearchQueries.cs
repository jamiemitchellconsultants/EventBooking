using EventBooking.Application.Audit;
using EventBooking.Application.ReadModels;

namespace EventBooking.Application.Abstractions;

/// <summary>
/// Bucketed audit search. The handler resolves the caller's buckets from its
/// capabilities and passes them; this port enforces them in SQL.
/// </summary>
public interface IAuditSearchQueries
{
    /// <summary>Searches one keyset page across the caller's buckets.</summary>
    /// <param name="shape">The caller shape.</param>
    /// <param name="buckets">The buckets the caller may see.</param>
    /// <param name="cursor">The opaque page cursor, or null for the newest page.</param>
    /// <param name="limit">The page size.</param>
    /// <param name="entityType">The entity-type filter.</param>
    /// <param name="action">The action filter.</param>
    /// <param name="from">The inclusive lower timestamp bound.</param>
    /// <param name="to">The inclusive upper timestamp bound.</param>
    /// <param name="entityId">The entity-instance filter.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<AuditSearchView> SearchAsync(
        CallerShape shape, IReadOnlyList<string> buckets, string? cursor, int limit,
        string? entityType, string? action, DateTimeOffset? from, DateTimeOffset? to,
        Guid? entityId, CancellationToken ct);

    /// <summary>Reads one keyset page of history for one audited entity instance.</summary>
    /// <param name="shape">The caller shape.</param>
    /// <param name="buckets">The buckets the caller may see.</param>
    /// <param name="entityId">The entity instance.</param>
    /// <param name="cursor">The opaque page cursor, or null for the newest page.</param>
    /// <param name="limit">The page size.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<AuditSearchView> HistoryAsync(
        CallerShape shape, IReadOnlyList<string> buckets, Guid entityId,
        string? cursor, int limit, CancellationToken ct);
}
