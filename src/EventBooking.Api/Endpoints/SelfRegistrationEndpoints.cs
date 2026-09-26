using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Api.Pagination;
using EventBooking.Application.EventGroups;
using EventBooking.Application.SelfRegistrations;
using EventBooking.Domain.Time;

namespace EventBooking.Api.Endpoints;

/// <summary>Maps the anonymous self-registration routes.</summary>
public static class SelfRegistrationEndpoints
{
    /// <summary>The submission body.</summary>
    /// <param name="EventId">The requested event id.</param>
    /// <param name="AttendeeGroupId">The requested attendee group id.</param>
    /// <param name="Name">The requester's name.</param>
    /// <param name="Email">The requester's email.</param>
    public sealed record SubmitSelfRegistrationRequest(
        Guid EventId, Guid AttendeeGroupId, string? Name, string? Email);

    /// <summary>The per-event submission body: the event comes from the route.</summary>
    /// <param name="AttendeeGroupId">The requested attendee group id.</param>
    /// <param name="Name">The requester's name.</param>
    /// <param name="Email">The requester's email.</param>
    public sealed record SubmitEventRegistrationRequest(
        Guid AttendeeGroupId, string? Name, string? Email);

    /// <summary>Maps the public event-group routes.</summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapSelfRegistrationEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup("/api/public/event-groups")
            .AllowAnonymous()
            .RequireRateLimiting(RemoteIpRateLimiterPolicy.PolicyName);

        group.MapGet("/", async (
            ListEventGroupsHandler handler,
            IEventWindowZones zones,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.ListPublicAsync(cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            return Results.Ok(new Page<PublicEventGroupResponse>(
                [.. result.Value.Select(x => ApiResponses.PublicEventGroup(x, zones))], null));
        })
            .WithAgentMetadata("listPublicEventGroups")
            .WithEventBookingList()
            .Produces<Page<PublicEventGroupResponse>>(200)
            .ProducesProblem(429);

        group.MapGet("/{id:guid}", async (
            Guid id,
            ListEventGroupsHandler handler,
            IEventWindowZones zones,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.GetPublicAsync(id, cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            return Results.Ok(ApiResponses.PublicEventGroup(result.Value, zones));
        })
            .WithAgentMetadata("getPublicEventGroup")
            .Produces<PublicEventGroupResponse>(200)
            .ProducesProblem(404)
            .ProducesProblem(429);

        group.MapPost("/{id:guid}/registrations", async (
            Guid id,
            SubmitSelfRegistrationRequest request,
            SubmitSelfRegistrationHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new SubmitSelfRegistrationCommand(
                    id, request.EventId, request.AttendeeGroupId, request.Name, request.Email),
                cancellationToken);
            return result.IsFailure
                ? result.ToResponse()
                : Results.Created(
                    $"/api/public/event-groups/{id}/registrations/{result.Value.RequestId}",
                    ApiResponses.SubmittedRegistration(result.Value));
        })
            .WithAgentMetadata("submitSelfRegistration")
            .Produces<SubmitSelfRegistrationResponse>(201)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(422)
            .ProducesProblem(429);

        group.MapPost("/{id:guid}/events/{eventId:guid}/registrations", async (
            Guid id,
            Guid eventId,
            SubmitEventRegistrationRequest request,
            SubmitSelfRegistrationHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new SubmitSelfRegistrationCommand(
                    id, eventId, request.AttendeeGroupId, request.Name, request.Email),
                cancellationToken);
            return result.IsFailure
                ? result.ToResponse()
                : Results.Created(
                    $"/api/public/event-groups/{id}/registrations/{result.Value.RequestId}",
                    ApiResponses.SubmittedRegistration(result.Value));
        })
            .WithAgentMetadata("submitEventRegistration")
            .Produces<SubmitSelfRegistrationResponse>(201)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(422)
            .ProducesProblem(429);

        group.MapGet("/confirm/{token}", async (
            string token,
            ViewSelfRegistrationHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new ViewSelfRegistrationQuery(token), cancellationToken);
            return result.IsFailure
                ? result.ToResponse()
                : Results.Ok(ApiResponses.SelfRegistrationSummary(result.Value));
        })
            .WithAgentMetadata("viewSelfRegistration")
            .Produces<SelfRegistrationSummaryResponse>(200)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(410)
            .ProducesProblem(429);

        group.MapPost("/confirm/{token}", async (
            string token,
            ConfirmSelfRegistrationHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new ConfirmSelfRegistrationCommand(token), cancellationToken);
            return result.IsFailure
                ? result.ToResponse()
                : Results.Created(
                    $"/api/public/event-groups/confirm/{token}",
                    new ConfirmSelfRegistrationResponse(result.Value.BookingId));
        })
            .WithAgentMetadata("confirmSelfRegistration")
            .Produces<ConfirmSelfRegistrationResponse>(201)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(410)
            .ProducesProblem(429);

        return app;
    }
}
