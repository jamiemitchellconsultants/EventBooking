namespace EventBooking.SeedData;

public static class SeedUsage
{
    public const string Text = """
        Usage: EventBooking.SeedData <postgres connection string> [--demo [--reanchor [yyyy-MM-dd]] [--reseed] [--load-fixture]] [--verbose]
           or set the ConnectionStrings__EventBooking environment variable.
           Database roles and pending migrations are always applied first, whichever mode runs.
           Without --demo the run stops there: migrations only, nothing is seeded.
           --demo seeds the deterministic demo dataset, converges the Keycloak demo staff (when
           Keycloak is configured) and sends the demo invitations through SMTP.
           --reanchor [yyyy-MM-dd] resolves every demo day offset against the given date (default:
           today in Europe/London) instead of the built-in anchor, and moves existing demo rows to
           match. Requires --demo. Use it when the built-in anchor has gone stale.
           --reseed wipes every domain table, then deletes and recreates the Keycloak realm from
           the file named by Keycloak__RealmExportPath (when Keycloak is configured), then seeds
           fresh. Requires --demo and EVENTBOOKING_ALLOW_RESEED=true.
           --load-fixture writes a load-test fixture manifest. Requires --demo,
           EVENTBOOKING_ENABLE_LOAD_FIXTURE=true and an absolute EVENTBOOKING_LOAD_FIXTURE_PATH.
           --verbose reports per-step progress; a failure prints the full exception.
           --help, -h prints this text.
           Keycloak demo users are converged when Keycloak__BaseUrl, Keycloak__Realm,
           Keycloak__AdminRealm, Keycloak__AdminUsername, Keycloak__AdminPassword and
           Keycloak__DemoPassword are set; otherwise only the database is seeded.
           Demo invitations are sent through SMTP using Portal__BaseUrl, Tokens__SigningKey,
           Email__Smtp__Host and Email__Smtp__Port. Local defaults apply only for a loopback
           Portal__BaseUrl; a non-local portal needs an explicit Tokens__SigningKey and
           Email__Smtp__Host. Match Tokens__SigningKey and Portal__BaseUrl to the running API.
        """;

    public static bool IsHelpRequest(IReadOnlyList<string> args) =>
        args.Any(x => x is "--help" or "-h");
}

public static class SeedFailureReport
{
    public static string Format(Exception exception, IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(args);
        return args.Contains("--verbose")
            ? $"Seed failed: {exception}"
            : $"Seed failed: {exception.Message}";
    }
}
