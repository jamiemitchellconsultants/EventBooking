# 00b — Vocabulary edits 106 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — tests/EventBooking.SeedData.Tests/DemoSeedSpecTests.cs — 1/1

<!-- vocabulary-file: {"id":364,"oldPath":"tests/EventBooking.SeedData.Tests/DemoSeedSpecTests.cs","newPath":"tests/EventBooking.SeedData.Tests/DemoSeedSpecTests.cs","beforeSha":"06e73effe0d8008bbe9be5e89d13aa13f72d8fab8c4b277f1ac1902348bfcdf4","afterSha":"9f174fbcea1dd8f1480abebcdfd03bbdb8ea51369300843518635ac6bb952391","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Access;
using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

/// <summary>
/// Verifies the shape and relationships of the embedded demo dataset.
/// </summary>
public sealed class DemoSeedSpecTests
{
    /// <summary>
    /// Verifies that the demo dataset provides three distinct agreed slot windows with capacity
    /// for all three appointment types.
    /// </summary>
    [Fact]
    public void AgreedSlots_AreThreeWindowsWithEightTwelveSix()
    {
        var slots = DemoSeedSpec.AgreedSlots();

        Assert.Equal(3, slots.Count);
        Assert.Equal(3, slots.Select(s => (s.Date, s.StartTime)).Distinct().Count());
        foreach (var slot in slots)
        {
            Assert.Equal(8, slot.DatHeadcount);
            Assert.Equal(12, slot.MedHeadcount);
            Assert.Equal(6, slot.UniHeadcount);
        }
    }

    /// <summary>
    /// Verifies that every open slot proposal is after the dataset anchor and initially records
    /// headcount for exactly one appointment type.
    /// </summary>
    [Fact]
    public void OpenProposals_AreFiveWindowsWithOneHeadcountEach()
    {
        var anchor = DemoSeedSpec.AnchorDate();

        var proposals = DemoSeedSpec.OpenProposals();

        Assert.Equal(5, proposals.Count);
        Assert.Equal(5, proposals.Select(p => (p.Date, p.StartTime)).Distinct().Count());
        foreach (var proposal in proposals)
        {
            Assert.True(proposal.Date > anchor);
            var filled = new[] { proposal.DatHeadcount, proposal.MedHeadcount, proposal.UniHeadcount }
                .Count(h => h is not null);
            Assert.Equal(1, filled);
        }
    }

    /// <summary>
    /// Verifies that seeded staff assignments match the users and role scopes in the Keycloak
    /// demo realm.
    /// </summary>
    [Fact]
    public void Staff_MatchesTheKeycloakRealmUsers()
    {
        var staff = DemoSeedSpec.Staff();

        Assert.Equal(6, staff.Count);
        Assert.Single(staff, s => s.Roles.SequenceEqual([Role.Admin]));
        Assert.Equal(3, staff.Count(s => s.Roles.Contains(Role.Manager)));
        Assert.Single(staff, s => s.Roles.SequenceEqual([Role.Coordinator]));
        Assert.Single(staff, s => s.Roles.SequenceEqual([Role.AppointmentStaff]));
        Assert.All(
            staff.Where(s => s.Roles.Contains(Role.Manager)),
            s => Assert.NotNull(s.AppointmentTypeId));
        Assert.Equal(
            staff.Single(s => s.Roles.SequenceEqual([Role.Coordinator])).UserId,
            DemoSeedSpec.CoordinatorUserId());
        Assert.Equal(6, staff.Select(s => s.StaffId).Distinct().Count());
        Assert.All(staff, s => Assert.True(StaffId.TryParse(s.StaffId.Value, out _)));
    }

    /// <summary>
    /// Verifies the deterministic candidate journey mix across all five employee groups.
    /// </summary>
    [Fact]
    public void Candidates_CoverEveryGroupAndJourney()
    {
        var candidates = DemoSeedSpec.Candidates();

        Assert.Equal(100, candidates.Count);
        Assert.Equal(100, candidates.Select(c => c.Email).Distinct().Count());
        Assert.All(candidates, c => Assert.False(string.IsNullOrWhiteSpace(c.Name)));
        Assert.All(
            candidates,
            c => Assert.Contains(
                c.EmployeeGroupCode,
                new[]
                {
                    "CABIN_CREW", "PILOTS", "GROUND_OPERATIONS_AGENT", "ENGINEERING",
                    "GROUND_TRANSPORT_SERVICES",
                }));
        Assert.Equal(20, candidates.Count(c => c.Journey == DemoCandidateJourney.Unbooked));
        Assert.Equal(30, candidates.Count(c => c.Journey == DemoCandidateJourney.Ready));
        Assert.Equal(25, candidates.Count(c => c.Journey == DemoCandidateJourney.Outstanding));
        Assert.Equal(15, candidates.Count(c => c.Journey == DemoCandidateJourney.NoShow));
        Assert.Equal(10, candidates.Count(c => c.Journey == DemoCandidateJourney.RecoveryCompleted));
    }
}
`````

## after — tests/EventBooking.SeedData.Tests/DemoSeedSpecTests.cs — 1/1

<!-- vocabulary-file: {"id":364,"oldPath":"tests/EventBooking.SeedData.Tests/DemoSeedSpecTests.cs","newPath":"tests/EventBooking.SeedData.Tests/DemoSeedSpecTests.cs","beforeSha":"06e73effe0d8008bbe9be5e89d13aa13f72d8fab8c4b277f1ac1902348bfcdf4","afterSha":"9f174fbcea1dd8f1480abebcdfd03bbdb8ea51369300843518635ac6bb952391","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Access;
using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

/// <summary>
/// Verifies the shape and relationships of the embedded demo dataset.
/// </summary>
public sealed class DemoSeedSpecTests
{
    /// <summary>
    /// Verifies that the demo dataset provides three distinct agreed event windows with capacity
    /// for all three appointment types.
    /// </summary>
    [Fact]
    public void AgreedEvents_AreThreeWindowsWithEightTwelveSix()
    {
        var events = DemoSeedSpec.AgreedEvents();

        Assert.Equal(3, events.Count);
        Assert.Equal(3, events.Select(s => (s.Date, s.StartTime)).Distinct().Count());
        foreach (var eventItem in events)
        {
            Assert.Equal(8, eventItem.DatHeadcount);
            Assert.Equal(12, eventItem.MedHeadcount);
            Assert.Equal(6, eventItem.UniHeadcount);
        }
    }

    /// <summary>
    /// Verifies that every open event proposal is after the dataset anchor and initially records
    /// headcount for exactly one appointment type.
    /// </summary>
    [Fact]
    public void OpenProposals_AreFiveWindowsWithOneHeadcountEach()
    {
        var anchor = DemoSeedSpec.AnchorDate();

        var proposals = DemoSeedSpec.OpenProposals();

        Assert.Equal(5, proposals.Count);
        Assert.Equal(5, proposals.Select(p => (p.Date, p.StartTime)).Distinct().Count());
        foreach (var proposal in proposals)
        {
            Assert.True(proposal.Date > anchor);
            var filled = new[] { proposal.DatHeadcount, proposal.MedHeadcount, proposal.UniHeadcount }
                .Count(h => h is not null);
            Assert.Equal(1, filled);
        }
    }

    /// <summary>
    /// Verifies that seeded staff assignments match the users and role scopes in the Keycloak
    /// demo realm.
    /// </summary>
    [Fact]
    public void Staff_MatchesTheKeycloakRealmUsers()
    {
        var staff = DemoSeedSpec.Staff();

        Assert.Equal(6, staff.Count);
        Assert.Single(staff, s => s.Roles.SequenceEqual([Role.Admin]));
        Assert.Equal(3, staff.Count(s => s.Roles.Contains(Role.Manager)));
        Assert.Single(staff, s => s.Roles.SequenceEqual([Role.Coordinator]));
        Assert.Single(staff, s => s.Roles.SequenceEqual([Role.AppointmentStaff]));
        Assert.All(
            staff.Where(s => s.Roles.Contains(Role.Manager)),
            s => Assert.NotNull(s.AppointmentTypeId));
        Assert.Equal(
            staff.Single(s => s.Roles.SequenceEqual([Role.Coordinator])).UserId,
            DemoSeedSpec.CoordinatorUserId());
        Assert.Equal(6, staff.Select(s => s.StaffId).Distinct().Count());
        Assert.All(staff, s => Assert.True(StaffId.TryParse(s.StaffId.Value, out _)));
    }

    /// <summary>
    /// Verifies the deterministic attendee journey mix across all five attendee groups.
    /// </summary>
    [Fact]
    public void Attendees_CoverEveryGroupAndJourney()
    {
        var attendees = DemoSeedSpec.Attendees();

        Assert.Equal(100, attendees.Count);
        Assert.Equal(100, attendees.Select(c => c.Email).Distinct().Count());
        Assert.All(attendees, c => Assert.False(string.IsNullOrWhiteSpace(c.Name)));
        Assert.All(
            attendees,
            c => Assert.Contains(
                c.AttendeeGroupCode,
                new[]
                {
                    "CABIN_CREW", "PILOTS", "GROUND_OPERATIONS_AGENT", "ENGINEERING",
                    "GROUND_TRANSPORT_SERVICES",
                }));
        Assert.Equal(20, attendees.Count(c => c.Journey == DemoAttendeeJourney.Unbooked));
        Assert.Equal(30, attendees.Count(c => c.Journey == DemoAttendeeJourney.Ready));
        Assert.Equal(25, attendees.Count(c => c.Journey == DemoAttendeeJourney.Outstanding));
        Assert.Equal(15, attendees.Count(c => c.Journey == DemoAttendeeJourney.NoShow));
        Assert.Equal(10, attendees.Count(c => c.Journey == DemoAttendeeJourney.RecoveryCompleted));
    }
}
`````

## before — tests/EventBooking.SeedData.Tests/EmployeeGroupJourneySeedTests.cs — 1/1

<!-- vocabulary-file: {"id":365,"oldPath":"tests/EventBooking.SeedData.Tests/EmployeeGroupJourneySeedTests.cs","newPath":"tests/EventBooking.SeedData.Tests/AttendeeGroupJourneySeedTests.cs","beforeSha":"ab98023c8332545e48d0d242e40e735e08387d25eac5687aa551186ed27d50a4","afterSha":"0f6b7e88a6cf717f9776abaea04d20110ad65dbdafd4488bcb5e98cabe17b47c","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

/// <summary>Verifies demo data explicitly assigns groups and covers readiness/recovery journeys.</summary>
public sealed class EmployeeGroupJourneySeedTests
{
    /// <summary>Every group and every approved demo journey appears without requirement input.</summary>
    [Fact]
    public void CandidateSpecsUseExplicitGroupsAndCoverJourneys()
    {
        var candidates = DemoSeedSpec.Candidates();

        Assert.Superset(
            new HashSet<string>
            {
                "CABIN_CREW",
                "PILOTS",
                "GROUND_OPERATIONS_AGENT",
                "ENGINEERING",
                "GROUND_TRANSPORT_SERVICES",
            },
            candidates.Select(candidate => candidate.EmployeeGroupCode).ToHashSet());
        Assert.Contains(candidates, candidate => candidate.Journey == DemoCandidateJourney.Ready);
        Assert.Contains(candidates, candidate => candidate.Journey == DemoCandidateJourney.Outstanding);
        Assert.Contains(candidates, candidate => candidate.Journey == DemoCandidateJourney.NoShow);
        Assert.Contains(candidates, candidate => candidate.Journey == DemoCandidateJourney.RecoveryCompleted);
        Assert.DoesNotContain(
            typeof(CandidateSpec).GetProperties(),
            property => property.Name.Contains("Requirement", StringComparison.Ordinal));
    }
}
`````

## after — tests/EventBooking.SeedData.Tests/AttendeeGroupJourneySeedTests.cs — 1/1

<!-- vocabulary-file: {"id":365,"oldPath":"tests/EventBooking.SeedData.Tests/EmployeeGroupJourneySeedTests.cs","newPath":"tests/EventBooking.SeedData.Tests/AttendeeGroupJourneySeedTests.cs","beforeSha":"ab98023c8332545e48d0d242e40e735e08387d25eac5687aa551186ed27d50a4","afterSha":"0f6b7e88a6cf717f9776abaea04d20110ad65dbdafd4488bcb5e98cabe17b47c","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

/// <summary>Verifies demo data explicitly assigns groups and covers readiness/recovery journeys.</summary>
public sealed class AttendeeGroupJourneySeedTests
{
    /// <summary>Every group and every approved demo journey appears without requirement input.</summary>
    [Fact]
    public void AttendeeSpecsUseExplicitGroupsAndCoverJourneys()
    {
        var attendees = DemoSeedSpec.Attendees();

        Assert.Superset(
            new HashSet<string>
            {
                "CABIN_CREW",
                "PILOTS",
                "GROUND_OPERATIONS_AGENT",
                "ENGINEERING",
                "GROUND_TRANSPORT_SERVICES",
            },
            attendees.Select(attendee => attendee.AttendeeGroupCode).ToHashSet());
        Assert.Contains(attendees, attendee => attendee.Journey == DemoAttendeeJourney.Ready);
        Assert.Contains(attendees, attendee => attendee.Journey == DemoAttendeeJourney.Outstanding);
        Assert.Contains(attendees, attendee => attendee.Journey == DemoAttendeeJourney.NoShow);
        Assert.Contains(attendees, attendee => attendee.Journey == DemoAttendeeJourney.RecoveryCompleted);
        Assert.DoesNotContain(
            typeof(AttendeeSpec).GetProperties(),
            property => property.Name.Contains("Requirement", StringComparison.Ordinal));
    }
}
`````

## before — tests/EventBooking.SeedData.Tests/ReanchorTests.cs — 1/1

<!-- vocabulary-file: {"id":366,"oldPath":"tests/EventBooking.SeedData.Tests/ReanchorTests.cs","newPath":"tests/EventBooking.SeedData.Tests/ReanchorTests.cs","beforeSha":"3daa0071379ae76b370023d4dd69dba45a2187d082313332f051234e00c5ec50","afterSha":"0c5acdc75e4058315aca9e44baf3e91732b6327a542f73b9a5099b0b9489c305","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Audit;
using EventBooking.Infrastructure.Persistence;
using EventBooking.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace EventBooking.SeedData.Tests;

/// <summary>
/// Verifies that a stale file anchor fails fast and that --reanchor (the run override)
/// restores a green seed without editing demo-seed.json.
/// </summary>
[Collection("seed-anchor")]
public sealed class ReanchorTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database =
        new PostgreSqlBuilder("postgres:16-alpine").Build();

    private ServiceProvider _services = null!;

    private DateOnly _today;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        // Simulate "today" moving five days past the file anchor: the +2-day proposal
        // then lands in the past, which the domain rejects.
        _today = DemoSeedSpec.AnchorDate().AddDays(5);

        var services = new ServiceCollection();
        services.AddEventBookingPersistence(_database.GetConnectionString());
        services.AddEventBookingApplication(
            new CandidatePortalOptions(
                "http://localhost:5002", "1 Example Street, London", "recruitment@example.com"));
        services.AddSingleton<IClock>(new FixedClock(_today));
        services.AddScoped<IAuditLogger, EfAuditLogger>();
        services.AddScoped<DemoSeeder>();
        _services = services.BuildServiceProvider();

        using var scope = _services.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<EventBookingDbContext>()
            .Database.MigrateAsync();
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        await _database.DisposeAsync();
    }

    /// <summary>Documents the stale-anchor failure the override exists to fix.</summary>
    [Fact]
    public async Task StaleAnchorFailsWithoutReanchor()
    {
        using var scope = _services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DemoSeeder>();

        var error = await Assert.ThrowsAsync<SeedException>(
            () => seeder.RunAsync(CancellationToken.None));

        Assert.Contains("must be proposed for a future date", error.Message);
    }

    /// <summary>Overriding the anchor to today restores the full deterministic seed.</summary>
    [Fact]
    public async Task ReanchorToTodayRestoresFullSeed()
    {
        DemoSeedSpec.OverrideAnchor(_today);
        try
        {
            using var scope = _services.CreateScope();
            var seeder = scope.ServiceProvider.GetRequiredService<DemoSeeder>();
            var database = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();

            var summary = await seeder.RunAsync(CancellationToken.None);

            Assert.Equal(6, summary.IdentitiesEnsured);
            Assert.Equal(6, summary.ProfilesEnsured);
            Assert.Equal(3, summary.AgreedSlotsImported);
            Assert.Equal(5, summary.ProposalsEnsured);
            Assert.Equal(100, summary.CandidatesCreated);
            Assert.Equal(100, await database.Candidates.CountAsync());
        }
        finally
        {
            DemoSeedSpec.OverrideAnchor(null);
        }
    }

    private sealed class FixedClock(DateOnly today) : IClock
    {
        private static readonly TimeZoneInfo HeadOfficeTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

        /// <inheritdoc/>
        public DateTimeOffset UtcNow { get; } =
            new(today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);

        /// <inheritdoc/>
        public DateTimeOffset NowAtHeadOffice => TimeZoneInfo.ConvertTime(UtcNow, HeadOfficeTimeZone);

        /// <inheritdoc/>
        public DateOnly TodayAtHeadOffice => DateAtHeadOffice(UtcNow);

        /// <inheritdoc/>
        public DateOnly DateAtHeadOffice(DateTimeOffset instant) =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, HeadOfficeTimeZone).DateTime);

        public DateTimeOffset InstantAtHeadOffice(DateTimeOffset instant) =>
            TimeZoneInfo.ConvertTime(instant, HeadOfficeTimeZone);
    }
}
`````

## after — tests/EventBooking.SeedData.Tests/ReanchorTests.cs — 1/1

<!-- vocabulary-file: {"id":366,"oldPath":"tests/EventBooking.SeedData.Tests/ReanchorTests.cs","newPath":"tests/EventBooking.SeedData.Tests/ReanchorTests.cs","beforeSha":"3daa0071379ae76b370023d4dd69dba45a2187d082313332f051234e00c5ec50","afterSha":"0c5acdc75e4058315aca9e44baf3e91732b6327a542f73b9a5099b0b9489c305","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Audit;
using EventBooking.Infrastructure.Persistence;
using EventBooking.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace EventBooking.SeedData.Tests;

/// <summary>
/// Verifies that a stale file anchor fails fast and that --reanchor (the run override)
/// restores a green seed without editing demo-seed.json.
/// </summary>
[Collection("seed-anchor")]
public sealed class ReanchorTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database =
        new PostgreSqlBuilder("postgres:16-alpine").Build();

    private ServiceProvider _services = null!;

    private DateOnly _today;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        // Simulate "today" moving five days past the file anchor: the +2-day proposal
        // then lands in the past, which the domain rejects.
        _today = DemoSeedSpec.AnchorDate().AddDays(5);

        var services = new ServiceCollection();
        services.AddEventBookingPersistence(_database.GetConnectionString());
        services.AddEventBookingApplication(
            new AttendeePortalOptions(
                "http://localhost:5002", "1 Example Street, London", "recruitment@example.com"));
        services.AddSingleton<IClock>(new FixedClock(_today));
        services.AddScoped<IAuditLogger, EfAuditLogger>();
        services.AddScoped<DemoSeeder>();
        _services = services.BuildServiceProvider();

        using var scope = _services.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<EventBookingDbContext>()
            .Database.MigrateAsync();
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        await _database.DisposeAsync();
    }

    /// <summary>Documents the stale-anchor failure the override exists to fix.</summary>
    [Fact]
    public async Task StaleAnchorFailsWithoutReanchor()
    {
        using var scope = _services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DemoSeeder>();

        var error = await Assert.ThrowsAsync<SeedException>(
            () => seeder.RunAsync(CancellationToken.None));

        Assert.Contains("must be proposed for a future date", error.Message);
    }

    /// <summary>Overriding the anchor to today restores the full deterministic seed.</summary>
    [Fact]
    public async Task ReanchorToTodayRestoresFullSeed()
    {
        DemoSeedSpec.OverrideAnchor(_today);
        try
        {
            using var scope = _services.CreateScope();
            var seeder = scope.ServiceProvider.GetRequiredService<DemoSeeder>();
            var database = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();

            var summary = await seeder.RunAsync(CancellationToken.None);

            Assert.Equal(6, summary.IdentitiesEnsured);
            Assert.Equal(6, summary.ProfilesEnsured);
            Assert.Equal(3, summary.AgreedEventsImported);
            Assert.Equal(5, summary.ProposalsEnsured);
            Assert.Equal(100, summary.AttendeesCreated);
            Assert.Equal(100, await database.Attendees.CountAsync());
        }
        finally
        {
            DemoSeedSpec.OverrideAnchor(null);
        }
    }

    private sealed class FixedClock(DateOnly today) : IClock
    {
        private static readonly TimeZoneInfo TransitionalLocationTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

        /// <inheritdoc/>
        public DateTimeOffset UtcNow { get; } =
            new(today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);

        /// <inheritdoc/>
        public DateTimeOffset NowAtTransitionalLocation => TimeZoneInfo.ConvertTime(UtcNow, TransitionalLocationTimeZone);

        /// <inheritdoc/>
        public DateOnly TodayAtTransitionalLocation => DateAtTransitionalLocation(UtcNow);

        /// <inheritdoc/>
        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, TransitionalLocationTimeZone).DateTime);

        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) =>
            TimeZoneInfo.ConvertTime(instant, TransitionalLocationTimeZone);
    }
}
`````

