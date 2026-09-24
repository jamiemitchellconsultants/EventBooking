using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Application.ReadModels;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Audit;

/// <summary>Bucketed cross-cutting search over the audit log.</summary>
/// <param name="StaffUserId">The searching staff member.</param>
/// <param name="Cursor">The opaque keyset cursor, or null for the newest page.</param>
/// <param name="Limit">Rows per page, between 1 and 200.</param>
/// <param name="EntityType">Optional entity-type filter within the caller's buckets.</param>
/// <param name="Action">Optional audit-action name filter.</param>
/// <param name="From">Inclusive lower bound on the recorded timestamp, or null.</param>
/// <param name="To">Inclusive upper bound on the recorded timestamp, or null.</param>
/// <param name="EntityId">Optional entity-instance filter.</param>
public sealed record SearchAuditQuery(
    Guid StaffUserId, string? Cursor, int Limit, string? EntityType, string? Action,
    DateTimeOffset? From, DateTimeOffset? To, Guid? EntityId);

/// <summary>One audit row: fixed identifiers only, never personal data.</summary>
/// <param name="Id">The audit entry id.</param>
/// <param name="EntityType">The audited entity type.</param>
/// <param name="Action">The recorded audit action name.</param>
/// <param name="ActorType">Who caused the change: staff, attendee token, or system.</param>
/// <param name="OccurredAt">When the change was recorded.</param>
/// <param name="Cursor">The opaque keyset cursor for this row.</param>
public sealed record AuditRow(
    Guid Id, string EntityType, string Action, string ActorType,
    DateTimeOffset OccurredAt, string Cursor);

/// <summary>One page of audit search results, newest first.</summary>
/// <param name="Items">The result rows in newest-first order.</param>
/// <param name="NextCursor">Opaque cursor for the following page, or null when exhausted.</param>
public sealed record AuditSearchView(IReadOnlyList<AuditRow> Items, string? NextCursor);

/// <summary>Keyset-paginated audit history for one attendee.</summary>
/// <param name="StaffUserId">The searching staff member.</param>
/// <param name="AttendeeId">The attendee whose history is read.</param>
/// <param name="Cursor">The opaque keyset cursor, or null for the newest page.</param>
/// <param name="Limit">Rows per page.</param>
public sealed record GetAttendeeHistoryQuery(
    Guid StaffUserId, Guid AttendeeId, string? Cursor, int Limit);

/// <summary>Keyset-paginated audit history for one event.</summary>
/// <param name="StaffUserId">The searching staff member.</param>
/// <param name="EventId">The event whose history is read.</param>
/// <param name="Cursor">The opaque keyset cursor, or null for the newest page.</param>
/// <param name="Limit">Rows per page.</param>
public sealed record GetEventHistoryQuery(
    Guid StaffUserId, Guid EventId, string? Cursor, int Limit);

/// <summary>Searches the audit log in the buckets the caller's capabilities grant.</summary>
/// <param name="queries">The bucketed audit search port.</param>
/// <param name="access">The staff access authorizer.</param>
public sealed class SearchAuditHandler(
    IAuditSearchQueries queries,
    IStaffAccessAuthorizer access)
{
    /// <summary>Handles the audit search query.</summary>
    /// <param name="query">The query.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<AuditSearchView>> HandleAsync(
        SearchAuditQuery query, CancellationToken ct)
    {
        if (query.Limit is < 1 or > 200)
            return Result<AuditSearchView>.Failure(
                Error.Validation("Limit must be between 1 and 200."));

        var eventAccess = await access.AuthorizeAsync(
            query.StaffUserId, StaffCapability.ViewEventAudit, null, ct);
        var attendeeAccess = await access.AuthorizeAsync(
            query.StaffUserId, StaffCapability.ViewAttendeeAudit, null, ct);
        if (eventAccess.IsFailure && attendeeAccess.IsFailure)
            return Result<AuditSearchView>.Failure(
                Error.Forbidden("Audit search needs an audit capability."));
        var shape = ShapeOf(
            eventAccess.IsSuccess ? eventAccess.Value : attendeeAccess.Value);
        var buckets = new List<string>();
        if (eventAccess.IsSuccess) buckets.Add("event");
        if (attendeeAccess.IsSuccess) buckets.Add("attendee");

        var page = await queries.SearchAsync(shape, buckets, query.Cursor, query.Limit,
            query.EntityType, query.Action, query.From, query.To, query.EntityId, ct);
        return Result<AuditSearchView>.Success(page);
    }

    private static CallerShape ShapeOf(StaffAccessContext context) => new(
        context.StaffUserId, context.Roles.Contains(Role.Admin),
        context.Roles.Select(r => r.ToString()).ToHashSet());
}

/// <summary>Reads one attendee's audit history under the attendee-audit capability.</summary>
/// <param name="queries">The bucketed audit search port.</param>
/// <param name="access">The staff access authorizer.</param>
public sealed class AttendeeHistoryHandler(
    IAuditSearchQueries queries,
    IStaffAccessAuthorizer access)
{
    /// <summary>Handles the attendee-history query.</summary>
    /// <param name="query">The query.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<AuditSearchView>> HandleAsync(
        GetAttendeeHistoryQuery query, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId, StaffCapability.ViewAttendeeAudit, null, ct);
        if (authorized.IsFailure) return Result<AuditSearchView>.Failure(authorized.Error);
        var page = await queries.HistoryAsync(ShapeOf(authorized.Value), ["attendee"],
            query.AttendeeId, query.Cursor, query.Limit, ct);
        return Result<AuditSearchView>.Success(page);
    }

    private static CallerShape ShapeOf(StaffAccessContext context) => new(
        context.StaffUserId, context.Roles.Contains(Role.Admin),
        context.Roles.Select(r => r.ToString()).ToHashSet());
}

/// <summary>Reads one event's audit history under the event-audit capability.</summary>
/// <param name="queries">The bucketed audit search port.</param>
/// <param name="access">The staff access authorizer.</param>
public sealed class EventHistoryHandler(
    IAuditSearchQueries queries,
    IStaffAccessAuthorizer access)
{
    /// <summary>Handles the event-history query.</summary>
    /// <param name="query">The query.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<AuditSearchView>> HandleAsync(
        GetEventHistoryQuery query, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId, StaffCapability.ViewEventAudit, null, ct);
        if (authorized.IsFailure) return Result<AuditSearchView>.Failure(authorized.Error);
        var page = await queries.HistoryAsync(ShapeOf(authorized.Value), ["event"],
            query.EventId, query.Cursor, query.Limit, ct);
        return Result<AuditSearchView>.Success(page);
    }

    private static CallerShape ShapeOf(StaffAccessContext context) => new(
        context.StaffUserId, context.Roles.Contains(Role.Admin),
        context.Roles.Select(r => r.ToString()).ToHashSet());
}
