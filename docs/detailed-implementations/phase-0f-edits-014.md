# 00f — Retire the single-site configuration, edits 14 (Task 3d)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.SeedData.Tests/DemoInvitationSeederTests.cs — 1/1

<!-- retirement-file: {"id":38,"file":"tests/EventBooking.SeedData.Tests/DemoInvitationSeederTests.cs","beforeSha":"ba2e38c53e30245ccd52041e0664e3506c32ad0a6be941a7462a50cda6e66826","afterSha":"26caf751bb59f2f8c7b4c39ef845f9d0e849e39780fb48604d09c8e321d38fa4","side":"after","part":1,"parts":1} -->

`````csharp
using System.Text.RegularExpressions;
using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Events;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;
using EventBooking.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace EventBooking.SeedData.Tests;

/// <summary>Exercises real seeded links, delivery recovery and attendee lifecycle preservation.</summary>
[Collection("seed-anchor")]
public sealed class DemoInvitationSeederTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly CapturingTransport _mail = new();
    private readonly DemoClock _clock = new();
    private ServiceProvider _services = null!;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        DemoSeedSpec.OverrideAnchor(_clock.TodayAtTransitionalLocation);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBookingInfrastructure(_postgres.GetConnectionString(),
            new ClockOptions("Europe/London"),
            new TokenOptions("test-seed-and-api-share-this-signing-key"));
        services.AddEventBookingApplication(new AttendeePortalOptions(
            "https://demo.example.test", "help@example.com"));
        services.AddSingleton<IClock>(_clock);
        services.AddSingleton<IEmailTransport>(_mail);
        services.AddScoped<DemoSeeder>();
        services.AddScoped<DemoInvitationSeeder>();
        _services = services.BuildServiceProvider();
        using var scope = _services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<EventBookingDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<DemoSeeder>().RunAsync(default);
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        DemoSeedSpec.OverrideAnchor(null);
        if (_services is not null) await _services.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>All five delivered HTML/text links open future options; one can book and be managed.</summary>
    [Fact]
    public async Task FreshSeedProducesFiveUsableLinksAndARealBooking()
    {
        Assert.Equal(5, await SeedAsync());
        Assert.Equal(5, _mail.Messages.Count);
        Assert.Equal(Enumerable.Range(1, 5).Select(i => $"demo-attendee-{i:000}@example.com"),
            _mail.Messages.Select(m => m.ToAddress));
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        Assert.Equal(100, await db.Attendees.CountAsync());
        Assert.Equal(5, await db.Attendees.CountAsync(c => c.Status == AttendeeStatus.Invited));
        // Journey seeds write booking rows directly without transitioning Attendee
        // status, so only the 5 newly invited attendees leave NotYetInvited.
        Assert.Equal(95, await db.Attendees.CountAsync(c => c.Status == AttendeeStatus.NotYetInvited));
        Assert.Equal(5, await db.Invites.CountAsync(i => i.Status == InviteStatus.Pending));
        Assert.Equal(5, await db.EmailLogs.CountAsync(e => e.Status == EmailStatus.Sent));
        Assert.Equal(12, await db.Events.CountAsync());
        // Five negotiation scenarios plus one accepted proposal for each of twelve events.
        Assert.Equal(17, await db.EventProposals.CountAsync());
        var events = await db.Events.AsNoTracking().ToListAsync();
        var proposals = await db.EventProposals.AsNoTracking().ToDictionaryAsync(p => p.Id);
        Assert.Equal(events.Count, events.Select(e => e.ProposalId).Distinct().Count());
        Assert.All(events, e => Assert.Equal(EventProposalStatus.Confirmed, proposals[e.ProposalId].Status));
        Assert.Equal(5, await db.Attendees.Where(c => c.Status == AttendeeStatus.Invited)
            .Select(c => c.AttendeeGroupId).Distinct().CountAsync());
        var view = scope.ServiceProvider.GetRequiredService<ViewInviteHandler>();
        foreach (var message in _mail.Messages.ToArray())
        {
            var token = Token(message);
            Assert.Contains($"href=\"https://demo.example.test/book/{token}\"", message.HtmlBody);
            var result = await view.HandleAsync(new ViewInviteQuery(token), default);
            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.Value.Options.Count);
            Assert.All(result.Value.Options, option => Assert.True(option.Date > _clock.TodayAtTransitionalLocation));
            var attendee = await db.Attendees.Include(c => c.Requirements)
                .SingleAsync(c => c.Id == message.AttendeeId);
            var invite = await db.Invites.Include(i => i.Requirements)
                .SingleAsync(i => i.Id == result.Value.InviteId);
            Assert.Equal(attendee.RequiredAppointmentTypeIds.Order(), invite.RequiredAppointmentTypeIds.Order());
            Assert.NotEqual(token, invite.TokenHash);
        }
        var first = Token(_mail.Messages[0]);
        var offered = await view.HandleAsync(new ViewInviteQuery(first), default);
        var booking = await scope.ServiceProvider.GetRequiredService<ConfirmBookingHandler>()
            .HandleAsync(new ConfirmBookingCommand(first, offered.Value.Options[0].EventId), default);
        Assert.True(booking.IsSuccess);
        Assert.Equal("Sent", booking.Value.DeliveryStatus);
        Assert.True((await scope.ServiceProvider.GetRequiredService<ViewBookingHandler>()
            .HandleAsync(new ViewBookingQuery(booking.Value.ManageToken), default)).IsSuccess);
        Assert.False((await view.HandleAsync(new ViewInviteQuery(first), default)).IsSuccess);
        Assert.Equal(EmailTemplate.BookingConfirmation, _mail.Messages[^1].Template);
    }

    /// <summary>A rerun preserves successful tokens, capacities and an already-consumed invitation.</summary>
    [Fact]
    public async Task RerunDoesNotResendOrUndoBooking()
    {
        await SeedAsync();
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var first = Token(_mail.Messages[0]);
        var offered = await scope.ServiceProvider.GetRequiredService<ViewInviteHandler>()
            .HandleAsync(new ViewInviteQuery(first), default);
        var booked = await scope.ServiceProvider.GetRequiredService<ConfirmBookingHandler>()
            .HandleAsync(new ConfirmBookingCommand(first, offered.Value.Options[0].EventId), default);
        Assert.True(booked.IsSuccess);
        var hashes = await db.Invites.AsNoTracking().OrderBy(i => i.Id).Select(i => i.TokenHash).ToListAsync();
        var capacities = await db.Events.AsNoTracking().Include(s => s.Capacities)
            .OrderBy(s => s.Id).ToListAsync();
        var before = capacities.SelectMany(s => s.Capacities.OrderBy(c => c.AppointmentTypeId))
            .Select(c => c.RemainingCapacity).ToArray();
        Assert.Equal(0, await SeedAsync());
        Assert.Equal(6, _mail.Messages.Count);
        Assert.Equal(hashes, await db.Invites.AsNoTracking().OrderBy(i => i.Id).Select(i => i.TokenHash).ToListAsync());
        var after = await db.Events.AsNoTracking().Include(s => s.Capacities)
            .OrderBy(s => s.Id).ToListAsync();
        Assert.Equal(before, after.SelectMany(s => s.Capacities.OrderBy(c => c.AppointmentTypeId))
            .Select(c => c.RemainingCapacity).ToArray());
    }

    /// <summary>Provider failure leaves a durable attempt that the next run retries once.</summary>
    [Fact]
    public async Task FailedDeliveryResumesWithoutDuplicatingSuccesses()
    {
        _mail.FailOnAttempt = 3;
        await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var failed = await db.EmailLogs.AsNoTracking().SingleAsync(e => e.Status == EmailStatus.Failed);
        var oldHash = await db.Invites.Where(i => i.Id == failed.InviteId).Select(i => i.TokenHash).SingleAsync();
        Assert.Equal(2, _mail.Messages.Count);
        _mail.FailOnAttempt = null;
        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(3, await SeedAsync());
        Assert.Equal(5, _mail.Messages.Count);
        Assert.Equal(5, _mail.Messages.Select(m => m.AttendeeId).Distinct().Count());
        Assert.Equal(5, await db.Invites.CountAsync(i => i.Status == InviteStatus.Pending));
        Assert.Equal(1, await db.EmailLogs.CountAsync(e => e.Status == EmailStatus.Resolved));
        Assert.Equal(5, await db.EmailLogs.CountAsync(e => e.Status == EmailStatus.Sent));
        Assert.NotEqual(oldHash, await db.Invites.Where(i => i.Id == failed.InviteId)
            .Select(i => i.TokenHash).SingleAsync());
        Assert.Equal(0, await SeedAsync());
    }

    /// <summary>Resetting database state creates new invitations and invalidates old raw links.</summary>
    [Fact]
    public async Task ReseedCreatesFreshLinks()
    {
        await SeedAsync();
        var oldToken = Token(_mail.Messages[0]);
        using (var scope = _services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<DemoSeeder>().ReseedAsync(default);
        Assert.Equal(5, await SeedAsync());
        using var verify = _services.CreateScope();
        var view = verify.ServiceProvider.GetRequiredService<ViewInviteHandler>();
        Assert.False((await view.HandleAsync(new ViewInviteQuery(oldToken), default)).IsSuccess);
        Assert.True((await view.HandleAsync(new ViewInviteQuery(Token(_mail.Messages[5])), default)).IsSuccess);
    }

    /// <summary>Expired history is not replaced even when demo dates are moved forward.</summary>
    [Fact]
    public async Task ExpiredInvitationsArePreserved()
    {
        await SeedAsync();
        _clock.Advance(TimeSpan.FromDays(30));
        DemoSeedSpec.OverrideAnchor(_clock.TodayAtTransitionalLocation);
        Assert.Equal(0, await SeedAsync());
        Assert.Equal(5, _mail.Messages.Count);
        using var scope = _services.CreateScope();
        Assert.False((await scope.ServiceProvider.GetRequiredService<ViewInviteHandler>()
            .HandleAsync(new ViewInviteQuery(Token(_mail.Messages[0])), default)).IsSuccess);
    }

    /// <summary>Stale windows fail before issuing a misleading invitation.</summary>
    [Fact]
    public async Task StaleAnchorFailsBeforeSending()
    {
        _clock.Advance(TimeSpan.FromDays(4));
        var error = await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        Assert.Contains("--reanchor", error.Message);
        Assert.Empty(_mail.Messages);
    }

    /// <summary>A live claim is not stolen, but an expired claim can be recovered on a later run.</summary>
    [Fact]
    public async Task PendingClaimUsesExistingLease()
    {
        await SeedAsync();
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var prior = await db.EmailLogs.SingleAsync(e => e.AttendeeId == _mail.Messages[0].AttendeeId);
            var pending = EmailLog.RecordPending(Guid.NewGuid(), prior.AttendeeId,
                EmailTemplate.AttendeeInvite, _clock.UtcNow.AddSeconds(1), prior.InviteId);
            Assert.True(pending.TryClaim(_clock.UtcNow, TimeSpan.FromMinutes(5)));
            db.EmailLogs.Add(pending);
            await db.SaveChangesAsync();
        }
        var error = await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        Assert.Contains("already being delivered", error.Message);
        Assert.Equal(5, _mail.Messages.Count);
        _clock.Advance(TimeSpan.FromMinutes(6));
        Assert.Equal(1, await SeedAsync());
        Assert.Equal(6, _mail.Messages.Count);
    }

    /// <summary>A pending invitation lacking its delivery record is not reported as sent.</summary>
    [Fact]
    public async Task MissingDeliveryFailsVisibly()
    {
        await SeedAsync();
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            await db.EmailLogs.Where(e => e.AttendeeId == _mail.Messages[0].AttendeeId).ExecuteDeleteAsync();
        }
        var error = await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        Assert.Contains("no matching delivery", error.Message);
        Assert.Equal(5, _mail.Messages.Count);
    }

    /// <summary>An old failure cannot hide a successful Coordinator replacement for the same Attendee.</summary>
    [Fact]
    public async Task CoordinatorReplacementPreservesItsSuccessfulDelivery()
    {
        _mail.FailOnAttempt = 1;
        await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        _mail.FailOnAttempt = null;
        _clock.Advance(TimeSpan.FromSeconds(1));
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var attendee = await db.Attendees.SingleAsync(c => c.Email == "demo-attendee-001@example.com");
            var issued = await scope.ServiceProvider
                .GetRequiredService<EventBooking.Application.Invites.TriggerInviteHandler>()
                .HandleAsync(new EventBooking.Application.Invites.TriggerInviteCommand(
                    DemoSeedSpec.CoordinatorUserId(), attendee.Id), default);
            Assert.True(issued.IsSuccess);
            Assert.True(issued.Value.EmailSent);
        }
        var replacementToken = Token(Assert.Single(_mail.Messages));
        Assert.Equal(4, await SeedAsync());
        Assert.Equal(5, _mail.Messages.Count);
        using var verify = _services.CreateScope();
        Assert.True((await verify.ServiceProvider.GetRequiredService<ViewInviteHandler>()
            .HandleAsync(new ViewInviteQuery(replacementToken), default)).IsSuccess);
        Assert.Equal(0, await SeedAsync());
    }

    /// <summary>Unrelated outstanding work is not retried or token-rotated by demo seeding.</summary>
    [Fact]
    public async Task AnotherOutstandingTemplateRequiresCoordinatorReview()
    {
        _mail.FailOnAttempt = 1;
        await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        _mail.FailOnAttempt = null;
        string originalHash;
        Guid inviteId;
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var prior = await db.EmailLogs.SingleAsync(e => e.Status == EmailStatus.Failed);
            inviteId = prior.InviteId!.Value;
            originalHash = await db.Invites.Where(i => i.Id == inviteId).Select(i => i.TokenHash).SingleAsync();
            var other = EmailLog.RecordPending(Guid.NewGuid(), prior.AttendeeId,
                EmailTemplate.AttendeeReinvite, _clock.UtcNow.AddSeconds(1), inviteId);
            db.EmailLogs.Add(other);
            await db.SaveChangesAsync();
        }
        var error = await Assert.ThrowsAsync<SeedException>(() => SeedAsync());
        Assert.Contains("Another outstanding delivery", error.Message);
        Assert.Empty(_mail.Messages);
        using var verify = _services.CreateScope();
        var database = verify.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        Assert.Equal(originalHash, await database.Invites.Where(i => i.Id == inviteId)
            .Select(i => i.TokenHash).SingleAsync());
    }

    private async Task<int> SeedAsync()
    {
        using var scope = _services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DemoInvitationSeeder>().RunAsync(default);
    }

    private static string Token(EmailMessage message) =>
        Regex.Match(message.TextBody, @"https://demo\.example\.test/book/([^\s]+)").Groups[1].Value;

    private sealed class CapturingTransport : IEmailTransport
    {
        /// <summary>Messages accepted by the external provider boundary.</summary>
        public List<EmailMessage> Messages { get; } = [];
        /// <summary>Optional one-based provider attempt to fail.</summary>
        public int? FailOnAttempt { get; set; }
        private int _attempt;
        /// <inheritdoc/>
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            if (++_attempt == FailOnAttempt) throw new IOException("Test SMTP failure.");
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class DemoClock : IClock
    {
        /// <inheritdoc/>
        public DateTimeOffset UtcNow { get; private set; } = new(2030, 1, 7, 12, 0, 0, TimeSpan.Zero);
        /// <inheritdoc/>
        public DateTimeOffset NowAtTransitionalLocation => TimeZoneInfo.ConvertTime(UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Europe/London"));
        /// <inheritdoc/>
        public DateOnly TodayAtTransitionalLocation => DateAtTransitionalLocation(UtcNow);
        /// <inheritdoc/>
        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) => DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById("Europe/London")).DateTime);
        /// <inheritdoc/>
        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(
            instant, TimeZoneInfo.FindSystemTimeZoneById("Europe/London"));
        /// <summary>Moves the observation clock without modifying persisted data.</summary>
        public void Advance(TimeSpan elapsed) => UtcNow += elapsed;
    }
}
`````

