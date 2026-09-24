using EventBooking.Application.Events;
using EventBooking.Application.Negotiation;
using EventBooking.Application.ReadModels;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Queries;

/// <summary>
/// The Event read side. The scope is applied here and not only in the handler, so a caller
/// that reached this query without a capability check still sees only their own type's
/// capacity — the shape Task 20b settled for the audit buckets.
/// </summary>
/// <param name="context">The database context.</param>
public sealed class EventReadQueries(EventBookingDbContext context) : IEventReadQueries
{
    /// <summary>The sort key format: ordinal comparison must match instant comparison.</summary>
    private const string SortKeyFormat = "yyyy-MM-ddTHH:mm:ss.fffffffZ";

    /// <inheritdoc />
    public async Task<IReadOnlyList<EventView>> ListAsync(
        ListEventsQuery query, EventScope scope, DateTimeOffset now, bool notStartedOnly,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(scope);

        var events = Filtered(scope)
            .Where(e => query.LocationId == null || e.LocationId == query.LocationId)
            .Where(e => query.From == null || e.Window.Date >= query.From)
            .Where(e => query.To == null || e.Window.Date <= query.To)
            .Where(e => query.AppointmentTypeId == null ||
                context.EventCapacities.Any(c =>
                    c.EventId == e.Id && c.AppointmentTypeId == query.AppointmentTypeId));

        if (notStartedOnly)
        {
            events = events.Where(e => e.Status == EventStatus.Active &&
                EF.Property<DateTimeOffset>(e, EventStartInstants.PropertyName) > now);
        }

        if (KeysetCursor.TryDecode(query.Cursor, out var sortKey, out var lastId) &&
            DateTimeOffset.TryParse(
                sortKey, null, System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var lastStart))
        {
            // The identifier is part of the key, not decoration: several events can share one
            // derived instant, and a keyset on the instant alone would drop or repeat the
            // rows at that boundary.
            events = events.Where(e =>
                EF.Property<DateTimeOffset>(e, EventStartInstants.PropertyName) > lastStart ||
                (EF.Property<DateTimeOffset>(e, EventStartInstants.PropertyName) == lastStart && e.Id.CompareTo(lastId) > 0));
        }

        var page = await events
            .OrderBy(e => EF.Property<DateTimeOffset>(e, EventStartInstants.PropertyName))
            .ThenBy(e => e.Id)
            .Take(query.Limit)
            .Select(e => new
            {
                Event = e,
                ActiveBookings = context.Bookings.Count(b =>
                    b.EventId == e.Id && b.Status == BookingStatus.Active),
            })
            .ToListAsync(ct);

        return await ProjectAsync([.. page.Select(x => x.Event.Id)],
            page.ToDictionary(x => x.Event.Id, x => x.ActiveBookings), scope, ct);
    }

    /// <inheritdoc />
    public async Task<EventView?> GetAsync(Guid eventId, EventScope scope, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var found = await Filtered(scope)
            .Where(e => e.Id == eventId)
            .Select(e => new
            {
                ActiveBookings = context.Bookings.Count(b =>
                    b.EventId == e.Id && b.Status == BookingStatus.Active),
            })
            .SingleOrDefaultAsync(ct);

        if (found is null)
        {
            return null;
        }

        var rows = await ProjectAsync(
            [eventId], new Dictionary<Guid, int> { [eventId] = found.ActiveBookings }, scope, ct);
        return rows.SingleOrDefault();
    }

    /// <summary>
    /// An event the caller may see at all. A scoped caller sees only events listing their own
    /// type; an all-types caller sees every event.
    /// </summary>
    private IQueryable<Event> Filtered(EventScope scope) =>
        context.Events.AsNoTracking().Where(e =>
            scope.AllTypes ||
            context.EventCapacities.Any(c =>
                c.EventId == e.Id && c.AppointmentTypeId == scope.AppointmentTypeId));

