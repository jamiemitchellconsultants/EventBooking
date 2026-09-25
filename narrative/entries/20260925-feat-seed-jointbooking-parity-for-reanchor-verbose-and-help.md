---
date: 2026-09-25
slug: feat-seed-jointbooking-parity-for-reanchor-verbose-and-help
title: "feat(seed): JointBooking parity for --reanchor, --verbose and --help"
summary: "Add the missing switches, but not `--skip-seed`: the original EventBooking design deliberately made migrate-only the default with `--demo` as the opt-in, and reversing that is out of scope."
kind: product
status: accepted
sequence: 2026-09-25T08:12:57.000Z
evidence: "https://github.com/jamiemitchellconsultants/EventBooking/pull/61; merge commit 2d6eb6d8a8d0e1e9b349e92eec3dee82b40357dd"
---

## Context

Operators and the LocalAI installer drive both JointBooking's and EventBooking's seed hosts, but EventBooking's exposed a thinner command line: no reanchor date, no step-level progress, message-only failures and one line of usage.

## Decision

Add the missing switches, but not `--skip-seed`: the original EventBooking design deliberately made migrate-only the default with `--demo` as the opt-in, and reversing that is out of scope. A malformed `--reanchor` date fails the run instead of silently falling back to today as JointBooking does, because that fallback leaves demo dates stale with no signal.

## Consequences

One mental model across both repositories apart from the documented `--demo` / `--skip-seed` inversion. The steps interface loses its reanchor-to-today member in favour of one taking an optional date and returning the date applied. The LocalAI installer can gain matching reanchor and verbose switches in a follow-up.

---

AI-Fingerprint: sha256:698e9e4e0ff7

🤖 Generated with [Claude Code](https://claude.com/claude-code)
