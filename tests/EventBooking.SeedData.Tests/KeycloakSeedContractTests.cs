using EventBooking.Domain.Access;
using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

/// <summary>Verifies the canonical demo identity and opt-in Keycloak configuration contracts.</summary>
[Collection("seed-anchor")]
public sealed class KeycloakSeedContractTests
{
    /// <summary>Verifies the database and provider seed share one identity record.</summary>
    [Fact]
    public void Staff_ExposesUsernameProviderIdStaffIdAndRoles()
    {
        var admin = DemoSeedSpec.Staff().Single(value => value.Username == "admin");

        Assert.Equal(Guid.Parse("40000000-0000-0000-0000-000000000001"), admin.UserId);
        Assert.Equal("DEMO001", admin.StaffId.Value);
        Assert.Equal([Role.Admin], admin.Roles);
    }

    /// <summary>Verifies an Entra-backed seed run does not require or contact Keycloak.</summary>
    [Fact]
    public void Options_AllSettingsAbsent_DisablesKeycloakSeed()
    {
        var options = KeycloakSeedOptions.From(_ => null);

        Assert.Null(options);
    }

    /// <summary>Verifies a complete configuration binds defaults without exposing secret values.</summary>
    [Fact]
    public void Options_CompleteSettings_BindsDefaults()
    {
        var values = new Dictionary<string, string?>
        {
            ["Keycloak__BaseUrl"] = "https://auth.example.test/",
            ["Keycloak__AdminUsername"] = "seed-admin",
            ["Keycloak__AdminPassword"] = "admin-secret",
            ["Keycloak__DemoPassword"] = "demo-secret",
        };

        var options = Assert.IsType<KeycloakSeedOptions>(
            KeycloakSeedOptions.From(name => values.GetValueOrDefault(name)));

        Assert.Equal(new Uri("https://auth.example.test/"), options.BaseUrl);
        Assert.Equal("eventbooking", options.Realm);
        Assert.Equal("master", options.AdminRealm);
        Assert.Equal("seed-admin", options.AdminUsername);
        Assert.Equal("admin-secret", options.AdminPassword);
        Assert.Equal("demo-secret", options.DemoPassword);
        Assert.Null(options.RealmExportPath);
        Assert.DoesNotContain("admin-secret", options.ToString());
        Assert.DoesNotContain("demo-secret", options.ToString());
    }

    /// <summary>Verifies the optional realm export path is bound when present.</summary>
    [Fact]
    public void Options_RealmExportPathSet_IsBound()
    {
        var values = new Dictionary<string, string?>
        {
            ["Keycloak__BaseUrl"] = "https://auth.example.test/",
            ["Keycloak__AdminUsername"] = "seed-admin",
            ["Keycloak__AdminPassword"] = "admin-secret",
            ["Keycloak__DemoPassword"] = "demo-secret",
            ["Keycloak__RealmExportPath"] = "/config/eventbooking-realm.json",
        };

        var options = Assert.IsType<KeycloakSeedOptions>(
            KeycloakSeedOptions.From(name => values.GetValueOrDefault(name)));

        Assert.Equal("/config/eventbooking-realm.json", options.RealmExportPath);
    }

    /// <summary>Verifies partial opt-in configuration fails with the missing key, not a secret.</summary>
    [Fact]
    public void Options_PartialSettings_NamesMissingSetting()
    {
        var values = new Dictionary<string, string?>
        {
            ["Keycloak__BaseUrl"] = "https://auth.example.test/",
            ["Keycloak__AdminUsername"] = "seed-admin",
            ["Keycloak__AdminPassword"] = "admin-secret",
        };

        var exception = Assert.Throws<SeedException>(() =>
            KeycloakSeedOptions.From(name => values.GetValueOrDefault(name)));

        Assert.Contains("Keycloak__DemoPassword", exception.Message);
        Assert.DoesNotContain("admin-secret", exception.Message);
    }
}
