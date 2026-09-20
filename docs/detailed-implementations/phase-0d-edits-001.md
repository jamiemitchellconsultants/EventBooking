# 00d — Retire direct event import, edits 1 (Task 3b)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — docs/user-guides/README.md — 1/1

<!-- retirement-file: {"id":0,"file":"docs/user-guides/README.md","beforeSha":"dc873be3c4c83ee19afda9619de2bfd6fe18e99a8caf1bb205d159a6a9c0a120","afterSha":"1a438462c88e8bfd02bc785e869822a20574847401f13caa6ba24a8b4e5d5283","side":"before","part":1,"parts":1} -->

`````text
# EventBooking user guides

EventBooking coordinates attendee appointments across five user types. Start with the guide for
your role, then use the workflow below to understand which earlier actions your work depends on.

Every guide here is also published inside the application at `/help`. Staff see all five guides on
that page; a signed-out visitor sees only the attendee guide.

| Guide | Who it is for | Main workspace |
|---|---|---|
| [Admin](admin-guide.md) | System administrators who set appointment-type scope, settings, and direct event imports | System settings, Staff access, Events, Audit trail |
| [Coordinator](coordinator-guide.md) | Recruitment coordinators who manage attendees, invitations, bookings, readiness, and recovery | Attendees, Dashboards, Events, Audit trail |
| [Manager](manager-guide.md) | Appointment-type managers who negotiate events, manage capacity, and may deliver appointments | Event proposals, Appointments |
| [Appointment staff](appointment-staff-guide.md) | Delivery staff who check attendees in and record outcomes | Appointments |
| [Attendee](attendee-guide.md) | Invited attendees who choose and manage appointment times | Email links for booking and booking management |

## End-to-end workflow and dependencies

| Stage | Owner | What must already exist | Screen and result |
|---|---|---|---|
| 1. Assign roles | Corporate identity provider, not EventBooking | The colleague's account exists in the company directory | Roles are granted centrally; EventBooking records what the sign-in token says |
| 2. Give scoped roles their type | Admin | The colleague has signed in once, so a profile exists | **Staff access**: set the one Appointment Type for a Manager or Appointment staff profile |
| 3. Prepare settings | Admin | Admin access | **System settings**: set invitation expiry and the invite re-issue limit |
| 4. Create capacity | Manager, Admin, or Coordinator | One Manager exists for each Appointment Type, or a event has already been agreed outside EventBooking | **Event proposals**: all three Managers accept; or **Events**: import the agreed window and all three capacities |
| 5. Add attendees | Coordinator | An Attendee Group is known for each attendee | **Attendees**: add manually or import `name,email,attendee_group`; EventBooking derives the required Appointment Types |
| 6. Send an invitation | Coordinator | At least three future Confirmed Events have capacity for every required Appointment Type | **Attendees**: select **Invite now**; the attendee receives three options |
| 7. Book | Attendee | A valid pending invitation | **Choose a time**: select an option and confirm; capacity is reserved and one Booking Appointment is created for each required type |
| 8. Deliver appointments | Manager or Appointment staff | The attendee has an active Booking on the selected Confirmed Event | **Appointments**: check in, complete, or record No-show for the caller's scoped type |
| 9. Check readiness | Coordinator | Appointment outcomes have been recorded | **Attendees**: expand readiness; the attendee is ready only when every current required type has a Completed outcome |
| 10. Recover a missed appointment | Coordinator, then Attendee | The latest unsatisfied attempt for at least one current required type is No-show | **Attendees**: choose **Arrange missed appointments**; the attendee books a new shared event containing only the missed types |
| 11. Account for a change | Admin or Coordinator | The change has been recorded | **Audit trail**, or the **History** control on a attendee or event row |

An upstream delay remains visible at the next stage. For example, a Coordinator cannot issue an
invitation until suitable event capacity exists, and a Attendee cannot appear in the Appointments
workspace until they confirm a time.

## Staff sign-in and navigation

Staff sign in from the EventBooking home page with their company account.

![Staff sign-in screen](screenshots/keycloak-sign-in.png)

The home page greets you by name and shows the roles and Appointment Type scope in the signed-in
access summary, followed by only the workspaces that profile permits. Once signed in, the same links
also appear as a persistent navigation bar at the top of every page, so switching workspaces never
requires returning to the home page first. **Help** is the last link in both places.

| Access profile | Workspace links |
|---|---|
| Admin | System settings, Staff access, Events, Audit trail |
| Coordinator | Attendees, Dashboards, Events, Audit trail |
| Manager | Event proposals, Appointments |
| Appointment staff | Appointments |

Admin is exclusive and cannot be combined with another role. Coordinator, Manager, and Appointment
staff can be combined in one profile. A combined profile receives the union of the links above, but
Manager and Appointment staff still share one Appointment Type scope.

If the home page says that no role is assigned, your account has no EventBooking role in the company
identity provider; ask whoever administers application access there, not a EventBooking Admin. If a
page or action is missing, first check the access summary: the absence is normally an access rule,
not a page fault.

Attendees do not sign in. They use personal, single-use links sent by email and see no staff
navigation. They can open `/help` without signing in to read the attendee guide.

## Who assigns what

Roles and the appointment-type scope come from two different places, and this is the most common
source of confusion:

- **Roles** (Admin, Coordinator, Manager, Appointment staff) are assigned centrally in the company
  identity provider. EventBooking records what the sign-in token says and can never change it.
- **Appointment Type scope** (the one type a Manager or Appointment staff profile works on) is
  assigned inside EventBooking by an Admin, on the Staff access screen.

A scoped role therefore needs two separate actions by two different people before it works, and a
profile only appears on the Staff access screen after that person has signed in at least once.

## Everyday vocabulary

- An **Attendee Group** is the attendee's employment category. It determines the complete set of
  Appointment Types the attendee requires; nobody selects those requirements individually.
- A **Event Proposal** is a suggested four-hour window. Once all three Managers accept it, it becomes
  a **Confirmed Event** that attendees can book.
- An **Invite** offers a attendee exactly three suitable Confirmed Events. Confirming one creates a
  **Booking**.
- A **Booking Appointment** is one required Appointment Type inside a Booking. Each type is checked
  in and completed separately.
- **Readiness** means every current required Appointment Type has a Completed Booking Appointment in
  the attendee's non-cancelled journey.
- A **recovery booking** contains only missed required Appointment Types. It preserves completed
  work and every earlier attempt.
- The **audit trail** is the record of every change: what changed, who changed it, and when. It is
  searchable on its own page and inline through the **History** control on attendee and event rows.

## About the screenshots

The screenshots in these guides were captured from the local demo stack against seeded data, and
are representative rather than exhaustive. They were recaptured on 2026-09-16 and 2026-09-17
against the current screens.

The Appointments check-in and correction screenshots need a event dated today that already holds
bookings: check-in only opens on a Confirmed Event's own date, and a attendee can only book a event
dated after today. To retake them, reseed with `--reseed --reanchor` (see the
[demo runbook](../demo-runbook.md)) on the day of capture. That dates the seeded 13:00 journey event
today, with Expected Uniform Fitting appointments for `appointment.staff` to check in.

Where a screenshot and the surrounding text disagree, the text describes the current screen.
`````

