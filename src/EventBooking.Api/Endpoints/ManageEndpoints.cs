using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Bookings;
using EventBooking.Domain.Time;

namespace EventBooking.Api.Endpoints;

/// <summary>Maps the two manage-token routes. Anonymous and rate-limited, like the book pair.</summary>
public static class ManageEndpoints
{
    /// <summary>The cancellation body design 05 names.</summary>
    /// <param name="RequestNewTime">Whether the attendee wants a replacement invitation.</param>
    public sealed record CancelBookingRequest(bool RequestNewTime);

    /// <summary>Maps the manage-token routes.</summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapManageEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup("/api/manage")
            .AllowAnonymous()
            .RequireRateLimiting(TokenPrefixRateLimiterPolicy.PolicyName);

        group.MapGet("/{token}", async (
            string token,
            ViewBookingHandler handler,
            IEventWindowZones zones,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new ViewBookingQuery(token), cancellationToken);
            return result.IsFailure
                ? result.ToResponse()
                : Results.Ok(ApiResponses.ManagedBooking(result.Value, zones, token));
        })
            .WithAgentMetadata("viewManagedBooking")
            .Produces<ManagedBookingResponse>(200)
            .ProducesProblem(404)
            .ProducesProblem(429);

        group.MapPost("/{token}/cancel", async (
            string token,
            CancelBookingRequest request,
            CancelBookingByAttendeeHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new CancelBookingByAttendeeCommand(token, request.RequestNewTime),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("cancelManagedBooking")
            .Produces<AttendeeCancelOutcome>(200)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(429);

        return app;
    }
}
