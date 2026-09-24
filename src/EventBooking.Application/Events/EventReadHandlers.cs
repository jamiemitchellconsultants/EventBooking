using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;

namespace EventBooking.Application.Events;

/// <summary>
/// The scope both Event reads resolve. Design 05 gives GET /api/events two capabilities,
/// which design 04's one-capability rule forbids a handler to demand; settlement #12 follows
/// Task 20b's audit precedent and resolves what the caller holds instead. Negotiation wins
/// over operations when a caller holds both: the matrix grants a Manager both, so
/// operations-first would resolve every Manager to all types and the scope machinery that
/// design 05's "my type" view needs would be dead.
/// </summary>
public static class EventScopeResolver
{
    /// <summary>Resolves the caller's Event scope, or refuses.</summary>
    /// <param name="access">The authorizer.</param>
    /// <param name="staffUserId">The calling staff identity.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The scope, or a forbidden failure.</returns>
    public static async Task<Result<EventScope>> ResolveAsync(
        IStaffAccessAuthorizer access, Guid staffUserId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(access);

        var negotiation = await access.AuthorizeAsync(
            staffUserId, StaffCapability.ManageEventNegotiation, null, ct);
        if (negotiation.IsSuccess && negotiation.Value.AppointmentTypeId is { } scope)
        {
            return Result<EventScope>.Success(new EventScope(AllTypes: false, scope));
        }

        var operations = await access.AuthorizeAsync(
            staffUserId, StaffCapability.ViewEventOperations, null, ct);
        if (operations.IsSuccess)
        {
            return Result<EventScope>.Success(new EventScope(AllTypes: true, null));
        }

        return Result<EventScope>.Failure(
            Error.Forbidden("Reading events needs an event capability."));
    }
}

/// <summary>Lists events, filtered and scoped, one keyset page at a time.</summary>
/// <param name="access">The authorizer.</param>
/// <param name="queries">The read side.</param>
/// <param name="clock">The clock.</param>
public sealed class ListEventsHandler(
    IStaffAccessAuthorizer access, IEventReadQueries queries, IClock clock)
{
    /// <summary>Returns one page of events.</summary>
    /// <param name="query">The filters and the page.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The page.</returns>
    public async Task<Result<EventListView>> HandleAsync(
        ListEventsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Limit is < 1 or > 200)
        {
            return Result<EventListView>.Failure(
                Error.Validation("Limit must be between 1 and 200."));
        }

        var scope = await EventScopeResolver.ResolveAsync(access, query.StaffUserId, ct);
        if (scope.IsFailure)
        {
            return Result<EventListView>.Failure(scope.Error);
        }

        var rows = await queries.ListAsync(
            query with { Limit = query.Limit + 1 }, scope.Value, clock.UtcNow,
            notStartedOnly: false, ct);
        return Result<EventListView>.Success(EventPage.From(rows, query.Limit));
    }
}

/// <summary>Lists the events a cancellation can still reach (FR-7.2).</summary>
/// <param name="access">The authorizer.</param>
/// <param name="queries">The read side.</param>
/// <param name="clock">The clock.</param>
public sealed class ListCancellableEventsHandler(
    IStaffAccessAuthorizer access, IEventReadQueries queries, IClock clock)
{
    /// <summary>Returns one page of cancellable events.</summary>
    /// <param name="query">The filters and the page.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The page.</returns>
    public async Task<Result<EventListView>> HandleAsync(
        ListCancellableEventsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Limit is < 1 or > 200)
        {
            return Result<EventListView>.Failure(
                Error.Validation("Limit must be between 1 and 200."));
        }

        // Design 05 gives this route ViewEventOperations alone, but a Manager holding
        // CancelEvent for events listing their type has the same need; the resolver already
        // answers both, and the query narrows a Manager to their own capacity row regardless.
        var scope = await EventScopeResolver.ResolveAsync(access, query.StaffUserId, ct);
        if (scope.IsFailure)
        {
            return Result<EventListView>.Failure(scope.Error);
        }

        var rows = await queries.ListAsync(
            new ListEventsQuery(
                query.StaffUserId, query.LocationId, query.From, query.To, null,
                query.Cursor, query.Limit + 1),
            scope.Value, clock.UtcNow, notStartedOnly: true, ct);
        return Result<EventListView>.Success(EventPage.From(rows, query.Limit));
    }
}

/// <summary>Reads one event, filtered the same way the list is.</summary>
/// <param name="access">The authorizer.</param>
/// <param name="queries">The read side.</param>
public sealed class GetEventHandler(IStaffAccessAuthorizer access, IEventReadQueries queries)
{
    /// <summary>Returns one event, or not found.</summary>
    /// <param name="query">The event to read.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The event.</returns>
    public async Task<Result<EventView>> HandleAsync(GetEventQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var scope = await EventScopeResolver.ResolveAsync(access, query.StaffUserId, ct);
        if (scope.IsFailure)
        {
            return Result<EventView>.Failure(scope.Error);
        }

        var row = await queries.GetAsync(query.EventId, scope.Value, ct);

        // An event outside the caller's scope reads as absent rather than forbidden. A
        // Manager who can tell "not yours" from "no such event" can enumerate other types'
        // scheduling, which is the same reasoning behind the attendee token's one answer.
        return row is null
            ? Result<EventView>.Failure(Error.NotFound("No such event."))
            : Result<EventView>.Success(row);
    }
}

/// <summary>Turns an over-read of one extra row into a page and its next cursor.</summary>
internal static class EventPage
{
    public static EventListView From(IReadOnlyList<EventView> rows, int limit)
    {
        var kept = rows.Count > limit ? rows.Take(limit).ToList() : rows.ToList();
        var next = rows.Count > limit ? kept[^1].Cursor : null;
        return new EventListView(kept, next);
    }
}
