using EventBooking.Application.Abstractions;
using EventBooking.Application.Invites;
using EventBooking.Domain.Attendees;

namespace EventBooking.Api;

/// <summary>How one sweep run resolved its due invites.</summary>
/// <param name="Expired">How many due invites were processed.</param>
/// <param name="ReIssued">How many attendees hold a fresh invite afterwards.</param>
/// <param name="FlaggedForFollowUp">How many attendees need follow-up afterwards.</param>
public sealed record InviteSweepSummary(int Expired, int ReIssued, int FlaggedForFollowUp);

/// <summary>
/// Runs the invite expiry sweep hourly. Expiry cannot wait for a attendee to open a link — the
/// whole point is the attendees who never do. Each due invite is expired on its own row lock;
/// Task 19 replaces this loop with advisory-locked claiming.
/// </summary>
public sealed class InviteSweepService(
    IServiceScopeFactory scopes,
    ILogger<InviteSweepService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var summary = await SweepOnceAsync(scope.ServiceProvider, stoppingToken);

                if (summary.Expired > 0)
                {
                    logger.LogInformation(
                        "Invite sweep: {Expired} expired, {ReIssued} re-issued, {Flagged} flagged.",
                        summary.Expired,
                        summary.ReIssued,
                        summary.FlaggedForFollowUp);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // One bad sweep must not stop every later sweep.
                logger.LogError(ex, "The invite sweep failed.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }
        }
    }

    private static async Task<InviteSweepSummary> SweepOnceAsync(
        IServiceProvider services, CancellationToken ct)
    {
        var invites = services.GetRequiredService<IInviteRepository>();
        var attendees = services.GetRequiredService<IAttendeeRepository>();
        var handler = services.GetRequiredService<ExpireInviteHandler>();
        var clock = services.GetRequiredService<IClock>();

        var due = await invites.ListPendingExpiredAsync(clock.UtcNow, ct);
        var expired = 0;
        var reissued = 0;
        var flagged = 0;

        foreach (var item in due)
        {
            var result = await handler.HandleAsync(new ExpireInviteCommand(item.Id), ct);
            if (result.IsFailure)
            {
                continue;
            }

            expired++;
            var attendee = await attendees.GetAsync(item.AttendeeId, ct);
            if (attendee?.Status == AttendeeStatus.Invited)
            {
                reissued++;
            }
            else if (attendee?.Status == AttendeeStatus.NoResponseNeedsFollowUp)
            {
                flagged++;
            }
        }

        return new InviteSweepSummary(expired, reissued, flagged);
    }
}
