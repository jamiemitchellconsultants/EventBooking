using EventBooking.Application.Abstractions;
using EventBooking.Application.Events;
using EventBooking.Domain.Events;
using EventBooking.Domain.Locations;

namespace EventBooking.Application.Invites;

/// <summary>
/// The one place the "which events may a attendee be offered" rule is reached from. The rule
/// itself lives in the database from Task 11 onward: this asks the eligibility port for ordered
/// identifiers and hydrates them, so an invite and a single replacement option are chosen by the
/// same statement.
/// </summary>
/// <param name="eligibility">The eligibility query.</param>
/// <param name="events">The events.</param>
/// <param name="clock">The clock.</param>
public sealed class EligibleEventFinder(
    IEventEligibilityQuery eligibility,
    IEventRepository events,
    IClock clock)
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
        var ids = await eligibility.FindEligibleEventsAsync(
            requiredAppointmentTypeIds,
            Locations,
            excludeEventIds,
            take,
            clock.UtcNow,
            cancellationToken);

        if (ids.Count == 0)
        {
            return [];
        }

        var loaded = (await events.ListByIdsAsync(ids, cancellationToken))
            .ToDictionary(eventItem => eventItem.Id);

        // The query decided the order; hydrating must not quietly re-impose another one.
        return [.. ids.Where(loaded.ContainsKey).Select(id => loaded[id])];
    }

    /// <summary>How many events the attendee could be offered, ignoring any option limit.</summary>
    /// <param name="requiredAppointmentTypeIds">The required appointment type ids.</param>
    /// <param name="excludeEventIds">The exclude event ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task<int> CountAsync(
        IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
        IReadOnlyCollection<Guid> excludeEventIds,
        CancellationToken cancellationToken) =>
        eligibility.CountEligibleEventsAsync(
            requiredAppointmentTypeIds,
            Locations,
            excludeEventIds,
            clock.UtcNow,
            cancellationToken);

    // Invites are restricted to the transitional location until Task 14, whose InviteAttendee
    // command carries the Coordinator's own selection of locations.
    private static IReadOnlyCollection<Guid> Locations => [TransitionalLocation.Id];
}
