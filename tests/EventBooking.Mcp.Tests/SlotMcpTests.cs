using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventBooking.Domain.Access;

namespace EventBooking.Mcp.Tests;

[Collection("mcp")]
public sealed class SlotMcpTests(McpFactory factory)
{
    [Fact]
    public async Task SlotOperationsReturnsCandidateFreeView()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);
        var payload = await CallToolAsync("get_slot_operations", new { });
        Assert.False(IsToolError(payload), payload.GetRawText());
        var text = payload.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString();
        using var result = JsonDocument.Parse(text!);
        Assert.Equal(JsonValueKind.Array, result.RootElement.GetProperty("slots").ValueKind);
        Assert.DoesNotContain("candidate", result.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SlotOperationsIsDeniedWithoutCapability()
    {
        // ViewSlotOperations allows scoped managers, so the unscoped
        // AppointmentStaff profile exercises the denial path instead.
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], null);
        var payload = await CallToolAsync("get_slot_operations", new { });
        Assert.True(IsToolError(payload));
    }

    private async Task<JsonElement> CallToolAsync(string name, object arguments)
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0", id = "1", method = "tools/call", @params = new { name, arguments },
            }), Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var data = body.TrimStart().StartsWith('{') ? body : body.Split('\n').Select(x => x.Trim())
            .Last(x => x.StartsWith("data: "))["data: ".Length..];
        return JsonDocument.Parse(data).RootElement.Clone();
    }

    private static bool IsToolError(JsonElement payload) =>
        payload.GetProperty("result").TryGetProperty("isError", out var error) && error.GetBoolean();
}
