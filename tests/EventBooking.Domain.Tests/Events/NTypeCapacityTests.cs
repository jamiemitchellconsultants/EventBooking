using EventBooking.Domain.Common;
using EventBooking.Domain.Events;
using EventBooking.Domain.Time;

namespace EventBooking.Domain.Tests.Events;

/// <summary>
/// Task 7: capacity is N rows, a booking charges only the types it requires, a headcount
/// adjustment is judged against the type's active-booking count, cancellation is refused once the
/// window has started, and the lock order is a pure function of the keys
/// (FR-3.1 to FR-3.6; design 01 — lock ordering and capacity).
/// </summary>
public class NTypeCapacityTests
{
    private static readonly Guid Medical = Guid.Parse("a0000001-0000-0000-0000-000000000001");
    private static readonly Guid Fitting = Guid.Parse("a0000002-0000-0000-0000-000000000002");
    private static readonly Guid Induction = Guid.Parse("a0000003-0000-0000-0000-000000000003");

    private static readonly Guid London = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Proposer = Guid.Parse("c0000001-0000-0000-0000-000000000001");

    private const string TimeZoneId = "Europe/London";
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly EventWindow Window = new(new DateOnly(2026, 10, 14), new TimeOnly(9, 30), 90);
    private static readonly AlwaysUniqueZones Zones = new();

    private static Event EventOf(params (Guid TypeId, int Headcount)[] listed)
    {
        var proposal = EventProposal.Propose(
            Guid.NewGuid(),
            London,
            locationIsActive: true,
            TimeZoneId,
            Window,
            Zones,
            Now,
            [.. listed.Select(l => new ProposableAppointmentType(l.TypeId, l.TypeId.ToString(), true, true))],
            listed[0].TypeId,
            Proposer,
            listed[0].Headcount);

        foreach (var (typeId, headcount) in listed.Skip(1))
        {
            proposal.Accept(typeId, Guid.NewGuid(), headcount);
        }

        return Event.CreateFrom(Guid.NewGuid(), proposal);
    }

    [Fact]
    public void ChargingOneRequiredTypeLeavesEveryOtherListedTypeUntouched()
    {
        var eventItem = EventOf((Medical, 4), (Fitting, 6), (Induction, 5));

        eventItem.ChargeRequiredTypes([Induction]);

        Assert.Equal(4, eventItem.CapacityFor(Medical).RemainingCapacity);
        Assert.Equal(6, eventItem.CapacityFor(Fitting).RemainingCapacity);
        Assert.Equal(4, eventItem.CapacityFor(Induction).RemainingCapacity);
    }

