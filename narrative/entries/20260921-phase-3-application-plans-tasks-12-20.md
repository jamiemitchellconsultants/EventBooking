---
date: 2026-09-21
slug: phase-3-application-plans-tasks-12-20
title: "Phase 3 application plans (Tasks 12–20)"
summary: "Author Tasks 12-20 as hand-authored plan documents (phase-3a through 3j, with Task 20 split into 20a/20b), settling: Task 12 owns reference data and settings only with attendee work deferred to Task 20 splits (#5); invites snapshot the…"
kind: product
status: accepted
sequence: 2026-09-21T05:01:50.000Z
evidence: "https://github.com/jamiemitchellconsultants/EventBooking/pull/20; merge commit c72867f58446f360942946b7a5f321d7b18b1022"
---

## Context

Phase 3 (master Tasks 12-20) generalises every use case behind the design 04 handler pattern: one transaction per command, canonical lock order, audit in the transaction, one StaffCapability per handler. Phases 0-2 were prototype-verified; Phase 3 is hand-authored — complete code and tests written straight into the documents for the executing model to compile and test-drive. Seven open contradictions between the spec, design package and master plan landed on these tasks and were settled with the user rather than alone.

## Decision

Author Tasks 12-20 as hand-authored plan documents (phase-3a through 3j, with Task 20 split into 20a/20b), settling: Task 12 owns reference data and settings only with attendee work deferred to Task 20 splits (#5); invites snapshot the three settings values at issue (#9, new ontology properties); replayed confirmations refuse as conflicts naming the existing booking, against the master plan's return-the-booking test (#2); recovery demands ManageAttendees while workspace stays under ConductAppointments (#4); missing staff identity is 403 except on /api/me (#3); EmailLog gains claim, backoff and correlation columns (#6); delivery is at-least-once with the crash window stated (#7). D13 (outbox), D14 (token lifecycle as implemented) and D15 go into code here.

## Consequences

The executor works Tasks 12-20 in order on the phase branch pattern, with Task 20b as the merge gate. Test counts in the documents are expectations, not observed figures — nothing was run. Phases 4-7 (Tasks 21-33) remain unauthored. The refuse-as-conflict replay semantic permanently overrides the master plan's idempotent-return test for Task 15.

---

AI-Fingerprint: sha256:802571da4763
