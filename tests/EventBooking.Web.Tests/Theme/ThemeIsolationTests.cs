using System.Text.RegularExpressions;

namespace EventBooking.Web.Tests.Theme;

public sealed partial class ThemeIsolationTests
{
    [Fact]
    public void ColourLiteralsExistOnlyInThemeCss()
    {
        var root = RepositoryRoot();
        var obj = $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}";
        var bin = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}";
        var files = Directory.EnumerateFiles(Path.Combine(root, "src", "EventBooking.Web"), "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".razor", StringComparison.Ordinal)
                || path.EndsWith(".css", StringComparison.Ordinal))
            .Where(path => !path.EndsWith(Path.Combine("wwwroot", "theme.css"), StringComparison.Ordinal))
            .Where(path => !path.Contains(obj, StringComparison.Ordinal)
                && !path.Contains(bin, StringComparison.Ordinal));

        var offenders = files
            .SelectMany(path => ColourLiteral().Matches(File.ReadAllText(path))
                .Select(match => $"{Path.GetRelativePath(root, path)}: {match.Value}"))
            .ToArray();

        Assert.Empty(offenders);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EventBooking.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    [GeneratedRegex(@"#[0-9a-fA-F]{3,8}\b|\brgba?\s*\(|\bhsla?\s*\(")]
    private static partial Regex ColourLiteral();
}
