# 23 — MCP parity (Task 23)

[← Overview](00-overview.md) · [Ontology](../../ontology.md)

> Hand-authored detailed implementation plan. Code-free downstream task definitions are not permitted here; every task below is TDD-shaped with complete tests and production code fragments.

## Goal

One tool per staff operation, named from the OpenAPI `x-mcp-tool` snake case extension (e.g. propose_event, record_acceptance, list_attendees, cancel_event). Tools call the same handlers under the same `StaffCapability` checks as REST, including the null-scope gate (scoped role with null scope grants nothing) and the Admin data gate (attendee read models refuse Admin-shaped callers). There are no tools for anonymous routes (link index, health, OpenAPI document, Swagger UI) or attendee token routes (booking and manage links). List tools return id plus name-or-code plus status for every row. CSV import over 1000 rows through MCP is refused identically to REST. A Manager with null scope is refused through MCP.

## Architecture

Tools split by area mirroring the endpoint files: reference data and settings, negotiation and events, attendees/invites/bookings, dashboards and audit, appointment workspace. A shared auth pipeline validates the bearer token, synchronises roles, applies the null-scope gate, resolves the `StaffCapability` from the role union, and applies the handler scope filter plus the Admin data gate — identical evaluation order for REST and MCP. A CI test compares the OpenAPI operation set (excluding anonymous and token endpoints) against tools/list and fails on any difference.

## Spec

Task 23 of docs/superpowers/plans/2026-09-19-eventbooking-implementation.md lines 913-932: tools/list equals the OpenAPI operation set minus anonymous and token routes; every list tool row carries id, name-or-code and status; CSV import through MCP over 1000 rows refused identically to REST; null-scope Manager refused through MCP.

## Global constraints

- Every async method contains await; no async without await.
- Backtick PascalCase only for ontology terms: `Attendee`, `AttendeeGroup`, `Event`, `EventProposal`, `Invite`, `Booking`, `StaffAccessProfile`, `StaffId`, `StaffCapability`, `Role`, `AuditLog`. File names, class names, tool names, capability names in prose, and role names stay plain.
- Same handlers and same checks as REST; no duplicate domain logic in tools.
- No tools for anonymous or token routes.

## Review focus

- Tool set exactly equals staff operation set; anonymous and token routes absent.
- Same handler, same capability, same gates; null-scope and Admin data gates verified through MCP.
- List shape uniform: id + name-or-code + status.
- Error slugs identical between REST and MCP including CSV refusal and confirmation/version conflicts.

### Task 23: MCP tool parity with the REST catalogue

**Files:**
- Modify: src/EventBooking.Mcp/Tools/ReferenceDataTools.cs
- Modify: src/EventBooking.Mcp/Tools/NegotiationTools.cs
- Modify: src/EventBooking.Mcp/Tools/AttendeeTools.cs
- Modify: src/EventBooking.Mcp/Tools/DashboardAuditTools.cs
- Modify: src/EventBooking.Mcp/Tools/WorkspaceTools.cs
- Modify: src/EventBooking.Mcp/Tools/ToolCatalog.cs
- Test: tests/EventBooking.Mcp.Tests/ParityTests.cs
- Test: tests/EventBooking.Mcp.Tests/ListShapeTests.cs

**Interfaces:**

Consumes the application handlers and the shared staff auth pipeline; produces one tool method per staff operation delegating to its handler.

