# `EventGroup` self-registration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.
>
> **This superpowers plan is deliberately code-free.** The matching [detailed implementation
> plan](../../../detailed-implementations/2026-09-25-event-group-self-registration/00-overview.md)
> contains complete tests and code fragments for a small local model.

**Goal:** Let Admins and Coordinators publish compatible `EventGroup`s so potential attendees can
choose an open Event, submit name/email/group, and confirm one ordinary Booking by email.

**Architecture:** A new `EventGroup` aggregate owns selected `AttendeeGroup`s and per-membership
Event publication. A pending `SelfRegistration` and outbox email precede an atomic confirmation
that creates or reuses the Attendee, issues a one-option Invite, and charges existing Event
Capacity. Staff and anonymous routes/clients stay separate.

**Tech Stack:** .NET 10, EF Core/PostgreSQL, ASP.NET Core minimal API, Blazor WebAssembly, xUnit,
Testcontainers, existing HMAC token and email outbox infrastructure.

**Spec:** [docs/superpowers/specs/2026-09-25-event-group-self-registration-design.md](../../specs/2026-09-25-event-group-self-registration-design.md)

## Global Constraints

- Read the spec and [canonical ontology](../../../ontology.md) before execution. Change ontology.ttl
  first and regenerate ontology.md if implementation needs a new domain term; never hand-edit the
  generated Markdown.
- The exact appointment-type rule is union of selected groups' requirements equals every member
  Event's capacity type set. Every selected group requires at least one type.
- Group and Event membership gates are independent and closed by default. An Event may belong to
  multiple `EventGroup`s. Closing either gate blocks new confirmations, not existing Bookings.
- The registration form takes name, email and `AttendeeGroup`. Submission sends email but reserves
  no capacity. Confirmation tokens last 24 hours; terminal personal data is retained 30 days.
- Admin and Coordinator alone get `ManageEventGroups`. Admin sees no attendee data. Admin alone
  edits `AttendeeGroup` reference data.
- Keep audit and telemetry free of title, description, name, email and raw token. Public endpoints
  never disclose whether an email belongs to an existing Attendee.
- Lock `EventGroup` before its gates and mapping reads, then preserve the established Attendee →
  Invite → Event → sorted `EventCapacity` lock order and the
  database's no-overbooking backstop. Use the durable outbox for both registration and booking mail.
- Follow AGENTS.md: one commit and push per detailed task on a feature branch, review the staged
  diff and run the ontology term checker before each commit, and use a pull request for main.

## Review Focus

1. A group's requirements change after an Event is added: the edit must be rejected if the exact
   union would change, even if the Event membership is private (Tasks 2–3).
2. An Event belongs to two groups with different publication flags: closing one must leave the
   other group's public listing intact (Tasks 2, 4, 6).
3. Two confirmations race for the last place: one Booking succeeds and capacity never becomes
   negative (Task 7).
4. An email belongs to an existing Attendee with another group or an active Booking: public
   submission remains neutral, while confirmation gives a conflict without changing that record
   (Tasks 6–7).
5. A gate closes or an Event starts after email is sent: confirmation refuses it without creating
   an Attendee or charging capacity (Task 7).

## File structure and ownership

| Area | Responsibility |
|---|---|
| Domain/AttendeeGroups and reference-data API/Web | Description and edit/list contract |
| Domain/EventGroups | Publication aggregate, selected groups, Event memberships, compatibility |
| Application/EventGroups and persistence | Staff commands/queries, capability, mapping-drift guard |
| Api/Endpoints/EventGroupEndpoints and Web/Pages/EventGroups | Staff management and share URL |
| Domain/SelfRegistrations and infrastructure | Pending request, token purpose, persistence and email outbox |
| Application/SelfRegistrations and Api/Endpoints/SelfRegistrationEndpoints | Public listing, submission and transactional confirmation |
| Web/Pages/PublicEventGroup and confirmation page | Anonymous list/form/confirm experience |
| Jobs, tests and docs/design | Expiry/retention, regression and documentation updates |

## Task 1: `AttendeeGroup` description

**Behaviour.** Add optional description (up to 500 characters, empty for existing rows) through
domain, persistence, staff request/response and Admin editor. Public consumers can read it only
when an `EventGroup` lists that `AttendeeGroup`.

