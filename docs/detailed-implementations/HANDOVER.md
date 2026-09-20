# Authoring handover — EventBooking detailed implementation plans

Saved 20 September 2026, approximately 07:35 Europe/London. This is an authoring handover, **not an executable implementation task**. The user asked to stop active work and preserve everything for another agent.

**Subsequent instruction:** the user requested a local checkpoint commit of the saved plans and this handover. The repository-state and backup descriptions below record the pre-commit handover snapshot. Use git log and git status to obtain the resulting commit; do not assume the documents are still untracked. No push or PR is part of this checkpoint request. The durable archives remain the original pre-commit snapshot.

## Start here

The assignment is to write documentation, not implement EventBooking in this repository. Read the original request, AGENTS.md and the governing inputs before continuing. Do not start the port again: substantial embedded source, tested checkpoints and authoring helpers already exist.

Suggested prompt for the incoming agent:

> Continue authoring the EventBooking detailed implementation plans. First read docs/detailed-implementations/HANDOVER.md and the original request it identifies. Preserve all existing work. The actual repository must remain documentation-only. Resume from the tested Task 3c checkpoint, package it, then finish Task 3d and the Phase 0 review/commit/push before moving to later phases. Do not claim the plans are complete: Tasks 4–33 have not been authored. Use the recorded snapshots and verification evidence instead of rebuilding the predecessor port from scratch.

## Assignment and authority

Original request, copied outside temporary storage:

- /Users/jamesmitchell/.codex/handoffs/eventbooking-detailed-plans-2026-09-20/original-request.txt
- Original attachment: /Users/jamesmitchell/.codex/attachments/ab418ccb-d03c-44ac-abbe-7a9570f0f9f7/pasted-text.txt

The requested deliverables are README.md and phase-0-port-and-strip.md through phase-7-verification-docs.md under docs/detailed-implementations/. Split files at approximately 1500 lines. Preserve all 33 master task numbers, phase boundaries and commit messages; lettered splits are permitted.

Target executor: opencode + superpowers + Qwen3.6 27B Q6. It will not have the predecessor checkout. Every new file must have complete code; every edit must have an exact before and after; every task needs complete tests, interfaces and domain context. No placeholders or instructions to retrieve predecessor files. Detailed constraints and literal commit/push/PR steps are in the original request.

The user explicitly approved retaining predecessor domain names temporarily in Task 1, followed by their removal in Task 2 before the Phase 0 PR. This resolves that conflict with the original request. Do not ask again.

Earlier messages about reverting and restarting were already handled. The one-time 01:42 resume was also handled. Do not revert the current work or schedule another resume.

## Repository state

Repository: /Users/jamesmitchell/RiderProjects/EventBooking

- Branch: docs/detailed-implementations.
- HEAD: 851072337a7ae9f52fee06a1a7aabda3e0aa5aff.
- Last observed origin/main: 48051c3a789741379c124c350262d656a9193195.
- Branch tracks origin/main and is behind two narrative-only commits. Recheck before integration.
- All plan files are currently **untracked**. Nothing from this authoring run has been committed or pushed; no PR exists.
- No production code was added to the actual repository. All executable validation occurred in scratch checkouts.
- No ontology source changes have been made in the actual repository.
- Before this handover, the only working-tree entry was docs/detailed-implementations/.

The user requested committing/pushing Phase 0 first, then each later phase, on this documentation branch. Phase 0 is not yet complete, so that first commit has not occurred. Do not mistake the lack of a commit for a lack of saved work; durable archives below preserve it.

## Durable backups

Directory: /Users/jamesmitchell/.codex/handoffs/eventbooking-detailed-plans-2026-09-20/

- authoring-scratch.tar.gz: all authoring scripts, source snapshots, logs and the latest prototype. Excludes generated bin/, obj/ and TestResults/ directories. Approximately 5.4 MB compressed.
- replayed-checkpoint.tar.gz: the independent, passing Task 3b replay checkout, excluding build outputs and the symlink to plan documents. Approximately 971 KB compressed.
- plan-documents.tar.gz: snapshot of the repository plan directory, including this handover.
- original-request.txt: original task in full.
- HANDOVER.md: convenience copy of this document.
- SHA256SUMS: checksums for the saved archives and request/handover copies.

Original live scratch locations still exist:

