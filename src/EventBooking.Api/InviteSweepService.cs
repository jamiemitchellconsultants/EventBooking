using EventBooking.Application.Invites;

namespace EventBooking.Api;

/// <summary>
/// Runs the invite expiry sweep hourly. Expiry cannot wait for a candidate to open a link — the
/// whole point is the candidates who never do.
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
                var handler = scope.ServiceProvider.GetRequiredService<ExpireInvitesHandler>();

                var summary = await handler.HandleAsync(stoppingToken);

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
}
