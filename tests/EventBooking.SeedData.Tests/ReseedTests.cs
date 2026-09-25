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
/// Verifies that reseeding restores the complete generalised demo dataset after domain data has
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
        DemoSeedSpec.OverrideAnchor(new DateOnly(2026, 9, 22));
        await _database.StartAsync();

        var services = new ServiceCollection();
        services.AddEventBookingPersistence(_database.GetConnectionString());
        services.AddSingleton<EventBooking.Domain.Time.IEventWindowZones>(
            new EventBooking.Infrastructure.Time.NodaTimeEventWindowZones());
        services.AddEventBookingApplication(
            new AttendeePortalOptions(
                "http://localhost:5002", "recruitment@example.com"));
        services.AddSingleton<IClock, EventBooking.Infrastructure.Time.SystemClock>();
        services.AddScoped<IAuditLogger, EfAuditLogger>();
        services.AddScoped<DemoSeeder>();
        _services = services.BuildServiceProvider();

        using var scope = _services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        await database.Database.ExecuteSqlRawAsync(DatabaseRoles.Script);
        await database.Database.MigrateAsync();
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        DemoSeedSpec.OverrideAnchor(null);
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

        Assert.Equal(3, summary.LocationsEnsured);
        Assert.Equal(6, summary.AppointmentTypesEnsured);
        Assert.Equal(4, summary.AttendeeGroupsEnsured);
        Assert.Equal(8, summary.IdentitiesEnsured);
        Assert.Equal(8, summary.ProfilesEnsured);
        Assert.Equal(7, summary.EventsEnsured);
        Assert.Equal(2, summary.ProposalsEnsured);
        Assert.Equal(9, summary.AttendeesEnsured);
        Assert.Equal(3, await database.Locations.CountAsync());
        Assert.Equal(6, await database.AppointmentTypes.CountAsync());
        Assert.Equal(
            "DOC",
            Assert.Single(await database.AppointmentTypes.Where(t => !t.IsActive).Select(t => t.Code).ToListAsync()));
        Assert.Equal(4, await database.AttendeeGroups.CountAsync());
        Assert.Equal(8, await database.StaffAccessProfiles.CountAsync());
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
        Assert.Equal(8, await database.StaffIdentities.CountAsync());
        Assert.Equal(7, await database.Events.CountAsync());
        // Seven accepted-event proposals plus the two open negotiation scenarios.
        Assert.Equal(9, await database.EventProposals.CountAsync());
        Assert.Equal(2, await database.EventProposals.CountAsync(p => p.Status == EventProposalStatus.Open));
        var events = await database.Events.AsNoTracking().ToListAsync();
        var proposals = await database.EventProposals.AsNoTracking().ToDictionaryAsync(p => p.Id);
        Assert.Equal(events.Count, events.Select(e => e.ProposalId).Distinct().Count());
        Assert.All(events, e => Assert.Equal(EventProposalStatus.Confirmed, proposals[e.ProposalId].Status));
        Assert.Equal(9, await database.Attendees.CountAsync());
        Assert.Equal(1, await database.SystemSettings.CountAsync());
        Assert.Equal(9, await database.Invites.CountAsync());
        Assert.Equal(6, await database.Bookings.CountAsync());
        Assert.Equal(16, await database.BookingAppointments.CountAsync());
        Assert.Equal(
            2,
            await database.BookingAppointments.CountAsync(
                appointment => appointment.Status == BookingAppointmentStatus.NoShow));
        Assert.Equal(
            2,
            await database.BookingAppointments.CountAsync(
                appointment => appointment.Status == BookingAppointmentStatus.Completed));
        Assert.True(
            await database.BookingAppointments.AnyAsync(
                appointment => appointment.Status == BookingAppointmentStatus.CheckedIn));
        Assert.Equal(0, await database.EmailLogs.CountAsync());
    }
}
