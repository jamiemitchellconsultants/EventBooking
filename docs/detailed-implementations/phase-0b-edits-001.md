# 00b — Vocabulary edits 1 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — docs/user-guides/README.md — 1/1

<!-- vocabulary-file: {"id":0,"oldPath":"docs/user-guides/README.md","newPath":"docs/user-guides/README.md","beforeSha":"c07e8293d279b7923743e61aa7d79c559785832bfbb03af07d6cca42cb842b5d","afterSha":"dc873be3c4c83ee19afda9619de2bfd6fe18e99a8caf1bb205d159a6a9c0a120","side":"before","part":1,"parts":1} -->

`````markdown
# EventBooking user guides

EventBooking coordinates candidate appointments across five user types. Start with the guide for
your role, then use the workflow below to understand which earlier actions your work depends on.

Every guide here is also published inside the application at `/help`. Staff see all five guides on
that page; a signed-out visitor sees only the candidate guide.

| Guide | Who it is for | Main workspace |
|---|---|---|
| [Admin](admin-guide.md) | System administrators who set appointment-type scope, settings, and direct slot imports | System settings, Staff access, Confirmed slots, Audit trail |
| [Coordinator](coordinator-guide.md) | Recruitment coordinators who manage candidates, invitations, bookings, readiness, and recovery | Candidates, Dashboards, Confirmed slots, Audit trail |
| [Manager](manager-guide.md) | Appointment-type managers who negotiate slots, manage capacity, and may deliver appointments | Slot proposals, Appointments |
| [Appointment staff](appointment-staff-guide.md) | Delivery staff who check candidates in and record outcomes | Appointments |
| [Candidate](candidate-guide.md) | Invited candidates who choose and manage appointment times | Email links for booking and booking management |

## End-to-end workflow and dependencies

| Stage | Owner | What must already exist | Screen and result |
|---|---|---|---|
| 1. Assign roles | Corporate identity provider, not EventBooking | The colleague's account exists in the company directory | Roles are granted centrally; EventBooking records what the sign-in token says |
| 2. Give scoped roles their type | Admin | The colleague has signed in once, so a profile exists | **Staff access**: set the one Appointment Type for a Manager or Appointment staff profile |
| 3. Prepare settings | Admin | Admin access | **System settings**: set invitation expiry and the invite re-issue limit |
| 4. Create capacity | Manager, Admin, or Coordinator | One Manager exists for each Appointment Type, or a slot has already been agreed outside EventBooking | **Slot proposals**: all three Managers accept; or **Confirmed slots**: import the agreed window and all three capacities |
| 5. Add candidates | Coordinator | An Employee Group is known for each candidate | **Candidates**: add manually or import `name,email,employee_group`; EventBooking derives the required Appointment Types |
| 6. Send an invitation | Coordinator | At least three future Confirmed Slots have capacity for every required Appointment Type | **Candidates**: select **Invite now**; the candidate receives three options |
| 7. Book | Candidate | A valid pending invitation | **Choose a time**: select an option and confirm; capacity is reserved and one Booking Appointment is created for each required type |
| 8. Deliver appointments | Manager or Appointment staff | The candidate has an active Booking on the selected Confirmed Slot | **Appointments**: check in, complete, or record No-show for the caller's scoped type |
| 9. Check readiness | Coordinator | Appointment outcomes have been recorded | **Candidates**: expand readiness; the candidate is ready only when every current required type has a Completed outcome |
| 10. Recover a missed appointment | Coordinator, then Candidate | The latest unsatisfied attempt for at least one current required type is No-show | **Candidates**: choose **Arrange missed appointments**; the candidate books a new shared slot containing only the missed types |
| 11. Account for a change | Admin or Coordinator | The change has been recorded | **Audit trail**, or the **History** control on a candidate or slot row |

An upstream delay remains visible at the next stage. For example, a Coordinator cannot issue an
invitation until suitable slot capacity exists, and a Candidate cannot appear in the Appointments
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
| Admin | System settings, Staff access, Confirmed slots, Audit trail |
| Coordinator | Candidates, Dashboards, Confirmed slots, Audit trail |
| Manager | Slot proposals, Appointments |
| Appointment staff | Appointments |

Admin is exclusive and cannot be combined with another role. Coordinator, Manager, and Appointment
staff can be combined in one profile. A combined profile receives the union of the links above, but
Manager and Appointment staff still share one Appointment Type scope.

If the home page says that no role is assigned, your account has no EventBooking role in the company
identity provider; ask whoever administers application access there, not a EventBooking Admin. If a
page or action is missing, first check the access summary: the absence is normally an access rule,
not a page fault.

Candidates do not sign in. They use personal, single-use links sent by email and see no staff
navigation. They can open `/help` without signing in to read the candidate guide.

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

- An **Employee Group** is the candidate's employment category. It determines the complete set of
  Appointment Types the candidate requires; nobody selects those requirements individually.
- A **Slot Proposal** is a suggested four-hour window. Once all three Managers accept it, it becomes
  a **Confirmed Slot** that candidates can book.
- An **Invite** offers a candidate exactly three suitable Confirmed Slots. Confirming one creates a
  **Booking**.
- A **Booking Appointment** is one required Appointment Type inside a Booking. Each type is checked
  in and completed separately.
- **Readiness** means every current required Appointment Type has a Completed Booking Appointment in
  the candidate's non-cancelled journey.
- A **recovery booking** contains only missed required Appointment Types. It preserves completed
  work and every earlier attempt.
- The **audit trail** is the record of every change: what changed, who changed it, and when. It is
  searchable on its own page and inline through the **History** control on candidate and slot rows.

## About the screenshots

The screenshots in these guides were captured from the local demo stack against seeded data, and
are representative rather than exhaustive. They were recaptured on 2026-09-16 and 2026-09-17
against the current screens.

The Appointments check-in and correction screenshots need a slot dated today that already holds
bookings: check-in only opens on a Confirmed Slot's own date, and a candidate can only book a slot
dated after today. To retake them, reseed with `--reseed --reanchor` (see the
[demo runbook](../demo-runbook.md)) on the day of capture. That dates the seeded 13:00 journey slot
today, with Expected Uniform Fitting appointments for `appointment.staff` to check in.

Where a screenshot and the surrounding text disagree, the text describes the current screen.
`````

