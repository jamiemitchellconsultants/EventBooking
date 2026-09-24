---
date: 2026-09-24
slug: phase-5-web-front-end-tasks-24-27
title: "Phase 5: web front end (Tasks 24–27)"
summary: "The web project stays a standalone WASM client that references no server assembly. Its wire DTOs duplicate the JSON contract and are checked against the served OpenAPI document."
kind: product
status: accepted
sequence: 2026-09-24T19:06:13.000Z
evidence: "https://github.com/jamiemitchellconsultants/EventBooking/pull/54; merge commit 1e39f8efe9807399ba8ace5dfcc5b181788f874a"
---

## Context

Phase 4 delivered the design-05 endpoint catalogue and MCP parity, but the web project was still the branded port, reading routes and shapes that no longer existed. The Phase 5 plans were hand-authored before Phase 4 was executed, and they required data the client is forbidden to invent — listed proposal types, workspace appointment identifiers and action links, attendee-facing location and time details, and an affordance for create, import and propose controls when a list is empty — so the web work could not start until the contract carried it.

## Decision

The web project stays a standalone WASM client that references no server assembly. Its wire DTOs duplicate the JSON contract and are checked against the served OpenAPI document. Material calls:

- Missing contract data was repaired in the API in one focused prerequisite commit ahead of any page, rather than letting the client compose it or fall back to role checks. Staff controls appear only when the resource's `_links` offer them; roles drive navigation and Help-guide selection only.
- Failures are read once, as RFC 9457 problem documents, and pages branch on the `type` slug, never on title or detail prose. A 403 is a forbidden state; only a 401 may send a staff user back through sign-in.
- Event times are rendered from the API's complete event-time representation; the client never converts a time zone. The one calculation is the unsaved proposal preview.
- Create forms use one Idempotency-Key per user submission, reused across retries.
- The Playwright/axe harness moved from Task 27 to Task 24, so every route family was rendered and scanned as it landed rather than in one gate at the end.

## Consequences

- Re-skinning is a theme-file change: no colour literal exists outside `wwwroot/theme.css`, and colour never carries meaning on its own.
- The client is coupled to the contract only through the OpenAPI document; a server change that drops a field the web uses fails the contract suite, not a page at runtime.
- Every route is held to zero axe-core violations at mobile and desktop widths in CI, including empty, forbidden, conflicted and loading states.
- The mistaken direct push to `main` meant this phase briefly reached `main` without a narrative entry; rewinding `main` and merging through this PR restores the review-first record.

---

AI-Fingerprint: sha256:154063e6bccb

🤖 Generated with [Claude Code](https://claude.com/claude-code)