- /private/tmp/eventbooking-detail.6yx6zE — authoring scratch.
- /private/tmp/eventbooking-detail.6yx6zE/verify — latest prototype: Task 3c implemented and passing, plus the deliberately failing Task 3d test.
- /private/tmp/eventbooking-plan-replay.MkPSRw — independently replayed Task 3b, passing.

Use the physical /private/tmp paths with relative EventBooking.sln when running dotnet. Mixing /tmp and /private/tmp aliases previously caused duplicate MSBuild graph issues.

If temporary directories are gone, extract archives into new empty directories, never over unrelated work:

```bash
AUTHORING_RESTORE=$(mktemp -d /private/tmp/eventbooking-authoring-restore.XXXXXX)
tar -xzf /Users/jamesmitchell/.codex/handoffs/eventbooking-detailed-plans-2026-09-20/authoring-scratch.tar.gz -C "$AUTHORING_RESTORE"
REPLAY_RESTORE=$(mktemp -d /private/tmp/eventbooking-replay-restore.XXXXXX)
tar -xzf /Users/jamesmitchell/.codex/handoffs/eventbooking-detailed-plans-2026-09-20/replayed-checkpoint.tar.gz -C "$REPLAY_RESTORE"
```

Several helpers hard-code the original scratch/repository paths. Inspect and update those paths via apply_patch before using restored helpers. Do not restore into the actual repository root. Package restore will recreate excluded build outputs.

## Governing inputs and process

All inputs were read in the previous session. An incoming agent must consult them rather than treating this handover as a replacement specification:

1. AGENTS.md in full.
2. docs/superpowers/specs/2026-09-19-eventbooking-design.md.
3. docs/design/00 through 09, including 03a and 03b.
4. docs/superpowers/plans/2026-09-19-eventbooking-implementation.md.
5. docs/ontology.md in full; docs/ontology.ttl is its editable source.
6. ../JointBooking at commit 6957928a6c9054372dda915b359f63b73969ebee when additional predecessor source is needed.

The design wins on detail; the spec wins on decisions. Use canonical ontology terms. No edits to generated Narrative.md or docs/ontology.md. Explicit original-request requirements override generic AGENTS examples: root-level phase filenames and explicit staging paths, never broad git add.

Superpowers skills already used: using-superpowers, writing-plans, systematic-debugging, test-driven-development and verification-before-completion. Continue following applicable skills. Writing-plans self-review is a self-review, not authorization to spawn agents. No subagents have been used.

## Existing plan documents

There were 229 Markdown files before this handover was added:

| Files | State |
| --- | --- |
| README.md | Prerequisites, branch/resume rules, baseline evidence; needs final execution links and completion status |
| phase-0a-import.md | Task 1 instructions, 1531 lines; slightly over target, should trim/split during review |
| phase-0a-files.md | Complete 584-path import manifest |
| phase-0a-source-001.md through 083 | Complete ported source/assets, maximum approximately 1450 lines per volume |
| phase-0b-vocabulary.md | Task 2 instructions, approximately 1261 lines |
| phase-0b-edits-001.md through 115 | 399 exact before/after edits |
| phase-0c-identity.md | Task 3a instructions |
| phase-0c-edits-001.md through 006 | Task 3a: 21 complete file changes |
| phase-0d-retire-import.md | Task 3b instructions |
| phase-0d-edits-001.md through 019 | Task 3b: 64 complete file changes |

No phase-0e files were present at the handover check. Task 3c implementation is saved and tested but its document generation was interrupted. There is no Phase 0 overview/final PR section yet, no Task 3d plan and no Phase 1–7 plans.

The many source volumes are intentional: the user requires the full predecessor port, including assets, without predecessor access. They are not duplicate alternative plans. Long files have numbered parts and SHA-256 metadata; five-backtick fences protect embedded Markdown.

## Verification evidence

All listed passing runs used a full build with warnings as errors, followed by all seven test projects, no skipped tests. Logs are in authoring scratch and its archive.

| Checkpoint | Domain | Application | Infrastructure | API | MCP | Web | Seed | Total |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Unmodified predecessor | 230 | 451 | 158 | 225 | 34 | 251 | 76 | 1425 |
| Task 1 replay | 230 | 451 | 157 | 226 | 34 | 251 | 75 | 1424 |
| Task 2 replay | 230 | 451 | 157 | 228 | 35 | 251 | 75 | 1427 |
| Task 3a prototype | 241 | 451 | 160 | 232 | 35 | 251 | 75 | 1445 |
| Task 3b prototype and independent replay | 235 | 423 | 160 | 231 | 35 | 245 | 75 | 1404 |
| Task 3c prototype | 237 | 423 | 160 | 232 | 35 | 242 | 75 | 1404 |

