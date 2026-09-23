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
                "http://localhost:5002", "recruitment@example.com"));
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
