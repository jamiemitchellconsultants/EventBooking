using EventBooking.Application.Abstractions;
using EventBooking.Application.Events;
using EventBooking.Application.Invites;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using EventBooking.Domain.Locations;

namespace EventBooking.Application.Tests.Invites;

/// <summary>
/// The rule this used to hold moved into the database in Task 11, and is proved there against a
/// real PostgreSQL. What is left here is the adapter: which filters it hands the eligibility port,
/// and that it hydrates the identifiers the port returns without re-imposing an order of its own.
/// </summary>
public class EligibleEventFinderTests
{
    private static readonly Guid[] NeedsTwo =
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting];

    private readonly InMemoryEventRepository _events = new();
    private readonly RecordingEligibilityQuery _eligibility = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    private EligibleEventFinder Finder => new(_eligibility, _events, _clock);

    [Fact]
    public async Task TheFiltersGoStraightToTheQueryWithTheClocksInstant()
    {
        var excluded = Guid.NewGuid();

        await Finder.FindAsync(NeedsTwo, 3, [excluded], CancellationToken.None);

        Assert.Equal(NeedsTwo, _eligibility.RequiredAppointmentTypeIds);
        Assert.Equal([excluded], _eligibility.ExcludeEventIds);
        Assert.Equal(3, _eligibility.Count);
        Assert.Equal(_clock.UtcNow, _eligibility.AsOf);
    }

    [Fact]
    public async Task OnlyTheTransitionalLocationIsAskedForUntilTaskFourteen()
    {
        await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Equal([TransitionalLocation.Id], _eligibility.LocationIds);
    }

    [Fact]
    public async Task TheEventsComeBackInTheOrderTheQueryChose()
    {
        var first = AddEvent(new DateOnly(2026, 9, 16));
        var second = AddEvent(new DateOnly(2026, 9, 10));
        var third = AddEvent(new DateOnly(2026, 9, 12));
        _eligibility.Answer = [third.Id, first.Id, second.Id];

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Equal([third.Id, first.Id, second.Id], result.Select(s => s.Id));
    }

    [Fact]
    public async Task NoEligibleEventMeansNoLookup()
    {
        AddEvent(new DateOnly(2026, 9, 10));

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task TheCountUsesTheSameFiltersWithoutALimit()
    {
        _eligibility.Answer = [Guid.NewGuid(), Guid.NewGuid()];

        var counted = await Finder.CountAsync(NeedsTwo, [], CancellationToken.None);

        Assert.Equal(2, counted);
        Assert.Equal([TransitionalLocation.Id], _eligibility.LocationIds);
        Assert.Null(_eligibility.Count);
    }

    private Event AddEvent(DateOnly date)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(9, 0), 240), Guid.NewGuid());
        foreach (var type in AppointmentTypeIds.All)
        {
            proposal.Accept(type, Guid.NewGuid(), 10);
        }

        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Add(eventItem);
        return eventItem;
    }

    /// <summary>Records what the adapter asked for, and answers with whatever it is told to.</summary>
    private sealed class RecordingEligibilityQuery : IEventEligibilityQuery
    {
        public IReadOnlyList<Guid> Answer { get; set; } = [];

        public IReadOnlyCollection<Guid>? RequiredAppointmentTypeIds { get; private set; }

        public IReadOnlyCollection<Guid>? LocationIds { get; private set; }

        public IReadOnlyCollection<Guid>? ExcludeEventIds { get; private set; }

        public int? Count { get; private set; }

        public DateTimeOffset? AsOf { get; private set; }

        public Task<IReadOnlyList<Guid>> FindEligibleEventsAsync(
            IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
            IReadOnlyCollection<Guid> locationIds,
            IReadOnlyCollection<Guid> excludeEventIds,
            int count,
            DateTimeOffset asOf,
            CancellationToken cancellationToken)
        {
            Record(requiredAppointmentTypeIds, locationIds, excludeEventIds, asOf);
            Count = count;
            return Task.FromResult(Answer);
        }

        public Task<int> CountEligibleEventsAsync(
            IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
            IReadOnlyCollection<Guid> locationIds,
            IReadOnlyCollection<Guid> excludeEventIds,
            DateTimeOffset asOf,
            CancellationToken cancellationToken)
        {
            Record(requiredAppointmentTypeIds, locationIds, excludeEventIds, asOf);
            return Task.FromResult(Answer.Count);
        }

        private void Record(
            IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
            IReadOnlyCollection<Guid> locationIds,
            IReadOnlyCollection<Guid> excludeEventIds,
            DateTimeOffset asOf)
        {
            RequiredAppointmentTypeIds = requiredAppointmentTypeIds;
            LocationIds = locationIds;
            ExcludeEventIds = excludeEventIds;
            AsOf = asOf;
        }
    }
}
