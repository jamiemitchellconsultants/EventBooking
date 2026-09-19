# EventBooking — Design Spec

**Date:** 2026-09-19
**Status:** Draft for review
**Origin:** Generalisation of JointBooking (`jamiemitchellconsultants/JointBooking`, `main` at `6957928`)

## 1. Purpose

JointBooking invites recruitment candidates to a 4-hour window at a single head office, where up
to three fixed appointment types (drug & alcohol testing, medical, uniform fitting) are run by
three fixed managers. Nothing about it can be reused outside that one process.

EventBooking is the general-purpose version:

- **Many `Location`s**, each with its own address and time zone.
- **Variable-length events.** The fixed 4-hour window becomes an `EventWindow` with an explicit
  `durationMinutes`.
- **Any number of `AppointmentType`s**, Admin-managed rather than seeded constants, each with at
  most one current Manager.
- **An `Event` lists its own `AppointmentType`s.** Creating an `EventProposal` requires that list,
  and every listed type's Manager must accept before the `Event` becomes bookable. JointBooking's
  3-way negotiation becomes an N-way one, where N is the length of the list (1 or more).
- **Vocabulary:** candidate becomes `Attendee`, slot becomes `Event`.
- **Deployment:** the local Docker Compose stack and the home-lab deployment carry over. AWS
  (Terraform, Lambda, SES, S3 static site, the AWS infrastructure adapter) is dropped.

What must survive unchanged is JointBooking's core guarantee: **no overbooking under
concurrency**, enforced both in the application and in the database.

## 2. Decisions taken

| # | Decision | Chosen | Rejected alternatives |
|---|---|---|---|
| D1 | Manager scope | One Manager per `AppointmentType`, across every `Location` | Per (location, type); types owned by a location |
| D2 | How an `Event` becomes bookable | Negotiation only. The proposing Manager lists the types, and every listed type's Manager accepts with a headcount | Creator sets every headcount; negotiation plus direct creation or import |
| D3 | Attendee requirements | Admin-managed `AttendeeGroup` mapped to `AppointmentType`s; `AttendeeRequirement` derived from the group | Types picked per attendee; groups with overrides |
| D4 | Location of offered events | The Coordinator selects one or more `Location`s per `Invite`, snapshotted as `InviteLocation` | Home location per attendee; any location |
| D5 | Implementation approach | Port JointBooking's solution into this repository and generalise it | Clean rebuild from `docs/redesign`; generalise JointBooking in place |
| D6 | Bulk event import | **Dropped.** It contradicts D2, and a CSV with a column per type no longer has a fixed shape | Keep import with an N-type header |
| D7 | Options per invite | `SystemSettings.inviteOptionCount`, 1–5, default 3 | Fixed at 3 |
| D8 | Identity provider | Keycloak only, for local and home-lab. The authentication-provider seam stays, so another OIDC provider can be added later | Keep the Entra ID adapter (it was only used by the AWS production path) |
| D9 | Object storage | MinIO is dropped. Nothing in JointBooking reads or writes it | Keep it as unused infrastructure |
| D10 | Branding | Neutral theme. JointBooking's airline-specific tokens and assets are removed | Carry the branded design system over |
| D11 | `StaffId` format | The format is a deployment-configured regular expression, defaulting to `^[A-Z0-9]{1,32}$`. JointBooking's `U`/`N` plus 6-digit rule was organisation-specific | Keep the hard-coded format |
| D12 | Database history | A fresh initial migration. No data migration from JointBooking | Port JointBooking's migration chain |

## 3. Vocabulary mapping

The canonical names are in [docs/ontology.md](../../ontology.md). The table maps JointBooking
names (plain text) to EventBooking terms, so the port can be done mechanically and reviewed against
a checklist.

