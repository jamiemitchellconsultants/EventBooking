using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;

namespace EventBooking.Api.Endpoints;

/// <summary>Maps the anonymous API discovery root.</summary>
public static class ApiDiscoveryEndpoints
{
    /// <summary>Maps the anonymous entry document listing every top-level resource.</summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapApiDiscoveryEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api", () =>
        {
            // Unconditional: the index is anonymous, so it advertises the resources rather
            // than the caller's permissions. Each resource then carries its own _links, which
            // is where capability actually shows.
            var links = new Dictionary<string, ApiLink>(StringComparer.Ordinal)
            {
                ["self"] = Link("getApiIndex"),
                ["openapi"] = Link("getOpenApiDocument"),
                ["swagger"] = Link("getSwaggerUi"),
                ["health"] = Link("getLiveness"),
                ["me"] = Link("getMyAccess"),
                ["locations"] = Link("listLocations"),
                ["appointmentTypes"] = Link("listAppointmentTypes"),
                ["attendeeGroups"] = Link("listAttendeeGroups"),
                ["settings"] = Link("getSettings"),
                ["staffAccess"] = Link("listStaffAccess"),
                ["eventProposals"] = Link("listEventProposals"),
                ["events"] = Link("listEvents"),
                ["attendees"] = Link("listAttendees"),
                ["dashboards"] = Link("getDashboards"),
                ["audit"] = Link("searchAudit"),
                ["appointmentWorkspace"] = Link("listWorkspaceEvents"),
            };
            return Results.Ok(new ApiDiscoveryResponse("EventBooking API", "v1", links));
        })
            .AllowAnonymous()
            .WithAgentMetadata("getApiIndex")
            .Produces<ApiDiscoveryResponse>(200);

        return app;
    }

    private static ApiLink Link(string operationId)
    {
        var operation = AgentOperationCatalog.Get(operationId);
        return new ApiLink(operation.Route, operation.Method, operation.OperationId);
    }
}
