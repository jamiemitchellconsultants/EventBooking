# Phase 1 — Domain generalisation (Tasks 4–8)

[← Plans overview](README.md) · [Phase 0](phase-0-port-and-strip.md) · [Master plan](../superpowers/plans/2026-09-19-eventbooking-implementation.md)

> Use superpowers:executing-plans. Execute one task document at a time, in the order below. Every
> task ends with its own commit and push on the same phase branch.

**Status: in progress.** Task 4 is written and verified. Tasks 5–8 are not yet authored; do not
start the phase expecting to finish it.

**Goal:** Generalise the domain itself — variable-length windows read in a location's zone,
Admin-managed reference data, N-type negotiation, N-row capacity, and location-restricted invites —
with everything proven by domain tests before any persistence or API work.

**Architecture:** Phase 1 changes the domain project and its tests. Infrastructure changes only
where a mapping must keep compiling; the fresh schema is Task 9's job. A fake clock and a fake zone
abstraction drive every time-dependent rule.

**Tech Stack:** .NET 10, xUnit, NodaTime (the IANA database), EF Core only where a mapping follows.

**Spec:** [domain model](../design/01-domain-model.md),
[functional requirements](../design/02-functional-requirements.md),
[master plan Tasks 4–8](../superpowers/plans/2026-09-19-eventbooking-implementation.md).

## Before you start

Phase 1 starts from `main` after the Phase 0 pull request has merged:

```bash
git fetch origin
test -z "$(git status --porcelain)"
git switch -c codex/phase-1-domain origin/main
export EXECUTOR_COAUTHOR="Your Harness <harness@example.invalid>"
```

## Task order

| Order | Task | Document | Commit message |
| --- | --- | --- | --- |
| 1 | Task 4 — variable-length windows in the location's zone | [phase-1a-event-window.md](phase-1a-event-window.md), with `phase-1a-edits-001.md` … `-028.md` | `feat(domain): variable-length EventWindow in the location's time zone` |
| 2 | Task 5 — reference-data aggregates and settings | not yet authored | `feat(domain): Admin-managed Location, AppointmentType and AttendeeGroup` |
| 3 | Task 6 — N-type negotiation | not yet authored | `feat(domain): negotiate an EventProposal across any number of types` |
| 4 | Task 7 — capacity generalised to N rows | not yet authored | `feat(domain): N-row EventCapacity charging only required types` |
| 5 | Task 8 — invites with locations, and the closed status table | not yet authored | `feat(domain): location-restricted invites and closed attendee transitions` |

## Verification evidence

| Checkpoint | Domain | Application | Infrastructure | API | MCP | Web | Seed | Total |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| End of Phase 0 (Task 3d) | 237 | 422 | 160 | 232 | 35 | 241 | 75 | 1402 |
| Task 4 | 258 | 422 | 173 | 232 | 35 | 241 | 75 | 1436 |

Each figure comes from a full `dotnet build EventBooking.sln -warnaserror` followed by every test
project, with Docker running and no skipped tests. Task 4 was additionally replayed from its own
documents into an independent checkout.

## Sequencing notes for Tasks 5–8

Two decisions taken while authoring Task 4 constrain what follows. They are recorded here so the
later tasks do not re-open them:

- **The zone abstraction lives in the domain project.** The design lists a time-zone resolver among
  the application's ports, but the domain's own rules ("has it started", "is it on the event date")
  depend on the answers, and dependencies point inward. The domain therefore declares the
  abstraction and infrastructure implements it with NodaTime; the application layer uses that same
  abstraction rather than declaring a second one.
- **The temporary single-zone clock survives Task 4.** The master plan removes it in Task 4, but
  nothing can supply a per-event zone until `Location` exists. It is therefore retired in Task 5,
  with the `Location` that replaces it, and every caller that still reads a single-zone "today"
  moves at that point.

Tasks 5–8 must also settle these, which the design package leaves open:

- **Who may withdraw a proposal.** FR-2.9 gives that to "the current Manager of the proposer's
  type", but `EventProposal` records only `createdByManagerUserId`. Either the proposer's
  `AppointmentType` becomes part of the proposal in the ontology, or the rule has to be derived
  from the proposer's own `ProposalAcceptance`. This is an ontology change, so it is settled before
  Task 6 is written, not during it.
- **Lock ordering versus the cancellation flow.** The documented order is `Attendee`,
  `EventProposal`, `Event`, then `EventCapacity`, while the cancellation sequence in design 03b
  takes the event first and then discovers attendees. Task 7 defines the ordering helper, so the
  reconciliation belongs there.
