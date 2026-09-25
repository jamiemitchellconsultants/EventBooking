# 05 — API Design

[← Solution architecture](04-solution-architecture.md) · [Security and authentication →](06-security-and-authentication.md)

The API is REST with JSON under `/api`. The machine contract is OpenAPI 3.1 at
`GET /openapi/v1.json`, with Swagger UI at `/swagger`. Each endpoint maps to exactly one
Application handler. The MCP server exposes the same handlers as tools (FR-14).

## Conventions

- **Naming.** Resource names are kebab-case plurals of ontology terms, and JSON fields are
  camelCase ontology property names. Enum values serialise as their ontology names (`Open`,
  `NoShow`, and so on).
- **Pagination.** Every list endpoint takes `?cursor=<opaque>&limit=<1–200, default 50>` and returns
  `{ "items": [...], "nextCursor": "<opaque>|null" }`. Cursors are opaque keyset tokens: a
  base64url-encoded, HMAC-protected sort key. There is no offset pagination anywhere.
- **Concurrency.** A write to a resource with a `version` carries `expectedVersion` in the body. A
  mismatch returns `409` with `type: version-conflict`, and `current` holds the server state.
- **Two-step destructive actions.** Without `?confirm=true` the request returns `409` with
  `type: confirmation-required` and a `consequence` object, for example `{ "activeBookings": 3 }`.
  With it, the action proceeds.
