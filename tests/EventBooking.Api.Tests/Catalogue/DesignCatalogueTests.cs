using System.Text.Json;
using System.Text.RegularExpressions;
using EventBooking.Api.OpenApi;

namespace EventBooking.Api.Tests.Catalogue;

/// <summary>
/// Design 05's endpoint tables, parsed, against the OpenAPI document the host serves. A route
/// on one side only fails this, which is what stops the contract and the code drifting.
/// </summary>
[Collection("api")]
public sealed partial class DesignCatalogueTests(ApiFactory factory)
{
    /// <summary>
    /// The one route the catalogue carries that design 05's tables do not name. Design 08
    /// requires it ("OpenTelemetry metrics are exposed at /metrics"); design 05 is the
    /// endpoint contract for callers, and a scrape endpoint has none.
    /// </summary>
    private static readonly HashSet<string> NotInDesign05 = new(StringComparer.Ordinal)
    {
        "GET /metrics",
    };

    /// <summary>
    /// The two operations that exist but are not OpenAPI operations: the document itself and
    /// the UI that renders it.
    /// </summary>
    private static readonly HashSet<string> NotOpenApiOperations = new(StringComparer.Ordinal)
    {
        "GET /openapi/v1.json",
        "GET /swagger",
    };

    /// <summary>
    /// A parser that matches nothing passes every other case in this file, so the row count is
    /// asserted first and on its own. Fifty-four is what this parser returns against design 05
    /// as it stands; if a table gains a row, this number changes with it, deliberately.
    /// </summary>
    [Fact]
    public void TheParserFindsEveryRowInDesignFive()
    {
        var design = ParseDesign();

        Assert.Equal(54, design.Count);
        Assert.Contains("GET /api/me", design.Keys);
        Assert.Contains("POST /api/events/{id}/cancel", design.Keys);
        Assert.Contains("POST /api/manage/{token}/cancel", design.Keys);
    }

    [Fact]
    public void TheCatalogueCarriesExactlyDesignFivesRoutesPlusTheScrapeEndpoint()
    {
        var design = ParseDesign().Keys.ToHashSet(StringComparer.Ordinal);
        var catalogue = AgentOperationCatalog.All.Values
            .Select(Key)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Empty(design.Except(catalogue));
        Assert.Equal(NotInDesign05, catalogue.Except(design).ToHashSet(StringComparer.Ordinal));
    }