    [Fact]
    public void AChargeIsAllOrNothingAndNamesTheExhaustedType()
    {
        var eventItem = EventOf((Medical, 4), (Fitting, 6), (Induction, 1));
        eventItem.ChargeRequiredTypes([Induction]);

        var exception = Assert.Throws<DomainException>(
            () => eventItem.ChargeRequiredTypes([Medical, Induction]));

        Assert.Contains("capacity-exhausted", exception.Message, StringComparison.Ordinal);
        Assert.Contains(Induction.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Equal(4, eventItem.CapacityFor(Medical).RemainingCapacity);
        Assert.Equal(0, eventItem.CapacityFor(Induction).RemainingCapacity);
    }

    [Fact]
    public void ChargingATypeTheEventDoesNotListIsRefused()
    {
        var eventItem = EventOf((Medical, 4), (Induction, 5));

        var exception = Assert.Throws<DomainException>(
            () => eventItem.ChargeRequiredTypes([Medical, Fitting]));

        Assert.Contains(Fitting.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Equal(4, eventItem.CapacityFor(Medical).RemainingCapacity);
    }

    [Fact]
    public void AChargeIsRefusedOnceTheEventIsCancelled()
    {
        var eventItem = EventOf((Medical, 4), (Induction, 5));
        eventItem.Cancel(Zones, TimeZoneId, Now);

        Assert.Throws<DomainException>(() => eventItem.ChargeRequiredTypes([Medical]));
        Assert.Equal(4, eventItem.CapacityFor(Medical).RemainingCapacity);
    }

    [Fact]
    public void ReleaseRestoresExactlyTheChargedTypes()
    {
        var eventItem = EventOf((Medical, 4), (Fitting, 6), (Induction, 5));
        eventItem.ChargeRequiredTypes([Medical, Induction]);

        eventItem.ReleaseTypes([Medical, Induction]);

        Assert.Equal(4, eventItem.CapacityFor(Medical).RemainingCapacity);
        Assert.Equal(6, eventItem.CapacityFor(Fitting).RemainingCapacity);
        Assert.Equal(5, eventItem.CapacityFor(Induction).RemainingCapacity);
    }

    [Fact]
    public void ReleasingMoreThanWasChargedIsRefusedWithoutMutation()
    {
        var eventItem = EventOf((Medical, 4), (Induction, 5));
        eventItem.ChargeRequiredTypes([Medical]);

        Assert.Throws<DomainException>(() => eventItem.ReleaseTypes([Medical, Induction]));

        Assert.Equal(3, eventItem.CapacityFor(Medical).RemainingCapacity);
        Assert.Equal(5, eventItem.CapacityFor(Induction).RemainingCapacity);
    }

    [Fact]
    public void ReducingTheTotalAppliesTheSameDeltaToRemaining()
    {
        var capacity = EventOf((Medical, 6)).CapacityFor(Medical);

        var adjustment = capacity.AdjustTotalHeadcount(4, activeBookingCount: 3);

        Assert.Equal(CapacityAdjustmentStatus.Adjusted, adjustment.Status);
        Assert.True(adjustment.Changed);
        Assert.Equal(4, capacity.TotalHeadcount);
        Assert.Equal(4, capacity.RemainingCapacity);
        Assert.Equal(4, adjustment.TotalHeadcount);
        Assert.Equal(4, adjustment.RemainingCapacity);
    }

    [Fact]
    public void ATotalBelowTheActiveBookingCountIsRefusedAndReturnsTheMinimum()
    {
        var capacity = EventOf((Medical, 6)).CapacityFor(Medical);

        var adjustment = capacity.AdjustTotalHeadcount(2, activeBookingCount: 3);

        Assert.Equal(CapacityAdjustmentStatus.BelowActiveBookings, adjustment.Status);
        Assert.False(adjustment.Changed);
        Assert.Equal(3, adjustment.MinimumTotalHeadcount);
        Assert.Equal(6, adjustment.TotalHeadcount);
        Assert.Equal(6, adjustment.RemainingCapacity);
        Assert.Equal(6, capacity.TotalHeadcount);
        Assert.Equal(6, capacity.RemainingCapacity);
    }

    [Fact]
    public void ResubmittingTheCurrentTotalReportsNoChange()
    {
        var capacity = EventOf((Medical, 6)).CapacityFor(Medical);

        var adjustment = capacity.AdjustTotalHeadcount(6, activeBookingCount: 3);

        Assert.Equal(CapacityAdjustmentStatus.Unchanged, adjustment.Status);
        Assert.False(adjustment.Changed);
        Assert.Equal(6, capacity.TotalHeadcount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void ATotalOutsideTheAllowedRangeIsRefused(int totalHeadcount)
    {
        var capacity = EventOf((Medical, 6)).CapacityFor(Medical);

        Assert.Throws<DomainException>(
            () => capacity.AdjustTotalHeadcount(totalHeadcount, activeBookingCount: 0));

        Assert.Equal(6, capacity.TotalHeadcount);
        Assert.Equal(6, capacity.RemainingCapacity);
    }

    [Fact]
    public void CancellationIsRefusedOnceTheWindowHasStarted()
    {
        var eventItem = EventOf((Medical, 4));
        var started = Window.StartInstant(Zones, TimeZoneId);

        var exception = Assert.Throws<DomainException>(
            () => eventItem.Cancel(Zones, TimeZoneId, started));

        Assert.Equal("An event whose window has started cannot be cancelled.", exception.Message);
        Assert.Equal(EventStatus.Active, eventItem.Status);
    }

    [Fact]
    public void TheLockOrderSortsByEventThenAppointmentType()
    {
        var firstEvent = Guid.Parse("e0000001-0000-0000-0000-000000000001");
        var secondEvent = Guid.Parse("e0000002-0000-0000-0000-000000000002");

        var ordered = Event.CapacityLockOrder(
        [
            new(secondEvent, Fitting),
            new(firstEvent, Induction),
            new(secondEvent, Medical),
            new(firstEvent, Medical),
            new(firstEvent, Induction),
        ]);

        Assert.Equal(
            [
                new EventCapacityKey(firstEvent, Medical),
                new EventCapacityKey(firstEvent, Induction),
                new EventCapacityKey(secondEvent, Medical),
                new EventCapacityKey(secondEvent, Fitting),
            ],
            ordered);
    }

    private sealed class AlwaysUniqueZones : IEventWindowZones
    {
        public bool IsKnownZone(string timeZoneId) => true;

        public LocalTimeValidity ValidityOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            LocalTimeValidity.Unique;

        public DateTimeOffset InstantOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            new(date.ToDateTime(time), TimeSpan.Zero);

        public DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId) =>
            DateOnly.FromDateTime(instant.UtcDateTime);

        public string AbbreviationOf(DateTimeOffset instant, string timeZoneId) => "BST";
    }
}
