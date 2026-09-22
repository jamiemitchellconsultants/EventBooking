# Phase 5 — Web (Tasks 24–27)

[← Plans overview](README.md) · [Phase 4](phase-4-api-and-mcp.md) · [Master plan](../superpowers/plans/2026-09-19-eventbooking-implementation.md) · [Ontology](../ontology.md)

> Use superpowers:executing-plans. Execute one task document at a time, in the order below. Every
> task ends with its own commit and push on the same phase branch.

**Status: complete as a plan; not executed.** All four documents are hand-authored. Complete tests
and implementation fragments are written into the task documents, and the executing model must
compile and test-drive them. No Phase 5 source has been built from these documents, so the phase has
no observed build or test count.

**Goal:** Replace the branded port with a neutral, re-skinnable standalone Blazor WebAssembly
front end. It consumes Phase 4's REST contract without referencing a server assembly, presents all
staff and attendee flows in the screen designs, and proves every route has zero axe-core violations
at mobile and desktop widths.

**Architecture:** The Web project remains a standalone WASM client. Its DTOs duplicate the JSON
wire contract and a contract suite checks them against the OpenAPI document Phase 4 serves. A
single response reader owns RFC 9457 parsing, pages switch on stable problem slugs rather than
messages, create commands use one Idempotency-Key per user submission, controls come from `_links`,
and event times are rendered from the API's complete event-time representation. Task 24 creates the
design system and the Playwright/axe harness; Tasks 25–27 add their route families to that harness.

**Tech Stack:** .NET 10, Blazor WebAssembly, bUnit 2.9.0, xUnit 2.9.3, Microsoft.Playwright 1.62.0,
axe-core 4.13.0, CSS custom properties, OpenAPI 3.1.

**Spec:** [Design system and information architecture](../design/03a-design-system-and-ia.md),
[screens and flows](../design/03b-screens-and-flows.md),
[API design](../design/05-api-design.md),
[security and authentication](../design/06-security-and-authentication.md),
[non-functional requirements](../design/08-nonfunctional-requirements.md), and
[master plan Tasks 24–27](../superpowers/plans/2026-09-19-eventbooking-implementation.md).

## Settlement carried by this phase

The authoring method remains section 6a of [the handover](HANDOVER.md): hand-authored, with its gap
and async sweeps run for every task. The Playwright/axe project and its workflow job move from
master Task 27 to Task 24. Markup-heavy work needs a rendered-page check from the first component
task; waiting until Task 27 would leave three tasks of plausible but unrendered Razor. Task 24 scans
the shell and its routes, and every later task extends the same manifest. Task 27 keeps the phase
gate: every final route, including seeded attendee-token routes, must be clean at both widths.

## Global constraints

- Web references no Domain, Application, Infrastructure, Api or Mcp assembly. Only test fixtures
  may host static files and stub HTTP; the Web project talks to `/api` over HTTP.
- Client contracts use camel-case JSON names from Task 22b. A contract test compares every wire
  DTO used by Web with the served OpenAPI document; client-composed view models are tested at their
  composition boundary instead.
- Every list reads `{ items, nextCursor }`, sends only `cursor` and `limit`, and offers “Load more”
  only while `nextCursor` is non-null. There is no offset.
- Failures are read as `application/problem+json`. UI branches use the `type` slug, never `title`,
  `detail` or message prose. A 403 other than `GET /api/me` is a forbidden state, not a sign-in
  prompt. A missing or malformed `staff_id` therefore reaches `/api/me` for its diagnostic profile
  and receives 403 on every other staff route, exactly as Phase 4 settled.
- Staff controls are present or absent from each resource's `_links`; no Razor file checks a role
  to decide whether a server action is allowed. Roles are used only for navigation and Help-guide
  selection.
- Every persisted event time is rendered from the API's `date`, `startTime`, `durationMinutes`,
  `startLocal`, `endLocal`, `startUtc`, `endUtc`, `timeZoneId` and `zoneAbbreviation`. The client
  never recomputes an API result or converts a time zone. The sole calculation is Task 26's
  unsaved proposal preview, which adds the selected 15-minute duration to the user's local input.
