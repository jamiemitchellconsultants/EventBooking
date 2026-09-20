# Authoring handover — EventBooking detailed implementation plans

Updated 20 September 2026, after Phase 1 Task 8, which completes the phase. Written for an agent
starting with no context: read this file, then the governing inputs it lists, before touching
anything.

## 1. What the assignment is

Write executable plan documents under `docs/detailed-implementations/` that let a **small local
model** (opencode + superpowers + Qwen3.6 27B at Q6) build EventBooking one task at a time. The
executor will not have the predecessor repository, so every task carries complete code, complete
tests and exact before/after edits. No placeholders, and no instruction to go and fetch a file.

The original request, in full, is at
`/Users/jamesmitchell/.codex/handoffs/eventbooking-detailed-plans-2026-09-20/original-request.txt`.

**This repository stays documentation-only.** EventBooking itself is never built here. All
executable work happens in scratch checkouts under `/private/tmp` (section 4).

## 2. Governing inputs, in reading order

1. `AGENTS.md` — binding. Ontology protocol, narrative protocol, AI-fingerprint protocol, git rules.
2. `docs/superpowers/specs/2026-09-19-eventbooking-design.md` — the decision record (D1–D15).
3. `docs/design/00` … `09` — the full design package.
4. `docs/superpowers/plans/2026-09-19-eventbooking-implementation.md` — the master plan: 33 tasks
   in 8 phases. Task numbers, phase boundaries and commit messages come from here and never change.
5. `docs/ontology.md` — canonical names, generated from `docs/ontology.ttl`.

Precedence: the spec wins on decisions, the design package wins on detail, the ontology wins on
names. The master plan says what each task is; this handover says how far it has been written.

## 3. Where the work stands

| Part | State |
| --- | --- |
| Phase 0 — master Tasks 1–3, split 1, 2, 3a, 3b, 3c, 3d | **Complete.** Written, verified and replayed |
| Phase 1 — master Tasks 4–8 | **Complete.** Tasks 4–8 written, replayed, and the pull-request gate is in the phase overview |
| Phase 2 — master Tasks 9–11 | Not authored. Prototype-verified, like Phases 0 and 1 |
| Phases 3–7 — master Tasks 12–33 | Not authored. Hand-authored, no prototype |

Branch `docs/detailed-implementations`, ten commits ahead of `origin/main`, all pushed, with an
open pull request covering what is authored so far. Nothing is merged. **Recompute the
AI-fingerprint and update the pull-request body after every further push to this branch**, or the
`ai-fingerprint` check fails on the stale hash:

```bash
MERGE_BASE=$(git merge-base origin/main HEAD)
git diff "$MERGE_BASE" HEAD | shasum -a 256 | cut -c1-12
```

Documents written so far:

| Phase | Overview | Task documents |
| --- | --- | --- |
| 0 | `phase-0-port-and-strip.md` | `phase-0a-import.md` + `phase-0a-files.md` + 83 source volumes; `phase-0b-vocabulary.md` + 115 edit volumes; `phase-0c-identity.md`; `phase-0d-retire-import.md`; `phase-0e-required-groups.md`; `phase-0f-retired-location-config.md`, each with their own edit volumes |
| 1 | `phase-1-domain.md` | `phase-1a-event-window.md`, `phase-1b-reference-data.md`, `phase-1c-negotiation.md`, `phase-1d-capacity.md`, `phase-1e-invites.md`, each with edit volumes |

`README.md` is the entry point for an executor. `phase-0-port-and-strip.md` is the model for a
phase overview: task order, evidence table, review checklist, pull-request gate.

## 4. Working locations

| Path | What it is |
| --- | --- |
| `/private/tmp/eventbooking-detail.6yx6zE` | Authoring scratch: generators, snapshots, build and test logs |
| `/private/tmp/eventbooking-detail.6yx6zE/verify` | **The prototype.** At Task 8, green |
| `/private/tmp/eventbooking-plan-replay.MkPSRw` | **Independent replay checkout.** At Task 8, green. It has a symlink `docs/detailed-implementations` to the plan directory of whichever checkout is being authored in — repoint it if you work in a different worktree |
| `/Users/jamesmitchell/.codex/handoffs/eventbooking-detailed-plans-2026-09-20/` | Archives of the first authoring session, plus the original request |

If the scratch directories are gone, extract `authoring-scratch.tar.gz` and
`replayed-checkpoint.tar.gz` from the archive directory into **new empty** directories and fix the
hard-coded paths in the generators. Use the physical `/private/tmp` paths, never `/tmp`: mixing the
two aliases produced duplicate MSBuild graph errors.

Snapshots are JSON maps of repository-relative path to file contents: `task-1.json` …
`task-8.json`. A generator diffs two snapshots to produce one task's edit volumes.

