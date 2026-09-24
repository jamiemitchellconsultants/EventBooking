using EventBooking.Application.Abstractions;
using EventBooking.Application.Jobs;
using EventBooking.Domain.Bookings;
using EventBooking.Infrastructure.Jobs;
using EventBooking.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventBooking.Api;

/// <summary>
/// Runs the invite sweep on an interval. Expiry cannot wait for a attendee to open a link — the
/// whole point is the attendees who never do. Each run executes three steps under an advisory
/// lock, each item in its own transaction: due-invite expiry, started-proposal withdrawal and
/// recovery-booking conclusion.
/// </summary>
/// <param name="scopes">The scope factory.</param>
/// <param name="configuration">The configuration.</param>
/// <param name="logger">The logger.</param>
public sealed class InviteSweepService(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<InviteSweepService> logger) : BackgroundService
{
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(15);

    /// <summary>Resolves the sweep interval from configuration, defaulting to 15 minutes.</summary>
    /// <param name="configuration">The configuration.</param>
    public static TimeSpan ResolveInterval(IConfiguration configuration) =>
        configuration.GetValue<TimeSpan?>("Jobs:SweepInterval") ?? DefaultInterval;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = ResolveInterval(configuration);
        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<SweepRunner>();
                var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
                var invites = scope.ServiceProvider.GetRequiredService<IInviteRepository>();
                var proposals = scope.ServiceProvider.GetRequiredService<IEventProposalRepository>();
                var bookings = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
                var appointments =
                    scope.ServiceProvider.GetRequiredService<IBookingAppointmentRepository>();
                var clock = scope.ServiceProvider.GetRequiredService<IClock>();

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
                        stoppingToken);

                    if (started)
                        logger.LogInformation(
                            "Sweep: {Expired} expired, {Withdrawn} withdrawn, {Concluded} concluded, {Failures} failed.",
                            metrics.ExpiredInvites, metrics.WithdrawnProposals,
                            metrics.ConcludedRecoveries, metrics.Failures);
                }
                finally
                {
                    if (started)
                    {
                        await AdvisoryLock.ReleaseSweepLockAsync(context, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "The invite sweep failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    // Recovery bookings that are still active with every appointment terminal. Read
    // without locks here; each item re-validates under its own lock in its transaction.
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
