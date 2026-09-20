# Phase 0 — Port and strip (Tasks 1–3)

[← Overview](README.md) · [Master plan](../superpowers/plans/2026-09-19-eventbooking-implementation.md) · [Ontology](../ontology.md)

> Use superpowers:executing-plans. Execute one task document at a time, in the order below. Every
> task ends with its own commit and push on the same phase branch. The phase pull request is opened
> only at the end of this document.

**Goal:** Land the predecessor's solution in this repository as EventBooking, without AWS, without
its organisation-specific assumptions, and with a green build and full test suite at every
checkpoint.

**Architecture:** Clean Architecture projects (Domain, Application, Infrastructure, Api.Auth, Api,
Mcp, Web, SeedData) with matching test projects. Phase 0 changes no domain rule that the design
package adds; it establishes the baseline the later phases generalise.

**Tech Stack:** .NET 10, xUnit, EF Core with Npgsql, PostgreSQL Testcontainers, Blazor WebAssembly
with bUnit, the MCP server, Keycloak.

**Spec:** [decision record](../superpowers/specs/2026-09-19-eventbooking-design.md) (D5, D6, D8,
D9, D11), [design package](../design/README.md),
[master plan Tasks 1–3](../superpowers/plans/2026-09-19-eventbooking-implementation.md).

## Before you start

```bash
dotnet --version
docker info
node --version
gh auth status
git fetch origin
test -z "$(git status --porcelain)"
git switch -c codex/phase-0-port-and-strip origin/main
```

Docker must be running: the Infrastructure and SeedData suites start real PostgreSQL containers and
must never be skipped. Set the executing harness co-author identity once per session, because every
task's commit command refuses an empty value:

```bash
export EXECUTOR_COAUTHOR="Your Harness <harness@example.invalid>"
```

## Task order

Master Task 3 is split into four lettered checkpoints. Every checkpoint keeps master Task 3's commit
message, so the phase history reads as one retirement step per commit.

| Order | Task | Document | Commit message |
| --- | --- | --- | --- |
| 1 | Task 1 — import the solution | [phase-0a-import.md](phase-0a-import.md), with [the file manifest](phase-0a-files.md) and source volumes `phase-0a-source-001.md` … `-083.md` | `build: port JointBooking solution as EventBooking without AWS` |
| 2 | Task 2 — vocabulary | [phase-0b-vocabulary.md](phase-0b-vocabulary.md), with edit volumes `phase-0b-edits-001.md` … `-115.md` | `refactor: apply the EventBooking vocabulary mapping` |
| 3 | Task 3a — configurable staff identity | [phase-0c-identity.md](phase-0c-identity.md), with `phase-0c-edits-001.md` … `-006.md` | `refactor: drop bulk import, head-office config and fixed StaffId format` |
| 4 | Task 3b — retire direct event import | [phase-0d-retire-import.md](phase-0d-retire-import.md), with `phase-0d-edits-001.md` … `-019.md` | the same Task 3 message |
| 5 | Task 3c — require an attendee group | [phase-0e-required-groups.md](phase-0e-required-groups.md), with `phase-0e-edits-001.md` … `-008.md` | the same Task 3 message |
| 6 | Task 3d — retire the single-site configuration | [phase-0f-retired-location-config.md](phase-0f-retired-location-config.md), with `phase-0f-edits-001.md` … `-015.md` | the same Task 3 message |

Task 1 deliberately lands predecessor domain names, which Task 2 then removes in one mechanical
pass. The user approved that sequence so the import and the rename stay separately reviewable. No
predecessor domain name survives Task 2, and none reaches the phase pull request.

## Verification evidence

Each checkpoint below was implemented and run before it was written down: a full
`dotnet build EventBooking.sln -warnaserror` followed by every test project, with Docker running and
no skipped tests. Tasks 1, 2, 3a, 3b, 3c and 3d were additionally replayed from these documents into
an independent checkout.

| Checkpoint | Domain | Application | Infrastructure | API | MCP | Web | Seed | Total |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Unmodified predecessor | 230 | 451 | 158 | 225 | 34 | 251 | 76 | 1425 |
| Task 1 | 230 | 451 | 157 | 226 | 34 | 251 | 75 | 1424 |
| Task 2 | 230 | 451 | 157 | 228 | 35 | 251 | 75 | 1427 |
| Task 3a | 241 | 451 | 160 | 232 | 35 | 251 | 75 | 1445 |
| Task 3b | 235 | 423 | 160 | 231 | 35 | 245 | 75 | 1404 |
| Task 3c | 237 | 423 | 160 | 232 | 35 | 242 | 75 | 1404 |
| Task 3d | 237 | 422 | 160 | 232 | 35 | 241 | 75 | 1402 |

