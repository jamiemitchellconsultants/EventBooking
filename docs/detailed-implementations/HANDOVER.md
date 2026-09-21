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
| Phase 3 — master Tasks 12–20 | **Written and reviewed, never executed.** Ten documents, Task 20 split into 20a/20b. Hand-authored, so no build or test has ever run against them. Merged through pull request #20 |
| Phase 4 — master Tasks 21–23 | **Written, never executed.** Four documents; master Task 22 split into 22a/22b (see §8). Hand-authored, like Phase 3, so no build or test has ever run against them |
| Phases 5–7 — master Tasks 24–33 | Not authored |

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
| 3 | `phase-3-application.md` | `phase-3a-reference-data-settings.md`, `phase-3b-negotiation.md`, `phase-3c-invite-engine.md`, `phase-3d-booking-cancellation.md`, `phase-3e-recovery-workspace.md`, `phase-3f-staff-authorization.md`, `phase-3g-notification-outbox.md`, `phase-3h-background-jobs.md`, `phase-3i-dashboards-attendees.md`, `phase-3j-audit-search.md` (hand-authored, unexecuted; Task 20 split into 20a/20b) |
| 4 | `phase-4-api-and-mcp.md` | `phase-4a-api-conventions.md` (Task 21), `phase-4b-event-read-models.md` (22a), `phase-4c-endpoint-catalogue.md` (22b), `phase-4d-mcp-parity.md` (23, phase gate) (hand-authored, unexecuted) |

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
- **Hand-authoring has no compiler, and Phase 3 showed what that costs.** Everything in Phases 0–2
  came out of a checkout that built with warnings as errors, so a whole class of defect could not
  reach the documents. Nothing protects a hand-authored phase. Phase 3's review found two methods
  that would have failed the executor's own build, a read model whose shape contradicted the
  requirement it cited, a lock order that trips the guard Task 15 installs, and nine blocks that
  described code instead of being it. Budget a review pass that reads the code as code, and run the
  mechanical sweeps in section 9 before asking anyone to read prose.
- **Phase 4 stays hand-authored; Phase 5 decides for itself.** Tasks 21–23 are endpoint and tool
  wiring over handlers Phase 3 already specifies, so the code is thin and repetitive and the method
  fits. Phase 5 is the web work, where the code is dense and its failure modes are visual, and the
  method is an open question rather than a settled one — section 11 carries the checkpoint. Do not
  let the answer be decided by momentum at the point someone starts writing Task 24.
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

## 6a. The loop for one hand-authored task

Phases 3 to 7 have no prototype, so nothing catches a mistake before the executor does. These steps
replace the compiler, and each one exists because its absence cost something in Phase 3.

1. **Read the requirement the task cites, before writing its tests.** Not the master plan's summary
   of it — the FR itself, and the screen in design 03b if it has one. Task 20a declared a dashboard
   of three tabs that do not exist in FR-13.1, and nothing caught it: its Application tests drove a
   memory fake, and its Infrastructure test was a prose description. Tests written against an
   invented shape agree with it perfectly.
2. **Write the code, not a description of the code.** A fenced block whose contents are all comment
   lines is a description, however the marker line labels it. Nine blocks in Phase 3 said
   `(complete)` and carried none.
3. **Sweep mechanically, while authoring.** Both of these take seconds and both found real defects
   after the fact, which is the expensive time to find them:

````bash
# Every Create:/Modify: entry must have its code somewhere in the document.
# Hand-authored phases only: in Phases 0-2 the code lives in separate edit volumes,
# so a Files entry there legitimately has no code beside it and every one would
# report as a gap.
python3 - <<'GAPS'
import re, glob, os
for f in sorted(glob.glob("docs/detailed-implementations/phase-[3-7]*.md")):
    txt = open(f).read()
    code = "\n".join(re.findall(r'```csharp\n(.*?)```', txt, re.S))
    # EF-generated migrations and the model snapshot are generated and reviewed, not
    # embedded (the Phase 2 convention), so their absence is correct.
    generated = ('<generated-timestamp>', 'EventBookingDbContextModelSnapshot')
    missing = [stem for stem in
               (m.group(2).rsplit('/', 1)[-1][:-3]
                for m in re.finditer(r'^- (Create|Modify): (\S+\.cs)', txt, re.M))
               if stem not in code and not stem.startswith(generated)]
    if missing:
        print(f"{os.path.basename(f)}: {', '.join(missing)}")
GAPS

