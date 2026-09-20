# 00a — Port source 78 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## tests/EventBooking.SeedData.Tests/EmployeeGroupJourneySeedTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.SeedData.Tests/EmployeeGroupJourneySeedTests.cs","encoding":"utf8","sha256":"ab98023c8332545e48d0d242e40e735e08387d25eac5687aa551186ed27d50a4","parts":1,"part":1} -->

`````csharp
using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

/// <summary>Verifies demo data explicitly assigns groups and covers readiness/recovery journeys.</summary>
public sealed class EmployeeGroupJourneySeedTests
{
    /// <summary>Every group and every approved demo journey appears without requirement input.</summary>
    [Fact]
    public void CandidateSpecsUseExplicitGroupsAndCoverJourneys()
    {
        var candidates = DemoSeedSpec.Candidates();

        Assert.Superset(
            new HashSet<string>
            {
                "CABIN_CREW",
                "PILOTS",
                "GROUND_OPERATIONS_AGENT",
                "ENGINEERING",
                "GROUND_TRANSPORT_SERVICES",
            },
            candidates.Select(candidate => candidate.EmployeeGroupCode).ToHashSet());
        Assert.Contains(candidates, candidate => candidate.Journey == DemoCandidateJourney.Ready);
        Assert.Contains(candidates, candidate => candidate.Journey == DemoCandidateJourney.Outstanding);
        Assert.Contains(candidates, candidate => candidate.Journey == DemoCandidateJourney.NoShow);
        Assert.Contains(candidates, candidate => candidate.Journey == DemoCandidateJourney.RecoveryCompleted);
        Assert.DoesNotContain(
            typeof(CandidateSpec).GetProperties(),
            property => property.Name.Contains("Requirement", StringComparison.Ordinal));
    }
}
`````

## tests/EventBooking.SeedData.Tests/EventBooking.SeedData.Tests.csproj — 1/1

<!-- port-file: {"path":"tests/EventBooking.SeedData.Tests/EventBooking.SeedData.Tests.csproj","encoding":"utf8","sha256":"d37155e2d71c274a679d0241cb302d4bbde99186ef7c7022be0ba1331b1563c9","parts":1,"part":1} -->

`````text
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="Testcontainers.PostgreSql" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\EventBooking.SeedData\EventBooking.SeedData.csproj" />
  </ItemGroup>

</Project>
`````

## tests/EventBooking.SeedData.Tests/IdentityProviderDocumentationTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.SeedData.Tests/IdentityProviderDocumentationTests.cs","encoding":"utf8","sha256":"78b4318a0242efc476356a5a60df22a241419d552c89530bb31c84eaa1640d9d","parts":1,"part":1} -->

`````csharp
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
`````

## tests/EventBooking.SeedData.Tests/IdentityProviderRoleBoundaryTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.SeedData.Tests/IdentityProviderRoleBoundaryTests.cs","encoding":"utf8","sha256":"a2a56745ce95360830f091b3d094ca1c0033fa3dee8ab584192588a28325f682","parts":1,"part":1} -->

`````csharp
using System.Text.Json;

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

    /// <summary>Verifies every local demo user has the reviewed role assignment.</summary>
    [Fact]
    public void LocalDemoUsersHaveExpectedRealmRoleAssignments()
    {
        using var seed = JsonDocument.Parse(File.ReadAllText(
            RepoFile("src/EventBooking.SeedData/demo-seed.json")));
        var expected = seed.RootElement.GetProperty("staff").EnumerateArray()
            .ToDictionary(
                row => row.GetProperty("username").GetString()!,
                row => row.GetProperty("roles").EnumerateArray()
                    .Select(role => role.GetString()!).OrderBy(role => role).ToArray());
        using var realm = JsonDocument.Parse(File.ReadAllText(
            RepoFile("deploy/keycloak/realm-export.json")));

        var actual = realm.RootElement.GetProperty("users").EnumerateArray()
            .ToDictionary(
                user => user.GetProperty("username").GetString()!,
                user => user.GetProperty("realmRoles").EnumerateArray()
                    .Select(role => role.GetString()!).OrderBy(role => role).ToArray());

        Assert.Equal(expected.Keys.OrderBy(value => value), actual.Keys.OrderBy(value => value));
        Assert.All(expected, pair => Assert.Equal(pair.Value, actual[pair.Key]));
    }

    /// <summary>Verifies the seed retains a non-empty role array and no legacy singular role.</summary>
    [Fact]
    public void DemoAccessRowsUseRoleArrays()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            RepoFile("src/EventBooking.SeedData/demo-seed.json")));

        Assert.All(document.RootElement.GetProperty("staff").EnumerateArray(), row =>
        {
            Assert.False(row.TryGetProperty("role", out _));
            Assert.Equal(JsonValueKind.Array, row.GetProperty("roles").ValueKind);
            Assert.NotEmpty(row.GetProperty("roles").EnumerateArray());
        });
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
        Assert.Equal("^[UuNn][0-9]{6}$",
            staffId.GetProperty("validations").GetProperty("pattern")
                .GetProperty("pattern").GetString());
    }

    /// <summary>Verifies realm users and executable seed share stable identities and staff numbers.</summary>
    [Fact]
    public void RealmUsersAndDemoSeedAgreeOnIdentityKeys()
    {
        using var realm = JsonDocument.Parse(File.ReadAllText(
            RepoFile("deploy/keycloak/realm-export.json")));
        using var seed = JsonDocument.Parse(File.ReadAllText(
            RepoFile("src/EventBooking.SeedData/demo-seed.json")));
        var realmKeys = realm.RootElement.GetProperty("users").EnumerateArray()
            .ToDictionary(
                user => user.GetProperty("username").GetString()!,
                user => (
                    user.GetProperty("id").GetString()!,
                    user.GetProperty("attributes").GetProperty("staffId")[0].GetString()!));
        var seedKeys = seed.RootElement.GetProperty("staff").EnumerateArray()
            .ToDictionary(
                row => row.GetProperty("username").GetString()!,
                row => (
                    row.GetProperty("userId").GetString()!,
                    row.GetProperty("staffId").GetString()!));

        Assert.Equal(6, realmKeys.Count);
        Assert.Equal(seedKeys, realmKeys);
        Assert.Equal(6, realmKeys.Values.Select(value => value.Item1).Distinct().Count());
        Assert.Equal(6, realmKeys.Values.Select(value => value.Item2).Distinct().Count());
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
`````

