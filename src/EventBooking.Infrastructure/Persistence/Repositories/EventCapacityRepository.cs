using EventBooking.Application.Abstractions;
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Repositories;

/// <summary>
/// The only raw SQL in the system, and the reason overbooking cannot happen. Read the three notes
/// in Task 47 of the plan before changing a character of the query below.
/// </summary>
public sealed class EventCapacityRepository(EventBookingDbContext context) : IEventCapacityRepository
{
    public async Task<IReadOnlyList<EventCapacity>> LockForUpdateAsync(
        Guid eventId,
        IReadOnlyCollection<Guid> appointmentTypeIds,
        CancellationToken cancellationToken)
    {
        // Sorted so that every caller takes the locks in the same order and two concurrent
        // confirmations for different type combinations cannot deadlock.
        var ordered = appointmentTypeIds.OrderBy(id => id).ToArray();

        return await context.EventCapacities
            .FromSql(
                $"""
                 SELECT event_id, appointment_type_id, total_headcount, remaining_capacity
                 FROM event_capacity
                 WHERE event_id = {eventId}
                   AND appointment_type_id = ANY({ordered})
                 ORDER BY appointment_type_id
                 FOR UPDATE
                 """)
            .ToListAsync(cancellationToken);
    }
}
