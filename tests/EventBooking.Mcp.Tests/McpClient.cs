using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EventBooking.Mcp.Tests;

/// <summary>
/// The JSON-RPC calls these suites make. The HTTP transport answers as JSON or as an event
/// stream depending on what the request accepts, so both are read here — a helper that
/// understood only one would pass or fail for reasons unrelated to the tool under test.
/// </summary>
/// <param name="client">The client to send through.</param>
public sealed class McpClient(HttpClient client)
{
    private int _id;

    /// <summary>Calls <c>tools/list</c>.</summary>
    /// <returns>The result element.</returns>
    public async Task<JsonElement> ListToolsAsync() => await SendAsync("tools/list", null);

    /// <summary>Calls one tool.</summary>
    /// <param name="tool">The tool name.</param>
    /// <param name="arguments">The tool arguments.</param>
    /// <returns>The result element.</returns>
    public async Task<JsonElement> CallAsync(string tool, object arguments) =>
        await SendAsync("tools/call", new { name = tool, arguments });

    /// <summary>Calls one tool and returns the raw envelope, errors included.</summary>
    /// <param name="tool">The tool name.</param>
    /// <param name="arguments">The tool arguments.</param>
    /// <returns>The whole response element.</returns>
    public async Task<JsonElement> CallRawAsync(string tool, object arguments) =>
        await SendRawAsync("tools/call", new { name = tool, arguments });

    private async Task<JsonElement> SendAsync(string method, object? parameters)
    {
        var envelope = await SendRawAsync(method, parameters);
        return envelope.GetProperty("result");
    }

    private async Task<JsonElement> SendRawAsync(string method, object? parameters)
    {
        var payload = parameters is null
            ? JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0", id = Interlocked.Increment(ref _id).ToString(), method,
            })
            : JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0", id = Interlocked.Increment(ref _id).ToString(), method,
                @params = parameters,
            });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var json = body.TrimStart().StartsWith('{')
            ? body
            : body.Split('\n').Select(line => line.Trim())
                .Last(line => line.StartsWith("data: ", StringComparison.Ordinal))["data: ".Length..];

        using var parsed = JsonDocument.Parse(json);
        return parsed.RootElement.Clone();
    }
}
