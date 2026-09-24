using EventBooking.Application.Abstractions;
using EventBooking.Application.Attendees;
using EventBooking.Application.ReadModels;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace EventBooking.Infrastructure.Persistence.Queries;

/// <summary>
/// One statement per page over the attendee set, keyset-ordered on (lower(name), id). The
/// Task 20a migration indexes (status, name, id), which serves the status filter and the
/// keyset tiebreak as an index-only scan; the shortlist sort by lower(name) is trivial.
/// The column names are this schema's: `status` on both attendee and email_log is the
/// enum stored through HasConversion&lt;int&gt;, so both are compared and read as integers.
/// </summary>
/// <param name="context">The read-only persistence context.</param>
public sealed class AttendeeListQueries(EventBookingDbContext context) : IAttendeeListQueries
{
    // The required type codes are projected only while no invite snapshot exists —
    // not-yet-invited or awaiting availability. Once invited, the invite's own snapshot
    // is the authority, and showing the live requirement set beside such a row would
    // contradict it.
    //
    // The latest delivery status is the newest outbox row for the attendee, or null when
    // none exists. LEFT JOIN LATERAL keeps that to one row per attendee rather than a
    // group-by over the whole log.
    private const string PageSql =
        """
        SELECT c.id,
               c.name,
               c.email,
               c.status,
               g.code AS group_code,
               COALESCE(
                   CASE WHEN c.status IN (@notYetInvited, @awaiting) THEN (
                       SELECT array_agg(t.code ORDER BY t.code)
                         FROM attendee_requirement r
                         JOIN appointment_type t ON t.id = r.appointment_type_id
                        WHERE r.attendee_id = c.id)
                   END,
                   '{}'::text[]) AS required_codes,
               latest.status AS latest_delivery_status
          FROM attendee c
          JOIN attendee_group g ON g.id = c.attendee_group_id
          LEFT JOIN LATERAL (
              SELECT e.status
                FROM email_log e
               WHERE e.attendee_id = c.id
               ORDER BY e.sent_at DESC NULLS LAST, e.id DESC
               LIMIT 1) AS latest ON TRUE
         WHERE (@status IS NULL OR c.status = @status)
           AND (@groupId IS NULL OR c.attendee_group_id = @groupId)
           AND (@prefix IS NULL
                OR lower(c.name) LIKE @prefix || '%'
                OR lower(c.email) LIKE @prefix || '%')
           AND (@cursorName IS NULL
                OR lower(c.name) > @cursorName
                OR (lower(c.name) = @cursorName AND c.id > @cursorId))
         ORDER BY lower(c.name), c.id
         LIMIT @limit
        """;

    /// <inheritdoc />
    public async Task<AttendeeListView> ListAttendeesAsync(
        CallerShape shape, string? cursor, int limit, string? status,
        Guid? attendeeGroupId, string? readiness, string? nameOrEmailPrefix,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(shape);

        // The read model refuses the Admin shape itself. The handler checks for a
        // Coordinator profile, but an Admin who also holds one must still not read
        // attendee rows, and that rule belongs where the rows are.
        if (shape.IsAdmin)
        {
            return new AttendeeListView([], null);
        }

        // A filter naming a status this system does not have matches nothing. That is an
        // empty page, not an error.
        int? statusValue = null;
        if (status is not null)
        {
            if (!Enum.TryParse<AttendeeStatus>(status, ignoreCase: true, out var parsed))
            {
                return new AttendeeListView([], null);
            }

            statusValue = (int)parsed;
        }

        string? cursorName = null;
        Guid? cursorId = null;
        if (AttendeeCursor.TryDecode(cursor, out var sortKey, out var decodedId))
        {
            cursorName = sortKey;
            cursorId = decodedId;
        }

        await context.Database.OpenConnectionAsync(ct);
        try
        {
            var connection = (NpgsqlConnection)context.Database.GetDbConnection();
            await using var command = new NpgsqlCommand(PageSql, connection);
            command.Transaction =
                context.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction;

            Add(command, "status", NpgsqlDbType.Integer, statusValue);
            Add(command, "groupId", NpgsqlDbType.Uuid, attendeeGroupId);
            Add(command, "prefix", NpgsqlDbType.Text, nameOrEmailPrefix?.ToLowerInvariant());
            Add(command, "cursorName", NpgsqlDbType.Text, cursorName);
            Add(command, "cursorId", NpgsqlDbType.Uuid, cursorId);
            Add(command, "notYetInvited", NpgsqlDbType.Integer, (int)AttendeeStatus.NotYetInvited);
            Add(command, "awaiting", NpgsqlDbType.Integer, (int)AttendeeStatus.AwaitingAvailability);
            command.Parameters.Add(new NpgsqlParameter("limit", limit + 1));

            var read = new List<AttendeeListItem>(limit + 1);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var id = reader.GetGuid(0);
                var name = reader.GetString(1);
                var attendeeStatus = (AttendeeStatus)reader.GetInt32(3);
                var delivery = await reader.IsDBNullAsync(6, ct)
                    ? null
                    : ((EmailStatus)reader.GetInt32(6)).ToString();

                read.Add(new AttendeeListItem(
                    id,
                    name,
                    reader.GetString(2),
                    attendeeStatus.ToString(),
                    reader.GetString(4),
                    AttendeeReadiness.Of(attendeeStatus, delivery),
                    reader.GetFieldValue<string[]>(5),
                    delivery,
                    AttendeeCursor.Encode(name.ToLowerInvariant(), id)));
            }

            // Readiness is a derived label, not a column, so it cannot be filtered in
            // SQL without materialising it there too. Filtering the page after the fact
            // would silently shorten pages, so it is applied here and the keyset still
            // advances by the last row actually read.
            var matched = readiness is null
                ? read
                : read.Where(r => string.Equals(
                    r.Readiness, readiness, StringComparison.OrdinalIgnoreCase)).ToList();

            if (read.Count <= limit)
            {
                return new AttendeeListView(matched, null);
            }

            if (matched.Count > limit)
            {
                var page = matched.Take(limit).ToList();
                return new AttendeeListView(page, page[^1].Cursor);
            }

            return new AttendeeListView(
                matched, (matched.Count == 0 ? read[^1] : matched[^1]).Cursor);
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
