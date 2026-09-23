using System.Text;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Audit;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Queries;

/// <summary>Read-side audit queries over the append-only audit log.</summary>
public sealed class AuditQueries(EventBookingDbContext context) : IAuditQueries
{
    private const int MaxPageSize = 200;

    /// <summary>Entries for one audited entity, newest first.</summary>
    public async Task<IReadOnlyList<AuditHistoryRow>> ForEntityAsync(
        string entityType,
        Guid entityId,
        CancellationToken cancellationToken) =>
        await context.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.Timestamp)
            .Select(a => new AuditHistoryRow(
                a.Timestamp,
                a.EntityType,
                a.EntityId,
                a.Action.ToString(),
                a.ActorType.ToString(),
                a.ActorId,
                a.Details))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Every entry recorded against the candidate record itself and against their invites, bookings,
    /// and booking appointments, newest first.
    /// </summary>
    public async Task<IReadOnlyList<AuditHistoryRow>> ForCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        var inviteIds = context.Invites
            .Where(i => i.CandidateId == candidateId)
            .Select(i => i.Id);

        var bookingIds = context.Bookings
            .Where(b => b.CandidateId == candidateId)
            .Select(b => b.Id);

        var bookingAppointmentIds = context.BookingAppointments
            .Where(a => bookingIds.Contains(a.BookingId))
            .Select(a => a.Id);

        return await context.AuditLogs
            .AsNoTracking()
            .Where(a => (a.EntityType == AuditEntityTypes.Candidate && a.EntityId == candidateId)
                || inviteIds.Contains(a.EntityId)
                || bookingIds.Contains(a.EntityId)
                || bookingAppointmentIds.Contains(a.EntityId))
            .OrderByDescending(a => a.Timestamp)
            .Select(a => new AuditHistoryRow(
                a.Timestamp,
                a.EntityType,
                a.EntityId,
                a.Action.ToString(),
                a.ActorType.ToString(),
                a.ActorId,
                a.Details))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Cross-cutting newest-first keyset-paginated search; malformed cursors restart from newest.</summary>
    public async Task<AuditSearchPage> SearchAsync(
        AuditSearchFilter filter,
        CancellationToken cancellationToken)
    {
        if (filter.AllowedEntityTypes.Count == 0)
        {
            return new AuditSearchPage([], null);
        }

        var pageSize = filter.PageSize <= 0 ? 50 : Math.Min(filter.PageSize, MaxPageSize);
        var allowed = filter.AllowedEntityTypes.ToList();
        var query = context.AuditLogs
            .AsNoTracking()
            .Where(a => allowed.Contains(a.EntityType));

        if (filter.EntityType is not null)
        {
            query = query.Where(a => a.EntityType == filter.EntityType);
        }

        if (filter.From is not null)
        {
            query = query.Where(a => a.Timestamp >= filter.From);
        }

        if (filter.To is not null)
        {
            query = query.Where(a => a.Timestamp <= filter.To);
        }

        if (filter.ActorType is not null)
        {
            query = query.Where(a => a.ActorType.ToString() == filter.ActorType);
        }

        if (filter.Action is not null)
        {
            query = query.Where(a => a.Action.ToString() == filter.Action);
        }

        if (filter.Identifier is not null)
        {
            query = Guid.TryParse(filter.Identifier, out var identifierGuid)
                ? query.Where(a => a.EntityId == identifierGuid || a.ActorId == filter.Identifier)
                : query.Where(a => a.ActorId == filter.Identifier);
        }

        if (TryDecodeCursor(filter.Cursor, out var cursorTimestamp, out var cursorId))
        {
            query = query.Where(a => a.Timestamp < cursorTimestamp
                || (a.Timestamp == cursorTimestamp && a.Id.CompareTo(cursorId) < 0));
        }

        var rows = await query
            .OrderByDescending(a => a.Timestamp)
            .ThenByDescending(a => a.Id)
            .Take(pageSize + 1)
            .Select(a => new
            {
                a.Id,
                a.Timestamp,
                Row = new AuditHistoryRow(
                    a.Timestamp,
                    a.EntityType,
                    a.EntityId,
                    a.Action.ToString(),
                    a.ActorType.ToString(),
                    a.ActorId,
                    a.Details),
            })
            .ToListAsync(cancellationToken);

        var page = rows.Take(pageSize).ToList();
        var nextCursor = rows.Count > pageSize && page.Count > 0
            ? EncodeCursor(page[^1].Timestamp, page[^1].Id)
            : null;

        return new AuditSearchPage([.. page.Select(r => r.Row)], nextCursor);
    }

    private static string EncodeCursor(DateTimeOffset timestamp, Guid id) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{timestamp.UtcTicks}|{id}"));

    private static bool TryDecodeCursor(string? cursor, out DateTimeOffset timestamp, out Guid id)
    {
        timestamp = default;
        id = Guid.Empty;
        if (string.IsNullOrEmpty(cursor))
        {
            return false;
        }

        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('|');
            if (parts.Length != 2 || !long.TryParse(parts[0], out var ticks) || !Guid.TryParse(parts[1], out id))
            {
                return false;
            }

            timestamp = new DateTimeOffset(ticks, TimeSpan.Zero);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
