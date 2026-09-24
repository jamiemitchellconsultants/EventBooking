using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Locations;

namespace EventBooking.Mcp.Tests;

/// <summary>Covers the MCP transport: authorization, identity, and stateless calls.</summary>
/// <remarks>
/// The tools/list surface itself is pinned by <see cref="ParityTests"/>, which compares the
/// advertised set, descriptions and hints against the operation catalogue both ways.
/// </remarks>
[Collection("mcp")]
public sealed class McpEndpointTests(McpFactory factory)
{
    /// <summary>Anonymous MCP requests are refused before any tool runs.</summary>
    [Fact]
    public async Task AnonymousMcpRequest_IsUnauthorized()
    {
        factory.SignedInAs = null;

        var response = await PostRpcAsync(new { jsonrpc = "2.0", id = "1", method = "tools/list" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>An authenticated profile without a valid staff claim cannot reach MCP tools.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("not-a-staff-id")]
    public async Task AuthenticatedMcpRequestWithoutValidStaffClaim_IsForbidden(string? staffIdClaim)
    {
        var originalStaffUserId = factory.SignedInAs;
        var originalStaffIdClaim = factory.StaffIdClaim;
        try
        {
            factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
            factory.StaffIdClaim = staffIdClaim;

            var response = await PostRpcAsync(
                new { jsonrpc = "2.0", id = "1", method = "tools/list" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            factory.SignedInAs = originalStaffUserId;
            factory.StaffIdClaim = originalStaffIdClaim;
        }
    }

    /// <summary>A tool call runs as the signed-in identity.</summary>
    [Fact]
    public async Task GetMyAccess_ReturnsCallerRoles()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);

        var payload = await CallToolAsync("get_my_access", new { });

        Assert.Contains("Coordinator", payload.GetRawText());
        Assert.False(IsToolError(payload));
    }

    /// <summary>The caller's validated staff claim is returned by the self-description tool.</summary>
    [Fact]
    public async Task GetMyAccess_ReturnsCallerStaffNumber()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Coordinator], null, "U123456");

        var payload = await CallToolAsync("get_my_access", new { });
        var contentText = payload.GetProperty("result").GetProperty("content")[0]
            .GetProperty("text").GetString();
        using var content = JsonDocument.Parse(contentText!);

        Assert.Equal(
            "U123456",
            content.RootElement.GetProperty("staffId").GetString());
        Assert.False(IsToolError(payload));
    }

    /// <summary>Sequential calls without any session identifier each succeed.</summary>
    [Fact]
    public async Task StatelessCalls_NeedNoSession()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);

        var first = await CallToolAsync("get_my_access", new { });
        var second = await CallToolAsync("get_dashboards", new { });

        Assert.True(first.TryGetProperty("result", out _));
        Assert.True(second.TryGetProperty("result", out _));
        Assert.False(IsToolError(first));
        Assert.False(IsToolError(second));
    }

    /// <summary>A capability failure surfaces as a tool error, not a transport failure.</summary>
    [Fact]
    public async Task ForbiddenCapability_SurfacesAsToolError()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);

        var payload = await CallToolAsync(
            "propose_event",
            new
            {
                locationId = TransitionalLocation.Id,
                date = "2026-10-01",
                startTime = "09:00",
                durationMinutes = 240,
                appointmentTypeIds = new[] { AppointmentTypeIds.DrugAndAlcoholTesting },
                headcount = 10,
            });

        Assert.True(IsToolError(payload));
    }

    private async Task<HttpResponseMessage> PostRpcAsync(object body)
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return await client.SendAsync(request);
    }

    private async Task<JsonElement> PostRpcJsonAsync(object body)
    {
        var response = await PostRpcAsync(body);
        response.EnsureSuccessStatusCode();
        return ParseRpcPayload(await response.Content.ReadAsStringAsync());
    }

    private Task<JsonElement> CallToolAsync(string name, object arguments) =>
        PostRpcJsonAsync(new
        {
            jsonrpc = "2.0",
            id = "1",
            method = "tools/call",
            @params = new { name, arguments },
        });

    private static JsonElement ParseRpcPayload(string body)
    {
        if (body.TrimStart().StartsWith('{'))
        {
            return JsonDocument.Parse(body).RootElement;
        }

        var data = body
            .Split('\n')
            .Select(line => line.Trim())
            .LastOrDefault(line => line.StartsWith("data: "))
            ?.Substring("data: ".Length);
        Assert.False(string.IsNullOrWhiteSpace(data), "MCP response carried no data frame.");
        return JsonDocument.Parse(data!).RootElement;
    }

    private static bool IsToolError(JsonElement payload) =>
        payload.TryGetProperty("result", out var result) &&
        result.TryGetProperty("isError", out var isError) &&
        isError.ValueKind == JsonValueKind.True;
}
