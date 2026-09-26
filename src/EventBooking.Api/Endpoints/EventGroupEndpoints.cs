using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Api.Pagination;
using EventBooking.Application.Access;
using EventBooking.Application.EventGroups;

namespace EventBooking.Api.Endpoints;

/// <summary>Maps the staff event-group routes.</summary>
public static class EventGroupEndpoints
{
    /// <summary>The creation body.</summary>
    /// <param name="Title">The public title.</param>
    /// <param name="Description">The public description.</param>
    /// <param name="AttendeeGroupIds">The selected attendee groups.</param>
    public sealed record CreateEventGroupRequest(
        string? Title, string? Description, IReadOnlyList<Guid>? AttendeeGroupIds);

    /// <summary>The update body.</summary>
    /// <param name="Title">The public title.</param>
    /// <param name="Description">The public description.</param>
    /// <param name="AttendeeGroupIds">The selected attendee groups.</param>
    /// <param name="IsOpen">Whether anonymous registration is open for this group.</param>
    /// <param name="ExpectedVersion">The version the caller read.</param>
    public sealed record UpdateEventGroupRequest(
        string? Title, string? Description, IReadOnlyList<Guid>? AttendeeGroupIds,
        bool IsOpen, long ExpectedVersion);

    /// <summary>The add-membership body.</summary>
    /// <param name="ExpectedVersion">The version the caller read.</param>
    public sealed record AddEventGroupEventRequest(long ExpectedVersion);

    /// <summary>The membership-gate body.</summary>
    /// <param name="IsOpen">Whether anonymous registration is open for this membership.</param>
    /// <param name="ExpectedVersion">The version the caller read.</param>
    public sealed record SetEventGroupEventOpenRequest(bool IsOpen, long ExpectedVersion);

    /// <summary>Maps the event-group routes.</summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapEventGroupEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup("/api/event-groups")
            .RequireAuthorization(AuthenticationExtensions.StaffPolicy)
            .RequireRateLimiting(StaffRateLimiterPolicy.PolicyName);

        group.MapGet("/", async (
            ListEventGroupsHandler handler,
            ICallerAccessor caller,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.ListAsync(
                caller.RequireStaffUserId(), cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Ok(new Page<EventGroupResponse>(
                [.. result.Value.Select(x => ApiResponses.EventGroup(x, held))], null));
        })
            .WithAgentMetadata("listEventGroups")
            .WithEventBookingList()
            .Produces<Page<EventGroupResponse>>(200)
            .ProducesProblem(401)
            .ProducesProblem(403);

        group.MapGet("/{id:guid}", async (
            Guid id,
            ListEventGroupsHandler handler,
            ICallerAccessor caller,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.GetAsync(
                caller.RequireStaffUserId(), id, cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Ok(ApiResponses.EventGroup(result.Value, held));
        })
            .WithAgentMetadata("getEventGroup")
            .Produces<EventGroupResponse>(200)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(404);

        group.MapPost("/", async (
            CreateEventGroupRequest request,
            ICallerAccessor caller,
            ManageEventGroupHandler handler,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.CreateAsync(
                new CreateEventGroupCommand(
                    caller.RequireStaffUserId(), request.Title, request.Description,
                    request.AttendeeGroupIds ?? []),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Created(
                $"/api/event-groups/{result.Value.Id}",
                ApiResponses.EventGroup(result.Value, held));
        })
            .WithAgentMetadata("createEventGroup")
            .Produces<EventGroupResponse>(201)
            .ProducesProblem(403)
            .ProducesProblem(409)
            .ProducesProblem(422);

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateEventGroupRequest request,
            ICallerAccessor caller,
            ManageEventGroupHandler handler,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.UpdateAsync(
                new UpdateEventGroupCommand(
                    caller.RequireStaffUserId(), id, request.Title, request.Description,
                    request.AttendeeGroupIds ?? [], request.IsOpen, request.ExpectedVersion),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Ok(ApiResponses.EventGroup(result.Value, held));
        })
            .WithAgentMetadata("updateEventGroup")
            .Produces<EventGroupResponse>(200)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(422);

        group.MapPut("/{id:guid}/events/{eventId:guid}", async (
            Guid id,
            Guid eventId,
            AddEventGroupEventRequest request,
            ICallerAccessor caller,
            ManageEventGroupHandler handler,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.ChangeEventAsync(
                new ChangeEventGroupEventCommand(
                    caller.RequireStaffUserId(), id, eventId, null, false,
                    request.ExpectedVersion),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Ok(ApiResponses.EventGroup(result.Value, held));
        })
            .WithAgentMetadata("addEventGroupEvent")
            .Produces<EventGroupResponse>(200)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(422);

        group.MapPatch("/{id:guid}/events/{eventId:guid}", async (
            Guid id,
            Guid eventId,
            SetEventGroupEventOpenRequest request,
            ICallerAccessor caller,
            ManageEventGroupHandler handler,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.ChangeEventAsync(
                new ChangeEventGroupEventCommand(
                    caller.RequireStaffUserId(), id, eventId, request.IsOpen, false,
                    request.ExpectedVersion),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Ok(ApiResponses.EventGroup(result.Value, held));
        })
            .WithAgentMetadata("setEventGroupEventOpen")
            .Produces<EventGroupResponse>(200)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(422);

        group.MapDelete("/{id:guid}/events/{eventId:guid}", async (
            Guid id,
            Guid eventId,
            long expectedVersion,
            ICallerAccessor caller,
            ManageEventGroupHandler handler,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.ChangeEventAsync(
                new ChangeEventGroupEventCommand(
                    caller.RequireStaffUserId(), id, eventId, null, true, expectedVersion),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Ok(ApiResponses.EventGroup(result.Value, held));
        })
            .WithAgentMetadata("removeEventGroupEvent")
            .Produces<EventGroupResponse>(200)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(422);

        return app;
    }
}
