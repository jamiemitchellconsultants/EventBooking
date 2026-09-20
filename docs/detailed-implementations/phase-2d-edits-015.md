# 02d — The invite eligibility query, and the start instant it orders on, edits 15 (Task 11)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.Infrastructure.Tests/Queries/EventEligibilityPerformanceTests.cs — 1/1

<!-- retirement-file: {"id":32,"file":"tests/EventBooking.Infrastructure.Tests/Queries/EventEligibilityPerformanceTests.cs","beforeSha":null,"afterSha":"75f5ec0a36388f81cc1a4f65bf3aebc2f36f2e03b76f2b9646247bd904eee9c3","side":"after","part":1,"parts":1} -->

`````csharp
using System.Diagnostics;
using EventBooking.Application.Events;
using EventBooking.Domain.Events;
using EventBooking.Domain.Locations;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Configurations;
using EventBooking.Infrastructure.Persistence.Queries;
using EventBooking.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace EventBooking.Infrastructure.Tests.Queries;

/// <summary>
/// NFR-P2 for the invite dialog: with two thousand eligible events and ten required types, the
/// query stays under 50 ms at p95, and it reaches those events through the eligibility index
/// rather than by reading the whole table.
///
/// The two assertions are not two ways of saying the same thing, and it is worth knowing which
/// one holds what. Dropping the index turns the plan's bitmap scan into a sequential one, which
/// the first test catches and the budget does not: at this size the scan of the event table is
/// not what the 50 ms is spent on. What the budget catches is the shape this task replaced —
/// loading every active event with its capacity rows and dividing in memory takes some 680 ms on
/// the same data, more than thirteen times the budget.
///
/// Tagged Performance so a run can exclude it with <c>--filter "Category!=Performance"</c>; it is
/// not skipped, because a budget nothing ever runs is not a budget.
/// </summary>
[Collection("postgres")]
[Trait("Category", "Performance")]
public class EventEligibilityPerformanceTests(PostgresFixture fixture)
{
    private const int EventCount = 2000;
    private const int FinishedEventCount = 48000;
    private const int CancelledEventCount = 500;
    private const int ElsewhereEventCount = 500;
    private const int RequiredTypeCount = 10;
    private const int Runs = 100;
    private const int BudgetMilliseconds = 50;

    private static readonly DateTimeOffset AsOf = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly Guid OtherLocationId = Guid.Parse("10000000-0000-0000-0000-00000000bbbb");

    private static readonly Guid[] RequiredTypeIds =
        [.. Enumerable.Range(1, RequiredTypeCount)
            .Select(index => Guid.Parse($"b0000000-0000-0000-0000-{index:D12}"))];

    [Fact]
    public async Task TheQueryStaysWithinItsBudgetOverTwoThousandEvents()
    {
        await using var context = await SeedAsync();
        var query = new EventEligibilityQuery(context);

        // One untimed run so the plan is cached and the pages are warm; the budget is about the
        // steady state, not about the first statement a connection ever issues.
        await query.FindEligibleEventsAsync(
            RequiredTypeIds, [TransitionalLocation.Id], [], 3, AsOf, CancellationToken.None);

        var elapsed = new List<double>(Runs);
        for (var run = 0; run < Runs; run++)
        {
            var timer = Stopwatch.StartNew();
            var eligible = await query.FindEligibleEventsAsync(
                RequiredTypeIds, [TransitionalLocation.Id], [], 3, AsOf, CancellationToken.None);
            timer.Stop();

            Assert.Equal(3, eligible.Count);
            elapsed.Add(timer.Elapsed.TotalMilliseconds);
        }

        elapsed.Sort();
        var p95 = elapsed[(int)Math.Ceiling(Runs * 0.95) - 1];

        Assert.True(
            p95 < BudgetMilliseconds,
            $"p95 was {p95:F1} ms over {Runs} runs, above the {BudgetMilliseconds} ms budget.");
    }

    [Fact]
    public async Task TheQueryReachesTheEventsThroughTheEligibilityIndex()
    {
        await using var context = await SeedAsync();

        var plan = await ExplainAsync(context);

        Assert.Contains(EventConfiguration.EligibilityIndexName, plan, StringComparison.Ordinal);
        Assert.DoesNotContain("Seq Scan on event ", plan, StringComparison.Ordinal);
    }

    /// <summary>
    /// Explains the statement the query itself issues, not a paraphrase of it: the constant and
    /// the parameter names are the query's own, so a rewrite that stops using the index cannot
    /// leave this assertion passing.
    /// </summary>
    private static async Task<string> ExplainAsync(EventBookingDbContext context)
    {
        await context.Database.OpenConnectionAsync();
        try
        {
            var connection = (NpgsqlConnection)context.Database.GetDbConnection();
            await using var command = new NpgsqlCommand(
                "EXPLAIN " + EventEligibilityQuery.ListSql, connection);
            command.Parameters.Add(
                new NpgsqlParameter("requiredTypeIds", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
                { Value = RequiredTypeIds });
            command.Parameters.Add(
                new NpgsqlParameter("locationIds", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
                { Value = new[] { TransitionalLocation.Id } });
            command.Parameters.Add(
                new NpgsqlParameter("excludeEventIds", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
                { Value = Array.Empty<Guid>() });
            command.Parameters.Add(
                new NpgsqlParameter("activeStatus", (int)EventStatus.Active));
            command.Parameters.Add(
                new NpgsqlParameter("asOf", NpgsqlDbType.TimestampTz) { Value = AsOf });
            command.Parameters.Add(new NpgsqlParameter("count", 3));

            var lines = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lines.Add(reader.GetString(0));
            }

            return string.Join('\n', lines);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    /// <summary>
    /// Builds the rows straight through SQL. The aggregate is not what is being measured, and
    /// fifty thousand events negotiated through the domain would take longer to arrange than the
    /// whole hundred runs take to execute.
    ///
    /// Two thousand of them are eligible, as the master plan's scenario says. The other forty-nine
    /// thousand are the rows a real table also holds — mostly events that have finished, plus some
    /// that were cancelled and some at another site. That matters: an index on (status, location
    /// id, start instant) earns nothing against a table where every row already qualifies, and
    /// PostgreSQL is right to scan such a table sequentially. Seeding only qualifying rows measures
    /// a plan production would never get.
    /// </summary>
    private async Task<EventBookingDbContext> SeedAsync()
    {
        await fixture.ResetAsync();
        var context = fixture.NewContext();

        context.Locations.Add(Location.Create(
            OtherLocationId, "ELSEWHERE", "Elsewhere", "Another site entirely.",
            "Asia/Tokyo", new NodaTimeEventWindowZones()));
        await context.SaveChangesAsync();

        await AddEventsAsync(
            context, EventCount, TransitionalLocation.Id, EventStatus.Active, "2026-07-15 08:00:00+00");
        await AddEventsAsync(
            context, FinishedEventCount, TransitionalLocation.Id, EventStatus.Active, "2022-01-15 08:00:00+00");
        await AddEventsAsync(
            context, CancelledEventCount, TransitionalLocation.Id, EventStatus.Cancelled, "2026-07-15 08:00:00+00");
        await AddEventsAsync(
            context, ElsewhereEventCount, OtherLocationId, EventStatus.Active, "2026-07-15 08:00:00+00");

        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO event_capacity
                (event_id, appointment_type_id, total_headcount, remaining_capacity)
            SELECT e.id, t.id, 10, 10
              FROM event e
             CROSS JOIN unnest({0}::uuid[]) AS t(id);
            """,
            [RequiredTypeIds]);

        // Without statistics the planner costs a fifty-thousand-row table as if it were empty, and
        // the plan it chooses says nothing about the plan production would get.
        await context.Database.ExecuteSqlRawAsync(
            "ANALYZE event; ANALYZE event_capacity; ANALYZE location;");

        return context;
    }

    private static Task AddEventsAsync(
        EventBookingDbContext context,
        int count,
        Guid locationId,
        EventStatus status,
        string firstInstant) =>
        context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO event
                (id, proposal_id, location_id, date, start_time, duration_minutes, status, start_utc)
            SELECT gen_random_uuid(),
                   gen_random_uuid(),
                   {0},
                   DATE '2026-07-15',
                   TIME '09:00',
                   120,
                   {1},
                   CAST({2} AS timestamptz) + (series || ' minutes')::interval
              FROM generate_series(1, {3}) AS series;
            """,
            [locationId, (int)status, firstInstant, count]);
}
`````

