---
date: 2026-09-24
slug: phase-3-application-layer-tasks-12-20b
title: "Phase 3: application layer (Tasks 12–20b)"
summary: "Chose the durable outbox dispatcher with golden templates for notifications; the invite engine enforces the token lifecycle as specified in D14; and audit search resolves the caller's buckets (event, attendee) from its capabilities while…"
kind: product
status: accepted
sequence: 2026-09-24T09:39:06.000Z
evidence: "https://github.com/jamiemitchellconsultants/EventBooking/pull/49; merge commit 53bc1583408c554ec211142b5bd97f584217afd0"
---

## Context

Phase 3 turns the Phase 1 domain and Phase 2 persistence into the use cases the API and
web layers will serve. The decisions that needed settling were the notification delivery
guarantee (at-least-once outbox versus inline send), the token lifecycle the invite engine
actually enforces, and the audit visibility rule (capability buckets enforced in SQL, not
just in handlers).

## Decision

Chose the durable outbox dispatcher with golden templates for notifications; the invite
engine enforces the token lifecycle as specified in D14; and audit search resolves the
caller's buckets (event, attendee) from its capabilities while the query re-derives the
bucket from each row's entity type, so a single-bucket caller cannot reach the other
bucket by naming an entity type. Reference-data and settings rows sit in the event bucket.

## Consequences

Api and Mcp layers keep consuming the pre-existing audit port for now; migrating them to
the new bucketed port is follow-up work for their owning phases. Histories and search
paginate newest-first under the shared keyset cursor rules, and audit rows carry fixed
identifiers only — no personal data on the read side.

---

AI-Fingerprint: sha256:520f98a482f0
