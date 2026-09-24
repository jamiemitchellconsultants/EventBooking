using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Bookings;
using EventBooking.Domain.Time;

namespace EventBooking.Api.Endpoints;

/// <summary>Maps the two book-token routes.</summary>
public static class BookingEndpoints
{
    /// <summary>The confirmation body design 05 names.</summary>
    /// <param name="EventId">The offered event to book.</param>
    public sealed record ConfirmBookingRequest(Guid EventId);

    /// <summary>Maps the book-token routes.</summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapBookingEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup("/api/booking")
            .AllowAnonymous()
            .RequireRateLimiting(TokenPrefixRateLimiterPolicy.PolicyName);

        group.MapGet("/{token}", async (
            string token,
            ViewInviteHandler handler,
            IEventWindowZones zones,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new ViewInviteQuery(token), cancellationToken);
            return result.IsFailure
                ? result.ToResponse()
                : Results.Ok(ApiResponses.Invite(result.Value, zones, token));
        })
            .WithAgentMetadata("viewInvite")
            .Produces<InviteResponse>(200)
            .ProducesProblem(404)
            .ProducesProblem(410)
            .ProducesProblem(429);

        group.MapPost("/{token}/confirm", async (
            string token,
            ConfirmBookingRequest request,
            ConfirmBookingHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new ConfirmBookingCommand(token, request.EventId), cancellationToken);
            return result.IsFailure
                ? result.ToResponse()
                : Results.Created(
                    $"/api/manage/{result.Value.ManageToken}",
                    new ConfirmBookingResponse(result.Value.BookingId, result.Value.ManageToken));
        })
            .WithAgentMetadata("confirmBooking")
            .Produces<ConfirmBookingResponse>(201)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(410)
            .ProducesProblem(429);

        return app;
    }
}
