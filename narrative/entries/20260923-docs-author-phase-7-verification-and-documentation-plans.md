---
date: 2026-09-23
slug: docs-author-phase-7-verification-and-documentation-plans
title: "docs: author Phase 7 verification and documentation plans"
summary: "Measure capacity-lock hold time through an Application observer implemented by the API metrics service, and render cumulative histogram buckets so the release test can assert the under-50-ms p95 target."
kind: product
status: accepted
sequence: 2026-09-23T11:10:04.000Z
evidence: "https://github.com/jamiemitchellconsultants/EventBooking/pull/37; merge commit cf8553eb607748ca10809f06e78b1398087063d8"
---

## Context

Phase 7 is the last authoring phase of the EventBooking master plan. Earlier hand-authored phases have not been built into a running application, so this pull request records executable verification and documentation instructions without claiming runtime results.

## Decision

Measure capacity-lock hold time through an Application observer implemented by the API metrics service, and render cumulative histogram buckets so the release test can assert the under-50-ms p95 target. Use a guarded fixture and an isolated load-only Compose override at 600 attendee requests per IP per minute for the 500-confirmation burst. Keep the normal 30-per-minute limit and the per-token limiter unchanged. Document the resulting local demo and home-lab operating paths.

## Consequences

The future executor must build and test the application, run the 500-way burst, and walk the demo from a fresh clone before treating Phase 7 as verified. The load fixture contains private book tokens and belongs only in a disposable project. The design package now records the six seeded appointment types, the guarded fixture mode, and the emitted lock-hold metric.

AI-Fingerprint: sha256:aa13b035f064
