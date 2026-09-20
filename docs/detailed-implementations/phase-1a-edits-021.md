# 01a — Variable-length windows in the location's zone, edits 21 (Task 4)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs — 1/1

<!-- retirement-file: {"id":57,"file":"tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs","beforeSha":"ad9da366dd7e3e050f4646b94047cafafee885a828db31207187ac5b7a4214f2","afterSha":"319566d5f29bf4f7dbf00bb2f550068027bb9f8d011f9f4d8fe1a82424d6c215","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Notifications;

/// <summary>Verifies attendee emails name exactly their persisted requirement snapshot.</summary>
public sealed class SnapshotEmailAuthorityTests
{
    private static readonly Attendee Attendee = Attendee.Create(
        Guid.NewGuid(), "Amara", "amara@example.com",
        AttendeeGroup.Define(
            AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting]));
    private static readonly Event Event = EventFixture.Create(
        Guid.NewGuid(), new EventWindow(new DateOnly(2026, 10, 10), new TimeOnly(9, 0)),
        new Dictionary<Guid, int>
        {
            [AppointmentTypeIds.DrugAndAlcoholTesting] = 10,
            [AppointmentTypeIds.MedicalCheckUp] = 10,
            [AppointmentTypeIds.UniformFitting] = 10,
        });
    private static readonly AttendeePortalOptions Portal = new(
        "https://booking.example", "recruitment@example.com");

    /// <summary>An Invite names only a one-type snapshot and uses singular recovery copy.</summary>
    [Fact]
    public void RecoveryInviteUsesSnapshotAndSingularCopy()
    {
        var email = AttendeeEmailComposer.Invite(
            Attendee,
            [AppointmentTypeIds.MedicalCheckUp],
            [Event, Event, Event],
            "https://booking.example/book/token",
            isReinvite: false,
            isRecovery: true);

        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.DoesNotContain("Uniform Fitting", email.TextBody);
        Assert.Contains("missed appointment", email.TextBody, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Booking confirmation names a two-type Booking snapshot, not all Attendee types.</summary>
    [Fact]
    public void ConfirmationUsesBookingSnapshot()
    {
        var email = AttendeeEmailComposer.BookingConfirmation(
            Attendee,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            Event,
            "https://booking.example/manage/token",
            Portal);

        Assert.Contains("Drug & Alcohol Testing", email.TextBody);
        Assert.Contains("Uniform Fitting", email.TextBody);
        Assert.DoesNotContain("Medical Check-up", email.TextBody);
    }

    /// <summary>Cancellation names only the affected Booking snapshot.</summary>
    [Fact]
    public void CancellationUsesAffectedBookingSnapshot()
    {
        var email = AttendeeEmailComposer.EventCancelled(
            Attendee, [AppointmentTypeIds.MedicalCheckUp], Event);

        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.DoesNotContain("Drug & Alcohol Testing", email.TextBody);
    }

    /// <summary>A three-type Invite names every snapshot type in code order.</summary>
    [Fact]
    public void ThreeTypeInviteNamesEverySnapshotType()
    {
        var email = AttendeeEmailComposer.Invite(
            Attendee,
            [AppointmentTypeIds.UniformFitting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.DrugAndAlcoholTesting],
            [Event, Event, Event],
            "https://booking.example/book/token",
            isReinvite: true,
            isRecovery: false);

        Assert.Contains("Drug & Alcohol Testing", email.TextBody);
        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.Contains("Uniform Fitting", email.TextBody);
        Assert.Contains("We have not heard back", email.TextBody);
    }

    /// <summary>A two-type cancellation names both snapshot types with plural copy.</summary>
    [Fact]
    public void TwoTypeCancellationUsesPluralCopy()
    {
        var email = AttendeeEmailComposer.EventCancelled(
            Attendee,
            [AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.DrugAndAlcoholTesting],
            Event);

        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.Contains("Drug & Alcohol Testing", email.TextBody);
        Assert.DoesNotContain("Uniform Fitting", email.TextBody);
        Assert.Contains("your appointments on", email.TextBody);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs — 1/1

<!-- retirement-file: {"id":57,"file":"tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs","beforeSha":"ad9da366dd7e3e050f4646b94047cafafee885a828db31207187ac5b7a4214f2","afterSha":"319566d5f29bf4f7dbf00bb2f550068027bb9f8d011f9f4d8fe1a82424d6c215","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Notifications;

/// <summary>Verifies attendee emails name exactly their persisted requirement snapshot.</summary>
public sealed class SnapshotEmailAuthorityTests
{
    private static readonly Attendee Attendee = Attendee.Create(
        Guid.NewGuid(), "Amara", "amara@example.com",
        AttendeeGroup.Define(
            AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting]));
    private static readonly Event Event = EventFixture.Create(
        Guid.NewGuid(), new EventWindow(new DateOnly(2026, 10, 10), new TimeOnly(9, 0), 240),
        new Dictionary<Guid, int>
        {
            [AppointmentTypeIds.DrugAndAlcoholTesting] = 10,
            [AppointmentTypeIds.MedicalCheckUp] = 10,
            [AppointmentTypeIds.UniformFitting] = 10,
        });
    private static readonly AttendeePortalOptions Portal = new(
        "https://booking.example", "recruitment@example.com");

    /// <summary>An Invite names only a one-type snapshot and uses singular recovery copy.</summary>
    [Fact]
    public void RecoveryInviteUsesSnapshotAndSingularCopy()
    {
        var email = AttendeeEmailComposer.Invite(
            Attendee,
            [AppointmentTypeIds.MedicalCheckUp],
            [Event, Event, Event],
            "https://booking.example/book/token",
            isReinvite: false,
            isRecovery: true);

        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.DoesNotContain("Uniform Fitting", email.TextBody);
        Assert.Contains("missed appointment", email.TextBody, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Booking confirmation names a two-type Booking snapshot, not all Attendee types.</summary>
    [Fact]
    public void ConfirmationUsesBookingSnapshot()
    {
        var email = AttendeeEmailComposer.BookingConfirmation(
            Attendee,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            Event,
            "https://booking.example/manage/token",
            Portal);

        Assert.Contains("Drug & Alcohol Testing", email.TextBody);
        Assert.Contains("Uniform Fitting", email.TextBody);
        Assert.DoesNotContain("Medical Check-up", email.TextBody);
    }

    /// <summary>Cancellation names only the affected Booking snapshot.</summary>
    [Fact]
    public void CancellationUsesAffectedBookingSnapshot()
    {
        var email = AttendeeEmailComposer.EventCancelled(
            Attendee, [AppointmentTypeIds.MedicalCheckUp], Event);

        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.DoesNotContain("Drug & Alcohol Testing", email.TextBody);
    }

    /// <summary>A three-type Invite names every snapshot type in code order.</summary>
    [Fact]
    public void ThreeTypeInviteNamesEverySnapshotType()
    {
        var email = AttendeeEmailComposer.Invite(
            Attendee,
            [AppointmentTypeIds.UniformFitting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.DrugAndAlcoholTesting],
            [Event, Event, Event],
            "https://booking.example/book/token",
            isReinvite: true,
            isRecovery: false);

        Assert.Contains("Drug & Alcohol Testing", email.TextBody);
        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.Contains("Uniform Fitting", email.TextBody);
        Assert.Contains("We have not heard back", email.TextBody);
    }

    /// <summary>A two-type cancellation names both snapshot types with plural copy.</summary>
    [Fact]
    public void TwoTypeCancellationUsesPluralCopy()
    {
        var email = AttendeeEmailComposer.EventCancelled(
            Attendee,
            [AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.DrugAndAlcoholTesting],
            Event);

        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.Contains("Drug & Alcohol Testing", email.TextBody);
        Assert.DoesNotContain("Uniform Fitting", email.TextBody);
        Assert.Contains("your appointments on", email.TextBody);
    }
}
`````

## before — tests/EventBooking.Domain.Tests/Events/EventCancellationTests.cs — 1/1

<!-- retirement-file: {"id":58,"file":"tests/EventBooking.Domain.Tests/Events/EventCancellationTests.cs","beforeSha":"f03dccfeef3344b486383b1f186ad08856971c45fc9388cef205f1ef7d09c034","afterSha":"a4aebe7b768744af1a7a3cd1c986fd6bd61af47795adefbebf48c47ec5fad1cf","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventCancellationTests
{
    private static Event ActiveEvent()
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 11), new TimeOnly(13, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        return Event.CreateFrom(Guid.NewGuid(), proposal);
    }

    [Fact]
    public void CancellingMarksTheEventCancelled()
    {
        var eventItem = ActiveEvent();

        eventItem.Cancel();

        Assert.Equal(EventStatus.Cancelled, eventItem.Status);
    }

    [Fact]
    public void ACancelledEventOffersNoSpareCapacityEvenWhenItsCountersAreFull()
    {
        var eventItem = ActiveEvent();
        Assert.True(eventItem.HasSpareCapacityForAll(AppointmentTypeIds.All));

        eventItem.Cancel();

        Assert.False(eventItem.HasSpareCapacityForAll(AppointmentTypeIds.All));
        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
    }

    [Fact]
    public void CancellingTwiceIsRejected()
    {
        var eventItem = ActiveEvent();
        eventItem.Cancel();

        var ex = Assert.Throws<DomainException>(() => eventItem.Cancel());
        Assert.Equal("This event has already been cancelled.", ex.Message);
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Events/EventCancellationTests.cs — 1/1

<!-- retirement-file: {"id":58,"file":"tests/EventBooking.Domain.Tests/Events/EventCancellationTests.cs","beforeSha":"f03dccfeef3344b486383b1f186ad08856971c45fc9388cef205f1ef7d09c034","afterSha":"a4aebe7b768744af1a7a3cd1c986fd6bd61af47795adefbebf48c47ec5fad1cf","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventCancellationTests
{
    private static Event ActiveEvent()
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 11), new TimeOnly(13, 0), 240),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        return Event.CreateFrom(Guid.NewGuid(), proposal);
    }

    [Fact]
    public void CancellingMarksTheEventCancelled()
    {
        var eventItem = ActiveEvent();

        eventItem.Cancel();

        Assert.Equal(EventStatus.Cancelled, eventItem.Status);
    }

    [Fact]
    public void ACancelledEventOffersNoSpareCapacityEvenWhenItsCountersAreFull()
    {
        var eventItem = ActiveEvent();
        Assert.True(eventItem.HasSpareCapacityForAll(AppointmentTypeIds.All));

        eventItem.Cancel();

        Assert.False(eventItem.HasSpareCapacityForAll(AppointmentTypeIds.All));
        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
    }

    [Fact]
    public void CancellingTwiceIsRejected()
    {
        var eventItem = ActiveEvent();
        eventItem.Cancel();

        var ex = Assert.Throws<DomainException>(() => eventItem.Cancel());
        Assert.Equal("This event has already been cancelled.", ex.Message);
    }
}
`````

## before — tests/EventBooking.Domain.Tests/Events/EventCapacityHeadcountAdjustmentTests.cs — 1/1

<!-- retirement-file: {"id":59,"file":"tests/EventBooking.Domain.Tests/Events/EventCapacityHeadcountAdjustmentTests.cs","beforeSha":"6aa700403be4d45ffa18517652992401abc7cb190c765b65a95b147cd10afc11","afterSha":"197b828df92f425cd9a4861cfc0fa10605ffc67ca0f8669b538632c7ac36d6dc","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventCapacityHeadcountAdjustmentTests
{
    private static EventCapacity CapacityOf(int totalHeadcount, int occupied = 0)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
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

<!-- retirement-file: {"id":59,"file":"tests/EventBooking.Domain.Tests/Events/EventCapacityHeadcountAdjustmentTests.cs","beforeSha":"6aa700403be4d45ffa18517652992401abc7cb190c765b65a95b147cd10afc11","afterSha":"197b828df92f425cd9a4861cfc0fa10605ffc67ca0f8669b538632c7ac36d6dc","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventCapacityHeadcountAdjustmentTests
{
    private static EventCapacity CapacityOf(int totalHeadcount, int occupied = 0)
    {
        var proposal = EventProposal.Create(
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

## before — tests/EventBooking.Domain.Tests/Events/EventCapacityTests.cs — 1/1

<!-- retirement-file: {"id":60,"file":"tests/EventBooking.Domain.Tests/Events/EventCapacityTests.cs","beforeSha":"ff6595e39327391c03f419b34895650a6c3ac8fa9404f0314edf6641c0741b0c","afterSha":"ddff62bfcd11d53612fedf9c1b646f99eeed6cb22ca844899ac77e66d547e2fc","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventCapacityTests
{
    private static EventCapacity CapacityOf(int headcount)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
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

<!-- retirement-file: {"id":60,"file":"tests/EventBooking.Domain.Tests/Events/EventCapacityTests.cs","beforeSha":"ff6595e39327391c03f419b34895650a6c3ac8fa9404f0314edf6641c0741b0c","afterSha":"ddff62bfcd11d53612fedf9c1b646f99eeed6cb22ca844899ac77e66d547e2fc","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventCapacityTests
{
    private static EventCapacity CapacityOf(int headcount)
    {
        var proposal = EventProposal.Create(
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

## before — tests/EventBooking.Domain.Tests/Events/EventProposalAcceptanceTests.cs — 1/1

<!-- retirement-file: {"id":61,"file":"tests/EventBooking.Domain.Tests/Events/EventProposalAcceptanceTests.cs","beforeSha":"96eeb4c786e05495844ff49c6ef022f71421e425f2b8f2af2b079dbaea99704d","afterSha":"7605c056e10cba87a5a9ef6302acabbe392439271e435d506230cb0167034685","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventProposalAcceptanceTests
{
    private static readonly Guid DrugAndAlcohol = AppointmentTypeIds.DrugAndAlcoholTesting;
    private static readonly Guid Medical = AppointmentTypeIds.MedicalCheckUp;
    private static readonly Guid DrugAndAlcoholManager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid MedicalManager = Guid.Parse("c0000002-0000-0000-0000-000000000002");

    private static EventProposal NewProposal() =>
        EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            DrugAndAlcoholManager);

    [Fact]
    public void AcceptingRecordsTheManagerAndTheirHeadcount()
    {
        var proposal = NewProposal();

        proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, 10);

        var acceptance = Assert.Single(proposal.Acceptances);
        Assert.Equal(proposal.Id, acceptance.ProposalId);
        Assert.Equal(DrugAndAlcohol, acceptance.AppointmentTypeId);
        Assert.Equal(DrugAndAlcoholManager, acceptance.ManagerUserId);
        Assert.Equal(10, acceptance.Headcount);
        Assert.True(proposal.IsAcceptedBy(DrugAndAlcohol));
        Assert.False(proposal.IsAcceptedBy(Medical));
    }

    [Fact]
    public void TwoManagersAcceptIndependently()
    {
        var proposal = NewProposal();

        proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, 10);
        proposal.Accept(Medical, MedicalManager, 6);

        Assert.Equal(2, proposal.Acceptances.Count);
        Assert.Equal(6, proposal.Acceptances.Single(a => a.AppointmentTypeId == Medical).Headcount);
    }

    [Fact]
    public void AcceptingTwiceForTheSameAppointmentTypeRevisesTheHeadcount()
    {
        var proposal = NewProposal();
        proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, 10);

        proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, 12);

        var acceptance = Assert.Single(proposal.Acceptances);
        Assert.Equal(12, acceptance.Headcount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void AcceptingWithoutARealHeadcountIsRejected(int headcount)
    {
        var proposal = NewProposal();

        var ex = Assert.Throws<DomainException>(
            () => proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, headcount));
        Assert.Equal("headcount must be greater than zero.", ex.Message);
        Assert.Empty(proposal.Acceptances);
    }

    [Fact]
    public void AcceptingForAnUnknownAppointmentTypeIsRejected()
    {
        var proposal = NewProposal();

        Assert.Throws<DomainException>(
            () => proposal.Accept(Guid.NewGuid(), DrugAndAlcoholManager, 10));
    }

    [Fact]
    public void WithdrawingAnUnknownAppointmentTypeIsRejected()
    {
        var proposal = NewProposal();
        proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, 10);
        var unknownAppointmentType = Guid.Parse("a0000004-0000-0000-0000-000000000004");

        var ex = Assert.Throws<DomainException>(
            () => proposal.WithdrawAcceptance(unknownAppointmentType, DrugAndAlcoholManager));

        Assert.Equal($"{unknownAppointmentType} is not one of the 3 appointment types.", ex.Message);
        Assert.Single(proposal.Acceptances);
    }

    [Fact]
    public void AWithdrawnProposalCannotBeAccepted()
    {
        var proposal = NewProposal();
        proposal.Withdraw(DrugAndAlcoholManager);

        var ex = Assert.Throws<DomainException>(
            () => proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, 10));
        Assert.Equal("Only an open proposal can be accepted.", ex.Message);
    }

    [Fact]
    public void AManagerCanWithdrawTheirOwnAcceptance()
    {
        var proposal = NewProposal();
        proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, 10);
        proposal.Accept(Medical, MedicalManager, 6);

        proposal.WithdrawAcceptance(Medical, MedicalManager);

        Assert.Single(proposal.Acceptances);
        Assert.False(proposal.IsAcceptedBy(Medical));
    }

    [Fact]
    public void AReplacementManagerCanWithdrawTheFormerManagersAcceptance()
    {
        var proposal = NewProposal();
        proposal.Accept(Medical, MedicalManager, 6);

        proposal.WithdrawAcceptance(Medical, DrugAndAlcoholManager);

        Assert.Empty(proposal.Acceptances);
    }

    [Fact]
    public void WithdrawingAnAcceptanceThatWasNeverGivenIsRejected()
    {
        var proposal = NewProposal();

        var ex = Assert.Throws<DomainException>(
            () => proposal.WithdrawAcceptance(Medical, MedicalManager));
        Assert.Equal("This appointment type has not accepted the proposal.", ex.Message);
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Events/EventProposalAcceptanceTests.cs — 1/1

<!-- retirement-file: {"id":61,"file":"tests/EventBooking.Domain.Tests/Events/EventProposalAcceptanceTests.cs","beforeSha":"96eeb4c786e05495844ff49c6ef022f71421e425f2b8f2af2b079dbaea99704d","afterSha":"7605c056e10cba87a5a9ef6302acabbe392439271e435d506230cb0167034685","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventProposalAcceptanceTests
{
    private static readonly Guid DrugAndAlcohol = AppointmentTypeIds.DrugAndAlcoholTesting;
    private static readonly Guid Medical = AppointmentTypeIds.MedicalCheckUp;
    private static readonly Guid DrugAndAlcoholManager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid MedicalManager = Guid.Parse("c0000002-0000-0000-0000-000000000002");

    private static EventProposal NewProposal() =>
        EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            DrugAndAlcoholManager);

    [Fact]
    public void AcceptingRecordsTheManagerAndTheirHeadcount()
    {
        var proposal = NewProposal();

        proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, 10);

        var acceptance = Assert.Single(proposal.Acceptances);
        Assert.Equal(proposal.Id, acceptance.ProposalId);
        Assert.Equal(DrugAndAlcohol, acceptance.AppointmentTypeId);
        Assert.Equal(DrugAndAlcoholManager, acceptance.ManagerUserId);
        Assert.Equal(10, acceptance.Headcount);
        Assert.True(proposal.IsAcceptedBy(DrugAndAlcohol));
        Assert.False(proposal.IsAcceptedBy(Medical));
    }

    [Fact]
    public void TwoManagersAcceptIndependently()
    {
        var proposal = NewProposal();

        proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, 10);
        proposal.Accept(Medical, MedicalManager, 6);

        Assert.Equal(2, proposal.Acceptances.Count);
        Assert.Equal(6, proposal.Acceptances.Single(a => a.AppointmentTypeId == Medical).Headcount);
    }

    [Fact]
    public void AcceptingTwiceForTheSameAppointmentTypeRevisesTheHeadcount()
    {
        var proposal = NewProposal();
        proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, 10);

        proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, 12);

        var acceptance = Assert.Single(proposal.Acceptances);
        Assert.Equal(12, acceptance.Headcount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void AcceptingWithoutARealHeadcountIsRejected(int headcount)
    {
        var proposal = NewProposal();

        var ex = Assert.Throws<DomainException>(
            () => proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, headcount));
        Assert.Equal("headcount must be greater than zero.", ex.Message);
        Assert.Empty(proposal.Acceptances);
    }

    [Fact]
    public void AcceptingForAnUnknownAppointmentTypeIsRejected()
    {
        var proposal = NewProposal();

        Assert.Throws<DomainException>(
            () => proposal.Accept(Guid.NewGuid(), DrugAndAlcoholManager, 10));
    }

    [Fact]
    public void WithdrawingAnUnknownAppointmentTypeIsRejected()
    {
        var proposal = NewProposal();
        proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, 10);
        var unknownAppointmentType = Guid.Parse("a0000004-0000-0000-0000-000000000004");

        var ex = Assert.Throws<DomainException>(
            () => proposal.WithdrawAcceptance(unknownAppointmentType, DrugAndAlcoholManager));

        Assert.Equal($"{unknownAppointmentType} is not one of the 3 appointment types.", ex.Message);
        Assert.Single(proposal.Acceptances);
    }

    [Fact]
    public void AWithdrawnProposalCannotBeAccepted()
    {
        var proposal = NewProposal();
        proposal.Withdraw(DrugAndAlcoholManager);

        var ex = Assert.Throws<DomainException>(
            () => proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, 10));
        Assert.Equal("Only an open proposal can be accepted.", ex.Message);
    }

    [Fact]
    public void AManagerCanWithdrawTheirOwnAcceptance()
    {
        var proposal = NewProposal();
        proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, 10);
        proposal.Accept(Medical, MedicalManager, 6);

        proposal.WithdrawAcceptance(Medical, MedicalManager);

        Assert.Single(proposal.Acceptances);
        Assert.False(proposal.IsAcceptedBy(Medical));
    }

    [Fact]
    public void AReplacementManagerCanWithdrawTheFormerManagersAcceptance()
    {
        var proposal = NewProposal();
        proposal.Accept(Medical, MedicalManager, 6);

        proposal.WithdrawAcceptance(Medical, DrugAndAlcoholManager);

        Assert.Empty(proposal.Acceptances);
    }

    [Fact]
    public void WithdrawingAnAcceptanceThatWasNeverGivenIsRejected()
    {
        var proposal = NewProposal();

        var ex = Assert.Throws<DomainException>(
            () => proposal.WithdrawAcceptance(Medical, MedicalManager));
        Assert.Equal("This appointment type has not accepted the proposal.", ex.Message);
    }
}
`````

## before — tests/EventBooking.Domain.Tests/Events/EventProposalConfirmationTests.cs — 1/1

<!-- retirement-file: {"id":62,"file":"tests/EventBooking.Domain.Tests/Events/EventProposalConfirmationTests.cs","beforeSha":"da7cfd3af32709634b989ed35d1fcf0343717c6c88f431c354221ccbe883675f","afterSha":"97f33489664bde9179549174c4f51384bf6f22d197dda35117c711adc6f2a947","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventProposalConfirmationTests
{
    private static EventProposal NewProposal() =>
        EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            Guid.NewGuid());

    private static EventProposal AcceptedByAllThree(int drugAndAlcohol = 10, int medical = 6, int uniform = 8)
    {
        var proposal = NewProposal();
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), drugAndAlcohol);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), medical);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), uniform);
        return proposal;
    }

    [Fact]
    public void AProposalWithNoAcceptancesIsNotFullyAccepted()
    {
        Assert.False(NewProposal().IsFullyAccepted);
    }

    [Fact]
    public void TwoOfThreeAcceptancesIsNotEnough()
    {
        var proposal = NewProposal();
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);

        Assert.False(proposal.IsFullyAccepted);
    }

    [Fact]
    public void AllThreeAcceptancesMakeItFullyAccepted()
    {
        Assert.True(AcceptedByAllThree().IsFullyAccepted);
    }

    [Fact]
    public void WithdrawingOneAcceptanceUndoesFullAcceptance()
    {
        var proposal = NewProposal();
        var medicalManager = Guid.NewGuid();
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, medicalManager, 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        Assert.True(proposal.IsFullyAccepted);

        proposal.WithdrawAcceptance(AppointmentTypeIds.MedicalCheckUp, medicalManager);

        Assert.False(proposal.IsFullyAccepted);
    }

    [Fact]
    public void AWithdrawnProposalIsNeverFullyAccepted()
    {
        var creator = Guid.NewGuid();
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            creator);
        proposal.Withdraw(creator);

        Assert.False(proposal.IsFullyAccepted);
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Events/EventProposalConfirmationTests.cs — 1/1

<!-- retirement-file: {"id":62,"file":"tests/EventBooking.Domain.Tests/Events/EventProposalConfirmationTests.cs","beforeSha":"da7cfd3af32709634b989ed35d1fcf0343717c6c88f431c354221ccbe883675f","afterSha":"97f33489664bde9179549174c4f51384bf6f22d197dda35117c711adc6f2a947","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventProposalConfirmationTests
{
    private static EventProposal NewProposal() =>
        EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            Guid.NewGuid());

    private static EventProposal AcceptedByAllThree(int drugAndAlcohol = 10, int medical = 6, int uniform = 8)
    {
        var proposal = NewProposal();
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), drugAndAlcohol);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), medical);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), uniform);
        return proposal;
    }

    [Fact]
    public void AProposalWithNoAcceptancesIsNotFullyAccepted()
    {
        Assert.False(NewProposal().IsFullyAccepted);
    }

    [Fact]
    public void TwoOfThreeAcceptancesIsNotEnough()
    {
        var proposal = NewProposal();
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);

        Assert.False(proposal.IsFullyAccepted);
    }

    [Fact]
    public void AllThreeAcceptancesMakeItFullyAccepted()
    {
        Assert.True(AcceptedByAllThree().IsFullyAccepted);
    }

    [Fact]
    public void WithdrawingOneAcceptanceUndoesFullAcceptance()
    {
        var proposal = NewProposal();
        var medicalManager = Guid.NewGuid();
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, medicalManager, 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        Assert.True(proposal.IsFullyAccepted);

        proposal.WithdrawAcceptance(AppointmentTypeIds.MedicalCheckUp, medicalManager);

        Assert.False(proposal.IsFullyAccepted);
    }

    [Fact]
    public void AWithdrawnProposalIsNeverFullyAccepted()
    {
        var creator = Guid.NewGuid();
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            creator);
        proposal.Withdraw(creator);

        Assert.False(proposal.IsFullyAccepted);
    }
}
`````

## before — tests/EventBooking.Domain.Tests/Events/EventProposalTests.cs — 1/1

<!-- retirement-file: {"id":63,"file":"tests/EventBooking.Domain.Tests/Events/EventProposalTests.cs","beforeSha":"f6453d7f74be3cc824026146ff70455bc4d306ca013a2ac486026effcef3187d","afterSha":"57c3a4ccb9a4bb646336ba3b2f234a4524c140535716263659278e3a9172ad04","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventProposalTests
{
    private static readonly Guid Creator = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly EventWindow Window =
        new(new DateOnly(2026, 9, 10), new TimeOnly(9, 0));

    private static EventProposal NewProposal() => EventProposal.Create(Guid.NewGuid(), Window, Creator);

    [Fact]
    public void ANewProposalIsOpenWithNoAcceptances()
    {
        var proposal = NewProposal();

        Assert.Equal(EventProposalStatus.Open, proposal.Status);
        Assert.Empty(proposal.Acceptances);
        Assert.Equal(Window, proposal.Window);
        Assert.Equal(Creator, proposal.CreatedByManagerUserId);
    }

    [Fact]
    public void AProposalWithoutACreatorIsRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => EventProposal.Create(Guid.NewGuid(), Window, Guid.Empty));
        Assert.Equal("createdByManagerUserId must not be empty.", ex.Message);
    }

    [Fact]
    public void AProposalWithoutAnIdentifierIsRejected()
    {
        Assert.Throws<DomainException>(() => EventProposal.Create(Guid.Empty, Window, Creator));
    }

    [Fact]
    public void TheCreatorCanWithdrawAnOpenProposal()
    {
        var proposal = NewProposal();

        proposal.Withdraw(Creator);

        Assert.Equal(EventProposalStatus.Withdrawn, proposal.Status);
    }

    [Fact]
    public void AnotherManagerCanWithdrawAnOpenProposal()
    {
        var proposal = NewProposal();

        proposal.Withdraw(Guid.NewGuid());

        Assert.Equal(EventProposalStatus.Withdrawn, proposal.Status);
    }

    [Fact]
    public void WithdrawingTwiceIsRejected()
    {
        var proposal = NewProposal();
        proposal.Withdraw(Creator);

        var ex = Assert.Throws<DomainException>(() => proposal.Withdraw(Creator));
        Assert.Equal("Only an open proposal can be withdrawn.", ex.Message);
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Events/EventProposalTests.cs — 1/1

<!-- retirement-file: {"id":63,"file":"tests/EventBooking.Domain.Tests/Events/EventProposalTests.cs","beforeSha":"f6453d7f74be3cc824026146ff70455bc4d306ca013a2ac486026effcef3187d","afterSha":"57c3a4ccb9a4bb646336ba3b2f234a4524c140535716263659278e3a9172ad04","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventProposalTests
{
    private static readonly Guid Creator = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly EventWindow Window =
        new(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240);

    private static EventProposal NewProposal() => EventProposal.Create(Guid.NewGuid(), Window, Creator);

    [Fact]
    public void ANewProposalIsOpenWithNoAcceptances()
    {
        var proposal = NewProposal();

        Assert.Equal(EventProposalStatus.Open, proposal.Status);
        Assert.Empty(proposal.Acceptances);
        Assert.Equal(Window, proposal.Window);
        Assert.Equal(Creator, proposal.CreatedByManagerUserId);
    }

    [Fact]
    public void AProposalWithoutACreatorIsRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => EventProposal.Create(Guid.NewGuid(), Window, Guid.Empty));
        Assert.Equal("createdByManagerUserId must not be empty.", ex.Message);
    }

    [Fact]
    public void AProposalWithoutAnIdentifierIsRejected()
    {
        Assert.Throws<DomainException>(() => EventProposal.Create(Guid.Empty, Window, Creator));
    }

    [Fact]
    public void TheCreatorCanWithdrawAnOpenProposal()
    {
        var proposal = NewProposal();

        proposal.Withdraw(Creator);

        Assert.Equal(EventProposalStatus.Withdrawn, proposal.Status);
    }

    [Fact]
    public void AnotherManagerCanWithdrawAnOpenProposal()
    {
        var proposal = NewProposal();

        proposal.Withdraw(Guid.NewGuid());

        Assert.Equal(EventProposalStatus.Withdrawn, proposal.Status);
    }

    [Fact]
    public void WithdrawingTwiceIsRejected()
    {
        var proposal = NewProposal();
        proposal.Withdraw(Creator);

        var ex = Assert.Throws<DomainException>(() => proposal.Withdraw(Creator));
        Assert.Equal("Only an open proposal can be withdrawn.", ex.Message);
    }
}
`````
