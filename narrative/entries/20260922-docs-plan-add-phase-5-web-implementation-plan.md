---
date: 2026-09-22
slug: docs-plan-add-phase-5-web-implementation-plan
title: "docs(plan): add Phase 5 web implementation plan"
summary: "Hand-author Phase 5, but move the Playwright project, axe integration, two-viewport route/state manifest, API stub, and Web-change workflow job from the phase-ending task into Task 24."
kind: product
status: accepted
sequence: 2026-09-22T14:53:27.000Z
evidence: "https://github.com/jamiemitchellconsultants/EventBooking/pull/31; merge commit 8d014681af20ed07e3b8658f031c3accfaa2be24"
---

## Context

Phase 5 needs to turn the approved Web design package and the Phase 4 API surface into instructions detailed enough for a small local model to execute without re-deriving the design. The earlier executable prototype stops at Task 11, while Tasks 12–23 are hand-authored and have not been executed. Advancing the prototype through those twelve tasks only to generate Web snapshots would add substantial cost. However, Web failures are visual and interactive, so source-level review alone is not a sufficient safety net.

## Decision

Hand-author Phase 5, but move the Playwright project, axe integration, two-viewport route/state manifest, API stub, and Web-change workflow job from the phase-ending task into Task 24. Tasks 25–27 must extend that manifest, and Task 27 remains the complete phase gate.

Keep the standalone Web project server-reference-free. Where the OpenAPI contract lacks required fields or action links, the executor must stop and repair the server contract in a prerequisite commit instead of inferring permissions, event state, or display data in the browser.

## Consequences

An executor can implement Tasks 24–27 as four TDD-shaped commits with browser and accessibility coverage present from the first task. The earlier safety net increases Task 24's scope, but prevents visual and accessibility debt from accumulating until Task 27. The plans make no claim that Phase 5 builds or passes yet; observed counts belong to the future execution. Phases 6–7, Tasks 28–33, remain to be authored.
