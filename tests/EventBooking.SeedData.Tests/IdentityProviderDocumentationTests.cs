namespace EventBooking.SeedData.Tests;

/// <summary>Guards operator documentation for the confirmed identity-provider ownership model.</summary>
public sealed class IdentityProviderDocumentationTests
{
    /// <summary>Verifies deployment docs name every Keycloak seed setting without revealing values.</summary>
    [Fact]
    public void HomeLabReadme_DocumentsSeedDataOwnedKeycloakProvisioning()
    {
        var text = File.ReadAllText(RepoFile("deploy/home-lab/README.md"));

        Assert.Contains("EventBooking.SeedData", text);
        Assert.Contains("Keycloak__BaseUrl", text);
        Assert.Contains("Keycloak__AdminUsername", text);
        Assert.Contains("Keycloak__AdminPassword", text);
        Assert.Contains("Keycloak__DemoPassword", text);
        Assert.Contains("idempotent", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("This deployment seeds no user accounts of its own", text);
    }

    /// <summary>Verifies the demo runbook describes one provider/database seed source.</summary>
    [Fact]
    public void DemoRunbook_DocumentsKeycloakAsRoleSource()
    {
        var text = File.ReadAllText(RepoFile("docs/demo-runbook.md"));

        Assert.Contains("demo-seed.json", text);
        Assert.Contains("Keycloak", text);
        Assert.Contains("roles claim", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pre-mirrors", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Keycloak supplies identity only", text);
    }

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
