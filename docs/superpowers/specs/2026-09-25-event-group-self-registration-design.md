# `EventGroup` self-registration — design

**Date:** 2026-09-25
**Status:** Proposed for review; the matching plans describe implementation but no product code has changed.

## Purpose and decision context

The original EventBooking design excluded public self-registration and required a Coordinator to
enter every Attendee. This feature changes that product decision for selected Events. An Admin or
Coordinator can publish an `EventGroup`, choose the `AttendeeGroup`s it serves, and expose individual
Events within it. A potential attendee can see the published Events, submit their name and email
address with an eligible `AttendeeGroup`, and confirm through an emailed link before a place is
taken. Staff invitation and booking remain available for other Events.

Success means staff can publish and withdraw access without changing event negotiation; anonymous
visitors see only deliberately open material; a confirmed registration creates one ordinary
Booking with the selected group's required appointments; and concurrent confirmations cannot
overbook a type.

## Decisions and alternatives

1. **A dedicated `EventGroup` aggregate.** It owns title, description, publication, allowed
   `AttendeeGroup`s, and Event memberships. Putting these settings on each Event would duplicate
   group copy and eligibility rules. Reusing an Invite as the public directory would make an
   attendee's chosen Event an indirect option.
2. **Per-membership publication.** An Event may belong to several `EventGroup`s. Each
   `EventGroupEvent` membership has its own open/private flag; opening it in one group does not
   expose it in another. This matches the requested individual gate.
3. **Exact coverage of appointment types.** The `EventGroup`'s appointment-type set is derived as
   the union of its listed `AttendeeGroup`s' requirements. Every member Event must list exactly that
   set. Each listed `AttendeeGroup` may need a subset; no Event type is unrelated to all listed
   groups. This is the chosen interpretation of “fully intersect.”
4. **Email confirmation before capacity charge.** Submission creates a pending `SelfRegistration`
   and an outbox email. No Attendee, Invite, Booking, or capacity charge exists until the emailed
   confirmation link is used. The form collects the required name as well as email and group.
5. **Use the existing booking journey.** Confirmation creates a one-option initial Invite for
   the chosen Event and immediately uses it to create the Booking. Its option-count snapshot is
   one for this path; ordinary invitations still use `SystemSettings`. The existing cancellation,
   confirmation email, and manage-link behavior then apply. Later re-invitation follows the
   existing location and requirement rules; the `EventGroup` gate controls the initial public
   acquisition only.

## Scope

Included:

- Staff list, create, edit, open/close, and membership management for `EventGroup`s.
- `AttendeeGroup` description in Admin reference-data create/edit and public group choice.
- Public `EventGroup` list page and Event registration page.
- Pending `SelfRegistration`, confirmation email, token, expiry, and transactional confirmation.
- Audit, rate limiting, API/OpenAPI/MCP parity, persistence migration, and tests.

Excluded:

- Public search or directory of `EventGroup`s; staff share a group's URL directly.
- Wait lists, payment, anonymous attendee-account creation, and automatic capacity reservation
  while an email is pending.
- Changing how Managers propose/accept Events or how appointment staff conduct appointments.
- Publishing attendee counts or exact remaining capacity to anonymous visitors.

## Domain and invariants

### `EventGroup` and memberships

`EventGroup` has an immutable ID, required title (1–160 characters), optional description (at most
2,000 characters), isOpen (default false), and a concurrency version. It must have at least one
active `AttendeeGroup`. Staff may replace its selected groups only when the resulting union of
required `AppointmentType`s remains identical to every member Event's type set. New Event
memberships start private. A membership is unique per `EventGroup` and Event, while one Event may
belong to multiple groups. An Event can be added only when it is active, future, and its capacity
type IDs equal the group's derived set. Staff may remove a membership; existing Bookings remain
valid. Closing a group or membership blocks new submissions and confirmations but never cancels
an existing Booking.

An `AttendeeGroup` gains an optional description of at most 500 characters. Existing rows receive
an empty description. Admin alone edits `AttendeeGroup` reference data; Admin and Coordinator can
manage `EventGroup`s through the new `ManageEventGroups` capability. Neither the staff list nor its
Admin view exposes attendee details. An `AttendeeGroup` in an `EventGroup` cannot be deactivated.
Editing its requirement mapping is refused if the new union would no longer equal any member
Event's type set; the existing active-booking restriction still applies.

### `SelfRegistration`

`SelfRegistration` stores ID, `EventGroup` ID, Event ID, `AttendeeGroup` ID, submitted name,
normalized email, expiry, status, token version, and nullable resulting Attendee/Booking IDs.
Pending, Confirmed, and Expired are its states, with terminalAt stamped on the latter two. The
confirmation link is a signed bearer token with its own purpose and a 24-hour expiry. The token
and rendered URL are never stored in the
database or audit log. The outbox stores its `SelfRegistration` ID and renders the email from that
row. Pending rows do not consume capacity. A pending request becomes expired when read after its
expiry; expired or confirmed tokens cannot book again.

