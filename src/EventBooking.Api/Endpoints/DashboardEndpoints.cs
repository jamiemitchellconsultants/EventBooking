using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Dashboards;

namespace EventBooking.Api.Endpoints;

/// <summary>Maps the dashboards route.</summary>
public static class DashboardEndpoints
{
    /// <summary>Maps <c>GET /api/dashboards</c>.</summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/dashboards", async (
            Guid? locationId,
            ICallerAccessor caller,
            GetDashboardsHandler handler,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetDashboardsQuery(caller.RequireStaffUserId(), locationId),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Ok(ApiResponses.Dashboards(result.Value, held));
        })
            .RequireAuthorization(AuthenticationExtensions.StaffPolicy)
            .RequireRateLimiting(StaffRateLimiterPolicy.PolicyName)
            .WithAgentMetadata("getDashboards")
            .Produces<DashboardsResponse>(200)
            .ProducesProblem(403);

        return app;
    }
}
