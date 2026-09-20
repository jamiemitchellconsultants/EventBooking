# 00c — Configurable staff identity, edits 5 (Task 3a)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — src/EventBooking.SeedData/Program.cs — 1/1

<!-- retirement-file: {"id":13,"file":"src/EventBooking.SeedData/Program.cs","beforeSha":"435edab819af4d5ba845f8a1d2b3729e3ec88399f0a083c633f816bed008ce51","afterSha":"21774106ccd66cd8e203887e3aa6eeebf92ec90881c8715fe0c6474b1abbdd79","side":"before","part":1,"parts":1} -->

`````csharp
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
        services.AddSingleton(new TransitionalLocationOptions("Europe/London"));
        services.AddSingleton<IClock, SystemClock>();
    }
    else
    {
        var email = DemoEmailOptions.From(Environment.GetEnvironmentVariable);
        services.AddEventBookingInfrastructure(connectionString, email.TransitionalLocation, email.Tokens);
        services.AddEventBookingApplication(email.Portal);
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
`````

## after — src/EventBooking.SeedData/Program.cs — 1/1

<!-- retirement-file: {"id":13,"file":"src/EventBooking.SeedData/Program.cs","beforeSha":"435edab819af4d5ba845f8a1d2b3729e3ec88399f0a083c633f816bed008ce51","afterSha":"21774106ccd66cd8e203887e3aa6eeebf92ec90881c8715fe0c6474b1abbdd79","side":"after","part":1,"parts":1} -->

`````csharp
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
        services.AddSingleton(new TransitionalLocationOptions("Europe/London"));
        services.AddSingleton<IClock, SystemClock>();
    }
    else
    {
        var email = DemoEmailOptions.From(Environment.GetEnvironmentVariable);
        services.AddEventBookingInfrastructure(connectionString, email.TransitionalLocation, email.Tokens);
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
`````

## before — tests/EventBooking.Api.Tests/CallerIdentityTests.cs — 1/1

<!-- retirement-file: {"id":14,"file":"tests/EventBooking.Api.Tests/CallerIdentityTests.cs","beforeSha":"3d07342c73ea078035748e6fc68b098ab0ab50f80b7bc1598a9459c0652222e2","afterSha":"a14e78bcb8ab447051fc12963449f3ba275bc01488dda6896ef037f05cdf7778","side":"before","part":1,"parts":1} -->

`````csharp
using System.Security.Claims;
using EventBooking.Api.Auth;

namespace EventBooking.Api.Tests;

/// <summary>Verifies untrusted staff-number claims are parsed at the request boundary.</summary>
public sealed class CallerIdentityTests
{
    /// <summary>Verifies valid claim casing is normalized into the domain value.</summary>
    [Theory]
    [InlineData("u123456", "U123456")]
    [InlineData("N654321", "N654321")]
    public void ValidClaimIsParsedAndCanonicalised(string claim, string expected)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(HttpContextCallerAccessor.StaffIdClaim, claim)], "test"));

        Assert.Equal(expected, HttpContextCallerAccessor.StaffIdOf(principal)!.Value);
    }

    /// <summary>Verifies absent and malformed claims are indistinguishable and non-throwing.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("X123456")]
    public void MissingOrMalformedClaimReturnsNull(string? claim)
    {
        var claims = claim is null ? [] : new[] { new Claim("staff_id", claim) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        Assert.Null(HttpContextCallerAccessor.StaffIdOf(principal));
    }
}
`````

## after — tests/EventBooking.Api.Tests/CallerIdentityTests.cs — 1/1

<!-- retirement-file: {"id":14,"file":"tests/EventBooking.Api.Tests/CallerIdentityTests.cs","beforeSha":"3d07342c73ea078035748e6fc68b098ab0ab50f80b7bc1598a9459c0652222e2","afterSha":"a14e78bcb8ab447051fc12963449f3ba275bc01488dda6896ef037f05cdf7778","side":"after","part":1,"parts":1} -->

`````csharp
using System.Security.Claims;
using EventBooking.Api.Auth;

namespace EventBooking.Api.Tests;

/// <summary>Verifies untrusted staff-number claims are parsed at the request boundary.</summary>
public sealed class CallerIdentityTests
{
    /// <summary>Verifies valid claim casing is normalized into the domain value.</summary>
    [Theory]
    [InlineData("u123456", "U123456")]
    [InlineData("N654321", "N654321")]
    public void ValidClaimIsParsedAndCanonicalised(string claim, string expected)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(HttpContextCallerAccessor.StaffIdClaim, claim)], "test"));

        Assert.Equal(expected, HttpContextCallerAccessor.StaffIdOf(principal)!.Value);
    }

    /// <summary>Verifies absent and malformed claims are indistinguishable and non-throwing.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("invalid staff id")]
    public void MissingOrMalformedClaimReturnsNull(string? claim)
    {
        var claims = claim is null ? [] : new[] { new Claim("staff_id", claim) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        Assert.Null(HttpContextCallerAccessor.StaffIdOf(principal));
    }
}
`````