```csharp
// ReferenceDataTools delegates to reference-data handlers.
public sealed class ReferenceDataTools
{
    // Lists locations; each row carries id + name-or-code + status.
    public async Task<IReadOnlyList<LocationRow>> list_locationsAsync(bool includeInactive, CancellationToken ct)
    {
        return await _handler.ListAsync(includeInactive, ct);
    }
    // Creates a location.
    public async Task<LocationRow> create_locationAsync(CreateLocationRequest request, CancellationToken ct)
    {
        return await _handler.CreateAsync(request, ct);
    }
    // Updates a location with concurrency check.
    public async Task<LocationRow> update_locationAsync(UpdateLocationRequest request, CancellationToken ct)
    {
        return await _handler.UpdateAsync(request, ct);
    }
    // Lists appointment types with current manager display name.
    public async Task<IReadOnlyList<TypeRow>> list_appointment_typesAsync(bool includeInactive, CancellationToken ct)
    {
        return await _handler.ListAsync(includeInactive, ct);
    }
    // Creates an appointment type.
    public async Task<TypeRow> create_appointment_typeAsync(CreateTypeRequest request, CancellationToken ct)
    {
        return await _handler.CreateAsync(request, ct);
    }
    // Updates an appointment type with concurrency check.
    public async Task<TypeRow> update_appointment_typeAsync(UpdateTypeRequest request, CancellationToken ct)
    {
        return await _handler.UpdateAsync(request, ct);
    }
    // Lists attendee groups with requirement ids and member count.
    public async Task<IReadOnlyList<GroupRow>> list_attendee_groupsAsync(bool includeInactive, CancellationToken ct)
    {
        return await _handler.ListAsync(includeInactive, ct);
    }
    // Creates an attendee group.
    public async Task<GroupRow> create_attendee_groupAsync(CreateGroupRequest request, CancellationToken ct)
    {
        return await _handler.CreateAsync(request, ct);
    }
    // Updates an attendee group with concurrency check.
    public async Task<GroupRow> update_attendee_groupAsync(UpdateGroupRequest request, CancellationToken ct)
    {
        return await _handler.UpdateAsync(request, ct);
    }
    // Reads settings.
    public async Task<SettingsRow> get_settingsAsync(CancellationToken ct)
    {
        return await _handler.GetAsync(ct);
    }
    // Updates settings with concurrency check.
    public async Task<SettingsRow> update_settingsAsync(UpdateSettingsRequest request, CancellationToken ct)
    {
        return await _handler.UpdateAsync(request, ct);
    }
    // Lists staff access profiles.
    public async Task<IReadOnlyList<AccessRow>> list_staff_accessAsync(CancellationToken ct)
    {
        return await _handler.ListAsync(ct);
    }
    // Updates a scoped assignment; response names any displaced Manager.
    public async Task<ScopeRow> update_staff_scopeAsync(UpdateScopeRequest request, CancellationToken ct)
    {
        return await _handler.UpdateScopeAsync(request, ct);
    }
}
```

```csharp
// NegotiationTools delegates to negotiation and event handlers.
public sealed class NegotiationTools
{
    // Lists the caller type proposals.
    public async Task<IReadOnlyList<ProposalRow>> list_event_proposalsAsync(string? status, string? locationId, CancellationToken ct)
    {
        return await _handler.ListAsync(status, locationId, ct);
    }
    // Proposes an event; returns Open or Confirmed with event id when confirmed.
    public async Task<ProposalRow> propose_eventAsync(ProposeEventRequest request, CancellationToken ct)
    {
        return await _handler.ProposeAsync(request, ct);
    }
    // Records or revises the caller type acceptance.
    public async Task<ProposalRow> record_acceptanceAsync(RecordAcceptanceRequest request, CancellationToken ct)
    {
        return await _handler.RecordAsync(request, ct);
    }
    // Withdraws the caller type acceptance.
    public async Task<ProposalRow> withdraw_acceptanceAsync(WithdrawAcceptanceRequest request, CancellationToken ct)
    {
        return await _handler.WithdrawAcceptanceAsync(request, ct);
    }
    // Withdraws the proposal for the proposer type only.
    public async Task<ProposalRow> withdraw_proposalAsync(WithdrawProposalRequest request, CancellationToken ct)
    {
        return await _handler.WithdrawAsync(request, ct);
    }
    // Lists events with scope filter applied inside the handler.
    public async Task<IReadOnlyList<EventRow>> list_eventsAsync(ListEventsQuery query, CancellationToken ct)
    {
        return await _handler.ListAsync(query, ct);
    }
    // Gets one event with capacities.
    public async Task<EventRow> get_eventAsync(string id, CancellationToken ct)
    {
        return await _handler.GetAsync(id, ct);
    }
    // Adjusts the caller own type headcount.
    public async Task<EventRow> adjust_capacityAsync(AdjustCapacityRequest request, CancellationToken ct)
    {
        return await _handler.AdjustAsync(request, ct);
    }
    // Cancels an event via the two-step flow.
    public async Task<CancelRow> cancel_eventAsync(CancelEventRequest request, bool confirm, CancellationToken ct)
    {
        return await _handler.CancelAsync(request, confirm, ct);
    }
    // Lists cancellable events in the operations window.
    public async Task<IReadOnlyList<EventRow>> list_cancellable_eventsAsync(ListCancellableQuery query, CancellationToken ct)
    {
        return await _handler.ListCancellableAsync(query, ct);
    }
}
```

