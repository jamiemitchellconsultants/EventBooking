# 00b — Vocabulary edits 50 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — src/EventBooking.Mcp/Tools/CandidateTools.cs — 1/1

<!-- vocabulary-file: {"id":175,"oldPath":"src/EventBooking.Mcp/Tools/CandidateTools.cs","newPath":"src/EventBooking.Mcp/Tools/AttendeeTools.cs","beforeSha":"9e2c33521a2caab8888945c2cb286e9ad93172d4cf0f1f6c1eb84804f485eede","afterSha":"8489f4e8506d6d99ee2121cc35d300a56cbcb877fb213a338f8442b325142f2d","side":"before","part":1,"parts":1} -->

`````csharp
using System.ComponentModel;
using EventBooking.Api.Auth;
using EventBooking.Api.Endpoints;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Candidates;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Candidates;
using ModelContextProtocol.Server;

namespace EventBooking.Mcp.Tools;

public sealed record CandidateToolView
{
    /// <summary>Gets the candidate identifier for follow-up tool calls.</summary>
    public required Guid CandidateId { get; init; }
    /// <summary>Gets the candidate display name.</summary>
    public required string Name { get; init; }
    /// <summary>Gets the candidate email address.</summary>
    public required string Email { get; init; }
    /// <summary>Gets the candidate lifecycle status name.</summary>
    public required string Status { get; init; }
    /// <summary>Gets the assigned Employee Group name, or null during reconciliation.</summary>
    public required string? EmployeeGroupName { get; init; }
    /// <summary>Gets the canonical Employee Group code, or null during Release 1 reconciliation.</summary>
    public required string? EmployeeGroupCode { get; init; }
    /// <summary>Gets whether explicit Coordinator assignment is still required.</summary>
    public required bool RequiresEmployeeGroupReconciliation { get; init; }
    /// <summary>Gets read-only derived Appointment Type summaries.</summary>
    public required IReadOnlyList<AppointmentTypeSummary> RequiredAppointmentTypes { get; init; }
    /// <summary>Gets internal readiness without exposing recovery mutation.</summary>
    public required CandidateReadiness? Readiness { get; init; }
}

/// <summary>Tool-safe readiness including Coordinator display wording.</summary>
/// <param name="CandidateId">The candidate the readiness was calculated for.</param>
/// <param name="Code">The readiness code name.</param>
/// <param name="Display">The Coordinator-facing display wording for the code.</param>
/// <param name="OutstandingAppointmentTypes">The appointment types still outstanding.</param>
public sealed record CandidateReadinessToolView(
    Guid CandidateId,
    string Code,
    string Display,
    IReadOnlyList<OutstandingAppointmentType> OutstandingAppointmentTypes);

/// <summary>Candidate management, invites, and delivery retry for coordinators.</summary>
[McpServerToolType]
public sealed class CandidateTools
{
    private const int DefaultPageSize = 50;

    private const int MaxPageSize = 200;

    /// <summary>Lists candidates, optionally filtered by status or search text.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The list handler.</param>
    /// <param name="readiness">Resolves internal readiness per listed candidate.</param>
    /// <param name="status">The candidate status name, or null for all.</param>
    /// <param name="search">Free-text filter, or null.</param>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="pageSize">Results per page, clamped to the tool maximum.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The bounded page of candidate views.</returns>
    [McpServerTool(Name = "list_candidates", Title = "List candidates", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("List candidates, optionally filtered by status name and search text. Caller must be a coordinator or admin.")]
    public async Task<IReadOnlyList<CandidateToolView>> ListCandidatesAsync(
        ICallerAccessor caller,
        ListCandidatesHandler handler,
        GetCandidateReadinessHandler readiness,
        [Description("Candidate status name (e.g. Invited) or null for all.")] string? status = null,
        [Description("Free-text name or email filter or null.")] string? search = null,
        [Description("1-based page number.")] int page = 1,
        [Description("Results per page, at most 200.")] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        CandidateStatus? parsed = null;
        if (status is not null)
        {
            if (!Enum.TryParse<CandidateStatus>(status, ignoreCase: false, out var value) ||
                !Enum.IsDefined(value))
            {
                throw new ModelContextProtocol.McpException(
                    "Status must be a recognised CandidateStatus name.");
            }

            parsed = value;
        }

        var staffUserId = caller.RequireStaffUserId();
        var result = await handler.HandleAsync(
            new ListCandidatesQuery(staffUserId, parsed, search),
            cancellationToken);
        var bounded = Math.Clamp(pageSize, 1, MaxPageSize);
        var skipped = Math.Max(page - 1, 0) * bounded;

        var views = new List<CandidateToolView>();
        foreach (var item in result.ValueOrThrow().Skip(skipped).Take(bounded))
        {
            var readinessResult = await readiness.HandleAsync(
                new GetCandidateReadinessQuery(staffUserId, item.CandidateId),
                cancellationToken);
            views.Add(new CandidateToolView
            {
                CandidateId = item.CandidateId,
                Name = item.Name,
                Email = item.Email,
                Status = item.Status.ToString(),
                EmployeeGroupName = item.EmployeeGroupName,
                EmployeeGroupCode = item.EmployeeGroupCode,
                RequiresEmployeeGroupReconciliation = item.RequiresEmployeeGroupReconciliation,
                RequiredAppointmentTypes = item.RequiredAppointmentTypes,
                Readiness = readinessResult.IsSuccess ? readinessResult.Value : null,
            });
        }

        return views;
    }

    /// <summary>Lists the Employee Groups available for candidate assignment.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The list handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The assignable groups with their required appointment types.</returns>
    [McpServerTool(Name = "list_employee_groups", Title = "List employee groups", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("List the employee groups that determine candidate requirements. Caller must be a coordinator or admin.")]
    public async Task<IReadOnlyList<EmployeeGroupListItem>> ListEmployeeGroupsAsync(
        ICallerAccessor caller,
        ListEmployeeGroupsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new ListEmployeeGroupsQuery(caller.RequireStaffUserId()),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Creates one candidate in an Employee Group.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The save handler.</param>
    /// <param name="groups">Resolves the assigned Employee Group.</param>
    /// <param name="name">The candidate name.</param>
    /// <param name="email">The candidate email.</param>
    /// <param name="employeeGroupCode">The canonical Employee Group code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new candidate identifier.</returns>
    [McpServerTool(Name = "create_candidate", Title = "Create candidate", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Create a candidate in one employee group; requirements derive from the group. Caller must be a coordinator or admin; creates a new candidate record.")]
    public async Task<Guid> CreateCandidateAsync(
        ICallerAccessor caller,
        SaveCandidateHandler handler,
        IEmployeeGroupRepository groups,
        [Description("Candidate full name.")] string name,
        [Description("Candidate email address.")] string email,
        [Description("Canonical employee group code (e.g. PILOTS).")] string employeeGroupCode,
        CancellationToken cancellationToken)
    {
        var groupId = await ResolveGroupIdAsync(groups, employeeGroupCode, cancellationToken);
        var result = await handler.CreateAsync(
            new CreateCandidateCommand(caller.RequireStaffUserId(), name, email, groupId),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Updates a candidate's name, email, or Employee Group.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The save handler.</param>
    /// <param name="groups">Resolves the assigned Employee Group.</param>
    /// <param name="candidateId">The candidate identifier.</param>
    /// <param name="name">The corrected name.</param>
    /// <param name="email">The corrected email.</param>
    /// <param name="employeeGroupCode">The canonical Employee Group code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation message.</returns>
    [McpServerTool(Name = "update_candidate", Title = "Update candidate", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Update a candidate. Caller must be a coordinator or admin; requirements are frozen while an active booking exists.")]
    public async Task<string> UpdateCandidateAsync(
        ICallerAccessor caller,
        SaveCandidateHandler handler,
        IEmployeeGroupRepository groups,
        [Description("The candidate identifier.")] Guid candidateId,
        [Description("Corrected full name.")] string name,
        [Description("Corrected email address.")] string email,
        [Description("Canonical employee group code (e.g. PILOTS).")] string employeeGroupCode,
        CancellationToken cancellationToken)
    {
        var groupId = await ResolveGroupIdAsync(groups, employeeGroupCode, cancellationToken);
        var result = await handler.UpdateAsync(
            new UpdateCandidateCommand(
                caller.RequireStaffUserId(), candidateId, name, email, groupId),
            cancellationToken);
        result.ThrowIfFailure();
        return "Candidate updated.";
    }

    /// <summary>Deletes a candidate, optionally cascading.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The delete handler.</param>
    /// <param name="candidateId">The candidate identifier.</param>
    /// <param name="confirm">Whether dependent data may be removed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation message.</returns>
    [McpServerTool(Name = "delete_candidate", Title = "Delete candidate", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Delete a candidate. Caller must be a coordinator or admin; set confirm to true to also remove dependent data.")]
    public async Task<string> DeleteCandidateAsync(
        ICallerAccessor caller,
        DeleteCandidateHandler handler,
        [Description("The candidate identifier.")] Guid candidateId,
        [Description("Whether dependent data may be removed.")] bool confirm,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new DeleteCandidateCommand(caller.RequireStaffUserId(), candidateId, confirm),
            cancellationToken);
        result.ThrowIfFailure();
        return "Candidate deleted.";
    }

    /// <summary>Imports candidates from CSV content.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The import handler.</param>
    /// <param name="csv">The CSV content.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The import outcome.</returns>
    [McpServerTool(Name = "import_candidates", Title = "Import candidates", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Import candidates from CSV content with name, email, and employee group code. Caller must be a coordinator or admin; creates new candidate records.")]
    public async Task<CandidateImportOutcome> ImportCandidatesAsync(
        ICallerAccessor caller,
        ImportCandidatesHandler handler,
        [Description("CSV content with one candidate per row.")] string csv,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new ImportCandidatesCommand(caller.RequireStaffUserId(), csv),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Resolves a canonical Employee Group code to its stable identifier.</summary>
    private static async Task<Guid> ResolveGroupIdAsync(
        IEmployeeGroupRepository groups,
        string employeeGroupCode,
        CancellationToken cancellationToken)
    {
        var group = await groups.GetByCodeAsync(employeeGroupCode, cancellationToken);
        if (group is null)
        {
            throw new ModelContextProtocol.McpException(
                $"Employee group '{employeeGroupCode}' is not known. Use list_employee_groups.");
        }

        return group.Id;
    }

    /// <summary>Triggers a fresh invite for a candidate.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The invite handler.</param>
    /// <param name="candidateId">The candidate identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The invite issue result.</returns>
    [McpServerTool(Name = "trigger_invite", Title = "Trigger invite", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Issue a fresh three-option invite to a candidate. Caller must be a coordinator or admin; sends an invite email.")]
    public async Task<InviteIssueResult> TriggerInviteAsync(
        ICallerAccessor caller,
        TriggerInviteHandler handler,
        [Description("The candidate identifier.")] Guid candidateId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new TriggerInviteCommand(caller.RequireStaffUserId(), candidateId),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Retries the latest unresolved email for a candidate.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The retry handler.</param>
    /// <param name="candidateId">The candidate identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The retry outcome.</returns>
    [McpServerTool(Name = "retry_candidate_email", Title = "Retry candidate email", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Retry the latest unresolved candidate email delivery. Caller must be a coordinator or admin; resends the latest unresolved email.")]
    public async Task<RetryEmailOutcome> RetryCandidateEmailAsync(
        ICallerAccessor caller,
        RetryEmailHandler handler,
        [Description("The candidate identifier.")] Guid candidateId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new RetryEmailCommand(caller.RequireStaffUserId(), candidateId),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Starts a recovery invite for a candidate with a missed appointment.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The recovery handler.</param>
    /// <param name="candidateId">The candidate identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The recovery invite issue result.</returns>
    [McpServerTool(Name = "start_recovery_invite", Title = "Start recovery invite", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Start a recovery invite for a candidate with a missed appointment. Caller must have ManageCandidates; sends a recovery invite email.")]
    public async Task<StartRecoveryResult> StartRecoveryInviteAsync(
        ICallerAccessor caller,
        StartRecoveryHandler handler,
        [Description("The candidate identifier.")] Guid candidateId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new StartRecoveryCommand(caller.RequireStaffUserId(), candidateId), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Cancels a pending recovery invite for a candidate.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The recovery cancellation handler.</param>
    /// <param name="candidateId">The candidate identifier.</param>
    /// <param name="inviteId">The recovery invite identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation message.</returns>
    [McpServerTool(Name = "cancel_recovery_invite", Title = "Cancel recovery invite", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Cancel a pending recovery invite for a candidate. Caller must have ManageCandidates; cancels the pending recovery invite.")]
    public async Task<string> CancelRecoveryInviteAsync(
        ICallerAccessor caller,
        CancelRecoveryInviteHandler handler,
        [Description("The candidate identifier.")] Guid candidateId,
        [Description("The recovery invite identifier.")] Guid inviteId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CancelRecoveryInviteCommand(caller.RequireStaffUserId(), candidateId, inviteId), cancellationToken);
        result.ThrowIfFailure();
        return "Recovery invite cancelled.";
    }

    /// <summary>Lists the active bookings a coordinator may cancel for one candidate.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The bookings handler.</param>
    /// <param name="candidateId">The candidate identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The candidate's active booking summaries.</returns>
    [McpServerTool(Name = "list_candidate_bookings", Title = "List candidate bookings", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("List a candidate's active bookings. Caller must have ManageCandidates; returns cancellable bookings without management tokens.")]
    public async Task<IReadOnlyList<CandidateBookingSummary>> ListCandidateBookingsAsync(
        ICallerAccessor caller,
        GetCandidateBookingsHandler handler,
        [Description("The candidate identifier.")] Guid candidateId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetCandidateBookingsQuery(caller.RequireStaffUserId(), candidateId), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Cancels one candidate booking, optionally rebooking the candidate.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The cancellation handler.</param>
    /// <param name="candidateId">The candidate identifier.</param>
    /// <param name="bookingId">The booking identifier.</param>
    /// <param name="rebook">Whether to issue a replacement invite.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cancellation outcome.</returns>
    [McpServerTool(Name = "cancel_candidate_booking", Title = "Cancel candidate booking", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Cancel one candidate booking, optionally rebooking. Caller must have ManageCandidates; cancels the booking and optionally sends a replacement invite.")]
    public async Task<CancelBookingOutcome> CancelCandidateBookingAsync(
        ICallerAccessor caller,
        CancelCandidateBookingHandler handler,
        [Description("The candidate identifier.")] Guid candidateId,
        [Description("The booking identifier.")] Guid bookingId,
        [Description("Whether to issue a replacement invite.")] bool rebook,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CancelCandidateBookingCommand(caller.RequireStaffUserId(), candidateId, bookingId, rebook), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Gets tool-safe readiness including Coordinator display wording.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The readiness handler.</param>
    /// <param name="candidateId">The candidate identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool-safe readiness view.</returns>
    [McpServerTool(Name = "get_candidate_readiness", Title = "Get candidate readiness", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Get a candidate's readiness with Coordinator display wording. Caller must have ManageCandidates; returns internal readiness without recovery mutation.")]
    public async Task<CandidateReadinessToolView> GetCandidateReadinessAsync(
        ICallerAccessor caller,
        GetCandidateReadinessHandler handler,
        [Description("The candidate identifier.")] Guid candidateId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetCandidateReadinessQuery(caller.RequireStaffUserId(), candidateId), cancellationToken);
        var value = result.ValueOrThrow();
        return new CandidateReadinessToolView(
            value.CandidateId,
            value.Code.ToString(),
            CandidateEndpoints.DisplayForTool(value.Code),
            value.OutstandingAppointmentTypes);
    }
}
`````