## after — docs/user-guides/README.md — 1/1

<!-- retirement-file: {"id":0,"file":"docs/user-guides/README.md","beforeSha":"dc873be3c4c83ee19afda9619de2bfd6fe18e99a8caf1bb205d159a6a9c0a120","afterSha":"1a438462c88e8bfd02bc785e869822a20574847401f13caa6ba24a8b4e5d5283","side":"after","part":1,"parts":1} -->

`````text
# EventBooking user guides

EventBooking coordinates attendee appointments across five user types. Start with the guide for
your role, then use the workflow below to understand which earlier actions your work depends on.

Every guide here is also published inside the application at `/help`. Staff see all five guides on
that page; a signed-out visitor sees only the attendee guide.

| Guide | Who it is for | Main workspace |
|---|---|---|
| [Admin](admin-guide.md) | System administrators who set appointment-type scope, settings, and event operations | System settings, Staff access, Events, Audit trail |
| [Coordinator](coordinator-guide.md) | Recruitment coordinators who manage attendees, invitations, bookings, readiness, and recovery | Attendees, Dashboards, Events, Audit trail |
| [Manager](manager-guide.md) | Appointment-type managers who negotiate events, manage capacity, and may deliver appointments | Event proposals, Appointments |
| [Appointment staff](appointment-staff-guide.md) | Delivery staff who check attendees in and record outcomes | Appointments |
| [Attendee](attendee-guide.md) | Invited attendees who choose and manage appointment times | Email links for booking and booking management |

## End-to-end workflow and dependencies

| Stage | Owner | What must already exist | Screen and result |
|---|---|---|---|
| 1. Assign roles | Corporate identity provider, not EventBooking | The colleague's account exists in the company directory | Roles are granted centrally; EventBooking records what the sign-in token says |
| 2. Give scoped roles their type | Admin | The colleague has signed in once, so a profile exists | **Staff access**: set the one Appointment Type for a Manager or Appointment staff profile |
| 3. Prepare settings | Admin | Admin access | **System settings**: set invitation expiry and the invite re-issue limit |
| 4. Create capacity | Manager, Admin, or Coordinator | One Manager exists for each Appointment Type, or a event has already been agreed outside EventBooking | **Event proposals**: all three Managers accept; or **Events**: import the agreed window and all three capacities |
| 5. Add attendees | Coordinator | An Attendee Group is known for each attendee | **Attendees**: add manually or import `name,email,attendee_group`; EventBooking derives the required Appointment Types |
| 6. Send an invitation | Coordinator | At least three future Confirmed Events have capacity for every required Appointment Type | **Attendees**: select **Invite now**; the attendee receives three options |
| 7. Book | Attendee | A valid pending invitation | **Choose a time**: select an option and confirm; capacity is reserved and one Booking Appointment is created for each required type |
| 8. Deliver appointments | Manager or Appointment staff | The attendee has an active Booking on the selected Confirmed Event | **Appointments**: check in, complete, or record No-show for the caller's scoped type |
| 9. Check readiness | Coordinator | Appointment outcomes have been recorded | **Attendees**: expand readiness; the attendee is ready only when every current required type has a Completed outcome |
| 10. Recover a missed appointment | Coordinator, then Attendee | The latest unsatisfied attempt for at least one current required type is No-show | **Attendees**: choose **Arrange missed appointments**; the attendee books a new shared event containing only the missed types |
| 11. Account for a change | Admin or Coordinator | The change has been recorded | **Audit trail**, or the **History** control on a attendee or event row |

An upstream delay remains visible at the next stage. For example, a Coordinator cannot issue an
invitation until suitable event capacity exists, and a Attendee cannot appear in the Appointments
workspace until they confirm a time.

## Staff sign-in and navigation

Staff sign in from the EventBooking home page with their company account.

![Staff sign-in screen](screenshots/keycloak-sign-in.png)

The home page greets you by name and shows the roles and Appointment Type scope in the signed-in
access summary, followed by only the workspaces that profile permits. Once signed in, the same links
also appear as a persistent navigation bar at the top of every page, so switching workspaces never
requires returning to the home page first. **Help** is the last link in both places.

| Access profile | Workspace links |
|---|---|
| Admin | System settings, Staff access, Events, Audit trail |
| Coordinator | Attendees, Dashboards, Events, Audit trail |
| Manager | Event proposals, Appointments |
| Appointment staff | Appointments |

Admin is exclusive and cannot be combined with another role. Coordinator, Manager, and Appointment
staff can be combined in one profile. A combined profile receives the union of the links above, but
Manager and Appointment staff still share one Appointment Type scope.

If the home page says that no role is assigned, your account has no EventBooking role in the company
identity provider; ask whoever administers application access there, not a EventBooking Admin. If a
page or action is missing, first check the access summary: the absence is normally an access rule,
not a page fault.

Attendees do not sign in. They use personal, single-use links sent by email and see no staff
navigation. They can open `/help` without signing in to read the attendee guide.

## Who assigns what

Roles and the appointment-type scope come from two different places, and this is the most common
source of confusion:

- **Roles** (Admin, Coordinator, Manager, Appointment staff) are assigned centrally in the company
  identity provider. EventBooking records what the sign-in token says and can never change it.
- **Appointment Type scope** (the one type a Manager or Appointment staff profile works on) is
  assigned inside EventBooking by an Admin, on the Staff access screen.

A scoped role therefore needs two separate actions by two different people before it works, and a
profile only appears on the Staff access screen after that person has signed in at least once.

## Everyday vocabulary

- An **Attendee Group** is the attendee's employment category. It determines the complete set of
  Appointment Types the attendee requires; nobody selects those requirements individually.
- A **Event Proposal** is a suggested four-hour window. Once all three Managers accept it, it becomes
  a **Confirmed Event** that attendees can book.
- An **Invite** offers a attendee exactly three suitable Confirmed Events. Confirming one creates a
  **Booking**.
- A **Booking Appointment** is one required Appointment Type inside a Booking. Each type is checked
  in and completed separately.
- **Readiness** means every current required Appointment Type has a Completed Booking Appointment in
  the attendee's non-cancelled journey.
- A **recovery booking** contains only missed required Appointment Types. It preserves completed
  work and every earlier attempt.
- The **audit trail** is the record of every change: what changed, who changed it, and when. It is
  searchable on its own page and inline through the **History** control on attendee and event rows.

## About the screenshots

The screenshots in these guides were captured from the local demo stack against seeded data, and
are representative rather than exhaustive. They were recaptured on 2026-09-16 and 2026-09-17
against the current screens.

The Appointments check-in and correction screenshots need a event dated today that already holds
bookings: check-in only opens on a Confirmed Event's own date, and a attendee can only book a event
dated after today. To retake them, reseed with `--reseed --reanchor` (see the
[demo runbook](../demo-runbook.md)) on the day of capture. That dates the seeded 13:00 journey event
today, with Expected Uniform Fitting appointments for `appointment.staff` to check in.

Where a screenshot and the surrounding text disagree, the text describes the current screen.
`````

