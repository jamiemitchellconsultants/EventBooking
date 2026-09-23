using System.Net;
using System.Text.Json;

namespace EventBooking.Api.Tests;

[Collection("api")]
public sealed class ApiDiscoveryTests(ApiFactory factory)
{
    [Fact]
    public async Task ApiRootIsAnonymousAndCarriesStableLinks()
    {
        factory.SignedInAs = null;
        using var response = await factory.CreateClient().GetAsync("/api");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("EventBooking API", json.RootElement.GetProperty("name").GetString());
        Assert.Equal("v1", json.RootElement.GetProperty("version").GetString());
        var links = json.RootElement.GetProperty("_links");
        AssertLink(links, "self", "/api", "GET", "getApiIndex");
        AssertLink(links, "openapi", "/openapi/v1.json", "GET", "getOpenApiDocument");
        AssertLink(links, "swagger", "/swagger", "GET", "getSwaggerUi");
        AssertLink(links, "me", "/api/me", "GET", "getMyAccess");
        AssertLink(links, "attendees", "/api/attendees", "GET", "listAttendees");
    }

    private static void AssertLink(JsonElement links, string relation, string href, string method, string operationId)
    {
        var link = links.GetProperty(relation);
        Assert.Equal(href, link.GetProperty("href").GetString());
        Assert.Equal(method, link.GetProperty("method").GetString());
        Assert.Equal(operationId, link.GetProperty("operationId").GetString());
    }
}
