# 03b — Screens and Flows

[← Design system and IA](03a-design-system-and-ia.md) · [Solution architecture →](04-solution-architecture.md)

For each screen this document gives:

- who can use it;
- what it captures and what it displays;
- its actions and the audit each action writes;
- its states.

Routes are the Blazor routes. Every staff screen loads for any signed-in user; capability checks
happen in the API, and a refusal renders as a forbidden state. FR references point to
[02](02-functional-requirements.md).

## Home (`/`)

**Who:** any signed-in staff identity.

**Displays:**

- the display name, role union and scoped type from the "who am I" endpoint;
- one card per area the user can reach;
- for an identity with no profile: "Your account has not been given a role yet. Contact your
  administrator."

**States:** signed out (redirect to Keycloak) · loading · no role · ready · error.

## Help (`/help`)

Described in [03a — Help](03a-design-system-and-ia.md#help).

**States:** loading · no role · one guide · several guides · anonymous.

## Locations (`/admin/locations`) — Admin edits; others read-only

```
┌ Locations ─────────────────────────────────────────────── [+ New location] ┐
│ Code        Name              Address                   Zone          Status │
│ LONDON_HQ   London HQ         1 Example St, London      Europe/London Active │ [Edit]
│ DUBLIN      Dublin Centre     2 Sample Rd, Dublin       Europe/Dublin Active │ [Edit]
│ LEEDS       Leeds (closed)    …                         Europe/London Inactive│ [Reactivate]
└──────────────────────────────────────────────────────────────────────────────┘
```

**Captures:** `code` (on create only), `name`, `address` and `timeZoneId`. The zone is picked from a
searchable IANA list showing the current offset.

**Actions and audit:**

- Create → `LocationCreated`.
- Edit → `LocationUpdated`. A zone change is disabled, with the reason, while the location has
  open proposals or future events (FR-1.2).
- Deactivate or reactivate → `LocationUpdated`. Deactivation is refused with blocking counts
  (FR-1.6).

**States:** loading · empty ("No locations yet. Add one before Managers can propose events.") ·
editing · conflict (values retained) · busy · error.

## Appointment types (`/admin/appointment-types`) — Admin edits; others read-only

**Displays:** `code`, `name`, status, and the current Manager's display name (or "No Manager
assigned" with a link to Staff access).

**Actions and audit:**

- Create → `AppointmentTypeCreated`.
- Rename → `AppointmentTypeUpdated`.
- Deactivate or reactivate → `AppointmentTypeUpdated`, refused with blocking counts where needed.

**States:** as for Locations.

## Attendee groups (`/admin/attendee-groups`) — Admin edits; others read-only

**Displays:** `code`, `name`, status, required type chips and member count.

**Captures:** name, and the required types through the multi-select type picker (at least one).

**Actions and audit:**

- Create → `AttendeeGroupCreated`.
- Edit name or requirements → `AttendeeGroupUpdated`. Before saving, a requirements change shows
  "This changes requirements for N attendees and replaces their pending invitations." It is refused
  if any member has an active booking, showing the count (FR-1.5).
- Deactivate → refused while the group has members.

**States:** as for Locations, plus requirements-change confirmation and blocked (with count).

## Settings (`/admin/settings`) — Admin

**Captures:** `inviteExpiryDays` (1–60), `maxAutoRetryCount` (0–10) and `inviteOptionCount` (1–5),
with a hint that changes apply to future invitations only.

**Actions and audit:** Save → `SystemSettingsChanged`.

**States:** loading · saved · conflict · error.

## Staff access (`/admin/staff-access`) — Admin

```
┌ Staff access ── Roles are assigned in the identity provider ───────────────────┐
│ Name            Staff no.  Roles                    Appointment type             │
│ Sam Patel       A10023     Admin                    —                            │
│ Jo Chen         A10044     Coordinator, Manager     Medical check        [Change]│
│ Ali Brown       A10051     AppointmentStaff         ⚠ Awaiting assignment [Assign]│
└──────────────────────────────────────────────────────────────────────────────────┘
```

**Displays:** one row per `StaffAccessProfile`, showing:

- display name and `StaffId`;
- read-only role chips;
- the scope, or an "Awaiting assignment" badge, or "—" for unscoped roles.

Empty state: "No one has signed in with an EventBooking role yet."

**Actions and audit:** assign, change or clear scope → `StaffAccessChanged`. Assigning Manager for
a type that already has one displaces the former Manager, and the saved confirmation names them
(FR-10.6). Nothing on this screen creates or deletes a profile, or edits roles.

**States:** loading · empty · saved (naming anyone displaced) · conflict · busy · error.

## Negotiation board (`/events/negotiate`) — Manager

```
┌ Negotiation — Medical check (your type) ─────────────────────── [+ Propose event] ┐
│ OPEN PROPOSALS                                                                      │
│ Location     When (local)                        Types           Accepted  You      │
│ London HQ    Tue 14 Oct 09:30–11:00 BST (90 min) MED FIT IND      2 of 3   [ 4 ] [Save] [Withdraw] │
│ Dublin       Wed 15 Oct 13:00–17:00 IST (4 h)    MED IND          1 of 2   [ — ] [Accept]          │
│ CONFIRMED EVENTS (your type)                                                        │
│ London HQ    Mon 13 Oct 09:00–13:00 BST          MED FIT          Total [ 6 ] Remaining 2 [Save] [Cancel event] │
└──────────────────────────────────────────────────────────────────────────────────────┘

┌ Propose event ─────────────────────────────────────────┐
│ Location      [ London HQ ▾ ]   (Europe/London)          │
│ Date          [ 2026-10-14 ]  Start [ 09:30 ]             │
│ Duration      [ 90 ] minutes  → ends 11:00 BST            │
│ Types         [x] MED Medical check (you, locked)         │
│               [x] FIT Equipment fitting                   │
│               [x] IND Induction                           │
│               [ ] ESC Escort briefing — no Manager        │
│ Your headcount [ 4 ]                                      │
│                                    [Cancel] [Propose]     │
└─────────────────────────────────────────────────────────┘
```

**Who:** Manager, scoped. The caller acts as the current Manager of their type (FR-2.10).

**Captures:**

- the proposal: location, date, start, duration (15-minute steps, derived end shown), types and
  own headcount;
- per-row acceptance headcount;
- per-event total headcount for the caller's type.

**Displays:** only proposals and events that list the caller's type. Other types show only
accepted or not accepted, never their headcount (FR-2.8).

**Actions and audit:**

- Propose → `ProposalCreated` and `AcceptanceRecorded`, then `EventConfirmed` if the proposal has a
  single type.
- Accept or revise → `AcceptanceRecorded` (only on a change), then `EventConfirmed` when the set
  completes.
- Withdraw acceptance → `AcceptanceWithdrawn`.
- Withdraw proposal (proposer's type only) → `ProposalWithdrawn`.
- Adjust total → `CapacityAdjusted`.
- Cancel event (two-step) → `EventCancelled` and its knock-on effects (FR-7.2). Disabled once the
  window has started.

**States:** loading · empty ("No open proposals for your type") · validation errors listed per
field (FR-2.2) · busy · capacity conflict (input retained, server value and minimum shown) ·
proposal no longer open (row refreshes) · cancel awaiting confirmation · error.

## Event operations (`/events/operations`) — Admin or Coordinator

**Displays:** every `Active` event whose window has not started, filterable by `Location` and date
range, cursor-paginated. Each row shows location, window, type chips with remaining/total, and
active booking count.

**Actions and audit:** Cancel event (two-step, naming the booking count) → FR-7.2.

**States:** loading · empty · cancel awaiting confirmation · conflict (the event changed; refresh)
· error.

## Attendees (`/attendees`) — Coordinator (never Admin)

```
┌ Attendees ───────── [Status ▾] [Group ▾] [Readiness ▾] [Search…]  [Upload CSV] [+ New] ┐
│ Name         Email            Group         Needs          Readiness        Status   Delivery │
│ R. Singh     r@example.org    Field staff   MED FIT IND    Outstanding: FIT⟲ Booked   Sent    │ [Arrange missed] [⋯]
│ T. Okafor    t@example.org    Office staff  IND            —                Awaiting Failed  │ [Invite…] [Resend]
└───────────────────────────────────────────────────────────────────────────────────────── [Load more] ┘

┌ Invite T. Okafor ─────────────────────────────┐
│ Needs: IND Induction                           │
│ Locations  [x] London HQ  [x] Dublin  [ ] Leeds│
│ Eligible events now: 5 (need 3)                │
│                         [Cancel] [Send invite] │
└───────────────────────────────────────────────┘
```

**Captures:**

- name, email and group, with the derived type chips previewed before saving;
- filters and search;
- the invite dialog's location selection, which shows a live count of eligible events;
- a CSV of `name,email,attendee_group`.

**Displays:** name, email, group, requirement chips, readiness (with a recoverable marker ⟲),
status and delivery.

**Actions and audit:**

- Create or edit → `AttendeeGroupAssigned` or `AttendeeGroupReassigned`.
- Upload CSV → all-or-nothing.
- Invite or re-invite → `InviteCreated` (with `InviteSent` after dispatch), or the attendee moves to
  `AwaitingAvailability`.
- Resend → email retry.
- Arrange missed appointments → `RecoveryInviteCreated`.
- Cancel recovery → `RecoveryInviteCancelled`.
- View bookings, then cancel a booking (two-step) → `BookingCancelled`.
- Delete (two-step) → `AttendeeDeleted`.
- Open audit history.

**States:** loading · empty · import errors · editing · requirement-change blocked · invite
dialog (with an "only N eligible events" warning) · busy · error.

## Dashboards (`/dashboards`) — Coordinator (never Admin)

**Displays:**

- the tabs Awaiting availability, No response and Events (FR-13.1);
- failed and pending email counts.

**Actions:**

- re-invite from No response, through the invite dialog;
- expand an event's audit history, which loads on first expand.

**States:** loading · empty per tab · busy · error.

## Appointment workspace (`/appointments`) — Manager or AppointmentStaff

```
┌ Appointments — Medical check ───── Event [ London HQ · Tue 14 Oct 09:30–11:00 BST ▾ ]  [Download roster] ┐
│ Expected 5 · Checked in 2 · Completed 1 · No-show 0                                                       │
│ Name        Email           Status      Checked in   Outcome                                             │
│ R. Singh    r@example.org   Expected    —            —          [Check in]                               │
│ M. Novak    m@example.org   CheckedIn   09:41        —          [Complete] [Undo check-in]               │
└───────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

**Displays:** see FR-8.1 to FR-8.2. The heading names the server-assigned type; there is no type
selector. The event selector groups events by location.

**Actions and audit:**

- Check in → `AppointmentCheckedIn`. Enabled only on the event's local date.
- Complete → `AppointmentCompleted`.
- No-show → `AppointmentMarkedNoShow`. Enabled only after the window ends.
- Corrections → `AppointmentStatusCorrected`.
- Download roster.

**States:** loading · no events · empty event · row busy · conflict (row refreshed and announced) ·
forbidden · error.

## Attendee booking (`/book/{token}`) and manage (`/manage/{token}`) — anonymous

```
┌ Choose a time ───────────────────────────────────┐
│ Hello Ravi. You need: Medical check, Induction    │
│ ○ Tue 14 Oct, 09:30–11:00 BST                      │
│   London HQ, 1 Example St, London                  │
│ ○ Wed 15 Oct, 13:00–17:00 IST                      │
│   Dublin Centre, 2 Sample Rd, Dublin               │
│ ○ Thu 16 Oct, 10:00–12:00 BST                      │
│   London HQ, 1 Example St, London                  │
│                              [Confirm this time]   │
└──────────────────────────────────────────────────┘
```

**Book:**

- Captures one option.
- Displays FR-6.2.
- Confirm → `BookingCreated` and `CapacityDecremented` for each type.
- If a place ran out, the option is removed with the message "That time has just filled up. Please
  choose another."
- Success shows the event, the location and the manage link.

**Manage:**

- Displays the booked event and appointment names.
- Cancel, or Cancel and choose a new time (disabled once started, FR-6.7), then one of the truthful
  outcomes (FR-6.8).

**States:** loading · expired or superseded link (replaces the page) · choose · no options
("We'll be in touch") · confirmed · manage · cancelled (4 wordings) · too late to cancel · busy ·
error.

## Audit search (`/audit`) — Admin or Coordinator

**Captures:**

- from and to dates;
- actor type (`Staff`, `AttendeeToken` or `System`, or any);
- `AuditAction`;
- entity type, from the caller's permitted buckets only;
- an exact entity id or actor id.

**Displays:** when, entity type, action, actor (the display name for staff, otherwise the id) and
`details`.

**Actions:** Search · Load more.

**States:** loading · empty · results with a next page · error.

## Audit history panel (shared component)

A collapsible panel inside a table row, for one event or one attendee. It loads on first expand.
The attendee panel is never available to Admin.

## Key flows

### Negotiation across N Managers

```mermaid
sequenceDiagram
    actor M1 as Manager (MED)
    actor M2 as Manager (FIT)
    actor M3 as Manager (IND)
    participant API
    participant DB
    M1->>API: POST /api/event-proposals {location, window, types [MED,FIT,IND], headcount 4}
    API->>DB: validate; insert proposal, 3 listed types, MED acceptance
    API-->>M1: 201 Open (1 of 3)
    par concurrent
        M2->>API: PUT /api/event-proposals/{id}/acceptance {headcount 6}
        API->>DB: SELECT proposal FOR UPDATE; insert FIT acceptance (2 of 3)
        API-->>M2: 200 Open (2 of 3)
    and
        M3->>API: PUT /api/event-proposals/{id}/acceptance {headcount 5}
        API->>DB: waits for proposal lock, then insert IND acceptance (3 of 3)
        API->>DB: set Confirmed; insert Event + EventCapacity MED 4, FIT 6, IND 5
        API-->>M3: 200 Confirmed (eventId)
    end
```

### Booking race for the last place

```mermaid
sequenceDiagram
    actor A as Attendee A (needs MED, IND)
    actor B as Attendee B (needs IND)
    participant API
    participant DB
    A->>API: POST /api/booking/{tokenA}/confirm {eventId E}
    B->>API: POST /api/booking/{tokenB}/confirm {eventId E}
    API->>DB: A: lock attendee A; lock EventCapacity(E,MED), (E,IND)
    API->>DB: B: lock attendee B; wait on EventCapacity(E,IND)
    API->>DB: A: IND remaining 1 → 0, MED 3 → 2; insert Booking; commit
    API-->>A: 201 Booked
    API->>DB: B: IND remaining 0 → reject; rollback
    API-->>B: 409 capacity-exhausted (option removed, others offered)
```

### Event cancellation

```mermaid
sequenceDiagram
    actor C as Coordinator
    participant API
    participant DB
    participant Outbox
    C->>API: POST /api/events/{id}/cancel
    API-->>C: 409 confirmation-required {activeBookings: 3}
    C->>API: POST /api/events/{id}/cancel?confirm=true
    API->>DB: lock event; for each booking (attendee id order): lock attendee, capacity; cancel booking; +1 capacity
    API->>DB: issue replacement Invite from original InviteLocations (or AwaitingAvailability)
    API->>DB: insert EmailLog Pending (EventCancelledRebookingNeeded, AttendeeInvite)
    API-->>C: 200 {cancelled: 3, reinvited: 2, awaitingAvailability: 1}
    Outbox->>DB: claim Pending (SKIP LOCKED); send via SMTP; mark Sent/Failed
```
