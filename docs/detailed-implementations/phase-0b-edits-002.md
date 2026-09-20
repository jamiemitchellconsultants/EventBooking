# 00b — Vocabulary edits 2 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — docs/user-guides/coordinator-guide.md — 1/1

<!-- vocabulary-file: {"id":4,"oldPath":"docs/user-guides/coordinator-guide.md","newPath":"docs/user-guides/coordinator-guide.md","beforeSha":"5de0b40e07963664a03e5695ebfaded31d6c0e4b9e8b321e9f587131eee6aae0","afterSha":"8270ca6d6f5e679d917d020cd10115a6756b45ecb47b5b9e86807ebb45a5dcbb","side":"before","part":1,"parts":1} -->

`````markdown
# Coordinator guide

[← All user guides](README.md)

As a Coordinator, you own the candidate journey from initial record through invitation, booking,
readiness, and missed-appointment recovery. EventBooking derives every required Appointment Type
from the candidate's Employee Group; you never add or remove requirements individually. You can also
cancel a candidate's booking on their behalf, cancel a whole confirmed window, and search the full
audit trail.

## Your workflow

1. Check **Dashboards** for candidates waiting for availability or follow-up.
2. Ensure at least three suitable future Confirmed Slots exist. Ask Managers to negotiate them or
   import already-agreed capacity on **Confirmed slots**.
3. Add candidates on **Candidates**, manually or with a CSV containing one Employee Group per row.
4. Review the derived Appointment Type chips, then select **Invite now**.
5. Monitor invitation and email status until the candidate books.
6. After delivery staff record outcomes, review each candidate's readiness.
7. If the latest unsatisfied attempt is No-show, select **Arrange missed appointments** and monitor
   the recovery invitation until the missed types are completed.
8. When a plan changes, cancel the candidate's booking from their row — with or without sending
   fresh options — rather than asking them to find their emailed link.

## Home page

After signing in, the home-page access summary should include **Coordinator**. It provides:

- **Candidates** (`/candidates`)
- **Dashboards** (`/dashboards`)
- **Confirmed slots** (`/confirmed-slots`)
- **Audit trail** (`/audit`)
- **Help** (`/help`) — every role's guide, including the candidate guide

The same links also appear as a navigation bar at the top of every page, so you can switch
workspaces without returning to the home page first.

If your profile also contains Manager or Appointment staff, the home page also shows their
workspaces. Those actions remain scoped to the one Appointment Type shown in the access summary.
Roles themselves are assigned in the company identity provider, not by a EventBooking Admin; an
Admin only sets the Appointment Type that scoped roles work within.

## Candidates screen

Use `/candidates` for candidate records, invitations, delivery status, readiness, bookings, history,
and recovery. There is no outbound onboarding integration and no readiness export: recovery is
arranged only through this screen (or the matching Coordinator API endpoints), never through MCP.

![Candidates screen with the add-candidate row and existing candidate list](screenshots/candidates-list.png)

### Find the correct work queue

- Use **Status** to filter by Not yet invited, Awaiting availability, Invited (pending response),
  Booked, No response - needs follow-up, or Cancelled.
- Enter a name or email in **Search by name or email**, then select **Search**.
- Clear the search and choose **Any status** to return to the full list.

### Add one candidate

The first table row is the add form.

1. Enter **Full name** and **Email address**.
2. Choose the required **Employee Group**.
3. Check the read-only Appointment Type chips previewed beneath the group. These are derived from
   the approved mapping and cannot be edited.
4. Select **Save candidate**.
5. Confirm the saved row shows the expected group name/code and derived types.

![Add-candidate row filled in with a name, email, and Cabin Crew employee group, showing the derived DAT/MED/UNI chips](screenshots/candidate-add-form.png)

The mappings are:

| Employee Group | CSV code | Derived Appointment Types |
|---|---|---|
| Cabin Crew | `CABIN_CREW` | DAT, MED, UNI |
| Pilots | `PILOTS` | DAT, UNI |
| Ground Operations Agent | `GROUND_OPERATIONS_AGENT` | MED |
| Engineering | `ENGINEERING` | MED |
| Ground Transport Services | `GROUND_TRANSPORT_SERVICES` | DAT, MED, UNI |

### Import candidates

For a batch, prepare a CSV with the exact header:

```text
name,email,employee_group
```

The `employee_group` field contains one code from the table above. Surrounding whitespace and letter
case are accepted, but EventBooking stores and displays the canonical uppercase code.

1. Select **Upload CSV** and choose a `.csv` file no larger than 1 MiB.
2. Wait while every row is validated.
3. If accepted, the list reloads with the new candidates.
4. If **Nothing was imported** appears, correct every line-numbered error and upload the whole file
   again.

The import is all-or-nothing. Blank or unknown group codes, duplicate emails in the file or system,
invalid names or emails, and malformed rows prevent every row from being added. The old
`appointment_types` column is not accepted.

### Edit a candidate

1. Select **Edit** on the candidate's row.
2. Change the name, email, or Employee Group.
3. Review the newly derived Appointment Type chips.
4. Read any warning, then select **Save**; select **Cancel** to discard the edit.

The effect of an Employee Group change depends on the booking journey:

- If the new group derives the same Appointment Types, EventBooking preserves pending invitations,
  active bookings, capacity, and appointment history.
- If it changes the required types before booking, a pending invitation is invalidated and the
  candidate returns to Not yet invited. Select **Invite now** after saving to send correct options.
- If it changes the required types during an active original Booking, the save is blocked. Cancel
  the booking from the **Booking** column first — **Cancel & rebook** keeps the candidate moving —
  then change the group. The entered edit values remain on screen while the save is refused.

Cabin Crew and Ground Transport Services are set-equivalent to each other. Ground Operations Agent
and Engineering are also set-equivalent.

### Read the candidate row

- **Required types** contains read-only code chips derived from the Employee Group. A legacy record
  with no group instead shows **Employee Group required**: edit it and choose a group before
  inviting.
- **Status** shows the invitation/booking stage.
- **Readiness** gives a compact result. Expand it for the outstanding Appointment Types.
- **Delivery** shows the latest candidate email. For a retryable Failed or Pending message, select
  **Resend**.
- **Booking** loads the active Bookings on demand and holds the cancellation actions.
- **History** expands the recorded changes for that candidate.
- **Actions** contains Edit, Invite now, and Delete. Delete confirms on a second click.

### Interpret readiness

| Screen text | Meaning | Next action |
|---|---|---|
| All required appointments completed | Every current required type has a Completed outcome | No appointment action is needed |
| Not ready — no active booking | The candidate has not confirmed an active original Booking | Send or follow up an invitation |
| Not ready, with outstanding type chips | One or more required types are Expected, Checked in, or No-show | Wait for delivery, or arrange recovery if the screen offers it |
| Readiness unavailable — contact support | Stored group, Booking, and appointment snapshots do not agree | Stop; do not re-invite or edit around the warning |

Only a Completed outcome satisfies a type. Expected, Checked in, and No-show remain outstanding. An
outstanding type is labelled **(recoverable)** when its latest attempt was a No-show.

### Send an initial invitation

1. Confirm the Employee Group and derived types are correct.
2. Select **Invite now**.
3. Wait for the row to refresh to Invited and review **Delivery**.

![Candidate row showing status Invited (pending response) and the delivery timestamp](screenshots/candidate-invited.png)

EventBooking selects exactly three future Confirmed Slots with remaining capacity for every derived
type and snapshots those requirements into the invitation. If fewer than three suitable options
exist, no invitation is created and the candidate moves to Awaiting availability. Create more
capacity, then try again.

### Cancel a candidate's booking

The **Booking** column shows a **Bookings** button. Select it to load and expand the list; the
button then reads **No active booking**, **1 active booking**, or a count. Each entry shows the date and four-hour window, marked **(recovery)** when it is a
recovery Booking rather than the original.

![Expanded Booking column for a booked candidate, with Cancel & rebook armed and showing Confirm cancel](screenshots/candidate-booking.png)

Two actions are offered, each confirming on its own second click:

1. **Cancel booking** releases the places and stops there. No replacement options are sent, so the
   candidate needs a fresh invitation from you if they still need an appointment.
2. **Cancel & rebook** releases the places and emails the candidate a fresh invitation with new
   options. It appears on the original Booking only — a recovery Booking is never auto-replaced.

The result appears under the list as one of:

- **Booking cancelled.**
- **Booking cancelled; replacement invite sent.**
- **Booking cancelled; replacement invite could not be delivered.** — the cancellation still
  happened. Check the email address and follow up manually.

Cancelling the original Booking cancels that appointment journey, including any pending or active
recovery. Cancelling a recovery Booking leaves the original journey and completed work intact.

### Arrange missed appointments

**Arrange missed appointments** appears only when at least one current outstanding type is
recoverable. A type becomes recoverable when its latest non-cancelled attempt is No-show and no
Completed attempt already satisfies it.

1. Expand readiness and review the recoverable type names.
2. Select **Arrange missed appointments**.
3. Confirm the resulting message lists exactly the missed types and reports the email outcome.
4. While the recovery invitation is Pending, use **Cancel recovery** only if it must be withdrawn;
   confirm the cancellation when prompted.
5. Monitor the candidate's readiness after they book and the replacement appointments are delivered.

Recovery never repeats an already Completed type. It requires three future slots with capacity for
all missed types in that recovery. Expected or Checked-in work cannot be recovered; staff must first
record the correct outcome. If the candidate misses the replacement too, a new recovery can be
arranged after that recovery Booking concludes.

### Read a candidate's history

Every candidate row carries a **History** disclosure. Expanding it loads the recorded changes to
the candidate record itself (such as an Employee Group being assigned or changed) and to that
candidate's invitations, Bookings, and Booking Appointments, newest first, with When, What, Who, and
Details. It is loaded on demand, so opening a long candidate list costs nothing until you ask for a
history.

![Expanded History on a candidate row, listing BookingCreated, InviteSent, InviteCreated, and EmployeeGroupAssigned entries with When, What, Who, and Details](screenshots/candidate-history.png)

Use History for a single candidate's story. Use the Audit trail screen when you need to search
across candidates, dates, or actions.

## Dashboards screen

Use `/dashboards` as the start-of-day and follow-up view.

![Dashboards screen showing the Slots tab with capacity by type and active booking counts](screenshots/dashboards.png)

- **Awaiting availability** lists candidates who could not receive three suitable options, their
  required types, and how long they have waited. Arrange more capacity before returning to their
  Candidate row and inviting again.
- **No response** lists candidates whose invitation follow-up window ended. Select **Re-invite now**
  after confirming their email and continued need.
- **Slots** shows each Confirmed Slot, capacity remaining/total by type, and active Booking count.
- Failed and Pending email totals appear beside the tabs. Resolve them from the Candidate row.
- Every tab carries the same **History** disclosure per row — per candidate on the first two tabs,
  per slot on Slots.

The dashboard tabs support mouse, touch, and keyboard. With focus on the tab list, use Left/Right,
Home, or End to change views.

## Confirmed slots screen

Use `/confirmed-slots` when all three teams have already agreed a complete window outside
EventBooking, and to cancel a window that cannot run. Admins see the same screen.

![Confirmed slots screen with CSV import control](screenshots/confirmed-slots-import.png)

### Import already agreed slots

The exact header is:

```text
date,startTime,DAT,MED,UNI
```

Choose the file in **Import already agreed slots**. Each row uses a `yyyy-MM-dd` date, `HH:mm` start
time, and positive capacity for all three types. Use future dates for bookable capacity; past slots
are never offered. A valid future import becomes bookable immediately. If any row is invalid,
nothing is imported and the screen lists every line error.

### Cancel a confirmed slot

The **Cancel a confirmed slot** card lists every confirmed window with its capacity by type and the
number of active Bookings. Select **Cancel slot**; if the window holds active Bookings the request
is refused with a warning, and the button becomes **Confirm cancel**.

![Cancel a confirmed slot card listing each window's date, capacity by type, active bookings, and a Cancel slot button](screenshots/confirmed-slots-cancel.png)

Cancelling voids every Booking on that window, releases the capacity, and starts the candidate
rebooking workflow — so tell the delivery teams first, and expect the affected candidates to need
watching afterwards. A Manager can cancel the same window from their own screen, so agree who is
acting before anyone clicks.

## Audit trail screen

Use `/audit` to search across the whole record. As a Coordinator you see both candidate entries
(candidates, invitations, bookings, and booking appointments) and operational entries (slot
proposals, confirmed slots, and staff access profiles).

![Audit trail screen with From, To, Actor, Action, Identifier, and Entity filters above newest-first results](screenshots/audit-trail.png)

1. Set **From** and **To** to bound the period. Both are optional.
2. Choose an **Actor**: Staff, CandidateToken (a candidate acting through their emailed link), or
   System.
3. Choose an **Action** to narrow to one recorded change, such as InviteSent, BookingCreated,
   BookingCancelled, AppointmentMarkedNoShow, RecoveryInviteCreated, or SlotCancelled.
4. Enter an **Identifier** to match one audited entity id or actor id exactly.
5. Narrow further with **Entity** when you want only one kind of record.
6. Select **Search**, then **Load more** to page further back. Results are newest first.

**Nothing matches these filters** means the search ran and found nothing, not that it failed.

## Troubleshooting

- **Invite cannot be created** — fewer than three suitable slots exist, the candidate data is
  inconsistent, or another action changed state. Refresh, fix the stated dependency, and retry.
- **Invitation link is invalid** — it may be expired, used, cancelled, or superseded by an Employee
  Group change. Verify the row and send a fresh invitation where appropriate.
- **Employee Group change is blocked** — it would alter the requirement set of an active original
  Booking. Cancel the booking from the Booking column first, then change the group.
- **Invite now is refused for a legacy record** — the row shows Employee Group required. Edit it and
  assign a group.
- **Arrange missed appointments is absent** — no current type has a recoverable No-show, or a
  recovery invitation/Booking already exists.
- **Recovery cannot start** — create three suitable future slots, correct any concurrently changed
  appointment state, or finish/cancel the existing recovery first.
- **Cancel & rebook is missing on a booking** — that Booking is a recovery. Cancel it, then arrange
  a new recovery once the appointment state allows it.
- **Email failed after an invitation was created** — the invitation remains valid. Correct the email
  address if necessary and use **Resend**; do not create duplicate candidate records.
- **Readiness unavailable** — contact support. Do not try to repair snapshot mismatches through
  repeated edits or invitations.
`````