## before — tests/EventBooking.SeedData.Tests/ReseedTests.cs — 1/1

<!-- vocabulary-file: {"id":367,"oldPath":"tests/EventBooking.SeedData.Tests/ReseedTests.cs","newPath":"tests/EventBooking.SeedData.Tests/ReseedTests.cs","beforeSha":"414e6944b04a1455f633d08f98700e6fb0e28ca9285cba381b54fc6133f71382","afterSha":"66d9e848201d9d4f3cfa7d6ec9e2244f2ac9d6c74401208ab49938f286028dba","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Bookings;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Audit;
using EventBooking.Infrastructure.Persistence;
using EventBooking.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace EventBooking.SeedData.Tests;

/// <summary>
/// Verifies that reseeding restores the complete deterministic demo dataset after domain data has
/// been mutated.
/// </summary>
[Collection("seed-anchor")]
public sealed class ReseedTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database =
        new PostgreSqlBuilder("postgres:16-alpine").Build();

    private ServiceProvider _services = null!;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        var services = new ServiceCollection();
        services.AddEventBookingPersistence(_database.GetConnectionString());
        services.AddEventBookingApplication(
            new CandidatePortalOptions(
                "http://localhost:5002", "1 Example Street, London", "recruitment@example.com"));
        services.AddSingleton<IClock>(new FixedClock(DemoSeedSpec.AnchorDate()));
        services.AddScoped<IAuditLogger, EfAuditLogger>();
        services.AddScoped<DemoSeeder>();
        _services = services.BuildServiceProvider();

        using var scope = _services.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<EventBookingDbContext>()
            .Database.MigrateAsync();
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        await _database.DisposeAsync();
    }

    /// <summary>
    /// Verifies that reseeding removes mutations and restores every seeded aggregate and reference
    /// record exactly once.
    /// </summary>
    [Fact]
    public async Task Reseed_RestoresExactSeedStateAfterMutations()
    {
        using var scope = _services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DemoSeeder>();
        var database = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();

        await seeder.RunAsync(CancellationToken.None);

        database.Candidates.RemoveRange(database.Candidates);
        database.StaffAccessProfiles.Remove(await database.StaffAccessProfiles.FirstAsync());
        await database.SaveChangesAsync();

        var summary = await seeder.ReseedAsync(CancellationToken.None);

        Assert.Equal(6, summary.IdentitiesEnsured);
        Assert.Equal(6, summary.ProfilesEnsured);
        Assert.Equal(3, summary.AgreedSlotsImported);
        Assert.Equal(5, summary.ProposalsEnsured);
        Assert.Equal(100, summary.CandidatesCreated);
        Assert.Equal(6, await database.StaffAccessProfiles.CountAsync());
        var expectedProfiles = DemoSeedSpec.Staff().ToDictionary(value => value.UserId);
        var actualProfiles = await database.StaffAccessProfiles
            .AsNoTracking()
            .ToDictionaryAsync(value => value.StaffUserId);

        Assert.Equal(expectedProfiles.Keys.OrderBy(value => value),
            actualProfiles.Keys.OrderBy(value => value));
        Assert.All(expectedProfiles, pair =>
        {
            var actual = actualProfiles[pair.Key];
            Assert.Equal(
                pair.Value.Roles.OrderBy(value => value),
                actual.Roles.OrderBy(value => value));
            Assert.Equal(pair.Value.AppointmentTypeId, actual.AppointmentTypeId);
        });
        Assert.Equal(6, await database.StaffIdentities.CountAsync());
        Assert.Equal(9, await database.ConfirmedSlots.CountAsync());
        Assert.Equal(5, await database.SlotProposals.CountAsync());
        Assert.Equal(100, await database.Candidates.CountAsync());
        Assert.Equal(3, await database.AppointmentTypes.CountAsync());
        Assert.Equal(1, await database.SystemSettings.CountAsync());
        Assert.Equal(5, await database.EmployeeGroups.CountAsync());
        Assert.Equal(
            10,
            await database.EmployeeGroups.SelectMany(group => group.Requirements).CountAsync());
        Assert.Equal(
            200,
            await database.Candidates.SelectMany(candidate => candidate.Requirements).CountAsync());
        Assert.Equal(90, await database.Invites.CountAsync());
        Assert.Equal(90, await database.Bookings.CountAsync());
        Assert.Equal(170, await database.BookingAppointments.CountAsync());
        Assert.Equal(
            170,
            await database.Invites.SelectMany(invite => invite.Requirements).CountAsync());
        Assert.Equal(
            25,
            await database.BookingAppointments.CountAsync(
                appointment => appointment.Status == BookingAppointmentStatus.NoShow));
        Assert.Equal(
            70,
            await database.BookingAppointments.CountAsync(
                appointment => appointment.Status == BookingAppointmentStatus.Completed));
        Assert.True(
            await database.BookingAppointments.AnyAsync(
                appointment => appointment.Status == BookingAppointmentStatus.CheckedIn));
    }

    private sealed class FixedClock(DateOnly today) : IClock
    {
        private static readonly TimeZoneInfo HeadOfficeTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

        /// <inheritdoc/>
        public DateTimeOffset UtcNow { get; } =
            new(today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);

        /// <inheritdoc/>
        public DateTimeOffset NowAtHeadOffice => TimeZoneInfo.ConvertTime(UtcNow, HeadOfficeTimeZone);

        /// <inheritdoc/>
        public DateOnly TodayAtHeadOffice => DateAtHeadOffice(UtcNow);

        /// <inheritdoc/>
        public DateOnly DateAtHeadOffice(DateTimeOffset instant) =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, HeadOfficeTimeZone).DateTime);

        public DateTimeOffset InstantAtHeadOffice(DateTimeOffset instant) =>
            TimeZoneInfo.ConvertTime(instant, HeadOfficeTimeZone);
    }
}
`````

## after — tests/EventBooking.SeedData.Tests/ReseedTests.cs — 1/1

<!-- vocabulary-file: {"id":367,"oldPath":"tests/EventBooking.SeedData.Tests/ReseedTests.cs","newPath":"tests/EventBooking.SeedData.Tests/ReseedTests.cs","beforeSha":"414e6944b04a1455f633d08f98700e6fb0e28ca9285cba381b54fc6133f71382","afterSha":"66d9e848201d9d4f3cfa7d6ec9e2244f2ac9d6c74401208ab49938f286028dba","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Bookings;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Audit;
using EventBooking.Infrastructure.Persistence;
using EventBooking.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace EventBooking.SeedData.Tests;

/// <summary>
/// Verifies that reseeding restores the complete deterministic demo dataset after domain data has
/// been mutated.
/// </summary>
[Collection("seed-anchor")]
public sealed class ReseedTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database =
        new PostgreSqlBuilder("postgres:16-alpine").Build();

    private ServiceProvider _services = null!;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        var services = new ServiceCollection();
        services.AddEventBookingPersistence(_database.GetConnectionString());
        services.AddEventBookingApplication(
            new AttendeePortalOptions(
                "http://localhost:5002", "1 Example Street, London", "recruitment@example.com"));
        services.AddSingleton<IClock>(new FixedClock(DemoSeedSpec.AnchorDate()));
        services.AddScoped<IAuditLogger, EfAuditLogger>();
        services.AddScoped<DemoSeeder>();
        _services = services.BuildServiceProvider();

        using var scope = _services.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<EventBookingDbContext>()
            .Database.MigrateAsync();
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        await _database.DisposeAsync();
    }

    /// <summary>
    /// Verifies that reseeding removes mutations and restores every seeded aggregate and reference
    /// record exactly once.
    /// </summary>
    [Fact]
    public async Task Reseed_RestoresExactSeedStateAfterMutations()
    {
        using var scope = _services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DemoSeeder>();
        var database = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();

        await seeder.RunAsync(CancellationToken.None);

        database.Attendees.RemoveRange(database.Attendees);
        database.StaffAccessProfiles.Remove(await database.StaffAccessProfiles.FirstAsync());
        await database.SaveChangesAsync();

        var summary = await seeder.ReseedAsync(CancellationToken.None);

        Assert.Equal(6, summary.IdentitiesEnsured);
        Assert.Equal(6, summary.ProfilesEnsured);
        Assert.Equal(3, summary.AgreedEventsImported);
        Assert.Equal(5, summary.ProposalsEnsured);
        Assert.Equal(100, summary.AttendeesCreated);
        Assert.Equal(6, await database.StaffAccessProfiles.CountAsync());
        var expectedProfiles = DemoSeedSpec.Staff().ToDictionary(value => value.UserId);
        var actualProfiles = await database.StaffAccessProfiles
            .AsNoTracking()
            .ToDictionaryAsync(value => value.StaffUserId);

        Assert.Equal(expectedProfiles.Keys.OrderBy(value => value),
            actualProfiles.Keys.OrderBy(value => value));
        Assert.All(expectedProfiles, pair =>
        {
            var actual = actualProfiles[pair.Key];
            Assert.Equal(
                pair.Value.Roles.OrderBy(value => value),
                actual.Roles.OrderBy(value => value));
            Assert.Equal(pair.Value.AppointmentTypeId, actual.AppointmentTypeId);
        });
        Assert.Equal(6, await database.StaffIdentities.CountAsync());
        Assert.Equal(9, await database.Events.CountAsync());
        Assert.Equal(5, await database.EventProposals.CountAsync());
        Assert.Equal(100, await database.Attendees.CountAsync());
        Assert.Equal(3, await database.AppointmentTypes.CountAsync());
        Assert.Equal(1, await database.SystemSettings.CountAsync());
        Assert.Equal(5, await database.AttendeeGroups.CountAsync());
        Assert.Equal(
            10,
            await database.AttendeeGroups.SelectMany(group => group.Requirements).CountAsync());
        Assert.Equal(
            200,
            await database.Attendees.SelectMany(attendee => attendee.Requirements).CountAsync());
        Assert.Equal(90, await database.Invites.CountAsync());
        Assert.Equal(90, await database.Bookings.CountAsync());
        Assert.Equal(170, await database.BookingAppointments.CountAsync());
        Assert.Equal(
            170,
            await database.Invites.SelectMany(invite => invite.Requirements).CountAsync());
        Assert.Equal(
            25,
            await database.BookingAppointments.CountAsync(
                appointment => appointment.Status == BookingAppointmentStatus.NoShow));
        Assert.Equal(
            70,
            await database.BookingAppointments.CountAsync(
                appointment => appointment.Status == BookingAppointmentStatus.Completed));
        Assert.True(
            await database.BookingAppointments.AnyAsync(
                appointment => appointment.Status == BookingAppointmentStatus.CheckedIn));
    }

    private sealed class FixedClock(DateOnly today) : IClock
    {
        private static readonly TimeZoneInfo TransitionalLocationTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

        /// <inheritdoc/>
        public DateTimeOffset UtcNow { get; } =
            new(today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);

        /// <inheritdoc/>
        public DateTimeOffset NowAtTransitionalLocation => TimeZoneInfo.ConvertTime(UtcNow, TransitionalLocationTimeZone);

        /// <inheritdoc/>
        public DateOnly TodayAtTransitionalLocation => DateAtTransitionalLocation(UtcNow);

        /// <inheritdoc/>
        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, TransitionalLocationTimeZone).DateTime);

        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) =>
            TimeZoneInfo.ConvertTime(instant, TransitionalLocationTimeZone);
    }
}
`````

