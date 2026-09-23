namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class CapacityAdjustmentConcurrencyTests(PostgresFixture fixture)
{
    [Fact]
    public async Task BookingWaitsForAnAdjustmentAndUsesTheNewCapacity()
    {
        await using var harness =
            await CapacityAdjustmentConcurrencyHarness.CreateAsync(fixture);
        var eventId = await harness.GivenEventAsync(totalHeadcount: 1);
        await using var held = await harness.HoldAdjustmentAsync(eventId, totalHeadcount: 2);

        var booking = harness.BookAsync(eventId);
        await AssertStillWaiting(booking);

        await held.CommitAsync();
        await booking.WaitAsync(TimeSpan.FromSeconds(5));

        var capacity = await harness.ReadAsync(eventId);
        Assert.Equal(2, capacity.TotalHeadcount);
        Assert.Equal(1, capacity.RemainingCapacity);
        Assert.InRange(capacity.RemainingCapacity, 0, capacity.TotalHeadcount);
    }

    [Fact]
    public async Task AdjustmentWaitsForABookingAndPreservesItsHold()
    {
        await using var harness =
            await CapacityAdjustmentConcurrencyHarness.CreateAsync(fixture);
        var eventId = await harness.GivenEventAsync(totalHeadcount: 2);
        await using var held = await harness.HoldBookingAsync(eventId);

        var adjustment = harness.AdjustAsync(eventId, totalHeadcount: 1);
        await AssertStillWaiting(adjustment);

        await held.CommitAsync();
        await adjustment.WaitAsync(TimeSpan.FromSeconds(5));

        var capacity = await harness.ReadAsync(eventId);
        Assert.Equal(1, capacity.TotalHeadcount);
        Assert.Equal(0, capacity.RemainingCapacity);
        Assert.InRange(capacity.RemainingCapacity, 0, capacity.TotalHeadcount);
    }

    private static async Task AssertStillWaiting(Task operation)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(150));
        Assert.False(operation.IsCompleted);
    }
}
