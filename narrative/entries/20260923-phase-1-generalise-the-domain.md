---
date: 2026-09-23
slug: phase-1-generalise-the-domain
title: "Phase 1: generalise the domain"
summary: "Generalise the domain first and prove it with domain tests, before any persistence or API work."
kind: product
status: accepted
sequence: 2026-09-23T19:04:14.000Z
evidence: "https://github.com/jamiemitchellconsultants/EventBooking/pull/45; merge commit d267fad74795f164d3a9e6c9d1564bd29648a44c"
---

## Context

The ported predecessor assumed three fixed appointment types, one site, one four-hour window shape
and a lifecycle whose illegal transitions were unstated. Every later phase — persistence, handlers,
API, screens — reads those assumptions, so they have to go before anything is built on top of them.

## Decision

Generalise the domain first and prove it with domain tests, before any persistence or API work.
An `EventProposal` lists 1 to 20 appointment types and is judged by the proposing type rather than
the person (D2); an `Invite` is restricted to the `Location`s the Coordinator chose (D4); and the
`AttendeeStatus` transitions become a closed table that refuses anything design 01 does not list
(D15). Reference data becomes Admin-managed and `inviteOptionCount` becomes stored state (D3, D7).
Two rules move inward to the aggregate that owns them: a headcount adjustment reports the minimum
it would accept rather than throwing, and the attendee status stamp leaves an infrastructure
interceptor for the `Attendee` itself.

## Consequences

The domain no longer names three types, one site or one window length, and Phase 2 can write a
fresh schema against it. Scaffolding survives on purpose and is scheduled: the single-zone clock
and the transitional-location constant retire in Phase 3, the inherited migration chain and the
stored book-token hash in Task 9, and `inviteOptionCount` becomes editable in Task 12. The charge
and release methods Task 7 adds are not yet called by the booking handlers; Task 10's ordered-lock
helpers are where they are adopted.

---

AI-Fingerprint: sha256:5d39b88e6a16
