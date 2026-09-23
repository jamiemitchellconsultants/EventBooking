using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Queries;

public sealed class DashboardQueries(EventBookingDbContext context, IClock clock) : IDashboardQueries
{
    public async Task<IReadOnlyList<AwaitingAvailabilityRow>> AwaitingAvailabilityAsync(
        CancellationToken cancellationToken)
    {
        var today = clock.TodayAtTransitionalLocation;
        var rows = await AttendeeRows(AttendeeStatus.AwaitingAvailability).ToListAsync(cancellationToken);

        return rows
            .Select(row =>
            {
                var since = clock.DateAtTransitionalLocation(row.StatusChangedAt);
                return new AwaitingAvailabilityRow(
                    row.Id,
                    row.Name,
                    row.Email,
                    row.AppointmentTypeIds.Select(AppointmentTypeIds.CodeOf)
                        .OrderBy(code => code, StringComparer.Ordinal).ToList(),
                    since,
                    Math.Max(0, today.DayNumber - since.DayNumber));
            })
            .OrderByDescending(row => row.DaysWaiting)
            .ToList();
    }

    public async Task<IReadOnlyList<NoResponseRow>> NoResponseAsync(CancellationToken cancellationToken)
    {
        var rows = await AttendeeRows(AttendeeStatus.NoResponseNeedsFollowUp).ToListAsync(cancellationToken);

        return rows
            .Select(row => new NoResponseRow(
                row.Id,
                row.Name,
                row.Email,
                row.AppointmentTypeIds.Select(AppointmentTypeIds.CodeOf)
                    .OrderBy(code => code, StringComparer.Ordinal).ToList(),
                clock.DateAtTransitionalLocation(row.StatusChangedAt)))
            .OrderBy(row => row.GaveUpOn)
            .ToList();
    }

    public async Task<IReadOnlyList<EventOverviewRow>> EventsOverviewAsync(CancellationToken cancellationToken)
    {
        // Past events can no longer be cancelled, so the operations list shows only
        // today and future events.
        var today = clock.TodayAtTransitionalLocation;
        var rows = await context.Events
            .AsNoTracking()
            .Where(eventItem => eventItem.Status == EventStatus.Active && eventItem.Window.Date >= today)
            .Select(eventItem => new
            {
                eventItem.Id,
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

        return rows
            .Select(row => new EventOverviewRow(
                row.Id,
                row.Date,
                row.StartTime,
                row.StartTime.Add(TimeSpan.FromMinutes(row.DurationMinutes)),
                row.Capacities.Select(capacity => new EventCapacityRow(
                    AppointmentTypeIds.CodeOf(capacity.AppointmentTypeId),
                    capacity.TotalHeadcount,
                    capacity.RemainingCapacity))
                    .OrderBy(capacity => capacity.Code, StringComparer.Ordinal).ToList(),
                row.ActiveBookings))
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
            .GroupBy(delivery => delivery.AttendeeId)
            .Select(group => group
                .OrderBy(delivery => delivery.Status is EmailStatus.Failed or EmailStatus.Pending ? 0 : 1)
                .ThenByDescending(delivery => delivery.SentAt)
                .ThenByDescending(delivery => delivery.Id)
                .First())
            .ToList();

        var attendeeIds = latest.Select(delivery => delivery.AttendeeId).Distinct().ToList();
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
        // active), matching RetryEmailHandler's booking lookup.
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
                delivery.AttendeeId,
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
                            && attendeeStatuses.TryGetValue(delivery.AttendeeId, out var status)
                            && status is AttendeeStatus.Invited or AttendeeStatus.AwaitingAvailability,
                        _ => false,
                    })))
            .ToList();
    }

    private IQueryable<AttendeeRow> AttendeeRows(AttendeeStatus status) =>
        context.Attendees
            .AsNoTracking()
            .Where(attendee => attendee.Status == status)
            .Select(attendee => new AttendeeRow(
                attendee.Id,
                attendee.Name,
                attendee.Email,
                EF.Property<DateTimeOffset>(attendee, StatusStampingInterceptor.ShadowProperty),
                attendee.Requirements.Select(requirement => requirement.AppointmentTypeId).ToList()));

    private sealed record AttendeeRow(
        Guid Id,
        string Name,
        string Email,
        DateTimeOffset StatusChangedAt,
        List<Guid> AppointmentTypeIds);
}
