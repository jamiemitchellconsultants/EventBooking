using EventBooking.Application.Abstractions;
using EventBooking.Application.Appointments;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Slots;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Queries;

/// <summary>Projects minimum appointment-delivery rows directly from PostgreSQL.</summary>
public sealed class AppointmentWorkspaceQueries(EventBookingDbContext context, IClock clock)
    : IAppointmentWorkspaceQueries
{
    /// <inheritdoc/>
    public async Task<AppointmentWorkspaceSlotList> ListSlotsAsync(
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
            join slot in context.ConfirmedSlots.AsNoTracking()
                on booking.ConfirmedSlotId equals slot.Id
            where appointment.AppointmentTypeId == appointmentTypeId
                && booking.Status == BookingStatus.Active
                && slot.Status == ConfirmedSlotStatus.Active
                && slot.Window.Date >= earliest
            group appointment by new
            {
                slot.Id,
                slot.Window.Date,
                slot.Window.StartTime,
            }
            into values
            orderby values.Key.Date, values.Key.StartTime, values.Key.Id
            select new
            {
                ConfirmedSlotId = values.Key.Id,
                values.Key.Date,
                values.Key.StartTime,
                Expected = values.Count(value => value.Status == BookingAppointmentStatus.Expected),
                CheckedIn = values.Count(value => value.Status == BookingAppointmentStatus.CheckedIn),
                Completed = values.Count(value => value.Status == BookingAppointmentStatus.Completed),
                NoShow = values.Count(value => value.Status == BookingAppointmentStatus.NoShow),
            }).ToListAsync(cancellationToken);

        return new AppointmentWorkspaceSlotList
        {
            AppointmentTypeName = appointmentTypeName,
            Slots = grouped.Select(value => new AppointmentSlotSummary
            {
                ConfirmedSlotId = value.ConfirmedSlotId,
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
    public async Task<AppointmentSlotDetail?> GetSlotAsync(
        Guid appointmentTypeId,
        Guid confirmedSlotId,
        CancellationToken cancellationToken)
    {
        var earliest = clock.TodayAtHeadOffice.AddDays(-AppointmentWorkspaceAllowance.RecentPastDays);
        var header = await (
            from appointment in context.BookingAppointments.AsNoTracking()
            join booking in context.Bookings.AsNoTracking()
                on appointment.BookingId equals booking.Id
            join slot in context.ConfirmedSlots.AsNoTracking()
                on booking.ConfirmedSlotId equals slot.Id
            join appointmentType in context.AppointmentTypes.AsNoTracking()
                on appointment.AppointmentTypeId equals appointmentType.Id
            where appointment.AppointmentTypeId == appointmentTypeId
                && slot.Id == confirmedSlotId
                && booking.Status == BookingStatus.Active
                && slot.Status == ConfirmedSlotStatus.Active
                && slot.Window.Date >= earliest
            select new
            {
                AppointmentTypeName = appointmentType.Name,
                ConfirmedSlotId = slot.Id,
                slot.Window.Date,
                slot.Window.StartTime,
            }).FirstOrDefaultAsync(cancellationToken);

        if (header is null)
        {
            return null;
        }

        var rows = await (
            from appointment in context.BookingAppointments.AsNoTracking()
            join booking in context.Bookings.AsNoTracking()
                on appointment.BookingId equals booking.Id
            join slot in context.ConfirmedSlots.AsNoTracking()
                on booking.ConfirmedSlotId equals slot.Id
            join candidate in context.Candidates.AsNoTracking()
                on booking.CandidateId equals candidate.Id
            where appointment.AppointmentTypeId == appointmentTypeId
                && slot.Id == confirmedSlotId
                && booking.Status == BookingStatus.Active
                && slot.Status == ConfirmedSlotStatus.Active
            orderby candidate.Name, candidate.Email, appointment.Id
            select new BookingAppointmentRow
            {
                BookingAppointmentId = appointment.Id,
                CandidateName = candidate.Name,
                CandidateEmail = candidate.Email,
                Status = appointment.Status,
                CheckedInAt = appointment.CheckedInAt,
                OutcomeAt = appointment.OutcomeAt,
                Version = appointment.Version,
            }).ToListAsync(cancellationToken);

        return new AppointmentSlotDetail
        {
            AppointmentTypeName = header.AppointmentTypeName,
            ConfirmedSlotId = header.ConfirmedSlotId,
            Date = header.Date,
            StartTime = header.StartTime,
            EndTime = header.StartTime.AddHours(4),
            Appointments = rows,
        };
    }
}
