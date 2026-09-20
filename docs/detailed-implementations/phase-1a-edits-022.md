# 01a — Variable-length windows in the location's zone, edits 22 (Task 4)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — tests/EventBooking.Domain.Tests/Events/EventTests.cs — 1/1

<!-- retirement-file: {"id":64,"file":"tests/EventBooking.Domain.Tests/Events/EventTests.cs","beforeSha":"19afa88eb3d03fcd2d05a26c0091add6b05077fe5f8909f7017b6cc2debc84f4","afterSha":"c40a3c9e751f0dca6f074b00e053639beddb5a99a04a642aba0c060851404017","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventTests
{
    private static EventProposal FullyAcceptedProposal(
        int drugAndAlcohol = 10, int medical = 6, int uniform = 8)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), drugAndAlcohol);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), medical);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), uniform);
        return proposal;
    }

    [Fact]
    public void ConfirmingCarriesTheWindowAndMarksTheProposalConfirmed()
    {
        var proposal = FullyAcceptedProposal();

        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        Assert.Equal(proposal.Id, eventItem.ProposalId);
        Assert.Equal(proposal.Window, eventItem.Window);
        Assert.Equal(EventStatus.Active, eventItem.Status);
        Assert.Equal(EventProposalStatus.Confirmed, proposal.Status);
    }

    [Fact]
    public void ConfirmingCreatesOneCapacityCounterPerAppointmentType()
    {
        var eventItem = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());

        Assert.Equal(3, eventItem.Capacities.Count);
        Assert.Equal(
            AppointmentTypeIds.All.OrderBy(id => id),
            eventItem.Capacities.Select(c => c.AppointmentTypeId).OrderBy(id => id));
    }

    [Fact]
    public void EachCounterStartsAtTheHeadcountItsManagerAccepted()
    {
        var eventItem = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal(10, 6, 8));

        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).TotalHeadcount);
        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(6, eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(8, eventItem.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
    }

    [Fact]
    public void EveryCounterBelongsToTheEventThatOwnsIt()
    {
        var id = Guid.NewGuid();

        var eventItem = Event.CreateFrom(id, FullyAcceptedProposal());

        Assert.All(eventItem.Capacities, c => Assert.Equal(id, c.EventId));
    }

    [Fact]
    public void APartlyAcceptedProposalCannotBeConfirmed()
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);

        var ex = Assert.Throws<DomainException>(() => Event.CreateFrom(Guid.NewGuid(), proposal));
        Assert.Equal("A proposal can only be confirmed once all 3 managers have accepted it.", ex.Message);
    }

    [Fact]
    public void AProposalCannotBeConfirmedTwice()
    {
        var proposal = FullyAcceptedProposal();
        Event.CreateFrom(Guid.NewGuid(), proposal);

        Assert.Throws<DomainException>(() => Event.CreateFrom(Guid.NewGuid(), proposal));
    }

    [Fact]
    public void CapacityForAnUnknownAppointmentTypeIsRejected()
    {
        var eventItem = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());

        Assert.Throws<DomainException>(() => eventItem.CapacityFor(Guid.NewGuid()));
    }

    [Fact]
    public void SpareCapacityIsCheckedAcrossEveryRequiredType()
    {
        var eventItem = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());

        Assert.True(eventItem.HasSpareCapacityForAll(
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
        Assert.True(eventItem.HasSpareCapacityForAll(AppointmentTypeIds.All));
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Events/EventTests.cs — 1/1

<!-- retirement-file: {"id":64,"file":"tests/EventBooking.Domain.Tests/Events/EventTests.cs","beforeSha":"19afa88eb3d03fcd2d05a26c0091add6b05077fe5f8909f7017b6cc2debc84f4","afterSha":"c40a3c9e751f0dca6f074b00e053639beddb5a99a04a642aba0c060851404017","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventTests
{
    private static EventProposal FullyAcceptedProposal(
        int drugAndAlcohol = 10, int medical = 6, int uniform = 8)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), drugAndAlcohol);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), medical);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), uniform);
        return proposal;
    }

    [Fact]
    public void ConfirmingCarriesTheWindowAndMarksTheProposalConfirmed()
    {
        var proposal = FullyAcceptedProposal();

        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        Assert.Equal(proposal.Id, eventItem.ProposalId);
        Assert.Equal(proposal.Window, eventItem.Window);
        Assert.Equal(EventStatus.Active, eventItem.Status);
        Assert.Equal(EventProposalStatus.Confirmed, proposal.Status);
    }

    [Fact]
    public void ConfirmingCreatesOneCapacityCounterPerAppointmentType()
    {
        var eventItem = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());

        Assert.Equal(3, eventItem.Capacities.Count);
        Assert.Equal(
            AppointmentTypeIds.All.OrderBy(id => id),
            eventItem.Capacities.Select(c => c.AppointmentTypeId).OrderBy(id => id));
    }

    [Fact]
    public void EachCounterStartsAtTheHeadcountItsManagerAccepted()
    {
        var eventItem = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal(10, 6, 8));

        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).TotalHeadcount);
        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(6, eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(8, eventItem.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
    }

    [Fact]
    public void EveryCounterBelongsToTheEventThatOwnsIt()
    {
        var id = Guid.NewGuid();

        var eventItem = Event.CreateFrom(id, FullyAcceptedProposal());

        Assert.All(eventItem.Capacities, c => Assert.Equal(id, c.EventId));
    }

    [Fact]
    public void APartlyAcceptedProposalCannotBeConfirmed()
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);

        var ex = Assert.Throws<DomainException>(() => Event.CreateFrom(Guid.NewGuid(), proposal));
        Assert.Equal("A proposal can only be confirmed once all 3 managers have accepted it.", ex.Message);
    }

    [Fact]
    public void AProposalCannotBeConfirmedTwice()
    {
        var proposal = FullyAcceptedProposal();
        Event.CreateFrom(Guid.NewGuid(), proposal);

        Assert.Throws<DomainException>(() => Event.CreateFrom(Guid.NewGuid(), proposal));
    }

    [Fact]
    public void CapacityForAnUnknownAppointmentTypeIsRejected()
    {
        var eventItem = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());

        Assert.Throws<DomainException>(() => eventItem.CapacityFor(Guid.NewGuid()));
    }

    [Fact]
    public void SpareCapacityIsCheckedAcrossEveryRequiredType()
    {
        var eventItem = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());

        Assert.True(eventItem.HasSpareCapacityForAll(
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
        Assert.True(eventItem.HasSpareCapacityForAll(AppointmentTypeIds.All));
    }
}
`````

## before — tests/EventBooking.Domain.Tests/Events/EventWindowTests.cs — 1/1

<!-- retirement-file: {"id":65,"file":"tests/EventBooking.Domain.Tests/Events/EventWindowTests.cs","beforeSha":"78b6d4dfd33f4d8c0ce05d9b400ea2cf7e1d26c6472fd3367e255df104e5b2d3","afterSha":"949f4f73ed68e834ad6de1ccfd6cc0ef7130c2a899ab3a9d9f86ffb0530b0404","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventWindowTests
{
    private static EventWindow Window(int day, int hour) =>
        new(new DateOnly(2026, 9, day), new TimeOnly(hour, 0));

    [Fact]
    public void EndTimeIsFourHoursAfterTheStart()
    {
        Assert.Equal(new TimeOnly(13, 0), Window(10, 9).EndTime);
        Assert.Equal(new TimeOnly(17, 0), Window(10, 13).EndTime);
    }

    [Fact]
    public void DurationIsAlwaysFourHours()
    {
        Assert.Equal(TimeSpan.FromHours(4), EventWindow.Duration);
    }

    [Fact]
    public void TwoWindowsWithTheSameDateAndStartAreEqual()
    {
        Assert.Equal(Window(10, 9), Window(10, 9));
        Assert.NotEqual(Window(10, 9), Window(10, 13));
    }

    [Fact]
    public void WindowsSortByDateThenStartTime()
    {
        var unsorted = new List<EventWindow> { Window(12, 9), Window(10, 13), Window(10, 9) };

        unsorted.Sort();

        Assert.Equal(new List<EventWindow> { Window(10, 9), Window(10, 13), Window(12, 9) }, unsorted);
    }

    [Fact]
    public void AStartTimeThatWouldRunPastMidnightIsRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(21, 0)));
        Assert.Equal("startTime must leave room for the full 4-hour window on the same day.", ex.Message);
    }

    [Fact]
    public void StartsAfterComparesOnDateOnly()
    {
        Assert.True(Window(10, 9).StartsAfter(new DateOnly(2026, 9, 9)));
        Assert.False(Window(10, 9).StartsAfter(new DateOnly(2026, 9, 10)));
        Assert.False(Window(10, 9).StartsAfter(new DateOnly(2026, 9, 11)));
    }

    [Fact]
    public void ToStringRendersTheWindowForEmailAndAuditText()
    {
        Assert.Equal("2026-09-10 09:00-13:00", Window(10, 9).ToString());
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Events/EventWindowTests.cs — 1/1

<!-- retirement-file: {"id":65,"file":"tests/EventBooking.Domain.Tests/Events/EventWindowTests.cs","beforeSha":"78b6d4dfd33f4d8c0ce05d9b400ea2cf7e1d26c6472fd3367e255df104e5b2d3","afterSha":"949f4f73ed68e834ad6de1ccfd6cc0ef7130c2a899ab3a9d9f86ffb0530b0404","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventWindowTests
{
    private static EventWindow Window(int day, int hour, int durationMinutes = 240) =>
        new(new DateOnly(2026, 9, day), new TimeOnly(hour, 0), durationMinutes);

    [Fact]
    public void EndTimeFollowsTheStatedDuration()
    {
        Assert.Equal(new TimeOnly(13, 0), Window(10, 9).EndTime);
        Assert.Equal(new TimeOnly(17, 0), Window(10, 13).EndTime);
    }

    [Fact]
    public void TheDurationIsStatedByTheCaller()
    {
        Assert.Equal(90, Window(10, 9, 90).DurationMinutes);
        Assert.Equal(new TimeOnly(10, 30), Window(10, 9, 90).EndTime);
    }

    [Fact]
    public void TwoWindowsWithTheSameDateAndStartAreEqual()
    {
        Assert.Equal(Window(10, 9), Window(10, 9));
        Assert.NotEqual(Window(10, 9), Window(10, 13));
    }

    [Fact]
    public void WindowsSortByDateThenStartTime()
    {
        var unsorted = new List<EventWindow> { Window(12, 9), Window(10, 13), Window(10, 9) };

        unsorted.Sort();

        Assert.Equal(new List<EventWindow> { Window(10, 9), Window(10, 13), Window(12, 9) }, unsorted);
    }

    [Fact]
    public void AStartTimeThatWouldRunPastMidnightIsRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(21, 0), 240));
        Assert.Equal("The window must end on the local date it starts.", ex.Message);
    }

    [Fact]
    public void StartsAfterComparesOnDateOnly()
    {
        Assert.True(Window(10, 9).StartsAfter(new DateOnly(2026, 9, 9)));
        Assert.False(Window(10, 9).StartsAfter(new DateOnly(2026, 9, 10)));
        Assert.False(Window(10, 9).StartsAfter(new DateOnly(2026, 9, 11)));
    }

    [Fact]
    public void ToStringRendersTheWindowForEmailAndAuditText()
    {
        Assert.Equal("2026-09-10 09:00-13:00", Window(10, 9).ToString());
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Events/EventWindowZoneRuleTests.cs — 1/1

<!-- retirement-file: {"id":66,"file":"tests/EventBooking.Domain.Tests/Events/EventWindowZoneRuleTests.cs","beforeSha":null,"afterSha":"a471f844e594c653b5631c93fc5ecf390695162d3434ee33869a780343512ee9","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Events;
using EventBooking.Domain.Time;

namespace EventBooking.Domain.Tests.Events;

/// <summary>
/// Task 4: every "has it started / has it ended / is it today" rule is answered in the
/// <c>Location</c>'s zone (design 01 — Time). The domain asks an abstraction; NodaTime answers it
/// in Infrastructure.
/// </summary>
public class EventWindowZoneRuleTests
{
    // Tokyo is nine hours ahead of UTC with no daylight saving, so a UTC instant late on one day is
    // already the next local day: the rules cannot be satisfied by reading UTC.
    private const string Tokyo = "Asia/Tokyo";

    private static readonly EventWindow Window =
        new(new DateOnly(2026, 9, 14), new TimeOnly(9, 30), 90);

    private static readonly FixedOffsetZones Zones = new(TimeSpan.FromHours(9));

    private static DateTimeOffset Utc(int day, int hour, int minute = 0) =>
        new(new DateTime(2026, 9, day, hour, minute, 0, DateTimeKind.Utc));

    [Fact]
    public void TheStartAndEndInstantsComeFromTheZone()
    {
        Assert.Equal(Utc(14, 0, 30), Window.StartInstant(Zones, Tokyo));
        Assert.Equal(Utc(14, 2, 0), Window.EndInstant(Zones, Tokyo));
    }

    [Fact]
    public void TheWindowHasStartedFromItsStartInstantOnwards()
    {
        Assert.False(Window.HasStarted(Zones, Tokyo, Utc(14, 0, 29)));
        Assert.True(Window.HasStarted(Zones, Tokyo, Utc(14, 0, 30)));
        Assert.True(Window.HasStarted(Zones, Tokyo, Utc(14, 5)));
    }

    [Fact]
    public void TheWindowHasEndedFromItsEndInstantOnwards()
    {
        Assert.False(Window.HasEnded(Zones, Tokyo, Utc(14, 1, 59)));
        Assert.True(Window.HasEnded(Zones, Tokyo, Utc(14, 2)));
    }

    [Fact]
    public void TheEventDateIsTheLocalDateNotTheUtcDate()
    {
        // 23:30 UTC on the 13th is 08:30 on the 14th in Tokyo: the event's own date.
        Assert.True(Window.IsOnEventDate(Zones, Tokyo, Utc(13, 23, 30)));
        Assert.False(Window.IsOnEventDate(Zones, Tokyo, Utc(14, 15, 30)));
    }

    [Fact]
    public void AWindowWhoseStartOrEndHasNoUniqueInstantIsRejected()
    {
        var gapAtStart = new AwkwardZones(LocalTimeValidity.Gap, LocalTimeValidity.Unique);
        var ambiguousAtEnd = new AwkwardZones(LocalTimeValidity.Unique, LocalTimeValidity.Ambiguous);

        Assert.Equal(EventWindowZoneProblem.StartHasNoUniqueInstant, Window.ProblemIn(gapAtStart, Tokyo));
        Assert.Equal(EventWindowZoneProblem.EndHasNoUniqueInstant, Window.ProblemIn(ambiguousAtEnd, Tokyo));
        Assert.Equal(EventWindowZoneProblem.None, Window.ProblemIn(Zones, Tokyo));
    }

    [Fact]
    public void AnUnknownZoneIsItsOwnProblem()
    {
        Assert.Equal(EventWindowZoneProblem.UnknownZone, Window.ProblemIn(Zones, "Mars/Olympus_Mons"));
    }

    private sealed class FixedOffsetZones(TimeSpan offset) : IEventWindowZones
    {
        public bool IsKnownZone(string timeZoneId) => timeZoneId == Tokyo;

        public LocalTimeValidity ValidityOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            LocalTimeValidity.Unique;

        public DateTimeOffset InstantOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            new DateTimeOffset(date.ToDateTime(time), offset).ToUniversalTime();

        public DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId) =>
            DateOnly.FromDateTime(instant.ToOffset(offset).DateTime);

        public string AbbreviationOf(DateTimeOffset instant, string timeZoneId) => "JST";
    }

    private sealed class AwkwardZones(LocalTimeValidity start, LocalTimeValidity end) : IEventWindowZones
    {
        public bool IsKnownZone(string timeZoneId) => timeZoneId == Tokyo;

        public LocalTimeValidity ValidityOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            time == Window.StartTime ? start : end;

        public DateTimeOffset InstantOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            new(date.ToDateTime(time), TimeSpan.Zero);

        public DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId) =>
            DateOnly.FromDateTime(instant.UtcDateTime);

        public string AbbreviationOf(DateTimeOffset instant, string timeZoneId) => "JST";
    }
}
`````

