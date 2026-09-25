using System.ComponentModel;
using EventBooking.Api.Auth;
using EventBooking.Application.EventGroups;
using ModelContextProtocol.Server;

namespace EventBooking.Mcp.Tools;

/// <summary>The seven staff event-group tools.</summary>
[McpServerToolType]
public sealed class EventGroupTools
{
    /// <summary>Lists every event group.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The list handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Every event group.</returns>
    [McpServerTool(
        Name = "list_event_groups", Title = "List event groups",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reads every event group with its selected attendee groups and memberships.")]
    public async Task<IReadOnlyList<EventGroupResult>> ListEventGroupsAsync(
        ICallerAccessor caller,
        ListEventGroupsHandler handler,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.ListAsync(
            caller.RequireStaffUserId(), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Reads one event group.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The list handler.</param>
    /// <param name="eventGroupId">The event group identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The event group.</returns>
    [McpServerTool(
        Name = "get_event_group", Title = "Read one event group",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reads one event group with its selected attendee groups and memberships.")]
    public async Task<EventGroupResult> GetEventGroupAsync(
        ICallerAccessor caller,
        ListEventGroupsHandler handler,
        [Description("The event group identifier.")] Guid eventGroupId,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.GetAsync(
            caller.RequireStaffUserId(), eventGroupId, cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Creates an event group.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The manage handler.</param>
    /// <param name="title">The public title.</param>
    /// <param name="description">The public description.</param>
    /// <param name="attendeeGroupIds">The selected attendee groups.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created group.</returns>
    [McpServerTool(
        Name = "create_event_group", Title = "Create event group",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Creates a closed event group serving the selected active attendee groups.")]
    public async Task<EventGroupResult> CreateEventGroupAsync(
        ICallerAccessor caller,
        ManageEventGroupHandler handler,
        [Description("Public title, 1 to 160 characters.")] string title,
        [Description("Public description, at most 2000 characters.")] string? description,
        [Description("Selected active attendee groups.")] Guid[] attendeeGroupIds,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.CreateAsync(
            new CreateEventGroupCommand(
                caller.RequireStaffUserId(), title, description, attendeeGroupIds),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Updates an event group.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The manage handler.</param>
    /// <param name="eventGroupId">The event group identifier.</param>
    /// <param name="title">The public title.</param>
    /// <param name="description">The public description.</param>
    /// <param name="attendeeGroupIds">The selected attendee groups.</param>
    /// <param name="isOpen">Whether anonymous registration is open for this group.</param>
    /// <param name="expectedVersion">The version you read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated group.</returns>
    [McpServerTool(
        Name = "update_event_group", Title = "Update event group",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Updates an event group's copy, selected groups and group gate. Replacing the selected groups is refused when the new union would break a member event.")]
    public async Task<EventGroupResult> UpdateEventGroupAsync(
        ICallerAccessor caller,
        ManageEventGroupHandler handler,
        [Description("The event group identifier.")] Guid eventGroupId,
        [Description("Public title, 1 to 160 characters.")] string title,
        [Description("Public description, at most 2000 characters.")] string? description,
        [Description("Selected active attendee groups.")] Guid[] attendeeGroupIds,
        [Description("Whether anonymous registration is open for this group.")] bool isOpen,
        [Description("The version you read.")] long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.UpdateAsync(
            new UpdateEventGroupCommand(
                caller.RequireStaffUserId(), eventGroupId, title, description,
                attendeeGroupIds, isOpen, expectedVersion),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Adds an event to a group.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The manage handler.</param>
    /// <param name="eventGroupId">The event group identifier.</param>
    /// <param name="eventId">The member event identifier.</param>
    /// <param name="expectedVersion">The version you read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated group.</returns>
    [McpServerTool(
        Name = "add_event_group_event", Title = "Add an event to a group",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Adds an active future event whose capacity types equal the group's set. New memberships start private.")]
    public async Task<EventGroupResult> AddEventGroupEventAsync(
        ICallerAccessor caller,
        ManageEventGroupHandler handler,
        [Description("The event group identifier.")] Guid eventGroupId,
        [Description("The member event identifier.")] Guid eventId,
        [Description("The version you read.")] long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.ChangeEventAsync(
            new ChangeEventGroupEventCommand(
                caller.RequireStaffUserId(), eventGroupId, eventId, null, false,
                expectedVersion),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Opens or closes a membership.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The manage handler.</param>
    /// <param name="eventGroupId">The event group identifier.</param>
    /// <param name="eventId">The member event identifier.</param>
    /// <param name="isOpen">Whether anonymous registration is open for this membership.</param>
    /// <param name="expectedVersion">The version you read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated group.</returns>
    [McpServerTool(
        Name = "set_event_group_event_open", Title = "Open or close a membership",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Toggles one event membership's public-registration gate; the group gate is independent.")]
    public async Task<EventGroupResult> SetEventGroupEventOpenAsync(
        ICallerAccessor caller,
        ManageEventGroupHandler handler,
        [Description("The event group identifier.")] Guid eventGroupId,
        [Description("The member event identifier.")] Guid eventId,
        [Description("Whether anonymous registration is open for this membership.")] bool isOpen,
        [Description("The version you read.")] long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.ChangeEventAsync(
            new ChangeEventGroupEventCommand(
                caller.RequireStaffUserId(), eventGroupId, eventId, isOpen, false,
                expectedVersion),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Removes an event from a group.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The manage handler.</param>
    /// <param name="eventGroupId">The event group identifier.</param>
    /// <param name="eventId">The member event identifier.</param>
    /// <param name="expectedVersion">The version you read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated group.</returns>
    [McpServerTool(
        Name = "remove_event_group_event", Title = "Remove an event from a group",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Removes one event membership; existing bookings stay valid.")]
    public async Task<EventGroupResult> RemoveEventGroupEventAsync(
        ICallerAccessor caller,
        ManageEventGroupHandler handler,
        [Description("The event group identifier.")] Guid eventGroupId,
        [Description("The member event identifier.")] Guid eventId,
        [Description("The version you read.")] long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.ChangeEventAsync(
            new ChangeEventGroupEventCommand(
                caller.RequireStaffUserId(), eventGroupId, eventId, null, true,
                expectedVersion),
            cancellationToken);
        return result.ValueOrThrow();
    }
}
