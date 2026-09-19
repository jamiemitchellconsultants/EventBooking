---
date: 2026-09-19
slug: docs-agents-add-detailed-implementation-plans-section
title: "docs(agents): add detailed implementation plans section"
summary: "Adopt JointBooking's convention as is. Each superpowers plan gets a matching detailed implementation plan under `docs/detailed-implementations/<slug>/`."
kind: product
status: accepted
sequence: 2026-09-19T19:47:38.000Z
evidence: "https://github.com/jamiemitchellconsultants/EventBooking/pull/13; merge commit 851072337a7ae9f52fee06a1a7aabda3e0aa5aff"
---

## Context

Superpowers plans under `docs/superpowers/plans/` are code-free by design. They are input for spec-driven development and can't be handed straight to an execution agent, least of all a small local model. JointBooking already uses a convention for expanding them into executable, task-level plans, and EventBooking had nothing equivalent.

## Decision

Adopt JointBooking's convention as is. Each superpowers plan gets a matching detailed implementation plan under `docs/detailed-implementations/<slug>/`. It is split into numbered TDD-shaped tasks, each with Files, Interfaces and a complete failing test first, and each task ends with its own commit and push. The target is plans that opencode + superpowers + qwen3.6 27b q6 can execute without re-deriving design decisions.

## Consequences

Planning becomes a two-stage process: a superpowers plan, then a detailed implementation plan. Detailed plans are plan documents under the ontology protocol, so they must use canonical terms. Tasks are numbered continuously across a slug's files. The instructions now match JointBooking's except for the corrected ontology link path. JointBooking still has the broken `../ontology.md` link.

---

AI-Fingerprint: sha256:a0d7fcf8ef56

🤖 Generated with [Claude Code](https://claude.com/claude-code)
