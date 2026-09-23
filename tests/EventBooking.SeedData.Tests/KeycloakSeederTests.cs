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