## before — tests/EventBooking.Domain.Tests/Events/ProposalAcceptanceHeadcountRevisionTests.cs — 1/1

<!-- retirement-file: {"id":67,"file":"tests/EventBooking.Domain.Tests/Events/ProposalAcceptanceHeadcountRevisionTests.cs","beforeSha":"a25264d1cd3bb63721b0c003f673a70eff65c452d1ccd4c01cb280804c8f9541","afterSha":"7a7f8e3cfe473b1211fdd05d6c89395c29caaa33edf22c42a3e43c113cbabc8c","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class ProposalAcceptanceHeadcountRevisionTests
{
    private static readonly Guid DrugAndAlcoholManager =
        Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid FormerDrugAndAlcoholManager =
        Guid.Parse("c0000011-0000-0000-0000-000000000011");
    private static readonly Guid MedicalManager =
        Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid UniformManager =
        Guid.Parse("c0000003-0000-0000-0000-000000000003");

    private static EventProposal NewProposal() =>
        EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            DrugAndAlcoholManager);

    [Fact]
    public void TheSameManagerRevisesTheExistingAcceptanceInPlace()
    {
        var proposal = NewProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        var original = Assert.Single(proposal.Acceptances);

        var changed = proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 12);

        Assert.True(changed);
        Assert.Same(original, Assert.Single(proposal.Acceptances));
        Assert.Equal(12, original.Headcount);
    }

    [Fact]
    public void ResubmittingTheCurrentHeadcountReportsNoChange()
    {
        var proposal = NewProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);

        var changed = proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);

        Assert.False(changed);
        Assert.Equal(10, Assert.Single(proposal.Acceptances).Headcount);
    }

    [Fact]
    public void AReplacementManagerRevisesTheFormerManagersAcceptance()
    {
        var proposal = NewProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, FormerDrugAndAlcoholManager, 10);

        var changed = proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 12);

        Assert.True(changed);
        Assert.Equal(12, Assert.Single(proposal.Acceptances).Headcount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void AnInvalidRevisionLeavesTheCurrentHeadcountUntouched(int headcount)
    {
        var proposal = NewProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);

        var ex = Assert.Throws<DomainException>(() => proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, headcount));

        Assert.Equal("headcount must be greater than zero.", ex.Message);
        Assert.Equal(10, Assert.Single(proposal.Acceptances).Headcount);
    }

    [Fact]
    public void AWithdrawnProposalCannotHaveAnAcceptanceRevised()
    {
        var proposal = NewProposal();
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        proposal.Withdraw(DrugAndAlcoholManager);

        var ex = Assert.Throws<DomainException>(() => proposal.Accept(
            AppointmentTypeIds.MedicalCheckUp, MedicalManager, 8));

        Assert.Equal("Only an open proposal can be accepted.", ex.Message);
        Assert.Equal(6, Assert.Single(proposal.Acceptances).Headcount);
    }

    [Fact]
    public void AConfirmedProposalCannotHaveAnAcceptanceRevised()
    {
        var proposal = NewProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, UniformManager, 8);
        Event.CreateFrom(Guid.NewGuid(), proposal);

        var ex = Assert.Throws<DomainException>(() => proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 12));

        Assert.Equal("Only an open proposal can be accepted.", ex.Message);
        Assert.Equal(
            10,
            proposal.Acceptances.Single(acceptance =>
                acceptance.AppointmentTypeId
                == AppointmentTypeIds.DrugAndAlcoholTesting).Headcount);
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Events/ProposalAcceptanceHeadcountRevisionTests.cs — 1/1