# No async method may lack an await. CS1998 is a warning, and the solution builds
# with -warnaserror, so one instance stops the executor with an error that has
# nothing to do with their task.
python3 - <<'ASYNC'
import re, glob, os
meth = re.compile(
    r'^(\s*)(?:public|private|internal|protected)(?:\s+(?:sealed|static|override|virtual|new))*'
    r'\s+async\s+(?:Task|ValueTask)[^\n=]*\n(.*?)(?=^\1(?:public|private|internal|protected|\})|\Z)',
    re.S | re.M)
for f in sorted(glob.glob("docs/detailed-implementations/phase-*.md")):
    for b in re.findall(r'```csharp\n(.*?)```', open(f).read(), re.S):
        for m in meth.finditer(b):
            if 'await' not in m.group(2):
                print(f"{os.path.basename(f)}: {re.search(r'(\w+)\s*\(', m.group(0)).group(1)}")
ASYNC
````

A clean sweep only means something if the detector engaged: print how many methods it matched, not
just how many failed. Run over Phases 0–2 the async sweep matches 7,978 methods and clears every
one, which is what a compiler-verified phase should look like and confirms the sweep works.

4. **Resolve anything the document would tell its executor to verify.** They have no repository and
   cannot verify anything. Task 20b's description said to check a column name against the audit
   configuration; the name it assumed was wrong, and the executor had no way to discover that. Read
   the prototype at `/private/tmp/eventbooking-detail.6yx6zE/verify` and write the answer in.
5. **State the counts as expectations.** No build has run, so no figure is observed. Say so in the
   document's own Step 4, and add no row to section 7's table.
6. Then the usual: update the phase overview, this handover and `README.md`, lint the plan
   directory explicitly, stage explicit paths, commit and push.

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

**Phase 3 has no row here, and must not be given one.** Its ten documents are hand-authored: no
build and no test has ever run against them, so any figure would be a guess wearing the same
typeface as eleven measured ones. Each Phase 3 document says so in its own Step 4. The last
measured checkpoint remains Task 11 at 1570, and the next real figure will be whatever an executor
reaches at Task 12.

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

**Phase 3 — the hand-authored phase, and what its review settled**

- **A block labelled complete that contains only comments is not code.** Nine of them read as
  supplied files and were descriptions of files. Two carried unanswered questions in their own
  prose. The label is not evidence; the ratio of code lines to comment lines is.
- **Task 20a's dashboard was the wrong dashboard.** It declared three tabs named Events, Attendees
  and Recovery, each carrying four attendee-status counts. FR-13.1 and design 03b give Awaiting
  availability, No response and Events, each with one row count. Two of its tabs do not exist in
  the design and no tab has four counts. Nothing pinned the semantics because there were none to
  pin: the Application tests used a memory fake and the Infrastructure test was itself a
  description. Reshaped onto the ported row types, which already carry what the requirement names.
- **A location filter narrows the Events tab only.** A `Attendee` awaiting availability has no
  `Event` and therefore no `Location`; filtering them by one needs a relationship the model does
  not have. The description said the filter applied to every tab.
- **Recovery-invite cancellation locks the `Attendee` before the `Invite`.** The description had it
  the other way round, which is a descent once Task 15 puts `Invite` above `Attendee` in the
  ladder. Read the invite unlocked to learn its attendee, lock downwards, re-read under the lock.
- **The workspace list goes through a query, not through every active `Event`.** Its handler was
  calling the repository's list-active with a minimum date and filtering in memory — the shape
  Task 11 removed from the eligibility path at some 680 ms against 18 ms. The exact end bound stays
  in the handler, because no end instant is stored.
- **That query takes no cursor, deliberately.** Paging a list the handler must then re-filter by end
  instant would page on a different set from the one it returns. If the window ever widens enough to
  need paging, page on the widened start instant and keep the end-bound drop where it is.
- **The audit instant is `timestamp`, not `occurred_at`.** Task 20b's description assumed the latter
  throughout and told the executor to verify it — which an executor cannot do, having no
  repository. The keyset index it needs already exists as `ix_audit_log_timestamp` on
  (`timestamp`, `id`) from Task 9b, so Task 20b adds no migration.
- **The audit bucket CASE yields NULL for an unclassified entity type.** With an ELSE of event, any
  entity type added later would silently become readable by anyone holding only ViewEventAudit.

### Transitional constructs, and when each retires

The prototype deliberately carries scaffolding. Each is named in the code and must go at its task:

