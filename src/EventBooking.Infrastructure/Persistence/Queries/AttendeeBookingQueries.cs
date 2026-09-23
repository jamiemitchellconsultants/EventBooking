using EventBooking.Application.Abstractions;
using EventBooking.Application.Attendees;
using EventBooking.Domain.Bookings;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Queries;

/// <summary>Projects a attendee's active bookings with their event windows and nothing else.</summary>
/// <param name="context">The read-only persistence context.</param>
public sealed class AttendeeBookingQueries(EventBookingDbContext context) : IAttendeeBookingQueries
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AttendeeBookingSummary>?> ListActiveForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken)
    {
        var exists = await context.Attendees
            .AsNoTracking()
            .AnyAsync(c => c.Id == attendeeId, cancellationToken);
        if (!exists)
        {
            return null;
        }

        var rows = await context.Bookings
            .AsNoTracking()
            .Where(b => b.AttendeeId == attendeeId && b.Status == BookingStatus.Active)
            .Join(
                context.Events.AsNoTracking(),
                b => b.EventId,
                s => s.Id,
                (b, s) => new
                {
                    b.Id,
                    IsOriginal = b.RecoveryOfBookingId == null,
                    s.Window.Date,
                    s.Window.StartTime,
                })
            // The original booking sorts first so the coordinator reads the journey in order.
            .OrderByDescending(r => r.IsOriginal)
            .ThenBy(r => r.Date)
            .ThenBy(r => r.StartTime)
            .ToListAsync(cancellationToken);

        // The window's end is derived by the domain, never stored, so it is computed here rather
        // than projected in SQL.
        return
        [
            .. rows.Select(r => new AttendeeBookingSummary(
                r.Id,
                r.IsOriginal,
                r.Date,
                r.StartTime,
                r.StartTime.Add(Domain.Events.EventWindow.Duration))),
        ];
    }
}