    /// <summary>
    /// Joins the location and the capacity rows for a page of identifiers. Two round trips
    /// rather than one projection, because a capacity join inside the keyset query would
    /// multiply the rows the limit is counting.
    /// </summary>
    private async Task<IReadOnlyList<EventView>> ProjectAsync(
        IReadOnlyList<Guid> eventIds, IReadOnlyDictionary<Guid, int> activeBookings,
        EventScope scope, CancellationToken ct)
    {
        if (eventIds.Count == 0)
        {
            return [];
        }

        var rows = await (
            from e in context.Events.AsNoTracking()
            join l in context.Locations.AsNoTracking() on e.LocationId equals l.Id
            where eventIds.Contains(e.Id)
            orderby EF.Property<DateTimeOffset>(e, EventStartInstants.PropertyName), e.Id
            select new
            {
                e.Id,
                e.ProposalId,
                e.LocationId,
                LocationCode = l.Code,
                LocationName = l.Name,
                l.TimeZoneId,
                e.Window.Date,
                e.Window.StartTime,
                e.Window.DurationMinutes,
                e.Status,
                StartUtc = EF.Property<DateTimeOffset>(e, EventStartInstants.PropertyName),
            }).ToListAsync(ct);

        var capacities = await (
            from c in context.EventCapacities.AsNoTracking()
            join t in context.AppointmentTypes.AsNoTracking()
                on c.AppointmentTypeId equals t.Id
            where eventIds.Contains(c.EventId) &&
                (scope.AllTypes || c.AppointmentTypeId == scope.AppointmentTypeId)
            orderby t.Code
            select new
            {
                c.EventId,
                c.AppointmentTypeId,
                t.Code,
                t.Name,
                c.TotalHeadcount,
                c.RemainingCapacity,
            }).ToListAsync(ct);

        var byEvent = capacities
            .GroupBy(c => c.EventId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<EventCapacityView>)[.. g.Select(c => new EventCapacityView(
                    c.AppointmentTypeId, c.Code, c.Name, c.TotalHeadcount, c.RemainingCapacity))]);

        return [.. rows.Select(r => new EventView(
            r.Id, r.ProposalId, r.LocationId, r.LocationCode, r.LocationName, r.TimeZoneId,
            r.Date, r.StartTime, r.DurationMinutes, r.Status.ToString(),
            byEvent.TryGetValue(r.Id, out var found) ? found : [],
            activeBookings.TryGetValue(r.Id, out var count) ? count : 0,
            KeysetCursor.Encode(
                r.StartUtc.ToUniversalTime().ToString(SortKeyFormat,
                    System.Globalization.CultureInfo.InvariantCulture),
                r.Id)))];
    }
}

/// <summary>
/// The proposal read side, in the same file because it reads the same three tables.
/// </summary>
public sealed class EventProposalListQueries(EventBookingDbContext context)
    : IEventProposalListQueries
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<EventProposalListItem>> ListAsync(
        Guid actingAppointmentTypeId, Guid staffUserId, EventProposalStatus? status,
        Guid? locationId, string? cursor, int limit, CancellationToken ct)
    {
        var listed = context.EventProposals.AsNoTracking().Where(p =>
            p.ListedTypes.Any(t => t.AppointmentTypeId == actingAppointmentTypeId));

        if (status is not null)
        {
            listed = listed.Where(p => p.Status == status);
        }

        if (locationId is not null)
        {
            listed = listed.Where(p => p.LocationId == locationId);
        }

        // The guard is load-bearing: the HMAC signs the payload, not the list it came
        // from, so a cursor minted for another list arrives here intact but short. Without
        // the length check that cursor throws instead of reading as the first page.
        if (KeysetCursor.TryDecode(cursor, out var sortKey, out var lastId) &&
            sortKey.Length >= 10 &&
            DateOnly.TryParse(sortKey[..10], out var lastDate))
        {
            listed = listed.Where(p =>
                p.Window.Date > lastDate ||
                (p.Window.Date == lastDate && p.Id.CompareTo(lastId) > 0));
        }

        var page = await (
            from p in listed
            join l in context.Locations.AsNoTracking() on p.LocationId equals l.Id
            orderby p.Window.Date, p.Id
            select new
            {
                p.Id,
                p.LocationId,
                LocationCode = l.Code,
                LocationName = l.Name,
                l.TimeZoneId,
                p.Window.Date,
                p.Window.StartTime,
                p.Window.DurationMinutes,
                p.Status,
                p.CreatedByManagerUserId,
                ListedTypeCount = p.ListedTypes.Count,
                AcceptedTypeCount = p.Acceptances.Count,
                MyAcceptedHeadcount = p.Acceptances
                    .Where(a => a.AppointmentTypeId == actingAppointmentTypeId)
                    .Select(a => (int?)a.Headcount)
                    .FirstOrDefault(),
                ProposerType = p.ProposerAppointmentTypeId,
            })
            .Take(limit)
            .ToListAsync(ct);

        // A proposal's window is ordered by its local date, not a derived instant: proposals
        // carry no start_utc, and FR-2.13's board is a Manager's own calendar rather than a
        // cross-site sequence.
        return [.. page.Select(p => new EventProposalListItem(
            p.Id, p.LocationId, p.LocationCode, p.LocationName, p.TimeZoneId,
            p.Date, p.StartTime, p.DurationMinutes, p.Status.ToString(),
            p.ListedTypeCount, p.AcceptedTypeCount, p.MyAcceptedHeadcount,
            p.MyAcceptedHeadcount is not null,
            p.ProposerType == actingAppointmentTypeId && p.CreatedByManagerUserId == staffUserId,
            KeysetCursor.Encode(p.Date.ToString("yyyy-MM-dd"), p.Id)))];
    }
}