## after — tests/EventBooking.Api.Tests/ConfiguredStaffIdBoundaryTests.cs — 1/1

<!-- retirement-file: {"id":15,"file":"tests/EventBooking.Api.Tests/ConfiguredStaffIdBoundaryTests.cs","beforeSha":null,"afterSha":"0ea8b534dfec96197a7035cd7f6739527d5754641933dff905869cebc88fdf76","side":"after","part":1,"parts":1} -->

`````csharp
using System.Security.Claims;
using EventBooking.Api.Auth;
using EventBooking.Application.Access;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventBooking.Api.Tests;

public sealed class ConfiguredStaffIdBoundaryTests
{
    [Theory]
    [InlineData(" a10023 ", "^[A-Z0-9]{1,32}$", "A10023")]
    [InlineData(" u123456 ", "^[UN][0-9]{6}$", "U123456")]
    [InlineData("A10023", "^[UN][0-9]{6}$", null)]
    public void Request_identity_uses_the_configured_policy(string input, string pattern, string? expected)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("staff_id", input)], "test")),
        };
        var accessor = new HttpContextCallerAccessor(new HttpContextAccessor { HttpContext = context },
            NullLogger<HttpContextCallerAccessor>.Instance, new StaffIdPolicy(pattern));
        Assert.Equal(expected, accessor.StaffId?.Value);
    }

    [Fact]
    public void Invalid_deployment_expression_fails_during_policy_construction()
    {
        Assert.ThrowsAny<ArgumentException>(() => new StaffIdPolicy("["));
    }
}
`````

## before — tests/EventBooking.Api.Tests/StaffIdentityRecorderTests.cs — 1/1