## 5. Method, as the user settled it

- **Phases 0, 1 and 2 are prototype-verified.** Implement the task in the prototype, run the full
  suite, generate the documents from before/after snapshots, then replay those documents into the
  independent checkout and run the full suite there.
- **Phases 3 to 7 are hand-authored.** Complete code and tests written straight into the documents,
  no prototype, because the executing model compiles and test-drives them itself.
- **Every contradiction between the spec, the design package and the master plan goes to the user**
  as it comes up (section 8). Do not settle one quietly.
- Test-driven throughout: write the failing test, watch it fail for the right reason, implement,
  watch it pass, run the whole suite.

## 6. The loop for one prototype-verified task

```bash
cd /private/tmp/eventbooking-detail.6yx6zE/verify
# 1. Write the failing tests, run them, confirm they fail for the intended reason.
dotnet test tests/EventBooking.Domain.Tests --filter "FullyQualifiedName~YourNewTests"
# 2. Implement. Then the whole suite, with warnings as errors.
dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
# 2a. Only if the task changes the schema. Read the generated migration before accepting it.
dotnet ef migrations add <Name> --project src/EventBooking.Infrastructure \
  --startup-project src/EventBooking.Api
# 3. Snapshot the prototype.
cd /private/tmp/eventbooking-detail.6yx6zE && node snapshots.mjs task-N
# 4. Add task N's entry to task-configs.json (copy the shape of an existing entry), then package.
node pack-task.mjs N
# 5. Replay the generated document in the independent checkout, from its own payload.
cd /private/tmp/eventbooking-plan-replay.MkPSRw
python3 - <<'EXTRACT'
import re
doc = open('docs/detailed-implementations/phase-1e-invites.md').read()   # the new document
m = re.search(r"<<'TASK_PAYLOAD'\n(.*?)\nTASK_PAYLOAD", doc, re.S)
open('.replay.mjs', 'w').write(m.group(1))
EXTRACT
node .replay.mjs && rm .replay.mjs
dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
# 6. Update the phase overview and this handover, lint, stage explicitly, commit, push.
```

Generators, in `/private/tmp/eventbooking-detail.6yx6zE`:

- `snapshots.mjs` — `walk`, `add`, `snapshot`. It no longer shells out to `apply_patch`: that
  binary belonged to the first session's harness, does not exist here, and is what stalled Task 3c.
  It writes files directly and refuses to overwrite.
- `pack-task.mjs` + `task-configs.json` — the generalised packager, used from Task 4 onward. One
  JSON entry per task supplies the prose, the context block, the interfaces, the test files, the
  red-state description, the counts and the commit message. Its `out` constant is the absolute path
  of the plan directory it writes into; point it at whichever checkout you are authoring in before
  running it.
- `pack-retirement.mjs` — the older Phase 0 packager for Tasks 3a–3d. Leave it alone.
- The snapshot JSONs double as a recovery point. Any file can be restored to its state at the end
  of task N with
  `node -e "require('fs').writeFileSync('verify/'+p, require('./task-N.json')[p])"`, which is how
  a model snapshot dirtied by a discarded migration gets put back.
- Transformation scripts (`retire-location-config.mjs`, `widen-event-window.mjs` and so on) are
  one-shot records of what each task did. **Never re-run one against the current prototype.**

## 7. Verification evidence

Every figure is a full `dotnet build EventBooking.sln -warnaserror` followed by every test project,
with Docker running and no skipped tests. Tasks 1–8 were each additionally replayed from their own
documents into the independent checkout.

| Checkpoint | Domain | Application | Infrastructure | API | MCP | Web | Seed | Total |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Unmodified predecessor | 230 | 451 | 158 | 225 | 34 | 251 | 76 | 1425 |
| Task 1 | 230 | 451 | 157 | 226 | 34 | 251 | 75 | 1424 |
| Task 2 | 230 | 451 | 157 | 228 | 35 | 251 | 75 | 1427 |
| Task 3a | 241 | 451 | 160 | 232 | 35 | 251 | 75 | 1445 |
| Task 3b | 235 | 423 | 160 | 231 | 35 | 245 | 75 | 1404 |
| Task 3c | 237 | 423 | 160 | 232 | 35 | 242 | 75 | 1404 |
| Task 3d — end of Phase 0 | 237 | 422 | 160 | 232 | 35 | 241 | 75 | 1402 |
| Task 4 | 258 | 422 | 173 | 232 | 35 | 241 | 75 | 1436 |
| Task 5 | 289 | 422 | 173 | 232 | 35 | 241 | 75 | 1467 |
| Task 6 | 306 | 422 | 173 | 232 | 35 | 241 | 75 | 1484 |
| Task 7 | 320 | 424 | 173 | 232 | 35 | 241 | 75 | 1500 |
| Task 8 — end of Phase 1 | 358 | 424 | 175 | 232 | 35 | 241 | 75 | 1540 |

