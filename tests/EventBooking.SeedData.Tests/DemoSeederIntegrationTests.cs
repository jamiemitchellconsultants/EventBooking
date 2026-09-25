// tests/EventBooking.SeedData.Tests/DemoSeederIntegrationTests.cs (complete)
using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Audit;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Time;
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

    [Theory]
    [InlineData(1)]
    // Event 1 (LONDON 09:00, anchor+3) lands on event 7's old slot (anchor+18), and its
    // proposal likewise; both move together, so neither is a collision.
    [InlineData(15)]
    public async Task ReanchorOnALaterDayMovesOwnedWindowsWithoutChangingCounts(int days)
    {
        await RunSeedAndInvitationsAsync();
        var before = await CountsAsync();

        _clock.Advance(TimeSpan.FromDays(days));
        DemoSeedSpec.OverrideAnchor(new DateOnly(2026, 9, 22).AddDays(days));
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
        var startInstants = await db.Events.Where(x => eventIds.Contains(x.Id))
            .Select(x => new { x.Id, StartUtc = EF.Property<DateTimeOffset?>(x, EventStartInstants.PropertyName) })
            .ToDictionaryAsync(x => x.Id, x => x.StartUtc);
        var zones = new NodaTimeEventWindowZones();
        Assert.All(target.Events, spec => Assert.Equal(
            EventStartInstants.InstantOf(
                new EventWindow(spec.Date, spec.StartTime, spec.DurationMinutes),
                target.Locations.Single(x => x.Code == spec.LocationCode).TimeZoneId, zones),
            startInstants[spec.Id]));
        var proposalIds = target.OpenProposals.Select(x => x.Id).ToList();
        var proposalDates = await db.EventProposals
            .Where(x => proposalIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Window.Date);
        Assert.All(target.OpenProposals, spec => Assert.Equal(spec.Date, proposalDates[spec.Id]));
    }

    [Fact]
    public async Task AJourneyThatFailsPartWayLeavesNoAttendeeForTheRerunToSkip()
    {
        await ExecuteSqlAsync(
            """
            CREATE FUNCTION fail_invite_insert() RETURNS trigger LANGUAGE plpgsql
                AS $$ BEGIN RAISE EXCEPTION 'injected invite failure'; END $$;
            CREATE TRIGGER fail_invite_insert BEFORE INSERT ON invite
                FOR EACH ROW EXECUTE FUNCTION fail_invite_insert();
            """);
        await Assert.ThrowsAnyAsync<Exception>(() => RunSeedOnlyAsync());
        await ExecuteSqlAsync("""
            DROP TRIGGER fail_invite_insert ON invite;
            DROP FUNCTION fail_invite_insert();
            """);

        await RunSeedOnlyAsync();

        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var statuses = await db.Attendees.ToDictionaryAsync(x => x.Email, x => x.Status);
        Assert.All(DemoSeedSpec.Build().Attendees,
            spec => Assert.Equal(spec.Status, statuses[spec.Email]));
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

    [Fact]
    public async Task StaleInvitationDatesFailFastUntilReanchored()
    {
        await RunSeedOnlyAsync();
        _clock.Advance(TimeSpan.FromDays(30));

        var error = await Assert.ThrowsAsync<SeedException>(() => RunInvitationsAsync());

        Assert.Contains("reanchor", error.Message);
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

    private async Task ExecuteSqlAsync(string sql)
    {
        await using var scope = _provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<EventBookingDbContext>()
            .Database.ExecuteSqlRawAsync(sql);
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
