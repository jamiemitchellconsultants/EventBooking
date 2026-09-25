using EventBooking.Domain.Events;
using EventBooking.Domain.Time;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence;

/// <summary>
/// Writes <c>start_utc</c>, the derived persistence column the eligibility query orders and
/// indexes on. It is not domain data: the window and the location's zone are the source of truth,
/// and this is the one function that turns them into an instant (design 04 — invite selection).
///
/// PostgreSQL cannot evaluate IANA rules in a generated column deterministically, so the value has
/// to be written by whoever inserts the row. The repository writes it as it adds the event, and
/// the context writes it for any other writer at save time, so the column cannot be left empty by
/// a path that has not heard of the rule.
/// </summary>
public static class EventStartInstants
{
    /// <summary>The shadow property that carries the column.</summary>
    public const string PropertyName = "StartUtc";

    /// <summary>Computes and stamps one tracked event's start instant.</summary>
    /// <param name="context">The context tracking the event.</param>
    /// <param name="eventItem">The event.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    /// <param name="zones">The zone abstraction.</param>
    public static void Stamp(
        DbContext context,
        Event eventItem,
        string timeZoneId,
        IEventWindowZones zones)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(eventItem);

        context.Entry(eventItem).Property<DateTimeOffset?>(PropertyName).CurrentValue =
            InstantOf(eventItem, timeZoneId, zones);
    }

    /// <summary>
    /// Fills in the start instant for every event being inserted that has not had one computed.
    /// The seeder and the suites add events straight through the context; this is what keeps the
    /// column's promise for them without each of them restating the rule.
    /// </summary>
    /// <param name="context">The context about to save.</param>
    /// <param name="zones">The zone abstraction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task StampPendingAsync(
        EventBookingDbContext context,
        IEventWindowZones zones,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var pending = context.ChangeTracker.Entries<Event>()
            .Where(entry => entry.State == EntityState.Added)
            .Where(entry => entry.Property<DateTimeOffset?>(PropertyName).CurrentValue is null)
            .ToList();

        if (pending.Count == 0)
        {
            return;
        }

        var locationIds = pending.Select(entry => entry.Entity.LocationId).Distinct().ToArray();
        var zonesByLocation = await context.Locations
            .Where(location => locationIds.Contains(location.Id))
            .Select(location => new { location.Id, location.TimeZoneId })
            .ToDictionaryAsync(row => row.Id, row => row.TimeZoneId, cancellationToken);

        foreach (var entry in pending)
        {
            var locationId = entry.Entity.LocationId;
            zonesByLocation.TryGetValue(locationId, out var timeZoneId);

            entry.Property<DateTimeOffset?>(PropertyName).CurrentValue =
                InstantOf(entry.Entity, Required(timeZoneId, locationId), zones);
        }
    }

    /// <summary>
    /// The window's start, as an instant at UTC. The resolver answers with the location's own
    /// offset, and the column stores an instant with no offset of its own, so the value is
    /// normalised here rather than at each caller — PostgreSQL refuses any other offset outright.
    /// </summary>
    /// <param name="eventItem">The event.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    /// <param name="zones">The zone abstraction.</param>
    private static DateTimeOffset InstantOf(
        Event eventItem, string timeZoneId, IEventWindowZones zones) =>
        InstantOf(eventItem.Window, timeZoneId, zones);

    /// <summary>
    /// The column value for a window at a location, for a writer that moves an event's window
    /// in SQL rather than through a tracked entity and so must restamp the column itself.
    /// </summary>
    /// <param name="window">The event's window.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    /// <param name="zones">The zone abstraction.</param>
    public static DateTimeOffset InstantOf(
        EventWindow window, string timeZoneId, IEventWindowZones zones)
    {
        ArgumentNullException.ThrowIfNull(window);

        return window.StartInstant(zones, timeZoneId).ToUniversalTime();
    }

    /// <summary>The zone an event's location must have, refusing a location that is not persisted.</summary>
    /// <param name="timeZoneId">The zone read for that location, or null if there was no row.</param>
    /// <param name="locationId">The location the event is at.</param>
    public static string Required(string? timeZoneId, Guid locationId) =>
        timeZoneId
        ?? throw new InvalidOperationException(
            $"Location {locationId} has no persisted row, so the event's start instant cannot be "
            + "computed. Seed the location before the event.");
}