- A create form generates one Idempotency-Key when the user submits, reuses it while retrying that
  submission, and replaces it only after success or an intentional edit starts a new submission.
- No colour literal occurs in Razor or CSS outside `wwwroot/theme.css`. Colour never stands alone;
  badges, validation and state changes always carry text.
- The compile-time `EVENTBOOKING_E2E` authentication provider is the only test bypass. A normal
  build cannot reference or instantiate it, and the E2E host publishes Web with that symbol into a
  temporary directory.
- WCAG 2.1 AA is the baseline. All actions are keyboard-operable, dialogs return focus, and
  banners plus refreshed conflict rows announce changes through live regions.
- Attendee pages remain usable below 640 px and interactive within the NFR-P4 budget. Task 27
  checks the trimmed, compressed publish output rather than only the debug bundle.

## Review focus

1. A 403 response on a staff page renders “You do not have access to this page” and does not start
   OIDC navigation; a 401 is the only staff response that may reauthenticate.
2. A `capacity-exhausted` failure removes only the selected option and preserves every other live
   option; no message text is inspected.
3. A cursor containing `+`, `/`, `=` or percent escapes survives the client's query builder
   unchanged after one URI-encoding pass and is never decoded by Web.
4. A create retry after a network failure sends the same Idempotency-Key, while editing and
   resubmitting after success sends a different one.
5. A route whose data is empty, forbidden, conflicted or still loading remains axe-clean at 390 ×
   844 and 1440 × 900; the route manifest names states, not only happy paths.

## Before you start

Phase 5 starts only after the Phase 4 pull request has merged. Pull request #27 was merged on
21 September 2026; still fetch and start from the latest `origin/main` when executing:

```bash
git fetch origin
test -z "$(git status --porcelain)"
git switch -c codex/phase-5-web origin/main
export EXECUTOR_COAUTHOR="Your Harness <harness@example.invalid>"
```

Before implementing pages, generate the Task 22b OpenAPI snapshot and run the Task 24 contract
suite. Several screens require data that Web is forbidden to invent: listed proposal types,
workspace appointment identifiers and action links, attendee-facing location/time details, and an
authorized collection affordance for create/import/propose controls when a list is empty. The
focused API prerequisite extends `GET /api/me` to the CurrentStaffResponse shape in Task 24: it
adds scope type code/name and caller-specific `_links` for those collection actions, leaving every
list envelope exactly `{items,nextCursor}`. If the merged contract lacks one of the other required
fields, repair it in that same focused prerequisite commit and regenerate the snapshot before
continuing. Do not replace a missing affordance with a role check or make a Page DTO diverge from
the envelope.

### Known contract-preflight repairs

Treat these as one focused API prerequisite before Task 24, with API contract tests and a refreshed
OpenAPI snapshot. The Web tasks define the complete client-side members and schema pairs; the API
repair must make those pairs true rather than weakening the Web tests.

