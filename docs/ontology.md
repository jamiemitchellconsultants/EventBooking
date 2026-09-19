# EventBooking — Application Ontology

<!-- GENERATED FROM ontology.ttl. Edit that file, not this one. -->

> **AI instructions:** Read this file in full before starting any task that touches domain
> concepts. To change it, edit `ontology.ttl` and run `node scripts/build-ontology.mjs`. This
> file must never lag behind the code. See the Ontology protocol section in this repository's
> agent-instruction file.

Every domain concept is named here exactly once, as a backticked PascalCase term. Code, specs,
plans and prose use those names; `ontology.ttl` is the source, this file and they are the
consumers. A term that appears in Markdown but not here fails the deterministic check in
`scripts/check-ontology-terms.mjs`.

**Delete the sections that do not apply to this repository from `ontology.ttl`**, and remove the
matching names from `sections` in `ontology.config.json`. An empty section is worse than an
absent one — the optional semantic reviewer treats an empty required section as a configuration
error.
---

## Entities

| Name | Properties | Description |
| --- | --- | --- |
| `Location` | `id`, `code`, `name`, `address`, `timeZoneId`, `isActive` | An Admin-managed physical site where `Event`s take place. `timeZoneId` is an IANA zone; every `EventWindow` at the site is interpreted in it. `code` is immutable, unique, canonical uppercase snake case, accepted case-insensitively at input boundaries. |
| `AppointmentType` | `id`, `code`, `name`, `isActive` | An Admin-managed kind of appointment an `Attendee` may be required to attend, run by at most one current Manager across every `Location`. `code` is immutable, unique, canonical uppercase snake case. There is no fixed number of types. |
| `EventProposal` | `id`, `locationId`, `date`, `startTime`, `durationMinutes`, `status`, `createdByManagerUserId` | An attendee-facing `EventWindow` at one `Location`, proposed by one Manager together with the list of `AppointmentType`s the `Event` will offer. Every listed type's Manager must accept before it becomes an `Event`. |
| `EventProposalAppointmentType` | `proposalId`, `appointmentTypeId` | One `AppointmentType` listed on an `EventProposal`. The list is fixed at creation, has at least one entry, and must include the proposing Manager's own type. |
| `ProposalAcceptance` | `proposalId`, `appointmentTypeId`, `managerUserId`, `headcount` | One Manager's acceptance of an `EventProposal` for their own listed `AppointmentType`, carrying their own positive headcount. The proposing Manager's acceptance is recorded when the proposal is created. The accepting Manager may revise `headcount` in place while the proposal is `Open`; no other Manager may read or change it. |
| `Event` | `id`, `proposalId`, `locationId`, `date`, `startTime`, `durationMinutes`, `status` | An `EventProposal` accepted for every listed `AppointmentType`, bookable by `Attendee`s. Carries the same `EventWindow` and `Location` as its proposal. |
| `EventCapacity` | `eventId`, `appointmentTypeId`, `totalHeadcount`, `remainingCapacity` | Remaining bookable headcount for one `AppointmentType` on one `Event`; exactly one row per listed type. While the `Event` is `Active`, that type's current Manager may replace `totalHeadcount` with any positive total that still covers every active booking requiring that type. |
| `AttendeeGroup` | `id`, `code`, `name`, `isActive` | An Admin-managed category of `Attendee` whose `AttendeeGroupRequirement` mappings determine which `AppointmentType`s its members require. `code` is immutable, unique, canonical uppercase snake case. |
| `AttendeeGroupRequirement` | `attendeeGroupId`, `appointmentTypeId` | One `AppointmentType` required by an `AttendeeGroup`. |
| `Attendee` | `id`, `name`, `email`, `attendeeGroupId`, `status`, `statusChangedAt` | A person invited to attend an `Event`, entered by a Coordinator via CSV upload or the API. `attendeeGroupId` is always required. `statusChangedAt` is stamped whenever `status` is written. |
| `AttendeeRequirement` | `attendeeId`, `appointmentTypeId` | One materialised `AppointmentType` an `Attendee` currently requires, derived only from their `AttendeeGroup`. |
| `Invite` | `id`, `attendeeId`, `recoveryOfBookingId`, `tokenHash`, `expiresAt`, `status`, `retryCount` | An offer to an `Attendee` of `SystemSettings` `inviteOptionCount` `Event` options, carrying a signed token, a snapshotted requirement set and a snapshotted set of eligible `Location`s. `recoveryOfBookingId` is null for an initial invitation and identifies the original `Booking` for missed-appointment recovery. |
| `InviteLocation` | `inviteId`, `locationId` | One `Location` a Coordinator selected when issuing an `Invite`. Options, replacements and automatic re-issues are drawn only from `Event`s at these `Location`s. |
| `InviteOption` | `inviteId`, `eventId` | One `Event` offered within an `Invite`. |
| `InviteRequirement` | `inviteId`, `appointmentTypeId` | One `AppointmentType` snapshotted when an `Invite` is issued. An initial `Invite` snapshots every current `AttendeeRequirement`; a recovery `Invite` snapshots only still-outstanding no-show types. |
| `Booking` | `id`, `attendeeId`, `eventId`, `inviteId`, `recoveryOfBookingId`, `createdAt`, `status`, `manageTokenHash` | Created when an `Attendee` confirms one `InviteOption`. Cancellation sets `status` rather than deleting the row. |
| `BookingAppointment` | `id`, `bookingId`, `appointmentTypeId`, `status`, `checkedInAt`, `outcomeAt`, `lastChangedByStaffUserId`, `lastChangedAt`, `version` | The operational instance of one required `AppointmentType` within one `Booking`; unique per `Booking` and `AppointmentType`. |
| `StaffAccessProfile` | `staffUserId`, `roles`, `appointmentTypeId`, `version` | The application's mirror of one staff identity's access. `roles` is written only by the identity-provider role sync; `appointmentTypeId` scope and `version` are Admin-owned. |
| `StaffIdentity` | `staffUserId`, `staffId`, `displayName`, `lastSeenAt` | The application's cached mirror of one authenticated staff identity. Never the source of truth. |
| `AuditLog` | `id`, `entityType`, `entityId`, `action`, `actorType`, `actorId`, `timestamp`, `details` | An append-only record of one state change. Carries identifiers, canonical codes and old/new statuses only; never names, email addresses or free text. |
| `EmailLog` | `id`, `attendeeId`, `templateName`, `sentAt`, `status`, `inviteId`, `bookingId`, `eventId`, `claimedAt` | A durable record of one attendee email delivery attempt. The nullable context identifiers let a failed attempt be regenerated without storing a token, URL or body. |
| `SystemSettings` | `inviteExpiryDays`, `maxAutoRetryCount`, `inviteOptionCount` | Single-row Admin-configurable settings. `inviteOptionCount` is between 1 and 5 and defaults to 3. |