## after — tests/EventBooking.Infrastructure.Tests/Queries/EventEligibilityQueryTests.cs — 1/1

<!-- retirement-file: {"id":33,"file":"tests/EventBooking.Infrastructure.Tests/Queries/EventEligibilityQueryTests.cs","beforeSha":null,"afterSha":"61725622caad89d18c0cd7803fc7af1a16d8c5e123ec595c58f989a090b53a98","side":"after","part":1,"parts":1} -->

`````csharp
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
`````

## before — tests/EventBooking.Infrastructure.Tests/RecoveryConcurrencyTests.cs — 1/1

<!-- retirement-file: {"id":34,"file":"tests/EventBooking.Infrastructure.Tests/RecoveryConcurrencyTests.cs","beforeSha":"d4a98dcfe4c854d7bd41fe34edbfa960b07bd4aaab8e4182c1f3cce771e01375","afterSha":"4205f862f0db3f5064ea75171876ec1eca7b0bef6641872325e3931104a46661","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Infrastructure.Tests;

/// <summary>
/// Races a recovery confirmation against an original-booking cancellation on PostgreSQL.
/// Row locks serialize the pair; the test proves both serialized outcomes leave no orphan
/// Booking, no duplicated capacity movement, and the exact lifecycle lock trace per racer.
/// </summary>
[Collection("postgres")]
public sealed class RecoveryConcurrencyTests(PostgresFixture fixture)
{
    /// <summary>
    /// Whichever racer commits first wins: a confirmed recovery is then cancelled with the
    /// whole journey, while a cancelled original leaves the recovery invite superseded. Both
    /// outcomes restore every capacity row and keep the attendee lifecycle consistent.
    /// </summary>
    [Fact]
    public async Task RecoveryConfirmationRacingOriginalCancellationSerializes()
    {
        await using var harness = await ConcurrencyHarness.CreateAsync(fixture);
        var race = await GivenRecoveryRaceAsync(harness);
        using var confirmScope = harness.CreateScope();
        using var cancelScope = harness.CreateScope();
        var confirmTrace = new List<string>();
        var cancelTrace = new List<string>();
        var confirmHandler = BuildConfirmHandler(confirmScope.ServiceProvider, confirmTrace);
        var cancelHandler = BuildCancelHandler(cancelScope.ServiceProvider, cancelTrace);

        var confirmTask = confirmHandler.HandleAsync(
            new ConfirmBookingCommand(race.RecoveryToken, race.RecoveryEventId), CancellationToken.None);
        var cancelTask = cancelHandler.HandleAsync(
            new CancelBookingCommand(race.ManageToken, false), CancellationToken.None);
        await Task.WhenAll(confirmTask, cancelTask);

        var confirmResult = await confirmTask;
        var cancelResult = await cancelTask;
        Assert.True(cancelResult.IsSuccess);

        await using var verify = fixture.NewContext();
        var bookings = await verify.Bookings.AsNoTracking().ToListAsync();
        var invites = await verify.Invites.AsNoTracking().ToListAsync();
        var recoveryInvite = Assert.Single(invites, i => i.RecoveryOfBookingId == race.OriginalId);

        var ids = bookings.Select(b => b.Id).ToHashSet();
        Assert.All(
            bookings.Where(b => b.RecoveryOfBookingId.HasValue),
            b => Assert.Contains(b.RecoveryOfBookingId!.Value, ids));

        if (confirmResult.IsSuccess)
        {
            var recovery = Assert.Single(bookings, b => b.RecoveryOfBookingId == race.OriginalId);
            Assert.Equal(BookingStatus.Cancelled, recovery.Status);
            Assert.Equal(BookingStatus.Cancelled, bookings.Single(b => b.Id == race.OriginalId).Status);
            Assert.Equal(InviteStatus.Used, recoveryInvite.Status);
            Assert.Equal(
                [
                    "transaction-begun",
                    "attendee-locked",
                    "invite-locked",
                    "original-booking-locked",
                    "active-recovery-locked",
                    "event-guard-locked",
                    "capacity-locked",
                ],
                confirmTrace);
            Assert.Equal(
                [
                    "booking-event-located",
                    "transaction-begun",
                    "attendee-locked",
                    "pending-invites-locked",
                    "booking-locked",
                    "active-recovery-locked",
                    "event-guard-locked",
                    "event-guard-locked",
                    "capacity-locked",
                    "capacity-locked",
                ],
                cancelTrace);
        }
        else
        {
            Assert.Equal("not_found", confirmResult.Error.Code);
            Assert.DoesNotContain(bookings, b => b.RecoveryOfBookingId.HasValue);
            Assert.Equal(InviteStatus.Superseded, recoveryInvite.Status);
            Assert.Equal(
                ["transaction-begun", "attendee-locked", "invite-locked"],
                confirmTrace);
            Assert.Equal(
                [
                    "booking-event-located",
                    "transaction-begun",
                    "attendee-locked",
                    "pending-invites-locked",
                    "booking-locked",
                    "active-recovery-locked",
                    "event-guard-locked",
                    "capacity-locked",
                ],
                cancelTrace);
        }

        Assert.Equal(BookingStatus.Cancelled, bookings.Single(b => b.Id == race.OriginalId).Status);
        var attendee = await verify.Attendees.SingleAsync(c => c.Id == race.AttendeeId);
        Assert.Equal(AttendeeStatus.NotYetInvited, attendee.Status);

        foreach (var eventId in new[] { race.OriginalEventId, race.RecoveryEventId })
        {
            var remaining = await verify.EventCapacities
                .Where(c => c.EventId == eventId
                    && c.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting)
                .Select(c => c.RemainingCapacity)
                .SingleAsync();
            Assert.Equal(10, remaining);
        }
    }

    /// <summary>
    /// Pins the cancel-first branch: after the original and its pending recovery invite are
    /// gone, confirming the recovery link fails closed without touching capacity.
    /// </summary>
    [Fact]
    public async Task CancellingFirstLeavesRecoveryConfirmationStale()
    {
        await using var harness = await ConcurrencyHarness.CreateAsync(fixture);
        var race = await GivenRecoveryRaceAsync(harness);
        using var cancelScope = harness.CreateScope();
        using var confirmScope = harness.CreateScope();
        var cancelTrace = new List<string>();
        var confirmTrace = new List<string>();

        var cancelResult = await BuildCancelHandler(cancelScope.ServiceProvider, cancelTrace)
            .HandleAsync(new CancelBookingCommand(race.ManageToken, false), CancellationToken.None);
        Assert.True(cancelResult.IsSuccess);

        var confirmResult = await BuildConfirmHandler(confirmScope.ServiceProvider, confirmTrace)
            .HandleAsync(
                new ConfirmBookingCommand(race.RecoveryToken, race.RecoveryEventId),
                CancellationToken.None);

        Assert.True(confirmResult.IsFailure);
        Assert.Equal("not_found", confirmResult.Error.Code);
        Assert.Equal(
            ["transaction-begun", "attendee-locked", "invite-locked"],
            confirmTrace);
        Assert.Equal(
            [
                "booking-event-located",
                "transaction-begun",
                "attendee-locked",
                "pending-invites-locked",
                "booking-locked",
                "active-recovery-locked",
                "event-guard-locked",
                "capacity-locked",
            ],
            cancelTrace);

        await using var verify = fixture.NewContext();
        Assert.DoesNotContain(
            await verify.Bookings.AsNoTracking().ToListAsync(),
            b => b.RecoveryOfBookingId.HasValue);
        Assert.Equal(
            InviteStatus.Superseded,
            await verify.Invites
                .Where(i => i.RecoveryOfBookingId == race.OriginalId)
                .Select(i => i.Status)
                .SingleAsync());
        Assert.Equal(
            10,
            await verify.EventCapacities
                .Where(c => c.EventId == race.RecoveryEventId
                    && c.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting)
                .Select(c => c.RemainingCapacity)
                .SingleAsync());
    }

    /// <summary>
    /// Pins the confirm-first branch: the recovery commits, then cancelling the original
    /// voids the whole journey and restores every capacity row.
    /// </summary>
    [Fact]
    public async Task ConfirmingFirstThenCancellingVoidsTheWholeJourney()
    {
        await using var harness = await ConcurrencyHarness.CreateAsync(fixture);
        var race = await GivenRecoveryRaceAsync(harness);
        using var confirmScope = harness.CreateScope();
        using var cancelScope = harness.CreateScope();
        var confirmTrace = new List<string>();
        var cancelTrace = new List<string>();

        var confirmResult = await BuildConfirmHandler(confirmScope.ServiceProvider, confirmTrace)
            .HandleAsync(
                new ConfirmBookingCommand(race.RecoveryToken, race.RecoveryEventId),
                CancellationToken.None);
        Assert.True(confirmResult.IsSuccess);

        var cancelResult = await BuildCancelHandler(cancelScope.ServiceProvider, cancelTrace)
            .HandleAsync(new CancelBookingCommand(race.ManageToken, false), CancellationToken.None);
        Assert.True(cancelResult.IsSuccess);

        Assert.Equal(
            [
                "transaction-begun",
                "attendee-locked",
                "invite-locked",
                "original-booking-locked",
                "active-recovery-locked",
                "event-guard-locked",
                "capacity-locked",
            ],
            confirmTrace);
        Assert.Equal(
            [
                "booking-event-located",
                "transaction-begun",
                "attendee-locked",
                "pending-invites-locked",
                "booking-locked",
                "active-recovery-locked",
                "event-guard-locked",
                "event-guard-locked",
                "capacity-locked",
                "capacity-locked",
            ],
            cancelTrace);

        await using var verify = fixture.NewContext();
        var bookings = await verify.Bookings.AsNoTracking().ToListAsync();
        Assert.Equal(BookingStatus.Cancelled, bookings.Single(b => b.Id == race.OriginalId).Status);
        Assert.Equal(
            BookingStatus.Cancelled,
            Assert.Single(bookings, b => b.RecoveryOfBookingId == race.OriginalId).Status);
        Assert.Equal(
            InviteStatus.Used,
            await verify.Invites
                .Where(i => i.RecoveryOfBookingId == race.OriginalId)
                .Select(i => i.Status)
                .SingleAsync());

        foreach (var eventId in new[] { race.OriginalEventId, race.RecoveryEventId })
        {
            Assert.Equal(
                10,
                await verify.EventCapacities
                    .Where(c => c.EventId == eventId
                        && c.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting)
                    .Select(c => c.RemainingCapacity)
                    .SingleAsync());
        }
    }

    /// <summary>One booked attendee with a no-show and a pending recovery invite.</summary>
    private sealed record RecoveryRace(
        Guid OriginalEventId,
        Guid RecoveryEventId,
        Guid AttendeeId,
        Guid OriginalId,
        string ManageToken,
        string RecoveryToken);

    private async Task<RecoveryRace> GivenRecoveryRaceAsync(ConcurrencyHarness harness)
    {
        var originalEventId = await harness.GivenEventAsync(10, 6, 8);
        var recoveryEventId = await harness.GivenEventAsync(10, 6, 8);

        var initialToken = await harness.GivenInvitedAttendeeAsync(
            originalEventId, AppointmentTypeIds.DrugAndAlcoholTesting);
        var confirmed = await harness.ConfirmAsync(initialToken, originalEventId);
        Assert.True(confirmed.IsSuccess);
        var originalId = confirmed.Value.BookingId;

        Guid attendeeId;
        await using (var context = fixture.NewContext())
        {
            var original = await context.Bookings.SingleAsync(b => b.Id == originalId);
            attendeeId = original.AttendeeId;
            var missed = await context.BookingAppointments.SingleAsync(a => a.BookingId == originalId);
            missed.TransitionTo(
                BookingAppointmentStatus.NoShow, Guid.NewGuid(), DateTimeOffset.UtcNow, false, true);
            await context.SaveChangesAsync();
        }

        string recoveryToken;
        using (var setup = harness.CreateScope())
        {
            var tokens = setup.ServiceProvider.GetRequiredService<ITokenService>();
            var recoveryId = Guid.NewGuid();
            var issued = tokens.Issue(TokenPurpose.Book, recoveryId, Invite.InitialTokenVersion);
            recoveryToken = issued;
            await using var context = fixture.NewContext();
            context.Invites.Add(Invite.CreateRecovery(
                recoveryId,
                attendeeId,
                originalId,
                DateTimeOffset.UtcNow.AddDays(4),
                ProposalFixture.LocationId,
                null,
                [recoveryEventId, ..harness.FallbackEventIds],
                [AppointmentTypeIds.DrugAndAlcoholTesting]));
            await context.SaveChangesAsync();
        }

        return new RecoveryRace(
            originalEventId, recoveryEventId, attendeeId, originalId,
            confirmed.Value.ManageToken, recoveryToken);
    }

    private static ConfirmBookingHandler BuildConfirmHandler(IServiceProvider services, List<string> trace) => new(
        new RecordingInviteRepository(services.GetRequiredService<IInviteRepository>(), trace),
        new RecordingAttendeeRepository(services.GetRequiredService<IAttendeeRepository>(), trace),
        new RecordingEventRepository(services.GetRequiredService<IEventRepository>(), trace),
        new RecordingBookingRepository(services.GetRequiredService<IBookingRepository>(), trace),
        services.GetRequiredService<IBookingAppointmentRepository>(),
        new RecordingCapacityRepository(services.GetRequiredService<IEventCapacityRepository>(), trace),
        new EligibleEventFinder(
            services.GetRequiredService<IEventRepository>(),
            services.GetRequiredService<IClock>()),
        services.GetRequiredService<ITokenService>(),
        services.GetRequiredService<EmailDeliveryService>(),
        services.GetRequiredService<IAuditLogger>(),
        new RecordingUnitOfWork(services.GetRequiredService<IUnitOfWork>(), trace),
        services.GetRequiredService<IClock>(),
        services.GetRequiredService<AttendeePortalOptions>());

    private static CancelBookingHandler BuildCancelHandler(IServiceProvider services, List<string> trace)
    {
        var bookings = new RecordingBookingRepository(services.GetRequiredService<IBookingRepository>(), trace);
        var capacities = new RecordingCapacityRepository(services.GetRequiredService<IEventCapacityRepository>(), trace);
        var audit = services.GetRequiredService<IAuditLogger>();
        var issuer = new InviteIssuer(
            new RecordingInviteRepository(services.GetRequiredService<IInviteRepository>(), trace),
            services.GetRequiredService<IAttendeeGroupRepository>(),
            new EligibleEventFinder(
                services.GetRequiredService<IEventRepository>(),
                services.GetRequiredService<IClock>()),
            services.GetRequiredService<ISystemSettingsRepository>(),
            services.GetRequiredService<ITokenService>(),
            services.GetRequiredService<EmailDeliveryService>(),
            audit,
            services.GetRequiredService<IClock>(),
            services.GetRequiredService<AttendeePortalOptions>());

        return new CancelBookingHandler(
            bookings,
            new RecordingEventRepository(services.GetRequiredService<IEventRepository>(), trace),
            new RecordingAttendeeRepository(services.GetRequiredService<IAttendeeRepository>(), trace),
            new RecordingInviteRepository(services.GetRequiredService<IInviteRepository>(), trace),
            new BookingCanceller(
                services.GetRequiredService<IBookingAppointmentRepository>(), capacities, audit),
            issuer,
            services.GetRequiredService<EmailDeliveryService>(),
            services.GetRequiredService<ITokenService>(),
            services.GetRequiredService<IClock>(),
            new RecordingUnitOfWork(services.GetRequiredService<IUnitOfWork>(), trace));
    }

    /// <summary>Records attendee lifecycle-lock acquisition around the real repository.</summary>
    private sealed class RecordingAttendeeRepository(
        IAttendeeRepository inner,
        List<string> trace) : IAttendeeRepository
    {
        public Task<Attendee?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            inner.GetAsync(id, cancellationToken);

        public Task<Attendee?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
        {
            trace.Add("attendee-locked");
            return inner.LockForUpdateAsync(id, cancellationToken);
        }

        public Task<Attendee?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
            inner.GetByEmailAsync(email, cancellationToken);

        public Task<IReadOnlyList<Attendee>> ListAsync(
            AttendeeStatus? status,
            CancellationToken cancellationToken) =>
            inner.ListAsync(status, cancellationToken);

        public void Add(Attendee attendee) => inner.Add(attendee);

        public void Remove(Attendee attendee) => inner.Remove(attendee);
    }

    /// <summary>Records invite-lock acquisition order around the real repository.</summary>
    private sealed class RecordingInviteRepository(
        IInviteRepository inner,
        List<string> trace) : IInviteRepository
    {
        public Task<Invite?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            inner.GetAsync(id, cancellationToken);

        public Task<Invite?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
        {
            trace.Add("invite-locked");
            return inner.LockForUpdateAsync(id, cancellationToken);
        }

        public Task<Invite?> LockPendingForAttendeeAsync(
            Guid attendeeId,
            CancellationToken cancellationToken) =>
            inner.LockPendingForAttendeeAsync(attendeeId, cancellationToken);

        public Task<Invite?> GetPendingForAttendeeAsync(
            Guid attendeeId,
            CancellationToken cancellationToken) =>
            inner.GetPendingForAttendeeAsync(attendeeId, cancellationToken);

        public Task<Invite?> LockPendingInitialForAttendeeAsync(
            Guid attendeeId,
            CancellationToken cancellationToken) =>
            inner.LockPendingInitialForAttendeeAsync(attendeeId, cancellationToken);

        public Task<IReadOnlyList<Invite>> LockPendingListForAttendeeAsync(
            Guid attendeeId,
            CancellationToken cancellationToken)
        {
            trace.Add("pending-invites-locked");
            return inner.LockPendingListForAttendeeAsync(attendeeId, cancellationToken);
        }

        public Task<IReadOnlyList<Invite>> ListPendingExpiredAsync(
            DateTimeOffset asAt,
            CancellationToken cancellationToken) =>
            inner.ListPendingExpiredAsync(asAt, cancellationToken);

        public void Add(Invite invite) => inner.Add(invite);
    }

    /// <summary>Records booking-lock acquisition order around the real repository.</summary>
    private sealed class RecordingBookingRepository(
        IBookingRepository inner,
        List<string> trace) : IBookingRepository
    {
        public Task<Booking?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            inner.GetAsync(id, cancellationToken);

        public Task<Booking?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
        {
            trace.Add("booking-locked");
            return inner.LockForUpdateAsync(id, cancellationToken);
        }

        public Task<Guid?> GetEventIdAsync(Guid id, CancellationToken cancellationToken)
        {
            trace.Add("booking-event-located");
            return inner.GetEventIdAsync(id, cancellationToken);
        }

        public Task<Guid?> GetAttendeeIdAsync(Guid id, CancellationToken cancellationToken) =>
            inner.GetAttendeeIdAsync(id, cancellationToken);

        public Task<Booking?> LockByIdForAttendeeAsync(
            Guid bookingId,
            Guid attendeeId,
            CancellationToken cancellationToken)
        {
            trace.Add("booking-locked");
            return inner.LockByIdForAttendeeAsync(bookingId, attendeeId, cancellationToken);
        }

        public Task<Booking?> LockActiveForAttendeeAsync(
            Guid attendeeId,
            CancellationToken cancellationToken) =>
            inner.LockActiveForAttendeeAsync(attendeeId, cancellationToken);

        public Task<Booking?> GetActiveForAttendeeAsync(
            Guid attendeeId,
            CancellationToken cancellationToken) =>
            inner.GetActiveForAttendeeAsync(attendeeId, cancellationToken);

        public Task<Booking?> LockActiveOriginalForAttendeeAsync(
            Guid attendeeId,
            CancellationToken cancellationToken)
        {
            trace.Add("original-booking-locked");
            return inner.LockActiveOriginalForAttendeeAsync(attendeeId, cancellationToken);
        }

        public Task<IReadOnlyList<Guid>> ListActiveAttendeeIdsForEventAsync(
            Guid eventId,
            CancellationToken cancellationToken) =>
            inner.ListActiveAttendeeIdsForEventAsync(eventId, cancellationToken);

        public Task<IReadOnlyList<Booking>> ListActiveForEventAsync(
            Guid eventId,
            CancellationToken cancellationToken) =>
            inner.ListActiveForEventAsync(eventId, cancellationToken);

        public Task<IReadOnlyList<Booking>> ListJourneyAsync(
            Guid originalBookingId,
            CancellationToken cancellationToken) =>
            inner.ListJourneyAsync(originalBookingId, cancellationToken);

        public Task<Booking?> LockActiveRecoveryAsync(
            Guid originalBookingId,
            CancellationToken cancellationToken)
        {
            trace.Add("active-recovery-locked");
            return inner.LockActiveRecoveryAsync(originalBookingId, cancellationToken);
        }

        public void Add(Booking booking) => inner.Add(booking);
    }

    /// <summary>Records event-guard acquisition around the real repository.</summary>
    private sealed class RecordingEventRepository(
        IEventRepository inner,
        List<string> trace) : IEventRepository
    {
        public Task<Event?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            inner.GetAsync(id, cancellationToken);

        public Task<Event?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
        {
            trace.Add("event-guard-locked");
            return inner.LockForUpdateAsync(id, cancellationToken);
        }

        public Task<IReadOnlyList<Event>> ListActiveAsync(
            DateOnly onOrAfter,
            CancellationToken cancellationToken) =>
            inner.ListActiveAsync(onOrAfter, cancellationToken);

        public Task<IReadOnlyList<Event>> ListAllAsync(CancellationToken cancellationToken) =>
            inner.ListAllAsync(cancellationToken);

        public void Add(Event eventItem) => inner.Add(eventItem);
    }

    /// <summary>Records capacity-lock acquisition around the real repository.</summary>
    private sealed class RecordingCapacityRepository(
        IEventCapacityRepository inner,
        List<string> trace) : IEventCapacityRepository
    {
        public Task<IReadOnlyList<EventCapacity>> LockForUpdateAsync(
            Guid eventId,
            IReadOnlyCollection<Guid> appointmentTypeIds,
            CancellationToken cancellationToken)
        {
            trace.Add("capacity-locked");
            return inner.LockForUpdateAsync(eventId, appointmentTypeIds, cancellationToken);
        }
    }

    /// <summary>Records transaction boundaries around the real unit of work.</summary>
    private sealed class RecordingUnitOfWork(IUnitOfWork inner, List<string> trace) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
            inner.SaveChangesAsync(cancellationToken);

        public Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken)
        {
            trace.Add("transaction-begun");
            return inner.BeginTransactionAsync(cancellationToken);
        }
    }
}
`````