| JointBooking | EventBooking | Change beyond renaming |
|---|---|---|
| Candidate | `Attendee` | `attendeeGroupId` always required; the legacy-reconciliation path is removed |
| CandidateRequirement | `AttendeeRequirement` | — |
| CandidateStatus | `AttendeeStatus` | — |
| CandidateReadiness / CandidateReadinessCode | `AttendeeReadiness` / `AttendeeReadinessCode` | EmployeeGroupUnassigned code removed (a group is always assigned) |
| EmployeeGroup / EmployeeGroupRequirement | `AttendeeGroup` / `AttendeeGroupRequirement` | Admin-managed rather than seeded, change-controlled data |
| SlotProposal | `EventProposal` | Adds `locationId`, `durationMinutes`, and a type list via `EventProposalAppointmentType` |
| SlotProposalStatus | `EventProposalStatus` | — |
| ConfirmedSlot | `Event` | `proposalId` is never null (no import); adds `locationId` and `durationMinutes` |
| ConfirmedSlotStatus | `EventStatus` | — |
| SlotCapacity | `EventCapacity` | One row per listed type rather than always 3 |
| SlotWindow | `EventWindow` | Adds `durationMinutes` |
| (none) | `Location`, `InviteLocation`, `EventProposalAppointmentType` | New |
| AppointmentType | `AppointmentType` | Admin-managed; adds `isActive` |
| SystemSettings | `SystemSettings` | Adds `inviteOptionCount` |
| EmailTemplate: CandidateInvite, CandidateReinvite, SlotCancelledRebookingNeeded | `AttendeeInvite`, `AttendeeReinvite`, `EventCancelledRebookingNeeded` | `BookingConfirmation` unchanged in name, and now includes the `Location` name and address |
| ActorType: CandidateToken | `AttendeeToken` | — |
| StaffCapability: ImportConfirmedSlots | removed | D6 |
| StaffCapability: ManageCandidates, ViewCandidateDashboards, ViewCandidateAudit, ViewSlotAudit, ManageSlotNegotiation, ViewSlotOperations, CancelConfirmedSlot | `ManageAttendees`, `ViewAttendeeDashboards`, `ViewAttendeeAudit`, `ViewEventAudit`, `ManageEventNegotiation`, `ViewEventOperations`, `CancelEvent` | — |
| (none) | `ManageReferenceData` | New: Admin manages `Location`, `AppointmentType` and `AttendeeGroup` |
| AuditAction: SlotConfirmed, SlotCancelled, EmployeeGroupAssigned, EmployeeGroupChanged, CandidateDeleted | `EventConfirmed`, `EventCancelled`, `AttendeeGroupAssigned`, `AttendeeGroupReassigned`, `AttendeeDeleted` | SlotImported and StaffAccessRemoved removed (nothing produces them) |
| (none) | `LocationCreated`, `LocationUpdated`, `AppointmentTypeCreated`, `AppointmentTypeUpdated`, `AttendeeGroupCreated`, `AttendeeGroupUpdated`, `SystemSettingsChanged` | New reference-data and settings audit |
| HeadOffice configuration (address, time zone) | `Location` rows | Configuration keys removed |

This also retires the placeholder SessionType concept from the scaffold ontology: the user-facing
concept is `AppointmentType`, as in JointBooking.

## 4. Domain model

The full entity, value-object, enum, relationship and invariant list lives in
[docs/ontology.md](../../ontology.md) and is not repeated here. This section covers only what is
genuinely new or behaves differently.

### 4.1 Reference data (Admin, `ManageReferenceData`)

- **`Location`**: create, rename, edit the address and `timeZoneId` (IANA, validated), and
  activate or deactivate. `code` is immutable. Editing `timeZoneId` is refused while the location
  hosts an `Open` `EventProposal` or an `Active` future `Event`, because it would silently move
  their wall-clock times.
- **`AppointmentType`**: create, rename, and activate or deactivate. `code` is immutable.
  Deactivation rules are in the ontology invariants.
