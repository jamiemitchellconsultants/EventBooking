# 07a — Confirmation burst (Task 32)

[← Phase overview](phase-7-verification-and-documentation.md) · [Previous task](phase-6d-images-and-release.md) · [Ontology](../ontology.md)

This task is the release verification layer. It creates a separate load-only seed fixture, records
the capacity-lock hold interval, and tests 500 simultaneous confirmations against the Task 29
local stack. The fixture and any tokens it writes are disposable.

> Use superpowers:executing-plans. Apply this document after Task 31 on the Phase 7 branch.

**Goal:** Make NFR-P3 and the 100-of-500 booking outcome a repeatable release gate.

**Architecture:** The existing SeedData CLI gains a guarded fixture mode after its regular demo
seed. It creates one single-type event with 100 places and 500 distinct pending invitations;
ordinary demo events keep their smaller capacities. The load script uses one k6 virtual user per
invitation and asserts the outcome counts. The API exposes a metric emitted from confirmation
through a layer-safe Application port.

**Tech Stack:** .NET 10, EF Core 10, PostgreSQL 16, Docker Compose, k6 and Bash.

**Spec:** [Master Task 32](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[NFR-P3 and verification](../design/08-nonfunctional-requirements.md#verification),
[Task 28 seed CLI](phase-6a-seed-cli.md) and [Task 29 Compose](phase-6b-local-compose.md).

### Task 32: Load fixture, capacity-lock measurement and 500-way confirmation burst

**Files:**

- Modify: src/EventBooking.SeedData/SeedCommand.cs
- Modify: src/EventBooking.SeedData/SeedRunSteps.cs
- Modify: src/EventBooking.SeedData/Program.cs
- Create: src/EventBooking.SeedData/LoadFixtureSeeder.cs
- Create: src/EventBooking.Application/Abstractions/ICapacityLockHoldObserver.cs
- Modify: src/EventBooking.Application/Bookings/ConfirmBookingHandler.cs
- Modify: src/EventBooking.Api/Observability/EventBookingMetrics.cs
- Modify: src/EventBooking.Api/Observability/PrometheusText.cs
- Modify: src/EventBooking.Api/Program.cs
- Create: tests/load/confirm-burst.js
- Create: tests/load/compose.load.yml
- Create: tests/load/run.sh
- Create: tests/load/.gitignore
- Create: tests/load/README.md
- Test: tests/EventBooking.SeedData.Tests/LoadFixtureTests.cs
- Modify: tests/EventBooking.SeedData.Tests/SeedCommandTests.cs
- Test: tests/EventBooking.Application.Tests/Bookings/CapacityLockHoldTests.cs
- Test: tests/EventBooking.Api.Tests/Observability/CapacityLockHoldHistogramTests.cs

**Interfaces:**

```csharp
namespace EventBooking.SeedData;

public sealed record SeedCliOptions(
    string ConnectionString, bool Demo, bool Reanchor, bool Reseed, bool Verbose,
    bool LoadFixture, string? LoadFixturePath)
{
    // Rejects an unguarded load fixture before roles, migrations, Keycloak or SMTP can run.
    public static SeedCliOptions Parse(string[] args, Func<string, string?> environment) =>
        throw new NotImplementedException(); // Complete body in Step 2.
}

public interface ISeedRunSteps : IAsyncDisposable
{
    // Existing members from Task 28 remain unchanged.
    // Creates a 500-invitation fixture and writes tokens to the absolute, ignored path.
    Task<int> SeedLoadFixtureAsync(string outputPath, CancellationToken ct);
}

public sealed record LoadFixtureManifest(Guid EventId, IReadOnlyList<string> BookTokens);

public sealed class LoadFixtureSeeder
{
    // Requires Task 28's demo reference data; never sends 500 emails.
    public Task<LoadFixtureManifest> RunAsync(CancellationToken ct) =>
        throw new NotImplementedException(); // Complete body in Step 2.
}
```

```csharp
namespace EventBooking.Application.Abstractions;

public interface ICapacityLockHoldObserver
{
    // Called once after the capacity lock is acquired and the transaction releases it.
    void Record(TimeSpan elapsed);
}
```

```csharp
namespace EventBooking.Api.Observability;

public sealed class EventBookingMetrics : ICapacityLockHoldObserver, IDisposable
{
    // The existing EventBooking.Api meter gains a capacity-lock duration histogram.
    public Histogram<double> CapacityLockHoldDuration { get; }

    // Records seconds without attendee, event or token labels.
    public void Record(TimeSpan elapsed) => throw new NotImplementedException();
    public void Dispose() => throw new NotImplementedException();
}
```

- [ ] **Step 1: Write the failing fixture tests**

Create this complete test file. The first focused run fails because the guarded CLI mode and
fixture seeder do not yet exist. It uses the same PostgreSQL Testcontainers setup as Task 28's
DemoSeederIntegrationTests and does not depend on Keycloak or SMTP.

```csharp
// tests/EventBooking.SeedData.Tests/LoadFixtureTests.cs (complete)
using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Invites;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Audit;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Tokens;
using EventBooking.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace EventBooking.SeedData.Tests;

[Collection("seed-anchor")]
public sealed class LoadFixtureTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly CapturingTransport _mail = new();
    private ServiceProvider _services = null!;

    public async Task InitializeAsync()
    {
        DemoSeedSpec.OverrideAnchor(new DateOnly(2026, 9, 22));
        await _postgres.StartAsync();
        var registrations = new ServiceCollection();
        registrations.AddLogging();
        registrations.AddEventBookingInfrastructure(_postgres.GetConnectionString(),
            new TokenOptions("load-test-signing-key-at-least-thirty-two-bytes"));
        registrations.AddEventBookingApplication(
            new AttendeePortalOptions("http://localhost:5002", "events@example.test"),
            new EventBooking.Application.Access.StaffIdPolicy("^[A-Z0-9]{1,32}$"));
        registrations.AddSingleton<IClock>(new FixedClock(
            new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero)));
        registrations.AddSingleton<IEmailTransport>(_mail);
        registrations.AddScoped<IAuditLogger, EfAuditLogger>();
        registrations.AddSingleton<OutboxDispatcher>();
        registrations.AddScoped<DemoSeeder>();
        registrations.AddScoped<LoadFixtureSeeder>();
        _services = registrations.BuildServiceProvider();
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        await db.Database.ExecuteSqlRawAsync(DatabaseRoles.Script);
        await db.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<DemoSeeder>().RunAsync(default);
    }

    public async Task DisposeAsync()
    {
        DemoSeedSpec.OverrideAnchor(null);
        await _services.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public void Guard_rejects_load_mode_before_any_side_effect()
    {
        var connection = _postgres.GetConnectionString();
        Assert.Throws<SeedException>(() => SeedCliOptions.Parse(
            [connection, "--load-fixture"], _ => null));
        Assert.Throws<SeedException>(() => SeedCliOptions.Parse(
            [connection, "--demo", "--load-fixture"],
            key => key == "EVENTBOOKING_ENABLE_LOAD_FIXTURE" ? "true" : null));
        var options = SeedCliOptions.Parse([connection, "--demo", "--load-fixture"], key =>
            key switch
            {
                "EVENTBOOKING_ENABLE_LOAD_FIXTURE" => "true",
                "EVENTBOOKING_LOAD_FIXTURE_PATH" => "/load/fixture.json",
                _ => null,
            });
        Assert.True(options.LoadFixture);
        Assert.Equal("/load/fixture.json", options.LoadFixturePath);
    }

    [Fact]
    public async Task Fixture_has_one_100_place_event_and_500_distinct_pending_invites()
    {
        await using var scope = _services.CreateAsyncScope();
        var fixture = await scope.ServiceProvider.GetRequiredService<LoadFixtureSeeder>()
            .RunAsync(default);
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var target = await db.Events.Include(x => x.Capacities)
            .SingleAsync(x => x.Id == fixture.EventId);
        Assert.Single(target.Capacities);
        Assert.Equal(100, target.Capacities[0].TotalHeadcount);
        Assert.Equal(100, target.Capacities[0].RemainingCapacity);
        Assert.Equal(500, fixture.BookTokens.Count);
        Assert.Equal(500, fixture.BookTokens.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(500, fixture.BookTokens.Select(x => x[..12]).Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(_mail.Recipients);

        var invites = await db.Invites.Include(x => x.Options).ToListAsync();
        var fixtureInviteIds = fixture.BookTokens.Select(token =>
        {
            Assert.True(tokens.TryRead(token, out var reference));
            return reference.EntityId;
        }).ToHashSet();
        invites = invites.Where(x => fixtureInviteIds.Contains(x.Id)).ToList();
        Assert.Equal(500, invites.Count);
        Assert.All(invites, invite =>
        {
            Assert.Equal(InviteStatus.Pending, invite.Status);
            Assert.True(invite.Offers(fixture.EventId));
            Assert.Equal(3, invite.Options.Count);
        });
        foreach (var token in fixture.BookTokens)
        {
            Assert.True(tokens.TryRead(token, out var reference));
            Assert.Contains(invites, invite => invite.Id == reference.EntityId);
        }
        await Assert.ThrowsAsync<SeedException>(() => scope.ServiceProvider
            .GetRequiredService<LoadFixtureSeeder>().RunAsync(default));
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class CapturingTransport : IEmailTransport
    {
        public List<string> Recipients { get; } = [];
        public Task<EmailSendOutcome> SendAsync(
            string recipient, string subject, string textBody, string htmlBody, CancellationToken ct)
        {
            Recipients.Add(recipient);
            return Task.FromResult(EmailSendOutcome.Sent);
        }
    }
}
```

Run:

```bash
dotnet test tests/EventBooking.SeedData.Tests --filter FullyQualifiedName~LoadFixtureTests
```

Expected: FAIL because SeedCliOptions has no guarded load mode and LoadFixtureSeeder does not
exist. If an earlier task does not compile, investigate its independent failure first.

- [ ] **Step 2: Add the guarded seed fixture mode**

Replace SeedCommand with the complete version below. It preserves Task 28's safe default and
destructive reseed guard. The load mode runs only after migrations and a successful regular demo
seed; it never makes load fixture generation the default.

```csharp
// src/EventBooking.SeedData/SeedCommand.cs (complete)
namespace EventBooking.SeedData;

public sealed record SeedCliOptions(
    string ConnectionString, bool Demo, bool Reanchor, bool Reseed, bool Verbose,
    bool LoadFixture, string? LoadFixturePath)
{
    public static SeedCliOptions Parse(string[] args, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(environment);
        var values = args.Where(x => !x.StartsWith('-')).ToList();
        if (values.Count > 1)
            throw new SeedException("Supply exactly one PostgreSQL connection string.");
        var connection = values.SingleOrDefault() ?? environment("ConnectionStrings__EventBooking");
        if (string.IsNullOrWhiteSpace(connection))
            throw new SeedException("A PostgreSQL connection string argument or ConnectionStrings__EventBooking is required.");
        var known = new HashSet<string>(StringComparer.Ordinal)
            { "--demo", "--reanchor", "--reseed", "--verbose", "--load-fixture" };
        var unknown = args.Where(x => x.StartsWith('-') && !known.Contains(x)).ToList();
        if (unknown.Count > 0)
            throw new SeedException($"Unknown option '{unknown[0]}'.");
        var demo = args.Contains("--demo", StringComparer.Ordinal);
        var reanchor = args.Contains("--reanchor", StringComparer.Ordinal);
        var reseed = args.Contains("--reseed", StringComparer.Ordinal);
        var load = args.Contains("--load-fixture", StringComparer.Ordinal);
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
        return new SeedCliOptions(connection, demo, reanchor, reseed,
            args.Contains("--verbose", StringComparer.Ordinal), load, path);
    }
}

public interface ISeedRunSteps : IAsyncDisposable
{
    Task ApplyDatabaseRolesAsync(CancellationToken ct);
    Task ApplyMigrationsAsync(CancellationToken ct);
    Task ReanchorToTodayAsync(CancellationToken ct);
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
        await steps.ApplyDatabaseRolesAsync(ct);
        await steps.ApplyMigrationsAsync(ct);
        if (!options.Demo)
        {
            await output.WriteLineAsync("Database roles and migrations applied; demo data was not requested.");
            return 0;
        }
        if (options.Reanchor) await steps.ReanchorToTodayAsync(ct);
        var keycloak = await steps.ConvergeKeycloakAsync(options.Reseed, ct);
        var summary = await steps.SeedDemoAsync(options.Reseed, ct);
        var sent = await steps.SendDemoInvitationsAsync(ct);
        await output.WriteLineAsync(
            $"Demo seed complete: {summary.LocationsEnsured} locations, " +
            $"{summary.AppointmentTypesEnsured} appointment types, {summary.AttendeesEnsured} attendees; " +
            $"{sent} invitations sent; Keycloak {(keycloak is null ? "not configured" : "converged")}.");
        if (options.LoadFixture)
        {
            var count = await steps.SeedLoadFixtureAsync(options.LoadFixturePath!, ct);
            await output.WriteLineAsync($"Load fixture ready: {count} invitations.");
        }
        return 0;
    }
}
```

Replace Program's usage string; keep the parse-before-SeedRunSteps.Create order:

```csharp
// src/EventBooking.SeedData/Program.cs (complete)
using EventBooking.SeedData;

try
{
    var options = SeedCliOptions.Parse(args, Environment.GetEnvironmentVariable);
    await using var steps = SeedRunSteps.Create(options, Environment.GetEnvironmentVariable);
    return await SeedCommand.RunAsync(options, steps, Console.Out, CancellationToken.None);
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Seed failed: {exception.Message}");
    Console.Error.WriteLine(
        "Usage: EventBooking.SeedData <connection-string> [--demo [--reanchor] [--reseed] [--load-fixture]] [--verbose]");
    return 2;
}
```

In SeedRunSteps.Create's existing `if (options.Demo)` branch, register LoadFixtureSeeder next to
DemoSeeder. Add the following method and import System.Text.Json. The existing methods and
constructor remain exactly as Task 28 wrote them; this is the complete addition to that file.

```csharp
// src/EventBooking.SeedData/SeedRunSteps.cs — complete Task 32 additions
services.AddScoped<LoadFixtureSeeder>();

public async Task<int> SeedLoadFixtureAsync(string outputPath, CancellationToken ct)
{
    if (File.Exists(outputPath))
        throw new SeedException("Load fixture output already exists; use a fresh disposable project.");
    await using var scope = Scope();
    var fixture = await scope.ServiceProvider.GetRequiredService<LoadFixtureSeeder>().RunAsync(ct);
    await using var stream = new FileStream(outputPath, new FileStreamOptions
    {
        Mode = FileMode.CreateNew,
        Access = FileAccess.Write,
        UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
    });
    await JsonSerializer.SerializeAsync(
        stream, fixture, new JsonSerializerOptions(JsonSerializerDefaults.Web), ct);
    return fixture.BookTokens.Count;
}
```

Add this exact member to the RecordingSteps fake in Task 28's SeedCommandTests. It preserves
the old tests and lets the new CLI branch assert the fixture call without touching a database.

```csharp
// tests/EventBooking.SeedData.Tests/SeedCommandTests.cs — add to RecordingSteps
public Task<int> SeedLoadFixtureAsync(string outputPath, CancellationToken ct)
{
    Calls.Add($"load:{outputPath}");
    return Task.FromResult(500);
}
```

Create the complete fixture seeder. Task 28's IND_ONLY group and two future London events with
IND are the two alternative options. The dedicated event is another London event 28 local days
ahead, with IND as its one shared type and headcount 100. Existing fixture rows cause a refusal:
re-run from a fresh load-only Compose project after a burst, never append 500 more invites.

```csharp
// src/EventBooking.SeedData/LoadFixtureSeeder.cs (complete)
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Events;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Time;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.SeedData;

public sealed record LoadFixtureManifest(Guid EventId, IReadOnlyList<string> BookTokens);

public sealed class LoadFixtureSeeder(
    EventBookingDbContext database,
    IAttendeeGroupRepository groups,
    ITokenService tokens,
    IClock clock,
    IEventWindowZones zones)
{
    private static readonly Guid ProposalId = Guid.Parse("80000000-0000-0000-0000-000000000001");
    private static readonly Guid EventId = Guid.Parse("80000000-0000-0000-0000-000000000002");

    public async Task<LoadFixtureManifest> RunAsync(CancellationToken ct)
    {
        if (await database.Events.AnyAsync(x => x.Id == EventId, ct)
            || await database.Attendees.AnyAsync(x => x.Email.StartsWith("load.attendee."), ct))
            throw new SeedException("The load fixture already exists; recreate the disposable load stack.");

        var location = await database.Locations.SingleAsync(x => x.Code == "LONDON", ct);
        var type = await database.AppointmentTypes.SingleAsync(x => x.Code == "IND", ct);
        var group = await groups.GetByCodeAsync("IND_ONLY", ct)
            ?? throw new SeedException("The IND_ONLY demo group is missing; run --demo first.");
        if (!location.IsActive || !type.IsActive || group.RequiredAppointmentTypeIds.Count != 1
            || group.RequiredAppointmentTypeIds[0] != type.Id)
            throw new SeedException("The IND_ONLY load fixture inputs have changed.");

        var data = DemoSeedSpec.Build();
        var alternativeIds = data.Events
            .Where(x => x.LocationCode == "LONDON" && x.TypeCodes.Contains("IND"))
            .Take(2).Select(x => x.Id).ToArray();
        if (alternativeIds.Length != 2 || await database.Events
            .CountAsync(x => alternativeIds.Contains(x.Id), ct) != 2)
            throw new SeedException("Two future London IND alternatives are required.");

        var date = zones.LocalDateOf(clock.UtcNow, location.TimeZoneId).AddDays(28);
        var window = new EventWindow(date, new TimeOnly(10, 0), 60);
        var manager = DemoSeedSpec.ManagerForType()[type.Id];
        var proposal = EventProposal.Propose(
            ProposalId, location.Id, true, location.TimeZoneId, window, zones, clock.UtcNow,
            [new ProposableAppointmentType(type.Id, type.Code, true, true)],
            type.Id, manager, 100);
        var eventItem = Event.CreateFrom(EventId, proposal);
        await using var transaction = await database.Database.BeginTransactionAsync(ct);
        database.EventProposals.Add(proposal);
        database.Events.Add(eventItem);
        await database.SaveChangesAsync(ct);

        var bookTokens = new List<string>(500);
        for (var number = 1; number <= 500; number++)
        {
            var attendee = Attendee.Create(
                Id(81, number), $"Load participant {number}",
                $"load.attendee.{number:000}@example.test", group, clock.UtcNow);
            var invite = Invite.CreateInitial(
                InviteId(number), attendee.Id, clock.UtcNow.AddDays(7), [location.Id],
                [EventId, .. alternativeIds], group.RequiredAppointmentTypeIds, 0);
            attendee.MarkInvited(clock.UtcNow);
            database.Attendees.Add(attendee);
            database.Invites.Add(invite);
            bookTokens.Add(tokens.Issue(TokenPurpose.Book, invite.Id, invite.TokenVersion));
        }
        await database.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new LoadFixtureManifest(EventId, bookTokens);
    }

    private static Guid Id(int family, int number) =>
        Guid.Parse($"{family:00}000000-0000-0000-0000-{number:000000000000}");

    // Task 21 partitions attendee-token requests on the first 12 encoded characters. Put the
    // sequence in the first GUID segment so all 500 tokens land in separate 10/min partitions.
    private static Guid InviteId(int number) =>
        Guid.Parse($"{number:00000000}-0000-0000-0000-820000000000");
}
```

Run the focused fixture test again. Expected: PASS and one event with exactly 100 remaining IND
places, 500 distinct pending invitations and no fixture email delivery. Record the observed test
count only when the executor has run it.

- [ ] **Step 3: Write failing lock-duration and exposition tests**

Add these focused tests before production changes. The first proves one interval is observed
after a transaction that acquired a capacity lock; the second proves Prometheus exposes
cumulative buckets, a count and a sum. A histogram rendered as the Task 21 running sum cannot
support the NFR-P3 percentile.

```csharp
// tests/EventBooking.Application.Tests/Bookings/CapacityLockHoldTests.cs (complete)
using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Tests.Bookings;
using Xunit;

namespace EventBooking.Application.Tests.Bookings;

public sealed class CapacityLockHoldTests
{
    [Fact]
    public async Task Successful_confirmation_records_one_post_lock_interval()
    {
        var fixture = BookingFixture.Create();
        var (_, inviteId) = fixture.InviteAttendee("IND");
        var invite = fixture.Invites.Items.Single(x => x.Id == inviteId);
        var observer = new RecordingObserver();
        var handler = Handler(fixture, observer);
        var outcome = await handler.HandleAsync(
            new ConfirmBookingCommand(fixture.BookTokenFor(inviteId, invite.TokenVersion), fixture.EventId), default);
        Assert.True(outcome.IsSuccess);
        Assert.Single(observer.Intervals);
        Assert.True(observer.Intervals[0] >= TimeSpan.Zero);
    }

    [Fact]
    public async Task Capacity_refusal_still_records_one_interval()
    {
        var fixture = BookingFixture.Create();
        fixture.Occupy("IND", 10);
        var (_, inviteId) = fixture.InviteAttendee("IND");
        var invite = fixture.Invites.Items.Single(x => x.Id == inviteId);
        var observer = new RecordingObserver();
        var handler = Handler(fixture, observer);
        var outcome = await handler.HandleAsync(
            new ConfirmBookingCommand(fixture.BookTokenFor(inviteId, invite.TokenVersion), fixture.EventId), default);
        Assert.False(outcome.IsSuccess);
        Assert.Single(observer.Intervals);
    }

    [Fact]
    public async Task Invalid_token_never_records_an_interval()
    {
        var fixture = BookingFixture.Create();
        var observer = new RecordingObserver();
        var handler = Handler(fixture, observer);
        var outcome = await handler.HandleAsync(
            new ConfirmBookingCommand("invalid", fixture.EventId), default);
        Assert.False(outcome.IsSuccess);
        Assert.Empty(observer.Intervals);
    }

    private sealed class RecordingObserver : ICapacityLockHoldObserver
    {
        public List<TimeSpan> Intervals { get; } = [];
        public void Record(TimeSpan elapsed) => Intervals.Add(elapsed);
    }

    private static ConfirmBookingHandler Handler(BookingFixture fixture, ICapacityLockHoldObserver observer) =>
        new(fixture.Invites, fixture.Attendees, fixture.Events, fixture.Capacities,
            fixture.Bookings, fixture.Appointments, fixture.Locations, fixture.Tokens,
            fixture.Emails, fixture.UnitOfWork, fixture.Audit, fixture.Clock,
            BookingTestZones.Instance, observer);
}
```

Task 15's BookingFixture and BookingTestZones are public test helpers in the same namespace;
the test uses their current factory and direct-construction shape.

```csharp
// tests/EventBooking.Api.Tests/Observability/CapacityLockHoldHistogramTests.cs (complete)
using EventBooking.Api.Observability;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventBooking.Api.Tests.Observability;

public sealed class CapacityLockHoldHistogramTests
{
    [Fact]
    public void Lock_duration_has_cumulative_50ms_bucket_count_and_sum()
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        using var provider = services.BuildServiceProvider();
        using var exposition = new PrometheusText();
        using var metrics = new EventBookingMetrics(
            provider.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>());
        metrics.Record(TimeSpan.FromMilliseconds(20));
        metrics.Record(TimeSpan.FromMilliseconds(75));
        var output = exposition.Render();
        Assert.Contains("# TYPE eventbooking_capacity_lock_hold_duration_seconds histogram", output);
        Assert.Contains(
            "eventbooking_capacity_lock_hold_duration_seconds_bucket{le=\"0.049999\"} 1", output);
        Assert.Contains(
            "eventbooking_capacity_lock_hold_duration_seconds_bucket{le=\"+Inf\"} 2", output);
        Assert.Contains("eventbooking_capacity_lock_hold_duration_seconds_count 2", output);
        Assert.Contains("eventbooking_capacity_lock_hold_duration_seconds_sum ", output);
    }
}
```

Run both focused suites. Expected: FAIL because the port, instrument and histogram buckets do
not exist. If a prior phase cannot compile, fix that separately before calling these tests red.

- [ ] **Step 4: Implement the Application timing port and API instrument**

Create this port. It is an observability contract, not a domain entity; do not change the ontology.

```csharp
// src/EventBooking.Application/Abstractions/ICapacityLockHoldObserver.cs (complete)
namespace EventBooking.Application.Abstractions;

public interface ICapacityLockHoldObserver
{
    void Record(TimeSpan elapsed);
}
```

Change ConfirmBookingHandler's constructor to append
`ICapacityLockHoldObserver? lockObserver = null`. The optional final argument keeps the many
existing direct-construction tests valid. Import System.Diagnostics and
EventBooking.Application.Invites for ViewInviteHandler. Replace its transaction
body with the complete control-flow pattern below, retaining the Task 21 token-shaped error
changes in the indicated validation section. The lock clock starts only after
`capacities.LockForUpdateAsync` returns, so time waiting to acquire rows is excluded; the
`finally` disposes the transaction first, so the observation includes commit/rollback and lock
release. Every return and exception after acquisition is measured exactly once.

```csharp
// src/EventBooking.Application/Bookings/ConfirmBookingHandler.cs — replace HandleAsync body
public async Task<Result<ConfirmBookingOutcome>> HandleAsync(
    ConfirmBookingCommand command, CancellationToken ct)
{
    if (!tokens.TryRead(command.BookToken, out var reference)
        || reference.Purpose != TokenPurpose.Book
        || reference.Version < Invite.InitialTokenVersion)
        return Result<ConfirmBookingOutcome>.Failure(
            Error.TokenInvalid(ViewInviteHandler.InvalidLinkMessage));

    var transaction = await unitOfWork.BeginTransactionAsync(ct);
    long? lockAcquiredAt = null;
    try
    {
        var port = await invites.GetAsync(reference.EntityId, ct);
        if (port is null)
            return Result<ConfirmBookingOutcome>.Failure(
                Error.TokenInvalid(ViewInviteHandler.InvalidLinkMessage));
        var attendee = await attendees.LockForUpdateAsync(port.AttendeeId, ct);
        if (attendee is null)
            return Result<ConfirmBookingOutcome>.Failure(
                Error.TokenInvalid(ViewInviteHandler.InvalidLinkMessage));
        var invite = await invites.LockForUpdateAsync(reference.EntityId, ct);
        if (invite is null || invite.AttendeeId != attendee.Id
            || invite.TokenVersion != reference.Version
            || invite.Status != InviteStatus.Pending && invite.Status != InviteStatus.Used)
            return Result<ConfirmBookingOutcome>.Failure(
                Error.TokenInvalid(ViewInviteHandler.InvalidLinkMessage));
        if (invite.Status == InviteStatus.Used)
        {
            var existing = await bookings.GetByInviteIdAsync(invite.Id, ct);
            await transaction.RollbackAsync(ct);
            return Result<ConfirmBookingOutcome>.Failure(Error.AlreadyConfirmed(
                $"This invite already confirmed booking {existing?.Id}.",
                existing?.Id ?? Guid.Empty));
        }
        if (!invite.IsUsableAt(clock.UtcNow))
            return Result<ConfirmBookingOutcome>.Failure(
                Error.TokenExpired(ViewInviteHandler.ExpiredLinkMessage));
        if (!invite.Offers(command.EventId))
            return Result<ConfirmBookingOutcome>.Failure(
                Error.Validation("The chosen event is not one of this invite's options."));

        var eventItem = await events.LockForUpdateAsync(command.EventId, ct);
        if (eventItem is null)
            return Result<ConfirmBookingOutcome>.Failure(Error.NotFound("No such event."));
        var location = await locations.GetAsync(eventItem.LocationId, ct);
        if (location is not null
            && eventItem.Window.HasStarted(zones, location.TimeZoneId, clock.UtcNow))
            return Result<ConfirmBookingOutcome>.Failure(
                Error.WindowStarted("This event has started and can no longer be booked."));

        var required = invite.RequiredAppointmentTypeIds;
        var locked = await capacities.LockForUpdateAsync(eventItem.Id, required, ct);
        lockAcquiredAt = Stopwatch.GetTimestamp();
        if (locked.Count != required.Count)
            return Result<ConfirmBookingOutcome>.Failure(
                Error.Validation("The event no longer lists every required appointment type."));
        try
        {
            eventItem.ChargeRequiredTypes(required);
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(ct);
            return Result<ConfirmBookingOutcome>.Failure(Error.CapacityExhausted(ex.Message));
        }

        var booking = Booking.Create(Guid.NewGuid(), invite, eventItem.Id, clock.UtcNow);
        bookings.Add(booking);
        foreach (var typeId in required)
            appointments.Add(BookingAppointment.Create(Guid.NewGuid(), booking.Id, typeId));
        invite.MarkUsed();
        if (attendee.Status != AttendeeStatus.Booked)
            attendee.MarkBooked(clock.UtcNow);
        audit.Record(AuditEntityTypes.Booking, booking.Id, AuditAction.BookingCreated,
            ActorType.AttendeeToken, invite.Id.ToString(), $"event {eventItem.Id}");
        foreach (var typeId in required)
            audit.Record(AuditEntityTypes.Event, eventItem.Id, AuditAction.CapacityDecremented,
                ActorType.AttendeeToken, invite.Id.ToString(), $"type {typeId}");
        emails.Add(EmailLog.RecordPending(Guid.NewGuid(), attendee.Id,
            EmailTemplate.BookingConfirmation, clock.UtcNow, bookingId: booking.Id));
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result<ConfirmBookingOutcome>.Success(new ConfirmBookingOutcome(
            booking.Id, tokens.Issue(TokenPurpose.Manage, booking.Id, booking.ManageTokenVersion)));
    }
    finally
    {
        try
        {
            await transaction.DisposeAsync();
        }
        finally
        {
            if (lockAcquiredAt is long started)
                lockObserver?.Record(Stopwatch.GetElapsedTime(started));
        }
    }
}
```

Replace EventBookingMetrics with this complete file. It preserves the four Task 21 instruments
and the existing meter name. The new metric carries no labels and records seconds.

```csharp
// src/EventBooking.Api/Observability/EventBookingMetrics.cs (complete)
using System.Diagnostics.Metrics;
using EventBooking.Application.Abstractions;

namespace EventBooking.Api.Observability;

public sealed class EventBookingMetrics : ICapacityLockHoldObserver, IDisposable
{
    public const string MeterName = "EventBooking.Api";
    private readonly Meter _meter;

    public EventBookingMetrics(IMeterFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _meter = factory.Create(MeterName);
        Requests = _meter.CreateCounter<long>(
            "eventbooking_http_requests_total", "requests",
            "Requests served, by route pattern, method and status.");
        Duration = _meter.CreateHistogram<double>(
            "eventbooking_http_request_duration_seconds", "s",
            "Request duration, by route pattern.");
        CapacityExhausted = _meter.CreateCounter<long>(
            "eventbooking_capacity_exhausted_total", "refusals",
            "Bookings refused for want of capacity — a business signal of undersupply.");
        RateLimited = _meter.CreateCounter<long>(
            "eventbooking_rate_limited_total", "rejections",
            "Requests rejected by a rate limiter, by policy.");
        CapacityLockHoldDuration = _meter.CreateHistogram<double>(
            "eventbooking_capacity_lock_hold_duration_seconds", "s",
            "Time from capacity lock acquisition through transaction release.");
    }

    public Counter<long> Requests { get; }
    public Histogram<double> Duration { get; }
    public Counter<long> CapacityExhausted { get; }
    public Counter<long> RateLimited { get; }
    public Histogram<double> CapacityLockHoldDuration { get; }
    public void Record(TimeSpan elapsed) =>
        CapacityLockHoldDuration.Record(elapsed.TotalSeconds);
    public void Dispose() => _meter.Dispose();
}
```

In API Program, add the metrics factory before Task 21's metric singleton, then register the
port against that same singleton meter:

```csharp
// src/EventBooking.Api/Program.cs — add with the Task 21 metrics registrations
builder.Services.AddMetrics();
builder.Services.AddSingleton<ICapacityLockHoldObserver>(
    provider => provider.GetRequiredService<EventBookingMetrics>());
```

Add `using EventBooking.Application.Abstractions;` if Program does not already import it.
The API composition root is the only place that knows both layers.

- [ ] **Step 5: Render actual Prometheus histogram buckets**

Replace Task 21's PrometheusText with this complete file. It keeps the existing locked
dictionary, tag escaping and counter semantics, but publishes standard cumulative histogram
bucket/count/sum lines. The 0.049999-second bound is deliberately below 50 ms: a bucket count
of at least 475 out of 500 proves the empirical 95th percentile is strictly below 50 ms.

```csharp
// src/EventBooking.Api/Observability/PrometheusText.cs (complete)
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text;

namespace EventBooking.Api.Observability;

public sealed class PrometheusText : IDisposable
{
    private static readonly double[] Bounds =
        [0.001, 0.005, 0.01, 0.025, 0.049999, 0.1, 0.25, 0.5, 1, 2.5, 5];
    private readonly MeterListener _listener = new();
    private readonly Dictionary<string, Series> _series = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    public PrometheusText()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == EventBookingMetrics.MeterName)
                listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>(
            (instrument, measurement, tags, _) => Record(instrument, measurement, tags));
        _listener.SetMeasurementEventCallback<double>(
            (instrument, measurement, tags, _) => Record(instrument, measurement, tags));
        _listener.Start();
    }

    public string Render()
    {
        var builder = new StringBuilder();
        lock (_gate)
        {
            foreach (var group in _series.Values.GroupBy(x => x.Name, StringComparer.Ordinal)
                .OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var first = group.First();
                builder.Append("# HELP ").Append(group.Key).Append(' ')
                    .Append(first.Description).Append('\n');
                builder.Append("# TYPE ").Append(group.Key).Append(' ')
                    .Append(first.IsHistogram ? "histogram" : "counter").Append('\n');
                foreach (var series in group.OrderBy(x => x.Labels, StringComparer.Ordinal))
                {
                    if (!series.IsHistogram)
                    {
                        Sample(builder, group.Key, series.Labels, series.Sum);
                        continue;
                    }
                    for (var index = 0; index < Bounds.Length; index++)
                        Sample(builder, group.Key + "_bucket",
                            BucketLabels(series.Labels, index == 4
                                ? "0.049999"
                                : Bounds[index].ToString("G17", CultureInfo.InvariantCulture)),
                            series.Buckets[index]);
                    Sample(builder, group.Key + "_bucket",
                        BucketLabels(series.Labels, "+Inf"), series.Count);
                    Sample(builder, group.Key + "_sum", series.Labels, series.Sum);
                    Sample(builder, group.Key + "_count", series.Labels, series.Count);
                }
            }
        }
        return builder.ToString();
    }

    public void Dispose() => _listener.Dispose();

    private void Record(Instrument instrument, double measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var labels = Format(tags);
        var key = instrument.Name + labels;
        lock (_gate)
        {
            if (!_series.TryGetValue(key, out var series))
            {
                series = new Series(instrument.Name, labels,
                    instrument is Histogram<double>,
                    instrument.Description ?? instrument.Name);
                _series[key] = series;
            }
            series.Sum += measurement;
            if (!series.IsHistogram) return;
            series.Count++;
            for (var index = 0; index < Bounds.Length; index++)
                if (measurement <= Bounds[index]) series.Buckets[index]++;
        }
    }

    private static void Sample(StringBuilder output, string name, string labels, double value) =>
        output.Append(name).Append(labels).Append(' ')
            .Append(value.ToString("G17", CultureInfo.InvariantCulture)).Append('\n');

    private static string BucketLabels(string labels, string bound) =>
        labels.Length == 0
            ? $"{{le=\"{bound}\"}}"
            : labels[..^1] + $",le=\"{bound}\"}}";

    private static string Format(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (tags.Length == 0) return string.Empty;
        var builder = new StringBuilder("{");
        for (var index = 0; index < tags.Length; index++)
        {
            if (index > 0) builder.Append(',');
            builder.Append(tags[index].Key).Append("=\"")
                .Append((tags[index].Value?.ToString() ?? string.Empty)
                    .Replace("\\", "\\\\", StringComparison.Ordinal)
                    .Replace("\"", "\\\"", StringComparison.Ordinal)
                    .Replace("\n", "\\n", StringComparison.Ordinal))
                .Append('"');
        }
        return builder.Append('}').ToString();
    }

    private sealed class Series(string name, string labels, bool isHistogram, string description)
    {
        public string Name { get; } = name;
        public string Labels { get; } = labels;
        public bool IsHistogram { get; } = isHistogram;
        public string Description { get; } = description;
        public double Sum { get; set; }
        public long Count { get; set; }
        public long[] Buckets { get; } = new long[Bounds.Length];
    }
}
```

The bound label is explicitly rendered as `0.049999` because `G17` may expose the binary
representation's extra digits. The k6 scraper reads that stable label.

Run the focused tests again. Expected: PASS. Do not record counts until observed.

- [ ] **Step 6: Write and inspect the k6 burst and isolated Compose profile**

The script below uses one invitation per VU, one iteration per VU and a shared start time.
`setup` primes the metrics listener and captures the pre-run histogram counters. `teardown`
scrapes after all iterations and emits the **delta**, so the p95 assertion cannot pass on a
previous run's process-lifetime samples. A missing scrape fails the metric thresholds.

```javascript
// tests/load/confirm-burst.js (complete)
import http from 'k6/http';
import { sleep } from 'k6';
import { Counter, Gauge } from 'k6/metrics';

const fixture = JSON.parse(open('./fixture.json'));
const target = __ENV.API_URL || 'http://localhost:5001';
const prefix = 'eventbooking_capacity_lock_hold_duration_seconds';
const created = new Counter('load_created');
const exhausted = new Counter('load_capacity_exhausted');
const unexpected = new Counter('load_unexpected');
const serverErrors = new Counter('load_server_errors');
const lockSamples = new Gauge('load_lock_samples');
const under50ms = new Gauge('load_lock_under_50ms');

if (!fixture.eventId || !Array.isArray(fixture.bookTokens)
    || fixture.bookTokens.length !== 500
    || new Set(fixture.bookTokens).size !== 500
    || new Set(fixture.bookTokens.map((token) => token.slice(0, 12))).size !== 500) {
  throw new Error('The load fixture must contain one event and 500 distinct token prefixes.');
}

export const options = {
  scenarios: {
    confirm: {
      executor: 'per-vu-iterations',
      vus: 500,
      iterations: 1,
      maxDuration: '5m',
    },
  },
  thresholds: {
    iterations: ['count==500'],
    load_created: ['count==100'],
    load_capacity_exhausted: ['count==400'],
    load_unexpected: ['count==0'],
    load_server_errors: ['count==0'],
    load_lock_samples: ['value==500'],
    load_lock_under_50ms: ['value>=475'],
  },
};

function sample(text, suffix) {
  const name = prefix + suffix;
  const row = text.split('\n').find((line) => line.startsWith(name + ' '));
  if (!row) return null;
  const value = Number(row.slice(name.length + 1).trim());
  return Number.isFinite(value) ? value : null;
}

function bucket(text) {
  const name = prefix + '_bucket{le="0.049999"}';
  const row = text.split('\n').find((line) => line.startsWith(name + ' '));
  if (!row) return null;
  const value = Number(row.slice(name.length + 1).trim());
  return Number.isFinite(value) ? value : null;
}

function scrape() {
  const response = http.get(target + '/metrics', { timeout: '10s' });
  if (response.status !== 200) return null;
  return { count: sample(response.body, '_count'), under: bucket(response.body) };
}

export function setup() {
  const before = scrape();
  if (!before) throw new Error('GET /metrics failed before the burst.');
  created.add(0);
  exhausted.add(0);
  unexpected.add(0);
  serverErrors.add(0);
  return { before, startAt: Date.now() + 10000 };
}

export default function (data) {
  const delay = (data.startAt - Date.now()) / 1000;
  if (delay > 0) sleep(delay);
  const token = fixture.bookTokens[__VU - 1];
  const response = http.post(
    target + '/api/booking/' + encodeURIComponent(token) + '/confirm',
    JSON.stringify({ eventId: fixture.eventId }),
    { headers: { 'Content-Type': 'application/json' }, timeout: '120s',
      tags: { name: 'POST /api/booking/{token}/confirm' } },
  );
  if (response.status === 201) {
    created.add(1);
    return;
  }
  if (response.status >= 500) serverErrors.add(1);
  if (response.status === 409) {
    try {
      const problem = JSON.parse(response.body);
      if (problem.type === 'capacity-exhausted') {
        exhausted.add(1);
        return;
      }
    } catch (_) {
      // A malformed error body is an unexpected outcome.
    }
  }
  unexpected.add(1);
}

export function teardown(data) {
  const after = scrape();
  const before = data.before;
  const count = after && after.count !== null
    ? after.count - (before.count === null ? 0 : before.count) : -1;
  const under = after && after.under !== null
    ? after.under - (before.under === null ? 0 : before.under) : -1;
  lockSamples.add(count);
  under50ms.add(under);
  if (count !== 500 || under < 475) {
    console.error('Capacity lock metric delta failed: count=' + count + ', under50ms=' + under);
  }
}
```

The only non-2xx success is the exact `capacity-exhausted` problem type. Every 429 and any
other 4xx increments `load_unexpected`. A 5xx increments both `load_server_errors` and
`load_unexpected`. The 0.049999-second cumulative bucket enforces **under** 50 ms for at
least 475 of 500 lock intervals.

The normal local Compose file keeps the Task 21 30/min attendee address limit. Create this
override only under tests/load; it raises the per-IP limit above 500 for the disposable load
project. The per-token limiter remains 10/min, and the generated tokens have distinct prefixes.

```yaml
# tests/load/compose.load.yml (complete)
services:
  api:
    environment:
      RateLimiting__AttendeePerMinute: "600"
  seed:
    environment:
      EVENTBOOKING_ENABLE_LOAD_FIXTURE: "true"
      EVENTBOOKING_LOAD_FIXTURE_PATH: /load/fixture.json
    volumes:
      - ./tests/load:/load
```

```gitignore
# tests/load/.gitignore (complete)
/fixture.json
/postgres.log
```

```bash
# tests/load/run.sh (complete)
#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$root"
command -v docker >/dev/null
command -v k6 >/dev/null
if [[ -e tests/load/fixture.json ]]; then
  echo "Remove the previous disposable fixture after inspecting it; this run requires a fresh one." >&2
  exit 2
fi
compose=(docker compose -p eventbooking-load -f docker-compose.yml -f tests/load/compose.load.yml)
"${compose[@]}" up --detach --build --wait postgres keycloak mailpit
"${compose[@]}" --profile seed run --rm --user "$(id -u):$(id -g)" seed --demo --reanchor --load-fixture
test -s tests/load/fixture.json
"${compose[@]}" up --detach --build --wait api
curl --fail --silent http://localhost:5001/health/ready >/dev/null
started="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
set +e
k6 run tests/load/confirm-burst.js
load_status=$?
"${compose[@]}" logs --no-color --since "$started" postgres > tests/load/postgres.log
logs_status=$?
set -e
if [[ "$logs_status" -ne 0 ]]; then
  echo "Could not inspect PostgreSQL logs for deadlocks." >&2
  exit 1
fi
if grep -Eiq 'deadlock detected|SQLSTATE[[:space:]]*40P01' tests/load/postgres.log; then
  echo "PostgreSQL reported a deadlock during the burst." >&2
  exit 1
fi
exit "$load_status"
```

Create the complete load guide:

````markdown
<!-- tests/load/README.md (complete) -->
# 500-invitation confirmation burst

Run this before each release against the local Task 29 stack. Install Docker Compose and k6,
then stop any normal local stack that occupies the same ports. The script uses a separate
`eventbooking-load` Compose project and requires a fresh disposable database volume.

From the repository root:

```bash
bash tests/load/run.sh
```

The runner starts PostgreSQL, Keycloak and Mailpit; runs the ordinary demo seed plus a guarded
load fixture; starts the API; then sends 500 one-shot confirmations to
`http://localhost:5001`. It writes 500 book tokens to ignored
`tests/load/fixture.json` with owner-only permissions. Treat that file as a secret and do not
commit or share it. The runner leaves the stack and logs for inspection. Recreate the
disposable project before another burst; never run against a normal or home-lab database:

```bash
docker compose -p eventbooking-load -f docker-compose.yml -f tests/load/compose.load.yml down --volumes
rm -f tests/load/fixture.json tests/load/postgres.log
```

The load-only override raises the attendee per-IP allowance to 600/min; normal local and
home-lab defaults remain 30/min. Each of the 500 book tokens occupies a different token
prefix partition, so the 10/min per-token policy remains enabled. The test passes only with
100 HTTP 201 bookings, 400 HTTP 409 `capacity-exhausted` problems, no unexpected responses,
no 5xx, no PostgreSQL deadlock, 500 new lock-hold samples and at least 475 samples strictly
under 50 ms. A missing or malformed metric fails the run.

If it fails, inspect k6 output and `tests/load/postgres.log`, then inspect API logs with
`docker compose -p eventbooking-load -f docker-compose.yml -f tests/load/compose.load.yml logs api`.
Do not reuse the partially booked fixture for a second measurement.
````

Review the JavaScript, Bash, Compose override and guide as complete files. Check syntax with:

```bash
node --input-type=module --check < tests/load/confirm-burst.js
bash -n tests/load/run.sh
docker compose -f docker-compose.yml -f tests/load/compose.load.yml config --quiet
```

- [ ] **Step 7: Run the verification gate and record only observed results**

Run the focused .NET tests, `dotnet build EventBooking.sln -warnaserror`, the full solution
tests, the three static checks above, then `bash tests/load/run.sh` against a fresh local
stack. Expected: exactly 100 201, 400 409 `capacity-exhausted`, zero 5xx, zero unexpected
outcomes, zero deadlocks, 500 new lock-hold samples and at least 475 strictly below 50 ms.
If an earlier task fails build or the stack cannot start, report that dependency failure and
leave the load result unobserved; do not substitute a static read for the required run.

**Authoring evidence:** This document is hand-authored. Phases 3–6 have never produced a built
application in this repository, so the 500-way burst, its percentile and its outcomes have
not been observed at authoring time. No result count is claimed here or in the handover.

- [ ] **Step 8: Commit and push**

Inspect the staged file list and diff, run the ontology term checker, commit this task alone,
inspect the commit diff and push the phase branch:

```bash
git add -A
git diff --cached --name-only
git diff --cached
node scripts/check-ontology-terms.mjs
git commit -m "test(load): 500-way confirmation burst"
git push
```
