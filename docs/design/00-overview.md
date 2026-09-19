# 00 — Overview

[Index](README.md) · [Domain model →](01-domain-model.md)

## The problem

An organisation runs events that people attend by invitation, for example onboarding days,
assessment centres, health screenings or equipment fittings. Each event has three properties:

- **It happens at one `Location`,** with its own address and time zone.
- **It lasts a fixed time.** This is its `EventWindow`: a date, a start time and a duration.
- **It offers several `AppointmentType`s,** each delivered by a different team whose `Manager`
  controls that team's capacity. On any given event, a team can see as many attendees as it has
  staff for, and that number differs team by team and day by day.

Different attendees need different combinations of appointment types, according to their
`AttendeeGroup`. An attendee attends one event and completes all their appointments inside its
window, in any order.

An event is only bookable once **every** team whose appointment type it offers has agreed to staff
it and has said how many attendees it can take. Attendees are then emailed a small set of event
options that cover everything they need, and they confirm one.

The problem EventBooking solves: **coordinating capacity across independent teams, and
guaranteeing that no appointment type on any event is ever booked beyond what its team agreed to**.
That guarantee has to hold even when many attendees confirm the last places at the same instant.

## What must never change

1. **No overbooking.** `EventCapacity.remainingCapacity` never goes below zero or above its
   `totalHeadcount`, under any concurrency. This is enforced in two independent layers: an
   application transaction with ordered row locks, and a database check constraint.
2. **Negotiated availability.** No `Event` becomes bookable until every listed `AppointmentType`'s
   `Manager` has independently accepted it with their own headcount. The coordination is built into
   the data model, not a policy people are trusted to follow.
3. **Minimum data by role.** Admin never sees attendee data. Appointment staff see only the fields
   needed to run their own appointment type. Audit records never contain names or email addresses.
4. **The vocabulary.** [docs/ontology.md](../ontology.md) is canonical. Code, API resources, table
   names and screens read directly off it.

## Scope

**In scope:**

- Admin-managed reference data: `Location`s, `AppointmentType`s, `AttendeeGroup`s and settings.
- Negotiation of `EventProposal`s by any number of Managers.
- Capacity management.
- `Attendee` management with CSV import.
- The invite engine, with location selection, expiry, automatic re-issue and option top-up.
- The anonymous attendee booking and manage flow.
- Cancellation and rescheduling.
- The appointment-day workspace: check-in, outcomes and the roster.
- Missed-appointment recovery.
- Staff access synchronised from the identity provider.
- Notifications through a durable outbox.
- Audit and audit search, and coordinator dashboards.
- A REST API with OpenAPI and an MCP tool server kept at parity.
- Local and home-lab Docker deployment.

**Out of scope, deliberately:**

- **Multi-tenancy.** An instance serves one organisation.
- **Per-location Managers.** One Manager per `AppointmentType`, across every `Location`.
- **Bulk import of events.** Events come only from negotiation.
- **Attendee-chosen appointment types.** Requirements always derive from the attendee's group.
- **Reminders before an upcoming booking.** See [09](09-predecessor-traceability.md#product-questions-carried-as-decided).
- **Historical reporting beyond the 7-day workspace window and audit search.**
- **Scheduling relative to an attendee's milestone** (for example, a start date).
- **Any cloud-provider deployment target.**
- **Payments, waiting lists and public self-registration.** Every attendee is entered by a
  Coordinator.

## Decisions

These were settled when the package was scoped. The full rationale is in the
[decision record](../superpowers/specs/2026-09-19-eventbooking-design.md#2-decisions-taken).

| # | Decision |
|---|---|
| D1 | One Manager per `AppointmentType`, across every `Location` |
| D2 | Negotiation is the only way an `Event` is created. The proposer lists the types, and each listed type's Manager accepts with a headcount |
| D3 | `AttendeeRequirement` is derived from an Admin-managed `AttendeeGroup` |
| D4 | The Coordinator chooses one or more `Location`s per `Invite`, snapshotted as `InviteLocation` |
| D5 | Built by porting the predecessor's .NET solution and generalising it |
| D6 | No bulk import of events |
| D7 | `SystemSettings.inviteOptionCount` is configurable from 1 to 5, default 3 |
| D8 | Keycloak (OIDC) is the only identity provider shipped, behind a provider seam |
| D9 | No object storage |
| D10 | A neutral visual theme, re-skinnable through tokens |
| D11 | The `StaffId` format is deployment-configured |
| D12 | A fresh database schema, with no migration from the predecessor |

Three further decisions were made in this package, closing questions the predecessor left open:

| # | Decision | Where |
|---|---|---|
| D13 | Notifications go through a durable outbox: `EmailLog` rows are dispatched by a background worker after commit | [04](04-solution-architecture.md#notification-outbox) |
| D14 | The manage token is usable while its `Booking` is `Active` and read-only afterwards; the book token expires with its `Invite` | [06](06-security-and-authentication.md#attendee-authentication) |
| D15 | `AttendeeStatus` transitions are a closed table; no other transition is legal | [01](01-domain-model.md#attendeestatus) |

## Glossary (plain language)

| Term | Meaning |
|---|---|
| `Location` | A site where events happen: an office, a clinic or a training centre |
| `AppointmentType` | One kind of appointment run by one team, for example a medical check or an equipment fitting |
| `EventProposal` | A Manager's suggestion: "an event at this location, this date and time, for this long, offering these appointment types" |
| `ProposalAcceptance` | One Manager saying "my team can take N people at that proposed event" |
| `Event` | A fully agreed proposal. Attendees can book it |
| `EventCapacity` | How many places are left for one appointment type at one event |
| `AttendeeGroup` | A category of attendee that determines which appointment types they need |
| `Attendee` | A person being invited |
| `Invite` | An email offering an attendee a few suitable events to choose from |
| `Booking` | The attendee's confirmed choice of event |
| `BookingAppointment` | One of the attendee's appointments within that booking, tracked through check-in to completed or no-show |
| Recovery | Re-booking only the appointments an attendee missed |
