# 01d — Capacity generalised to N rows, edits 7 (Task 7)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — tests/EventBooking.Domain.Tests/Events/EventCapacityHeadcountAdjustmentTests.cs — 1/1

<!-- retirement-file: {"id":18,"file":"tests/EventBooking.Domain.Tests/Events/EventCapacityHeadcountAdjustmentTests.cs","beforeSha":"1c1d57a49c01945271da427d0b6c72812b9025c81beba79c8d302b324b45ddbd","afterSha":"745154149b0476c28ae3894836575db1c50772dee7f17b9b8106db26c72fde0e","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventCapacityHeadcountAdjustmentTests
{
    private static EventCapacity CapacityOf(int totalHeadcount, int occupied = 0)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            Guid.NewGuid());
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), totalHeadcount);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 20);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 20);

        var capacity = Event
            .CreateFrom(Guid.NewGuid(), proposal)
            .CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);

        for (var index = 0; index < occupied; index++)
        {
            capacity.Decrement();
        }

        return capacity;
    }

    [Fact]
    public void IncreasingTheTotalIncreasesRemainingByTheSameDelta()
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 6);

        var changed = capacity.AdjustTotalHeadcount(12);

        Assert.True(changed);
        Assert.Equal(12, capacity.TotalHeadcount);
        Assert.Equal(6, capacity.RemainingCapacity);
        Assert.Equal(6, capacity.OccupiedCapacity);
    }

    [Fact]
    public void DecreasingTheTotalDecreasesRemainingByTheSameDelta()
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 6);

        var changed = capacity.AdjustTotalHeadcount(8);

        Assert.True(changed);
        Assert.Equal(8, capacity.TotalHeadcount);
        Assert.Equal(2, capacity.RemainingCapacity);
        Assert.Equal(6, capacity.OccupiedCapacity);
    }

    [Fact]
    public void TheTotalMayEqualOccupiedCapacity()
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 6);

        capacity.AdjustTotalHeadcount(6);

        Assert.Equal(6, capacity.TotalHeadcount);
        Assert.Equal(0, capacity.RemainingCapacity);
        Assert.False(capacity.HasSpare);
    }

    [Fact]
    public void ATotalBelowOccupiedCapacityIsRejectedWithoutMutation()
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 6);

        var exception = Assert.Throws<DomainException>(
            () => capacity.AdjustTotalHeadcount(5));

        Assert.Equal(
            "totalHeadcount cannot be lower than occupied capacity.",
            exception.Message);
        Assert.Equal(10, capacity.TotalHeadcount);
        Assert.Equal(4, capacity.RemainingCapacity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ATotalMustRemainPositive(int totalHeadcount)
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 2);

        var exception = Assert.Throws<DomainException>(
            () => capacity.AdjustTotalHeadcount(totalHeadcount));

        Assert.Equal("totalHeadcount must be greater than zero.", exception.Message);
        Assert.Equal(10, capacity.TotalHeadcount);
        Assert.Equal(8, capacity.RemainingCapacity);
    }

    [Fact]
    public void ResubmittingTheCurrentTotalReportsNoChange()
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 6);

        var changed = capacity.AdjustTotalHeadcount(10);

        Assert.False(changed);
        Assert.Equal(10, capacity.TotalHeadcount);
        Assert.Equal(4, capacity.RemainingCapacity);
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Events/EventCapacityHeadcountAdjustmentTests.cs — 1/1

