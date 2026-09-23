using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventBooking.Domain.Access;

namespace EventBooking.Mcp.Tests;

[Collection("mcp")]
public sealed class VocabularyToolTests(McpFactory factory)
{
    [Fact]
    public async Task Advertised_tools_use_canonical_vocabulary()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":\"names\",\"method\":\"tools/list\"}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        using var response = await factory.CreateClient().SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var data = body.TrimStart().StartsWith('{') ? body : body.Split('\n').Select(x => x.Trim())
            .Last(x => x.StartsWith("data: "))["data: ".Length..];
        using var json = JsonDocument.Parse(data);
        var names = json.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString()!).ToArray();
        Assert.NotEmpty(names);
        string[] retired = ["candi" + "date", "slo" + "t", "employee" + "group", "head" + "office"];
        Assert.DoesNotContain(names, name => retired.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }
}
