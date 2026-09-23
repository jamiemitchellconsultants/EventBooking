using EventBooking.Application.Abstractions;
using EventBooking.Application.Candidates;
using EventBooking.Domain.Bookings;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Queries;

/// <summary>Projects a candidate's active bookings with their slot windows and nothing else.</summary>
/// <param name="context">The read-only persistence context.</param>
public sealed class CandidateBookingQueries(EventBookingDbContext context) : ICandidateBookingQueries
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<CandidateBookingSummary>?> ListActiveForCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        var exists = await context.Candidates
            .AsNoTracking()
            .AnyAsync(c => c.Id == candidateId, cancellationToken);
        if (!exists)
        {
            return null;
        }

        var rows = await context.Bookings
            .AsNoTracking()
            .Where(b => b.CandidateId == candidateId && b.Status == BookingStatus.Active)
            .Join(
                context.ConfirmedSlots.AsNoTracking(),
                b => b.ConfirmedSlotId,
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
            .. rows.Select(r => new CandidateBookingSummary(
                r.Id,
                r.IsOriginal,
                r.Date,
                r.StartTime,
                r.StartTime.Add(Domain.Slots.SlotWindow.Duration))),
        ];
    }
}
