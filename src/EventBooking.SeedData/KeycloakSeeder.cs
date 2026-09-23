using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EventBooking.Domain.Access;

namespace EventBooking.SeedData;

/// <summary>Counts Keycloak objects changed by one idempotent convergence.</summary>
/// <param name="RolesCreated">The missing EventBooking realm roles created.</param>
/// <param name="MapperWrites">The missing or drifted `roles` mappers written.</param>
/// <param name="UsersCreated">The missing demo users created.</param>
/// <param name="RoleMappingWrites">The add/remove role-mapping requests made.</param>
public sealed record KeycloakSeedSummary(
    int RolesCreated,
    int MapperWrites,
    int UsersCreated,
    int RoleMappingWrites);

/// <summary>Converges the Keycloak-owned half of the deterministic demo staff seed.</summary>
public sealed class KeycloakSeeder
{
    private static readonly string[] BusinessRoleNames = Enum.GetNames<Role>();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient http;
    private readonly KeycloakSeedOptions options;

    /// <summary>Creates a Keycloak demo seeder over the supplied HTTP transport.</summary>
    /// <param name="http">The transport used for token and Admin API calls.</param>
    /// <param name="options">Validated Keycloak seed settings.</param>
    public KeycloakSeeder(HttpClient http, KeycloakSeedOptions options)
    {
        this.http = http;
        this.options = options;
        this.http.BaseAddress = options.BaseUrl;
    }

    /// <summary>Deletes the configured realm if it exists, then recreates it from a realm export
    /// document. Intended only for a --reseed run: this discards every user, role, and client in
    /// the realm, demo or not.</summary>
    /// <param name="realmExportJson">The full realm representation to import after deletion.</param>
    /// <param name="cancellationToken">Cancels Keycloak network operations.</param>
    /// <exception cref="SeedException">Keycloak rejects the delete or the recreate request.</exception>
    public async Task ResetRealmAsync(string realmExportJson, CancellationToken cancellationToken)
    {
        var token = await GetTokenAsync(cancellationToken);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var realmPath = $"admin/realms/{Escape(options.Realm)}";
        using var deleteResponse = await http.DeleteAsync(realmPath, cancellationToken);
        if (deleteResponse.StatusCode != HttpStatusCode.NotFound)
        {
            await EnsureSuccessAsync(deleteResponse, realmPath, cancellationToken);
        }

        await SendAsync(
            HttpMethod.Post,
            "admin/realms",
            new StringContent(realmExportJson, System.Text.Encoding.UTF8, "application/json"),
            cancellationToken);
    }