## after — docs/user-guides/coordinator-guide.md — 1/1

<!-- vocabulary-file: {"id":4,"oldPath":"docs/user-guides/coordinator-guide.md","newPath":"docs/user-guides/coordinator-guide.md","beforeSha":"5de0b40e07963664a03e5695ebfaded31d6c0e4b9e8b321e9f587131eee6aae0","afterSha":"8270ca6d6f5e679d917d020cd10115a6756b45ecb47b5b9e86807ebb45a5dcbb","side":"after","part":1,"parts":1} -->

`````markdown
# Coordinator guide

[← All user guides](README.md)

As a Coordinator, you own the attendee journey from initial record through invitation, booking,
readiness, and missed-appointment recovery. EventBooking derives every required Appointment Type
from the attendee's Attendee Group; you never add or remove requirements individually. You can also
cancel a attendee's booking on their behalf, cancel a whole confirmed window, and search the full
audit trail.

## Your workflow

1. Check **Dashboards** for attendees waiting for availability or follow-up.
2. Ensure at least three suitable future Confirmed Events exist. Ask Managers to negotiate them or
   import already-agreed capacity on **Events**.
3. Add attendees on **Attendees**, manually or with a CSV containing one Attendee Group per row.
4. Review the derived Appointment Type chips, then select **Invite now**.
5. Monitor invitation and email status until the attendee books.
6. After delivery staff record outcomes, review each attendee's readiness.
7. If the latest unsatisfied attempt is No-show, select **Arrange missed appointments** and monitor
   the recovery invitation until the missed types are completed.
8. When a plan changes, cancel the attendee's booking from their row — with or without sending
   fresh options — rather than asking them to find their emailed link.

## Home page

After signing in, the home-page access summary should include **Coordinator**. It provides:

- **Attendees** (`/attendees`)
- **Dashboards** (`/dashboards`)
- **Events** (`/events`)
- **Audit trail** (`/audit`)
- **Help** (`/help`) — every role's guide, including the attendee guide

The same links also appear as a navigation bar at the top of every page, so you can switch
workspaces without returning to the home page first.

If your profile also contains Manager or Appointment staff, the home page also shows their
workspaces. Those actions remain scoped to the one Appointment Type shown in the access summary.
Roles themselves are assigned in the company identity provider, not by a EventBooking Admin; an
Admin only sets the Appointment Type that scoped roles work within.

## Attendees screen

Use `/attendees` for attendee records, invitations, delivery status, readiness, bookings, history,
and recovery. There is no outbound onboarding integration and no readiness export: recovery is
arranged only through this screen (or the matching Coordinator API endpoints), never through MCP.

![Attendees screen with the add-attendee row and existing attendee list](screenshots/attendees-list.png)

### Find the correct work queue

- Use **Status** to filter by Not yet invited, Awaiting availability, Invited (pending response),
  Booked, No response - needs follow-up, or Cancelled.
- Enter a name or email in **Search by name or email**, then select **Search**.
- Clear the search and choose **Any status** to return to the full list.

### Add one attendee

The first table row is the add form.

1. Enter **Full name** and **Email address**.
2. Choose the required **Attendee Group**.
3. Check the read-only Appointment Type chips previewed beneath the group. These are derived from
   the approved mapping and cannot be edited.
4. Select **Save attendee**.
5. Confirm the saved row shows the expected group name/code and derived types.

![Add-attendee row filled in with a name, email, and Cabin Crew attendee group, showing the derived DAT/MED/UNI chips](screenshots/attendee-add-form.png)

The mappings are:

| Attendee Group | CSV code | Derived Appointment Types |
|---|---|---|
| Cabin Crew | `CABIN_CREW` | DAT, MED, UNI |
| Pilots | `PILOTS` | DAT, UNI |
| Ground Operations Agent | `GROUND_OPERATIONS_AGENT` | MED |
| Engineering | `ENGINEERING` | MED |
| Ground Transport Services | `GROUND_TRANSPORT_SERVICES` | DAT, MED, UNI |

### Import attendees

For a batch, prepare a CSV with the exact header:

```text
name,email,attendee_group
```

The `attendee_group` field contains one code from the table above. Surrounding whitespace and letter
case are accepted, but EventBooking stores and displays the canonical uppercase code.

1. Select **Upload CSV** and choose a `.csv` file no larger than 1 MiB.
2. Wait while every row is validated.
3. If accepted, the list reloads with the new attendees.
4. If **Nothing was imported** appears, correct every line-numbered error and upload the whole file
   again.

The import is all-or-nothing. Blank or unknown group codes, duplicate emails in the file or system,
invalid names or emails, and malformed rows prevent every row from being added. The old
`appointment_types` column is not accepted.

### Edit a attendee

1. Select **Edit** on the attendee's row.
2. Change the name, email, or Attendee Group.
3. Review the newly derived Appointment Type chips.
4. Read any warning, then select **Save**; select **Cancel** to discard the edit.

The effect of an Attendee Group change depends on the booking journey:

- If the new group derives the same Appointment Types, EventBooking preserves pending invitations,
  active bookings, capacity, and appointment history.
- If it changes the required types before booking, a pending invitation is invalidated and the
  attendee returns to Not yet invited. Select **Invite now** after saving to send correct options.
- If it changes the required types during an active original Booking, the save is blocked. Cancel
  the booking from the **Booking** column first — **Cancel & rebook** keeps the attendee moving —
  then change the group. The entered edit values remain on screen while the save is refused.

Cabin Crew and Ground Transport Services are set-equivalent to each other. Ground Operations Agent
and Engineering are also set-equivalent.

### Read the attendee row

- **Required types** contains read-only code chips derived from the Attendee Group. A legacy record
  with no group instead shows **Attendee Group required**: edit it and choose a group before
  inviting.
- **Status** shows the invitation/booking stage.
- **Readiness** gives a compact result. Expand it for the outstanding Appointment Types.
- **Delivery** shows the latest attendee email. For a retryable Failed or Pending message, select
  **Resend**.
- **Booking** loads the active Bookings on demand and holds the cancellation actions.
- **History** expands the recorded changes for that attendee.
- **Actions** contains Edit, Invite now, and Delete. Delete confirms on a second click.

### Interpret readiness

| Screen text | Meaning | Next action |
|---|---|---|
| All required appointments completed | Every current required type has a Completed outcome | No appointment action is needed |
| Not ready — no active booking | The attendee has not confirmed an active original Booking | Send or follow up an invitation |
| Not ready, with outstanding type chips | One or more required types are Expected, Checked in, or No-show | Wait for delivery, or arrange recovery if the screen offers it |
| Readiness unavailable — contact support | Stored group, Booking, and appointment snapshots do not agree | Stop; do not re-invite or edit around the warning |

Only a Completed outcome satisfies a type. Expected, Checked in, and No-show remain outstanding. An
outstanding type is labelled **(recoverable)** when its latest attempt was a No-show.

### Send an initial invitation

1. Confirm the Attendee Group and derived types are correct.
2. Select **Invite now**.
3. Wait for the row to refresh to Invited and review **Delivery**.

![Attendee row showing status Invited (pending response) and the delivery timestamp](screenshots/attendee-invited.png)

EventBooking selects exactly three future Confirmed Events with remaining capacity for every derived
type and snapshots those requirements into the invitation. If fewer than three suitable options
exist, no invitation is created and the attendee moves to Awaiting availability. Create more
capacity, then try again.

### Cancel a attendee's booking

The **Booking** column shows a **Bookings** button. Select it to load and expand the list; the
button then reads **No active booking**, **1 active booking**, or a count. Each entry shows the date and four-hour window, marked **(recovery)** when it is a
recovery Booking rather than the original.

![Expanded Booking column for a booked attendee, with Cancel & rebook armed and showing Confirm cancel](screenshots/attendee-booking.png)

Two actions are offered, each confirming on its own second click:

1. **Cancel booking** releases the places and stops there. No replacement options are sent, so the
   attendee needs a fresh invitation from you if they still need an appointment.
2. **Cancel & rebook** releases the places and emails the attendee a fresh invitation with new
   options. It appears on the original Booking only — a recovery Booking is never auto-replaced.

The result appears under the list as one of:

- **Booking cancelled.**
- **Booking cancelled; replacement invite sent.**
- **Booking cancelled; replacement invite could not be delivered.** — the cancellation still
  happened. Check the email address and follow up manually.

Cancelling the original Booking cancels that appointment journey, including any pending or active
recovery. Cancelling a recovery Booking leaves the original journey and completed work intact.

### Arrange missed appointments

**Arrange missed appointments** appears only when at least one current outstanding type is
recoverable. A type becomes recoverable when its latest non-cancelled attempt is No-show and no
Completed attempt already satisfies it.

1. Expand readiness and review the recoverable type names.
2. Select **Arrange missed appointments**.
3. Confirm the resulting message lists exactly the missed types and reports the email outcome.
4. While the recovery invitation is Pending, use **Cancel recovery** only if it must be withdrawn;
   confirm the cancellation when prompted.
5. Monitor the attendee's readiness after they book and the replacement appointments are delivered.

Recovery never repeats an already Completed type. It requires three future events with capacity for
all missed types in that recovery. Expected or Checked-in work cannot be recovered; staff must first
record the correct outcome. If the attendee misses the replacement too, a new recovery can be
arranged after that recovery Booking concludes.

### Read a attendee's history

Every attendee row carries a **History** disclosure. Expanding it loads the recorded changes to
the attendee record itself (such as an Attendee Group being assigned or changed) and to that
attendee's invitations, Bookings, and Booking Appointments, newest first, with When, What, Who, and
Details. It is loaded on demand, so opening a long attendee list costs nothing until you ask for a
history.

![Expanded History on a attendee row, listing BookingCreated, InviteSent, InviteCreated, and AttendeeGroupAssigned entries with When, What, Who, and Details](screenshots/attendee-history.png)

Use History for a single attendee's story. Use the Audit trail screen when you need to search
across attendees, dates, or actions.

## Dashboards screen

Use `/dashboards` as the start-of-day and follow-up view.

![Dashboards screen showing the Events tab with capacity by type and active booking counts](screenshots/dashboards.png)

- **Awaiting availability** lists attendees who could not receive three suitable options, their
  required types, and how long they have waited. Arrange more capacity before returning to their
  Attendee row and inviting again.
- **No response** lists attendees whose invitation follow-up window ended. Select **Re-invite now**
  after confirming their email and continued need.
- **Events** shows each Confirmed Event, capacity remaining/total by type, and active Booking count.
- Failed and Pending email totals appear beside the tabs. Resolve them from the Attendee row.
- Every tab carries the same **History** disclosure per row — per attendee on the first two tabs,
  per event on Events.

The dashboard tabs support mouse, touch, and keyboard. With focus on the tab list, use Left/Right,
Home, or End to change views.

## Events screen

Use `/events` when all three teams have already agreed a complete window outside
EventBooking, and to cancel a window that cannot run. Admins see the same screen.

![Events screen with CSV import control](screenshots/events-import.png)

### Import already agreed events

The exact header is:

```text
date,startTime,DAT,MED,UNI
```

Choose the file in **Import already agreed events**. Each row uses a `yyyy-MM-dd` date, `HH:mm` start
time, and positive capacity for all three types. Use future dates for bookable capacity; past events
are never offered. A valid future import becomes bookable immediately. If any row is invalid,
nothing is imported and the screen lists every line error.

### Cancel a event

The **Cancel a event** card lists every confirmed window with its capacity by type and the
number of active Bookings. Select **Cancel event**; if the window holds active Bookings the request
is refused with a warning, and the button becomes **Confirm cancel**.

![Cancel a event card listing each window's date, capacity by type, active bookings, and a Cancel event button](screenshots/events-cancel.png)

Cancelling voids every Booking on that window, releases the capacity, and starts the attendee
rebooking workflow — so tell the delivery teams first, and expect the affected attendees to need
watching afterwards. A Manager can cancel the same window from their own screen, so agree who is
acting before anyone clicks.

## Audit trail screen

Use `/audit` to search across the whole record. As a Coordinator you see both attendee entries
(attendees, invitations, bookings, and booking appointments) and operational entries (event
proposals, events, and staff access profiles).

![Audit trail screen with From, To, Actor, Action, Identifier, and Entity filters above newest-first results](screenshots/audit-trail.png)

1. Set **From** and **To** to bound the period. Both are optional.
2. Choose an **Actor**: Staff, AttendeeToken (a attendee acting through their emailed link), or
   System.
3. Choose an **Action** to narrow to one recorded change, such as InviteSent, BookingCreated,
   BookingCancelled, AppointmentMarkedNoShow, RecoveryInviteCreated, or EventCancelled.
4. Enter an **Identifier** to match one audited entity id or actor id exactly.
5. Narrow further with **Entity** when you want only one kind of record.
6. Select **Search**, then **Load more** to page further back. Results are newest first.

**Nothing matches these filters** means the search ran and found nothing, not that it failed.

## Troubleshooting

- **Invite cannot be created** — fewer than three suitable events exist, the attendee data is
  inconsistent, or another action changed state. Refresh, fix the stated dependency, and retry.
- **Invitation link is invalid** — it may be expired, used, cancelled, or superseded by an Employee
  Group change. Verify the row and send a fresh invitation where appropriate.
- **Attendee Group change is blocked** — it would alter the requirement set of an active original
  Booking. Cancel the booking from the Booking column first, then change the group.
- **Invite now is refused for a legacy record** — the row shows Attendee Group required. Edit it and
  assign a group.
- **Arrange missed appointments is absent** — no current type has a recoverable No-show, or a
  recovery invitation/Booking already exists.
- **Recovery cannot start** — create three suitable future events, correct any concurrently changed
  appointment state, or finish/cancel the existing recovery first.
- **Cancel & rebook is missing on a booking** — that Booking is a recovery. Cancel it, then arrange
  a new recovery once the appointment state allows it.
- **Email failed after an invitation was created** — the invitation remains valid. Correct the email
  address if necessary and use **Resend**; do not create duplicate attendee records.
- **Readiness unavailable** — contact support. Do not try to repair snapshot mismatches through
  repeated edits or invitations.
`````

