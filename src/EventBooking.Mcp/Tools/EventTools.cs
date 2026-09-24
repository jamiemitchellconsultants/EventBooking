using System.ComponentModel;
using EventBooking.Api.Auth;
using EventBooking.Application.Events;
using EventBooking.Application.Negotiation;
using ModelContextProtocol.Server;

using ApplicationCancelEventHandler = EventBooking.Application.Events.CancelEventHandler;

namespace EventBooking.Mcp.Tools;

/// <summary>The confirmed-event tools.</summary>
[McpServerToolType]
public sealed class EventTools
{
    /// <summary>Lists events.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The list handler.</param>
    /// <param name="limit">The page size.</param>
    /// <param name="locationId">The location filter, or null for every site.</param>
    /// <param name="from">The earliest start day, yyyy-MM-dd, or null.</param>
    /// <param name="to">The latest start day, yyyy-MM-dd, or null.</param>
    /// <param name="appointmentTypeId">The type filter, or null for every type.</param>
    /// <param name="cursor">The page cursor, or null for the first page.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>One page of events.</returns>
    [McpServerTool(
        Name = "list_events", Title = "List events",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reads events filtered by location, date range and appointment type. A Manager sees their own type's capacity; an Admin or Coordinator sees every type.")]
    public async Task<EventListView> ListEventsAsync(
        ICallerAccessor caller,
        ListEventsHandler handler,
        [Description("Page size, 1 to 200.")] int limit = 50,
        [Description("Narrow to one location.")] Guid? locationId = null,
        [Description("Earliest start day, yyyy-MM-dd.")] string? from = null,
        [Description("Latest start day, yyyy-MM-dd.")] string? to = null,
        [Description("Narrow to one appointment type.")] Guid? appointmentTypeId = null,
        [Description("The nextCursor from the previous page.")] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new ListEventsQuery(
                caller.RequireStaffUserId(), locationId, ParseDate(from), ParseDate(to),
                appointmentTypeId, cursor, limit),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Reads one event.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The read handler.</param>
    /// <param name="eventId">The event.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The event.</returns>
    [McpServerTool(
        Name = "get_event", Title = "Get event",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reads one event with its capacities, filtered the same way the list is. An event outside the caller's scope reads as not found.")]
    public async Task<EventView> GetEventAsync(
        ICallerAccessor caller,
        GetEventHandler handler,
        [Description("The event identifier.")] Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new GetEventQuery(caller.RequireStaffUserId(), eventId), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Replaces one type's total headcount on an event.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The adjust handler.</param>
    /// <param name="eventId">The event.</param>
    /// <param name="appointmentTypeId">The caller's own type.</param>
    /// <param name="totalHeadcount">The replacement total.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The adjusted capacity.</returns>
    [McpServerTool(
        Name = "adjust_event_capacity", Title = "Adjust event capacity",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Replaces the total headcount for one appointment type on an active event. The type must be the caller's own, and the total must still cover every active booking.")]
    public async Task<AdjustEventCapacityOutcome> AdjustEventCapacityAsync(
        ICallerAccessor caller,
        AdjustEventCapacityHandler handler,
        [Description("The event identifier.")] Guid eventId,
        [Description("Your own appointment type.")] Guid appointmentTypeId,
        [Description("The replacement total.")] int totalHeadcount,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new AdjustEventCapacityCommand(
                caller.RequireStaffUserId(), eventId, totalHeadcount, appointmentTypeId),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Cancels an event, re-inviting the affected attendees.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The cancel handler.</param>
    /// <param name="eventId">The event.</param>
    /// <param name="confirm">Pass true once the consequence has been shown.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The consequence, or the cancellation once confirmed.</returns>
    [McpServerTool(
        Name = "cancel_event", Title = "Cancel event",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Cancels an event, voiding its bookings and re-inviting the affected attendees. Two-step: without confirm=true the call reports its consequence and changes nothing.")]
    public async Task<CancelEventOutcome> CancelEventAsync(
        ICallerAccessor caller,
        ApplicationCancelEventHandler handler,
        [Description("The event identifier.")] Guid eventId,
        [Description("Pass true once you have shown the consequence.")] bool confirm = false,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new CancelEventCommand(caller.RequireStaffUserId(), eventId, confirm),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Lists the active future events a cancellation can still reach.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The list handler.</param>
    /// <param name="limit">The page size.</param>
    /// <param name="locationId">The location filter, or null for every site.</param>
    /// <param name="from">The earliest start day, yyyy-MM-dd, or null.</param>
    /// <param name="to">The latest start day, yyyy-MM-dd, or null.</param>
    /// <param name="cursor">The page cursor, or null for the first page.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>One page of cancellable events.</returns>
    [McpServerTool(
        Name = "list_cancellable_events", Title = "List cancellable events",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reads the active events whose window has not started, which are the ones a cancellation can still reach (FR-7.2).")]
    public async Task<EventListView> ListCancellableEventsAsync(
        ICallerAccessor caller,
        ListCancellableEventsHandler handler,
        [Description("Page size, 1 to 200.")] int limit = 50,
        [Description("Narrow to one location.")] Guid? locationId = null,
        [Description("Earliest start day, yyyy-MM-dd.")] string? from = null,
        [Description("Latest start day, yyyy-MM-dd.")] string? to = null,
        [Description("The nextCursor from the previous page.")] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new ListCancellableEventsQuery(
                caller.RequireStaffUserId(), locationId, ParseDate(from), ParseDate(to),
                cursor, limit),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Parses an optional calendar day or throws a plain refusal.</summary>
    private static DateOnly? ParseDate(string? value)
    {
        if (value is null)
        {
            return null;
        }

        if (!DateOnly.TryParse(value, out var parsed))
        {
            throw new ModelContextProtocol.McpException(
                "A yyyy-MM-dd date is required.");
        }

        return parsed;
    }
}