```csharp
// AttendeeTools delegates to attendee, invite, booking and recovery handlers.
public sealed class AttendeeTools
{
    // Cursor lists attendees with readiness and delivery.
    public async Task<Page<AttendeeRow>> list_attendeesAsync(ListAttendeesQuery query, CancellationToken ct)
    {
        return await _handler.ListAsync(query, ct);
    }
    // Creates an attendee.
    public async Task<AttendeeRow> create_attendeeAsync(CreateAttendeeRequest request, CancellationToken ct)
    {
        return await _handler.CreateAsync(request, ct);
    }
    // Edits an attendee.
    public async Task<AttendeeRow> update_attendeeAsync(UpdateAttendeeRequest request, CancellationToken ct)
    {
        return await _handler.UpdateAsync(request, ct);
    }
    // Deletes an attendee via the two-step flow.
    public async Task<DeleteRow> delete_attendeeAsync(string id, bool confirm, CancellationToken ct)
    {
        return await _handler.DeleteAsync(id, confirm, ct);
    }
    // Imports a CSV all-or-nothing; over 1000 rows refused identically to REST.
    public async Task<ImportRow> import_attendeesAsync(byte[] csv, CancellationToken ct)
    {
        return await _handler.ImportAsync(csv, ct);
    }
    // Live eligible event count for the invite dialog.
    public async Task<CountRow> get_eligible_event_countAsync(string id, string[] locationIds, CancellationToken ct)
    {
        return await _handler.CountAsync(id, locationIds, ct);
    }
    // Invites an attendee across locations.
    public async Task<InviteRow> invite_attendeeAsync(InviteRequest request, CancellationToken ct)
    {
        return await _handler.InviteAsync(request, ct);
    }
    // Starts recovery with additional locations.
    public async Task<InviteRow> start_recovery_inviteAsync(RecoveryRequest request, CancellationToken ct)
    {
        return await _handler.StartRecoveryAsync(request, ct);
    }
    // Cancels a pending recovery invite.
    public async Task<InviteRow> cancel_recovery_inviteAsync(string id, string inviteId, CancellationToken ct)
    {
        return await _handler.CancelRecoveryAsync(id, inviteId, ct);
    }
    // Lists bookings with event and location.
    public async Task<IReadOnlyList<BookingRow>> list_bookingsAsync(string id, CancellationToken ct)
    {
        return await _handler.ListBookingsAsync(id, ct);
    }
    // Cancels a booking via the two-step coordinator flow.
    public async Task<CancelBookingRow> cancel_bookingAsync(string id, string bookingId, bool confirm, CancellationToken ct)
    {
        return await _handler.CancelBookingAsync(id, bookingId, confirm, ct);
    }
    // Retries the newest failed or stale email.
    public async Task<RetryRow> retry_emailAsync(string id, CancellationToken ct)
    {
        return await _handler.RetryAsync(id, ct);
    }
    // Returns attendee readiness.
    public async Task<ReadinessRow> get_readinessAsync(string id, CancellationToken ct)
    {
        return await _handler.GetReadinessAsync(id, ct);
    }
}
```

```csharp
// DashboardAuditTools delegates to dashboard and audit handlers.
public sealed class DashboardAuditTools
{
    // Returns all dashboard tabs and counts.
    public async Task<DashboardRow> get_dashboardsAsync(string? locationId, CancellationToken ct)
    {
        return await _handler.GetAsync(locationId, ct);
    }
    // Searches the audit log scoped by bucket.
    public async Task<Page<AuditRow>> search_auditAsync(AuditQuery query, CancellationToken ct)
    {
        return await _handler.SearchAsync(query, ct);
    }
    // Returns attendee history.
    public async Task<IReadOnlyList<AuditRow>> get_attendee_historyAsync(string id, CancellationToken ct)
    {
        return await _handler.AttendeeHistoryAsync(id, ct);
    }
    // Returns event history including its proposal.
    public async Task<IReadOnlyList<AuditRow>> get_event_historyAsync(string id, CancellationToken ct)
    {
        return await _handler.EventHistoryAsync(id, ct);
    }
}
```