## before — docs/user-guides/admin-guide.md — 1/1

<!-- retirement-file: {"id":1,"file":"docs/user-guides/admin-guide.md","beforeSha":"8248e055a84c548e7874a972cc61499a5f62b233e794b43a404357d53c044a1a","afterSha":"3764481ee5e0593399903d0fb3fc2abebb83e2537757097de9bd1cdad2833699","side":"before","part":1,"parts":1} -->

`````text
# Admin guide

[← All user guides](README.md)

As an Admin, you prepare EventBooking for other staff. You set the Appointment Type scope that
scoped roles need, configure invitation timing, import Confirmed Events agreed outside the normal
Manager negotiation, cancel a window that cannot run, and search the operational audit trail. Admin
access is exclusive: it cannot be combined with another role, and it never exposes attendee,
invitation, booking, readiness, or recovery data.

## What you do and do not control

You do **not** assign roles. Admin, Coordinator, Manager, and Appointment staff are granted centrally
in the company identity provider, and EventBooking only records what a person's sign-in token says.
Nothing on any EventBooking screen can add or remove a role, and EventBooking never writes a role
back to the provider.

You **do** own the Appointment Type scope. A Manager or Appointment staff role means nothing until
you give that profile one of the three types, because the identity provider has no concept of
appointment types. That makes scope assignment the one access task that belongs to you.

## Your workflow

1. Confirm the colleague has been given a EventBooking role in the identity provider, and has signed
   in once — a profile only appears on **Staff access** after a first sign-in.
2. On **Staff access**, set the Appointment Type for every Manager and Appointment staff profile
   showing **Awaiting appointment-type assignment**.
3. On **System settings**, confirm invitation expiry and the invite re-issue limit before
   Coordinators start sending invitations.
4. If a full event has already been agreed outside EventBooking, import it on **Events**.
5. Hand attendee and invitation work to a Coordinator; hand negotiated capacity to the Managers.
6. Use **Audit trail** when someone asks who changed a event, a proposal, or an access profile.

## Home page

After signing in, check the access summary at the top of the home page. It should show **Admin** and
no Appointment Type. The page provides four workspace links plus Help:

- **System settings** (`/settings`)
- **Staff access** (`/staff-access`)
- **Events** (`/events`)
- **Audit trail** (`/audit`)
- **Help** (`/help`) — every role's guide, including this one

![Admin home page with the workspace links](screenshots/admin-home.png)

The same links also appear as a navigation bar at the top of every Admin page, so you can switch
workspaces without returning to the home page first.

If attendee, dashboard, event-proposal, or appointment links are absent, EventBooking is enforcing
the Admin data boundary correctly.

## Staff access screen

Use `/staff-access` to see who holds which role and to set the one Appointment Type that scoped
roles work within. The table lists Staff number, Roles, Appointment type, and an Action column.

The Staff number column shows the person's name with their staff number in brackets — the staff
number is the identifier to quote when you act on a profile. Roles are read-only chips: they came
from the identity provider and this screen cannot change them.

![Staff access screen listing profiles with read-only role chips and appointment types, with the Edit appointment type editor open below the table](screenshots/staff-access.png)

### Assign or change an Appointment Type

1. Find the row by name or staff number.
2. Look at the Appointment type column:
   - A named type means the scope is already set.
   - **Awaiting appointment-type assignment** means the profile holds Manager or Appointment staff
     but cannot yet use its workspace.
   - An em dash means the profile is Admin or Coordinator, which are never scoped.
3. Select **Assign** (first time) or **Edit** (a change). The editor opens below the table showing
   the staff number and read-only role chips.
4. Choose Drug & Alcohol Testing, Medical Check-up, or Uniform Fitting in **Appointment type**.
5. Select **Save appointment type** and wait for **Appointment type saved.**

If the profile you saved becomes the Manager for a type that already had one, the confirmation also
reports that the former Manager's scope was cleared. Each Appointment Type has only one current
Manager.

### Clear a scope

Open the editor for a profile that already has a type and select **Clear scope**. The role stays
assigned — only the type is removed — and the profile returns to **Awaiting appointment-type
assignment** until you set a new one. Use this when a Manager moves teams and a replacement has not
been named yet.

### When a profile is missing

The empty state reads **No one has signed in with a EventBooking role yet.** A profile you expect to
see is missing for one of two reasons, and neither is repairable from this screen:

- The person has no EventBooking role in the identity provider. Raise it through the corporate
  access process, not here.
- They have the role but have never signed in. Ask them to sign in once, then reload this screen.

You cannot pre-provision scope for somebody who has never signed in.

### Conflicting changes

If another Admin changed the same profile first, your save is rejected and the screen reloads with
the current state. Review the reloaded roles and scope, then reapply your change if it is still
needed. Every successful access change is audited and is searchable on the Audit trail screen.

### Access dependencies to establish

- Scope one Manager profile for each of the three Appointment Types before relying on in-system event
  negotiation.
- Scope Appointment staff before the appointment date so they can open the Appointments workspace.
- Never treat Admin as "all access": Admin intentionally receives less personal data than a
  Coordinator, and cannot see attendees, invitations, bookings, or attendee audit history.

## System settings screen

Use `/settings` to review the fixed Appointment Types and configure invitation timing.

![System settings screen showing fixed appointment types and invite timing fields](screenshots/system-settings.png)

1. Review **Fixed appointment types**. The three types are fixed by the system; only the Manager
   assignment changes. The Manager identifier column shows the current Manager's name and staff
   number, or **Unassigned** when no Manager holds that type. Change an assignment by scoping a
   Manager profile on Staff access, not here.
2. Set **Invite expiry window (days)** to the number of days a new invitation remains valid.
3. Set **Max auto-retry count** to the number of times an unanswered invitation is automatically
   re-issued before the attendee is marked No response - needs follow-up for a Coordinator to
   re-invite manually.
4. Select **Save changes**.
5. Wait for **Saved. These changes apply to invites created from now on.** before leaving.

Changes do not reach back and alter invitations already sent.

## Events screen

Use `/events` for a complete window agreed outside EventBooking, and to cancel a confirmed
window. Coordinators see the same screen.

![Events screen with CSV import control](screenshots/events-import.png)

### Import already agreed events

A direct import skips Manager acceptance and becomes bookable immediately.

1. Prepare a UTF-8 CSV whose exact header is:

   ```text
   date,startTime,DAT,MED,UNI
   ```

2. Add one four-hour window per row in `yyyy-MM-dd,HH:mm` format, with a positive total headcount
   for Drug & Alcohol Testing (`DAT`), Medical Check-up (`MED`), and Uniform Fitting (`UNI`). Use
   future dates for bookable capacity; past events are retained as history and are never offered.
3. In **Import already agreed events**, choose the CSV file.
4. Wait for the imported-count confirmation.

The import is all-or-nothing. If the screen says **Nothing was imported**, use every line-numbered
message to correct the file, then upload the whole file again. Imported events do not require an
additional save or Manager approval.

### Cancel a event

The **Cancel a event** card lists every confirmed window with its date, four-hour window,
remaining-over-total capacity for each type, and the number of active Bookings. Cancel only when the
whole window cannot run.

![Cancel a event card listing each window's date, capacity by type, active bookings, and a Cancel event button](screenshots/events-cancel.png)

1. Warn the Coordinator and the delivery teams first.
2. Select **Cancel event** on the row.
3. If the event holds active Bookings, the request is refused and the message asks you to confirm.
   Read it, then select **Confirm cancel** only when you intend to proceed.
4. Tell the Coordinator to monitor the affected attendees and their replacement invitations.

Cancellation voids the Bookings on that event, releases their capacity, and triggers the attendee
rebooking workflow. A Manager can cancel their own confirmed windows from the Event proposals screen,
and a Coordinator can cancel from this screen, so agree who is acting before anyone clicks.

## Audit trail screen

Use `/audit` to answer "who changed this, and when". As an Admin you see the operational record:
event proposals, events, and staff access profiles. Attendee, invitation, booking, and
appointment entries are outside the Admin data boundary and are not returned to you — a Coordinator
searches those.

1. Set **From** and **To** to bound the period. Both are optional.
2. Choose an **Actor** to narrow by who caused the change: Staff, AttendeeToken, or System.
3. Choose an **Action** to narrow to one recorded change, such as EventConfirmed, EventCancelled,
   CapacityAdjusted, EventImported, StaffAccessChanged, or StaffRolesSynced.
4. Enter an **Identifier** to match one audited entity id or actor id exactly.
5. Select **Search**. Results are newest first, showing When, What, Who, and Details.
6. Select **Load more** to page further back. **Nothing matches these filters** means the search ran
   and found nothing, not that it failed.

Role changes made in the identity provider appear here as StaffRolesSynced entries, recorded the
next time that person signs in. That is the only trace EventBooking has of a role change, because
the change itself happened elsewhere.

## Troubleshooting

- **A colleague is not on Staff access** — they hold no EventBooking role in the identity provider,
  or they have never signed in. Neither is fixable from this screen.
- **A Manager cannot open Event proposals** — their profile shows Awaiting appointment-type
  assignment. Assign the type.
- **Roles look wrong and there is no way to edit them** — correct. Raise the role change through the
  corporate identity process; it appears here after their next sign-in.
- **Scope save conflicts** — another Admin changed the profile first. The screen reloads; review and
  reapply.
- **CSV is rejected** — check the exact header, `yyyy-MM-dd` date, `HH:mm` start time, positive
  capacities, and every line-numbered error. No rows have been imported.
- **Cancel event asks a second time** — active Bookings are affected. Coordinate first; the second
  click performs the cancellation.
- **Audit search returns no attendee entries** — expected. The Admin boundary excludes attendee
  data; ask a Coordinator.
- **Attendee page is unavailable** — expected. A Coordinator must complete attendee work.
`````