- **Errors.** Errors are RFC 9457 `application/problem+json` of the form
  `{ type, title, status, detail, errors?: [{ field?, line?, code, message }], current? }`. `type`
  is a stable slug from the [error catalogue](#error-catalogue). An expected failure is never a 500.
- **Hypermedia.** Each resource representation carries `_links` to the actions the caller is
  currently permitted to take. The Web front end uses them to enable or disable controls, which
  keeps the UI honest about capability.
- **Times.** Event times are returned as `date`, `startTime` and `durationMinutes`, plus derived
  `startLocal`, `endLocal` and `startUtc`, `endUtc` (ISO 8601), and `timeZoneId` and
  `zoneAbbreviation`. Timestamps are UTC ISO 8601.
- **Idempotency.** Attendee confirm and cancel are idempotent against the final state. Staff
  commands that create resources accept an optional `Idempotency-Key` header, retained for 24
  hours.

## Discovery and identity

| Method and path | Purpose | Auth |
|---|---|---|
| `GET /api` | Link index | Anonymous |
| `GET /health/live`, `GET /health/ready` | Liveness; readiness (database reachable) | Anonymous |
| `GET /openapi/v1.json`, `/swagger` | API contract and UI | Anonymous |
| `GET /api/me` | Display name, `StaffId`, roles, scoped type, and granted capabilities (FR-10.8) | Staff; no capability needed |

## Reference data and settings

| Method and path | Use case | Capability |
|---|---|---|
| `GET /api/locations?includeInactive=` | List locations | Any staff |
| `POST /api/locations` | Create `{code, name, address, timeZoneId}` | `ManageReferenceData` |
| `PUT /api/locations/{id}` | Update `{name, address, timeZoneId, isActive, expectedVersion}` | `ManageReferenceData` |
| `GET /api/appointment-types?includeInactive=` | List types, each with its current Manager's display name | Any staff |
| `POST /api/appointment-types` | Create `{code, name}` | `ManageReferenceData` |
| `PUT /api/appointment-types/{id}` | Update `{name, isActive, expectedVersion}` | `ManageReferenceData` |
| `GET /api/attendee-groups?includeInactive=` | List groups, each with its requirement type ids and member count | Any staff |
| `POST /api/attendee-groups` | Create `{code, name, appointmentTypeIds[]}` | `ManageReferenceData` |
| `PUT /api/attendee-groups/{id}` | Update `{name, isActive, appointmentTypeIds[], expectedVersion}`; see FR-1.5 | `ManageReferenceData` |
| `GET /api/settings` | Read settings | `ManageSettings` |
| `PUT /api/settings` | `{inviteExpiryDays, maxAutoRetryCount, inviteOptionCount, expectedVersion}` | `ManageSettings` |

## Staff access

| Method and path | Use case | Capability |
|---|---|---|
| `GET /api/staff-access` | List profiles, with display name, `StaffId`, read-only roles and scope | `ManageStaffAccess` |
| `PUT /api/staff-access/{staffUserId}/scope` | `{appointmentTypeId \| null, expectedVersion}`. The response names any displaced Manager | `ManageStaffAccess` |

There is deliberately no endpoint to create a profile, delete one, or edit roles (FR-10.5).

## Negotiation and events

| Method and path | Use case | Capability |
|---|---|---|
| `GET /api/event-proposals?status=Open&locationId=` | The caller's type's proposals (FR-2.13) | `ManageEventNegotiation` |
| `POST /api/event-proposals` | ProposeEvent `{locationId, date, startTime, durationMinutes, appointmentTypeIds[], headcount}` returns `201`, with `status` `Open` or `Confirmed` and `eventId` if confirmed | `ManageEventNegotiation` |
| `PUT /api/event-proposals/{id}/acceptance` | Record or revise the caller's type's acceptance `{headcount}` | `ManageEventNegotiation` |
| `DELETE /api/event-proposals/{id}/acceptance` | Withdraw the caller's type's acceptance | `ManageEventNegotiation` |
| `POST /api/event-proposals/{id}/withdraw` | Withdraw the proposal (proposer's type only) | `ManageEventNegotiation` |
| `GET /api/events?locationId=&from=&to=&appointmentTypeId=` | Events. A Manager sees their type's view; Admin and Coordinator see all types | `ManageEventNegotiation` or `ViewEventOperations` |
| `GET /api/events/{id}` | One event with capacities, filtered the same way | as above |
| `PUT /api/events/{id}/capacities/{appointmentTypeId}` | Adjust `{totalHeadcount}` (caller's own type only) | `ManageEventNegotiation` |
| `POST /api/events/{id}/cancel?confirm=` | CancelEvent, two-step (FR-7.2 to 7.4) | `CancelEvent` |
| `GET /api/events/cancellable?locationId=&from=&to=` | Event operations list (window not started) | `ViewEventOperations` |

## Event groups

| Method and path | Use case | Capability |
|---|---|---|
| `GET /api/event-groups` | List groups with selected groups and memberships | `ManageEventGroups` |
| `GET /api/event-groups/{id}` | One group with selected groups and memberships | `ManageEventGroups` |
| `POST /api/event-groups` | Create `{title, description, attendeeGroupIds[]}` | `ManageEventGroups` |
| `PUT /api/event-groups/{id}` | Update `{title, description, attendeeGroupIds[], isOpen, expectedVersion}` | `ManageEventGroups` |
| `PUT /api/event-groups/{id}/events/{eventId}` | Add `{expectedVersion}`; active future event with the group's exact type set | `ManageEventGroups` |
| `PATCH /api/event-groups/{id}/events/{eventId}` | Toggle one membership `{isOpen, expectedVersion}` | `ManageEventGroups` |
| `DELETE /api/event-groups/{id}/events/{eventId}?expectedVersion=` | Remove one membership; bookings stay valid | `ManageEventGroups` |

## Attendees, invites and bookings

| Method and path | Use case | Capability |
|---|---|---|
| `GET /api/attendees?status=&groupId=&readiness=&search=` | Cursor list, with readiness and delivery | `ManageAttendees` |
| `POST /api/attendees` | Create `{name, email, attendeeGroupId}` | `ManageAttendees` |
| `PUT /api/attendees/{id}` | Edit `{name, email, attendeeGroupId}` (FR-4.2) | `ManageAttendees` |
| `DELETE /api/attendees/{id}?confirm=` | Delete, two-step (FR-4.5) | `ManageAttendees` |
| `POST /api/attendees/import` | Multipart CSV; all-or-nothing (FR-4.3) | `ManageAttendees` |
| `GET /api/attendees/{id}/eligible-event-count?locationIds=` | Live count for the invite dialog | `ManageAttendees` |
| `POST /api/attendees/{id}/invites` | Invite `{locationIds[]}`, returning `Invited` or `AwaitingAvailability` | `ManageAttendees` |
| `POST /api/attendees/{id}/recovery-invites` | Start recovery `{additionalLocationIds[]}` (FR-9.1) | `ManageAttendees` |
| `DELETE /api/attendees/{id}/recovery-invites/{inviteId}` | Cancel a pending recovery invite | `ManageAttendees` |
| `GET /api/attendees/{id}/bookings` | Bookings, with event and location | `ManageAttendees` |
| `POST /api/attendees/{id}/bookings/{bookingId}/cancel?confirm=` | Coordinator cancellation, two-step (FR-7.1) | `ManageAttendees` |
| `POST /api/attendees/{id}/email-retry` | Retry the newest failed or stale email (FR-11.3) | `ManageAttendees` |
| `GET /api/attendees/{id}/readiness` | `AttendeeReadiness` | `ViewAttendeeDashboards` |

## Dashboards and audit

| Method and path | Use case | Capability |
|---|---|---|
| `GET /api/dashboards?locationId=` | All tabs and counts (FR-13) | `ViewAttendeeDashboards` |
| `GET /api/audit?from=&to=&action=&actorType=&actorId=&entityType=&entityId=` | Search, scoped by bucket (FR-12.3) | `ViewEventAudit` or `ViewAttendeeAudit` |
| `GET /api/audit/attendees/{id}` | Attendee history | `ViewAttendeeAudit` |
| `GET /api/audit/events/{id}` | Event history, including its proposal | `ViewEventAudit` |

## Appointment workspace

| Method and path | Use case | Capability |
|---|---|---|
| `GET /api/appointment-workspace/events?locationId=` | Selector: events listing the caller's type, ending between 7 days ago and 14 days ahead | `ConductAppointments` |
| `GET /api/appointment-workspace/events/{eventId}` | Minimum-data roster (FR-8.1) | `ConductAppointments` |
| `PUT /api/appointment-workspace/appointments/{id}/status` | `{targetStatus, expectedVersion}` | `ConductAppointments` |
| `GET /api/appointment-workspace/events/{eventId}/roster.csv` | CSV download (FR-8.7, 8.8) | `ConductAppointments` |

## Attendee token endpoints (anonymous, rate-limited, REST-only)

| Method and path | Use case |
|---|---|
| `GET /api/booking/{token}` | View live options, topped up (FR-5.8), or the expired or superseded state |
| `POST /api/booking/{token}/confirm` | `{eventId}` returns `201` with the booking and manage link |
| `GET /api/manage/{token}` | View the booking, plus a `canCancel` flag |
| `POST /api/manage/{token}/cancel` | `{requestNewTime}` returns the outcome: `reinvited`, `reinvitePending`, `noEligibleEvents` or `cancelled` |

## MCP tools

The MCP server exposes one tool per staff operation above. Each tool is named from the OpenAPI
`operationId` in snake case, for example `propose_event`, `record_acceptance`, `list_attendees` and
`cancel_event`. Tools call the same handlers under the same capability checks, with the same bearer
token. A CI test compares the OpenAPI operation set, excluding anonymous and token endpoints, with
`tools/list`, and fails on any difference (FR-14.1). Each `operationId` also carries an
`x-mcp-tool` extension naming its tool.

## Error catalogue

| `type` | Status | Meaning |
|---|---|---|
| `validation-failed` | 422 | Field or row errors in `errors[]` (for CSV, with `line`) |
| `version-conflict` | 409 | Stale `expectedVersion`. `current` holds the latest state |
| `confirmation-required` | 409 | A two-step action's first call. `consequence` holds its effects |
| `capacity-exhausted` | 409 | A required type has no place left on that event |
| `capacity-below-bookings` | 409 | Adjustment below active bookings. `minimum` gives the lowest allowed value |
| `proposal-not-open` | 409 | Acceptance or withdrawal on a `Confirmed` or `Withdrawn` proposal |
| `window-started` | 409 | Cancellation or other change refused because the event has begun |
| `in-use` | 409 | Deactivation or re-zoning of reference data that is in use. `blocking` holds the counts |
| `requirements-locked` | 409 | A requirement change refused because of active bookings |
| `insufficient-events` | 409 | Fewer eligible events than `inviteOptionCount`. The attendee is set to `AwaitingAvailability` |
| `recovery-active` | 409 | Another recovery is pending, or the correction is blocked by recovery |
| `last-admin` | 409 | The operation would leave no Admin |
| `token-invalid` | 404 | Unknown, superseded or tampered attendee token. Deliberately indistinguishable from not found |
| `token-expired` | 410 | The book token's `Invite` has expired |
| `forbidden` | 403 | Capability missing, scope null, or Admin requesting attendee data |
| `unauthenticated` | 401 | Missing or invalid bearer token, or missing `staff_id` |
| `rate-limited` | 429 | Includes `Retry-After` |

### Example

```http
POST /api/booking/3q9…/confirm
Content-Type: application/json

{ "eventId": "b7e1c2d4-…" }
```

```http
409 Conflict
Content-Type: application/problem+json

{
  "type": "capacity-exhausted",
  "title": "This time is no longer available",
  "status": 409,
  "detail": "The last Induction place at this event was just taken.",
  "errors": [{ "field": "eventId", "code": "capacity-exhausted" }]
}
```
