using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventBooking.Domain.Access;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Mcp.Tests;

/// <summary>Covers the attendee read-parity contract exposed by MCP.</summary>
[Collection("mcp")]
public sealed class AttendeeMcpTests(McpFactory factory)
{
    /// <summary>Listing exposes group identity, derived requirements, and readiness.</summary>
    [Fact]
    public async Task ListAttendees_ReturnsGroupRequirementsAndReadiness()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
        var email = $"mcp-{Guid.NewGuid():N}@example.com";
        await CallToolResultAsync(
            "create_attendee",
            new { name = "Mcp Pilot", email, attendeeGroupCode = "PILOTS" });

        var items = await CallToolResultAsync(
            "list_attendees", new { search = email });
        var item = items.EnumerateArray().Single();

        Assert.Equal("PILOTS", item.GetProperty("attendeeGroupCode").GetString());
        Assert.False(item.GetProperty("requiresAttendeeGroupReconciliation").GetBoolean());
        Assert.Equal(2, item.GetProperty("requiredAppointmentTypes").GetArrayLength());
        Assert.Equal(
            "NoActiveBooking",
            item.GetProperty("readiness").GetProperty("code").GetString());
    }

    /// <summary>Pages slice the list without overlap.</summary>
    [Fact]
    public async Task ListAttendees_PagesWithoutOverlap()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
        var prefix = $"paged-{Guid.NewGuid():N}";
        for (var index = 0; index < 3; index++)
        {
            await CallToolResultAsync(
                "create_attendee",
                new
                {
                    name = $"Paged {index}",
                    email = $"{prefix}-{index}@example.com",
                    attendeeGroupCode = "PILOTS",
                });
        }

        var first = await CallToolResultAsync(
            "list_attendees", new { search = prefix, page = 1, pageSize = 2 });
        var second = await CallToolResultAsync(
            "list_attendees", new { search = prefix, page = 2, pageSize = 2 });

        Assert.Equal(2, first.GetArrayLength());
        Assert.Single(second.EnumerateArray());
        Assert.Empty(
            first.EnumerateArray().Select(item => item.GetRawText())
                .Intersect(second.EnumerateArray().Select(item => item.GetRawText())));
    }

    /// <summary>An unknown status surfaces as a tool error, not a transport failure.</summary>
    [Fact]
    public async Task ListAttendees_RejectsUnknownStatus()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);

        var payload = await CallToolAsync("list_attendees", new { status = "Bogus" });

        Assert.True(IsToolError(payload));
    }

    /// <summary>Create input accepts a group code and no appointment-type codes.</summary>
    [Fact]
    public async Task CreateAttendeeSchema_HasGroupCodeWithoutAppointmentTypes()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);

        var payload = await PostRpcJsonAsync(new { jsonrpc = "2.0", id = "1", method = "tools/list" });
        var create = payload
            .GetProperty("result").GetProperty("tools").EnumerateArray()
            .Single(tool => tool.GetProperty("name").GetString() == "create_attendee");
        var properties = create
            .GetProperty("inputSchema").GetProperty("properties").EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("attendeeGroupCode", properties);
        Assert.DoesNotContain("appointmentTypeIds", properties);
        Assert.DoesNotContain("appointmentTypes", properties);
        Assert.DoesNotContain("requiredTypes", properties);
    }

    /// <summary>Recovery tool inputs carry attendee and invite identifiers and no token.</summary>
    [Fact]
    public async Task RecoveryToolSchemas_HaveIdentifiersWithoutTokens()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);

        var payload = await PostRpcJsonAsync(new { jsonrpc = "2.0", id = "1", method = "tools/list" });
        var tools = payload
            .GetProperty("result").GetProperty("tools").EnumerateArray()
            .Where(tool => tool.GetProperty("name").GetString() is "start_recovery_invite" or "cancel_recovery_invite")
            .ToList();

        Assert.Equal(2, tools.Count);
        foreach (var tool in tools)
        {
            var properties = tool
                .GetProperty("inputSchema").GetProperty("properties").EnumerateObject()
                .Select(property => property.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.Contains("attendeeId", properties);
            Assert.DoesNotContain("token", properties);
        }

        var cancel = tools.Single(tool => tool.GetProperty("name").GetString() == "cancel_recovery_invite");
        var cancelProperties = cancel
            .GetProperty("inputSchema").GetProperty("properties").EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("inviteId", cancelProperties);
    }

    private async Task<JsonElement> CallToolResultAsync(string name, object arguments)
    {
        var payload = await CallToolAsync(name, arguments);
        Assert.False(IsToolError(payload), payload.GetRawText());
        var text = payload.GetProperty("result").GetProperty("content")[0]
            .GetProperty("text").GetString();
        using var document = JsonDocument.Parse(text!);
        return document.RootElement.Clone();
    }

    private Task<JsonElement> CallToolAsync(string name, object arguments) =>
        PostRpcJsonAsync(new
        {
            jsonrpc = "2.0",
            id = "1",
            method = "tools/call",
            @params = new { name, arguments },
        });

    private async Task<JsonElement> PostRpcJsonAsync(object body)
    {
        var response = await PostRpcAsync(body);
        response.EnsureSuccessStatusCode();
        return ParseRpcPayload(await response.Content.ReadAsStringAsync());
    }

    private async Task<HttpResponseMessage> PostRpcAsync(object body)
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return await client.SendAsync(request);
    }

    private static JsonElement ParseRpcPayload(string body)
    {
        if (body.TrimStart().StartsWith('{'))
        {
            return JsonDocument.Parse(body).RootElement.Clone();
        }

        var data = body.Split('\n').Select(line => line.Trim())
            .Last(line => line.StartsWith("data: "))["data: ".Length..];
        return JsonDocument.Parse(data).RootElement.Clone();
    }

    private static bool IsToolError(JsonElement payload) =>
        payload.TryGetProperty("result", out var result) &&
        result.TryGetProperty("isError", out var isError) &&
        isError.ValueKind == JsonValueKind.True;
}
