# Phase 2 — Persistence (Tasks 9–11)

[← Plans overview](README.md) · [Phase 1](phase-1-domain.md) · [Master plan](../superpowers/plans/2026-09-19-eventbooking-implementation.md)

> Use superpowers:executing-plans. Execute one task document at a time, in the order below. Every
> task ends with its own commit and push on the same phase branch.

**Status: in progress.** Task 9a is written, verified and replayed. Tasks 9b, 10 and 11 are not yet
authored; do not start the phase expecting to finish it.

**Goal:** Give the generalised domain a schema of its own — one fresh initial migration with the
constraints the design names, database roles that keep the audit trail append-only, ordered row-lock
helpers with a concurrency harness that proves they do not deadlock, and the relational-division
query that finds eligible events.

**Architecture:** Phase 2 changes the infrastructure project and its tests. The domain changes only
where a stored value moves onto an aggregate that should always have owned it. Every figure below
comes from a real PostgreSQL 16 in Testcontainers, never an in-memory provider.

**Tech Stack:** .NET 10, xUnit, EF Core with Npgsql, Testcontainers, PostgreSQL 16.

**Spec:** [solution architecture](../design/04-solution-architecture.md),
[security and authentication](../design/06-security-and-authentication.md),
[non-functional requirements](../design/08-nonfunctional-requirements.md),
[master plan Tasks 9–11](../superpowers/plans/2026-09-19-eventbooking-implementation.md).

## Before you start

Phase 2 starts from `main` after the Phase 1 pull request has merged:

```bash
git fetch origin
test -z "$(git status --porcelain)"
git switch -c codex/phase-2-persistence origin/main
export EXECUTOR_COAUTHOR="Your Harness <harness@example.invalid>"
```

## Task order

| Order | Task | Document | Commit message |
| --- | --- | --- | --- |
| 1 | Task 9a — deterministic attendee links and the version counter | [phase-2a-attendee-tokens.md](phase-2a-attendee-tokens.md), with `phase-2a-edits-001.md` … `-032.md` | `feat(security): deterministic attendee tokens with a stored version counter` |
| 2 | Task 9b — the fresh schema and database roles | not yet authored | `feat(persistence): fresh initial schema with capacity and role constraints` |
| 3 | Task 10 — unit of work, ordered lock helpers, concurrency harness | not yet authored | `feat(persistence): ordered row-lock helpers and concurrency harness` |
| 4 | Task 11 — invite eligibility query | not yet authored | `feat(persistence): relational-division invite eligibility query` |

Master Task 9 is split into two documents. The user settled that design 06's version counter moves
out of Task 8 and lands here, so the fresh schema writes the column once; splitting the token change
away from the squash keeps each document reviewable and lets each one be replayed on its own.

## Verification evidence

| Checkpoint | Domain | Application | Infrastructure | API | MCP | Web | Seed | Total |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| End of Phase 1 (Task 8) | 358 | 424 | 175 | 232 | 35 | 241 | 75 | 1540 |
| Task 9a | 360 | 424 | 174 | 232 | 35 | 241 | 75 | 1541 |

Each figure comes from a full `dotnet build EventBooking.sln -warnaserror` followed by every test
project, with Docker running and no skipped tests. Each task was additionally replayed from its own
documents into an independent checkout.

Infrastructure drops by one at Task 9a. The predecessor's token tests covered a random nonce and a
stored hash, and neither exists any more; the replacements cover the purpose, the version and the
canonical encoding instead.

## Sequencing notes

Task 9a settled three things that the rest of the phase inherits:

- **The token is derived, not stored.** Signing the purpose, the identifier and the version into the
  token means the server can reproduce any link from the row. Four hash-keyed repository methods and
  their two unique indexes go with the hash, and every attendee lookup becomes a primary-key read
  after a constant-time signature check.
- **A resend reuses the current link.** Design 06 says so explicitly, and it is the whole point of a
  deterministic token: the confirmation page and the confirmation email carry the same manage link.
  The retry path therefore no longer rotates the counter, and the tests that asserted rotation now
  assert that it does not move.
- **Version 0 does not exist.** It is refused at issue and rejected on read, so the generated
  migration's backfill of `0` is wrong. Task 9a's migration writes `1` and then drops the column
  default, which is the pattern every column-adding migration in this project follows.

The migration Task 9a adds is deliberately short-lived: **Task 9b deletes the entire inherited chain
including it**, and writes one initial migration that carries `token_version` and
`manage_token_version` from the start. Nothing between the two tasks depends on the intermediate
chain, and the guard tests that pin the inherited migrations retire with it.

## Pull request

Phase 2 is decision-bearing: it puts D12 (the fresh schema) and D14 (the attendee token lifecycle)
into code. The pull request therefore carries the `narrative-required` label and the three narrative
headings, spelled exactly as `.github/pull_request_template.md` spells them. Supplying a body
replaces that template wholesale, so carry those headings and the `AI-Fingerprint:` footer in the
body yourself, exactly as [Phase 1](phase-1-domain.md#pull-request) shows.

Open it only after Task 11, which is the master plan's own gate for this phase. Recompute the
fingerprint and update the body after any further push to the branch, or the `ai-fingerprint` check
fails on the stale value.

Do not merge the pull request yourself: code-owner review and the required checks stand.
