# Authoring handover — EventBooking detailed implementation plans

Updated 20 September 2026, after Phase 2 Task 11. Written for an agent starting with no context:
read this file, then the governing inputs it lists, before touching anything.

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
| Phase 2 — master Tasks 9–11 | **Complete.** Tasks 9a, 9b, 10 and 11 written, replayed and merged. Prototype-verified, like Phases 0 and 1 |
| Phases 3–7 — master Tasks 12–33 | Task 12 authored (hand-authored, unexecuted). Tasks 13–33 not authored |

Phases 0, 1 and 2 are all **merged into `main`**: pull request #15 with its narrative proposal #16
for Phases 0 and 1, and pull request #17 with its narrative proposal #18 for Phase 2. Nothing is
outstanding on either branch. Phase 3 starts from a fresh branch off `origin/main`.

Two things about that merge are worth carrying forward, because both cost a cycle here:

- **A phase branch is finished when its pull request merges.** A later commit pushed to it goes
  nowhere — it is not on `main`, and the merged pull request will not pick it up. Anything found
  after the merge needs a fresh branch off the updated `main` and a pull request of its own. This
  entry is one.
- **Recompute the AI-fingerprint and update the pull-request body after every push to the branch**,
  or the `ai-fingerprint` check fails on the stale hash. Editing the body immediately after a push
  can still fail, because the check re-runs on the `edited` event against whichever head GitHub has
  registered, which lags the push by a few seconds. Wait until the pull request reports the head
  you just pushed, then update the body.

```bash
MERGE_BASE=$(git merge-base origin/main HEAD)
git diff "$MERGE_BASE" HEAD | shasum -a 256 | cut -c1-12
```

Documents written so far:

| Phase | Overview | Task documents |
| --- | --- | --- |
| 0 | `phase-0-port-and-strip.md` | `phase-0a-import.md` + `phase-0a-files.md` + 83 source volumes; `phase-0b-vocabulary.md` + 115 edit volumes; `phase-0c-identity.md`; `phase-0d-retire-import.md`; `phase-0e-required-groups.md`; `phase-0f-retired-location-config.md`, each with their own edit volumes |
| 1 | `phase-1-domain.md` | `phase-1a-event-window.md`, `phase-1b-reference-data.md`, `phase-1c-negotiation.md`, `phase-1d-capacity.md`, `phase-1e-invites.md`, each with edit volumes |
| 2 | `phase-2-persistence.md` | `phase-2a-attendee-tokens.md` + 32 edit volumes; `phase-2b-fresh-schema.md` + 26 edit volumes; `phase-2c-ordered-locks.md` + 2 edit volumes; `phase-2d-invite-eligibility.md` + 16 edit volumes |

`README.md` is the entry point for an executor. `phase-0-port-and-strip.md` is the model for a
phase overview: task order, evidence table, review checklist, pull-request gate.

## 4. Working locations

| Path | What it is |
| --- | --- |
| `/private/tmp/eventbooking-detail.6yx6zE` | Authoring scratch: generators, snapshots, build and test logs |
| `/private/tmp/eventbooking-detail.6yx6zE/verify` | **The prototype.** At Task 10, green |
| `/private/tmp/eventbooking-plan-replay.MkPSRw` | **Independent replay checkout.** At Task 10, green. It has a symlink `docs/detailed-implementations` to the plan directory of whichever checkout is being authored in — repoint it if you work in a different worktree |
| `/Users/jamesmitchell/.codex/handoffs/eventbooking-detailed-plans-2026-09-20/` | Archives of the first authoring session, plus the original request |

If the scratch directories are gone, extract `authoring-scratch.tar.gz` and
`replayed-checkpoint.tar.gz` from the archive directory into **new empty** directories and fix the
hard-coded paths in the generators. Use the physical `/private/tmp` paths, never `/tmp`: mixing the
two aliases produced duplicate MSBuild graph errors.

Snapshots are JSON maps of repository-relative path to file contents: `task-1.json` …
`task-8.json`, then `task-9a.json`, `task-9b.json`, `task-10.json` and `task-11.json`. A generator
diffs two snapshots to produce one task's edit volumes.

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
with Docker running and no skipped tests. Tasks 1–11 were each additionally replayed from their own
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
| Task 9a | 360 | 424 | 174 | 232 | 35 | 241 | 75 | 1541 |
| Task 9b — end of master Task 9 | 360 | 424 | 176 | 232 | 35 | 241 | 75 | 1543 |
| Task 10 | 360 | 424 | 190 | 232 | 35 | 241 | 75 | 1557 |
| Task 11 — end of Phase 2 | 360 | 421 | 206 | 232 | 35 | 241 | 75 | 1570 |

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

