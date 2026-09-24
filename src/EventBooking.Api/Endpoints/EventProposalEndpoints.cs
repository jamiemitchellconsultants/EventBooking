using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Api.Pagination;
using EventBooking.Application.Negotiation;
using EventBooking.Domain.Time;

namespace EventBooking.Api.Endpoints;

/// <summary>Maps the five proposal routes.</summary>
public static class EventProposalEndpoints
{
    /// <summary>The proposal body design 05 names.</summary>
    /// <param name="LocationId">Where the event would be held.</param>
    /// <param name="Date">The local calendar date.</param>
    /// <param name="StartTime">The local start time.</param>
    /// <param name="DurationMinutes">The window length.</param>
    /// <param name="AppointmentTypeIds">Every type the event will offer.</param>
    /// <param name="Headcount">The proposer's own headcount.</param>
    public sealed record ProposeEventRequest(
        Guid LocationId, DateOnly Date, TimeOnly StartTime, int DurationMinutes,
        IReadOnlyList<Guid>? AppointmentTypeIds, int Headcount);

    /// <summary>The acceptance body design 05 names.</summary>
    /// <param name="Headcount">The accepting type's headcount.</param>
    public sealed record RecordAcceptanceRequest(int Headcount);

    /// <summary>Maps the proposal routes.</summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapEventProposalEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup("/api/event-proposals")
            .RequireAuthorization(AuthenticationExtensions.StaffPolicy)
            .RequireRateLimiting(StaffRateLimiterPolicy.PolicyName);

        group.MapGet("/", async (
            string? status,
            Guid? locationId,
            string? cursor,
            int? limit,
            ICallerAccessor caller,
            ListEventProposalsHandler handler,
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

            // The signed cursor is unwrapped here and nowhere else: the handler's cursor is a
            // sort key, and a caller must never be handed one that is not signed.
            string? inner = null;
            if (page.Cursor is not null && !cursors.TryUnprotect(page.Cursor, out inner!))
            {
                return ResultResponses.ValidationFailed(
                    "cursor", "cursor-invalid", "That cursor is not valid.");
            }

            var result = await handler.HandleAsync(
                new ListEventProposalsQuery(
                    caller.RequireStaffUserId(), status, locationId, inner, page.Limit),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Ok(new Page<EventProposalResponse>(
                [.. result.Value.Items.Select(x => ApiResponses.EventProposal(x, zones, held))],
                result.Value.NextCursor is null ? null : cursors.Protect(result.Value.NextCursor)));
        })
            .WithAgentMetadata("listEventProposals")
            .Produces<Page<EventProposalResponse>>(200)
            .ProducesProblem(403)
            .ProducesProblem(422);

        group.MapPost("/", async (
            ProposeEventRequest request,
            ICallerAccessor caller,
            ProposeEventHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new ProposeEventCommand(
                    caller.RequireStaffUserId(), request.LocationId, request.Date,
                    request.StartTime, request.DurationMinutes,
                    request.AppointmentTypeIds ?? [], request.Headcount),
                cancellationToken);
            return result.IsFailure
                ? result.ToResponse()
                : Results.Created($"/api/event-proposals/{result.Value.ProposalId}", new
                {
                    id = result.Value.ProposalId,
                    status = result.Value.Status,
                    eventId = result.Value.EventId,
                });
        })
            .WithAgentMetadata("proposeEvent")
            .Produces(201)
            .ProducesProblem(403)
            .ProducesProblem(409)
            .ProducesProblem(422);

        group.MapPut("/{id:guid}/acceptance", async (
            Guid id,
            RecordAcceptanceRequest request,
            ICallerAccessor caller,
            RecordAcceptanceHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new RecordAcceptanceCommand(caller.RequireStaffUserId(), id, request.Headcount),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("recordAcceptance")
            .Produces<RecordAcceptanceOutcome>(200)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(422);

        group.MapDelete("/{id:guid}/acceptance", async (
            Guid id,
            ICallerAccessor caller,
            WithdrawAcceptanceHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new WithdrawAcceptanceCommand(caller.RequireStaffUserId(), id),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("withdrawAcceptance")
            .Produces(204)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapPost("/{id:guid}/withdraw", async (
            Guid id,
            ICallerAccessor caller,
            WithdrawProposalHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new WithdrawProposalCommand(caller.RequireStaffUserId(), id),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("withdrawProposal")
            .Produces(204)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        return app;
    }
}
