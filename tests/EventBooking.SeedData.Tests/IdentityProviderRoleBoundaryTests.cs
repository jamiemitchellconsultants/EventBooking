using System.Text.Json;
using System.Text.RegularExpressions;

namespace EventBooking.SeedData.Tests;

/// <summary>Guards the reviewed Keycloak bootstrap files and executable seed from drift.</summary>
public sealed class IdentityProviderRoleBoundaryTests
{
    private static readonly string[] ExpectedRoles =
        ["Admin", "Coordinator", "Manager", "AppointmentStaff"];

    /// <summary>Verifies both realm files declare exactly the EventBooking business roles.</summary>
    [Theory]
    [InlineData("deploy/keycloak/realm-export.json")]
    [InlineData("deploy/home-lab/keycloak/eventbooking-realm.json")]
    public void RealmExportsDeclareEventBookingBusinessRoles(string relativePath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(RepoFile(relativePath)));
        var names = document.RootElement.GetProperty("roles").GetProperty("realm")
            .EnumerateArray()
            .Select(role => role.GetProperty("name").GetString())
            .ToList();

        Assert.Equal(ExpectedRoles.OrderBy(value => value), names.OrderBy(value => value));
    }

    /// <summary>Verifies every demo identity carries a non-empty business-role set.</summary>
    [Fact]
    public void DemoAccessRowsUseRoleArrays()
    {
        var staff = DemoSeedSpec.Staff();

        Assert.Equal(8, staff.Count);
        Assert.All(staff, person => Assert.NotEmpty(person.Roles));
    }

    /// <summary>Verifies both clients emit the same flat multi-valued `roles` claim.</summary>
    [Theory]
    [InlineData("deploy/keycloak/realm-export.json")]
    [InlineData("deploy/home-lab/keycloak/eventbooking-realm.json")]
    public void RealmExportsIssueTheRolesClaim(string relativePath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(RepoFile(relativePath)));
        var mapper = Client(document).GetProperty("protocolMappers").EnumerateArray()
            .Single(value => value.GetProperty("name").GetString() == "roles");
        var config = mapper.GetProperty("config");

        Assert.Equal("oidc-usermodel-realm-role-mapper",
            mapper.GetProperty("protocolMapper").GetString());
        Assert.Equal("roles", config.GetProperty("claim.name").GetString());
        Assert.Equal("true", config.GetProperty("multivalued").GetString());
        Assert.Equal("true", config.GetProperty("id.token.claim").GetString());
        Assert.Equal("true", config.GetProperty("access.token.claim").GetString());
    }

    /// <summary>Verifies the existing provider-specific staff-number mapper remains intact.</summary>
    [Theory]
    [InlineData("deploy/keycloak/realm-export.json")]
    [InlineData("deploy/home-lab/keycloak/eventbooking-realm.json")]
    public void RealmExportsIssueTheStaffIdClaim(string relativePath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(RepoFile(relativePath)));
        var mapper = Client(document).GetProperty("protocolMappers").EnumerateArray()
            .Single(value => value.GetProperty("name").GetString() == "staff_id");
        var config = mapper.GetProperty("config");

        Assert.Equal("oidc-usermodel-attribute-mapper",
            mapper.GetProperty("protocolMapper").GetString());
        Assert.Equal("staffId", config.GetProperty("user.attribute").GetString());
        Assert.Equal("staff_id", config.GetProperty("claim.name").GetString());
        Assert.Equal("true", config.GetProperty("id.token.claim").GetString());
        Assert.Equal("true", config.GetProperty("access.token.claim").GetString());
    }

    /// <summary>Verifies realm user profiles still require a validated staff number.</summary>
    [Theory]
    [InlineData("deploy/keycloak/realm-export.json")]
    [InlineData("deploy/home-lab/keycloak/eventbooking-realm.json")]
    public void RealmExportsRequireAValidatedStaffIdProfileAttribute(string relativePath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(RepoFile(relativePath)));
        var provider = document.RootElement.GetProperty("components")
            .GetProperty("org.keycloak.userprofile.UserProfileProvider")
            .EnumerateArray()
            .Single(value => value.GetProperty("providerId").GetString()
                == "declarative-user-profile");
        var encoded = provider.GetProperty("config").GetProperty("config-piece-0")[0].GetString();
        using var profile = JsonDocument.Parse(encoded!);
        var staffId = profile.RootElement.GetProperty("attributes").EnumerateArray()
            .Single(value => value.GetProperty("name").GetString() == "staffId");

        Assert.Contains("user", staffId.GetProperty("required").GetProperty("roles")
            .EnumerateArray().Select(value => value.GetString()));
        Assert.Contains("admin", staffId.GetProperty("required").GetProperty("roles")
            .EnumerateArray().Select(value => value.GetString()));
        var pattern = staffId.GetProperty("validations").GetProperty("pattern")
            .GetProperty("pattern").GetString();
        Assert.Equal("^[A-Za-z0-9]{1,32}$", pattern);

        var expression = new Regex(
            pattern!, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        Assert.All(DemoSeedSpec.Staff(), person =>
        {
            var value = person.StaffId.Value;
            var match = expression.Match(value);
            Assert.True(match.Success && match.Index == 0 && match.Length == value.Length,
                $"Seed staffId '{value}' must satisfy the realm profile pattern.");
        });
    }

    private static JsonElement Client(JsonDocument document) =>
        document.RootElement.GetProperty("clients").EnumerateArray()
            .Single(value => value.GetProperty("clientId").GetString() == "eventbooking-web");

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

    /// <summary>Verifies both realms emit the standard name claim the identity mirror reads.</summary>
    [Theory]
    [InlineData("deploy/keycloak/realm-export.json")]
    [InlineData("deploy/home-lab/keycloak/eventbooking-realm.json")]
    public void RealmDeclaresNameMapperOnWebClient(string relativePath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(RepoFile(relativePath)));
        var client = document.RootElement.GetProperty("clients").EnumerateArray()
            .Single(entry => entry.GetProperty("clientId").GetString() == "eventbooking-web");
        var mapper = client.GetProperty("protocolMappers").EnumerateArray()
            .Single(entry => entry.GetProperty("name").GetString() == "name");

        Assert.Equal("openid-connect", mapper.GetProperty("protocol").GetString());
        Assert.Equal("oidc-full-name-mapper", mapper.GetProperty("protocolMapper").GetString());
        var config = mapper.GetProperty("config");
        Assert.Equal("true", config.GetProperty("id.token.claim").GetString());
        Assert.Equal("true", config.GetProperty("access.token.claim").GetString());
        Assert.Equal("true", config.GetProperty("userinfo.token.claim").GetString());
    }

    /// <summary>Verifies every demo user composes a name, so the mapper has something to emit.</summary>
    [Fact]
    public void DemoUsersAllCarryFirstAndLastName()
    {
        using var realm = JsonDocument.Parse(File.ReadAllText(
            RepoFile("deploy/keycloak/realm-export.json")));

        var users = realm.RootElement.GetProperty("users").EnumerateArray().ToList();

        Assert.NotEmpty(users);
        Assert.All(users, user =>
        {
            Assert.False(string.IsNullOrWhiteSpace(user.GetProperty("firstName").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(user.GetProperty("lastName").GetString()));
        });
    }
}
