using System.ComponentModel;
using EventBooking.Api.Auth;
using EventBooking.Application.ReferenceData;
using ModelContextProtocol.Server;

namespace EventBooking.Mcp.Tools;

/// <summary>The nine Admin-managed reference-data tools.</summary>
[McpServerToolType]
public sealed class ReferenceDataTools
{
    /// <summary>Lists locations.</summary>
    /// <param name="handler">The list handler.</param>
    /// <param name="includeInactive">Whether retired sites are included.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The locations.</returns>
    [McpServerTool(
        Name = "list_locations", Title = "List locations",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reads every location in code order. Requires staff auth.")]
    public async Task<IReadOnlyList<LocationListItem>> ListLocationsAsync(
        ListLocationsHandler handler,
        [Description("Include retired locations.")] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new ListLocationsQuery(includeInactive), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Creates a location.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The create handler.</param>
    /// <param name="code">The canonical code.</param>
    /// <param name="name">The display name.</param>
    /// <param name="address">The postal address.</param>
    /// <param name="timeZoneId">The IANA zone.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created location.</returns>
    [McpServerTool(
        Name = "create_location", Title = "Create location",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Creates a location with its code, name, address and IANA zone.")]
    public async Task<LocationResult> CreateLocationAsync(
        ICallerAccessor caller,
        CreateLocationHandler handler,
        [Description("Canonical uppercase snake-case code.")] string code,
        [Description("Display name.")] string name,
        [Description("Postal address.")] string address,
        [Description("IANA time-zone identifier, for example Europe/London.")] string timeZoneId,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new CreateLocationCommand(
                caller.RequireStaffUserId(), code, name, address, timeZoneId),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Updates a location.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The update handler.</param>
    /// <param name="locationId">The location to update.</param>
    /// <param name="name">The display name.</param>
    /// <param name="address">The postal address.</param>
    /// <param name="timeZoneId">The IANA zone.</param>
    /// <param name="isActive">Whether the location stays in use.</param>
    /// <param name="expectedVersion">The version the caller read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated location.</returns>
    [McpServerTool(
        Name = "update_location", Title = "Update location",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Updates a location's name, address, zone and active flag. Refuses a zone change or a deactivation while the location has open proposals or future events.")]
    public async Task<LocationResult> UpdateLocationAsync(
        ICallerAccessor caller,
        UpdateLocationHandler handler,
        [Description("The location identifier.")] Guid locationId,
        [Description("Display name.")] string name,
        [Description("Postal address.")] string address,
        [Description("IANA time-zone identifier.")] string timeZoneId,
        [Description("Whether the location stays in use.")] bool isActive,
        [Description("The version you read.")] long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new UpdateLocationCommand(
                caller.RequireStaffUserId(), locationId, name, address, timeZoneId, isActive,
                expectedVersion),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Lists appointment types.</summary>
    /// <param name="handler">The list handler.</param>
    /// <param name="includeInactive">Whether retired types are included.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The appointment types.</returns>
    [McpServerTool(
        Name = "list_appointment_types", Title = "List appointment types",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reads every appointment type with its current Manager's display name.")]
    public async Task<IReadOnlyList<AppointmentTypeListItem>> ListAppointmentTypesAsync(
        ListAppointmentTypesHandler handler,
        [Description("Include retired types.")] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new ListAppointmentTypesQuery(includeInactive), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Creates an appointment type.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The create handler.</param>
    /// <param name="code">The canonical code.</param>
    /// <param name="name">The display name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created type.</returns>
    [McpServerTool(
        Name = "create_appointment_type", Title = "Create appointment type",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Creates an appointment type with its code and name.")]
    public async Task<AppointmentTypeResult> CreateAppointmentTypeAsync(
        ICallerAccessor caller,
        CreateAppointmentTypeHandler handler,
        [Description("Canonical uppercase snake-case code.")] string code,
        [Description("Display name.")] string name,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new CreateAppointmentTypeCommand(caller.RequireStaffUserId(), code, name),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Updates an appointment type.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The update handler.</param>
    /// <param name="appointmentTypeId">The type to update.</param>
    /// <param name="name">The display name.</param>
    /// <param name="isActive">Whether the type stays in use.</param>
    /// <param name="expectedVersion">The version the caller read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated type.</returns>
    [McpServerTool(
        Name = "update_appointment_type", Title = "Update appointment type",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Updates an appointment type's name and active flag. Refuses a deactivation while it is listed on an open proposal or a future event, or mapped by an active group.")]
    public async Task<AppointmentTypeResult> UpdateAppointmentTypeAsync(
        ICallerAccessor caller,
        UpdateAppointmentTypeHandler handler,
        [Description("The appointment type identifier.")] Guid appointmentTypeId,
        [Description("Display name.")] string name,
        [Description("Whether the type stays in use.")] bool isActive,
        [Description("The version you read.")] long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new UpdateAppointmentTypeCommand(
                caller.RequireStaffUserId(), appointmentTypeId, name, isActive, expectedVersion),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Lists attendee groups.</summary>
    /// <param name="handler">The list handler.</param>
    /// <param name="includeInactive">Whether retired groups are included.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The attendee groups.</returns>
    [McpServerTool(
        Name = "list_attendee_groups", Title = "List attendee groups",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reads every attendee group with its requirement type ids and member count.")]
    public async Task<IReadOnlyList<AttendeeGroupListItem>> ListAttendeeGroupsAsync(
        ListAttendeeGroupsHandler handler,
        [Description("Include retired groups.")] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new ListAttendeeGroupsQuery(includeInactive), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Creates an attendee group.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The create handler.</param>
    /// <param name="code">The canonical code.</param>
    /// <param name="name">The display name.</param>
    /// <param name="appointmentTypeIds">The types its members require.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created group.</returns>
    [McpServerTool(
        Name = "create_attendee_group", Title = "Create attendee group",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Creates an attendee group mapping at least one active appointment type.")]
    public async Task<AttendeeGroupResult> CreateAttendeeGroupAsync(
        ICallerAccessor caller,
        CreateAttendeeGroupHandler handler,
        [Description("Canonical uppercase snake-case code.")] string code,
        [Description("Display name.")] string name,
        [Description("Appointment types every member requires.")] Guid[] appointmentTypeIds,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new CreateAttendeeGroupCommand(
                caller.RequireStaffUserId(), code, name, appointmentTypeIds),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Updates an attendee group.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The update handler.</param>
    /// <param name="attendeeGroupId">The group to update.</param>
    /// <param name="name">The display name.</param>
    /// <param name="appointmentTypeIds">The replacement mapping, or null to leave it alone.</param>
    /// <param name="isActive">Whether the group stays in use.</param>
    /// <param name="expectedVersion">The version the caller read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated group.</returns>
    [McpServerTool(
        Name = "update_attendee_group", Title = "Update attendee group",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Updates a group's name, requirement mapping and active flag. A mapping change re-derives every member's requirements and is refused while any member holds an active original booking (FR-1.5).")]
    public async Task<AttendeeGroupResult> UpdateAttendeeGroupAsync(
        ICallerAccessor caller,
        UpdateAttendeeGroupHandler handler,
        [Description("The attendee group identifier.")] Guid attendeeGroupId,
        [Description("Display name.")] string name,
        [Description("Whether the group stays in use.")] bool isActive,
        [Description("The version you read.")] long expectedVersion,
        [Description("Replacement requirement mapping, or omit to leave it unchanged.")]
        Guid[]? appointmentTypeIds = null,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new UpdateAttendeeGroupCommand(
                caller.RequireStaffUserId(), attendeeGroupId, name, appointmentTypeIds,
                isActive, expectedVersion),
            cancellationToken);
        return result.ValueOrThrow();
    }
}
