---
date: 2026-09-20
slug: detailed-implementation-plans-phases-0-and-1
title: "Detailed implementation plans: Phases 0 and 1"
summary: "Write the plans as prototype-verified documents rather than hand-authored ones for Phases 0 to 2: implement each task in a scratch checkout, run the full suite, generate the task document from before/after snapshots, then replay that…"
kind: product
status: accepted
sequence: 2026-09-20T10:06:20.000Z
evidence: "https://github.com/jamiemitchellconsultants/EventBooking/pull/15; merge commit 883a1945394b2783a1e89517cfd60384ae33d2e0"
---

## Context

The design package and the master plan describe what EventBooking should become, but neither is
executable: a superpowers plan is deliberately code-free. The intended executor is a small local
model with no access to the predecessor repository, so anything it has to re-derive is a place the
build can silently diverge from the design. Authoring the plans also forces every ambiguity in the
design package into the open, because a document that has to compile cannot leave one unresolved.

## Decision

Write the plans as prototype-verified documents rather than hand-authored ones for Phases 0 to 2:
implement each task in a scratch checkout, run the full suite, generate the task document from
before/after snapshots, then replay that document into a second, independent checkout and run the
suite again. A document that cannot rebuild its own checkpoint is not evidence.

Several design questions were settled in the course of that and are recorded in the plans:
withdrawing an `EventProposal` is judged against `proposerAppointmentTypeId` rather than the
person, so a successor Manager inherits it; the documented lock order wins over the cancellation
sequence diagram, which is corrected here along with the matching row in the domain model; a
capacity adjustment below the active-booking count is a returned outcome carrying the minimum
rather than an exception, because FR-3.6 has to tell the Manager what it would accept; and the
`AttendeeStatus` table is closed in the aggregate as a set that callers query, so no later method
can widen it by accident.

One master-plan instruction is deliberately deferred: design 06's `tokenVersion` counter replaces
the predecessor's stored token hash in Task 9's fresh schema, not in Task 8, because the hash is
load-bearing in 62 files and Task 9 rewrites the column regardless.

## Consequences

Phases 0 and 1 are executable end to end, each with its own pull-request gate, and Phase 2 has a
green domain to write a fresh schema against. The remaining 25 tasks are not yet authored, and the
handover says so in terms that resist being read as progress.

Scaffolding survives on purpose and each piece is scheduled in the handover's transitional-constructs
table: the single-zone clock and the predecessor's fixed appointment types retire in Phase 3, the
inherited migration chain and the token hash in Task 9, and the invite's fixed option count and
transitional location in Task 14. Ten design contradictions remain open, each tagged with the task
that must resolve it; they are listed rather than settled, because settling one quietly is how a
design document stops describing the system.

---

AI-Fingerprint: sha256:d307d26d5c52

🤖 Generated with [Claude Code](https://claude.com/claude-code)
