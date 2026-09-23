namespace EventBooking.Api.OpenApi;

/// <summary>Applies stable agent-facing metadata to Minimal API endpoints.</summary>
public static class EndpointMetadataExtensions
{
    /// <summary>Applies the catalogued operation identity and human description.</summary>
    /// <param name="builder">The endpoint being described.</param>
    /// <param name="operationId">The lower-camel-case operation id in the agent catalog.</param>
    /// <returns>The endpoint builder for chaining.</returns>
    public static RouteHandlerBuilder WithAgentMetadata(
        this RouteHandlerBuilder builder,
        string operationId)
    {
        var operation = AgentOperationCatalog.Get(operationId);
        return builder
            .WithName(operation.OperationId)
            .WithTags(operation.Tag)
            .WithSummary(operation.Summary)
            .WithDescription(operation.Description);
    }
}
