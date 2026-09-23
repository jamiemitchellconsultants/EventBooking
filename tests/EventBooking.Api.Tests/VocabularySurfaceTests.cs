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

    [Fact]
    public void Source_display_text_uses_canonical_vocabulary()
    {
        string[] retiredDisplay = ["employee " + "group", "head-" + "office", "head " + "office"];
        var hits = Directory.EnumerateFiles(RepoRoot(), "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal)
                || path.EndsWith(".razor", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => (path, index, line))
                .Where(item => retiredDisplay.Any(term =>
                    item.line.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .Select(item => $"{Path.GetRelativePath(RepoRoot(), item.path)}:{item.index + 1}: {item.line.Trim()}"))
            .ToList();

        Assert.True(hits.Count == 0, $"Retired display text found:{Environment.NewLine}{string.Join(Environment.NewLine, hits)}");
    }

    private static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null
                && !File.Exists(Path.Combine(directory.FullName, "EventBooking.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, "src");
    }
}
