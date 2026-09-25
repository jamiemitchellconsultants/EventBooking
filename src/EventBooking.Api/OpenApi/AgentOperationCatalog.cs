using EventBooking.Application.Access;

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
/// <param name="Capability">The design 05 capability, or null for open and filtered reads.</param>
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
    /// <summary>Gets the design 05 capability, or null for open and filtered reads.</summary>
    string? Capability,
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
            // Discovery, health and the document itself. None is a business capability, so none
            // has a tool: an agent discovers this surface through tools/list, not through /api.
            Excluded("getApiIndex", HttpMethods.Get, "/api", "Discovery",
                "Read the API entry document.",
                "Anonymous entry document listing every top-level resource.",
                "Transport discovery, not a business capability. MCP has tools/list."),
            Excluded("getOpenApiDocument", HttpMethods.Get, "/openapi/v1.json", "Discovery",
                "Read the OpenAPI document.", "Anonymous machine-readable API contract.",
                "Transport discovery, not a business capability."),
            Excluded("getSwaggerUi", HttpMethods.Get, "/swagger", "Discovery",
                "Open the Swagger UI.", "Anonymous human documentation UI.",
                "Human documentation UI, not a business capability."),
            Excluded("getLiveness", HttpMethods.Get, "/health/live", "Health",
                "Read the liveness probe.", "Anonymous deployment liveness probe.",
                "Deployment probe, not a staff workflow."),
            Excluded("getReadiness", HttpMethods.Get, "/health/ready", "Health",
                "Read the readiness probe.",
                "Anonymous readiness probe; reports whether the database is reachable.",
                "Deployment probe, not a staff workflow."),
            Excluded("getMetrics", HttpMethods.Get, "/metrics", "Health",
                "Scrape the metrics endpoint.",
                "Anonymous Prometheus exposition of the API's meters (design 08).",
                "Operational scrape endpoint, not a staff workflow."),

            // Identity. A staff token with no valid staff number still reaches this one, which is
            // the whole of contradiction #3's exception.
            Staff("getMyAccess", HttpMethods.Get, "/api/me", "Identity",
                "Read the signed-in staff access.",
                "Reads display name, staff number, roles, scope and granted capabilities (FR-10.8). " +
                "Requires staff auth; no capability, and answers even when the staff " +
                "number claim is missing or malformed.",
                null, "get_my_access", Read()),

            // Reference data. Reads are open to any staff member; writes are Admin-only.
            Staff("listLocations", HttpMethods.Get, "/api/locations", "Reference Data",
                "List locations.",
                "Reads every location in code order. Requires staff auth.",
                null, "list_locations", Read()),
            Staff("createLocation", HttpMethods.Post, "/api/locations", "Reference Data",
                "Create a location.",
                "Creates a location with its code, name, address and IANA zone.",
                nameof(StaffCapability.ManageReferenceData), "create_location", Create()),
            Staff("updateLocation", HttpMethods.Put, "/api/locations/{id}", "Reference Data",
                "Update a location.",
                "Updates a location's name, address, zone and active flag. Refuses a zone change " +
                "or a deactivation while the location has open proposals or future events.",
                nameof(StaffCapability.ManageReferenceData), "update_location", Transition()),
            Staff("listAppointmentTypes", HttpMethods.Get, "/api/appointment-types", "Reference Data",
                "List appointment types.",
                "Reads every appointment type with its current Manager's display name.",
                null, "list_appointment_types", Read()),
            Staff("createAppointmentType", HttpMethods.Post, "/api/appointment-types", "Reference Data",
                "Create an appointment type.", "Creates an appointment type with its code and name.",
                nameof(StaffCapability.ManageReferenceData), "create_appointment_type", Create()),
            Staff("updateAppointmentType", HttpMethods.Put, "/api/appointment-types/{id}",
                "Reference Data", "Update an appointment type.",
                "Updates an appointment type's name and active flag. Refuses a deactivation while " +
                "it is listed on an open proposal or a future event, or mapped by an active group.",
                nameof(StaffCapability.ManageReferenceData), "update_appointment_type", Transition()),
            Staff("listAttendeeGroups", HttpMethods.Get, "/api/attendee-groups", "Reference Data",
                "List attendee groups.",
                "Reads every attendee group with its requirement type ids and member count.",
                null, "list_attendee_groups", Read()),
            Staff("createAttendeeGroup", HttpMethods.Post, "/api/attendee-groups", "Reference Data",
                "Create an attendee group.",
                "Creates an attendee group mapping at least one active appointment type.",
                nameof(StaffCapability.ManageReferenceData), "create_attendee_group", Create()),
            Staff("updateAttendeeGroup", HttpMethods.Put, "/api/attendee-groups/{id}",
                "Reference Data", "Update an attendee group.",
                "Updates a group's name, requirement mapping and active flag. A mapping change " +
                "re-derives every member's requirements and is refused while any member holds an " +
                "active original booking (FR-1.5).",
                nameof(StaffCapability.ManageReferenceData), "update_attendee_group", Transition()),
            Staff("getSettings", HttpMethods.Get, "/api/settings", "Settings",
                "Read the system settings.", "Reads the single settings row and its version.",
                nameof(StaffCapability.ManageSettings), "get_settings", Read()),
            Staff("updateSettings", HttpMethods.Put, "/api/settings", "Settings",
                "Update the system settings.",
                "Updates invite expiry, automatic retry count, option count and self-registration " +
                "expiry. Existing invites keep the values they were issued under.",
                nameof(StaffCapability.ManageSettings), "update_settings", Transition()),

            // Staff access. There is deliberately no create, delete or role edit (FR-10.5).
            Staff("listStaffAccess", HttpMethods.Get, "/api/staff-access", "Staff Access",
                "List staff access profiles.",
                "Reads every profile with its display name, staff number, read-only roles and scope.",
                nameof(StaffCapability.ManageStaffAccess), "list_staff_access", Read()),
            Staff("setStaffAccessScope", HttpMethods.Put, "/api/staff-access/{staffUserId}/scope",
                "Staff Access", "Set a staff access scope.",
                "Sets or clears one profile's appointment-type scope. Assigning a type that " +
                "another Manager holds displaces them, and the response names who.",
                nameof(StaffCapability.ManageStaffAccess), "set_staff_access_scope", Transition()),

            // Negotiation.
            Staff("listEventProposals", HttpMethods.Get, "/api/event-proposals", "Negotiation",
                "List the caller's type's proposals.",
                "Reads the proposals listing the caller's own appointment type (FR-2.13), " +
                "filtered by status and location, one keyset page at a time.",
                nameof(StaffCapability.ManageEventNegotiation), "list_event_proposals", Read()),
            Staff("proposeEvent", HttpMethods.Post, "/api/event-proposals", "Negotiation",
                "Propose an event.",
                "Creates a proposal for one location, window and listed appointment-type set. " +
                "A proposal listing only the proposer's type is confirmed on creation.",
                nameof(StaffCapability.ManageEventNegotiation), "propose_event", Create()),
            Staff("recordAcceptance", HttpMethods.Put, "/api/event-proposals/{id}/acceptance",
                "Negotiation", "Record or revise an acceptance.",
                "Records the caller's type's acceptance with its headcount, or revises it while " +
                "the proposal is open. The last missing acceptance confirms the event.",
                nameof(StaffCapability.ManageEventNegotiation), "record_acceptance", Transition()),
            Staff("withdrawAcceptance", HttpMethods.Delete, "/api/event-proposals/{id}/acceptance",
                "Negotiation", "Withdraw an acceptance.",
                "Withdraws the caller's type's acceptance while the proposal is open.",
                nameof(StaffCapability.ManageEventNegotiation), "withdraw_acceptance", Delete()),
            Staff("withdrawProposal", HttpMethods.Post, "/api/event-proposals/{id}/withdraw",
                "Negotiation", "Withdraw a proposal.",
                "Withdraws the whole proposal. Judged against the proposing appointment type, so " +
                "a Manager inherits it from a predecessor.",
                nameof(StaffCapability.ManageEventNegotiation), "withdraw_proposal", Delete()),

            // Events. The two reads carry no capability: design 05 gives them two, and the
            // handler filters by whichever the caller holds (settlement #12).
            Staff("listEvents", HttpMethods.Get, "/api/events", "Events", "List events.",
                "Reads events filtered by location, date range and appointment type. A Manager " +
                "sees their own type's capacity; an Admin or Coordinator sees every type.",
                null, "list_events", Read()),
            Staff("getEvent", HttpMethods.Get, "/api/events/{id}", "Events", "Read one event.",
                "Reads one event with its capacities, filtered the same way the list is. An event " +
                "outside the caller's scope reads as not found.",
                null, "get_event", Read()),
            Staff("adjustEventCapacity", HttpMethods.Put,
                "/api/events/{id}/capacities/{appointmentTypeId}", "Events",
                "Adjust an event capacity.",
                "Replaces the total headcount for one appointment type on an active event. The " +
                "type must be the caller's own, and the total must still cover every active booking.",
                nameof(StaffCapability.ManageEventNegotiation), "adjust_event_capacity", Transition()),
            Staff("cancelEvent", HttpMethods.Post, "/api/events/{id}/cancel", "Events",
                "Cancel an event.",
                "Cancels an event, voiding its bookings and re-inviting the affected attendees. " +
                "Two-step: without confirm=true the call reports its consequence and changes nothing.",
                nameof(StaffCapability.CancelEvent), "cancel_event", Delete()),

            Staff("listCancellableEvents", HttpMethods.Get, "/api/events/cancellable", "Events",
                "List cancellable events.",
                "Reads the active events whose window has not started, which are the ones a " +
                "cancellation can still reach (FR-7.2).",
                nameof(StaffCapability.ViewEventOperations), "list_cancellable_events", Read()),

            // Event groups.
            Staff("listEventGroups", HttpMethods.Get, "/api/event-groups", "Event groups",
                "List event groups.",
                "Reads every event group with its selected attendee groups and memberships.",
                nameof(StaffCapability.ManageEventGroups), "list_event_groups", Read()),
            Staff("getEventGroup", HttpMethods.Get, "/api/event-groups/{id}", "Event groups",
                "Read one event group.",
                "Reads one event group with its selected attendee groups and memberships.",
                nameof(StaffCapability.ManageEventGroups), "get_event_group", Read()),
            Staff("createEventGroup", HttpMethods.Post, "/api/event-groups", "Event groups",
                "Create an event group.",
                "Creates a closed event group serving the selected active attendee groups.",
                nameof(StaffCapability.ManageEventGroups), "create_event_group", Create()),
            Staff("updateEventGroup", HttpMethods.Put, "/api/event-groups/{id}", "Event groups",
                "Update an event group.",
                "Updates an event group's copy, selected groups and group gate. Replacing the " +
                "selected groups is refused when the new union would break a member event.",
                nameof(StaffCapability.ManageEventGroups), "update_event_group", Transition()),
            Staff("addEventGroupEvent", HttpMethods.Put,
                "/api/event-groups/{id}/events/{eventId}", "Event groups",
                "Add an event to a group.",
                "Adds an active future event whose capacity types equal the group's set. " +
                "New memberships start private.",
                nameof(StaffCapability.ManageEventGroups), "add_event_group_event", Transition()),
            Staff("setEventGroupEventOpen", HttpMethods.Patch,
                "/api/event-groups/{id}/events/{eventId}", "Event groups",
                "Open or close a membership.",
                "Toggles one event membership's public-registration gate; the group gate is " +
                "independent.",
                nameof(StaffCapability.ManageEventGroups), "set_event_group_event_open", Transition()),
            Staff("removeEventGroupEvent", HttpMethods.Delete,
                "/api/event-groups/{id}/events/{eventId}", "Event groups",
                "Remove an event from a group.",
                "Removes one event membership; existing bookings stay valid.",
                nameof(StaffCapability.ManageEventGroups), "remove_event_group_event", Delete()),

            // Attendees.
            Staff("listAttendees", HttpMethods.Get, "/api/attendees", "Attendees", "List attendees.",
                "Reads attendees filtered by status, group, readiness and a name or email prefix, " +
                "one keyset page at a time, with each row's latest delivery status.",
                nameof(StaffCapability.ManageAttendees), "list_attendees", Read()),
            Staff("createAttendee", HttpMethods.Post, "/api/attendees", "Attendees",
                "Create an attendee.",
                "Creates an attendee in a group, deriving their requirements from it.",
                nameof(StaffCapability.ManageAttendees), "create_attendee", Create()),
            Staff("updateAttendee", HttpMethods.Put, "/api/attendees/{id}", "Attendees",
                "Update an attendee.",
                "Updates an attendee's name, email and group (FR-4.2). A group change that would " +
                "alter an active booking's requirements is refused.",
                nameof(StaffCapability.ManageAttendees), "update_attendee", Transition()),
            Staff("deleteAttendee", HttpMethods.Delete, "/api/attendees/{id}", "Attendees",
                "Delete an attendee.",
                "Deletes an attendee and their bookings (FR-4.5). Two-step: without confirm=true " +
                "the call reports its consequence and changes nothing.",
                nameof(StaffCapability.ManageAttendees), "delete_attendee", Delete()),
            Staff("importAttendees", HttpMethods.Post, "/api/attendees/import", "Attendees",
                "Import attendees from CSV.",
                "Imports attendees from CSV content, all or nothing (FR-4.3). At most 1000 rows and 1 MB.",
                nameof(StaffCapability.ManageAttendees), "import_attendees", Create()),
            Staff("countEligibleEvents", HttpMethods.Get,
                "/api/attendees/{id}/eligible-event-count", "Attendees",
                "Count an attendee's eligible events.",
                "Counts the events this attendee could currently be offered at the given locations.",
                nameof(StaffCapability.ManageAttendees), "count_eligible_events", Read()),
            Staff("inviteAttendee", HttpMethods.Post, "/api/attendees/{id}/invites", "Attendees",
                "Invite an attendee.",
                "Issues an invitation restricted to the chosen locations, returning Invited or " +
                "AwaitingAvailability when too few events are eligible.",
                nameof(StaffCapability.ManageAttendees), "invite_attendee", Create()),
            Staff("startRecoveryInvite", HttpMethods.Post,
                "/api/attendees/{id}/recovery-invites", "Attendees", "Start a recovery invite.",
                "Starts missed-appointment recovery for the attendee's outstanding no-show types " +
                "(FR-9.1), optionally widening the locations.",
                nameof(StaffCapability.ManageAttendees), "start_recovery_invite", Create()),
            Staff("cancelRecoveryInvite", HttpMethods.Delete,
                "/api/attendees/{id}/recovery-invites/{inviteId}", "Attendees",
                "Cancel a recovery invite.", "Cancels one pending recovery invitation.",
                nameof(StaffCapability.ManageAttendees), "cancel_recovery_invite", Delete()),
            Staff("listAttendeeBookings", HttpMethods.Get, "/api/attendees/{id}/bookings",
                "Attendees", "List an attendee's bookings.",
                "Reads the attendee's bookings with their event and location.",
                nameof(StaffCapability.ManageAttendees), "list_attendee_bookings", Read()),
            Staff("cancelAttendeeBooking", HttpMethods.Post,
                "/api/attendees/{id}/bookings/{bookingId}/cancel", "Attendees",
                "Cancel an attendee's booking.",
                "Cancels one booking on the attendee's behalf (FR-7.1). Two-step: without " +
                "confirm=true the call reports its consequence and changes nothing.",
                nameof(StaffCapability.ManageAttendees), "cancel_attendee_booking", Delete()),
            Staff("retryAttendeeEmail", HttpMethods.Post, "/api/attendees/{id}/email-retry",
                "Attendees", "Retry an attendee email.",
                "Regenerates and re-queues the newest failed or stale delivery (FR-11.3).",
                nameof(StaffCapability.ManageAttendees), "retry_attendee_email", Create()),
            Staff("getAttendeeReadiness", HttpMethods.Get, "/api/attendees/{id}/readiness",
                "Attendees", "Read an attendee's readiness.",
                "Reads the calculated readiness and any outstanding appointment types.",
                nameof(StaffCapability.ViewAttendeeDashboards), "get_attendee_readiness", Read()),

            // Dashboards and audit. The search carries no capability for the same reason the
            // event reads do not: Task 20b's handler filters by the buckets the caller holds.
            Staff("getDashboards", HttpMethods.Get, "/api/dashboards", "Dashboards",
                "Read the dashboards.",
                "Reads the three tabs and their counts (FR-13), optionally filtered by location.",
                nameof(StaffCapability.ViewAttendeeDashboards), "get_dashboards", Read()),
            Staff("searchAudit", HttpMethods.Get, "/api/audit", "Audit", "Search the audit log.",
                "Searches audit entries, scoped to the buckets the caller's capabilities reach " +
                "(FR-12.3), one keyset page at a time.",
                null, "search_audit", Read()),
            Staff("getAttendeeAuditHistory", HttpMethods.Get, "/api/audit/attendees/{id}", "Audit",
                "Read an attendee's history.", "Reads the audit history of one attendee.",
                nameof(StaffCapability.ViewAttendeeAudit), "get_attendee_audit_history", Read()),
            Staff("getEventAuditHistory", HttpMethods.Get, "/api/audit/events/{id}", "Audit",
                "Read an event's history.",
                "Reads the audit history of one event, including its proposal.",
                nameof(StaffCapability.ViewEventAudit), "get_event_audit_history", Read()),

            // Appointment workspace.
            Staff("listWorkspaceEvents", HttpMethods.Get, "/api/appointment-workspace/events",
                "Appointment Workspace", "List workspace events.",
                "Reads the events listing the caller's type and ending between seven days ago and " +
                "fourteen days ahead.",
                nameof(StaffCapability.ConductAppointments), "list_workspace_events", Read()),
            Staff("getWorkspaceRoster", HttpMethods.Get,
                "/api/appointment-workspace/events/{eventId}", "Appointment Workspace",
                "Read a workspace roster.",
                "Reads the minimum-data roster for one event (FR-8.1): name, email, the caller's " +
                "own type's appointment status, and nothing else.",
                nameof(StaffCapability.ConductAppointments), "get_workspace_roster", Read()),
            Staff("setAppointmentStatus", HttpMethods.Put,
                "/api/appointment-workspace/appointments/{id}/status", "Appointment Workspace",
                "Set an appointment status.",
                "Moves one booking appointment to a target status, or applies a bounded correction. " +
                "A same-status submission is idempotent.",
                nameof(StaffCapability.ConductAppointments), "set_appointment_status", Transition()),
            Staff("exportWorkspaceRoster", HttpMethods.Get,
                "/api/appointment-workspace/events/{eventId}/roster.csv", "Appointment Workspace",
                "Export a workspace roster.",
                "Downloads the roster as CSV (FR-8.7, FR-8.8), with formula characters neutralised.",
                nameof(StaffCapability.ConductAppointments), "export_workspace_roster", Read()),

            // Attendee token routes. Anonymous, rate-limited and REST-only by the approved remote
            // MCP design: an agent holding a staff token must not be able to act as an attendee.
            Excluded("viewInvite", HttpMethods.Get, "/api/booking/{token}", "Attendee Booking",
                "View an invitation.",
                "Anonymous. Shows the live options, topped up (FR-5.8), or the expired state.",
                "Anonymous attendee token flow; excluded by the approved remote MCP design."),
            Excluded("confirmBooking", HttpMethods.Post, "/api/booking/{token}/confirm",
                "Attendee Booking", "Confirm a booking.",
                "Anonymous. Confirms one offered event and returns the booking and its manage link.",
                "Anonymous attendee token flow; excluded by the approved remote MCP design."),
            Excluded("viewManagedBooking", HttpMethods.Get, "/api/manage/{token}",
                "Attendee Booking", "View a booking.",
                "Anonymous. Shows the booking and whether it can still be cancelled.",
                "Anonymous attendee token flow; excluded by the approved remote MCP design."),
            Excluded("cancelManagedBooking", HttpMethods.Post, "/api/manage/{token}/cancel",
                "Attendee Booking", "Cancel a booking.",
                "Anonymous. Cancels the booking and optionally asks for a new time.",
                "Anonymous attendee token flow; excluded by the approved remote MCP design."),

            // Public event groups. Anonymous, rate-limited and REST-only like the attendee
            // token routes above: an agent holding a staff token must not act as a registrant.
            Excluded("listPublicEventGroups", HttpMethods.Get, "/api/public/event-groups",
                "Public Event Groups", "List open event groups.",
                "Anonymous. Lists every open group with its public choices.",
                "Anonymous public flow; excluded by the approved remote MCP design."),
            Excluded("getPublicEventGroup", HttpMethods.Get, "/api/public/event-groups/{id}",
                "Public Event Groups", "Read one open event group.",
                "Anonymous. Reads one open group with its public choices.",
                "Anonymous public flow; excluded by the approved remote MCP design."),
            Excluded("submitSelfRegistration", HttpMethods.Post,
                "/api/public/event-groups/{id}/registrations", "Public Event Groups",
                "Submit a registration request.",
                "Anonymous. Submits a request to join an event and returns its confirmation token.",
                "Anonymous public flow; excluded by the approved remote MCP design."),
            Excluded("submitEventRegistration", HttpMethods.Post,
                "/api/public/event-groups/{id}/events/{eventId}/registrations",
                "Public Event Groups", "Submit a registration request for one event.",
                "Anonymous. Submits a request to join one event and returns its confirmation token.",
                "Anonymous public flow; excluded by the approved remote MCP design."),
            Excluded("viewSelfRegistration", HttpMethods.Get,
                "/api/public/event-groups/confirm/{token}", "Public Event Groups",
                "View a pending registration request.",
                "Anonymous. Shows one pending request's public summary before confirmation.",
                "Anonymous public flow; excluded by the approved remote MCP design."),
            Excluded("confirmSelfRegistration", HttpMethods.Post,
                "/api/public/event-groups/confirm/{token}", "Public Event Groups",
                "Confirm a registration request.",
                "Anonymous. Confirms one pending request and returns the created booking.",
                "Anonymous public flow; excluded by the approved remote MCP design."),
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
        string operationId, string method, string route, string tag, string summary,
        string description, string? capability, string mcpTool, AgentHints hints) =>
        new(operationId, method, route, tag, summary, description, true, capability, mcpTool, null, hints);

    private static AgentOperation Excluded(
        string operationId, string method, string route, string tag, string summary,
        string description, string reason) =>
        new(operationId, method, route, tag, summary, description, false, null, null, reason,
            new AgentHints(true, false, true, false));

    private static AgentHints Read() => new(true, false, true, false);

    private static AgentHints Create() => new(false, false, false, false);

    private static AgentHints Transition() => new(false, true, true, false);

    private static AgentHints Delete() => new(false, true, true, false);
}