    [Fact]
    public async Task TheOpenApiDocumentCarriesExactlyTheCataloguedRoutes()
    {
        factory.SignedInAs = null;
        using var document = JsonDocument.Parse(
            await factory.CreateClient().GetStringAsync("/openapi/v1.json"));

        var served = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                served.Add($"{operation.Name.ToUpperInvariant()} {path.Name}");
            }
        }

        var expected = AgentOperationCatalog.All.Values
            .Select(Key)
            .Where(key => !NotOpenApiOperations.Contains(key))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(expected.Order(StringComparer.Ordinal), served.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Design 05's third column against the catalogue's. "Any staff" and "Staff; no capability
    /// needed" both mean a Bearer [REDACTED] and no capability, which is not the same as anonymous
    /// — the catalogue distinguishes them, and so does this.
    /// </summary>
    [Fact]
    public void EveryRouteDemandsTheCapabilityDesignFiveNames()
    {
        var design = ParseDesign();
        var catalogue = AgentOperationCatalog.All.Values.ToDictionary(Key, x => x, StringComparer.Ordinal);

        foreach (var (key, expected) in design)
        {
            var actual = catalogue[key];
            Assert.Equal(expected.RequiresBearer, actual.RequiresBearer);

            // A row naming two capabilities is one the handler filters on instead of
            // demanding — settlement #12 for the Event reads, and Task 20b's own settlement
            // for the audit search. The catalogue declares no single capability for those,
            // and declaring one would be a lie the OpenAPI document then published.
            if (expected.Capabilities.Count == 1)
            {
                Assert.Equal(expected.Capabilities[0], actual.Capability);
            }
            else
            {
                Assert.Null(actual.Capability);
            }
        }
    }

    /// <summary>
    /// Every staff operation has a tool and every anonymous or token route has none (FR-14.1).
    /// Task 23 asserts the tool list equals this set; here the catalogue's own side is pinned.
    /// </summary>
    [Fact]
    public void EveryStaffOperationHasAToolAndNoTokenRouteDoes()
    {
        foreach (var operation in AgentOperationCatalog.All.Values)
        {
            if (operation.RequiresBearer)
            {
                Assert.NotNull(operation.McpTool);
                Assert.Matches("^[a-z][a-z0-9_]*$", operation.McpTool);
            }
            else
            {
                Assert.Null(operation.McpTool);
                Assert.False(string.IsNullOrWhiteSpace(operation.ExclusionReason));
            }
        }

        Assert.Equal(45, AgentOperationCatalog.All.Values.Count(x => x.McpTool is not null));
    }

    [Fact]
    public async Task ProtectedOperationsCarryBearerAndTheToolExtension()
    {
        factory.SignedInAs = null;
        using var document = JsonDocument.Parse(
            await factory.CreateClient().GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");

        foreach (var operation in AgentOperationCatalog.All.Values.Where(x => x.McpTool is not null))
        {
            var served = paths.GetProperty(operation.Route)
                .GetProperty(operation.Method.ToLowerInvariant());
            Assert.Equal(operation.OperationId, served.GetProperty("operationId").GetString());
            Assert.Equal(operation.McpTool, served.GetProperty("x-mcp-tool").GetString());
            Assert.True(served.TryGetProperty("security", out _));
            if (operation.Capability is not null)
            {
                Assert.Equal(
                    operation.Capability, served.GetProperty("x-capability").GetString());
            }
        }
    }

    private static string Key(AgentOperation operation) =>
        $"{operation.Method.ToUpperInvariant()} {operation.Route}";

    private sealed record DesignRoute(bool RequiresBearer, IReadOnlyList<string> Capabilities);

    /// <summary>
    /// Parses every "Method and path" table in design 05. Query strings are dropped — they are
    /// documentation of the filters, not part of the route — and a row naming two paths yields
    /// two entries. A row whose capability column reads "as above" inherits the row before it,
    /// which is how the design writes the single-event read.
    /// </summary>
    private static IReadOnlyDictionary<string, DesignRoute> ParseDesign()
    {
        var path = Path.Combine(RepositoryRoot(), "docs", "design", "05-api-design.md");
        var routes = new Dictionary<string, DesignRoute>(StringComparer.Ordinal);
        var inTable = false;
        DesignRoute? previous = null;

        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.StartsWith("| Method and path", StringComparison.Ordinal))
            {
                inTable = true;
                previous = null;
                continue;
            }

            if (!line.StartsWith('|'))
            {
                inTable = false;
                continue;
            }

            if (!inTable || line.StartsWith("|---", StringComparison.Ordinal) ||
                line.StartsWith("|--", StringComparison.Ordinal))
            {
                continue;
            }

            // Split on unescaped pipes only: one use-case cell contains "{appointmentTypeId \| null}".
            var cells = PipeSplitter().Split(line.Trim('|'))
                .Select(cell => cell.Replace("\\|", "|", StringComparison.Ordinal).Trim())
                .ToList();
            if (cells.Count < 2)
            {
                continue;
            }

            var route = ParseCapability(cells.Count >= 3 ? cells[^1] : "Anonymous", previous);
            previous = route;

            var lastMethod = "GET";
            foreach (Match match in BacktickedPath().Matches(cells[0]))
            {
                var text = match.Groups[1].Value;
                var space = text.IndexOf(' ', StringComparison.Ordinal);
                var method = space > 0 ? text[..space] : lastMethod;
                var target = (space > 0 ? text[(space + 1)..] : text).Split('?')[0].Trim();
                lastMethod = method;
                routes[$"{method} {target}"] = route;
            }
        }

        return routes;
    }

    private static DesignRoute ParseCapability(string cell, DesignRoute? previous)
    {
        if (cell.Equals("as above", StringComparison.OrdinalIgnoreCase) && previous is not null)
        {
            return previous;
        }

        if (cell.Contains("Anonymous", StringComparison.OrdinalIgnoreCase))
        {
            return new DesignRoute(RequiresBearer: false, []);
        }

        var capabilities = BacktickedPath().Matches(cell)
            .Select(match => match.Groups[1].Value)
            .ToList();
        return new DesignRoute(RequiresBearer: true, capabilities);
    }

    /// <summary>Walks up from the test assembly to the directory holding the design package.</summary>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
            !Directory.Exists(Path.Combine(directory.FullName, "docs", "design")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("The design package was not found above the test assembly.");
    }

    [GeneratedRegex(@"(?<!\\)\|")]
    private static partial Regex PipeSplitter();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex BacktickedPath();
}
