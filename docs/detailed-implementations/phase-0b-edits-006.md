# 00b — Vocabulary edits 6 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — src/EventBooking.Api/InviteSweepService.cs — 1/1

<!-- vocabulary-file: {"id":20,"oldPath":"src/EventBooking.Api/InviteSweepService.cs","newPath":"src/EventBooking.Api/InviteSweepService.cs","beforeSha":"f79720a78c6139067908aa0fe36536570230ff789ad02979993b30a5286f6b6b","afterSha":"68be4815f9037d89c38156df73cc00fab102564f534dfa95023e5ee1a5b16256","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Invites;

namespace EventBooking.Api;

/// <summary>
/// Runs the invite expiry sweep hourly. Expiry cannot wait for a candidate to open a link — the
/// whole point is the candidates who never do.
/// </summary>
public sealed class InviteSweepService(
    IServiceScopeFactory scopes,
    ILogger<InviteSweepService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<ExpireInvitesHandler>();

                var summary = await handler.HandleAsync(stoppingToken);

                if (summary.Expired > 0)
                {
                    logger.LogInformation(
                        "Invite sweep: {Expired} expired, {ReIssued} re-issued, {Flagged} flagged.",
                        summary.Expired,
                        summary.ReIssued,
                        summary.FlaggedForFollowUp);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // One bad sweep must not stop every later sweep.
                logger.LogError(ex, "The invite sweep failed.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }
        }
    }
}
`````

## after — src/EventBooking.Api/InviteSweepService.cs — 1/1

<!-- vocabulary-file: {"id":20,"oldPath":"src/EventBooking.Api/InviteSweepService.cs","newPath":"src/EventBooking.Api/InviteSweepService.cs","beforeSha":"f79720a78c6139067908aa0fe36536570230ff789ad02979993b30a5286f6b6b","afterSha":"68be4815f9037d89c38156df73cc00fab102564f534dfa95023e5ee1a5b16256","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Invites;

namespace EventBooking.Api;

/// <summary>
/// Runs the invite expiry sweep hourly. Expiry cannot wait for a attendee to open a link — the
/// whole point is the attendees who never do.
/// </summary>
public sealed class InviteSweepService(
    IServiceScopeFactory scopes,
    ILogger<InviteSweepService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<ExpireInvitesHandler>();

                var summary = await handler.HandleAsync(stoppingToken);

                if (summary.Expired > 0)
                {
                    logger.LogInformation(
                        "Invite sweep: {Expired} expired, {ReIssued} re-issued, {Flagged} flagged.",
                        summary.Expired,
                        summary.ReIssued,
                        summary.FlaggedForFollowUp);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // One bad sweep must not stop every later sweep.
                logger.LogError(ex, "The invite sweep failed.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }
        }
    }
}
`````

## before — src/EventBooking.Api/OpenApi/AgentOperationCatalog.cs — 1/1

<!-- vocabulary-file: {"id":21,"oldPath":"src/EventBooking.Api/OpenApi/AgentOperationCatalog.cs","newPath":"src/EventBooking.Api/OpenApi/AgentOperationCatalog.cs","beforeSha":"b39b7c5f6de0d5d007ceaa9fdebfd04fbf53bcbfa553ea87e9e67ce2589736a2","afterSha":"6097e2dcb14e98fd1264aeb990de65895c9d2a44eef3c545987be5876f8ff8d3","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Api.OpenApi;

/// <summary>Behavior hints shared by OpenAPI and MCP discovery.</summary>
/// <param name="ReadOnly">Whether the operation performs no state change.</param>
/// <param name="Destructive">Whether the operation deletes, cancels, withdraws, clears, replaces, or transitions existing state.</param>
/// <param name="Idempotent">Whether repeating the operation has the same effect as performing it once.</param>
/// <param name="OpenWorld">Whether the operation interacts with arbitrary external entities. Always false for this tool set.</param>
public sealed record AgentHints(
    /// <summary>Gets whether the operation performs no state change.</summary>
    bool ReadOnly,
    /// <summary>Gets whether the operation deletes, cancels, withdraws, clears, replaces, or transitions existing state.</summary>
    bool Destructive,
    /// <summary>Gets whether repeating the operation has the same effect as performing it once.</summary>
    bool Idempotent,
    /// <summary>Gets whether the operation interacts with arbitrary external entities.</summary>
    bool OpenWorld);

/// <summary>One stable HTTP operation and its MCP parity decision.</summary>
/// <param name="OperationId">The unique lower-camel-case OpenAPI operation id.</param>
/// <param name="Method">The uppercase HTTP method of the operation.</param>
/// <param name="Route">The route pattern of the operation.</param>
/// <param name="Tag">The OpenAPI tag grouping the operation.</param>
/// <param name="Summary">The short human-readable summary of the operation.</param>
/// <param name="Description">The longer description stating purpose, authorization, and side effects.</param>
/// <param name="RequiresBearer">Whether the operation requires a staff bearer token.</param>
/// <param name="McpTool">The snake-case MCP tool name, or null when intentionally excluded.</param>
/// <param name="ExclusionReason">The reviewed reason for exclusion, or null when an MCP tool exists.</param>
/// <param name="Hints">The behavior hints shared by OpenAPI and MCP discovery.</param>
public sealed record AgentOperation(
    /// <summary>Gets the unique lower-camel-case OpenAPI operation id.</summary>
    string OperationId,
    /// <summary>Gets the uppercase HTTP method of the operation.</summary>
    string Method,
    /// <summary>Gets the route pattern of the operation.</summary>
    string Route,
    /// <summary>Gets the OpenAPI tag grouping the operation.</summary>
    string Tag,
    /// <summary>Gets the short human-readable summary of the operation.</summary>
    string Summary,
    /// <summary>Gets the longer description stating purpose, authorization, and side effects.</summary>
    string Description,
    /// <summary>Gets whether the operation requires a staff bearer token.</summary>
    bool RequiresBearer,
    /// <summary>Gets the snake-case MCP tool name, or null when intentionally excluded.</summary>
    string? McpTool,
    /// <summary>Gets the reviewed reason for exclusion, or null when an MCP tool exists.</summary>
    string? ExclusionReason,
    /// <summary>Gets the behavior hints shared by OpenAPI and MCP discovery.</summary>
    AgentHints Hints);

/// <summary>Shared registry of HTTP operations and their MCP parity decisions.</summary>
public static class AgentOperationCatalog
{
    /// <summary>Gets every catalogued application operation keyed by operation id.</summary>
    public static IReadOnlyDictionary<string, AgentOperation> All { get; }

    static AgentOperationCatalog()
    {
        var operations = new List<AgentOperation>
        {
            Staff("getMyAccess", HttpMethods.Get, "/api/me", "Identity", "Read the signed-in staff access.", "Reads the signed-in staff identity and capability scope. Requires a staff bearer token.", "get_my_access", Read()),
            Staff("getSlotBoard", HttpMethods.Get, "/api/slots/board", "Slots", "Read the scoped slot board.", "Reads the slot board visible to the signed-in staff member. Requires a staff bearer token.", "slot_board", Read()),
            Staff("getSlotOperations", HttpMethods.Get, "/api/slots/operations", "Slots", "Read the scoped slot operations view.", "Reads the slot operations view visible to the signed-in staff member. Requires a staff bearer token.", "get_slot_operations", Read()),
            Staff("proposeSlot", HttpMethods.Post, "/api/slots/proposals", "Slots", "Propose a slot.", "Creates a slot proposal. Requires a staff bearer token.", "propose_slot", Create()),
            Staff("acceptProposal", HttpMethods.Post, "/api/slots/proposals/{id}/acceptance", "Slots", "Accept a slot proposal.", "Transitions a slot proposal to accepted. Requires a staff bearer token.", "accept_proposal", Transition()),
            Staff("withdrawAcceptance", HttpMethods.Delete, "/api/slots/proposals/{id}/acceptance", "Slots", "Withdraw a slot acceptance.", "Withdraws an accepted slot proposal. Requires a staff bearer token.", "withdraw_acceptance", Delete()),
            Staff("withdrawProposal", HttpMethods.Delete, "/api/slots/proposals/{id}", "Slots", "Withdraw a slot proposal.", "Withdraws a slot proposal. Requires a staff bearer token.", "withdraw_proposal", Delete()),
            Staff("adjustConfirmedSlotCapacity", HttpMethods.Put, "/api/slots/confirmed/{id}/capacity", "Slots", "Adjust confirmed slot capacity.", "Updates the capacity of a confirmed slot. Requires a staff bearer token.", "adjust_slot_capacity", Transition()),
            Staff("cancelConfirmedSlot", HttpMethods.Delete, "/api/slots/confirmed/{id}", "Slots", "Cancel a confirmed slot.", "Cancels a confirmed slot. Requires a staff bearer token.", "cancel_confirmed_slot", Delete()),
            Staff("importConfirmedSlots", HttpMethods.Post, "/api/confirmed-slots/import", "Slots", "Import confirmed slots.", "Imports confirmed slots from CSV. Requires a staff bearer token.", "import_confirmed_slots", Create()),
            Staff("listCandidates", HttpMethods.Get, "/api/candidates", "Candidates", "List candidates.", "Reads the candidate collection. Requires a staff bearer token.", "list_candidates", Read()),
            Staff("createCandidate", HttpMethods.Post, "/api/candidates", "Candidates", "Create a candidate.", "Creates a candidate. Requires a staff bearer token.", "create_candidate", Create()),
            Staff("updateCandidate", HttpMethods.Put, "/api/candidates/{id}", "Candidates", "Update a candidate.", "Updates an existing candidate. Requires a staff bearer token.", "update_candidate", Transition()),
            Staff("deleteCandidate", HttpMethods.Delete, "/api/candidates/{id}", "Candidates", "Delete a candidate.", "Deletes a candidate. Requires a staff bearer token.", "delete_candidate", Delete()),
            Staff("importCandidates", HttpMethods.Post, "/api/candidates/import", "Candidates", "Import candidates.", "Imports candidates from CSV. Requires a staff bearer token.", "import_candidates", Create()),
            Staff("triggerCandidateInvite", HttpMethods.Post, "/api/candidates/{id}/invite", "Candidates", "Trigger a candidate invite.", "Sends a booking invite to a candidate. Requires a staff bearer token.", "trigger_invite", Create()),
            Staff("retryCandidateEmail", HttpMethods.Post, "/api/candidates/{id}/email-retry", "Candidates", "Retry candidate email.", "Retries pending candidate email delivery. Requires a staff bearer token.", "retry_candidate_email", Create()),
            Staff("startRecoveryInvite", HttpMethods.Post, "/api/candidates/{candidateId}/recovery-invites", "Candidates", "Start a recovery invite.", "Starts a recovery invite for a candidate. Requires a staff bearer token.", "start_recovery_invite", Create()),
            Staff("cancelRecoveryInvite", HttpMethods.Delete, "/api/candidates/{candidateId}/recovery-invites/{inviteId}", "Candidates", "Cancel a recovery invite.", "Cancels a pending recovery invite. Requires a staff bearer token.", "cancel_recovery_invite", Delete()),
            Staff("listCandidateBookings", HttpMethods.Get, "/api/candidates/{candidateId}/bookings", "Candidate Booking", "List candidate bookings.", "Reads the active bookings of a candidate. Requires a staff bearer token.", "list_candidate_bookings", Read()),
            Staff("cancelCandidateBooking", HttpMethods.Post, "/api/candidates/{candidateId}/bookings/{bookingId}/cancel", "Candidate Booking", "Cancel a candidate booking.", "Cancels a candidate booking. Requires a staff bearer token.", "cancel_candidate_booking", Delete()),
            Staff("getCandidateReadiness", HttpMethods.Get, "/api/candidates/{candidateId}/readiness", "Candidates", "Read candidate readiness.", "Reads the booking readiness of a candidate. Requires a staff bearer token.", "get_candidate_readiness", Read()),
            Staff("listEmployeeGroups", HttpMethods.Get, "/api/employee-groups", "Employee Groups", "List employee groups.", "Reads the employee group collection. Requires a staff bearer token.", "list_employee_groups", Read()),
            Staff("getSettings", HttpMethods.Get, "/api/admin/settings", "Settings", "Read settings.", "Reads the application settings. Requires a staff bearer token.", "get_settings", Read()),
            Staff("updateSettings", HttpMethods.Put, "/api/admin/settings", "Settings", "Update settings.", "Updates the application settings. Requires a staff bearer token.", "update_settings", Transition()),
            Staff("listStaffAccess", HttpMethods.Get, "/api/admin/staff-access", "Staff Access", "List staff access.", "Reads the staff access collection. Requires a staff bearer token.", "list_staff_access", Read()),
            Staff("replaceStaffAccessScope", HttpMethods.Put, "/api/admin/staff-access/{staffUserId}", "Staff Access", "Replace staff access scope.", "Replaces the access scope of a staff user. Requires a staff bearer token.", "replace_staff_access_scope", Transition()),
            Staff("clearStaffAccessScope", HttpMethods.Delete, "/api/admin/staff-access/{staffUserId}", "Staff Access", "Clear staff access scope.", "Clears the access scope of a staff user. Requires a staff bearer token.", "clear_staff_access_scope", Delete()),
            Staff("getDashboards", HttpMethods.Get, "/api/dashboards", "Dashboards", "Read dashboards.", "Reads the dashboard views visible to the signed-in staff member. Requires a staff bearer token.", "get_dashboards", Read()),
            Staff("getSlotAuditHistory", HttpMethods.Get, "/api/audit/slot/{id}", "Audit", "Read slot audit history.", "Reads the audit history of a slot. Requires a staff bearer token.", "slot_audit_history", Read()),
            Staff("getCandidateAuditHistory", HttpMethods.Get, "/api/audit/candidate/{id}", "Audit", "Read candidate audit history.", "Reads the audit history of a candidate. Requires a staff bearer token.", "candidate_audit_history", Read()),
            Staff("searchAudit", HttpMethods.Get, "/api/audit/search", "Audit", "Search audit events.", "Searches audit events with filters and cursor paging. Requires a staff bearer token.", "search_audit", Read()),
            Staff("listAppointmentSlots", HttpMethods.Get, "/api/appointment-workspace/slots", "Appointment Workspace", "List appointment slots.", "Reads the appointment workspace slots. Requires a staff bearer token.", "appointment_slots", Read()),
            Staff("getAppointmentSlot", HttpMethods.Get, "/api/appointment-workspace/slots/{confirmedSlotId}", "Appointment Workspace", "Read an appointment slot.", "Reads one appointment workspace slot. Requires a staff bearer token.", "appointment_slot_detail", Read()),
            Staff("exportAppointmentRoster", HttpMethods.Get, "/api/appointment-workspace/slots/{confirmedSlotId}/roster", "Appointment Workspace", "Export an appointment roster.", "Exports the appointment roster CSV. Requires a staff bearer token.", "export_appointment_roster", Read()),
            Staff("updateAppointmentStatus", HttpMethods.Put, "/api/appointment-workspace/appointments/{bookingAppointmentId}/status", "Appointment Workspace", "Update appointment status.", "Updates the status of a booking appointment. Requires a staff bearer token.", "update_appointment_status", Transition()),
            Excluded("getApiIndex", HttpMethods.Get, "/api", "Discovery", "Read the API entry document.", "Anonymous API entry document listing principal entry points.", "Transport discovery, not a business capability. MCP has tools/list."),
            Excluded("getOpenApiDocument", HttpMethods.Get, "/openapi/v1.json", "Discovery", "Read the OpenAPI document.", "Anonymous machine-readable API contract.", "Transport discovery, not a business capability."),
            Excluded("getSwaggerUi", HttpMethods.Get, "/swagger", "Discovery", "Open the Swagger UI.", "Anonymous human documentation UI.", "Human documentation UI, not a business capability."),
            Excluded("getHealth", HttpMethods.Get, "/health", "Health", "Read the deployment health probe.", "Anonymous deployment liveness probe.", "Deployment probe, not a staff workflow."),
            Excluded("viewInvite", HttpMethods.Get, "/api/booking/{token}", "Candidate Booking", "View an invite.", "Anonymous candidate token flow.", "Anonymous Candidate token flow; excluded by the approved remote MCP design."),
            Excluded("confirmBooking", HttpMethods.Post, "/api/booking/{token}/confirm", "Candidate Booking", "Confirm a booking.", "Anonymous candidate token flow.", "Anonymous Candidate token flow; excluded by the approved remote MCP design."),
            Excluded("viewManagedBooking", HttpMethods.Get, "/api/booking/manage/{token}", "Candidate Booking", "View a managed booking.", "Anonymous candidate token flow.", "Anonymous Candidate token flow; excluded by the approved remote MCP design."),
            Excluded("cancelManagedBooking", HttpMethods.Post, "/api/booking/manage/{token}/cancel", "Candidate Booking", "Cancel a managed booking.", "Anonymous candidate token flow.", "Anonymous Candidate token flow; excluded by the approved remote MCP design."),
        };

        var byId = new Dictionary<string, AgentOperation>(StringComparer.Ordinal);
        var byRoute = new HashSet<string>(StringComparer.Ordinal);
        var byTool = new HashSet<string>(StringComparer.Ordinal);
        foreach (var operation in operations)
        {
            if ((operation.McpTool is null) == (operation.ExclusionReason is null))
            {
                throw new InvalidOperationException($"Operation {operation.OperationId} must set exactly one of McpTool or ExclusionReason.");
            }

            if (!byId.TryAdd(operation.OperationId, operation))
            {
                throw new InvalidOperationException($"Duplicate operation id {operation.OperationId}.");
            }

            if (!byRoute.Add($"{operation.Method} {operation.Route}"))
            {
                throw new InvalidOperationException($"Duplicate method and route {operation.Method} {operation.Route}.");
            }

            if (operation.McpTool is not null && !byTool.Add(operation.McpTool))
            {
                throw new InvalidOperationException($"Duplicate MCP tool {operation.McpTool}.");
            }
        }

        All = byId;
    }

    /// <summary>Gets one required operation or throws for a programming error.</summary>
    /// <param name="operationId">The operation id to look up.</param>
    /// <returns>The catalogued operation.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the operation id is unknown.</exception>
    public static AgentOperation Get(string operationId) => All[operationId];

    private static AgentOperation Staff(
        string operationId, string method, string route, string tag,
        string summary, string description, string mcpTool, AgentHints hints) =>
        new(operationId, method, route, tag, summary, description, true, mcpTool, null, hints);

    private static AgentOperation Excluded(
        string operationId, string method, string route, string tag,
        string summary, string description, string reason) =>
        new(operationId, method, route, tag, summary, description, false, null, reason, new AgentHints(true, false, true, false));

    private static AgentHints Read() => new(true, false, true, false);

    private static AgentHints Create() => new(false, false, false, false);

    private static AgentHints Transition() => new(false, true, true, false);

    private static AgentHints Delete() => new(false, true, true, false);
}
`````

## after — src/EventBooking.Api/OpenApi/AgentOperationCatalog.cs — 1/1

<!-- vocabulary-file: {"id":21,"oldPath":"src/EventBooking.Api/OpenApi/AgentOperationCatalog.cs","newPath":"src/EventBooking.Api/OpenApi/AgentOperationCatalog.cs","beforeSha":"b39b7c5f6de0d5d007ceaa9fdebfd04fbf53bcbfa553ea87e9e67ce2589736a2","afterSha":"6097e2dcb14e98fd1264aeb990de65895c9d2a44eef3c545987be5876f8ff8d3","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Api.OpenApi;

/// <summary>Behavior hints shared by OpenAPI and MCP discovery.</summary>
/// <param name="ReadOnly">Whether the operation performs no state change.</param>
/// <param name="Destructive">Whether the operation deletes, cancels, withdraws, clears, replaces, or transitions existing state.</param>
/// <param name="Idempotent">Whether repeating the operation has the same effect as performing it once.</param>
/// <param name="OpenWorld">Whether the operation interacts with arbitrary external entities. Always false for this tool set.</param>
public sealed record AgentHints(
    /// <summary>Gets whether the operation performs no state change.</summary>
    bool ReadOnly,
    /// <summary>Gets whether the operation deletes, cancels, withdraws, clears, replaces, or transitions existing state.</summary>
    bool Destructive,
    /// <summary>Gets whether repeating the operation has the same effect as performing it once.</summary>
    bool Idempotent,
    /// <summary>Gets whether the operation interacts with arbitrary external entities.</summary>
    bool OpenWorld);

/// <summary>One stable HTTP operation and its MCP parity decision.</summary>
/// <param name="OperationId">The unique lower-camel-case OpenAPI operation id.</param>
/// <param name="Method">The uppercase HTTP method of the operation.</param>
/// <param name="Route">The route pattern of the operation.</param>
/// <param name="Tag">The OpenAPI tag grouping the operation.</param>
/// <param name="Summary">The short human-readable summary of the operation.</param>
/// <param name="Description">The longer description stating purpose, authorization, and side effects.</param>
/// <param name="RequiresBearer">Whether the operation requires a staff bearer token.</param>
/// <param name="McpTool">The snake-case MCP tool name, or null when intentionally excluded.</param>
/// <param name="ExclusionReason">The reviewed reason for exclusion, or null when an MCP tool exists.</param>
/// <param name="Hints">The behavior hints shared by OpenAPI and MCP discovery.</param>
public sealed record AgentOperation(
    /// <summary>Gets the unique lower-camel-case OpenAPI operation id.</summary>
    string OperationId,
    /// <summary>Gets the uppercase HTTP method of the operation.</summary>
    string Method,
    /// <summary>Gets the route pattern of the operation.</summary>
    string Route,
    /// <summary>Gets the OpenAPI tag grouping the operation.</summary>
    string Tag,
    /// <summary>Gets the short human-readable summary of the operation.</summary>
    string Summary,
    /// <summary>Gets the longer description stating purpose, authorization, and side effects.</summary>
    string Description,
    /// <summary>Gets whether the operation requires a staff bearer token.</summary>
    bool RequiresBearer,
    /// <summary>Gets the snake-case MCP tool name, or null when intentionally excluded.</summary>
    string? McpTool,
    /// <summary>Gets the reviewed reason for exclusion, or null when an MCP tool exists.</summary>
    string? ExclusionReason,
    /// <summary>Gets the behavior hints shared by OpenAPI and MCP discovery.</summary>
    AgentHints Hints);

/// <summary>Shared registry of HTTP operations and their MCP parity decisions.</summary>
public static class AgentOperationCatalog
{
    /// <summary>Gets every catalogued application operation keyed by operation id.</summary>
    public static IReadOnlyDictionary<string, AgentOperation> All { get; }

    static AgentOperationCatalog()
    {
        var operations = new List<AgentOperation>
        {
            Staff("getMyAccess", HttpMethods.Get, "/api/me", "Identity", "Read the signed-in staff access.", "Reads the signed-in staff identity and capability scope. Requires a staff bearer token.", "get_my_access", Read()),
            Staff("getEventBoard", HttpMethods.Get, "/api/events/board", "Events", "Read the scoped event board.", "Reads the event board visible to the signed-in staff member. Requires a staff bearer token.", "event_board", Read()),
            Staff("getEventOperations", HttpMethods.Get, "/api/events/operations", "Events", "Read the scoped event operations view.", "Reads the event operations view visible to the signed-in staff member. Requires a staff bearer token.", "get_event_operations", Read()),
            Staff("proposeEvent", HttpMethods.Post, "/api/event-proposals", "Events", "Propose a eventItem.", "Creates a event proposal. Requires a staff bearer token.", "propose_event", Create()),
            Staff("acceptProposal", HttpMethods.Post, "/api/event-proposals/{id}/acceptance", "Events", "Accept a event proposal.", "Transitions a event proposal to accepted. Requires a staff bearer token.", "accept_proposal", Transition()),
            Staff("withdrawAcceptance", HttpMethods.Delete, "/api/event-proposals/{id}/acceptance", "Events", "Withdraw a event acceptance.", "Withdraws an accepted event proposal. Requires a staff bearer token.", "withdraw_acceptance", Delete()),
            Staff("withdrawProposal", HttpMethods.Delete, "/api/event-proposals/{id}", "Events", "Withdraw a event proposal.", "Withdraws a event proposal. Requires a staff bearer token.", "withdraw_proposal", Delete()),
            Staff("adjustEventCapacity", HttpMethods.Put, "/api/events/{id}/capacity", "Events", "Adjust event capacity.", "Updates the capacity of a eventItem. Requires a staff bearer token.", "adjust_event_capacity", Transition()),
            Staff("cancelEvent", HttpMethods.Delete, "/api/events/{id}", "Events", "Cancel a eventItem.", "Cancels a eventItem. Requires a staff bearer token.", "cancel_event", Delete()),
            Staff("importEvents", HttpMethods.Post, "/api/events/import", "Events", "Import events.", "Imports events from CSV. Requires a staff bearer token.", "import_events", Create()),
            Staff("listAttendees", HttpMethods.Get, "/api/attendees", "Attendees", "List attendees.", "Reads the attendee collection. Requires a staff bearer token.", "list_attendees", Read()),
            Staff("createAttendee", HttpMethods.Post, "/api/attendees", "Attendees", "Create a attendee.", "Creates a attendee. Requires a staff bearer token.", "create_attendee", Create()),
            Staff("updateAttendee", HttpMethods.Put, "/api/attendees/{id}", "Attendees", "Update a attendee.", "Updates an existing attendee. Requires a staff bearer token.", "update_attendee", Transition()),
            Staff("deleteAttendee", HttpMethods.Delete, "/api/attendees/{id}", "Attendees", "Delete a attendee.", "Deletes a attendee. Requires a staff bearer token.", "delete_attendee", Delete()),
            Staff("importAttendees", HttpMethods.Post, "/api/attendees/import", "Attendees", "Import attendees.", "Imports attendees from CSV. Requires a staff bearer token.", "import_attendees", Create()),
            Staff("triggerAttendeeInvite", HttpMethods.Post, "/api/attendees/{id}/invite", "Attendees", "Trigger a attendee invite.", "Sends a booking invite to a attendee. Requires a staff bearer token.", "trigger_invite", Create()),
            Staff("retryAttendeeEmail", HttpMethods.Post, "/api/attendees/{id}/email-retry", "Attendees", "Retry attendee email.", "Retries pending attendee email delivery. Requires a staff bearer token.", "retry_attendee_email", Create()),
            Staff("startRecoveryInvite", HttpMethods.Post, "/api/attendees/{attendeeId}/recovery-invites", "Attendees", "Start a recovery invite.", "Starts a recovery invite for a attendee. Requires a staff bearer token.", "start_recovery_invite", Create()),
            Staff("cancelRecoveryInvite", HttpMethods.Delete, "/api/attendees/{attendeeId}/recovery-invites/{inviteId}", "Attendees", "Cancel a recovery invite.", "Cancels a pending recovery invite. Requires a staff bearer token.", "cancel_recovery_invite", Delete()),
            Staff("listAttendeeBookings", HttpMethods.Get, "/api/attendees/{attendeeId}/bookings", "Attendee Booking", "List attendee bookings.", "Reads the active bookings of a attendee. Requires a staff bearer token.", "list_attendee_bookings", Read()),
            Staff("cancelAttendeeBooking", HttpMethods.Post, "/api/attendees/{attendeeId}/bookings/{bookingId}/cancel", "Attendee Booking", "Cancel a attendee booking.", "Cancels a attendee booking. Requires a staff bearer token.", "cancel_attendee_booking", Delete()),
            Staff("getAttendeeReadiness", HttpMethods.Get, "/api/attendees/{attendeeId}/readiness", "Attendees", "Read attendee readiness.", "Reads the booking readiness of a attendee. Requires a staff bearer token.", "get_attendee_readiness", Read()),
            Staff("listAttendeeGroups", HttpMethods.Get, "/api/attendee-groups", "Attendee Groups", "List attendee groups.", "Reads the attendee group collection. Requires a staff bearer token.", "list_attendee_groups", Read()),
            Staff("getSettings", HttpMethods.Get, "/api/admin/settings", "Settings", "Read settings.", "Reads the application settings. Requires a staff bearer token.", "get_settings", Read()),
            Staff("updateSettings", HttpMethods.Put, "/api/admin/settings", "Settings", "Update settings.", "Updates the application settings. Requires a staff bearer token.", "update_settings", Transition()),
            Staff("listStaffAccess", HttpMethods.Get, "/api/admin/staff-access", "Staff Access", "List staff access.", "Reads the staff access collection. Requires a staff bearer token.", "list_staff_access", Read()),
            Staff("replaceStaffAccessScope", HttpMethods.Put, "/api/admin/staff-access/{staffUserId}", "Staff Access", "Replace staff access scope.", "Replaces the access scope of a staff user. Requires a staff bearer token.", "replace_staff_access_scope", Transition()),
            Staff("clearStaffAccessScope", HttpMethods.Delete, "/api/admin/staff-access/{staffUserId}", "Staff Access", "Clear staff access scope.", "Clears the access scope of a staff user. Requires a staff bearer token.", "clear_staff_access_scope", Delete()),
            Staff("getDashboards", HttpMethods.Get, "/api/dashboards", "Dashboards", "Read dashboards.", "Reads the dashboard views visible to the signed-in staff member. Requires a staff bearer token.", "get_dashboards", Read()),
            Staff("getEventAuditHistory", HttpMethods.Get, "/api/audit/event/{id}", "Audit", "Read event audit history.", "Reads the audit history of a eventItem. Requires a staff bearer token.", "event_audit_history", Read()),
            Staff("getAttendeeAuditHistory", HttpMethods.Get, "/api/audit/attendee/{id}", "Audit", "Read attendee audit history.", "Reads the audit history of a attendee. Requires a staff bearer token.", "attendee_audit_history", Read()),
            Staff("searchAudit", HttpMethods.Get, "/api/audit/search", "Audit", "Search audit events.", "Searches audit events with filters and cursor paging. Requires a staff bearer token.", "search_audit", Read()),
            Staff("listAppointmentEvents", HttpMethods.Get, "/api/appointment-workspace/events", "Appointment Workspace", "List appointment events.", "Reads the appointment workspace events. Requires a staff bearer token.", "appointment_events", Read()),
            Staff("getAppointmentEvent", HttpMethods.Get, "/api/appointment-workspace/events/{eventId}", "Appointment Workspace", "Read an appointment eventItem.", "Reads one appointment workspace eventItem. Requires a staff bearer token.", "appointment_event_detail", Read()),
            Staff("exportAppointmentRoster", HttpMethods.Get, "/api/appointment-workspace/events/{eventId}/roster", "Appointment Workspace", "Export an appointment roster.", "Exports the appointment roster CSV. Requires a staff bearer token.", "export_appointment_roster", Read()),
            Staff("updateAppointmentStatus", HttpMethods.Put, "/api/appointment-workspace/appointments/{bookingAppointmentId}/status", "Appointment Workspace", "Update appointment status.", "Updates the status of a booking appointment. Requires a staff bearer token.", "update_appointment_status", Transition()),
            Excluded("getApiIndex", HttpMethods.Get, "/api", "Discovery", "Read the API entry document.", "Anonymous API entry document listing principal entry points.", "Transport discovery, not a business capability. MCP has tools/list."),
            Excluded("getOpenApiDocument", HttpMethods.Get, "/openapi/v1.json", "Discovery", "Read the OpenAPI document.", "Anonymous machine-readable API contract.", "Transport discovery, not a business capability."),
            Excluded("getSwaggerUi", HttpMethods.Get, "/swagger", "Discovery", "Open the Swagger UI.", "Anonymous human documentation UI.", "Human documentation UI, not a business capability."),
            Excluded("getHealth", HttpMethods.Get, "/health", "Health", "Read the deployment health probe.", "Anonymous deployment liveness probe.", "Deployment probe, not a staff workflow."),
            Excluded("viewInvite", HttpMethods.Get, "/api/booking/{token}", "Attendee Booking", "View an invite.", "Anonymous attendee token flow.", "Anonymous Attendee token flow; excluded by the approved remote MCP design."),
            Excluded("confirmBooking", HttpMethods.Post, "/api/booking/{token}/confirm", "Attendee Booking", "Confirm a booking.", "Anonymous attendee token flow.", "Anonymous Attendee token flow; excluded by the approved remote MCP design."),
            Excluded("viewManagedBooking", HttpMethods.Get, "/api/booking/manage/{token}", "Attendee Booking", "View a managed booking.", "Anonymous attendee token flow.", "Anonymous Attendee token flow; excluded by the approved remote MCP design."),
            Excluded("cancelManagedBooking", HttpMethods.Post, "/api/booking/manage/{token}/cancel", "Attendee Booking", "Cancel a managed booking.", "Anonymous attendee token flow.", "Anonymous Attendee token flow; excluded by the approved remote MCP design."),
        };

        var byId = new Dictionary<string, AgentOperation>(StringComparer.Ordinal);
        var byRoute = new HashSet<string>(StringComparer.Ordinal);
        var byTool = new HashSet<string>(StringComparer.Ordinal);
        foreach (var operation in operations)
        {
            if ((operation.McpTool is null) == (operation.ExclusionReason is null))
            {
                throw new InvalidOperationException($"Operation {operation.OperationId} must set exactly one of McpTool or ExclusionReason.");
            }

            if (!byId.TryAdd(operation.OperationId, operation))
            {
                throw new InvalidOperationException($"Duplicate operation id {operation.OperationId}.");
            }

            if (!byRoute.Add($"{operation.Method} {operation.Route}"))
            {
                throw new InvalidOperationException($"Duplicate method and route {operation.Method} {operation.Route}.");
            }

            if (operation.McpTool is not null && !byTool.Add(operation.McpTool))
            {
                throw new InvalidOperationException($"Duplicate MCP tool {operation.McpTool}.");
            }
        }

        All = byId;
    }

    /// <summary>Gets one required operation or throws for a programming error.</summary>
    /// <param name="operationId">The operation id to look up.</param>
    /// <returns>The catalogued operation.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the operation id is unknown.</exception>
    public static AgentOperation Get(string operationId) => All[operationId];

    private static AgentOperation Staff(
        string operationId, string method, string route, string tag,
        string summary, string description, string mcpTool, AgentHints hints) =>
        new(operationId, method, route, tag, summary, description, true, mcpTool, null, hints);

    private static AgentOperation Excluded(
        string operationId, string method, string route, string tag,
        string summary, string description, string reason) =>
        new(operationId, method, route, tag, summary, description, false, null, reason, new AgentHints(true, false, true, false));

    private static AgentHints Read() => new(true, false, true, false);

    private static AgentHints Create() => new(false, false, false, false);

    private static AgentHints Transition() => new(false, true, true, false);

    private static AgentHints Delete() => new(false, true, true, false);
}
`````

## before — src/EventBooking.Api/OpenApi/OpenApiConfiguration.cs — 1/1

<!-- vocabulary-file: {"id":22,"oldPath":"src/EventBooking.Api/OpenApi/OpenApiConfiguration.cs","newPath":"src/EventBooking.Api/OpenApi/OpenApiConfiguration.cs","beforeSha":"bb145267497bd88e13905e93c24ea810a344ad809318e0d6fc17756f8ab7f920","afterSha":"0c19e1de975e98331f8b1d11221b7e305b66bdae79d7a715851a9c35fbabcdf8","side":"before","part":1,"parts":1} -->

`````csharp
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace EventBooking.Api.OpenApi;

/// <summary>Registers the first-party OpenAPI document with agent extensions.</summary>
public static class OpenApiConfiguration
{
    /// <summary>Registers the v1 document, security scheme, and agent extensions.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEventBookingOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "EventBooking API",
                    Version = "v1",
                    Description = "EventBooking staff API and anonymous Candidate booking links.",
                };
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes["bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                };
                return Task.CompletedTask;
            });
            options.AddOperationTransformer((operation, context, _) =>
            {
                var name = context.Description.ActionDescriptor.EndpointMetadata
                    .OfType<EndpointNameMetadata>()
                    .FirstOrDefault()?.EndpointName;
                if (name is null || !AgentOperationCatalog.All.TryGetValue(name, out var entry))
                {
                    return Task.CompletedTask;
                }

                if (entry.McpTool is not null)
                {
                    operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                    operation.Extensions["x-mcp-tool"] = new JsonNodeExtension(JsonValue.Create(entry.McpTool)!);
                    operation.Extensions["x-agent-hints"] = new JsonNodeExtension(new JsonObject
                    {
                        ["readOnly"] = entry.Hints.ReadOnly,
                        ["destructive"] = entry.Hints.Destructive,
                        ["idempotent"] = entry.Hints.Idempotent,
                        ["openWorld"] = entry.Hints.OpenWorld,
                    });
                }

                if (entry.RequiresBearer)
                {
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("bearer")] = [],
                    });
                }

                return Task.CompletedTask;
            });
        });
        return services;
    }
}
`````

## after — src/EventBooking.Api/OpenApi/OpenApiConfiguration.cs — 1/1

<!-- vocabulary-file: {"id":22,"oldPath":"src/EventBooking.Api/OpenApi/OpenApiConfiguration.cs","newPath":"src/EventBooking.Api/OpenApi/OpenApiConfiguration.cs","beforeSha":"bb145267497bd88e13905e93c24ea810a344ad809318e0d6fc17756f8ab7f920","afterSha":"0c19e1de975e98331f8b1d11221b7e305b66bdae79d7a715851a9c35fbabcdf8","side":"after","part":1,"parts":1} -->

`````csharp
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace EventBooking.Api.OpenApi;

/// <summary>Registers the first-party OpenAPI document with agent extensions.</summary>
public static class OpenApiConfiguration
{
    /// <summary>Registers the v1 document, security scheme, and agent extensions.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEventBookingOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "EventBooking API",
                    Version = "v1",
                    Description = "EventBooking staff API and anonymous Attendee booking links.",
                };
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes["bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                };
                return Task.CompletedTask;
            });
            options.AddOperationTransformer((operation, context, _) =>
            {
                var name = context.Description.ActionDescriptor.EndpointMetadata
                    .OfType<EndpointNameMetadata>()
                    .FirstOrDefault()?.EndpointName;
                if (name is null || !AgentOperationCatalog.All.TryGetValue(name, out var entry))
                {
                    return Task.CompletedTask;
                }

                if (entry.McpTool is not null)
                {
                    operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                    operation.Extensions["x-mcp-tool"] = new JsonNodeExtension(JsonValue.Create(entry.McpTool)!);
                    operation.Extensions["x-agent-hints"] = new JsonNodeExtension(new JsonObject
                    {
                        ["readOnly"] = entry.Hints.ReadOnly,
                        ["destructive"] = entry.Hints.Destructive,
                        ["idempotent"] = entry.Hints.Idempotent,
                        ["openWorld"] = entry.Hints.OpenWorld,
                    });
                }

                if (entry.RequiresBearer)
                {
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("bearer")] = [],
                    });
                }

                return Task.CompletedTask;
            });
        });
        return services;
    }
}
`````

## before — src/EventBooking.Api/Program.cs — 1/1

<!-- vocabulary-file: {"id":23,"oldPath":"src/EventBooking.Api/Program.cs","newPath":"src/EventBooking.Api/Program.cs","beforeSha":"c1ace40de0a90cec9d634746bc9a7c86e234bbd4112d5c91cb16e3c4f3618d7a","afterSha":"7ea712cb8cac6276f9eb4a8cc7b69dcd6b7707f5cda9366cad7d8aec4de954c2","side":"before","part":1,"parts":1} -->

`````csharp
using System.Threading.RateLimiting;
using EventBooking.Api;
using EventBooking.Api.Auth;
using EventBooking.Api.Endpoints;
using EventBooking.Application;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Email;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using EventBooking.Api.OpenApi;

var builder = WebApplication.CreateBuilder(args);


const string WebClientCorsPolicy = "web-client";
var allowedWebOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

var (connectionString, headOffice, tokens, email, portal) =
    EventBookingConfiguration.Read(builder.Configuration);

builder.Services.AddEventBookingInfrastructure(connectionString, headOffice, tokens);

var smtpHost = builder.Configuration["Email:Smtp:Host"]
    ?? throw new InvalidOperationException(
        "Email:Smtp:Host is required when Email:Provider is Smtp.");
var smtpPort = int.TryParse(builder.Configuration["Email:Smtp:Port"], out var port)
    ? port
    : throw new InvalidOperationException(
        "Email:Smtp:Port must be a valid integer when Email:Provider is Smtp.");

builder.Services.AddLocalEmailTransport(email, new SmtpOptions(smtpHost, smtpPort));
builder.Services.AddEventBookingApplication(portal);
builder.Services.AddEventBookingAuthentication(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddEventBookingOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy(WebClientCorsPolicy, policy =>
    {
        if (allowedWebOrigins.Length > 0)
        {
            policy
                .WithOrigins(allowedWebOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .WithExposedHeaders("Content-Disposition");
        }
    });
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Anonymous, token-addressed routes: generous for a real candidate, unattractive for a script.
    // Partitioned by client address so one client's burst (or script) cannot consume the
    // allowance of every other candidate. ForwardedHeadersMiddleware (below) restores the real
    // client address when the app runs behind a proxy or load balancer.
    options.AddPolicy<string, RemoteIpRateLimiterPolicy>(BookingEndpoints.RateLimiterPolicy);
});

builder.Services.AddHostedService<InviteSweepService>();

var app = builder.Build();

app.MapOpenApi("/openapi/{documentName}.json").AllowAnonymous();
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "EventBooking API v1";
    options.SwaggerEndpoint("/openapi/v1.json", "EventBooking API v1");
    options.DisplayOperationId();
    options.HeadContent = "<link rel=\"alternate\" type=\"application/json\" href=\"/openapi/v1.json\" />";
});
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
});
app.UseCors(WebClientCorsPolicy);
app.UseAuthentication();
app.UseMiddleware<StaffIdentityRecorder>();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous().WithAgentMetadata("getHealth");
app.MapApiDiscoveryEndpoints();

app.MapSlotEndpoints();
app.MapCandidateEndpoints();
app.MapEmployeeGroupEndpoints();
app.MapAdminEndpoints();
app.MapStaffAccessEndpoints();
app.MapMeEndpoints();
app.MapBookingEndpoints();
app.MapDashboardEndpoints();
app.MapAuditEndpoints();
app.MapAppointmentWorkspaceEndpoints();

app.Run();

/// <summary>Named so the integration test factory can start this host.</summary>
public partial class Program;
`````

## after — src/EventBooking.Api/Program.cs — 1/1

<!-- vocabulary-file: {"id":23,"oldPath":"src/EventBooking.Api/Program.cs","newPath":"src/EventBooking.Api/Program.cs","beforeSha":"c1ace40de0a90cec9d634746bc9a7c86e234bbd4112d5c91cb16e3c4f3618d7a","afterSha":"7ea712cb8cac6276f9eb4a8cc7b69dcd6b7707f5cda9366cad7d8aec4de954c2","side":"after","part":1,"parts":1} -->

`````csharp
using System.Threading.RateLimiting;
using EventBooking.Api;
using EventBooking.Api.Auth;
using EventBooking.Api.Endpoints;
using EventBooking.Application;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Email;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using EventBooking.Api.OpenApi;

var builder = WebApplication.CreateBuilder(args);


const string WebClientCorsPolicy = "web-client";
var allowedWebOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

var (connectionString, transitionalLocation, tokens, email, portal) =
    EventBookingConfiguration.Read(builder.Configuration);

builder.Services.AddEventBookingInfrastructure(connectionString, transitionalLocation, tokens);

var smtpHost = builder.Configuration["Email:Smtp:Host"]
    ?? throw new InvalidOperationException(
        "Email:Smtp:Host is required when Email:Provider is Smtp.");
var smtpPort = int.TryParse(builder.Configuration["Email:Smtp:Port"], out var port)
    ? port
    : throw new InvalidOperationException(
        "Email:Smtp:Port must be a valid integer when Email:Provider is Smtp.");

builder.Services.AddLocalEmailTransport(email, new SmtpOptions(smtpHost, smtpPort));
builder.Services.AddEventBookingApplication(portal);
builder.Services.AddEventBookingAuthentication(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddEventBookingOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy(WebClientCorsPolicy, policy =>
    {
        if (allowedWebOrigins.Length > 0)
        {
            policy
                .WithOrigins(allowedWebOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .WithExposedHeaders("Content-Disposition");
        }
    });
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Anonymous, token-addressed routes: generous for a real attendee, unattractive for a script.
    // Partitioned by client address so one client's burst (or script) cannot consume the
    // allowance of every other attendee. ForwardedHeadersMiddleware (below) restores the real
    // client address when the app runs behind a proxy or load balancer.
    options.AddPolicy<string, RemoteIpRateLimiterPolicy>(BookingEndpoints.RateLimiterPolicy);
});

builder.Services.AddHostedService<InviteSweepService>();

var app = builder.Build();

app.MapOpenApi("/openapi/{documentName}.json").AllowAnonymous();
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "EventBooking API v1";
    options.SwaggerEndpoint("/openapi/v1.json", "EventBooking API v1");
    options.DisplayOperationId();
    options.HeadContent = "<link rel=\"alternate\" type=\"application/json\" href=\"/openapi/v1.json\" />";
});
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
});
app.UseCors(WebClientCorsPolicy);
app.UseAuthentication();
app.UseMiddleware<StaffIdentityRecorder>();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous().WithAgentMetadata("getHealth");
app.MapApiDiscoveryEndpoints();

app.MapEventEndpoints();
app.MapAttendeeEndpoints();
app.MapAttendeeGroupEndpoints();
app.MapAdminEndpoints();
app.MapStaffAccessEndpoints();
app.MapMeEndpoints();
app.MapBookingEndpoints();
app.MapDashboardEndpoints();
app.MapAuditEndpoints();
app.MapAppointmentWorkspaceEndpoints();

app.Run();

/// <summary>Named so the integration test factory can start this host.</summary>
public partial class Program;
`````

## before — src/EventBooking.Api/appsettings.Local.json — 1/1

<!-- vocabulary-file: {"id":24,"oldPath":"src/EventBooking.Api/appsettings.Local.json","newPath":"src/EventBooking.Api/appsettings.Local.json","beforeSha":"b42048a31267f7418519ee1ba7a6f5e444a85f6c5d06f8298fb2d9f9e9a838fb","afterSha":"19da1e0664eb87dfaae9b4953c8f16ca0ce42dc866e02982e37ebf4b38bf77a8","side":"before","part":1,"parts":1} -->

`````text
{
  "ConnectionStrings": {
    "EventBooking": "Host=localhost;Database=eventbooking;Username=eventbooking_app;Password=eventbooking_local"
  },
  "HeadOffice": {
    "TimeZoneId": "Europe/London",
    "Address": "1 Example Street, London"
  },
  "Tokens": {
    "SigningKey": "a-local-signing-key-that-is-at-least-32-characters"
  },
  "Auth": {
    "Provider": "Local",
    "Local": {
      "Authority": "http://localhost:8081/realms/eventbooking",
      "Audience": "eventbooking-web"
    }
  },
  "Email": {
    "Provider": "Smtp",
    "FromAddress": "recruitment@example.com",
    "FromName": "Recruitment Team",
    "Smtp": {
      "Host": "localhost",
      "Port": 1025
    }
  },
  "Portal": {
    "BaseUrl": "http://localhost:5002",
    "CoordinatorContact": "recruitment@example.com"
  }
}
`````

## after — src/EventBooking.Api/appsettings.Local.json — 1/1

<!-- vocabulary-file: {"id":24,"oldPath":"src/EventBooking.Api/appsettings.Local.json","newPath":"src/EventBooking.Api/appsettings.Local.json","beforeSha":"b42048a31267f7418519ee1ba7a6f5e444a85f6c5d06f8298fb2d9f9e9a838fb","afterSha":"19da1e0664eb87dfaae9b4953c8f16ca0ce42dc866e02982e37ebf4b38bf77a8","side":"after","part":1,"parts":1} -->

`````text
{
  "ConnectionStrings": {
    "EventBooking": "Host=localhost;Database=eventbooking;Username=eventbooking_app;Password=eventbooking_local"
  },
  "TransitionalLocation": {
    "TimeZoneId": "Europe/London",
    "Address": "1 Example Street, London"
  },
  "Tokens": {
    "SigningKey": "a-local-signing-key-that-is-at-least-32-characters"
  },
  "Auth": {
    "Provider": "Local",
    "Local": {
      "Authority": "http://localhost:8081/realms/eventbooking",
      "Audience": "eventbooking-web"
    }
  },
  "Email": {
    "Provider": "Smtp",
    "FromAddress": "recruitment@example.com",
    "FromName": "Recruitment Team",
    "Smtp": {
      "Host": "localhost",
      "Port": 1025
    }
  },
  "Portal": {
    "BaseUrl": "http://localhost:5002",
    "CoordinatorContact": "recruitment@example.com"
  }
}
`````

## before — src/EventBooking.Api/appsettings.json — 1/1

<!-- vocabulary-file: {"id":25,"oldPath":"src/EventBooking.Api/appsettings.json","newPath":"src/EventBooking.Api/appsettings.json","beforeSha":"3db7caaea817b92399958425edcfe0a1b69730070dc7586385e0a3fde6def9ec","afterSha":"6855695313e4a2b2d82e55823cb4fb8b1db347cedec8dea8c8fe671672331f4a","side":"before","part":1,"parts":1} -->

`````text
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Cors": {
    "AllowedOrigins": [
      "https://localhost:5002",
      "http://localhost:5002"
    ]
  },
  "ConnectionStrings": {
    "EventBooking": "Host=localhost;Database=eventbooking;Username=postgres;Password=postgres"
  },
  "HeadOffice": {
    "TimeZoneId": "Europe/London",
    "Address": "Corporate HQ, 1 Example Street, London"
  },
  "Tokens": {
    "SigningKey": "replace-this-with-a-real-secret-of-at-least-32-characters"
  },
  "Auth": {
    "Provider": "Local",
    "Local": {
      "Authority": "http://localhost:8081/realms/eventbooking",
      "Audience": "eventbooking-web"
    }
  },
  "Email": {
    "Provider": "Smtp",
    "FromAddress": "recruitment@example.com",
    "FromName": "Recruitment Team",
    "Smtp": {
      "Host": "localhost",
      "Port": 1025
    }
  },
  "Portal": {
    "BaseUrl": "https://localhost:5001",
    "CoordinatorContact": "recruitment@example.com"
  }
}
`````

## after — src/EventBooking.Api/appsettings.json — 1/1

<!-- vocabulary-file: {"id":25,"oldPath":"src/EventBooking.Api/appsettings.json","newPath":"src/EventBooking.Api/appsettings.json","beforeSha":"3db7caaea817b92399958425edcfe0a1b69730070dc7586385e0a3fde6def9ec","afterSha":"6855695313e4a2b2d82e55823cb4fb8b1db347cedec8dea8c8fe671672331f4a","side":"after","part":1,"parts":1} -->

`````text
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Cors": {
    "AllowedOrigins": [
      "https://localhost:5002",
      "http://localhost:5002"
    ]
  },
  "ConnectionStrings": {
    "EventBooking": "Host=localhost;Database=eventbooking;Username=postgres;Password=postgres"
  },
  "TransitionalLocation": {
    "TimeZoneId": "Europe/London",
    "Address": "Corporate HQ, 1 Example Street, London"
  },
  "Tokens": {
    "SigningKey": "replace-this-with-a-real-secret-of-at-least-32-characters"
  },
  "Auth": {
    "Provider": "Local",
    "Local": {
      "Authority": "http://localhost:8081/realms/eventbooking",
      "Audience": "eventbooking-web"
    }
  },
  "Email": {
    "Provider": "Smtp",
    "FromAddress": "recruitment@example.com",
    "FromName": "Recruitment Team",
    "Smtp": {
      "Host": "localhost",
      "Port": 1025
    }
  },
  "Portal": {
    "BaseUrl": "https://localhost:5001",
    "CoordinatorContact": "recruitment@example.com"
  }
}
`````

## before — src/EventBooking.Application/Abstractions/IAppointmentWorkspaceQueries.cs — 1/1

<!-- vocabulary-file: {"id":26,"oldPath":"src/EventBooking.Application/Abstractions/IAppointmentWorkspaceQueries.cs","newPath":"src/EventBooking.Application/Abstractions/IAppointmentWorkspaceQueries.cs","beforeSha":"60f38618a6fc16508f1f51c6c5315c2286f6253765e051472b7da9cc6376b6c6","afterSha":"f86212925390c5ff817c3d80b4f635712449b5bf12e4002997df93f2a043c015","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Appointments;

namespace EventBooking.Application.Abstractions;

/// <summary>Projects only appointment-delivery data inside a trusted appointment-type scope.</summary>
public interface IAppointmentWorkspaceQueries
{
    /// <summary>Lists recent-past, current, and future active slots containing active bookings in trusted scope.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    /// <param name="onOrAfter">The on or after.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AppointmentWorkspaceSlotList> ListSlotsAsync(
        Guid appointmentTypeId,
        DateOnly onOrAfter,
        CancellationToken cancellationToken);

    /// <summary>Gets one active slot's minimum appointment rows inside trusted scope.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    /// <param name="confirmedSlotId">The confirmed slot id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AppointmentSlotDetail?> GetSlotAsync(
        Guid appointmentTypeId,
        Guid confirmedSlotId,
        CancellationToken cancellationToken);
}
`````

## after — src/EventBooking.Application/Abstractions/IAppointmentWorkspaceQueries.cs — 1/1

<!-- vocabulary-file: {"id":26,"oldPath":"src/EventBooking.Application/Abstractions/IAppointmentWorkspaceQueries.cs","newPath":"src/EventBooking.Application/Abstractions/IAppointmentWorkspaceQueries.cs","beforeSha":"60f38618a6fc16508f1f51c6c5315c2286f6253765e051472b7da9cc6376b6c6","afterSha":"f86212925390c5ff817c3d80b4f635712449b5bf12e4002997df93f2a043c015","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Appointments;

namespace EventBooking.Application.Abstractions;

/// <summary>Projects only appointment-delivery data inside a trusted appointment-type scope.</summary>
public interface IAppointmentWorkspaceQueries
{
    /// <summary>Lists recent-past, current, and future active events containing active bookings in trusted scope.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    /// <param name="onOrAfter">The on or after.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AppointmentWorkspaceEventList> ListEventsAsync(
        Guid appointmentTypeId,
        DateOnly onOrAfter,
        CancellationToken cancellationToken);

    /// <summary>Gets one active event's minimum appointment rows inside trusted scope.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    /// <param name="eventId">The event id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AppointmentEventDetail?> GetEventAsync(
        Guid appointmentTypeId,
        Guid eventId,
        CancellationToken cancellationToken);
}
`````

## before — src/EventBooking.Application/Abstractions/IAuditQueries.cs — 1/1

<!-- vocabulary-file: {"id":27,"oldPath":"src/EventBooking.Application/Abstractions/IAuditQueries.cs","newPath":"src/EventBooking.Application/Abstractions/IAuditQueries.cs","beforeSha":"6cd75710abe6bc99a89d0a60b3616ca3f08d3d02277beea87c5eb5d08cf704a1","afterSha":"f30a9944fd96da3fa0ba1b3ef94fc51b07232c2c60be170828e6bbc041ca6a55","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Application.Abstractions;

/// <summary>A single row of audit history for one state change recorded in the audit log.</summary>
/// <param name="Timestamp">When the audited state change was recorded.</param>
/// <param name="EntityType">The audited entity type the row describes.</param>
/// <param name="EntityId">The identifier of the audited entity instance.</param>
/// <param name="Action">The recorded audit action name.</param>
/// <param name="ActorType">Who caused the change: staff, candidate token, or system.</param>
/// <param name="ActorId">The actor identifier, or null for a system actor.</param>
/// <param name="Details">Fixed identifiers, codes, and statuses only; never personal data.</param>
public sealed record AuditHistoryRow(
    DateTimeOffset Timestamp,
    string EntityType,
    Guid EntityId,
    string Action,
    string ActorType,
    string? ActorId,
    string? Details);

/// <summary>Cross-cutting audit search criteria. AllowedEntityTypes is set by the handler, never by the caller.</summary>
/// <param name="From">Inclusive lower bound on the recorded timestamp, or null for no bound.</param>
/// <param name="To">Inclusive upper bound on the recorded timestamp, or null for no bound.</param>
/// <param name="ActorType">Actor type name to match, or null for any.</param>
/// <param name="Action">Audit action name to match, or null for any.</param>
/// <param name="Identifier">Free-text identifier matched exactly against entity id or actor id, or null.</param>
/// <param name="AllowedEntityTypes">Entity types the caller may see, computed from granted capabilities.</param>
/// <param name="EntityType">Optional single entity type requested within the allowed bucket.</param>
/// <param name="Cursor">Opaque keyset cursor for the next page, or null for the newest page.</param>
/// <param name="PageSize">Rows per page; clamped to 200 by the implementation.</param>
public sealed record AuditSearchFilter(
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? ActorType,
    string? Action,
    string? Identifier,
    IReadOnlyList<string> AllowedEntityTypes,
    string? EntityType,
    string? Cursor,
    int PageSize = 50);

/// <summary>One page of audit search results, newest first.</summary>
/// <param name="Rows">The result rows in newest-first order.</param>
/// <param name="NextCursor">Opaque cursor for the following page, or null when exhausted.</param>
public sealed record AuditSearchPage(
    IReadOnlyList<AuditHistoryRow> Rows,
    string? NextCursor);

/// <summary>Defines iaudit queries for the current use case.</summary>
public interface IAuditQueries
{
    /// <summary>Provides for entity async within this contract.</summary>
    /// <param name="entityType">The entity type.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<AuditHistoryRow>> ForEntityAsync(
        string entityType, Guid entityId, CancellationToken cancellationToken);

    /// <summary>
    /// Every entry recorded against the candidate record itself and against their invites, bookings,
    /// and booking appointments, newest first. The caller must already hold candidate-audit access.
    /// </summary>
    /// <param name="candidateId">The candidate id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<AuditHistoryRow>> ForCandidateAsync(
        Guid candidateId, CancellationToken cancellationToken);

    /// <summary>Cross-cutting newest-first keyset-paginated search over the audit log.</summary>
    /// <param name="filter">The filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AuditSearchPage> SearchAsync(AuditSearchFilter filter, CancellationToken cancellationToken);
}
`````

## after — src/EventBooking.Application/Abstractions/IAuditQueries.cs — 1/1

<!-- vocabulary-file: {"id":27,"oldPath":"src/EventBooking.Application/Abstractions/IAuditQueries.cs","newPath":"src/EventBooking.Application/Abstractions/IAuditQueries.cs","beforeSha":"6cd75710abe6bc99a89d0a60b3616ca3f08d3d02277beea87c5eb5d08cf704a1","afterSha":"f30a9944fd96da3fa0ba1b3ef94fc51b07232c2c60be170828e6bbc041ca6a55","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Application.Abstractions;

/// <summary>A single row of audit history for one state change recorded in the audit log.</summary>
/// <param name="Timestamp">When the audited state change was recorded.</param>
/// <param name="EntityType">The audited entity type the row describes.</param>
/// <param name="EntityId">The identifier of the audited entity instance.</param>
/// <param name="Action">The recorded audit action name.</param>
/// <param name="ActorType">Who caused the change: staff, attendee token, or system.</param>
/// <param name="ActorId">The actor identifier, or null for a system actor.</param>
/// <param name="Details">Fixed identifiers, codes, and statuses only; never personal data.</param>
public sealed record AuditHistoryRow(
    DateTimeOffset Timestamp,
    string EntityType,
    Guid EntityId,
    string Action,
    string ActorType,
    string? ActorId,
    string? Details);

/// <summary>Cross-cutting audit search criteria. AllowedEntityTypes is set by the handler, never by the caller.</summary>
/// <param name="From">Inclusive lower bound on the recorded timestamp, or null for no bound.</param>
/// <param name="To">Inclusive upper bound on the recorded timestamp, or null for no bound.</param>
/// <param name="ActorType">Actor type name to match, or null for any.</param>
/// <param name="Action">Audit action name to match, or null for any.</param>
/// <param name="Identifier">Free-text identifier matched exactly against entity id or actor id, or null.</param>
/// <param name="AllowedEntityTypes">Entity types the caller may see, computed from granted capabilities.</param>
/// <param name="EntityType">Optional single entity type requested within the allowed bucket.</param>
/// <param name="Cursor">Opaque keyset cursor for the next page, or null for the newest page.</param>
/// <param name="PageSize">Rows per page; clamped to 200 by the implementation.</param>
public sealed record AuditSearchFilter(
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? ActorType,
    string? Action,
    string? Identifier,
    IReadOnlyList<string> AllowedEntityTypes,
    string? EntityType,
    string? Cursor,
    int PageSize = 50);

/// <summary>One page of audit search results, newest first.</summary>
/// <param name="Rows">The result rows in newest-first order.</param>
/// <param name="NextCursor">Opaque cursor for the following page, or null when exhausted.</param>
public sealed record AuditSearchPage(
    IReadOnlyList<AuditHistoryRow> Rows,
    string? NextCursor);

/// <summary>Defines iaudit queries for the current use case.</summary>
public interface IAuditQueries
{
    /// <summary>Provides for entity async within this contract.</summary>
    /// <param name="entityType">The entity type.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<AuditHistoryRow>> ForEntityAsync(
        string entityType, Guid entityId, CancellationToken cancellationToken);

    /// <summary>
    /// Every entry recorded against the attendee record itself and against their invites, bookings,
    /// and booking appointments, newest first. The caller must already hold attendee-audit access.
    /// </summary>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<AuditHistoryRow>> ForAttendeeAsync(
        Guid attendeeId, CancellationToken cancellationToken);

    /// <summary>Cross-cutting newest-first keyset-paginated search over the audit log.</summary>
    /// <param name="filter">The filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AuditSearchPage> SearchAsync(AuditSearchFilter filter, CancellationToken cancellationToken);
}
`````

