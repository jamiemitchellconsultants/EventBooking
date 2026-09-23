# 06a — Seed CLI and demo dataset (Task 28)

[← Phase overview](phase-6-seed-and-deployment.md) · [Ontology](../ontology.md)

This task is the seed/application boundary of Phase 6. It makes migration-only operation the safe
default, reconstructs demo state around Admin-managed reference data and per-location time zones,
and removes the final single-zone clock configuration before any container depends on it.

> Use superpowers:executing-plans. Apply this document after Phase 5 on the Phase 6 branch.

**Goal:** Ship a side-effect-safe CLI whose optional demo dataset covers every generalised axis,
is naturally idempotent, converges Keycloak identities and can be deliberately reanchored or
destructively reseeded only behind the documented guard.

**Architecture:** SeedCliOptions performs a complete preflight before SeedCommand invokes a side
effect. SeedRunSteps owns infrastructure composition; DemoSeedSpec is a deterministic declarative
graph; DemoSeeder resolves that graph by natural key and uses domain factories for proposals,
events, invites, bookings and appointment transitions. Location zones replace every transitional
clock conversion; IClock exposes UTC now only.

**Tech Stack:** .NET 10, EF Core 10, Npgsql, PostgreSQL 16 Testcontainers, NodaTime and Keycloak
Admin REST.

