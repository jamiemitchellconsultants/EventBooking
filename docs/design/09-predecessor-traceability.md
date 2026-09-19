# 09 — Predecessor Traceability

[← Non-functional requirements](08-nonfunctional-requirements.md) · [Index](README.md)

EventBooking generalises JointBooking, a private, single-organisation recruitment scheduler. This
document accounts for **every** JointBooking capability, design increment and known defect, so that
nothing is lost without a recorded decision. JointBooking's own names are written in plain text,
because they are not EventBooking vocabulary.

Dispositions:

- **Carried**: the same behaviour, renamed.
- **Generalised**: the behaviour is kept but extended for many locations or types.
- **Changed**: deliberately different.
- **Dropped**: not in EventBooking.

## Capability areas

| JointBooking area | Disposition | EventBooking |
|---|---|---|
| Reference data: three fixed appointment types, seeded employee groups, settings | Generalised | FR-1: Admin-managed `Location`, `AppointmentType` and `AttendeeGroup`; `inviteOptionCount` added |
| Slot negotiation: three Managers per 4-hour window | Generalised | FR-2: any number of listed types, variable duration, per `Location` |
| Capacity and no-overbooking: three capacity rows | Generalised | FR-3: N rows, ordered locking, charges only required types |
| Candidate management, CSV upload, readiness | Carried | FR-4 (`Attendee`, `name,email,attendee_group` CSV) |
| Invite engine: 3 options, expiry sweep, retries | Generalised | FR-5: location-restricted, configurable option count |
| Candidate booking and manage flow | Carried | FR-6, with location and zone shown |
| Cancellation and rescheduling | Carried | FR-7 |
| Appointment workspace: check-in and outcomes, roster CSV | Generalised | FR-8: selector spans locations |
| Missed-appointment recovery | Generalised | FR-9: recovery location defaults to the original booking's |
| Scoped multi-role authorization | Carried | FR-10, [06](06-security-and-authentication.md#authorization) |
| Staff identity: staff-number claim | Changed | FR-10.1: the pattern is deployment-configured (D11) |
| Identity-provider-sourced roles | Carried | FR-10.2 |
| Staff display name | Carried | FR-10.9 |
| Notifications: 4 templates, delivery log, retry | Changed | FR-11: a durable outbox dispatcher (D13), SMTP only |
| Audit trail and audit search | Carried | FR-12, with the reference-data and settings actions added |
| Coordinator dashboards | Generalised | FR-13: filterable by location, bounded |
| REST, OpenAPI, hypermedia, MCP parity | Carried | FR-14, [05](05-api-design.md) |
| Confirmed-slot bulk CSV import | Dropped | D6: negotiation is the only source of events |
| Hourly invite sweep as an in-process service or serverless function | Changed | FR-15: a 15-minute hosted service with an advisory lock; the serverless entry point is dropped |
| Head-office address and time-zone configuration | Changed | Moved onto `Location` |
| Airline-branded design system and assets | Dropped | D10: neutral tokens, re-skinnable |
| In-app Help with role guides | Carried | [03a — Help](03a-design-system-and-ia.md#help) |
| Demo seed, reanchor and reseed, seeded invitation emails | Changed | [07 — Seed data](07-deployment.md#seed-data): migrate-only by default; demo data behind `--demo` |
| Local Docker Compose stack | Carried | [07 — Local](07-deployment.md#local-docker-composeyml-at-the-repository-root), without MinIO (D9) |
| Home-lab deployment | Carried | [07 — Home lab](07-deployment.md#home-lab-deployhome-lab), with a reference install script in-repo |
| Cloud deployment: Terraform, functions, managed database, email service, static site | Dropped | Out of scope |
| Cloud identity-provider adapter | Dropped | D8: generic OIDC plus Keycloak |
| Object storage (MinIO) | Dropped | D9: nothing used it |
| Ontology tooling, Narrative, AI fingerprint, docs-skipping build | Carried | Already present in this repository ([AGENTS.md](../../AGENTS.md)) |

## Design increments

| JointBooking design | Disposition | Where |
|---|---|---|
| Core joint-booking design | Generalised | This package |
| Deployment infrastructure (cloud and local compose) | Changed | Local compose carried; cloud dropped |
| Home-lab deployment | Carried | [07](07-deployment.md) |
| Scoped multi-role authorization | Carried | [06](06-security-and-authentication.md) |
| Booking-appointment workspace | Generalised | FR-8 |
| Staff-number identity claim | Changed | FR-10.1, D11 |
| Remote MCP server | Carried | FR-14 |
| Employee-group requirements | Generalised | FR-1.4, FR-1.5, FR-4 |
| Identity-provider-sourced staff roles | Carried | FR-10.2, FR-10.3 |
| Appointment roster CSV download | Carried | FR-8.7, FR-8.8 |
| Audit trail discovery (search) | Carried | FR-12.2, FR-12.3 |
| Coordinator individual-booking cancellation | Carried | FR-7.1 |
| Manager assignment and roles visibility | Changed | FR-10.5, FR-10.6. Clearing a type's only Manager is now allowed, because types are Admin-created and may exist before any Manager does |
| Slot cancellation for Admin and Coordinator | Carried | FR-7.2 (`CancelEvent`) |
| Staff identity display name | Carried | FR-10.9 |
| Seeded candidate invitations | Carried | [07 — Seed data](07-deployment.md#seed-data) |
| Agent-friendly API, OpenAPI and MCP parity | Carried | [05](05-api-design.md) |
| Invite option replacement and coordinator notice | Carried | FR-5.8 |
| No-show recovery window (7 days past) | Carried | FR-8.2 |

## Product questions carried as decided

JointBooking recorded proposed dispositions, never formally accepted, for three open product
questions. EventBooking decides them:

| Question | Decision for EventBooking |
|---|---|
| Reminders before an upcoming booking | **Not provided.** The only reminder is `AttendeeReinvite`, for an unanswered invite. A pre-appointment reminder would need a new `EmailTemplate` and a sweep rule; it is a future increment |
| Operational-reporting baseline | **Point-in-time only:** dashboards (FR-13), the workspace, and audit search (FR-12). No historical reporting or exports beyond the roster CSV |
| Scheduling against a recruitment milestone | **Not applicable.** EventBooking has no milestone concept. Organisations needing one should model it outside EventBooking |

## Known defects in the predecessor

JointBooking's independent gap review raised 34 findings. The table gives each one's status here.

| # | Finding | Status in EventBooking |
|---|---|---|
| 1 | The sweep never re-issued invites; cancel-and-rebook failed with a pending recovery | Closed by FR-5.6 and FR-9.6 |
| 2 | Cloud functions started on placeholder configuration | Closed by the startup validation of † settings ([04](04-solution-architecture.md#configuration), [06](06-security-and-authentication.md#attendee-authentication)) |
| 3 | No network route to the identity-provider keys or the email API (cloud) | Not applicable: no cloud target. JWKS reachability is covered by NFR-R4 |
| 4 | The sweep could not run serverless | Not applicable: it is a hosted service (FR-15) |
| 5 | The CDN returned 403 for booking links | Closed by the SPA fallback in the web containers ([07](07-deployment.md)) |
| 6 | Role changes took effect only on the "who am I" endpoint | Closed by FR-10.2 |
| 7 | Cancellation possible after the appointment | Closed by FR-6.7 and FR-7.4 |
| 8 | Deleting a candidate leaked recovery capacity | Closed by FR-4.5 |
| 9 | One shared rate-limit bucket | Closed by [06 — Rate limiting](06-security-and-authentication.md#rate-limiting) |
| 10 | Failed cancellation emails could not be retried | Closed by FR-7.5 and FR-11.3 |
| 11 | Slot import returned 500 and accepted past dates | Not applicable: import is dropped (D6). Proposals validate the window (FR-2.2) |
| 12 | An unscoped Manager could cancel any slot | Closed by FR-10.7 |
| 13 | An MCP list returned rows with no identity | Closed by FR-14.2 |
| 14 | MCP lagged REST and lacked import limits | Closed by FR-14.1 and FR-14.3 |
| 15 | Audit stored email addresses | Closed by FR-11.4 |
| 16 | Replacing a Manager orphaned proposals | Closed by FR-2.10 |
| 17 | Roster CSV formula injection | Closed by FR-8.8 |
| 18 | No-shows recordable only until midnight | Closed by FR-8.2 |
| 19 | Retry count documented as email retry | Closed: FR-1.9 and FR-5.6 describe it correctly; the guides must match |
| 20 | Build did not gate `main`; guide-only changes skipped tests | Closed by the definition of done ([08](08-nonfunctional-requirements.md#definition-of-done)): guides ship in the web bundle, and the Web tests cover them |
| 21 | No web bundle artefact or migration path (cloud) | Closed by the images and migrations bundle ([07](07-deployment.md#images)) |
| 22 | Database unencrypted; the app used the master user (cloud) | Partly applicable: separate migration and application roles ([06](06-security-and-authentication.md#data-protection)). Encryption at rest is the host's concern |
| 23 | Sweep left candidates stuck when a re-issue failed | Closed by FR-5.7 |
| 24 | An unreachable cancelled candidate status | Closed: `AttendeeStatus` has no such value, and transitions are a closed table (D15) |
| 25 | Filled options dropped silently | Closed by FR-5.8 |
| 26 | Stale progress documents | Not applicable. This package is maintained under the definition of done |
| 27 | Terraform unvalidated; actions unpinned | Terraform: not applicable. Actions are pinned by SHA ([07](07-deployment.md#cicd)) |
| 28 | Documentation rule not enforced by the build | Adopted: the .NET documentation-file build setting with warnings as errors for public APIs in Domain and Application |
| 29 | New surfaces lacked narrative entries | Governance: the Narrative protocol in [AGENTS.md](../../AGENTS.md) applies |
| 30 | Settings and CSV imports missing from audit | Closed by FR-1.10 and FR-4.4 |
| 31 | Unbounded dashboard and list queries | Closed by FR-4.6, FR-13.3 and cursor pagination everywhere |
| 32 | Seed tooling seeded and sent email by default | Closed: migrate-only by default ([07](07-deployment.md#seed-data)) |
| 33 | No `.dockerignore`; containers ran as root | Closed ([07](07-deployment.md#images)) |
| 34 | No machine-readable ontology | Closed: `docs/ontology.ttl` is the source |

## Open documentation questions in the predecessor, closed here

| Question | Closed by |
|---|---|
| Reconcile the audit contract (settings, candidate actions, admin search access) | FR-1.10, FR-12.3 |
| Authoritative candidate status transitions and invite selection | [01 — AttendeeStatus](01-domain-model.md#attendeestatus), FR-5.2, FR-5.3 |
| Candidate token lifecycle | [06 — Attendee authentication](06-security-and-authentication.md#attendee-authentication) (D14) |
| Durable notification outbox | [04 — Notification outbox](04-solution-architecture.md#notification-outbox) (D13) |
| Open NFR boundary values | [08 — Boundary values](08-nonfunctional-requirements.md#boundary-values) |
| Cutover and release definition of done | [08 — Definition of done](08-nonfunctional-requirements.md#definition-of-done). There is no cutover: EventBooking starts with a fresh schema (D12) |