## tests/EventBooking.SeedData.Tests/KeycloakSeedContractTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.SeedData.Tests/KeycloakSeedContractTests.cs","encoding":"utf8","sha256":"7abbf8821027903eb381d29737b8306a38bfaabebe21d4451d3ea80d25a81e40","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Access;
using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

/// <summary>Verifies the canonical demo identity and opt-in Keycloak configuration contracts.</summary>
public sealed class KeycloakSeedContractTests
{
    /// <summary>Verifies the database and provider seed share one identity record.</summary>
    [Fact]
    public void Staff_ExposesUsernameProviderIdStaffIdAndRoles()
    {
        var admin = DemoSeedSpec.Staff().Single(value => value.Username == "admin.user");

        Assert.Equal(Guid.Parse("17e8cd60-b849-470f-a7d1-44ff39993688"), admin.UserId);
        Assert.Equal("U000001", admin.StaffId.Value);
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
`````

## tests/EventBooking.SeedData.Tests/KeycloakSeederTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.SeedData.Tests/KeycloakSeederTests.cs","encoding":"utf8","sha256":"c172e37c97690432bffc24db4454dc2966aaefbe6e5fbfc4612ee516c5a0e407","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Text;
using EventBooking.Domain.Access;
using EventBooking.Domain.Common;
using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

/// <summary>Verifies deterministic and fail-closed Keycloak Admin API convergence.</summary>
public sealed class KeycloakSeederTests
{
    private static readonly string[] RoleNames = Enum.GetNames<Role>();

    /// <summary>Verifies an existing realm is deleted, then recreated from the export document.</summary>
    [Fact]
    public async Task ResetRealmAsync_ExistingRealm_DeletesThenRecreates()
    {
        var handler = new ScriptedHandler();
        handler.Add(HttpMethod.Post, "/realms/master/protocol/openid-connect/token", HttpStatusCode.OK,
            "{\"access_token\":\"token\"}");
        handler.Add(HttpMethod.Delete, "/admin/realms/eventbooking", HttpStatusCode.NoContent);
        handler.Add(HttpMethod.Post, "/admin/realms", HttpStatusCode.Created);
        var sut = CreateSut(handler);

        await sut.ResetRealmAsync("{\"realm\":\"eventbooking\"}", CancellationToken.None);

        Assert.Empty(handler.Pending);
        var create = Assert.Single(handler.Requests, request => request.Method == HttpMethod.Post
            && request.Path == "/admin/realms");
        Assert.Equal("{\"realm\":\"eventbooking\"}", create.Body);
    }

    /// <summary>Verifies a realm that does not yet exist is simply created, not treated as failure.</summary>
    [Fact]
    public async Task ResetRealmAsync_RealmAbsent_StillRecreates()
    {
        var handler = new ScriptedHandler();
        handler.Add(HttpMethod.Post, "/realms/master/protocol/openid-connect/token", HttpStatusCode.OK,
            "{\"access_token\":\"token\"}");
        handler.Add(HttpMethod.Delete, "/admin/realms/eventbooking", HttpStatusCode.NotFound);
        handler.Add(HttpMethod.Post, "/admin/realms", HttpStatusCode.Created);
        var sut = CreateSut(handler);

        await sut.ResetRealmAsync("{\"realm\":\"eventbooking\"}", CancellationToken.None);

        Assert.Empty(handler.Pending);
    }

    /// <summary>Verifies a failed recreate surfaces as a SeedException naming the realm path.</summary>
    [Fact]
    public async Task ResetRealmAsync_RecreateRejected_ThrowsSeedException()
    {
        var handler = new ScriptedHandler();
        handler.Add(HttpMethod.Post, "/realms/master/protocol/openid-connect/token", HttpStatusCode.OK,
            "{\"access_token\":\"token\"}");
        handler.Add(HttpMethod.Delete, "/admin/realms/eventbooking", HttpStatusCode.NoContent);
        handler.Add(HttpMethod.Post, "/admin/realms", HttpStatusCode.BadRequest, "malformed");
        var sut = CreateSut(handler);

        var exception = await Assert.ThrowsAsync<SeedException>(() =>
            sut.ResetRealmAsync("{\"realm\":\"eventbooking\"}", CancellationToken.None));

        Assert.Contains("admin/realms", exception.Message);
    }

    /// <summary>Verifies an empty realm receives every provider-side demo object.</summary>
    [Fact]
    public async Task EnsureAsync_MissingState_CreatesRolesMapperUserAndAssignment()
    {
        var handler = new ScriptedHandler();
        handler.Add(HttpMethod.Post, "/realms/master/protocol/openid-connect/token", HttpStatusCode.OK,
            "{\"access_token\":\"token\"}");
        foreach (var role in RoleNames)
        {
            handler.Add(HttpMethod.Get, $"/admin/realms/eventbooking/roles/{role}", HttpStatusCode.NotFound);
            handler.Add(HttpMethod.Post, "/admin/realms/eventbooking/roles", HttpStatusCode.Created);
            handler.Add(HttpMethod.Get, $"/admin/realms/eventbooking/roles/{role}", HttpStatusCode.OK,
                $"{{\"id\":\"role-{role}\",\"name\":\"{role}\"}}");
        }
        handler.Add(HttpMethod.Get,
            "/admin/realms/eventbooking/clients?clientId=eventbooking-web", HttpStatusCode.OK,
            "[{\"id\":\"web-id\",\"clientId\":\"eventbooking-web\"}]");
        handler.Add(HttpMethod.Get,
            "/admin/realms/eventbooking/clients/web-id/protocol-mappers/models", HttpStatusCode.OK,
            "[]");
        handler.Add(HttpMethod.Post,
            "/admin/realms/eventbooking/clients/web-id/protocol-mappers/models", HttpStatusCode.Created);
        handler.Add(HttpMethod.Get,
            "/admin/realms/eventbooking/users?username=admin.user&exact=true&briefRepresentation=false", HttpStatusCode.OK,
            "[]");
        handler.Add(HttpMethod.Post, "/admin/realms/eventbooking/users", HttpStatusCode.Created);
        handler.Add(HttpMethod.Get,
            "/admin/realms/eventbooking/users?username=admin.user&exact=true&briefRepresentation=false", HttpStatusCode.OK,
            ExistingAdmin());
        handler.Add(HttpMethod.Get,
            "/admin/realms/eventbooking/users/17e8cd60-b849-470f-a7d1-44ff39993688/role-mappings/realm",
            HttpStatusCode.OK, "[]");
        handler.Add(HttpMethod.Post,
            "/admin/realms/eventbooking/users/17e8cd60-b849-470f-a7d1-44ff39993688/role-mappings/realm",
            HttpStatusCode.NoContent);
        var sut = CreateSut(handler);

        var summary = await sut.EnsureAsync([Admin()], CancellationToken.None);

        Assert.Equal(new KeycloakSeedSummary(4, 1, 1, 1), summary);
        Assert.Empty(handler.Pending);
        Assert.All(handler.Requests.Skip(1), request =>
            Assert.Equal("Bearer token", request.Authorization));
        Assert.Contains(handler.Requests, request =>
            request.Path == "/admin/realms/eventbooking/users"
            && request.Body.Contains("\"credentials\"", StringComparison.Ordinal)
            && request.Body.Contains("\"staffId\":[\"U000001\"]", StringComparison.Ordinal));
    }

    /// <summary>Verifies equivalent state causes no Admin API writes.</summary>
    [Fact]
    public async Task EnsureAsync_EquivalentState_MakesNoMutatingAdminRequests()
    {
        var handler = ExistingRealm(
            mapper: CorrectMapper(),
            user: ExistingAdmin(),
            mappings: "[{\"id\":\"role-Admin\",\"name\":\"Admin\"}]");
        var sut = CreateSut(handler);

        var summary = await sut.EnsureAsync([Admin()], CancellationToken.None);

        Assert.Equal(new KeycloakSeedSummary(0, 0, 0, 0), summary);
        Assert.Empty(handler.Pending);
        Assert.DoesNotContain(handler.Requests.Skip(1), request =>
            request.Method != HttpMethod.Get);
    }

    /// <summary>Verifies owned drift is corrected while unrelated roles are preserved.</summary>
    [Fact]
    public async Task EnsureAsync_OwnedDrift_ReplacesMapperAndBusinessRoleOnly()
    {
        var handler = ExistingRealm(
            mapper: "[{\"id\":\"mapper-id\",\"name\":\"roles\",\"protocolMapper\":\"wrong\",\"config\":{}}]",
            user: ExistingAdmin(),
            mappings: "[{\"id\":\"role-Manager\",\"name\":\"Manager\"},{\"id\":\"offline\",\"name\":\"offline_access\"}]");
        handler.InsertBefore(
            "/admin/realms/eventbooking/users?username=admin.user&exact=true&briefRepresentation=false",
            HttpMethod.Put,
            "/admin/realms/eventbooking/clients/web-id/protocol-mappers/models/mapper-id",
            HttpStatusCode.NoContent);
        handler.Add(HttpMethod.Post,
            "/admin/realms/eventbooking/users/17e8cd60-b849-470f-a7d1-44ff39993688/role-mappings/realm",
            HttpStatusCode.NoContent);
        handler.Add(HttpMethod.Delete,
            "/admin/realms/eventbooking/users/17e8cd60-b849-470f-a7d1-44ff39993688/role-mappings/realm",
            HttpStatusCode.NoContent);
        var sut = CreateSut(handler);

        var summary = await sut.EnsureAsync([Admin()], CancellationToken.None);

        Assert.Equal(new KeycloakSeedSummary(0, 1, 0, 2), summary);
        var delete = Assert.Single(handler.Requests, request => request.Method == HttpMethod.Delete);
        Assert.Contains("Manager", delete.Body);
        Assert.DoesNotContain("offline_access", delete.Body);
    }

    /// <summary>Verifies a matching username cannot silently adopt another provider identity.</summary>
    [Fact]
    public async Task EnsureAsync_ConflictingProviderId_FailsWithoutChangingTheUser()
    {
        var conflicting =
            "[{\"id\":\"00000000-0000-0000-0000-000000000099\",\"username\":\"admin.user\",\"attributes\":{\"staffId\":[\"U000001\"]}}]";
        var handler = ExistingRealm(CorrectMapper(), conflicting, "[]", includeMappings: false);
        var sut = CreateSut(handler);

        var exception = await Assert.ThrowsAsync<SeedException>(() =>
            sut.EnsureAsync([Admin()], CancellationToken.None));

        Assert.Contains("admin.user", exception.Message);
        Assert.Contains("provider identifier", exception.Message);
        Assert.DoesNotContain(handler.Requests, request =>
            request.Method is not null && request.Method != HttpMethod.Get
            && request.Path.Contains("/users", StringComparison.Ordinal));
    }

    /// <summary>Verifies a matching username cannot silently adopt another staff number.</summary>
    [Fact]
    public async Task EnsureAsync_ConflictingStaffId_FailsWithoutChangingTheUser()
    {
        var conflicting =
            "[{\"id\":\"17e8cd60-b849-470f-a7d1-44ff39993688\",\"username\":\"admin.user\",\"attributes\":{\"staffId\":[\"U999999\"]}}]";
        var handler = ExistingRealm(CorrectMapper(), conflicting, "[]", includeMappings: false);
        var sut = CreateSut(handler);

        var exception = await Assert.ThrowsAsync<SeedException>(() =>
            sut.EnsureAsync([Admin()], CancellationToken.None));

        Assert.Contains("admin.user", exception.Message);
        Assert.Contains("staffId", exception.Message);
        Assert.DoesNotContain(handler.Requests, request =>
            request.Method != HttpMethod.Get
            && request.Path.Contains("/users", StringComparison.Ordinal));
    }

    /// <summary>Verifies duplicate exact usernames fail before either user can be changed.</summary>
    [Fact]
    public async Task EnsureAsync_DuplicateExactUsername_FailsWithoutChangingEitherUser()
    {
        var duplicate =
            "[{\"id\":\"17e8cd60-b849-470f-a7d1-44ff39993688\",\"username\":\"admin.user\",\"attributes\":{\"staffId\":[\"U000001\"]}},"
            + "{\"id\":\"00000000-0000-0000-0000-000000000099\",\"username\":\"admin.user\",\"attributes\":{\"staffId\":[\"U000001\"]}}]";
        var handler = ExistingRealm(CorrectMapper(), duplicate, "[]", includeMappings: false);
        var sut = CreateSut(handler);

        var exception = await Assert.ThrowsAsync<SeedException>(() =>
            sut.EnsureAsync([Admin()], CancellationToken.None));

        Assert.Contains("multiple exact users", exception.Message);
        Assert.DoesNotContain(handler.Requests, request =>
            request.Method != HttpMethod.Get
            && request.Path.Contains("/users", StringComparison.Ordinal));
    }

    /// <summary>Verifies a missing application client fails before any user request.</summary>
    [Fact]
    public async Task EnsureAsync_MissingEventBookingClient_FailsBeforeUserConvergence()
    {
        var handler = new ScriptedHandler();
        handler.Add(HttpMethod.Post, "/realms/master/protocol/openid-connect/token", HttpStatusCode.OK,
            "{\"access_token\":\"token\"}");
        foreach (var role in RoleNames)
        {
            handler.Add(HttpMethod.Get, $"/admin/realms/eventbooking/roles/{role}", HttpStatusCode.OK,
                $"{{\"id\":\"role-{role}\",\"name\":\"{role}\"}}");
        }
        handler.Add(HttpMethod.Get,
            "/admin/realms/eventbooking/clients?clientId=eventbooking-web", HttpStatusCode.OK,
            "[]");
        var sut = CreateSut(handler);

        var exception = await Assert.ThrowsAsync<SeedException>(() =>
            sut.EnsureAsync([Admin()], CancellationToken.None));

        Assert.Contains("eventbooking-web", exception.Message);
        Assert.DoesNotContain(handler.Requests, request =>
            request.Path.Contains("/users", StringComparison.Ordinal));
    }

    private static StaffProfileSpec Admin() => new(
        "admin.user",
        Guid.Parse("17e8cd60-b849-470f-a7d1-44ff39993688"),
        new StaffId("U000001"),
        [Role.Admin],
        null);

    private static KeycloakSeeder CreateSut(HttpMessageHandler handler)
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
        return new KeycloakSeeder(new HttpClient(handler), options);
    }

    private static ScriptedHandler ExistingRealm(
        string mapper,
        string user,
        string mappings,
        bool includeMappings = true)
    {
        var handler = new ScriptedHandler();
        handler.Add(HttpMethod.Post, "/realms/master/protocol/openid-connect/token", HttpStatusCode.OK,
            "{\"access_token\":\"token\"}");
        foreach (var role in RoleNames)
        {
            handler.Add(HttpMethod.Get, $"/admin/realms/eventbooking/roles/{role}", HttpStatusCode.OK,
                $"{{\"id\":\"role-{role}\",\"name\":\"{role}\"}}");
        }
        handler.Add(HttpMethod.Get,
            "/admin/realms/eventbooking/clients?clientId=eventbooking-web", HttpStatusCode.OK,
            "[{\"id\":\"web-id\",\"clientId\":\"eventbooking-web\"}]");
        handler.Add(HttpMethod.Get,
            "/admin/realms/eventbooking/clients/web-id/protocol-mappers/models", HttpStatusCode.OK,
            mapper);
        handler.Add(HttpMethod.Get,
            "/admin/realms/eventbooking/users?username=admin.user&exact=true&briefRepresentation=false", HttpStatusCode.OK,
            user);
        if (includeMappings)
        {
            handler.Add(HttpMethod.Get,
                "/admin/realms/eventbooking/users/17e8cd60-b849-470f-a7d1-44ff39993688/role-mappings/realm",
                HttpStatusCode.OK, mappings);
        }
        return handler;
    }

    private static string ExistingAdmin() =>
        "[{\"id\":\"17e8cd60-b849-470f-a7d1-44ff39993688\",\"username\":\"admin.user\",\"attributes\":{\"staffId\":[\"U000001\"]}}]";

    private static string CorrectMapper() =>
        "[{\"id\":\"mapper-id\",\"name\":\"roles\",\"protocol\":\"openid-connect\",\"protocolMapper\":\"oidc-usermodel-realm-role-mapper\",\"config\":{\"multivalued\":\"true\",\"claim.name\":\"roles\",\"jsonType.label\":\"String\",\"id.token.claim\":\"true\",\"access.token.claim\":\"true\"}}]";

    private sealed record RequestRecord(HttpMethod Method, string Path, string Body, string? Authorization);

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path, HttpStatusCode Status, string Body)> Pending { get; } = [];
        public List<RequestRecord> Requests { get; } = [];

        public void Add(HttpMethod method, string path, HttpStatusCode status, string body = "") =>
            Pending.Add((method, path, status, body));

        public void InsertBefore(
            string followingPath,
            HttpMethod method,
            string path,
            HttpStatusCode status,
            string body = "")
        {
            var index = Pending.FindIndex(item => item.Path == followingPath);
            Assert.True(index >= 0, $"No scripted request has path '{followingPath}'.");
            Pending.Insert(index, (method, path, status, body));
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RequestRecord(
                request.Method,
                request.RequestUri!.PathAndQuery,
                body,
                request.Headers.Authorization?.ToString()));

            Assert.NotEmpty(Pending);
            var next = Pending[0];
            Pending.RemoveAt(0);
            Assert.Equal(next.Method, request.Method);
            Assert.Equal(next.Path, request.RequestUri.PathAndQuery);

            return new HttpResponseMessage(next.Status)
            {
                Content = new StringContent(next.Body, Encoding.UTF8, "application/json"),
            };
        }
    }
}
`````

## tests/EventBooking.SeedData.Tests/KeycloakSeedStepTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.SeedData.Tests/KeycloakSeedStepTests.cs","encoding":"utf8","sha256":"c23021cfec74cc9cf881ff6bf877c9da5908cca298289be2b05cf0d7f63691de","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Text;
using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

/// <summary>Verifies disabled seed modes cannot parse Keycloak settings or create HTTP transport,
/// and that a --reseed run wires the realm delete-and-recreate step correctly.</summary>
public sealed class KeycloakSeedStepTests
{
    private static readonly Dictionary<string, string?> CompleteSettings = new()
    {
        ["Keycloak__BaseUrl"] = "https://auth.example.test/",
        ["Keycloak__AdminUsername"] = "seed-admin",
        ["Keycloak__AdminPassword"] = "admin-secret",
        ["Keycloak__DemoPassword"] = "demo-secret",
    };

    /// <summary>Verifies absent configuration performs no provider work.</summary>
    [Fact]
    public async Task AbsentConfigurationDoesNotCreateAnHttpClient()
    {
        var clientsCreated = 0;
        var step = new KeycloakSeedStep(
            _ => null,
            () =>
            {
                clientsCreated++;
                return new HttpClient();
            });

        var result = await step.RunAsync(
            skipSeed: false, reseed: false, DemoSeedSpec.Staff(), CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, clientsCreated);
    }

    /// <summary>Verifies --skip-seed bypasses even a partial, otherwise-invalid configuration.</summary>
    [Fact]
    public async Task SkipSeedDoesNotParseSettingsOrCreateAnHttpClient()
    {
        var settingsRead = 0;
        var clientsCreated = 0;
        var step = new KeycloakSeedStep(
            _ =>
            {
                settingsRead++;
                return "partial";
            },
            () =>
            {
                clientsCreated++;
                return new HttpClient();
            });

        var result = await step.RunAsync(
            skipSeed: true, reseed: false, DemoSeedSpec.Staff(), CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, settingsRead);
        Assert.Equal(0, clientsCreated);
    }

    /// <summary>Verifies a reseed without a configured realm export path fails before any HTTP
    /// client or file is touched, naming the missing setting.</summary>
    [Fact]
    public async Task ReseedWithoutRealmExportPathThrowsWithoutCreatingAnHttpClientOrReadingAFile()
    {
        var clientsCreated = 0;
        var filesRead = 0;
        var step = new KeycloakSeedStep(
            name => CompleteSettings.GetValueOrDefault(name),
            () =>
            {
                clientsCreated++;
                return new HttpClient();
            },
            _ =>
            {
                filesRead++;
                return "{}";
            });

        var exception = await Assert.ThrowsAsync<SeedException>(() =>
            step.RunAsync(skipSeed: false, reseed: true, DemoSeedSpec.Staff(), CancellationToken.None));

        Assert.Contains("Keycloak__RealmExportPath", exception.Message);
        Assert.Equal(0, clientsCreated);
        Assert.Equal(0, filesRead);
    }

    /// <summary>Verifies a plain (non-reseed) run never deletes or recreates the realm, even when a
    /// realm export path is configured.</summary>
    [Fact]
    public async Task PlainRunNeverCallsRealmDeleteOrCreate()
    {
        var settings = new Dictionary<string, string?>(CompleteSettings)
        {
            ["Keycloak__RealmExportPath"] = "/config/eventbooking-realm.json",
        };
        var handler = new RecordingHandler();
        handler.Enqueue(HttpStatusCode.OK, "{\"access_token\":\"token\"}");
        handler.Enqueue(HttpStatusCode.NotFound); // client lookup fails fast in this minimal script
        var step = new KeycloakSeedStep(
            name => settings.GetValueOrDefault(name),
            () => new HttpClient(handler),
            _ => throw new InvalidOperationException("A plain run must not read the realm export file."));

        await Assert.ThrowsAsync<SeedException>(() =>
            step.RunAsync(skipSeed: false, reseed: false, DemoSeedSpec.Staff(), CancellationToken.None));

        Assert.DoesNotContain(handler.Requests, request =>
            request.Method == HttpMethod.Delete || request.Path == "/admin/realms");
    }

    private sealed record RequestRecord(HttpMethod Method, string Path);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> responses = new();
        public List<RequestRecord> Requests { get; } = [];

        public void Enqueue(HttpStatusCode status, string body = "") => responses.Enqueue((status, body));

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new RequestRecord(request.Method, request.RequestUri!.PathAndQuery));
            var (status, body) = responses.Count > 0
                ? responses.Dequeue()
                : (HttpStatusCode.NotFound, string.Empty);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
`````

## tests/EventBooking.SeedData.Tests/LoopbackSmtpReceiver.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.SeedData.Tests/LoopbackSmtpReceiver.cs","encoding":"utf8","sha256":"1325fe95dc4e93161d34eb7e73395e6dce62a410b50e28acc1fa63f7e75350d0","parts":1,"part":1} -->

`````csharp
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using MimeKit;

namespace EventBooking.SeedData.Tests;

/// <summary>Captures messages from the actual SMTP client in disposable host tests.</summary>
internal sealed class LoopbackSmtpReceiver : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _worker;

    /// <summary>Starts a test receiver on an unused loopback port.</summary>
    public LoopbackSmtpReceiver()
    {
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _worker = ReceiveAsync(_stop.Token);
    }

    /// <summary>Gets the port to inject into the seed subprocess.</summary>
    public int Port { get; }
    /// <summary>Gets accepted MIME messages after the SMTP DATA phase.</summary>
    public ConcurrentQueue<MimeMessage> Messages { get; } = new();
    /// <summary>Gets or sets whether the provider rejects DATA before accepting a message.</summary>
    public bool RejectMessages { get; set; }

    private async Task ReceiveAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
                await using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true)
                {
                    NewLine = "\r\n",
                    AutoFlush = true,
                };
                await writer.WriteLineAsync("220 localhost test SMTP ready");
                while (await reader.ReadLineAsync(cancellationToken) is { } command)
                {
                    if (command.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("221 Bye");
                        break;
                    }
                    if (command.StartsWith("DATA", StringComparison.OrdinalIgnoreCase))
                    {
                        if (RejectMessages)
                        {
                            await writer.WriteLineAsync("550 Test provider rejection");
                            continue;
                        }
                        await writer.WriteLineAsync("354 End with a single dot");
                        var data = new StringBuilder();
                        while (await reader.ReadLineAsync(cancellationToken) is { } line && line != ".")
                            data.Append(line.StartsWith("..", StringComparison.Ordinal) ? line[1..] : line)
                                .Append("\r\n");
                        using var bytes = new MemoryStream(Encoding.UTF8.GetBytes(data.ToString()));
                        Messages.Enqueue(await MimeMessage.LoadAsync(bytes, cancellationToken));
                        await writer.WriteLineAsync("250 Message accepted");
                    }
                    else
                    {
                        await writer.WriteLineAsync("250 localhost");
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    /// <summary>Stops the receiver and observes any unexpected protocol failure.</summary>
    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        try { await _worker; }
        finally
        {
            _listener.Stop();
            _stop.Dispose();
            foreach (var message in Messages) message.Dispose();
        }
    }
}
`````

## tests/EventBooking.SeedData.Tests/ReanchorTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.SeedData.Tests/ReanchorTests.cs","encoding":"utf8","sha256":"3daa0071379ae76b370023d4dd69dba45a2187d082313332f051234e00c5ec50","parts":1,"part":1} -->

`````csharp
using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Audit;
using EventBooking.Infrastructure.Persistence;
using EventBooking.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace EventBooking.SeedData.Tests;

/// <summary>
/// Verifies that a stale file anchor fails fast and that --reanchor (the run override)
/// restores a green seed without editing demo-seed.json.
/// </summary>
[Collection("seed-anchor")]
public sealed class ReanchorTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database =
        new PostgreSqlBuilder("postgres:16-alpine").Build();

