# Coordinator guide

[← All user guides](README.md)

As a Coordinator, you own the attendee journey from initial record through invitation, booking,
readiness, and missed-appointment recovery. EventBooking derives every required Appointment Type
from the attendee's Attendee Group; you never add or remove requirements individually. You can also
cancel a attendee's booking on their behalf, cancel a whole confirmed window, and search the full
audit trail.

## Your workflow

1. Check **Dashboards** for attendees waiting for availability or follow-up.
2. Ensure at least three suitable future Confirmed Events exist. Ask Managers to negotiate and accept them.
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

Use `/events/operations` to review Events and cancel a window that cannot run. Admins see the same screen.

Event capacity is created through Manager negotiation. Direct event CSV import is not available.

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