**Task 9a — deterministic attendee links**

- **Master Task 9 is split into two documents**, 9a (the token) and 9b (the schema squash), the way
  Phase 0 split master Task 3. The user's decision that the fresh schema writes the counter once is
  what fixes the order: the token change comes first, and 9b deletes the whole inherited chain
  including the short-lived migration 9a adds.
- **A resend reuses the current link.** Design 06 says so, and a deterministic token is what makes
  it possible. The predecessor rotated on every retry because it had thrown the raw token away. The
  retry path no longer touches the counter, and the tests that asserted rotation now assert that it
  does not move.
- **Attendee lookups are primary-key reads.** Verify the signature, load by the identifier the token
  carries, then require that the row's stored version matches. The four hash-keyed repository
  methods are gone, along with the two unique hash indexes; the two remaining manage-token reads
  take a booking id.
- **Only the canonical base64url spelling verifies.** The token's last character carries two
  significant bits, so three other spellings decode to the same bytes; accepting them would make one
  link answer to four URLs.

**Task 9b — the fresh schema**

- **Seed rows live in the model, not in migration SQL.** The predecessor inserted the appointment
  types, the attendee groups and their requirement mappings from raw SQL inside named migrations.
  They are in the model's seed data now, so the single migration stays regenerable. The transitional
  `Location` is seeded with them.
- **The roles script runs before the migration, and its absence is not fatal.** The migration grants
  to roles `Persistence/Sql/roles.sql` creates, and skips the grants when they are not there, so a
  database migrated without the script is still valid. Both roles are `NOLOGIN`: a deployment
  attaches its own login role, and a test reaches them with `SET ROLE`.
- **`start_utc` is nullable until Task 11**, which is where the repository computes and writes it.
  A non-nullable column takes EF's `0001-01-01` for every row nothing has computed, and the
  eligibility query filters on it — a wrong instant hides the event instead of failing.
- **The capacity check is the master plan's, not the predecessor's.** It is renamed to
  `ck_event_capacity_bounds` and gains the missing `total_headcount > 0`; the predecessor's
  constraint allowed a total of zero.
- **The five inherited-migration guard tests are gone** with the chain they pinned. What they
  protected is asserted directly by the fifteen schema tests, against a real PostgreSQL 16.

**Task 10 — ordered row locks**

- **The order lives in one class.** Every row lock goes through `Persistence/Locking/`, including
  the repository methods earlier handlers already call, so those handlers gained the guard without
  being touched — and none of them trips it.
- **The guard is on in Debug, off in a released build.** The tracker records the level either way.
  A lock order no test has reached should not become a 500 for whoever hits it first.
- **Only the four documented levels are tracked.** `Invite` and `Booking` locks sit between the
  attendee and the event in the real handlers, but the design names four and this task builds four.
  **Extending the ladder is Task 15's**, when the booking handler adopts the helpers — and that is
  also where Task 7's charge and release methods are finally called.
- **The obvious two-event deadlock test does not work.** With the event locks in place, two
  attempts naming the same events in opposite orders serialise on the event rows before reaching a
  capacity row, so that test passes whether or not the rows are ordered. The scenario that pins the
  ordering is the same shuffle with the event locks removed; it was verified by deleting the
  domain's capacity ordering function and watching it deadlock.

**Task 11 — the eligibility query**

- **The rule moved into the database, and the Application tests moved with it.** Eight cases in the
  eligible-event suite asserted the selection rule against objects in memory. They are gone: eleven
  cases assert it against a real PostgreSQL instead, and what is left on the Application side is
  five cases about the adapter. That is the whole of the Application count's drop from 424 to 421.
- **The port returns identifiers, not aggregates.** The finder hydrates them through the repository
  and keeps the order the query chose. A query that only proposes candidates must not look like a
  read of authoritative state, and capacity is re-checked under lock at booking time regardless.
- **The design's statement needed one change and one guard.** `status` is stored as an integer by
  this model, not the string design 04 writes, so the comparison is against the integer. And the
  required type identifiers are de-duplicated before the statement runs: `cardinality` on a
  parameter with a repeat exceeds anything the group could count, and would reject every `Event`.
