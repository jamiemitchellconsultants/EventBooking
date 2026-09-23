using System.Text.Json.Serialization;

namespace EventBooking.Api.Contracts;

/// <summary>One root-relative API affordance joined to OpenAPI by operation id.</summary>
/// <param name="Href">The root-relative URI of the linked operation.</param>
/// <param name="Method">The uppercase HTTP method of the linked operation.</param>
/// <param name="OperationId">The OpenAPI operation id of the linked operation.</param>
public sealed record ApiLink(
    /// <summary>Gets the root-relative URI of the linked operation.</summary>
    string Href,
    /// <summary>Gets the uppercase HTTP method of the linked operation.</summary>
    string Method,
    /// <summary>Gets the OpenAPI operation id of the linked operation.</summary>
    string OperationId);

/// <summary>The anonymous top-level API entry document.</summary>
/// <param name="Name">The service name displayed to API clients.</param>
/// <param name="Version">The API version displayed to API clients.</param>
/// <param name="Links">The entry-point relations keyed by stable relation name.</param>
public sealed record ApiDiscoveryResponse(
    /// <summary>Gets the service name displayed to API clients.</summary>
    string Name,
    /// <summary>Gets the API version displayed to API clients.</summary>
    string Version,
    /// <summary>Gets the entry-point relations keyed by stable relation name.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);
