using System.ComponentModel;
using EventBooking.Api.Auth;
using EventBooking.Api.Pagination;
using EventBooking.Api.Endpoints;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Dashboards;
using EventBooking.Domain.Audit;
using ModelContextProtocol.Server;

namespace EventBooking.Mcp.Tools;

/// <summary>The read-only audit tools. Timestamps parse with the REST route's own parser.</summary>
[McpServerToolType]
public sealed class AuditTools
{
    /// <summary>Searches the audit log within the caller's buckets.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The search handler.</param>
    /// <param name="cursors">The REST cursor signer.</param>
    /// <param name="limit">The page size.</param>
    /// <param name="entityType">The entity-type bucket, or null for every bucket.</param>
    /// <param name="action">The action filter, or null for every action.</param>
    /// <param name="actorType">The actor-type filter, or null for every actor.</param>
    /// <param name="actorId">The actor identifier, or null.</param>
    /// <param name="entityId">The entity identifier, or null.</param>
    /// <param name="from">The earliest instant, or null.</param>
    /// <param name="to">The latest instant, or null.</param>
    /// <param name="cursor">The page cursor, or null for the first page.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>One page of audit rows.</returns>
    [McpServerTool(
        Name = "search_audit", Title = "Search audit",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Searches audit entries, scoped to the buckets the caller's capabilities reach (FR-12.3), one keyset page at a time.")]
    public async Task<AuditSearchPage> SearchAuditAsync(
        ICallerAccessor caller,
        GetAuditSearchHandler handler,
        PageCursor cursors,
        [Description("Page size, 1 to 200.")] int limit = 50,
        [Description("Narrow to one entity type.")] string? entityType = null,
        [Description("Narrow to one action.")] string? action = null,
        [Description("Narrow to one actor type.")] string? actorType = null,
        [Description("Narrow to one actor; not with entityId.")] string? actorId = null,
        [Description("Narrow to one entity; not with actorId.")] Guid? entityId = null,
        [Description("Earliest instant, ISO 8601.")] string? from = null,
        [Description("Latest instant, ISO 8601.")] string? to = null,
        [Description("The nextCursor from the previous page.")] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (!AuditInputParser.TryParseBound(from, out var fromBound)
            || !AuditInputParser.TryParseBound(to, out var toBound))
        {
            throw new ModelContextProtocol.McpException(
                "Bounds must be ISO 8601 timestamps.");
        }

        // The handler takes one identifier; the route refuses both, so the tool does too.
        if (actorId is not null && entityId is not null)
        {
            throw new ModelContextProtocol.McpException(
                "Search by actorId or entityId, not both.");
        }

        var result = await handler.HandleAsync(
            new GetAuditSearchQuery(
                caller.RequireStaffUserId(), fromBound, toBound, actorType, action,
                actorId ?? entityId?.ToString("D"), entityType, cursors.Unwrap(cursor), limit),
            cancellationToken);
        var page = result.ValueOrThrow();
        return page with { NextCursor = cursors.Wrap(page.NextCursor) };
    }

    /// <summary>Reads one attendee's audit history.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The history handler.</param>
    /// <param name="attendeeId">The attendee.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The history rows, newest first.</returns>
    [McpServerTool(
        Name = "get_attendee_audit_history", Title = "Get attendee audit history",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reads the audit history of one attendee.")]
    public async Task<IReadOnlyList<AuditHistoryRow>> GetAttendeeAuditHistoryAsync(
        ICallerAccessor caller,
        GetAuditHistoryHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new GetAuditHistoryQuery(caller.RequireStaffUserId(), null, attendeeId),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Reads one event's audit history, including its proposal.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The history handler.</param>
    /// <param name="eventId">The event.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The history rows, newest first.</returns>
    [McpServerTool(
        Name = "get_event_audit_history", Title = "Get event audit history",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reads the audit history of one event, including its proposal.")]
    public async Task<IReadOnlyList<AuditHistoryRow>> GetEventAuditHistoryAsync(
        ICallerAccessor caller,
        GetAuditHistoryHandler handler,
        [Description("The event identifier.")] Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new GetAuditHistoryQuery(
                caller.RequireStaffUserId(), AuditEntityTypes.Event, eventId),
            cancellationToken);
        return result.ValueOrThrow();
    }
}
