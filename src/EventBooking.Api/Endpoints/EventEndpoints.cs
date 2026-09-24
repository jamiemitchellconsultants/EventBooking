using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Api.Pagination;
using EventBooking.Application.Events;
using EventBooking.Application.Negotiation;
using EventBooking.Domain.Time;

namespace EventBooking.Api.Endpoints;

/// <summary>Maps the five event routes.</summary>
public static class EventEndpoints
{
    /// <summary>The capacity body design 05 names.</summary>
    /// <param name="TotalHeadcount">The new total for the named type.</param>
    public sealed record AdjustCapacityRequest(int TotalHeadcount);

    /// <summary>Maps the event routes.</summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup("/api/events")
            .RequireAuthorization(AuthenticationExtensions.StaffPolicy)
            .RequireRateLimiting(StaffRateLimiterPolicy.PolicyName);

        group.MapGet("/", async (
            Guid? locationId,
            DateOnly? from,
            DateOnly? to,
            Guid? appointmentTypeId,
            string? cursor,
            int? limit,
            ICallerAccessor caller,
            ListEventsHandler handler,
            PageCursor cursors,
            IEventWindowZones zones,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
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
                new ListEventsQuery(
                    caller.RequireStaffUserId(), locationId, from, to, appointmentTypeId,
                    inner, page.Limit),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Ok(new Page<EventResponse>(
                [.. result.Value.Items.Select(x => ApiResponses.Event(x, zones, held))],
                result.Value.NextCursor is null ? null : cursors.Protect(result.Value.NextCursor)));
        })
            .WithAgentMetadata("listEvents")
            .Produces<Page<EventResponse>>(200)
            .ProducesProblem(403)
            .ProducesProblem(422);

        // Registered before the parameter route for readability only: ASP.NET already prefers
        // a literal segment over a parameter, so "cancellable" can never be read as an id.
        group.MapGet("/cancellable", async (
            Guid? locationId,
            DateOnly? from,
            DateOnly? to,
            string? cursor,
            int? limit,
            ICallerAccessor caller,
            ListCancellableEventsHandler handler,
            PageCursor cursors,
            IEventWindowZones zones,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
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
                new ListCancellableEventsQuery(
                    caller.RequireStaffUserId(), locationId, from, to, inner, page.Limit),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Ok(new Page<EventResponse>(
                [.. result.Value.Items.Select(x => ApiResponses.Event(x, zones, held))],
                result.Value.NextCursor is null ? null : cursors.Protect(result.Value.NextCursor)));
        })
            .WithAgentMetadata("listCancellableEvents")
            .Produces<Page<EventResponse>>(200)
            .ProducesProblem(403)
            .ProducesProblem(422);

        group.MapGet("/{id:guid}", async (
            Guid id,
            ICallerAccessor caller,
            GetEventHandler handler,
            IEventWindowZones zones,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetEventQuery(caller.RequireStaffUserId(), id), cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Ok(ApiResponses.Event(result.Value, zones, held));
        })
            .WithAgentMetadata("getEvent")
            .Produces<EventResponse>(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        group.MapPut("/{id:guid}/capacities/{appointmentTypeId:guid}", async (
            Guid id,
            Guid appointmentTypeId,
            AdjustCapacityRequest request,
            ICallerAccessor caller,
            AdjustEventCapacityHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new AdjustEventCapacityCommand(
                    caller.RequireStaffUserId(), id, request.TotalHeadcount, appointmentTypeId),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("adjustEventCapacity")
            .Produces<AdjustEventCapacityOutcome>(200)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(422);

        // Two-step cancellation. The handler reports the consequence as a SUCCESS carrying
        // the affected count, so the translation is the endpoint's: without confirm the call
        // reports and changes nothing, even when the count is zero.
        group.MapPost("/{id:guid}/cancel", async (
            Guid id,
            bool? confirm,
            ICallerAccessor caller,
            CancelEventHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new CancelEventCommand(caller.RequireStaffUserId(), id, confirm ?? false),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            if (confirm != true)
            {
                return ResultResponses.ConfirmationRequired(
                    "Cancelling this event will cancel its bookings and re-invite the affected attendees.",
                    new Dictionary<string, long>
                    {
                        ["affectedBookings"] = result.Value.CancelledCount,
                    });
            }

            return Results.Ok(result.Value);
        })
            .WithAgentMetadata("cancelEvent")
            .Produces<CancelEventOutcome>(200)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        return app;
    }
}