- **`AttendeeGroup`**: create, rename, activate or deactivate, and replace its mapping set.
  Replacing the mappings re-derives `AttendeeRequirement` for every member in the same transaction,
  and supersedes their pending initial `Invite`s so the attendee receives options that match. It is
  refused, with the count of blocking attendees, while any member has an active original `Booking`.
  This is the same rule JointBooking applies to a single attendee's group change, applied to the
  whole group.
- Every reference-data write is audited and optimistically concurrency-checked (a `version`
  column, as with `StaffAccessProfile`).

Nothing is ever hard-deleted. With an unbounded set of types, deactivation is the only safe removal
path while history references them.

### 4.2 Negotiation (Manager, `ManageEventNegotiation`)

1. A Manager submits a proposal: `locationId`, `date`, `startTime`, `durationMinutes`, a non-empty
   list of `appointmentTypeId`s including their own, and their own `headcount`.
2. The system validates the following, then creates the `EventProposal` (`Open`), one
   `EventProposalAppointmentType` per listed type, and the proposer's `ProposalAcceptance`:
   - the `Location` is active;
   - the `EventWindow` is valid and in the future, in that location's time zone;
   - every listed type is active, not duplicated, and has a current Manager.
3. **If the list has exactly one entry,** the proposal is confirmed in the same transaction (step 5).
4. Each other listed type's Manager sees the proposal in their queue and records, revises or
   withdraws their own `ProposalAcceptance`.
5. Whichever acceptance completes the set (acceptances = listed types) confirms the proposal in the
   same transaction. It sets `Confirmed`, creates the `Event` with the same `Location` and
   `EventWindow`, and creates one `EventCapacity` per listed type with
   `totalHeadcount = remainingCapacity = headcount`.
6. The proposer may withdraw an `Open` proposal. Once `Confirmed`, the type list is frozen for the
   life of the `Event`.

**Concurrency.** Every acceptance write first takes a row lock on its `EventProposal`
(`SELECT … FOR UPDATE`). This serialises the "last acceptance" race, so two Managers accepting
concurrently cannot both create an `Event`, and neither can miss the confirmation. A unique
constraint on `Event.proposalId` is the database backstop.

**Manager replacement.** Proposals and acceptances belong to the `AppointmentType`, not to the
person. A new Manager for a type can act on everything their predecessor left
(JointBooking hardening FR-2.6). `managerUserId` and `createdByManagerUserId` remain as audit
attribution only.

**Visibility.** A Manager sees the proposal's `Location`, `EventWindow`, the listed types, and which
types have accepted. They see **their own** headcount only; other Managers' headcounts are never
returned (as in JointBooking).

### 4.3 Capacity and the no-overbooking guarantee

This is unchanged in principle and generalised from 3 rows to N:

- `EventCapacity` has a check constraint `0 <= remainingCapacity <= totalHeadcount` and a unique key
  on (`eventId`, `appointmentTypeId`).
- Every capacity-changing operation (booking, cancellation, recovery booking, Manager adjustment,
  event cancellation) locks the affected `EventCapacity` rows **in ascending `appointmentTypeId`
  order** within one transaction. JointBooking's fixed 3-row set made lock order incidental; with N
  types and overlapping required subsets, a fixed order is what prevents deadlocks.
- A booking charges only the `AppointmentType`s in the `Invite`'s `InviteRequirement` set. An
  `Event` may list types an attendee does not need. Those rows are untouched.
- A Manager adjusts only their own type's `EventCapacity`, never below the active-booking count
  for that type.

### 4.4 Invites (Coordinator, `ManageAttendees`)

- Issuing an initial `Invite` requires one or more active `Location`s, snapshotted as
  `InviteLocation`.
- **Eligible `Event`:**
  - `Active`, and its window has not started;
  - at one of the invite's `InviteLocation`s;
  - lists **every** `InviteRequirement` type, with `remainingCapacity > 0` for each.

  Selection is a single relational-division query executed in the database, ordered by start
  instant (UTC) and then by `Event` id, taking `inviteOptionCount` rows.
