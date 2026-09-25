---
date: 2026-09-25
slug: verify-and-document-eventbooking-release
title: "Verify and document EventBooking release"
summary: "- The booking handler reports its capacity-lock hold, from the event row lock through transaction release, through an Application timing port implemented by the existing API metrics service."
kind: product
status: accepted
sequence: 2026-09-25T07:05:38.000Z
evidence: "https://github.com/jamiemitchellconsultants/EventBooking/pull/58; merge commit a25de6a6eebc34b75839d6a1095b318d201515a1"
---

## Context

The release gate needed proof that 500 simultaneous confirmations against one 100-place event produce exactly 100 bookings and 400 capacity-exhausted refusals without 5xx responses, deadlocks or lock-hold regressions, plus reproducible first-run and home-lab instructions. The first burst failed with 362 5xx responses; PostgreSQL reported its connection slots exhausted. An attendee confirmation holds one connection, but the API opened Npgsql's default pool of up to 100 against PostgreSQL's default limit of 100, which also serves reserved slots and the MCP role: design 08's 20-per-replica pool default had never been applied.

## Decision

- The booking handler reports its capacity-lock hold, from the event row lock through transaction release, through an Application timing port implemented by the existing API metrics service. It is rendered as cumulative histogram buckets with a bound just under 50 ms, set on that instrument alone, so the k6 script asserts the percentile from this run's before-and-after delta rather than a process-lifetime aggregate.
- Every stack applies design 08's Npgsql pool default of 20 per replica unless the connection string names a size, so a burst queues for a pooled connection instead of exhausting PostgreSQL. PostgreSQL keeps its default connection limit everywhere, including the load project.
- The burst runs in an isolated disposable Compose project whose load-only override raises the attendee per-IP allowance to 600/min. Normal local and home-lab stacks keep the 30/min limit; per-token limits stay enabled everywhere.
- Rejected: loosening any gate threshold, rate limit or fixture shape to make the burst pass; raising PostgreSQL's connection limit on the load rig only, which hid the same failure on normal stacks; routing fixture invitations through the mail dispatcher.

## Consequences

- Each release runs the burst before publishing; a red gate blocks the release, not a pull request.
- Operators running several API replicas size PostgreSQL's connection limit from design 08's replica count × 20 + 10; a connection string can still name its own pool size.
- The demo runbook was walked headlessly over the API from a fresh clone; browser rendering and a live check-in transition remain unobserved by design (future-dated seed events).

---

AI-Fingerprint: sha256:9f9491fefdaf

🤖 Generated with [Claude Code](https://claude.com/claude-code)