```csharp
// WorkspaceTools delegates to appointment workspace handlers.
public sealed class WorkspaceTools
{
    // Selector listing the caller type events in the 7-day-back 14-day-ahead window.
    public async Task<IReadOnlyList<EventRow>> list_workspace_eventsAsync(string? locationId, CancellationToken ct)
    {
        return await _handler.ListAsync(locationId, ct);
    }
    // Minimum-data roster for one event.
    public async Task<RosterRow> get_workspace_rosterAsync(string eventId, CancellationToken ct)
    {
        return await _handler.GetRosterAsync(eventId, ct);
    }
    // Updates an appointment status with concurrency check.
    public async Task<AppointmentRow> update_appointment_statusAsync(UpdateStatusRequest request, CancellationToken ct)
    {
        return await _handler.UpdateStatusAsync(request, ct);
    }
    // Downloads the roster CSV with formula-cell neutralisation.
    public async Task<byte[]> download_roster_csvAsync(string eventId, CancellationToken ct)
    {
        return await _handler.DownloadCsvAsync(eventId, ct);
    }
}
```

```csharp
// ToolCatalog exposes the full staff-operation tool set for tools/list comparison.
public sealed class ToolCatalog
{
    // Returns every registered tool name in snake case.
    public async Task<IReadOnlyList<string>> list_tool_namesAsync(CancellationToken ct)
    {
        return await _registry.ListNamesAsync(ct);
    }
    // Compares tools/list against the OpenAPI operation set minus anonymous and token routes.
    public async Task<CatalogDiff> diff_against_openapiAsync(OpenApiDocument openapi, CancellationToken ct)
    {
        return await _comparer.DiffAsync(await _registry.ListNamesAsync(ct), openapi, ct);
    }
}
```

- [ ] **Step 1: Write the failing tests** — complete tests/EventBooking.Mcp.Tests/ParityTests.cs (tools/list equals the OpenAPI operation set minus anonymous and token routes; null-scope Manager refused through MCP; CSV import over 1000 rows refused identically to REST; Admin attendee-data gate holds through MCP) and tests/EventBooking.Mcp.Tests/ListShapeTests.cs (every list tool row has id, name-or-code and status) using the real test framework and fixtures.
- [ ] **Step 2: Run the tests and watch them fail (red).**
- [ ] **Step 3: Implement the production tool code** — the ReferenceDataTools, NegotiationTools, AttendeeTools, DashboardAuditTools, WorkspaceTools and ToolCatalog classes above as real delegating methods with await, sharing the auth pipeline and handler scope filters.
- [ ] **Step 4: Run the tests and watch them pass.** Expectation only: ParityTests count equals the staff-operation count from the OpenAPI document; ListShapeTests covers every list tool; CSV and null-scope refusal tests pass.
- [ ] **Step 5: Commit and push** with explicit paths:
```
git add -A src/EventBooking.Mcp/Tools/ReferenceDataTools.cs src/EventBooking.Mcp/Tools/NegotiationTools.cs src/EventBooking.Mcp/Tools/AttendeeTools.cs src/EventBooking.Mcp/Tools/DashboardAuditTools.cs src/EventBooking.Mcp/Tools/WorkspaceTools.cs src/EventBooking.Mcp/Tools/ToolCatalog.cs tests/EventBooking.Mcp.Tests/ParityTests.cs tests/EventBooking.Mcp.Tests/ListShapeTests.cs docs/detailed-implementations/phase-4c-mcp-parity.md
git commit -m "feat(mcp): tool parity with the REST catalogue"
git push
```
- [ ] **Step 6: Pull-request gate (do NOT run it, just document exact commands).** Open the Phase 4 pull request with the narrative-required label plus the three Narrative headings plus the AI-Fingerprint footer; wait for head checks before updating the body:
```
gh pr create --label narrative-required --title "..." --body "..."
MERGE_BASE=$(git merge-base origin/main HEAD)
git diff "$MERGE_BASE" HEAD | sha256sum | cut -c1-12
gh pr edit <number> --body "... AI-Fingerprint: sha256:<12 hex> ..."
```
Note: wait-for-head before body update — recompute the fingerprint after any push that changes HEAD.
