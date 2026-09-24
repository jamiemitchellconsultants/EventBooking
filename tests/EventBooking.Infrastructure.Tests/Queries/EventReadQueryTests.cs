using EventBooking.Application.Events;
using EventBooking.Application.ReadModels;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using EventBooking.Domain.Locations;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using EventBooking.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests.Queries;

/// <summary>
/// The scope rule and the keyset walk, against real PostgreSQL. The scope cases drive the
/// query with no capability check in front of it, because that is the only way to prove the
/// filter is in the query rather than only in the handler.
/// </summary>
[Collection("postgres")]
public sealed class EventReadQueryTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 21, 9, 0, 0, TimeSpan.Zero);
    private static readonly EventScope EveryType = new(AllTypes: true, null);
    private static readonly EventScope MedicalOnly =
        new(AllTypes: false, AppointmentTypeIds.MedicalCheckUp);

    [Fact]
    public async Task AnAllTypesCallerSeesEveryCapacityRow()
    {
        await fixture.ResetAsync();
        var location = await GivenLocationAsync("QRY_ALL", "Europe/London");
        await GivenEventAsync(location, new DateOnly(2026, 10, 14), new TimeOnly(9, 30));

        var rows = await Query().ListAsync(
            Filter(), EveryType, Now, notStartedOnly: false, CancellationToken.None);

        Assert.Equal(3, Assert.Single(rows).Capacities.Count);
    }

    /// <summary>
    /// Driven straight at the query. A Manager sees their own type's headcount and no other
    /// type's, which is the whole of design 05's "a Manager sees their type's view".
    /// </summary>
    [Fact]
    public async Task AScopedCallerSeesOnlyTheirOwnTypesCapacity()
    {
        await fixture.ResetAsync();
        var location = await GivenLocationAsync("QRY_SCOPE", "Europe/London");
        await GivenEventAsync(location, new DateOnly(2026, 10, 14), new TimeOnly(9, 30));

        var rows = await Query().ListAsync(
            Filter(), MedicalOnly, Now, notStartedOnly: false, CancellationToken.None);

        var capacity = Assert.Single(Assert.Single(rows).Capacities);
        Assert.Equal(AppointmentTypeIds.MedicalCheckUp, capacity.AppointmentTypeId);
    }

    /// <summary>An event that does not list the caller's type is not theirs to see at all.</summary>
    [Fact]
    public async Task AnEventThatDoesNotListTheScopedTypeIsAbsent()
    {
        await fixture.ResetAsync();
        var location = await GivenLocationAsync("QRY_ABSENT", "Europe/London");
        await GivenEventAsync(
            location, new DateOnly(2026, 10, 14), new TimeOnly(9, 30),
            listed: [AppointmentTypeIds.DrugAndAlcoholTesting]);

        var rows = await Query().ListAsync(
            Filter(), MedicalOnly, Now, notStartedOnly: false, CancellationToken.None);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task ASingleReadOutsideTheScopeIsNull()
    {
        await fixture.ResetAsync();
        var location = await GivenLocationAsync("QRY_ONE", "Europe/London");
        var eventId = await GivenEventAsync(
            location, new DateOnly(2026, 10, 14), new TimeOnly(9, 30),
            listed: [AppointmentTypeIds.DrugAndAlcoholTesting]);

        Assert.NotNull(await Query().GetAsync(eventId, EveryType, CancellationToken.None));
        Assert.Null(await Query().GetAsync(eventId, MedicalOnly, CancellationToken.None));
    }

    /// <summary>
    /// Five events sharing one start instant. A keyset on the instant alone would either
    /// repeat the boundary row or drop everything after it, so this is the case that proves
    /// the identifier is part of the key.
    /// </summary>
    [Fact]
    public async Task AWalkOverCollidingStartInstantsLosesAndRepeatsNothing()
    {
        await fixture.ResetAsync();
        var location = await GivenLocationAsync("QRY_TIE", "Europe/London");
        for (var index = 0; index < 5; index++)
        {
            await GivenEventAsync(location, new DateOnly(2026, 10, 14), new TimeOnly(9, 30));
        }

        var seen = new List<Guid>();
        string? cursor = null;
        for (var page = 0; page < 5; page++)
        {
            var rows = await Query().ListAsync(
                Filter() with { Cursor = cursor, Limit = 2 },
                EveryType, Now, notStartedOnly: false, CancellationToken.None);
            if (rows.Count == 0)
            {
                break;
            }

            seen.AddRange(rows.Select(x => x.EventId));
            cursor = rows[^1].Cursor;
        }

        Assert.Equal(5, seen.Count);
        Assert.Equal(5, seen.Distinct().Count());
    }

    [Fact]
    public async Task TheLocationFilterNarrowsToOneSite()
    {
        await fixture.ResetAsync();
        var london = await GivenLocationAsync("QRY_LON", "Europe/London");
        var tokyo = await GivenLocationAsync("QRY_TOK", "Asia/Tokyo");
        await GivenEventAsync(london, new DateOnly(2026, 10, 14), new TimeOnly(9, 30));
        await GivenEventAsync(tokyo, new DateOnly(2026, 10, 14), new TimeOnly(9, 30));

        var rows = await Query().ListAsync(
            Filter() with { LocationId = tokyo }, EveryType, Now, notStartedOnly: false,
            CancellationToken.None);

        Assert.Equal("Asia/Tokyo", Assert.Single(rows).TimeZoneId);
    }

    /// <summary>
    /// London and Tokyo, not London and Dublin: contradiction #10. The two windows share a
    /// local date and time, and the ordering is by the derived instant, so Tokyo's must come
    /// first — an ordering that a shared offset could not demonstrate at all.
    /// </summary>
    [Fact]
    public async Task TheOrderIsByTheDerivedInstantRatherThanTheLocalTime()
    {
        await fixture.ResetAsync();
        var london = await GivenLocationAsync("QRY_ORD_LON", "Europe/London");
        var tokyo = await GivenLocationAsync("QRY_ORD_TOK", "Asia/Tokyo");
        await GivenEventAsync(london, new DateOnly(2026, 10, 14), new TimeOnly(9, 30));
        await GivenEventAsync(tokyo, new DateOnly(2026, 10, 14), new TimeOnly(9, 30));

        var rows = await Query().ListAsync(
            Filter(), EveryType, Now, notStartedOnly: false, CancellationToken.None);

        Assert.Equal(["Asia/Tokyo", "Europe/London"], rows.Select(x => x.TimeZoneId));
    }

    [Fact]
    public async Task TheDateRangeIsInclusiveAtBothEnds()
    {
        await fixture.ResetAsync();
        var location = await GivenLocationAsync("QRY_RANGE", "Europe/London");
        await GivenEventAsync(location, new DateOnly(2026, 10, 13), new TimeOnly(9, 30));
        await GivenEventAsync(location, new DateOnly(2026, 10, 14), new TimeOnly(9, 30));
        await GivenEventAsync(location, new DateOnly(2026, 10, 15), new TimeOnly(9, 30));

        var rows = await Query().ListAsync(
            Filter() with { From = new DateOnly(2026, 10, 13), To = new DateOnly(2026, 10, 14) },
            EveryType, Now, notStartedOnly: false, CancellationToken.None);

        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public async Task TheAppointmentTypeFilterKeepsEventsListingThatType()
    {
        await fixture.ResetAsync();
        var location = await GivenLocationAsync("QRY_TYPE", "Europe/London");
        await GivenEventAsync(
            location, new DateOnly(2026, 10, 14), new TimeOnly(9, 30),
            listed: [AppointmentTypeIds.DrugAndAlcoholTesting]);
        await GivenEventAsync(location, new DateOnly(2026, 10, 15), new TimeOnly(9, 30));

        var rows = await Query().ListAsync(
            Filter() with { AppointmentTypeId = AppointmentTypeIds.MedicalCheckUp },
            EveryType, Now, notStartedOnly: false, CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 10, 15), Assert.Single(rows).Date);
    }

    /// <summary>
    /// FR-7.2: the cancellable list is what a cancellation can still reach. A window that has
    /// started and a cancelled event are both out, and the bound is the derived instant.
    /// </summary>
    [Fact]
    public async Task TheCancellableListExcludesStartedAndCancelledEvents()
    {
        await fixture.ResetAsync();
        var location = await GivenLocationAsync("QRY_CANCELLABLE", "Europe/London");
        await GivenEventAsync(location, new DateOnly(2026, 9, 20), new TimeOnly(9, 30));
        var cancelled = await GivenEventAsync(
            location, new DateOnly(2026, 10, 14), new TimeOnly(9, 30));
        var live = await GivenEventAsync(
            location, new DateOnly(2026, 10, 15), new TimeOnly(9, 30));
        await CancelAsync(cancelled);

        var rows = await Query().ListAsync(
            Filter(), EveryType, Now, notStartedOnly: true, CancellationToken.None);

        Assert.Equal(live, Assert.Single(rows).EventId);
    }

    /// <summary>
    /// A signed cursor carries no list identity, so one minted for the attendee list
    /// arrives here intact but with a short sort key. It must read as the first page,
    /// not throw slicing a date off it.
    /// </summary>
    [Fact]
    public async Task ACursorMintedForAnotherListReadsAsTheFirstPage()
    {
        await fixture.ResetAsync();

        var rows = await new EventProposalListQueries(NewContext()).ListAsync(
            AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), null, null,
            KeysetCursor.Encode("amy", Guid.NewGuid()), 50, CancellationToken.None);

        Assert.Empty(rows);
    }

    private static ListEventsQuery Filter() =>
        new(Guid.NewGuid(), null, null, null, null, null, 50);

    private EventReadQueries Query() => new(NewContext());

    private EventBookingDbContext NewContext() =>
        new(new DbContextOptionsBuilder<EventBookingDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options);

    private async Task<Guid> GivenLocationAsync(string code, string timeZoneId)
    {
        await using var context = NewContext();
        var location = Location.Create(
            Guid.NewGuid(), code, code, "1 Test Street", timeZoneId, ProposalFixture.Zones);
        context.Locations.Add(location);
        await context.SaveChangesAsync();
        return location.Id;
    }

    private async Task<Guid> GivenEventAsync(
        Guid locationId, DateOnly date, TimeOnly startTime, IReadOnlyList<Guid>? listed = null)
    {
        var types = listed ??
        [
            AppointmentTypeIds.DrugAndAlcoholTesting,
            AppointmentTypeIds.MedicalCheckUp,
            AppointmentTypeIds.UniformFitting,
        ];

        await using var context = NewContext();
        var manager = Guid.NewGuid();
        var proposal = EventProposal.Propose(
            Guid.NewGuid(),
            ProposalFixture.LocationId,
            locationIsActive: true,
            ProposalFixture.TimeZoneId,
            new EventWindow(date, startTime, 240),
            ProposalFixture.Zones,
            ProposalFixture.Now,
            [.. types.Select(type => new ProposableAppointmentType(
                type, AppointmentTypeIds.CodeOf(type), true, true))],
            types[0],
            manager,
            headcount: 10);
        foreach (var type in types)
        {
            proposal.Accept(type, manager, 10);
        }

        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        context.EventProposals.Add(proposal);
        context.Events.Add(eventItem);
        await context.SaveChangesAsync();

        // The fixture builds every proposal at its own transitional site, so the location is
        // set here rather than in the fixture: these cases are about filtering and ordering
        // across sites, which is exactly what the fixture's single site cannot show.
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE event SET location_id = {0} WHERE id = {1}",
            locationId, eventItem.Id);
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE event_proposal SET location_id = {0} WHERE id = {1}",
            locationId, proposal.Id);

        // start_utc is derived from the location's zone, so moving the event must recompute
        // it through the same stamp the save-time backstop uses. Marking the row Modified
        // does not restamp it — the backstop only stamps inserts — so the stamp is explicit.
        await using var restamp = NewContext();
        var reread = await restamp.Events.SingleAsync(e => e.Id == eventItem.Id);
        var timeZoneId = await restamp.Locations
            .Where(l => l.Id == locationId)
            .Select(l => l.TimeZoneId)
            .SingleAsync();
        EventStartInstants.Stamp(restamp, reread, timeZoneId, new NodaTimeEventWindowZones());
        await restamp.SaveChangesAsync();
        return eventItem.Id;
    }

    private async Task CancelAsync(Guid eventId)
    {
        await using var context = NewContext();
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE event SET status = {0} WHERE id = {1}", (int)EventStatus.Cancelled, eventId);
    }
}