## after — src/EventBooking.Mcp/Tools/AttendeeTools.cs — 1/1

<!-- vocabulary-file: {"id":175,"oldPath":"src/EventBooking.Mcp/Tools/CandidateTools.cs","newPath":"src/EventBooking.Mcp/Tools/AttendeeTools.cs","beforeSha":"9e2c33521a2caab8888945c2cb286e9ad93172d4cf0f1f6c1eb84804f485eede","afterSha":"8489f4e8506d6d99ee2121cc35d300a56cbcb877fb213a338f8442b325142f2d","side":"after","part":1,"parts":1} -->

`````csharp
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
    /// <summary>Gets the assigned Attendee Group name, or null during reconciliation.</summary>
    public required string? AttendeeGroupName { get; init; }
    /// <summary>Gets the canonical Attendee Group code, or null during Release 1 reconciliation.</summary>
    public required string? AttendeeGroupCode { get; init; }
    /// <summary>Gets whether explicit Coordinator assignment is still required.</summary>
    public required bool RequiresAttendeeGroupReconciliation { get; init; }
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
                RequiresAttendeeGroupReconciliation = item.RequiresAttendeeGroupReconciliation,
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
                $"Employee group '{attendeeGroupCode}' is not known. Use list_attendee_groups.");
        }

        return group.Id;
    }

    /// <summary>Triggers a fresh invite for a attendee.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The invite handler.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The invite issue result.</returns>
    [McpServerTool(Name = "trigger_invite", Title = "Trigger invite", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Issue a fresh three-option invite to a attendee. Caller must be a coordinator or admin; sends an invite email.")]
    public async Task<InviteIssueResult> TriggerInviteAsync(
        ICallerAccessor caller,
        TriggerInviteHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new TriggerInviteCommand(caller.RequireStaffUserId(), attendeeId),
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

    /// <summary>Cancels one attendee booking, optionally rebooking the attendee.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The cancellation handler.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="bookingId">The booking identifier.</param>
    /// <param name="rebook">Whether to issue a replacement invite.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cancellation outcome.</returns>
    [McpServerTool(Name = "cancel_attendee_booking", Title = "Cancel attendee booking", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Cancel one attendee booking, optionally rebooking. Caller must have ManageAttendees; cancels the booking and optionally sends a replacement invite.")]
    public async Task<CancelBookingOutcome> CancelAttendeeBookingAsync(
        ICallerAccessor caller,
        CancelAttendeeBookingHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        [Description("The booking identifier.")] Guid bookingId,
        [Description("Whether to issue a replacement invite.")] bool rebook,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CancelAttendeeBookingCommand(caller.RequireStaffUserId(), attendeeId, bookingId, rebook), cancellationToken);
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
`````