<!-- retirement-file: {"id":67,"file":"tests/EventBooking.Domain.Tests/Events/ProposalAcceptanceHeadcountRevisionTests.cs","beforeSha":"a25264d1cd3bb63721b0c003f673a70eff65c452d1ccd4c01cb280804c8f9541","afterSha":"7a7f8e3cfe473b1211fdd05d6c89395c29caaa33edf22c42a3e43c113cbabc8c","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class ProposalAcceptanceHeadcountRevisionTests
{
    private static readonly Guid DrugAndAlcoholManager =
        Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid FormerDrugAndAlcoholManager =
        Guid.Parse("c0000011-0000-0000-0000-000000000011");
    private static readonly Guid MedicalManager =
        Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid UniformManager =
        Guid.Parse("c0000003-0000-0000-0000-000000000003");

    private static EventProposal NewProposal() =>
        EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            DrugAndAlcoholManager);

    [Fact]
    public void TheSameManagerRevisesTheExistingAcceptanceInPlace()
    {
        var proposal = NewProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        var original = Assert.Single(proposal.Acceptances);

        var changed = proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 12);

        Assert.True(changed);
        Assert.Same(original, Assert.Single(proposal.Acceptances));
        Assert.Equal(12, original.Headcount);
    }

    [Fact]
    public void ResubmittingTheCurrentHeadcountReportsNoChange()
    {
        var proposal = NewProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);

        var changed = proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);

        Assert.False(changed);
        Assert.Equal(10, Assert.Single(proposal.Acceptances).Headcount);
    }

    [Fact]
    public void AReplacementManagerRevisesTheFormerManagersAcceptance()
    {
        var proposal = NewProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, FormerDrugAndAlcoholManager, 10);

        var changed = proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 12);

        Assert.True(changed);
        Assert.Equal(12, Assert.Single(proposal.Acceptances).Headcount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void AnInvalidRevisionLeavesTheCurrentHeadcountUntouched(int headcount)
    {
        var proposal = NewProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);

        var ex = Assert.Throws<DomainException>(() => proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, headcount));

        Assert.Equal("headcount must be greater than zero.", ex.Message);
        Assert.Equal(10, Assert.Single(proposal.Acceptances).Headcount);
    }

    [Fact]
    public void AWithdrawnProposalCannotHaveAnAcceptanceRevised()
    {
        var proposal = NewProposal();
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        proposal.Withdraw(DrugAndAlcoholManager);

        var ex = Assert.Throws<DomainException>(() => proposal.Accept(
            AppointmentTypeIds.MedicalCheckUp, MedicalManager, 8));

        Assert.Equal("Only an open proposal can be accepted.", ex.Message);
        Assert.Equal(6, Assert.Single(proposal.Acceptances).Headcount);
    }

    [Fact]
    public void AConfirmedProposalCannotHaveAnAcceptanceRevised()
    {
        var proposal = NewProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, UniformManager, 8);
        Event.CreateFrom(Guid.NewGuid(), proposal);

        var ex = Assert.Throws<DomainException>(() => proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 12));

        Assert.Equal("Only an open proposal can be accepted.", ex.Message);
        Assert.Equal(
            10,
            proposal.Acceptances.Single(acceptance =>
                acceptance.AppointmentTypeId
                == AppointmentTypeIds.DrugAndAlcoholTesting).Headcount);
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Events/VariableEventWindowTests.cs — 1/1

