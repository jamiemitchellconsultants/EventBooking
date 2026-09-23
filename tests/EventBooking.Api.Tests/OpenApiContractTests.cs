using System.Text.Json;
using EventBooking.Api.OpenApi;

namespace EventBooking.Api.Tests;

[Collection("api")]
public sealed class OpenApiContractTests(ApiFactory factory)
{
    [Fact]
    public async Task EveryCataloguedApplicationRouteHasItsOperationId()
    {
        using var document = await GetDocumentAsync();
        var paths = document.RootElement.GetProperty("paths");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var expected in AgentOperationCatalog.All.Values.Where(IsGeneratedOperation))
        {
            var path = paths.GetProperty(expected.Route);
            var operation = path.GetProperty(expected.Method.ToLowerInvariant());
            var operationId = operation.GetProperty("operationId").GetString();
            Assert.Equal(expected.OperationId, operationId);
            Assert.True(seen.Add(operationId!));
            Assert.Equal(expected.Tag, operation.GetProperty("tags")[0].GetString());
            Assert.False(string.IsNullOrWhiteSpace(operation.GetProperty("summary").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(operation.GetProperty("description").GetString()));
        }
    }

    [Fact]
    public async Task ProtectedOperationsCarryBearerAndMcpExtensions()
    {
        using var document = await GetDocumentAsync();
        var root = document.RootElement;
        Assert.True(root.GetProperty("components").GetProperty("securitySchemes")
            .TryGetProperty("bearer", out var bearer));
        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());

        foreach (var expected in AgentOperationCatalog.All.Values.Where(x => x.McpTool is not null))
        {
            var operation = root.GetProperty("paths").GetProperty(expected.Route)
                .GetProperty(expected.Method.ToLowerInvariant());
            Assert.Equal(expected.McpTool, operation.GetProperty("x-mcp-tool").GetString());
            var hints = operation.GetProperty("x-agent-hints");
            Assert.Equal(expected.Hints.ReadOnly, hints.GetProperty("readOnly").GetBoolean());
            Assert.Equal(expected.Hints.Destructive, hints.GetProperty("destructive").GetBoolean());
            Assert.Equal(expected.Hints.Idempotent, hints.GetProperty("idempotent").GetBoolean());
            Assert.False(hints.GetProperty("openWorld").GetBoolean());
            Assert.True(operation.TryGetProperty("security", out _));
        }
    }

    [Fact]
    public async Task ContractDeclaresProblemJsonCsvAndNoContent()
    {
        using var document = await GetDocumentAsync();
        var paths = document.RootElement.GetProperty("paths");
        var attendeeImport = paths.GetProperty("/api/attendees/import").GetProperty("post");
        Assert.True(attendeeImport.GetProperty("requestBody").GetProperty("content").TryGetProperty("text/csv", out _));
        var roster = paths.GetProperty("/api/appointment-workspace/events/{eventId}/roster").GetProperty("get");
        Assert.True(roster.GetProperty("responses").GetProperty("200").GetProperty("content").TryGetProperty("text/csv", out _));
        var withdraw = paths.GetProperty("/api/event-proposals/{id}/acceptance").GetProperty("delete");
        Assert.True(withdraw.GetProperty("responses").TryGetProperty("204", out _));
        Assert.True(withdraw.GetProperty("responses").GetProperty("404").GetProperty("content")
            .TryGetProperty("application/problem+json", out _));
    }

    private async Task<JsonDocument> GetDocumentAsync() => JsonDocument.Parse(
        await factory.CreateClient().GetStringAsync("/openapi/v1.json"));

    private static bool IsGeneratedOperation(AgentOperation operation) =>
        operation.OperationId is not "getOpenApiDocument" and not "getSwaggerUi";
}