## before — src/EventBooking.Mcp/Tools/OperationsTools.cs — 1/1

<!-- vocabulary-file: {"id":176,"oldPath":"src/EventBooking.Mcp/Tools/OperationsTools.cs","newPath":"src/EventBooking.Mcp/Tools/OperationsTools.cs","beforeSha":"ada909195d6947b2a7aa37bf0e9d01910204e1384c4321f77834ca6252884e10","afterSha":"d07d2197a37792e8267d6981b238342a98dca7f71c7396e2f409924134467b82","side":"before","part":1,"parts":1} -->

`````csharp
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

    /// <summary>Returns the audit history of one confirmed slot.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The audit history handler.</param>
    /// <param name="confirmedSlotId">The confirmed slot identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The audit rows.</returns>
    [McpServerTool(Name = "slot_audit_history", Title = "Slot audit history", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Return the audit history of one confirmed slot. Caller must have audit visibility (Admin or Coordinator).")]
    public async Task<IReadOnlyList<AuditHistoryRow>> GetSlotAuditHistoryAsync(
        ICallerAccessor caller,
        GetAuditHistoryHandler handler,
        [Description("The confirmed slot identifier.")] Guid confirmedSlotId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetAuditHistoryQuery(
                caller.RequireStaffUserId(), AuditEntityTypes.ConfirmedSlot, confirmedSlotId),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Returns the audit history of one candidate.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The audit history handler.</param>
    /// <param name="candidateId">The candidate identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The audit rows.</returns>
    [McpServerTool(Name = "candidate_audit_history", Title = "Candidate audit history", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Return the audit history of one candidate. Caller must have audit visibility (Admin or Coordinator).")]
    public async Task<IReadOnlyList<AuditHistoryRow>> GetCandidateAuditHistoryAsync(
        ICallerAccessor caller,
        GetAuditHistoryHandler handler,
        [Description("The candidate identifier.")] Guid candidateId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetAuditHistoryQuery(caller.RequireStaffUserId(), null, candidateId),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Lists the appointment workspace slots for the caller's scope.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The workspace handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The workspace slot list.</returns>
    [McpServerTool(Name = "appointment_slots", Title = "Appointment slots", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("List current and upcoming active slots with appointment counts for your scope. Caller must be a coordinator, admin, or manager within scope.")]
    public async Task<AppointmentWorkspaceSlotList> ListAppointmentSlotsAsync(
        ICallerAccessor caller,
        GetAppointmentWorkspaceHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ListSlotsAsync(
            caller.RequireStaffUserId(), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Returns one appointment workspace slot with its operational rows.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The workspace handler.</param>
    /// <param name="confirmedSlotId">The confirmed slot identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The slot detail.</returns>
    [McpServerTool(Name = "appointment_slot_detail", Title = "Appointment slot detail", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Return one active slot with its minimum-data appointment rows. Caller must be a coordinator, admin, or manager within scope.")]
    public async Task<AppointmentSlotDetail> GetAppointmentSlotAsync(
        ICallerAccessor caller,
        GetAppointmentWorkspaceHandler handler,
        [Description("The confirmed slot identifier.")] Guid confirmedSlotId,
        CancellationToken cancellationToken)
    {
        var result = await handler.GetSlotAsync(
            caller.RequireStaffUserId(), confirmedSlotId, cancellationToken);
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

    /// <summary>Exports one scoped slot roster as CSV text with its download filename.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The workspace handler.</param>
    /// <param name="formatter">The roster CSV formatter.</param>
    /// <param name="confirmedSlotId">The confirmed slot identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The roster CSV text and download filename.</returns>
    [McpServerTool(Name = "export_appointment_roster", Title = "Export appointment roster", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Export one appointment roster as CSV text with its download filename. Requires the ConductAppointments scope; the CSV is returned as text, not a server-side file.")]
    public async Task<RosterCsvResult> ExportAppointmentRosterAsync(
        ICallerAccessor caller,
        GetAppointmentWorkspaceHandler handler,
        AppointmentRosterCsvFormatter formatter,
        [Description("The confirmed slot identifier.")] Guid confirmedSlotId,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.GetSlotAsync(
            caller.RequireStaffUserId(), confirmedSlotId, cancellationToken);
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
`````

## after — src/EventBooking.Mcp/Tools/OperationsTools.cs — 1/1

<!-- vocabulary-file: {"id":176,"oldPath":"src/EventBooking.Mcp/Tools/OperationsTools.cs","newPath":"src/EventBooking.Mcp/Tools/OperationsTools.cs","beforeSha":"ada909195d6947b2a7aa37bf0e9d01910204e1384c4321f77834ca6252884e10","afterSha":"d07d2197a37792e8267d6981b238342a98dca7f71c7396e2f409924134467b82","side":"after","part":1,"parts":1} -->

`````csharp
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
`````