Important logs:

- task-3a-test.log.
- task-3b-build.log and task-3b-test.log.
- task-3ab-replay-build.log and task-3ab-replay-test.log: actual embedded plan payload replay passed.
- task-3c-build.log and task-3c-test.log: 0 warnings/errors, 1404 passing.
- task-3c-red.log: required-group regression failed before implementation.
- task-3d-red.log: expected failure due to missing retired configuration keys.

Task 3c has **not** yet been replayed from documents. Task 3d has **not** been implemented. The current live prototype therefore has one intentional failing test beyond the last passing checkpoint.

At handover, explicit ontology lint over all untracked plan Markdown, including this handover, passed: 260 files checked against 122 canonical terms (the count includes existing tracked Markdown). None of the current documents has passed a final full self-review/staged review. Repeat lint after further edits.

## Exact next steps

1. Inspect git status and confirm this handover still matches the working tree. Preserve unrelated work.
2. Inspect the saved generator pack-retirement.mjs and complete Task 3c packaging. Its last run hung inside its first apply_patch subprocess for over five minutes. The cause was not diagnosed. No phase-0e output was observed.
3. The stuck processes were explicitly stopped for this handover: PID 41679 (node pack-retirement.mjs 3c) and its child PID 41727 (apply_patch). A subsequent process check found no owned test/generation process still running. Recheck if needed; never kill unrelated dotnet processes.
4. Prefer diagnosing the generator in isolation or emitting each patch through the tool. Do not rerun all earlier transformations on the latest prototype.
5. Generate/review Task 3c instructions and exact before/after volumes from task-3b.json to task-3c.json, not from the live prototype (which also contains Task 3d's red test).
6. Replay Task 3c on the independent Task 3b checkout using the generated payload. Run the full build/tests. The replay checkout already has a symlink docs/detailed-implementations to the real plan directory; the durable replay archive intentionally excludes it.
7. Implement and validate Task 3d **in scratch**, then package it. The red test and expected failure already exist. Details below.
8. Write phase-0-port-and-strip.md as the phase execution overview and final PR gate. Link all Task 1, 2 and 3a–3d instruction files.
9. Self-review Phase 0 against the user request and writing-plans checklist, including the concrete issues listed below. Lint every untracked plan file explicitly.
10. Stage only the intended Phase 0 documents, review the full staged file list and diff, then run ontology validation. Commit/push the documentation branch as originally requested. Avoid staging local backup archives.
11. Continue Phase 1 onward, committing/pushing each completed phase. Finally open one PR for the plan documents, with a current AI-Fingerprint and substantive Narrative sections if unresolved design decisions were settled.

## Snapshot and helper inventory

Snapshots are JSON objects mapping repository-relative path to full UTF-8 file contents. Binary assets are supplied separately by the Task 1 source pack.

- task-1.json: final Task 1, 573 text files.
- task-1-manifest.json: 584 files including assets.
- task-2.json: final Task 2, 576 text files.
- task-2-operations.json: 399 complete rename/edit operations.
- task-3a.json: final Task 3a, 582 text files.
- task-3b.json: final Task 3b, 582 text files.
- task-3c.json: final Task 3c, 584 text files, excludes Task 3d red test.
- task-2-red.json: earlier pre-rename snapshot, not a final checkpoint.

Key helpers in authoring scratch:

- snapshots.mjs: exports root, scratch, walk, add and snapshot. add invokes apply_patch; walk excludes build output directories. **Its top-level process.argv[2] branch also runs when imported**, so generators with arguments create extra files named 3a.json/3b.json/3c.json. Those are incidental live snapshots; use task-3a.json/task-3b.json/task-3c.json instead. Consider guarding direct invocation before continuing.
- pack-plan.mjs: Task 1 source/assets volumes from its pinned snapshot.
- task1-document.mjs: Task 1 instructions/extractor; **stale** relative to manual Interfaces corrections in phase-0a-import.md. Do not rerun without preserving those corrections. It also risks adding a duplicate using Xunit to the guard test on rerun.
- pack-task2.mjs and task2-document.mjs: Task 2 volumes/instructions/extractor. Check generated step numbering; a duplicated Step 4 appeared during one inspection.
- pack-retirement.mjs: Tasks 3a, 3b, 3c generator. The string replacement bug that left SECTIONCOUNT undefined was fixed and Tasks 3a/3b were regenerated and replayed successfully. Task 3c packaging subsequently hung, as noted above.
- extract-port.mjs, apply-task2.mjs, apply-task3a.mjs, apply-task3b.mjs: corresponding payload extractors. No apply-task3c.mjs existed at handover.
- prepare.mjs, fixtures.mjs, rename.mjs, fix-keyword.mjs, routes.mjs, fix-nav-tests.mjs: one-off already-applied transformations. Do not rerun on the latest prototype.
- staff-policy.mjs, retire-import.mjs, seed-without-import.mjs, retired-import-docs.mjs, required-groups.mjs: already-applied later transformations. Some expect before snippets/deleted files and are not rerunnable.
- rewrite/Program.cs: Roslyn utility removing named methods/classes via apply_patch, restricted to the prototype directory.
- document/Program.cs: earlier Roslyn XML-documentation utility.

## Task-specific implementation notes

### Task 1: complete port

Commit message: build: port JointBooking solution as EventBooking without AWS

- AWS and Entra integrations removed; local email infrastructure folded into Infrastructure/Email.
- Generic Blazor OIDC replaces MSAL; local Keycloak realm/configuration included.
- Domain/Application public XML documentation filled so warnings-as-errors builds pass.
- Complete browser assets embedded, including original branding pending Phase 5. Bootstrap content is unminified under the existing filename for manageable source packaging.
- Two architecture guard tests run red against a minimal bootstrap test project, then pass against the full solution.
- The extractor validates all parts/hashes, bootstrap preconditions and safe paths before writing; refuses unrelated changes and symlinks.
- Task 1 was replayed from the actual generated documents, not merely tested in the authoring prototype.

### Task 2: vocabulary

Commit message: refactor: apply the EventBooking vocabulary mapping

- Renames domain/code/storage/routes/client contracts together.
- Two distinct pages: EventNegotiation.razor and EventOperations.razor; distinct clients avoid collisions.
- Temporary TransitionalLocation adapter replaces the old single-site name until Tasks 3–4.
- Reserved local event identifiers changed to eventItem; XML parameter names and SQL placeholders corrected.
- Two API tests inspect actual public types/OpenAPI; one MCP test inspects tools/list.
- An inherited MCP fixture now clears/restores role claims to prevent an earlier administrator test contaminating the unassigned-caller case.
- Some inherited comments/strings have awkward eventItem wording after mechanical replacement; review prose without undoing valid C# identifiers.

### Task 3a: configurable staff identity

All lettered Task 3 checkpoints keep the master message:

refactor: drop bulk import, head-office config and fixed StaffId format

- StaffId defaults to ^[A-Z0-9]{1,32}$; trims/uppercases, enforces 1–32 independently, requires a complete regex match, with 100 ms timeout.
- Optional deployment pattern flows through API/MCP/seed startup and application identity handling.
- New immutable StaffIdPolicy validates configuration at startup.
- StaffId.FromPersisted permits already-canonical identifiers admitted under an earlier/custom policy; does not apply today's admission regex during rehydration.
- PostgreSQL changes char(7) to varchar(32), with length/trim check. Custom ORG-10023 and 32-character values round-trip in real PostgreSQL tests.
- API project now references EF Core Design so the user-required startup-project migration command works.
- Migration: 20260920060813_GeneralizeStaffIdentifiers, designer and snapshot included. Its Down direction can lose/refuse incompatible values; instructions warn against blind production rollback.

### Task 3b: remove direct event import

- Removed event import handler/parser, endpoint, MCP tool, capability, upload UI/client and tests specifically for that retired functionality.
- Attendee CSV import remains; a new OpenAPI test checks both absence and retention.
- Removed EventImported and StaffAccessRemoved audit members without renumbering survivors.
- Event.CreateImported removed; ProposalId is now non-null Guid.
- Migration 20260920061416_RequireEventProposal explicitly refuses existing null proposal IDs. EF initially generated an empty-GUID fill; that was removed. Never fabricate history to pass this migration.
- DemoEventFactory reconstructs fully accepted proposals and persists both proposal and event. Nine seeded events add nine confirmed proposals to five standalone scenarios (14); invitation-capacity fixtures add three more (17).
- Seed tests assert distinct event proposal IDs and confirmed status, plus existing invitation/booking behavior.
- Four test projects have a shared-shape EventFixture helper that creates an accepted proposal then an Event. It does not persist the proposal because the inherited schema currently has no proposal FK. **When Task 9 introduces the correct FK, evolve these fixtures to persist the proposal.**
- Catalogue now has 35 operations. Updated guide routes and removed direct-event-import instructions.

### Task 3c: required attendee group — code done, packaging pending

- Attendee.AttendeeGroupId is Guid rather than nullable Guid.
- Required group flows through list, readiness, API, MCP and web contracts; group code/name are non-null on list surfaces.
- Removed AttendeeGroupUnassigned readiness value, reconciliation error and reconciliation DTO flag/UI paths. Surviving enum numeric values unchanged.
- Removed nullable-group branches from InviteIssuer and SaveAttendeeHandler; requirement mismatch checks remain.
- ListAttendeesHandler loads active groups plus referenced inactive groups, then uses their required identifiers. Current schema FK supplies integrity; a missing referenced group is not silently rendered as an unassigned attendee.
- Existing required-group migration/guard tests are retained until Task 9 replaces the inherited migration chain.
- Replaced the old reconciliation readiness test with assigned-group/no-active-booking behavior. Web fixtures now provide valid non-null group data.
- Added RequiredAttendeeGroupTests (2 cases) and RequiredAttendeeGroupContractTests (1 case). Creation without a group already failed before the change; the nullable public surface and retired enum assertions provided the red tests.
- Full build and 1404 tests passed. Exact snapshot is task-3c.json.

### Task 3d: retired single-site configuration — immediate unfinished task

Only tests/EventBooking.Api.Tests/RetiredLocationConfigurationTests.cs has been added to the live prototype. It is saved in the archive, not in task-3c.json.

It builds configuration with Clock:TimeZoneId and no retired section, invokes EventBookingConfiguration.Read, then checks that the retired options type and portal address property are absent. It currently fails exactly as expected: missing TransitionalLocation:TimeZoneId and TransitionalLocation:Address.

Planned direction, not yet implemented:

- Replace Infrastructure/Time/TransitionalLocationOptions with a temporary ClockOptions or equivalent, using Clock:TimeZoneId / Clock__TimeZoneId. Task 4 will replace single-zone domain behavior with Location/NodaTime.
- Remove retired address binding from API/MCP configuration and DemoEmailOptions, JSON defaults and tests.
- Remove the deployment address from AttendeePortalOptions rather than merely renaming the retired binding.
- Update confirmation outcome/client/page/email code and tests that consume TransitionalLocationAddress. Location-specific address presentation belongs to later location-aware work; do not invent a substitute global address.
- Update all constructor call sites for the portal options, including target-typed new expressions in tests.
- API and MCP both use EventBookingConfiguration.Read. Seed Program/DemoEmailOptions also create clock options; tests cover environment isolation.
- Clock methods and presentation adapters still have temporary TransitionalLocation names. Scope replacements carefully; master Task 3 explicitly allows a temporary single-zone clock until Task 4.
- Test suites for existing configured address behavior need replacement/removal tied to the retired feature, not arbitrary expectation changes.

Useful searches in the prototype (exclude bin/obj):

```bash
rg -n 'TransitionalLocationOptions|TransitionalLocationAddress|TransitionalLocation:|TransitionalLocation__' src tests -g '*.cs' -g '*.json' -g '*.razor' -g '!**/bin/**' -g '!**/obj/**'
rg -n 'AttendeePortalOptions' src tests -g '*.cs' -g '!**/bin/**' -g '!**/obj/**'
```

## Known review defects and risks before Phase 0 commit

- **Incorrect FR citation:** generated Task 3b context says FR-3.1 is creation by final acceptance. FR-3.1 actually defines capacity bounds. Correct the citation/text from the actual requirements (final acceptance is in FR-2), in both generator and document.
- Task 3c generator paraphrases FR-4.2 as requirement derivation; FR-4.1 is creation/derivation, while FR-4.2 covers editing and active-booking protection. Correct it before packaging.
- User requested relevant FR text and ontology invariants copied inline. Existing context blocks often summarize rather than quote exact relevant text. Audit and improve them; do not claim checklist completion yet.
- Interfaces in phase-0a-import.md were manually fixed to full actual configuration/DI code; its generator is stale.
- Check phase-0b-vocabulary.md for a duplicated Step 4 heading.
- Phase 0a is 1531 lines. Split/trim slightly without omitting code.
- Every task must show ontology handling accurately. Current retirement instructions say no ontology edit because the canonical invariants already exist. Verify AGENTS compliance; if semantics need changes, edit TTL and regenerate MD together rather than adding aliases.
- Generated migration commands are included as provenance because exact generated migration/designer/snapshot code is embedded. Instructions say not to generate a duplicate. Review this wording against the user's requested migration-command workflow.
- No complete Phase 0 PR body, fingerprint calculation, narrative gate or self-review has yet been written.
- Full source import contains inherited wording/assets; distinguish retained predecessor source from author-authored placeholders during placeholder scans.

## Design contradictions/gaps noticed for later phases

These are working review findings, not blanket authorization to redesign. Verify against sources, resolve explicitly and record decisions in the final documentation PR if necessary.

1. Required lock order is Attendee → EventProposal → Event → EventCapacity, with ascending IDs as specified. A cancellation diagram in 03b shows Event first. Cancellation needs attendee discovery/revalidation/retry without acquiring lower-order locks after Event.
2. Seed brief has ESC active but unmanaged and DOC inactive, yet includes four-type events/proposals and an active group requiring MED+DOC. Historical managed state/inactive group may resolve this, but needs explicit treatment.
3. Used-token invalidity table versus FR-6.5 confirmation idempotency: replay must be narrowly scoped to the existing successful booking, not a second write.
4. Missing staff_id status differs between catalogue (401) and security/Task 17 (403 except /me). Apply source precedence explicitly.
5. Task 16's workspace capability wording conflicts with Coordinator recovery actions requiring ManageAttendees.
6. Attendee CRUD/import/boundary implementation is underallocated between Tasks 12/20; likely needs lettered Task 20 work.
7. EmailLog ontology lacks some outbox attempt/backoff/correlation and cancellation replacement context required by the design. Define needed concepts in TTL before code/plans.
8. SMTP crash after send but before marking sent cannot guarantee exactly-once delivery. Leases prevent simultaneous workers, not all crash duplicates; describe at-least-once accurately.
9. Task 32's 500 same-IP confirmations conflicts with 30/min rate limiting; isolate load-test configuration or use distinct identities without silently weakening production policy.
10. London and Dublin share offsets for the proposed sorting example; use genuinely different zones, such as Tokyo, for UTC-order tests.
11. Task 3's “no import operation” means no event import. Attendee CSV import stays.
12. “Only stored instant is start_utc” concerns derived event-window instants, not removal of audit/expiry timestamps.
13. Settings affecting only future invitations may require snapshot fields/invariants not yet in the ontology.
14. Successor-manager withdrawal scope requires retaining the proposer's appointment type, not only proposer user ID. Check/add the corresponding ontology field before later code.

## Validation and integration reminders

For untracked plan Markdown, the ontology checker will not discover files by itself. Explicitly lint the current directory before staging:

```bash
node --input-type=module <<'LINT_PLANS'
import fs from 'node:fs';
import {execFileSync} from 'node:child_process';
const directory = 'docs/detailed-implementations';
const files = fs.readdirSync(directory).filter(name => name.endsWith('.md')).map(name => directory + '/' + name);
process.stdout.write(execFileSync('node', ['scripts/check-ontology-terms.mjs', '--also', ...files], {encoding:'utf8', maxBuffer:1000000}));
LINT_PLANS
```

Before committing: stage explicit intended paths, inspect git diff --cached --name-only and git diff --cached, then run the repository ontology checks. Do not stage unrelated files to make lint see them. No direct push to main, no merging and no deleting remote branches.

For a PR body: save a scratch Markdown file, lint with --also, carry the repository template's three exact Narrative headings when decision-bearing, and append the current AI-Fingerprint as the last line. Recompute after every push that changes HEAD. The authoring PR should distinguish the user-approved Task 1 exception from any further decisions actually settled.

Phase boundaries remaining: Phase 1 Tasks 4–8; Phase 2 Tasks 9–11; Phase 3 Tasks 12–20; Phase 4 Tasks 21–23; Phase 5 Tasks 24–27; Phase 6 Tasks 28–31; Phase 7 Tasks 32–33. Read exact later commit messages from the master plan. Phases 0, 1, 2, 3, 5 and 6 are marked decision-bearing; 4 and 7 require Narrative only if new decisions are made.

Do not claim the full assignment complete or open its final PR merely because the port is large. The requested later plans are still substantive unfinished work.