## before — tests/EventBooking.SeedData.Tests/ReanchorTests.cs — 1/1

<!-- retirement-file: {"id":39,"file":"tests/EventBooking.SeedData.Tests/ReanchorTests.cs","beforeSha":"0c5acdc75e4058315aca9e44baf3e91732b6327a542f73b9a5099b0b9489c305","afterSha":"82913c5bc70bbb5afb47599bbb8f968caa5c2f5f88414918276d2111a26d82d2","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.SeedData.Tests/ReanchorTests.cs — 1/1

<!-- retirement-file: {"id":39,"file":"tests/EventBooking.SeedData.Tests/ReanchorTests.cs","beforeSha":"0c5acdc75e4058315aca9e44baf3e91732b6327a542f73b9a5099b0b9489c305","afterSha":"82913c5bc70bbb5afb47599bbb8f968caa5c2f5f88414918276d2111a26d82d2","side":"after","part":1,"parts":1} -->

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
`````

## before — tests/EventBooking.SeedData.Tests/ReseedTests.cs — 1/1

<!-- retirement-file: {"id":40,"file":"tests/EventBooking.SeedData.Tests/ReseedTests.cs","beforeSha":"acba6154e52baf235aad56fc73c365f34b843887a41d5a50f1ff13f51a08fb57","afterSha":"690e4268e663978d94fc1baff6ef3b345bf112cd70f3e9a6c4431e7daca802cc","side":"before","part":1,"parts":1} -->

`````csharp
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
`````

## after — tests/EventBooking.SeedData.Tests/ReseedTests.cs — 1/1

<!-- retirement-file: {"id":40,"file":"tests/EventBooking.SeedData.Tests/ReseedTests.cs","beforeSha":"acba6154e52baf235aad56fc73c365f34b843887a41d5a50f1ff13f51a08fb57","afterSha":"690e4268e663978d94fc1baff6ef3b345bf112cd70f3e9a6c4431e7daca802cc","side":"after","part":1,"parts":1} -->

`````csharp
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
`````

## before — tests/EventBooking.Web.Tests/RepairDNotificationComponentTests.cs — 1/1

<!-- retirement-file: {"id":41,"file":"tests/EventBooking.Web.Tests/RepairDNotificationComponentTests.cs","beforeSha":"5efd396df1ac4624fa4d278434967336d768cafa2572c1b49e4dc7e8d762be16","afterSha":"68c693b319507c7c7b0be186141027b912e9eb570433c5261f0b38fc2636cb52","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

/// <summary>Verifies attendee pages describe durable email outcomes without false promises.</summary>
public class RepairDNotificationComponentTests : BunitContext
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Booking confirmation failure still displays the usable manage-link recovery.</summary>
    [Fact]
    public void BookPageUsesNeutralCopyWhenConfirmationDeliveryFails()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new InviteDto(
            Guid.NewGuid(),
            "Amara Novak",
            ["DAT"],
            [new InviteOptionDto(
                Guid.NewGuid(),
                new DateOnly(2030, 1, 14),
                new TimeOnly(9, 0),
                new TimeOnly(13, 0),
                "Monday 14 Jan 2030, 09:00-13:00")] )));
        handler.Enqueue(_ => Json(new ConfirmedBookingDto(
            Guid.NewGuid(),
            new DateOnly(2030, 1, 14),
            new TimeOnly(9, 0),
            new TimeOnly(13, 0),
            "fresh-manage-token",
            "Failed")));
        Services.AddSingleton(new BookingClient(NewHttpClient(handler)));
        Services.AddSingleton(new AttendeePageOptions("recruitment@example.com"));

        var cut = Render<Book>(parameters => parameters.Add(page => page.Token, "invite-token"));
        cut.WaitForAssertion(() => Assert.Contains("Confirm this time", cut.Markup));
        cut.Find("input[type=radio]").Change(true);
        cut.Find("button.booking-page__button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("could not confirm email delivery", cut.Markup);
            Assert.Contains("fresh-manage-token", cut.Markup);
            Assert.DoesNotContain("is on its way", cut.Markup);
        });
    }

    /// <summary>The confirmed page names the transitional location the API sent, the same one the confirmation email uses.</summary>
    [Fact]
    public void BookPageShowsTheTransitionalLocationAddressReturnedByTheApi()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new InviteDto(
            Guid.NewGuid(),
            "Amara Novak",
            ["DAT"],
            [new InviteOptionDto(
                Guid.NewGuid(),
                new DateOnly(2030, 1, 14),
                new TimeOnly(9, 0),
                new TimeOnly(13, 0),
                "Monday 14 Jan 2030, 09:00-13:00")])));
        handler.Enqueue(_ => Json(new ConfirmedBookingDto(
            Guid.NewGuid(),
            new DateOnly(2030, 1, 14),
            new TimeOnly(9, 0),
            new TimeOnly(13, 0),
            "fresh-manage-token",
            "Sent",
            "2 Api Street, London")));
        Services.AddSingleton(new BookingClient(NewHttpClient(handler)));
        Services.AddSingleton(new AttendeePageOptions("recruitment@example.com"));

        var cut = Render<Book>(parameters => parameters.Add(page => page.Token, "invite-token"));
        cut.WaitForAssertion(() => Assert.Contains("Confirm this time", cut.Markup));
        cut.Find("input[type=radio]").Change(true);
        cut.Find("button.booking-page__button").Click();

        cut.WaitForAssertion(() => Assert.Contains("Head office: 2 Api Street, London", cut.Markup));
    }

    /// <summary>Booking confirmation pending delivery also uses neutral recovery copy.</summary>
    [Fact]
    public void BookPageUsesNeutralCopyWhenConfirmationDeliveryIsPending()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new InviteDto(
            Guid.NewGuid(),
            "Amara Novak",
            ["DAT"],
            [new InviteOptionDto(
                Guid.NewGuid(),
                new DateOnly(2030, 1, 14),
                new TimeOnly(9, 0),
                new TimeOnly(13, 0),
                "Monday 14 Jan 2030, 09:00-13:00")])));
        handler.Enqueue(_ => Json(new ConfirmedBookingDto(
            Guid.NewGuid(),
            new DateOnly(2030, 1, 14),
            new TimeOnly(9, 0),
            new TimeOnly(13, 0),
            "fresh-manage-token",
            "Pending")));
        Services.AddSingleton(new BookingClient(NewHttpClient(handler)));
        Services.AddSingleton(new AttendeePageOptions("recruitment@example.com"));

        var cut = Render<Book>(parameters => parameters.Add(page => page.Token, "invite-token"));
        cut.WaitForAssertion(() => Assert.Contains("Confirm this time", cut.Markup));
        cut.Find("input[type=radio]").Change(true);
        cut.Find("button.booking-page__button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("could not confirm email delivery", cut.Markup);
            Assert.Contains("fresh-manage-token", cut.Markup);
            Assert.DoesNotContain("has been sent", cut.Markup);
        });
    }

    /// <summary>Cancel/rebook failure displays neutral recovery rather than sent wording.</summary>
    [Fact]
    public void ManagePageUsesNeutralCopyWhenReplacementDeliveryFails()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new BookingDto(
            new DateOnly(2030, 1, 14),
            new TimeOnly(9, 0),
            new TimeOnly(13, 0),
            "Monday 14 Jan 2030, 09:00-13:00",
            "Amara Novak")));
        handler.Enqueue(_ => Json(new CancelOutcomeDto(
            Reinvited: true,
            InviteCreated: true,
            DeliveryStatus: "Failed",
            DeliveryId: Guid.NewGuid())));
        Services.AddSingleton(new BookingClient(NewHttpClient(handler)));
        Services.AddSingleton(new AttendeePageOptions("recruitment@example.com"));

        var cut = Render<ManageBooking>(parameters => parameters.Add(page => page.Token, "manage-token"));
        cut.WaitForAssertion(() => Assert.Contains("Cancel and choose a new time", cut.Markup));
        cut.FindAll("button").Single(button => button.TextContent.Contains("Cancel and choose")).Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("could not confirm delivery", cut.Markup);
            Assert.DoesNotContain("has been sent", cut.Markup);
            Assert.DoesNotContain("on its way", cut.Markup);
        });
    }

    /// <summary>Cancel/rebook pending delivery displays neutral recovery guidance.</summary>
    [Fact]
    public void ManagePageUsesNeutralCopyWhenReplacementDeliveryIsPending()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new BookingDto(
            new DateOnly(2030, 1, 14),
            new TimeOnly(9, 0),
            new TimeOnly(13, 0),
            "Monday 14 Jan 2030, 09:00-13:00",
            "Amara Novak")));
        handler.Enqueue(_ => Json(new CancelOutcomeDto(
            Reinvited: true,
            InviteCreated: true,
            DeliveryStatus: "Pending",
            DeliveryId: Guid.NewGuid())));
        Services.AddSingleton(new BookingClient(NewHttpClient(handler)));
        Services.AddSingleton(new AttendeePageOptions("recruitment@example.com"));

        var cut = Render<ManageBooking>(parameters => parameters.Add(page => page.Token, "manage-token"));
        cut.WaitForAssertion(() => Assert.Contains("Cancel and choose a new time", cut.Markup));
        cut.FindAll("button").Single(button => button.TextContent.Contains("Cancel and choose")).Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("could not confirm delivery", cut.Markup);
            Assert.DoesNotContain("has been sent", cut.Markup);
            Assert.DoesNotContain("on its way", cut.Markup);
        });
    }

    /// <summary>A missing replacement invite explains that the team will arrange the next time.</summary>
    [Fact]
    public void ManagePageDistinguishesUnavailableReplacementInvite()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new BookingDto(
            new DateOnly(2030, 1, 14),
            new TimeOnly(9, 0),
            new TimeOnly(13, 0),
            "Monday 14 Jan 2030, 09:00-13:00",
            "Amara Novak")));
        handler.Enqueue(_ => Json(new CancelOutcomeDto(
            Reinvited: false,
            InviteCreated: false,
            DeliveryStatus: "Unavailable")));
        Services.AddSingleton(new BookingClient(NewHttpClient(handler)));
        Services.AddSingleton(new AttendeePageOptions("recruitment@example.com"));

        var cut = Render<ManageBooking>(parameters => parameters.Add(page => page.Token, "manage-token"));
        cut.WaitForAssertion(() => Assert.Contains("Cancel and choose a new time", cut.Markup));
        cut.FindAll("button").Single(button => button.TextContent.Contains("Cancel and choose")).Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("There are no times available right now", cut.Markup);
            Assert.DoesNotContain("could not confirm delivery", cut.Markup);
            Assert.DoesNotContain("has been sent", cut.Markup);
        });
    }

    /// <summary>A sent replacement invite is the only state that promises delivery.</summary>
    [Fact]
    public void ManagePagePromisesReplacementDeliveryOnlyWhenSent()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new BookingDto(
            new DateOnly(2030, 1, 14),
            new TimeOnly(9, 0),
            new TimeOnly(13, 0),
            "Monday 14 Jan 2030, 09:00-13:00",
            "Amara Novak")));
        handler.Enqueue(_ => Json(new CancelOutcomeDto(
            Reinvited: true,
            InviteCreated: true,
            DeliveryStatus: "Sent",
            DeliveryId: Guid.NewGuid())));
        Services.AddSingleton(new BookingClient(NewHttpClient(handler)));
        Services.AddSingleton(new AttendeePageOptions("recruitment@example.com"));

        var cut = Render<ManageBooking>(parameters => parameters.Add(page => page.Token, "manage-token"));
        cut.WaitForAssertion(() => Assert.Contains("Cancel and choose a new time", cut.Markup));
        cut.FindAll("button").Single(button => button.TextContent.Contains("Cancel and choose")).Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("new invitation with fresh times has been sent", cut.Markup);
            Assert.DoesNotContain("could not confirm delivery", cut.Markup);
        });
    }

    /// <summary>A stale failed delivery remains visible but offers no enabled resend action.</summary>
    [Fact]
    public void AttendeesHideResendWhenServerMarksTheLatestContextStale()
    {
        var attendeeId = Guid.NewGuid();
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new List<AttendeeDto>
        {
            new(attendeeId, "Amara Novak", "a.novak@mail.com", Guid.NewGuid(), "MED", "Medical", [new("DAT", "Drug & Alcohol Testing")], 1, "Not yet invited"),
        }));
        handler.Enqueue(_ => Json(new DashboardsDto(
            AwaitingAvailability: [],
            NoResponse: [],
            Events: [],
            EmailStatuses:
            [new AttendeeEmailStatusDto(
                attendeeId,
                "Booking confirmation",
                new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.Zero),
                "Failed",
                CanRetry: false)])));
        handler.Enqueue(_ => Json(new List<AttendeeGroupOptionDto>()));
        Services.AddSingleton(new AttendeesClient(NewHttpClient(handler)));
        Services.AddSingleton(new DashboardsClient(NewHttpClient(handler)));
        Services.AddSingleton(new AuditClient(NewHttpClient(handler)));
        Services.AddSingleton(new TransitionalLocationTimePresentation("Europe/London"));

        var cut = Render<Attendees>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Booking confirmation", cut.Markup);
            Assert.DoesNotContain(">Resend<", cut.Markup);
        });
    }

    /// <summary>Actionable failed and pending deliveries use the retry endpoint, not the invite endpoint.</summary>
    [Theory]
    [InlineData("Failed")]
    [InlineData("Pending")]
    public void AttendeesResendActionUsesTheTemplateAwareRetryEndpoint(string status)
    {
        var attendeeId = Guid.NewGuid();
        var attendee = new AttendeeDto(
            attendeeId, "Amara Novak", "a.novak@mail.com", Guid.NewGuid(), "MED", "Medical", [new("DAT", "Drug & Alcohol Testing")], 1, "Not yet invited");
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new List<AttendeeDto> { attendee }));
        handler.Enqueue(_ => Json(new DashboardsDto(
            AwaitingAvailability: [],
            NoResponse: [],
            Events: [],
            EmailStatuses:
            [new AttendeeEmailStatusDto(
                attendeeId,
                "Booking confirmation",
                new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.Zero),
                status,
                CanRetry: true)])));
        handler.Enqueue(_ => Json(new List<AttendeeGroupOptionDto>()));
        handler.Enqueue(_ => Json(new EmailRetryDto("Sent", Guid.NewGuid())));
        handler.Enqueue(_ => Json(new List<AttendeeDto> { attendee }));
        handler.Enqueue(_ => Json(new DashboardsDto(
            AwaitingAvailability: [],
            NoResponse: [],
            Events: [],
            EmailStatuses:
            [new AttendeeEmailStatusDto(
                attendeeId,
                "Booking confirmation",
                new DateTimeOffset(2026, 9, 7, 10, 1, 0, TimeSpan.Zero),
                "Sent",
                CanRetry: false)])));
        handler.Enqueue(_ => Json(new List<AttendeeGroupOptionDto>()));
        Services.AddSingleton(new AttendeesClient(NewHttpClient(handler)));
        Services.AddSingleton(new DashboardsClient(NewHttpClient(handler)));
        Services.AddSingleton(new AuditClient(NewHttpClient(handler)));
        Services.AddSingleton(new TransitionalLocationTimePresentation("Europe/London"));

        var cut = Render<Attendees>();
        cut.WaitForAssertion(() => Assert.Contains(">Resend<", cut.Markup));

        cut.FindAll("button").Single(button => button.TextContent == "Resend").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(
                handler.Requests,
                request => request.Method == HttpMethod.Post
                    && request.Path == $"/api/attendees/{attendeeId}/email-retry");
            Assert.DoesNotContain(
                handler.Requests,
                request => request.Method == HttpMethod.Post
                    && request.Path == $"/api/attendees/{attendeeId}/invite");
        });
    }

    private static HttpClient NewHttpClient(HttpMessageHandler handler) => new(handler)
    {
        BaseAddress = new Uri("https://api.example.com"),
    };

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(value, options: CamelCase),
    };

    private sealed class RoutedHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = [];

        /// <summary>Records the method and path of every handled request.</summary>
        public List<(HttpMethod Method, string Path)> Requests { get; } = [];

        /// <summary>Queues one deterministic response for the next page request.</summary>
        public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> response) => _responses.Enqueue(response);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add((request.Method, request.RequestUri?.AbsolutePath ?? string.Empty));
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No HTTP response was configured.");
            }

            return Task.FromResult(_responses.Dequeue()(request));
        }
    }
}
`````
