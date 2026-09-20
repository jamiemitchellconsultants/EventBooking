# Phase 2 — Persistence (Tasks 9–11)

[← Plans overview](README.md) · [Phase 1](phase-1-domain.md) · [Master plan](../superpowers/plans/2026-09-19-eventbooking-implementation.md)

> Use superpowers:executing-plans. Execute one task document at a time, in the order below. Every
> task ends with its own commit and push on the same phase branch.

**Status: in progress.** Tasks 9a, 9b and 10 are written, verified and replayed. Task 11 is not yet
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
| 2 | Task 9b — the fresh schema and database roles | [phase-2b-fresh-schema.md](phase-2b-fresh-schema.md), with `phase-2b-edits-001.md` … `-026.md` | `feat(persistence): fresh initial schema with capacity and role constraints` |
| 3 | Task 10 — unit of work, ordered lock helpers, concurrency harness | [phase-2c-ordered-locks.md](phase-2c-ordered-locks.md), with `phase-2c-edits-001.md` … `-002.md` | `feat(persistence): ordered row-lock helpers and concurrency harness` |
| 4 | Task 11 — invite eligibility query | not yet authored | `feat(persistence): relational-division invite eligibility query` |

Master Task 9 is split into two documents. The user settled that design 06's version counter moves
out of Task 8 and lands here, so the fresh schema writes the column once; splitting the token change
away from the squash keeps each document reviewable and lets each one be replayed on its own.

## Verification evidence

| Checkpoint | Domain | Application | Infrastructure | API | MCP | Web | Seed | Total |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| End of Phase 1 (Task 8) | 358 | 424 | 175 | 232 | 35 | 241 | 75 | 1540 |
| Task 9a | 360 | 424 | 174 | 232 | 35 | 241 | 75 | 1541 |
| Task 9b — end of master Task 9 | 360 | 424 | 176 | 232 | 35 | 241 | 75 | 1543 |
| Task 10 | 360 | 424 | 190 | 232 | 35 | 241 | 75 | 1557 |

Each figure comes from a full `dotnet build EventBooking.sln -warnaserror` followed by every test
project, with Docker running and no skipped tests. Each task was additionally replayed from its own
documents into an independent checkout.

Infrastructure drops by one at Task 9a. The predecessor's token tests covered a random nonce and a
stored hash, and neither exists any more; the replacements cover the purpose, the version and the
canonical encoding instead. It rises by two at Task 9b despite five inherited-migration guard tests
retiring with the chain they pinned: fifteen schema tests take their place, and what the guards
protected is now asserted as a live constraint rather than as a step in a chain.

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

Task 9b settled four things that Tasks 10 and 11 inherit:

- **Seed rows live in the model, not in migration SQL.** The predecessor inserted the three
  appointment types, the five attendee groups and their requirement mappings from raw SQL inside
  named migrations. They move into the model's seed data, so the single migration stays regenerable
  rather than hand-patched. The transitional `Location` joins them, for the same reason: the rest of
  the schema references it and nothing manages locations until Phase 3.
- **The roles script runs before the migration, and its absence is not fatal.** The migration grants
  to roles it deliberately does not create, because a deployment owns who logs in. Both roles are
  `NOLOGIN`; a deployment attaches its own login role and a test reaches them with `SET ROLE`.
- **`start_utc` is nullable until Task 11.** It is a derived column, and Task 11's repository is
  what computes it. A non-nullable column would take a silent `0001-01-01` for every row nothing has
  computed yet — and the eligibility query filters on `start_utc`, so a wrong instant would quietly
  hide the event instead of failing.
- **The capacity check gained its third predicate.** The inherited constraint allowed a total of
  zero. The master plan's `ck_event_capacity_bounds` does not, and the schema tests prove all three
  directions.

Task 10 settled three things Task 11 and Phase 3 inherit:

- **The order is a property of one class, not of each handler.** Every row lock goes through the
  helpers, including the repository methods handlers written in earlier phases already call, so
  those handlers gained the guard without being touched — and none of them trips it, which is the
  first evidence that the documented order is the one the code was already taking.
- **The guard is on in Debug and off in a released build.** The tracker records the level either
  way; what a released build does not do is turn a lock order no test has ever reached into a 500
  for the attendee who happened to hit it.
- **Only the four documented levels are tracked.** `Invite` and `Booking` locks sit between the
  attendee and the event in the real handlers, but the design names four levels and this task
  builds four helpers. Extending the ladder is Task 15's business, when the real booking handler
  adopts the helpers.

One thing about the harness is worth stating, because the obvious version of the test does not
work: with the event locks in place, two attempts naming the same two events in opposite orders
serialise on the event rows before they ever reach a capacity row, so that test passes whether or
not the capacity rows are ordered. The scenario that actually pins the ordering is the same shuffle
with the event locks removed — remove the domain's capacity ordering function and it deadlocks
within seconds, which is how it was verified.

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
