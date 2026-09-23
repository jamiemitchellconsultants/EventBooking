using System.ComponentModel;
using EventBooking.Api.Auth;
using EventBooking.Api.Endpoints;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Appointments;
using EventBooking.Application.Dashboards;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using ModelContextProtocol.Server;

namespace EventBooking.Mcp.Tools;

/// <summary>Dashboards, audit history, and the appointment workspace.</summary>
[McpServerToolType]
public sealed class OperationsTools
{
    /// <summary>Returns the coordinator dashboards for the caller's scope.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The dashboards handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The dashboards view.</returns>
    [McpServerTool(Name = "get_dashboards", Title = "Get dashboards", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Return the coordinator dashboards visible to the caller. Caller must be a coordinator or admin.")]
    public async Task<DashboardsView> GetDashboardsAsync(
        ICallerAccessor caller,
        GetDashboardsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetDashboardsQuery(caller.RequireStaffUserId()), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Returns the audit history of one eventItem.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The audit history handler.</param>
    /// <param name="eventId">The event identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The audit rows.</returns>
    [McpServerTool(Name = "event_audit_history", Title = "Event audit history", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Return the audit history of one eventItem. Caller must have audit visibility (Admin or Coordinator).")]
    public async Task<IReadOnlyList<AuditHistoryRow>> GetEventAuditHistoryAsync(
        ICallerAccessor caller,
        GetAuditHistoryHandler handler,
        [Description("The event identifier.")] Guid eventId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetAuditHistoryQuery(
                caller.RequireStaffUserId(), AuditEntityTypes.Event, eventId),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Returns the audit history of one attendee.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The audit history handler.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The audit rows.</returns>
    [McpServerTool(Name = "attendee_audit_history", Title = "Attendee audit history", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Return the audit history of one attendee. Caller must have audit visibility (Admin or Coordinator).")]
    public async Task<IReadOnlyList<AuditHistoryRow>> GetAttendeeAuditHistoryAsync(
        ICallerAccessor caller,
        GetAuditHistoryHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetAuditHistoryQuery(caller.RequireStaffUserId(), null, attendeeId),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Lists the appointment workspace events for the caller's scope.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The workspace handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The workspace event list.</returns>
    [McpServerTool(Name = "appointment_events", Title = "Appointment events", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("List current and upcoming active events with appointment counts for your scope. Caller must be a coordinator, admin, or manager within scope.")]
    public async Task<AppointmentWorkspaceEventList> ListAppointmentEventsAsync(
        ICallerAccessor caller,
        GetAppointmentWorkspaceHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ListEventsAsync(
            caller.RequireStaffUserId(), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Returns one appointment workspace event with its operational rows.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The workspace handler.</param>
    /// <param name="eventId">The event identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The event detail.</returns>
    [McpServerTool(Name = "appointment_event_detail", Title = "Appointment event detail", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Return one active event with its minimum-data appointment rows. Caller must be a coordinator, admin, or manager within scope.")]
    public async Task<AppointmentEventDetail> GetAppointmentEventAsync(
        ICallerAccessor caller,
        GetAppointmentWorkspaceHandler handler,
        [Description("The event identifier.")] Guid eventId,
        CancellationToken cancellationToken)
    {
        var result = await handler.GetEventAsync(
            caller.RequireStaffUserId(), eventId, cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Searches audit events with optional filters and cursor paging.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The audit search handler.</param>
    /// <param name="from">Inclusive lower timestamp bound, or null for no lower bound.</param>
    /// <param name="to">Inclusive upper timestamp bound, or null for no upper bound.</param>
    /// <param name="actorType">Actor type name to match, or null for any.</param>
    /// <param name="action">Audit action name to match, or null for any.</param>
    /// <param name="identifier">Free-text identifier matched against entity or actor id.</param>
    /// <param name="entityType">Optional entity type within the caller's allowed bucket.</param>
    /// <param name="cursor">Opaque keyset cursor, or null for the newest page.</param>
    /// <param name="pageSize">Rows per page; defaults to 50 and is clamped to 200.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching audit page; an empty cursor means no further pages.</returns>
    [McpServerTool(Name = "search_audit", Title = "Search audit", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Search audit events with optional from/to timestamp bounds, actorType, action, identifier, and entityType filters plus an opaque cursor. Page size defaults to 50 and is clamped to 200. Entity visibility is scoped to the caller's audit capabilities.")]
    public async Task<AuditSearchPage> SearchAuditAsync(
        ICallerAccessor caller,
        GetAuditSearchHandler handler,
        [Description("Inclusive lower timestamp bound, or null for no lower bound.")] string? from = null,
        [Description("Inclusive upper timestamp bound, or null for no upper bound.")] string? to = null,
        [Description("Actor type name to match, or null for any.")] string? actorType = null,
        [Description("Audit action name to match, or null for any.")] string? action = null,
        [Description("Free-text identifier matched against entity or actor id.")] string? identifier = null,
        [Description("Optional entity type within the caller's allowed bucket.")] string? entityType = null,
        [Description("Opaque keyset cursor, or null for the newest page.")] string? cursor = null,
        [Description("Rows per page; defaults to 50 and is clamped to 200.")] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (!AuditInputParser.TryParseBound(from, out var fromBound) ||
            !AuditInputParser.TryParseBound(to, out var toBound))
        {
            throw new ModelContextProtocol.McpException("The from/to bound is not a valid timestamp.");
        }

        var result = await handler.HandleAsync(
            new GetAuditSearchQuery(
                caller.RequireStaffUserId(),
                fromBound,
                toBound,
                actorType,
                action,
                identifier,
                entityType,
                cursor,
                pageSize <= 0 ? 50 : pageSize),
            cancellationToken);
        var page = result.ValueOrThrow();
        return page.NextCursor is null ? page with { NextCursor = string.Empty } : page;
    }

    /// <summary>Exports one scoped event roster as CSV text with its download filename.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The workspace handler.</param>
    /// <param name="formatter">The roster CSV formatter.</param>
    /// <param name="eventId">The event identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The roster CSV text and download filename.</returns>
    [McpServerTool(Name = "export_appointment_roster", Title = "Export appointment roster", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Export one appointment roster as CSV text with its download filename. Requires the ConductAppointments scope; the CSV is returned as text, not a server-side file.")]
    public async Task<RosterCsvResult> ExportAppointmentRosterAsync(
        ICallerAccessor caller,
        GetAppointmentWorkspaceHandler handler,
        AppointmentRosterCsvFormatter formatter,
        [Description("The event identifier.")] Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.GetEventAsync(
            caller.RequireStaffUserId(), eventId, cancellationToken);
        return formatter.Format(result.ValueOrThrow());
    }

    /// <summary>Records check-in, completion, no-show, or a bounded correction.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The status handler.</param>
    /// <param name="bookingAppointmentId">The booking appointment identifier.</param>
    /// <param name="status">The requested status name.</param>
    /// <param name="expectedVersion">The positive version last observed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated appointment row.</returns>
    [McpServerTool(Name = "update_appointment_status", Title = "Update appointment status", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Check in, complete, mark no-show, or correct one booked appointment. Caller must be a coordinator or admin; mutates the appointment status.")]
    public async Task<BookingAppointmentUpdateView> UpdateAppointmentStatusAsync(
        ICallerAccessor caller,
        UpdateBookingAppointmentStatusHandler handler,
        [Description("The booking appointment identifier.")] Guid bookingAppointmentId,
        [Description("Requested status name: Expected, CheckedIn, Completed, or NoShow.")] string status,
        [Description("Positive version last observed by the caller.")] long expectedVersion,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<BookingAppointmentStatus>(status, ignoreCase: false, out var parsed) ||
            !Enum.IsDefined(parsed) ||
            expectedVersion <= 0)
        {
            throw new ModelContextProtocol.McpException(
                "A recognised status and positive expectedVersion are required.");
        }

        var result = await handler.HandleAsync(
            new UpdateBookingAppointmentStatusCommand
            {
                StaffUserId = caller.RequireStaffUserId(),
                BookingAppointmentId = bookingAppointmentId,
                Status = parsed,
                ExpectedVersion = expectedVersion,
            },
            cancellationToken);
        return result.ValueOrThrow();
    }
}