- If fewer than `inviteOptionCount` eligible events exist, no `Invite` is created and the
  `Attendee` becomes `AwaitingAvailability`, as in JointBooking. There is no automatic pick-up:
  the attendee is listed on the Coordinator dashboard, and the Coordinator re-issues the invite
  (choosing `Location`s again) once suitable `Event`s exist. The dashboard shows, per attendee, the
  required type codes, so a Coordinator can see which combination is missing.
- Automatic re-issue after expiry, option replacement, and re-invites after an `Event` is cancelled
  all reuse the originating `Invite`'s `InviteLocation` set. A **recovery** `Invite` defaults to
  the original `Booking`'s `Location`, and the Coordinator may widen the selection when starting
  recovery.
- Expiry, retry limit (`maxAutoRetryCount`), supersession, and recovery semantics are otherwise
  unchanged from JointBooking, including its hardening items: the sweep re-issues correctly,
  cancel-and-rebook supersedes a pending recovery `Invite`, and an unavailable option with no
  replacement flags the attendee instead of failing silently.

### 4.5 Time

- An `EventWindow` is stored as a local `date`, `startTime` and `durationMinutes`, and interpreted
  in its `Location`'s `timeZoneId`. The start and end instants are computed in the domain from that
  zone, never stored.
- All "has it started / has it ended / is it today" rules (cancellation cut-off, check-in on the
  event date, `NoShow` only after the end) use the `Location`'s zone. JointBooking's single
  head-office time-zone setting is removed.
- Windows that fall into a daylight-saving gap or overlap for their zone are rejected at proposal
  time.
- Emails and screens show times in the `Location`'s zone with the zone abbreviation.

### 4.6 Appointment workspace

This is unchanged apart from scope. Manager and AppointmentStaff profiles see only
`BookingAppointment` rows for their own `AppointmentType`, across every `Location`, filterable by
`Location` and `Event`. The roster CSV, including formula-injection neutralisation, and the
7-day recent-past selector carry over.

## 5. Authorization

Roles and the `StaffAccessProfile` shape rules are unchanged (see the ontology invariants).
The capability matrix is as follows:

| Capability | Admin | Coordinator | Manager (scoped) | AppointmentStaff (scoped) |
|---|:---:|:---:|:---:|:---:|
| `ManageSettings` | ✔ | | | |
| `ManageReferenceData` | ✔ | | | |
| `ManageStaffAccess` | ✔ | | | |
| `ManageAttendees` | | ✔ | | |
| `ViewAttendeeDashboards` | | ✔ | | |
| `ViewAttendeeAudit` | | ✔ | | |
| `ViewEventAudit` | ✔ | ✔ | | |
| `ManageEventNegotiation` | | | ✔ | |
| `ViewEventOperations` | ✔ | ✔ | | |
| `CancelEvent` | ✔ | ✔ | ✔ (an `Event` that lists their type) | |
| `ConductAppointments` | | | ✔ (own type) | ✔ (own type) |

Carried-over hardening:

- a single default-deny gate for a scoped role with a null scope;
- token roles re-compared against the stored profile on every staff request, REST and MCP alike;
- Admin receives no attendee data, enforced at the query layer.

Attendees authenticate only through HMAC-signed invite and manage tokens, as in JointBooking.

## 6. Architecture

### 6.1 Solution layout

The Clean Architecture layering is ported from JointBooking, with every project renamed from
JointBooking.* to EventBooking.*:

| Project | Port action |
|---|---|
| Domain | Port and generalise: the N-type `EventProposal`, `EventWindow` with duration and zone, and the new reference-data aggregates |
| Application | Port and generalise the handlers. Add reference-data handlers. Remove the import handler. Import and payload limits stay in handlers, not at the REST edge |
| Infrastructure | Port the EF Core/PostgreSQL persistence, row-locking and outbox email. **Absorb Infrastructure.Local** (SMTP sender, system clock, HMAC tokens): with AWS gone, the local/AWS split has no second implementation to justify it |
| Infrastructure.Aws | **Delete** |
| Api | Port. Remove the sweep Lambda entry point and keep the hosted invite-sweep background service. Keep the `/api` link index, OpenAPI and Swagger |
| Api.Auth.Local | Port (Keycloak JWT bearer and the roles/`staff_id`/`name` claims) |
| Api.Auth.EntraId | **Delete** (D8) |
| Mcp | Port, keeping REST/MCP parity including the new reference-data tools |
| Web | Port the Blazor WebAssembly app with a neutral theme (D10). Add Admin screens for locations, appointment types and attendee groups. The proposal form gains location, duration and a multi-select type list; tables become dynamic per type rather than three fixed columns |
| SeedData | Port and regenerate the demo dataset (§8) |

