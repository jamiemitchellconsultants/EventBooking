# Phase 1 — Domain generalisation (Tasks 4–8)

[← Plans overview](README.md) · [Phase 0](phase-0-port-and-strip.md) · [Master plan](../superpowers/plans/2026-09-19-eventbooking-implementation.md)

> Use superpowers:executing-plans. Execute one task document at a time, in the order below. Every
> task ends with its own commit and push on the same phase branch.

**Status: complete.** All five task documents are written, verified and replayed. Work through
them in order, then open the pull request the last section describes.

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
| 2 | Task 5 — reference-data aggregates and settings | [phase-1b-reference-data.md](phase-1b-reference-data.md), with `phase-1b-edits-001.md` … `-005.md` | `feat(domain): Admin-managed Location, AppointmentType and AttendeeGroup` |
| 3 | Task 6 — N-type negotiation | [phase-1c-negotiation.md](phase-1c-negotiation.md), with `phase-1c-edits-001.md` … `-025.md` | `feat(domain): negotiate an EventProposal across any number of types` |
| 4 | Task 7 — capacity generalised to N rows | [phase-1d-capacity.md](phase-1d-capacity.md), with `phase-1d-edits-001.md` … `-009.md` | `feat(domain): N-row EventCapacity charging only required types` |
| 5 | Task 8 — invites with locations, and the closed status table | [phase-1e-invites.md](phase-1e-invites.md), with `phase-1e-edits-001.md` … `-034.md` | `feat(domain): location-restricted invites and closed attendee transitions` |

## Verification evidence

| Checkpoint | Domain | Application | Infrastructure | API | MCP | Web | Seed | Total |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| End of Phase 0 (Task 3d) | 237 | 422 | 160 | 232 | 35 | 241 | 75 | 1402 |
| Task 4 | 258 | 422 | 173 | 232 | 35 | 241 | 75 | 1436 |
| Task 5 | 289 | 422 | 173 | 232 | 35 | 241 | 75 | 1467 |
| Task 6 | 306 | 422 | 173 | 232 | 35 | 241 | 75 | 1484 |
| Task 7 | 320 | 424 | 173 | 232 | 35 | 241 | 75 | 1500 |
| Task 8 — end of Phase 1 | 358 | 424 | 175 | 232 | 35 | 241 | 75 | 1540 |

Each figure comes from a full `dotnet build EventBooking.sln -warnaserror` followed by every test
project, with Docker running and no skipped tests. Each task was additionally replayed from its own
documents into an independent checkout.

## Sequencing notes for Tasks 5–8


Two decisions taken while authoring Task 4 constrain what follows. They are recorded here so the
later tasks do not re-open them:

- **The zone abstraction lives in the domain project.** The design lists a time-zone resolver among
  the application's ports, but the domain's own rules ("has it started", "is it on the event date")
  depend on the answers, and dependencies point inward. The domain therefore declares the
  abstraction and infrastructure implements it with NodaTime; the application layer uses that same
  abstraction rather than declaring a second one.
- **The temporary single-zone clock outlives Phase 1's first tasks.** The master plan removes it in
  Task 4, but nothing can supply a per-event zone until handlers carry a `Location`, which is Phase
  3's work. Task 5 introduces the `Location` aggregate; the clock and its transitional member names
  retire when the handlers start reading zones from it.
- **The predecessor's seeded appointment types survive Phase 1.** Task 5 makes the type
  Admin-managed, but the three inherited rows and their fixed identifiers stay until Phase 3 moves
  seeding and the handlers onto the managed create path.
- **`inviteOptionCount` is domain state before it is editable.** Task 5 adds it with the design's
  bounds and default; Task 12 adds it to the settings command, the API and the MCP tool together.
  Until then the handler passes the stored value straight back.

Two further decisions were taken in Task 6:

- **Who may withdraw a proposal, settled.** `proposerAppointmentTypeId` is now part of
  `EventProposal` in the ontology, and both the proposal's withdrawal and the proposer's own
  acceptance are judged against that type. A successor Manager inherits both.
- **The predecessor's Admin fallback for withdrawing a proposal is gone.** It contradicted FR-2.9,
  and the design's capability matrix gives Admin no negotiation capability. The null-scope gate
  (FR-10.7) therefore arrives early in these handlers: a scoped role with no scope is refused
  rather than crashing.

Task 7 settled three more:

- **Lock ordering versus the cancellation flow, applied.** The documented order — `Attendee`,
  `EventProposal`, `Event`, `EventCapacity` — wins. Event cancellation reads the affected attendee
  identifiers without locks, then takes each booking's locks in that order and re-validates under
  lock. The sequence diagram in [design 03b](../design/03b-screens-and-flows.md) locked the `Event`
  first and is corrected, along with the matching row in
  [design 01](../design/01-domain-model.md)'s cross-aggregate table, which carried the same
  ordering. Both corrections live in this repository; neither file exists in the executor's
  checkout.
- **A refusal and a bad request are different results.** A headcount below the type's
  active-booking count is returned as an outcome carrying the minimum, because FR-3.6 has to tell
  the Manager what it would accept. A non-positive total, or one above 1000, is still thrown: it is
  a malformed request, not a decision the domain is reporting.