- **`start_utc` has two writers and one rule.** The repository computes it as it adds the `Event`,
  which is what the master plan asks for; the context fills in any unstamped `Event` at save time,
  because the demo seeder and some thirty test sites add `Event`s straight through the context and
  a derived not-null column cannot depend on which path inserted the row. Both call one function.
  It is normalised to UTC there: the resolver answers with the `Location`'s own offset, and
  PostgreSQL refuses any offset but zero for a timestamp with time zone.
- **The column's CLR type stays nullable although the column is not.** That is what makes an
  unstamped row distinguishable from one stamped with a default, which is what the save-time
  backstop looks for.
- **A reset must restore the transitional `Location`.** It is seeded by the migration, and both the
  PostgreSQL test fixture and the demo reseeder truncated it away without putting it back. Nothing
  needed it before; every derived start instant does.
- **The phase now has two migrations, and the initial one is not rewritten.** It is committed, and
  a chain is what keeps it regenerable. The schema test that asserted a single migration asserts
  both, by name.
- **The master plan's performance scenario proves nothing as written.** Two thousand active events
  and nothing else is a table where every row qualifies, and PostgreSQL is right to scan it
  sequentially — so the index is never used and the EXPLAIN assertion cannot hold. The suite seeds
  those two thousand among forty-nine thousand finished, cancelled and other-site events, and the
  plan then uses `ix_event_eligibility`.
- **The two performance assertions are not two ways of saying the same thing.** Verified by making
  each fail. Dropping the index turns the bitmap scan into a sequential one and fails the EXPLAIN
  case; the p95 case still passes at 17 ms, because at this size the scan of the `Event` table is
  not where the time goes. What the budget catches is the in-memory shape this task replaced, which
  takes some 680 ms on the same data — more than thirteen times the budget.
- **The budget test is tagged, not skipped.** `Trait("Category", "Performance")` lets a run exclude
  it with `--filter "Category!=Performance"`. It still runs in the full suite, because a budget
  nothing ever runs is not a budget, and because this project's checkpoints require zero skipped
  tests.

### Transitional constructs, and when each retires

The prototype deliberately carries scaffolding. Each is named in the code and must go at its task:

| Construct | Retires in |
| --- | --- |
| The single-zone clock and its transitional member names | Phase 3, when handlers carry a `Location` |
| The predecessor's fixed appointment-type identifiers and seeded rows | Phase 3 |
| The seeded transitional `Location` row, and `event.location_id` without a foreign key to it | Phase 3 |
| `start_utc` nullable, because nothing computes it yet | Retired in Task 11 |
| The inherited migration chain and its guard tests | Retired in Task 9b |
| The stored book-token hash, in place of design 06's `tokenVersion` | Retired in Task 9a |
| `inviteOptionCount` stored but not editable | Task 12 |
| The transitional-location constant in the negotiation and capacity handlers | Task 13 |
| Invites restricted to the transitional location, because no command carries a Coordinator's selection | Task 14, whose InviteAttendee takes location ids. The eligibility port already takes a location set; the adapter passes the one identifier |
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
| 10 | London and Dublin share an offset, so they cannot demonstrate zone-dependent ordering; use a genuinely different zone such as `Asia/Tokyo` | wherever ordering is proved. **Applied in Task 11**, whose ordering cases pair London with Tokyo; still open for later tasks that prove an ordering |

## 11. Next steps

1. **Phase 2 is done.** All four documents — 9a, 9b, 10 and 11 — are verified in the prototype,
   replayed into the independent checkout at 1570 tests, and merged through pull request #17. There
   is nothing left owing on it.
2. **Phases 3–7 (Tasks 12–33)** are hand-authored: complete code and tests written straight into the
   documents, no prototype. Task 12 (reference-data and settings handlers) is written as
   `phase-3a-reference-data-settings.md` with the phase overview `phase-3-application.md`; its
   counts are expectations for the executor, not observed figures. Contradictions #5
   (reference-data only; attendee work stays with Task 20 splits) and #9 (settings snapshot
   fields added to the ontology's invite) were settled with the user before writing.
3. Several transitional constructs now come due in Phase 3 and in Tasks 12–15. Read section 8's
   table before starting any of them; Task 15 in particular inherits three separate debts — the
   booking handler adopting the lock helpers, the lock ladder gaining its `Invite` and `Booking`
   levels, and Task 7's charge and release methods finally being called.

Do not claim the assignment is complete while Tasks 12–33 are unwritten. The size of Phase 0 is not
evidence of progress through the rest.
