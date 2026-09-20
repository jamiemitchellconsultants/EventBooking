# Authoring handover — EventBooking detailed implementation plans

Updated 20 September 2026. This is an authoring handover: the assignment is to write the plan
documents under `docs/detailed-implementations/`, not to implement EventBooking in this repository.
The repository stays documentation-only; all executable work happens in scratch checkouts.

## Where the work stands

| Part | State |
| --- | --- |
| Phase 0 (master Tasks 1–3, split 1, 2, 3a–3d) | **Complete, verified, committed and pushed.** Every checkpoint was replayed from its own documents into an independent checkout |
| Phase 1 (master Tasks 4–8) | Tasks 4, 5 and 6 complete, packaged and replayed. Tasks 7 and 8 not started |
| Phases 2–7 (master Tasks 9–33) | Not authored |

Branch `docs/detailed-implementations` in `/Users/jamesmitchell/RiderProjects/EventBooking`, pushed
to `origin`. No pull request has been opened for the plans themselves yet.

## Method, as the user settled it

- **Phases 1 and 2 are prototype-verified** like Phase 0: implement the task in a scratch checkout,
  run the full suite, generate documents with checksummed before/after payloads, then replay those
  documents into an independent checkout.
- **Phases 3 to 7 are hand-authored**: complete code and tests written into the documents without a
  prototype, because the executing model compiles and test-drives them itself.
- **Every contradiction between the spec, the design package and the master plan is put to the
  user** as it comes up, rather than decided quietly. Two are already queued below.

## Working locations

| Path | What it is |
| --- | --- |
| `/private/tmp/eventbooking-detail.6yx6zE` | Authoring scratch: generators, snapshots, logs |
| `/private/tmp/eventbooking-detail.6yx6zE/verify` | The prototype. Currently at Task 6, green |
| `/private/tmp/eventbooking-plan-replay.MkPSRw` | Independent replay checkout. Currently at Task 6 |
| `/Users/jamesmitchell/.codex/handoffs/eventbooking-detailed-plans-2026-09-20/` | Archives from the first authoring session, with the original request |

Snapshots are JSON maps of repository-relative path to file contents: `task-1.json` through
`task-6.json`. Each generator diffs two snapshots to produce a task's edit volumes.

## Tooling notes

- `snapshots.mjs` no longer shells out to `apply_patch`. That binary belonged to the previous
  harness and does not exist here, which is what stalled Task 3c packaging. It now writes files
  directly and refuses to overwrite. Its snapshot-on-import side effect is also guarded.
- `pack-retirement.mjs` packages Tasks 3a–3d. `pack-task.mjs` is the generalised version used from
  Task 4 onward; it reads `task-configs.json`, keyed by task number.
- To package a task: snapshot the prototype (`node snapshots.mjs task-N`), add its entry to
  `task-configs.json`, then `node pack-task.mjs N`.
- To replay: extract the fenced payload between the `TASK_PAYLOAD` markers in the generated
  instruction document, run it in the replay checkout, then build and run the full suite there.

## Verification evidence

Every figure is a full `dotnet build EventBooking.sln -warnaserror` followed by every test project,
with Docker running and no skipped tests.

| Checkpoint | Domain | Application | Infrastructure | API | MCP | Web | Seed | Total |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Unmodified predecessor | 230 | 451 | 158 | 225 | 34 | 251 | 76 | 1425 |
| Task 3d (end of Phase 0) | 237 | 422 | 160 | 232 | 35 | 241 | 75 | 1402 |
| Task 4 | 258 | 422 | 173 | 232 | 35 | 241 | 75 | 1436 |
| Task 5 | 289 | 422 | 173 | 232 | 35 | 241 | 75 | 1467 |
| Task 6 | 306 | 422 | 173 | 232 | 35 | 241 | 75 | 1484 |

## Decisions taken while authoring, which later tasks must respect

- The time-zone abstraction the domain calls is declared in the domain project, not the application
  project, because domain rules depend on its answers and dependencies point inward. Infrastructure
  implements it with NodaTime.
- The temporary single-zone clock survives Task 4 and retires when `Location` can supply zones.
- The predecessor's fixed appointment-type identifiers and their seeded rows survive Phase 1. They
  retire in Phase 3, when seeding and the handlers move onto Admin-managed types.
- `inviteOptionCount` is domain state from Task 5 but not editable until Task 12 adds it to the
  command, the API and the MCP tool together. The settings handler passes the stored value through.
- Proposals are made at one transitional location until Task 13 carries a `Location` through the
  command, the API and the MCP tool. The constant is in the application layer and says so.
- The predecessor's Admin fallback for withdrawing a proposal is removed: FR-2.9 judges withdrawal
  by the proposing appointment type, and Admin holds no negotiation capability. The null-scope gate
  (FR-10.7) therefore lands early in the negotiation handlers.
- One proposal builder is shared by every test project, linked from `tests/TestSupport/` with a
  global using, because proposals now need a location, a listed set and a proposing type.
- A migration's backfill values are part of the change, not an afterthought: EF's generated zero,
  false and empty defaults have misdescribed existing rows in three tasks so far, and each one was
  corrected by hand, with the column default dropped afterwards.

## Questions the user has settled

1. **Who may withdraw a proposal (Task 6).** `proposerAppointmentTypeId` is now part of
   `EventProposal` in `docs/ontology.ttl`, with the invariant that withdrawal of the proposal and of
   the proposer's own acceptance is judged against that type, never against
   `createdByManagerUserId`. Task 6 implements it, and the fresh schema in Task 9 carries the
   column.
2. **Lock order versus the cancellation flow (Task 7).** The documented order wins:
   `Attendee`, `EventProposal`, `Event`, `EventCapacity`. Event cancellation reads the affected
   attendee identifiers without locks, then takes each booking's locks in that order and
   re-validates under lock. The sequence diagram in `docs/design/03b-screens-and-flows.md` is
   corrected to match, as part of Task 7.

The full list of fourteen contradictions the first authoring session found is preserved in this
file's history at commit `2b192ca`, and each remaining one is raised as its task is reached.

## Next steps

1. Author Task 7 (N-row capacity and the lock-ordering helper), correcting the cancellation
   sequence diagram in `docs/design/03b-screens-and-flows.md` in the same task.
2. Author Task 8 (location-restricted invites and the closed attendee-status table).
3. Then write the Phase 1 pull-request gate into
   `phase-1-domain.md` in the shape Phase 0 uses.
4. Phase 2 (Tasks 9–11) stays prototype-verified. Phase 3 onward is hand-authored.
5. Keep committing and pushing each finished task on `docs/detailed-implementations`. Lint every
   plan file explicitly before staging, since the ontology checker only sees tracked Markdown:

```bash
node --input-type=module <<'LINT_PLANS'
import fs from 'node:fs';
import {execFileSync} from 'node:child_process';
const directory = 'docs/detailed-implementations';
const files = fs.readdirSync(directory).filter(name => name.endsWith('.md')).map(name => directory + '/' + name);
process.stdout.write(execFileSync('node', ['scripts/check-ontology-terms.mjs', '--also', ...files], {encoding:'utf8', maxBuffer:1e7}));
LINT_PLANS
```

Do not stage broadly, do not push to `main`, and do not merge. The plans' own pull request comes
last, with a recomputed fingerprint and narrative sections covering whatever was settled.
