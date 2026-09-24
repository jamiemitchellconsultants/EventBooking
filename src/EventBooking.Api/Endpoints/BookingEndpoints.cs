using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Bookings;

namespace EventBooking.Api.Endpoints;

public static class BookingEndpoints
{
    public sealed record ConfirmBookingRequest(Guid EventId);

    public sealed record CancelBookingRequest(bool RequestNewTime);

    public static IEndpointRouteBuilder MapBookingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/booking")
            .AllowAnonymous()
            .RequireRateLimiting(TokenPrefixRateLimiterPolicy.PolicyName);

        group.MapGet("/{token}", async (
            string token,
            ViewInviteHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new ViewInviteQuery(token), cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            return Results.Ok(InviteResourceResponse.From(result.Value, token));
        })
            .WithAgentMetadata("viewInvite")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(404)
            .ProducesProblem(429);

        group.MapPost("/{token}/confirm", async (
            string token,
            ConfirmBookingRequest request,
            ConfirmBookingHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new ConfirmBookingCommand(token, request.EventId), cancellationToken))
                .ToResponse())
            .WithAgentMetadata("confirmBooking")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(429);

        group.MapGet("/manage/{token}", async (
            string token,
            ViewBookingHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new ViewBookingQuery(token), cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            return Results.Ok(ManagedBookingResourceResponse.From(result.Value, token));
        })
            .WithAgentMetadata("viewManagedBooking")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(404)
            .ProducesProblem(429);

        group.MapPost("/manage/{token}/cancel", async (
            string token,
            CancelBookingRequest request,
            CancelBookingByAttendeeHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new CancelBookingByAttendeeCommand(token, request.RequestNewTime), cancellationToken))
                .ToResponse())
            .WithAgentMetadata("cancelManagedBooking")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(429);

        return app;
    }
}
