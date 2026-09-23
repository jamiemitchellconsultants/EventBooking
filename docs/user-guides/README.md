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
