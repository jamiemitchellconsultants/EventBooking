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