    private ServiceProvider _services = null!;

    private DateOnly _today;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        // Simulate "today" moving five days past the file anchor: the +2-day proposal
        // then lands in the past, which the domain rejects.
        _today = DemoSeedSpec.AnchorDate().AddDays(5);

        var services = new ServiceCollection();
        services.AddEventBookingPersistence(_database.GetConnectionString());
        services.AddEventBookingApplication(
            new CandidatePortalOptions(
                "http://localhost:5002", "1 Example Street, London", "recruitment@example.com"));
        services.AddSingleton<IClock>(new FixedClock(_today));
        services.AddScoped<IAuditLogger, EfAuditLogger>();
        services.AddScoped<DemoSeeder>();
        _services = services.BuildServiceProvider();

        using var scope = _services.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<EventBookingDbContext>()
            .Database.MigrateAsync();
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        await _database.DisposeAsync();
    }

    /// <summary>Documents the stale-anchor failure the override exists to fix.</summary>
    [Fact]
    public async Task StaleAnchorFailsWithoutReanchor()
    {
        using var scope = _services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DemoSeeder>();

        var error = await Assert.ThrowsAsync<SeedException>(
            () => seeder.RunAsync(CancellationToken.None));

        Assert.Contains("must be proposed for a future date", error.Message);
    }

    /// <summary>Overriding the anchor to today restores the full deterministic seed.</summary>
    [Fact]
    public async Task ReanchorToTodayRestoresFullSeed()
    {
        DemoSeedSpec.OverrideAnchor(_today);
        try
        {
            using var scope = _services.CreateScope();
            var seeder = scope.ServiceProvider.GetRequiredService<DemoSeeder>();
            var database = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();

            var summary = await seeder.RunAsync(CancellationToken.None);

            Assert.Equal(6, summary.IdentitiesEnsured);
            Assert.Equal(6, summary.ProfilesEnsured);
            Assert.Equal(3, summary.AgreedSlotsImported);
            Assert.Equal(5, summary.ProposalsEnsured);
            Assert.Equal(100, summary.CandidatesCreated);
            Assert.Equal(100, await database.Candidates.CountAsync());
        }
        finally
        {
            DemoSeedSpec.OverrideAnchor(null);
        }
    }

    private sealed class FixedClock(DateOnly today) : IClock
    {
        private static readonly TimeZoneInfo HeadOfficeTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

        /// <inheritdoc/>
        public DateTimeOffset UtcNow { get; } =
            new(today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);

        /// <inheritdoc/>
        public DateTimeOffset NowAtHeadOffice => TimeZoneInfo.ConvertTime(UtcNow, HeadOfficeTimeZone);

        /// <inheritdoc/>
        public DateOnly TodayAtHeadOffice => DateAtHeadOffice(UtcNow);

        /// <inheritdoc/>
        public DateOnly DateAtHeadOffice(DateTimeOffset instant) =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, HeadOfficeTimeZone).DateTime);

        public DateTimeOffset InstantAtHeadOffice(DateTimeOffset instant) =>
            TimeZoneInfo.ConvertTime(instant, HeadOfficeTimeZone);
    }
}
`````

## tests/EventBooking.SeedData.Tests/ReseedTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.SeedData.Tests/ReseedTests.cs","encoding":"utf8","sha256":"414e6944b04a1455f633d08f98700e6fb0e28ca9285cba381b54fc6133f71382","parts":1,"part":1} -->

`````csharp
using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Bookings;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Audit;
using EventBooking.Infrastructure.Persistence;
using EventBooking.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace EventBooking.SeedData.Tests;