<!-- retirement-file: {"id":68,"file":"tests/EventBooking.Domain.Tests/Events/VariableEventWindowTests.cs","beforeSha":null,"afterSha":"2aef75c8ff74036602609d81fbde7b9e1158a1eee35defcdd29269b51283a40f","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

/// <summary>
/// Task 4: the fixed four-hour window becomes an explicit duration in 15-minute steps that must
/// end on the local date it starts (design 01 — Time; boundary values in design 08).
/// </summary>
public class VariableEventWindowTests
{
    private static EventWindow Window(int durationMinutes, int hour = 9, int minute = 0) =>
        new(new DateOnly(2026, 9, 14), new TimeOnly(hour, minute), durationMinutes);

    [Theory]
    [InlineData(15)]
    [InlineData(90)]
    [InlineData(240)]
    [InlineData(720)]
    public void AnyQuarterHourDurationUpToTwelveHoursIsAccepted(int durationMinutes)
    {
        Assert.Equal(durationMinutes, Window(durationMinutes).DurationMinutes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-15)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(725)]
    [InlineData(735)]
    public void ADurationOutsideTheQuarterHourRangeIsRefused(int durationMinutes)
    {
        Assert.Throws<DomainException>(() => Window(durationMinutes));
    }

    [Fact]
    public void TheEndTimeIsDerivedFromTheDuration()
    {
        Assert.Equal(new TimeOnly(11, 0), Window(90, 9, 30).EndTime);
        Assert.Equal(new TimeOnly(17, 0), Window(240, 13).EndTime);
    }

    [Fact]
    public void AWindowThatWouldCrossLocalMidnightIsRefused()
    {
        Assert.Throws<DomainException>(() => Window(90, 23));
        Assert.Throws<DomainException>(() => Window(90, 22, 30));
    }

    [Fact]
    public void AWindowThatEndsBeforeLocalMidnightIsAccepted()
    {
        Assert.Equal(new TimeOnly(23, 45), Window(90, 22, 15).EndTime);
    }

    [Fact]
    public void WindowsWithTheSameDateAndStartButDifferentDurationsDiffer()
    {
        Assert.NotEqual(Window(90), Window(120));
    }

    [Fact]
    public void OrderingStaysByDateThenStartTime()
    {
        var early = new EventWindow(new DateOnly(2026, 9, 14), new TimeOnly(9, 0), 480);
        var late = new EventWindow(new DateOnly(2026, 9, 14), new TimeOnly(9, 30), 15);

        Assert.True(early.CompareTo(late) < 0);
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs — 1/1

<!-- retirement-file: {"id":69,"file":"tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs","beforeSha":"b66e333808c4f3c80ef576739625139ffd58a5e85e394b1f5671bbcfb17384b3","afterSha":"7686bea6bfe312fa1cd31e1fbf34aeeb59db18d74c41645ef9e0b5afea137e52","side":"before","part":1,"parts":1} -->

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
            new EventWindow(date, new TimeOnly(9, 0)),
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

<!-- retirement-file: {"id":69,"file":"tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs","beforeSha":"b66e333808c4f3c80ef576739625139ffd58a5e85e394b1f5671bbcfb17384b3","afterSha":"7686bea6bfe312fa1cd31e1fbf34aeeb59db18d74c41645ef9e0b5afea137e52","side":"after","part":1,"parts":1} -->

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
