namespace EventBooking.SeedData;

/// <summary>Validated settings for one opt-in Keycloak demo-seed run.</summary>
public sealed class KeycloakSeedOptions
{
    private static readonly string[] RequiredNames =
    [
        "Keycloak__BaseUrl",
        "Keycloak__AdminUsername",
        "Keycloak__AdminPassword",
        "Keycloak__DemoPassword",
    ];

    private KeycloakSeedOptions(
        Uri baseUrl,
        string realm,
        string adminRealm,
        string adminUsername,
        string adminPassword,
        string demoPassword,
        string? realmExportPath)
    {
        BaseUrl = baseUrl;
        Realm = realm;
        AdminRealm = adminRealm;
        AdminUsername = adminUsername;
        AdminPassword = adminPassword;
        DemoPassword = demoPassword;
        RealmExportPath = realmExportPath;
    }

    /// <summary>Gets the Keycloak origin used for token and Admin API calls.</summary>
    public Uri BaseUrl { get; }

    /// <summary>Gets the realm whose EventBooking demo identities are converged.</summary>
    public string Realm { get; }

    /// <summary>Gets the realm used to authenticate the Keycloak administrator.</summary>
    public string AdminRealm { get; }

    /// <summary>Gets the Keycloak administrator username; never written to progress output.</summary>
    public string AdminUsername { get; }

    /// <summary>Gets the Keycloak administrator password; never written to progress output.</summary>
    public string AdminPassword { get; }

    /// <summary>Gets the non-production password assigned only when a demo user is created.</summary>
    public string DemoPassword { get; }

    /// <summary>Gets the path to a realm export JSON file, required only for a --reseed realm
    /// delete-and-recreate; unused by ordinary convergence.</summary>
    public string? RealmExportPath { get; }

    /// <summary>Reads Keycloak settings from process environment variables.</summary>
    public static KeycloakSeedOptions? FromEnvironment() =>
        From(Environment.GetEnvironmentVariable);

    /// <summary>
    /// Returns null when every Keycloak setting is absent and otherwise validates the complete set.
    /// </summary>
    /// <param name="readSetting">Reads one setting by its environment-variable name.</param>
    /// <returns>Validated settings, or null when Keycloak seeding is disabled.</returns>
    /// <exception cref="SeedException">A required setting is absent or the base URL is invalid.</exception>
    public static KeycloakSeedOptions? From(Func<string, string?> readSetting)
    {
        ArgumentNullException.ThrowIfNull(readSetting);

        var values = RequiredNames.ToDictionary(name => name, readSetting);
        values["Keycloak__Realm"] = readSetting("Keycloak__Realm");
        values["Keycloak__AdminRealm"] = readSetting("Keycloak__AdminRealm");
        var realmExportPath = readSetting("Keycloak__RealmExportPath");

        if (values.Values.All(string.IsNullOrWhiteSpace) && string.IsNullOrWhiteSpace(realmExportPath))
        {
            return null;
        }

        foreach (var name in RequiredNames)
        {
            if (string.IsNullOrWhiteSpace(values[name]))
            {
                throw new SeedException($"Keycloak seed setting '{name}' is required.");
            }
        }

        if (!Uri.TryCreate(values["Keycloak__BaseUrl"], UriKind.Absolute, out var baseUrl)
            || (baseUrl.Scheme != Uri.UriSchemeHttp && baseUrl.Scheme != Uri.UriSchemeHttps))
        {
            throw new SeedException(
                "Keycloak seed setting 'Keycloak__BaseUrl' must be an absolute HTTP(S) URL.");
        }

        return new KeycloakSeedOptions(
            baseUrl,
            string.IsNullOrWhiteSpace(values["Keycloak__Realm"])
                ? "eventbooking"
                : values["Keycloak__Realm"]!,
            string.IsNullOrWhiteSpace(values["Keycloak__AdminRealm"])
                ? "master"
                : values["Keycloak__AdminRealm"]!,
            values["Keycloak__AdminUsername"]!,
            values["Keycloak__AdminPassword"]!,
            values["Keycloak__DemoPassword"]!,
            string.IsNullOrWhiteSpace(realmExportPath) ? null : realmExportPath);
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"KeycloakSeedOptions {{ BaseUrl = {BaseUrl}, Realm = {Realm}, AdminRealm = {AdminRealm}, AdminUsername = [REDACTED], AdminPassword = [REDACTED], DemoPassword = [REDACTED], RealmExportPath = {RealmExportPath ?? "(none)"} }}";
}
