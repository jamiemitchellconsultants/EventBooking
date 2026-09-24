using EventBooking.Application.Abstractions;
using EventBooking.Application.Attendees;
using EventBooking.Domain.Bookings;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Queries;

/// <summary>Projects the readiness journey without personal, token, audit, or capacity data.</summary>
/// <param name="context">The read-only persistence context.</param>
public sealed class AttendeeReadinessQueries(EventBookingDbContext context) : IAttendeeReadinessQueries
{
    /// <inheritdoc />
    public async Task<AttendeeReadinessSnapshot?> GetSnapshotAsync(
        Guid attendeeId,
        CancellationToken cancellationToken)
    {
        var attendee = await context.Attendees
            .AsNoTracking()
            .Where(c => c.Id == attendeeId)
            .Select(c => new
            {
                c.Id,
                c.AttendeeGroupId,
                RequirementTypeIds = c.Requirements
                    .Select(r => r.AppointmentTypeId)
                    .ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (attendee is null)
        {
            return null;
        }

        var originalId = await context.Bookings
            .AsNoTracking()
            .Where(b => b.AttendeeId == attendeeId
                && b.Status == BookingStatus.Active
                && b.RecoveryOfBookingId == null)
            .Select(b => (Guid?)b.Id)
            .SingleOrDefaultAsync(cancellationToken);

        var appointmentTypes = await context.AppointmentTypes
            .AsNoTracking()
            .Select(t => new AttendeeReadinessType(t.Id, t.Code, t.Name))
            .ToListAsync(cancellationToken);

        if (originalId is null)
        {
            return new AttendeeReadinessSnapshot(
                attendee.Id, attendee.AttendeeGroupId, attendee.RequirementTypeIds, null, [],
                appointmentTypes.ToDictionary(t => t.Id));
        }

        var bookings = await context.Bookings
            .AsNoTracking()
            .Where(b => b.Id == originalId.Value || b.RecoveryOfBookingId == originalId.Value)
            .Select(b => new { b.Id, b.Status, b.CreatedAt })
            .ToListAsync(cancellationToken);

        var journeyIds = bookings.Select(b => b.Id).ToList();
        var states = bookings.ToDictionary(b => b.Id);

        var attempts = await context.BookingAppointments
            .AsNoTracking()
            .Where(a => journeyIds.Contains(a.BookingId))
            .Select(a => new { a.Id, a.BookingId, a.AppointmentTypeId, a.Status })
            .ToListAsync(cancellationToken);

        return new AttendeeReadinessSnapshot(
            attendee.Id,
            attendee.AttendeeGroupId,
            attendee.RequirementTypeIds,
            originalId,
            attempts
                .Select(a => new AttendeeReadinessAttempt(
                    a.Id,
                    a.AppointmentTypeId,
                    a.Status,
                    a.BookingId,
                    states[a.BookingId].Status,
                    states[a.BookingId].CreatedAt))
                .ToList(),
            appointmentTypes.ToDictionary(t => t.Id));
    }
}