## before — docs/user-guides/manager-guide.md — 1/1

<!-- vocabulary-file: {"id":5,"oldPath":"docs/user-guides/manager-guide.md","newPath":"docs/user-guides/manager-guide.md","beforeSha":"54b39f2c36e75c66404fd8003b3751e79e4592e601098d457a477e7c89196a0e","afterSha":"59e289e70d89ed03596d3e57b15070e91a5a4b8448baa173787ce19d5026f4f3","side":"before","part":1,"parts":1} -->

`````markdown
# Manager guide

[← All user guides](README.md)

As a Manager, you own capacity for one Appointment Type: Drug & Alcohol Testing, Medical Check-up,
or Uniform Fitting. You negotiate shared four-hour windows with the other two Managers, maintain
your type's capacity, and can use the scoped Appointments workspace to deliver the service.

## Your workflow

1. On **Slot proposals**, review open proposals and create any new windows your team can support.
2. Accept each workable proposal with your team's headcount. All three Managers must accept before
   the window becomes bookable.
3. On confirmed slots, keep your total capacity accurate and never reduce it below active demand.
4. Before and during the window, use **Appointments** to monitor Expected candidates and record your
   type's outcomes if you are part of delivery.
5. Tell Coordinators promptly about shortages or cancellations because their invitations depend on
   the capacity you control.

## Home page

After signing in, check that the access summary shows **Manager** and the correct Appointment Type.
The home page provides:

- **Slot proposals** (`/slots`)
- **Appointments** (`/appointments`)
- **Help** (`/help`) — every role's guide, including the candidate guide

![Manager home page showing Slot proposals and Appointments links](screenshots/manager-home.png)

The same links also appear as a navigation bar at the top of every page, so you can move between
Slot proposals and Appointments without returning to the home page first.

A combined Coordinator profile also shows Candidates, Dashboards, Confirmed slots, and Audit trail.
Manager data remains scoped: you never see another Manager's headcount or another Appointment Type's
appointment rows.

### If your Appointment Type is missing or wrong

Your **role** comes from the company identity provider. Your **Appointment Type** is set inside
EventBooking by an Admin. If the access summary shows Manager with no type — or the wrong type —
Slot proposals and Appointments will not work for you. Ask an Admin to set the scope on their Staff
access screen. Do not act in the wrong workspace in the meantime.

## Slot proposals screen

Use `/slots` to propose windows, accept or withdraw, adjust capacity, and cancel Confirmed Slots.

![Slot proposals screen showing open proposals and each type's acceptance chips](screenshots/slot-proposals.png)

### Propose a new slot

1. In **Propose a new slot**, choose a future **Date**.
2. Choose the **Start time**. Every window lasts exactly four hours.
3. Select **Submit proposal**.
4. Find the new row under **Open proposals** and, when ready, enter your own headcount and accept it.

Submitting a proposal does not itself provide your team's acceptance or headcount.

### Review and accept an open proposal

Each row shows Date, Window, Accepted by, My headcount, and Actions.

1. Check the date and four-hour window.
2. Review the **Accepted by** chips to see which Appointment Types have agreed. Their headcount
   values remain private.
3. Enter a positive number in **My headcount**.
4. Select **Accept**.

After your acceptance, the action reads **Update acceptance**. You can replace your own headcount
while the proposal remains open. When the third Manager accepts, EventBooking creates the Confirmed
Slot immediately; Coordinators can then invite candidates against it.

### Withdraw before confirmation

- Select **Withdraw acceptance** to remove your own acceptance while the proposal remains open.
- If you created the proposal, select **Withdraw proposal** to withdraw the entire proposal.

Neither action is available after confirmation. A confirmed window must be managed as a Confirmed
Slot.

### Adjust confirmed capacity

Under **Confirmed slots (this type)**, each row shows your total and remaining capacity only.

1. Enter the replacement total in **My total**.
2. Confirm **Remaining** still makes operational sense.
3. Select **Adjust headcount**.

The new total must be positive and cannot be lower than the number of active Bookings requiring your
type. If a candidate books at the same time, the save may be rejected to prevent overbooking.
Reload the board and enter a total that covers the updated demand.

Employee Groups determine which types each candidate needs. You do not need to know the group to
manage capacity: EventBooking reserves only the capacity required by each invitation or recovery
snapshot.

### Cancel a Confirmed Slot

Cancel only when the whole four-hour window cannot run.

1. Warn Coordinator and delivery colleagues first.
2. Select **Cancel slot**.
3. If active Bookings are affected, read the warning and select **Confirm cancel** only when you
   intend to proceed.
4. Tell the Coordinator to monitor candidates and replacement invitation delivery.

Cancellation voids Bookings on the slot, releases their capacity, and triggers the appropriate
candidate rebooking workflow. Cancelling a recovery slot preserves the original journey and
already Completed appointments.

Admins and Coordinators can cancel the same window from their own Confirmed slots screen, so agree
who is acting before anyone clicks.

## Appointments screen

Managers have the same delivery controls as Appointment staff within their own Appointment Type,
including **Download roster** for a printable working list. Use `/appointments` when you are
recording day-of work. See the [Appointment staff guide](appointment-staff-guide.md) for the
complete screen workflow, screenshots, and correction rules.

## Troubleshooting

- **The wrong Appointment Type is shown** — stop and ask an Admin to correct your scope on Staff
  access. Do not act in the wrong workspace.
- **No Appointment Type is shown at all** — an Admin has not scoped your profile yet. The role alone
  is not enough.
- **Accept or update failed** — the proposal was changed, withdrawn, or confirmed while you were
  viewing it. Reload the board.
- **Another Manager's headcount is missing** — expected. You can see who accepted, not their number.
- **Capacity change was rejected** — the total is invalid, below active demand, or demand changed
  concurrently. Reload and recalculate.
- **Candidates are awaiting availability** — propose more future windows or accept open proposals.
  An invitation needs three suitable Confirmed Slots across every required type.
- **Cancel requires a second confirmation** — active Bookings are affected. Coordinate first; the
  second click performs the cancellation.
- **A window disappeared from your board** — an Admin or Coordinator may have cancelled it. Ask the
  Coordinator to check the audit trail; Managers do not have that screen.
`````

