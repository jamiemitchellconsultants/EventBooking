using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Dashboards;
using EventBooking.Application.Events;

namespace EventBooking.Api.Endpoints;

public static class EventEndpoints
{
    public sealed record ProposeEventRequest(DateOnly Date, TimeOnly StartTime);

    public sealed record AcceptProposalRequest(int Headcount);

    public sealed record AdjustEventCapacityRequest(int TotalHeadcount);

    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/events").RequireAuthorization(AuthenticationExtensions.StaffPolicy);
        var proposals = app.MapGroup("/api/event-proposals").RequireAuthorization(AuthenticationExtensions.StaffPolicy);

        group.MapGet("/board", async (
            ICallerAccessor caller,
            GetManagerEventBoardHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetManagerEventBoardQuery(caller.RequireStaffUserId()), cancellationToken);
            return result.IsSuccess
                ? Results.Ok(EventBoardResourceResponse.From(result.Value))
                : result.ToResponse();
        })
            .WithAgentMetadata("getEventBoard")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        group.MapGet("/operations", async (
            ICallerAccessor caller,
            GetEventOperationsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetEventOperationsQuery(caller.RequireStaffUserId()), cancellationToken);
            return result.IsSuccess
                ? Results.Ok(EventOperationsResourceResponse.From(result.Value))
                : result.ToResponse();
        })
            .WithAgentMetadata("getEventOperations")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        proposals.MapPost("", async (
            ProposeEventRequest request,
            ICallerAccessor caller,
            ProposeEventHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new ProposeEventCommand(caller.RequireStaffUserId(), request.Date, request.StartTime),
                cancellationToken))
                .ToCreated(id => $"/api/event-proposals/{id}"))
            .WithAgentMetadata("proposeEvent")
            .Produces<Guid>(201)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(409);

        proposals.MapPost("/{id:guid}/acceptance", async (
            Guid id,
            AcceptProposalRequest request,
            ICallerAccessor caller,
            AcceptProposalHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new AcceptProposalCommand(caller.RequireStaffUserId(), id, request.Headcount),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("acceptProposal")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        proposals.MapDelete("/{id:guid}/acceptance", async (
            Guid id,
            ICallerAccessor caller,
            WithdrawAcceptanceHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new WithdrawAcceptanceCommand(caller.RequireStaffUserId(), id), cancellationToken))
                .ToResponse())
            .WithAgentMetadata("withdrawAcceptance")
            .Produces(204)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        proposals.MapDelete("/{id:guid}", async (
            Guid id,
            ICallerAccessor caller,
            WithdrawProposalHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new WithdrawProposalCommand(caller.RequireStaffUserId(), id), cancellationToken))
                .ToResponse())
            .WithAgentMetadata("withdrawProposal")
            .Produces(204)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapPut("/{id:guid}/capacity", async (
            Guid id,
            AdjustEventCapacityRequest request,
            ICallerAccessor caller,
            AdjustEventCapacityHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new AdjustEventCapacityCommand(
                    caller.RequireStaffUserId(),
                    id,
                    request.TotalHeadcount),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("adjustEventCapacity")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapDelete("/{id:guid}", async (
            Guid id,
            bool? confirm,
            ICallerAccessor caller,
            CancelEventHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new CancelEventCommand(caller.RequireStaffUserId(), id, confirm ?? false),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("cancelEvent")
            .Produces(204)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        app.MapPost("/api/events/import", async (
            HttpRequest request,
            ICallerAccessor caller,
            ImportEventsHandler handler,
            CancellationToken cancellationToken) =>
        {
            using var reader = new StreamReader(request.Body);
            var csv = await reader.ReadToEndAsync(cancellationToken);
            return (await handler.HandleAsync(
                new ImportEventsCommand(caller.RequireStaffUserId(), csv),
                cancellationToken)).ToResponse();
        }).RequireAuthorization(AuthenticationExtensions.StaffPolicy)
            .WithAgentMetadata("importEvents")
            .Accepts<string>("text/csv")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(413)
            .ProducesProblem(415);

        return app;
    }
}
