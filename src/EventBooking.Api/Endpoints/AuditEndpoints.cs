using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Api.Pagination;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Application.Dashboards;
using EventBooking.Domain.Audit;

namespace EventBooking.Api.Endpoints;

/// <summary>Maps the three audit routes.</summary>
public static class AuditEndpoints
{
    /// <summary>Maps the audit routes.</summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup("/api/audit")
            .RequireAuthorization(AuthenticationExtensions.StaffPolicy)
            .RequireRateLimiting(StaffRateLimiterPolicy.PolicyName);

        group.MapGet("/", async (
            string? from,
            string? to,
            string? action,
            string? actorType,
            string? actorId,
            string? entityType,
            Guid? entityId,
            string? cursor,
            int? limit,
            ICallerAccessor caller,
            GetAuditSearchHandler handler,
            PageCursor cursors,
            IStaffIdentityRepository identities,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            if (!AuditInputParser.TryParseBound(from, out var fromBound))
            {
                return Invalid(nameof(from));
            }

            if (!AuditInputParser.TryParseBound(to, out var toBound))
            {
                return Invalid(nameof(to));
            }

            // The handler matches one identifier exactly against the entity id or the actor
            // id, so two different identifiers cannot be expressed in one search.
            var entityIdText = entityId?.ToString();
            if (actorId is not null && entityIdText is not null &&
                !string.Equals(actorId, entityIdText, StringComparison.Ordinal))
            {
                return ResultResponses.ValidationFailed(
                    "entityId", "mutually-exclusive",
                    "Search by actorId or entityId, not both.");
            }

            if (!PageRequest.TryBind(cursor, limit, out var page, out var field))
            {
                return ResultResponses.ValidationFailed(
                    field!, "out-of-range", "Limit must be between 1 and 200.");
            }

            string? inner = null;
            if (page.Cursor is not null && !cursors.TryUnprotect(page.Cursor, out inner!))
            {
                return ResultResponses.ValidationFailed(
                    "cursor", "cursor-invalid", "That cursor is not valid.");
            }

            var result = await handler.HandleAsync(
                new GetAuditSearchQuery(
                    caller.RequireStaffUserId(), fromBound, toBound, actorType, action,
                    actorId ?? entityIdText, entityType, inner, page.Limit),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            var displays = await StaffDisplaysAsync(identities, cancellationToken);
            return Results.Ok(new Page<AuditRowResponse>(
                [.. result.Value.Rows.Select(row => ApiResponses.AuditRow(
                    row, DisplayOf(row, displays), held))],
                result.Value.NextCursor is null
                    ? null
                    : cursors.Protect(result.Value.NextCursor)));
        })
            .WithAgentMetadata("searchAudit")
            .WithEventBookingList()
            .Produces<Page<AuditRowResponse>>(200)
            .ProducesProblem(403)
            .ProducesProblem(422);

        group.MapGet("/attendees/{id:guid}", async (
            Guid id,
            ICallerAccessor caller,
            GetAuditHistoryHandler handler,
            IStaffIdentityRepository identities,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetAuditHistoryQuery(caller.RequireStaffUserId(), null, id),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            var displays = await StaffDisplaysAsync(identities, cancellationToken);
            return Results.Ok(new Page<AuditRowResponse>(
                [.. result.Value.Select(row => ApiResponses.AuditRow(
                    row, DisplayOf(row, displays), held))],
                null));
        })
            .WithAgentMetadata("getAttendeeAuditHistory")
            .WithEventBookingList()
            .Produces<Page<AuditRowResponse>>(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        group.MapGet("/events/{id:guid}", async (
            Guid id,
            ICallerAccessor caller,
            GetAuditHistoryHandler handler,
            IStaffIdentityRepository identities,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetAuditHistoryQuery(
                    caller.RequireStaffUserId(), AuditEntityTypes.Event, id),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            var displays = await StaffDisplaysAsync(identities, cancellationToken);
            return Results.Ok(new Page<AuditRowResponse>(
                [.. result.Value.Select(row => ApiResponses.AuditRow(
                    row, DisplayOf(row, displays), held))],
                null));
        })
            .WithAgentMetadata("getEventAuditHistory")
            .WithEventBookingList()
            .Produces<Page<AuditRowResponse>>(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        return app;
    }

    private static async Task<IReadOnlyDictionary<Guid, string?>> StaffDisplaysAsync(
        IStaffIdentityRepository identities, CancellationToken cancellationToken) =>
        (await identities.ListAsync(cancellationToken))
            .ToDictionary(identity => identity.StaffUserId, identity => identity.DisplayName);

    private static string? DisplayOf(
        AuditHistoryRow row, IReadOnlyDictionary<Guid, string?> displays) =>
        string.Equals(row.ActorType, ActorType.Staff.ToString(), StringComparison.Ordinal)
        && Guid.TryParse(row.ActorId, out var staffUserId)
        && displays.TryGetValue(staffUserId, out var display)
            ? display
            : null;

    private static IResult Invalid(string parameter) =>
        Result<AuditSearchPage>
            .Failure(Error.Validation($"The {parameter} bound is not a valid timestamp."))
            .ToResponse();
}
