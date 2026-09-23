namespace EventBooking.SeedData;

/// <summary>Runs optional Keycloak convergence, and a --reseed realm delete-and-recreate, at the
/// seed-host boundary.</summary>
/// <param name="readSetting">Reads one environment-style setting by name.</param>
/// <param name="createHttpClient">Creates HTTP transport only for a configured provider run.</param>
/// <param name="readFile">Reads a realm export file's full contents, only for a --reseed run.</param>
public sealed class KeycloakSeedStep(
    Func<string, string?> readSetting,
    Func<HttpClient> createHttpClient,
    Func<string, string>? readFile = null)
{
    /// <summary>Runs provider convergence unless all seed work or Keycloak itself is disabled. When
    /// <paramref name="reseed"/> is also requested, first deletes and recreates the configured realm
    /// from <c>Keycloak__RealmExportPath</c> before converging demo users into it.</summary>
    /// <param name="skipSeed">Whether the host is in migrations-only mode.</param>
    /// <param name="reseed">Whether the database is also being fully reseeded.</param>
    /// <param name="staff">The canonical provider and database identity rows.</param>
    /// <param name="cancellationToken">Cancels provider requests.</param>
    /// <returns>The provider change summary, or null when no provider work was attempted.</returns>
    /// <exception cref="SeedException">
    /// A reseed is requested but <c>Keycloak__RealmExportPath</c> is not set, or Keycloak rejects
    /// the realm delete, recreate, or convergence requests.
    /// </exception>
    public async Task<KeycloakSeedSummary?> RunAsync(
        bool skipSeed,
        bool reseed,
        IReadOnlyList<StaffProfileSpec> staff,
        CancellationToken cancellationToken)
    {
        if (skipSeed)
        {
            return null;
        }

        var options = KeycloakSeedOptions.From(readSetting);
        if (options is null)
        {
            return null;
        }

        if (reseed && string.IsNullOrWhiteSpace(options.RealmExportPath))
        {
            throw new SeedException(
                "Keycloak seed setting 'Keycloak__RealmExportPath' is required for --reseed.");
        }

        using var http = createHttpClient();
        var seeder = new KeycloakSeeder(http, options);

        if (reseed)
        {
            var reader = readFile ?? File.ReadAllText;
            var realmExportJson = reader(options.RealmExportPath!);
            await seeder.ResetRealmAsync(realmExportJson, cancellationToken);
        }

        return await seeder.EnsureAsync(staff, cancellationToken);
    }
}
