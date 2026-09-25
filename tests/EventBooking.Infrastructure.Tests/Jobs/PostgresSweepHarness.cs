using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Jobs;
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Events;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Locations;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Jobs;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Tests.Email;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;
using EventBooking.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace EventBooking.Infrastructure.Tests.Jobs;

/// <summary>Captures the sweep runner's log lines for the failing-item test.</summary>
public sealed class SweepListLogger : ILogger<SweepRunner>
{
    /// <summary>The formatted lines logged so far.</summary>
    public List<string> Lines { get; } = [];

    /// <inheritdoc />
    IDisposable? ILogger.BeginScope<TState>(TState state) => null;

    /// <inheritdoc />
    bool ILogger.IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    void ILogger.Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        Lines.Add(formatter(state, exception));
}

/// <summary>Holds one run's advisory lock until the test releases it.</summary>
/// <param name="scope">The run's service scope.</param>
public sealed class RunHold(IServiceScope scope) : IAsyncDisposable
{
    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        await AdvisoryLock.ReleaseSweepLockAsync(context, CancellationToken.None);
        scope.Dispose();
    }
}

/// <summary>Seeds sweep candidates through the real database and drives runner passes.</summary>
public abstract class PostgresSweepHarness(PostgresFixture fixture) : IDisposable
{
    /// <summary>Mid-January, so the London zone reads as GMT with no daylight-saving risk.</summary>
    protected static readonly DateTimeOffset Now =
        new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The clock the harness and its runners share.</summary>
    protected readonly HarnessClock Clock = new(Now);

