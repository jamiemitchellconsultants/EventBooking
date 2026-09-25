// src/EventBooking.SeedData/SeedCommand.cs (complete)
using System.Globalization;

namespace EventBooking.SeedData;

public sealed record SeedCliOptions(
    string ConnectionString, bool Demo, bool Reanchor, DateOnly? ReanchorDate, bool Reseed,
    bool Verbose, bool LoadFixture, string? LoadFixturePath)
{
    public static SeedCliOptions Parse(string[] args, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(environment);
        var tokens = args.ToList();
        DateOnly? reanchorDate = null;
        var reanchorIndex = tokens.IndexOf("--reanchor");
        if (reanchorIndex >= 0 && reanchorIndex + 1 < tokens.Count
            && LooksLikeDate(tokens[reanchorIndex + 1]))
        {
            var token = tokens[reanchorIndex + 1];
            if (!DateOnly.TryParseExact(
                    token, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                throw new SeedException($"--reanchor date must be yyyy-MM-dd (got '{token}').");
            reanchorDate = parsed;
            tokens.RemoveAt(reanchorIndex + 1);
        }

        var values = tokens.Where(x => !x.StartsWith('-')).ToList();
        if (values.Count > 1)
            throw new SeedException("Supply exactly one PostgreSQL connection string.");
        var connection = values.SingleOrDefault() ?? environment("ConnectionStrings__EventBooking");
        if (string.IsNullOrWhiteSpace(connection))
            throw new SeedException("A PostgreSQL connection string argument or ConnectionStrings__EventBooking is required.");
        var known = new HashSet<string>(StringComparer.Ordinal)
            { "--demo", "--reanchor", "--reseed", "--verbose", "--load-fixture" };
        var unknown = tokens.Where(x => x.StartsWith('-') && !known.Contains(x)).ToList();
        if (unknown.Count > 0)
            throw new SeedException($"Unknown option '{unknown[0]}'.");
        var demo = tokens.Contains("--demo", StringComparer.Ordinal);
        var reanchor = tokens.Contains("--reanchor", StringComparer.Ordinal);
        var reseed = tokens.Contains("--reseed", StringComparer.Ordinal);
        var load = tokens.Contains("--load-fixture", StringComparer.Ordinal);
        if (reanchor && !demo) throw new SeedException("--reanchor requires --demo.");
        if (reseed && !demo) throw new SeedException("--reseed requires --demo.");
        if (reseed && !string.Equals(environment("EVENTBOOKING_ALLOW_RESEED"), "true", StringComparison.Ordinal))
            throw new SeedException("--reseed requires EVENTBOOKING_ALLOW_RESEED=true.");
        if (load && !demo) throw new SeedException("--load-fixture requires --demo.");
        if (load && !string.Equals(environment("EVENTBOOKING_ENABLE_LOAD_FIXTURE"), "true", StringComparison.Ordinal))
            throw new SeedException("--load-fixture requires EVENTBOOKING_ENABLE_LOAD_FIXTURE=true.");
        var path = load ? environment("EVENTBOOKING_LOAD_FIXTURE_PATH") : null;
        if (load && (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)))
            throw new SeedException("--load-fixture requires an absolute EVENTBOOKING_LOAD_FIXTURE_PATH.");
        return new SeedCliOptions(connection, demo, reanchor, reanchorDate, reseed,
            tokens.Contains("--verbose", StringComparer.Ordinal), load, path);
    }

    // Shape check only (dddd-dd-dd); whether it is a real calendar date is decided by TryParseExact.
    private static bool LooksLikeDate(string token) =>
        token.Length == 10 && token[4] == '-' && token[7] == '-'
        && token.Where((_, index) => index != 4 && index != 7).All(char.IsAsciiDigit);
}

public interface ISeedRunSteps : IAsyncDisposable
{
    Task ApplyDatabaseRolesAsync(CancellationToken ct);
    Task ApplyMigrationsAsync(CancellationToken ct);
    Task<DateOnly> ReanchorAsync(DateOnly? date, CancellationToken ct);
    Task<KeycloakSeedSummary?> ConvergeKeycloakAsync(bool recreateRealm, CancellationToken ct);
    Task<SeedSummary> SeedDemoAsync(bool wipeFirst, CancellationToken ct);
    Task<int> SendDemoInvitationsAsync(CancellationToken ct);
    Task<int> SeedLoadFixtureAsync(string outputPath, CancellationToken ct);
}

public static class SeedCommand
{
    public static async Task<int> RunAsync(
        SeedCliOptions options, ISeedRunSteps steps, TextWriter output, CancellationToken ct)
    {
        async Task Progress(string message)
        {
            if (options.Verbose) await output.WriteLineAsync($"[seed] {message}");
        }

        await Progress("Applying database roles...");
        await steps.ApplyDatabaseRolesAsync(ct);
        await Progress("Applying pending migrations...");
        await steps.ApplyMigrationsAsync(ct);
        await Progress("Migrations applied.");
        if (!options.Demo)
        {
            await output.WriteLineAsync("Database roles and migrations applied; demo data was not requested.");
            return 0;
        }
        if (options.Reanchor)
        {
            var anchor = await steps.ReanchorAsync(options.ReanchorDate, ct);
            await output.WriteLineAsync($"[seed] Reanchored to {anchor:yyyy-MM-dd}.");
        }
        if (options.Reseed)
            await Progress("Reseed requested: the Keycloak realm will be deleted and recreated first, if configured.");
        var keycloak = await steps.ConvergeKeycloakAsync(options.Reseed, ct);
        if (keycloak is null)
        {
            await Progress("Keycloak provider seed skipped.");
        }
        else
        {
            await Progress(options.Reseed
                ? "Keycloak realm reset and convergence complete."
                : "Keycloak convergence complete.");
            await output.WriteLineAsync(
                $"Keycloak seed complete: {keycloak.RolesCreated} roles created, " +
                $"{keycloak.MapperWrites} mapper writes, " +
                $"{keycloak.UsersCreated} users created, " +
                $"{keycloak.RoleMappingWrites} role-mapping writes.");
        }
        await Progress("Seeding demo data...");
        var summary = await steps.SeedDemoAsync(options.Reseed, ct);
        await Progress("Sending demo invitations...");
        var sent = await steps.SendDemoInvitationsAsync(ct);
        await output.WriteLineAsync(
            $"Demo seed complete: {summary.LocationsEnsured} locations, " +
            $"{summary.AppointmentTypesEnsured} appointment types, {summary.AttendeesEnsured} attendees; " +
            $"{sent} invitations sent; Keycloak {(keycloak is null ? "not configured" : "converged")}.");
        if (options.LoadFixture)
        {
            await Progress("Writing load fixture...");
            var count = await steps.SeedLoadFixtureAsync(options.LoadFixturePath!, ct);
            await output.WriteLineAsync($"Load fixture ready: {count} invitations.");
        }
        return 0;
    }
}
