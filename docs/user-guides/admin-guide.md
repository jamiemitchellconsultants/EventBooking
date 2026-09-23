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