## after — docs/user-guides/README.md — 1/1

<!-- vocabulary-file: {"id":0,"oldPath":"docs/user-guides/README.md","newPath":"docs/user-guides/README.md","beforeSha":"c07e8293d279b7923743e61aa7d79c559785832bfbb03af07d6cca42cb842b5d","afterSha":"dc873be3c4c83ee19afda9619de2bfd6fe18e99a8caf1bb205d159a6a9c0a120","side":"after","part":1,"parts":1} -->

`````markdown
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

## before — docs/user-guides/admin-guide.md — 1/1

<!-- vocabulary-file: {"id":1,"oldPath":"docs/user-guides/admin-guide.md","newPath":"docs/user-guides/admin-guide.md","beforeSha":"7d4926e9e58ad9bd7f01a5dd660f5a20dc28a9290eba884c01f9807aedd0ec05","afterSha":"8248e055a84c548e7874a972cc61499a5f62b233e794b43a404357d53c044a1a","side":"before","part":1,"parts":1} -->

`````markdown
# Admin guide

[← All user guides](README.md)

As an Admin, you prepare EventBooking for other staff. You set the Appointment Type scope that
scoped roles need, configure invitation timing, import Confirmed Slots agreed outside the normal
Manager negotiation, cancel a window that cannot run, and search the operational audit trail. Admin
access is exclusive: it cannot be combined with another role, and it never exposes candidate,
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
4. If a full slot has already been agreed outside EventBooking, import it on **Confirmed slots**.
5. Hand candidate and invitation work to a Coordinator; hand negotiated capacity to the Managers.
6. Use **Audit trail** when someone asks who changed a slot, a proposal, or an access profile.

