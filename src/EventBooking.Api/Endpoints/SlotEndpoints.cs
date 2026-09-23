using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Dashboards;
using EventBooking.Application.Slots;

namespace EventBooking.Api.Endpoints;

public static class SlotEndpoints
{
    public sealed record ProposeSlotRequest(DateOnly Date, TimeOnly StartTime);

    public sealed record AcceptProposalRequest(int Headcount);

    public sealed record AdjustConfirmedSlotCapacityRequest(int TotalHeadcount);

    public static IEndpointRouteBuilder MapSlotEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/slots").RequireAuthorization(AuthenticationExtensions.StaffPolicy);

        group.MapGet("/board", async (
            ICallerAccessor caller,
            GetManagerSlotBoardHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetManagerSlotBoardQuery(caller.RequireStaffUserId()), cancellationToken);
            return result.IsSuccess
                ? Results.Ok(SlotBoardResourceResponse.From(result.Value))
                : result.ToResponse();
        })
            .WithAgentMetadata("getSlotBoard")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        group.MapGet("/operations", async (
            ICallerAccessor caller,
            GetSlotOperationsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetSlotOperationsQuery(caller.RequireStaffUserId()), cancellationToken);
            return result.IsSuccess
                ? Results.Ok(SlotOperationsResourceResponse.From(result.Value))
                : result.ToResponse();
        })
            .WithAgentMetadata("getSlotOperations")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        group.MapPost("/proposals", async (
            ProposeSlotRequest request,
            ICallerAccessor caller,
            ProposeSlotHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new ProposeSlotCommand(caller.RequireStaffUserId(), request.Date, request.StartTime),
                cancellationToken))
                .ToCreated(id => $"/api/slots/proposals/{id}"))
            .WithAgentMetadata("proposeSlot")
            .Produces<Guid>(201)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(409);

        group.MapPost("/proposals/{id:guid}/acceptance", async (
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

        group.MapDelete("/proposals/{id:guid}/acceptance", async (
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

        group.MapDelete("/proposals/{id:guid}", async (
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

        group.MapPut("/confirmed/{id:guid}/capacity", async (
            Guid id,
            AdjustConfirmedSlotCapacityRequest request,
            ICallerAccessor caller,
            AdjustConfirmedSlotCapacityHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new AdjustConfirmedSlotCapacityCommand(
                    caller.RequireStaffUserId(),
                    id,
                    request.TotalHeadcount),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("adjustConfirmedSlotCapacity")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapDelete("/confirmed/{id:guid}", async (
            Guid id,
            bool? confirm,
            ICallerAccessor caller,
            CancelConfirmedSlotHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new CancelConfirmedSlotCommand(caller.RequireStaffUserId(), id, confirm ?? false),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("cancelConfirmedSlot")
            .Produces(204)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        app.MapPost("/api/confirmed-slots/import", async (
            HttpRequest request,
            ICallerAccessor caller,
            ImportConfirmedSlotsHandler handler,
            CancellationToken cancellationToken) =>
        {
            using var reader = new StreamReader(request.Body);
            var csv = await reader.ReadToEndAsync(cancellationToken);
            return (await handler.HandleAsync(
                new ImportConfirmedSlotsCommand(caller.RequireStaffUserId(), csv),
                cancellationToken)).ToResponse();
        }).RequireAuthorization(AuthenticationExtensions.StaffPolicy)
            .WithAgentMetadata("importConfirmedSlots")
            .Accepts<string>("text/csv")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(413)
            .ProducesProblem(415);

        return app;
    }
}