Counts fall where a retired capability's tests go with it, and rise where a checkpoint adds
regression tests. A count that does not match after a task is a signal to stop and read the diff,
not to adjust the expectation.

## What Phase 0 deliberately leaves alone

These belong to later phases. Do not start them here, even though the names look temporary:

- The single-zone clock and its transitional member names. Task 4 replaces them with `EventWindow`
  resolution against a `Location`'s `timeZoneId`, using NodaTime.
- Every `Location`, per-`AppointmentType` and N-way `EventProposal` behaviour: Phase 1 onward.
- The inherited migration chain. Task 9 replaces it with one fresh initial migration, and the
  inherited migration guard tests stay until it does.
- Attendee CSV import, which is a surviving requirement (FR-4.3), not part of the retired direct
  event import.

## Phase review, before the pull request

Run this checklist once, after Task 3d's commit and push:

```bash
git log --oneline origin/main..HEAD
dotnet build EventBooking.sln -warnaserror
dotnet test EventBooking.sln
node scripts/check-ontology-terms.mjs
git status --short
```

Confirm each of these by inspection, and fix anything that fails before continuing:

- Six commits, in the task order above, each carrying the master plan's commit message.
- The full suite reports 1402 passing, zero failed and zero skipped.
- No predecessor domain name survives:
  `git grep -nI -E 'Candidate|ConfirmedSlot|SlotProposal|EmployeeGroup|HeadOffice' -- src tests`
  returns nothing.
- No cloud-provider remnant survives:
  `git grep -nI -iE 'aws|entra|terraform|minio' -- src tests` returns nothing relevant.
- The working tree is clean and holds no editor or build leftovers.
- The ontology source is unchanged by this phase: Phase 0 aligns ported code to concepts the
  ontology already defines, and adds none. If a task did make you change `docs/ontology.ttl`,
  regenerate with `node scripts/build-ontology.mjs` and commit both files together before opening
  the pull request.

## Pull request

Phase 0 is decision-bearing: it puts D5, D6, D8, D9 and D11 into code. The pull request therefore
carries the `narrative-required` label and the three narrative headings, spelled exactly as
`.github/pull_request_template.md` spells them. Supplying a body replaces that template wholesale,
so the body below carries those headings and the fingerprint footer itself.

```bash
cat > /tmp/phase-0-body.md <<'PHASE_0_BODY'
## Change

Ports the predecessor solution into this repository as EventBooking and strips the assumptions the
design package retires: AWS and Entra hosting, direct event import, the fixed staff-number format,
the optional attendee group, and the single-site address and time-zone configuration.

Six commits, one per task document under `docs/detailed-implementations/`. Full suite: 1402 passing,
zero skipped.

## Narrative classification

- Apply `narrative-required` when this PR makes a meaningful product, architecture, governance,
  operational, correction, or experimental decision.
- Leave the label off for mechanical changes that do not alter project intent.

## Narrative Context

EventBooking is built by porting and generalising the predecessor (D5) rather than rebuilding from
scratch. The port has to reach this repository before any generalisation, and it arrives carrying
organisation-specific assumptions that the design package explicitly retires.

## Narrative Decision

Land the port as one phase of six reviewable commits: import the solution without the cloud
projects, apply the vocabulary mapping mechanically, then retire one predecessor assumption per
checkpoint — a configurable `StaffId` (D11), no direct event import (D6), a required
`AttendeeGroup`, and no deployment-level address or time zone. Keycloak is the only identity
provider shipped (D8) and no object storage is carried over (D9). A temporary single-zone clock
survives until Task 4 replaces it with per-`Location` zones.

## Narrative Consequences

The solution builds with warnings as errors and the full suite passes at every checkpoint, giving
Phase 1 a green baseline. The inherited migration chain and the transitional clock names remain, and
are removed by Tasks 9 and 4 respectively. No data migration from the predecessor is supported (D12).

---

AI-Fingerprint: sha256:FINGERPRINT
PHASE_0_BODY

MERGE_BASE=$(git merge-base origin/main HEAD)
FINGERPRINT=$(git diff "$MERGE_BASE" HEAD | shasum -a 256 | cut -c1-12)
sed -i '' "s/FINGERPRINT/$FINGERPRINT/" /tmp/phase-0-body.md
node scripts/check-ontology-terms.mjs --also /tmp/phase-0-body.md
gh pr create --base main --title "Phase 0: port the predecessor solution as EventBooking" \
  --label narrative-required --body-file /tmp/phase-0-body.md
```

On Linux, use `sha256sum` in place of `shasum -a 256`, and `sed -i` without the empty argument.
Recompute the fingerprint and update the body after any further push to the branch, or the
`ai-fingerprint` check fails on the stale value.

Do not merge the pull request yourself: code-owner review and the required checks stand. Once it has
merged, start Phase 1 from a fresh branch cut from the updated `main`.
