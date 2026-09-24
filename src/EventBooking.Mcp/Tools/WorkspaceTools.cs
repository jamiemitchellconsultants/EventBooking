using System.ComponentModel;
using EventBooking.Api.Auth;
using EventBooking.Application.Appointments;
using EventBooking.Domain.Bookings;
using ModelContextProtocol.Server;

namespace EventBooking.Mcp.Tools;

/// <summary>The appointment workspace: selector, roster, status and CSV export.</summary>
[McpServerToolType]
public sealed class WorkspaceTools
{
    /// <summary>Lists the selector events.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The list handler.</param>
    /// <param name="locationId">The location filter, or null for every site.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The events listing the caller's type in the workspace window.</returns>
    [McpServerTool(
        Name = "list_workspace_events", Title = "List workspace events",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reads the events listing the caller's type and ending between seven days ago and fourteen days ahead.")]
    public async Task<IReadOnlyList<WorkspaceEventView>> ListWorkspaceEventsAsync(
        ICallerAccessor caller,
        ListWorkspaceEventsHandler handler,
        [Description("Narrow to one location.")] Guid? locationId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new ListWorkspaceEventsQuery(caller.RequireStaffUserId(), locationId),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Reads one event's minimum-data roster.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The roster handler.</param>
    /// <param name="eventId">The event.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The roster rows.</returns>
    [McpServerTool(
        Name = "get_workspace_roster", Title = "Get workspace roster",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reads the minimum-data roster for one event (FR-8.1): name, email, the caller's own type's appointment status, and nothing else.")]
    public async Task<IReadOnlyList<WorkspaceRosterRow>> GetWorkspaceRosterAsync(
        ICallerAccessor caller,
        GetWorkspaceRosterHandler handler,
        [Description("The event identifier.")] Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new GetWorkspaceRosterQuery(caller.RequireStaffUserId(), eventId),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Moves one booking appointment to a target status.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The status handler.</param>
    /// <param name="appointmentId">The booking appointment.</param>
    /// <param name="targetStatus">The wanted status name.</param>
    /// <param name="expectedVersion">The version the caller read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The appointment's row-local state after the move.</returns>
    [McpServerTool(
        Name = "set_appointment_status", Title = "Set appointment status",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Moves one booking appointment to a target status, or applies a bounded correction. A same-status submission is idempotent.")]
    public async Task<BookingAppointmentUpdateView> SetAppointmentStatusAsync(
        ICallerAccessor caller,
        UpdateBookingAppointmentStatusHandler handler,
        [Description("The booking appointment identifier.")] Guid appointmentId,
        [Description("The wanted status name.")] string targetStatus,
        [Description("The version you read.")] long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<BookingAppointmentStatus>(targetStatus, out var status)
            || !Enum.IsDefined(status)
            || expectedVersion <= 0)
        {
            throw new ModelContextProtocol.McpException(
                "A recognised status and positive expectedVersion are required.");
        }

        var result = await handler.HandleAsync(
            new UpdateBookingAppointmentStatusCommand
            {
                StaffUserId = caller.RequireStaffUserId(),
                BookingAppointmentId = appointmentId,
                Status = status,
                ExpectedVersion = expectedVersion,
            },
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Downloads one event's roster as CSV text.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The download handler.</param>
    /// <param name="eventId">The event.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The roster as CSV text.</returns>
    [McpServerTool(
        Name = "export_workspace_roster", Title = "Export workspace roster",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Downloads the roster as CSV (FR-8.7, FR-8.8), with formula characters neutralised.")]
    public async Task<string> ExportWorkspaceRosterAsync(
        ICallerAccessor caller,
        DownloadRosterHandler handler,
        [Description("The event identifier.")] Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new GetWorkspaceRosterQuery(caller.RequireStaffUserId(), eventId),
            cancellationToken);
        return result.ValueOrThrow();
    }
}