## after — docs/user-guides/admin-guide.md — 1/1

<!-- retirement-file: {"id":1,"file":"docs/user-guides/admin-guide.md","beforeSha":"8248e055a84c548e7874a972cc61499a5f62b233e794b43a404357d53c044a1a","afterSha":"3764481ee5e0593399903d0fb3fc2abebb83e2537757097de9bd1cdad2833699","side":"after","part":1,"parts":1} -->

`````text
# Admin guide

[← All user guides](README.md)

As an Admin, you prepare EventBooking for other staff. You set the Appointment Type scope that
scoped roles need, configure invitation timing, cancel a window that cannot run, and search the operational audit trail. Admin
access is exclusive: it cannot be combined with another role, and it never exposes attendee,
invitation, booking, readiness, or recovery data.

## What you do and do not control

You do **not** assign roles. Admin, Coordinator, Manager, and Appointment staff are granted centrally
in the company identity provider, and EventBooking only records what a person's sign-in token says.
Nothing on any EventBooking screen can add or remove a role, and EventBooking never writes a role
back to the provider.

You **do** own the Appointment Type scope. A Manager or Appointment staff role means nothing until
you give that profile one of the three types, because the identity provider has no concept of
appointment types. That makes scope assignment the one access task that belongs to you.

## Your workflow

1. Confirm the colleague has been given a EventBooking role in the identity provider, and has signed
   in once — a profile only appears on **Staff access** after a first sign-in.
2. On **Staff access**, set the Appointment Type for every Manager and Appointment staff profile
   showing **Awaiting appointment-type assignment**.
3. On **System settings**, confirm invitation expiry and the invite re-issue limit before
   Coordinators start sending invitations.
4. Ask the Managers to record and accept each proposal before it becomes an Event.
5. Hand attendee and invitation work to a Coordinator; hand negotiated capacity to the Managers.
6. Use **Audit trail** when someone asks who changed a event, a proposal, or an access profile.

## Home page

After signing in, check the access summary at the top of the home page. It should show **Admin** and
no Appointment Type. The page provides four workspace links plus Help:

- **System settings** (`/settings`)
- **Staff access** (`/staff-access`)
- **Events** (`/events`)
- **Audit trail** (`/audit`)
- **Help** (`/help`) — every role's guide, including this one

![Admin home page with the workspace links](screenshots/admin-home.png)

The same links also appear as a navigation bar at the top of every Admin page, so you can switch
workspaces without returning to the home page first.

If attendee, dashboard, event-proposal, or appointment links are absent, EventBooking is enforcing
the Admin data boundary correctly.

## Staff access screen

Use `/staff-access` to see who holds which role and to set the one Appointment Type that scoped
roles work within. The table lists Staff number, Roles, Appointment type, and an Action column.

The Staff number column shows the person's name with their staff number in brackets — the staff
number is the identifier to quote when you act on a profile. Roles are read-only chips: they came
from the identity provider and this screen cannot change them.

![Staff access screen listing profiles with read-only role chips and appointment types, with the Edit appointment type editor open below the table](screenshots/staff-access.png)

### Assign or change an Appointment Type

1. Find the row by name or staff number.
2. Look at the Appointment type column:
   - A named type means the scope is already set.
   - **Awaiting appointment-type assignment** means the profile holds Manager or Appointment staff
     but cannot yet use its workspace.
   - An em dash means the profile is Admin or Coordinator, which are never scoped.
3. Select **Assign** (first time) or **Edit** (a change). The editor opens below the table showing
   the staff number and read-only role chips.
4. Choose Drug & Alcohol Testing, Medical Check-up, or Uniform Fitting in **Appointment type**.
5. Select **Save appointment type** and wait for **Appointment type saved.**

If the profile you saved becomes the Manager for a type that already had one, the confirmation also
reports that the former Manager's scope was cleared. Each Appointment Type has only one current
Manager.

### Clear a scope

Open the editor for a profile that already has a type and select **Clear scope**. The role stays
assigned — only the type is removed — and the profile returns to **Awaiting appointment-type
assignment** until you set a new one. Use this when a Manager moves teams and a replacement has not
been named yet.

### When a profile is missing

The empty state reads **No one has signed in with a EventBooking role yet.** A profile you expect to
see is missing for one of two reasons, and neither is repairable from this screen:

- The person has no EventBooking role in the identity provider. Raise it through the corporate
  access process, not here.
- They have the role but have never signed in. Ask them to sign in once, then reload this screen.

You cannot pre-provision scope for somebody who has never signed in.

### Conflicting changes

If another Admin changed the same profile first, your save is rejected and the screen reloads with
the current state. Review the reloaded roles and scope, then reapply your change if it is still
needed. Every successful access change is audited and is searchable on the Audit trail screen.

### Access dependencies to establish

- Scope one Manager profile for each of the three Appointment Types before relying on in-system event
  negotiation.
- Scope Appointment staff before the appointment date so they can open the Appointments workspace.
- Never treat Admin as "all access": Admin intentionally receives less personal data than a
  Coordinator, and cannot see attendees, invitations, bookings, or attendee audit history.

## System settings screen

Use `/settings` to review the fixed Appointment Types and configure invitation timing.

![System settings screen showing fixed appointment types and invite timing fields](screenshots/system-settings.png)

1. Review **Fixed appointment types**. The three types are fixed by the system; only the Manager
   assignment changes. The Manager identifier column shows the current Manager's name and staff
   number, or **Unassigned** when no Manager holds that type. Change an assignment by scoping a
   Manager profile on Staff access, not here.
2. Set **Invite expiry window (days)** to the number of days a new invitation remains valid.
3. Set **Max auto-retry count** to the number of times an unanswered invitation is automatically
   re-issued before the attendee is marked No response - needs follow-up for a Coordinator to
   re-invite manually.
4. Select **Save changes**.
5. Wait for **Saved. These changes apply to invites created from now on.** before leaving.

Changes do not reach back and alter invitations already sent.

## Events screen

Use `/events/operations` to review Events and cancel a window that cannot run. Coordinators see the same screen.

Event capacity is created through Manager negotiation. Direct event CSV import is not available.

### Cancel a event

The **Cancel a event** card lists every confirmed window with its date, four-hour window,
remaining-over-total capacity for each type, and the number of active Bookings. Cancel only when the
whole window cannot run.

![Cancel a event card listing each window's date, capacity by type, active bookings, and a Cancel event button](screenshots/events-cancel.png)

1. Warn the Coordinator and the delivery teams first.
2. Select **Cancel event** on the row.
3. If the event holds active Bookings, the request is refused and the message asks you to confirm.
   Read it, then select **Confirm cancel** only when you intend to proceed.
4. Tell the Coordinator to monitor the affected attendees and their replacement invitations.

Cancellation voids the Bookings on that event, releases their capacity, and triggers the attendee
rebooking workflow. A Manager can cancel their own confirmed windows from the Event proposals screen,
and a Coordinator can cancel from this screen, so agree who is acting before anyone clicks.

## Audit trail screen

Use `/audit` to answer "who changed this, and when". As an Admin you see the operational record:
event proposals, events, and staff access profiles. Attendee, invitation, booking, and
appointment entries are outside the Admin data boundary and are not returned to you — a Coordinator
searches those.

1. Set **From** and **To** to bound the period. Both are optional.
2. Choose an **Actor** to narrow by who caused the change: Staff, AttendeeToken, or System.
3. Choose an **Action** to narrow to one recorded change, such as EventConfirmed, EventCancelled,
   CapacityAdjusted, EventImported, StaffAccessChanged, or StaffRolesSynced.
4. Enter an **Identifier** to match one audited entity id or actor id exactly.
5. Select **Search**. Results are newest first, showing When, What, Who, and Details.
6. Select **Load more** to page further back. **Nothing matches these filters** means the search ran
   and found nothing, not that it failed.

Role changes made in the identity provider appear here as StaffRolesSynced entries, recorded the
next time that person signs in. That is the only trace EventBooking has of a role change, because
the change itself happened elsewhere.

## Troubleshooting

- **A colleague is not on Staff access** — they hold no EventBooking role in the identity provider,
  or they have never signed in. Neither is fixable from this screen.
- **A Manager cannot open Event proposals** — their profile shows Awaiting appointment-type
  assignment. Assign the type.
- **Roles look wrong and there is no way to edit them** — correct. Raise the role change through the
  corporate identity process; it appears here after their next sign-in.
- **Scope save conflicts** — another Admin changed the profile first. The screen reloads; review and
  reapply.
- **CSV is rejected** — check the exact header, `yyyy-MM-dd` date, `HH:mm` start time, positive
  capacities, and every line-numbered error. No rows have been imported.
- **Cancel event asks a second time** — active Bookings are affected. Coordinate first; the second
  click performs the cancellation.
- **Audit search returns no attendee entries** — expected. The Admin boundary excludes attendee
  data; ask a Coordinator.
- **Attendee page is unavailable** — expected. A Coordinator must complete attendee work.
`````

