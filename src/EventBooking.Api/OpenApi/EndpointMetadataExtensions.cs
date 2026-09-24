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

    /// <summary>Marks a GET returning the items/nextCursor page envelope for OpenAPI.</summary>
    /// <param name="builder">The endpoint being described.</param>
    /// <returns>The endpoint builder for chaining.</returns>
    public static RouteHandlerBuilder WithEventBookingList(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithMetadata(new EventBookingListMetadata());
    }
}

/// <summary>Marks a GET whose 200 body is the items/nextCursor page envelope.</summary>
public sealed record EventBookingListMetadata();