---

## Value Objects

| Name | Properties | Description |
| --- | --- | --- |
| `EventWindow` | `date`, `startTime`, `durationMinutes` | The attendee-facing window shared by an `EventProposal` and its `Event`, in the `Location`'s time zone. `durationMinutes` is a positive multiple of 15, at most 720, and the window must end on the same local date it starts. The end time is always derived, never stored. |
| `StaffId` | `value` | The organisation's staff number, sourced only from an identity-provider token claim and validated against a deployment-configured pattern. Normalised to uppercase. |
| `AttendeeReadiness` | `attendeeId`, `code`, `outstandingAppointmentTypes` | A calculated, non-persisted view of whether an `Attendee`'s current requirements are satisfied across their active original `Booking` and its non-cancelled recovery `Booking`s. |
| `OutstandingAppointmentType` | `code`, `name`, `isRecoverable` | Readiness detail for one outstanding `AttendeeRequirement`. `isRecoverable` is true only when its latest non-cancelled attempt is `NoShow`. |

---

## Enums

| Name | Values | Description |
| --- | --- | --- |
| `Role` | `Manager`, `Coordinator`, `Admin`, `AppointmentStaff` | Business roles asserted by the identity provider's `roles` claim. |
| `StaffCapability` | `ManageSettings`, `ManageReferenceData`, `ManageStaffAccess`, `ManageAttendees`, `ViewAttendeeDashboards`, `ViewAttendeeAudit`, `ViewEventAudit`, `ManageEventNegotiation`, `ViewEventOperations`, `CancelEvent`, `ConductAppointments` | The unit of authorization checked at every staff-facing operation; roles map to capabilities. |
| `AttendeeStatus` | `NotYetInvited`, `AwaitingAvailability`, `Invited`, `Booked`, `NoResponseNeedsFollowUp` | Lifecycle of an `Attendee`. |
| `EventProposalStatus` | `Open`, `Withdrawn`, `Confirmed` | Lifecycle of an `EventProposal`. |
| `EventStatus` | `Active`, `Cancelled` | Lifecycle of an `Event`. |
| `InviteStatus` | `Pending`, `Used`, `Expired`, `Superseded`, `Cancelled` | Lifecycle of an `Invite`. |
| `BookingStatus` | `Active`, `Cancelled`, `Concluded` | Lifecycle of a `Booking`. |
| `BookingAppointmentStatus` | `Expected`, `CheckedIn`, `Completed`, `NoShow` | Lifecycle of a `BookingAppointment`. |
| `AttendeeReadinessCode` | `Ready`, `NoActiveBooking`, `RequirementSnapshotMismatch`, `AppointmentsOutstanding` | The calculated code carried by `AttendeeReadiness`. |
| `ActorType` | `Staff`, `AttendeeToken`, `System` | Who caused an `AuditLog` entry. |
| `AuditAction` | `ProposalCreated`, `ProposalWithdrawn`, `AcceptanceRecorded`, `AcceptanceWithdrawn`, `EventConfirmed`, `EventCancelled`, `CapacityDecremented`, `CapacityIncremented`, `CapacityAdjusted`, `InviteCreated`, `InviteSent`, `InviteExpired`, `InviteOptionReplaced`, `BookingCreated`, `BookingCancelled`, `StaffAccessChanged`, `StaffRolesSynced`, `AppointmentCheckedIn`, `AppointmentCompleted`, `AppointmentMarkedNoShow`, `AppointmentStatusCorrected`, `AttendeeGroupAssigned`, `AttendeeGroupReassigned`, `RecoveryInviteCreated`, `RecoveryInviteCancelled`, `RecoveryBookingCreated`, `RecoveryBookingConcluded`, `AttendeeDeleted`, `LocationCreated`, `LocationUpdated`, `AppointmentTypeCreated`, `AppointmentTypeUpdated`, `AttendeeGroupCreated`, `AttendeeGroupUpdated`, `SystemSettingsChanged` | The complete vocabulary of audited state changes. |
| `EmailTemplate` | `AttendeeInvite`, `BookingConfirmation`, `EventCancelledRebookingNeeded`, `AttendeeReinvite` | The attendee-facing email templates. |
| `EmailStatus` | `Sent`, `Failed`, `Pending`, `Resolved` | Delivery state of an `EmailLog`. |

