---
date: 2026-09-21
slug: phase-4-api-conventions-the-endpoint-catalogue-and-mcp-parity
title: "Phase 4 — API conventions, the endpoint catalogue and MCP parity"
summary: "**Master Task 22 is split into 22a and 22b**, the way master Task 20 was split into 20a and 20b. Task 22a adds the missing Application queries, read models and command shapes; Task 22b is the endpoint catalogue over them and holds no rule."
kind: product
status: accepted
sequence: 2026-09-21T20:00:51.000Z
evidence: "https://github.com/jamiemitchellconsultants/EventBooking/pull/27; merge commit 09a6086a84ee7d9dcad3400982fb7400459dbfb2"
---

## Context

Master Task 22 scopes itself as wiring: "Consumes: every Phase 3 handler", and "endpoints only
translate HTTP to handler calls; no business rule lives in an endpoint". Mapping design 05's
endpoint tables onto what Phases 0 to 3 actually produce showed that seven of its endpoints have
nothing to consume. Task 20a deletes the ported operations handler outright, and Task 13's
negotiation board takes a staff identity and no filters, so the filtered `Event` list, the
single-`Event` read and the cancellable-`Event` list have no handler at all; `includeInactive`,
an update carrying an active flag, and the capacity route's appointment-type identifier have no
command shape behind them either.

Filtering those in the endpoint was the available shortcut. It is also the in-memory shape Task
11 removed from the eligibility path at some 680 ms against 18 ms, and it would have put an
authorization decision into the transport layer — so it was not a neutral fallback.

Nine further contradictions surfaced between design 05, design 06, design 04 and what Phase 3
settled. Four of them are about the error catalogue: design 05 names slugs that no Phase 3 code
can produce, and Phase 3 produces refusals design 05 names no slug for. Two are about
capabilities: design 05 gives `GET /api/events` and `GET /api/audit` two capabilities each,
against design 04's rule that a handler demands exactly one.

## Decision

**Master Task 22 is split into 22a and 22b**, the way master Task 20 was split into 20a and 20b.
Task 22a adds the missing Application queries, read models and command shapes; Task 22b is the
endpoint catalogue over them and holds no rule. Both carry the master plan's single commit
message, because the master plan's messages never change.

**Two capabilities on one route follow Task 20b's audit precedent.** The handler demands neither
and filters by whichever the caller holds — a Manager sees their own type's view, an Admin or a
Coordinator sees every type — with the filter enforced in the query rather than only in the
handler, so bypassing the capability check still filters.

Eight further contradictions were put to the user and settled, and are recorded as entries #11
to #18 in the handover's section 8:

- **Attendee readiness demands `ViewAttendeeDashboards`**, per design 05, not the ported
  handler's `ManageAttendees`.
- **An expired attendee token returns 410, distinct from 404.** The ported handlers collapsed
  every token failure into one answer; design 05 and design 06 both distinguish a lapsed link,
  which is what lets the page say who to contact. `Used`, `Superseded` and `Cancelled` stay
  indistinguishable from a forgery, because those states are not the holder's doing.
- **`proposal-not-open` becomes a typed error**, so the status travels in the problem body
  rather than only in its prose.
- **`last-admin` is reachable from the staff-access endpoint**, not from per-request role
  synchronisation, which keeps its silent refusal and its alert: a background reconciliation must
  not fail an unrelated request because of someone else's identity-provider change.
- **The error catalogue gains four slugs design 05's table omits** — `not-found`,
  `requirement-mismatch`, `already-confirmed` (which is a Phase 3 settlement postdating the
  design) and `conflict` as the residual generic. Every slug design 05 does name still has a
  producing error code, which the catalogue test asserts.
- **The outbox row's correlation identifier is the request's where one exists.** An earlier
  settlement records the column as the dispatcher's; master Task 21 asks for the request's to be
  propagated in. Both hold once the dispatcher's claim coalesces rather than overwrites.

Contradiction #3, settled during Phase 3, becomes real here: a missing or malformed `staff_id`
is 403 on every staff route except `GET /api/me`, and `unauthenticated` stays 401 for a missing
or invalid bearer token — two different failures that must not be merged.

**One shared operation catalogue is the spine of both surfaces.** The OpenAPI document, the
`/api` link index, the hypermedia links on each representation and the MCP tool list all read
from it, so a route cannot appear in one and be missing from another. Task 22b's catalogue test
parses design 05's own tables — fifty-four rows, verified against the design as it stands — and
compares them against the served document, including the capability each row names.

## Consequences

Task 22a deletes the three set-active handlers Task 12 introduced, because design 05's single
update body carries the active flag and one endpoint calling two handlers would be a rule in the
endpoint. Task 22b deletes the four ported hypermedia response files and the ported
administration endpoint file, and Task 23 deletes two ported tool files: almost none of the
predecessor's route surface survives design 05, and the catalogue test is what makes that
comprehensive rather than approximate.

Three documents change Application or Domain code that earlier phases wrote. Task 21 repoints
the token handlers, three negotiation catch blocks and the staff-scope handler at typed errors;
Task 22a folds activation into the reference-data updates and adds the appointment-type
identifier to the capacity command. Each is named in its task's Files list with its reason, and
Task 12's three reference-data suites need their handler constructions updated alongside.

Authoring Task 21 also found something it did not fix: **Phase 3's transitional-construct table
claims the single-zone clock retires there, and no Phase 3 document removes it.** The clock
options type, its configuration key and the transitional member on the system clock all survive.
Task 21 made the key optional rather than inventing a removal that would leave the
infrastructure registration asking for options nothing supplies. The table now says Phase 6, and
section 8 records why.

**Two rounds of review ran before execution, and the second found that the phase could not
start.** Phase 3 reshaped ListAttendeesQuery and GetDashboardsQuery and deleted seven handlers
without updating their call sites in the Api and Mcp projects, so the solution does not build on
`main` — while every task here gates its commit on a green build and test run. Task 21 now opens
by restoring the build, and it repairs by removal: the orphaned routes, tools and the ported
suites that drive them are deleted, because Tasks 22b and 23 replace that surface with design
05's anyway, and writing a second implementation of a route about to be deleted would be work
thrown away twice. The attendee list is the exception, repaired onto Phase 3's paged query
because Task 21's own pagination suite drives it. Section 8 records this as settlement #19. The
consequence is that the suite count falls at Task 21 and rises again at Tasks 22b and 23.

The first round's corrections are in the same branch: every attendee route and tool written as
code rather than prose, the read-model fragments moved onto the mapped persistence shapes
Phases 0 to 3 define, MCP failures keeping their application error code, both attendee rate
limits actually composed, forwarded headers trusted only when a proxy network is configured,
attendee tokens kept out of every log scope and out of the framework's own request logging, and
same-key idempotent requests serialised. Two types the catalogue used but never declared, and a
rate-limit policy name that was no longer registered, were found while applying them.

What stays deliberately open: **the method for Phase 5 is not decided here.** Section 11's
checkpoint asks whether the web work carries on hand-authored with a heavier review, or whether
the prototype is brought forward and Phase 5 generated from before-and-after snapshots the way
Phases 0 to 2 were. Phase 4 was thin wiring over handlers that already exist, which is why it
stayed hand-authored; Phase 5 is dense components whose failure modes are visual, where neither
mechanical sweep helps much. Answering it as a side effect of finishing Phase 4 is exactly what
the handover warns against, so it is left for the user.

---

AI-Fingerprint: sha256:dbac3c578e5f
