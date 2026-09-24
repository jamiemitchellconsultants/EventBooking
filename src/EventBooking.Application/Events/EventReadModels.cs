namespace EventBooking.Application.Events;

/// <summary>What an Event read is allowed to show, resolved once per request.</summary>
/// <param name="AllTypes">True for a caller holding ViewEventOperations.</param>
/// <param name="AppointmentTypeId">The Manager's own scope.</param>
public sealed record EventScope(bool AllTypes, Guid? AppointmentTypeId);

/// <summary>Design 05's four filters on GET /api/events, plus the page.</summary>
/// <param name="StaffUserId">The calling staff identity.</param>
/// <param name="LocationId">The location filter, or null for every site.</param>
/// <param name="From">Inclusive local-date lower bound, or null.</param>
/// <param name="To">Inclusive local-date upper bound, or null.</param>
/// <param name="AppointmentTypeId">The listed-type filter, or null for every type.</param>
/// <param name="Cursor">The keyset cursor, or null for the first page.</param>
/// <param name="Limit">The page size.</param>
public sealed record ListEventsQuery(
    Guid StaffUserId, Guid? LocationId, DateOnly? From, DateOnly? To,
    Guid? AppointmentTypeId, string? Cursor, int Limit);

/// <summary>Reads one event.</summary>
/// <param name="StaffUserId">The calling staff identity.</param>
/// <param name="EventId">The event.</param>
public sealed record GetEventQuery(Guid StaffUserId, Guid EventId);

/// <summary>FR-7.2: the events a cancellation could still reach.</summary>
/// <param name="StaffUserId">The calling staff identity.</param>
/// <param name="LocationId">The location filter, or null for every site.</param>
/// <param name="From">Inclusive local-date lower bound, or null.</param>
/// <param name="To">Inclusive local-date upper bound, or null.</param>
/// <param name="Cursor">The keyset cursor, or null for the first page.</param>
/// <param name="Limit">The page size.</param>
public sealed record ListCancellableEventsQuery(
    Guid StaffUserId, Guid? LocationId, DateOnly? From, DateOnly? To, string? Cursor, int Limit);

/// <summary>One type's capacity row on an event.</summary>
/// <param name="AppointmentTypeId">The appointment type.</param>
/// <param name="Code">The type code.</param>
/// <param name="Name">The type name.</param>
/// <param name="TotalHeadcount">The total headcount.</param>
/// <param name="RemainingCapacity">The remaining capacity.</param>
public sealed record EventCapacityView(
    Guid AppointmentTypeId, string Code, string Name, int TotalHeadcount, int RemainingCapacity);

/// <summary>One listed event with its visible capacity rows.</summary>
/// <param name="EventId">The event.</param>
/// <param name="ProposalId">The proposal it was confirmed from.</param>
/// <param name="LocationId">The hosting location.</param>
/// <param name="LocationCode">The location code.</param>
/// <param name="LocationName">The location name.</param>
/// <param name="TimeZoneId">The location's time zone.</param>
/// <param name="Date">The window's local date.</param>
/// <param name="StartTime">The window's local start time.</param>
/// <param name="DurationMinutes">The window length.</param>
/// <param name="Status">The event status name.</param>
/// <param name="Capacities">The capacity rows the caller's scope may see.</param>
/// <param name="ActiveBookings">The active booking count.</param>
/// <param name="Cursor">The keyset cursor for this row.</param>
public sealed record EventView(
    Guid EventId, Guid ProposalId, Guid LocationId, string LocationCode, string LocationName,
    string TimeZoneId, DateOnly Date, TimeOnly StartTime, int DurationMinutes, string Status,
    IReadOnlyList<EventCapacityView> Capacities, int ActiveBookings, string Cursor);

/// <summary>One page of listed events.</summary>
/// <param name="Items">The rows.</param>
/// <param name="NextCursor">The next cursor, or null on the last page.</param>
public sealed record EventListView(IReadOnlyList<EventView> Items, string? NextCursor);

/// <summary>
/// The read side. Implementations project directly and apply the scope themselves, so a caller
/// that reached the query without a capability check still sees only its own type's capacity.
/// </summary>
public interface IEventReadQueries
{
    /// <summary>Lists one over-read keyset page.</summary>
    /// <param name="query">The filters and the page.</param>
    /// <param name="scope">The caller's scope.</param>
    /// <param name="now">The instant not-started is judged at.</param>
    /// <param name="notStartedOnly">Whether to exclude started or cancelled events.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<IReadOnlyList<EventView>> ListAsync(
        ListEventsQuery query, EventScope scope, DateTimeOffset now, bool notStartedOnly,
        CancellationToken ct);

    /// <summary>Reads one event, or null when absent or outside the scope.</summary>
    /// <param name="eventId">The event.</param>
    /// <param name="scope">The caller's scope.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<EventView?> GetAsync(Guid eventId, EventScope scope, CancellationToken ct);
}
