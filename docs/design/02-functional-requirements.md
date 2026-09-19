# 02 — Functional Requirements

[← Domain model](01-domain-model.md) · [Design system and IA →](03a-design-system-and-ia.md)

Requirements use EARS forms:

| Form | Pattern |
|---|---|
| Ubiquitous | "The system shall…" |
| Event-driven | "When…, the system shall…" |
| State-driven | "While…" |
| Unwanted behaviour | "If…, then…" |

Tags:

- **(carried hardening)** closes a defect found in the predecessor. The defect is traced in
  [09](09-predecessor-traceability.md).
- **(new)** exists because of the generalisation to many locations and types.

**Cross-cutting rule.** Every requirement that writes data is exactly one database transaction,
with no partial writes visible. Every write that changes `EventCapacity` holds the row locks
described in [01 — Lock ordering](01-domain-model.md#lock-ordering). Every state change writes its
`AuditLog` entry in the same transaction.

## FR-1 — Reference data and settings

- FR-1.1 **(new)** The system shall let an Admin create a `Location` with `code`, `name`, `address`
  and `timeZoneId`, validating that:
  - `code` is unique canonical uppercase snake case, at most 50 characters;
  - `timeZoneId` is a valid IANA zone.
- FR-1.2 **(new)** The system shall let an Admin edit a `Location`'s `name` and `address` at any
  time. It shall refuse a `timeZoneId` change while the location hosts an `Open` `EventProposal` or
  a future `Active` `Event`, returning the count of each.
- FR-1.3 **(new)** The system shall let an Admin create and rename an `AppointmentType`
  (`code` at most 50 characters, `name` at most 100 characters).
- FR-1.4 **(new)** The system shall let an Admin create and rename an `AttendeeGroup` and replace
  its set of `AttendeeGroupRequirement`s. The set must contain at least one active, non-duplicated
  `AppointmentType`.
- FR-1.5 **(new)** When an Admin replaces an `AttendeeGroup`'s requirement set, the system shall,
  in one transaction:
  - re-derive `AttendeeRequirement` for every member;
  - supersede each member's `Pending` initial `Invite`;
  - set each affected member's status to `NotYetInvited`.

  If any member holds an active original `Booking`, then the system shall refuse the whole change,
  unless the new set is identical to the old one, and return the number of blocking members.
- FR-1.6 **(new)** The system shall let an Admin deactivate and reactivate a `Location`,
  `AppointmentType` or `AttendeeGroup`. It shall refuse deactivation under the conditions in
  [01 — Reference data](01-domain-model.md#reference-data), naming the blocking counts.
- FR-1.7 The system shall never hard-delete reference data. Codes shall be immutable after creation.
- FR-1.8 Every reference-data and settings write shall be optimistic-concurrency-checked against
  `version`. A mismatch shall return the current state, and the client shall retain the user's
  unsaved values.
- FR-1.9 When an Admin saves `SystemSettings`, the system shall validate the values below and apply
  them only to `Invite`s created afterwards:
  - `inviteExpiryDays` between 1 and 60;
  - `maxAutoRetryCount` between 0 and 10;
  - `inviteOptionCount` between 1 and 5.
- FR-1.10 **(carried hardening)** Every reference-data and settings write shall be audited
  (`LocationCreated`, `LocationUpdated`, `AppointmentTypeCreated`, `AppointmentTypeUpdated`,
  `AttendeeGroupCreated`, `AttendeeGroupUpdated`, `SystemSettingsChanged`), with old and new values
  in `details`.
- FR-1.11 Any authenticated staff member shall be able to read the active `Location`s,
  `AppointmentType`s (each with its current Manager's display name) and `AttendeeGroup`s (each with
  its mappings).

## FR-2 — Event negotiation

- FR-2.1 **(new)** When a Manager submits a proposal with the following fields, the system shall
  create an `EventProposal` with status `Open`, one `EventProposalAppointmentType` per listed type,
  and the Manager's own `ProposalAcceptance`:
  - `locationId`;
  - `date`, `startTime` and `durationMinutes`;
  - a list of `appointmentTypeId`s;
  - their own `headcount`.
- FR-2.2 **(new)** If a proposal fails any of the checks below, then the system shall reject it,
  naming each failure:
  - the `Location` is inactive;
  - the `EventWindow` is invalid (see [01 — Time](01-domain-model.md#time)) or not in the future;
  - the list is empty, has more than 20 entries, or contains duplicates;
  - a listed type is inactive or has no current Manager;
  - the list omits the proposer's own type;
  - `headcount` is not a positive integer of at most 1000.
- FR-2.3 **(new)** If the list contains exactly one type, then the system shall confirm the
  proposal in the same transaction (FR-2.6).
- FR-2.4 While a proposal is `Open`, when the current Manager of a listed, not-yet-accepted type
  submits a headcount, the system shall record their `ProposalAcceptance`. When they submit again,
  the system shall replace the headcount in place, writing an audit entry only if the value
  changed.
- FR-2.5 If a Manager submits an acceptance for a type not on the list, or for a type other than
  their own, then the system shall reject it.
- FR-2.6 **(new)** When a `ProposalAcceptance` makes the number of distinct accepted types equal
  the number of listed types, the system shall, in the same transaction:
  - set the proposal `Confirmed`;
  - create an `Active` `Event` with the proposal's `Location` and `EventWindow`;
  - create one `EventCapacity` per listed type with
    `totalHeadcount = remainingCapacity = headcount`;
  - audit `AcceptanceRecorded` and then `EventConfirmed`.
- FR-2.7 Every acceptance write shall first lock the `EventProposal` row, so concurrent acceptances
  are serialised and exactly one of them confirms. A unique constraint on `Event.proposalId` shall
  back this up.
- FR-2.8 A Manager shall be able to read and change only their own type's `ProposalAcceptance`.
  The system shall never return another type's headcount to a Manager.
- FR-2.9 While a proposal is `Open`:
  - a listed type's Manager may withdraw their own acceptance, except the proposer's type;
  - the current Manager of the proposer's type may withdraw the whole proposal.
- FR-2.10 **(carried hardening)** Acceptances and proposals belong to the `AppointmentType`, not to
  the person. When a type's Manager is replaced, the successor shall be able to act on everything
  the predecessor left, including withdrawing proposals the predecessor created.
- FR-2.11 If an acceptance, revision or withdrawal targets a proposal that is `Confirmed` or
  `Withdrawn`, then the system shall return a conflict with the current status and change nothing.
- FR-2.12 **(new)** When the invite sweep runs, it shall withdraw every `Open` proposal whose
  window has started, auditing `ProposalWithdrawn` with actor type `System`.
- FR-2.13 The negotiation board shall show the caller's type's view only:
  - `Open` proposals listing the caller's type, each with its location, window, listed types,
    which types have accepted, and the caller's own headcount;
  - `Active` future events listing the caller's type, each with that type's total and remaining
    capacity.

## FR-3 — Capacity and the no-overbooking guarantee

- FR-3.1 The system shall hold, at all times, `0 <= remainingCapacity <= totalHeadcount` for every
  `EventCapacity`. This shall be enforced by a transactional check and by a database check
  constraint.
- FR-3.2 When several attendees confirm concurrently, and their requirements overlap on an
  `AppointmentType` with fewer remaining places than confirmations, the system shall accept as many
  confirmations as there are places and reject the rest as `capacity-exhausted`. It shall never
  overbook and never deadlock.
- FR-3.3 **(new)** Every capacity-changing command shall lock the affected `EventCapacity` rows in
  ascending (`eventId`, `appointmentTypeId`) order before reading them.
- FR-3.4 **(new)** A booking shall decrement exactly the `InviteRequirement` types. Types listed on
  the `Event` but not required shall be untouched.
- FR-3.5 While an `Event` is `Active` and its window has not started, the current Manager of a
  listed type shall be able to set that type's `totalHeadcount` to any positive value no lower than
  the type's active-booking count. `remainingCapacity` shall change by the same delta, audited as
  `CapacityAdjusted` only on a real change.
- FR-3.6 If a capacity adjustment would go below the active-booking count, then the system shall
  reject it, returning the minimum allowed value and the current server values.

## FR-4 — Attendee management

- FR-4.1 The system shall let a Coordinator create an `Attendee`, validating the fields below, and
  shall derive `AttendeeRequirement` from the group:
  - `name`: trimmed, 1–200 characters;
  - `email`: trimmed, lower-cased, a valid address of at most 320 characters, unique among
    attendees;
  - `attendeeGroupId`: an active group.

  The create shall be audited as `AttendeeGroupAssigned`.
- FR-4.2 The system shall let a Coordinator edit an attendee's `name`, `email` and
  `attendeeGroupId`:
  - a group change shall be audited as `AttendeeGroupReassigned`;
  - a requirement-changing group change shall supersede a `Pending` initial `Invite`;
  - if the attendee holds an active original `Booking`, then a requirement-changing group change
    shall be rejected, with guidance to cancel the booking first. A set-equivalent change shall be
    allowed with no warning.
- FR-4.3 When a Coordinator uploads a CSV with header `name,email,attendee_group`, the system shall
  validate every row and import all rows or none. Validation covers:
  - the header;
  - field count;
  - blank fields;
  - unknown or inactive group codes;
  - duplicate emails within the file and against existing attendees;
  - the 1000-row and 1 MB limits.

  A rejection shall list every row error by line number.
- FR-4.4 **(carried hardening)** A CSV-imported attendee shall receive the same audit entries as
  one created individually.
- FR-4.5 When a Coordinator deletes an `Attendee` (two-step confirmation), the system shall, in one
  transaction:
  - cancel their `Pending` `Invite`s;
  - cancel their active original and recovery `Booking`s, returning `EventCapacity`;
  - delete the attendee and their requirements;
  - audit `AttendeeDeleted` with no personal data.

  `AuditLog` and `EmailLog` rows shall be retained, keyed by id. This is **(carried hardening)**:
  the recovery booking must be cancelled too.
- FR-4.6 **(carried hardening)** The attendee list shall be cursor-paginated, filterable by
  `AttendeeStatus`, `AttendeeGroup` and `AttendeeReadinessCode`, and searchable by name or email
  prefix.
- FR-4.7 The system shall compute `AttendeeReadiness` for every listed attendee as a read-time
  query, per [01 — AttendeeReadiness](01-domain-model.md#attendeereadiness).
- FR-4.8 The attendee list shall show each attendee's latest `EmailLog` status (the Delivery
  column), from the same projection the dashboards use.

## FR-5 — Invite engine

- FR-5.1 **(new)** When a Coordinator invites an attendee with at least one `AttendeeRequirement`
  and selects one or more active `Location`s, the system shall snapshot the following, and shall
  send the `AttendeeInvite` email through the outbox:
  - every requirement, as `InviteRequirement`;
  - the locations, as `InviteLocation`;
  - `inviteOptionCount` eligible `Event`s, as `InviteOption`s.

  The `Invite` shall have `expiresAt = now + inviteExpiryDays`.
- FR-5.2 **(new)** An `Event` shall be eligible for an `Invite` if and only if it meets all of the
  following:
  - it is `Active`;
  - its window has not started;
  - its `Location` is one of the invite's `InviteLocation`s;
  - it lists every `InviteRequirement` type, with `remainingCapacity` of at least 1 for each;
  - it is not already an option on this `Invite`;
  - it is not the `Event` of the attendee's current or just-cancelled `Booking` for this journey.
- FR-5.3 **(new)** Eligible events shall be chosen by one database query, ordered by start instant
  (UTC) ascending and then by `Event.id`. Spare capacity alone shall not change the order.
- FR-5.4 If fewer than `inviteOptionCount` events are eligible, then the system shall create no
  `Invite`, set the attendee to `AwaitingAvailability`, and report this to the caller.
- FR-5.5 Issuing a new `Invite` for the same journey shall set any `Pending` `Invite` to
  `Superseded`, invalidating its book token.
- FR-5.6 **(carried hardening)** When the sweep finds a `Pending` `Invite` past `expiresAt`, it
  shall set it to `Expired`:
  - if `retryCount < maxAutoRetryCount`, it shall issue a fresh `Invite` with the same
    `InviteLocation` set and `retryCount + 1`, sending `AttendeeReinvite`;
  - otherwise it shall set the attendee to `NoResponseNeedsFollowUp`.

  This transition shall work while the same `Invite` instance is already held in the unit of work.
- FR-5.7 **(carried hardening)** If an automatic re-issue fails (too few eligible events, or an
  inactive group), then the system shall set the attendee to `NoResponseNeedsFollowUp`, never leave
  them `Invited` with no `Pending` invite, and audit the reason.
- FR-5.8 **(carried hardening)** When an attendee views their `Invite`, or confirms a booking, and
  some options are no longer live (full, cancelled or started), the system shall top up to
  `inviteOptionCount` from eligible events (audited as `InviteOptionReplaced`). If the count is
  still short, then the system shall set the attendee to `NoResponseNeedsFollowUp`. With zero live
  options, the page shall stay viewable with an explanation but offer nothing to book.
- FR-5.9 A Coordinator shall be able to re-invite an attendee in `AwaitingAvailability` or
  `NoResponseNeedsFollowUp` at any time, choosing `Location`s again.

## FR-6 — Attendee booking flow

- FR-6.1 The attendee shall reach the booking flow only through the signed book token in the link
  they were emailed. There is no sign-in.
- FR-6.2 The booking page shall show the attendee's first name, the names of their required types,
  and each live option's location name, address, local date, start and end time, and zone
  abbreviation. A recovery invite shall be headed "Choose a new time for your missed
  appointment(s)".
- FR-6.3 When the attendee confirms an option, the system shall, in one transaction:
  - lock the attendee and the required `EventCapacity` rows;
  - create the `Booking` and one `BookingAppointment` per `InviteRequirement`;
  - decrement capacity;
  - set the `Invite` to `Used` and the attendee to `Booked`;
  - issue a manage token;
  - stage `BookingConfirmation` in the outbox;
  - audit `BookingCreated` and one `CapacityDecremented` per type.
- FR-6.4 If the chosen option has no capacity left for any required type, then the system shall
  return `capacity-exhausted`. The page shall remove that option and offer the rest; this is an
  expected outcome, not an error.
- FR-6.5 A repeated confirm on a `Used` invite shall return the existing booking, never create a
  second one.
- FR-6.6 The manage page shall show the booked event, the appointment names and the location
  details. It shall offer "Cancel" and "Cancel and choose a new time" until the window starts.
- FR-6.7 **(carried hardening)** Once the window has started, the manage page shall offer no
  cancellation and shall show the coordinator contact instead. The API shall refuse the action
  whatever the client does.
- FR-6.8 After a cancellation, the manage page shall state the true outcome, never assuming
  success. The possible outcomes are:
  - a new invite was sent;
  - a new invite was created but delivery is pending or failed;
  - no suitable event exists and the team will be in touch;
  - the booking was cancelled with no new time requested.

## FR-7 — Cancellation and rescheduling

- FR-7.1 When a Coordinator cancels an attendee's `Booking`, the system shall cancel it, return its
  capacity and issue a replacement `Invite` using the original `InviteLocation` set. If a
  replacement is not possible, then the attendee shall be set to `AwaitingAvailability`.
- FR-7.2 When `CancelEvent` is invoked by an Admin, a Coordinator, or the Manager of any listed
  type, the system shall:
  - set the `Event` to `Cancelled`;
  - cancel every `Active` `Booking` against it and return all capacity;
  - for each affected attendee, issue a replacement `Invite` from the original `InviteLocation`
    set, and send `EventCancelledRebookingNeeded` with outcome-truthful wording;
  - audit `EventCancelled`, plus per-booking `BookingCancelled` and `CapacityIncremented`.
- FR-7.3 Event cancellation and coordinator booking cancellation shall be two-step. The first
  request, without confirmation, is rejected with the count of active bookings that would be
  cancelled; the same request with confirmation proceeds.
- FR-7.4 **(carried hardening)** The system shall refuse to cancel an `Event`, or any `Booking`
  against it, once the window has started. Every list offered for cancellation shall exclude such
  events.
- FR-7.5 **(carried hardening)** A failed `EventCancelledRebookingNeeded` email shall be retryable
  like any other `EmailLog`, carrying the booking context needed to regenerate it.

## FR-8 — Appointment workspace

- FR-8.1 A Manager or AppointmentStaff profile shall see `BookingAppointment` rows only for its own
  scoped `AppointmentType`, across all `Location`s. The only fields shown shall be:
  - attendee name and email;
  - status;
  - check-in and outcome timestamps;
  - `version`.

  The workspace shall never show:
  - attendee, booking or invite identifiers;
  - group or requirement data;
  - token or email data;
  - capacity;
  - audit data;
  - other types' data.
- FR-8.2 The event selector shall list events that list the caller's type, whose end instant falls
  between 7 days ago and 14 days ahead. It shall be filterable by `Location`, and shall default to
  the nearest event that is current or next.
- FR-8.3 The system shall permit `Expected` to `CheckedIn` only on the event's local date, and
  `Expected` to `NoShow` only after the window ends.
- FR-8.4 The system shall permit only these corrections: `CheckedIn` to `Expected`, `Completed` to
  `CheckedIn`, and `NoShow` to `Expected`. A same-status submission shall be a no-op.
- FR-8.5 Every real transition shall be checked against `version`, attributed to the staff member,
  timestamped and audited. A conflict shall return the current row, and the client shall refresh
  it rather than replay the action.
- FR-8.6 **(carried hardening)** A `NoShow` to `Expected` correction shall be refused while a later
  recovery for that requirement is `Pending` or not cancelled.
- FR-8.7 The workspace shall offer a roster CSV for the selected event, with exactly the on-screen
  columns, excluding the command target id and `version`.
- FR-8.8 **(carried hardening)** Every roster cell beginning with `=`, `+`, `-`, `@`, a tab or a
  carriage return shall be prefixed with a single quote.

## FR-9 — Missed-appointment recovery

- FR-9.1 When an attendee's `AttendeeReadiness` lists at least one recoverable type, a Coordinator
  shall be able to start recovery. The system shall create a recovery `Invite` that:
  - snapshots only the recoverable types;
  - references the original `Booking` through `recoveryOfBookingId`;
  - uses `InviteLocation` defaulting to the original booking's `Location`, plus any locations the
    Coordinator adds;
  - is sent as `AttendeeInvite` with recovery wording, and audited as `RecoveryInviteCreated`.
- FR-9.2 Only one recovery `Invite` or recovery `Booking` shall be active per original `Booking`.
- FR-9.3 A Coordinator shall be able to cancel a `Pending` recovery `Invite`
  (`RecoveryInviteCancelled`).
- FR-9.4 Confirming a recovery option shall create a recovery `Booking` whose
  `BookingAppointment`s cover only the recovered types, charging capacity only for those
  (`RecoveryBookingCreated`).
- FR-9.5 When every appointment on a recovery `Booking` is `Completed` or `NoShow`, the system shall
  set it to `Concluded` (`RecoveryBookingConcluded`).
- FR-9.6 **(carried hardening)** Cancel-and-rebook of an original `Booking` shall succeed while a
  recovery `Invite` is `Pending`, by superseding it in the same transaction.

## FR-10 — Staff access and identity

- FR-10.1 The system shall accept staff requests only with a valid identity-provider bearer token
  carrying:
  - a subject identifier;
  - a `staff_id` claim matching the configured `StaffId` pattern;
  - optionally, `name` and `roles`.
- FR-10.2 On every staff request, the system shall upsert `StaffIdentity` (`displayName`,
  `lastSeenAt`, refreshed at most every 15 minutes). It shall also compare the token's roles with
  the stored `StaffAccessProfile`:
  - on a difference, it shall update `roles` before authorizing, audited as `StaffRolesSynced`
    with actor type `System`;
  - if the token asserts no roles, it shall remove the profile.

  This is **(carried hardening)**: the check runs on every request, not only on the "who am I"
  endpoint.
- FR-10.3 If a role sync would remove the last remaining Admin, then the system shall refuse that
  sync, keeping the stored profile, and log an operational alert.
- FR-10.4 The system shall enforce the `StaffAccessProfile` shape rules in the ontology on every
  write.
- FR-10.5 An Admin shall be able to assign, change or clear the `AppointmentType` scope of a
  profile holding Manager or AppointmentStaff. There is no Admin action that creates a profile,
  deletes a profile or edits `roles`.
- FR-10.6 Assigning Manager scope for a type that already has a current Manager shall atomically
  clear the former Manager's scope, and the saved confirmation shall name the person displaced.
  Directly clearing the only Manager scope of a type shall be allowed; the type then has no Manager
  until one is assigned.
- FR-10.7 **(carried hardening)** A profile whose scoped role carries a null scope shall be granted
  no capability. This shall be one default-deny gate evaluated before any per-capability check.
- FR-10.8 The "who am I" endpoint shall return:
  - display name;
  - `StaffId`;
  - role union;
  - scope type (code and name);
  - the list of granted capabilities.

  A signed-in identity with no profile shall get a no-role response, not an error.
- FR-10.9 Everywhere a staff member is shown to other staff, the system shall show
  `StaffIdentity.displayName`, falling back to `StaffId`.

## FR-11 — Notifications

- FR-11.1 The system shall send exactly four attendee templates: `AttendeeInvite`,
  `BookingConfirmation`, `EventCancelledRebookingNeeded` and `AttendeeReinvite`.
- FR-11.2 Every send shall be written as a `Pending` `EmailLog` in the business transaction, and
  dispatched after commit by the outbox worker ([04](04-solution-architecture.md#notification-outbox)).
  It then moves to `Sent`, or to `Failed` with no raw error text stored.
- FR-11.3 A Coordinator shall be able to retry an attendee's newest `Failed` or stale `Pending`
  email. The system shall derive the template and context server-side from the `EmailLog` context
  ids. A superseded failure shall be marked `Resolved`.
- FR-11.4 **(carried hardening)** `EmailLog` and `AuditLog` shall never contain names, email
  addresses, tokens, URLs or message bodies.
- FR-11.5 Every email shall be multipart: a plain-text body plus a simple HTML rendering of the
  same lines.
- FR-11.6 In every email, times shall be given in the event `Location`'s zone, with its
  abbreviation. `{types}` lists `AppointmentType` names in `code` order.

### Email content

In the table, `{window}` means `dddd d MMM yyyy, HH:mm–HH:mm zzz` and `{location}` means the
location's name followed by its address on the next line.

| Template | Subject | Body |
|---|---|---|
| `AttendeeInvite` (initial) | "Choose a time for your appointments" | "Please choose one of the following times for your appointments." · Appointments: `{types}` · one line per option: `{window}` at `{location}` · "Choose your time here:" plus the book link |
| `AttendeeInvite` (recovery) | Same subject | "You missed an appointment, so here are new times to complete it." (or "…complete them." for more than one) · otherwise as initial |
| `AttendeeReinvite` | "Reminder: choose a time for your appointments" | "We have not heard back from you, so here are the latest available times." · otherwise as initial |
| `BookingConfirmation` | "Your appointments are confirmed" | "Your appointments are confirmed for:" `{window}` at `{location}` · Appointments: `{types}` · "Need to change or cancel? Use this link:" plus the manage link · "Any questions, contact {coordinatorContact}." |
| `EventCancelledRebookingNeeded` | "Your appointment time has been cancelled" | "We are sorry, your appointments on `{window}` at `{location}` have had to be cancelled." · Affected appointments: `{types}` · then "A new invitation with fresh times is on its way to you." only if a replacement `Invite` was created, otherwise "The team will contact you with the next available times." |

## FR-12 — Audit trail

- FR-12.1 Every state change named in the `AuditAction` enum shall write exactly one `AuditLog`
  entry in the same transaction, with actor type and actor id. For staff, the actor id is the
  `StaffId`.
- FR-12.2 **(carried hardening)** Audit search shall be keyset-paginated and filterable by:
  - date range;
  - `AuditAction`;
  - actor type;
  - actor id;
  - entity type;
  - entity id.
- FR-12.3 Audit results shall be scoped by capability:
  - `ViewAttendeeAudit` covers `Attendee`, `Invite`, `Booking` and `BookingAppointment`;
  - `ViewEventAudit` covers `EventProposal`, `Event`, `StaffAccessProfile`, reference data and
    `SystemSettings`.

  A caller receives only the buckets their capabilities cover.
- FR-12.4 An attendee-scoped history shall gather entries for the attendee and their own
  `Invite`s, `Booking`s and `BookingAppointment`s. An event-scoped history shall gather entries
  for the `Event` and its `EventProposal`.
- FR-12.5 There shall be no update or delete path for `AuditLog`.

## FR-13 — Coordinator dashboards

- FR-13.1 The dashboard shall present these tabs from one query, each with a row count:
  - **Awaiting availability**, with "waiting since" from `statusChangedAt` and the required type
    codes;
  - **No response**;
  - **Events**, covering events that end between 7 days ago and 60 days ahead, each with its
    location, window, capacity per type and active bookings, filterable by `Location`.
- FR-13.2 The dashboard shall show counts of failed and pending emails from the latest
  `EmailLog` per attendee.
- FR-13.3 **(carried hardening)** Dashboard queries shall aggregate in the database and be bounded
  by the date windows above.
- FR-13.4 From the No response tab, a Coordinator shall be able to re-invite, choosing locations.
  From the Events tab, they shall be able to open the event's audit history.

## FR-14 — API and agent (MCP) parity

- FR-14.1 Every staff capability reachable through REST shall have an equivalent MCP tool that
  calls the same application handler. A CI test shall fail if the OpenAPI operation list and the
  MCP tool list diverge, excluding the attendee-token routes.
- FR-14.2 **(carried hardening)** Every list-returning MCP tool shall return each row's id, name or
  code, and status.
- FR-14.3 **(carried hardening)** Import limits and all validation shall live in the application
  handlers, so both surfaces enforce them identically.
- FR-14.4 Attendee-token operations shall be REST-only.

## FR-15 — Background processing

- FR-15.1 The API host shall run the invite sweep every 15 minutes. It shall:
  - expire and re-issue invites (FR-5.6);
  - withdraw started proposals (FR-2.12);
  - conclude eligible recovery bookings missed by the event-driven path (FR-9.5).
- FR-15.2 The API host shall run the outbox dispatcher continuously (FR-11.2).
- FR-15.3 Each background job shall hold a PostgreSQL advisory lock for its run, so that running
  several API replicas never duplicates work. Each job shall be idempotent.
- FR-15.4 **(carried hardening)** Background job failures shall be logged with a job-level metric,
  and shall not stop later runs.
