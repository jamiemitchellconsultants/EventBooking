using System.Text.RegularExpressions;

namespace EventBooking.Web.Tests;

public sealed partial class WebStylesheetTests
{
    [Fact]
    public void EveryCustomPropertyUsedByAStylesheetIsDefinedByOne()
    {
        var files = StylesheetFiles();
        var defined = files
            .SelectMany(file => Declaration().Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);
        var undefined = files
            .SelectMany(file => Usage().Matches(File.ReadAllText(file))
                .Select(m => (Property: m.Groups[1].Value, File: Path.GetFileName(file))))
            .Where(use => !defined.Contains(use.Property))
            .Select(use => $"{use.Property} ({use.File})")
            .Distinct()
            .Order()
            .ToList();

        Assert.True(
            undefined.Count == 0,
            "A var() whose property is defined nowhere silently drops its whole declaration. Undefined: "
            + string.Join(", ", undefined));
    }

    private static List<string> StylesheetFiles()
    {
        var root = RepoRoot();
        var web = Path.Combine(root, "src", "EventBooking.Web");
        var separator = Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(web, "*.css", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{separator}bin{separator}", StringComparison.Ordinal)
                           && !file.Contains($"{separator}obj{separator}", StringComparison.Ordinal))
            .ToList();
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
        return directory!.FullName;
    }

    [GeneratedRegex(@"(--[a-zA-Z0-9-]+)\s*:")]
    private static partial Regex Declaration();

    [GeneratedRegex(@"var\(\s*(--[a-zA-Z0-9-]+)")]
    private static partial Regex Usage();
}
