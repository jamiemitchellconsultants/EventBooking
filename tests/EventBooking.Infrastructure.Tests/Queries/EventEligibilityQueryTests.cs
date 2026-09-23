using EventBooking.Application.Events;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using EventBooking.Domain.Locations;
using EventBooking.Domain.Time;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using EventBooking.Infrastructure.Persistence.Repositories;
using EventBooking.Infrastructure.Time;

namespace EventBooking.Infrastructure.Tests.Queries;

/// <summary>
/// The eligibility rule, proved against a real PostgreSQL 16 rather than against objects in
/// memory. Relational division is the whole point: an event qualifies only if it covers every
/// required type with a place left, so an event that lists fewer types than are required is not
/// eligible no matter how much room the types it does list have.
/// </summary>
[Collection("postgres")]
public class EventEligibilityQueryTests(PostgresFixture fixture)
{
    // The master plan writes the three required types as MED, FIT and IND. The prototype carries
    // the predecessor's three seeded types until Phase 3, so the plan's codes map onto those:
    // MED is the medical check-up, FIT the uniform fitting and IND the drug and alcohol testing.
    private static readonly Guid Med = AppointmentTypeIds.MedicalCheckUp;
    private static readonly Guid Fit = AppointmentTypeIds.UniformFitting;
    private static readonly Guid Ind = AppointmentTypeIds.DrugAndAlcoholTesting;

    private static readonly Guid TokyoLocationId = Guid.Parse("10000000-0000-0000-0000-00000000aaaa");

    private static readonly IEventWindowZones Zones = new NodaTimeEventWindowZones();

    /// <summary>A summer instant well before every window these tests build.</summary>
    private static readonly DateTimeOffset AsOf = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly SummerDate = new(2026, 7, 15);

    private static Guid[] Transitional => [TransitionalLocation.Id];

    [Fact]
    public async Task AnEventListingEveryRequiredTypeIsEligibleForASubsetOfThem()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();
        var eventId = await AddEventAsync(context, new TimeOnly(9, 0), All(10));

        var eligible = await Query(context).FindEligibleEventsAsync(
            [Ind], Transitional, [], 10, AsOf, CancellationToken.None);