<!-- retirement-file: {"id":16,"file":"tests/EventBooking.Api.Tests/StaffIdentityRecorderTests.cs","beforeSha":"2e671dd157b26c6fbe9aef9a778f6adaa310aa95552496b478cf14541adebde2","afterSha":"1286a2a8cf8d035621a8f189ea520b06ecbf679699fa72a9f00bbcc34766ed4f","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

/// <summary>Verifies authenticated identity recording occurs before staff authorization.</summary>
[Collection("api")]
public sealed class StaffIdentityRecorderTests(ApiFactory factory)
{
    /// <summary>Verifies first sight is recorded even when no access profile exists.</summary>
    [Fact]
    public async Task FirstValidIdentityIsRecordedBeforeTheRequestIsForbidden()
    {
        var staffUserId = Guid.NewGuid();
        factory.SignedInAs = staffUserId;
        factory.StaffIdClaim = "u123456";

        var response = await factory.CreateClient().GetAsync("/api/admin/staff-access");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var identity = await database.StaffIdentities.SingleAsync(
            value => value.StaffUserId == staffUserId);
        Assert.Equal("U123456", identity.StaffId.Value);
    }

    /// <summary>Verifies invalid identity data neither records a row nor blocks authenticated-only APIs.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("X123456")]
    public async Task MissingOrMalformedClaimIsNotRecordedButMeRemainsReachable(string? claim)
    {
        var staffUserId = Guid.NewGuid();
        factory.SignedInAs = staffUserId;
        factory.StaffIdClaim = claim;

        var me = await factory.CreateClient().GetAsync("/api/me");
        var staff = await factory.CreateClient().GetAsync("/api/admin/staff-access");

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, staff.StatusCode);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        Assert.False(await database.StaffIdentities.AnyAsync(
            value => value.StaffUserId == staffUserId));
    }

    /// <summary>Verifies the process cache avoids refreshing the row on every request.</summary>
    [Fact]
    public async Task CachedIdentityDoesNotWriteAgain()
    {
        var staffUserId = Guid.NewGuid();
        factory.SignedInAs = staffUserId;
        factory.StaffIdClaim = "N654321";
        var client = factory.CreateClient();

        await client.GetAsync("/api/me");
        DateTimeOffset firstSeen;
        using (var firstScope = factory.Services.CreateScope())
        {
            firstSeen = await firstScope.ServiceProvider
                .GetRequiredService<EventBookingDbContext>()
                .StaffIdentities
                .Where(value => value.StaffUserId == staffUserId)
                .Select(value => value.LastSeenAt)
                .SingleAsync();
        }

        await client.GetAsync("/api/me");

        using var secondScope = factory.Services.CreateScope();
        var secondSeen = await secondScope.ServiceProvider
            .GetRequiredService<EventBookingDbContext>()
            .StaffIdentities
            .Where(value => value.StaffUserId == staffUserId)
            .Select(value => value.LastSeenAt)
            .SingleAsync();
        Assert.Equal(firstSeen, secondSeen);
    }

    /// <summary>Verifies a provider uniqueness conflict is logged without failing the caller's request.</summary>
    [Fact]
    public async Task DuplicateStaffNumberDoesNotFailTheAuthenticatedRequest()
    {
        factory.StaffIdClaim = "U777777";
        factory.SignedInAs = Guid.NewGuid();
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/me")).StatusCode);

        var conflictingUserId = Guid.NewGuid();
        factory.SignedInAs = conflictingUserId;

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        Assert.False(await database.StaffIdentities.AnyAsync(
            value => value.StaffUserId == conflictingUserId));
    }

    /// <summary>Verifies a present name claim is mirrored onto the identity on a cache miss.</summary>
    [Fact]
    public async Task RecorderPassesDisplayNameThroughOnCacheMiss()
    {
        var staffUserId = Guid.NewGuid();
        factory.SignedInAs = staffUserId;
        factory.StaffIdClaim = "U200001";
        factory.NameClaim = "Dana Datson";

        await factory.CreateClient().GetAsync("/api/me");

        Assert.Equal("Dana Datson", await DisplayNameOfAsync(staffUserId));
        factory.NameClaim = null;
    }

    /// <summary>Verifies an absent name claim mirrors no name and never blocks the request.</summary>
    [Fact]
    public async Task RecorderPassesNullDisplayNameWhenClaimAbsent()
    {
        var staffUserId = Guid.NewGuid();
        factory.SignedInAs = staffUserId;
        factory.StaffIdClaim = "U200002";
        factory.NameClaim = null;

        var response = await factory.CreateClient().GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(await DisplayNameOfAsync(staffUserId));
    }

    /// <summary>Verifies a rename lags until the cache expires, exactly like the observation time.</summary>
    [Fact]
    public async Task RecorderSuppressesTheWriteOnACacheHitSoARenameLags()
    {
        var staffUserId = Guid.NewGuid();
        factory.SignedInAs = staffUserId;
        factory.StaffIdClaim = "U200003";
        factory.NameClaim = "Old Name";
        var client = factory.CreateClient();

        await client.GetAsync("/api/me");
        Assert.Equal("Old Name", await DisplayNameOfAsync(staffUserId));

        factory.NameClaim = "New Name";
        await client.GetAsync("/api/me");

        Assert.Equal("Old Name", await DisplayNameOfAsync(staffUserId));
        factory.NameClaim = null;
    }

    private async Task<string?> DisplayNameOfAsync(Guid staffUserId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider
            .GetRequiredService<EventBookingDbContext>()
            .StaffIdentities
            .Where(value => value.StaffUserId == staffUserId)
            .Select(value => value.DisplayName)
            .SingleAsync();
    }
}
`````

## after — tests/EventBooking.Api.Tests/StaffIdentityRecorderTests.cs — 1/1

<!-- retirement-file: {"id":16,"file":"tests/EventBooking.Api.Tests/StaffIdentityRecorderTests.cs","beforeSha":"2e671dd157b26c6fbe9aef9a778f6adaa310aa95552496b478cf14541adebde2","afterSha":"1286a2a8cf8d035621a8f189ea520b06ecbf679699fa72a9f00bbcc34766ed4f","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

/// <summary>Verifies authenticated identity recording occurs before staff authorization.</summary>
[Collection("api")]
public sealed class StaffIdentityRecorderTests(ApiFactory factory)
{
    /// <summary>Verifies first sight is recorded even when no access profile exists.</summary>
    [Fact]
    public async Task FirstValidIdentityIsRecordedBeforeTheRequestIsForbidden()
    {
        var staffUserId = Guid.NewGuid();
        factory.SignedInAs = staffUserId;
        factory.StaffIdClaim = "u123456";

        var response = await factory.CreateClient().GetAsync("/api/admin/staff-access");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var identity = await database.StaffIdentities.SingleAsync(
            value => value.StaffUserId == staffUserId);
        Assert.Equal("U123456", identity.StaffId.Value);
    }

    /// <summary>Verifies invalid identity data neither records a row nor blocks authenticated-only APIs.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("invalid staff id")]
    public async Task MissingOrMalformedClaimIsNotRecordedButMeRemainsReachable(string? claim)
    {
        var staffUserId = Guid.NewGuid();
        factory.SignedInAs = staffUserId;
        factory.StaffIdClaim = claim;

        var me = await factory.CreateClient().GetAsync("/api/me");
        var staff = await factory.CreateClient().GetAsync("/api/admin/staff-access");

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, staff.StatusCode);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        Assert.False(await database.StaffIdentities.AnyAsync(
            value => value.StaffUserId == staffUserId));
    }

    /// <summary>Verifies the process cache avoids refreshing the row on every request.</summary>
    [Fact]
    public async Task CachedIdentityDoesNotWriteAgain()
    {
        var staffUserId = Guid.NewGuid();
        factory.SignedInAs = staffUserId;
        factory.StaffIdClaim = "N654321";
        var client = factory.CreateClient();

        await client.GetAsync("/api/me");
        DateTimeOffset firstSeen;
        using (var firstScope = factory.Services.CreateScope())
        {
            firstSeen = await firstScope.ServiceProvider
                .GetRequiredService<EventBookingDbContext>()
                .StaffIdentities
                .Where(value => value.StaffUserId == staffUserId)
                .Select(value => value.LastSeenAt)
                .SingleAsync();
        }

        await client.GetAsync("/api/me");

        using var secondScope = factory.Services.CreateScope();
        var secondSeen = await secondScope.ServiceProvider
            .GetRequiredService<EventBookingDbContext>()
            .StaffIdentities
            .Where(value => value.StaffUserId == staffUserId)
            .Select(value => value.LastSeenAt)
            .SingleAsync();
        Assert.Equal(firstSeen, secondSeen);
    }

    /// <summary>Verifies a provider uniqueness conflict is logged without failing the caller's request.</summary>
    [Fact]
    public async Task DuplicateStaffNumberDoesNotFailTheAuthenticatedRequest()
    {
        factory.StaffIdClaim = "U777777";
        factory.SignedInAs = Guid.NewGuid();
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/me")).StatusCode);

        var conflictingUserId = Guid.NewGuid();
        factory.SignedInAs = conflictingUserId;

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        Assert.False(await database.StaffIdentities.AnyAsync(
            value => value.StaffUserId == conflictingUserId));
    }

    /// <summary>Verifies a present name claim is mirrored onto the identity on a cache miss.</summary>
    [Fact]
    public async Task RecorderPassesDisplayNameThroughOnCacheMiss()
    {
        var staffUserId = Guid.NewGuid();
        factory.SignedInAs = staffUserId;
        factory.StaffIdClaim = "U200001";
        factory.NameClaim = "Dana Datson";

        await factory.CreateClient().GetAsync("/api/me");

        Assert.Equal("Dana Datson", await DisplayNameOfAsync(staffUserId));
        factory.NameClaim = null;
    }

    /// <summary>Verifies an absent name claim mirrors no name and never blocks the request.</summary>
    [Fact]
    public async Task RecorderPassesNullDisplayNameWhenClaimAbsent()
    {
        var staffUserId = Guid.NewGuid();
        factory.SignedInAs = staffUserId;
        factory.StaffIdClaim = "U200002";
        factory.NameClaim = null;

        var response = await factory.CreateClient().GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(await DisplayNameOfAsync(staffUserId));
    }

    /// <summary>Verifies a rename lags until the cache expires, exactly like the observation time.</summary>
    [Fact]
    public async Task RecorderSuppressesTheWriteOnACacheHitSoARenameLags()
    {
        var staffUserId = Guid.NewGuid();
        factory.SignedInAs = staffUserId;
        factory.StaffIdClaim = "U200003";
        factory.NameClaim = "Old Name";
        var client = factory.CreateClient();

        await client.GetAsync("/api/me");
        Assert.Equal("Old Name", await DisplayNameOfAsync(staffUserId));

        factory.NameClaim = "New Name";
        await client.GetAsync("/api/me");

        Assert.Equal("Old Name", await DisplayNameOfAsync(staffUserId));
        factory.NameClaim = null;
    }

    private async Task<string?> DisplayNameOfAsync(Guid staffUserId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider
            .GetRequiredService<EventBookingDbContext>()
            .StaffIdentities
            .Where(value => value.StaffUserId == staffUserId)
            .Select(value => value.DisplayName)
            .SingleAsync();
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Access/ConfigurableStaffIdTests.cs — 1/1

<!-- retirement-file: {"id":17,"file":"tests/EventBooking.Domain.Tests/Access/ConfigurableStaffIdTests.cs","beforeSha":null,"afterSha":"52a1a1a04bb7791343d62650e70d87282ed4b37e5a3945c686c37b15159b84ce","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Access;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Tests.Access;

public sealed class ConfigurableStaffIdTests
{
    [Theory]
    [InlineData("A10023", "A10023")]
    [InlineData(" a10023 ", "A10023")]
    [InlineData("123", "123")]
    public void Default_format_accepts_and_canonicalises_organisation_neutral_identifiers(string input, string expected)
    {
        Assert.Equal(expected, new StaffId(input).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void Default_format_rejects_missing_or_overlong_identifiers(string input)
    {
        Assert.Throws<DomainException>(() => new StaffId(input));
    }

    [Fact]
    public void Deployment_format_restricts_identifiers_after_normalisation()
    {
        Assert.Equal("U123456", new StaffId(" u123456 ", "^[UN][0-9]{6}$").Value);
        Assert.Throws<DomainException>(() => new StaffId("A10023", "^[UN][0-9]{6}$"));
    }
}
`````

