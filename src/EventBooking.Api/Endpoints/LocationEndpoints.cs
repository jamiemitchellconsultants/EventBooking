using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Api.Pagination;
using EventBooking.Application.Access;
using EventBooking.Application.ReferenceData;

namespace EventBooking.Api.Endpoints;

/// <summary>Maps the three location routes.</summary>
public static class LocationEndpoints
{
    /// <summary>The creation body design 05 names.</summary>
    /// <param name="Code">The canonical code.</param>
    /// <param name="Name">The display name.</param>
    /// <param name="Address">The postal address.</param>
    /// <param name="TimeZoneId">The IANA zone.</param>
    public sealed record CreateLocationRequest(
        string? Code, string? Name, string? Address, string? TimeZoneId);

    /// <summary>The update body design 05 names.</summary>
    /// <param name="Name">The display name.</param>
    /// <param name="Address">The postal address.</param>
    /// <param name="TimeZoneId">The IANA zone.</param>
    /// <param name="IsActive">Whether the location stays in use.</param>
    /// <param name="ExpectedVersion">The version the caller read.</param>
    public sealed record UpdateLocationRequest(
        string? Name, string? Address, string? TimeZoneId, bool IsActive, long ExpectedVersion);

    /// <summary>Maps the location routes.</summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapLocationEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup("/api/locations")
            .RequireAuthorization(AuthenticationExtensions.StaffPolicy)
            .RequireRateLimiting(StaffRateLimiterPolicy.PolicyName);

        group.MapGet("/", async (
            bool? includeInactive,
            ListLocationsHandler handler,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new ListLocationsQuery(includeInactive ?? false), cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Ok(new Page<LocationResponse>(
                [.. result.Value.Select(x => ApiResponses.Location(x, held))], null));
        })
            .WithAgentMetadata("listLocations")
            .WithEventBookingList()
            .Produces<Page<LocationResponse>>(200)
            .ProducesProblem(401)
            .ProducesProblem(403);

        group.MapPost("/", async (
            CreateLocationRequest request,
            ICallerAccessor caller,
            CreateLocationHandler handler,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new CreateLocationCommand(
                    caller.RequireStaffUserId(), request.Code, request.Name, request.Address,
                    request.TimeZoneId),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Created(
                $"/api/locations/{result.Value.Id}",
                ApiResponses.Location(result.Value, held));
        })
            .WithAgentMetadata("createLocation")
            .Produces<LocationResponse>(201)
            .ProducesProblem(403)
            .ProducesProblem(409)
            .ProducesProblem(422);

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateLocationRequest request,
            ICallerAccessor caller,
            UpdateLocationHandler handler,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new UpdateLocationCommand(
                    caller.RequireStaffUserId(), id, request.Name, request.Address,
                    request.TimeZoneId, request.IsActive, request.ExpectedVersion),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Ok(ApiResponses.Location(result.Value, held));
        })
            .WithAgentMetadata("updateLocation")
            .Produces<LocationResponse>(200)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(422);

        return app;
    }
}
