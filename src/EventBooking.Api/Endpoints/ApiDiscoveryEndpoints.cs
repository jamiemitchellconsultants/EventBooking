using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;

namespace EventBooking.Api.Endpoints;

/// <summary>Maps the anonymous API discovery root.</summary>
public static class ApiDiscoveryEndpoints
{
    /// <summary>Maps the anonymous <c>GET /api</c> entry document.</summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapApiDiscoveryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api", () =>
        {
            var links = new Dictionary<string, ApiLink>(StringComparer.Ordinal)
            {
                ["self"] = Link("getApiIndex"),
                ["openapi"] = Link("getOpenApiDocument"),
                ["swagger"] = Link("getSwaggerUi"),
                ["health"] = Link("getHealth"),
                ["me"] = Link("getMyAccess"),
                ["slotBoard"] = Link("getSlotBoard"),
                ["slotOperations"] = Link("getSlotOperations"),
                ["candidates"] = Link("listCandidates"),
                ["employeeGroups"] = Link("listEmployeeGroups"),
                ["settings"] = Link("getSettings"),
                ["staffAccess"] = Link("listStaffAccess"),
                ["dashboards"] = Link("getDashboards"),
                ["audit"] = Link("searchAudit"),
                ["appointments"] = Link("listAppointmentSlots"),
            };
            return Results.Ok(new ApiDiscoveryResponse("EventBooking API", "v1", links));
        }).AllowAnonymous()
            .WithAgentMetadata("getApiIndex")
            .Produces(200);
        return app;
    }

    private static ApiLink Link(string operationId)
    {
        var operation = AgentOperationCatalog.Get(operationId);
        return new ApiLink(operation.Route, operation.Method, operation.OperationId);
    }
}
