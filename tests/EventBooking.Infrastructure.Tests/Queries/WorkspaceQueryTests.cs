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
/// The workspace candidate set, proved against real PostgreSQL: active events that offer
/// the caller's type, at the requested location when one is given, starting inside the
/// widened window. The exact end bound is the handler's; this query only narrows.
/// </summary>
[Collection("postgres")]
public class WorkspaceQueryTests(PostgresFixture fixture)
{
    private static readonly Guid Med = AppointmentTypeIds.MedicalCheckUp;
    private static readonly Guid Ind = AppointmentTypeIds.DrugAndAlcoholTesting;

    private static readonly Guid TokyoLocationId = Guid.Parse("10000000-0000-0000-0000-00000000aaaa");

    private static readonly IEventWindowZones Zones = new NodaTimeEventWindowZones();

    private static readonly DateTimeOffset AsOf = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task EventsStartingInsideTheWindowAreListedInStartOrder()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();
        var late = await AddEventAsync(context, new DateOnly(2026, 7, 10), new TimeOnly(13, 0));
        var early = await AddEventAsync(context, new DateOnly(2026, 7, 8), new TimeOnly(9, 0));

        var ids = await Query(context).ListWorkspaceEventIdsAsync(
            Med, null, AsOf.AddDays(-7), AsOf.AddDays(14), CancellationToken.None);

        Assert.Equal([early, late], ids);
    }

    [Fact]
    public async Task EventsStartingOutsideTheWindowAreExcluded()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();
        await AddEventAsync(context, new DateOnly(2026, 6, 20), new TimeOnly(9, 0));
        await AddEventAsync(context, new DateOnly(2026, 7, 20), new TimeOnly(9, 0));

        var ids = await Query(context).ListWorkspaceEventIdsAsync(
            Med, null, AsOf.AddDays(-7), AsOf.AddDays(14), CancellationToken.None);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task EventsNotListingTheTypeAndCancelledEventsAreExcluded()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();
        await AddEventAsync(
            context, new DateOnly(2026, 7, 8), new TimeOnly(9, 0),
            headcounts: new Dictionary<Guid, int> { [Ind] = 10 });
        await AddEventAsync(context, new DateOnly(2026, 7, 8), new TimeOnly(11, 0), cancel: true);

        var ids = await Query(context).ListWorkspaceEventIdsAsync(
            Med, null, AsOf.AddDays(-7), AsOf.AddDays(14), CancellationToken.None);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task LocationFilterNarrowsToOneSite()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();
        await AddTokyoLocationAsync(context);
        await AddEventAsync(context, new DateOnly(2026, 7, 8), new TimeOnly(9, 0));
        var tokyo = await AddEventAsync(
            context, new DateOnly(2026, 7, 8), new TimeOnly(11, 0), locationId: TokyoLocationId);

        var query = Query(context);
        Assert.Equal(
            [tokyo],
            await query.ListWorkspaceEventIdsAsync(
                Med, TokyoLocationId, AsOf.AddDays(-7), AsOf.AddDays(14), CancellationToken.None));
        Assert.Empty(await query.ListWorkspaceEventIdsAsync(
            Med, Guid.NewGuid(), AsOf.AddDays(-7), AsOf.AddDays(14), CancellationToken.None));
    }

    private static WorkspaceQueries Query(EventBookingDbContext context) => new(context);

    private static async Task<Guid> AddEventAsync(
        EventBookingDbContext context,
        DateOnly date,
        TimeOnly startTime,
        IReadOnlyDictionary<Guid, int>? headcounts = null,
        Guid? locationId = null,
        bool cancel = false)
    {
        var site = locationId ?? TransitionalLocation.Id;
        var timeZoneId = site == TokyoLocationId ? "Asia/Tokyo" : TransitionalLocation.TimeZoneId;
        var window = new EventWindow(date, startTime, 120);
        var manager = Guid.NewGuid();
        var counts = headcounts ?? new Dictionary<Guid, int> { [Med] = 10, [Ind] = 10 };
        var types = counts.Keys.Order().ToArray();

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
            counts[types[0]]);

        foreach (var type in types)
        {
            proposal.Accept(type, manager, counts[type]);
        }

        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        if (cancel)
        {
            eventItem.Cancel(Zones, timeZoneId, AsOf.AddDays(-1));
        }

        await new EventRepository(context, Zones).AddAsync(eventItem, CancellationToken.None);
        await context.SaveChangesAsync();

        return eventItem.Id;
    }

    private static async Task AddTokyoLocationAsync(EventBookingDbContext context)
    {
        context.Locations.Add(Domain.Locations.Location.Create(
            TokyoLocationId, "TOKYO", "Tokyo", "2 Shibuya", "Asia/Tokyo", Zones));
        await context.SaveChangesAsync();
    }
}
