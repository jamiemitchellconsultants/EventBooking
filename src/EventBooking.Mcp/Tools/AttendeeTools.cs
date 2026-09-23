using System.ComponentModel;
using EventBooking.Api.Auth;
using EventBooking.Api.Endpoints;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Attendees;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Attendees;
using ModelContextProtocol.Server;

namespace EventBooking.Mcp.Tools;

public sealed record AttendeeToolView
{
    /// <summary>Gets the attendee identifier for follow-up tool calls.</summary>
    public required Guid AttendeeId { get; init; }
    /// <summary>Gets the attendee display name.</summary>
    public required string Name { get; init; }
    /// <summary>Gets the attendee email address.</summary>
    public required string Email { get; init; }
    /// <summary>Gets the attendee lifecycle status name.</summary>
    public required string Status { get; init; }
    /// <summary>Gets the assigned Attendee Group name.</summary>
    public required string AttendeeGroupName { get; init; }
    /// <summary>Gets the canonical Attendee Group code.</summary>
    public required string AttendeeGroupCode { get; init; }
    /// <summary>Gets read-only derived Appointment Type summaries.</summary>
    public required IReadOnlyList<AppointmentTypeSummary> RequiredAppointmentTypes { get; init; }
    /// <summary>Gets internal readiness without exposing recovery mutation.</summary>
    public required AttendeeReadiness? Readiness { get; init; }
}

/// <summary>Tool-safe readiness including Coordinator display wording.</summary>
/// <param name="AttendeeId">The attendee the readiness was calculated for.</param>
/// <param name="Code">The readiness code name.</param>
/// <param name="Display">The Coordinator-facing display wording for the code.</param>
/// <param name="OutstandingAppointmentTypes">The appointment types still outstanding.</param>
public sealed record AttendeeReadinessToolView(
    Guid AttendeeId,
    string Code,
    string Display,
    IReadOnlyList<OutstandingAppointmentType> OutstandingAppointmentTypes);

/// <summary>Attendee management, invites, and delivery retry for coordinators.</summary>
[McpServerToolType]
public sealed class AttendeeTools
{
    private const int DefaultPageSize = 50;

    private const int MaxPageSize = 200;

