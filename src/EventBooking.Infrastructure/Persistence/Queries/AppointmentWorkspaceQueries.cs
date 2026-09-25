using EventBooking.Application.Abstractions;
using EventBooking.Application.Appointments;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Events;
using EventBooking.Domain.Time;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Queries;

/// <summary>Projects minimum appointment-delivery rows directly from PostgreSQL.</summary>
public sealed class AppointmentWorkspaceQueries(
    EventBookingDbContext context, IClock clock, IEventWindowZones zones)
    : IAppointmentWorkspaceQueries
{
    /// <inheritdoc/>
    public async Task<AppointmentWorkspaceEventList> ListEventsAsync(
        Guid appointmentTypeId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken)
    {
        var appointmentTypeName = await context.AppointmentTypes
            .AsNoTracking()
            .Where(value => value.Id == appointmentTypeId)
            .Select(value => value.Name)
            .SingleAsync(cancellationToken);

        var widened = WidenedEarliest(asOf);

        var grouped = await (
            from appointment in context.BookingAppointments.AsNoTracking()
            join booking in context.Bookings.AsNoTracking()
                on appointment.BookingId equals booking.Id
            join eventItem in context.Events.AsNoTracking()
                on booking.EventId equals eventItem.Id
            join location in context.Locations.AsNoTracking()
                on eventItem.LocationId equals location.Id
            where appointment.AppointmentTypeId == appointmentTypeId
                && booking.Status == BookingStatus.Active
                && eventItem.Status == EventStatus.Active
                && eventItem.Window.Date >= widened
            group appointment by new
            {
                eventItem.Id,
                eventItem.Window.Date,
                eventItem.Window.StartTime,
                location.TimeZoneId,
            }
            into values
            orderby values.Key.Date, values.Key.StartTime, values.Key.Id
            select new
            {
                EventId = values.Key.Id,
                values.Key.Date,
                values.Key.StartTime,
                values.Key.TimeZoneId,
                Expected = values.Count(value => value.Status == BookingAppointmentStatus.Expected),
                CheckedIn = values.Count(value => value.Status == BookingAppointmentStatus.CheckedIn),
                Completed = values.Count(value => value.Status == BookingAppointmentStatus.Completed),
                NoShow = values.Count(value => value.Status == BookingAppointmentStatus.NoShow),
            }).ToListAsync(cancellationToken);

        return new AppointmentWorkspaceEventList
        {
            AppointmentTypeName = appointmentTypeName,
            Events = grouped
                .Where(value => value.Date >= EarliestAt(asOf, value.TimeZoneId))
                .Select(value => new AppointmentEventSummary
                {
                    EventId = value.EventId,
                    Date = value.Date,
                    StartTime = value.StartTime,
                    EndTime = value.StartTime.AddHours(4),
                    Counts = new AppointmentStatusCounts
                    {
                        Expected = value.Expected,
                        CheckedIn = value.CheckedIn,
                        Completed = value.Completed,
                        NoShow = value.NoShow,
                    },
                }).ToList(),
        };
    }

    /// <inheritdoc/>
    public async Task<AppointmentEventDetail?> GetEventAsync(
        Guid appointmentTypeId,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var widened = WidenedEarliest(now);
        var header = await (
            from appointment in context.BookingAppointments.AsNoTracking()
            join booking in context.Bookings.AsNoTracking()
                on appointment.BookingId equals booking.Id
            join eventItem in context.Events.AsNoTracking()
                on booking.EventId equals eventItem.Id
            join appointmentType in context.AppointmentTypes.AsNoTracking()
                on appointment.AppointmentTypeId equals appointmentType.Id
            join location in context.Locations.AsNoTracking()
                on eventItem.LocationId equals location.Id
            where appointment.AppointmentTypeId == appointmentTypeId
                && eventItem.Id == eventId
                && booking.Status == BookingStatus.Active
                && eventItem.Status == EventStatus.Active
                && eventItem.Window.Date >= widened
            select new
            {
                AppointmentTypeName = appointmentType.Name,
                EventId = eventItem.Id,
                eventItem.Window.Date,
                eventItem.Window.StartTime,
                location.TimeZoneId,
            }).FirstOrDefaultAsync(cancellationToken);

        if (header is null || header.Date < EarliestAt(now, header.TimeZoneId))
        {
            return null;
        }

        var rows = await (
            from appointment in context.BookingAppointments.AsNoTracking()
            join booking in context.Bookings.AsNoTracking()
                on appointment.BookingId equals booking.Id
            join eventItem in context.Events.AsNoTracking()
                on booking.EventId equals eventItem.Id
            join attendee in context.Attendees.AsNoTracking()
                on booking.AttendeeId equals attendee.Id
            where appointment.AppointmentTypeId == appointmentTypeId
                && eventItem.Id == eventId
                && booking.Status == BookingStatus.Active
                && eventItem.Status == EventStatus.Active
            orderby attendee.Name, attendee.Email, appointment.Id
            select new BookingAppointmentRow
            {
                BookingAppointmentId = appointment.Id,
                AttendeeName = attendee.Name,
                AttendeeEmail = attendee.Email,
                Status = appointment.Status,
                CheckedInAt = appointment.CheckedInAt,
                OutcomeAt = appointment.OutcomeAt,
                Version = appointment.Version,
            }).ToListAsync(cancellationToken);

        return new AppointmentEventDetail
        {
            AppointmentTypeName = header.AppointmentTypeName,
            EventId = header.EventId,
            Date = header.Date,
            StartTime = header.StartTime,
            EndTime = header.StartTime.AddHours(4),
            Appointments = rows,
        };
    }

    /// <summary>
    /// The earliest event date the recent-past allowance retains at one location: the allowance
    /// counts back from the location's own date, the same date check-in and no-show use.
    /// </summary>
    /// <param name="asOf">The current instant.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    private DateOnly EarliestAt(DateTimeOffset asOf, string timeZoneId) =>
        zones.LocalDateOf(asOf, timeZoneId).AddDays(-AppointmentWorkspaceAllowance.RecentPastDays);

    /// <summary>
    /// A SQL pre-filter a day earlier than any zone's allowance can reach, since every location's
    /// date is within a day of the UTC date. The exact cut is <see cref="EarliestAt"/>.
    /// </summary>
    /// <param name="asOf">The current instant.</param>
    private static DateOnly WidenedEarliest(DateTimeOffset asOf) =>
        DateOnly.FromDateTime(asOf.UtcDateTime).AddDays(-AppointmentWorkspaceAllowance.RecentPastDays - 1);
}