| Construct | Retires in |
| --- | --- |
| The single-zone clock and its transitional member names | **Not retired in Phase 3, although its table said so.** Task 21 made `Clock:TimeZoneId` optional; the options type and the transitional member survive. Retire with Phase 6's seed rework |
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

### Phase 3 settlements (binding on later tasks)

Settled with the user while authoring Phase 3, each against the master plan, spec or design
wording it overrides:

- **#9 (Task 12).** Invites snapshot the three settings values at issue: `inviteExpiryDays`,
  `maxAutoRetryCount` and `inviteOptionCount` are ontology properties of the invite, written by
  the Task 14 issuer. Later settings changes apply only to future invitations.
- **#5 (Tasks 12, 20).** Task 12 owns reference data and settings only; attendee CRUD, import
  and boundary work stay with Task 20, which is split into 20a (dashboards, attendee list,
  readiness) and 20b (audit search, histories, phase gate).
- **#2 (Task 15).** A replayed confirmation is refused as a conflict naming the existing
  booking — against the master plan's "second confirm returns the same booking" test, which
  Task 15's document replaces with the conflict test.
- **#4 (Task 16).** Recovery handlers demand `ManageAttendees`; workspace handlers and queries
  demand `ConductAppointments` scoped to the caller's type.
- **#3 (Task 17; still open for Task 21).** A missing or malformed `staff_id` is 403 everywhere
  except `/api/me` — against the API catalogue's 401. Task 21's conventions implement the same
  rule at the endpoint layer.
- **#6 (Task 18).** The email log carries the dispatcher's `claimCount`, `notBefore` and
  `correlationId`; Task 18's migration adds them.
- **#7 (Task 18).** Delivery is at-least-once, with the SMTP-crash window stated explicitly;
  duplicates are harmless because links regenerate deterministically.

### Phase 4 settlements (binding on later tasks)

Settled with the user while authoring Phase 4, each against the master plan, spec or design
wording it overrides:

- **#11 (Task 22).** **Master Task 22 splits into 22a and 22b.** Seven of design 05's endpoints
  have no Phase 3 handler behind them — the filtered `Event` and `EventProposal` lists, the
  single-`Event` read, the cancellable-`Event` list, `includeInactive` on the three reference-data
  lists, an update carrying `isActive` where Task 12 splits update from set-active, and the
  capacity route's appointment-type identifier. Task 22 scopes itself as wiring, so the missing
  read side goes into 22a and the endpoint catalogue stays pure in 22b. Both carry the master
  plan's single commit message, as 20a and 20b do.
- **#12 (Task 22b).** **`GET /api/events` follows Task 20b's audit precedent.** Design 05 gives it
  either `ManageEventNegotiation` or `ViewEventOperations`, against "exactly one `StaffCapability`
  per handler". The handler demands neither and filters by whichever the caller holds, with the
  filter enforced in the query rather than only in the handler.
- **#13 (Task 22b).** **`GET /api/attendees/{id}/readiness` demands `ViewAttendeeDashboards`**, per
  design 05, not the ported handler's `ManageAttendees`. The design package wins on detail, and a
  readiness read belongs with the other dashboard reads.
- **#14 (Task 21).** **An expired attendee token returns 410 `token-expired`.** Design 05 and
  design 06 both distinguish it from 404 `token-invalid`; the ported handlers collapsed every
  token failure into one not-found. A token whose signature verifies, whose row resolves and
  whose stored version matches, but whose `Invite` has lapsed, is a link the holder knows they
  were sent. `Used`, `Superseded` and `Cancelled` stay indistinguishable from a forgery.
- **#15 (Task 21).** **`proposal-not-open` becomes a typed error**, and Task 13's three catch
  blocks are repointed at it, so the status travels in the problem body rather than only in the
  message.
- **#16 (Task 21).** **`last-admin` is reachable from the staff-access endpoint**, not from role
  synchronisation, which keeps its silent refusal and its alert: a background reconciliation must
  not fail an unrelated request because of someone else's identity-provider change.
