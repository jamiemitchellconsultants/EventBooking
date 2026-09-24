using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace EventBooking.Api.OpenApi;

/// <summary>Registers the first-party OpenAPI document with agent extensions.</summary>
public static class OpenApiConfiguration
{
    /// <summary>Registers the v1 document, security scheme, and agent extensions.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEventBookingOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "EventBooking API",
                    Version = "v1",
                    Description = "EventBooking staff API and anonymous Attendee booking links.",
                };
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes["bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                };
                return Task.CompletedTask;
            });
            options.AddOperationTransformer((operation, context, _) =>
            {
                var name = context.Description.ActionDescriptor.EndpointMetadata
                    .OfType<EndpointNameMetadata>()
                    .FirstOrDefault()?.EndpointName;
                if (name is null || !AgentOperationCatalog.All.TryGetValue(name, out var entry))
                {
                    return Task.CompletedTask;
                }

                operation.Summary = entry.Summary;
                operation.Description = entry.Description;
                operation.Tags = new HashSet<OpenApiTagReference> { new(entry.Tag) };

                if (entry.Capability is not null)
                {
                    operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                    operation.Extensions["x-capability"] =
                        new JsonNodeExtension(JsonValue.Create(entry.Capability)!);
                }

                if (entry.McpTool is not null)
                {
                    operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                    operation.Extensions["x-mcp-tool"] = new JsonNodeExtension(JsonValue.Create(entry.McpTool)!);
                    operation.Extensions["x-agent-hints"] = new JsonNodeExtension(new JsonObject
                    {
                        ["readOnly"] = entry.Hints.ReadOnly,
                        ["destructive"] = entry.Hints.Destructive,
                        ["idempotent"] = entry.Hints.Idempotent,
                        ["openWorld"] = entry.Hints.OpenWorld,
                    });
                }

                if (entry.RequiresBearer)
                {
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("bearer")] = [],
                    });
                }

                return Task.CompletedTask;
            });
        });
        return services;
    }
}
