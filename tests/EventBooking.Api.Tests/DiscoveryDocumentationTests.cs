namespace EventBooking.Api.Tests;

/// <summary>Prevents the public discovery routes from becoming undocumented.</summary>
public sealed class DiscoveryDocumentationTests
{
    [Fact]
    public void RootReadmeDocumentsRestAndMcpDiscovery()
    {
        var root = FindRepositoryRoot();
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        Assert.Contains("/api", readme, StringComparison.Ordinal);
        Assert.Contains("/openapi/v1.json", readme, StringComparison.Ordinal);
        Assert.Contains("/swagger", readme, StringComparison.Ordinal);
        Assert.Contains("/mcp", readme, StringComparison.Ordinal);
        Assert.Contains("bearer", readme, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HomeLabReadmeDocumentsDeployedSwagger()
    {
        var root = FindRepositoryRoot();
        var readme = File.ReadAllText(Path.Combine(root, "deploy", "home-lab", "README.md"));
        Assert.Contains("/openapi/v1.json", readme, StringComparison.Ordinal);
        Assert.Contains("/swagger", readme, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EventBooking.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
