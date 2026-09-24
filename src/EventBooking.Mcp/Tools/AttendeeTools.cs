using System.ComponentModel;
using EventBooking.Api.Auth;
using EventBooking.Application.Attendees;
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Recovery;
using ModelContextProtocol.Server;

namespace EventBooking.Mcp.Tools;

/// <summary>The thirteen attendee tools. Every one resolves staff by identity, not by number.</summary>
[McpServerToolType]
public sealed class AttendeeTools
{
    /// <summary>Lists attendees.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The list handler.</param>
    /// <param name="limit">The page size.</param>
    /// <param name="status">The status filter, or null for every status.</param>
    /// <param name="groupId">The group filter, or null for every group.</param>
    /// <param name="readiness">The readiness filter, or null for every readiness.</param>
    /// <param name="search">The name or email prefix, or null.</param>
    /// <param name="cursor">The page cursor, or null for the first page.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>One page of attendees.</returns>
    [McpServerTool(
        Name = "list_attendees", Title = "List attendees",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reads attendees filtered by status, group, readiness and a name or email prefix, one keyset page at a time, with each row's latest delivery status.")]
    public async Task<AttendeeListView> ListAttendeesAsync(
        ICallerAccessor caller,
        ListAttendeesHandler handler,
        [Description("Page size, 1 to 200.")] int limit = 50,
        [Description("Narrow to one status.")] string? status = null,
        [Description("Narrow to one attendee group.")] Guid? groupId = null,
        [Description("Narrow to one readiness.")] string? readiness = null,
        [Description("A name or email prefix.")] string? search = null,
        [Description("The nextCursor from the previous page.")] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new ListAttendeesQuery(
                caller.RequireStaffUserId(), cursor, limit, status, groupId, readiness,
                search),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Creates an attendee.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The save handler.</param>
    /// <param name="name">The full name.</param>
    /// <param name="email">The email address.</param>
    /// <param name="attendeeGroupId">The group, whose requirements are derived.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new attendee's identifier.</returns>
    [McpServerTool(
        Name = "create_attendee", Title = "Create attendee",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Creates an attendee in a group, deriving their requirements from it.")]
    public async Task<Guid> CreateAttendeeAsync(
        ICallerAccessor caller,
        SaveAttendeeHandler handler,
        [Description("Full name.")] string name,
        [Description("Email address.")] string email,
        [Description("The attendee group identifier.")] Guid attendeeGroupId,
        CancellationToken cancellationToken = default) =>
        await handler.CreateAsync(
            new CreateAttendeeCommand(
                caller.RequireStaffUserId(), name, email, attendeeGroupId),
            cancellationToken).ValueOrThrowAsync();

    /// <summary>Updates an attendee.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The save handler.</param>
    /// <param name="attendeeId">The attendee.</param>
    /// <param name="name">The full name, or null to leave it alone.</param>
    /// <param name="email">The email address, or null to leave it alone.</param>
    /// <param name="attendeeGroupId">The group, or null to leave it alone.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation message.</returns>
    [McpServerTool(
        Name = "update_attendee", Title = "Update attendee",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Updates an attendee's name, email and group (FR-4.2). A group change that would alter an active booking's requirements is refused.")]
    public async Task<string> UpdateAttendeeAsync(
        ICallerAccessor caller,
        SaveAttendeeHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        [Description("Full name, or omit to leave it unchanged.")] string? name = null,
        [Description("Email address, or omit to leave it unchanged.")] string? email = null,
        [Description("The attendee group identifier, or omit to leave it unchanged.")]
        Guid? attendeeGroupId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.UpdateAsync(
            new UpdateAttendeeCommand(
                caller.RequireStaffUserId(), attendeeId, name, email, attendeeGroupId),
            cancellationToken);
        result.ThrowIfFailure();
        return "Attendee updated.";
    }

    /// <summary>Deletes an attendee and their bookings.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The delete handler.</param>
    /// <param name="attendeeId">The attendee.</param>
    /// <param name="confirm">Pass true once the consequence has been shown.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation message.</returns>
    [McpServerTool(
        Name = "delete_attendee", Title = "Delete attendee",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Deletes an attendee and their bookings (FR-4.5). Two-step: without confirm=true the call reports its consequence and changes nothing.")]
    public async Task<string> DeleteAttendeeAsync(
        ICallerAccessor caller,
        DeleteAttendeeHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        [Description("Pass true once you have shown the consequence.")] bool confirm = false,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new DeleteAttendeeCommand(caller.RequireStaffUserId(), attendeeId, confirm),
            cancellationToken);
        result.ThrowIfFailure();
        return "Attendee deleted.";
    }

    /// <summary>Imports attendees from CSV content, all or nothing.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The import handler.</param>
    /// <param name="csv">The CSV content.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome, carrying row errors when nothing was imported.</returns>
    [McpServerTool(
        Name = "import_attendees", Title = "Import attendees",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Imports attendees from CSV content, all or nothing (FR-4.3). At most 1000 rows and 1 MB.")]
    public async Task<AttendeeImportOutcome> ImportAttendeesAsync(
        ICallerAccessor caller,
        ImportAttendeesHandler handler,
        [Description("The CSV content, with a name,email,attendee_group header.")] string csv,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new ImportAttendeesCommand(caller.RequireStaffUserId(), csv), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Counts the events an attendee could currently be offered.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The count handler.</param>
    /// <param name="attendeeId">The attendee.</param>
    /// <param name="locationIds">The locations to count across.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The eligible event count.</returns>
    [McpServerTool(
        Name = "count_eligible_events", Title = "Count eligible events",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Counts the events this attendee could currently be offered at the given locations.")]
    public async Task<int> CountEligibleEventsAsync(
        ICallerAccessor caller,
        CountEligibleEventsHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        [Description("The locations to count across.")] Guid[]? locationIds = null,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new CountEligibleEventsQuery(
                caller.RequireStaffUserId(), attendeeId, locationIds ?? []),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Issues an invitation restricted to the chosen locations.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The invite handler.</param>
    /// <param name="attendeeId">The attendee.</param>
    /// <param name="locationIds">The locations to open, or omit for every active location.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Invited, or AwaitingAvailability when too few events are eligible.</returns>
    [McpServerTool(
        Name = "invite_attendee", Title = "Invite attendee",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Issues an invitation restricted to the chosen locations, returning Invited or AwaitingAvailability when too few events are eligible.")]
    public async Task<InviteAttendeeOutcome> InviteAttendeeAsync(
        ICallerAccessor caller,
        InviteAttendeeHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        [Description("The locations to open; omit for every active location.")]
        Guid[]? locationIds = null,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new InviteAttendeeCommand(
                caller.RequireStaffUserId(), attendeeId, locationIds ?? []),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Starts missed-appointment recovery for one attendee.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The recovery handler.</param>
    /// <param name="attendeeId">The attendee.</param>
    /// <param name="additionalLocationIds">Locations to widen the recovery with.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The recovery outcome.</returns>
    [McpServerTool(
        Name = "start_recovery_invite", Title = "Start recovery invite",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Starts missed-appointment recovery for the attendee's outstanding no-show types (FR-9.1), optionally widening the locations.")]
    public async Task<StartRecoveryOutcome> StartRecoveryInviteAsync(
        ICallerAccessor caller,
        StartRecoveryHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        [Description("Further locations to widen the recovery with.")]
        Guid[]? additionalLocationIds = null,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new StartRecoveryCommand(
                caller.RequireStaffUserId(), attendeeId, additionalLocationIds ?? []),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Cancels one pending recovery invitation.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The cancel handler.</param>
    /// <param name="attendeeId">The attendee.</param>
    /// <param name="inviteId">The recovery invitation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task tracking the cancellation.</returns>
    [McpServerTool(
        Name = "cancel_recovery_invite", Title = "Cancel recovery invite",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Cancels one pending recovery invitation.")]
    public async Task CancelRecoveryInviteAsync(
        ICallerAccessor caller,
        CancelRecoveryInviteHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        [Description("The recovery invitation identifier.")] Guid inviteId,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new CancelRecoveryInviteCommand(
                caller.RequireStaffUserId(), attendeeId, inviteId),
            cancellationToken);
        result.ThrowIfFailure();
    }

    /// <summary>Lists one attendee's bookings.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The bookings handler.</param>
    /// <param name="attendeeId">The attendee.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The bookings.</returns>
    [McpServerTool(
        Name = "list_attendee_bookings", Title = "List attendee bookings",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reads the attendee's bookings with their event and location.")]
    public async Task<IReadOnlyList<AttendeeBookingSummary>> ListAttendeeBookingsAsync(
        ICallerAccessor caller,
        GetAttendeeBookingsHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new GetAttendeeBookingsQuery(caller.RequireStaffUserId(), attendeeId),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Cancels one booking on the attendee's behalf.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The cancel handler.</param>
    /// <param name="attendeeId">The attendee.</param>
    /// <param name="bookingId">The booking.</param>
    /// <param name="confirm">Pass true once the consequence has been shown.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The consequence, or the cancellation once confirmed.</returns>
    [McpServerTool(
        Name = "cancel_attendee_booking", Title = "Cancel attendee booking",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Cancels one booking on the attendee's behalf (FR-7.1). Two-step: without confirm=true the call reports its consequence and changes nothing.")]
    public async Task<CoordinatorCancelOutcome> CancelAttendeeBookingAsync(
        ICallerAccessor caller,
        CancelBookingByCoordinatorHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        [Description("The booking identifier.")] Guid bookingId,
        [Description("Pass true once you have shown the consequence.")] bool confirm = false,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new CancelBookingByCoordinatorCommand(
                caller.RequireStaffUserId(), attendeeId, bookingId, confirm),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Regenerates and re-queues the newest failed or stale delivery.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The retry handler.</param>
    /// <param name="attendeeId">The attendee.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The retry outcome.</returns>
    [McpServerTool(
        Name = "retry_attendee_email", Title = "Retry attendee email",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Regenerates and re-queues the newest failed or stale delivery (FR-11.3).")]
    public async Task<RetryEmailOutcome> RetryAttendeeEmailAsync(
        ICallerAccessor caller,
        RetryEmailHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new RetryNewestEmailCommand(caller.RequireStaffUserId(), attendeeId),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Reads one attendee's calculated readiness.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The readiness handler.</param>
    /// <param name="attendeeId">The attendee.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The readiness.</returns>
    [McpServerTool(
        Name = "get_attendee_readiness", Title = "Get attendee readiness",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reads the calculated readiness and any outstanding appointment types.")]
    public async Task<AttendeeReadiness> GetAttendeeReadinessAsync(
        ICallerAccessor caller,
        GetAttendeeReadinessHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new GetAttendeeReadinessQuery(caller.RequireStaffUserId(), attendeeId),
            cancellationToken);
        return result.ValueOrThrow();
    }
}
