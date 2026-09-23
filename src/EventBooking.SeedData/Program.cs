using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Time;
using EventBooking.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var reseed = args.Contains("--reseed");
var skipSeed = args.Contains("--skip-seed");
var verbose = args.Contains("--verbose");
var reanchorIndex = args.ToList().IndexOf("--reanchor");
var reanchorRequested = reanchorIndex >= 0;
DateOnly? reanchorDate = null;
if (reanchorRequested
    && reanchorIndex + 1 < args.Length
    && DateOnly.TryParse(args[reanchorIndex + 1], out var parsedDate))
{
    reanchorDate = parsedDate;
}
var connectionString = args.FirstOrDefault(a => !a.StartsWith('-'))
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__EventBooking");

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project src/EventBooking.SeedData -- \"<postgres connection string>\" [--reseed] [--skip-seed] [--verbose]");
    Console.Error.WriteLine(
        "   or set the ConnectionStrings__EventBooking environment variable.");
    Console.Error.WriteLine(
        "   Pending migrations are always applied first, whichever mode runs.");
    Console.Error.WriteLine(
        "   --reseed wipes every domain table first, then seeds fresh.");
    Console.Error.WriteLine(
        "   --skip-seed applies migrations only and seeds nothing.");
    Console.Error.WriteLine(
        "   --verbose reports per-step progress; failures print the full exception.");
    Console.Error.WriteLine(
        "   --reanchor [yyyy-MM-dd] resolves every day offset against the given date");
    Console.Error.WriteLine(
        "   (default: today at transitional location) instead of the file anchor, without editing");
    Console.Error.WriteLine(
        "   demo-seed.json. Use it when the file anchor has gone stale and proposals");
    Console.Error.WriteLine(
        "   land on today or earlier.");
    Console.Error.WriteLine(
        "   Keycloak demo users are converged when Keycloak__BaseUrl, Keycloak__Realm,");
    Console.Error.WriteLine(
        "   Keycloak__AdminRealm, Keycloak__AdminUsername, Keycloak__AdminPassword, and");
    Console.Error.WriteLine(
        "   Keycloak__DemoPassword are set; otherwise only the database is seeded.");
    Console.Error.WriteLine(
        "   --reseed also deletes and recreates the Keycloak realm from the file named by");
    Console.Error.WriteLine(
        "   Keycloak__RealmExportPath, when Keycloak settings are configured.");
    Console.Error.WriteLine("   Normal seed/reseed sends five demo invitations through Mailpit SMTP.");
    Console.Error.WriteLine("   Local defaults: Portal__BaseUrl=http://localhost:5002, Email__Smtp__Host=localhost,");
    Console.Error.WriteLine("   Email__Smtp__Port=1025 and the local API's development token key.");
    Console.Error.WriteLine("   Non-local portals require explicit Tokens__SigningKey and Email__Smtp__Host.");
    Console.Error.WriteLine("   Match Tokens__SigningKey and Portal__BaseUrl to the running API.");
    Console.Error.WriteLine("   --skip-seed does not read email settings or send any messages.");
    return 2;
}

try
{
    var services = new ServiceCollection();
    services.AddLogging();
    if (skipSeed)
    {
        // The persistence interceptor still needs IClock, even for migration-only context creation.
        services.AddEventBookingPersistence(connectionString);
        services.AddSingleton(new ClockOptions("Europe/London"));
        services.AddSingleton<IClock, SystemClock>();
    }
    else
    {
        var email = DemoEmailOptions.From(Environment.GetEnvironmentVariable);
        services.AddEventBookingInfrastructure(connectionString, email.Clock, email.Tokens);
        services.AddEventBookingApplication(email.Portal,
            new EventBooking.Application.Access.StaffIdPolicy(Environment.GetEnvironmentVariable("Identity__StaffIdPattern")));
        services.AddLocalEmailTransport(email.Sender, email.Smtp);
        services.AddScoped<DemoSeeder>();
        services.AddScoped<DemoInvitationSeeder>();
    }
    using var provider = services.BuildServiceProvider();
    using var scope = provider.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
    if (verbose)
    {
        Console.WriteLine("[seed] Applying pending migrations...");
    }

    await database.Database.MigrateAsync();
    if (verbose)
    {
        Console.WriteLine("[seed] Migrations applied.");
    }

    if (skipSeed)
    {
        Console.WriteLine("Migrations applied. Skipping seed data (--skip-seed).");
        return 0;
    }

    // Seed staff numbers must satisfy the same deployment policy the application enforces.
    DemoSeedSpec.ConfigureStaffIdPattern(
        Environment.GetEnvironmentVariable("Identity__StaffIdPattern"));

    var keycloakStep = new KeycloakSeedStep(
        Environment.GetEnvironmentVariable,
        static () => new HttpClient());
    if (reseed && verbose)
    {
        Console.WriteLine("[seed] Reseed requested: the Keycloak realm will be deleted and recreated first, if configured.");
    }

    var keycloakSummary = await keycloakStep.RunAsync(
        skipSeed, reseed, DemoSeedSpec.Staff(), CancellationToken.None);
    if (keycloakSummary is not null)
    {
        if (verbose)
        {
            Console.WriteLine(reseed
                ? "[seed] Keycloak realm reset and convergence complete."
                : "[seed] Keycloak convergence complete.");
        }

        Console.WriteLine(
            $"Keycloak seed complete: {keycloakSummary.RolesCreated} roles created, " +
            $"{keycloakSummary.MapperWrites} mapper writes, " +
            $"{keycloakSummary.UsersCreated} users created, " +
            $"{keycloakSummary.RoleMappingWrites} role-mapping writes.");
    }
    else if (verbose)
    {
        Console.WriteLine("[seed] Keycloak provider seed skipped.");
    }

    if (reanchorRequested)
    {
        reanchorDate ??= scope.ServiceProvider
            .GetRequiredService<IClock>()
            .TodayAtTransitionalLocation;
        DemoSeedSpec.OverrideAnchor(reanchorDate.Value);
        Console.WriteLine($"[seed] Reanchored to {reanchorDate:yyyy-MM-dd}.");
    }

    var seeder = scope.ServiceProvider.GetRequiredService<DemoSeeder>();
    if (verbose)
    {
        seeder.Progress = Console.Out;
    }

    var summary = reseed
        ? await seeder.ReseedAsync(CancellationToken.None)
        : await seeder.RunAsync(CancellationToken.None);

    var invitations = scope.ServiceProvider.GetRequiredService<DemoInvitationSeeder>();
    if (verbose) invitations.Progress = Console.Out;
    var invitationEmailsSent = await invitations.RunAsync(CancellationToken.None);

    Console.WriteLine(
        $"Migrations applied. " +
        $"{(reseed ? "Reseed complete (database was cleared): " : "Seed complete: ")}" +
        $"{summary.IdentitiesEnsured} identities, " +
        $"{summary.ProfilesEnsured} profiles, " +
        $"{summary.AgreedEventsImported} agreed events, " +
        $"{summary.ProposalsEnsured} proposals, " +
        $"{summary.AcceptancesApplied} acceptances, " +
        $"{summary.AttendeesCreated} attendees; " +
        $"{invitationEmailsSent} invitation emails sent or retried.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(verbose ? $"Seed failed: {ex}" : $"Seed failed: {ex.Message}");
    return 2;
}
