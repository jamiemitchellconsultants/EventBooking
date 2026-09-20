# 02d — The invite eligibility query, and the start instant it orders on (Task 11)

[← Phase overview](phase-2-persistence.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This task follows Task 10 and closes Phase 2. The rule that decides which `Event`s an `Attendee` may be offered stops being a filter over loaded objects and becomes one SQL statement behind an Application port. The derived column that statement orders and indexes on stops being nullable in the same task, because the repository now computes it from the `EventWindow` and the `Location`'s zone as it inserts the row.

> Use superpowers:executing-plans. Complete changed types and exact before/after files are embedded
> in the numbered companion volumes; apply them with the script in Step 3, never by hand.

**Goal:** One port with two operations, implemented once in infrastructure: find the eligible `Event`s for a set of required `AppointmentType`s, at a set of `Location`s, excluding those already offered, as of an instant; and count them under the same filters without a limit. Each is a single relational-division statement. The `Event` repository writes `start_utc` in the transaction that inserts the row, a save-time backstop writes it for every other writer, and a migration backfills the rows already there before making the column required.

**Architecture:** Relational division belongs in the database. The join keeps only capacity rows for a required type with a place left, so the group for one `Event` has as many rows as the types it covers, and the HAVING clause keeps only the `Event`s that cover all of them — an `Event` that does not list a required type simply contributes fewer rows and drops out. The alternative, which is what was inherited, is to load every active `Event` with its `EventCapacity` rows and divide in memory; on the performance suite's own data that takes some 680 ms against the query's 18 ms. The port returns identifiers rather than aggregates, because a query that proposes candidates must not look like a read of authoritative state: capacity is re-checked under lock at booking time, so a stale option can never overbook. The Application-side finder stays, as the one place the five callers reach the rule through, and becomes an adapter that hydrates what the port chose without re-imposing an order of its own.

**Tech Stack:** .NET 10, xUnit, EF Core, PostgreSQL Testcontainers, bUnit.

**Spec:** [Master Task 11](../superpowers/plans/2026-09-19-eventbooking-implementation.md), [domain model](../design/01-domain-model.md), [functional requirements](../design/02-functional-requirements.md), [ontology](../ontology.md).

## Global constraints

The statement is design 04's, with one deliberate difference: `status` is stored as an integer by this model, so the comparison is against the integer value rather than the string the design writes. Required type identifiers are de-duplicated before the statement runs, because `cardinality` on a parameter with a repeat would exceed anything the group could count and reject every `Event`. The derived instant is normalised to UTC before it is stored — the zone resolver answers with the `Location`'s own offset and PostgreSQL refuses any offset but zero for a timestamp with time zone. The column's CLR type stays nullable although the column is not, so an unstamped row is distinguishable from one stamped with a default. Keep the four documented lock levels: nothing here takes a lock, and extending the ladder is Task 15's.

## Review focus

STOP AND CHECK four things. The division is proved by the `Event` that lists fewer types than are required, not by the one that lists more: an `Event` offering every type is eligible for any subset of them, while an `Event` offering one type is not eligible for two. Ordering is by instant and not by the clock on the wall — the pair that proves it is at two `Location`s in genuinely different zones, because London and Dublin share an offset and prove nothing. The EXPLAIN assertion only means something against a table whose rows do not all qualify; with fifty thousand `Event`s of which two thousand are eligible the plan uses the index, and dropping the index turns it into a sequential scan and fails the test. And the p95 budget is not a second way of saying the same thing: it passes with the index dropped, because at this size the scan of the `Event` table is not where the time goes. What it catches is the in-memory shape this task replaced.

### Task 11: Relational division behind an Application port, and the derived start instant

**Files:**

- Create: src/EventBooking.Application/Abstractions/IEventEligibilityQuery.cs
- Modify: src/EventBooking.Application/Abstractions/IEventRepository.cs
- Modify: src/EventBooking.Application/Events/AcceptProposalHandler.cs
- Modify: src/EventBooking.Application/Invites/EligibleEventFinder.cs
- Modify: src/EventBooking.Infrastructure/DependencyInjection.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs
- Modify: src/EventBooking.Infrastructure/Persistence/EventBookingDbContext.cs
- Create: src/EventBooking.Infrastructure/Persistence/EventStartInstants.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260920145721_RequireEventStartInstant.Designer.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260920145721_RequireEventStartInstant.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/EventBookingDbContextModelSnapshot.cs
- Create: src/EventBooking.Infrastructure/Persistence/Queries/EventEligibilityQuery.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs
- Modify: src/EventBooking.SeedData/DemoSeeder.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/CancelAttendeeBookingHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/CancelBookingHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/ConfirmBookingHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/InviteOptionReplacementTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/InviteSnapshotAuthorityTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/ViewInviteHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/EventCancellationConcurrencyTests.cs
- Modify: tests/EventBooking.Application.Tests/Fakes/InMemoryRepositories.cs
- Modify: tests/EventBooking.Application.Tests/Invites/EligibleEventFinderTests.cs
- Modify: tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Invites/InviteIssuerTests.cs
- Modify: tests/EventBooking.Application.Tests/Invites/RecoveryInviteHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Invites/TriggerInviteHandlerTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/EventPersistenceTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/PostgresFixture.cs
- Test: tests/EventBooking.Infrastructure.Tests/Queries/EventEligibilityPerformanceTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/Queries/EventEligibilityQueryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/RecoveryConcurrencyTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/SchemaTests.cs

**Interfaces:**

These complete types define the changed public boundary and its domain behavior. Apply them with the after files at Step 3, not before the failing test.

```csharp
namespace EventBooking.Application.Abstractions;

/// <summary>
/// Which events a attendee may be offered. Relational division: an event qualifies only if it
/// covers every required appointment type with at least one place left, and an event that does not
/// list a required type at all is never a candidate however much room its other types have.
///
/// The rule is executed in the database, in one statement, because the alternative is loading
/// every active event and its capacity rows into memory to filter them there (design 04 — invite
/// selection). The query only proposes candidates: capacity is re-checked under lock at booking
/// time, so a stale option can never overbook.
/// </summary>
public interface IEventEligibilityQuery
{
    /// <summary>
    /// The eligible events, earliest first by start instant and then by identifier, at most
    /// <paramref name="count"/> of them.
    /// </summary>
    /// <param name="requiredAppointmentTypeIds">Every type the attendee needs; duplicates collapse.</param>
    /// <param name="locationIds">The locations the caller will offer; an event elsewhere is not a candidate.</param>
    /// <param name="excludeEventIds">Events the caller has already offered or ruled out.</param>
    /// <param name="count">The most identifiers to return.</param>
    /// <param name="asOf">The instant to judge "still to come" against.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Guid>> FindEligibleEventsAsync(
        IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
        IReadOnlyCollection<Guid> locationIds,
        IReadOnlyCollection<Guid> excludeEventIds,
        int count,
        DateTimeOffset asOf,
        CancellationToken cancellationToken);

    /// <summary>
    /// How many events the same filters match, with no limit. The invite dialog shows the number
    /// before it shows the options, and counting in the database avoids fetching rows to discard.
    /// </summary>
    /// <param name="requiredAppointmentTypeIds">Every type the attendee needs; duplicates collapse.</param>
    /// <param name="locationIds">The locations the caller will offer.</param>
    /// <param name="excludeEventIds">Events the caller has already offered or ruled out.</param>
    /// <param name="asOf">The instant to judge "still to come" against.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<int> CountEligibleEventsAsync(
        IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
        IReadOnlyCollection<Guid> locationIds,
        IReadOnlyCollection<Guid> excludeEventIds,
        DateTimeOffset asOf,
        CancellationToken cancellationToken);
}
```

```csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Events;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Invites;

/// <summary>
/// The one place the "which events may a attendee be offered" rule is reached from. The rule
/// itself lives in the database from Task 11 onward: this asks the eligibility port for ordered
/// identifiers and hydrates them, so an invite and a single replacement option are chosen by the
/// same statement.
/// </summary>
/// <param name="eligibility">The eligibility query.</param>
/// <param name="events">The events.</param>
/// <param name="clock">The clock.</param>
public sealed class EligibleEventFinder(
    IEventEligibilityQuery eligibility,
    IEventRepository events,
    IClock clock)
{
    /// <summary>Defines find async for the current use case.</summary>
    /// <param name="requiredAppointmentTypeIds">The required appointment type ids.</param>
    /// <param name="take">The take.</param>
    /// <param name="excludeEventIds">The exclude event ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<IReadOnlyList<Event>> FindAsync(
        IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
        int take,
        IReadOnlyCollection<Guid> excludeEventIds,
        CancellationToken cancellationToken)
    {
        var ids = await eligibility.FindEligibleEventsAsync(
            requiredAppointmentTypeIds,
            Locations,
            excludeEventIds,
            take,
            clock.UtcNow,
            cancellationToken);

        if (ids.Count == 0)
        {
            return [];
        }

        var loaded = (await events.ListByIdsAsync(ids, cancellationToken))
            .ToDictionary(eventItem => eventItem.Id);

        // The query decided the order; hydrating must not quietly re-impose another one.
        return [.. ids.Where(loaded.ContainsKey).Select(id => loaded[id])];
    }

    /// <summary>How many events the attendee could be offered, ignoring any option limit.</summary>
    /// <param name="requiredAppointmentTypeIds">The required appointment type ids.</param>
    /// <param name="excludeEventIds">The exclude event ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task<int> CountAsync(
        IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
        IReadOnlyCollection<Guid> excludeEventIds,
        CancellationToken cancellationToken) =>
        eligibility.CountEligibleEventsAsync(
            requiredAppointmentTypeIds,
            Locations,
            excludeEventIds,
            clock.UtcNow,
            cancellationToken);

    // Invites are restricted to the transitional location until Task 14, whose InviteAttendee
    // command carries the Coordinator's own selection of locations.
    private static IReadOnlyCollection<Guid> Locations => [TransitionalLocation.Id];
}
```

```csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace EventBooking.Infrastructure.Persistence.Queries;

/// <summary>
/// Relational division, executed in PostgreSQL in one statement (design 04 — invite selection).
/// The join keeps only capacity rows for a required type with a place left; the group then has as
/// many rows as the event covers required types, and the HAVING clause keeps only the events that
/// cover all of them. An event that does not list a required type simply contributes fewer rows.
/// </summary>
/// <param name="context">The context whose connection the statement runs on.</param>
public sealed class EventEligibilityQuery(EventBookingDbContext context) : IEventEligibilityQuery
{
    private const string Body =
        """
          FROM event e
          JOIN location l ON l.id = e.location_id
          JOIN event_capacity c ON c.event_id = e.id
                               AND c.appointment_type_id = ANY(@requiredTypeIds)
                               AND c.remaining_capacity >= 1
         WHERE e.status = @activeStatus
           AND e.location_id = ANY(@locationIds)
           AND e.start_utc > @asOf
           AND e.id <> ALL(@excludeEventIds)
         GROUP BY e.id, e.start_utc
        HAVING COUNT(*) = cardinality(@requiredTypeIds)
        """;

    /// <summary>
    /// The listing statement, exposed so the performance test can explain the statement the query
    /// actually issues rather than a paraphrase of it that could drift away from this one.
    /// </summary>
    public const string ListSql =
        "SELECT e.id\n" + Body + "\n ORDER BY e.start_utc, e.id\n LIMIT @count";

    /// <summary>The counting statement: the same division, wrapped so the groups are counted.</summary>
    public const string CountSql =
        "SELECT COUNT(*)::int FROM (SELECT e.id\n" + Body + "\n) AS eligible";

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> FindEligibleEventsAsync(
        IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
        IReadOnlyCollection<Guid> locationIds,
        IReadOnlyCollection<Guid> excludeEventIds,
        int count,
        DateTimeOffset asOf,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        if (IsUnsatisfiable(requiredAppointmentTypeIds, locationIds))
        {
            return [];
        }

        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = Command(
                ListSql, requiredAppointmentTypeIds, locationIds, excludeEventIds, asOf);
            command.Parameters.Add(new NpgsqlParameter("count", count));

            var ids = new List<Guid>(count);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(reader.GetGuid(0));
            }

            return ids;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    /// <inheritdoc />
    public async Task<int> CountEligibleEventsAsync(
        IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
        IReadOnlyCollection<Guid> locationIds,
        IReadOnlyCollection<Guid> excludeEventIds,
        DateTimeOffset asOf,
        CancellationToken cancellationToken)
    {
        if (IsUnsatisfiable(requiredAppointmentTypeIds, locationIds))
        {
            return 0;
        }

        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = Command(
                CountSql, requiredAppointmentTypeIds, locationIds, excludeEventIds, asOf);

            return (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    /// <summary>
    /// Naming no required type, or no location, cannot be satisfied by a division: the join has
    /// nothing to keep. Answering it without a round trip says so plainly, rather than relying on
    /// an empty array parameter to mean the same thing by accident.
    /// </summary>
    private static bool IsUnsatisfiable(
        IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
        IReadOnlyCollection<Guid> locationIds)
    {
        ArgumentNullException.ThrowIfNull(requiredAppointmentTypeIds);
        ArgumentNullException.ThrowIfNull(locationIds);

        return requiredAppointmentTypeIds.Count == 0 || locationIds.Count == 0;
    }

    private NpgsqlCommand Command(
        string sql,
        IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
        IReadOnlyCollection<Guid> locationIds,
        IReadOnlyCollection<Guid> excludeEventIds,
        DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(excludeEventIds);

        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        var command = new NpgsqlCommand(sql, connection);

        // The query is a read inside whatever transaction the handler opened; without this the
        // driver refuses to run it on a connection that is already in one.
        command.Transaction =
            context.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction;

        // Duplicates would make cardinality larger than the group can ever count, so the division
        // would reject every event.
        command.Parameters.Add(Uuids("requiredTypeIds", requiredAppointmentTypeIds.Distinct()));
        command.Parameters.Add(Uuids("locationIds", locationIds.Distinct()));
        command.Parameters.Add(Uuids("excludeEventIds", excludeEventIds.Distinct()));
        command.Parameters.Add(new NpgsqlParameter("activeStatus", (int)EventStatus.Active));
        command.Parameters.Add(
            new NpgsqlParameter("asOf", NpgsqlDbType.TimestampTz) { Value = asOf });

        return command;
    }

    private static NpgsqlParameter Uuids(string name, IEnumerable<Guid> values) =>
        new(name, NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = values.ToArray() };
}
```

```csharp
using EventBooking.Domain.Events;
using EventBooking.Domain.Time;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence;

/// <summary>
/// Writes <c>start_utc</c>, the derived persistence column the eligibility query orders and
/// indexes on. It is not domain data: the window and the location's zone are the source of truth,
/// and this is the one function that turns them into an instant (design 04 — invite selection).
///
/// PostgreSQL cannot evaluate IANA rules in a generated column deterministically, so the value has
/// to be written by whoever inserts the row. The repository writes it as it adds the event, and
/// the context writes it for any other writer at save time, so the column cannot be left empty by
/// a path that has not heard of the rule.
/// </summary>
public static class EventStartInstants
{
    /// <summary>The shadow property that carries the column.</summary>
    public const string PropertyName = "StartUtc";

    /// <summary>Computes and stamps one tracked event's start instant.</summary>
    /// <param name="context">The context tracking the event.</param>
    /// <param name="eventItem">The event.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    /// <param name="zones">The zone abstraction.</param>
    public static void Stamp(
        DbContext context,
        Event eventItem,
        string timeZoneId,
        IEventWindowZones zones)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(eventItem);

        context.Entry(eventItem).Property<DateTimeOffset?>(PropertyName).CurrentValue =
            InstantOf(eventItem, timeZoneId, zones);
    }

    /// <summary>
    /// Fills in the start instant for every event being inserted that has not had one computed.
    /// The seeder and the suites add events straight through the context; this is what keeps the
    /// column's promise for them without each of them restating the rule.
    /// </summary>
    /// <param name="context">The context about to save.</param>
    /// <param name="zones">The zone abstraction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task StampPendingAsync(
        EventBookingDbContext context,
        IEventWindowZones zones,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var pending = context.ChangeTracker.Entries<Event>()
            .Where(entry => entry.State == EntityState.Added)
            .Where(entry => entry.Property<DateTimeOffset?>(PropertyName).CurrentValue is null)
            .ToList();

        if (pending.Count == 0)
        {
            return;
        }

        var locationIds = pending.Select(entry => entry.Entity.LocationId).Distinct().ToArray();
        var zonesByLocation = await context.Locations
            .Where(location => locationIds.Contains(location.Id))
            .Select(location => new { location.Id, location.TimeZoneId })
            .ToDictionaryAsync(row => row.Id, row => row.TimeZoneId, cancellationToken);

        foreach (var entry in pending)
        {
            var locationId = entry.Entity.LocationId;
            zonesByLocation.TryGetValue(locationId, out var timeZoneId);

            entry.Property<DateTimeOffset?>(PropertyName).CurrentValue =
                InstantOf(entry.Entity, Required(timeZoneId, locationId), zones);
        }
    }

    /// <summary>
    /// The window's start, as an instant at UTC. The resolver answers with the location's own
    /// offset, and the column stores an instant with no offset of its own, so the value is
    /// normalised here rather than at each caller — PostgreSQL refuses any other offset outright.
    /// </summary>
    /// <param name="eventItem">The event.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    /// <param name="zones">The zone abstraction.</param>
    private static DateTimeOffset InstantOf(
        Event eventItem, string timeZoneId, IEventWindowZones zones) =>
        eventItem.Window.StartInstant(zones, timeZoneId).ToUniversalTime();

    /// <summary>The zone an event's location must have, refusing a location that is not persisted.</summary>
    /// <param name="timeZoneId">The zone read for that location, or null if there was no row.</param>
    /// <param name="locationId">The location the event is at.</param>
    public static string Required(string? timeZoneId, Guid locationId) =>
        timeZoneId
        ?? throw new InvalidOperationException(
            $"Location {locationId} has no persisted row, so the event's start instant cannot be "
            + "computed. Seed the location before the event.");
}
```

**Context you need**

- Master plan Task 11: the port operation FindEligibleEvents (required type ids, location ids, excluded event ids, count, as-of instant) returns ordered event ids, and CountEligibleEvents takes the same filters minus the count, for the invite dialog. One SQL statement each.
- Master plan Task 11: the ported eligible-slot finder moves to the infrastructure query behind the Application port, and the `Event` repository writes `start_utc` in the same transaction as the insert, computed with the Task 4 resolver.
- Design 04 — invite selection gives the statement itself, and says why `start_utc` is a derived persistence column the application writes rather than a generated one: PostgreSQL cannot evaluate IANA rules in a generated column deterministically. It is safe because a `Location`'s zone cannot change while the site has future `Event`s (FR-1.2).
- Design 04 — invite selection: the query only proposes candidates. Capacity is re-checked under lock at booking time, so a stale option can never overbook.
- Task 9b left `start_utc` nullable on purpose, because nothing computed it yet and a non-nullable column takes a silent 0001-01-01 for every row — which the eligibility query would read as an `Event` that has already started, hiding it rather than failing.
- Task 9b seeds the transitional `Location` in the model. Every derived start instant is read from its `Location`, so any path that empties the table and re-seeds has to put that row back: the test fixture and the demo reseeder both did not, and both do now.
- The master plan writes the three appointment types as MED, FIT and IND. The prototype carries the predecessor’s three seeded types until Phase 3, so the plan’s codes map onto those: MED is the medical check-up, FIT the uniform fitting and IND the drug and alcohol testing.
- Invites are restricted to the transitional `Location` until Task 14, whose InviteAttendee command carries the Coordinator’s own selection, so the adapter passes that one identifier for now.
- NFR-P2 is the invite dialog’s budget: p95 below 50 ms over 100 runs, with the (status, location id, start instant) index proved by EXPLAIN.

- [ ] **Step 1: Write the failing tests**

Create these complete files before applying production changes.

tests/EventBooking.Infrastructure.Tests/Queries/EventEligibilityQueryTests.cs

```csharp
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
```

tests/EventBooking.Infrastructure.Tests/Queries/EventEligibilityPerformanceTests.cs

```csharp
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
```

tests/EventBooking.Application.Tests/Invites/EligibleEventFinderTests.cs

```csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Events;
using EventBooking.Application.Invites;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;

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
```

- [ ] **Step 2: Verify the tests fail**

```bash
dotnet test tests/EventBooking.Infrastructure.Tests --filter FullyQualifiedName~EventEligibilityQueryTests
dotnet test tests/EventBooking.Infrastructure.Tests --filter FullyQualifiedName~EventEligibilityPerformanceTests
dotnet test tests/EventBooking.Application.Tests --filter FullyQualifiedName~EligibleEventFinderTests
```

Expected: the suite does not compile. The eligibility port, its infrastructure implementation and the repository’s asynchronous add do not exist yet, so the new query suite, the in-memory fake and the recording repository in the recovery concurrency suite all fail to build. A Docker startup failure is not the intended failure.

- [ ] **Step 3: Apply the exact implementation and regression edits**

The 16 phase-2d-edits-NNN.md files supply 36 complete before/after changes. The script validates every payload and current file before writing. It accepts an already-applied after state, refuses unrelated edits, and only deletes explicitly listed files whose before hash matches.

```bash
node --input-type=module <<'TASK_PAYLOAD'
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
const root=fs.realpathSync('.'), plan='docs/detailed-implementations';
const sha=s=>crypto.createHash('sha256').update(s).digest('hex');
const names=fs.readdirSync(plan).filter(n=>n.startsWith('phase-2d-edits-')&&n.endsWith('.md')).sort();
if(names.length!==16)throw Error('Incomplete edit volumes.');
const entries=new Map();
for(const name of names){
 const text=fs.readFileSync(path.join(plan,name),'utf8');
 const pattern=/<!-- retirement-file: (.+) -->\n\n`{5}[^\n]*\n([\s\S]*?)\n`{5}/g;
 for(const match of text.matchAll(pattern)){
  const m=JSON.parse(match[1]);
  if(path.isAbsolute(m.file)||m.file.split('/').includes('..'))throw Error('Unsafe path.');
  const e=entries.get(m.id)??{...m,before:new Map(),after:new Map(),counts:{}};
  if(e.file!==m.file||e.beforeSha!==m.beforeSha||e.afterSha!==m.afterSha||e[m.side].has(m.part))throw Error('Conflicting metadata.');
  e[m.side].set(m.part,match[2]+'\n');e.counts[m.side]=m.parts;entries.set(m.id,e);
 }
}
if(entries.size!==36)throw Error('Incomplete operation set.');
const actions=[];
for(const e of entries.values()){
 for(const side of ['before','after']){
  if(e[side+'Sha']===null)continue;
  if(e[side].size!==e.counts[side])throw Error('Missing parts.');
  const parts=Array.from({length:e.counts[side]},(_,i)=>e[side].get(i+1));
  if(parts.some(p=>p===undefined))throw Error('Missing part number.');
  e[side+'Text']=parts.join('');
  if(sha(e[side+'Text'])!==e[side+'Sha'])throw Error('Payload checksum mismatch.');
 }
 const target=path.join(root,e.file);
 let parent=path.dirname(target);while(!fs.existsSync(parent))parent=path.dirname(parent);
 const resolved=fs.realpathSync(parent);
 if(resolved!==root&&!resolved.startsWith(root+path.sep))throw Error('Parent escapes checkout.');
 if(fs.existsSync(target)&&fs.lstatSync(target).isSymbolicLink())throw Error('Symlink target.');
 const actual=fs.existsSync(target)?sha(fs.readFileSync(target)):null;
 if(actual!==e.beforeSha&&actual!==e.afterSha)throw Error('Unrelated edit: '+e.file);
 actions.push({target,body:e.afterText,remove:e.afterSha===null});
}
for(const action of actions){
 if(action.remove){if(fs.existsSync(action.target))fs.unlinkSync(action.target);}
 else{fs.mkdirSync(path.dirname(action.target),{recursive:true});fs.writeFileSync(action.target,action.body);}
}
console.log('Applied '+actions.length+' verified file changes.');
TASK_PAYLOAD
```

The included migration, designer and model snapshot were generated with this exact command, and are already represented in the supplied after files. Do not generate a duplicate migration:

```bash
dotnet ef migrations add RequireEventStartInstant --project src/EventBooking.Infrastructure --startup-project src/EventBooking.Api
```

Review the complete migration in the edit volumes before running database-dependent tests.

- [ ] **Step 4: Verify the targeted tests pass**

```bash
dotnet test tests/EventBooking.Infrastructure.Tests --filter FullyQualifiedName~EventEligibilityQueryTests
dotnet test tests/EventBooking.Infrastructure.Tests --filter FullyQualifiedName~EventEligibilityPerformanceTests
dotnet test tests/EventBooking.Application.Tests --filter FullyQualifiedName~EligibleEventFinderTests
```

Expected: all targeted cases pass, with zero skipped tests.

- [ ] **Step 5: Build and validate the complete solution**

```bash
dotnet build EventBooking.sln -warnaserror
dotnet test EventBooking.sln
```

Expected: zero warnings, zero errors and zero failed or skipped tests. The verified checkpoint contains 1570 tests: Domain 360, Application 421, Infrastructure 206, API 232, MCP 35, Web 241 and SeedData 75.

- [ ] **Step 6: Commit and push**

No ontology change belongs to this task. `start_utc` is a derived persistence column, not domain data — design 04 says so explicitly, and `docs/ontology.ttl` gives `Event` its window, not an instant. If you find a concept that is genuinely missing, edit the source and regenerate before committing.

```bash
git add -- \
  'src/EventBooking.Application/Abstractions/IEventEligibilityQuery.cs' \
  'src/EventBooking.Application/Abstractions/IEventRepository.cs' \
  'src/EventBooking.Application/Events/AcceptProposalHandler.cs' \
  'src/EventBooking.Application/Invites/EligibleEventFinder.cs' \
  'src/EventBooking.Infrastructure/DependencyInjection.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/EventBookingDbContext.cs' \
  'src/EventBooking.Infrastructure/Persistence/EventStartInstants.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920145721_RequireEventStartInstant.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920145721_RequireEventStartInstant.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/EventBookingDbContextModelSnapshot.cs' \
  'src/EventBooking.Infrastructure/Persistence/Queries/EventEligibilityQuery.cs' \
  'src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs' \
  'src/EventBooking.SeedData/DemoSeeder.cs' \
  'tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/CancelAttendeeBookingHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/CancelBookingHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/ConfirmBookingHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/InviteOptionReplacementTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/InviteSnapshotAuthorityTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/ViewInviteHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/EventCancellationConcurrencyTests.cs' \
  'tests/EventBooking.Application.Tests/Fakes/InMemoryRepositories.cs' \
  'tests/EventBooking.Application.Tests/Invites/EligibleEventFinderTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/InviteIssuerTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/RecoveryInviteHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/TriggerInviteHandlerTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/EventPersistenceTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/PostgresFixture.cs' \
  'tests/EventBooking.Infrastructure.Tests/Queries/EventEligibilityPerformanceTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/Queries/EventEligibilityQueryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/RecoveryConcurrencyTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/SchemaTests.cs'
git diff --cached --name-only
git diff --cached
node scripts/check-ontology-terms.mjs
: "${EXECUTOR_COAUTHOR:?Set the executing harness co-author identity}"
git commit -m "feat(persistence): relational-division invite eligibility query" -m "Co-authored-by: $EXECUTOR_COAUTHOR"
git push -u origin HEAD
```

That is the last task of Phase 2. Open the phase pull request as [phase-2-persistence.md](phase-2-persistence.md#pull-request) describes: the `narrative-required` label, the three narrative headings and the AI-Fingerprint footer, all carried in the body you supply.
