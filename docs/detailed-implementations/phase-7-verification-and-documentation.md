# Phase 7 — Verification and documentation (Tasks 32–33)

[← Plans overview](README.md) · [Phase 6](phase-6-seed-and-deployment.md) · [Master plan](../superpowers/plans/2026-09-19-eventbooking-implementation.md) · [Ontology](../ontology.md)

> Use superpowers:executing-plans. Execute the two task documents in order. Each task ends with its
> own commit and push on the same phase branch.

**Status: complete as a plan; not executed.** Phase 7 is hand-authored. The load script, fixture,
instrumentation, tests and runbooks are instructions for a future executor. No Phase 7 code has
been built, no 500-way burst has run, and no fresh-clone walkthrough has been observed. The last
measured checkpoint is Task 11 at 1,570 tests. Neither Task 32 nor Task 33 adds a measured row to
the authoring handover's evidence table.

**Goal:** Prove the 500-confirmation capacity race and provide a reproducible developer demo and
home-lab operating guide.

**Architecture:** Task 32 creates a dedicated, guarded seed fixture and a one-shot k6 burst against
the local Compose API. Booking confirmation records lock-hold duration through an Application port
into API metrics. Task 33 writes the application entry point, demo walkthrough and final operator
runbook, then reconciles design-package wording with the implemented seed and metric contract.

**Tech Stack:** .NET 10, EF Core 10, PostgreSQL 16, Docker Compose, k6, JavaScript, xUnit and
Markdown.

**Spec:** [Master Tasks 32–33](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[verification](../design/08-nonfunctional-requirements.md#verification),
[deployment](../design/07-deployment.md) and [ontology](../ontology.md).

## Global constraints

- The local API is `http://localhost:5001`; the home-lab topology is outside the load test.
- A dedicated fixture owns one future event with 100 places for a shared type and exactly 500
  pending invitations that offer it. General demo events do not supply this capacity.
- A normal local stack keeps the 30-per-minute attendee address limit. The load-only override
  changes that limit only for this scenario; token limits remain at 10 per minute per token.
- The burst asserts exactly 100 created bookings, exactly 400 `capacity-exhausted` responses,
  zero 5xx, zero deadlocks and the NFR-P3 lock-hold bound.
- The fixture file contains book tokens and is ignored by Git. Keep it and any request-level
  diagnostics private; the k6 request metric uses a route-pattern name, without a token label.
- Task 33 documents the actual Task 29 commands. No runbook step claims an unobserved result.
- Phase 7 has no build, test, stack, load, or fresh-clone result until the executor runs it.

## Review focus

1. The fixture produces 500 distinct pending book tokens, all with the target event as an option,
   without charging any of its 100 places before the burst.
2. The load-only address-limit override cannot change a normal local or home-lab deployment.
3. The metric counts one interval per confirmation that acquired capacity locks; the k6 assertion
   reads the interval for this run, rather than a stale process lifetime aggregate.
4. The k6 result fails on a 429, an unexpected 4xx, a missing metric, a 5xx, a wrong outcome count
   or a PostgreSQL deadlock, even when other thresholds pass.
5. Demo instructions use Task 28's actual users and future-dated events; steps that require an
   event's local date are not presented as immediately runnable on a fresh clone.

## Before you start

Start from the latest `origin/main` after Phase 6 has merged. Pull request #34 and its narrative
proposal #35 merged on 23 September 2026. Confirm both on GitHub, then create a Phase 7 branch.
Run the full solution once before Task 32. Phases 3–6 are hand-authored and may expose a compile
failure the plan author could not observe. Investigate a real failure before continuing.

## Task order

| Order | Task | Document | Commit message |
| --- | --- | --- | --- |
| 1 | Task 32 — load fixture, metric and 500-way burst | [phase-7a-load-test.md](phase-7a-load-test.md) | `test(load): 500-way confirmation burst` |
| 2 | Task 33 — application, demo and operator documentation | [phase-7b-documentation.md](phase-7b-documentation.md) | `docs: README, demo runbook and operator guide` |

## Verification evidence

No Phase 7 result has been observed. Task 32's instruction to run against the local stack and
Task 33's instruction to walk a fresh-clone demo are execution gates, not authoring evidence. If
the executor cannot bring up the stack, those tasks remain incomplete and no success count is
written. The last measured checkpoint is Task 11: Domain 360, Application 421, Infrastructure
206, API 232, MCP 35, Web 241 and SeedData 75; total 1,570.

## Per-task authoring checks

Run both hand-authored sweeps from [HANDOVER section 6a](HANDOVER.md#6a-the-loop-for-one-hand-authored-task)
after each task document and print their match counts. Those sweeps see C# fenced blocks only;
read the k6 script, load README and all documentation as complete files, and validate JavaScript
syntax separately. Lint every plan Markdown file explicitly before staging.

## Pull request

Task 33 carries the final pull-request gate. This phase adds an Application-to-API observability
port and a load-only operational profile, so its pull request is decision-bearing. Include the
`narrative-required` label, all three Narrative body headings and a fresh AI-Fingerprint. Do not
merge or bypass code-owner review. The authoring handoff requires the plan author to ask before
opening this pull request.