## before — src/EventBooking.Application/Abstractions/IBookingAppointmentRepository.cs — 1/1

<!-- vocabulary-file: {"id":28,"oldPath":"src/EventBooking.Application/Abstractions/IBookingAppointmentRepository.cs","newPath":"src/EventBooking.Application/Abstractions/IBookingAppointmentRepository.cs","beforeSha":"11bbf8506b3f04a2f169271d42a1512f11b292fe7ea4b14c44a969c1dc168f69","afterSha":"de8b323ab1206c661dd806542d9984b5bae679f138f28cade497c0486886c4c3","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Abstractions;

/// <summary>Identifies the lifecycle owners of one appointment without granting authority.</summary>
/// <param name="CandidateId">The candidate owning the parent Booking.</param>
/// <param name="OriginalBookingId">The journey-root Booking identifier.</param>
/// <param name="BookingId">The parent booking identifier.</param>
/// <param name="ConfirmedSlotId">The confirmed slot selected by that booking.</param>
/// <param name="AppointmentTypeId">The appointment type scoping the lookup.</param>
public sealed record BookingAppointmentLocator(
    Guid CandidateId,
    Guid OriginalBookingId,
    Guid BookingId,
    Guid ConfirmedSlotId,
    Guid AppointmentTypeId);

/// <summary>Persists and locks booking appointments without widening appointment-type scope.</summary>
public interface IBookingAppointmentRepository
{
    /// <summary>Adds one appointment to the current unit of work.</summary>
    /// <param name="appointment">The appointment to track.</param>
    void Add(BookingAppointment appointment);

    /// <summary>Finds immutable parent identifiers only when record and trusted type both match.</summary>
    /// <param name="id">The stable appointment-record identifier.</param>
    /// <param name="appointmentTypeId">The trusted appointment-type scope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The immutable parent identifiers, or null when out of scope.</returns>
    Task<BookingAppointmentLocator?> FindLocatorInScopeAsync(
        Guid id,
        Guid appointmentTypeId,
        CancellationToken cancellationToken);

    /// <summary>Locks and returns one appointment only when record and trusted type both match.</summary>
    /// <param name="id">The stable appointment-record identifier.</param>
    /// <param name="appointmentTypeId">The trusted appointment-type scope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The locked appointment, or null when out of scope.</returns>
    Task<BookingAppointment?> LockForUpdateAsync(
        Guid id,
        Guid appointmentTypeId,
        CancellationToken cancellationToken);

    /// <summary>Locks all appointments for one Booking ordered by stable ID.</summary>
    /// <param name="bookingId">The parent booking identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The locked appointments in stable ID order.</returns>
    Task<IReadOnlyList<BookingAppointment>> LockForBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken);

    /// <summary>Lists the immutable Appointment Type snapshot owned by one Booking.</summary>
    /// <param name="bookingId">The booking id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<BookingAppointment>> ListForBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists the immutable Appointment Type snapshots owned by a whole Booking journey in stable
    /// ID order, so recovery eligibility reads every attempt deterministically in one round trip.
    /// </summary>
    /// <param name="bookingIds">The booking ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<BookingAppointment>> ListForBookingsAsync(
        IReadOnlyCollection<Guid> bookingIds,
        CancellationToken cancellationToken);
}
`````

## after — src/EventBooking.Application/Abstractions/IBookingAppointmentRepository.cs — 1/1

<!-- vocabulary-file: {"id":28,"oldPath":"src/EventBooking.Application/Abstractions/IBookingAppointmentRepository.cs","newPath":"src/EventBooking.Application/Abstractions/IBookingAppointmentRepository.cs","beforeSha":"11bbf8506b3f04a2f169271d42a1512f11b292fe7ea4b14c44a969c1dc168f69","afterSha":"de8b323ab1206c661dd806542d9984b5bae679f138f28cade497c0486886c4c3","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Abstractions;

/// <summary>Identifies the lifecycle owners of one appointment without granting authority.</summary>
/// <param name="AttendeeId">The attendee owning the parent Booking.</param>
/// <param name="OriginalBookingId">The journey-root Booking identifier.</param>
/// <param name="BookingId">The parent booking identifier.</param>
/// <param name="EventId">The event selected by that booking.</param>
/// <param name="AppointmentTypeId">The appointment type scoping the lookup.</param>
public sealed record BookingAppointmentLocator(
    Guid AttendeeId,
    Guid OriginalBookingId,
    Guid BookingId,
    Guid EventId,
    Guid AppointmentTypeId);

/// <summary>Persists and locks booking appointments without widening appointment-type scope.</summary>
public interface IBookingAppointmentRepository
{
    /// <summary>Adds one appointment to the current unit of work.</summary>
    /// <param name="appointment">The appointment to track.</param>
    void Add(BookingAppointment appointment);

    /// <summary>Finds immutable parent identifiers only when record and trusted type both match.</summary>
    /// <param name="id">The stable appointment-record identifier.</param>
    /// <param name="appointmentTypeId">The trusted appointment-type scope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The immutable parent identifiers, or null when out of scope.</returns>
    Task<BookingAppointmentLocator?> FindLocatorInScopeAsync(
        Guid id,
        Guid appointmentTypeId,
        CancellationToken cancellationToken);

    /// <summary>Locks and returns one appointment only when record and trusted type both match.</summary>
    /// <param name="id">The stable appointment-record identifier.</param>
    /// <param name="appointmentTypeId">The trusted appointment-type scope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The locked appointment, or null when out of scope.</returns>
    Task<BookingAppointment?> LockForUpdateAsync(
        Guid id,
        Guid appointmentTypeId,
        CancellationToken cancellationToken);

    /// <summary>Locks all appointments for one Booking ordered by stable ID.</summary>
    /// <param name="bookingId">The parent booking identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The locked appointments in stable ID order.</returns>
    Task<IReadOnlyList<BookingAppointment>> LockForBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken);

    /// <summary>Lists the immutable Appointment Type snapshot owned by one Booking.</summary>
    /// <param name="bookingId">The booking id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<BookingAppointment>> ListForBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists the immutable Appointment Type snapshots owned by a whole Booking journey in stable
    /// ID order, so recovery eligibility reads every attempt deterministically in one round trip.
    /// </summary>
    /// <param name="bookingIds">The booking ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<BookingAppointment>> ListForBookingsAsync(
        IReadOnlyCollection<Guid> bookingIds,
        CancellationToken cancellationToken);
}
`````