**Tests to write first.** Domain rejects overlong description; create/update/list round-trip it;
the Admin form preserves it through a version conflict. Run focused domain/API/web tests.

## Task 2: `EventGroup` compatibility domain

**Behaviour.** Define `EventGroup`, `EventGroupAttendeeGroup` and `EventGroupEvent` with closed
defaults, optimistic version, duplicate checks, exact-union validation and independent gates.
The Event aggregate and negotiation path stay unchanged.

**Tests to write first.** Multiple groups with different requirement subsets form an exact union;
Event with missing/extra type is rejected; one Event can join two groups with independent gates;
blank/overlong copy and duplicate memberships are rejected.

## Task 3: `EventGroup` persistence and reference-data guard

**Behaviour.** Map the aggregate and join rows, add a migration and repository, enforce unique
memberships and version checks. Before an `AttendeeGroup` requirement edit or deactivation, refuse
changes that break any `EventGroup` containing it. Serialize that check against `EventGroup` edits.

**Tests to write first.** PostgreSQL round-trip; unique membership; mapping drift rejected for
private and open Event memberships; safe set-equivalent edits accepted; deactivation refused while
listed.

## Task 4: Staff management API

**Behaviour.** Add `ManageEventGroups` to the capability matrix for Admin/Coordinator. Create
staff commands and list/detail, create/update, membership add/remove and gate toggles under
/api/event-groups. Expose operation metadata, audit only safe IDs/states, and return 409 on version
or compatibility conflicts.

**Tests to write first.** Both allowed roles succeed; Manager/AppointmentStaff fail; Admin result
has no attendee data; incompatible/started Event fails; independent gates work; stale version
returns 409.

## Task 5: Staff `EventGroup`s screen

**Behaviour.** Add a staff navigation entry and screen to edit title, description, selected
`AttendeeGroup`s, member Events and each gate. Show compatibility explanations and public URL.
Preserve form values after version conflicts.

**Tests to write first.** Client request paths/bodies, role link visibility, create and edit form
states, conflict handling and no attendee fields in Admin view.

## Task 6: Pending registration and public discovery/submission

**Behaviour.** Persist `SelfRegistration`; add a purpose-scoped HMAC token and `SelfRegistration`
Confirmation email outbox template. Provide public group/event reads and a rate-limited submission
route. Submission validates copy and current eligibility, stores a pending request, and enqueues
mail in one transaction without taking capacity. It responds neutrally for existing addresses.

**Tests to write first.** Private/unknown resources share 404 shape; open membership in another
group remains visible; name/email/group validation; full group returns 409; successful submission
leaves capacity unchanged and produces one durable email; token/body are absent from the database
and logs.

## Task 7: Atomic email confirmation

**Behaviour.** Confirm a valid pending token once. Under the established lock order, recheck
publication, group mapping, Event time/status and capacity; create/reuse Attendee, supersede an
old pending initial Invite if needed, create a one-option Invite, Booking and appointments, and
enqueue `BookingConfirmation` in the same transaction. Expose a rate-limited confirm endpoint.

**Tests to write first.** Last-place race, replay, expired token, gate closed after submission,
Event started/cancelled, existing email with mismatched group or active Booking, and no partial
records after a failed confirmation.

## Task 8: Anonymous web flow

**Behaviour.** Add a public group list page, event registration form, and confirmation page using
the anonymous HTTP client and attendee layout. Display group descriptions, local Event times,
availability by group and neutral submission messaging; handle closed/full/expired states accessibly.

**Tests to write first.** Client contract tests and rendered component states for 404, empty
group, full group, form errors, check-email state, expired token and success.

## Task 9: Maintenance and integration

**Behaviour.** Expire pending requests, delete terminal requests and related email rows after 30
days, run cross-layer API/PostgreSQL and web smoke tests, and update current design/operator docs
to describe the new acquisition path without rewriting accepted narrative history.

**Tests to write first.** Sweep does not remove live requests; terminal rows and related mail are
removed after the retention boundary; end-to-end submit→email→confirm yields one Booking and
the normal manage link; ontology and build gates pass.

## Execution handoff

Execute tasks 1–9 in order using the matching numbered detailed files. Each task ends with its
own reviewed commit and push. After the final task, open a pull request with the required
Narrative sections, decision-bearing label, and current AI fingerprint.
