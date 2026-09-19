---
date: 2026-09-19
slug: add-path-filtered-build-and-test-workflow
title: "Add path-filtered build and test workflow"
summary: "Build and test run only on pull requests and pushes to `main`, skip when every changed file is markdown, `docs/`, `narrative/` or an agent-instruction directory, and cancel superseded runs."
kind: product
status: accepted
sequence: 2026-09-19T17:26:38.000Z
evidence: "https://github.com/jamiemitchellconsultants/EventBooking/pull/3; merge commit eebb3265130da11f13e22e4374a1a6bf7a5cc9e4"
---

## Context

JointBooking ran full build and test workflows on every commit and pull request, including documentation-only changes, and fixed it with path filtering in its build workflow. This repository has no build workflow or .NET solution yet, and it was asked to carry the same mechanism so it is in place before code arrives.

## Decision

Build and test run only on pull requests and pushes to `main`, skip when every changed file is markdown, `docs/`, `narrative/` or an agent-instruction directory, and cancel superseded runs. A `no-build-check` label makes the job report skipped for PRs that touch build paths but need no build. The job reports success while no solution file exists. `build` is deliberately not a required status check, because a path-filtered required check that never runs would leave a pull request pending forever. The .NET SDK version 10.0.x is copied from JointBooking and is an assumption for this repository.

## Consequences

Documentation-only PRs no longer pay for a build. A code change to a filtered path could in principle escape a build only if it is entirely under an ignored path, which by construction cannot affect a build. Intermediate commits on a branch go unvalidated when a newer commit cancels their run. Making `build` required later means removing the path filter first.

AI-Fingerprint: sha256:e25adb570700
