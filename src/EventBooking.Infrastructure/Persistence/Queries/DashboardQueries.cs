using EventBooking.Application.Abstractions;
using EventBooking.Application.Dashboards;
using EventBooking.Application.ReadModels;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;
using EventBooking.Domain.Time;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Queries;

public sealed class DashboardQueries(
    EventBookingDbContext context, IClock clock, IEventWindowZones eventZones) : IDashboardQueries
{
    public async Task<IReadOnlyList<AwaitingAvailabilityRow>> AwaitingAvailabilityAsync(
        CancellationToken cancellationToken)
    {
        var today = UtcToday();
        var codes = await AppointmentTypeCodes.LoadAsync(context, cancellationToken);
        var rows = await AttendeeRows(AttendeeStatus.AwaitingAvailability).ToListAsync(cancellationToken);

        return rows
            .Select(row =>
            {
                var since = DateOnly.FromDateTime(row.StatusChangedAt.UtcDateTime);
                return new AwaitingAvailabilityRow(
                    row.Id,
                    row.Name,
                    row.Email,
                    row.AppointmentTypeIds.Select(codes.CodeOf)
                        .OrderBy(code => code, StringComparer.Ordinal).ToList(),
                    since,
                    Math.Max(0, today.DayNumber - since.DayNumber));
            })
            .OrderByDescending(row => row.DaysWaiting)
            .ToList();
    }

    public async Task<IReadOnlyList<NoResponseRow>> NoResponseAsync(CancellationToken cancellationToken)
    {
        var codes = await AppointmentTypeCodes.LoadAsync(context, cancellationToken);
        var rows = await AttendeeRows(AttendeeStatus.NoResponseNeedsFollowUp).ToListAsync(cancellationToken);

        return rows
            .Select(row => new NoResponseRow(
                row.Id,
                row.Name,
                row.Email,
                row.AppointmentTypeIds.Select(codes.CodeOf)
                    .OrderBy(code => code, StringComparer.Ordinal).ToList(),
                DateOnly.FromDateTime(row.StatusChangedAt.UtcDateTime)))
            .OrderBy(row => row.GaveUpOn)
            .ToList();
    }

    public async Task<IReadOnlyList<EventOverviewRow>> EventsOverviewAsync(CancellationToken cancellationToken)
    {
        // Past events can no longer be cancelled, so the operations list shows only
        // today and future events, where "today" is the date at each event's own location.
        // SQL pre-filters a day either side of the UTC date; the exact cut is per zone below.
        var now = clock.UtcNow;
        var earliest = UtcToday().AddDays(-1);
        var codes = await AppointmentTypeCodes.LoadAsync(context, cancellationToken);
        var rows = await context.Events
            .AsNoTracking()
            .Where(eventItem => eventItem.Status == EventStatus.Active && eventItem.Window.Date >= earliest)
            .Select(eventItem => new
            {
                eventItem.Id,
                eventItem.LocationId,
                eventItem.Window.Date,
                eventItem.Window.StartTime,
                eventItem.Window.DurationMinutes,
                Capacities = eventItem.Capacities.Select(capacity => new
                {
                    capacity.AppointmentTypeId,
                    capacity.TotalHeadcount,
                    capacity.RemainingCapacity,
                }).ToList(),
                ActiveBookings = context.Bookings.Count(booking =>
                    booking.EventId == eventItem.Id && booking.Status == BookingStatus.Active),
            })
            .ToListAsync(cancellationToken);

        var locations = await context.Locations
            .AsNoTracking()
            .Select(location => new { location.Id, location.Name, location.TimeZoneId })
            .ToDictionaryAsync(location => location.Id, cancellationToken);

        return rows
            .Where(row => locations.TryGetValue(row.LocationId, out var location)
                && row.Date >= eventZones.LocalDateOf(now, location.TimeZoneId))
            .Select(row => new EventOverviewRow(
                row.Id,
                row.LocationId,
                locations[row.LocationId].Name,
                row.Date,
                row.StartTime,
                row.StartTime.Add(TimeSpan.FromMinutes(row.DurationMinutes)),
                row.Capacities.Select(capacity => new EventCapacityRow(
                    codes.CodeOf(capacity.AppointmentTypeId),
                    capacity.TotalHeadcount,
                    capacity.RemainingCapacity))
                    .OrderBy(capacity => capacity.Code, StringComparer.Ordinal).ToList(),
                row.ActiveBookings,
                locations[row.LocationId].TimeZoneId,
                row.DurationMinutes))
            .OrderBy(row => row.Date)
            .ThenBy(row => row.StartTime)
            .ToList();
    }

    /// <summary>Returns the latest delivery per attendee and whether its persisted context remains actionable.</summary>
    public async Task<IReadOnlyList<AttendeeEmailStatusRow>> LatestEmailStatusAsync(
        CancellationToken cancellationToken)
    {
        // EF Core cannot translate this per-group priority cleanly. An unresolved attempt remains
        // visible ahead of terminal history so one attendee's second message cannot hide it.
        var latest = (await context.EmailLogs
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .Where(delivery => delivery.AttendeeId.HasValue)
            .GroupBy(delivery => delivery.AttendeeId!.Value)
            .Select(group => group
                .OrderBy(delivery => delivery.Status is EmailStatus.Failed or EmailStatus.Pending ? 0 : 1)
                .ThenByDescending(delivery => delivery.SentAt)
                .ThenByDescending(delivery => delivery.Id)
                .First())
            .ToList();

        var attendeeIds = latest.Select(delivery => delivery.AttendeeId!.Value).Distinct().ToList();
        var attendeeStatuses = await context.Attendees
            .AsNoTracking()
            .Where(attendee => attendeeIds.Contains(attendee.Id))
            .ToDictionaryAsync(attendee => attendee.Id, attendee => attendee.Status, cancellationToken);

        var inviteIds = latest.Where(delivery => delivery.InviteId.HasValue)
            .Select(delivery => delivery.InviteId!.Value).Distinct().ToList();
        var retryableInviteIds = await context.Invites
            .AsNoTracking()
            .Where(invite => inviteIds.Contains(invite.Id)
                && invite.Status == InviteStatus.Pending
                && invite.ExpiresAt > clock.UtcNow)
            .Select(invite => invite.Id)
            .ToHashSetAsync(cancellationToken);

        var bookingIds = latest.Where(delivery => delivery.BookingId.HasValue)
            .Select(delivery => delivery.BookingId!.Value).Distinct().ToList();
        var retryableBookingIds = await context.Bookings
            .AsNoTracking()
            .Where(booking => bookingIds.Contains(booking.Id) && booking.Status == BookingStatus.Active)
            .Select(booking => booking.Id)
            .ToHashSetAsync(cancellationToken);

        // Event-cancellation retry needs the staged booking to exist (it is cancelled, not
        // active), matching the dispatcher's cancellation context lookup.
        var existingBookingIds = await context.Bookings
            .AsNoTracking()
            .Where(booking => bookingIds.Contains(booking.Id))
            .Select(booking => booking.Id)
            .ToHashSetAsync(cancellationToken);

        var eventIds = latest.Where(delivery => delivery.EventId.HasValue)
            .Select(delivery => delivery.EventId!.Value).Distinct().ToList();
        var cancelledEventIds = await context.Events
            .AsNoTracking()
            .Where(eventItem => eventIds.Contains(eventItem.Id) && eventItem.Status == EventStatus.Cancelled)
            .Select(eventItem => eventItem.Id)
            .ToHashSetAsync(cancellationToken);

        return latest
            .Select(delivery => new AttendeeEmailStatusRow(
                delivery.AttendeeId!.Value,
                delivery.TemplateName,
                delivery.SentAt,
                delivery.Status,
                (delivery.Status is EmailStatus.Failed or EmailStatus.Pending)
                && (delivery.TemplateName switch
                    {
                        EmailTemplate.AttendeeInvite or EmailTemplate.AttendeeReinvite =>
                            delivery.InviteId is { } inviteId && retryableInviteIds.Contains(inviteId),
                        EmailTemplate.BookingConfirmation =>
                            delivery.BookingId is { } bookingId && retryableBookingIds.Contains(bookingId),
                        EmailTemplate.EventCancelledRebookingNeeded =>
                            delivery.EventId is { } eventId
                            && cancelledEventIds.Contains(eventId)
                            && delivery.BookingId is { } cancellationBookingId
                            && existingBookingIds.Contains(cancellationBookingId)
                            && attendeeStatuses.TryGetValue(delivery.AttendeeId!.Value, out var status)
                            && status is AttendeeStatus.Invited or AttendeeStatus.AwaitingAvailability,
                        _ => false,
                    })))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<DashboardsView> GetDashboardsAsync(
        CallerShape shape, Guid? locationId, DateTimeOffset now,
        IEventWindowZones zones, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(zones);

        // The read model refuses the Admin shape itself (FR-13: Coordinator, never Admin).
        // The handler checks for a Coordinator profile, but an Admin who also holds one must
        // still not read attendee rows, and that rule belongs where the rows are.
        if (shape.IsAdmin)
        {
            return new DashboardsView(
                new AwaitingAvailabilityTab(0, []), new NoResponseTab(0, []),
                new EventsTab(0, []), 0, 0);
        }

        var today = DateOnly.FromDateTime(now.UtcDateTime);

        // The two attendee tabs are one status each (FR-13.1). They carry no location:
        // an attendee awaiting availability has no event yet, so a location filter cannot
        // narrow them without inventing a relationship the model does not have. The filter
        // applies to the Events tab, which is the tab that has one.
        var awaiting = await AttendeeTabRowsAsync(
            AttendeeStatus.AwaitingAvailability, today, ct);
        var noResponseRows = await AttendeeTabRowsAsync(
            AttendeeStatus.NoResponseNeedsFollowUp, today, ct);
        var noResponse = noResponseRows
            .Select(row => new NoResponseRow(
                row.AttendeeId, row.Name, row.Email, row.RequiredCodes, row.WaitingSince))
            .ToList();

        // Events ending between 7 days ago and 60 days ahead. There is no stored end
        // instant — PostgreSQL cannot evaluate IANA rules deterministically (design 04) —
        // so SQL pre-filters start_utc to the widened window and the exact end bound is
        // applied here over the shortlist, with each event's location zone.
        var from = now.AddDays(-7);
        var to = now.AddDays(60);
        var widenedFrom = from.AddMinutes(-EventWindow.MaximumDurationMinutes);

        var shortlist = await context.Events
            .AsNoTracking()
            .Where(e => e.Status == EventStatus.Active)
            .Where(e => locationId == null || e.LocationId == locationId)
            .Where(e => EF.Property<DateTimeOffset>(e, EventStartInstants.PropertyName) >= widenedFrom
                && EF.Property<DateTimeOffset>(e, EventStartInstants.PropertyName) <= to)
            .Select(e => new
            {
                e.Id,
                e.LocationId,
                e.Window.Date,
                e.Window.StartTime,
                e.Window.DurationMinutes,
                Capacities = e.Capacities
                    .Select(c => new { c.AppointmentTypeId, c.TotalHeadcount, c.RemainingCapacity })
                    .ToList(),
                ActiveBookings = context.Bookings
                    .Count(b => b.EventId == e.Id && b.Status == BookingStatus.Active),
            })
            .ToListAsync(ct);

        var locations = await context.Locations
            .AsNoTracking()
            .Select(l => new { l.Id, l.Name, l.TimeZoneId })
            .ToDictionaryAsync(l => l.Id, ct);
        var typeCodes = await context.AppointmentTypes
            .AsNoTracking()
            .Select(t => new { t.Id, t.Code })
            .ToDictionaryAsync(t => t.Id, t => t.Code, ct);

        var events = new List<EventOverviewRow>();
        foreach (var row in shortlist)
        {
            if (!locations.TryGetValue(row.LocationId, out var location))
            {
                continue;
            }

            var endTime = row.StartTime.Add(TimeSpan.FromMinutes(row.DurationMinutes));
            var endInstant = zones.InstantOf(row.Date, endTime, location.TimeZoneId);
            if (endInstant < from || endInstant > to)
            {
                continue;
            }

            events.Add(new EventOverviewRow(
                row.Id,
                row.LocationId,
                location.Name,
                row.Date,
                row.StartTime,
                endTime,
                [.. row.Capacities
                    .Where(c => typeCodes.ContainsKey(c.AppointmentTypeId))
                    .Select(c => new EventCapacityRow(
                        typeCodes[c.AppointmentTypeId], c.TotalHeadcount, c.RemainingCapacity))
                    .OrderBy(c => c.Code)],
                row.ActiveBookings,
                location.TimeZoneId,
                row.DurationMinutes));
        }

        events = [.. events.OrderBy(e => e.Date).ThenBy(e => e.StartTime).ThenBy(e => e.EventId)];

        // FR-13.2: failed and pending counts come from the latest email per attendee, not
        // from every row ever written, or one attendee's long retry history would dominate.
        // EF Core cannot translate the per-group priority, so the narrow columns are
        // materialised first and the latest-per-attendee pick happens in memory.
        var latest = (await context.EmailLogs
            .AsNoTracking()
            .Select(e => new { e.AttendeeId, e.SentAt, e.Id, e.Status })
            .ToListAsync(ct))
            .GroupBy(e => e.AttendeeId)
            .Select(g => g
                .OrderByDescending(e => e.SentAt)
                .ThenByDescending(e => e.Id)
                .Select(e => e.Status)
                .First())
            .ToList();

        return new DashboardsView(
            new AwaitingAvailabilityTab(awaiting.Count, awaiting),
            new NoResponseTab(noResponse.Count, noResponse),
            new EventsTab(events.Count, events),
            latest.Count(status => status == EmailStatus.Failed),
            latest.Count(status => status == EmailStatus.Pending));
    }

    private async Task<IReadOnlyList<AwaitingAvailabilityRow>> AttendeeTabRowsAsync(
        AttendeeStatus status, DateOnly today, CancellationToken ct)
    {
        var rows = await context.Attendees
            .AsNoTracking()
            .Where(c => c.Status == status)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Email,
                c.StatusChangedAt,
                Codes = c.Requirements
                    .Join(context.AppointmentTypes, r => r.AppointmentTypeId, t => t.Id,
                        (r, t) => t.Code)
                    .OrderBy(code => code)
                    .ToList(),
            })
            .OrderBy(c => c.StatusChangedAt)
            .ThenBy(c => c.Id)
            .ToListAsync(ct);

        return
        [
            .. rows.Select(c =>
            {
                var since = DateOnly.FromDateTime(c.StatusChangedAt.UtcDateTime);
                return new AwaitingAvailabilityRow(
                    c.Id, c.Name, c.Email, c.Codes, since, today.DayNumber - since.DayNumber);
            }),
        ];
    }

    private IQueryable<AttendeeRow> AttendeeRows(AttendeeStatus status) =>
        context.Attendees
            .AsNoTracking()
            .Where(attendee => attendee.Status == status)
            .Select(attendee => new AttendeeRow(
                attendee.Id,
                attendee.Name,
                attendee.Email,
                attendee.StatusChangedAt,
                attendee.Requirements.Select(requirement => requirement.AppointmentTypeId).ToList()));

    /// <summary>The current UTC date. Attendee aggregates have no location, so they cut off at
    /// UTC; event cutoffs use the event's own location zone.</summary>
    private DateOnly UtcToday() => DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

    private sealed record AttendeeRow(
        Guid Id,
        string Name,
        string Email,
        DateTimeOffset StatusChangedAt,
        List<Guid> AppointmentTypeIds);
}
