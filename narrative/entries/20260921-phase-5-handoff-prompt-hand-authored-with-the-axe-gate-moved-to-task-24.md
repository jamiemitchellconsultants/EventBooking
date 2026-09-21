---
date: 2026-09-21
slug: phase-5-handoff-prompt-hand-authored-with-the-axe-gate-moved-to-task-24
title: "Phase 5 handoff prompt: hand-authored, with the axe gate moved to Task 24"
summary: "**Phase 5 stays hand-authored, and the Playwright and axe-core project moves from Task 27 to Task 24.** Task 24 creates it alongside the design-system components, every later task adds its own routes to it, and the phase gate is zero axe…"
kind: product
status: accepted
sequence: 2026-09-21T20:08:58.000Z
evidence: "https://github.com/jamiemitchellconsultants/EventBooking/pull/29; merge commit 8d0a6ecda132b13e9ade7e3e86dc26f81813a71b"
---

## Context

Section 11's fourth next step says the method for Phase 5 is a user decision, not an authoring one,
and that it must be settled before Task 24 is written. The reason it was held open: Phase 4 was
thin wiring over handlers that already existed, which is why it stayed hand-authored, but Phase 5
is dense components whose failure modes are visual. Neither of section 6a's mechanical sweeps
reaches them — the gap sweep still works, the async sweep matches little in markup — and a reviewer
reading Razor cannot tell a working page from a plausible one. That is the same gap the compiler
filled for Phases 0 to 2 and nothing has filled since Task 11.

The two options on the table were to carry on hand-authoring and budget a heavier review, or to
bring the scratch prototype back and generate Phase 5 from before-and-after snapshots the way
Phases 0 to 2 were built.

## Decision

**Phase 5 stays hand-authored, and the Playwright and axe-core project moves from Task 27 to Task
24.** Task 24 creates it alongside the design-system components, every later task adds its own
routes to it, and the phase gate is zero axe violations on every route at mobile and desktop
widths. The axe job in the build workflow moves to Task 24 with it.

The move is the point. Left where the master plan puts it, three tasks of components would be
written before anything rendered them; brought forward, each task is checked by something that
actually loads the page, which is the cheapest available substitute for the compiler the
prototype-verified phases had.

**The prototype is not brought forward.** Doing so would mean executing twelve tasks of Phase 3 and
Phase 4 work in the scratch checkout before Task 24 could begin, and what that buys is verification
of the server phases that are already written rather than of the components Phase 5 adds.

Writing the prompt also turned up three places where the master plan's Phase 5 names files that do
not exist, each checked against the edit volumes: it calls the negotiation page `Negotiate.razor`
"(was the slots page)" when Phase 0's vocabulary rename already made it `EventNegotiation.razor`;
it says to rewrite role guides under `wwwroot/help/` when no such file exists and Task 27 must
create them; and it describes the branded assets Task 24 deletes as a category rather than as the
named files they are. All three are written into the prompt as traps, with the real names.

## Consequences

Task 24 grows by the E2E project and the workflow job, and Tasks 25, 26 and 27 each gain a step
that adds their routes to the axe sweep. The Phase 5 author records the move as a settlement in the
handover's section 8 and answers section 11's checkpoint with it; Phases 6 and 7 inherit the method
that settles here.

Nothing in this pull request is executed and no test figure is claimed. The prompt says so twice,
and it tells its agent that the comparison against Task 11's 1570 stopped being meaningful at Task
12 — Task 21 deliberately lowers the suite count before Tasks 22b and 23 raise it.

The prompt depends on pull request #27 having merged, and says so as its first instruction: Phase 5
is a client for the endpoint catalogue, the problem catalogue, the page contract, the event-time
representation and the capability links that Phase 4 puts on the wire.

---

AI-Fingerprint: sha256:88228812f515
