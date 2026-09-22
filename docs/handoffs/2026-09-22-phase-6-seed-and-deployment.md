<!-- A handoff prompt: paste the whole of this file into a fresh agent. It is written to be
     self-sufficient for an agent on a different machine, with no access to the scratch
     prototype Phases 0-2 were built in. Provenance: written 22 September 2026, after Phase 5
     was authored and reviewed, while pull request #31 (Phase 5's plan) was still open. -->

Work on the EventBooking repository: https://github.com/jamiemitchellconsultants/EventBooking

Clone it fresh, then read docs/detailed-implementations/HANDOVER.md before anything else. It is
written for an agent with no context and tells you the assignment, the governing inputs and their
precedence, the standing rules that have already bitten, and how far the work has got. Follow it.

Your job: author Phase 6, master Tasks 28, 29, 30 and 31 — SeedData CLI and deployment. Their specs
are in docs/superpowers/plans/2026-09-19-eventbooking-implementation.md under "Phase 6 — Seed and
deployment". The deployment shape, the Compose files, the home-lab install steps, the image list
and the seed CLI's flags and demo dataset are all in docs/design/07-deployment.md — read it in
full before writing anything; it is short and every task cites it directly. Keycloak's realm,
client and role/claim mapping also draw on docs/design/06-security-and-authentication.md.

BEFORE YOU START, check that pull request #31 has merged. If it is still open, stop and say so
rather than working around it. Phase 6 is written the same way Phase 5 was: hand-authored, no
prototype, and it inherits every standing rule the last four phases accumulated. Section 3's table
and section 11 of the handover tell you whether #31 has landed; do not trust this file's own
provenance line for that, it will be stale by the time you read it.

THE METHOD IS THE SAME AS PHASES 3-5, WITH NO SPECIAL-CASE DECISION LIKE PHASE 5's.

Phase 6 stays hand-authored — complete code and complete tests (and, for this phase, complete
Compose files, Dockerfiles and workflow YAML) written straight into each document, no prototype.
Section 6a's loop is your method, and both of its mechanical sweeps still run per task with their
match counts printed. Unlike Phase 5, there is no known reason to move work between tasks — say so
in the phase overview rather than silently inventing a settlement the way #20 was for Phase 5. If
you do find a reason a boundary has to move, that is itself a decision for section 8, not something
to decide alone.

**One thing to notice going in: section 6a's two mechanical sweeps are C#-shaped.** The gap sweep
matches `- (Create|Modify): (\S+\.cs)` against fenced ```csharp blocks, and the async sweep only
walks ```csharp blocks too. Tasks 28-31 are the first hand-authored tasks whose Files lists are
mostly *not* `.cs` — `docker-compose.yml`, Dockerfiles, `realm-export.json`, `install.sh`,
`.github/workflows/images.yml` and `compose-smoke.yml`. Both sweeps will silently pass over every
one of those entries, not because they are complete, but because the sweep cannot see them. Do not
read a clean sweep as evidence for anything but the `.cs` files in Task 28. For everything else,
the read-it-as-code discipline in section 6a step 2 is the only check there is: `docker compose
config` locally, `actionlint` on the workflow, `shellcheck` on `install.sh` — the master plan
already asks for these per task; do not treat them as optional because the mechanical sweep is
quiet.

TWO OPEN ITEMS THE HANDOVER ALREADY FLAGS FOR THIS PHASE. Resolve both before or while writing
Task 28; do not let either arrive as a surprise mid-task.

* **Contradiction #1 (handover section 10, still open).** "The seed brief has an appointment type
  that is active but unmanaged, and an inactive type, yet also lists events and proposals that
  would need them." Design 07's demo dataset gives you ESC (active, no Manager) and DOC (inactive),
  and separately asks for events listing 1-4 types and open proposals at 1 of 2 and 2 of 4 accepted.
  Work out which types the seeded events and proposals actually list before you write the coverage
  test in Task 28's Step 1 — if ESC or DOC would have to appear on a seeded event or proposal to
  satisfy the "every axis" requirement, that contradicts what they are seeded to demonstrate
  (the disabled picker state and the blocked-deactivation state), and it goes to the user as
  section 8 requires. Do not silently pick a reading and move on; Phase 3's review found exactly
  this kind of unresolved tension five review cycles ago.
* **The single-zone clock, still not retired.** Section 8's Phase 4 entry and the transitional-
  constructs table both say `Clock:TimeZoneId`, its options type and the transitional member on the
  system clock survive every phase so far and are due to retire "with Phase 6's seed rework."
  Task 28 is the seed rework. If retiring it is in scope for Task 28, say so in the Files list and
  do the removal; if it genuinely belongs to a later task or a different one of 28-31, say that
  explicitly in the phase overview and update the transitional-constructs table's own row rather
  than leaving it to drift again.

STATE YOU CAN RELY ON, SO YOU DO NOT RE-DERIVE IT.

* Phases 0, 1 and 2 are prototype-verified, replayed and merged. The last measured checkpoint is
  Task 11: 1570 tests — Domain 360, Application 421, Infrastructure 206, API 232, MCP 35, Web 241,
  SeedData 75.
* Phases 3, 4 and 5 (Tasks 12-27) are written, reviewed and merged, and none of them has ever been
  built or tested. Section 7 explains why they carry no test figures and must not be given any.
  Yours will not either. Say so in each document's own Step 4, the same way Phases 3-5 do.