## after — docs/user-guides/manager-guide.md — 1/1

<!-- vocabulary-file: {"id":5,"oldPath":"docs/user-guides/manager-guide.md","newPath":"docs/user-guides/manager-guide.md","beforeSha":"54b39f2c36e75c66404fd8003b3751e79e4592e601098d457a477e7c89196a0e","afterSha":"59e289e70d89ed03596d3e57b15070e91a5a4b8448baa173787ce19d5026f4f3","side":"after","part":1,"parts":1} -->

`````markdown
# Manager guide

[← All user guides](README.md)

As a Manager, you own capacity for one Appointment Type: Drug & Alcohol Testing, Medical Check-up,
or Uniform Fitting. You negotiate shared four-hour windows with the other two Managers, maintain
your type's capacity, and can use the scoped Appointments workspace to deliver the service.

## Your workflow

1. On **Event proposals**, review open proposals and create any new windows your team can support.
2. Accept each workable proposal with your team's headcount. All three Managers must accept before
   the window becomes bookable.
3. On events, keep your total capacity accurate and never reduce it below active demand.
4. Before and during the window, use **Appointments** to monitor Expected attendees and record your
   type's outcomes if you are part of delivery.
5. Tell Coordinators promptly about shortages or cancellations because their invitations depend on
   the capacity you control.

## Home page

After signing in, check that the access summary shows **Manager** and the correct Appointment Type.
The home page provides:

- **Event proposals** (`/events`)
- **Appointments** (`/appointments`)
- **Help** (`/help`) — every role's guide, including the attendee guide

![Manager home page showing Event proposals and Appointments links](screenshots/manager-home.png)

The same links also appear as a navigation bar at the top of every page, so you can move between
Event proposals and Appointments without returning to the home page first.

A combined Coordinator profile also shows Attendees, Dashboards, Events, and Audit trail.
Manager data remains scoped: you never see another Manager's headcount or another Appointment Type's
appointment rows.

### If your Appointment Type is missing or wrong

Your **role** comes from the company identity provider. Your **Appointment Type** is set inside
EventBooking by an Admin. If the access summary shows Manager with no type — or the wrong type —
Event proposals and Appointments will not work for you. Ask an Admin to set the scope on their Staff
access screen. Do not act in the wrong workspace in the meantime.

## Event proposals screen

Use `/events` to propose windows, accept or withdraw, adjust capacity, and cancel Confirmed Events.

![Event proposals screen showing open proposals and each type's acceptance chips](screenshots/event-proposals.png)

### Propose a new event

1. In **Propose a new event**, choose a future **Date**.
2. Choose the **Start time**. Every window lasts exactly four hours.
3. Select **Submit proposal**.
4. Find the new row under **Open proposals** and, when ready, enter your own headcount and accept it.

Submitting a proposal does not itself provide your team's acceptance or headcount.

### Review and accept an open proposal

Each row shows Date, Window, Accepted by, My headcount, and Actions.

1. Check the date and four-hour window.
2. Review the **Accepted by** chips to see which Appointment Types have agreed. Their headcount
   values remain private.
3. Enter a positive number in **My headcount**.
4. Select **Accept**.

After your acceptance, the action reads **Update acceptance**. You can replace your own headcount
while the proposal remains open. When the third Manager accepts, EventBooking creates the Confirmed
Event immediately; Coordinators can then invite attendees against it.

### Withdraw before confirmation

- Select **Withdraw acceptance** to remove your own acceptance while the proposal remains open.
- If you created the proposal, select **Withdraw proposal** to withdraw the entire proposal.

Neither action is available after confirmation. A confirmed window must be managed as a Confirmed
Event.

### Adjust confirmed capacity

Under **Events (this type)**, each row shows your total and remaining capacity only.

1. Enter the replacement total in **My total**.
2. Confirm **Remaining** still makes operational sense.
3. Select **Adjust headcount**.

The new total must be positive and cannot be lower than the number of active Bookings requiring your
type. If a attendee books at the same time, the save may be rejected to prevent overbooking.
Reload the board and enter a total that covers the updated demand.

Attendee Groups determine which types each attendee needs. You do not need to know the group to
manage capacity: EventBooking reserves only the capacity required by each invitation or recovery
snapshot.

### Cancel a Confirmed Event

Cancel only when the whole four-hour window cannot run.

1. Warn Coordinator and delivery colleagues first.
2. Select **Cancel event**.
3. If active Bookings are affected, read the warning and select **Confirm cancel** only when you
   intend to proceed.
4. Tell the Coordinator to monitor attendees and replacement invitation delivery.

Cancellation voids Bookings on the event, releases their capacity, and triggers the appropriate
attendee rebooking workflow. Cancelling a recovery event preserves the original journey and
already Completed appointments.

Admins and Coordinators can cancel the same window from their own Events screen, so agree
who is acting before anyone clicks.

## Appointments screen

Managers have the same delivery controls as Appointment staff within their own Appointment Type,
including **Download roster** for a printable working list. Use `/appointments` when you are
recording day-of work. See the [Appointment staff guide](appointment-staff-guide.md) for the
complete screen workflow, screenshots, and correction rules.

## Troubleshooting

- **The wrong Appointment Type is shown** — stop and ask an Admin to correct your scope on Staff
  access. Do not act in the wrong workspace.
- **No Appointment Type is shown at all** — an Admin has not scoped your profile yet. The role alone
  is not enough.
- **Accept or update failed** — the proposal was changed, withdrawn, or confirmed while you were
  viewing it. Reload the board.
- **Another Manager's headcount is missing** — expected. You can see who accepted, not their number.
- **Capacity change was rejected** — the total is invalid, below active demand, or demand changed
  concurrently. Reload and recalculate.
- **Attendees are awaiting availability** — propose more future windows or accept open proposals.
  An invitation needs three suitable Confirmed Events across every required type.
- **Cancel requires a second confirmation** — active Bookings are affected. Coordinate first; the
  second click performs the cancellation.
- **A window disappeared from your board** — an Admin or Coordinator may have cancelled it. Ask the
  Coordinator to check the audit trail; Managers do not have that screen.
`````

