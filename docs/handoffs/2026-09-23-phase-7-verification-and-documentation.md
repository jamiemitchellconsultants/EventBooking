<!-- A handoff prompt: paste the whole of this file into a fresh agent. It is written to be
     self-sufficient for an agent on a different machine, with no access to the scratch
     prototype Phases 0-2 were built in. Provenance: written 23 September 2026, after Phase 6
     was authored, reviewed and merged (pull request #34), and after its narrative proposal
     (pull request #35) merged too. -->

Work on the EventBooking repository: https://github.com/jamiemitchellconsultants/EventBooking

Clone it fresh, then read docs/detailed-implementations/HANDOVER.md before anything else. It is
written for an agent with no context and tells you the assignment, the governing inputs and their
precedence, the standing rules that have already bitten, and how far the work has got. Follow it.

Your job: author Phase 7, master Tasks 32 and 33 — the load test and documentation. This is the
**last** phase in the master plan (docs/superpowers/plans/2026-09-19-eventbooking-implementation.md,
"Phase 7 — Verification and documentation"). When Task 33 is written, the whole assignment section 1
of the handover describes is complete: do not stop one task early and call it done.

BEFORE YOU START, check that pull request #34 has merged. It has as of this writing — it is on
`main` already, along with its automated narrative proposal (pull request #35, also merged) — but
section 3's table and section 11 of the handover are the source of truth if that has changed by the
time you read this, not this file's own provenance line.

THE METHOD IS THE SAME AS PHASES 3-6: hand-authored, no prototype, complete code and complete tests
written straight into the documents. Section 6a's loop is your method, and both of its mechanical
sweeps still run per task with their match counts printed. Task 32's files are JavaScript (k6), not
C# — same caveat the Phase 6 handoff gave you about Dockerfiles and YAML: the gap sweep and the
async sweep only see ```csharp blocks, so they will silently pass over `confirm-burst.js` and
`tests/load/README.md`, not because those are complete, but because the sweep cannot see them. Read
the load-test script as code; there is no substitute check for it here.

**Neither task can be observed running, and that is a bigger gap here than in Phases 3-6.** Every
hand-authored phase so far has said "no build has run, state the counts as expectations" for its own
new code. Phase 7 is different in kind, not just in degree: master Task 32 Step 2 says "Run against
the local stack. Expected: all thresholds pass," and Task 33 Step 3 says "Walk the demo runbook end
to end on a fresh clone. Expected: every step works as written." Both instructions presuppose a
running system. Nothing from Phases 3-6 has ever been built, so there is no local stack to run the
k6 script against and no fresh clone on which the runbook's steps would do anything. Do not silently
reinterpret "run" as "read." State this explicitly in both task documents' own Step 2/Step 3 and in
the phase overview, the same way Phases 3-6 state that their test counts are unobserved — and do not
add a row to section 7's evidence table.

TWO GAPS THIS HANDOVER HAS NOT SURFACED BEFORE. Resolve both while writing Task 32; do not let
either arrive as a surprise mid-task, the way Phase 3's dashboard shape and Phase 6's clock retirement
did for their own phases.

* **The capacity-lock-hold metric Task 32 needs to assert on does not exist anywhere in the plan.**
  Master Task 32's threshold is "capacity lock hold p95 under 50 ms (from the metric emitted by the
  booking handler)," and design 08's own Verification section repeats NFR-P3 the same way. Grep
  every phase document and design 04/08: no task ever creates a lock-hold histogram. Task 21
  (`phase-4a-api-conventions.md`) creates the only metrics that exist —
  `EventBookingMetrics.Requests`, `.Duration`, `.CapacityExhausted`, `.RateLimited`, on a meter
  named `EventBooking.Api`, defined in `src/EventBooking.Api/Observability/EventBookingMetrics.cs`
  — and none of them is a lock-hold duration. Design 08's own Observability list (the "Metrics"
  bullets) does not name one either.
  This is not just a missing file. EventBookingMetrics lives in Api; ConfirmBookingHandler lives
  in Application (`src/EventBooking.Application/Bookings/ConfirmBookingHandler.cs`, Task 15); the
  row locks it holds are acquired through Infrastructure's RowLocks/TransactionLocks
  (`src/EventBooking.Infrastructure/Persistence/Locking/`, Task 10). Design 04's layering table is
  explicit that dependencies point inward — Infrastructure depends on Application and Domain only,
  never on Api — so neither the handler nor the locking helpers can reference Api's
  EventBookingMetrics to record anything. Master Task 32's own Files list (a k6 script, a README
  and a seed fixture mode) has no entry for the C# file this requires. Work out where the
  instrument is actually created and recorded — a port in Application that Infrastructure's locking
  code calls, with Api supplying the concrete meter, is one shape; there may be a simpler one — and
  say so explicitly in Task 32's Files list and Interfaces section rather than asserting a threshold
  against a metric nothing emits. This is exactly the kind of contradiction AGENTS.md's "every
  contradiction... goes to the user" line covers: put the shape you settle on to the user, do not
  pick one quietly, the way Phase 3 and Phase 6 each had to for their own settlements (handover
  section 8).
* **`docs/design/07-deployment.md`'s seed-data section still lists five appointment types.** It says
  "5 AppointmentTypes: MED, FIT, IND, ESC and DOC" and never mentions LAB. `git log` on that file
  shows one commit, the original design package import — Phase 6 never touched it, despite
  settlement #21 (handover section 8) adding LAB as a sixth, active, managed type and making it
  binding on every later task. Design 08's own "Definition of done" item 5 says the design package
  is updated whenever a documented rule changes, and master Task 33's Files list already names "any
  design-package document whose rule changed during implementation" — so fixing this is squarely
  Task 33's job. It is called out here only so you do not have to rediscover it: update design 07's
  seed-data bullet list (and anywhere else in the design package that still enumerates five types)
  to match what Task 28 actually seeds.

STATE YOU CAN RELY ON, SO YOU DO NOT RE-DERIVE IT.

* Phases 0, 1 and 2 are prototype-verified, replayed and merged. The last measured checkpoint is
  Task 11: 1570 tests — Domain 360, Application 421, Infrastructure 206, API 232, MCP 35, Web 241,
  SeedData 75.
* Phases 3, 4, 5 and 6 (Tasks 12-31) are written, reviewed and merged, and none of them has ever
  been built or tested. Section 7 explains why they carry no test figures and must not be given any.
  Yours will not either, for the reason given above — restated in each document's own Step 2/3, not
  invented fresh.
* **SeedData's current state is not in the Phase 0-2 edit-volume format.** The `at-task-11.py`
  reader the Phase 4-6 handoffs gave you only covers Tasks 1-11 and cannot see anything later — and
  it would be wrong to use here regardless, because Task 28 (`phase-6a-seed-cli.md`) rewrote every
  file under `src/EventBooking.SeedData/` by hand, with each file's complete current content
  embedded directly in that document's own fenced blocks (look for the `(complete)` marker), not as
  a before/after diff volume. For Task 32's "seed fixture mode for the load scenario," read
  `phase-6a-seed-cli.md` directly — `DemoSeedSpec.cs`, `DemoSeeder.cs`, `SeedCommand.cs` and
  `SeedRunSteps.cs` are all there — rather than reconstructing anything through the old reader. No
  task after 28 touches SeedData, so that document's code blocks are still current.
* The local Compose stack (`phase-6b-local-compose.md`, Task 29) exposes the API on
  `http://localhost:5001`. That is what `confirm-burst.js` targets; the hardened home-lab stack
  (Task 30) is a different topology and is not what Task 32 tests against. The demo seed's shared
  type and 100-place event for the burst scenario need to come from Task 32's own fixture mode, not
  from the general demo dataset design 07 describes — the general dataset's events cap around the
  sizes listed there, not 500 invites against one 100-place event.
* You are creating the phase overview too — `phase-7-verification-and-documentation.md`, modelled on
  `phase-5-web.md` and `phase-6-seed-and-deployment.md` (hand-authored phases with no observed test
  result). Name the task documents `phase-7a-load-test.md` (Task 32) and
  `phase-7b-documentation.md` (Task 33), continuing the lettering the way every other phase has, even
  though this phase only has two tasks — there is no split like master Task 9 or Task 22 needed
  here. Task 33 carries the phase's pull-request gate, and it is the **last** gate in the whole
  master plan.
* Whether Task 33's pull request is decision-bearing depends on what you settle for the metric gap
  above — introducing a new cross-layer port or moving where metrics are created is an architecture
  decision under AGENTS.md's narrative criteria, even though nothing in the D1-D15 spec coverage
  table names a Phase-7 decision. Judge it against AGENTS.md's own list ("architecture, governance,
  operational, correction, or experimental decision") once you know what you actually did, rather
  than assuming either way from this note.

WHAT EARLIER PHASES GIVE YOU, AND WHAT YOU MUST NOT RE-DERIVE.

* The error catalogue is closed as of Task 21: `capacity-exhausted` is the slug the 400 non-2xx
  responses in the burst scenario must carry, and it is the same slug the CapacityExhausted
  counter's business signal already names — reuse it, do not invent a second name.
* `README.md` at the repository root here is this documentation-only repository's own placeholder
  and is unrelated to the target application's README that Task 33 writes. Do not conflate the two;
  Task 33's README content is the application's, generated for `src/../README.md`-equivalent
  delivery when the plan is executed, quoting the quick-start commands Task 29 already wrote in
  `phase-6b-local-compose.md` (`docker compose up --detach --build --wait ...`).
* Help guides live at `src/EventBooking.Web/wwwroot/help/{admin,coordinator,manager,appointment-
  staff,attendee}.md`, written in Task 27 (`phase-5d-coordinator-attendee-help.md`). Check each
  against what changed since: LAB as a sixth type (Task 28), migrate-only-by-default seeding and the
  retired clock configuration (Task 28), and the deployment topology (Tasks 29-30) are the candidates
  most likely to have made one of the five guides stale. Update only the ones that actually changed;
  do not rewrite guides whose content Phase 6 left untouched, the way the Phase 5 handoff warned you
  not to invent work Phase 4's role guides didn't need either.
* `deploy/home-lab/README.md` already exists as of Task 30 (`phase-6c-home-lab.md`) — Task 33
  modifies it into the final operator runbook, it does not create it from nothing. Read Task 30's
  version first.

STANDING RULES WORTH REPEATING BECAUSE EACH HAS COST A CYCLE.

* If a domain concept changes, edit docs/ontology.ttl, run node scripts/build-ontology.mjs, and
  commit both files together. Nothing in Phase 7 is expected to touch the ontology, but the metric
  decision above might if you choose to model it as a named domain concept rather than pure
  infrastructure — check before assuming either way.
* Lint the plan directory explicitly before staging — the recipe is in the handover's section 9.
* In plan prose, backtick a PascalCase word only if the ontology defines it. Class, test, service
  and file names go in plain text or inside fenced code blocks.
* Stage explicit paths. Never git add -A.
* Never push to main, never merge. One commit per task on the phase branch.
* The ai-fingerprint check goes red once on every push and that red is not yours to fix. Wait until
  the pull request reports the head you just pushed, then update the body.

Finish each task with its own commit and push on the phase branch. Do not open a pull request
without asking me first. When Task 33 lands, update the handover's section 3 table, section 7 (it
still gets no new row), section 8 with whatever you settled for the metric gap, and section 11 to
say the assignment is complete — the size of Phase 0 was never evidence of progress through the
rest, and Phase 7 finishing is what actually closes it.
