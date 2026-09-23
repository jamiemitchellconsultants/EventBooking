namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class CapacityAdjustmentConcurrencyTests(PostgresFixture fixture)
{
    [Fact]
    public async Task BookingWaitsForAnAdjustmentAndUsesTheNewCapacity()
    {
        await using var harness =
            await CapacityAdjustmentConcurrencyHarness.CreateAsync(fixture);
        var slotId = await harness.GivenSlotAsync(totalHeadcount: 1);
        await using var held = await harness.HoldAdjustmentAsync(slotId, totalHeadcount: 2);

        var booking = harness.BookAsync(slotId);
        await AssertStillWaiting(booking);

        await held.CommitAsync();
        await booking.WaitAsync(TimeSpan.FromSeconds(5));

        var capacity = await harness.ReadAsync(slotId);
        Assert.Equal(2, capacity.TotalHeadcount);
        Assert.Equal(1, capacity.RemainingCapacity);
        Assert.InRange(capacity.RemainingCapacity, 0, capacity.TotalHeadcount);
    }

    [Fact]
    public async Task AdjustmentWaitsForABookingAndPreservesItsHold()
    {
        await using var harness =
            await CapacityAdjustmentConcurrencyHarness.CreateAsync(fixture);
        var slotId = await harness.GivenSlotAsync(totalHeadcount: 2);
        await using var held = await harness.HoldBookingAsync(slotId);

        var adjustment = harness.AdjustAsync(slotId, totalHeadcount: 1);
        await AssertStillWaiting(adjustment);

        await held.CommitAsync();
        await adjustment.WaitAsync(TimeSpan.FromSeconds(5));

        var capacity = await harness.ReadAsync(slotId);
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
