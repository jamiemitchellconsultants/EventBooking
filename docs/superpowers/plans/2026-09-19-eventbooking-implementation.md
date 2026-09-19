# EventBooking Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.
>
> **This plan is deliberately code-free.** Each task states the behaviour, the contracts other
> tasks rely on, and the exact test cases to write first. The implementer writes the code. Where a
> task says "port", the source is the JointBooking repository at commit `6957928`; read the named
> JointBooking file, then carry its behaviour across under EventBooking names.

**Goal:** Build EventBooking by porting JointBooking's .NET solution into this repository and
generalising it to many locations, variable-length events and any number of appointment types,
without weakening the no-overbooking guarantee.

**Architecture:** Clean Architecture (Domain, Application, Infrastructure, Api.Auth, Api, Mcp,
Web, SeedData) ported from JointBooking, with the AWS and Entra ID projects deleted and the local
infrastructure folded into one Infrastructure project. PostgreSQL enforces capacity with ordered
row locks plus a check constraint. Email goes through a durable outbox. REST and MCP call the
same Application handlers.

**Tech Stack:** .NET 10 (`net10.0`), ASP.NET Core minimal APIs, EF Core with Npgsql, PostgreSQL 16,
NodaTime, Blazor WebAssembly, Keycloak 26 (OIDC), Mailpit/SMTP, the MCP C# SDK, xUnit,
Testcontainers, WebApplicationFactory, bUnit, Playwright with axe-core, k6, Docker Compose, Caddy,
GitHub Actions.

**Spec:** [docs/superpowers/specs/2026-09-19-eventbooking-design.md](../specs/2026-09-19-eventbooking-design.md)
(decisions D1–D12, the vocabulary mapping and scope) and the design package
[docs/design/](../../design/README.md) (all detail, plus D13–D15). Where the two differ in detail
the design package wins; where they differ on a decision the spec wins. Known detail differences
this plan resolves in favour of the design package:

- Seed data: 4 `AttendeeGroup`s and demo users as listed in
  [07 — Seed data](../../design/07-deployment.md#seed-data), not the 3 groups in spec §8.
- Seed flags: migrate-only by default with `--demo`, `--reanchor` and `--reseed`, not
  `--skip-seed` from spec §7.2.

## Global Constraints

- Target framework `net10.0`; Nullable enabled; TreatWarningsAsErrors true; package versions
  pinned centrally in `Directory.Build.props` and `Directory.Packages.props`.
- Documentation-file generation on, with warnings as errors, for public APIs in Domain and
  Application (predecessor finding 28).
- Vocabulary: [docs/ontology.md](../../ontology.md) is canonical. Every type, table, column, route,
  JSON field, MCP tool and screen label reads off it. No JointBooking names survive in code
  (Candidate, Slot, ConfirmedSlot, EmployeeGroup, head office).
- Ontology changes: edit `docs/ontology.ttl`, run `node scripts/build-ontology.mjs`, commit both
  with the code. Run `node scripts/check-ontology-terms.mjs` after staging and before every commit.
- Pagination: cursor (keyset) only, `?cursor=&limit=` with limit 1–200, default 50, response
  `{ items, nextCursor }`. Never offset.
- Errors: RFC 9457 problem details with the stable `type` slugs in
  [05 — Error catalogue](../../design/05-api-design.md#error-catalogue). An expected failure is never
  a 500.
- Transactions: every write is one transaction; every state change writes its `AuditLog` entry in
  the same transaction; every email is a `Pending` `EmailLog` row committed with the business
  change.
- Lock order, always: `Attendee` row → `EventProposal` row → `Event` rows by ascending id →
  `EventCapacity` rows by (`eventId`, `appointmentTypeId`) ascending.
- Boundary values exactly as in [08 — Boundary values](../../design/08-nonfunctional-requirements.md#boundary-values)
  (for example `durationMinutes` 15–720 in multiples of 15, headcount 1–1000, types per proposal
  1–20, `inviteOptionCount` 1–5 default 3, CSV at most 1000 rows and 1 MB).
- Personal data (`name`, `email`) never appears in `AuditLog`, `EmailLog` or logs; no token, URL or
  message body is ever stored.
- Time: an `EventWindow` is local wall-clock time at its `Location`; instants are computed with
  NodaTime from `timeZoneId`; the only stored instant is the derived `start_utc` column.
- `StaffId` pattern is configuration, default `^[A-Z0-9]{1,32}$`.
- No cloud provider code, no MinIO, no Entra ID, no bulk event import, no data migration from
  JointBooking.
- Every third-party GitHub Action pinned by commit SHA. Every image runs as non-root.
- Definition of done per change: [08 — Definition of done](../../design/08-nonfunctional-requirements.md#definition-of-done)
  (tests at the right level, REST + MCP + OpenAPI parity, Help guides for user-visible changes,
  design package updated if a documented rule changes, Narrative sections when decision-bearing).
- Git: work on a feature branch per phase; open one pull request per phase; never push to `main`.
  Each PR body carries the `AI-Fingerprint:` footer and, when decision-bearing, the
  `narrative-required` label and the three Narrative headings (see `AGENTS.md`).

---

## Delivery shape

The spec covers several subsystems, so the work is split into **eight phases**. Each phase ends
with a green build, a working system and one pull request. A phase may be lifted out into its own
plan if it grows; the task numbering is global so cross-references stay stable.

| Phase | Tasks | Ends with | PR is decision-bearing? |
|---|---|---|---|
| 0 Port and strip | 1–3 | JointBooking behaviour, EventBooking names, AWS gone, build green | Yes (D5, D6, D8, D9, D11) |
| 1 Domain generalisation | 4–8 | Pure domain for N types, locations and zones, fully unit-tested | Yes |
| 2 Persistence | 9–11 | Fresh schema, lock helpers, eligibility query, concurrency harness | Yes (D12) |
| 3 Application | 12–20 | Every use case generalised, outbox and background jobs | Yes (D13–D15) |
| 4 API and MCP | 21–23 | Full REST catalogue, conventions, MCP parity | No, unless a contract decision is made |
| 5 Web | 24–27 | Neutral theme, admin screens, dynamic type columns, attendee pages | Yes (D10) |
| 6 Seed and deployment | 28–31 | Demo dataset, local stack, home lab, image pipeline | Yes |
| 7 Verification and docs | 32–33 | Load test, runbooks, guides | No |

### Target file structure

Source projects under `src/`, tests under `tests/`, each renamed from JointBooking.* to
EventBooking.*:

| Path | Responsibility |
|---|---|
| `EventBooking.sln`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig` | Solution and central build settings |
| `src/EventBooking.Domain/` | Folders by aggregate: `Locations/`, `AppointmentTypes/`, `AttendeeGroups/`, `Settings/`, `Events/` (proposal, event, capacity, window), `Attendees/`, `Invites/`, `Bookings/`, `Access/`, `Audit/`, `Notifications/`, `Time/`, `Common/` |
| `src/EventBooking.Application/` | Folders by use-case area: `ReferenceData/`, `Settings/`, `Negotiation/`, `Events/`, `Attendees/`, `Invites/`, `Bookings/`, `Recovery/`, `Appointments/`, `Access/`, `Notifications/`, `Dashboards/`, `Audit/`, `Jobs/`, `Abstractions/` (ports), `Common/` |
| `src/EventBooking.Infrastructure/` | `Persistence/` (DbContext, configurations, one fresh migration, repositories, unit of work, lock helpers, read-model queries), `Email/` (SMTP sender, composer, outbox dispatcher), `Tokens/`, `Time/` (clock, NodaTime resolver), `Jobs/` (advisory locks) |
| `src/EventBooking.Api.Auth/` | Provider-neutral OIDC bearer and claim mapping (renamed from Api.Auth.Local) |
| `src/EventBooking.Api/` | Endpoints by area, problem-details mapping, hypermedia, OpenAPI, rate limiting, hosted services |
| `src/EventBooking.Mcp/` | One tool per staff operation |
| `src/EventBooking.Web/` | Blazor WASM: `Pages/`, `Components/` (design-system components), `Services/` (typed clients), `wwwroot/theme.css`, `wwwroot/help/*.md` |
| `src/EventBooking.SeedData/` | Migrate + seed CLI, demo dataset spec, Keycloak convergence |
| `tests/EventBooking.*.Tests/` | Mirrors each source project |
| `tests/load/` | k6 load test |
| `docker-compose.yml`, `deploy/keycloak/`, `deploy/web/`, `deploy/home-lab/` | Deployment |
| `.github/workflows/` | `dotnet-build.yml` (exists), `ontology-lint.yml` (exists), new `images.yml`, `compose-smoke.yml` |

---

## Phase 0 — Port and strip

### Task 1: Import the solution under EventBooking names, without AWS

**Files:**
- Create: `EventBooking.sln`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`,
  `.dockerignore`, every project under `src/` and `tests/` listed in the target structure.
- Source: JointBooking `src/*`, `tests/*`, `Directory.*.props`, `JointBooking.sln`.
- Do not import: JointBooking `src/JointBooking.Infrastructure.Aws`, `src/JointBooking.Api.Auth.EntraId`,
  `terraform/`, `deploy/minio/`, `src/JointBooking.Api/SweepLambdaFunction.cs`, their tests,
  `docs/estimated-run-costs.md`, AWS diagrams, `.github/workflows/deploy-artefacts.yml`'s Lambda
  steps.
- Modify: `.github/workflows/dotnet-build.yml` only if its solution discovery needs the new name.

**Interfaces:**
- Consumes: nothing.
- Produces: a buildable solution with projects EventBooking.Domain, .Application,
  .Infrastructure, .Api.Auth, .Api, .Mcp, .Web, .SeedData and matching test projects. The
  Infrastructure project contains what JointBooking had in Infrastructure.Local (SMTP transport,
  SMTP options, system clock, HMAC tokens), registered from one dependency-injection entry point.

- [ ] **Step 1: Establish the baseline.** In JointBooking at `6957928`, run the full test suite and
  record the pass count per test project. This is the parity target for Tasks 1–3.
- [ ] **Step 2: Copy and rename.** Copy the projects into this repository, renaming assemblies,
  root namespaces, project references and the solution from JointBooking to EventBooking. Only the
  project/namespace prefix changes in this step; domain names stay JointBooking-shaped until Task 2.
- [ ] **Step 3: Delete the AWS and Entra ID paths.** Remove the Aws infrastructure project, the
  Entra ID auth project, the sweep Lambda entry point and every test that exercises them
  (for example the Entra ID authentication-extension tests in Api.Tests). Remove the configuration
  switch that selected between local and AWS providers.
- [ ] **Step 4: Fold Infrastructure.Local into Infrastructure.** Move its SMTP transport, options
  and registration into `src/EventBooking.Infrastructure/Email/`, and rename Api.Auth.Local to
  Api.Auth. There must be one registration entry point for infrastructure.
- [ ] **Step 5: Turn on documentation files** for Domain and Application with warnings as errors.
  Add missing XML documentation on public members until the build is clean.
- [ ] **Step 6: Build and test.** Run `dotnet build EventBooking.sln -warnaserror` and
  `dotnet test EventBooking.sln`. Expected: green; test count equals the baseline minus the deleted
  AWS/Entra ID tests. Tests needing Docker (Testcontainers) must run, not be skipped.
- [ ] **Step 7: Guard against regression.** Add a test in Api.Tests asserting that no loaded
  assembly name contains "Aws" or "EntraId" and that the Api project has no reference to an AWS SDK
  package.
- [ ] **Step 8: Commit** with message "build: port JointBooking solution as EventBooking without AWS".

### Task 2: Apply the vocabulary mapping

**Files:**
- Modify: every source and test file carrying a JointBooking name; EF configurations and the
  ported migrations (they are replaced in Task 9, but must still build and apply here); API routes
  and contracts; MCP tool names; Web pages, services and route paths; email templates; Help guides.
- Reference: spec §3 vocabulary table.

**Interfaces:**
- Produces: domain types named exactly as the ontology: `Attendee`, `AttendeeRequirement`,
  `AttendeeStatus`, `AttendeeReadiness`, `AttendeeReadinessCode`, `AttendeeGroup`,
  `AttendeeGroupRequirement`, `EventProposal`, `EventProposalStatus`, `Event`, `EventStatus`,
  `EventCapacity`, `EventWindow`; `EmailTemplate` values `AttendeeInvite`, `AttendeeReinvite`,
  `EventCancelledRebookingNeeded`, `BookingConfirmation`; `ActorType` value `AttendeeToken`;
  `StaffCapability` values renamed per spec §3; `AuditAction` values renamed per spec §3. Routes
  `/api/attendees`, `/api/event-proposals`, `/api/events`; Web routes `/attendees`,
  `/events/negotiate`, `/events/operations`.

- [ ] **Step 1: Write the guard test first.** Add a test in Domain.Tests that reflects over every
  public type and enum member in Domain and Application and fails if any name contains "Candidate",
  "Slot", "EmployeeGroup" or "HeadOffice". Add an equivalent check over the OpenAPI document paths
  and schema names in Api.Tests, and over the MCP tool list in Mcp.Tests.
- [ ] **Step 2: Run them.** Expected: FAIL, listing the JointBooking names.
- [ ] **Step 3: Rename mechanically,** working through spec §3 row by row. Rename types, files,
  folders, properties, enum members, JSON names, route segments, MCP tool names and Web routes.
  Keep behaviour identical: no logic change in this task.
- [ ] **Step 4: Update email wording** so "candidate" reads "attendee" in every template. The
  head-office wording stays until Task 18 replaces it with `Location` details.
- [ ] **Step 5: Run the full suite.** Expected: the guard tests pass and the test count equals Task 1.
- [ ] **Step 6: Run** `node scripts/check-ontology-terms.mjs` over staged Markdown (Help guides
  are Markdown). Fix any flagged term by using the canonical name.
- [ ] **Step 7: Commit** with message "refactor: apply the EventBooking vocabulary mapping".

### Task 3: Remove what the decisions retire

**Files:**
- Delete: the confirmed-slot import handler and parser (JointBooking
  `Application/Slots/ImportConfirmedSlotsHandler.cs`, `ConfirmedSlotImportParser.cs`) under their
  Task 2 names, the import endpoint, MCP tool, Web upload control and their tests.
- Modify: `Domain/Access/StaffId` (pattern from configuration), `StaffCapability` (remove the import
  capability), `AuditAction` (remove the slot-imported and staff-access-removed members),
  `AttendeeReadinessCode` (remove the group-unassigned code), the `Attendee` aggregate (group is
  always required; delete the legacy-reconciliation path), configuration binding (remove head-office
  address and time-zone keys).

**Interfaces:**
- Produces: `StaffId` parsing takes the pattern as a parameter supplied from `Identity__StaffIdPattern`,
  default `^[A-Z0-9]{1,32}$`; values are trimmed and upper-cased before matching.

- [ ] **Step 1: Write failing tests.**
  - Domain.Tests: `StaffId` accepts `A10023` under the default pattern, upper-cases `a10023`,
    rejects an empty value, rejects 33 characters, and accepts `U123456` under a custom pattern
    `^[UN][0-9]{6}$` while rejecting `A10023` under it.
  - Domain.Tests: creating an `Attendee` without an `AttendeeGroup` is refused.
  - Api.Tests: the OpenAPI document has no import operation; the authorization matrix test has no
    import capability row.
  - Api.Tests: startup does not bind any head-office configuration section (the options type no
    longer exists).
- [ ] **Step 2: Run them.** Expected: FAIL.
- [ ] **Step 3: Implement** the deletions and the configurable pattern. Replace every use of the
  head-office time zone with a temporary single-zone configuration value so the build stays green;
  Task 4 removes it.
- [ ] **Step 4: Run the full suite.** Expected: PASS.
- [ ] **Step 5: Commit** with message "refactor: drop bulk import, head-office config and fixed StaffId format".
- [ ] **Step 6: Open the Phase 0 pull request** with the `narrative-required` label; Narrative
  sections record D5, D6, D8, D9 and D11 taking effect in code. Compute and include the
  `AI-Fingerprint:` footer.

---

## Phase 1 — Domain generalisation

All Phase 1 tasks are pure Domain code and Domain.Tests. No persistence changes; Infrastructure
may need mapping tweaks to keep compiling, but schema work waits for Task 9. Use a fake clock
everywhere.

### Task 4: `EventWindow` with duration and location time zone

**Files:**
- Modify: `src/EventBooking.Domain/Events/EventWindow` (was the slot window).
- Create: `src/EventBooking.Domain/Time/` time-zone resolver port and its result types.
- Create: `src/EventBooking.Infrastructure/Time/` NodaTime implementation.
- Test: `tests/EventBooking.Domain.Tests/Events/EventWindowTests`,
  `tests/EventBooking.Infrastructure.Tests/Time/NodaTimeZoneResolverTests`.

**Interfaces:**
- Produces:
  - `EventWindow` value object: `date` (local date), `startTime` (local time), `durationMinutes`
    (int); derived local end time. Construction validates duration and midnight rules and refuses
    otherwise with a domain error naming the rule.
  - Time-zone resolver port with operations: validate an IANA zone id; resolve a window in a zone
    to a start instant and end instant, or report "gap" or "ambiguous" for either end; report the
    zone abbreviation at an instant; give the local date of an instant in a zone.
  - Window rule helpers taking (window, zone, now): has started (now ≥ start), has ended
    (now ≥ end), is on the event date (local date of now equals `date`).

- [ ] **Step 1: Write failing domain tests** for `EventWindow`: 15, 90 and 720 minutes accepted;
  0, 10, 725 and 735 minutes refused; 20 minutes refused (not a multiple of 15); 23:00 start with
  90 minutes refused (crosses midnight); 22:30 start with 90 minutes refused (ends at 00:00, which
  is the next date); 22:15 start with 90 minutes accepted (ends 23:45); end time derived correctly.
- [ ] **Step 2: Write failing resolver tests** against real IANA data: `Europe/London` 2026-03-29
  01:30 is a gap; `Europe/London` 2026-10-25 01:30 is ambiguous; a window whose *end* falls in the
  gap is reported as gap; `Europe/Dublin` and `Europe/London` give the same instant for the same
  local time in winter; the abbreviation for London in July is BST and in January is GMT; an
  unknown zone id is invalid.
- [ ] **Step 3: Write failing rule tests** with a fake clock: "has started" is true exactly at the
  start instant; "has ended" is true exactly at the end instant; "on the event date" uses the
  location's zone (an instant that is 23:30 UTC on the 13th is the 14th in `Asia/Tokyo`).
- [ ] **Step 4: Run.** Expected: FAIL.
- [ ] **Step 5: Implement** the value object, port and NodaTime adapter. Remove the temporary
  single-zone value introduced in Task 3; callers now pass the `Location`'s zone (until Task 5
  lands, tests supply it directly).
- [ ] **Step 6: Run.** Expected: PASS.
- [ ] **Step 7: Commit** with message "feat(domain): variable-length EventWindow in the location's time zone".

### Task 5: Reference-data aggregates and settings

**Files:**
- Create: `src/EventBooking.Domain/Locations/Location`.
- Modify: `src/EventBooking.Domain/AppointmentTypes/AppointmentType` (Admin-managed; delete the
  JointBooking fixed-id constants), `src/EventBooking.Domain/AttendeeGroups/AttendeeGroup` and
  `AttendeeGroupRequirement` (delete the seeded-id constants), `src/EventBooking.Domain/Settings/SystemSettings`.
- Create: a shared code value rule in `src/EventBooking.Domain/Common/` for canonical codes.
- Test: `tests/EventBooking.Domain.Tests/Locations/`, `AppointmentTypes/`, `AttendeeGroups/`, `Settings/`.

**Interfaces:**
- Produces:
  - Canonical code rule: trims, upper-cases, accepts only uppercase snake case (letters, digits,
    underscores, starting with a letter), enforces a per-entity maximum length.
  - `Location`: create (code ≤ 50, name ≤ 100, address ≤ 500, valid zone), rename, edit address,
    change zone given "blocking counts" (open proposals, future active events) — refused when
    either is non-zero, deactivate given the same blocking counts, reactivate; `version`.
  - `AppointmentType`: create (code ≤ 50, name ≤ 100), rename, deactivate given blocking counts
    (open proposals listing it, future active events listing it, active groups mapping it),
    reactivate; `version`.
  - `AttendeeGroup`: create (code ≤ 50, name ≤ 200, at least one requirement), rename, replace the
    requirement set given (member count with an active original `Booking`) — refused unless the
    new set equals the old one, deactivate given member count, reactivate; `version`. Returns
    whether the set actually changed.
  - `SystemSettings`: `inviteExpiryDays` 1–60 default 7, `maxAutoRetryCount` 0–10 default 2,
    `inviteOptionCount` 1–5 default 3; `version`.
  - A domain "in use" error carrying named blocking counts, used by every refusal above.

- [ ] **Step 1: Write failing tests** for each rule in the Interfaces block, one test per rule,
  plus: code `london_hq` becomes `LONDON_HQ`; code `1ABC` and `A-B` are refused; code cannot be
  changed after creation (no operation exists — assert by reflection that the code property has no
  public setter); a requirement set with a duplicate type or zero types is refused; replacing a set
  with the same types in a different order counts as unchanged and is allowed even with blocking
  members.
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** with message "feat(domain): Admin-managed Location, AppointmentType and AttendeeGroup".

### Task 6: N-type negotiation

**Files:**
- Modify: `src/EventBooking.Domain/Events/EventProposal`, `ProposalAcceptance`, `Event`.
- Create: `src/EventBooking.Domain/Events/EventProposalAppointmentType`.
- Test: `tests/EventBooking.Domain.Tests/Events/EventProposalTests`.

**Interfaces:**
- Consumes: `EventWindow` (Task 4), `Location` and `AppointmentType` activity (Task 5).
- Produces:
  - Propose: inputs are location (with active flag and zone), window, now, the listed types (each
    with active flag and "has current Manager"), the proposer's type, the proposer's user id and
    headcount. It collects **every** failure (not just the first) and refuses with the full list:
    inactive location; window invalid, gap/ambiguous, or not in the future; list empty, over 20 or
    duplicated; listed type inactive or without a Manager (naming the type codes); proposer's type
    missing; headcount outside 1–1000. On success: status `Open`, the listed types, and the
    proposer's acceptance.
  - Record acceptance (type, manager user id, headcount): only for a listed type; insert or
    replace in place; reports whether the value changed. When distinct accepted types equal listed
    types, the proposal becomes `Confirmed` and returns a new `Event` (same location and window,
    status `Active`) with one `EventCapacity` per listed type,
    `totalHeadcount = remainingCapacity = headcount`.
  - Single-type proposals confirm during propose.
  - Withdraw acceptance: refused for the proposer's type.
  - Withdraw proposal: allowed only for the proposer's type (not a specific user — FR-2.10).
  - Withdraw by the system once the window has started.
  - Every mutation on a non-`Open` proposal refuses with a "proposal not open" error carrying the
    current status.
  - The type list has no mutation operation after creation.

- [ ] **Step 1: Write failing tests:** a 3-type proposal is `Open` with 1 of 3 accepted; accepting
  the 2nd leaves it `Open`; accepting the 3rd confirms and produces 3 capacity rows with each
  Manager's own headcount; a 1-type proposal confirms immediately; revising a headcount to the same
  value reports "unchanged"; accepting for an unlisted type is refused; withdrawing the proposer's
  acceptance is refused; withdrawing another type's acceptance then re-accepting works; a proposal
  with every failure at once reports all of them; acceptance on `Confirmed` and on `Withdrawn`
  both refuse with the status; a successor Manager (different user id, same type) can withdraw
  the proposal.
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement,** porting JointBooking's slot proposal logic and replacing its three
  fixed types with the listed set.
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** with message "feat(domain): negotiate an EventProposal across any number of types".

### Task 7: Capacity generalised to N rows

**Files:**
- Modify: `src/EventBooking.Domain/Events/Event`, `EventCapacity`.
- Test: `tests/EventBooking.Domain.Tests/Events/EventCapacityTests`, EventTests.

**Interfaces:**
- Produces:
  - `EventCapacity`: decrement (refuses at 0 with a "capacity exhausted" error naming the type),
    increment (refuses above total), adjust total to a new value given the active-booking count for
    that type (refuses below it, returning the minimum; applies the same delta to remaining;
    reports "unchanged" on equal value; refuses a non-positive value or above 1000).
  - `Event`: charge a set of required types (all-or-nothing: checks every row before changing
    any), release a set of types, cancel (refused once started, via Task 4's rule), and a pure
    function returning the lock order for a set of (event, type) pairs: ascending event id, then
    ascending type id.

- [ ] **Step 1: Write failing tests:** charging {IND} on an event listing {MED, FIT, IND} touches
  only IND; charging {MED, IND} where IND is 0 changes nothing and names IND; charging a type the
  event does not list is refused; release restores exactly the charged types; adjusting 6 → 4 with
  3 active bookings gives remaining −2 applied; adjusting to 2 with 3 active bookings is refused
  with minimum 3; cancel after start is refused; the lock-order function sorts a shuffled input.
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** with message "feat(domain): N-row EventCapacity charging only required types".

### Task 8: Invites with locations, and the closed `AttendeeStatus` table

**Files:**
- Modify: `src/EventBooking.Domain/Invites/Invite`, `src/EventBooking.Domain/Attendees/Attendee`.
- Create: `src/EventBooking.Domain/Invites/InviteLocation`.
- Test: `tests/EventBooking.Domain.Tests/Invites/`, `tests/EventBooking.Domain.Tests/Attendees/AttendeeStatusTransitionTests`.

**Interfaces:**
- Produces:
  - `Invite` creation takes 1–50 location ids (duplicates refused), requirements, options, expiry
    instant, retry count, and an optional recovery-of booking id; it stores them as
    `InviteLocation`, `InviteRequirement` and `InviteOption`; `tokenVersion` starts at 1.
  - A "reissue" factory that copies the originating invite's location set (used by expiry re-issue,
    top-up and event cancellation).
  - A recovery factory whose default location set is the original booking's location, plus any
    additional locations supplied.
  - `Attendee` status changes only through named transition methods, one per row of
    [01 — AttendeeStatus](../../design/01-domain-model.md#attendeestatus). Any other transition is
    refused. `statusChangedAt` is stamped on every change.

- [ ] **Step 1: Write failing tests:** an invite with zero or 51 locations is refused; a reissue
  carries the same location set and `retryCount + 1`; a recovery invite defaults to the original
  booking's location and unions additional ones without duplicates; a table-driven test enumerating
  **every** (from, to) pair of `AttendeeStatus` values asserts that exactly the legal rows succeed
  and all others refuse; recovery operations leave `AttendeeStatus` unchanged.
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** the whole Domain.Tests project. Expected: PASS.
- [ ] **Step 5: Commit** with message "feat(domain): location-restricted invites and closed attendee transitions".
- [ ] **Step 6: Open the Phase 1 pull request** (decision-bearing: records D15 in code and the
  N-type negotiation model).

---

## Phase 2 — Persistence

### Task 9: Fresh schema and database roles

**Files:**
- Delete: every ported migration under `src/EventBooking.Infrastructure/Persistence/Migrations/`.
- Modify: `Persistence/EventBookingDbContext` and every entity configuration under
  `Persistence/Configurations/`.
- Create: configurations for `Location`, `EventProposalAppointmentType`, `InviteLocation`; one new
  initial migration; a SQL script for database roles in `Persistence/Sql/roles.sql`, applied by
  the SeedData CLI before migrating.
- Test: `tests/EventBooking.Infrastructure.Tests/Persistence/SchemaTests`.

**Interfaces:**
- Produces: snake_case table and column names read off ontology names (`event_capacity`,
  `event_proposal`, `invite_location`, and so on). Constraints:
  - `event_capacity`: primary key (`event_id`, `appointment_type_id`); check
    `remaining_capacity >= 0 AND remaining_capacity <= total_headcount AND total_headcount > 0`
    named `ck_event_capacity_bounds`.
  - `event`: unique `proposal_id` (not null); `start_utc` column; index on (`status`,
    `location_id`, `start_utc`).
  - `event_proposal_appointment_type`: primary key (`proposal_id`, `appointment_type_id`).
  - `proposal_acceptance`: primary key (`proposal_id`, `appointment_type_id`).
  - Unique `code` on `location`, `appointment_type`, `attendee_group`; unique lower-cased `email`
    on `attendee`; unique `staff_id` on `staff_identity`.
  - Partial unique index enforcing at most one Manager scope per `appointment_type_id` on
    `staff_access_profile`.
  - `version` concurrency token on `location`, `appointment_type`, `attendee_group`,
    `system_settings`, `staff_access_profile`, `booking_appointment`.
  - `email_log` has `claimed_at` and a claim-count column.
  - Roles: a migration owner role, and an application role with DML on every table except
    `audit_log`, where it has only `INSERT` and `SELECT`.

- [ ] **Step 1: Write failing Testcontainers tests:** the migration applies to an empty
  PostgreSQL 16; inserting a capacity row with remaining −1, remaining above total, or total 0 each
  fails on `ck_event_capacity_bounds`; a second `event` with the same `proposal_id` fails; a second
  Manager profile for the same type fails; as the application role, `UPDATE` and `DELETE` on
  `audit_log` are denied while `INSERT` succeeds; a stale `version` update on `location` raises a
  concurrency exception.
- [ ] **Step 2: Run** `dotnet test tests/EventBooking.Infrastructure.Tests --filter SchemaTests`.
  Expected: FAIL.
- [ ] **Step 3: Implement** the configurations, generate the single initial migration, write the
  roles script.
- [ ] **Step 4: Run.** Expected: PASS. Then run the full suite; fix ported Infrastructure tests
  that referenced old tables.
- [ ] **Step 5: Commit** with message "feat(persistence): fresh initial schema with capacity and role constraints".

### Task 10: Unit of work, ordered lock helpers and the concurrency harness

**Files:**
- Modify: `Persistence/UnitOfWork`, repositories for `EventProposal`, `Event`, `Attendee`.
- Create: `Persistence/Locking/` lock helpers.
- Test: `tests/EventBooking.Infrastructure.Tests/Concurrency/` (new harness).

**Interfaces:**
- Consumes: Task 7's lock-order function.
- Produces: unit-of-work lock helpers — lock attendee by id; lock proposal by id; lock events by
  ids (ascending); lock capacity rows for (event id, type ids) ordered by
  (`event_id`, `appointment_type_id`) with `FOR UPDATE`. The unit of work records the highest lock
  level taken in the current transaction and throws (in Debug and in tests) if a lower-level lock
  is requested after a higher one. Repositories expose "load with lock mode: none, update, skip
  locked".

- [ ] **Step 1: Write a failing unit test** that requesting an attendee lock after a capacity lock
  throws the lock-order violation.
- [ ] **Step 2: Write failing harness tests** (real PostgreSQL, parallel tasks, each in its own
  connection):
  - 40 parallel bookings on one event with MED=10, FIT=10, IND=10, where requirement subsets are
    {MED, IND}, {IND}, {FIT, IND}, {MED, FIT, IND} in rotation: exactly 10 succeed, the others
    report capacity exhausted, no row goes below 0, and no PostgreSQL deadlock (SQLSTATE 40P01)
    is raised.
  - Two events, bookings taking locks across both in shuffled request order: no deadlock.
  - A capacity adjustment from 10 to 5 racing 8 bookings: final remaining equals
    total − successful bookings, and the adjustment is refused if bookings exceed 5 at the time it
    runs.
  The harness drives the Application handlers once they exist; for now it drives a minimal
  in-test booking routine built only on the lock helpers, replaced by the real handler in Task 15.
- [ ] **Step 3: Run.** Expected: FAIL.
- [ ] **Step 4: Implement** the helpers and the lock-level tracking.
- [ ] **Step 5: Run.** Expected: PASS, repeated 20 times in a loop to shake out flakiness.
- [ ] **Step 6: Commit** with message "feat(persistence): ordered row-lock helpers and concurrency harness".

### Task 11: Invite eligibility query

**Files:**
- Modify: the ported eligible-slot finder, moved to
  `src/EventBooking.Infrastructure/Persistence/Queries/EventEligibilityQuery` behind the
  Application port.
- Create: the port `Application/Abstractions/` event eligibility.
- Test: `tests/EventBooking.Infrastructure.Tests/Queries/EventEligibilityQueryTests`.

**Interfaces:**
- Produces: the port operation FindEligibleEvents(required type ids, location ids, excluded event
  ids, count, as-of instant) returning ordered event ids; and CountEligibleEvents with the same
  filters minus count (for the invite dialog). One SQL statement each, relational division as in
  [04 — Invite selection](../../design/04-solution-architecture.md#invite-selection). The `Event`
  repository writes `start_utc` in the same transaction as the insert, computed with the Task 4
  resolver.

- [ ] **Step 1: Write failing tests:** an event listing {MED, FIT, IND} is eligible for {IND}; an
  event listing {IND} is not eligible for {MED, IND}; an event with IND remaining 0 is not eligible
  for {IND} but is for {MED}; a `Cancelled` event is excluded; a started event is excluded; an
  event at a location outside the set is excluded; excluded ids are honoured; ordering is by UTC
  instant so a 09:00 Dublin event and a 09:30 London event on a summer date sort by instant, and
  equal instants sort by event id; `count` limits results; the count query agrees with the list
  query.
- [ ] **Step 2: Write a performance test** (tagged so it runs only on demand): 2 000 active events,
  10 required types, p95 below 50 ms over 100 runs (NFR-P2), and assert via `EXPLAIN` that the
  (`status`, `location_id`, `start_utc`) index is used.
- [ ] **Step 3: Run.** Expected: FAIL.
- [ ] **Step 4: Implement.**
- [ ] **Step 5: Run.** Expected: PASS.
- [ ] **Step 6: Commit** with message "feat(persistence): relational-division invite eligibility query".
- [ ] **Step 7: Open the Phase 2 pull request** (decision-bearing: D12, fresh schema).

---

## Phase 3 — Application

Every handler here follows the pattern in
[04 — Commands, queries and transactions](../../design/04-solution-architecture.md#commands-queries-and-transactions)
and demands exactly one `StaffCapability`. Application.Tests use in-memory fakes of the ports;
anything that depends on locking or SQL is also covered in Infrastructure.Tests.

### Task 12: Reference-data and settings handlers

**Files:**
- Create: `src/EventBooking.Application/ReferenceData/` handlers for create, update and
  activate/deactivate of `Location`, `AppointmentType` and `AttendeeGroup`, plus list queries.
- Modify: `src/EventBooking.Application/Settings/` save handler.
- Create: read-model queries in `Infrastructure/Persistence/Queries/` for blocking counts and lists.
- Test: `tests/EventBooking.Application.Tests/ReferenceData/`, `Settings/`; Infrastructure tests for
  blocking-count queries and for ReplaceAttendeeGroupRequirements.

**Interfaces:**
- Consumes: Task 5 aggregates; Task 8 transitions; unit of work.
- Produces handlers (all demand `ManageReferenceData` except reads and settings): CreateLocation,
  UpdateLocation, CreateAppointmentType, UpdateAppointmentType, CreateAttendeeGroup,
  UpdateAttendeeGroup (which runs ReplaceAttendeeGroupRequirements when the set changes),
  SaveSystemSettings (`ManageSettings`), ListLocations, ListAppointmentTypes (with current Manager
  display name, falling back to `StaffId`), ListAttendeeGroups (with requirement type ids and
  member count) — reads open to any staff member. Results carry the updated `version`. Refusals map
  to `in-use` with blocking counts, `requirements-locked` with the blocking member count, and
  `version-conflict` with the current state.

- [ ] **Step 1: Write failing tests:**
  - each create and update writes exactly one of `LocationCreated`, `LocationUpdated`,
    `AppointmentTypeCreated`, `AppointmentTypeUpdated`, `AttendeeGroupCreated`,
    `AttendeeGroupUpdated`, `SystemSettingsChanged`, with old and new values and no names or
    emails;
  - a zone change on a location with 1 open proposal and 2 future events returns `in-use` with
    those counts;
  - deactivating a type mapped by an active group returns `in-use` naming the group count;
  - a group requirement change with 2 members, one holding a pending initial `Invite`: both
    members' `AttendeeRequirement`s are re-derived, the invite becomes `Superseded`, both become
    `NotYetInvited`, all in one transaction;
  - the same change with one member holding an active original `Booking` is refused with count 1
    and nothing changes;
  - a stale `expectedVersion` returns the current state;
  - settings outside range are refused per field; valid settings do not alter existing invites.
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement.** ReplaceAttendeeGroupRequirements takes each member's attendee lock in
  id order before changing anything.
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** with message "feat(app): reference-data and settings use cases".

### Task 13: Negotiation and capacity-adjustment handlers

**Files:**
- Modify: ported propose, accept, withdraw-acceptance, withdraw-proposal, board and
  capacity-adjustment handlers, moved to `src/EventBooking.Application/Negotiation/`.
- Test: `tests/EventBooking.Application.Tests/Negotiation/`,
  `tests/EventBooking.Infrastructure.Tests/Concurrency/AcceptanceRaceTests`.

**Interfaces:**
- Consumes: Task 6 domain; Task 10 proposal lock; Task 11 `start_utc` write.
- Produces handlers under `ManageEventNegotiation`: ProposeEvent (returns status and event id if
  confirmed; `validation-failed` with every failure and offending type codes), RecordAcceptance,
  WithdrawAcceptance, WithdrawProposal, GetNegotiationBoard (FR-2.13), AdjustEventCapacity
  (caller's own type only; `capacity-below-bookings` with `minimum` and current values). Every
  write locks the proposal row first. The caller's scoped type is the only type they can act for.

- [ ] **Step 1: Write failing application tests:** audit sequence for a 1-type proposal is
  `ProposalCreated`, `AcceptanceRecorded`, `EventConfirmed`; revising to the same headcount writes
  no audit; the board for a MED Manager never contains another type's headcount (assert the
  serialised result has no FIT or IND headcount anywhere); the board excludes proposals not listing
  MED; a proposal-not-open conflict returns the current status and no audit; adjusting another
  type's capacity is forbidden.
- [ ] **Step 2: Write a failing race test:** a 4-type proposal with 1 accepted; three Managers
  accept concurrently 50 times over fresh proposals; each run yields exactly one `Event`, exactly
  4 capacity rows, and a `Confirmed` proposal.
- [ ] **Step 3: Run.** Expected: FAIL.
- [ ] **Step 4: Implement.**
- [ ] **Step 5: Run.** Expected: PASS.
- [ ] **Step 6: Commit** with message "feat(app): N-way negotiation with serialised confirmation".

### Task 14: Invite engine

**Files:**
- Modify: ported invite issuer, trigger-invite, expire-invites and option top-up logic, moved to
  `src/EventBooking.Application/Invites/`.
- Test: `tests/EventBooking.Application.Tests/Invites/`.

**Interfaces:**
- Consumes: Task 8 invite factories and transitions; Task 11 port; `SystemSettings`.
- Produces: InviteAttendee(attendee id, location ids) returning `Invited` or `AwaitingAvailability`
  (`insufficient-events`); CountEligibleEventsForAttendee(attendee id, location ids); an internal
  InviteIssuer used by every path that creates an `Invite` (initial, re-invite, reissue after
  expiry, top-up, replacement after cancellation, recovery), which always supersedes the journey's
  `Pending` invite and stages the right `EmailTemplate`; ExpireInvites (sweep step, per item); and
  TopUpOptions (called on view and on confirm).

- [ ] **Step 1: Write failing tests:**
  - inviting with 3 eligible events and `inviteOptionCount` 3 creates the invite with 3 options in
    UTC order, `InviteLocation` equal to the selection, `expiresAt` = now + `inviteExpiryDays`,
    audit `InviteCreated`, and one `Pending` `AttendeeInvite` `EmailLog`;
  - with 2 eligible events: no invite, status `AwaitingAvailability`, `insufficient-events`;
  - an inactive location in the selection is refused;
  - re-invite from `NoResponseNeedsFollowUp` with new locations works (FR-5.9);
  - expiry with retry below the limit reissues with the same `InviteLocation`s and `retryCount`+1
    and stages `AttendeeReinvite`; at the limit the attendee becomes `NoResponseNeedsFollowUp`;
    a failed reissue also yields `NoResponseNeedsFollowUp` with a reason in the audit (FR-5.7);
  - the expiry transition works while the same invite is already tracked in the unit of work;
  - top-up replaces a full option with the next eligible event (audit `InviteOptionReplaced`),
    excludes events already offered and the just-cancelled booking's event, and sets
    `NoResponseNeedsFollowUp` when still short.
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** with message "feat(app): location-restricted invite engine".

### Task 15: Booking, cancellation and event cancellation

**Files:**
- Modify: ported confirm-booking, manage-cancel, coordinator-cancel-booking and cancel-event
  handlers under `src/EventBooking.Application/Bookings/` and `Events/`.
- Modify: `tests/EventBooking.Infrastructure.Tests/Concurrency/` harness to drive the real
  ConfirmBooking handler.
- Test: `tests/EventBooking.Application.Tests/Bookings/`, `Events/`.

**Interfaces:**
- Consumes: Tasks 7, 10, 14.
- Produces: ConfirmBooking(book token, event id) → booking and manage link, or
  `capacity-exhausted`; idempotent on a `Used` invite. CancelBookingByAttendee(manage token,
  request new time) → outcome `reinvited`, `reinvitePending`, `noEligibleEvents` or `cancelled`.
  CancelBookingByCoordinator(attendee id, booking id, confirm) (`ManageAttendees`, two-step with
  `confirmation-required` and active-booking count). CancelEvent(event id, confirm) (`CancelEvent`;
  a Manager only for events listing their type) → counts cancelled, reinvited, awaiting
  availability. All refuse with `window-started` once the event has started in its location's
  zone.

- [ ] **Step 1: Write failing tests:**
  - confirming for an attendee needing {IND} on an event listing {MED, FIT, IND} decrements only
    IND, creates one `BookingAppointment`, sets the invite `Used` and the attendee `Booked`, audits
    `BookingCreated` and one `CapacityDecremented`, and stages `BookingConfirmation`;
  - a second confirm returns the same booking;
  - capacity exhausted on any required type changes nothing;
  - attendee cancel without a new time sets `NotYetInvited` and releases capacity;
  - attendee cancel with a new time while a recovery invite is pending supersedes it (FR-9.6);
  - CancelEvent without confirm returns the booking count; with confirm cancels every active
    booking in attendee-id order, reissues from each original `InviteLocation` set, stages
    `EventCancelledRebookingNeeded` with the "replacement created" flag in its context, and
    reports counts;
  - a FIT Manager cancelling an event not listing FIT is forbidden;
  - every cancel after the start instant returns `window-started`.
- [ ] **Step 2: Switch the concurrency harness** to the real handler and re-run Task 10's scenarios
  plus: CancelEvent racing bookings on the same event never deadlocks.
- [ ] **Step 3: Run.** Expected: FAIL.
- [ ] **Step 4: Implement.**
- [ ] **Step 5: Run** the application and infrastructure suites. Expected: PASS.
- [ ] **Step 6: Commit** with message "feat(app): booking and cancellation over N capacity rows".

### Task 16: Recovery and the appointment workspace

**Files:**
- Modify: ported start-recovery, cancel-recovery, recovery requirement selector, and workspace
  handlers and queries under `src/EventBooking.Application/Recovery/` and `Appointments/`.
- Test: `tests/EventBooking.Application.Tests/Recovery/`, `Appointments/`; Infrastructure tests for
  the workspace read model.

**Interfaces:**
- Produces: StartRecovery(attendee id, additional location ids), CancelRecoveryInvite,
  ListWorkspaceEvents(location id optional) — events listing the caller's type, end instant between
  7 days ago and 14 days ahead, grouped by location, default the nearest current or next;
  GetWorkspaceRoster(event id); SetAppointmentStatus(appointment id, target status, expected
  version); DownloadRoster(event id) as CSV. All under `ConductAppointments`, scoped to the
  caller's type across every location.

- [ ] **Step 1: Write failing tests:**
  - recovery defaults to the original booking's location and adds extra ones; it snapshots only
    recoverable types; a second concurrent recovery is refused with `recovery-active`;
  - check-in is allowed only on the event's local date in the location's zone (test a Dublin event
    with the clock at 23:30 UTC the day before in summer — allowed, since it is 00:30 local);
  - `NoShow` is allowed only after the end instant;
  - correcting `NoShow` to `Expected` is refused while a later recovery is pending;
  - a recovery booking concludes when all its appointments are terminal (`RecoveryBookingConcluded`);
  - the workspace roster contains only name, email, status, timestamps and `version`, never ids of
    attendees, bookings or invites (assert on the serialised JSON);
  - the roster CSV neutralises cells starting with `=`, `+`, `-`, `@`, tab and carriage return;
  - an AppointmentStaff profile for MED never sees FIT rows.
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** with message "feat(app): recovery and workspace across locations".

### Task 17: Staff identity, role sync and authorization

**Files:**
- Modify: `src/EventBooking.Api.Auth/` (provider-neutral OIDC; claim names from configuration),
  ported caller accessor, staff identity recorder, role-sync and capability mapping under
  `src/EventBooking.Application/Access/`, staff-access scope handler.
- Test: `tests/EventBooking.Api.Tests/` auth tests, `tests/EventBooking.Application.Tests/Access/`.

**Interfaces:**
- Produces: configuration keys `Auth__Authority`, `Auth__Audience`, `Auth__Claims__StaffId`,
  `Auth__Claims__Name`, `Auth__Claims__Roles`, `Identity__StaffIdPattern`. A single authorization
  pipeline shared by REST and MCP, evaluating in the order of
  [06 — Authorization](../../design/06-security-and-authentication.md#authorization). The
  capability matrix as a single table in code, matching the design exactly, including
  `ManageReferenceData` for Admin and scoped `CancelEvent` for Manager. SetStaffScope(staff user id,
  type id or null, expected version) returning the displaced Manager's display name when any.
  GetMe returning display name, `StaffId`, roles, scope and capabilities, or a no-role response.

- [ ] **Step 1: Write failing tests:**
  - a token with no `staff_id` gets 403 everywhere except `/api/me`, which explains the problem and
    writes no `StaffIdentity`;
  - a `staff_id` not matching the configured pattern behaves the same;
  - custom claim names from configuration are honoured;
  - role drift updates the profile and audits `StaffRolesSynced` as `System` before authorizing the
    same request; a token with no roles removes the profile; a sync removing the last Admin is
    refused and logged;
  - a Manager role with a null scope gets no capability at all;
  - a generated authorization-matrix test enumerates every capability × role combination against
    the design table;
  - assigning MED Manager to Jo while Sam holds it clears Sam atomically and names Sam; clearing
    the only MED Manager is allowed;
  - `StaffIdentity` is refreshed at most every 15 minutes.
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** with message "feat(auth): provider-neutral OIDC with per-request role sync".

### Task 18: Notification outbox and templates

**Files:**
- Create: `src/EventBooking.Infrastructure/Email/OutboxDispatcher` (hosted service),
  `Email/EmailComposer` (pure), claim query.
- Modify: SMTP sender (outcomes, 30 s timeout), `EmailLog` handling, email-retry handler.
- Test: `tests/EventBooking.Infrastructure.Tests/Email/`, `tests/EventBooking.Application.Tests/Notifications/`.

**Interfaces:**
- Produces: the email sender port returns sent, transient failure or permanent failure and never
  throws for a provider error. The composer maps (`EmailTemplate`, context) to subject, text and
  HTML exactly per [02 — Email content](../../design/02-functional-requirements.md#email-content),
  with `{types}` in `code` order and `{window}` in the location's zone with abbreviation. The
  dispatcher claims up to 20 rows with `FOR UPDATE SKIP LOCKED`, reclaims claims older than 5
  minutes, keeps a transient failure `Pending` for up to 3 claims with backoff, then `Failed`;
  audits `InviteSent` for invite emails. RetryEmail(attendee id) creates a new `Pending` row and
  marks the old one `Resolved`. Links are regenerated from entity id and token version at send
  time.

- [ ] **Step 1: Write failing tests:**
  - composer golden tests for all four templates, including the recovery singular and plural
    wording and both `EventCancelledRebookingNeeded` endings; a London window in July renders
    "BST"; types render in code order regardless of input order; the HTML part carries the same
    lines;
  - two dispatcher instances against one database never send the same row twice;
  - a row claimed 6 minutes ago by a crashed dispatcher is reclaimed;
  - transient × 3 ends `Failed` with no error text stored;
  - a resent email carries the same link as the original;
  - `EmailLog` rows never contain an address, name, token or URL (assert over all columns).
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement.** Remove any in-transaction send left from the port.
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** with message "feat(email): durable outbox dispatcher and location-aware templates".

### Task 19: Background jobs

**Files:**
- Modify: `src/EventBooking.Api/InviteSweepService` (15-minute hosted service).
- Create: `src/EventBooking.Infrastructure/Jobs/AdvisoryLock`, sweep steps under
  `src/EventBooking.Application/Jobs/`.
- Test: `tests/EventBooking.Infrastructure.Tests/Jobs/`.

**Interfaces:**
- Produces: a sweep run that, under `pg_try_advisory_lock`, runs three steps each item in its own
  transaction: ExpireInvites (Task 14), WithdrawStartedProposals (FR-2.12, audited as `System`),
  ConcludeRecoveryBookings (FR-9.5). Interval from `Jobs__SweepInterval`, default 15 minutes.
  Metrics: runs, failures, items processed.

- [ ] **Step 1: Write failing tests:** two concurrent sweep runs — the second skips; an item that
  throws is logged and counted and the rest of the run continues; the next run proceeds after a
  failed run; an `Open` proposal whose window started 1 minute ago is withdrawn with actor `System`;
  a recovery booking with all appointments terminal is concluded.
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** with message "feat(jobs): advisory-locked invite sweep".

### Task 20: Dashboards, attendee list and audit search

**Files:**
- Modify: ported dashboard, attendee-list, readiness and audit-search queries and handlers.
- Test: Application and Infrastructure tests for each query.

**Interfaces:**
- Produces: GetDashboards(location id optional) — one query returning the three tabs with counts
  (FR-13.1) and failed/pending email counts; ListAttendees with cursor, status, group, readiness
  and name/email prefix filters, readiness and latest delivery status; GetReadiness; SearchAudit
  with the FR-12.2 filters, keyset-paginated, scoped by bucket (FR-12.3, reference data and
  settings in the event bucket); attendee and event history. Every attendee read model refuses an
  Admin-shaped caller.

- [ ] **Step 1: Write failing tests:** the Events tab is bounded to end instants between 7 days ago
  and 60 days ahead and filters by location; awaiting-availability rows show the required type
  codes; an Admin calling ListAttendees, GetDashboards, GetReadiness or attendee history is refused
  by the read model even when the capability check is bypassed in the test; an audit search by a
  caller with only `ViewEventAudit` never returns `Attendee` rows; audit pagination returns
  non-overlapping pages under concurrent inserts; the attendee list with 50 000 rows returns a page
  in under 300 ms (on-demand performance test).
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** with message "feat(app): bounded dashboards and bucketed audit search".
- [ ] **Step 6: Open the Phase 3 pull request** (decision-bearing: D13 outbox, D14 token lifecycle
  as implemented, D15).

---

## Phase 4 — API and MCP

### Task 21: API conventions

**Files:**
- Modify: `src/EventBooking.Api/Endpoints/ResultResponses` (problem details), Program setup,
  OpenAPI configuration, rate limiting, CORS, health, logging, metrics.
- Create: `src/EventBooking.Api/Pagination/` cursor codec; `Idempotency/` middleware.
- Test: `tests/EventBooking.Api.Tests/Conventions/`.

**Interfaces:**
- Produces: an opaque cursor codec (base64url of the sort key plus HMAC; tampered cursors return
  `validation-failed`); the full error catalogue mapping; the event time representation (`date`,
  `startTime`, `durationMinutes`, `startLocal`, `endLocal`, `startUtc`, `endUtc`, `timeZoneId`,
  `zoneAbbreviation`) as one shared contract; `_links` generation from the caller's capabilities;
  `Idempotency-Key` retained 24 hours on create endpoints; rate limits of 30 per minute per IP and
  10 per minute per token prefix on attendee routes and 300 per minute per staff member, with
  forwarded headers trusted only from the configured proxy network; `/health/live`,
  `/health/ready`, `/metrics`; structured JSON logs with correlation id propagated into outbox
  rows; startup validation of every † setting in
  [04 — Configuration](../../design/04-solution-architecture.md#configuration), rejecting known
  placeholder signing keys and keys under 32 bytes.

- [ ] **Step 1: Write failing tests** for each item: a tampered cursor; each error `type` slug
  appears with the right status for a representative request; a 1-page, 3-page and empty
  pagination walk; the same `Idempotency-Key` twice returns the first result; the 31st attendee
  request in a minute from one IP gets 429 with `Retry-After`; a forwarded IP from outside the
  proxy network is ignored; startup fails with a missing `Portal__BaseUrl` and with a placeholder
  signing key; logs for a booking request contain no email address or token.
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** with message "feat(api): pagination, error catalogue, rate limits and config validation".

### Task 22: Endpoint catalogue

**Files:**
- Create or modify endpoint files under `src/EventBooking.Api/Endpoints/`: LocationEndpoints,
  AppointmentTypeEndpoints, AttendeeGroupEndpoints, SettingsEndpoints, StaffAccessEndpoints,
  EventProposalEndpoints, EventEndpoints, AttendeeEndpoints, DashboardEndpoints,
  AuditEndpoints, AppointmentWorkspaceEndpoints, BookingEndpoints, ManageEndpoints,
  MeEndpoints, ApiDiscoveryEndpoints.
- Test: one endpoint test class per file, plus the OpenAPI snapshot.

**Interfaces:**
- Consumes: every Phase 3 handler.
- Produces: exactly the routes, bodies and capabilities in [05](../../design/05-api-design.md),
  each with an `operationId` and an `x-mcp-tool` extension naming its snake-case tool. The `/api`
  link index lists every top-level resource.

- [ ] **Step 1: Write a failing catalogue test** that parses the endpoint tables in
  `docs/design/05-api-design.md` and asserts that the OpenAPI document has exactly that set of
  (method, path) pairs, and that each has the stated capability requirement.
- [ ] **Step 2: Write failing endpoint tests** per file: happy path, each documented refusal, and
  the forbidden case per the matrix. Include: ProposeEvent returns 201 `Open` for 3 types and 201
  `Confirmed` with `eventId` for 1; confirm returns 409 `capacity-exhausted` with the example body
  shape from the design; two-step cancel returns `confirmation-required` with `consequence`.
- [ ] **Step 3: Run.** Expected: FAIL.
- [ ] **Step 4: Implement.** Endpoints only translate HTTP to handler calls; no business rule lives
  in an endpoint.
- [ ] **Step 5: Run.** Expected: PASS. Refresh the OpenAPI snapshot and review its diff.
- [ ] **Step 6: Commit** with message "feat(api): full EventBooking endpoint catalogue".

### Task 23: MCP parity

**Files:**
- Modify: `src/EventBooking.Mcp/Tools/` (split by area to match the endpoint files).
- Test: `tests/EventBooking.Mcp.Tests/ParityTests`, ListShapeTests.

**Interfaces:**
- Produces: one tool per staff operation, named from `x-mcp-tool`, calling the same handler under
  the same pipeline. List tools return id, name or code, and status for every row. Attendee-token
  routes have no tool.

- [ ] **Step 1: Write failing tests:** `tools/list` equals the OpenAPI operation set minus anonymous
  and token routes; every list tool's rows have id, name-or-code and status; a CSV import through
  MCP over 1000 rows is refused identically to REST; a Manager with a null scope is refused through
  MCP.
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** with message "feat(mcp): tool parity with the REST catalogue".
- [ ] **Step 6: Open the Phase 4 pull request.**

---

## Phase 5 — Web

Web never references server assemblies; client DTOs are duplicated and covered by contract tests
against the OpenAPI snapshot from Task 22.

### Task 24: Neutral theme and design-system components

**Files:**
- Delete: JointBooking's branded tokens, fonts, logos and images from `wwwroot`.
- Create: `src/EventBooking.Web/wwwroot/theme.css` (tokens only), product name and optional logo in
  `appsettings.json`.
- Create: `src/EventBooking.Web/Components/` — StatusBadge, TypeChip, DataTable (with dynamic type
  columns), TypePicker, LocationPicker, TwoStepButton, Banner, CsvImportResult, EventTime.
- Test: `tests/EventBooking.Web.Tests/Components/`.

**Interfaces:**
- Produces component contracts per [03a — Component contracts](../../design/03a-design-system-and-ia.md#component-contracts):
  TypeChip colour is a deterministic hash of `code` over an AA-safe palette; DataTable renders one
  column per listed type in `code` order and collapses to a single "Capacity" cell beyond 5;
  TypePicker locks and pre-selects the caller's type and disables types without a Manager, showing
  why; LocationPicker is single- or multi-select and shows name, short address and zone
  abbreviation; TwoStepButton resets after 10 seconds or navigation and never uses `confirm()`;
  EventTime renders "Tue 14 Oct 2026, 09:30–11:00 BST" plus location name on staff screens.

- [ ] **Step 1: Write failing bUnit tests** for each contract above, plus: no component contains a
  hard-coded colour (a test scans `.razor` and `.css` files outside `theme.css` for hex and rgb
  literals); badges always carry text; live-region announcements fire on banner appearance.
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** with message "feat(web): neutral theme and design-system components".

### Task 25: Admin screens

**Files:**
- Create: `Pages/Admin/Locations.razor`, `AppointmentTypes.razor`, `AttendeeGroups.razor`.
- Modify: `Pages/Settings.razor` (adds `inviteOptionCount`), `Pages/StaffAccess.razor`,
  `Pages/Home.razor`, navigation.
- Test: `tests/EventBooking.Web.Tests/Pages/Admin/`.

**Interfaces:**
- Produces screens per [03b](../../design/03b-screens-and-flows.md) sections Locations, Appointment
  types, Attendee groups, Settings and Staff access, with every listed state. Read-only rendering
  for Coordinator and Manager. Navigation per the matrix in 03a.

- [ ] **Step 1: Write failing tests** per screen for each listed state (loading, empty with the
  exact empty-state copy, editing, conflict retaining values, busy, error), the zone-change control
  disabled with a reason when blocked, the group requirements-change confirmation naming N
  attendees, and staff-access saved confirmation naming a displaced Manager.
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** with message "feat(web): Admin reference-data screens".

### Task 26: Manager and operations screens

**Files:**
- Modify: `Pages/Negotiate.razor` (was the slots page), `Pages/EventOperations.razor` (was the
  confirmed-slots page, now without import), `Pages/Appointments.razor`.
- Test: `tests/EventBooking.Web.Tests/Pages/Negotiation/`, `Operations/`, `Appointments/`.

**Interfaces:**
- Produces the negotiation board and propose dialog (location, date, start, duration in 15-minute
  steps with derived end, type picker, headcount), event operations with location and date filters,
  and the workspace with an event selector grouped by location and no type selector.

- [ ] **Step 1: Write failing tests:** the propose dialog shows the derived end time in the
  location's zone as duration changes; server validation errors render per field; the board shows
  "2 of 3" and never another type's headcount; capacity conflict retains input and shows the
  minimum; cancel event is two-step and disabled after start; the workspace heading names the
  scoped type; check-in is disabled outside the event date and no-show before the end.
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** with message "feat(web): N-type negotiation board and workspace".

### Task 27: Coordinator screens, attendee pages, Help and accessibility

**Files:**
- Modify: `Pages/Attendees.razor`, `Pages/Dashboards.razor`, `Pages/Audit.razor`, `Pages/Book.razor`,
  `Pages/ManageBooking.razor`, `Pages/Help.razor`.
- Rewrite: role guides in `src/EventBooking.Web/wwwroot/help/` (admin, coordinator, manager,
  appointment-staff, attendee) for EventBooking behaviour.
- Create: `tests/EventBooking.Web.E2E/` Playwright project with axe-core over every route, using
  seeded tokens.
- Modify: `.github/workflows/dotnet-build.yml` to run the axe job (or add a separate job) when Web
  changes.

**Interfaces:**
- Produces: the invite dialog with multi-select locations and a live eligible-event count with the
  "only N eligible events" warning; CSV import result; dashboards with a location filter; book page
  listing location name, address, local time and zone; manage page with the four truthful cancel
  outcomes and the post-start state.

- [ ] **Step 1: Write failing tests:** the invite dialog count updates as locations change; the
  import rejection lists line-numbered errors and "Nothing was imported."; the book page removes a
  filled option with the exact copy "That time has just filled up. Please choose another."; each
  cancel outcome renders its own wording; after start the manage page shows the coordinator
  contact and no cancel control; Help shows every held role's guide with a contents list and only
  the attendee guide when anonymous; the guides pass the ontology term check; axe reports zero
  violations on every route at mobile and desktop widths.
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: PASS. Check the attendee bundle is trimmed and compressed.
- [ ] **Step 5: Commit** with message "feat(web): coordinator and attendee flows with location selection".
- [ ] **Step 6: Open the Phase 5 pull request** (decision-bearing: D10 neutral theme).

---

## Phase 6 — Seed and deployment

### Task 28: SeedData CLI and demo dataset

**Files:**
- Modify: `src/EventBooking.SeedData/Program`, DemoSeedSpec, DemoSeeder, DemoInvitationSeeder,
  KeycloakSeeder.
- Test: `tests/EventBooking.SeedData.Tests/`.

**Interfaces:**
- Produces: the CLI flags in [07 — Seed data](../../design/07-deployment.md#seed-data): no flag
  applies roles and migrations only; `--demo` upserts the dataset by natural key, converges
  Keycloak demo users when settings are present, and sends demo invitations; `--reanchor` shifts
  demo dates relative to today; `--reseed` wipes domain tables and recreates the realm, refused
  unless `--demo` and `EVENTBOOKING_ALLOW_RESEED=true`. The dataset exactly as listed there
  (3 locations across 2 zones with one inactive; MED, FIT, IND, ESC without Manager, DOC inactive;
  4 groups; events of 60, 90, 240 and 480 minutes with 1–4 types, one on a daylight-saving change
  date; open proposals at 1 of 2 and 2 of 4; attendees in every status; bookings in every
  appointment status; one completed and one pending recovery; the listed Keycloak users).

- [ ] **Step 1: Write failing tests:** running with no flag sends no email and inserts no domain
  rows; running `--demo` twice yields identical row counts; `--reseed` without the environment flag
  exits non-zero and changes nothing; a coverage test asserts every axis in the dataset list
  (each `AttendeeStatus`, each `BookingAppointmentStatus`, both zones, each duration, the DST-date
  event, the Manager-less type, the always-awaiting group); every demo proposal passes the same
  domain validation as a real one.
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** with message "feat(seed): migrate-only default and generalised demo dataset".

### Task 29: Local Docker Compose stack

**Files:**
- Create: `docker-compose.yml`, `deploy/keycloak/realm-export.json` (realm `eventbooking`, client
  `eventbooking-web`, four roles, `roles`/`staff_id`/`name` mappers, 5-minute access tokens),
  `deploy/web/` (nginx image config), Dockerfiles for api, mcp, web, web-caddy and seed.
- Create: `.github/workflows/compose-smoke.yml`.

**Interfaces:**
- Produces: services and ports per [07 — Local](../../design/07-deployment.md#local-docker-composeyml-at-the-repository-root)
  (postgres 5432, keycloak 8081, mailpit 1025/8025, api 5001, web 5002, mcp 5003, seed under
  profile `seed`), no MinIO. Every image non-root, built from a `.dockerignore`-filtered context.

- [ ] **Step 1: Write the smoke workflow first:** `docker compose config`, bring up the stack, run
  the seed with `--demo`, probe `/health/ready` and `/api`, and assert Mailpit received the demo
  invitations. Actions pinned by SHA.
- [ ] **Step 2: Run it locally** with the documented three commands. Expected: FAIL (no files yet).
- [ ] **Step 3: Implement** the Compose file, realm and Dockerfiles.
- [ ] **Step 4: Run** the three documented commands, sign in as each demo user, and exercise
  propose, accept, invite, book and check-in once. Expected: all succeed; Mailpit shows the emails
  with location and zone.
- [ ] **Step 5: Commit** with message "feat(deploy): local Compose stack without MinIO".

### Task 30: Home-lab deployment

**Files:**
- Create: `deploy/home-lab/docker-compose.yml`, `web/Caddyfile`, `web/Dockerfile`,
  `web/appsettings.json`, `caddy/eventbooking.caddy`, `keycloak/eventbooking-realm.json`,
  `seed/Dockerfile`, `.env.example`, `install.sh`, `README.md`.

**Interfaces:**
- Produces: the file set, variables and install steps in
  [07 — Home lab](../../design/07-deployment.md#home-lab-deployhome-lab); `eventbooking-private`
  as an internal network; the optional `eventbooking-backup` service (nightly `pg_dump`, 14 days);
  security headers (CSP allowing self, API and identity-provider origins and WebAssembly,
  `nosniff`, `no-referrer`, `DENY`); `/mailpit` behind basic auth; `theme.css` mountable.

- [ ] **Step 1: Extend the smoke workflow** to run `docker compose config` on the home-lab file
  with `.env.example`, and to lint `install.sh` with shellcheck.
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement** the files. `install.sh` performs exactly the seven documented steps and
  is idempotent.
- [ ] **Step 4: Rehearse** on a scratch host or VM: install, upgrade to a new tag, restore a backup
  into a fresh volume within the 2-hour RTO; record timings in the README runbook.
- [ ] **Step 5: Run** the smoke workflow. Expected: PASS.
- [ ] **Step 6: Commit** with message "feat(deploy): home-lab deployment with reference install script".

### Task 31: Image and release pipeline

**Files:**
- Create: `.github/workflows/images.yml`.
- Modify: `.github/workflows/dotnet-build.yml` if Testcontainers needs Docker set-up steps.

**Interfaces:**
- Produces: on push to `main` and on `vX.Y.Z` tags, build and push `eventbooking-api`,
  `eventbooking-mcp`, `eventbooking-web`, `eventbooking-web-caddy` and `eventbooking-seed` to GHCR
  tagged `latest` and `sha-<short>` (and the version on tags); attach a self-contained EF Core
  migrations bundle to releases. All actions pinned by SHA.

- [ ] **Step 1: Validate** the workflow with `actionlint`. Expected: clean.
- [ ] **Step 2: Dry-run** the image builds locally for all five images; assert each runs as a
  non-root user (inspect the image user).
- [ ] **Step 3: Commit** with message "ci: publish images and migrations bundle".
- [ ] **Step 4: Open the Phase 6 pull request** (decision-bearing: deployment shape and seed
  defaults).

---

## Phase 7 — Verification and documentation

### Task 32: Load test

**Files:**
- Create: `tests/load/confirm-burst.js` (k6), `tests/load/README.md`, a seed fixture mode for the
  load scenario.

**Interfaces:**
- Produces: the scenario in [08 — Verification](../../design/08-nonfunctional-requirements.md#verification):
  500 invites whose options all include one event with 100 places for a shared type, all 500
  confirmed concurrently.

- [ ] **Step 1: Write the scenario** with thresholds as assertions: exactly 100 `201`s, exactly 400
  `capacity-exhausted` responses, zero 5xx, zero deadlocks in the PostgreSQL log, capacity lock hold
  p95 under 50 ms (from the metric emitted by the booking handler).
- [ ] **Step 2: Run** against the local stack. Expected: all thresholds pass. If not, investigate
  with superpowers:systematic-debugging before changing any code.
- [ ] **Step 3: Commit** with message "test(load): 500-way confirmation burst".

### Task 33: Documentation

**Files:**
- Modify: `README.md` (what EventBooking is, quick start from Task 29, links to the design package).
- Create: `docs/runbooks/demo.md` (demo walkthrough with the seeded users).
- Modify: `deploy/home-lab/README.md` (final operator runbook), Help guides where behaviour changed
  since Task 27, any design-package document whose rule changed during implementation.

- [ ] **Step 1: Write** the documents.
- [ ] **Step 2: Run** `node scripts/check-ontology-terms.mjs` after staging. Expected: clean.
- [ ] **Step 3: Walk the demo runbook** end to end on a fresh clone. Expected: every step works as
  written.
- [ ] **Step 4: Commit** with message "docs: README, demo runbook and operator guide".
- [ ] **Step 5: Open the Phase 7 pull request.**

---

## Spec coverage

| Spec / design item | Task |
|---|---|
| D1 one Manager per type | 9 (partial unique index), 17 |
| D2 negotiation only | 3 (import removed), 6, 13 |
| D3 group-derived requirements | 5, 12 |
| D4 invite locations | 8, 11, 14 |
| D5 port and generalise | 1–3 |
| D6 no bulk import | 3 |
| D7 `inviteOptionCount` | 5, 12, 14 |
| D8 Keycloak only, provider seam | 1, 17, 29 |
| D9 no MinIO | 1, 29, 30 |
| D10 neutral theme | 24 |
| D11 configurable `StaffId` | 3, 17 |
| D12 fresh schema | 9 |
| D13 outbox | 18 |
| D14 token lifecycle | 15, 21 |
| D15 closed `AttendeeStatus` table | 8 |
| FR-1 reference data and settings | 5, 12, 25 |
| FR-2 negotiation | 6, 13, 19, 26 |
| FR-3 capacity | 7, 9, 10, 13, 15 |
| FR-4 attendees | 12 (group change), 20, 27 |
| FR-5 invite engine | 8, 11, 14, 19 |
| FR-6 booking flow | 15, 27 |
| FR-7 cancellation | 15, 26 |
| FR-8 workspace | 16, 26 |
| FR-9 recovery | 8, 16, 19 |
| FR-10 staff access | 3, 17, 25 |
| FR-11 notifications | 18 |
| FR-12 audit | 12–20 (writes), 20 (search) |
| FR-13 dashboards | 20, 27 |
| FR-14 parity | 22, 23 |
| FR-15 background processing | 18, 19 |
| 03a/03b screens, accessibility, Help | 24–27 |
| 06 security headers, rate limits, DB roles | 9, 21, 30 |
| 07 deployment and CI | 29–31 |
| 08 NFRs and verification | 11, 20, 21, 32 |
| 09 predecessor defects 1–34 | Carried by the FR tasks above; each "Closed by FR-x" row is covered where that FR is |