A count that does not match after a task is a signal to read the diff, not to adjust the number.

## 8. Decisions already taken

Settled by the user, and binding:

1. **Who may withdraw a proposal.** `proposerAppointmentTypeId` is part of `EventProposal` in
   `docs/ontology.ttl`, with the invariant that withdrawing the proposal and the proposer's own
   acceptance are judged against that type, never against `createdByManagerUserId`. Implemented in
   Task 6; Task 9's fresh schema carries the column.
2. **Lock order versus the cancellation flow.** The documented order wins: `Attendee`,
   `EventProposal`, `Event`, `EventCapacity`. Event cancellation reads the affected attendee
   identifiers without locks, then takes each booking's locks in that order and re-validates under
   lock. Applied in Task 7: the sequence diagram in `docs/design/03b-screens-and-flows.md` and the
   cross-aggregate row in `docs/design/01-domain-model.md`, both of which locked the event first,
   are corrected.

Taken while authoring, and binding on later tasks. Each is grouped under the task that settled it,
so a later task can see what it inherits without re-reading the whole list.

**Task 4 — the event window**

- The time-zone abstraction the domain calls is declared in the **domain** project, not the
  application project as design 04 lists it, because domain rules depend on its answers and
  dependencies point inward. Infrastructure implements it with NodaTime.

**Task 6 — negotiation**

- The predecessor's Admin fallback for withdrawing a proposal is removed: FR-2.9 judges withdrawal
  by the proposing type and Admin holds no negotiation capability. The null-scope gate (FR-10.7)
  therefore arrives early in the negotiation handlers.
- A proposal that is no longer `Open` produces a **conflict** carrying its current status, per
  FR-2.11 and the `proposal-not-open` error, where the predecessor returned a validation error.
- One proposal builder is shared by every test project, linked from `tests/TestSupport/` with a
  global using, because proposals now need a location, a listed type set and a proposing type. It
  has since grown a location constant and a cancel-before-start helper; add to it rather than
  reinventing either in a suite.

**Task 7 — capacity**

- A headcount adjustment below the type's active-booking count is a **returned outcome** carrying
  the minimum the row would accept, per FR-3.6, while a non-positive total or one above 1000 still
  throws. A malformed request and a decision the domain is reporting are different results.
- Cancelling an `Event` is judged on the window's **start instant**, not its date. The zone is the
  transitional location's until Phase 3.
- The charge and release methods are domain API the booking and cancellation handlers do not call
  yet; they still work on the rows their repository locked. **Task 10's ordered-lock helpers are
  where those handlers adopt them** — do not rework the handlers earlier.

**Task 8 — invites and the attendee lifecycle**

- **The book token stays a stored hash until Task 9.** The master plan puts design 06's
  `tokenVersion` counter in Task 8; the user settled that it moves to Task 9 instead, where the
  fresh schema writes the column once alongside the HMAC token service. The predecessor's hash is
  load-bearing in 62 files, and doing it in Task 8 would rewrite them twice.
- The legal `AttendeeStatus` moves live in the aggregate as a set, exposed through a pure
  predicate. **Callers ask the set rather than restating the rule**: that is how the invite issuer
  tells FR-5.4's parked attendee from FR-5.7's failed re-issue.
- `NotYetInvited` to `NotYetInvited` is **not** a legal move. Design 01's last row reads "any
  except `Booked`", which would admit it, but the table lists *changes*, and the only self-move it
  names is `Invited` to `Invited` for an automatic re-issue. Task 14 relies on this.
- statusChangedAt is the aggregate's own property, stamped from an instant the caller supplies.
  The predecessor's save-changes interceptor and EF shadow property are retired; the column and its
  inherited migration are unchanged.
- An invited `Attendee` never drops back to `AwaitingAvailability`. The inherited expiry path did,
  and is corrected to `NoResponseNeedsFollowUp` per FR-5.7 — which is what Task 14's own test list
  then expects.

### Transitional constructs, and when each retires

The prototype deliberately carries scaffolding. Each is named in the code and must go at its task:

| Construct | Retires in |
| --- | --- |
| The single-zone clock and its transitional member names | Phase 3, when handlers carry a `Location` |
| The predecessor's fixed appointment-type identifiers and seeded rows | Phase 3 |
| The inherited migration chain and its guard tests | Task 9 |
| The stored book-token hash, in place of design 06's `tokenVersion` | Task 9 |
| `inviteOptionCount` stored but not editable | Task 12 |
| The transitional-location constant in the negotiation and capacity handlers | Task 13 |
| Invites restricted to the transitional location, because no command carries a Coordinator's selection | Task 14, whose InviteAttendee takes location ids |
| The invite's fixed three options, in place of the stored `inviteOptionCount` | Task 14 |
| Event cancellation reading its zone from the transitional-location constant | Phase 3 |

