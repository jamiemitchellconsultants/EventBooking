# EventBooking — Design Package

EventBooking is a general-purpose booking engine for invitation-only events. Each event takes place
at one location, runs for a fixed length of time, and offers a set of appointment types, each
delivered by a different team. Attendees are invited to choose one event whose appointments cover
everything they need, and the system guarantees that no appointment type on any event is ever
overbooked, however many attendees confirm at the same moment.

This package is the complete, self-contained design. A team should be able to build EventBooking
from it alone. It needs no access to the system it was derived from, JointBooking, a
single-organisation recruitment scheduler that is not public.

## Reading order

| # | Document | Contents |
|---|---|---|
| 00 | [Overview](00-overview.md) | Purpose, what must never change, scope, decisions, glossary |
| 01 | [Domain model](01-domain-model.md) | ERD, aggregates, lifecycles and state machines, and the invariants in narrative form. The canonical names are in [docs/ontology.md](../ontology.md) |
| 02 | [Functional requirements](02-functional-requirements.md) | EARS-style requirements per capability area (FR-1 to FR-15) |
| 03a | [Design system and information architecture](03a-design-system-and-ia.md) | Personas, tokens, component contracts, accessibility, navigation matrix |
| 03b | [Screens and flows](03b-screens-and-flows.md) | Every screen's data, actions, audit and states, with wireframes and sequence diagrams |
| 04 | [Solution architecture](04-solution-architecture.md) | Layers, projects, ports, transactions and locking, background work, outbox |
| 05 | [API design](05-api-design.md) | REST conventions, the full endpoint catalogue, MCP parity, error catalogue |
| 06 | [Security and authentication](06-security-and-authentication.md) | Keycloak staff auth, the capability matrix, attendee tokens, rate limiting, audit, data protection |
| 07 | [Deployment](07-deployment.md) | Local Docker Compose, home-lab deployment, CI/CD, images, migrations, seed |
| 08 | [Non-functional requirements](08-nonfunctional-requirements.md) | Performance, reliability, observability, accessibility, privacy, boundary values, definition of done |
| 09 | [Predecessor traceability](09-predecessor-traceability.md) | Every JointBooking feature, design and known gap, with its disposition in EventBooking |

The decision record that fixed the scope of this package is
[docs/superpowers/specs/2026-09-19-eventbooking-design.md](../superpowers/specs/2026-09-19-eventbooking-design.md).
Where it and this package differ in detail, this package wins. Where they differ on a decision, the
spec wins, and this package must be corrected.

## Conventions

- **Vocabulary.** Every backticked PascalCase term is defined in [docs/ontology.md](../ontology.md),
  which is generated from `docs/ontology.ttl`, and CI enforces it. Roles (Admin, Coordinator,
  Manager, AppointmentStaff) are values of `Role`. Use-case names such as ProposeEvent are written
  plain: they name application operations, not domain concepts.
- **Requirement IDs.** `FR-n.m` identifiers are stable. A requirement is superseded by adding a new
  ID and marking the old one withdrawn, never by renumbering.
- **Diagrams.** Diagrams are Mermaid, embedded inline so they render on GitHub and diff as text.
  Wireframes are low-fidelity ASCII: they fix information content and layout intent, not visual
  design.
- **Tags.** A requirement tagged **(carried hardening)** closes a defect found in the predecessor.
  A requirement tagged **(new)** exists only because of the generalisation. See
  [09-predecessor-traceability.md](09-predecessor-traceability.md).
