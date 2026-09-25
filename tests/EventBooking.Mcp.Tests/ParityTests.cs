using System.Text.Json;
using EventBooking.Api.OpenApi;
using EventBooking.Domain.Access;

namespace EventBooking.Mcp.Tests;

/// <summary>
/// FR-14.1: the tool set is the OpenAPI operation set minus the anonymous and token routes.
/// Compared both ways, so a tool without a route fails as loudly as a route without a tool.
/// </summary>
[Collection("mcp")]
public sealed class ParityTests(McpFactory factory)
{
    [Fact]
    public async Task TheToolSetEqualsTheStaffOperationSet()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);
        var tools = await ToolsAsync();
        var expected = AgentOperationCatalog.All.Values
            .Where(x => x.McpTool is not null)
            .Select(x => x.McpTool!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(52, expected.Count);
        Assert.Equal(expected.Count, tools.Count);
        Assert.Empty(expected.Except(tools.Keys));
        Assert.Empty(tools.Keys.Except(expected));
    }

    /// <summary>
    /// The four attendee-token routes and the six discovery and health routes have no tool.
    /// Naming them here rather than deriving them means a later decision to expose one has to
    /// change this test on purpose. The names are the snake-case spellings such tools would
    /// take under the catalogue's own convention.
    /// </summary>
    [Theory]
    [InlineData("view_invite")]
    [InlineData("confirm_booking")]
    [InlineData("view_managed_booking")]
    [InlineData("cancel_managed_booking")]
    [InlineData("get_api_index")]
    [InlineData("get_open_api_document")]
    [InlineData("get_swagger_ui")]
    [InlineData("get_liveness")]
    [InlineData("get_readiness")]
    [InlineData("get_metrics")]
    public async Task NoToolExistsForAnAnonymousRoute(string absent)
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);

        var tools = await ToolsAsync();

        Assert.DoesNotContain(absent, tools.Keys);
    }

    /// <summary>Each tool's description and hints are the catalogue's, not its own.</summary>
    [Fact]
    public async Task EveryToolCarriesTheCataloguesDescriptionAndHints()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);
        var tools = await ToolsAsync();

        foreach (var operation in AgentOperationCatalog.All.Values.Where(x => x.McpTool is not null))
        {
            var tool = tools[operation.McpTool!];
            Assert.Equal(operation.Description, tool.GetProperty("description").GetString());
            var annotations = tool.GetProperty("annotations");
            Assert.Equal(
                operation.Hints.ReadOnly, annotations.GetProperty("readOnlyHint").GetBoolean());
            Assert.Equal(
                operation.Hints.Destructive, annotations.GetProperty("destructiveHint").GetBoolean());
            Assert.Equal(
                operation.Hints.Idempotent, annotations.GetProperty("idempotentHint").GetBoolean());
            Assert.False(annotations.GetProperty("openWorldHint").GetBoolean());
        }
    }

    [Fact]
    public async Task EveryToolNameIsSnakeCase()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);

        var tools = await ToolsAsync();

        Assert.All(tools.Keys, name => Assert.Matches("^[a-z][a-z0-9_]*$", name));
    }

    /// <summary>
    /// updateAttendee is a whole replacement on both surfaces: the handler refuses a missing
    /// group, name or email rather than keeping the stored value. A schema that let an agent
    /// omit one would advertise a partial update that every such call is refused for.
    /// </summary>
    [Fact]
    public async Task UpdateAttendeeRequiresEveryFieldTheHandlerReplaces()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);
        var tools = await ToolsAsync();

        var required = tools["update_attendee"].GetProperty("inputSchema").GetProperty("required")
            .EnumerateArray().Select(x => x.GetString()).ToHashSet(StringComparer.Ordinal);

        Assert.Superset(
            new HashSet<string?>(["attendeeId", "name", "email", "attendeeGroupId"], StringComparer.Ordinal),
            required);
    }

    private async Task<IReadOnlyDictionary<string, JsonElement>> ToolsAsync()
    {
        var client = new McpClient(factory.CreateClient());
        var result = await client.ListToolsAsync();
        return result.GetProperty("tools").EnumerateArray()
            .ToDictionary(x => x.GetProperty("name").GetString()!, x => x);
    }
}
