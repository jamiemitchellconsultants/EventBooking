using EventBooking.Application.Abstractions;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Invites;

/// <summary>
/// The one place the "which events may a attendee be offered" rule lives. Task 36 uses it to build
/// an invite; Task 40 uses it to find a single replacement when an option fills up.
/// </summary>
/// <param name="events">The events.</param>
/// <param name="clock">The clock.</param>
public sealed class EligibleEventFinder(IEventRepository events, IClock clock)
{
    /// <summary>Defines find async for the current use case.</summary>
    /// <param name="requiredAppointmentTypeIds">The required appointment type ids.</param>
    /// <param name="take">The take.</param>
    /// <param name="excludeEventIds">The exclude event ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<IReadOnlyList<Event>> FindAsync(
        IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
        int take,
        IReadOnlyCollection<Guid> excludeEventIds,
        CancellationToken cancellationToken)
    {
        var today = clock.TodayAtTransitionalLocation;

        var attendees = await events.ListActiveAsync(today, cancellationToken);

        return attendees
            .Where(s => !excludeEventIds.Contains(s.Id))
            .Where(s => s.Window.StartsAfter(today))
            .Where(s => s.HasSpareCapacityForAll(requiredAppointmentTypeIds))
            .OrderBy(s => s.Window)
            .Take(take)
            .ToList();
    }
}