- **Cancellation is judged on the window's start instant, not its date.** Task 4 supplied the
  instant and this is its first `Event`-level caller, so an event that began earlier today can no
  longer be cancelled while one later today still can. The zone is the transitional location's
  until Phase 3 gives each handler the `Event`'s own `Location`.

The charge and release methods this task adds are domain API that the booking and cancellation
handlers do not call yet: they still work on the rows their repository locked. Task 10's
ordered-lock helpers are what let them adopt these.

Task 8 closes the phase, and settled three things of its own:

- **The status table is the rule, not a comment beside it.** The legal moves live in `Attendee` as
  a set, exposed through a pure predicate. A method added later cannot widen the table by accident,
  and the invite issuer asks the same set rather than restating FR-5.4 and FR-5.7 in its own words.
- **An invited `Attendee` never drops back to `AwaitingAvailability`.** Design 01 does not list
  that move; FR-5.7 sends a failed automatic re-issue to `NoResponseNeedsFollowUp`. The inherited
  expiry path did the former, and is corrected.
- **The status stamp belongs to the aggregate.** The predecessor wrote statusChangedAt from an
  infrastructure save-changes interceptor through an EF shadow property, which put an ontology
  attribute out of the domain's reach. The column and its migration are unchanged; the value now
  comes from the caller's instant and the interceptor is retired.

One thing the master plan asks of Task 8 is **deliberately deferred**: design 06 replaces the
stored book-token hash with a `tokenVersion` counter, and the master plan puts that in Task 8. The
hash is load-bearing in 62 files, and Task 9 rewrites the column anyway in the fresh schema, so the
switch happens there, with the HMAC token service, rather than twice.

## Pull request

Phase 1 is decision-bearing: it puts D2, D4 and D15 into code, and carries D3 and D7 further. The
pull request therefore carries the `narrative-required` label and the three narrative headings,
spelled exactly as `.github/pull_request_template.md` spells them. Supplying a body replaces that
template wholesale, so the body below carries those headings and the fingerprint footer itself.

```bash
cat > /tmp/phase-1-body.md <<'PHASE_1_BODY'
## Change

Generalises the domain: event windows carry a duration and are read in a location's time zone,
reference data becomes Admin-managed, negotiation runs across any number of appointment types,
capacity becomes one row per listed type charged only where required, and invites are restricted to
the locations a Coordinator chose while the attendee status table becomes closed.

Five commits, one per task document under `docs/detailed-implementations/`. Full suite: 1540
passing, zero skipped.

## Narrative classification

- Apply `narrative-required` when this PR makes a meaningful product, architecture, governance,
  operational, correction, or experimental decision.
- Leave the label off for mechanical changes that do not alter project intent.

## Narrative Context

The ported predecessor assumed three fixed appointment types, one site, one four-hour window shape
and a lifecycle whose illegal transitions were unstated. Every later phase — persistence, handlers,
API, screens — reads those assumptions, so they have to go before anything is built on top of them.

## Narrative Decision

Generalise the domain first and prove it with domain tests, before any persistence or API work.
An `EventProposal` lists 1 to 20 appointment types and is judged by the proposing type rather than
the person (D2); an `Invite` is restricted to the `Location`s the Coordinator chose (D4); and the
`AttendeeStatus` transitions become a closed table that refuses anything design 01 does not list
(D15). Reference data becomes Admin-managed and `inviteOptionCount` becomes stored state (D3, D7).
Two rules move inward to the aggregate that owns them: a headcount adjustment reports the minimum
it would accept rather than throwing, and the attendee status stamp leaves an infrastructure
interceptor for the `Attendee` itself.

## Narrative Consequences

The domain no longer names three types, one site or one window length, and Phase 2 can write a
fresh schema against it. Scaffolding survives on purpose and is scheduled: the single-zone clock
and the transitional-location constant retire in Phase 3, the inherited migration chain and the
stored book-token hash in Task 9, and `inviteOptionCount` becomes editable in Task 12. The charge
and release methods Task 7 adds are not yet called by the booking handlers; Task 10's ordered-lock
helpers are where they are adopted.

---

AI-Fingerprint: sha256:FINGERPRINT
PHASE_1_BODY

MERGE_BASE=$(git merge-base origin/main HEAD)
FINGERPRINT=$(git diff "$MERGE_BASE" HEAD | shasum -a 256 | cut -c1-12)
sed -i '' "s/FINGERPRINT/$FINGERPRINT/" /tmp/phase-1-body.md
node scripts/check-ontology-terms.mjs --also /tmp/phase-1-body.md
gh pr create --base main --title "Phase 1: generalise the domain" \
  --label narrative-required --body-file /tmp/phase-1-body.md
```

On Linux, use `sha256sum` in place of `shasum -a 256`, and `sed -i` without the empty argument.
Recompute the fingerprint and update the body after any further push to the branch, or the
`ai-fingerprint` check fails on the stale value.

Do not merge the pull request yourself: code-owner review and the required checks stand. Once it has
merged, start Phase 2 from a fresh branch cut from the updated `main`.
