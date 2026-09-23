using System.Net;
using System.Text.Json;

namespace EventBooking.Api.Tests;

/// <summary>Verifies production-available OpenAPI and Swagger hosting.</summary>
[Collection("api")]
public sealed class OpenApiHostingTests(ApiFactory factory)
{
    /// <summary>The anonymous machine document is available under the stable v1 URL.</summary>
    [Fact]
    public async Task OpenApiDocumentIsAvailableAnonymously()
    {
        factory.SignedInAs = null;
        using var response = await factory.CreateClient().GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("EventBooking API", json.RootElement.GetProperty("info").GetProperty("title").GetString());
        Assert.Equal("v1", json.RootElement.GetProperty("info").GetProperty("version").GetString());
        Assert.True(json.RootElement.GetProperty("paths").TryGetProperty("/health", out _));
    }

    /// <summary>The browser documentation is available outside Development-only conditionals.</summary>
    [Fact]
    public async Task SwaggerUiLoadsTheFirstPartyDocument()
    {
        factory.SignedInAs = null;
        using var response = await factory.CreateClient().GetAsync("/swagger/index.html");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("EventBooking API v1", html, StringComparison.Ordinal);
        Assert.Contains("/openapi/v1.json", html, StringComparison.Ordinal);
    }

    /// <summary>The MCP host is not accidentally given REST documentation middleware.</summary>
    [Fact]
    public async Task ApiDocumentDoesNotDescribeMcpTransport()
    {
        using var json = JsonDocument.Parse(
            await factory.CreateClient().GetStringAsync("/openapi/v1.json"));
        Assert.False(json.RootElement.GetProperty("paths").TryGetProperty("/mcp", out _));
    }
}
