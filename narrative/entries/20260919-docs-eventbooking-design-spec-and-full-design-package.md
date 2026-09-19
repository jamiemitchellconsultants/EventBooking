---
date: 2026-09-19
slug: docs-eventbooking-design-spec-and-full-design-package
title: "docs: EventBooking design spec and full design package"
summary: "EventBooking ports JointBooking's .NET solution and generalises it (D5). A clean rebuild from JointBooking's redesign docs, and generalising JointBooking in place, were both rejected."
kind: product
status: accepted
sequence: 2026-09-19T19:04:32.000Z
evidence: "https://github.com/jamiemitchellconsultants/EventBooking/pull/8; merge commit 3ef3888f0a9c83daa2a24c29129b37536c8b27c4"
---

## Context

JointBooking coordinates recruitment candidates across three fixed appointment types, negotiated
by three fixed managers, in 4-hour windows at one head office, and deploys to AWS. We wanted a
general-purpose, public product for invitation-only events that keeps JointBooking's two core
guarantees: no overbooking under concurrency, and bookability only after every responsible team
agrees. It also needed four generalisations:

- many locations, each with its own time zone;
- variable-length events;
- any number of Admin-managed appointment types;
- self-hosted deployment only.

## Decision

EventBooking ports JointBooking's .NET solution and generalises it (D5). A clean rebuild from
JointBooking's redesign docs, and generalising JointBooking in place, were both rejected.

- **Negotiation.** An `EventProposal` lists its own `AppointmentType`s. Each listed type's single
  Manager, one per type across all locations (D1), accepts with a headcount, and the proposal
  confirms once every listed type has accepted. Negotiation is the only source of events; bulk
  import is dropped (D6).
- **Requirements.** An `Attendee`'s requirements derive from an Admin-managed `AttendeeGroup` (D3).
- **Invites.** The Coordinator selects the eligible `Location`s per `Invite` (D4), and the number
  of options is configurable (D7).
- **Deployment.** AWS, Terraform, the Entra ID adapter and MinIO are dropped in favour of local
  Docker Compose and the home lab with Keycloak (D8, D9).
- **Questions settled here that JointBooking left open:**
  - a durable email outbox (D13);
  - deterministic, versioned attendee tokens (D14);
  - a closed `AttendeeStatus` transition table (D15);
  - fixed boundary values.

## Consequences

- The domain model is broader:
  - capacity locking now needs a canonical lock order;
  - invite selection becomes a relational-division query;
  - every time rule is evaluated in the location's zone, and proposals falling on a DST gap or
    overlap are rejected.
- Two JointBooking behaviours change: an Admin may clear a type's only Manager, and the seed tool
  migrates only unless `--demo` is passed.
- Deliberately out of scope:
  - multi-tenancy;
  - per-location managers;
  - reminders before an upcoming booking;
  - historical reporting;
  - milestone-relative scheduling;
  - any cloud target.
- The design package is maintained alongside the code under its definition of done. The
  implementation plan comes next.

---

AI-Fingerprint: sha256:89469b5a9146

🤖 Generated with [Claude Code](https://claude.com/claude-code)
