---
date: 2026-09-21
slug: the-hand-authored-task-loop-and-a-method-checkpoint-before-phase-5
title: "The hand-authored task loop, and a method checkpoint before Phase 5"
summary: "Write the hand-authored loop down, and make its checks mechanical rather than attentional. The two sweeps are embedded in the handover as runnable scripts, not described."
kind: product
status: accepted
sequence: 2026-09-21T08:57:12.000Z
evidence: "https://github.com/jamiemitchellconsultants/EventBooking/pull/22; merge commit 7cf42abe7b4f43cfb39f6aad913e5632b4e8ab1b"
---

## Context

Phases 0–2 were prototype-verified: implemented in a scratch checkout, compiled with warnings as
errors, tested, then generated from before/after snapshots. A whole class of defect could not reach
those documents, because the compiler rejected it first. Phases 3–7 are hand-authored by an earlier
decision, and that decision removed the compiler without replacing it with anything written down.

Phase 3's review showed the cost. It found two methods that would have failed the executor's own
build, a read model whose shape contradicted the requirement it cited, a lock order that trips the
guard its own Task 15 installs, and nine blocks labelled complete that described code instead of
being it. None of these are subtle; all of them survived because nothing mechanical ever looked.

## Decision

Write the hand-authored loop down, and make its checks mechanical rather than attentional.

The two sweeps are embedded in the handover as runnable scripts, not described. That distinction is
the point of the change, and it was tested on itself: running them verbatim out of the document
before committing caught three defects in the instructions — an inner fence that closed the outer
block, a glob that flooded on Phase 0's file manifest where code legitimately lives in separate edit
volumes, and generated EF migrations reported as gaps when they are generate-and-review by
convention. An instruction nobody has executed is a description of an instruction, which is the same
failure the loop exists to prevent one level down.

Phase 4 stays hand-authored, because Tasks 21–23 are endpoint and tool wiring over handlers Phase 3
already specifies: thin, repetitive code where the method fits. Phase 5 is deliberately left open
rather than settled now. The web work is dense, its failure modes are visual, neither sweep helps
much against markup, and a reviewer cannot tell a working page from a plausible one by reading it.
The alternative — bringing the prototype back and generating Phase 5 from snapshots — is available
rather than theoretical, since the prototype still exists and is green at Task 11, but it would
first have to be carried forward through Tasks 12–23. That trade is a user decision, and the
checkpoint says so rather than letting momentum decide it at the moment someone starts Task 24.

## Consequences

Phase 4 has a written method before anyone starts it, which Phase 3 did not. The sweeps are cheap
enough to run per task rather than per phase, and both have already found real defects after the
fact — the expensive time to find them.

Running the gap sweep against the current Phase 3 documents reports seven entries that are still
named in a Files list without their code: the reference-data repository and its in-memory fake and
one settings test in Task 12, two authentication files in Task 17, and two view handlers in Task 18.
They are the remainder of a tail already identified in review, and the sweep now surfaces them
rather than relying on someone remembering. They are not fixed here; this change is the instrument,
not the repair.

What the sweeps cannot see is stated in the handover rather than left implied: an `await` inside a
nested lambda still reads as present to the async sweep, and no mechanical check distinguishes a
correct component from a plausible one. That limit is the reason Phase 5's checkpoint exists.

---

AI-Fingerprint: sha256:7395de55ca96

🤖 Generated with [Claude Code](https://claude.com/claude-code)
