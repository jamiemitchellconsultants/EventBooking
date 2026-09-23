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
