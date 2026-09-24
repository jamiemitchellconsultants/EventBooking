using System.Globalization;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Audit;
using EventBooking.Application.ReadModels;
using EventBooking.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace EventBooking.Infrastructure.Persistence.Queries;

/// <summary>
/// Bucketed audit search in one statement per page.
///
/// The column names here are this schema's, read from AuditLogConfiguration rather than
/// assumed: the instant is `timestamp` (quoted, because it is also a type name), while
/// `action` and `actor_type` are the domain enums stored through HasConversion&lt;int&gt;
/// and are therefore read as integers and named in .NET. The keyset index this paging
/// needs already exists — `ix_audit_log_timestamp` on (`timestamp`, `id`), created by the
/// Task 9b initial schema — so this task adds no migration.
/// </summary>
/// <param name="context">The read-only persistence context.</param>
public sealed class AuditSearchQueries(EventBookingDbContext context) : IAuditSearchQueries
{
    /// <summary>
    /// The bucket is derived in SQL and never taken from the caller, so a staff member
    /// holding only one audit capability cannot reach the other bucket's rows by naming
    /// an entity type in a filter. An entity type in neither list yields NULL and matches
    /// no bucket: a type added later is invisible until it is classified here, rather
    /// than defaulting into `event` and leaking.
    /// </summary>
    private const string BucketCase =
        """
        CASE
            WHEN a.entity_type IN ('Attendee', 'Invite', 'Booking') THEN 'attendee'
            WHEN a.entity_type IN ('Location', 'AppointmentType', 'AttendeeGroup',
                                   'SystemSettings', 'EventProposal', 'Event',
                                   'BookingAppointment', 'StaffAccessProfile') THEN 'event'
        END
        """;

    // Newest first: an audit trail is read backwards from the most recent change. The
    // index serves either direction, so this is a product choice, not a planner one.
    private static readonly string PageSql =
        $"""
         SELECT a.id, a.entity_type, a.action, a.actor_type, a."timestamp"
           FROM audit_log a
          WHERE ({BucketCase}) = ANY(@buckets)
            AND (@entityType IS NULL OR a.entity_type = @entityType)
            AND (@action IS NULL OR a.action = @action)
            AND (@from IS NULL OR a."timestamp" >= @from)
            AND (@to IS NULL OR a."timestamp" <= @to)
            AND (@entityId IS NULL OR a.entity_id = @entityId)
            AND (@cursorAt IS NULL
                 OR a."timestamp" < @cursorAt
                 OR (a."timestamp" = @cursorAt AND a.id < @cursorId))
          ORDER BY a."timestamp" DESC, a.id DESC
          LIMIT @limit
         """;

    /// <inheritdoc />
    public Task<AuditSearchView> SearchAsync(
        CallerShape shape, IReadOnlyList<string> buckets, string? cursor, int limit,
        string? entityType, string? action, DateTimeOffset? from, DateTimeOffset? to,
        Guid? entityId, CancellationToken ct) =>
        PageAsync(buckets, cursor, limit, entityType, action, from, to, entityId, ct);

    /// <inheritdoc />
    public Task<AuditSearchView> HistoryAsync(
        CallerShape shape, IReadOnlyList<string> buckets, Guid entityId,
        string? cursor, int limit, CancellationToken ct) =>
        PageAsync(buckets, cursor, limit, null, null, null, null, entityId, ct);

    // `shape` is part of the port because the handler holds it and Task 21 signs the
    // cursor with it. It deliberately does not widen or narrow the buckets here: the
    // capability check is the handler's, and duplicating it would give two places to
    // get the same rule wrong.
    private async Task<AuditSearchView> PageAsync(
        IReadOnlyList<string> buckets, string? cursor, int limit, string? entityType,
        string? action, DateTimeOffset? from, DateTimeOffset? to, Guid? entityId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(buckets);

        // No capability, no rows. The handler refuses this first; the query does not
        // rely on that, because an empty bucket set must never mean "everything".
        if (buckets.Count == 0)
        {
            return new AuditSearchView([], null);
        }

        // A filter naming an action this system does not emit matches nothing. That is
        // an empty page, not an error: the caller asked a well-formed question.
        int? actionValue = null;
        if (action is not null)
        {
            if (!Enum.TryParse<AuditAction>(action, ignoreCase: true, out var parsed))
            {
                return new AuditSearchView([], null);
            }

            actionValue = (int)parsed;
        }

        DateTimeOffset? cursorAt = null;
        Guid? cursorId = null;
        if (AttendeeCursor.TryDecode(cursor, out var sortKey, out var decodedId)
            && DateTimeOffset.TryParse(
                sortKey, null, DateTimeStyles.RoundtripKind, out var decodedAt))
        {
            cursorAt = decodedAt.ToUniversalTime();
            cursorId = decodedId;
        }

        await context.Database.OpenConnectionAsync(ct);
        try
        {
            var connection = (NpgsqlConnection)context.Database.GetDbConnection();
            await using var command = new NpgsqlCommand(PageSql, connection);
            command.Transaction =
                context.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction;

            command.Parameters.Add(
                new NpgsqlParameter("buckets", NpgsqlDbType.Array | NpgsqlDbType.Text)
                { Value = buckets.ToArray() });
            Add(command, "entityType", NpgsqlDbType.Text, entityType);
            Add(command, "action", NpgsqlDbType.Integer, actionValue);
            Add(command, "from", NpgsqlDbType.TimestampTz, from?.ToUniversalTime());
            Add(command, "to", NpgsqlDbType.TimestampTz, to?.ToUniversalTime());
            Add(command, "entityId", NpgsqlDbType.Uuid, entityId);
            Add(command, "cursorAt", NpgsqlDbType.TimestampTz, cursorAt);
            Add(command, "cursorId", NpgsqlDbType.Uuid, cursorId);
            // One more than asked for, so the presence of a next page is known without
            // a second count query.
            command.Parameters.Add(new NpgsqlParameter("limit", limit + 1));

            var rows = new List<AuditRow>(limit + 1);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var id = reader.GetGuid(0);
                var occurredAt = reader.GetFieldValue<DateTimeOffset>(4);
                rows.Add(new AuditRow(
                    id,
                    reader.GetString(1),
                    ((AuditAction)reader.GetInt32(2)).ToString(),
                    ((ActorType)reader.GetInt32(3)).ToString(),
                    occurredAt,
                    AttendeeCursor.Encode(
                        occurredAt.ToUniversalTime().ToString("o"), id)));
            }

            if (rows.Count <= limit)
            {
                return new AuditSearchView(rows, null);
            }

            var page = rows.Take(limit).ToList();
            return new AuditSearchView(page, page[^1].Cursor);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static void Add(
        NpgsqlCommand command, string name, NpgsqlDbType type, object? value) =>
        command.Parameters.Add(
            new NpgsqlParameter(name, type) { Value = value ?? DBNull.Value });
}
