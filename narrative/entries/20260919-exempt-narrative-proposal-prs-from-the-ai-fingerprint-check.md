---
date: 2026-09-19
slug: exempt-narrative-proposal-prs-from-the-ai-fingerprint-check
title: "Exempt Narrative proposal PRs from the AI fingerprint check"
summary: "Narrative proposal PRs are exempt from the fingerprint requirement, correcting the earlier decision that they should carry one."
kind: product
status: accepted
sequence: 2026-09-19T17:31:05.000Z
evidence: "https://github.com/jamiemitchellconsultants/EventBooking/pull/6; merge commit 4db5ddfd5e9723bae7915f375a7762c0abe152b9"
---

## Context

The fingerprint protocol was applied to every PR, with a step to append the footer to Narrative proposal PRs. In practice the proposal PR's fingerprint check ran when the bot opened it, before the footer existed, and the later body edit made with the workflow token did not re-run the check. The required check therefore failed on the first proposal PR, for #2.

## Decision

Narrative proposal PRs are exempt from the fingerprint requirement, correcting the earlier decision that they should carry one. The exemption needs both the branch prefix and the bot author, because only the workflow token can author as `github-actions[bot]`, whereas anyone can name a branch. The footer-appending step is removed as pointless.

## Consequences

Proposal PRs pass the required check without a footer. Their bodies are written by the Narrative action and are not covered by the protocol; a human still reviews and merges them. Proposal PRs already open, including #4 and #5, keep their failing run until their next event, such as a close and reopen, or an admin merges past it.

AI-Fingerprint: sha256:057149eaddf9
