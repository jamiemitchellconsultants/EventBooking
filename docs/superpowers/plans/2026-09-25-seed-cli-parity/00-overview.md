# Seed CLI parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.
>
> **This plan is deliberately code-free.** Each task states the behaviour, the contracts other
> tasks rely on, and the exact test cases to write first. The matching detailed implementation plan
> in [docs/detailed-implementations/2026-09-25-seed-cli-parity/](../../../detailed-implementations/2026-09-25-seed-cli-parity/00-overview.md)
> carries the code.

**Goal:** Give the EventBooking seed host the same operator-facing switches as JointBooking's:
`--reanchor` with an optional date, step-level `--verbose` progress with full exceptions on
failure, a reanchor confirmation line, Keycloak summary counts and complete `--help` usage text.

**Architecture:** All changes live in the existing SeedData host. Option parsing stays in
SeedCliOptions, orchestration and its progress lines in SeedCommand, the one interface change
(reanchor takes an optional date and returns the date used) in ISeedRunSteps and its production
implementation SeedRunSteps. Usage text and failure formatting move into a new small file so they
are testable without a process.

**Tech Stack:** .NET 10 (`net10.0`), xUnit. No database is needed by any new test.

**Spec:** [docs/superpowers/specs/2026-09-25-seed-cli-parity-design.md](../../specs/2026-09-25-seed-cli-parity-design.md).

## Global Constraints

- Follow AGENTS.md: work on a branch, open a pull request, do not push to main.
- `--skip-seed` is deliberately not added (spec, Non-goals). Migrate-only stays "no `--demo`".
- Normal (non-verbose) output must not gain lines other than the two the spec lists as always
  printed: the Keycloak counts line and the reanchor confirmation line.
- Domain-term rule: do not backtick code identifiers in Markdown; the ontology term check flags them.

## File Structure

| File | Responsibility |
|---|---|
| src/EventBooking.SeedData/SeedCommand.cs | Options record and parser; command orchestration; the steps interface |
| src/EventBooking.SeedData/SeedRunSteps.cs | Production steps; reanchor now takes an optional date |
| src/EventBooking.SeedData/SeedUsage.cs (new) | Usage text and failure formatting |
| src/EventBooking.SeedData/Program.cs | Entry point: help, run, failure reporting |
| tests/EventBooking.SeedData.Tests/SeedCommandTests.cs | Parser and command tests; recording steps double |
| tests/EventBooking.SeedData.Tests/SeedUsageTests.cs (new) | Usage and failure-report tests |
| deploy/home-lab/README.md | Operator description of the switches |

## Task 1: Optional reanchor date in the parser

**Behaviour.** SeedCliOptions gains ReanchorDate (a nullable date). The token directly after
--reanchor is consumed as the date when it has the shape yyyy-MM-dd; it must then be a real
calendar date. A consumed date is never counted as a connection string. Bare --reanchor leaves
ReanchorDate null. --reanchor still requires --demo.

**Tests to write first.**
- A date after --reanchor is captured and does not trip the one-connection-string rule.
- The date works before and after the connection string on the command line.
- 2026-02-30 and 2026-13-01 are rejected with the documented message; a token with the wrong
  shape (for example 25/09/2026) is treated as a stray positional and rejected by the existing
  one-connection-string rule.
- Bare --reanchor gives a null date; --reanchor with a date but no --demo is rejected.

## Task 2: Verbose steps, reanchor date, Keycloak counts

**Behaviour.** ISeedRunSteps replaces ReanchorToTodayAsync with ReanchorAsync taking the optional
date and returning the date it applied (today in Europe/London when none was given).
SeedRunSteps implements it. SeedCommand prints the reanchor confirmation line always, prints the
Keycloak counts line whenever a summary comes back, and writes the spec's [seed] step lines to its
output writer only when Verbose is set.

**Tests to write first** (against the recording steps double, which is updated to the new
signature and records the date it was given):
- The reanchor step receives the requested date, and the confirmation line shows the date the
  step returned rather than the requested one.
- Verbose output contains the step lines in the spec's order; non-verbose output contains none of
  them, and contains neither the Keycloak line (when no summary) nor any reanchor line (when not
  requested).
- A returned Keycloak summary prints the four counts.
- The existing call-order tests keep passing unchanged apart from the renamed reanchor call.

## Task 3: Usage text, help and failure reporting

**Behaviour.** New SeedUsage.Text documents every switch and environment variable named in the
spec. New SeedFailureReport formats a failure: message only by default, the full exception when
--verbose is among the raw arguments (even if parsing failed). Program prints usage and exits 0
for --help or -h before parsing anything; on an option-parse failure it prints the report followed
by the usage text and exits 2; on a runtime failure it prints the report only and exits 2.

**Tests to write first.**
- The usage text mentions every switch and the two --reseed and --load-fixture environment guards.
- The failure report is message-only without --verbose and contains the exception type name and
  stack frames with it, including for an option-parse failure.
- A help-request detector recognises --help and -h and nothing else.

## Task 4: Operator documentation

**Behaviour.** deploy/home-lab/README.md gains a short "Seed switches" section that lists the
switches, states the --demo versus --skip-seed inversion relative to JointBooking, and shows the
reanchor-with-date invocation. No ontology change is needed.

**Verification.** The ontology term check passes on the staged files.
