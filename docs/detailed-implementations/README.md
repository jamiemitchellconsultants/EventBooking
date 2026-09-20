# EventBooking detailed implementation plans

**Phases 0 and 1 are complete and executable; Phase 2 is under way. Phases 3–7 are not yet written.** Tasks 1 to 10, including the lettered splits 3a–3d and 9a–9b, are documented, implemented and replayed from these documents into an independent checkout, ending at a green build and 1557 passing tests. Master Tasks 11–33 still have to be authored; read [the authoring handover](HANDOVER.md) before continuing that work.

Start at [phase-0-port-and-strip.md](phase-0-port-and-strip.md), which gives the branch, the task order and the phase pull request. [phase-1-domain.md](phase-1-domain.md) carries Phase 1 — variable-length windows, Admin-managed reference data, N-type negotiation, N-row capacity, and location-restricted invites with a closed attendee status table — and ends with its own pull-request gate. [phase-2-persistence.md](phase-2-persistence.md) carries Phase 2 — the attendee link the server can reproduce, the fresh schema with its constraints and database roles, and the ordered row-lock helpers with their concurrency harness; its last task, the eligibility query, is still to be written.

Execute the master plan's phases in this order: 0 port and strip (Tasks 1–3), 1 domain (4–8), 2 persistence (9–11), 3 application (12–20), 4 API and MCP (21–23), 5 web (24–27), 6 seed and deployment (28–31), 7 verification and documentation (32–33). Lettered tasks retain their parent's number and commit message. Read and execute one task at a time using superpowers:executing-plans.

These plans target opencode with superpowers and Qwen3.6 27B at Q6. Each task supplies its interfaces, domain context, tests, implementation and validation commands. The predecessor is JointBooking commit 6957928a6c9054372dda915b359f63b73969ebee; the executor does not need access to that repository.

The user approved retaining predecessor domain names temporarily in Task 1. Task 2 removes them before the Phase 0 pull request. This exception preserves the master plan's separate import and vocabulary-refactoring tasks; it does not permit predecessor names in the completed phase.

## Prerequisites

Install the .NET 10 SDK, Docker for PostgreSQL Testcontainers, Node 20 for the ontology scripts, Git and GitHub CLI. Docker must be running; container tests are mandatory. Authenticate GitHub CLI with permission to push feature branches and open pull requests. Use Bash for shell blocks and run from the repository root.

```bash
dotnet --version
docker info
node --version
gh auth status
git status --short --branch
```

The co-author footer comes from the executing harness. Set EXECUTOR_COAUTHOR to its exact required `Name <email>` value before running a task's commit command; do not substitute the plan author's identity. Each commit command refuses an unset value.

## One branch and pull request per phase

Start each phase from the latest origin/main after the preceding phase's pull request has merged. Use the phase's branch command. Never push to main or merge a pull request as part of executing these plans. Code-owner review and required CI remain in force.

Phase 0 starts with:

```bash
git fetch origin
test -z "$(git status --porcelain)"
git switch -c codex/phase-0-port-and-strip origin/main
```

Every task ends with a commit and push. Stage explicit paths only. This follows the request's explicit prohibition of broad staging, overriding the generic broad-staging example in AGENTS.md. The requested phase filenames at this directory level likewise override its default slug-directory layout.

## Resuming a failed or interrupted task

```bash
git status --short --branch
git log -5 --oneline
git diff
git diff --cached --name-only
git diff --cached
```

Resume the first incomplete step on the existing phase branch. If the commit exists but the push failed, retry the push without creating another commit. If an exact before-snippet no longer matches, inspect whether that edit already happened. Preserve unrelated working and staged changes.

Use superpowers:systematic-debugging for a failing test or build. Keep the regression test, fix the cause, and repeat the affected checks. Do not skip Testcontainers, suppress documentation warnings, or proceed to the next task on a failing build. A failed push is also an incomplete task.

## Verification evidence

Each phase carries its own checkpoint evidence: [Phase 0](phase-0-port-and-strip.md#verification-evidence), [Phase 1](phase-1-domain.md#verification-evidence) and [Phase 2](phase-2-persistence.md#verification-evidence). The unmodified predecessor's full suite passed in this session: Domain 230, Application 451, Infrastructure 158, Api 225, Mcp 34, Web 251 and SeedData 76; total 1425, zero failures and zero skipped tests. The runtime was .NET SDK 10.0.400 with Docker 29.4.3. This is the baseline for accounting for removed provider tests and added port regressions; it is not evidence that a future implementation is complete.

The governing inputs are the [decision record](../superpowers/specs/2026-09-19-eventbooking-design.md), [design package](../design/README.md), [master plan](../superpowers/plans/2026-09-19-eventbooking-implementation.md) and [ontology](../ontology.md). The design wins on detail; the spec wins on decisions. Canonical names come from the ontology.
