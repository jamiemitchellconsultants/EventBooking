using EventBooking.Application.Jobs;

namespace EventBooking.Infrastructure.Tests.Jobs;

[Collection("postgres")]
public sealed class SweepServiceTests(PostgresFixture fixture) : PostgresSweepHarness(fixture)
{
    [Fact]
    public async Task Second_concurrent_run_skips()
    {
        await using var first = await AcquireRunAsync();
        var second = await TryRunOnceAsync();

        Assert.False(second.Started);
        Assert.Equal(new SweepMetrics(0, 0, 0, 0), second.Metrics);
    }

    [Fact]
    public async Task Failing_item_does_not_stop_run()
    {
        await SeedExpiredInviteAsync();
        await SeedFailingItemAsync();
        await SeedExpiredInviteAsync();

        var metrics = (await RunOnceAsync()).Metrics;

        Assert.Equal(2, metrics.ExpiredInvites);
        Assert.Equal(1, metrics.Failures);
        Assert.True(LogContains("sweep item failed"));
    }

    [Fact]
    public async Task Started_proposal_withdrawn_as_system_one_minute_past_start()
    {
        var proposal = await SeedOpenProposalStartingAMinuteAgoAsync();

        var metrics = (await RunOnceAsync()).Metrics;

        Assert.Equal(1, metrics.WithdrawnProposals);
        var entry = Assert.Single(AuditFor(proposal, "ProposalWithdrawn"));
        Assert.Equal("System", entry.ActorType.ToString());
    }

    [Fact]
    public async Task Terminal_recovery_booking_concludes()
    {
        var booking = await SeedTerminalRecoveryBookingAsync();

        var metrics = (await RunOnceAsync()).Metrics;

        Assert.Equal(1, metrics.ConcludedRecoveries);
        Assert.Equal("Concluded", await BookingStatusAsync(booking));
    }

    [Fact]
    public async Task Next_run_proceeds_after_failed_run()
    {
        await SeedFailingItemAsync();
        _ = await RunOnceAsync();

        var metrics = (await RunOnceAsync()).Metrics;

        Assert.Equal(1, metrics.Failures);
    }
}