---

## Relationships

| From | Relationship | To | Cardinality |
| --- | --- | --- | --- |
| `Location` | hosts | `EventProposal` | 1 → many |
| `Location` | hosts | `Event` | 1 → many |
| `EventProposal` | lists | `EventProposalAppointmentType` | 1 → 1..* |
| `EventProposal` | receives | `ProposalAcceptance` | 1 → 1..* |
| `EventProposal` | becomes | `Event` | 1 → 0..1 |
| `Event` | has | `EventCapacity` | 1 → 1..* (one per listed `AppointmentType`) |
| `Event` | offered as | `InviteOption` | 1 → many |
| `Event` | holds | `Booking` | 1 → many |
| `AppointmentType` | listed on | `EventProposalAppointmentType` | 1 → many |
| `AppointmentType` | measured by | `EventCapacity` | 1 → many |
| `AppointmentType` | required by group through | `AttendeeGroupRequirement` | 1 → many |
| `AppointmentType` | required by | `AttendeeRequirement` | 1 → many |
| `AppointmentType` | is delivered as | `BookingAppointment` | 1 → many |
| `AppointmentType` | scopes | `StaffAccessProfile` | 1 → many, at most one carrying Manager |
| `AttendeeGroup` | maps through | `AttendeeGroupRequirement` | 1 → 1..* while active |
| `AttendeeGroup` | assigned to | `Attendee` | 1 → many |
| `Attendee` | requires | `AttendeeRequirement` | 1 → 1..* |
| `Attendee` | receives | `Invite` | 1 → many |
| `Attendee` | makes | `Booking` | 1 → many |
| `Invite` | restricted to | `InviteLocation` | 1 → 1..* |
| `Invite` | offers | `InviteOption` | 1 → `inviteOptionCount` |
| `Invite` | snapshots | `InviteRequirement` | 1 → 1..* |
| `Invite` | results in | `Booking` | 1 → 0..1 |
| `Booking` | recovered by | `Booking` | 1 → many sequential, at most one active |
| `Booking` | contains | `BookingAppointment` | 1 → one per `InviteRequirement` |
| `StaffIdentity` | is the identity behind | `StaffAccessProfile` | 1 → 0..1 |
| `AuditLog` | records a change on | `Event` | many → 1 per entry (also any other audited entity) |
| `EmailLog` | records a delivery attempt for | `Attendee` | many → 1 |

