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
/// query stays under 150 ms at p95, and it reaches those events through the eligibility index
/// rather than by reading the whole table.
///
/// The two assertions are not two ways of saying the same thing, and it is worth knowing which
/// one holds what. Dropping the index turns the plan's bitmap scan into a sequential one, which
/// the first test catches and the budget does not: at this size the scan of the event table is
/// not what the 150 ms is spent on. What the budget catches is the shape this task replaced —
/// loading every active event with its capacity rows and dividing in memory takes some 680 ms on
/// the same data, more than four times the budget.
///
/// The budget is calibrated to the hardware that enforces it. The same correct implementation
/// measures 19 ms isolated locally and 64-75 ms on the project's shared CI runners; parallel
/// load from the rest of the suite was ruled out (18.9 ms in-suite versus 19.1 ms isolated),
/// so the gap is the runners themselves. 150 ms is twice the slowest CI observation and still
/// leaves the in-memory shape no place to hide.
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
    private const int BudgetMilliseconds = 150;

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