## Home page

After signing in, check the access summary at the top of the home page. It should show **Admin** and
no Appointment Type. The page provides four workspace links plus Help:

- **System settings** (`/settings`)
- **Staff access** (`/staff-access`)
- **Confirmed slots** (`/confirmed-slots`)
- **Audit trail** (`/audit`)
- **Help** (`/help`) — every role's guide, including this one

![Admin home page with the workspace links](screenshots/admin-home.png)

The same links also appear as a navigation bar at the top of every Admin page, so you can switch
workspaces without returning to the home page first.

If candidate, dashboard, slot-proposal, or appointment links are absent, EventBooking is enforcing
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

- Scope one Manager profile for each of the three Appointment Types before relying on in-system slot
  negotiation.
- Scope Appointment staff before the appointment date so they can open the Appointments workspace.
- Never treat Admin as "all access": Admin intentionally receives less personal data than a
  Coordinator, and cannot see candidates, invitations, bookings, or candidate audit history.

## System settings screen

Use `/settings` to review the fixed Appointment Types and configure invitation timing.

![System settings screen showing fixed appointment types and invite timing fields](screenshots/system-settings.png)

1. Review **Fixed appointment types**. The three types are fixed by the system; only the Manager
   assignment changes. The Manager identifier column shows the current Manager's name and staff
   number, or **Unassigned** when no Manager holds that type. Change an assignment by scoping a
   Manager profile on Staff access, not here.
2. Set **Invite expiry window (days)** to the number of days a new invitation remains valid.
3. Set **Max auto-retry count** to the number of times an unanswered invitation is automatically
   re-issued before the candidate is marked No response - needs follow-up for a Coordinator to
   re-invite manually.
4. Select **Save changes**.
5. Wait for **Saved. These changes apply to invites created from now on.** before leaving.

Changes do not reach back and alter invitations already sent.

## Confirmed slots screen

Use `/confirmed-slots` for a complete window agreed outside EventBooking, and to cancel a confirmed
window. Coordinators see the same screen.

![Confirmed slots screen with CSV import control](screenshots/confirmed-slots-import.png)

### Import already agreed slots

A direct import skips Manager acceptance and becomes bookable immediately.

1. Prepare a UTF-8 CSV whose exact header is:

   ```text
   date,startTime,DAT,MED,UNI
   ```

2. Add one four-hour window per row in `yyyy-MM-dd,HH:mm` format, with a positive total headcount
   for Drug & Alcohol Testing (`DAT`), Medical Check-up (`MED`), and Uniform Fitting (`UNI`). Use
   future dates for bookable capacity; past slots are retained as history and are never offered.
3. In **Import already agreed slots**, choose the CSV file.
4. Wait for the imported-count confirmation.

The import is all-or-nothing. If the screen says **Nothing was imported**, use every line-numbered
message to correct the file, then upload the whole file again. Imported slots do not require an
additional save or Manager approval.

### Cancel a confirmed slot

The **Cancel a confirmed slot** card lists every confirmed window with its date, four-hour window,
remaining-over-total capacity for each type, and the number of active Bookings. Cancel only when the
whole window cannot run.

