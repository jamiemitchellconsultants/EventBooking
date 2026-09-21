<!-- A handoff prompt: paste the whole of this file into a fresh agent. It is written to be
     self-sufficient for an agent on a different machine, with no access to the scratch
     prototype Phases 0-2 were built in. Provenance: written 21 September 2026, after Phase 3
     merged. -->

Work on the EventBooking repository: https://github.com/jamiemitchellconsultants/EventBooking

Clone it fresh, then read docs/detailed-implementations/HANDOVER.md before anything else. It is
written for an agent with no context and tells you the assignment, the governing inputs and their
precedence, the standing rules that have already bitten, and how far the work has got. Follow it.

Your job: author Phase 4, master Tasks 21, 22 and 23 — API conventions, the endpoint catalogue and
MCP parity. Their specs are in docs/superpowers/plans/2026-09-19-eventbooking-implementation.md
under "Phase 4 — API and MCP"; the endpoint contract itself is docs/design/05-api-design.md.

BEFORE YOU START, check two pull requests have merged. If either is still open, stop and say so
rather than working around it:

* #22 adds section 6a to the handover — the loop for a hand-authored task. That section IS your
  method. Without it you would be authoring Phase 4 the way Phase 3 was authored, which is what
  this whole step exists to avoid.
* #23 closes seven Phase 3 gaps. Phase 4 consumes Phase 3's handlers, so you want them complete.

THE PROTOTYPE IS NOT ON YOUR MACHINE. Read this before you go looking for it.

Phases 0-2 were built in a scratch checkout under /private/tmp on one particular machine. The
handover references it, section 6a's step 4 tells you to read it, and it does not exist for you.
Do not try to recreate it and do not guess at type shapes because you cannot see it.

You do not need it. This repository carries every file's complete content: the edit volumes hold
before and after sides for every change through Task 11. To see any file as it stands at the end of
Phase 2, save this script as at-task-11.py in the repository root and run it. It is tested — these
exact invocations were verified working:

```python
#!/usr/bin/env python3
"""Print a file's content as it stands at the end of Phase 2 (Task 11).

Usage: python3 at-task-11.py src/EventBooking.Api/Endpoints/ResultResponses.cs
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

Two caveats on it. It only covers Tasks 1-11, because Phase 3 is hand-authored and has no edit
volumes — for a Phase 3 type, read the phase-3*.md document that creates it. And a file created
after Task 11 or deleted before it returns nothing, which is information rather than an error.

State you can rely on, so you do not re-derive it:

* Phases 0, 1 and 2 are prototype-verified, replayed and merged. The last measured checkpoint is
  Task 11: 1570 tests — Domain 360, Application 421, Infrastructure 206, API 232, MCP 35, Web 241,
  SeedData 75.
* Phase 3 (Tasks 12-20, with Task 20 split into 20a/20b) is written, reviewed and merged. It has
  never been built or tested, and section 7 of the handover explains why it has no test figures and
  must not be given any. Yours will not either. Say so in each document's own Step 4.
* Phase 4 stays hand-authored. That was settled deliberately: Tasks 21-23 are endpoint and tool
  wiring over handlers Phase 3 already specifies, so the code is thin and repetitive and the method
  fits. Do not reopen it.
* Phase 5 is a different question, and the handover carries an explicit checkpoint for it. Do not
  answer it as a side effect of finishing Phase 4. If you reach Task 24, stop and put it to the
  user.
* Task 21 inherits a settlement it has to implement: contradiction #3 was settled as 403 for a
  missing or malformed staff_id everywhere except /api/me, against the API catalogue's 401. Phase 3
  settled the rule; the endpoint layer is where it becomes real. Section 8 records it.
* You are creating the phase overview too — phase-4-api-and-mcp.md, modelled on
  phase-3-application.md. Name the task documents phase-4a-, phase-4b-, phase-4c-, continuing the
  lettering. Task 23 carries the phase's pull-request gate.

Follow section 6a's loop for every task. Its two mechanical sweeps are the part people skip, so
run them per task rather than per phase, and print how many the detector matched — a clean sweep
where nothing was matched is worthless, and that has already happened once. For reference, the
async sweep over Phase 3 matches 180 methods and over Phases 0-2 matches 7,978, clearing all of
them.

Standing rules worth repeating because each has cost a cycle:

* If a domain concept changes, edit docs/ontology.ttl, run node scripts/build-ontology.mjs, and
  commit both files together.
* Lint the plan directory explicitly before staging — the recipe is in the handover's section 9.
  The checker only sees tracked Markdown.
* In plan prose, backtick a PascalCase word only if the ontology defines it. Class, test and file
  names go in plain text or inside fenced code blocks.
* Stage explicit paths. Never git add -A.
* Never push to main, never merge. One commit per task on the phase branch.
* The ai-fingerprint check goes red once on every push and that red is not yours to fix. Wait until
  the pull request reports the head you just pushed, then update the body.

Finish each task with its own commit and push on the phase branch. Do not open a pull request
without asking me first.
