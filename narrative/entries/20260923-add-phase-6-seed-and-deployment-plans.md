---
date: 2026-09-23
slug: add-phase-6-seed-and-deployment-plans
title: "Add Phase 6 seed and deployment plans"
summary: "Add LAB as a sixth active, managed appointment type and keep live events and proposals on MED, FIT, IND, and LAB. Keep ESC active but unmanaged and DOC inactive."
kind: product
status: accepted
sequence: 2026-09-23T09:06:36.000Z
evidence: "https://github.com/jamiemitchellconsultants/EventBooking/pull/34; merge commit 1387b15fa359eac1af1ae7e247e8e16cc1efbbcd"
---

## Context

Phase 6 needed to turn the seed and deployment requirements into self-sufficient plans for a smaller execution model. The original five-type demo dataset could not satisfy four-type live scenarios while keeping ESC unmanaged and DOC inactive. The existing single-zone clock also remained due for retirement.

## Decision

Add LAB as a sixth active, managed appointment type and keep live events and proposals on MED, FIT, IND, and LAB. Keep ESC active but unmanaged and DOC inactive. Retire the single-zone clock in Task 28, and retain the master boundaries for local Compose, home-lab deployment, and artifact publication.

## Consequences

Task 28 can cover all required demo axes without weakening negative-reference cases. Tasks 29–31 specify non-root images, local and isolated home-lab topologies, recovery rehearsal, pinned Actions, and the migrations bundle. Phase 6 remains hand-authored and unexecuted; execution must supply real build, test, and rehearsal evidence.

---

AI-Fingerprint: sha256:28ffbff3b63e
