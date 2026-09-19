---
date: 2026-09-19
slug: add-ai-fingerprint-protocol-and-check
title: "Add AI fingerprint protocol and check"
summary: "Port the mechanism unchanged: the footer is the first 12 hex characters of the sha256 of the diff from the merge-base to the head."
kind: product
status: accepted
sequence: 2026-09-19T17:26:05.000Z
evidence: "https://github.com/jamiemitchellconsultants/EventBooking/pull/2; merge commit 24219e34c67ad86f570179534dd5971d3467fb6b"
---

## Context

JointBooking requires every pull request body to carry an `AI-Fingerprint:` footer derived from the branch diff, as friction that makes hand-typed PR bodies unattractive. It was asked for here as well. JointBooking bundles the check into a `docs-checks` job; this repository has no such job.

## Decision

Port the mechanism unchanged: the footer is the first 12 hex characters of the sha256 of the diff from the merge-base to the head. Enforce it in a standalone `ai-fingerprint` workflow rather than inventing a bundled job, and have `maintain-narrative.yml` attach the footer to Narrative proposal PRs because the external action cannot know about it. It is not cryptographically secure and there is no author exemption.

## Consequences

Every PR must recompute and update its footer after each push. The check is not yet a required status on `main`, so it is advisory until that is configured. Pull requests opened by the default workflow token may not trigger workflows at all, so proposal PRs may need a manual reopen or edit before the check reports.

AI-Fingerprint: sha256:757780df21ee