![Cancel a confirmed slot card listing each window's date, capacity by type, active bookings, and a Cancel slot button](screenshots/confirmed-slots-cancel.png)

1. Warn the Coordinator and the delivery teams first.
2. Select **Cancel slot** on the row.
3. If the slot holds active Bookings, the request is refused and the message asks you to confirm.
   Read it, then select **Confirm cancel** only when you intend to proceed.
4. Tell the Coordinator to monitor the affected candidates and their replacement invitations.

Cancellation voids the Bookings on that slot, releases their capacity, and triggers the candidate
rebooking workflow. A Manager can cancel their own confirmed windows from the Slot proposals screen,
and a Coordinator can cancel from this screen, so agree who is acting before anyone clicks.

## Audit trail screen

Use `/audit` to answer "who changed this, and when". As an Admin you see the operational record:
slot proposals, confirmed slots, and staff access profiles. Candidate, invitation, booking, and
appointment entries are outside the Admin data boundary and are not returned to you — a Coordinator
searches those.

1. Set **From** and **To** to bound the period. Both are optional.
2. Choose an **Actor** to narrow by who caused the change: Staff, CandidateToken, or System.
3. Choose an **Action** to narrow to one recorded change, such as SlotConfirmed, SlotCancelled,
   CapacityAdjusted, SlotImported, StaffAccessChanged, or StaffRolesSynced.
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
- **A Manager cannot open Slot proposals** — their profile shows Awaiting appointment-type
  assignment. Assign the type.
- **Roles look wrong and there is no way to edit them** — correct. Raise the role change through the
  corporate identity process; it appears here after their next sign-in.
- **Scope save conflicts** — another Admin changed the profile first. The screen reloads; review and
  reapply.
- **CSV is rejected** — check the exact header, `yyyy-MM-dd` date, `HH:mm` start time, positive
  capacities, and every line-numbered error. No rows have been imported.
- **Cancel slot asks a second time** — active Bookings are affected. Coordinate first; the second
  click performs the cancellation.
- **Audit search returns no candidate entries** — expected. The Admin boundary excludes candidate
  data; ask a Coordinator.
- **Candidate page is unavailable** — expected. A Coordinator must complete candidate work.
`````

## after — docs/user-guides/admin-guide.md — 1/1

<!-- vocabulary-file: {"id":1,"oldPath":"docs/user-guides/admin-guide.md","newPath":"docs/user-guides/admin-guide.md","beforeSha":"7d4926e9e58ad9bd7f01a5dd660f5a20dc28a9290eba884c01f9807aedd0ec05","afterSha":"8248e055a84c548e7874a972cc61499a5f62b233e794b43a404357d53c044a1a","side":"after","part":1,"parts":1} -->

`````markdown
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

## before — docs/user-guides/appointment-staff-guide.md — 1/1

<!-- vocabulary-file: {"id":2,"oldPath":"docs/user-guides/appointment-staff-guide.md","newPath":"docs/user-guides/appointment-staff-guide.md","beforeSha":"1a50212b8a833807f4c0caef6232a8521d89a6d701f5c7c3489c2cd8a72c0810","afterSha":"371a629c9d347ac73062d14ec207611109663ea720f56a67f239d234f2a52892","side":"before","part":1,"parts":1} -->

`````markdown
# Appointment staff guide

[← All user guides](README.md)

As Appointment staff, you deliver one scoped Appointment Type. The Appointments workspace contains
only operational status: who is expected, who checked in, and whether the appointment was completed
or missed. EventBooking stores no clinical findings, notes, measurements, or results in this flow.

## Before the appointment day

- Sign in and check that the home-page access summary names the correct Appointment Type. Your role
  comes from the company identity provider; the Appointment Type is set by an Admin inside
  EventBooking. If the type is missing or wrong, ask an Admin before the day.
- Open **Appointments** and confirm the expected Confirmed Slot appears in the selector.
- If the slot or a candidate is missing, ask a Coordinator to check the Booking. Do not create a
  substitute record.
- If you will be working away from a screen, use **Download roster** to take the list with you.

The dependencies are deliberate: Managers or a direct import create the Confirmed Slot, a
Coordinator issues an invitation, and the candidate must book before a Booking Appointment appears
for you.

## Appointments screen

Use `/appointments` for the full day-of workflow.

1. In **Slot**, select the date and four-hour window you are delivering. Only current and upcoming
   windows for your Appointment Type are listed.
2. Confirm the page heading names your Appointment Type.
3. Review the summary chips: Expected, Checked in, Completed, and No-show.
4. Find the candidate row by name and email.
5. Use only the action that matches what has happened at your station.

Each row shows the current Status, Check-in recorded time, Outcome recorded time, and available
Actions. The counts update when an action succeeds. The candidate list scrolls in place, so the
summary chips and slot selector stay visible while you work down a long list.

![Appointments screen for a slot with summary chips, the Download roster button, and Expected candidates](screenshots/appointments-list.png)

### Normal outcome path

1. Select **Check in** when the candidate arrives at your station. Check-in opens only on the
   Confirmed Slot's head-office date; until then the row says so and the button is unavailable.
2. Select **Complete** when your appointment is finished.

The normal path is Expected → Checked in → Completed. You cannot jump directly from Expected to
Completed.

![Appointments screen after a check-in, showing the CheckedIn status and updated summary chips](screenshots/appointments-after-checkin.png)

### Record a no-show

1. Wait until the four-hour window has ended. Until then the row says No-show is available after the
   window ends.
2. On an Expected row, select **No-show**.
3. Review the candidate name in the confirmation prompt.
4. Select **Confirm**.

No-show remains an outstanding requirement. It may make **Arrange missed appointments** available
to the Coordinator, who can send the candidate a recovery invitation containing only recoverable
missed types.

### Correct a mistake

Corrections move one permitted step back and require confirmation:

- Checked in → select **Correct to expected**.
- Completed → select **Correct to checked in**.
- No-show → select **Correct to expected**.

Read the candidate name in the prompt, then select **Confirm** or **Cancel**. The prompt appears as
an overlay above the candidate list so it draws attention before you act.

![Correction confirmation overlay for a Checked in candidate, with Confirm and Cancel buttons](screenshots/appointments-correction-overlay.png)

A correction changes readiness immediately. A No-show correction can be blocked if a later recovery
invitation or Booking already relies on that outcome. Ask the Coordinator to cancel the pending
recovery first; do not record an unrelated status to work around the conflict.

If another staff member changes the same appointment first, EventBooking refreshes the row and asks
you to review it before trying again.

## Download the roster

**Download roster**, beside the summary chips, saves the selected slot's list as a CSV file so you
can work from paper or a tablet without the app.

1. Select the slot you are delivering.
2. Select **Download roster**. The button reads **Preparing…** while the file is built.
3. The file is saved as `roster-<appointment type>-<date>-<start time>.csv`, for example
   `roster-uniform-fitting-2026-09-18-1300.csv`.

The file holds one row per candidate in the same order as the screen, with these columns:

```text
Candidate Name,Candidate Email,Appointment Type,Status,Checked In At,Outcome At
```

Times are head-office local time. The file is a snapshot taken when you pressed the button — it is
not updated afterwards, and writing on it changes nothing in EventBooking. Every check-in, outcome,
and correction must still be recorded on the screen. Treat the file as personal data: it carries
candidate names and email addresses, so store and dispose of it accordingly.

## How your outcome affects the wider workflow

- Each Appointment Type progresses independently. Completing your row never completes another
  team's row.
- A candidate becomes ready only when every current required type has a Completed outcome across
  the original and any non-cancelled recovery Bookings.
- Expected, Checked in, and No-show all remain outstanding.
- A recovery Booking concludes once every appointment in that recovery is Completed or No-show.
  Another No-show can then be recovered again without deleting the earlier attempts.
- Cancelled Bookings and slots cannot be acted on and may disappear from the active list. A
  Coordinator or Admin can cancel a whole window, which removes it from your selector.
- Every action you record is written to the audit trail with your identity and the time. A
  Coordinator can see it on the candidate's History.

## Troubleshooting

- **No current or upcoming slots** — no active slot in scope contains appointment work. Check your
  Appointment Type with an Admin and the Booking with a Coordinator.
- **Check in is unavailable** — the selected slot's head-office date is not today.
- **No-show is unavailable** — the four-hour window has not ended.
- **Nobody to see in this window** — no candidate in that slot requires your Appointment Type.
- **Candidate is absent** — they may not have booked, may have cancelled, or may be on another slot.
  Ask a Coordinator to inspect the candidate journey.
- **Only one type is visible** — expected. Your profile exposes only its scoped type.
- **The row refreshed after an error** — another staff member changed it first. Review the current
  status before choosing another action.
- **No-show correction is blocked** — a later recovery depends on it. Ask a Coordinator to cancel
  the pending recovery before correcting the original outcome.
- **Roster download failed** — reselect the slot and try again; if it persists the slot may have
  been cancelled while the page was open.
`````

## after — docs/user-guides/appointment-staff-guide.md — 1/1

<!-- vocabulary-file: {"id":2,"oldPath":"docs/user-guides/appointment-staff-guide.md","newPath":"docs/user-guides/appointment-staff-guide.md","beforeSha":"1a50212b8a833807f4c0caef6232a8521d89a6d701f5c7c3489c2cd8a72c0810","afterSha":"371a629c9d347ac73062d14ec207611109663ea720f56a67f239d234f2a52892","side":"after","part":1,"parts":1} -->

`````markdown
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

## before — docs/user-guides/candidate-guide.md — 1/1

<!-- vocabulary-file: {"id":3,"oldPath":"docs/user-guides/candidate-guide.md","newPath":"docs/user-guides/attendee-guide.md","beforeSha":"4dc4f217816639ceaa63be533ef5241787e676de34f6420a83c97d4f15d51705","afterSha":"6f699b6fcfe664abe0b9af57f81537b0c4da8e26f92f069c0b0f6afccf51b3da","side":"before","part":1,"parts":1} -->

`````markdown
# Candidate guide

[← All user guides](README.md)

EventBooking helps you choose one four-hour visit for the appointments required by your Employee
Group. You do not need an account and never sign in. Use only the personal links sent to your email.

## Book your first appointment visit

You can start after the recruitment team sends your invitation.

1. Open the **Choose a time** link in the invitation email.
2. Check that your name and the listed appointments are correct.
3. Review the three available dates and four-hour windows.
4. Select one option.

   ![Choose a time page listing the candidate's name, required appointments, and three date options](screenshots/choose-a-time.png)

5. Select **Confirm this time** once and wait for **Booking confirmed**.
6. Check the confirmed date, start and end time, and head-office address.
7. Open or save **Use your booking management link**. A copy is also sent in the confirmation email.

   ![Booking confirmed page showing the confirmed date, head-office address, and management link](screenshots/booking-confirmed.png)

The invitation link is personal and single-use. EventBooking reserves space only for the
appointments listed on that invitation. If one option loses capacity while you are choosing, the
page refreshes the available choices; select one of the remaining options or contact the recruitment
team if none remain.

Your Booking is valid as soon as **Booking confirmed** appears. If the page says email delivery
could not be confirmed, save the on-screen management link and contact the recruitment team only if
you need help; do not repeat the booking.

## Attend your appointments

Come to head office during the confirmed four-hour window. Follow the team's arrival instructions.
Each appointment is checked in and completed separately, but every appointment listed in your
initial confirmation takes place within the same shared window.

Completing one appointment does not automatically complete the others. The recruitment team can
see when all required appointments are complete.

## Manage or cancel a Booking

Open the management link for the Booking you want to change. The **Manage your booking** screen
shows its date and four-hour window.

![Manage your booking screen showing the appointment date/window and Cancel booking / Cancel and choose a new time actions](screenshots/manage-booking.png)

### Cancel without choosing another time

1. Select **Cancel booking**.
2. Wait for **Booking cancelled**.
3. Contact the recruitment team if you still need an appointment; no replacement invitation is
   requested by this action.

### Cancel and request fresh choices for your original visit

1. Select **Cancel and choose a new time**.
2. Wait for **Booking cancelled**.
3. If suitable capacity exists, EventBooking sends a new invitation with fresh options.
4. If no suitable times exist or email delivery cannot be confirmed, the page tells you the
   recruitment team will follow up.

Cancelling the original Booking cancels that appointment journey, including any pending or active
missed-appointment recovery.

### Cancel a missed-appointment recovery

Use the management link from the recovery confirmation and select **Cancel booking**. Cancelling a
recovery returns only that recovery's reserved capacity. It leaves the original visit, already
Completed appointments, and earlier attempts intact. EventBooking does not automatically create a
replacement recovery invitation; the recruitment team can arrange another if the appointment is
still required.

### If the recruitment team cancels for you

The recruitment team can also cancel your booking on your behalf — for example when a whole visiting
window has to be called off. You may then receive one of two things without having asked for it:

- A fresh invitation email with new times. Choose one exactly as you did the first time; your old
  management link no longer works.
- Nothing immediately, because no suitable times were open. The team will contact you.

Either way, the visit you had booked no longer stands. If you receive a new invitation you were not
expecting, treat it as the current one and contact the recruitment team if it looks wrong.

## Book missed appointments

If an appointment is recorded as No-show and still needs to be completed, the recruitment team may
send a recovery invitation.

1. Open the email link. The page heading says **Choose a new time for your missed appointment**
   (one missed appointment) or **Choose a new time for your missed appointments** (two or three).
2. Check the list: it contains only the still-required missed appointments being recovered. It does
   not repeat appointments you already completed.
3. Choose one of the three shared four-hour windows.
4. Select **Confirm this time** and wait for **Booking confirmed**.
5. Keep the new management link; it manages this recovery Booking.

Attend only the appointment types named in that recovery confirmation. If a replacement appointment
is also missed, the recruitment team can arrange another recovery without erasing the earlier
history.

## When a link does not work

For privacy, malformed, expired, used, cancelled, superseded, or otherwise stale invitation links
all show the same message: **This booking link is no longer valid.**

- If the page says the link has expired or is no longer valid, contact the recruitment team for a
  fresh invitation. The contact address is shown on the page.
- If **Nothing fits right now** appears, contact the recruitment team; suitable capacity is not
  currently available.
- If the page reports a temporary loading or confirmation error, select **Try again** once. If it
  persists, contact the recruitment team and do not forward your personal link.
- If your name or appointment list is wrong, stop before confirming and contact the recruitment
  team.
- If you lose a management link, check the confirmation email. If it is not available, ask the
  recruitment team for help.

## Reading this guide again

This guide is also published inside EventBooking. Open `/help` on the same site as your booking
link — no sign-in needed — and it is the page you see.
`````

## after — docs/user-guides/attendee-guide.md — 1/1

<!-- vocabulary-file: {"id":3,"oldPath":"docs/user-guides/candidate-guide.md","newPath":"docs/user-guides/attendee-guide.md","beforeSha":"4dc4f217816639ceaa63be533ef5241787e676de34f6420a83c97d4f15d51705","afterSha":"6f699b6fcfe664abe0b9af57f81537b0c4da8e26f92f069c0b0f6afccf51b3da","side":"after","part":1,"parts":1} -->

`````markdown
# Attendee guide

[← All user guides](README.md)

EventBooking helps you choose one four-hour visit for the appointments required by your Employee
Group. You do not need an account and never sign in. Use only the personal links sent to your email.

## Book your first appointment visit

You can start after the recruitment team sends your invitation.

1. Open the **Choose a time** link in the invitation email.
2. Check that your name and the listed appointments are correct.
3. Review the three available dates and four-hour windows.
4. Select one option.

   ![Choose a time page listing the attendee's name, required appointments, and three date options](screenshots/choose-a-time.png)

5. Select **Confirm this time** once and wait for **Booking confirmed**.
6. Check the confirmed date, start and end time, and transitional-location address.
7. Open or save **Use your booking management link**. A copy is also sent in the confirmation email.

   ![Booking confirmed page showing the confirmed date, transitional-location address, and management link](screenshots/booking-confirmed.png)

The invitation link is personal and single-use. EventBooking reserves space only for the
appointments listed on that invitation. If one option loses capacity while you are choosing, the
page refreshes the available choices; select one of the remaining options or contact the recruitment
team if none remain.

Your Booking is valid as soon as **Booking confirmed** appears. If the page says email delivery
could not be confirmed, save the on-screen management link and contact the recruitment team only if
you need help; do not repeat the booking.

## Attend your appointments

Come to transitional location during the confirmed four-hour window. Follow the team's arrival instructions.
Each appointment is checked in and completed separately, but every appointment listed in your
initial confirmation takes place within the same shared window.

Completing one appointment does not automatically complete the others. The recruitment team can
see when all required appointments are complete.

## Manage or cancel a Booking

Open the management link for the Booking you want to change. The **Manage your booking** screen
shows its date and four-hour window.

![Manage your booking screen showing the appointment date/window and Cancel booking / Cancel and choose a new time actions](screenshots/manage-booking.png)

### Cancel without choosing another time

1. Select **Cancel booking**.
2. Wait for **Booking cancelled**.
3. Contact the recruitment team if you still need an appointment; no replacement invitation is
   requested by this action.

### Cancel and request fresh choices for your original visit

1. Select **Cancel and choose a new time**.
2. Wait for **Booking cancelled**.
3. If suitable capacity exists, EventBooking sends a new invitation with fresh options.
4. If no suitable times exist or email delivery cannot be confirmed, the page tells you the
   recruitment team will follow up.

Cancelling the original Booking cancels that appointment journey, including any pending or active
missed-appointment recovery.

### Cancel a missed-appointment recovery

Use the management link from the recovery confirmation and select **Cancel booking**. Cancelling a
recovery returns only that recovery's reserved capacity. It leaves the original visit, already
Completed appointments, and earlier attempts intact. EventBooking does not automatically create a
replacement recovery invitation; the recruitment team can arrange another if the appointment is
still required.

### If the recruitment team cancels for you

The recruitment team can also cancel your booking on your behalf — for example when a whole visiting
window has to be called off. You may then receive one of two things without having asked for it:

- A fresh invitation email with new times. Choose one exactly as you did the first time; your old
  management link no longer works.
- Nothing immediately, because no suitable times were open. The team will contact you.

Either way, the visit you had booked no longer stands. If you receive a new invitation you were not
expecting, treat it as the current one and contact the recruitment team if it looks wrong.

## Book missed appointments

If an appointment is recorded as No-show and still needs to be completed, the recruitment team may
send a recovery invitation.

1. Open the email link. The page heading says **Choose a new time for your missed appointment**
   (one missed appointment) or **Choose a new time for your missed appointments** (two or three).
2. Check the list: it contains only the still-required missed appointments being recovered. It does
   not repeat appointments you already completed.
3. Choose one of the three shared four-hour windows.
4. Select **Confirm this time** and wait for **Booking confirmed**.
5. Keep the new management link; it manages this recovery Booking.

Attend only the appointment types named in that recovery confirmation. If a replacement appointment
is also missed, the recruitment team can arrange another recovery without erasing the earlier
history.

## When a link does not work

For privacy, malformed, expired, used, cancelled, superseded, or otherwise stale invitation links
all show the same message: **This booking link is no longer valid.**

- If the page says the link has expired or is no longer valid, contact the recruitment team for a
  fresh invitation. The contact address is shown on the page.
- If **Nothing fits right now** appears, contact the recruitment team; suitable capacity is not
  currently available.
- If the page reports a temporary loading or confirmation error, select **Try again** once. If it
  persists, contact the recruitment team and do not forward your personal link.
- If your name or appointment list is wrong, stop before confirming and contact the recruitment
  team.
- If you lose a management link, check the confirmation email. If it is not available, ask the
  recruitment team for help.

## Reading this guide again

This guide is also published inside EventBooking. Open `/help` on the same site as your booking
link — no sign-in needed — and it is the page you see.
`````
