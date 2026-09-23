using EventBooking.Application.Abstractions;
using EventBooking.Application.Appointments;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Queries;

/// <summary>Projects minimum appointment-delivery rows directly from PostgreSQL.</summary>
public sealed class AppointmentWorkspaceQueries(EventBookingDbContext context, IClock clock)
    : IAppointmentWorkspaceQueries
{
    /// <inheritdoc/>
    public async Task<AppointmentWorkspaceEventList> ListEventsAsync(
        Guid appointmentTypeId,
        DateOnly onOrAfter,
        CancellationToken cancellationToken)
    {
        var appointmentTypeName = await context.AppointmentTypes
            .AsNoTracking()
            .Where(value => value.Id == appointmentTypeId)
            .Select(value => value.Name)
            .SingleAsync(cancellationToken);

        var earliest = onOrAfter.AddDays(-AppointmentWorkspaceAllowance.RecentPastDays);

        var grouped = await (
            from appointment in context.BookingAppointments.AsNoTracking()
            join booking in context.Bookings.AsNoTracking()
                on appointment.BookingId equals booking.Id
            join eventItem in context.Events.AsNoTracking()
                on booking.EventId equals eventItem.Id
            where appointment.AppointmentTypeId == appointmentTypeId
                && booking.Status == BookingStatus.Active
                && eventItem.Status == EventStatus.Active
                && eventItem.Window.Date >= earliest
            group appointment by new
            {
                eventItem.Id,
                eventItem.Window.Date,
                eventItem.Window.StartTime,
            }
            into values
            orderby values.Key.Date, values.Key.StartTime, values.Key.Id
            select new
            {
                EventId = values.Key.Id,
                values.Key.Date,
                values.Key.StartTime,
                Expected = values.Count(value => value.Status == BookingAppointmentStatus.Expected),
                CheckedIn = values.Count(value => value.Status == BookingAppointmentStatus.CheckedIn),
                Completed = values.Count(value => value.Status == BookingAppointmentStatus.Completed),
                NoShow = values.Count(value => value.Status == BookingAppointmentStatus.NoShow),
            }).ToListAsync(cancellationToken);

        return new AppointmentWorkspaceEventList
        {
            AppointmentTypeName = appointmentTypeName,
            Events = grouped.Select(value => new AppointmentEventSummary
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
        var earliest = clock.TodayAtTransitionalLocation.AddDays(-AppointmentWorkspaceAllowance.RecentPastDays);
        var header = await (
            from appointment in context.BookingAppointments.AsNoTracking()
            join booking in context.Bookings.AsNoTracking()
                on appointment.BookingId equals booking.Id
            join eventItem in context.Events.AsNoTracking()
                on booking.EventId equals eventItem.Id
            join appointmentType in context.AppointmentTypes.AsNoTracking()
                on appointment.AppointmentTypeId equals appointmentType.Id
            where appointment.AppointmentTypeId == appointmentTypeId
                && eventItem.Id == eventId
                && booking.Status == BookingStatus.Active
                && eventItem.Status == EventStatus.Active
                && eventItem.Window.Date >= earliest
            select new
            {
                AppointmentTypeName = appointmentType.Name,
                EventId = eventItem.Id,
                eventItem.Window.Date,
                eventItem.Window.StartTime,
            }).FirstOrDefaultAsync(cancellationToken);

        if (header is null)
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
}