        Assert.Equal([eventId], eligible);
    }

    [Fact]
    public async Task AnEventThatDoesNotListEveryRequiredTypeIsNotEligible()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();
        await AddEventAsync(context, new TimeOnly(9, 0), new Dictionary<Guid, int> { [Ind] = 10 });

        var eligible = await Query(context).FindEligibleEventsAsync(
            [Med, Ind], Transitional, [], 10, AsOf, CancellationToken.None);

        Assert.Empty(eligible);
    }

    [Fact]
    public async Task AnEventWithNoPlaceLeftInOneTypeIsEligibleOnlyForTheOthers()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();
        var eventId = await AddEventAsync(
            context,
            new TimeOnly(9, 0),
            new Dictionary<Guid, int> { [Ind] = 1, [Med] = 10, [Fit] = 10 },
            exhaust: [Ind]);

        var forExhausted = await Query(context).FindEligibleEventsAsync(
            [Ind], Transitional, [], 10, AsOf, CancellationToken.None);
        var forOther = await Query(context).FindEligibleEventsAsync(
            [Med], Transitional, [], 10, AsOf, CancellationToken.None);

        Assert.Empty(forExhausted);
        Assert.Equal([eventId], forOther);
    }

    [Fact]
    public async Task ACancelledEventIsNeverEligible()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();
        await AddEventAsync(context, new TimeOnly(9, 0), All(10), cancel: true);

        var eligible = await Query(context).FindEligibleEventsAsync(
            [Ind], Transitional, [], 10, AsOf, CancellationToken.None);

        Assert.Empty(eligible);
    }

    [Fact]
    public async Task AnEventThatHasAlreadyStartedIsNeverEligible()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();
        await AddEventAsync(context, new TimeOnly(9, 0), All(10));

        // 09:00 on the summer date at the transitional location is 08:00 UTC, so an as-of instant
        // one minute later is after the window has started.
        var eligible = await Query(context).FindEligibleEventsAsync(
            [Ind],
            Transitional,
            [],
            10,
            new DateTimeOffset(2026, 7, 15, 8, 1, 0, TimeSpan.Zero),
            CancellationToken.None);

        Assert.Empty(eligible);
    }

    [Fact]
    public async Task AnEventAtALocationOutsideTheSetIsNeverEligible()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();
        await AddTokyoLocationAsync(context);
        await AddEventAsync(context, new TimeOnly(9, 0), All(10), locationId: TokyoLocationId);

        var eligible = await Query(context).FindEligibleEventsAsync(
            [Ind], Transitional, [], 10, AsOf, CancellationToken.None);

        Assert.Empty(eligible);
        Assert.Equal(
            1,
            await Query(context).CountEligibleEventsAsync(
                [Ind], [TokyoLocationId], [], AsOf, CancellationToken.None));
    }

    [Fact]
    public async Task AnExcludedEventIsSkipped()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();
        var first = await AddEventAsync(context, new TimeOnly(9, 0), All(10));
        var second = await AddEventAsync(context, new TimeOnly(11, 0), All(10));

        var eligible = await Query(context).FindEligibleEventsAsync(
            [Ind], Transitional, [first], 10, AsOf, CancellationToken.None);

        Assert.Equal([second], eligible);
    }

    [Fact]
    public async Task EventsAreOrderedByInstantAndNotByLocalClockTime()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();
        await AddTokyoLocationAsync(context);

        // London and Dublin share an offset and cannot demonstrate this, so the second location is
        // Asia/Tokyo. On a summer date 09:00 in London is 08:00 UTC, while the later local time of
        // 09:30 in Tokyo is 00:30 UTC — earlier by instant, later by the clock on the wall.
        var london = await AddEventAsync(context, new TimeOnly(9, 0), All(10));
        var tokyo = await AddEventAsync(
            context, new TimeOnly(9, 30), All(10), locationId: TokyoLocationId);

        var eligible = await Query(context).FindEligibleEventsAsync(
            [Ind],
            [TransitionalLocation.Id, TokyoLocationId],
            [],
            10,
            AsOf,
            CancellationToken.None);

        Assert.Equal([tokyo, london], eligible);
    }

    [Fact]
    public async Task EventsAtTheSameInstantAreOrderedByEventId()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();
        await AddTokyoLocationAsync(context);

        // 17:00 in Tokyo and 09:00 in London are the same instant on a summer date: 08:00 UTC.
        var lower = Guid.Parse("00000000-0000-0000-0000-00000000000a");
        var higher = Guid.Parse("00000000-0000-0000-0000-00000000000b");
        await AddEventAsync(context, new TimeOnly(17, 0), All(10), locationId: TokyoLocationId, id: higher);
        await AddEventAsync(context, new TimeOnly(9, 0), All(10), id: lower);

        var eligible = await Query(context).FindEligibleEventsAsync(
            [Ind],
            [TransitionalLocation.Id, TokyoLocationId],
            [],
            10,
            AsOf,
            CancellationToken.None);

        Assert.Equal([lower, higher], eligible);
    }

    [Fact]
    public async Task TheCountLimitsTheResultsToTheEarliestOnes()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();
        var first = await AddEventAsync(context, new TimeOnly(9, 0), All(10));
        var second = await AddEventAsync(context, new TimeOnly(11, 0), All(10));
        await AddEventAsync(context, new TimeOnly(13, 0), All(10));

        var eligible = await Query(context).FindEligibleEventsAsync(
            [Ind], Transitional, [], 2, AsOf, CancellationToken.None);

        Assert.Equal([first, second], eligible);
    }

    [Fact]
    public async Task TheCountQueryAgreesWithTheListQueryIgnoringTheLimit()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();
        await AddEventAsync(context, new TimeOnly(9, 0), All(10));
        await AddEventAsync(context, new TimeOnly(11, 0), All(10));
        await AddEventAsync(context, new TimeOnly(13, 0), All(10));
        var excluded = await AddEventAsync(context, new TimeOnly(15, 0), All(10));
        await AddEventAsync(context, new TimeOnly(16, 0), new Dictionary<Guid, int> { [Med] = 10 });

        var listed = await Query(context).FindEligibleEventsAsync(
            [Ind], Transitional, [excluded], 100, AsOf, CancellationToken.None);
        var counted = await Query(context).CountEligibleEventsAsync(
            [Ind], Transitional, [excluded], AsOf, CancellationToken.None);

        Assert.Equal(3, listed.Count);
        Assert.Equal(listed.Count, counted);
    }

    private static EventEligibilityQuery Query(EventBookingDbContext context) => new(context);

    private static Dictionary<Guid, int> All(int headcount) => new()
    {
        [Ind] = headcount,
        [Med] = headcount,
        [Fit] = headcount,
    };

    private static async Task AddTokyoLocationAsync(EventBookingDbContext context)
    {
        context.Locations.Add(Location.Create(
            TokyoLocationId, "TOKYO", "Tokyo", "A second zone, well away from London.",
            "Asia/Tokyo", Zones));
        await context.SaveChangesAsync();
    }

    private static async Task<Guid> AddEventAsync(
        EventBookingDbContext context,
        TimeOnly startTime,
        IReadOnlyDictionary<Guid, int> headcounts,
        Guid? locationId = null,
        Guid? id = null,
        IReadOnlyCollection<Guid>? exhaust = null,
        bool cancel = false)
    {
        var site = locationId ?? TransitionalLocation.Id;
        var timeZoneId = site == TokyoLocationId ? "Asia/Tokyo" : TransitionalLocation.TimeZoneId;
        var window = new EventWindow(SummerDate, startTime, 120);
        var manager = Guid.NewGuid();
        var types = headcounts.Keys.Order().ToArray();

        var proposal = EventProposal.Propose(
            Guid.NewGuid(),
            site,
            locationIsActive: true,
            timeZoneId,
            window,
            Zones,
            AsOf.AddDays(-30),
            [.. types.Select(type => new ProposableAppointmentType(
                type, AppointmentTypeIds.CodeOf(type), true, true))],
            types[0],
            manager,
            headcounts[types[0]]);

        foreach (var type in types)
        {
            proposal.Accept(type, manager, headcounts[type]);
        }

        var eventItem = Event.CreateFrom(id ?? Guid.NewGuid(), proposal);

        foreach (var type in exhaust ?? [])
        {
            eventItem.CapacityFor(type).Decrement();
        }

        if (cancel)
        {
            eventItem.Cancel(Zones, timeZoneId, AsOf.AddDays(-1));
        }

        await new EventRepository(context, Zones).AddAsync(eventItem, CancellationToken.None);
        await context.SaveChangesAsync();

        return eventItem.Id;
    }
}
