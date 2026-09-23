---
date: 2026-09-23
slug: phase-2-persistence-tasks-9a-9b-10-11
title: "Phase 2: persistence (Tasks 9a, 9b, 10, 11)"
summary: "Put D12 and D14 into code."
kind: product
status: accepted
sequence: 2026-09-23T20:03:54.000Z
evidence: "https://github.com/jamiemitchellconsultants/EventBooking/pull/47; merge commit fe29551a2989f281ad455ec6729c20c12b1f6e6a"
---

## Context

The predecessor's migration chain describes a database EventBooking never had: a three-type, single-site schema patched 34 times toward one it would never reach, and every new table would have to be added to that chain. Its attendee link was a random nonce whose hash was stored, so the server could never reproduce a link and every resend rotated it. Lock order lived implicitly in how each handler happened to be written, and eligibility loaded every active `Event` with its `EventCapacity` rows to divide in memory — some 680 ms on the performance suite's own data against the query's 18 ms.

## Decision

Put D12 and D14 into code. D12: one fresh initial migration with the design's constraints (`ck_event_capacity_bounds` with its third predicate, unique proposal per `Event`, one Manager per `AppointmentType`), seed rows in the model rather than in migration SQL, and a roles script applied before migrating that leaves the `AuditLog` append-only for the application role. D14: the link is `base64url(purpose ‖ id ‖ version ‖ HMAC)`, only `tokenVersion` on the `Invite` and `manageTokenVersion` on the `Booking` are stored, lookups are primary-key reads after a constant-time signature check, and a resend reuses the current link. Rejected: porting the chain, keeping the hash lookup, and filtering eligibility in memory. The documented lock order (`Attendee`, `EventProposal`, `Event`, `EventCapacity`) becomes a property of one helpers class with a tracker that throws on descent in Debug builds, and the eligibility port returns identifiers rather than aggregates because a query that only proposes candidates must not look like authoritative state.

## Consequences

The schema is regenerable from the model, links are reproducible without storing them, and no combination of type sets or event orders deadlocks. Scaffolding survives on purpose and is scheduled: `Invite` and `Booking` join the lock ladder in Task 15 where the real booking handler adopts the helpers and Task 7's charge and release methods are finally called; the transitional `Location` and its seeder row retire in Phase 3; `start_utc` stays shadow-mapped (CLR-nullable, column-required) so an unstamped row remains distinguishable. The Phase 1 review's location scoping survives the squash: the fresh migration creates the scoped proposal-window index directly and keeps its regression test.

---

AI-Fingerprint: sha256:16129020c412