| Consumer | Phase 4 shape that is insufficient | Required wire repair |
| --- | --- | --- |
| Every staff page | `/api/me` has scope id/capabilities but no scope code/name or mutation affordances | CurrentStaffResponse adds scope code/name and caller-specific createLocation, createAppointmentType, createAttendeeGroup, proposeEvent, createAttendee and importAttendees links |
| Reference data and negotiation | AppointmentTypeResponse exposes only nullable manager display text, which cannot distinguish no assignment from an assigned Manager whose display name is unavailable | Add explicit `hasManager`; Web copies that field into TypeOption and never derives assignment from ManagerDisplayName |
| Negotiation and operations | Proposal rows omit listed type detail; proposal links are not state-specific; capacity rows have no adjust link; the capacity-conflict writer discards the current total and remaining values | Add ProposalTypeResponse rows, accept/withdraw-acceptance/withdraw links by state, an `adjust` link on each caller-editable capacity, and a `current` object containing `totalHeadcount` and `remainingCapacity` beside `minimum` on `capacity-below-bookings` |
| Appointment workspace | WorkspaceEventView uses loose date/time fields; WorkspaceRosterRow does not expose the appointment resource/action links | Extend those existing schemas in place: WorkspaceEventView returns EventTimeResponse plus workspace event links, and WorkspaceRosterRow adds appointmentId plus status-action links; do not rename either schema |
| Coordinator | Attendee, eligible-count, import, invite, dashboard and audit responses omit fields/actions the screens require | Add the response schemas paired in Task 27, preserving the page envelope and exposing actor display/details and complete event time |
| Anonymous attendee | Invite and managed-booking views omit location/address/complete time and action links; confirm has no named schema | Add InviteResponse, ConfirmBookingResponse and ManagedBookingResponse exactly as Task 27 pairs them, with confirm/cancel links only while allowed |

Do not fold these changes into a Web task commit: they change the API contract and need their own
reviewable prerequisite commit before the four one-commit Web tasks begin.

## Task order

| Order | Task | Document | Commit message |
| --- | --- | --- | --- |
| 1 | Task 24 — neutral theme, component contracts, and early rendered-page gate | [phase-5a-design-system.md](phase-5a-design-system.md) | `feat(web): neutral theme and design-system components` |
| 2 | Task 25 — Admin screens | [phase-5b-admin-screens.md](phase-5b-admin-screens.md) | `feat(web): Admin reference-data screens` |
| 3 | Task 26 — Manager and operations screens | [phase-5c-manager-and-operations.md](phase-5c-manager-and-operations.md) | `feat(web): N-type negotiation board and workspace` |
| 4 | Task 27 — Coordinator, attendee, Help, and phase gate | [phase-5d-coordinator-attendee-help.md](phase-5d-coordinator-attendee-help.md) | `feat(web): coordinator and attendee flows with location selection` |

## Route ownership

| Task | Routes added to the axe manifest |
| --- | --- |
| 24 | `/`, `/help` and not-found; the harness also establishes authenticated and anonymous test principals |
| 25 | `/admin/locations`, `/admin/appointment-types`, `/admin/attendee-groups`, `/admin/settings`, `/admin/staff-access` |
| 26 | `/events/negotiate`, `/events/operations`, `/appointments` |
| 27 | `/attendees`, `/dashboards`, `/audit`, `/book/{seeded-token}`, `/manage/{seeded-token}` and every alternate state named in 03b |

Each manifest entry fixes a role, viewport and fixture state. The E2E stub owns deterministic IDs
and seeded attendee tokens; the pages still execute as published WASM and make real HTTP requests
to the stub. This checks the rendered accessibility tree without coupling Web to server assemblies.

## Verification evidence

No verified count exists for Phase 5. Like Phases 3 and 4, it is hand-authored and has never been
executed. Each task's Step 4 prints its expected test additions but labels them expectations. The
last measured repository checkpoint remains Task 11: Domain 360, Application 421,
Infrastructure 206, API 232, MCP 35, Web 241 and SeedData 75; total 1570. Tasks 12–23 deliberately
make that comparison non-linear, so the executor records the next real checkpoint instead of
reconciling against 1570.

## Per-task authoring checks

Run both hand-authored sweeps from [HANDOVER section 6a](HANDOVER.md#6a-the-loop-for-one-hand-authored-task)
after each task document is applied. The gap sweep must report no missing C# file stem. The async
sweep must print its match count and report zero async methods without `await`; a zero match count
is not evidence. Then build, run every test, and run the task's mobile and desktop axe matrix.

## Pull request

Task 27 carries the phase gate and opens the pull request. Phase 5 is decision-bearing because it
implements D10's neutral theme and settlement #20's early accessibility gate. The body therefore
carries the `narrative-required` label, the three exact Narrative headings, and the current
AI-Fingerprint. Do not merge it; code-owner approval and CI remain mandatory.