## before — src/EventBooking.Api/Auth/RemoteIpRateLimiterPolicy.cs — 1/1

<!-- vocabulary-file: {"id":6,"oldPath":"src/EventBooking.Api/Auth/RemoteIpRateLimiterPolicy.cs","newPath":"src/EventBooking.Api/Auth/RemoteIpRateLimiterPolicy.cs","beforeSha":"9c1e0d3eda5998f41233de610bfa0f091b90736bb9c3a2cb3d310137a8087ccb","afterSha":"541e9a994dab85fda8900ea2e02ea3157c3727c5149b0146e4febc6999b7ba19","side":"before","part":1,"parts":1} -->

`````csharp
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace EventBooking.Api.Auth;

/// <summary>Applies the anonymous candidate-link allowance independently per client address.</summary>
public sealed class RemoteIpRateLimiterPolicy : IRateLimiterPolicy<string>
{
    /// <inheritdoc/>
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;

    /// <inheritdoc/>
    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        // ForwardedHeadersMiddleware runs before the limiter, so RemoteIpAddress is already the
        // real client address when behind a proxy. Unknown addresses share one fallback bucket
        // rather than bypassing the limit.
        var client = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(client, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    }
}
`````

## after — src/EventBooking.Api/Auth/RemoteIpRateLimiterPolicy.cs — 1/1