**Spec:** [Master Task 28](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[seed data](../design/07-deployment.md#seed-data),
[Keycloak](../design/06-security-and-authentication.md#staff-authentication-keycloak-by-default),
and [ontology](../ontology.md).

## Global constraints

The [phase constraints](phase-6-seed-and-deployment.md#global-constraints) apply. LAB is the fourth
active managed type. The default anchor is 22 September 2026; `--reanchor` replaces it with the
current London calendar date for this run without editing a file. A daylight-saving scenario is
calculated from the anchor, not represented by a fixed offset that stops being a transition after
reanchoring.

## Review focus

STOP AND CHECK: invalid arguments cause zero calls on SeedRunSteps; no-flag mode never constructs
SMTP or Keycloak demo configuration; every natural-key count is unchanged on the second `--demo`
run; every seeded proposal is accepted by EventProposal.Propose; no live scenario lists ESC or
DOC; LAB has exactly one Manager; and no ClockOptions or Clock:TimeZoneId reference remains.

### Task 28: Migrate-only CLI, generalised demo data and clock retirement

**Files:**

- Create: src/EventBooking.SeedData/SeedCommand.cs
- Create: src/EventBooking.SeedData/SeedRunSteps.cs
- Modify: src/EventBooking.SeedData/Program.cs
- Modify: src/EventBooking.SeedData/DemoSeedSpec.cs
- Modify: src/EventBooking.SeedData/DemoSeeder.cs
- Modify: src/EventBooking.SeedData/DemoEventFactory.cs
- Modify: src/EventBooking.SeedData/DemoInvitationSeeder.cs
- Modify: src/EventBooking.SeedData/DemoEmailOptions.cs
- Modify: src/EventBooking.SeedData/KeycloakSeeder.cs
- Delete: src/EventBooking.SeedData/demo-seed.json
- Modify: src/EventBooking.SeedData/EventBooking.SeedData.csproj
- Modify: src/EventBooking.Infrastructure/Email/OutboxDispatcher.cs
- Modify: src/EventBooking.Infrastructure/Email/ClaimQuery.cs
- Modify: src/EventBooking.Application/Abstractions/IClock.cs
- Modify: src/EventBooking.Infrastructure/Time/SystemClock.cs
- Delete: src/EventBooking.Infrastructure/Time/ClockOptions.cs
- Modify: src/EventBooking.Infrastructure/DependencyInjection.cs
- Modify: src/EventBooking.Api/EventBookingConfiguration.cs
- Modify: src/EventBooking.Api/Program.cs
- Modify: src/EventBooking.Mcp/Program.cs
- Test: tests/EventBooking.SeedData.Tests/SeedCommandTests.cs
- Test: tests/EventBooking.SeedData.Tests/DemoSeedSpecTests.cs
- Test: tests/EventBooking.SeedData.Tests/DemoSeederIntegrationTests.cs
- Delete: tests/EventBooking.SeedData.Tests/DemoInvitationSeederTests.cs (obsolete fixed-dataset and
  immediate-send suite; the complete PostgreSQL invitation cases move into DemoSeederIntegrationTests)
- Test: tests/EventBooking.SeedData.Tests/KeycloakSeederTests.cs
- Test: tests/EventBooking.SeedData.Tests/ReseedTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/Email/OutboxDispatcherTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/SystemClockTests.cs

**Interfaces:**

```csharp
namespace EventBooking.SeedData;

public sealed record SeedCliOptions(
    string ConnectionString,
    bool Demo,
    bool Reanchor,
    bool Reseed,
    bool Verbose)
{
    // Parses every option and destructive guard before any I/O object is constructed.
    public static SeedCliOptions Parse(string[] args, Func<string, string?> environment);
}

public interface ISeedRunSteps : IAsyncDisposable
{
    Task ApplyDatabaseRolesAsync(CancellationToken ct);
    Task ApplyMigrationsAsync(CancellationToken ct);
    Task ReanchorToTodayAsync(CancellationToken ct);
    Task<KeycloakSeedSummary?> ConvergeKeycloakAsync(bool recreateRealm, CancellationToken ct);
    Task<SeedSummary> SeedDemoAsync(bool wipeFirst, CancellationToken ct);
    Task<int> SendDemoInvitationsAsync(CancellationToken ct);
}

public static class SeedCommand
{
    // Always applies roles and migrations. Demo/Keycloak/email work is reachable only when Demo.
    public static Task<int> RunAsync(
        SeedCliOptions options,
        ISeedRunSteps steps,
        TextWriter output,
        CancellationToken ct);
}

public sealed record DemoDataset(
    DateOnly Anchor,
    IReadOnlyList<DemoLocationSpec> Locations,
    IReadOnlyList<DemoAppointmentTypeSpec> AppointmentTypes,
    IReadOnlyList<DemoAttendeeGroupSpec> AttendeeGroups,
    IReadOnlyList<StaffProfileSpec> Staff,
    IReadOnlyList<DemoEventSpec> Events,
    IReadOnlyList<DemoProposalSpec> OpenProposals,
    IReadOnlyList<DemoAttendeeSpec> Attendees);

public static class DemoSeedSpec
{
    public static DateOnly AnchorDate();
    public static bool AnchorOverridden { get; }
    public static void OverrideAnchor(DateOnly? anchor);
    public static DemoDataset Build();
    public static IReadOnlyList<StaffProfileSpec> Staff();
    public static Guid AdminUserId();
    public static Guid CoordinatorUserId();
    public static IReadOnlyDictionary<Guid, Guid> ManagerForType();
}

public sealed class DemoSeeder
{
    // Moves only deterministic demo Events and EventProposals before the normal natural-key upsert.
    public Task ReanchorAsync(DemoDataset target, CancellationToken ct);
}

namespace EventBooking.Infrastructure.Email;

public sealed class OutboxDispatcher
{
    // Claims and dispatches only this delivery; demo seeding must not drain unrelated outbox work.
    public Task<int> DispatchOneAsync(Guid emailLogId, CancellationToken ct);
}

namespace EventBooking.Application.Abstractions;

public interface IClock
{
    // The sole system-clock concern. Location-aware date/instant work uses IEventWindowZones.
    DateTimeOffset UtcNow { get; }
}
```

- [ ] **Step 1: Write the failing test**

Create the command preflight suite. The fake makes the absence of side effects observable rather
than inferring it from row counts after the fact.

```csharp
// tests/EventBooking.SeedData.Tests/SeedCommandTests.cs (complete)
using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

public sealed class SeedCommandTests
{
    private const string Connection = "Host=localhost;Database=eventbooking";

    [Fact]
    public async Task NoFlagAppliesRolesAndMigrationsOnly()
    {
        var steps = new RecordingSteps();
        var options = SeedCliOptions.Parse([Connection], _ => null);

        var exit = await SeedCommand.RunAsync(options, steps, TextWriter.Null, default);

        Assert.Equal(0, exit);
        Assert.Equal(["roles", "migrations"], steps.Calls);
    }

    [Theory]
    [InlineData("--unknown")]
    [InlineData("--reanchor")]
    [InlineData("--reseed")]
    public void InvalidOrDemoOnlyArgumentsAreRejectedBeforeStepsExist(string option)
    {
        var error = Assert.Throws<SeedException>(() =>
            SeedCliOptions.Parse([Connection, option], _ => null));

        Assert.Contains(option, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReseedWithoutExactEnvironmentGuardIsRejected()
    {
        var calls = 0;
        string? Read(string key)
        {
            calls++;
            return key == "EVENTBOOKING_ALLOW_RESEED" ? "TRUE" : null;
        }

        var error = Assert.Throws<SeedException>(() =>
            SeedCliOptions.Parse([Connection, "--demo", "--reseed"], Read));

        Assert.Contains("EVENTBOOKING_ALLOW_RESEED=true", error.Message);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task DemoReanchorAndGuardedReseedRunInSafeOrder()
    {
        var steps = new RecordingSteps();
        var options = SeedCliOptions.Parse(
            [Connection, "--demo", "--reanchor", "--reseed"],
            key => key == "EVENTBOOKING_ALLOW_RESEED" ? "true" : null);

        var exit = await SeedCommand.RunAsync(options, steps, TextWriter.Null, default);

        Assert.Equal(0, exit);
        Assert.Equal(
            ["roles", "migrations", "reanchor", "keycloak:reset", "seed:wipe", "invitations"],
            steps.Calls);
    }

    private sealed class RecordingSteps : ISeedRunSteps
    {
        public List<string> Calls { get; } = [];
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public Task ApplyDatabaseRolesAsync(CancellationToken ct) { Calls.Add("roles"); return Task.CompletedTask; }
        public Task ApplyMigrationsAsync(CancellationToken ct) { Calls.Add("migrations"); return Task.CompletedTask; }
        public Task ReanchorToTodayAsync(CancellationToken ct) { Calls.Add("reanchor"); return Task.CompletedTask; }
        public Task<KeycloakSeedSummary?> ConvergeKeycloakAsync(bool recreateRealm, CancellationToken ct)
        { Calls.Add(recreateRealm ? "keycloak:reset" : "keycloak"); return Task.FromResult<KeycloakSeedSummary?>(null); }
        public Task<SeedSummary> SeedDemoAsync(bool wipeFirst, CancellationToken ct)
        { Calls.Add(wipeFirst ? "seed:wipe" : "seed"); return Task.FromResult(SeedSummary.Empty); }
        public Task<int> SendDemoInvitationsAsync(CancellationToken ct)
        { Calls.Add("invitations"); return Task.FromResult(1); }
    }
}
```

Replace the old fixed-set coverage suite with the generalised one. This test is deliberately about
the declarative graph; the PostgreSQL suite below proves persistence and idempotence.

```csharp
// tests/EventBooking.SeedData.Tests/DemoSeedSpecTests.cs (complete)
using EventBooking.Domain.Access;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Time;
using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

[Collection("seed-anchor")]
public sealed class DemoSeedSpecTests
{
    [Fact]
    public void DatasetCoversEveryRequiredAxisWithoutPuttingEscOrDocOnLiveScenarios()
    {
        DemoSeedSpec.OverrideAnchor(new DateOnly(2026, 9, 22));
        var data = DemoSeedSpec.Build();

        Assert.Equal(3, data.Locations.Count);
        Assert.Equal(2, data.Locations.Select(x => x.TimeZoneId).Distinct().Count());
        Assert.Single(data.Locations, x => !x.IsActive);

        Assert.Equal(6, data.AppointmentTypes.Count);
        Assert.Single(data.AppointmentTypes, x => x.Code == "ESC" && x.IsActive && x.ManagerUsername is null);
        Assert.Single(data.AppointmentTypes, x => x.Code == "DOC" && !x.IsActive);
        Assert.Single(data.AppointmentTypes, x => x.Code == "LAB" && x.IsActive && x.ManagerUsername is not null);

        Assert.Equal([1, 2, 3, 4], data.Events.Select(x => x.TypeCodes.Count).Distinct().Order());
        Assert.Equal([60, 90, 240, 480], data.Events.Select(x => x.DurationMinutes).Distinct().Order());
        Assert.Contains(data.Events, x => IsOffsetChangeDate(x.Date, data.Locations.Single(l => l.Code == x.LocationCode).TimeZoneId));
        Assert.DoesNotContain(data.Events.SelectMany(x => x.TypeCodes), x => x is "ESC" or "DOC");

        Assert.Contains(data.OpenProposals, x => x.TypeCodes.Count == 2 && x.AcceptedTypeCodes.Count == 1);
        Assert.Contains(data.OpenProposals, x => x.TypeCodes.Count == 4 && x.AcceptedTypeCodes.Count == 2);
        Assert.DoesNotContain(data.OpenProposals.SelectMany(x => x.TypeCodes), x => x is "ESC" or "DOC");

        Assert.Equal(Enum.GetValues<AttendeeStatus>().Order(), data.Attendees.Select(x => x.Status).Distinct().Order());
        Assert.Equal(Enum.GetValues<BookingAppointmentStatus>().Order(),
            data.Attendees.SelectMany(x => x.AppointmentStatuses).Distinct().Order());
        Assert.Contains(data.Attendees, x => x.Recovery == DemoRecovery.Pending);
        Assert.Contains(data.Attendees, x => x.Recovery == DemoRecovery.Completed);
        Assert.Contains(data.Attendees, x => x.GroupCode == "FIT_ESC" && x.Status == AttendeeStatus.AwaitingAvailability);
    }

    [Fact]
    public void EveryEventAndOpenProposalPassesTheRealProposalFactory()
    {
        DemoSeedSpec.OverrideAnchor(new DateOnly(2026, 9, 22));
        var data = DemoSeedSpec.Build();
        var zones = new NodaTimeEventWindowZones();
        var types = data.AppointmentTypes.ToDictionary(x => x.Code);
        var locations = data.Locations.ToDictionary(x => x.Code);
        var managers = data.Staff.Where(x => x.Roles.Contains(Role.Manager))
            .ToDictionary(x => x.AppointmentTypeId!.Value, x => x.UserId);

        foreach (var scenario in data.Events.Cast<IDemoProposalScenario>().Concat(data.OpenProposals))
        {
            var location = locations[scenario.LocationCode];
            var listed = scenario.TypeCodes.Select(code => types[code]).ToList();
            var proposer = listed[0];
            var window = new EventWindow(scenario.Date, scenario.StartTime, scenario.DurationMinutes);
            var createdAt = window.StartInstant(zones, location.TimeZoneId).AddDays(-1);
            var proposal = EventProposal.Propose(
                Guid.NewGuid(), location.Id, location.IsActive, location.TimeZoneId, window, zones,
                createdAt,
                listed.Select(x => new ProposableAppointmentType(x.Id, x.Code, x.IsActive, x.ManagerUsername is not null)).ToList(),
                proposer.Id, managers[proposer.Id], 12);

            foreach (var code in scenario.AcceptedTypeCodes.Skip(1))
            {
                var type = types[code];
                proposal.Accept(type.Id, managers[type.Id], 12);
            }

            Assert.Equal(scenario.AcceptedTypeCodes.Count, proposal.Acceptances.Count);
        }
    }

    private static bool IsOffsetChangeDate(DateOnly date, string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return zone.GetUtcOffset(start) != zone.GetUtcOffset(start.AddDays(1));
    }
}
```

Add the PostgreSQL idempotence and reanchor tests. The fixture is complete here so the executor does
not need to reconstruct service registration. Counts include invitations and delivery logs: a
second run that silently duplicates either row type is a failure even when the reference-data
counts remain stable.

```csharp
// tests/EventBooking.SeedData.Tests/DemoSeederIntegrationTests.cs (complete)
using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
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
public sealed class DemoSeederIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero));
    private readonly CapturingTransport _mail = new();
    private ServiceProvider _provider = null!;

    public async Task InitializeAsync()
    {
        DemoSeedSpec.OverrideAnchor(new DateOnly(2026, 9, 22));
        await _postgres.StartAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBookingInfrastructure(
            _postgres.GetConnectionString(),
            new TokenOptions("seed-tests-signing-key-at-least-thirty-two-bytes"));
        services.AddEventBookingApplication(
            new AttendeePortalOptions("http://localhost:5002", "events@example.test"),
            new EventBooking.Application.Access.StaffIdPolicy("^[A-Z0-9]{1,32}$"));
        services.AddSingleton<IClock>(_clock);
        services.AddSingleton<IEmailTransport>(_mail);
        services.AddScoped<IAuditLogger, EfAuditLogger>();
        services.AddScoped<DemoSeeder>();
        services.AddScoped<DemoInvitationSeeder>();
        services.AddSingleton<OutboxDispatcher>();
        _provider = services.BuildServiceProvider();
        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        await db.Database.ExecuteSqlRawAsync(DatabaseRoles.Script);
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        DemoSeedSpec.OverrideAnchor(null);
        await _provider.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task TwoDemoRunsHaveIdenticalNaturalKeyAndDeliveryCounts()
    {
        await RunSeedAndInvitationsAsync();
        var first = await CountsAsync();

        await RunSeedAndInvitationsAsync();
        var second = await CountsAsync();

        Assert.Equal(first, second);
        Assert.Equal((3, 6, 4, 8), (second.Locations, second.Types, second.Groups, second.Profiles));
        Assert.True(second.Events >= 7);
        Assert.Equal(second.Events + 2, second.Proposals);
        Assert.Equal(2, second.OpenProposals);
        Assert.Equal(DemoSeedSpec.Build().Attendees.Count, second.Attendees);
        Assert.True(second.Invites > 0);
        Assert.True(second.EmailLogs > 0);
        Assert.Single(_mail.Recipients);
    }

    [Fact]
    public async Task ReanchorOnALaterDayMovesOwnedWindowsWithoutChangingCounts()
    {
        await RunSeedAndInvitationsAsync();
        var before = await CountsAsync();

        _clock.Advance(TimeSpan.FromDays(1));
        DemoSeedSpec.OverrideAnchor(new DateOnly(2026, 9, 23));
        var target = DemoSeedSpec.Build();
        await using (var scope = _provider.CreateAsyncScope())
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DemoSeeder>();
            await seeder.ReanchorAsync(target, default);
            await seeder.RunAsync(default);
            await scope.ServiceProvider.GetRequiredService<DemoInvitationSeeder>().RunAsync(default);
        }

        Assert.Equal(before, await CountsAsync());
        Assert.Single(_mail.Recipients);
        await using var verify = _provider.CreateAsyncScope();
        var db = verify.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var eventIds = target.Events.Select(x => x.Id).ToList();
        var eventDates = await db.Events.Where(x => eventIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Window.Date);
        Assert.All(target.Events, spec => Assert.Equal(spec.Date, eventDates[spec.Id]));
        var proposalIds = target.OpenProposals.Select(x => x.Id).ToList();
        var proposalDates = await db.EventProposals
            .Where(x => proposalIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Window.Date);
        Assert.All(target.OpenProposals, spec => Assert.Equal(spec.Date, proposalDates[spec.Id]));
    }

    [Fact]
    public async Task FailedInvitationDeliveryCreatesOneTargetedRetryAndThenConverges()
    {
        await RunSeedOnlyAsync();
        _mail.Outcome = EmailSendOutcome.PermanentFailure;
        await Assert.ThrowsAsync<SeedException>(() => RunInvitationsAsync());
        var failed = await CountsAsync();

        _mail.Outcome = EmailSendOutcome.Sent;
        Assert.Equal(1, await RunInvitationsAsync());
        var recovered = await CountsAsync();

        Assert.Equal(failed.Invites, recovered.Invites);
        Assert.Equal(failed.EmailLogs + 1, recovered.EmailLogs);
        Assert.Equal(0, await RunInvitationsAsync());
        Assert.Equal(recovered, await CountsAsync());
        Assert.Single(_mail.Recipients);
    }

    private async Task RunSeedAndInvitationsAsync()
    {
        await RunSeedOnlyAsync();
        await RunInvitationsAsync();
    }

    private async Task RunSeedOnlyAsync()
    {
        await using var scope = _provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DemoSeeder>().RunAsync(default);
    }

    private async Task<int> RunInvitationsAsync()
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<DemoInvitationSeeder>().RunAsync(default);
    }

    private async Task<Counts> CountsAsync()
    {
        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        return new Counts(
            await db.Locations.CountAsync(),
            await db.AppointmentTypes.CountAsync(),
            await db.AttendeeGroups.CountAsync(),
            await db.StaffAccessProfiles.CountAsync(),
            await db.Events.CountAsync(),
            await db.EventProposals.CountAsync(),
            await db.EventProposals.CountAsync(x => x.Status == EventBooking.Domain.Events.EventProposalStatus.Open),
            await db.Attendees.CountAsync(),
            await db.Invites.CountAsync(),
            await db.EmailLogs.CountAsync());
    }

    private sealed record Counts(
        int Locations, int Types, int Groups, int Profiles,
        int Events, int Proposals, int OpenProposals, int Attendees, int Invites, int EmailLogs);

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = utcNow;
        public void Advance(TimeSpan elapsed) => UtcNow += elapsed;
    }

    private sealed class CapturingTransport : IEmailTransport
    {
        public List<string> Recipients { get; } = [];
        public EmailSendOutcome Outcome { get; set; } = EmailSendOutcome.Sent;
        public Task<EmailSendOutcome> SendAsync(
            string recipient, string subject, string textBody, string htmlBody, CancellationToken ct)
        {
            if (Outcome == EmailSendOutcome.Sent) Recipients.Add(recipient);
            return Task.FromResult(Outcome);
        }
    }
}
```

Delete the predecessor DemoInvitationSeederTests file. Its fixed 100-attendee/five-recipient data,
single-zone clock and immediate-send harness describe the retired seed architecture; the complete
PostgreSQL suite above now owns fresh delivery, rerun idempotence, reanchor and failed-delivery
retry coverage against the Task 28 composition.

- [ ] **Step 2: Run the focused tests and verify the red state**

```bash
dotnet test tests/EventBooking.SeedData.Tests --filter \
  "FullyQualifiedName~SeedCommandTests|FullyQualifiedName~DemoSeedSpecTests|FullyQualifiedName~DemoSeederIntegrationTests"
dotnet test tests/EventBooking.Infrastructure.Tests --filter \
  FullyQualifiedName~Targeted_dispatch_leaves_other_due_rows_unclaimed
```

Expected: FAIL because SeedCliOptions, SeedCommand, LAB and the general dataset do not exist; the
old default path seeds immediately; and the old seed still depends on fixed IDs and the
transitional clock.

- [ ] **Step 3: Implement the preflighted command boundary**

Create the complete parser and command. Keep usage text in Program; parsing throws SeedException so
both the process and tests receive one stable failure type.

```csharp
// src/EventBooking.SeedData/SeedCommand.cs (complete)
namespace EventBooking.SeedData;

public sealed record SeedCliOptions(
    string ConnectionString, bool Demo, bool Reanchor, bool Reseed, bool Verbose)
{
    public static SeedCliOptions Parse(string[] args, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(environment);

        var values = args.Where(x => !x.StartsWith('-')).ToList();
        if (values.Count > 1)
            throw new SeedException("Supply exactly one PostgreSQL connection string.");
        var connection = values.SingleOrDefault()
            ?? environment("ConnectionStrings__EventBooking");
        if (string.IsNullOrWhiteSpace(connection))
            throw new SeedException("A PostgreSQL connection string argument or ConnectionStrings__EventBooking is required.");

        var known = new HashSet<string>(StringComparer.Ordinal)
            { "--demo", "--reanchor", "--reseed", "--verbose" };
        var unknown = args.Where(x => x.StartsWith('-') && !known.Contains(x)).ToList();
        if (unknown.Count > 0)
            throw new SeedException($"Unknown option '{unknown[0]}'.");
        var demo = args.Contains("--demo", StringComparer.Ordinal);
        var reanchor = args.Contains("--reanchor", StringComparer.Ordinal);
        var reseed = args.Contains("--reseed", StringComparer.Ordinal);
        if (reanchor && !demo)
            throw new SeedException("--reanchor requires --demo.");
        if (reseed && !demo)
            throw new SeedException("--reseed requires --demo.");
        if (reseed && !string.Equals(environment("EVENTBOOKING_ALLOW_RESEED"), "true", StringComparison.Ordinal))
            throw new SeedException("--reseed requires EVENTBOOKING_ALLOW_RESEED=true.");

        return new SeedCliOptions(connection, demo, reanchor, reseed,
            args.Contains("--verbose", StringComparer.Ordinal));
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
        return 0;
    }
}
```

Replace Program with this host. SeedRunSteps.Create must remain after parsing: constructing it
reads SMTP settings and creates the service provider, which no-flag mode must not do beyond the
minimal persistence provider.

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
        "Usage: EventBooking.SeedData <connection-string> [--demo [--reanchor] [--reseed]] [--verbose]");
    return 2;
}
```

Create SeedRunSteps as the only composition root for the CLI. This is complete; Keycloak settings
remain optional on a demo run, while email settings are required because `--demo` sends invitations.

```csharp
// src/EventBooking.SeedData/SeedRunSteps.cs (complete)
using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.SeedData;

public sealed class SeedRunSteps : ISeedRunSteps
{
    private readonly SeedCliOptions _options;
    private readonly Func<string, string?> _environment;
    private readonly ServiceProvider _provider;

    private SeedRunSteps(SeedCliOptions options, Func<string, string?> environment, ServiceProvider provider)
    {
        _options = options;
        _environment = environment;
        _provider = provider;
    }

    public static SeedRunSteps Create(SeedCliOptions options, Func<string, string?> environment)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (options.Demo)
        {
            var email = DemoEmailOptions.From(environment);
            services.AddEventBookingInfrastructure(options.ConnectionString, email.Tokens);
            services.AddEventBookingApplication(email.Portal,
                new EventBooking.Application.Access.StaffIdPolicy(environment("Identity__StaffIdPattern")));
            services.AddLocalEmailTransport(email.Sender, email.Smtp);
            services.AddScoped<DemoSeeder>();
            services.AddScoped<DemoInvitationSeeder>();
            services.AddSingleton<OutboxDispatcher>();
        }
        else
        {
            services.AddEventBookingPersistence(options.ConnectionString);
            services.AddSingleton<IClock, SystemClock>();
            services.AddSingleton<EventBooking.Domain.Time.IEventWindowZones, NodaTimeEventWindowZones>();
        }

        return new SeedRunSteps(options, environment, services.BuildServiceProvider());
    }

    private AsyncServiceScope Scope() => _provider.CreateAsyncScope();

    public async Task ApplyDatabaseRolesAsync(CancellationToken ct)
    {
        await using var scope = Scope();
        await scope.ServiceProvider.GetRequiredService<EventBookingDbContext>()
            .Database.ExecuteSqlRawAsync(DatabaseRoles.Script, ct);
    }

    public async Task ApplyMigrationsAsync(CancellationToken ct)
    {
        await using var scope = Scope();
        await scope.ServiceProvider.GetRequiredService<EventBookingDbContext>().Database.MigrateAsync(ct);
    }

    public async Task ReanchorToTodayAsync(CancellationToken ct)
    {
        await using var scope = Scope();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var zones = scope.ServiceProvider.GetRequiredService<EventBooking.Domain.Time.IEventWindowZones>();
        DemoSeedSpec.OverrideAnchor(zones.LocalDateOf(clock.UtcNow, "Europe/London"));
        await scope.ServiceProvider.GetRequiredService<DemoSeeder>()
            .ReanchorAsync(DemoSeedSpec.Build(), ct);
    }

    public async Task<KeycloakSeedSummary?> ConvergeKeycloakAsync(bool recreateRealm, CancellationToken ct)
    {
        var step = new KeycloakSeedStep(_environment, static () => new HttpClient());
        return await step.RunAsync(false, recreateRealm, DemoSeedSpec.Staff(), ct);
    }

    public async Task<SeedSummary> SeedDemoAsync(bool wipeFirst, CancellationToken ct)
    {
        await using var scope = Scope();
        var seeder = scope.ServiceProvider.GetRequiredService<DemoSeeder>();
        if (_options.Verbose) seeder.Progress = Console.Out;
        return wipeFirst ? await seeder.ReseedAsync(ct) : await seeder.RunAsync(ct);
    }

    public async Task<int> SendDemoInvitationsAsync(CancellationToken ct)
    {
        await using var scope = Scope();
        var seeder = scope.ServiceProvider.GetRequiredService<DemoInvitationSeeder>();
        if (_options.Verbose) seeder.Progress = Console.Out;
        return await seeder.RunAsync(ct);
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
```

- [ ] **Step 4: Replace the fixed demo specification and seed paths**

Delete `demo-seed.json` and remove its EmbeddedResource item. Put the whole deterministic graph in
DemoSeedSpec. Keep the GUID groups below: tests, local Keycloak convergence and screenshots may rely
on them. The offset-change helper scans calendar days, so `--reanchor` always retains a real transition.

```csharp
// src/EventBooking.SeedData/DemoSeedSpec.cs (complete)
using EventBooking.Domain.Access;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;

namespace EventBooking.SeedData;

public interface IDemoProposalScenario
{
    string LocationCode { get; }
    DateOnly Date { get; }
    TimeOnly StartTime { get; }
    int DurationMinutes { get; }
    IReadOnlyList<string> TypeCodes { get; }
    IReadOnlyList<string> AcceptedTypeCodes { get; }
}

public sealed record DemoLocationSpec(Guid Id, string Code, string Name, string Address, string TimeZoneId, bool IsActive);
public sealed record DemoAppointmentTypeSpec(Guid Id, string Code, string Name, bool IsActive, string? ManagerUsername);
public sealed record DemoAttendeeGroupSpec(Guid Id, string Code, string Name, IReadOnlyList<string> TypeCodes, bool IsActive);
public sealed record DemoEventSpec(
    Guid Id, string LocationCode, DateOnly Date, TimeOnly StartTime, int DurationMinutes,
    IReadOnlyList<string> TypeCodes, IReadOnlyList<string> AcceptedTypeCodes) : IDemoProposalScenario;
public sealed record DemoProposalSpec(
    Guid Id, string LocationCode, DateOnly Date, TimeOnly StartTime, int DurationMinutes,
    IReadOnlyList<string> TypeCodes, IReadOnlyList<string> AcceptedTypeCodes) : IDemoProposalScenario;
public enum DemoRecovery { None, Pending, Completed }
public sealed record DemoAttendeeSpec(
    Guid Id, string Name, string Email, string GroupCode, AttendeeStatus Status,
    IReadOnlyList<BookingAppointmentStatus> AppointmentStatuses,
    DemoRecovery Recovery, bool SendInvitation = false);

public sealed record StaffProfileSpec(
    string Username, string GivenName, string FamilyName, string Email,
    Guid UserId, StaffId StaffId, IReadOnlyList<Role> Roles, Guid? AppointmentTypeId);

public sealed record DemoDataset(
    DateOnly Anchor,
    IReadOnlyList<DemoLocationSpec> Locations,
    IReadOnlyList<DemoAppointmentTypeSpec> AppointmentTypes,
    IReadOnlyList<DemoAttendeeGroupSpec> AttendeeGroups,
    IReadOnlyList<StaffProfileSpec> Staff,
    IReadOnlyList<DemoEventSpec> Events,
    IReadOnlyList<DemoProposalSpec> OpenProposals,
    IReadOnlyList<DemoAttendeeSpec> Attendees);

public static class DemoSeedSpec
{
    private static DateOnly? _anchor;
    public static DateOnly AnchorDate() => _anchor ?? new DateOnly(2026, 9, 22);
    public static bool AnchorOverridden => _anchor.HasValue;
    public static void OverrideAnchor(DateOnly? anchor) => _anchor = anchor;

    public static DemoDataset Build()
    {
        var anchor = AnchorDate();
        var locations = new[]
        {
            new DemoLocationSpec(Id(1, 1), "LONDON", "London Centre", "1 Example Street, London", "Europe/London", true),
            new DemoLocationSpec(Id(1, 2), "MANCHESTER", "Manchester Centre", "2 Example Street, Manchester", "Europe/London", false),
            new DemoLocationSpec(Id(1, 3), "DUBLIN", "Dublin Centre", "3 Example Street, Dublin", "Europe/Dublin", true),
        };
        var types = new[]
        {
            new DemoAppointmentTypeSpec(Id(2, 1), "MED", "Medical", true, "coordinator.med"),
            new DemoAppointmentTypeSpec(Id(2, 2), "FIT", "Fitness", true, "manager.fit"),
            new DemoAppointmentTypeSpec(Id(2, 3), "IND", "Induction", true, "manager.ind"),
            new DemoAppointmentTypeSpec(Id(2, 4), "LAB", "Laboratory", true, "manager.lab"),
            new DemoAppointmentTypeSpec(Id(2, 5), "ESC", "Escalation", true, null),
            new DemoAppointmentTypeSpec(Id(2, 6), "DOC", "Document review", false, null),
        };
        var typeByCode = types.ToDictionary(x => x.Code);
        var staff = new[]
        {
            Staff("admin", 1, "Ari", "Admin", [Role.Admin], null),
            Staff("coordinator", 2, "Casey", "Coordinator", [Role.Coordinator], null),
            Staff("coordinator.med", 3, "Morgan", "Medical", [Role.Coordinator, Role.Manager], typeByCode["MED"].Id),
            Staff("manager.fit", 4, "Frankie", "Fitness", [Role.Manager], typeByCode["FIT"].Id),
            Staff("manager.ind", 5, "Indra", "Induction", [Role.Manager], typeByCode["IND"].Id),
            Staff("manager.lab", 6, "Luca", "Laboratory", [Role.Manager], typeByCode["LAB"].Id),
            Staff("appointment.med", 7, "Sam", "Appointments", [Role.AppointmentStaff], typeByCode["MED"].Id),
            Staff("appointment.unscoped", 8, "Taylor", "Unscoped", [Role.AppointmentStaff], null),
        };
        var groups = new[]
        {
            new DemoAttendeeGroupSpec(Id(3, 1), "IND_ONLY", "Induction only", ["IND"], true),
            new DemoAttendeeGroupSpec(Id(3, 2), "CORE_THREE", "Medical fitness and induction", ["MED", "FIT", "IND"], true),
            new DemoAttendeeGroupSpec(Id(3, 3), "DOC_HISTORY", "Historical document review", ["MED", "DOC"], false),
            new DemoAttendeeGroupSpec(Id(3, 4), "FIT_ESC", "Fitness and escalation", ["FIT", "ESC"], true),
        };
        var dst = NextOffsetChange(anchor.AddDays(1), "Europe/Dublin");
        var events = new[]
        {
            Event(1, "LONDON", anchor.AddDays(3), 9, 0, 60, ["MED"]),
            Event(2, "DUBLIN", anchor.AddDays(6), 9, 30, 90, ["MED", "FIT"]),
            Event(3, "LONDON", anchor.AddDays(9), 10, 0, 240, ["MED", "FIT", "IND"]),
            Event(4, "DUBLIN", dst, 12, 0, 480, ["MED", "FIT", "IND", "LAB"]),
            Event(5, "LONDON", anchor.AddDays(12), 8, 0, 240, ["MED", "FIT", "IND", "LAB"]),
            Event(6, "DUBLIN", anchor.AddDays(15), 8, 30, 240, ["MED", "FIT", "IND", "LAB"]),
            Event(7, "LONDON", anchor.AddDays(18), 9, 0, 240, ["MED", "FIT", "IND", "LAB"]),
        };
        var proposals = new[]
        {
            new DemoProposalSpec(Id(6, 1), "LONDON", anchor.AddDays(20), new TimeOnly(9, 0), 90,
                ["MED", "FIT"], ["MED"]),
            new DemoProposalSpec(Id(6, 2), "DUBLIN", anchor.AddDays(22), new TimeOnly(10, 0), 240,
                ["MED", "FIT", "IND", "LAB"], ["MED", "FIT"]),
        };
        var attendees = new[]
        {
            Person(1, "Nia New", "IND_ONLY", AttendeeStatus.NotYetInvited, [], send: true),
            Person(2, "Avery Awaiting", "FIT_ESC", AttendeeStatus.AwaitingAvailability, []),
            Person(3, "Ira Invited", "IND_ONLY", AttendeeStatus.Invited, []),
            Person(4, "Blair Booked", "CORE_THREE", AttendeeStatus.Booked, [BookingAppointmentStatus.Expected]),
            Person(5, "Chris Checked", "CORE_THREE", AttendeeStatus.Booked, [BookingAppointmentStatus.CheckedIn]),
            Person(6, "Rae Ready", "CORE_THREE", AttendeeStatus.Booked, [BookingAppointmentStatus.Completed]),
            Person(7, "Noah No Response", "IND_ONLY", AttendeeStatus.NoResponseNeedsFollowUp, []),
            Person(8, "Parker Pending Recovery", "CORE_THREE", AttendeeStatus.Booked,
                [BookingAppointmentStatus.NoShow], DemoRecovery.Pending),
            Person(9, "Robin Recovered", "CORE_THREE", AttendeeStatus.Booked,
                [BookingAppointmentStatus.NoShow, BookingAppointmentStatus.Completed], DemoRecovery.Completed),
        };
        return new DemoDataset(anchor, locations, types, groups, staff, events, proposals, attendees);
    }

    public static IReadOnlyList<StaffProfileSpec> Staff() => Build().Staff;
    public static Guid AdminUserId() => Staff().Single(x => x.Roles.SequenceEqual([Role.Admin])).UserId;
    public static Guid CoordinatorUserId() => Staff().Single(x => x.Username == "coordinator").UserId;
    public static IReadOnlyDictionary<Guid, Guid> ManagerForType() => Staff()
        .Where(x => x.Roles.Contains(Role.Manager))
        .ToDictionary(x => x.AppointmentTypeId!.Value, x => x.UserId);

    private static StaffProfileSpec Staff(
        string username, int number, string given, string family, IReadOnlyList<Role> roles, Guid? type) =>
        new(username, given, family, $"{username}@example.test", Id(4, number),
            new StaffId($"DEMO{number:000}"), roles, type);
    private static DemoEventSpec Event(
        int number, string location, DateOnly date, int hour, int minute, int duration, string[] types) =>
        new(Id(5, number), location, date, new TimeOnly(hour, minute), duration, types, types);
    private static DemoAttendeeSpec Person(
        int number, string name, string group, AttendeeStatus status,
        BookingAppointmentStatus[] appointments, DemoRecovery recovery = DemoRecovery.None, bool send = false) =>
        new(Id(7, number), name, $"demo.attendee.{number:00}@example.test", group, status,
            appointments, recovery, send);
    private static Guid Id(int family, int number) =>
        Guid.Parse($"{family}0000000-0000-0000-0000-{number:000000000000}");

    private static DateOnly NextOffsetChange(DateOnly first, string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        for (var date = first; date < first.AddYears(2); date = date.AddDays(1))
        {
            var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
            if (zone.GetUtcOffset(start) != zone.GetUtcOffset(start.AddDays(1))) return date;
        }
        throw new SeedException($"No offset change found for {timeZoneId} within two years of {first:yyyy-MM-dd}.");
    }
}
```

Replace DemoSeeder and DemoEventFactory around this graph. The following rules are implementation
requirements, not optional prose; keep each helper as a real method with the exact natural key and
domain call shown:

```csharp
// src/EventBooking.SeedData/DemoEventFactory.cs (complete)
using EventBooking.Domain.Events;
using EventBooking.Domain.Time;
using EventBooking.Infrastructure.Persistence;

namespace EventBooking.SeedData;

internal static class DemoEventFactory
{
    internal static EventProposal CreateProposal(
        EventBookingDbContext database,
        IDemoProposalScenario spec,
        DemoDataset data,
        IEventWindowZones zones)
    {
        var location = data.Locations.Single(x => x.Code == spec.LocationCode);
        var types = spec.TypeCodes.Select(code => data.AppointmentTypes.Single(x => x.Code == code)).ToList();
        var staff = data.Staff.ToDictionary(x => x.Username);
        var proposer = types[0];
        var manager = staff[proposer.ManagerUsername!];
        var window = new EventWindow(spec.Date, spec.StartTime, spec.DurationMinutes);
        var proposal = EventProposal.Propose(
            spec is DemoProposalSpec p ? p.Id : Guid.NewGuid(),
            location.Id, location.IsActive, location.TimeZoneId, window, zones,
            window.StartInstant(zones, location.TimeZoneId).AddDays(-1),
            types.Select(x => new ProposableAppointmentType(
                x.Id, x.Code, x.IsActive, x.ManagerUsername is not null)).ToList(),
            proposer.Id, manager.UserId, 12);
        foreach (var code in spec.AcceptedTypeCodes.Skip(1))
        {
            var type = data.AppointmentTypes.Single(x => x.Code == code);
            proposal.Accept(type.Id, staff[type.ManagerUsername!].UserId, 12);
        }
        database.EventProposals.Add(proposal);
        return proposal;
    }

    internal static Event CreateEvent(
        EventBookingDbContext database, DemoEventSpec spec, DemoDataset data, IEventWindowZones zones)
    {
        var proposal = CreateProposal(database, spec, data, zones);
        var eventItem = Event.CreateFrom(spec.Id, proposal);
        database.Events.Add(eventItem);
        return eventItem;
    }
}
```

For DemoSeeder, retain the existing constructor's repositories and journey helpers, inject
IEventWindowZones as `zones` beside the UTC-only IClock, replace all fixed-ID methods with these
exact graph-driven methods and extend SeedSummary with
LocationsEnsured, AppointmentTypesEnsured, AttendeeGroupsEnsured and AttendeesEnsured. The complete
top-level run and the non-negotiable persistence methods are:

```csharp
// src/EventBooking.SeedData/DemoSeeder.cs — replace RunAsync, ReseedAsync and fixed-set helpers
public sealed record SeedSummary(
    int LocationsEnsured,
    int AppointmentTypesEnsured,
    int AttendeeGroupsEnsured,
    int IdentitiesEnsured,
    int ProfilesEnsured,
    int EventsEnsured,
    int ProposalsEnsured,
    int AcceptancesApplied,
    int AttendeesEnsured)
{
    public static readonly SeedSummary Empty = new(0, 0, 0, 0, 0, 0, 0, 0, 0);
}

public async Task<SeedSummary> RunAsync(CancellationToken ct)
{
    var data = DemoSeedSpec.Build();
    var locations = await EnsureLocationsAsync(data, ct);
    var types = await EnsureAppointmentTypesAsync(data, ct);
    var groups = await EnsureAttendeeGroupsAsync(data, ct);
    var identities = await EnsureIdentitiesAsync(ct);
    var profiles = await EnsureProfilesAsync(ct);
    var events = await EnsureEventsAsync(data, ct);
    var proposals = await EnsureOpenProposalsAsync(data, ct);
    var attendees = await EnsureAttendeesAsync(data, ct);
    return new SeedSummary(locations, types, groups, identities, profiles, events, proposals, 0, attendees);
}

public async Task<SeedSummary> ReseedAsync(CancellationToken ct)
{
    await database.Database.ExecuteSqlRawAsync(
        """
        DO $$ DECLARE row record;
        BEGIN
          FOR row IN SELECT tablename FROM pg_tables
                     WHERE schemaname='public' AND tablename <> '__EFMigrationsHistory'
          LOOP EXECUTE 'TRUNCATE TABLE ' || quote_ident(row.tablename) || ' CASCADE'; END LOOP;
        END $$;
        """, ct);
    database.ChangeTracker.Clear();
    return await RunAsync(ct);
}

public async Task ReanchorAsync(DemoDataset target, CancellationToken ct)
{
    var eventIds = target.Events.Select(x => x.Id).ToList();
    var proposalIds = target.OpenProposals.Select(x => x.Id).ToList();
    await using var transaction = await database.Database.BeginTransactionAsync(ct);
    var locationByCode = await database.Locations.AsNoTracking().ToDictionaryAsync(x => x.Code, ct);
    var existingEvents = await database.Events.AsNoTracking()
        .Where(x => eventIds.Contains(x.Id))
        .Select(x => new
        {
            x.Id, x.LocationId, x.ProposalId,
            Date = x.Window.Date, Start = x.Window.StartTime, x.Window.DurationMinutes,
        })
        .ToListAsync(ct);

    foreach (var spec in target.Events)
    {
        var current = existingEvents.SingleOrDefault(x => x.Id == spec.Id);
        if (current is null) continue;
        var locationId = locationByCode[spec.LocationCode].Id;
        if (current.LocationId != locationId || current.Start != spec.StartTime
            || current.DurationMinutes != spec.DurationMinutes || current.ProposalId == Guid.Empty)
            throw new SeedException($"Demo Event {spec.Id} no longer has its seeded identity.");
        var collision = await database.Events.AnyAsync(x => x.Id != spec.Id
            && x.LocationId == locationId && x.Window.Date == spec.Date
            && x.Window.StartTime == spec.StartTime, ct);
        if (collision)
            throw new SeedException($"Cannot reanchor demo Event {spec.Id}; its target window is occupied.");
        if (current.Date == spec.Date) continue;
        var eventsMoved = await database.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "event" SET "date" = {spec.Date} WHERE "id" = {spec.Id}""", ct);
        var proposalsMoved = await database.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "event_proposal" SET "date" = {spec.Date} WHERE "id" = {current.ProposalId}""", ct);
        if (eventsMoved != 1 || proposalsMoved != 1)
            throw new SeedException($"Demo Event {spec.Id} could not be reanchored atomically.");
    }

    var existingProposals = await database.EventProposals.AsNoTracking()
        .Where(x => proposalIds.Contains(x.Id))
        .Select(x => new
        {
            x.Id, x.LocationId, x.Status,
            Date = x.Window.Date, Start = x.Window.StartTime, x.Window.DurationMinutes,
        })
        .ToListAsync(ct);
    foreach (var spec in target.OpenProposals)
    {
        var current = existingProposals.SingleOrDefault(x => x.Id == spec.Id);
        if (current is null) continue;
        var locationId = locationByCode[spec.LocationCode].Id;
        if (current.LocationId != locationId || current.Start != spec.StartTime
            || current.DurationMinutes != spec.DurationMinutes || current.Status != EventProposalStatus.Open)
            throw new SeedException($"Demo EventProposal {spec.Id} no longer has its seeded identity.");
        var collision = await database.EventProposals.AnyAsync(x => x.Id != spec.Id
            && x.LocationId == locationId && x.Window.Date == spec.Date
            && x.Window.StartTime == spec.StartTime, ct);
        if (collision)
            throw new SeedException($"Cannot reanchor demo EventProposal {spec.Id}; its target window is occupied.");
        if (current.Date == spec.Date) continue;
        if (await database.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "event_proposal" SET "date" = {spec.Date} WHERE "id" = {spec.Id}""", ct) != 1)
            throw new SeedException($"Demo EventProposal {spec.Id} could not be reanchored.");
    }

    await transaction.CommitAsync(ct);
    database.ChangeTracker.Clear();
}

private async Task<int> EnsureLocationsAsync(DemoDataset data, CancellationToken ct)
{
    var added = 0;
    foreach (var spec in data.Locations)
    {
        var current = await database.Locations.SingleOrDefaultAsync(x => x.Code == spec.Code, ct);
        if (current is null)
        {
            current = Location.Create(spec.Id, spec.Code, spec.Name, spec.Address, spec.TimeZoneId, zones);
            if (!spec.IsActive) current.Deactivate(LocationUsage.None);
            database.Locations.Add(current);
            added++;
        }
        else if (current.Id != spec.Id)
            throw new SeedException($"Location code {spec.Code} belongs to a non-demo identifier.");
    }
    await database.SaveChangesAsync(ct);
    return added;
}

private async Task<int> EnsureAppointmentTypesAsync(DemoDataset data, CancellationToken ct)
{
    var added = 0;
    foreach (var spec in data.AppointmentTypes)
    {
        var current = await database.AppointmentTypes.SingleOrDefaultAsync(x => x.Code == spec.Code, ct);
        if (current is null)
        {
            current = AppointmentType.Create(spec.Id, spec.Code, spec.Name);
            database.AppointmentTypes.Add(current);
            added++;
        }
        else if (current.Id != spec.Id)
            throw new SeedException($"Appointment type code {spec.Code} belongs to a non-demo identifier.");
    }
    await database.SaveChangesAsync(ct);
    return added;
}

private async Task<int> EnsureAttendeeGroupsAsync(DemoDataset data, CancellationToken ct)
{
    var typeByCode = await database.AppointmentTypes.ToDictionaryAsync(x => x.Code, ct);
    var added = 0;
    foreach (var spec in data.AttendeeGroups)
    {
        var current = await database.AttendeeGroups.SingleOrDefaultAsync(x => x.Code == spec.Code, ct);
        if (current is null)
        {
            var ids = spec.TypeCodes.Select(code => typeByCode[code].Id).ToList();
            current = AttendeeGroup.Create(spec.Id, spec.Code, spec.Name, ids, ids);
            if (!spec.IsActive) current.Deactivate(0);
            database.AttendeeGroups.Add(current);
            added++;
        }
        else if (current.Id != spec.Id)
            throw new SeedException($"Attendee group code {spec.Code} belongs to a non-demo identifier.");
    }
    await database.SaveChangesAsync(ct);
    var doc = typeByCode["DOC"];
    if (doc.IsActive)
    {
        doc.Deactivate(AppointmentTypeUsage.None);
        await database.SaveChangesAsync(ct);
    }
    return added;
}

private async Task<int> EnsureEventsAsync(DemoDataset data, CancellationToken ct)
{
    var locationByCode = await database.Locations.ToDictionaryAsync(x => x.Code, ct);
    var added = 0;
    foreach (var spec in data.Events)
    {
        var locationId = locationByCode[spec.LocationCode].Id;
        var current = await database.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == spec.Id, ct);
        if (current is not null)
        {
            if (current.LocationId != locationId || current.Window.Date != spec.Date
                || current.Window.StartTime != spec.StartTime
                || current.Window.DurationMinutes != spec.DurationMinutes)
                throw new SeedException($"Demo Event {spec.Id} exists with a different natural key; use --reanchor.");
            continue;
        }
        if (await database.Events.AnyAsync(x => x.LocationId == locationId
            && x.Window.Date == spec.Date && x.Window.StartTime == spec.StartTime, ct))
            throw new SeedException($"The demo Event target window for {spec.Id} belongs to a non-demo row.");
        DemoEventFactory.CreateEvent(database, spec, data, zones);
        await database.SaveChangesAsync(ct);
        added++;
    }
    return added;
}

private async Task<int> EnsureOpenProposalsAsync(DemoDataset data, CancellationToken ct)
{
    var locationByCode = await database.Locations.ToDictionaryAsync(x => x.Code, ct);
    var added = 0;
    foreach (var spec in data.OpenProposals)
    {
        var locationId = locationByCode[spec.LocationCode].Id;
        var current = await database.EventProposals.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == spec.Id, ct);
        if (current is not null)
        {
            if (current.Status != EventProposalStatus.Open || current.LocationId != locationId
                || current.Window.Date != spec.Date || current.Window.StartTime != spec.StartTime
                || current.Window.DurationMinutes != spec.DurationMinutes)
                throw new SeedException($"Demo EventProposal {spec.Id} exists with a different natural key; use --reanchor.");
            continue;
        }
        if (await database.EventProposals.AnyAsync(x => x.LocationId == locationId
            && x.Window.Date == spec.Date && x.Window.StartTime == spec.StartTime, ct))
            throw new SeedException($"The demo EventProposal target window for {spec.Id} belongs to a non-demo row.");
        DemoEventFactory.CreateProposal(database, spec, data, zones);
        await database.SaveChangesAsync(ct);
        added++;
    }
    return added;
}
```

Replace the predecessor attendee loop and journey helper with the complete graph-driven methods
below. Every invite option points at a real Event from DemoDataset; every status transition goes
through a domain method; and pending/completed recovery rows use the recovery factories. Keep the
existing SeedId helper and constructor dependencies. Add the Booking, Invite and appointment
namespaces required by these methods.

```csharp
// src/EventBooking.SeedData/DemoSeeder.cs — complete attendee/journey replacement
private async Task<int> EnsureAttendeesAsync(DemoDataset data, CancellationToken ct)
{
    var created = 0;
    foreach (var spec in data.Attendees)
    {
        if (await attendees.GetByEmailAsync(spec.Email, ct) is not null) continue;
        var groupSpec = data.AttendeeGroups.Single(x => x.Code == spec.GroupCode);
        if (!groupSpec.IsActive)
            throw new SeedException($"Demo attendee {spec.Email} cannot use inactive group {spec.GroupCode}.");
        var group = await groups.GetByCodeAsync(spec.GroupCode, ct)
            ?? throw new SeedException($"Demo AttendeeGroup {spec.GroupCode} is missing.");
        var result = await saveAttendee.CreateAsync(
            new CreateAttendeeCommand(CoordinatorUserId(), spec.Name, spec.Email, group.Id), ct);
        if (result.IsFailure)
            throw new SeedException($"Creating demo Attendee {spec.Email} failed: {result.Error}.");
        var attendee = await attendees.GetAsync(result.Value, ct)
            ?? throw new SeedException($"Created demo Attendee {spec.Email} cannot be loaded.");
        await BuildJourneyAsync(attendee, group, groupSpec, spec, data, ct);
        created++;
    }
    return created;
}

private async Task BuildJourneyAsync(
    Attendee attendee,
    AttendeeGroup group,
    DemoAttendeeGroupSpec groupSpec,
    DemoAttendeeSpec spec,
    DemoDataset data,
    CancellationToken ct)
{
    var now = clock.UtcNow;
    switch (spec.Status)
    {
        case AttendeeStatus.NotYetInvited:
            return;
        case AttendeeStatus.AwaitingAvailability:
            attendee.MarkAwaitingAvailability(now);
            await unitOfWork.SaveChangesAsync(ct);
            return;
        case AttendeeStatus.Invited:
        case AttendeeStatus.NoResponseNeedsFollowUp:
        {
            var invite = CreateInitialInvite(attendee, group, groupSpec, spec, data, now);
            database.Invites.Add(invite);
            attendee.MarkInvited(now);
            if (spec.Status == AttendeeStatus.NoResponseNeedsFollowUp)
            {
                invite.MarkExpired();
                attendee.MarkNoResponse(now);
            }
            await unitOfWork.SaveChangesAsync(ct);
            return;
        }
        case AttendeeStatus.Booked:
            await BuildBookedJourneyAsync(attendee, group, groupSpec, spec, data, now, ct);
            return;
        default:
            throw new SeedException($"Demo AttendeeStatus {spec.Status} is not supported.");
    }
}

private async Task BuildBookedJourneyAsync(
    Attendee attendee,
    AttendeeGroup group,
    DemoAttendeeGroupSpec groupSpec,
    DemoAttendeeSpec spec,
    DemoDataset data,
    DateTimeOffset now,
    CancellationToken ct)
{
    if (spec.AppointmentStatuses.Count == 0)
        throw new SeedException($"Booked demo Attendee {spec.Email} needs an appointment status.");
    if (spec.Recovery != DemoRecovery.None
        && spec.AppointmentStatuses[0] != BookingAppointmentStatus.NoShow)
        throw new SeedException($"Recovery demo Attendee {spec.Email} must begin with NoShow.");
    if (spec.Recovery == DemoRecovery.Completed && spec.AppointmentStatuses.Count != 2)
        throw new SeedException($"Completed recovery demo Attendee {spec.Email} needs two statuses.");
    var eligible = EligibleEvents(groupSpec, data);
    if (eligible.Count < 5 && spec.Recovery != DemoRecovery.None)
        throw new SeedException($"Recovery demo Attendee {spec.Email} needs five eligible Events.");
    var initialOptions = eligible.Take(3).ToList();
    var invite = CreateInitialInvite(attendee, group, spec, initialOptions, data, now);
    database.Invites.Add(invite);
    attendee.MarkInvited(now);
    var booking = Booking.Create(
        SeedId(attendee.Email, "initial:booking"), invite, initialOptions[0].Id, now);
    invite.MarkUsed();
    attendee.MarkBooked(now);
    database.Bookings.Add(booking);
    var appointments = group.RequiredAppointmentTypeIds.Order()
        .Select(typeId => BookingAppointment.Create(
            SeedId(attendee.Email, $"initial:appointment:{typeId}"), booking.Id, typeId))
        .ToList();
    database.BookingAppointments.AddRange(appointments);
    ApplyAppointmentStatus(appointments[0], spec.AppointmentStatuses[0], now);

    if (spec.Recovery != DemoRecovery.None)
    {
        var recoveryOptions = eligible.Skip(2).Take(3).ToList();
        var locationByCode = data.Locations.ToDictionary(x => x.Code);
        var originalLocationId = locationByCode[initialOptions[0].LocationCode].Id;
        var otherLocationIds = recoveryOptions.Select(x => locationByCode[x.LocationCode].Id)
            .Where(x => x != originalLocationId).Distinct().ToList();
        var recoveryInvite = Invite.CreateRecovery(
            SeedId(attendee.Email, "recovery:invite"), attendee.Id, booking.Id, now.AddDays(7),
            originalLocationId, otherLocationIds, recoveryOptions.Select(x => x.Id),
            [appointments[0].AppointmentTypeId]);
        database.Invites.Add(recoveryInvite);

        if (spec.Recovery == DemoRecovery.Completed)
        {
            var recovery = Booking.CreateRecovery(
                SeedId(attendee.Email, "recovery:booking"), recoveryInvite, booking,
                recoveryOptions[0].Id, now.AddMinutes(5));
            recoveryInvite.MarkUsed();
            database.Bookings.Add(recovery);
            var recoveryAppointment = BookingAppointment.Create(
                SeedId(attendee.Email, "recovery:appointment"), recovery.Id,
                appointments[0].AppointmentTypeId);
            database.BookingAppointments.Add(recoveryAppointment);
            ApplyAppointmentStatus(recoveryAppointment, spec.AppointmentStatuses[1], now);
        }
    }

    await unitOfWork.SaveChangesAsync(ct);
}

private Invite CreateInitialInvite(
    Attendee attendee,
    AttendeeGroup group,
    DemoAttendeeGroupSpec groupSpec,
    DemoAttendeeSpec spec,
    DemoDataset data,
    DateTimeOffset now) =>
    CreateInitialInvite(attendee, group, spec, EligibleEvents(groupSpec, data).Take(3).ToList(), data, now);

private Invite CreateInitialInvite(
    Attendee attendee,
    AttendeeGroup group,
    DemoAttendeeSpec spec,
    IReadOnlyList<DemoEventSpec> options,
    DemoDataset data,
    DateTimeOffset now)
{
    if (options.Count != 3)
        throw new SeedException($"Demo Attendee {spec.Email} needs exactly three eligible Events.");
    var locationByCode = data.Locations.ToDictionary(x => x.Code);
    var locationIds = options.Select(x => locationByCode[x.LocationCode].Id).Distinct().ToList();
    return Invite.CreateInitial(
        SeedId(attendee.Email, "initial:invite"), attendee.Id, now.AddDays(7), locationIds,
        options.Select(x => x.Id), group.RequiredAppointmentTypeIds, 0);
}

private static IReadOnlyList<DemoEventSpec> EligibleEvents(
    DemoAttendeeGroupSpec group, DemoDataset data) => data.Events
    .Where(item => group.TypeCodes.All(code => item.TypeCodes.Contains(code)))
    .OrderBy(item => item.Date).ThenBy(item => item.StartTime).ToList();

private void ApplyAppointmentStatus(
    BookingAppointment appointment, BookingAppointmentStatus status, DateTimeOffset now)
{
    switch (status)
    {
        case BookingAppointmentStatus.Expected:
            return;
        case BookingAppointmentStatus.CheckedIn:
            appointment.TransitionTo(status, CoordinatorUserId(), now,
                checkInAllowed: true, noShowAllowed: false);
            return;
        case BookingAppointmentStatus.Completed:
            appointment.TransitionTo(BookingAppointmentStatus.CheckedIn, CoordinatorUserId(), now,
                checkInAllowed: true, noShowAllowed: false);
            appointment.TransitionTo(status, CoordinatorUserId(), now,
                checkInAllowed: true, noShowAllowed: false);
            return;
        case BookingAppointmentStatus.NoShow:
            appointment.TransitionTo(status, CoordinatorUserId(), now,
                checkInAllowed: false, noShowAllowed: true);
            return;
        default:
            throw new SeedException($"Demo BookingAppointmentStatus {status} is not supported.");
    }
}
```

The second run finds every attendee by natural email and returns zero. Replace every
AppointmentTypeIds, AttendeeGroupIds and TransitionalLocation reference in DemoSeeder; the focused
build and retirement proof below must find none.

Demo invitation delivery must not drain unrelated application email. Extend the Task 18 dispatcher
with a single-row claim that uses the same lease, backoff and rendering path as the batch loop:

```csharp
// src/EventBooking.Infrastructure/Email/ClaimQuery.cs — add beside Sql
public const string OneSql = """
    UPDATE email_log SET claimed_at = @now, claim_count = claim_count + 1,
        correlation_id = @correlationId
     WHERE id = @deliveryId
       AND status = 3
       AND (claimed_at IS NULL OR claimed_at < @now - make_interval(mins => 5))
       AND (not_before IS NULL OR not_before <= @now)
    RETURNING id;
    """;
```

```csharp
// src/EventBooking.Infrastructure/Email/OutboxDispatcher.cs — replace DispatchOnceAsync and add
// DispatchOneAsync plus the shared helper; keep SendRowAsync and MarkTransientAsync unchanged.
public async Task<int> DispatchOnceAsync(CancellationToken ct = default)
{
    using var scope = scopes.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
    var clock = scope.ServiceProvider.GetRequiredService<IClock>();
    var now = clock.UtcNow;
    var claimed = await ClaimAsync(context, Guid.NewGuid().ToString(), now, ct);
    var sent = 0;
    foreach (var row in claimed) sent += await DispatchClaimedAsync(scope, row, now, ct);
    return sent;
}

public async Task<int> DispatchOneAsync(Guid deliveryId, CancellationToken ct = default)
{
    using var scope = scopes.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
    var clock = scope.ServiceProvider.GetRequiredService<IClock>();
    var now = clock.UtcNow;
    var ids = await context.Database.SqlQueryRaw<Guid>(ClaimQuery.OneSql,
        new NpgsqlParameter("@now", now),
        new NpgsqlParameter("@correlationId", Guid.NewGuid().ToString()),
        new NpgsqlParameter("@deliveryId", deliveryId)).ToListAsync(ct);
    if (ids.Count == 0) return 0;
    var row = await context.EmailLogs.SingleAsync(x => x.Id == deliveryId, ct);
    return await DispatchClaimedAsync(scope, row, now, ct);
}

private async Task<int> DispatchClaimedAsync(
    IServiceScope scope, EmailLog row, DateTimeOffset now, CancellationToken ct)
{
    try
    {
        await SendRowAsync(scope, row, now, ct);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Outbox send failed for delivery {DeliveryId}.", row.Id);
        await MarkTransientAsync(scope, row, now, ct);
    }
    return row.Status == EmailStatus.Sent ? 1 : 0;
}
```

Add this focused case to OutboxDispatcherTests before implementation. It proves the seed path cannot
claim a different due row while sending its selected invitation:

```csharp
[Fact]
public async Task Targeted_dispatch_leaves_other_due_rows_unclaimed()
{
    var other = await StagePendingAsync(EmailTemplate.BookingConfirmation);
    var selected = await StagePendingAsync(EmailTemplate.AttendeeInvite);

    Assert.Equal(1, await Dispatcher(transportA).DispatchOneAsync(selected, default));

    Assert.Equal(EmailStatus.Sent, await StatusOfAsync(selected));
    Assert.Equal(EmailStatus.Pending, await StatusOfAsync(other));
    Assert.Single(transportA.Sent);
}
```

Replace DemoInvitationSeeder with this complete implementation. It selects only attendees marked
SendInvitation, validates their active group, uses the LONDON and DUBLIN locations with
InviteAttendeeHandler, and dispatches only the matching outbox row. A sent delivery is skipped; a
failed delivery goes through RetryEmailHandler to create a fresh pending row; and a due pending row
uses the targeted dispatcher directly.

```csharp
// src/EventBooking.SeedData/DemoInvitationSeeder.cs (complete)
using EventBooking.Application.Abstractions;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Time;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.SeedData;

public sealed class DemoInvitationSeeder(
    EventBookingDbContext database,
    IAttendeeRepository attendees,
    InviteAttendeeHandler inviteAttendee,
    RetryEmailHandler retryEmail,
    OutboxDispatcher dispatcher,
    IEventWindowZones zones,
    IClock clock)
{
    public TextWriter Progress { get; set; } = TextWriter.Null;

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var data = DemoSeedSpec.Build();
        var locationSpecs = data.Locations.ToDictionary(x => x.Code);
        if (data.Events.Any(item => item.Date <=
            zones.LocalDateOf(clock.UtcNow, locationSpecs[item.LocationCode].TimeZoneId)))
            throw new SeedException("Demo invitation dates are stale. Run with --reanchor.");

        var locationCodes = new[] { "LONDON", "DUBLIN" };
        var locations = await database.Locations.AsNoTracking()
            .Where(x => locationCodes.Contains(x.Code)).ToDictionaryAsync(x => x.Code, ct);
        if (locationCodes.Any(code => !locations.TryGetValue(code, out var item) || !item.IsActive))
            throw new SeedException("Demo invitations require active LONDON and DUBLIN locations.");
        var locationIds = locationCodes.Select(code => locations[code].Id).ToList();
        var recipients = data.Attendees.Where(x => x.SendInvitation).ToList();
        var sent = 0;

        foreach (var spec in recipients)
        {
            var group = data.AttendeeGroups.Single(x => x.Code == spec.GroupCode);
            if (!group.IsActive)
                throw new SeedException($"Demo invitation group {group.Code} is inactive.");
            var attendee = await attendees.GetByEmailAsync(spec.Email, ct)
                ?? throw new SeedException($"Seed Attendee is missing: {spec.Email}.");
            var persistedGroup = await database.AttendeeGroups.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == attendee.AttendeeGroupId, ct);
            if (persistedGroup is null || persistedGroup.Code != group.Code || !persistedGroup.IsActive)
                throw new SeedException($"Demo invitation requires active group {group.Code}.");
            if (attendee.Status == AttendeeStatus.Booked)
            {
                Report($"Preserved existing booking: {spec.Email}.");
                continue;
            }

            var hasHistory = await database.Invites.AnyAsync(x => x.AttendeeId == attendee.Id, ct);
            var pendingInvite = await database.Invites.AsNoTracking().SingleOrDefaultAsync(
                x => x.AttendeeId == attendee.Id && x.Status == InviteStatus.Pending
                    && x.RecoveryOfBookingId == null, ct);
            if (!hasHistory)
            {
                var issued = await inviteAttendee.HandleAsync(
                    new InviteAttendeeCommand(DemoSeedSpec.CoordinatorUserId(), attendee.Id, locationIds), ct);
                if (issued.IsFailure)
                    throw new SeedException($"Demo invitation for {spec.Email} failed: {issued.Error}.");
                var inviteId = issued.Value.InviteId
                    ?? throw new SeedException($"Demo invitation for {spec.Email} produced no Invite.");
                pendingInvite = await database.Invites.AsNoTracking()
                    .SingleAsync(x => x.Id == inviteId, ct);
            }
            else if (pendingInvite is null || !pendingInvite.IsUsableAt(clock.UtcNow))
            {
                Report($"Preserved invitation history: {spec.Email}.");
                continue;
            }

            var delivery = await database.EmailLogs.AsNoTracking()
                .Where(x => x.AttendeeId == attendee.Id && x.InviteId == pendingInvite.Id
                    && x.TemplateName == EmailTemplate.AttendeeInvite
                    && x.Status != EmailStatus.Resolved)
                .OrderByDescending(x => x.SentAt).ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(ct)
                ?? throw new SeedException($"Pending demo invitation has no delivery: {spec.Email}.");
            if (delivery.Status == EmailStatus.Sent)
            {
                Report($"Invitation already delivered: {spec.Email}.");
                continue;
            }

            Guid deliveryId;
            if (delivery.Status == EmailStatus.Failed)
            {
                var retried = await retryEmail.HandleAsync(new RetryEmailCommand(
                    DemoSeedSpec.CoordinatorUserId(), attendee.Id, delivery.Id), ct);
                if (retried.IsFailure)
                    throw new SeedException($"Demo delivery retry for {spec.Email} failed: {retried.Error}.");
                deliveryId = retried.Value.EmailLogId;
            }
            else if (delivery.Status == EmailStatus.Pending)
            {
                deliveryId = delivery.Id;
            }
            else
            {
                throw new SeedException($"Demo delivery for {spec.Email} is {delivery.Status}.");
            }

            await dispatcher.DispatchOneAsync(deliveryId, ct);
            var status = await database.EmailLogs.AsNoTracking().Where(x => x.Id == deliveryId)
                .Select(x => x.Status).SingleAsync(ct);
            if (status != EmailStatus.Sent) throw DeliveryFailed(spec.Email, status);
            sent++;
            Report($"Invitation delivered: {spec.Email}.");
        }

        return sent;
    }

    private static SeedException DeliveryFailed(string recipient, EmailStatus status) => new(
        $"Demo invitation delivery for {recipient} is {status}; fix SMTP or wait for backoff and rerun.");

    private void Report(string message) => Progress.WriteLine($"[seed] {message}");
}
```

Extend KeycloakSeeder's user payload so the name mapper has source data:

```csharp
// src/EventBooking.SeedData/KeycloakSeeder.cs — inside KeycloakSeeder.BuildUserRepresentation
private static object BuildUserRepresentation(StaffProfileSpec person, string password) => new
{
    username = person.Username,
    enabled = true,
    firstName = person.GivenName,
    lastName = person.FamilyName,
    email = person.Email,
    emailVerified = true,
    attributes = new Dictionary<string, string[]> { ["staffId"] = [person.StaffId.Value] },
    credentials = new[] { new { type = "password", value = password, temporary = false } },
};
```

KeycloakSeeder already derives all realm role names from Role and converges exact mappings; do not
special-case LAB in provider code. Extend its tests to assert eight users, the LAB Manager mapping,
the unscoped AppointmentStaff user and the four fields above.

- [ ] **Step 5: Retire the single-zone clock**

Replace IClock and SystemClock and delete ClockOptions:

```csharp
// src/EventBooking.Application/Abstractions/IClock.cs (complete)
namespace EventBooking.Application.Abstractions;

/// <summary>The testable source of the current UTC instant.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
```

```csharp
// src/EventBooking.Infrastructure/Time/SystemClock.cs (complete)
using EventBooking.Application.Abstractions;

namespace EventBooking.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
```

Change the infrastructure overload to
`AddEventBookingInfrastructure(IServiceCollection, string connectionString, TokenOptions tokens)`,
remove the ClockOptions registration and retain `services.AddSingleton<IClock, SystemClock>()` plus
the NodaTime zone resolver. Remove the Clock member and constructor argument from
EventBookingSettings, remove the Infrastructure.Time using from EventBookingConfiguration, and
change the API and MCP calls to:

```csharp
// src/EventBooking.Api/EventBookingConfiguration.cs — complete settings record after retirement
namespace EventBooking.Api;

public sealed record EventBookingSettings(
    string ConnectionString,
    TokenOptions Tokens,
    EmailOptions Email,
    SmtpOptions Smtp,
    AttendeePortalOptions Portal,
    IReadOnlyList<string> AllowedOrigins,
    IReadOnlyList<string> ProxyNetworks,
    RateLimitSettings RateLimits,
    TimeSpan SweepInterval);

public static class EventBookingConfiguration
{
    // Retain the existing validation helpers and Bind implementation unchanged except that
    // Bind neither reads Clock:TimeZoneId nor passes a ClockOptions argument above.
}

// src/EventBooking.Api/Program.cs and src/EventBooking.Mcp/Program.cs
builder.Services.AddEventBookingInfrastructure(settings.ConnectionString, settings.Tokens);
```

Remove Clock__TimeZoneId parsing and the Clock property from DemoEmailOptions. Its constructor and
From result contain only AttendeePortalOptions, TokenOptions, EmailOptions and SmtpOptions.

Replace SystemClockTests with:

```csharp
// tests/EventBooking.Infrastructure.Tests/SystemClockTests.cs (complete)
using EventBooking.Infrastructure.Time;

namespace EventBooking.Infrastructure.Tests;

public sealed class SystemClockTests
{
    [Fact]
    public void ClockReportsAUtcInstant()
    {
        var before = DateTimeOffset.UtcNow;
        var actual = new SystemClock().UtcNow;
        var after = DateTimeOffset.UtcNow;
        Assert.Equal(TimeSpan.Zero, actual.Offset);
        Assert.InRange(actual, before, after);
    }
}
```

Run the retirement proof; every command must print no match:

```bash
rg -n "ClockOptions|Clock[:_]TimeZoneId|TodayAtTransitionalLocation|NowAtTransitionalLocation|DateAtTransitionalLocation|InstantAtTransitionalLocation|AppointmentTypeIds|AttendeeGroupIds|TransitionalLocation" \
  src/EventBooking.SeedData src/EventBooking.Api src/EventBooking.Mcp \
  src/EventBooking.Application/Abstractions/IClock.cs src/EventBooking.Infrastructure/Time \
  src/EventBooking.Infrastructure/DependencyInjection.cs
```

- [ ] **Step 6: Run focused and full verification**

```bash
dotnet test tests/EventBooking.SeedData.Tests --filter \
  "FullyQualifiedName~SeedCommandTests|FullyQualifiedName~DemoSeedSpecTests|FullyQualifiedName~DemoSeederIntegrationTests|FullyQualifiedName~KeycloakSeederTests|FullyQualifiedName~ReseedTests"
dotnet test tests/EventBooking.Infrastructure.Tests --filter \
  "FullyQualifiedName~SystemClockTests|FullyQualifiedName~Targeted_dispatch_leaves_other_due_rows_unclaimed"
dotnet build EventBooking.sln -warnaserror
dotnet test EventBooking.sln
```

Expected: PASS with zero skipped tests. Run `--demo` twice against a disposable PostgreSQL and
Mailpit, then compare the query below before and after the second run:

```bash
dotnet run --project src/EventBooking.SeedData -- "$SEED_TEST_CONNECTION" --demo --reanchor
psql "$SEED_TEST_CONNECTION" -Atc \
  "select (select count(*) from location),(select count(*) from appointment_type),(select count(*) from attendee_group),(select count(*) from event),(select count(*) from event_proposal),(select count(*) from attendee),(select count(*) from invite),(select count(*) from email_log);"
dotnet run --project src/EventBooking.SeedData -- "$SEED_TEST_CONNECTION" --demo --reanchor
psql "$SEED_TEST_CONNECTION" -Atc \
  "select (select count(*) from location),(select count(*) from appointment_type),(select count(*) from attendee_group),(select count(*) from event),(select count(*) from event_proposal),(select count(*) from attendee),(select count(*) from invite),(select count(*) from email_log);"
```

The two lines must be identical. **No count in this document is observed.** Record the executor's
real full-suite and SeedData counts in HANDOVER.md; the last pre-execution checkpoint remains Task
11 at 1,570.

- [ ] **Step 7: Commit and push**

```bash
git add -A
git diff --cached --name-only
git diff --cached
node scripts/check-ontology-terms.mjs
git commit -m "feat(seed): migrate-only default and generalised demo dataset"
git push
```
