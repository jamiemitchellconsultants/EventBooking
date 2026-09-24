using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventBooking.Domain.Access;

namespace EventBooking.Mcp.Tests;

/// <summary>Covers the MCP transport: authorization, tool discovery, and stateless calls.</summary>
[Collection("mcp")]
public sealed class McpEndpointTests(McpFactory factory)
{
    private static readonly string[] ExpectedTools =
    [
        "propose_event", "accept_proposal", "withdraw_acceptance", "withdraw_proposal",
        "event_board", "adjust_event_capacity", "cancel_event",
        "list_attendees", "list_invite_locations", "create_attendee", "update_attendee", "delete_attendee",
        "list_attendee_groups",
        "import_attendees", "trigger_invite", "retry_attendee_email",
        "start_recovery_invite", "cancel_recovery_invite", "list_attendee_bookings",
        "cancel_attendee_booking", "get_attendee_readiness",
        "get_settings", "update_settings", "list_staff_access", "replace_staff_access_scope",
        "clear_staff_access_scope", "get_my_access",
        "get_dashboards", "event_audit_history", "attendee_audit_history", "search_audit",
        "appointment_events", "appointment_event_detail", "export_appointment_roster", "update_appointment_status",
        "get_event_operations",
    ];

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

    /// <summary>An authenticated caller discovers the full staff tool surface.</summary>
    [Fact]
    public async Task ToolsList_ExposesFullStaffSurface()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);

        var payload = await PostRpcJsonAsync(new { jsonrpc = "2.0", id = "1", method = "tools/list" });
        var names = payload
            .GetProperty("result").GetProperty("tools").EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString())
            .ToHashSet();

        Assert.Equal(ExpectedTools.Order(), names.Order());
        Assert.Equal(36, names.Count);
    }

    /// <summary>Every tool carries explicit safety hints with a closed world.</summary>
    [Fact]
    public async Task ToolsList_ExposesExplicitSafetyHints()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);

        var payload = await PostRpcJsonAsync(new { jsonrpc = "2.0", id = "1", method = "tools/list" });
        foreach (var tool in payload.GetProperty("result").GetProperty("tools").EnumerateArray())
        {
            Assert.True(tool.TryGetProperty("annotations", out var annotations), $"Tool {tool.GetProperty("name")} is missing annotations.");
            Assert.True(annotations.TryGetProperty("readOnlyHint", out _), $"Tool {tool.GetProperty("name")} is missing readOnlyHint.");
            Assert.True(annotations.TryGetProperty("destructiveHint", out _), $"Tool {tool.GetProperty("name")} is missing destructiveHint.");
            Assert.True(annotations.TryGetProperty("idempotentHint", out _), $"Tool {tool.GetProperty("name")} is missing idempotentHint.");
            Assert.True(
                annotations.TryGetProperty("openWorldHint", out var openWorld) && openWorld.ValueKind == JsonValueKind.False,
                $"Tool {tool.GetProperty("name")} must have openWorldHint false.");
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
            "propose_event", new { date = "2026-10-01", startTime = "09:00" });

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