<!-- vocabulary-file: {"id":6,"oldPath":"src/EventBooking.Api/Auth/RemoteIpRateLimiterPolicy.cs","newPath":"src/EventBooking.Api/Auth/RemoteIpRateLimiterPolicy.cs","beforeSha":"9c1e0d3eda5998f41233de610bfa0f091b90736bb9c3a2cb3d310137a8087ccb","afterSha":"541e9a994dab85fda8900ea2e02ea3157c3727c5149b0146e4febc6999b7ba19","side":"after","part":1,"parts":1} -->

`````csharp
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace EventBooking.Api.Auth;

/// <summary>Applies the anonymous attendee-link allowance independently per client address.</summary>
public sealed class RemoteIpRateLimiterPolicy : IRateLimiterPolicy<string>
{
    /// <inheritdoc/>
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;

    /// <inheritdoc/>
    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        // ForwardedHeadersMiddleware runs before the limiter, so RemoteIpAddress is already the
        // real client address when behind a proxy. Unknown addresses share one fallback bucket
        // rather than bypassing the limit.
        var client = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(client, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    }
}
`````

## before — src/EventBooking.Api/Contracts/AdministrationHypermediaResponses.cs — 1/1

<!-- vocabulary-file: {"id":7,"oldPath":"src/EventBooking.Api/Contracts/AdministrationHypermediaResponses.cs","newPath":"src/EventBooking.Api/Contracts/AdministrationHypermediaResponses.cs","beforeSha":"3ac8df71105816ed4916c18ffb68b76d86c8196d82dfefe792886f2c77e3f507","afterSha":"ec3f9a2cd60ef36c4780ed9e72f93bee81362957d338d4888ed7f14a600f66ec","side":"before","part":1,"parts":1} -->

