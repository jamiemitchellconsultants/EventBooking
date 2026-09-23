using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Audit;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Audit;
using EventBooking.Infrastructure.Persistence;
using EventBooking.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace EventBooking.SeedData.Tests;

/// <summary>
/// Verifies seed-created events carry the same negotiation audit history as
/// handler-created events.
/// </summary>
[Collection("seed-anchor")]
public sealed class SeedEventAuditTests : IAsyncLifetime
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
        services.AddSingleton<EventBooking.Domain.Time.IEventWindowZones>(
            new EventBooking.Infrastructure.Time.NodaTimeEventWindowZones());
        services.AddEventBookingApplication(
            new AttendeePortalOptions(
                "http://localhost:5002", "recruitment@example.com"));
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
    /// Verifies every agreed seed event has its proposal, acceptance, and confirmation
    /// audit entries with the negotiating managers as actors.
    /// </summary>
    [Fact]
    public async Task AgreedEvents_HaveNegotiationAuditHistory()
    {
        using var scope = _services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DemoSeeder>();
        var database = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();

        await seeder.RunAsync(CancellationToken.None);

        var managers = DemoSeedSpec.ManagerForType().Values
            .Select(id => id.ToString()).ToHashSet();
        var agreed = DemoSeedSpec.AgreedEvents();
        Assert.Equal(3, agreed.Count);

        foreach (var spec in agreed)
        {
            var seeded = await database.Events.AsNoTracking()
                .SingleAsync(e => e.Window.Date == spec.Date
                    && e.Window.StartTime == spec.StartTime);
            var confirmed = await database.AuditLogs.AsNoTracking()
                .SingleAsync(a => a.EntityType == AuditEntityTypes.Event
                    && a.EntityId == seeded.Id
                    && a.Action == AuditAction.EventConfirmed);
            Assert.Equal(ActorType.Staff, confirmed.ActorType);
            Assert.NotNull(confirmed.ActorId);
            Assert.Contains(confirmed.ActorId, managers);
            Assert.Equal(seeded.Window.ToString(), confirmed.Details);

            var proposalEntries = await database.AuditLogs.AsNoTracking()
                .Where(a => a.EntityType == AuditEntityTypes.EventProposal
                    && a.EntityId == seeded.ProposalId)
                .ToListAsync();
            var created = Assert.Single(proposalEntries,
                a => a.Action == AuditAction.ProposalCreated);
            Assert.Equal(ActorType.Staff, created.ActorType);
            Assert.NotNull(created.ActorId);
            Assert.Contains(created.ActorId, managers);
            Assert.Equal(seeded.Window.ToString(), created.Details);

            var acceptances = proposalEntries
                .Where(a => a.Action == AuditAction.AcceptanceRecorded)
                .ToList();
            Assert.Equal(3, acceptances.Count);
            Assert.All(acceptances, a => Assert.NotNull(a.ActorId));
            Assert.Equal(managers, acceptances.Select(a => a.ActorId!).ToHashSet());
            Assert.All(acceptances, a => Assert.Equal(ActorType.Staff, a.ActorType));
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
