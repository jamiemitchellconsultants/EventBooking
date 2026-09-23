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