<!-- retirement-file: {"id":18,"file":"tests/EventBooking.Domain.Tests/Events/EventCapacityHeadcountAdjustmentTests.cs","beforeSha":"1c1d57a49c01945271da427d0b6c72812b9025c81beba79c8d302b324b45ddbd","afterSha":"745154149b0476c28ae3894836575db1c50772dee7f17b9b8106db26c72fde0e","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventCapacityHeadcountAdjustmentTests
{
    private static EventCapacity CapacityOf(int totalHeadcount, int occupied = 0)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            Guid.NewGuid());
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), totalHeadcount);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 20);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 20);

        var capacity = Event
            .CreateFrom(Guid.NewGuid(), proposal)
            .CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);

        for (var index = 0; index < occupied; index++)
        {
            capacity.Decrement();
        }

        return capacity;
    }

    [Fact]
    public void IncreasingTheTotalIncreasesRemainingByTheSameDelta()
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 6);

        var adjustment = capacity.AdjustTotalHeadcount(12, activeBookingCount: 6);

        Assert.Equal(CapacityAdjustmentStatus.Adjusted, adjustment.Status);
        Assert.Equal(12, capacity.TotalHeadcount);
        Assert.Equal(6, capacity.RemainingCapacity);
        Assert.Equal(6, capacity.OccupiedCapacity);
    }

    [Fact]
    public void DecreasingTheTotalDecreasesRemainingByTheSameDelta()
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 6);

        var adjustment = capacity.AdjustTotalHeadcount(8, activeBookingCount: 6);

        Assert.Equal(CapacityAdjustmentStatus.Adjusted, adjustment.Status);
        Assert.Equal(8, capacity.TotalHeadcount);
        Assert.Equal(2, capacity.RemainingCapacity);
        Assert.Equal(6, capacity.OccupiedCapacity);
    }

    [Fact]
    public void TheTotalMayEqualOccupiedCapacity()
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 6);

        capacity.AdjustTotalHeadcount(6, activeBookingCount: 6);

        Assert.Equal(6, capacity.TotalHeadcount);
        Assert.Equal(0, capacity.RemainingCapacity);
        Assert.False(capacity.HasSpare);
    }

    [Fact]
    public void ATotalBelowTheActiveBookingCountIsRejectedWithoutMutation()
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 6);

        var adjustment = capacity.AdjustTotalHeadcount(5, activeBookingCount: 6);

        Assert.Equal(CapacityAdjustmentStatus.BelowActiveBookings, adjustment.Status);
        Assert.Equal(6, adjustment.MinimumTotalHeadcount);
        Assert.Equal(10, capacity.TotalHeadcount);
        Assert.Equal(4, capacity.RemainingCapacity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ATotalMustRemainPositive(int totalHeadcount)
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 2);

        var exception = Assert.Throws<DomainException>(
            () => capacity.AdjustTotalHeadcount(totalHeadcount, activeBookingCount: 2));

        Assert.Equal("totalHeadcount must be greater than zero.", exception.Message);
        Assert.Equal(10, capacity.TotalHeadcount);
        Assert.Equal(8, capacity.RemainingCapacity);
    }

    [Fact]
    public void ResubmittingTheCurrentTotalReportsNoChange()
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 6);

        var adjustment = capacity.AdjustTotalHeadcount(10, activeBookingCount: 6);

        Assert.Equal(CapacityAdjustmentStatus.Unchanged, adjustment.Status);
        Assert.Equal(10, capacity.TotalHeadcount);
        Assert.Equal(4, capacity.RemainingCapacity);
    }
}
`````

## before — tests/EventBooking.Domain.Tests/Events/EventCapacityTests.cs — 1/1

<!-- retirement-file: {"id":19,"file":"tests/EventBooking.Domain.Tests/Events/EventCapacityTests.cs","beforeSha":"5fabb400a6cbce6815afc21786a75fc2f6c2ca260021bb1fdff66fec76cedf1e","afterSha":"a98782b48d779b1e60d5e19d278f3582e539b96ddce616105138b0d1f873b6c9","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventCapacityTests
{
    private static EventCapacity CapacityOf(int headcount)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), headcount);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), headcount);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), headcount);

        return Event
            .CreateFrom(Guid.NewGuid(), proposal)
            .CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);
    }

    [Fact]
    public void DecrementReducesTheRemainingCountByOne()
    {
        var capacity = CapacityOf(3);

        capacity.Decrement();

        Assert.Equal(2, capacity.RemainingCapacity);
        Assert.Equal(3, capacity.TotalHeadcount);
        Assert.True(capacity.HasSpare);
    }

    [Fact]
    public void DecrementingToZeroLeavesNoSpare()
    {
        var capacity = CapacityOf(1);

        capacity.Decrement();

        Assert.Equal(0, capacity.RemainingCapacity);
        Assert.False(capacity.HasSpare);
    }

    [Fact]
    public void DecrementingPastZeroIsRejected()
    {
        var capacity = CapacityOf(1);
        capacity.Decrement();

        var ex = Assert.Throws<DomainException>(() => capacity.Decrement());
        Assert.Equal("No remaining capacity for this appointment type on this eventItem.", ex.Message);
        Assert.Equal(0, capacity.RemainingCapacity);
    }

    [Fact]
    public void IncrementGivesTheHeadcountBack()
    {
        var capacity = CapacityOf(2);
        capacity.Decrement();

        capacity.Increment();

        Assert.Equal(2, capacity.RemainingCapacity);
    }

    [Fact]
    public void IncrementingAboveTheAcceptedHeadcountIsRejected()
    {
        var capacity = CapacityOf(2);

        var ex = Assert.Throws<DomainException>(() => capacity.Increment());
        Assert.Equal("Remaining capacity cannot exceed the headcount the manager accepted.", ex.Message);
        Assert.Equal(2, capacity.RemainingCapacity);
    }

    [Fact]
    public void RemainingCapacityStaysWithinBoundsAcrossManyOperations()
    {
        var capacity = CapacityOf(5);

        for (var i = 0; i < 5; i++)
        {
            capacity.Decrement();
            Assert.InRange(capacity.RemainingCapacity, 0, 5);
        }

        for (var i = 0; i < 5; i++)
        {
            capacity.Increment();
            Assert.InRange(capacity.RemainingCapacity, 0, 5);
        }

        Assert.Equal(5, capacity.RemainingCapacity);
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Events/EventCapacityTests.cs — 1/1

<!-- retirement-file: {"id":19,"file":"tests/EventBooking.Domain.Tests/Events/EventCapacityTests.cs","beforeSha":"5fabb400a6cbce6815afc21786a75fc2f6c2ca260021bb1fdff66fec76cedf1e","afterSha":"a98782b48d779b1e60d5e19d278f3582e539b96ddce616105138b0d1f873b6c9","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventCapacityTests
{
    private static EventCapacity CapacityOf(int headcount)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), headcount);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), headcount);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), headcount);

        return Event
            .CreateFrom(Guid.NewGuid(), proposal)
            .CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);
    }

    [Fact]
    public void DecrementReducesTheRemainingCountByOne()
    {
        var capacity = CapacityOf(3);

        capacity.Decrement();

        Assert.Equal(2, capacity.RemainingCapacity);
        Assert.Equal(3, capacity.TotalHeadcount);
        Assert.True(capacity.HasSpare);
    }

    [Fact]
    public void DecrementingToZeroLeavesNoSpare()
    {
        var capacity = CapacityOf(1);

        capacity.Decrement();

        Assert.Equal(0, capacity.RemainingCapacity);
        Assert.False(capacity.HasSpare);
    }

    [Fact]
    public void DecrementingPastZeroIsRejected()
    {
        var capacity = CapacityOf(1);
        capacity.Decrement();

        var ex = Assert.Throws<DomainException>(() => capacity.Decrement());
        Assert.Equal(
            "capacity-exhausted: appointment type "
            + $"{AppointmentTypeIds.DrugAndAlcoholTesting} has no remaining capacity on this event.",
            ex.Message);
        Assert.Equal(0, capacity.RemainingCapacity);
    }

    [Fact]
    public void IncrementGivesTheHeadcountBack()
    {
        var capacity = CapacityOf(2);
        capacity.Decrement();

        capacity.Increment();

        Assert.Equal(2, capacity.RemainingCapacity);
    }

    [Fact]
    public void IncrementingAboveTheAcceptedHeadcountIsRejected()
    {
        var capacity = CapacityOf(2);

        var ex = Assert.Throws<DomainException>(() => capacity.Increment());
        Assert.Equal("Remaining capacity cannot exceed the headcount the manager accepted.", ex.Message);
        Assert.Equal(2, capacity.RemainingCapacity);
    }

    [Fact]
    public void RemainingCapacityStaysWithinBoundsAcrossManyOperations()
    {
        var capacity = CapacityOf(5);

        for (var i = 0; i < 5; i++)
        {
            capacity.Decrement();
            Assert.InRange(capacity.RemainingCapacity, 0, 5);
        }

        for (var i = 0; i < 5; i++)
        {
            capacity.Increment();
            Assert.InRange(capacity.RemainingCapacity, 0, 5);
        }

        Assert.Equal(5, capacity.RemainingCapacity);
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Events/NTypeCapacityTests.cs — 1/1

<!-- retirement-file: {"id":20,"file":"tests/EventBooking.Domain.Tests/Events/NTypeCapacityTests.cs","beforeSha":null,"afterSha":"18d3b448a764b9ac5b61f60e2490355b33257331a59d7d59c81395c39c87775b","side":"after","part":1,"parts":1} -->

`````csharp
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
`````

## before — tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs — 1/1

<!-- retirement-file: {"id":21,"file":"tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs","beforeSha":"7686bea6bfe312fa1cd31e1fbf34aeeb59db18d74c41645ef9e0b5afea137e52","afterSha":"3d866b1e9e576f0946a7f262be777bb5996b2f1586838572359adccbdb66a825","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies workspace projections are scoped and contain only operational data.</summary>
[Collection("postgres")]
public sealed class AppointmentWorkspaceQueryTests(PostgresFixture fixture)
{
    /// <summary>Verifies event counts retain recent-past rows and exclude older, cancelled, inactive-booking, and other-type rows.</summary>
    [Fact]
    public async Task EventListContainsOnlyRetainedActiveScopedAppointments()
    {
        await fixture.ResetAsync();
        await using (var write = fixture.NewContext())
        {
            var current = AddEvent(write, new DateOnly(2026, 9, 7), cancelled: false);
            var future = AddEvent(write, new DateOnly(2026, 9, 8), cancelled: false);
            var recentPast = AddEvent(write, new DateOnly(2026, 9, 6), cancelled: false);
            var tooOld = AddEvent(write, new DateOnly(2026, 8, 30), cancelled: false);
            var cancelled = AddEvent(write, new DateOnly(2026, 9, 9), cancelled: true);
            AddBooking(write, current, "Alex Morgan", "alex@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.CheckedIn, false);
            AddBooking(write, current, "Priya Shah", "priya@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, current, "Other Type", "other@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            AddBooking(write, future, "Future Attendee", "future@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, recentPast, "Past Attendee", "past@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, tooOld, "Too Old", "old@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, cancelled, "Cancelled Event", "event@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, current, "Cancelled Booking", "booking@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, true);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var result = await new AppointmentWorkspaceQueries(read, new TestClock()).ListEventsAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            new DateOnly(2026, 9, 7),
            CancellationToken.None);

        Assert.Equal("Drug & Alcohol Testing", result.AppointmentTypeName);
        Assert.Equal(3, result.Events.Count);
        Assert.Equal(new DateOnly(2026, 9, 6), result.Events[0].Date);
        Assert.Equal(new DateOnly(2026, 9, 7), result.Events[1].Date);
        Assert.Equal(1, result.Events[1].Counts.Expected);
        Assert.Equal(1, result.Events[1].Counts.CheckedIn);
        Assert.Equal(0, result.Events[1].Counts.Completed);
        Assert.Equal(0, result.Events[1].Counts.NoShow);
        Assert.Equal(new DateOnly(2026, 9, 8), result.Events[2].Date);
    }

    /// <summary>Verifies event detail returns only same-type active rows ordered for staff use.</summary>
    [Fact]
    public async Task SelectedEventReturnsOnlyMinimumScopedAttendeeRows()
    {
        await fixture.ResetAsync();
        Guid eventId;
        await using (var write = fixture.NewContext())
        {
            var eventItem = AddEvent(write, new DateOnly(2026, 9, 7), cancelled: false);
            eventId = eventItem.Id;
            AddBooking(write, eventItem, "Zara Young", "zara@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Completed, false);
            AddBooking(write, eventItem, "Alex Morgan", "alex@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, eventItem, "Medical Attendee", "medical@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var detail = await new AppointmentWorkspaceQueries(read, new TestClock()).GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            eventId,
            CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal("Drug & Alcohol Testing", detail!.AppointmentTypeName);
        Assert.Equal(eventId, detail.EventId);
        Assert.Collection(
            detail.Appointments,
            row =>
            {
                Assert.Equal("Alex Morgan", row.AttendeeName);
                Assert.Equal("alex@example.com", row.AttendeeEmail);
                Assert.Equal(BookingAppointmentStatus.Expected, row.Status);
            },
            row =>
            {
                Assert.Equal("Zara Young", row.AttendeeName);
                Assert.Equal(BookingAppointmentStatus.Completed, row.Status);
                Assert.NotNull(row.CheckedInAt);
                Assert.NotNull(row.OutcomeAt);
            });
    }

    /// <summary>Verifies a event that has no row in trusted scope is indistinguishable from missing.</summary>
    [Fact]
    public async Task SelectedEventOutsideTrustedScopeIsNotFound()
    {
        await fixture.ResetAsync();
        Guid eventId;
        await using (var write = fixture.NewContext())
        {
            var eventItem = AddEvent(write, new DateOnly(2026, 9, 7), cancelled: false);
            eventId = eventItem.Id;
            AddBooking(write, eventItem, "Medical Attendee", "medical@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        Assert.Null(await new AppointmentWorkspaceQueries(read, new TestClock()).GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            eventId,
            CancellationToken.None));
    }

    private static Event AddEvent(
        EventBookingDbContext context,
        DateOnly date,
        bool cancelled)
    {
        var eventItem = EventFixture.Create(
            Guid.NewGuid(),
            new EventWindow(date, new TimeOnly(9, 0), 240),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 20));
        if (cancelled)
        {
            eventItem.Cancel();
        }

        context.Events.Add(eventItem);
        return eventItem;
    }

    private static void AddBooking(
        EventBookingDbContext context,
        Event eventItem,
        string name,
        string email,
        Guid appointmentTypeId,
        BookingAppointmentStatus status,
        bool cancelled)
    {
        var group = AttendeeGroup.Define(
            Guid.NewGuid(), $"WORKSPACE_{Guid.NewGuid():N}".ToUpperInvariant(), "Workspace", true,
            [appointmentTypeId]);
        context.AttendeeGroups.Add(group);
        var attendee = Attendee.Create(Guid.NewGuid(), name, email, group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            $"invite-{attendee.Id}",
            DateTimeOffset.UtcNow.AddDays(1),
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, $"manage-{attendee.Id}", DateTimeOffset.UtcNow);
        if (cancelled)
        {
            booking.Cancel();
        }

        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, appointmentTypeId);
        var staff = Guid.NewGuid();
        var checkIn = new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero);
        if (status is BookingAppointmentStatus.CheckedIn or BookingAppointmentStatus.Completed)
        {
            appointment.TransitionTo(
                BookingAppointmentStatus.CheckedIn, staff, checkIn, true, false);
        }
        if (status == BookingAppointmentStatus.Completed)
        {
            appointment.TransitionTo(
                BookingAppointmentStatus.Completed, staff, checkIn.AddHours(1), false, false);
        }
        if (status == BookingAppointmentStatus.NoShow)
        {
            appointment.TransitionTo(
                BookingAppointmentStatus.NoShow, staff, checkIn.AddHours(4), false, true);
        }

        context.Attendees.Add(attendee);
        context.Bookings.Add(booking);
        context.BookingAppointments.Add(appointment);
    }

    /// <summary>A fixed clock pinning transitional-location today to 2026-09-07 for workspace tests.</summary>
    private sealed class TestClock : IClock
    {
        /// <inheritdoc/>
        public DateTimeOffset UtcNow => new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

        /// <inheritdoc/>
        public DateTimeOffset NowAtTransitionalLocation => UtcNow;

        /// <inheritdoc/>
        public DateOnly TodayAtTransitionalLocation => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => instant;
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs — 1/1

<!-- retirement-file: {"id":21,"file":"tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs","beforeSha":"7686bea6bfe312fa1cd31e1fbf34aeeb59db18d74c41645ef9e0b5afea137e52","afterSha":"3d866b1e9e576f0946a7f262be777bb5996b2f1586838572359adccbdb66a825","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies workspace projections are scoped and contain only operational data.</summary>
[Collection("postgres")]
public sealed class AppointmentWorkspaceQueryTests(PostgresFixture fixture)
{
    /// <summary>Verifies event counts retain recent-past rows and exclude older, cancelled, inactive-booking, and other-type rows.</summary>
    [Fact]
    public async Task EventListContainsOnlyRetainedActiveScopedAppointments()
    {
        await fixture.ResetAsync();
        await using (var write = fixture.NewContext())
        {
            var current = AddEvent(write, new DateOnly(2026, 9, 7), cancelled: false);
            var future = AddEvent(write, new DateOnly(2026, 9, 8), cancelled: false);
            var recentPast = AddEvent(write, new DateOnly(2026, 9, 6), cancelled: false);
            var tooOld = AddEvent(write, new DateOnly(2026, 8, 30), cancelled: false);
            var cancelled = AddEvent(write, new DateOnly(2026, 9, 9), cancelled: true);
            AddBooking(write, current, "Alex Morgan", "alex@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.CheckedIn, false);
            AddBooking(write, current, "Priya Shah", "priya@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, current, "Other Type", "other@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            AddBooking(write, future, "Future Attendee", "future@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, recentPast, "Past Attendee", "past@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, tooOld, "Too Old", "old@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, cancelled, "Cancelled Event", "event@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, current, "Cancelled Booking", "booking@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, true);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var result = await new AppointmentWorkspaceQueries(read, new TestClock()).ListEventsAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            new DateOnly(2026, 9, 7),
            CancellationToken.None);

        Assert.Equal("Drug & Alcohol Testing", result.AppointmentTypeName);
        Assert.Equal(3, result.Events.Count);
        Assert.Equal(new DateOnly(2026, 9, 6), result.Events[0].Date);
        Assert.Equal(new DateOnly(2026, 9, 7), result.Events[1].Date);
        Assert.Equal(1, result.Events[1].Counts.Expected);
        Assert.Equal(1, result.Events[1].Counts.CheckedIn);
        Assert.Equal(0, result.Events[1].Counts.Completed);
        Assert.Equal(0, result.Events[1].Counts.NoShow);
        Assert.Equal(new DateOnly(2026, 9, 8), result.Events[2].Date);
    }

    /// <summary>Verifies event detail returns only same-type active rows ordered for staff use.</summary>
    [Fact]
    public async Task SelectedEventReturnsOnlyMinimumScopedAttendeeRows()
    {
        await fixture.ResetAsync();
        Guid eventId;
        await using (var write = fixture.NewContext())
        {
            var eventItem = AddEvent(write, new DateOnly(2026, 9, 7), cancelled: false);
            eventId = eventItem.Id;
            AddBooking(write, eventItem, "Zara Young", "zara@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Completed, false);
            AddBooking(write, eventItem, "Alex Morgan", "alex@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, eventItem, "Medical Attendee", "medical@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var detail = await new AppointmentWorkspaceQueries(read, new TestClock()).GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            eventId,
            CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal("Drug & Alcohol Testing", detail!.AppointmentTypeName);
        Assert.Equal(eventId, detail.EventId);
        Assert.Collection(
            detail.Appointments,
            row =>
            {
                Assert.Equal("Alex Morgan", row.AttendeeName);
                Assert.Equal("alex@example.com", row.AttendeeEmail);
                Assert.Equal(BookingAppointmentStatus.Expected, row.Status);
            },
            row =>
            {
                Assert.Equal("Zara Young", row.AttendeeName);
                Assert.Equal(BookingAppointmentStatus.Completed, row.Status);
                Assert.NotNull(row.CheckedInAt);
                Assert.NotNull(row.OutcomeAt);
            });
    }

    /// <summary>Verifies a event that has no row in trusted scope is indistinguishable from missing.</summary>
    [Fact]
    public async Task SelectedEventOutsideTrustedScopeIsNotFound()
    {
        await fixture.ResetAsync();
        Guid eventId;
        await using (var write = fixture.NewContext())
        {
            var eventItem = AddEvent(write, new DateOnly(2026, 9, 7), cancelled: false);
            eventId = eventItem.Id;
            AddBooking(write, eventItem, "Medical Attendee", "medical@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        Assert.Null(await new AppointmentWorkspaceQueries(read, new TestClock()).GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            eventId,
            CancellationToken.None));
    }

    private static Event AddEvent(
        EventBookingDbContext context,
        DateOnly date,
        bool cancelled)
    {
        var eventItem = EventFixture.Create(
            Guid.NewGuid(),
            new EventWindow(date, new TimeOnly(9, 0), 240),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 20));
        if (cancelled)
        {
            eventItem.CancelBeforeStart();
        }

        context.Events.Add(eventItem);
        return eventItem;
    }

    private static void AddBooking(
        EventBookingDbContext context,
        Event eventItem,
        string name,
        string email,
        Guid appointmentTypeId,
        BookingAppointmentStatus status,
        bool cancelled)
    {
        var group = AttendeeGroup.Define(
            Guid.NewGuid(), $"WORKSPACE_{Guid.NewGuid():N}".ToUpperInvariant(), "Workspace", true,
            [appointmentTypeId]);
        context.AttendeeGroups.Add(group);
        var attendee = Attendee.Create(Guid.NewGuid(), name, email, group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            $"invite-{attendee.Id}",
            DateTimeOffset.UtcNow.AddDays(1),
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, $"manage-{attendee.Id}", DateTimeOffset.UtcNow);
        if (cancelled)
        {
            booking.Cancel();
        }

        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, appointmentTypeId);
        var staff = Guid.NewGuid();
        var checkIn = new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero);
        if (status is BookingAppointmentStatus.CheckedIn or BookingAppointmentStatus.Completed)
        {
            appointment.TransitionTo(
                BookingAppointmentStatus.CheckedIn, staff, checkIn, true, false);
        }
        if (status == BookingAppointmentStatus.Completed)
        {
            appointment.TransitionTo(
                BookingAppointmentStatus.Completed, staff, checkIn.AddHours(1), false, false);
        }
        if (status == BookingAppointmentStatus.NoShow)
        {
            appointment.TransitionTo(
                BookingAppointmentStatus.NoShow, staff, checkIn.AddHours(4), false, true);
        }

        context.Attendees.Add(attendee);
        context.Bookings.Add(booking);
        context.BookingAppointments.Add(appointment);
    }

    /// <summary>A fixed clock pinning transitional-location today to 2026-09-07 for workspace tests.</summary>
    private sealed class TestClock : IClock
    {
        /// <inheritdoc/>
        public DateTimeOffset UtcNow => new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

        /// <inheritdoc/>
        public DateTimeOffset NowAtTransitionalLocation => UtcNow;

        /// <inheritdoc/>
        public DateOnly TodayAtTransitionalLocation => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => instant;
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs — 1/1

<!-- retirement-file: {"id":22,"file":"tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs","beforeSha":"c81d671ba4a285b9f7351bd55c91d227fc30dbe35c3d5e94e4de8d6d8e18e83b","afterSha":"d8202dd14f7726f394bf93f5645bb8afa9de7af0dcf5e4e3619e025683b0e5c2","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies the workspace retains recently past events for late outcome recording.</summary>
[Collection("postgres")]
public sealed class AppointmentWorkspaceRecentPastTests(PostgresFixture fixture)
{
    /// <summary>Verifies the event list includes 7-days-past events and excludes 8-days-past events.</summary>
    [Fact]
    public async Task EventListRetainsSevenDaysPastAndExcludesOlderEvents()
    {
        await fixture.ResetAsync();
        await using (var write = fixture.NewContext())
        {
            var current = AddEvent(write, new DateOnly(2026, 9, 7), cancelled: false);
            var sevenDaysPast = AddEvent(write, new DateOnly(2026, 8, 31), cancelled: false);
            var eightDaysPast = AddEvent(write, new DateOnly(2026, 8, 30), cancelled: false);
            AddBooking(write, current, "Alex Morgan", "alex@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, sevenDaysPast, "Recent Past", "recent@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, eightDaysPast, "Too Old", "old@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var result = await new AppointmentWorkspaceQueries(read, new TestClock()).ListEventsAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            new DateOnly(2026, 9, 7),
            CancellationToken.None);

        Assert.Equal(2, result.Events.Count);
        Assert.Equal(new DateOnly(2026, 8, 31), result.Events[0].Date);
        Assert.Equal(new DateOnly(2026, 9, 7), result.Events[1].Date);
    }

    /// <summary>Verifies event detail loads a recently past event inside trusted scope.</summary>
    [Fact]
    public async Task SelectedEventLoadsRecentlyPastEventInScope()
    {
        await fixture.ResetAsync();
        Guid eventId;
        await using (var write = fixture.NewContext())
        {
            var eventItem = AddEvent(write, new DateOnly(2026, 9, 6), cancelled: false);
            eventId = eventItem.Id;
            AddBooking(write, eventItem, "Past Attendee", "past@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var detail = await new AppointmentWorkspaceQueries(read, new TestClock()).GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            eventId,
            CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(eventId, detail!.EventId);
        Assert.Equal(new DateOnly(2026, 9, 6), detail.Date);
        Assert.Single(detail.Appointments);
    }

    /// <summary>Verifies event detail returns null for events older than the allowance or outside scope.</summary>
    [Fact]
    public async Task SelectedEventTooOldOrOutOfScopeIsNotFound()
    {
        await fixture.ResetAsync();
        Guid tooOldId;
        Guid wrongScopeId;
        await using (var write = fixture.NewContext())
        {
            var tooOld = AddEvent(write, new DateOnly(2026, 8, 30), cancelled: false);
            tooOldId = tooOld.Id;
            AddBooking(write, tooOld, "Too Old", "old@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            var wrongScope = AddEvent(write, new DateOnly(2026, 9, 6), cancelled: false);
            wrongScopeId = wrongScope.Id;
            AddBooking(write, wrongScope, "Medical Attendee", "medical@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var queries = new AppointmentWorkspaceQueries(read, new TestClock());
        Assert.Null(await queries.GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, tooOldId, CancellationToken.None));
        Assert.Null(await queries.GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, wrongScopeId, CancellationToken.None));
    }

    private static Event AddEvent(
        EventBookingDbContext context,
        DateOnly date,
        bool cancelled)
    {
        var eventItem = EventFixture.Create(
            Guid.NewGuid(),
            new EventWindow(date, new TimeOnly(9, 0), 240),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 20));
        if (cancelled)
        {
            eventItem.Cancel();
        }

        context.Events.Add(eventItem);
        return eventItem;
    }

    private static void AddBooking(
        EventBookingDbContext context,
        Event eventItem,
        string name,
        string email,
        Guid appointmentTypeId,
        BookingAppointmentStatus status,
        bool cancelled)
    {
        var group = AttendeeGroup.Define(
            Guid.NewGuid(), $"WORKSPACE_{Guid.NewGuid():N}".ToUpperInvariant(), "Workspace", true,
            [appointmentTypeId]);
        context.AttendeeGroups.Add(group);
        var attendee = Attendee.Create(Guid.NewGuid(), name, email, group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            $"invite-{attendee.Id}",
            DateTimeOffset.UtcNow.AddDays(1),
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, $"manage-{attendee.Id}", DateTimeOffset.UtcNow);
        if (cancelled)
        {
            booking.Cancel();
        }

        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, appointmentTypeId);

        context.Attendees.Add(attendee);
        context.Bookings.Add(booking);
        context.BookingAppointments.Add(appointment);
    }

    /// <summary>A fixed clock pinning transitional-location today to 2026-09-07 for workspace tests.</summary>
    private sealed class TestClock : IClock
    {
        /// <inheritdoc/>
        public DateTimeOffset UtcNow => new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

        /// <inheritdoc/>
        public DateTimeOffset NowAtTransitionalLocation => UtcNow;

        /// <inheritdoc/>
        public DateOnly TodayAtTransitionalLocation => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => instant;
    }
}
`````
