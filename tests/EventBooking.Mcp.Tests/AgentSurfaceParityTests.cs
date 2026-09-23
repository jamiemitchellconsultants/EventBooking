using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventBooking.Api.OpenApi;
using EventBooking.Domain.Access;

namespace EventBooking.Mcp.Tests;

[Collection("mcp")]
public sealed class AgentSurfaceParityTests(McpFactory factory)
{
    [Fact]
    public async Task ToolsListExactlyMatchesCatalogAndHints()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { jsonrpc = "2.0", id = "1", method = "tools/list" }),
                Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var data = body.TrimStart().StartsWith('{') ? body : body.Split('\n').Select(x => x.Trim())
            .Last(x => x.StartsWith("data: "))["data: ".Length..];
        using var json = JsonDocument.Parse(data);
        var actual = json.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
            .ToDictionary(x => x.GetProperty("name").GetString()!);
        var expected = AgentOperationCatalog.All.Values.Where(x => x.McpTool is not null)
            .ToDictionary(x => x.McpTool!);
        Assert.Equal(35, actual.Count);
        Assert.Equal(expected.Keys.Order(), actual.Keys.Order());
        foreach (var pair in expected)
        {
            var annotations = actual[pair.Key].GetProperty("annotations");
            Assert.Equal(pair.Value.Hints.ReadOnly, annotations.GetProperty("readOnlyHint").GetBoolean());
            Assert.Equal(pair.Value.Hints.Destructive, annotations.GetProperty("destructiveHint").GetBoolean());
            Assert.Equal(pair.Value.Hints.Idempotent, annotations.GetProperty("idempotentHint").GetBoolean());
            Assert.Equal(pair.Value.Hints.OpenWorld, annotations.GetProperty("openWorldHint").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(actual[pair.Key].GetProperty("description").GetString()));
        }
    }
}
