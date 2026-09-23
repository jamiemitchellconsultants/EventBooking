using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Dashboards;

namespace EventBooking.Api.Endpoints;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dashboards", async (
            ICallerAccessor caller,
            GetDashboardsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetDashboardsQuery(caller.RequireStaffUserId()), cancellationToken);
            return result.IsSuccess
                ? Results.Ok(DashboardResourceResponse.From(result.Value))
                : result.ToResponse();
        })
            .RequireAuthorization(AuthenticationExtensions.StaffPolicy)
            .WithAgentMetadata("getDashboards")
            .Produces(200)
            .ProducesProblem(403);

        return app;
    }
}