## before — docs/user-guides/appointment-staff-guide.md — 1/1

<!-- retirement-file: {"id":2,"file":"docs/user-guides/appointment-staff-guide.md","beforeSha":"371a629c9d347ac73062d14ec207611109663ea720f56a67f239d234f2a52892","afterSha":"81ce6c79a03ae79ac7266b6a34dfa7deb3624e8ed1c8df301d0ce52f4a42a9d3","side":"before","part":1,"parts":1} -->

`````text
# Appointment staff guide

[← All user guides](README.md)

As Appointment staff, you deliver one scoped Appointment Type. The Appointments workspace contains
only operational status: who is expected, who checked in, and whether the appointment was completed
or missed. EventBooking stores no clinical findings, notes, measurements, or results in this flow.

## Before the appointment day

- Sign in and check that the home-page access summary names the correct Appointment Type. Your role
  comes from the company identity provider; the Appointment Type is set by an Admin inside
  EventBooking. If the type is missing or wrong, ask an Admin before the day.
- Open **Appointments** and confirm the expected Confirmed Event appears in the selector.
- If the event or a attendee is missing, ask a Coordinator to check the Booking. Do not create a
  substitute record.
- If you will be working away from a screen, use **Download roster** to take the list with you.

The dependencies are deliberate: Managers or a direct import create the Confirmed Event, a
Coordinator issues an invitation, and the attendee must book before a Booking Appointment appears
for you.

## Appointments screen

Use `/appointments` for the full day-of workflow.

1. In **Event**, select the date and four-hour window you are delivering. Only current and upcoming
   windows for your Appointment Type are listed.
2. Confirm the page heading names your Appointment Type.
3. Review the summary chips: Expected, Checked in, Completed, and No-show.
4. Find the attendee row by name and email.
5. Use only the action that matches what has happened at your station.

Each row shows the current Status, Check-in recorded time, Outcome recorded time, and available
Actions. The counts update when an action succeeds. The attendee list scrolls in place, so the
summary chips and event selector stay visible while you work down a long list.

![Appointments screen for a event with summary chips, the Download roster button, and Expected attendees](screenshots/appointments-list.png)

### Normal outcome path

1. Select **Check in** when the attendee arrives at your station. Check-in opens only on the
   Confirmed Event's transitional-location date; until then the row says so and the button is unavailable.
2. Select **Complete** when your appointment is finished.

The normal path is Expected → Checked in → Completed. You cannot jump directly from Expected to
Completed.

![Appointments screen after a check-in, showing the CheckedIn status and updated summary chips](screenshots/appointments-after-checkin.png)

### Record a no-show

1. Wait until the four-hour window has ended. Until then the row says No-show is available after the
   window ends.
2. On an Expected row, select **No-show**.
3. Review the attendee name in the confirmation prompt.
4. Select **Confirm**.

No-show remains an outstanding requirement. It may make **Arrange missed appointments** available
to the Coordinator, who can send the attendee a recovery invitation containing only recoverable
missed types.

### Correct a mistake

Corrections move one permitted step back and require confirmation:

- Checked in → select **Correct to expected**.
- Completed → select **Correct to checked in**.
- No-show → select **Correct to expected**.

Read the attendee name in the prompt, then select **Confirm** or **Cancel**. The prompt appears as
an overlay above the attendee list so it draws attention before you act.

![Correction confirmation overlay for a Checked in attendee, with Confirm and Cancel buttons](screenshots/appointments-correction-overlay.png)

A correction changes readiness immediately. A No-show correction can be blocked if a later recovery
invitation or Booking already relies on that outcome. Ask the Coordinator to cancel the pending
recovery first; do not record an unrelated status to work around the conflict.

If another staff member changes the same appointment first, EventBooking refreshes the row and asks
you to review it before trying again.

## Download the roster

**Download roster**, beside the summary chips, saves the selected event's list as a CSV file so you
can work from paper or a tablet without the app.

1. Select the event you are delivering.
2. Select **Download roster**. The button reads **Preparing…** while the file is built.
3. The file is saved as `roster-<appointment type>-<date>-<start time>.csv`, for example
   `roster-uniform-fitting-2026-09-18-1300.csv`.

The file holds one row per attendee in the same order as the screen, with these columns:

```text
Attendee Name,Attendee Email,Appointment Type,Status,Checked In At,Outcome At
```

Times are transitional-location local time. The file is a snapshot taken when you pressed the button — it is
not updated afterwards, and writing on it changes nothing in EventBooking. Every check-in, outcome,
and correction must still be recorded on the screen. Treat the file as personal data: it carries
attendee names and email addresses, so store and dispose of it accordingly.

## How your outcome affects the wider workflow

- Each Appointment Type progresses independently. Completing your row never completes another
  team's row.
- A attendee becomes ready only when every current required type has a Completed outcome across
  the original and any non-cancelled recovery Bookings.
- Expected, Checked in, and No-show all remain outstanding.
- A recovery Booking concludes once every appointment in that recovery is Completed or No-show.
  Another No-show can then be recovered again without deleting the earlier attempts.
- Cancelled Bookings and events cannot be acted on and may disappear from the active list. A
  Coordinator or Admin can cancel a whole window, which removes it from your selector.
- Every action you record is written to the audit trail with your identity and the time. A
  Coordinator can see it on the attendee's History.

## Troubleshooting

- **No current or upcoming events** — no active event in scope contains appointment work. Check your
  Appointment Type with an Admin and the Booking with a Coordinator.
- **Check in is unavailable** — the selected event's transitional-location date is not today.
- **No-show is unavailable** — the four-hour window has not ended.
- **Nobody to see in this window** — no attendee in that event requires your Appointment Type.
- **Attendee is absent** — they may not have booked, may have cancelled, or may be on another event.
  Ask a Coordinator to inspect the attendee journey.
- **Only one type is visible** — expected. Your profile exposes only its scoped type.
- **The row refreshed after an error** — another staff member changed it first. Review the current
  status before choosing another action.
- **No-show correction is blocked** — a later recovery depends on it. Ask a Coordinator to cancel
  the pending recovery before correcting the original outcome.
- **Roster download failed** — reselect the event and try again; if it persists the event may have
  been cancelled while the page was open.
`````

