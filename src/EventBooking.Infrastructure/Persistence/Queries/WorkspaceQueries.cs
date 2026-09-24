using EventBooking.Application.Abstractions;
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Queries;

/// <summary>
/// One statement. The join to event_capacity is what restricts the list to events that
/// actually offer the caller's type, so an appointment-staff member never sees an event
/// they could not work; the join to location keeps an event whose location row has gone
/// out of the result rather than failing the whole read.
/// </summary>
/// <param name="context">The read-only persistence context.</param>
public sealed class WorkspaceQueries(EventBookingDbContext context) : IWorkspaceQueries
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> ListWorkspaceEventIdsAsync(
        Guid appointmentTypeId, Guid? locationId,
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct) =>
        await context.Events
            .AsNoTracking()
            .Where(e => e.Status == EventStatus.Active)
            .Where(e => locationId == null || e.LocationId == locationId)
            .Where(e => context.Locations.Any(l => l.Id == e.LocationId))
            .Where(e => e.Capacities.Any(c => c.AppointmentTypeId == appointmentTypeId))
            .Where(e => EF.Property<DateTimeOffset>(e, EventStartInstants.PropertyName) >= from
                && EF.Property<DateTimeOffset>(e, EventStartInstants.PropertyName) <= to)
            .OrderBy(e => EF.Property<DateTimeOffset>(e, EventStartInstants.PropertyName))
            .ThenBy(e => e.Id)
            .Select(e => e.Id)
            .ToListAsync(ct);
}
