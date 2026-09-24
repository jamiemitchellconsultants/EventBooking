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
            Staff("listAttendees", HttpMethods.Get, "/api/attendees", "Attendees", "List attendees.", "Reads the attendee collection. Requires a staff bearer token.", "list_attendees", Read()),
            Staff("createAttendee", HttpMethods.Post, "/api/attendees", "Attendees", "Create a attendee.", "Creates a attendee. Requires a staff bearer token.", "create_attendee", Create()),
            Staff("updateAttendee", HttpMethods.Put, "/api/attendees/{id}", "Attendees", "Update a attendee.", "Updates an existing attendee. Requires a staff bearer token.", "update_attendee", Transition()),
            Staff("deleteAttendee", HttpMethods.Delete, "/api/attendees/{id}", "Attendees", "Delete a attendee.", "Deletes a attendee. Requires a staff bearer token.", "delete_attendee", Delete()),
            Staff("importAttendees", HttpMethods.Post, "/api/attendees/import", "Attendees", "Import attendees.", "Imports attendees from CSV. Requires a staff bearer token.", "import_attendees", Create()),
            Staff("listInviteLocations", HttpMethods.Get, "/api/attendees/invite-locations", "Attendees", "List invite locations.", "Reads the active locations an invite can offer events at. Requires a staff bearer token.", "list_invite_locations", Read()),
            Staff("triggerAttendeeInvite", HttpMethods.Post, "/api/attendees/{id}/invite", "Attendees", "Trigger a attendee invite.", "Sends a booking invite to a attendee, at every active location unless locations are given. Requires a staff bearer token.", "trigger_invite", Create()),
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
            Excluded("getLiveness", HttpMethods.Get, "/health/live", "Health", "Read the liveness probe.", "Anonymous deployment liveness probe.", "Deployment probe, not a staff workflow."),
            Excluded("getReadiness", HttpMethods.Get, "/health/ready", "Health", "Read the readiness probe.", "Anonymous deployment readiness probe reporting database reachability.", "Deployment probe, not a staff workflow."),
            Excluded("getMetrics", HttpMethods.Get, "/metrics", "Health", "Scrape Prometheus metrics.", "Anonymous Prometheus exposition of request and business instruments.", "Deployment telemetry, not a staff workflow."),
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