    private readonly SweepListLogger _runnerLogger = new();
    private readonly List<ServiceProvider> _providers = [];
    private bool _reset;

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var provider in _providers)
        {
            provider.Dispose();
        }
    }

    /// <summary>Acquires the sweep lock and holds it until the test releases the run.</summary>
    protected async Task<IAsyncDisposable> AcquireRunAsync()
    {
        var scope = NewProvider().CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        Assert.True(await AdvisoryLock.TryAcquireSweepLockAsync(context, CancellationToken.None));
        return new RunHold(scope);
    }

    /// <summary>Runs one sweep pass the way the hosted service does.</summary>
    protected async Task<(bool Started, SweepMetrics Metrics)> RunOnceAsync()
    {
        using var scope = NewProvider().CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<EventBookingDbContext>();
        var steps = services.GetRequiredService<ISweepSteps>();
        var runner = new SweepRunner(steps, _runnerLogger);
        var invites = services.GetRequiredService<IInviteRepository>();
        var proposals = services.GetRequiredService<IEventProposalRepository>();
        var bookings = services.GetRequiredService<IBookingRepository>();
        var appointments = services.GetRequiredService<IBookingAppointmentRepository>();
        var clock = services.GetRequiredService<IClock>();

        var started = false;
        try
        {
            (started, var metrics) = await runner.RunOnceAsync(
                token => AdvisoryLock.TryAcquireSweepLockAsync(context, token),
                async token => (await invites.ListPendingExpiredAsync(clock.UtcNow, token))
                    .Select(i => i.Id).ToList(),
                async token => (await proposals.ListOpenAsync(token))
                    .Select(p => p.Id).ToList(),
                token => ConcludingAsync(bookings, appointments, token),
                CancellationToken.None);
            return (started, metrics);
        }
        finally
        {
            if (started)
            {
                await AdvisoryLock.ReleaseSweepLockAsync(context, CancellationToken.None);
            }
        }
    }

    /// <summary>Attempts one sweep pass while another run may hold the lock.</summary>
    protected Task<(bool Started, SweepMetrics Metrics)> TryRunOnceAsync() => RunOnceAsync();

    /// <summary>Whether the runner logged a line containing the given text.</summary>
    /// <param name="text">The text to look for.</param>
    protected bool LogContains(string text) =>
        _runnerLogger.Lines.Any(line =>
            line.Contains(text, StringComparison.OrdinalIgnoreCase));

    /// <summary>Seeds one expired invite whose attendee is invited and out of retries.</summary>
    protected async Task<Guid> SeedExpiredInviteAsync()
    {
        await ResetOnceAsync();
        await using var seed = fixture.NewContext();
        var group = AttendeeGroup.Define(
            Guid.NewGuid(), $"SWEEP_{Guid.NewGuid():N}".ToUpperInvariant(), "Sweep", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Sweep", $"sweep-{Guid.NewGuid():N}@example.invalid", group,
            Now.AddDays(-30));
        attendee.MarkInvited(Now.AddDays(-30));
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            Now.AddMinutes(-1),
            [TransitionalLocation.Id],
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            retryCount: 0,
            maxAutoRetryCount: 0);
        seed.AttendeeGroups.Add(group);
        seed.Attendees.Add(attendee);
        seed.Invites.Add(invite);
        await seed.SaveChangesAsync();
        return invite.Id;
    }

    /// <summary>Seeds one expired invite with no attendee row, so the expiry step fails.</summary>
    protected async Task<Guid> SeedFailingItemAsync()
    {
        await ResetOnceAsync();
        await using var seed = fixture.NewContext();
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddMinutes(-1),
            [TransitionalLocation.Id],
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting],
            retryCount: 0,
            maxAutoRetryCount: 0);
        seed.Invites.Add(invite);
        await seed.SaveChangesAsync();
        return invite.Id;
    }

    /// <summary>Seeds one open proposal whose window started a minute ago in London.</summary>
    protected async Task<Guid> SeedOpenProposalStartingAMinuteAgoAsync()
    {
        await ResetOnceAsync();
        await using var seed = fixture.NewContext();
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 1, 15), new TimeOnly(11, 59), 60),
            Guid.NewGuid());
        seed.EventProposals.Add(proposal);
        await seed.SaveChangesAsync();
        return proposal.Id;
    }

    /// <summary>Seeds one active recovery booking with every appointment terminal.</summary>
    protected async Task<Guid> SeedTerminalRecoveryBookingAsync()
    {
        await ResetOnceAsync();
        await using var seed = fixture.NewContext();
        var group = AttendeeGroup.Define(
            Guid.NewGuid(), $"RECOVERY_{Guid.NewGuid():N}".ToUpperInvariant(), "Recovery", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Recovery", $"recovery-{Guid.NewGuid():N}@example.invalid", group,
            Now.AddDays(-30));
        attendee.MarkInvited(Now.AddDays(-30));
        var eventId = Guid.NewGuid();
        var originalInvite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            Now.AddDays(7),
            [TransitionalLocation.Id],
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            retryCount: 0);
        var original = Booking.Create(Guid.NewGuid(), originalInvite, eventId, Now.AddDays(-1));
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(),
            attendee.Id,
            original.Id,
            Now.AddDays(7),
            TransitionalLocation.Id,
            null,
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds);
        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, eventId, Now.AddHours(-1));
        originalInvite.MarkUsed();
        recoveryInvite.MarkUsed();
        var staffUserId = Guid.NewGuid();
        var completed = BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.DrugAndAlcoholTesting);
        completed.TransitionTo(
            BookingAppointmentStatus.CheckedIn, staffUserId, Now.AddMinutes(-30), true, false);
        completed.TransitionTo(
            BookingAppointmentStatus.Completed, staffUserId, Now.AddMinutes(-10), false, false);
        var noShow = BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.MedicalCheckUp);
        noShow.TransitionTo(
            BookingAppointmentStatus.NoShow, staffUserId, Now.AddMinutes(-10), false, true);
        seed.AttendeeGroups.Add(group);
        seed.Attendees.Add(attendee);
        seed.Invites.Add(originalInvite);
        seed.Invites.Add(recoveryInvite);
        seed.Bookings.Add(original);
        seed.Bookings.Add(recovery);
        seed.BookingAppointments.Add(completed);
        seed.BookingAppointments.Add(noShow);
        await seed.SaveChangesAsync();
        return recovery.Id;
    }

    /// <summary>Reads the audit rows for one entity and action name.</summary>
    /// <param name="entityId">The entity.</param>
    /// <param name="actionName">The action name.</param>
    protected IReadOnlyList<AuditLog> AuditFor(Guid entityId, string actionName)
    {
        using var context = fixture.NewContext();
        var action = Enum.Parse<AuditAction>(actionName);
        return context.AuditLogs.AsNoTracking()
            .Where(e => e.EntityId == entityId && e.Action == action)
            .ToList();
    }

    /// <summary>Reads one booking's status name on a fresh context.</summary>
    /// <param name="bookingId">The booking.</param>
    protected async Task<string> BookingStatusAsync(Guid bookingId)
    {
        await using var context = fixture.NewContext();
        var booking = await context.Bookings.AsNoTracking()
            .SingleAsync(b => b.Id == bookingId);
        return booking.Status.ToString();
    }

    private async Task ResetOnceAsync()
    {
        if (_reset)
        {
            return;
        }

        _reset = true;
        await fixture.ResetAsync();
    }

    private ServiceProvider NewProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBookingInfrastructure(
            fixture.ConnectionString,
            new TokenOptions("a-test-signing-key-that-is-long-enough-here"));
        services.AddEventBookingApplication(new AttendeePortalOptions(
            "https://portal.example.invalid", "coordinator@example.invalid"));
        services.RemoveAll<IClock>();
        services.AddSingleton<IClock>(Clock);
        var provider = services.BuildServiceProvider();
        _providers.Add(provider);
        return provider;
    }

    // Recovery bookings that are still active with every appointment terminal, mirroring
    // the hosted service's candidate listing so the runner pass reads the same rows.
    private static async Task<IReadOnlyList<Guid>> ConcludingAsync(
        IBookingRepository bookings,
        IBookingAppointmentRepository appointments,
        CancellationToken ct)
    {
        var ids = new List<Guid>();
        foreach (var booking in await bookings.ListActiveRecoveriesAsync(ct))
        {
            var rows = await appointments.ListForBookingAsync(booking.Id, ct);
            if (rows.Count > 0 && rows.All(a =>
                    a.Status is BookingAppointmentStatus.Completed or BookingAppointmentStatus.NoShow))
                ids.Add(booking.Id);
        }

        return ids;
    }
}