## 9. Standing rules that have bitten already

- **Check whether the column already exists before adding one.** Task 8's statusChangedAt looked
  like a new field and a new migration; the predecessor already had `status_changed_at`, written
  from a save-changes interceptor through an EF shadow property. Grep the configurations and the
  migrations for the column name first. A property the domain should own but infrastructure writes
  behind its back is a mapping change, not a schema change.
- **A `HasOne<T>()` to an unmapped aggregate invents a table.** Task 8's first `invite_location`
  migration silently created a second `Location` table, because the `Location` aggregate Task 5
  added to the domain is not persisted until Task 9. Read every generated migration before
  accepting it. If one has to be discarded, `dotnet ef migrations remove` needs a live database and
  will fail here: delete the two migration files by hand, then restore
  `EventBookingDbContextModelSnapshot.cs` from the previous task's snapshot JSON before
  regenerating, or the next migration diffs against a dirty model.
- **EF migration defaults are wrong by default.** Every task that added a column got a generated
  backfill of `0`, `false` or an empty GUID, each of which misdescribes rows that already exist.
  Set the true historical value, then drop the column default with a raw SQL statement so new rows
  must state their own. Where no true value exists — Task 6's proposing type — make the migration
  **refuse** rather than invent one.
- **The ontology checker only sees tracked Markdown.** Lint the plan directory explicitly before
  staging:

```bash
node --input-type=module <<'LINT_PLANS'
import fs from 'node:fs';
import {execFileSync} from 'node:child_process';
const directory = 'docs/detailed-implementations';
const files = fs.readdirSync(directory).filter(name => name.endsWith('.md')).map(name => directory + '/' + name);
process.stdout.write(execFileSync('node', ['scripts/check-ontology-terms.mjs', '--also', ...files], {encoding:'utf8', maxBuffer:1e7}));
LINT_PLANS
```

- **In plan prose, backtick a PascalCase word only if the ontology defines it.** Class, test and
  file names go in plain text or inside fenced code blocks; the checker skips fenced blocks.
- **Stage explicit paths.** Never `git add -A`. Review `git diff --cached --name-only` and
  `git diff --cached` before committing. Leave `.DS_Store` files alone; they are untracked noise.
- **Never push to `main`, never merge.** One commit per task on the documentation branch.
- End commit messages with the co-author line the executing harness requires.
- If a domain concept changes, edit `docs/ontology.ttl`, run `node scripts/build-ontology.mjs`, and
  commit both files with the change.

## 10. Contradictions still open

Put each to the user when its task is reached; do not decide it alone. The full original list of
fourteen is in this file's history at commit `2b192ca`.

| # | Contradiction | Task |
| --- | --- | --- |
| 1 | The seed brief has an appointment type that is active but unmanaged, and an inactive type, yet also lists events and proposals that would need them | 28 |
| 2 | The used-token table versus FR-6.5 idempotency: a replayed confirmation must return the existing booking, not write a second one | 15 |
| 3 | A missing `staff_id` is 401 in the API catalogue and 403 everywhere except `/api/me` in the security document | 17, 21 |
| 4 | Task 16's workspace capability wording conflicts with Coordinator recovery needing `ManageAttendees` | 16 |
| 5 | Attendee CRUD, import and boundary work is under-allocated between Tasks 12 and 20; Task 20 probably needs lettered splits | 12, 20 |
| 6 | `EmailLog` lacks the outbox attempt, backoff and correlation fields the design's dispatcher needs, and the cancellation replacement context | 18 |
| 7 | An SMTP crash after send but before marking sent cannot give exactly-once delivery; the design should say at-least-once | 18 |
| 8 | Task 32's 500 same-IP confirmations collide with the 30-per-minute attendee rate limit | 32 |
| 9 | Settings that apply only to future invitations may need snapshot fields the ontology does not define | 12 |
| 10 | London and Dublin share an offset, so they cannot demonstrate zone-dependent ordering; use a genuinely different zone such as `Asia/Tokyo` | wherever ordering is proved |

## 11. Next steps

1. **Phase 2 (Tasks 9–11)** stays prototype-verified: the fresh schema — which also lands design
   06's `tokenVersion` in place of the inherited token hash — the ordered-lock helpers and
   concurrency harness, and the relational-division eligibility query.
2. **Phases 3–7 (Tasks 12–33)** are hand-authored.
3. The plan branch's pull request is already open and carries the three narrative headings and the
   `narrative-required` label. Keep its body's fingerprint current as later phases land on the
   branch, and extend its narrative sections as further decisions are settled.

Do not claim the assignment is complete while Tasks 9–33 are unwritten. The size of Phase 0 is not
evidence of progress through the rest.