* The SeedData project (`src/EventBooking.SeedData/`) is untouched by Phases 3-5: the Seed column
  in section 7's checkpoint table sits at 75 from Task 9b onward with nothing later touching it, and
  no phase-3/4/5 document's Files list names a SeedData path. That means the `at-task-11.py` reader
  described below gives you Task 28's current file state directly, with no later edit volume to
  reconcile against it — unlike the Web pages Phase 5 modified, which had ported state you had to
  read from phase-0's volumes and nothing since.
* You are creating the phase overview too — phase-6-seed-and-deployment.md, modelled on
  phase-5-web.md (both are hand-authored phases with no observed test result; phase-0-port-and-
  strip.md is the more general model the handover names in section 3 if you want the fuller
  original). Name the task documents phase-6a-, phase-6b-, phase-6c- and phase-6d-, continuing the
  lettering — one task per document, the way Phase 5 did, since Phase 6 has no task that needs
  splitting the way master Task 9 or Task 22 did. Task 31 carries the phase's pull-request gate.
* Task 31's pull request is decision-bearing per the master plan's own Step 4 ("deployment shape and
  seed defaults"), so it carries the narrative-required label and the three exact narrative
  headings, the same as Task 27's did for D10.

THE PROTOTYPE IS NOT ON YOUR MACHINE. Read this before you go looking for it.

Phases 0-2 were built in a scratch checkout under /private/tmp on one particular machine. The
handover references it in section 4, and it does not exist for you. Do not try to recreate it and
do not guess at type shapes because you cannot see it. For Task 28's SeedData files, save this
script as at-task-11.py in the repository root — it is the same script the Phase 5 handoff used,
unmodified, and it is tested:

```python
#!/usr/bin/env python3
"""Print a file's content as it stands at the end of Phase 2 (Task 11).

Usage: python3 at-task-11.py src/EventBooking.SeedData/DemoSeedSpec.cs
The plan's edit volumes carry every file's complete after-side; the last volume
in task order that names the path holds its current state.
"""
import glob, re, sys

path = sys.argv[1]
marker = f"## after — {path} — "
newest = None
for vol in sorted(glob.glob("docs/detailed-implementations/phase-[0-2]*.md")):
    text = open(vol).read()
    if marker in text:
        newest = (vol, text)
if newest is None:
    sys.exit(f"No after-side for {path}. It may be created later than Task 11, or deleted.")

vol, text = newest
parts = []
for m in re.finditer(re.escape(marker) + r"(\d+)/(\d+)\n\n<!--.*?-->\n\n`{5}\w*\n(.*?)\n`{5}", text, re.S):
    parts.append((int(m.group(1)), m.group(3)))
print(f"# from {vol} ({len(parts)} part(s))", file=sys.stderr)
for _, body in sorted(parts):
    print(body)
```

Caveats, unchanged from Phase 5: it only covers Tasks 1-11, a path created after Task 11 or deleted
before it returns nothing (which is information, not an error), and check every path the master
plan gives you with the reader before you copy it into a Files list — Phase 5 found three master-
plan paths that no longer matched reality, and nothing about Phase 6 is exempt from the same check.
`docker-compose.yml`, everything under `deploy/`, and `.github/workflows/images.yml` are new in
this phase; the reader will correctly return nothing for all of them, because they are Task 28-31's
own creations, not Task-11 state.

WHAT EARLIER PHASES GIVE YOU, AND WHAT YOU MUST NOT RE-DERIVE.

* **Every failure arrives as `application/problem+json` with a `type` from Task 21's closed
  catalogue.** The seed CLI is not a web client, so this mostly does not apply to Task 28 itself,
  but `compose-smoke.yml` (Task 29) probes `/health/ready` and `/api` and should not invent its own
  error-shape assumptions about what a probe failure looks like.
* **Config keys not already in design 04's table were added where design 06 needed them**
  (`Proxy__Networks`, the two rate-limit keys, and the still-surviving `Clock:TimeZoneId`). Check
  the actual configuration surface with the reader or with `grep` over `appsettings*.json` and the
  options classes before inventing a new environment variable name for something Task 21 already
  named.
* **Web never references server assemblies**, per Phase 5. The seed and deployment layer is the
  opposite case — the seed CLI runs against the real Domain/Application/Infrastructure assemblies,
  not a duplicated client contract — so do not import Phase 5's "duplicate the wire shape" pattern
  here; it solves a problem Task 28 does not have.

STANDING RULES WORTH REPEATING BECAUSE EACH HAS COST A CYCLE.

* If a domain concept changes, edit docs/ontology.ttl, run node scripts/build-ontology.mjs, and
  commit both files together.
* Lint the plan directory explicitly before staging — the recipe is in the handover's section 9.
  The checker only sees tracked Markdown, and only flags backticked PascalCase terms; a status or
  readiness string used as a plain code literal will not be caught even if it is wrong, so check
  those against docs/ontology.md by hand rather than trusting the linter's silence.
* In plan prose, backtick a PascalCase word only if the ontology defines it. Class, test, service
  and file names go in plain text or inside fenced code blocks.
* Stage explicit paths. Never git add -A.
* Every third-party GitHub Action is pinned by commit SHA, per design 07's CI/CD table — this
  applies to anything Task 29's compose-smoke.yml and Task 31's images.yml add.
* Never push to main, never merge. One commit per task on the phase branch.
* The ai-fingerprint check goes red once on every push and that red is not yours to fix. Wait until
  the pull request reports the head you just pushed, then update the body.

Finish each task with its own commit and push on the phase branch. Do not open a pull request
without asking me first — and when you do, Task 31's is decision-bearing, so it carries the
narrative-required label and the three narrative headings.