`````csharp
using System.Text.Json.Serialization;
using EventBooking.Application.Settings;

namespace EventBooking.Api.Contracts;

/// <summary>Application settings plus the self and update affordances.</summary>
public sealed record SettingsResourceResponse(
    /// <summary>Gets the number of days an invite stays usable.</summary>
    int InviteExpiryDays,
    /// <summary>Gets the maximum number of times an unanswered invite is automatically re-issued.</summary>
    int MaxAutoRetryCount,
    /// <summary>Gets the appointment types with their assigned managers.</summary>
    IReadOnlyList<AppointmentTypeView> AppointmentTypes,
    /// <summary>Gets the settings affordances.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one settings view into its hypermedia resource.</summary>
    /// <param name="view">The application settings view to project.</param>
    /// <returns>The API resource with settings links.</returns>
    public static SettingsResourceResponse From(SettingsView view) =>
        new(view.InviteExpiryDays, view.MaxAutoRetryCount, view.AppointmentTypes,
            StaffResourceLinks.ForSettings());
}

/// <summary>One staff access profile plus its replace and clear affordances.</summary>
public sealed record StaffAccessResourceResponse(
    /// <summary>Gets the stable staff identity targeted by administration.</summary>
    Guid StaffUserId,
    /// <summary>Gets the enterprise staff number, or null until the identity signs in.</summary>
    string? StaffId,
    /// <summary>Gets the identity-provider roles mirrored on the profile.</summary>
    IReadOnlyList<string> Roles,
    /// <summary>Gets the scoped appointment-type identifier, or null when unscoped.</summary>
    Guid? AppointmentTypeId,
    /// <summary>Gets the scoped appointment-type name, or null when unscoped.</summary>
    string? AppointmentTypeName,
    /// <summary>Gets the positive concurrency version required by scope commands.</summary>
    long Version,
    /// <summary>Gets the human-readable name mirrored from the identity provider, or null when absent.</summary>
    string? DisplayName,
    /// <summary>Gets the replace and clear affordances for the profile.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>A staff-access mutation result plus the follow-up affordances.</summary>
public sealed record StaffAccessMutationResourceResponse(
    /// <summary>Gets the updated staff access profile.</summary>
    StaffAccessResourceResponse Profile,
    /// <summary>Gets the displaced manager identity, when a replacement displaced one.</summary>
    Guid? FormerManagerStaffUserId,
    /// <summary>Gets the follow-up affordances for the mutated profile.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>The signed-in staff identity plus self and collection entry affordances.</summary>
public sealed record MeResourceResponse(
    /// <summary>Gets the caller's enterprise staff number, or null until recorded.</summary>
    string? StaffId,
    /// <summary>Gets the caller's current role names.</summary>
    IReadOnlyList<string> Roles,
    /// <summary>Gets the caller's scoped appointment-type identifier, or null when unscoped.</summary>
    Guid? AppointmentTypeId,
    /// <summary>Gets the caller's scoped appointment-type name, or null when unscoped.</summary>
    string? AppointmentTypeName,
    /// <summary>Gets the self and role-relevant collection entry affordances. Links are discoverability hints, not authorization.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Builds the identity resource with self plus role-relevant entry links.</summary>
    /// <param name="staffId">The caller's enterprise staff number, or null until recorded.</param>
    /// <param name="roles">The caller's current role names.</param>
    /// <param name="appointmentTypeId">The caller's scoped appointment-type identifier, or null.</param>
    /// <param name="appointmentTypeName">The caller's scoped appointment-type name, or null.</param>
    /// <returns>The API resource with identity links.</returns>
    public static MeResourceResponse From(
        string? staffId,
        IReadOnlyList<string> roles,
        Guid? appointmentTypeId,
        string? appointmentTypeName)
    {
        var links = new Dictionary<string, ApiLink>
        {
            ["self"] = new("/api/me", "GET", "getMyAccess"),
        };
        if (roles.Contains("Coordinator") || roles.Contains("Admin"))
        {
            links["candidates"] = new("/api/candidates", "GET", "listCandidates");
            links["dashboards"] = new("/api/dashboards", "GET", "getDashboards");
            links["audit"] = new("/api/audit/search", "GET", "searchAudit");
        }

        if (roles.Contains("Manager"))
        {
            links["slotBoard"] = new("/api/slots/board", "GET", "getSlotBoard");
            links["slotOperations"] = new("/api/slots/operations", "GET", "getSlotOperations");
        }

        if (roles.Contains("AppointmentStaff") || roles.Contains("Manager"))
        {
            links["appointmentSlots"] = new("/api/appointment-workspace/slots", "GET", "listAppointmentSlots");
        }

        if (roles.Contains("Admin"))
        {
            links["settings"] = new("/api/admin/settings", "GET", "getSettings");
            links["staffAccess"] = new("/api/admin/staff-access", "GET", "listStaffAccess");
        }

        return new MeResourceResponse(staffId, roles, appointmentTypeId, appointmentTypeName, links);
    }
}
`````