    /// <summary>Lists attendees, optionally filtered by status or search text.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The list handler.</param>
    /// <param name="readiness">Resolves internal readiness per listed attendee.</param>
    /// <param name="status">The attendee status name, or null for all.</param>
    /// <param name="search">Free-text filter, or null.</param>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="pageSize">Results per page, clamped to the tool maximum.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The bounded page of attendee views.</returns>
    [McpServerTool(Name = "list_attendees", Title = "List attendees", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("List attendees, optionally filtered by status name and search text. Caller must be a coordinator or admin.")]
    public async Task<IReadOnlyList<AttendeeToolView>> ListAttendeesAsync(
        ICallerAccessor caller,
        ListAttendeesHandler handler,
        GetAttendeeReadinessHandler readiness,
        [Description("Attendee status name (e.g. Invited) or null for all.")] string? status = null,
        [Description("Free-text name or email filter or null.")] string? search = null,
        [Description("1-based page number.")] int page = 1,
        [Description("Results per page, at most 200.")] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        AttendeeStatus? parsed = null;
        if (status is not null)
        {
            if (!Enum.TryParse<AttendeeStatus>(status, ignoreCase: false, out var value) ||
                !Enum.IsDefined(value))
            {
                throw new ModelContextProtocol.McpException(
                    "Status must be a recognised AttendeeStatus name.");
            }

            parsed = value;
        }

        var staffUserId = caller.RequireStaffUserId();
        var result = await handler.HandleAsync(
            new ListAttendeesQuery(staffUserId, parsed, search),
            cancellationToken);
        var bounded = Math.Clamp(pageSize, 1, MaxPageSize);
        var skipped = Math.Max(page - 1, 0) * bounded;

        var views = new List<AttendeeToolView>();
        foreach (var item in result.ValueOrThrow().Skip(skipped).Take(bounded))
        {
            var readinessResult = await readiness.HandleAsync(
                new GetAttendeeReadinessQuery(staffUserId, item.AttendeeId),
                cancellationToken);
            views.Add(new AttendeeToolView
            {
                AttendeeId = item.AttendeeId,
                Name = item.Name,
                Email = item.Email,
                Status = item.Status.ToString(),
                AttendeeGroupName = item.AttendeeGroupName,
                AttendeeGroupCode = item.AttendeeGroupCode,
                RequiredAppointmentTypes = item.RequiredAppointmentTypes,
                Readiness = readinessResult.IsSuccess ? readinessResult.Value : null,
            });
        }

        return views;
    }

    /// <summary>Lists the Attendee Groups available for attendee assignment.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The list handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The assignable groups with their required appointment types.</returns>
    [McpServerTool(Name = "list_attendee_groups", Title = "List attendee groups", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("List the attendee groups that determine attendee requirements. Caller must be a coordinator or admin.")]
    public async Task<IReadOnlyList<AttendeeGroupListItem>> ListAttendeeGroupsAsync(
        ICallerAccessor caller,
        ListAttendeeGroupsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new ListAttendeeGroupsQuery(caller.RequireStaffUserId()),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Creates one attendee in an Attendee Group.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The save handler.</param>
    /// <param name="groups">Resolves the assigned Attendee Group.</param>
    /// <param name="name">The attendee name.</param>
    /// <param name="email">The attendee email.</param>
    /// <param name="attendeeGroupCode">The canonical Attendee Group code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new attendee identifier.</returns>
    [McpServerTool(Name = "create_attendee", Title = "Create attendee", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Create a attendee in one attendee group; requirements derive from the group. Caller must be a coordinator or admin; creates a new attendee record.")]
    public async Task<Guid> CreateAttendeeAsync(
        ICallerAccessor caller,
        SaveAttendeeHandler handler,
        IAttendeeGroupRepository groups,
        [Description("Attendee full name.")] string name,
        [Description("Attendee email address.")] string email,
        [Description("Canonical attendee group code (e.g. PILOTS).")] string attendeeGroupCode,
        CancellationToken cancellationToken)
    {
        var groupId = await ResolveGroupIdAsync(groups, attendeeGroupCode, cancellationToken);
        var result = await handler.CreateAsync(
            new CreateAttendeeCommand(caller.RequireStaffUserId(), name, email, groupId),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Updates a attendee's name, email, or Attendee Group.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The save handler.</param>
    /// <param name="groups">Resolves the assigned Attendee Group.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="name">The corrected name.</param>
    /// <param name="email">The corrected email.</param>
    /// <param name="attendeeGroupCode">The canonical Attendee Group code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation message.</returns>
    [McpServerTool(Name = "update_attendee", Title = "Update attendee", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Update a attendee. Caller must be a coordinator or admin; requirements are frozen while an active booking exists.")]
    public async Task<string> UpdateAttendeeAsync(
        ICallerAccessor caller,
        SaveAttendeeHandler handler,
        IAttendeeGroupRepository groups,
        [Description("The attendee identifier.")] Guid attendeeId,
        [Description("Corrected full name.")] string name,
        [Description("Corrected email address.")] string email,
        [Description("Canonical attendee group code (e.g. PILOTS).")] string attendeeGroupCode,
        CancellationToken cancellationToken)
    {
        var groupId = await ResolveGroupIdAsync(groups, attendeeGroupCode, cancellationToken);
        var result = await handler.UpdateAsync(
            new UpdateAttendeeCommand(
                caller.RequireStaffUserId(), attendeeId, name, email, groupId),
            cancellationToken);
        result.ThrowIfFailure();
        return "Attendee updated.";
    }

    /// <summary>Deletes a attendee, optionally cascading.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The delete handler.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="confirm">Whether dependent data may be removed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation message.</returns>
    [McpServerTool(Name = "delete_attendee", Title = "Delete attendee", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Delete a attendee. Caller must be a coordinator or admin; set confirm to true to also remove dependent data.")]
    public async Task<string> DeleteAttendeeAsync(
        ICallerAccessor caller,
        DeleteAttendeeHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        [Description("Whether dependent data may be removed.")] bool confirm,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new DeleteAttendeeCommand(caller.RequireStaffUserId(), attendeeId, confirm),
            cancellationToken);
        result.ThrowIfFailure();
        return "Attendee deleted.";
    }

    /// <summary>Imports attendees from CSV content.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The import handler.</param>
    /// <param name="csv">The CSV content.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The import outcome.</returns>
    [McpServerTool(Name = "import_attendees", Title = "Import attendees", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Import attendees from CSV content with name, email, and attendee group code. Caller must be a coordinator or admin; creates new attendee records.")]
    public async Task<AttendeeImportOutcome> ImportAttendeesAsync(
        ICallerAccessor caller,
        ImportAttendeesHandler handler,
        [Description("CSV content with one attendee per row.")] string csv,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new ImportAttendeesCommand(caller.RequireStaffUserId(), csv),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Resolves a canonical Attendee Group code to its stable identifier.</summary>
    private static async Task<Guid> ResolveGroupIdAsync(
        IAttendeeGroupRepository groups,
        string attendeeGroupCode,
        CancellationToken cancellationToken)
    {
        var group = await groups.GetByCodeAsync(attendeeGroupCode, cancellationToken);
        if (group is null)
        {
            throw new ModelContextProtocol.McpException(
                $"Attendee group '{attendeeGroupCode}' is not known. Use list_attendee_groups.");
        }

        return group.Id;
    }

    /// <summary>Triggers a fresh invite for a attendee.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The invite handler.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="locationIds">The locations the Coordinator opened for this invite.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The invite issue outcome.</returns>
    [McpServerTool(Name = "trigger_invite", Title = "Trigger invite", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Issue a fresh invite to a attendee at explicit locations. Caller must be a coordinator or admin; stages an invite email for sending.")]
    public async Task<InviteAttendeeOutcome> TriggerInviteAsync(
        ICallerAccessor caller,
        InviteAttendeeHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        [Description("The locations the Coordinator opened for this invite.")] Guid[] locationIds,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new InviteAttendeeCommand(caller.RequireStaffUserId(), attendeeId, locationIds),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Retries the latest unresolved email for a attendee.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The retry handler.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The retry outcome.</returns>
    [McpServerTool(Name = "retry_attendee_email", Title = "Retry attendee email", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Retry the latest unresolved attendee email delivery. Caller must be a coordinator or admin; resends the latest unresolved email.")]
    public async Task<RetryEmailOutcome> RetryAttendeeEmailAsync(
        ICallerAccessor caller,
        RetryEmailHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new RetryEmailCommand(caller.RequireStaffUserId(), attendeeId),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Starts a recovery invite for a attendee with a missed appointment.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The recovery handler.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The recovery invite issue result.</returns>
    [McpServerTool(Name = "start_recovery_invite", Title = "Start recovery invite", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Start a recovery invite for a attendee with a missed appointment. Caller must have ManageAttendees; sends a recovery invite email.")]
    public async Task<StartRecoveryResult> StartRecoveryInviteAsync(
        ICallerAccessor caller,
        StartRecoveryHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new StartRecoveryCommand(caller.RequireStaffUserId(), attendeeId), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Cancels a pending recovery invite for a attendee.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The recovery cancellation handler.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="inviteId">The recovery invite identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation message.</returns>
    [McpServerTool(Name = "cancel_recovery_invite", Title = "Cancel recovery invite", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Cancel a pending recovery invite for a attendee. Caller must have ManageAttendees; cancels the pending recovery invite.")]
    public async Task<string> CancelRecoveryInviteAsync(
        ICallerAccessor caller,
        CancelRecoveryInviteHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        [Description("The recovery invite identifier.")] Guid inviteId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CancelRecoveryInviteCommand(caller.RequireStaffUserId(), attendeeId, inviteId), cancellationToken);
        result.ThrowIfFailure();
        return "Recovery invite cancelled.";
    }

    /// <summary>Lists the active bookings a coordinator may cancel for one attendee.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The bookings handler.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The attendee's active booking summaries.</returns>
    [McpServerTool(Name = "list_attendee_bookings", Title = "List attendee bookings", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("List a attendee's active bookings. Caller must have ManageAttendees; returns cancellable bookings without management tokens.")]
    public async Task<IReadOnlyList<AttendeeBookingSummary>> ListAttendeeBookingsAsync(
        ICallerAccessor caller,
        GetAttendeeBookingsHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetAttendeeBookingsQuery(caller.RequireStaffUserId(), attendeeId), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Cancels one attendee booking in two steps: preview, then confirm.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The cancellation handler.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="bookingId">The booking identifier.</param>
    /// <param name="confirm">Whether this call carries the confirmation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cancellation outcome.</returns>
    [McpServerTool(Name = "cancel_attendee_booking", Title = "Cancel attendee booking", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Cancel one attendee booking in two steps. Caller must have ManageAttendees; call first with confirm false to preview the consequence, then with confirm true to cancel.")]
    public async Task<CoordinatorCancelOutcome> CancelAttendeeBookingAsync(
        ICallerAccessor caller,
        CancelBookingByCoordinatorHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        [Description("The booking identifier.")] Guid bookingId,
        [Description("Whether this call carries the confirmation.")] bool confirm,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CancelBookingByCoordinatorCommand(caller.RequireStaffUserId(), attendeeId, bookingId, confirm), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Gets tool-safe readiness including Coordinator display wording.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The readiness handler.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool-safe readiness view.</returns>
    [McpServerTool(Name = "get_attendee_readiness", Title = "Get attendee readiness", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Get a attendee's readiness with Coordinator display wording. Caller must have ManageAttendees; returns internal readiness without recovery mutation.")]
    public async Task<AttendeeReadinessToolView> GetAttendeeReadinessAsync(
        ICallerAccessor caller,
        GetAttendeeReadinessHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetAttendeeReadinessQuery(caller.RequireStaffUserId(), attendeeId), cancellationToken);
        var value = result.ValueOrThrow();
        return new AttendeeReadinessToolView(
            value.AttendeeId,
            value.Code.ToString(),
            AttendeeEndpoints.DisplayForTool(value.Code),
            value.OutstandingAppointmentTypes);
    }
}