    /// <summary>Ensures realm roles, mapper, demo users, and exact business-role mappings.</summary>
    /// <param name="staff">The canonical demo identity rows.</param>
    /// <param name="cancellationToken">Cancels Keycloak network operations.</param>
    /// <returns>Counts of objects changed by this convergence.</returns>
    /// <exception cref="SeedException">Keycloak rejects a request or identity keys conflict.</exception>
    public async Task<KeycloakSeedSummary> EnsureAsync(
        IReadOnlyList<StaffProfileSpec> staff,
        CancellationToken cancellationToken)
    {
        var token = await GetTokenAsync(cancellationToken);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var roleWrites = 0;
        var mapperWrites = 0;
        var userWrites = 0;
        var mappingWrites = 0;
        var roles = new Dictionary<string, RoleRepresentation>(StringComparer.Ordinal);

        foreach (var name in BusinessRoleNames)
        {
            var path = $"admin/realms/{Escape(options.Realm)}/roles/{Escape(name)}";
            var response = await http.GetAsync(path, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                await SendAsync(
                    HttpMethod.Post,
                    $"admin/realms/{Escape(options.Realm)}/roles",
                    JsonContent.Create(new { name, description = $"EventBooking {name}" }),
                    cancellationToken);
                roleWrites++;
                response = await http.GetAsync(path, cancellationToken);
            }

            await EnsureSuccessAsync(response, path, cancellationToken);
            roles[name] = (await response.Content.ReadFromJsonAsync<RoleRepresentation>(
                Json, cancellationToken))
                ?? throw new SeedException($"Keycloak returned an empty role '{name}'.");
        }

        var clientPath =
            $"admin/realms/{Escape(options.Realm)}/clients?clientId=eventbooking-web";
        var clients = await GetAsync<List<ClientRepresentation>>(clientPath, cancellationToken);
        var client = clients.SingleOrDefault(value => value.ClientId == "eventbooking-web")
            ?? throw new SeedException("Keycloak client 'eventbooking-web' does not exist.");
        var mapperPath =
            $"admin/realms/{Escape(options.Realm)}/clients/{Escape(client.Id)}/protocol-mappers/models";
        var mappers = await GetAsync<List<MapperRepresentation>>(mapperPath, cancellationToken);
        var currentMapper = mappers.SingleOrDefault(value => value.Name == "roles");
        var desiredMapper = DesiredMapper(currentMapper?.Id);
        if (currentMapper is null)
        {
            await SendAsync(HttpMethod.Post, mapperPath, JsonContent.Create(desiredMapper, options: Json), cancellationToken);
            mapperWrites++;
        }
        else if (!MapperMatches(currentMapper))
        {
            await SendAsync(
                HttpMethod.Put,
                $"{mapperPath}/{Escape(currentMapper.Id!)}",
                JsonContent.Create(desiredMapper, options: Json),
                cancellationToken);
            mapperWrites++;
        }

        foreach (var person in staff)
        {
            var userPath =
                $"admin/realms/{Escape(options.Realm)}/users?username={Escape(person.Username)}&exact=true&briefRepresentation=false";
            var matches = await GetAsync<List<UserRepresentation>>(userPath, cancellationToken);
            if (matches.Count > 1)
            {
                throw new SeedException(
                    $"Keycloak returned multiple exact users for '{person.Username}'.");
            }

            if (matches.Count == 0)
            {
                await SendAsync(
                    HttpMethod.Post,
                    $"admin/realms/{Escape(options.Realm)}/users",
                    JsonContent.Create(new
                    {
                        id = person.UserId,
                        username = person.Username,
                        enabled = true,
                        attributes = new Dictionary<string, string[]>
                        {
                            ["staffId"] = [person.StaffId.Value],
                        },
                        credentials = new[]
                        {
                            new { type = "password", value = options.DemoPassword, temporary = false },
                        },
                    }, options: Json),
                    cancellationToken);
                userWrites++;
                matches = await GetAsync<List<UserRepresentation>>(userPath, cancellationToken);
                if (matches.Count != 1)
                {
                    throw new SeedException(
                        $"Keycloak did not return the newly created user '{person.Username}'.");
                }

                ValidateIdentity(matches[0], person);
            }
            else
            {
                ValidateIdentity(matches[0], person);
            }

            var id = person.UserId.ToString();
            var mappingsPath =
                $"admin/realms/{Escape(options.Realm)}/users/{Escape(id)}/role-mappings/realm";
            var currentMappings = await GetAsync<List<RoleRepresentation>>(
                mappingsPath, cancellationToken);
            var currentBusiness = currentMappings
                .Where(value => BusinessRoleNames.Contains(value.Name, StringComparer.Ordinal))
                .ToDictionary(value => value.Name, StringComparer.Ordinal);
            var desiredNames = person.Roles.Select(value => value.ToString())
                .ToHashSet(StringComparer.Ordinal);
            var missing = desiredNames.Except(currentBusiness.Keys)
                .Select(name => roles[name]).ToArray();
            var extra = currentBusiness.Keys.Except(desiredNames)
                .Select(name => currentBusiness[name]).ToArray();

            if (missing.Length > 0)
            {
                await SendAsync(HttpMethod.Post, mappingsPath,
                    JsonContent.Create(missing, options: Json), cancellationToken);
                mappingWrites++;
            }
            if (extra.Length > 0)
            {
                await SendAsync(HttpMethod.Delete, mappingsPath,
                    JsonContent.Create(extra, options: Json), cancellationToken);
                mappingWrites++;
            }
        }

        return new KeycloakSeedSummary(roleWrites, mapperWrites, userWrites, mappingWrites);
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        var path = $"realms/{Escape(options.AdminRealm)}/protocol/openid-connect/token";
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = "admin-cli",
            ["username"] = options.AdminUsername,
            ["password"] = options.AdminPassword,
        });
        using var response = await http.PostAsync(path, content, cancellationToken);
        await EnsureSuccessAsync(response, path, cancellationToken);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.GetProperty("access_token").GetString()
            ?? throw new SeedException("Keycloak token response omitted access_token.");
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(path, cancellationToken);
        await EnsureSuccessAsync(response, path, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken))
            ?? throw new SeedException($"Keycloak returned an empty response for '{path}'.");
    }

    private async Task SendAsync(
        HttpMethod method,
        string path,
        HttpContent content,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        using var response = await http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, path, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string path,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new SeedException(
            $"Keycloak request '{path}' failed with {(int)response.StatusCode}: {body}");
    }

    private static void ValidateIdentity(UserRepresentation actual, StaffProfileSpec expected)
    {
        if (!Guid.TryParse(actual.Id, out var actualId) || actualId != expected.UserId)
        {
            throw new SeedException(
                $"Keycloak user '{expected.Username}' has a conflicting provider identifier.");
        }

        if (actual.Attributes is null
            || !actual.Attributes.TryGetValue("staffId", out var values)
            || values.Count != 1
            || !string.Equals(values[0], expected.StaffId.Value, StringComparison.OrdinalIgnoreCase))
        {
            throw new SeedException(
                $"Keycloak user '{expected.Username}' has a conflicting staffId attribute.");
        }
    }

    private static MapperRepresentation DesiredMapper(string? id) => new()
    {
        Id = id,
        Name = "roles",
        Protocol = "openid-connect",
        ProtocolMapper = "oidc-usermodel-realm-role-mapper",
        ConsentRequired = false,
        Config = new Dictionary<string, string>
        {
            ["multivalued"] = "true",
            ["claim.name"] = "roles",
            ["jsonType.label"] = "String",
            ["id.token.claim"] = "true",
            ["access.token.claim"] = "true",
        },
    };

    private static bool MapperMatches(MapperRepresentation mapper)
    {
        var desired = DesiredMapper(mapper.Id);
        return mapper.Protocol == desired.Protocol
            && mapper.ProtocolMapper == desired.ProtocolMapper
            && mapper.ConsentRequired == desired.ConsentRequired
            && desired.Config.All(pair =>
                mapper.Config.TryGetValue(pair.Key, out var value) && value == pair.Value);
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private sealed record RoleRepresentation(string Id, string Name);
    private sealed record ClientRepresentation(string Id, string ClientId);
    private sealed record UserRepresentation(
        string Id,
        string Username,
        Dictionary<string, List<string>>? Attributes);
    private sealed class MapperRepresentation
    {
        public string? Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Protocol { get; init; } = string.Empty;
        public string ProtocolMapper { get; init; } = string.Empty;
        public bool ConsentRequired { get; init; }
        public Dictionary<string, string> Config { get; init; } = [];
    }
}
