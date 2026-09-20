# Phase 3 — Application (Tasks 12–20)

[← Plans overview](README.md) · [Phase 2](phase-2-persistence.md) · [Master plan](../superpowers/plans/2026-09-19-eventbooking-implementation.md)

> Use superpowers:executing-plans. Execute one task document at a time, in the order below. Every
> task ends with its own commit and push on the same phase branch.

**Status: Tasks 12–18 written; Tasks 19–20 not yet authored.** Phase 3 is hand-authored: complete code
and complete tests are written straight into each document, with no prototype, because the
executing model compiles and test-drives them itself.

**Goal:** Generalise every use case behind the handler pattern in design 04 ("Commands, queries
and transactions"): one transaction per command, locks in the canonical order, the audit entry
written in the transaction, no domain entity leaving the handler, exactly one StaffCapability
demanded per handler. Reference data and settings first, then negotiation, invites, booking and
cancellation, recovery and workspace, identity and authorization, the notification outbox,
background jobs, and finally dashboards, the attendee list and audit search.

**Architecture:** Phase 3 changes the Application project and its tests, plus read-model queries
in Infrastructure. The domain changes only where Task 12 makes the settings snapshot explicit on
the invite (see below). Application tests use in-memory fakes of the ports; anything depending on
locking or SQL is also covered in Infrastructure tests.

**Tech Stack:** .NET 10, xUnit, EF Core with Npgsql, Testcontainers, PostgreSQL 16.

**Spec:** [solution architecture](../design/04-solution-architecture.md),
[functional requirements](../design/02-functional-requirements.md),
[master plan Tasks 12–20](../superpowers/plans/2026-09-19-eventbooking-implementation.md).

## Before you start

Phase 3 starts from `main` after the Phase 2 pull request has merged:

```bash
git fetch origin
test -z "$(git status --porcelain)"
git switch -c codex/phase-3-application origin/main
export EXECUTOR_COAUTHOR="Your Harness <harness@example.invalid>"
```

## Task order

| Order | Task | Document | Commit message |
| --- | --- | --- | --- |
| 1 | Task 12 — reference-data and settings handlers | [phase-3a-reference-data-settings.md](phase-3a-reference-data-settings.md) | `feat(app): reference-data and settings use cases` |
| 2 | Task 13 — negotiation and capacity-adjustment handlers | [phase-3b-negotiation.md](phase-3b-negotiation.md) | `feat(app): N-way negotiation with serialised confirmation` |
| 3 | Task 14 — invite engine | [phase-3c-invite-engine.md](phase-3c-invite-engine.md) | `feat(app): location-restricted invite engine` |
| 4 | Task 15 — booking, cancellation and event cancellation | [phase-3d-booking-cancellation.md](phase-3d-booking-cancellation.md) | `feat(app): booking and cancellation over N capacity rows` |
| 4 | Task 15 — booking, cancellation and event cancellation | not yet authored | `feat(app): booking and cancellation over N capacity rows` |
| 5 | Task 16 — recovery and the appointment workspace | [phase-3e-recovery-workspace.md](phase-3e-recovery-workspace.md) | `feat(app): recovery and workspace across locations` |
| 6 | Task 17 — staff identity, role sync and authorization | [phase-3f-staff-authorization.md](phase-3f-staff-authorization.md) | `feat(auth): provider-neutral OIDC with per-request role sync` |
| 7 | Task 18 — notification outbox and templates | [phase-3g-notification-outbox.md](phase-3g-notification-outbox.md) | `feat(email): durable outbox dispatcher and location-aware templates` |
| 8 | Task 19 — background jobs | not yet authored | `feat(jobs): advisory-locked invite sweep` |
| 9 | Task 20 — dashboards, attendee list and audit search | not yet authored (probably needs lettered splits) | `feat(app): bounded dashboards and bucketed audit search` |

## Verification evidence

No verified test count exists for Task 12 yet: it is hand-authored, nothing has been run, and any
figure in its document is what the executor should expect to reach, not a figure observed here.
The checkpoint the phase starts from is the end of Phase 2 (Task 11): Domain 360, Application
421, Infrastructure 206, API 232, MCP 35, Web 241, SeedData 75; total 1570.

## Sequencing notes

Task 12 settles three things the rest of the phase inherits:

- **The task owns reference data and settings only.** Attendee CRUD, import and boundary work stay
  with Task 20, which will probably need lettered splits (contradiction #5, settled with the user
  before Task 12 was written).
- **Settings snapshot onto the invite.** The master plan's own test list says valid settings must
  not alter existing invites; the ontology now carries the three snapshot fields on the invite
  (`inviteExpiryDays`, `maxAutoRetryCount`, `inviteOptionCount`), and Task 12 writes them at issue
  time in Task 14's issuer contract (contradiction #9, settled with the user).
- **`inviteOptionCount` becomes editable.** It has been stored since Task 5 but carried through the
  ported settings handler unchanged; Task 12 adds it to the command, the view and the domain
  update, and retires that transitional pass-through.

Task 12 also starts retiring the Phase 3 scaffolding: the predecessor's fixed appointment-type
identifiers and seeded rows give way to Admin-managed types (the plan's MED, FIT and IND codes
are used in tests; the DAT/MED/UNI predecessor rows they map onto retire with the seed rework in
Phase 6), and the seeded transitional location row stays only until Task 13 removes the
transitional-location constant from the negotiation and capacity handlers.

## Pull request

Phase 3 is decision-bearing (D13 outbox, D14 token lifecycle as implemented, D15). The pull
request therefore carries the `narrative-required` label and the three narrative headings, spelled
exactly as `.github/pull_request_template.md` spells them. Task 20 carries the phase's
pull-request gate — not Task 12. Task 12 ends at a commit and a push on the phase branch.
Recompute the fingerprint and update the body after any further push to the branch, or the
`ai-fingerprint` check fails on the stale value.

Do not merge the pull request yourself: code-owner review and the required checks stand.
