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