## before — tests/EventBooking.Web.Tests/AppointmentsClientTests.cs — 1/1

<!-- vocabulary-file: {"id":368,"oldPath":"tests/EventBooking.Web.Tests/AppointmentsClientTests.cs","newPath":"tests/EventBooking.Web.Tests/AppointmentsClientTests.cs","beforeSha":"a4a2bcafa9afb2341b47e1a9a4a7dd758b84ee074168d703679d91028cca2cd6","afterSha":"2a774e2b199e37a2dd17c5222e56687eb908fb342529571bc3835361265b365b","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the browser client uses only the three minimum-data workspace routes.</summary>
public sealed class AppointmentsClientTests
{
    /// <summary>Verifies slot-list retrieval and deserialization.</summary>
    [Fact]
    public async Task ListSlotsGetsTheWorkspaceCollection()
    {
        var (client, handler) = Given(JsonContent.Create(new AppointmentWorkspaceSlotListDto
        {
            AppointmentTypeName = "Uniform Fitting",
            Slots = [],
        }));

        var result = await client.ListSlotsAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Uniform Fitting", result.Value!.AppointmentTypeName);
        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Equal("/api/appointment-workspace/slots", handler.Request.RequestUri!.AbsolutePath);
    }

    /// <summary>Verifies selected-slot retrieval targets only its route identifier.</summary>
    [Fact]
    public async Task GetSlotUsesTheConfirmedSlotRoute()
    {
        var slotId = Guid.NewGuid();
        var (client, handler) = Given(JsonContent.Create(new AppointmentSlotDetailDto
        {
            AppointmentTypeName = "Uniform Fitting",
            ConfirmedSlotId = slotId,
            Date = new DateOnly(2026, 9, 7),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(13, 0),
            Appointments = [],
        }));

        Assert.True((await client.GetSlotAsync(slotId, CancellationToken.None)).IsSuccess);
        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Equal(
            $"/api/appointment-workspace/slots/{slotId}",
            handler.Request.RequestUri!.AbsolutePath);
    }

    /// <summary>Verifies updates send status and version without scope or candidate identifiers.</summary>
    [Fact]
    public async Task UpdateSendsOnlyStatusAndExpectedVersion()
    {
        var appointmentId = Guid.NewGuid();
        var (client, handler) = Given(JsonContent.Create(new BookingAppointmentUpdateDto
        {
            BookingAppointmentId = appointmentId,
            Status = "CheckedIn",
            CheckedInAt = DateTimeOffset.Parse("2026-09-07T08:05:00Z"),
            OutcomeAt = null,
            Version = 2,
        }));

        var result = await client.UpdateStatusAsync(
            appointmentId, "CheckedIn", 1, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpMethod.Put, handler.Request!.Method);
        Assert.Equal(
            $"/api/appointment-workspace/appointments/{appointmentId}/status",
            handler.Request.RequestUri!.AbsolutePath);
        var body = await handler.Request.Content!.ReadAsStringAsync();
        Assert.Contains("\"status\":\"CheckedIn\"", body);
        Assert.Contains("\"expectedVersion\":1", body);
        Assert.DoesNotContain("appointmentType", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("candidate", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bookingId", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies problem details preserve the appointment concurrency discriminator.</summary>
    [Fact]
    public async Task ConflictReturnsItsSafeDetail()
    {
        var (client, handler) = Given(new StringContent(
            """{"title":"appointment_version_conflict","detail":"This appointment changed. Refresh and try again.","status":409}""",
            System.Text.Encoding.UTF8, "application/problem+json"));
        handler.StatusCode = HttpStatusCode.Conflict;

        var result = await client.UpdateStatusAsync(
            Guid.NewGuid(), "Completed", 2, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("appointment_version_conflict", result.ErrorCode);
        Assert.Equal("This appointment changed. Refresh and try again.", result.ErrorMessage);
    }


    /// <summary>Verifies the roster download returns raw CSV plus the server-suggested filename.</summary>
    [Fact]
    public async Task GetRosterReturnsCsvContentAndFilename()
    {
        var slotId = Guid.NewGuid();
        var csv = "Candidate Name,Candidate Email,Appointment Type,Status,Checked In At,Outcome At\n";
        var content = new StringContent(csv, Encoding.UTF8, "text/csv");
        content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileName = "roster-medical-check-up-2026-09-15-0930.csv",
        };
        var (client, handler) = Given(content);

        var result = await client.GetRosterAsync(slotId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(csv, result.Value!.Content);
        Assert.Equal("roster-medical-check-up-2026-09-15-0930.csv", result.Value.FileName);
        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Equal(
            $"/api/appointment-workspace/slots/{slotId}/roster",
            handler.Request.RequestUri!.AbsolutePath);
    }

    /// <summary>Verifies a quoted filename is unwrapped rather than passed through with quotes.</summary>
    [Fact]
    public async Task GetRosterUnwrapsAQuotedFilename()
    {
        var content = new StringContent("Candidate Name\n", Encoding.UTF8, "text/csv");
        content.Headers.TryAddWithoutValidation(
            "Content-Disposition", "attachment; filename=\"roster-uniform-fitting-2026-09-15-0930.csv\"");
        var (client, _) = Given(content);

        var result = await client.GetRosterAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("roster-uniform-fitting-2026-09-15-0930.csv", result.Value!.FileName);
    }

    /// <summary>Verifies the starred filename is preferred when the header carries both.</summary>
    [Fact]
    public async Task GetRosterPrefersTheStarredFilename()
    {
        var content = new StringContent("Candidate Name\n", Encoding.UTF8, "text/csv");
        content.Headers.TryAddWithoutValidation(
            "Content-Disposition",
            "attachment; filename=fallback.csv; filename*=UTF-8''roster-drug-%26-alcohol-testing-2026-09-15-0930.csv");
        var (client, _) = Given(content);

        var result = await client.GetRosterAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("roster-drug-&-alcohol-testing-2026-09-15-0930.csv", result.Value!.FileName);
    }

    /// <summary>Verifies a response with no disposition header still yields a usable filename.</summary>
    [Fact]
    public async Task GetRosterFallsBackToADefaultFilename()
    {
        var (client, _) = Given(new StringContent("Candidate Name\n", Encoding.UTF8, "text/csv"));

        var result = await client.GetRosterAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("roster.csv", result.Value!.FileName);
    }

    /// <summary>Verifies a refused download maps through the shared problem-details failure shape.</summary>
    [Fact]
    public async Task GetRosterMapsFailureThroughProblemDetails()
    {
        var handler = new StubHandler
        {
            Content = JsonContent.Create(new { title = "forbidden", status = 403 }),
            StatusCode = HttpStatusCode.Forbidden,
        };
        var client = new AppointmentsClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") });

        var result = await client.GetRosterAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
        Assert.Equal("You do not have permission to do that.", result.ErrorMessage);
    }

    private static (AppointmentsClient Client, StubHandler Handler) Given(HttpContent content)
    {
        var handler = new StubHandler { Content = content };
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        return (new AppointmentsClient(http), handler);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        /// <summary>Gets the last captured request.</summary>
        public HttpRequestMessage? Request { get; private set; }
        /// <summary>Gets the response content returned by the stub.</summary>
        public HttpContent Content { get; init; } = JsonContent.Create(new { });
        /// <summary>Gets or sets the response status returned by the stub.</summary>
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(StatusCode) { Content = Content });
        }
    }
}
`````

## after — tests/EventBooking.Web.Tests/AppointmentsClientTests.cs — 1/1

<!-- vocabulary-file: {"id":368,"oldPath":"tests/EventBooking.Web.Tests/AppointmentsClientTests.cs","newPath":"tests/EventBooking.Web.Tests/AppointmentsClientTests.cs","beforeSha":"a4a2bcafa9afb2341b47e1a9a4a7dd758b84ee074168d703679d91028cca2cd6","afterSha":"2a774e2b199e37a2dd17c5222e56687eb908fb342529571bc3835361265b365b","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the browser client uses only the three minimum-data workspace routes.</summary>
public sealed class AppointmentsClientTests
{
    /// <summary>Verifies event-list retrieval and deserialization.</summary>
    [Fact]
    public async Task ListEventsGetsTheWorkspaceCollection()
    {
        var (client, handler) = Given(JsonContent.Create(new AppointmentWorkspaceEventListDto
        {
            AppointmentTypeName = "Uniform Fitting",
            Events = [],
        }));

        var result = await client.ListEventsAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Uniform Fitting", result.Value!.AppointmentTypeName);
        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Equal("/api/appointment-workspace/events", handler.Request.RequestUri!.AbsolutePath);
    }

    /// <summary>Verifies selected-event retrieval targets only its route identifier.</summary>
    [Fact]
    public async Task GetEventUsesTheEventRoute()
    {
        var eventId = Guid.NewGuid();
        var (client, handler) = Given(JsonContent.Create(new AppointmentEventDetailDto
        {
            AppointmentTypeName = "Uniform Fitting",
            EventId = eventId,
            Date = new DateOnly(2026, 9, 7),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(13, 0),
            Appointments = [],
        }));

        Assert.True((await client.GetEventAsync(eventId, CancellationToken.None)).IsSuccess);
        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Equal(
            $"/api/appointment-workspace/events/{eventId}",
            handler.Request.RequestUri!.AbsolutePath);
    }

    /// <summary>Verifies updates send status and version without scope or attendee identifiers.</summary>
    [Fact]
    public async Task UpdateSendsOnlyStatusAndExpectedVersion()
    {
        var appointmentId = Guid.NewGuid();
        var (client, handler) = Given(JsonContent.Create(new BookingAppointmentUpdateDto
        {
            BookingAppointmentId = appointmentId,
            Status = "CheckedIn",
            CheckedInAt = DateTimeOffset.Parse("2026-09-07T08:05:00Z"),
            OutcomeAt = null,
            Version = 2,
        }));

        var result = await client.UpdateStatusAsync(
            appointmentId, "CheckedIn", 1, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpMethod.Put, handler.Request!.Method);
        Assert.Equal(
            $"/api/appointment-workspace/appointments/{appointmentId}/status",
            handler.Request.RequestUri!.AbsolutePath);
        var body = await handler.Request.Content!.ReadAsStringAsync();
        Assert.Contains("\"status\":\"CheckedIn\"", body);
        Assert.Contains("\"expectedVersion\":1", body);
        Assert.DoesNotContain("appointmentType", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("attendee", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bookingId", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies problem details preserve the appointment concurrency discriminator.</summary>
    [Fact]
    public async Task ConflictReturnsItsSafeDetail()
    {
        var (client, handler) = Given(new StringContent(
            """{"title":"appointment_version_conflict","detail":"This appointment changed. Refresh and try again.","status":409}""",
            System.Text.Encoding.UTF8, "application/problem+json"));
        handler.StatusCode = HttpStatusCode.Conflict;

        var result = await client.UpdateStatusAsync(
            Guid.NewGuid(), "Completed", 2, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("appointment_version_conflict", result.ErrorCode);
        Assert.Equal("This appointment changed. Refresh and try again.", result.ErrorMessage);
    }


    /// <summary>Verifies the roster download returns raw CSV plus the server-suggested filename.</summary>
    [Fact]
    public async Task GetRosterReturnsCsvContentAndFilename()
    {
        var eventId = Guid.NewGuid();
        var csv = "Attendee Name,Attendee Email,Appointment Type,Status,Checked In At,Outcome At\n";
        var content = new StringContent(csv, Encoding.UTF8, "text/csv");
        content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileName = "roster-medical-check-up-2026-09-15-0930.csv",
        };
        var (client, handler) = Given(content);

        var result = await client.GetRosterAsync(eventId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(csv, result.Value!.Content);
        Assert.Equal("roster-medical-check-up-2026-09-15-0930.csv", result.Value.FileName);
        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Equal(
            $"/api/appointment-workspace/events/{eventId}/roster",
            handler.Request.RequestUri!.AbsolutePath);
    }

    /// <summary>Verifies a quoted filename is unwrapped rather than passed through with quotes.</summary>
    [Fact]
    public async Task GetRosterUnwrapsAQuotedFilename()
    {
        var content = new StringContent("Attendee Name\n", Encoding.UTF8, "text/csv");
        content.Headers.TryAddWithoutValidation(
            "Content-Disposition", "attachment; filename=\"roster-uniform-fitting-2026-09-15-0930.csv\"");
        var (client, _) = Given(content);

        var result = await client.GetRosterAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("roster-uniform-fitting-2026-09-15-0930.csv", result.Value!.FileName);
    }

    /// <summary>Verifies the starred filename is preferred when the header carries both.</summary>
    [Fact]
    public async Task GetRosterPrefersTheStarredFilename()
    {
        var content = new StringContent("Attendee Name\n", Encoding.UTF8, "text/csv");
        content.Headers.TryAddWithoutValidation(
            "Content-Disposition",
            "attachment; filename=fallback.csv; filename*=UTF-8''roster-drug-%26-alcohol-testing-2026-09-15-0930.csv");
        var (client, _) = Given(content);

        var result = await client.GetRosterAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("roster-drug-&-alcohol-testing-2026-09-15-0930.csv", result.Value!.FileName);
    }

    /// <summary>Verifies a response with no disposition header still yields a usable filename.</summary>
    [Fact]
    public async Task GetRosterFallsBackToADefaultFilename()
    {
        var (client, _) = Given(new StringContent("Attendee Name\n", Encoding.UTF8, "text/csv"));

        var result = await client.GetRosterAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("roster.csv", result.Value!.FileName);
    }

    /// <summary>Verifies a refused download maps through the shared problem-details failure shape.</summary>
    [Fact]
    public async Task GetRosterMapsFailureThroughProblemDetails()
    {
        var handler = new StubHandler
        {
            Content = JsonContent.Create(new { title = "forbidden", status = 403 }),
            StatusCode = HttpStatusCode.Forbidden,
        };
        var client = new AppointmentsClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") });

        var result = await client.GetRosterAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
        Assert.Equal("You do not have permission to do that.", result.ErrorMessage);
    }

    private static (AppointmentsClient Client, StubHandler Handler) Given(HttpContent content)
    {
        var handler = new StubHandler { Content = content };
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        return (new AppointmentsClient(http), handler);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        /// <summary>Gets the last captured request.</summary>
        public HttpRequestMessage? Request { get; private set; }
        /// <summary>Gets the response content returned by the stub.</summary>
        public HttpContent Content { get; init; } = JsonContent.Create(new { });
        /// <summary>Gets or sets the response status returned by the stub.</summary>
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(StatusCode) { Content = Content });
        }
    }
}
`````