## before — tests/EventBooking.Domain.Tests/Access/StaffIdTests.cs — 1/1

<!-- retirement-file: {"id":18,"file":"tests/EventBooking.Domain.Tests/Access/StaffIdTests.cs","beforeSha":"f47b88a3ffe623c782d30905c6d944208dd17a6d860d8cc4735180fdb9907d16","afterSha":"25a699539c20517ed07a5c538ef582f6a83a7c0992792777d11f161921c5c143","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Access;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Tests.Access;

/// <summary>Verifies canonical enterprise staff-number validation and identity invariants.</summary>
public sealed class StaffIdTests
{
    /// <summary>Verifies accepted values are exposed in the canonical uppercase form.</summary>
    [Theory]
    [InlineData("U000000", "U000000")]
    [InlineData("N999999", "N999999")]
    [InlineData("u123456", "U123456")]
    [InlineData("n654321", "N654321")]
    public void ValidValuesAreCanonicalised(string input, string expected)
    {
        var staffId = new StaffId(input);

        Assert.Equal(expected, staffId.Value);
        Assert.Equal(expected, staffId.ToString());
    }

    /// <summary>Verifies malformed or absent values cannot enter the domain.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" U123456")]
    [InlineData("U123456 ")]
    [InlineData("X123456")]
    [InlineData("U12345")]
    [InlineData("U1234567")]
    [InlineData("U12345A")]
    public void InvalidValuesCannotBeConstructed(string? input)
    {
        Assert.Throws<DomainException>(() => new StaffId(input!));
        Assert.False(StaffId.TryParse(input, out var parsed));
        Assert.Null(parsed);
    }

    /// <summary>Verifies record equality observes canonical rather than input casing.</summary>
    [Fact]
    public void EqualityUsesTheCanonicalValue()
    {
        Assert.Equal(new StaffId("u123456"), new StaffId("U123456"));
    }

    /// <summary>Verifies the identity pair is immutable while its approximate observation advances.</summary>
    [Fact]
    public void StaffIdentityKeepsThePairAndRefreshesLastSeenOnly()
    {
        var userId = Guid.NewGuid();
        var firstSeen = DateTimeOffset.Parse("2026-09-08T09:00:00Z");
        var identity = StaffIdentity.Create(userId, new StaffId("N123456"), null, firstSeen);

        identity.MarkSeen(null, firstSeen.AddHours(1));

        Assert.Equal(userId, identity.StaffUserId);
        Assert.Equal(new StaffId("N123456"), identity.StaffId);
        Assert.Equal(firstSeen.AddHours(1), identity.LastSeenAt);
    }

    /// <summary>Verifies an identity observed without a name claim mirrors no display name.</summary>
    [Fact]
    public void CreateStoresNullDisplayName()
    {
        var identity = StaffIdentity.Create(
            Guid.NewGuid(), new StaffId("U000002"), null, DateTimeOffset.UtcNow);

        Assert.Null(identity.DisplayName);
    }

    /// <summary>Verifies an observed name is mirrored onto the identity.</summary>
    [Fact]
    public void CreateStoresNonNullDisplayName()
    {
        var identity = StaffIdentity.Create(
            Guid.NewGuid(), new StaffId("U000003"), "Dana Datson", DateTimeOffset.UtcNow);

        Assert.Equal("Dana Datson", identity.DisplayName);
    }

    /// <summary>Verifies a later token without a name clears the mirrored value.</summary>
    [Fact]
    public void MarkSeenOverwritesDisplayNameBackToNull()
    {
        var start = DateTimeOffset.UtcNow;
        var identity = StaffIdentity.Create(
            Guid.NewGuid(), new StaffId("U000004"), "Meddy Medson", start);

        identity.MarkSeen(null, start.AddMinutes(1));

        Assert.Null(identity.DisplayName);
    }

    /// <summary>Verifies a rename observed at the next refresh replaces the mirrored value.</summary>
    [Fact]
    public void MarkSeenUpdatesDisplayNameToNewValue()
    {
        var start = DateTimeOffset.UtcNow;
        var identity = StaffIdentity.Create(Guid.NewGuid(), new StaffId("U000005"), "Old Name", start);

        identity.MarkSeen("New Name", start.AddMinutes(1));

        Assert.Equal("New Name", identity.DisplayName);
        Assert.Equal(start.AddMinutes(1), identity.LastSeenAt);
    }

    /// <summary>Verifies the observation time still refuses to move backwards.</summary>
    [Fact]
    public void MarkSeenStillRejectsBackwardsTime()
    {
        var start = DateTimeOffset.UtcNow;
        var identity = StaffIdentity.Create(Guid.NewGuid(), new StaffId("U000006"), "Old Name", start);

        Assert.Throws<DomainException>(() => identity.MarkSeen("New Name", start.AddMinutes(-1)));
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Access/StaffIdTests.cs — 1/1

<!-- retirement-file: {"id":18,"file":"tests/EventBooking.Domain.Tests/Access/StaffIdTests.cs","beforeSha":"f47b88a3ffe623c782d30905c6d944208dd17a6d860d8cc4735180fdb9907d16","afterSha":"25a699539c20517ed07a5c538ef582f6a83a7c0992792777d11f161921c5c143","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Access;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Tests.Access;

/// <summary>Verifies canonical enterprise staff-number validation and identity invariants.</summary>
public sealed class StaffIdTests
{
    /// <summary>Verifies accepted values are exposed in the canonical uppercase form.</summary>
    [Theory]
    [InlineData("U000000", "U000000")]
    [InlineData("N999999", "N999999")]
    [InlineData("u123456", "U123456")]
    [InlineData("n654321", "N654321")]
    [InlineData(" U123456", "U123456")]
    [InlineData("U123456 ", "U123456")]
    [InlineData("X123456", "X123456")]
    [InlineData("U12345", "U12345")]
    [InlineData("U1234567", "U1234567")]
    [InlineData("U12345A", "U12345A")]
    public void ValidValuesAreCanonicalised(string input, string expected)
    {
        var staffId = new StaffId(input);

        Assert.Equal(expected, staffId.Value);
        Assert.Equal(expected, staffId.ToString());
    }

    /// <summary>Verifies malformed or absent values cannot enter the domain.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("bad-id")]
    [InlineData("A A")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void InvalidValuesCannotBeConstructed(string? input)
    {
        Assert.Throws<DomainException>(() => new StaffId(input!));
        Assert.False(StaffId.TryParse(input, out var parsed));
        Assert.Null(parsed);
    }

    /// <summary>Verifies record equality observes canonical rather than input casing.</summary>
    [Fact]
    public void EqualityUsesTheCanonicalValue()
    {
        Assert.Equal(new StaffId("u123456"), new StaffId("U123456"));
    }

    /// <summary>Verifies the identity pair is immutable while its approximate observation advances.</summary>
    [Fact]
    public void StaffIdentityKeepsThePairAndRefreshesLastSeenOnly()
    {
        var userId = Guid.NewGuid();
        var firstSeen = DateTimeOffset.Parse("2026-09-08T09:00:00Z");
        var identity = StaffIdentity.Create(userId, new StaffId("N123456"), null, firstSeen);

        identity.MarkSeen(null, firstSeen.AddHours(1));

        Assert.Equal(userId, identity.StaffUserId);
        Assert.Equal(new StaffId("N123456"), identity.StaffId);
        Assert.Equal(firstSeen.AddHours(1), identity.LastSeenAt);
    }

    /// <summary>Verifies an identity observed without a name claim mirrors no display name.</summary>
    [Fact]
    public void CreateStoresNullDisplayName()
    {
        var identity = StaffIdentity.Create(
            Guid.NewGuid(), new StaffId("U000002"), null, DateTimeOffset.UtcNow);

        Assert.Null(identity.DisplayName);
    }

    /// <summary>Verifies an observed name is mirrored onto the identity.</summary>
    [Fact]
    public void CreateStoresNonNullDisplayName()
    {
        var identity = StaffIdentity.Create(
            Guid.NewGuid(), new StaffId("U000003"), "Dana Datson", DateTimeOffset.UtcNow);

        Assert.Equal("Dana Datson", identity.DisplayName);
    }

    /// <summary>Verifies a later token without a name clears the mirrored value.</summary>
    [Fact]
    public void MarkSeenOverwritesDisplayNameBackToNull()
    {
        var start = DateTimeOffset.UtcNow;
        var identity = StaffIdentity.Create(
            Guid.NewGuid(), new StaffId("U000004"), "Meddy Medson", start);

        identity.MarkSeen(null, start.AddMinutes(1));

        Assert.Null(identity.DisplayName);
    }

    /// <summary>Verifies a rename observed at the next refresh replaces the mirrored value.</summary>
    [Fact]
    public void MarkSeenUpdatesDisplayNameToNewValue()
    {
        var start = DateTimeOffset.UtcNow;
        var identity = StaffIdentity.Create(Guid.NewGuid(), new StaffId("U000005"), "Old Name", start);

        identity.MarkSeen("New Name", start.AddMinutes(1));

        Assert.Equal("New Name", identity.DisplayName);
        Assert.Equal(start.AddMinutes(1), identity.LastSeenAt);
    }

    /// <summary>Verifies the observation time still refuses to move backwards.</summary>
    [Fact]
    public void MarkSeenStillRejectsBackwardsTime()
    {
        var start = DateTimeOffset.UtcNow;
        var identity = StaffIdentity.Create(Guid.NewGuid(), new StaffId("U000006"), "Old Name", start);

        Assert.Throws<DomainException>(() => identity.MarkSeen("New Name", start.AddMinutes(-1)));
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/ConfigurableStaffIdPersistenceTests.cs — 1/1

<!-- retirement-file: {"id":19,"file":"tests/EventBooking.Infrastructure.Tests/ConfigurableStaffIdPersistenceTests.cs","beforeSha":null,"afterSha":"1fb4a3d08e0db0fec8f4ff4813cdc7d8006e2ed794c5479a99d174a00945cadf","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Access;
using EventBooking.Infrastructure.Persistence.Repositories;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public sealed class ConfigurableStaffIdPersistenceTests(PostgresFixture fixture)
{
    [Theory]
    [InlineData("A10023", "^[A-Z0-9]{1,32}$")]
    [InlineData("ORG-10023", "^ORG-[0-9]{5}$")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "^[A-Z0-9]{1,32}$")]
    public async Task Validated_staff_number_round_trips_without_the_retired_seven_character_constraint(string value, string pattern)
    {
        await fixture.ResetAsync();
        var id = Guid.NewGuid();
        var staffId = new StaffId(value, pattern);
        await using (var write = fixture.NewContext())
            await new StaffIdentityRepository(write).UpsertAsync(id, staffId, null,
                DateTimeOffset.Parse("2026-09-20T09:00:00Z"), CancellationToken.None);
        await using var read = fixture.NewContext();
        var identity = await new StaffIdentityRepository(read).GetByStaffIdAsync(staffId, CancellationToken.None);
        Assert.NotNull(identity);
        Assert.Equal(id, identity.StaffUserId);
        Assert.Equal(value, identity.StaffId.Value);
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/StaffIdentityPersistenceTests.cs — 1/1

<!-- retirement-file: {"id":20,"file":"tests/EventBooking.Infrastructure.Tests/StaffIdentityPersistenceTests.cs","beforeSha":"b47ceace9ff92c36152f48bcd17d3e2dbd10e1a25f6915548c239bb9df2479aa","afterSha":"3406c42e5e68cef44a575d95f72ff6da9dfa9f5d2dacaf5a25921044bc354da4","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Access;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies the database contract for the provider identity mirror.</summary>
[Collection("postgres")]
public sealed class StaffIdentityPersistenceTests(PostgresFixture fixture)
{
    /// <summary>Verifies a canonical staff number resolves its provider identity.</summary>
    [Fact]
    public async Task IdentityRoundTripsAndIsFoundByCanonicalStaffId()
    {
        await fixture.ResetAsync();
        var userId = Guid.NewGuid();
        await using (var write = fixture.NewContext())
        {
            await new StaffIdentityRepository(write).UpsertAsync(
                userId,
                new StaffId("u123456"),
                null,
                DateTimeOffset.Parse("2026-09-08T10:00:00Z"),
                CancellationToken.None);
        }

        await using var read = fixture.NewContext();
        var actual = await new StaffIdentityRepository(read)
            .GetByStaffIdAsync(new StaffId("U123456"), CancellationToken.None);

        Assert.Equal(userId, actual!.StaffUserId);
        Assert.Equal("U123456", actual.StaffId.Value);
    }

    /// <summary>Verifies a repeated provider key atomically refreshes the mirrored observation.</summary>
    [Fact]
    public async Task UpsertRefreshesTheExistingProviderIdentity()
    {
        await fixture.ResetAsync();
        var userId = Guid.NewGuid();
        await using var write = fixture.NewContext();
        var repository = new StaffIdentityRepository(write);
        var first = DateTimeOffset.Parse("2026-09-08T10:00:00Z");

        await repository.UpsertAsync(
            userId, new StaffId("U123456"), null, first, CancellationToken.None);
        await repository.UpsertAsync(
            userId, new StaffId("U123456"), null, first.AddHours(1), CancellationToken.None);

        await using var read = fixture.NewContext();
        var identities = await new StaffIdentityRepository(read).ListAsync(CancellationToken.None);
        var identity = Assert.Single(identities);
        Assert.Equal(first.AddHours(1), identity.LastSeenAt);
    }

    /// <summary>Verifies the database backstops accept lowercase shape but reject bad or reused values.</summary>
    [Fact]
    public async Task DatabaseAcceptsLowercaseButRejectsMalformedAndDuplicateStaffIds()
    {
        await fixture.ResetAsync();
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using (var valid = connection.CreateCommand())
        {
            valid.CommandText = "INSERT INTO staff_identity (staff_user_id, staff_id, last_seen_at) VALUES (@user, 'u123456', now())";
            valid.Parameters.AddWithValue("user", Guid.NewGuid());
            await valid.ExecuteNonQueryAsync();
        }

        var invalid = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO staff_identity (staff_user_id, staff_id, last_seen_at) VALUES (@user, 'X123456', now())";
            command.Parameters.AddWithValue("user", Guid.NewGuid());
            await command.ExecuteNonQueryAsync();
        });
        Assert.Equal(PostgresErrorCodes.CheckViolation, invalid.SqlState);

        var duplicate = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO staff_identity (staff_user_id, staff_id, last_seen_at) VALUES (@user, 'u123456', now())";
            command.Parameters.AddWithValue("user", Guid.NewGuid());
            await command.ExecuteNonQueryAsync();
        });
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
    }

    /// <summary>Verifies an observed name round-trips through the upsert and the listing.</summary>
    [Fact]
    public async Task UpsertWritesAndReadsBackDisplayName()
    {
        await fixture.ResetAsync();
        var userId = Guid.NewGuid();

        await using (var write = fixture.NewContext())
        {
            await new StaffIdentityRepository(write).UpsertAsync(
                userId,
                new StaffId("U000002"),
                "Dana Datson",
                DateTimeOffset.Parse("2026-09-08T10:00:00Z"),
                CancellationToken.None);
        }

        await using var read = fixture.NewContext();
        var identities = await new StaffIdentityRepository(read).ListAsync(CancellationToken.None);

        var identity = Assert.Single(identities);
        Assert.Equal("Dana Datson", identity.DisplayName);
    }

    /// <summary>Verifies a later token without a name clears the stored value.</summary>
    [Fact]
    public async Task UpsertOverwritesDisplayNameBackToNull()
    {
        await fixture.ResetAsync();
        var userId = Guid.NewGuid();
        var first = DateTimeOffset.Parse("2026-09-08T10:00:00Z");

        await using (var write = fixture.NewContext())
        {
            var repository = new StaffIdentityRepository(write);
            await repository.UpsertAsync(
                userId, new StaffId("U000003"), "Dana Datson", first, CancellationToken.None);
            await repository.UpsertAsync(
                userId, new StaffId("U000003"), null, first.AddHours(1), CancellationToken.None);
        }

        await using var read = fixture.NewContext();
        var identity = await new StaffIdentityRepository(read)
            .GetByStaffIdAsync(new StaffId("U000003"), CancellationToken.None);

        Assert.NotNull(identity);
        Assert.Null(identity!.DisplayName);
        Assert.Equal("U000003", identity.StaffId.Value);
        Assert.Equal(first.AddHours(1), identity.LastSeenAt);
    }

    /// <summary>Verifies an identity observed without a name stores and reads back a null column.</summary>
    [Fact]
    public async Task UpsertNullDisplayNameRoundTrips()
    {
        await fixture.ResetAsync();
        var userId = Guid.NewGuid();

        await using (var write = fixture.NewContext())
        {
            await new StaffIdentityRepository(write).UpsertAsync(
                userId,
                new StaffId("U000004"),
                null,
                DateTimeOffset.Parse("2026-09-08T10:00:00Z"),
                CancellationToken.None);
        }

        await using var read = fixture.NewContext();
        var identity = await new StaffIdentityRepository(read)
            .GetByStaffIdAsync(new StaffId("U000004"), CancellationToken.None);

        Assert.NotNull(identity);
        Assert.Equal(userId, identity!.StaffUserId);
        Assert.Null(identity.DisplayName);
    }

    /// <summary>Verifies a rename observed at the next refresh replaces the stored value.</summary>
    [Fact]
    public async Task UpsertReplacesAnEarlierDisplayName()
    {
        await fixture.ResetAsync();
        var userId = Guid.NewGuid();
        var first = DateTimeOffset.Parse("2026-09-08T10:00:00Z");

        await using (var write = fixture.NewContext())
        {
            var repository = new StaffIdentityRepository(write);
            await repository.UpsertAsync(
                userId, new StaffId("U000005"), "Old Name", first, CancellationToken.None);
            await repository.UpsertAsync(
                userId, new StaffId("U000005"), "New Name", first.AddHours(1), CancellationToken.None);
        }

        await using var read = fixture.NewContext();
        var identity = await new StaffIdentityRepository(read)
            .GetByStaffIdAsync(new StaffId("U000005"), CancellationToken.None);

        Assert.Equal("New Name", identity!.DisplayName);
    }
}
`````