Test projects mirror the source projects. AWS-specific tests are deleted.

### 6.2 What is dropped with AWS

- `terraform/` in full
- the sweep Lambda
- the SES email sender
- the S3/CloudFront static-site path
- Lambda deployment zips in the release workflow
- `docs/estimated-run-costs.md`
- the AWS architecture diagrams

GitHub Actions keeps:

- build and test;
- the GHCR image publish for the api, mcp, web, web-caddy and seed images;
- the self-contained EF migrations bundle.

### 6.3 API conventions

Everything is carried over:

- REST under `/api` with hypermedia links;
- OpenAPI v1 at `/openapi/v1.json`;
- **cursor-based pagination on every list endpoint** (keyset, never offset);
- optimistic concurrency through `version` with 409 responses;
- the same problem-details error model.

The new resources are `/api/locations`, `/api/appointment-types` and `/api/attendee-groups`.
Slot resources become `/api/event-proposals` and `/api/events`, and candidate resources become
`/api/attendees`. Attendee token routes stay REST-only and are never exposed through MCP.

### 6.4 Email

The only email provider is SMTP: Mailpit locally and in the home-lab demo, or any SMTP relay in a
real home-lab deployment. The durable `EmailLog` outbox, retry and no-PII rules carry over.
Template wording is ported with "candidate" becoming "attendee" and "head office" becoming the
`Location` name and address. `{types}` lists `AppointmentType` names in `code` order.

## 7. Deployment

### 7.1 Local (repository root `docker-compose.yml`)

| Service | Notes |
|---|---|
| postgres | 16-alpine, database `eventbooking` |
| keycloak | `start-dev --import-realm` with `deploy/keycloak/realm-export.json` (realm `eventbooking`) |
| mailpit | SMTP 1025, UI 8025 |
| api / mcp / web | Built from source, or pulled as `ghcr.io/jamiemitchellconsultants/eventbooking-*:${EVENTBOOKING_IMAGE_TAG:-latest}` |

MinIO and its bootstrap service are removed (D9). The port assignments are unchanged from
JointBooking, so the demo runbook carries over.

### 7.2 Home lab (`deploy/home-lab/`)

This ports JointBooking's structure with names changed from jointbooking to eventbooking:

- a Compose file with private and public networks;
- a web Dockerfile, Caddyfile and appsettings;
- a Caddy ingress fragment;
- a Keycloak realm file with the `EVENTBOOKING_HOSTNAME` placeholder;
- a seed profile that always migrates first, then optionally seeds, with `--skip-seed`, `--reseed`
  and `--reanchor`;
- Mailpit behind basic auth at `/mailpit`.

The `EVENTBOOKING_*` environment variables replace the `JOINTBOOKING_*` ones, and the MinIO
variables are removed.

The installer script lives in the separate LocalAI repository (`setup-jointbooking-windows.ps1`).
An equivalent `setup-eventbooking-windows.ps1` is needed there. That is **out of scope for this
repository**, but it is a prerequisite for the first home-lab deploy, so it should be tracked as a
companion issue in LocalAI.

## 8. Seed data

The demo dataset is regenerated for the general model. It deliberately exercises the new axes:

- **3 `Location`s** in 2 time zones (for example London, Manchester and Dublin), one of them
  inactive.