- **#17 (Task 21).** **The error catalogue gains four slugs design 05's table omits** —
  `not-found` (404), `requirement-mismatch` (409), `already-confirmed` (409, which is settlement
  #2 and postdates the design) and `conflict` (409) as the residual generic. Every one of design
  05's own slugs still has a producing error code, which the catalogue test asserts.
- **#18 (Task 21).** **The outbox row's correlation identifier is the request's where one
  exists.** Settlement #6 records the column as the dispatcher's; master Task 21 asks for the
  request's to be propagated in. Both hold once the dispatcher's claim writes
  `COALESCE(correlation_id, @correlationId)`, and the stamp itself is write-once.
- **Two configuration keys design 04's table does not name** are added by Task 21 because design
  06 needs them: `Proxy__Networks`, without which "forwarded headers are trusted only from the
  configured reverse-proxy network" cannot be implemented, and `RateLimiting__TokenPerMinute` and
  `__StaffPerMinute` beside the attendee limit the table does name.

**Found while authoring Task 21, and still open.** Phase 3's transitional-construct table retires
the single-zone clock "when handlers carry a `Location`", but **no Phase 3 document removes it**:
`Clock:TimeZoneId`, the clock options type and the transitional member on the system clock all
survive Phase 3, and the infrastructure registration still asks for them. Task 21 keeps the key,
made optional and defaulting to `Etc/UTC` because design 04's table does not list it. Removing it
belongs with Phase 6's seed rework, and the transitional-constructs table above now says so.

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
- **In a hand-authored phase, run the mechanical sweeps before reading prose.** Two that have
  already caught real defects, both cheap:
  - Every `Create:`/`Modify:` entry in a Files list must have its code somewhere in the document.
    Match the file's stem against the fenced blocks; the ones with no match are the gaps.
  - No async method may lack an `await`. CS1998 is a warning and this solution builds with
    `-warnaserror`, so one instance stops the executor with an error that has nothing to do with
    their task. Two were found this way in Phase 3. The same sweep over Phases 0–2 matched 7,978
    async methods and cleared every one, which is what a compiler-verified phase should look like.
- **Verify a column name against the configuration, not against the prose that names it.** Task
  20b's description said `occurred_at` and was wrong; the audit configuration says `timestamp`. A
  document that tells its executor to "verify before accepting" is telling the one party who cannot
  verify anything — resolve it while authoring, where the prototype is readable.
- **The fingerprint check goes red once on every push, and that red is not yours to fix.** It runs
  on the push, before any body edit can land, and compares against the previous hash. Wait until the
  pull request reports the head you just pushed, then update the body; the re-run passes and the
  earlier failure is superseded. Do not chase the first red.
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

## 10. Contradictions

Put each open one to the user when its task is reached; do not decide it alone. Rows marked
settled point at the binding §8 entry — do not re-raise them. The full original list of
fourteen is in this file's history at commit `2b192ca`.

| # | Contradiction | Task |
| --- | --- | --- |
| 1 | The seed brief has an appointment type that is active but unmanaged, and an inactive type, yet also lists events and proposals that would need them | 28 |
| 2 | **Settled in Phase 3 (§8):** a replayed confirmation is refused as a conflict naming the existing booking | 15 |
| 3 | **Settled in Phase 3 and implemented in Task 21 (§8):** a missing `staff_id` is 403 everywhere except `/api/me`, and `unauthenticated` stays 401 for a missing or invalid bearer token | 17, 21 |
| 4 | **Settled in Phase 3 (§8):** recovery demands `ManageAttendees`, workspace stays under `ConductAppointments` | 16 |
| 5 | **Settled in Phase 3 (§8):** Task 12 owns reference data and settings only; Task 20 is split into 20a/20b | 12, 20 |
| 6 | **Settled in Phase 3 (§8):** the email log carries `claimCount`, `notBefore` and `correlationId` | 18 |
| 7 | **Settled in Phase 3 (§8):** delivery is at-least-once, crash window stated | 18 |
| 8 | Task 32's 500 same-IP confirmations collide with the 30-per-minute attendee rate limit | 32 |
| 9 | **Settled in Phase 3 (§8):** invites snapshot the three settings values at issue | 12 |
| 10 | London and Dublin share an offset, so they cannot demonstrate zone-dependent ordering; use a genuinely different zone such as `Asia/Tokyo` | wherever ordering is proved. **Applied in Task 11**, whose ordering cases pair London with Tokyo, and again in Task 21's event-time contract cases; still open for later tasks that prove an ordering |
| 11 | **Settled in Phase 4 (§8):** master Task 22 splits into 22a and 22b, because seven of design 05's endpoints have no Phase 3 handler | 22 |
| 12 | **Settled in Phase 4 (§8):** `GET /api/events` follows Task 20b's audit precedent for its two capabilities | 22 |
| 13 | **Settled in Phase 4 (§8):** attendee readiness demands `ViewAttendeeDashboards`, per design 05 | 22 |
| 14 | **Settled in Phase 4 (§8):** an expired attendee token returns 410, distinct from 404 | 21 |
| 15 | **Settled in Phase 4 (§8):** `proposal-not-open` becomes a typed error | 21 |
| 16 | **Settled in Phase 4 (§8):** `last-admin` is reachable from staff-access, not from role sync | 21 |
| 17 | **Settled in Phase 4 (§8):** the error catalogue gains four slugs design 05's table omits | 21 |
| 18 | **Settled in Phase 4 (§8):** the outbox correlation identifier is the request's where one exists | 18, 21 |

## 11. Next steps

1. **Phase 2 is done.** All four documents — 9a, 9b, 10 and 11 — are verified in the prototype,
   replayed into the independent checkout at 1570 tests, and merged through pull request #17. There
   is nothing left owing on it.
2. **Phase 3 (Tasks 12–20) is written and reviewed, and has never been executed.** Ten documents,
   with master Task 20 split into 20a and 20b; the mapping from task to document is in section 3's
   table, and the phase overview is `phase-3-application.md`. Pull request #20 is open. What the
   phase settled — seven contradictions, and the eight review findings that reshaped parts of it —
   is in section 8; do not re-derive any of it from this list, which is why this entry no longer
   repeats it. Section 7 says why the phase has no test figures and must not be given any.

   Pull request #20 has merged, along with #23, which closed the seven Files entries that carried
   no code. The EF migrations in Tasks 12, 18 and 20a are generate-and-review by the Phase 2
   convention rather than embedded, which is correct and not a gap.

3. **Phase 4 (Tasks 21–23) is written, and stayed hand-authored.** Follow section 6a's loop, and
   run both of its sweeps per task rather than per phase. The phase overview is
   `phase-4-api-and-mcp.md`.

   - **Task 21 is written** (`phase-4a-api-conventions.md`). It implements contradiction #3's
     403 rule at the endpoint layer and settles five more, all recorded in section 8 as #14 to
     #18. It also found, and did not fix, the single-zone clock Phase 3's table claimed to
     retire; that is recorded in section 8 too.
   - **Task 22a is written** (`phase-4b-event-read-models.md`). It adds the filtered `Event`
     and `EventProposal` reads, the single-`Event` read, the cancellable list, `includeInactive`
     on the three reference-data lists, activation folded into the reference-data update, the
     appointment-type identifier on the capacity adjustment, and settlements #12 and #13. It
     deletes Task 12's three set-active handlers, which nothing else calls.
   - **Task 22b is written** (`phase-4c-endpoint-catalogue.md`). Fifty-five operations across
     fifteen endpoint files, over one shared catalogue that the OpenAPI document, the `/api`
     index, `_links` and Task 23's tool list all read from. Its catalogue test parses design
     05's own tables: forty-five staff operations with a tool, ten anonymous or token routes
     without one, and `GET /metrics` as the single route design 05's tables do not name.
   - **Task 23 is written** (`phase-4d-mcp-parity.md`), and carries the phase's pull-request
     gate. Forty-five tools over the same handlers, with names, descriptions and hints read
     from the shared catalogue so the two surfaces cannot describe themselves differently.

   Phase 4 is therefore complete as authored and has never been executed. Its pull request has
   not been opened: the phase branch is `claude/phase-4-api-and-mcp`, and Task 23's Step 6
   carries the gate, the narrative requirements and the fingerprint recipe.

4. **Decide the method for Phase 5 before writing Task 24, and put the decision to the user.** Phase
   4 is thin wiring over handlers that already exist, which is why it stays hand-authored. Phase 5
   is the web work: dense components whose failure modes are visual, where neither of section 6a's
   sweeps helps much and a reviewer reading markup cannot tell a working page from a plausible one.
   The options are to carry on hand-authoring and budget a heavier review, or to bring the prototype
   back for Phase 5 and generate its documents from before/after snapshots the way Phases 0–2 were
   built. The prototype still exists and is green at Task 11, so the second is available rather than
   theoretical — but it would first have to be brought forward through Tasks 12–23. Weigh that cost
   against what a phase of unverified bUnit components is worth. This is a user decision, not an
   authoring one.
5. **Phases 6–7 (Tasks 28–33)** inherit whatever Phase 5 settles.
6. Several transitional constructs come due across Phase 3 and in Phase 6's seed rework. Read
   section 8's table before starting any of them; Task 15 in particular inherits three separate
   debts — the booking handler adopting the lock helpers, the lock ladder gaining its `Invite` and
   `Booking` levels, and Task 7's charge and release methods finally being called.

Do not claim the assignment is complete while Tasks 22–33 are unwritten. The size of Phase 0 is not
evidence of progress through the rest.