## after — docs/user-guides/appointment-staff-guide.md — 1/1

<!-- retirement-file: {"id":2,"file":"docs/user-guides/appointment-staff-guide.md","beforeSha":"371a629c9d347ac73062d14ec207611109663ea720f56a67f239d234f2a52892","afterSha":"81ce6c79a03ae79ac7266b6a34dfa7deb3624e8ed1c8df301d0ce52f4a42a9d3","side":"after","part":1,"parts":1} -->

`````text
# Appointment staff guide

[← All user guides](README.md)

As Appointment staff, you deliver one scoped Appointment Type. The Appointments workspace contains
only operational status: who is expected, who checked in, and whether the appointment was completed
or missed. EventBooking stores no clinical findings, notes, measurements, or results in this flow.

## Before the appointment day

- Sign in and check that the home-page access summary names the correct Appointment Type. Your role
  comes from the company identity provider; the Appointment Type is set by an Admin inside
  EventBooking. If the type is missing or wrong, ask an Admin before the day.
- Open **Appointments** and confirm the expected Confirmed Event appears in the selector.
- If the event or a attendee is missing, ask a Coordinator to check the Booking. Do not create a
  substitute record.
- If you will be working away from a screen, use **Download roster** to take the list with you.

The dependencies are deliberate: Managers negotiate and accept the Event, a
Coordinator issues an invitation, and the attendee must book before a Booking Appointment appears
for you.

## Appointments screen

Use `/appointments` for the full day-of workflow.

1. In **Event**, select the date and four-hour window you are delivering. Only current and upcoming
   windows for your Appointment Type are listed.
2. Confirm the page heading names your Appointment Type.
3. Review the summary chips: Expected, Checked in, Completed, and No-show.
4. Find the attendee row by name and email.
5. Use only the action that matches what has happened at your station.

Each row shows the current Status, Check-in recorded time, Outcome recorded time, and available
Actions. The counts update when an action succeeds. The attendee list scrolls in place, so the
summary chips and event selector stay visible while you work down a long list.

![Appointments screen for a event with summary chips, the Download roster button, and Expected attendees](screenshots/appointments-list.png)

### Normal outcome path

1. Select **Check in** when the attendee arrives at your station. Check-in opens only on the
   Confirmed Event's transitional-location date; until then the row says so and the button is unavailable.
2. Select **Complete** when your appointment is finished.

The normal path is Expected → Checked in → Completed. You cannot jump directly from Expected to
Completed.

![Appointments screen after a check-in, showing the CheckedIn status and updated summary chips](screenshots/appointments-after-checkin.png)

### Record a no-show

1. Wait until the four-hour window has ended. Until then the row says No-show is available after the
   window ends.
2. On an Expected row, select **No-show**.
3. Review the attendee name in the confirmation prompt.
4. Select **Confirm**.

No-show remains an outstanding requirement. It may make **Arrange missed appointments** available
to the Coordinator, who can send the attendee a recovery invitation containing only recoverable
missed types.

### Correct a mistake

Corrections move one permitted step back and require confirmation:

- Checked in → select **Correct to expected**.
- Completed → select **Correct to checked in**.
- No-show → select **Correct to expected**.

Read the attendee name in the prompt, then select **Confirm** or **Cancel**. The prompt appears as
an overlay above the attendee list so it draws attention before you act.

![Correction confirmation overlay for a Checked in attendee, with Confirm and Cancel buttons](screenshots/appointments-correction-overlay.png)

A correction changes readiness immediately. A No-show correction can be blocked if a later recovery
invitation or Booking already relies on that outcome. Ask the Coordinator to cancel the pending
recovery first; do not record an unrelated status to work around the conflict.

If another staff member changes the same appointment first, EventBooking refreshes the row and asks
you to review it before trying again.

## Download the roster

**Download roster**, beside the summary chips, saves the selected event's list as a CSV file so you
can work from paper or a tablet without the app.

1. Select the event you are delivering.
2. Select **Download roster**. The button reads **Preparing…** while the file is built.
3. The file is saved as `roster-<appointment type>-<date>-<start time>.csv`, for example
   `roster-uniform-fitting-2026-09-18-1300.csv`.

The file holds one row per attendee in the same order as the screen, with these columns:

```text
Attendee Name,Attendee Email,Appointment Type,Status,Checked In At,Outcome At
```

Times are transitional-location local time. The file is a snapshot taken when you pressed the button — it is
not updated afterwards, and writing on it changes nothing in EventBooking. Every check-in, outcome,
and correction must still be recorded on the screen. Treat the file as personal data: it carries
attendee names and email addresses, so store and dispose of it accordingly.

## How your outcome affects the wider workflow

- Each Appointment Type progresses independently. Completing your row never completes another
  team's row.
- A attendee becomes ready only when every current required type has a Completed outcome across
  the original and any non-cancelled recovery Bookings.
- Expected, Checked in, and No-show all remain outstanding.
- A recovery Booking concludes once every appointment in that recovery is Completed or No-show.
  Another No-show can then be recovered again without deleting the earlier attempts.
- Cancelled Bookings and events cannot be acted on and may disappear from the active list. A
  Coordinator or Admin can cancel a whole window, which removes it from your selector.
- Every action you record is written to the audit trail with your identity and the time. A
  Coordinator can see it on the attendee's History.

## Troubleshooting

- **No current or upcoming events** — no active event in scope contains appointment work. Check your
  Appointment Type with an Admin and the Booking with a Coordinator.
- **Check in is unavailable** — the selected event's transitional-location date is not today.
- **No-show is unavailable** — the four-hour window has not ended.
- **Nobody to see in this window** — no attendee in that event requires your Appointment Type.
- **Attendee is absent** — they may not have booked, may have cancelled, or may be on another event.
  Ask a Coordinator to inspect the attendee journey.
- **Only one type is visible** — expected. Your profile exposes only its scoped type.
- **The row refreshed after an error** — another staff member changed it first. Review the current
  status before choosing another action.
- **No-show correction is blocked** — a later recovery depends on it. Ask a Coordinator to cancel
  the pending recovery before correcting the original outcome.
- **Roster download failed** — reselect the event and try again; if it persists the event may have
  been cancelled while the page was open.
`````

## before — docs/user-guides/coordinator-guide.md — 1/1

<!-- retirement-file: {"id":3,"file":"docs/user-guides/coordinator-guide.md","beforeSha":"8270ca6d6f5e679d917d020cd10115a6756b45ecb47b5b9e86807ebb45a5dcbb","afterSha":"4c33d20fccc8c55007bb596bf666779efa897eeb64774c2b42bdbf5e30c37fbf","side":"before","part":1,"parts":1} -->

`````text
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