- **5 `AppointmentType`s**, one of them inactive.
- **3 `AttendeeGroup`s:**
  - one needing 1 type;
  - one needing 3 types;
  - one needing a pair of types that no single demo `Event` fully covers, so the
    `AwaitingAvailability` path can be demonstrated.
- `Event`s with durations of 60, 90, 240 and 480 minutes, listing 1 to 5 types.
- Open proposals partially accepted (for example 2 of 4 types accepted).
- Keycloak demo users:
  - one Admin;
  - one Coordinator;
  - a Manager for each active type;
  - two AppointmentStaff.

The seed keeps date anchoring (`--reanchor`), idempotent natural-key upserts, and the seeded
attendee invitation emails through Mailpit.

## 9. Error handling

This carries over the JointBooking model, with the new cases below. Every rejection is a 4xx
problem-details response that names the rule, never a 500.

| Case | Response |
|---|---|
| Proposal lists a type with no current Manager, an inactive type, a duplicate, or omits the proposer's own type | 422 with the offending type codes |
| `EventWindow` invalid (non-15-minute multiple, over 720 minutes, crosses midnight, in the past, or in a DST gap or overlap) | 422 |
| Acceptance for a type not on the list, or by a non-Manager of that type | 403 / 422 |
| Concurrent acceptances on one proposal | Serialised by the proposal row lock. Exactly one of them completes the set and confirms, so no race outcome exists |
| Acceptance, revision or withdrawal on a proposal that is already `Confirmed` or `Withdrawn` | 409 with the current status and no side effect |
| Deactivating or re-zoning reference data that is in use | 409 with blocking counts |
| `AttendeeGroup` mapping change blocked by active bookings | 409 with the count of blocking attendees |
| Invite with no eligible `Location` selected, or fewer than `inviteOptionCount` eligible events | 422, or `AwaitingAvailability` status per §4.4 |

## 10. Testing

- **Domain unit tests** for:
  - `EventWindow` validation, including DST cases;
  - N-type confirmation, including the single-type auto-confirm;
  - capacity arithmetic;
  - the `BookingAppointment` state machine;
  - the reference-data invariants.
- **Concurrency tests** against real PostgreSQL (Testcontainers, as in JointBooking):
  - concurrent acceptances of the last N outstanding types produce exactly one `Event`;
  - concurrent bookings on the last capacity of overlapping type subsets never overbook and never
    deadlock;
  - Manager capacity adjustment racing a booking.
- **Selection query tests:**
  - an `Event` listing a superset of the requirements is eligible;
  - a subset is not;
  - a `Location` filter is honoured;
  - ordering across time zones follows the UTC instant.
- **API, MCP-parity and authorization-matrix tests** ported and extended with the new capabilities
  and resources.
- **Web component tests** for the dynamic per-type columns and the multi-select type list.
- **Seed tests** that the demo dataset covers every axis in §8.

## 11. Porting sequence (input to the implementation plan)

1. Copy the solution with projects renamed; delete Aws, EntraId, terraform and the Lambda entry
   point; fold Infrastructure.Local into Infrastructure. Build green with the old domain.
2. Apply the vocabulary mapping (§3) mechanically. Build and test green.
3. Add the reference-data aggregates (`Location`, `AppointmentType` admin, `AttendeeGroup` admin)
   and the fresh initial migration.
4. Generalise negotiation to N types with `EventWindow` duration and time zone.
5. Generalise capacity locking, the invite selection query and `InviteLocation`.
6. Add the Web admin screens and dynamic type columns, and apply the neutral theme.
7. Regenerate the seed data. Rename the Compose and home-lab files, and remove MinIO.
8. Update the docs: README, demo runbook, home-lab README and user guides.

## 12. Out of scope

- Multi-tenancy (one deploying organisation per instance)
- Per-location Managers (D1)
- Bulk event import (D6)
- Attendee-chosen appointment types, or per-attendee overrides (D3)
- Any cloud deployment target
- The LocalAI installer script (§7.2)
- Migrating data from a running JointBooking instance (D12)