---

## Business Rules & Invariants

- **`Location`** — Cannot be deactivated while it hosts an `Open` `EventProposal` or an `Active` future `Event`.
- **`AppointmentType`** — Cannot be deactivated while listed on an `Open` `EventProposal` or an `Active` future `Event`, or mapped by an active `AttendeeGroup`.
- **`EventProposal`** — Becomes `Confirmed`, creating its `Event` and one `EventCapacity` per listed `AppointmentType` from each `ProposalAcceptance` headcount, in the same transaction that records the final missing acceptance. A proposal whose list has one entry is confirmed on creation.
- **`EventProposal`** — Every listed `AppointmentType` must be active and have a current Manager when the proposal is created; the `Location` must be active; the `EventWindow` must start in the future.
- **`ProposalAcceptance`** — At most one per `EventProposal` and `AppointmentType`, only for a listed type, and only by that type's current Manager. A replaced Manager's acceptances and proposals pass to their successor.
- **`Event`** — Behaves identically regardless of how many `AppointmentType`s it lists. Cancelling it voids every `Booking` against it, returns all `EventCapacity`, and issues a fresh `Invite` to each affected `Attendee`. Cancellation is refused once the `EventWindow` has started.
- **`EventCapacity`** — No overbooking: `remainingCapacity` is never below 0 or above `totalHeadcount`. Every read-check-write is one transaction that row-locks the affected `EventCapacity` rows in ascending `appointmentTypeId` order, backed by a database check constraint.
- **`AttendeeGroup`** — An active group maps to at least one active `AppointmentType`. Changing its mappings re-derives `AttendeeRequirement` for its members and supersedes their pending initial `Invite`s, and is refused while any member has an active original `Booking`.
- **`AttendeeRequirement`** — Derived only from the `Attendee`'s `AttendeeGroup`. Cannot change while the `Attendee` has an active original `Booking`, except for set-equivalent changes.
- **`Invite`** — Offers exactly `inviteOptionCount` `InviteOption`s, each an `Active`, future `Event` at one of its `InviteLocation`s that lists every snapshotted `InviteRequirement` with spare `EventCapacity`. An `Event` may list types the `Attendee` does not require; only required types are charged.
- **`Booking`** — Confirming an `InviteOption` creates the `Booking`, one `BookingAppointment` per `InviteRequirement`, and decrements `EventCapacity` for exactly those types, in one transaction.
- **`BookingAppointment`** — `Expected` to `CheckedIn` only on the `Event`'s local date; `Expected` to `NoShow` only after the `EventWindow` ends. Bounded corrections only: `CheckedIn` to `Expected`, `Completed` to `CheckedIn`, `NoShow` to `Expected`. Same-status submissions are idempotent; every real transition is versioned and audited.
- **`StaffAccessProfile`** — Admin is exclusive and unscoped and receives no attendee data. Coordinator may combine with Manager and/or AppointmentStaff but never Admin. Manager and AppointmentStaff share one `AppointmentType` scope. Each `AppointmentType` has at most one current Manager profile. A scoped role with a null scope grants no capability.
- **`StaffIdentity`** — One row per authenticated identity, `staffId` unique. A token without a valid `StaffId` claim is refused by every staff-authorized operation.
- **`AuditLog`** — Append-only; never stores names, email addresses or free text.
- **`AttendeeReadiness`** — `Ready` requires an active original `Booking` and one `Completed` `BookingAppointment` for every current `AttendeeRequirement` across that `Booking` and its non-cancelled recovery `Booking`s.
