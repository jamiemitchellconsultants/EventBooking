using System.Text.Json;

namespace EventBooking.Api.Tests;

[Collection("api")]
public sealed class VocabularySurfaceTests(ApiFactory factory)
{
    [Fact]
    public async Task OpenApi_paths_and_schemas_use_canonical_vocabulary()
    {
        using var document = JsonDocument.Parse(await factory.CreateClient().GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToArray();
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas")
            .EnumerateObject().Select(p => p.Name);
        string[] retired = ["candi" + "date", "slo" + "t", "employee" + "group", "head" + "office"];
        Assert.DoesNotContain(paths.Concat(schemas), name => retired.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase)));
        Assert.Contains("/api/attendees", paths);
        Assert.Contains("/api/event-proposals", paths);
        Assert.Contains("/api/events/{id}", paths);
    }
}
