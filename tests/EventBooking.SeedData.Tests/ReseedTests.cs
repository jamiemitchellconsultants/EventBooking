using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Events;
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
        // Five negotiation scenarios plus one accepted proposal for each of nine events.
        Assert.Equal(14, await database.EventProposals.CountAsync());
        var events = await database.Events.AsNoTracking().ToListAsync();
        var proposals = await database.EventProposals.AsNoTracking().ToDictionaryAsync(p => p.Id);
        Assert.Equal(events.Count, events.Select(e => e.ProposalId).Distinct().Count());
        Assert.All(events, e => Assert.Equal(EventProposalStatus.Confirmed, proposals[e.ProposalId].Status));
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
