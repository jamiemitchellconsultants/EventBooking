# Phase 4 API and MCP Tasks 21-23

[← Plans overview](README.md) · [Phase 3](phase-3-application.md) · [Master plan](../superpowers/plans/2026-09-19-eventbooking-implementation.md)

> Use superpowers:executing-plans. Execute one task document at a time, in the order below. Every
> task ends with its own commit and push on the same phase branch.

**Status: hand-authored.** Tasks 21-23 are written, with no observed counts. The checkpoint the
phase starts from is the end of Phase 3 (Task 11 totals 1570).

**Goal:** Endpoint translation only: one handler per endpoint, no domain entity leaving the
handler, no business logic in the translation layer. Contradiction #3 settles the 403 rule:
missing or malformed StaffId returns 403 on every authenticated route except the /api/me route,
which carries its own handling.

**Architecture:** Phase 4 adds the Api and Mcp projects and their tests. REST endpoints translate
HTTP into Phase 3 handlers and back; OpenAPI 3.1 plus Swagger documents the catalogue; MCP tools
call the same handlers so REST and MCP stay in parity.

**Tech Stack:** .NET 10, xUnit, EF Core with Npgsql, Testcontainers, PostgreSQL 16.

**Spec:** [solution architecture](../design/04-solution-architecture.md),
[functional requirements](../design/02-functional-requirements.md),
[master plan Tasks 21-23](../superpowers/plans/2026-09-19-eventbooking-implementation.md).

## Before you start

Phase 4 starts from main after the Phase 3 pull request has merged:

```bash
git fetch origin
test -z "$(git status --porcelain)"
git switch -c claude/phase-4-api-mcp origin/main
export EXECUTOR_COAUTHOR="Your Harness <harness@example.invalid>"
```

## Task order

| Order | Task | Document | Commit message |
| --- | --- | --- | --- |
| 1 | Task 21 — API conventions | phase-4a-api-conventions.md | feat(api): pagination, error catalogue, rate limits and config validation |
| 2 | Task 22 — endpoint catalogue | phase-4b-endpoint-catalogue.md | feat(api): full EventBooking endpoint catalogue |
| 3 | Task 23 — MCP parity | phase-4c-mcp-parity.md | feat(mcp): tool parity with the REST catalogue |

Master Tasks 21-23 map one-to-one onto the three documents above. Each task ends with its own
commit and push on the phase branch, using the commit message in the table.

## Verification evidence

No verified test count exists for these tasks yet: they are hand-authored, nothing has been run,
and no figures are stated here. The checkpoint the phase starts from is Task 11 totals 1570.

## Sequencing notes

Task 21 conventions first, including the 403 rule for missing or malformed `StaffId`:

- Pagination, the error catalogue, rate limits, and config validation are settled once so the
  endpoint catalogue inherits them unchanged.
- The 403 rule applies to every authenticated route except the /api/me route.

Task 22 consumes all Phase 3 handlers plus the OpenAPI snapshot:

- One handler per endpoint across `Location`, `Event`, `EventProposal`, `ProposalAcceptance`,
  `EventCapacity`, `Attendee`, `AttendeeGroup`, `AttendeeGroupRequirement`, `AttendeeRequirement`,
  `Invite`, `InviteLocation`, `InviteOption`, `InviteRequirement`, `Booking`,
  `BookingAppointment`, `StaffAccessProfile`, `StaffIdentity`, `AuditLog`, `EmailLog`,
  `SystemSettings`, and `EventWindow`, with `AttendeeReadiness`, `Role`, `StaffCapability`, and
  `AttendeeStatus` enforced at the handler boundary.
- The OpenAPI snapshot is committed alongside the catalogue so drift is visible in review.

Task 23 is parity minus anonymous and token routes:

- Every authenticated REST endpoint gets a matching MCP tool over the same handler.
- Anonymous and token routes stay REST-only.

## Pull request

Phase 4 is decision-bearing. The pull request therefore carries the narrative-required label and
the three narrative headings, spelled exactly as the pull request template spells them. Task 23
carries the phase gate — not Task 21 or Task 22. Tasks 21 and 22 end at a commit and a push on
the phase branch. Recompute the fingerprint and update the body after any further push to the
branch, or the ai-fingerprint check fails on the stale value.

Do not merge the pull request yourself: code-owner review and the required checks stand.