/// <summary>
/// Verifies that reseeding restores the complete deterministic demo dataset after domain data has
/// been mutated.
/// </summary>
[Collection("seed-anchor")]
public sealed class ReseedTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database =
        new PostgreSqlBuilder("postgres:16-alpine").Build();

    private ServiceProvider _services = null!;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        var services = new ServiceCollection();
        services.AddEventBookingPersistence(_database.GetConnectionString());
        services.AddEventBookingApplication(
            new CandidatePortalOptions(
                "http://localhost:5002", "1 Example Street, London", "recruitment@example.com"));
        services.AddSingleton<IClock>(new FixedClock(DemoSeedSpec.AnchorDate()));
        services.AddScoped<IAuditLogger, EfAuditLogger>();
        services.AddScoped<DemoSeeder>();
        _services = services.BuildServiceProvider();

        using var scope = _services.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<EventBookingDbContext>()
            .Database.MigrateAsync();
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        await _database.DisposeAsync();
    }

    /// <summary>
    /// Verifies that reseeding removes mutations and restores every seeded aggregate and reference
    /// record exactly once.
    /// </summary>
    [Fact]
    public async Task Reseed_RestoresExactSeedStateAfterMutations()
    {
        using var scope = _services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DemoSeeder>();
        var database = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();

        await seeder.RunAsync(CancellationToken.None);

        database.Candidates.RemoveRange(database.Candidates);
        database.StaffAccessProfiles.Remove(await database.StaffAccessProfiles.FirstAsync());
        await database.SaveChangesAsync();

        var summary = await seeder.ReseedAsync(CancellationToken.None);

        Assert.Equal(6, summary.IdentitiesEnsured);
        Assert.Equal(6, summary.ProfilesEnsured);
        Assert.Equal(3, summary.AgreedSlotsImported);
        Assert.Equal(5, summary.ProposalsEnsured);
        Assert.Equal(100, summary.CandidatesCreated);
        Assert.Equal(6, await database.StaffAccessProfiles.CountAsync());
        var expectedProfiles = DemoSeedSpec.Staff().ToDictionary(value => value.UserId);
        var actualProfiles = await database.StaffAccessProfiles
            .AsNoTracking()
            .ToDictionaryAsync(value => value.StaffUserId);

        Assert.Equal(expectedProfiles.Keys.OrderBy(value => value),
            actualProfiles.Keys.OrderBy(value => value));
        Assert.All(expectedProfiles, pair =>
        {
            var actual = actualProfiles[pair.Key];
            Assert.Equal(
                pair.Value.Roles.OrderBy(value => value),
                actual.Roles.OrderBy(value => value));
            Assert.Equal(pair.Value.AppointmentTypeId, actual.AppointmentTypeId);
        });
        Assert.Equal(6, await database.StaffIdentities.CountAsync());
        Assert.Equal(9, await database.ConfirmedSlots.CountAsync());
        Assert.Equal(5, await database.SlotProposals.CountAsync());
        Assert.Equal(100, await database.Candidates.CountAsync());
        Assert.Equal(3, await database.AppointmentTypes.CountAsync());
        Assert.Equal(1, await database.SystemSettings.CountAsync());
        Assert.Equal(5, await database.EmployeeGroups.CountAsync());
        Assert.Equal(
            10,
            await database.EmployeeGroups.SelectMany(group => group.Requirements).CountAsync());
        Assert.Equal(
            200,
            await database.Candidates.SelectMany(candidate => candidate.Requirements).CountAsync());
        Assert.Equal(90, await database.Invites.CountAsync());
        Assert.Equal(90, await database.Bookings.CountAsync());
        Assert.Equal(170, await database.BookingAppointments.CountAsync());
        Assert.Equal(
            170,
            await database.Invites.SelectMany(invite => invite.Requirements).CountAsync());
        Assert.Equal(
            25,
            await database.BookingAppointments.CountAsync(
                appointment => appointment.Status == BookingAppointmentStatus.NoShow));
        Assert.Equal(
            70,
            await database.BookingAppointments.CountAsync(
                appointment => appointment.Status == BookingAppointmentStatus.Completed));
        Assert.True(
            await database.BookingAppointments.AnyAsync(
                appointment => appointment.Status == BookingAppointmentStatus.CheckedIn));
    }

    private sealed class FixedClock(DateOnly today) : IClock
    {
        private static readonly TimeZoneInfo HeadOfficeTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

        /// <inheritdoc/>
        public DateTimeOffset UtcNow { get; } =
            new(today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);

        /// <inheritdoc/>
        public DateTimeOffset NowAtHeadOffice => TimeZoneInfo.ConvertTime(UtcNow, HeadOfficeTimeZone);

        /// <inheritdoc/>
        public DateOnly TodayAtHeadOffice => DateAtHeadOffice(UtcNow);

        /// <inheritdoc/>
        public DateOnly DateAtHeadOffice(DateTimeOffset instant) =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, HeadOfficeTimeZone).DateTime);

        public DateTimeOffset InstantAtHeadOffice(DateTimeOffset instant) =>
            TimeZoneInfo.ConvertTime(instant, HeadOfficeTimeZone);
    }
}
`````
