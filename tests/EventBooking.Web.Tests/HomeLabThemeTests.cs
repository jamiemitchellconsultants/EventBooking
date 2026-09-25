using System.Text.RegularExpressions;

namespace EventBooking.Web.Tests;

public sealed partial class HomeLabThemeTests
{
    [Fact]
    public void HomeLabThemeDefinesEveryTokenTheApplicationThemeDefines()
    {
        var appTokens = Tokens(RepoFile("src/EventBooking.Web/wwwroot/theme.css"));
        var homeLabTokens = Tokens(RepoFile("deploy/home-lab/theme.css"));

        Assert.NotEmpty(appTokens);
        var missing = appTokens.Except(homeLabTokens).Order().ToList();
        Assert.True(
            missing.Count == 0,
            "deploy/home-lab/theme.css replaces the application theme when mounted, so it must define "
            + $"every token. Missing: {string.Join(", ", missing)}");
    }

    private static HashSet<string> Tokens(string path) =>
        TokenDeclaration().Matches(File.ReadAllText(path))
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    [GeneratedRegex(@"(--[a-z0-9-]+)\s*:")]
    private static partial Regex TokenDeclaration();

    private static string RepoFile(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "EventBooking.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, relativePath);
    }
}
