---
date: 2026-09-20
slug: phase-2-the-fresh-schema-ordered-locks-and-the-eligibility-query
title: "Phase 2: the fresh schema, ordered locks and the eligibility query"
summary: "Give the generalised domain a persistence layer of its own, and prove each claim against a real PostgreSQL 16 rather than an in-memory provider."
kind: product
status: accepted
sequence: 2026-09-20T15:18:57.000Z
evidence: "https://github.com/jamiemitchellconsultants/EventBooking/pull/17; merge commit a0f3de1ac11bcde387f6c6cd56631fadfd2de51b"
---

## Context

Phase 1 generalised the domain but left it sitting on the predecessor's schema: an inherited
migration chain written for three fixed appointment types and one site, a book token whose raw value
the server had thrown away, row locks taken in whatever order each handler happened to be written
in, and an event selection rule that loaded every active `Event` into memory to filter it. Every
later phase reads that persistence layer, so it had to be settled before handlers, API or screens
are built on top of it.

## Decision

Give the generalised domain a persistence layer of its own, and prove each claim against a real
PostgreSQL 16 rather than an in-memory provider.

A fresh initial migration, with no data migration from the predecessor (D12); seed rows live in the
model so that migration stays regenerable. The attendee token becomes derived rather than stored, so
a resend reuses the current link instead of rotating it (D14). The documented lock order becomes a
property of one class that every row lock passes through, which is what let handlers written in
earlier phases gain the guard without being touched. And event selection becomes one relational-
division SQL statement behind an Application port that returns identifiers, not aggregates —
because a query that only proposes candidates must not look like a read of authoritative state.

Three alternatives were rejected on evidence rather than taste. Rewriting the committed initial
migration to carry Task 11's not-null column was rejected: a chain is what keeps that migration
regenerable, so Phase 2 ends with two. Leaving `start_utc` to a single writer was rejected once it
was clear the demo seeder and some thirty test sites insert `Event`s straight through the context;
the repository writes it, and the context fills in anything unstamped at save time, both through one
function. And the master plan's own performance scenario was rejected as written: two thousand
active events and nothing else is a table where every row qualifies, so PostgreSQL is right to scan
it sequentially and the index the scenario asks EXPLAIN to prove is never used.

## Consequences

Phase 3 can write handlers against a schema, a lock discipline and an eligibility port that are all
settled. Two claims in this phase are narrower than they look, and both are recorded where the next
author will meet them. The master plan's two-event deadlock test passes whether or not capacity rows
are ordered, because the event locks serialise the attempts first; the scenario that pins the
ordering is the same shuffle with those locks removed. And the eligibility suite's EXPLAIN assertion
and its p95 budget are not two ways of saying the same thing — dropping the index fails the first
and not the second, because at this size the table scan is not where the time goes. What the budget
catches is the in-memory shape Task 11 replaced, at some 680 ms against its 50 ms. Each was verified
by making it fail.

Scaffolding survives on purpose and stays scheduled: the single-zone clock and the transitional
`Location` constant retire in Phase 3, `inviteOptionCount` becomes editable in Task 12, and invites
stop being restricted to one `Location` in Task 14. Task 15 inherits three debts at once — the
booking handler adopting the lock helpers, the lock ladder gaining its `Invite` and `Booking`
levels, and Task 7's charge and release methods finally being called. Master Tasks 12 to 33 are
still unwritten, and are hand-authored rather than prototype-verified.

---

AI-Fingerprint: sha256:7d8bd9b672e7
