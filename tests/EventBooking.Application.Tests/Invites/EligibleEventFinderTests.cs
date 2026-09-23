using EventBooking.Application.Invites;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Invites;

public class EligibleEventFinderTests
{
    private static readonly Guid[] NeedsTwo =
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting];

    private readonly InMemoryEventRepository _events = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    private EligibleEventFinder Finder => new(_events, _clock);

    [Fact]
    public async Task TheThreeEarliestQualifyingEventsAreReturnedInOrder()
    {
        AddEvent(new DateOnly(2026, 9, 14));
        AddEvent(new DateOnly(2026, 9, 10));
        AddEvent(new DateOnly(2026, 9, 12));
        AddEvent(new DateOnly(2026, 9, 16));

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.Equal(
            new[] { new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 14) },
            result.Select(s => s.Window.Date));
    }

    [Fact]
    public async Task TwoWindowsOnOneDayAreOrderedByStartTime()
    {
        AddEvent(new DateOnly(2026, 9, 10), startHour: 13);
        AddEvent(new DateOnly(2026, 9, 10), startHour: 9);

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Equal(
            new[] { new TimeOnly(9, 0), new TimeOnly(13, 0) },
            result.Select(s => s.Window.StartTime));
    }

    [Fact]
    public async Task AEventFullInOneRequiredTypeIsNotEligibleEvenIfTheOthersHaveRoom()
    {
        var eventItem = AddEvent(new DateOnly(2026, 9, 10), drugAndAlcoholHeadcount: 1);
        eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task AEventFullOnlyInATypeTheAttendeeDoesNotNeedIsStillEligible()
    {
        var eventItem = AddEvent(new DateOnly(2026, 9, 10), medicalHeadcount: 1);
        eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).Decrement();

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Single(result);
    }

    [Fact]
    public async Task CancelledEventsAreNeverEligible()
    {
        var eventItem = AddEvent(new DateOnly(2026, 9, 10));
        eventItem.Cancel();

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task TodaysAndPastWindowsAreNeverEligible()
    {
        AddEvent(new DateOnly(2026, 9, 3));
        AddEvent(new DateOnly(2026, 9, 1));

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ExcludedEventsAreSkipped()
    {
        var first = AddEvent(new DateOnly(2026, 9, 10));
        AddEvent(new DateOnly(2026, 9, 12));

        var result = await Finder.FindAsync(NeedsTwo, 3, [first.Id], CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 9, 12), Assert.Single(result).Window.Date);
    }

    [Fact]
    public async Task FewerQualifyingEventsThanAskedForReturnsWhatThereIs()
    {
        AddEvent(new DateOnly(2026, 9, 10));
        AddEvent(new DateOnly(2026, 9, 12));

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Equal(2, result.Count);
    }

    private Event AddEvent(
        DateOnly date,
        int startHour = 9,
        int drugAndAlcoholHeadcount = 10,
        int medicalHeadcount = 6,
        int uniformHeadcount = 8)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(startHour, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), drugAndAlcoholHeadcount);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), medicalHeadcount);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), uniformHeadcount);

        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Add(eventItem);
        return eventItem;
    }
}
