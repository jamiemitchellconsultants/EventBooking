# Phase 4 — API and MCP (Tasks 21–23)

[← Plans overview](README.md) · [Phase 3](phase-3-application.md) · [Master plan](../superpowers/plans/2026-09-19-eventbooking-implementation.md)

> Use superpowers:executing-plans. Execute one task document at a time, in the order below. Every
> task ends with its own commit and push on the same phase branch.

**Status: Tasks 21 and 22a written.** Phase 4 is hand-authored, like Phase 3: complete code and tests are
written straight into each document, with no prototype, because the executing model compiles and
test-drives them itself. That method was settled deliberately for this phase — Tasks 21 to 23 are
endpoint and tool wiring over handlers Phase 3 already specifies, so the code is thin and
repetitive and the method fits.

**Goal:** Put design 05's contract on the wire. Task 21 builds the conventions every endpoint then
obeys — the RFC 9457 problem body and its error catalogue, the signed opaque cursor, the one shared
event-time representation, `_links` from the caller's capabilities, the Idempotency-Key retention,
the three rate limits, the probes, the metrics endpoint, structured logging with a correlation
identifier, and startup validation. Task 22a adds the read models design 05 asks for that Phase 3
does not supply. Task 22b maps the whole endpoint catalogue over them. Task 23 gives every staff
operation an MCP tool and proves the two surfaces cannot drift.

**Architecture:** Phase 4 changes the Api and Mcp projects and their tests, plus the read-model
queries Task 22a adds in Application and Infrastructure. No endpoint and no tool holds a business
rule: both translate a transport call into a handler call and render what comes back. The
authorization decision stays inside the handler, which is what lets one capability check serve REST
and MCP alike.

**Tech Stack:** .NET 10, xUnit, WebApplicationFactory, EF Core with Npgsql, Testcontainers,
PostgreSQL 16.

**Spec:** [API design](../design/05-api-design.md),
[security and authentication](../design/06-security-and-authentication.md),
[solution architecture](../design/04-solution-architecture.md),
[non-functional requirements](../design/08-nonfunctional-requirements.md),
[master plan Tasks 21–23](../superpowers/plans/2026-09-19-eventbooking-implementation.md).

## Before you start

Phase 4 starts from `main` after the Phase 3 pull request has merged:

```bash
git fetch origin
test -z "$(git status --porcelain)"
git switch -c codex/phase-4-api-and-mcp origin/main
export EXECUTOR_COAUTHOR="Your Harness <harness@example.invalid>"
```

## Task order

| Order | Task | Document | Commit message |
| --- | --- | --- | --- |
| 1 | Task 21 — API conventions | [phase-4a-api-conventions.md](phase-4a-api-conventions.md) | `feat(api): pagination, error catalogue, rate limits and config validation` |
| 2 | Task 22a — the read models the catalogue needs | [phase-4b-event-read-models.md](phase-4b-event-read-models.md) | `feat(api): full EventBooking endpoint catalogue` |
| 3 | Task 22b — the endpoint catalogue | phase-4c-endpoint-catalogue.md | `feat(api): full EventBooking endpoint catalogue` |
| 4 | Task 23 — MCP parity, phase gate | phase-4d-mcp-parity.md | `feat(mcp): tool parity with the REST catalogue` |

Master Task 22 is split into two documents, the way master Task 20 was split into 20a and 20b.
The reason is in the sequencing notes below. Both carry the master plan's single commit message,
because the master plan's messages never change; 22a and 22b are therefore two commits with the
same subject, which is what the Task 20a/20b split already does.

## Verification evidence

No verified test count exists for any Phase 4 task. Each is hand-authored, nothing has been run,
and any figure in a task document is what the executor should expect to reach, not a figure
observed here. The checkpoint the phase starts from is the end of Phase 2 (Task 11): Domain 360,
Application 421, Infrastructure 206, API 232, MCP 35, Web 241, SeedData 75; total 1570. Phase 3
adds no measured row for the same reason, so the next real figure is whatever an executor reaches
at Task 12.

## Sequencing notes

**Why master Task 22 is split.** Task 22 scopes itself as wiring — "Consumes: every Phase 3
handler", and "endpoints only translate HTTP to handler calls; no business rule lives in an
endpoint". Seven of design 05's endpoints have no handler behind them: the filtered `Event` and
`EventProposal` lists, the single-`Event` read, the cancellable-`Event` list, `includeInactive` on
the three reference-data lists, an update carrying `isActive` where Task 12 splits update from
set-active, and the capacity route's appointment-type identifier, which Task 13's command does not
take. Filtering those in the endpoint is the in-memory shape Task 11 removed from the eligibility
path at some 680 ms against 18 ms, so it is not a neutral fallback. The user settled this as a
lettered split: 22a adds the missing Application queries and read models, 22b is the pure endpoint
catalogue over them.

**Two capabilities on one route.** Design 05 gives `GET /api/events` either
`ManageEventNegotiation` or `ViewEventOperations`, against design 04's "exactly one
`StaffCapability` per handler". Settled as Task 20b's audit precedent: the handler demands
neither and filters by whichever the caller holds — a Manager sees their own type's view, an
Admin or a Coordinator sees every type — with the filter enforced in the query rather than only
in the handler, so bypassing the capability check still filters.

**Task 21 settles six contradictions**, listed in its own document and recorded in the handover's
section 8. The one every later task inherits is contradiction #3: a missing or malformed
`staff_id` is 403 everywhere except `GET /api/me`, and `unauthenticated` stays 401 for a missing
or invalid bearer token. The other five close the error catalogue, which is what Tasks 22b and 23
render every refusal through.

## Pull request

Phase 4 is decision-bearing: the endpoint contract, the MCP parity rule, and the six settlements
Task 21 makes. The pull request therefore carries the `narrative-required` label and the three
narrative headings, spelled exactly as `.github/pull_request_template.md` spells them. Task 23
carries the phase's pull-request gate — not Task 21. Every task before it ends at a commit and a
push on the phase branch. Recompute the fingerprint and update the body after any further push to
the branch, or the `ai-fingerprint` check fails on the stale value.

Do not merge the pull request yourself: code-owner review and the required checks stand.