## after — src/EventBooking.Api/Contracts/AdministrationHypermediaResponses.cs — 1/1

<!-- vocabulary-file: {"id":7,"oldPath":"src/EventBooking.Api/Contracts/AdministrationHypermediaResponses.cs","newPath":"src/EventBooking.Api/Contracts/AdministrationHypermediaResponses.cs","beforeSha":"3ac8df71105816ed4916c18ffb68b76d86c8196d82dfefe792886f2c77e3f507","afterSha":"ec3f9a2cd60ef36c4780ed9e72f93bee81362957d338d4888ed7f14a600f66ec","side":"after","part":1,"parts":1} -->

`````csharp
using System.Text.Json.Serialization;
using EventBooking.Application.Settings;

namespace EventBooking.Api.Contracts;

/// <summary>Application settings plus the self and update affordances.</summary>
public sealed record SettingsResourceResponse(
    /// <summary>Gets the number of days an invite stays usable.</summary>
    int InviteExpiryDays,
    /// <summary>Gets the maximum number of times an unanswered invite is automatically re-issued.</summary>
    int MaxAutoRetryCount,
    /// <summary>Gets the appointment types with their assigned managers.</summary>
    IReadOnlyList<AppointmentTypeView> AppointmentTypes,
    /// <summary>Gets the settings affordances.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one settings view into its hypermedia resource.</summary>
    /// <param name="view">The application settings view to project.</param>
    /// <returns>The API resource with settings links.</returns>
    public static SettingsResourceResponse From(SettingsView view) =>
        new(view.InviteExpiryDays, view.MaxAutoRetryCount, view.AppointmentTypes,
            StaffResourceLinks.ForSettings());
}

/// <summary>One staff access profile plus its replace and clear affordances.</summary>
public sealed record StaffAccessResourceResponse(
    /// <summary>Gets the stable staff identity targeted by administration.</summary>
    Guid StaffUserId,
    /// <summary>Gets the enterprise staff number, or null until the identity signs in.</summary>
    string? StaffId,
    /// <summary>Gets the identity-provider roles mirrored on the profile.</summary>
    IReadOnlyList<string> Roles,
    /// <summary>Gets the scoped appointment-type identifier, or null when unscoped.</summary>
    Guid? AppointmentTypeId,
    /// <summary>Gets the scoped appointment-type name, or null when unscoped.</summary>
    string? AppointmentTypeName,
    /// <summary>Gets the positive concurrency version required by scope commands.</summary>
    long Version,
    /// <summary>Gets the human-readable name mirrored from the identity provider, or null when absent.</summary>
    string? DisplayName,
    /// <summary>Gets the replace and clear affordances for the profile.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>A staff-access mutation result plus the follow-up affordances.</summary>
public sealed record StaffAccessMutationResourceResponse(
    /// <summary>Gets the updated staff access profile.</summary>
    StaffAccessResourceResponse Profile,
    /// <summary>Gets the displaced manager identity, when a replacement displaced one.</summary>
    Guid? FormerManagerStaffUserId,
    /// <summary>Gets the follow-up affordances for the mutated profile.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>The signed-in staff identity plus self and collection entry affordances.</summary>
public sealed record MeResourceResponse(
    /// <summary>Gets the caller's enterprise staff number, or null until recorded.</summary>
    string? StaffId,
    /// <summary>Gets the caller's current role names.</summary>
    IReadOnlyList<string> Roles,
    /// <summary>Gets the caller's scoped appointment-type identifier, or null when unscoped.</summary>
    Guid? AppointmentTypeId,
    /// <summary>Gets the caller's scoped appointment-type name, or null when unscoped.</summary>
    string? AppointmentTypeName,
    /// <summary>Gets the self and role-relevant collection entry affordances. Links are discoverability hints, not authorization.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Builds the identity resource with self plus role-relevant entry links.</summary>
    /// <param name="staffId">The caller's enterprise staff number, or null until recorded.</param>
    /// <param name="roles">The caller's current role names.</param>
    /// <param name="appointmentTypeId">The caller's scoped appointment-type identifier, or null.</param>
    /// <param name="appointmentTypeName">The caller's scoped appointment-type name, or null.</param>
    /// <returns>The API resource with identity links.</returns>
    public static MeResourceResponse From(
        string? staffId,
        IReadOnlyList<string> roles,
        Guid? appointmentTypeId,
        string? appointmentTypeName)
    {
        var links = new Dictionary<string, ApiLink>
        {
            ["self"] = new("/api/me", "GET", "getMyAccess"),
        };
        if (roles.Contains("Coordinator") || roles.Contains("Admin"))
        {
            links["attendees"] = new("/api/attendees", "GET", "listAttendees");
            links["dashboards"] = new("/api/dashboards", "GET", "getDashboards");
            links["audit"] = new("/api/audit/search", "GET", "searchAudit");
        }

        if (roles.Contains("Manager"))
        {
            links["eventBoard"] = new("/api/events/board", "GET", "getEventBoard");
            links["eventOperations"] = new("/api/events/operations", "GET", "getEventOperations");
        }

        if (roles.Contains("AppointmentStaff") || roles.Contains("Manager"))
        {
            links["appointmentEvents"] = new("/api/appointment-workspace/events", "GET", "listAppointmentEvents");
        }

        if (roles.Contains("Admin"))
        {
            links["settings"] = new("/api/admin/settings", "GET", "getSettings");
            links["staffAccess"] = new("/api/admin/staff-access", "GET", "listStaffAccess");
        }

        return new MeResourceResponse(staffId, roles, appointmentTypeId, appointmentTypeName, links);
    }
}
`````
