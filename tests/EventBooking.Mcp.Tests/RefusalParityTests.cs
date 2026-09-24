using System.Text.Json;
using EventBooking.Domain.Access;

namespace EventBooking.Mcp.Tests;

/// <summary>
/// The two refusals master Task 23 names, proved to be the same refusal REST gives. Both come
/// from the handler, which is the point: a tool with its own check would pass these and still
/// diverge the moment the rule changed.
/// </summary>
[Collection("mcp")]
public sealed class RefusalParityTests(McpFactory factory)
{
    /// <summary>
    /// FR-10.7. A Manager profile with no appointment-type scope grants no capability at all,
    /// so every scoped tool refuses — and the tool itself has no scope check to have written.
    /// </summary>
    [Fact]
    public async Task AManagerWithANullScopeIsRefused()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Manager], null);
        var client = new McpClient(factory.CreateClient());

        var envelope = await client.CallRawAsync("list_event_proposals", new { limit = 50 });

        AssertRestWouldRender(envelope, "forbidden", 403);
    }

    /// <summary>
    /// The same file the REST import suite uses, refused with the same application error code
    /// — and that code is then put through the REST catalogue, so this asserts the two
    /// surfaces answer one refusal identically rather than that both merely said no. Looking
    /// for the word "validation" in a message would pass for a tool that invented its own.
    /// </summary>
    [Fact]
    public async Task AnImportOverTheRowLimitRefusesWithTheCodeRestRendersAsValidationFailed()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
        var client = new McpClient(factory.CreateClient());
        var csv = OversizedCsv();

        var envelope = await client.CallRawAsync("import_attendees", new { csv });

        AssertRestWouldRender(envelope, "validation-failed", 422);
    }

    /// <summary>An Admin gets no attendee data through MCP either (design 06's data gate).</summary>
    [Fact]
    public async Task AnAdminIsRefusedAttendeeData()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);
        var client = new McpClient(factory.CreateClient());

        var envelope = await client.CallRawAsync("list_attendees", new { limit = 50 });

        AssertRestWouldRender(envelope, "forbidden", 403);
    }

    /// <summary>
    /// A token with no staff number is refused exactly as it is over REST — which means 403 on
    /// the transport, before any tool runs, per contradiction #3. There is no JSON-RPC body to
    /// read: the shared helper throws on the status, so this case sends the request itself.
    /// </summary>
    [Fact]
    public async Task AMissingStaffNumberIsForbiddenOnTheTransport()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
        factory.StaffIdClaim = null;
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                "{\"jsonrpc\":\"2.0\",\"id\":\"1\",\"method\":\"tools/list\"}",
                System.Text.Encoding.UTF8,
                "application/json"),
        };

        var response = await client.SendAsync(request);

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// A refusal reaches the agent either as a JSON-RPC error or as a tool result marked as
    /// an error; both carry the application's own message, and this reads whichever arrived.
    /// The transport frames a tool error as "An error occurred invoking 'name': ...", so that
    /// framing is stripped first: what remains is the application's own "code: message".
    /// </summary>
    private static string Failure(JsonElement envelope)
    {
        var text = envelope.TryGetProperty("error", out var error)
            ? error.GetProperty("message").GetString() ?? string.Empty
            : ToolErrorText(envelope);
        return System.Text.RegularExpressions.Regex.Replace(
            text, "^An error occurred invoking '[^']*': ", string.Empty);
    }

    private static string ToolErrorText(JsonElement envelope)
    {
        var result = envelope.GetProperty("result");
        Assert.True(result.GetProperty("isError").GetBoolean());
        return result.GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty;
    }

    /// <summary>
    /// Puts the application error code the tool refused with through the REST catalogue, and
    /// asserts the slug and status a caller on the other surface would have been given. That
    /// is what makes "refused identically" a comparison rather than a coincidence, and it is
    /// only possible because McpErrors keeps the code in front of the message.
    /// </summary>
    /// <param name="envelope">The JSON-RPC envelope.</param>
    /// <param name="slug">The problem type design 05 names.</param>
    /// <param name="status">The status that slug carries.</param>
    private static void AssertRestWouldRender(JsonElement envelope, string slug, int status)
    {
        var failure = Failure(envelope);
        var separator = failure.IndexOf(": ", StringComparison.Ordinal);
        Assert.True(separator > 0, $"The refusal carried no application error code: {failure}");

        var shape = EventBooking.Api.Endpoints.ProblemCatalogue.For(failure[..separator]);

        Assert.Equal(slug, shape.Type);
        Assert.Equal(status, shape.Status);
    }

    /// <summary>One row more than the thousand the REST suite refuses identically.</summary>
    private static string OversizedCsv()
    {
        var rows = new System.Text.StringBuilder("name,email,attendee_group\n");
        for (var index = 0; index < 1001; index++)
        {
            rows.Append($"Row {index},row{index}@example.com,MCP_BIG\n");
        }

        return rows.ToString();
    }
}
