---
date: 2026-09-19
slug: docs-plan-add-the-eventbooking-implementation-plan
title: "docs(plan): add the EventBooking implementation plan"
summary: "Deliver in eight phases, one pull request each, following the spec's porting sequence: first port JointBooking under EventBooking names with AWS removed and the build green, then generalise the domain, persistence, application, API/MCP…"
kind: product
status: accepted
sequence: 2026-09-19T19:19:02.000Z
evidence: "https://github.com/jamiemitchellconsultants/EventBooking/pull/10; merge commit 3def28e95c9181b502e7a72100d841f6f81ac18b"
---

## Context

The design package fixes what EventBooking must do, but not the order of work, how the port from JointBooking is staged, or how to resolve two detail differences between the spec and the design package (demo attendee groups, and seed flags).

## Decision

Deliver in eight phases, one pull request each, following the spec's porting sequence: first port JointBooking under EventBooking names with AWS removed and the build green, then generalise the domain, persistence, application, API/MCP and web in that order, then seed and deployment, then load test and docs. The plan is code-free: it specifies behaviour, contracts and tests, and leaves the code to the implementer. Where the spec and design package differ in detail, the design package wins (4 demo groups; migrate-only seed by default with `--demo`), as its README requires.

Rejected: one plan per subsystem (cross-references between phases would be lost); a clean rebuild ordering (contradicts D5).

## Consequences

Implementation can start at Task 1, using subagent-driven or inline execution. Each phase PR states whether it makes a decision. The plan is a living document: if a task changes a documented rule, the design package is updated in the same PR.

---

AI-Fingerprint: sha256:5da7b6ed8fea

🤖 Generated with [Claude Code](https://claude.com/claude-code)