Confirmation revalidates the group and membership open flags, current `AttendeeGroup` membership,
exact appointment-type compatibility, active/future Event, and spare capacity for every type the
selected `AttendeeGroup` requires. It locks in the established Attendee → Invite → Event →
`EventCapacity` order, after first locking the `EventGroup` row; staff publication and referenced
`AttendeeGroup` mapping edits take that same `EventGroup` lock before changing those gates or
mappings. This makes a close or mapping edit and a confirmation serialize at the group boundary.
Creation or reuse of the Attendee, one-option Invite, Booking, `BookingAppointment`s, status
changes, audit entries, and confirmation email outbox row commit together. The capacity rows are
locked in ascending Appointment Type ID order; a unique active-original-Booking constraint and
`SelfRegistration` state prevent duplicate bookings.

An existing Attendee with the same normalized email may be reused only when the selected Attendee
Group matches and no active original Booking exists. Its stored name is preserved. A different
group or active Booking produces a clear conflict after email ownership is proven; no staff record
is silently reassigned. Any pending initial Invite for a reused Attendee is superseded before the
one-option Invite is created. A database email uniqueness race is treated as a conflict and does not
leave a partial registration. A repeated confirmation shows an already-used message without
creating another Booking. The usual booking confirmation email supplies the manage link.

## Staff experience and API

The staff `EventGroup`s screen lists title, publication state, selected `AttendeeGroup`s, and Event
memberships. A create/edit form validates lengths and selected groups, shows the derived
`AppointmentType`s, and explains why a candidate Event is incompatible or already started. Staff
can toggle the group gate and each `EventGroupEvent` gate separately. The shared public URL is
visible to staff. Admin and Coordinator are authorized server-side for every mutation; other
staff roles receive 403. Optimistic concurrency returns 409 with a reload prompt.

Staff API routes sit under /api/event-groups and require staff authorization plus the new
capability. They provide list/detail, create/update, add/remove Event, and membership publication
operations. `AttendeeGroup` reference-data requests/responses gain description. All operations
appear in OpenAPI and the existing agent operation catalog. Audit records capture IDs, canonical
group/type codes and old/new open states only, never title, description, name or email.

## Anonymous experience and API

A shareable /event-groups/{groupId} page displays the group title and description, eligible
`AttendeeGroup` names and descriptions, and open member Events ordered by local start date/time.
Each Event card shows Location, local Event Window, and appointment types. A private group returns
the same 404 shape as an unknown group. Private membership, cancelled or started Event never
appears. An open group with no available Events shows an empty state.

Selecting an Event opens /event-groups/{groupId}/events/{eventId}. The visitor enters name
(1–200 characters), email (at most 320 characters), and one of the `EventGroup`'s active Attendee
Groups. The page explains that a confirmation email is required and that submission does not hold
a place. An Event may be available for one
group and full for another; the form disables a group without capacity, while the server remains
authoritative. A successful submission gives the same neutral “check your email” result whether
the address is new or belongs to an existing Attendee. Invalid fields return 422; a closed or
unknown public resource returns 404; capacity exhaustion returns 409. The POST is rate limited by
IP and normalized-email key. No email address or Attendee existence is returned by public GETs.

The email link opens /event-groups/confirm/{token}. The page shows the selected Event summary and
a confirm button. Confirmation rechecks all gates and capacity. If the Event has filled, become
private, started, or been cancelled, it explains that registration cannot be completed and points
to the group page or Coordinator contact. Success shows a booking-created message and explains
that the manage link is in the booking confirmation email. No third-party scripts or analytics
receive the token. Both anonymous endpoints use the plain public HTTP client, never staff tokens.

## Notifications, retention, and failure behavior

The existing durable `EmailLog` outbox gains a `SelfRegistrationConfirmation` template and a
nullable `SelfRegistration` context ID. Exactly one of Attendee ID and `SelfRegistration` ID is
present for a delivery attempt. The dispatcher renders a confirmation email only while the
request is pending and unexpired. Retries use existing backoff, and duplicate deliveries remain
safe because confirmation is single-use. The email contains the event summary and the link, but
logs and telemetry never contain name, email, or raw token.

Expired pending requests are marked expired by the normal maintenance sweep. Confirmed and
expired `SelfRegistration` rows, with their name and email, are deleted 30 days after terminal
state; related confirmation `EmailLog` rows are removed first under the repository's existing
retention pattern. Audit records retain identifiers but no submitted personal data.

## Validation

- Domain tests: exact union, incompatible Event rejection, duplicate membership, independent
  gates, description and title limits, state transitions.
- Application tests: Admin/Coordinator authorization, group mapping drift, closed group or Event,
  delayed confirmation, duplicate clicks, existing-email cases, and capacity races.
- PostgreSQL/API tests: migration constraints and indexes, public 404/409/422/429 behavior,
  one Booking under concurrent confirmation, durable email retry, no personal data in audit.
- Web tests: public list/detail and form states, client request shape, private resource handling,
  accessible labels and error announcements.
- Repository gates: ontology generation/check, ontology term check, build and test suite.

## Consequences

This adds a second acquisition path to the existing Attendee lifecycle and deliberately reverses
the original self-registration exclusion. It does not weaken Manager negotiation or capacity
accounting. Publication can be withdrawn immediately, although already sent emails may then lead
to a closed-registration message and existing Bookings are unaffected. A submitted request does
not guarantee a place; capacity is allocated only when confirmation succeeds.
