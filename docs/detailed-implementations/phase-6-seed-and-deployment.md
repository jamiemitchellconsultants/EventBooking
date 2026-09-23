# Phase 6 — Seed and deployment (Tasks 28–31)

[← Plans overview](README.md) · [Phase 5](phase-5-web.md) · [Master plan](../superpowers/plans/2026-09-19-eventbooking-implementation.md) · [Ontology](../ontology.md)

> Use superpowers:executing-plans. Execute one task document at a time, in the order below. Every
> task ends with its own commit and push on the same phase branch.

**Status: complete as a plan; not executed.** All four documents are hand-authored. Complete tests,
source fragments, container definitions and workflow files are written into the task documents,
and the executing model must compile, test and exercise them. No Phase 6 code or deployment has
been run from these documents, so the phase has no observed build, test, image or rehearsal result.

**Goal:** Replace the predecessor demo seed with an explicit migrate-only-by-default CLI and a
generalised, idempotent dataset, then ship reproducible local and home-lab container deployments
and a release pipeline for all five images plus the EF migrations bundle.

**Architecture:** Task 28 separates argument validation from side effects, retires the last
single-zone clock surface, and rebuilds demo state through the real domain model. Task 29 packages
the API, MCP server, Web client and seed CLI into a local Compose stack with PostgreSQL, Keycloak
and Mailpit. Task 30 layers a private home-lab topology behind existing Caddy and Keycloak services,
with backup, restore and an idempotent installer. Task 31 publishes immutable GHCR tags and the
self-contained migration bundle.

**Tech Stack:** .NET 10, EF Core 10, PostgreSQL 16, NodaTime, Keycloak 26, Mailpit, Docker Compose,
nginx, Caddy, GitHub Actions and GHCR.

**Spec:** [Deployment](../design/07-deployment.md),
[security and authentication](../design/06-security-and-authentication.md),
[non-functional requirements](../design/08-nonfunctional-requirements.md), and
[master plan Tasks 28–31](../superpowers/plans/2026-09-19-eventbooking-implementation.md).

## Settlements carried by this phase

The seed brief originally required exactly five appointment types while also requiring an open
proposal listing four types. MED, FIT and IND were the only types both active and managed: ESC is
deliberately unmanaged and DOC deliberately inactive, and the domain refuses either on a proposal.
The user settled this by adding LAB as a sixth, active, managed type plus a LAB Manager demo user.
Events and proposals may therefore cover one through four types without weakening the ESC disabled
picker or DOC inactive-reference cases.

Task 28 also retires the single-zone clock in full. IClock becomes a UTC-instant-only port,
SystemClock becomes parameterless, ClockOptions and the Clock:TimeZoneId configuration key are
deleted, and the seed derives local dates through each seeded location's IANA zone. This closes the
transitional construct left open by Tasks 12–27.

No master task boundary moves in this phase. Several ported files already exist despite the master
plan saying “Create”: the three project Dockerfiles, `.dockerignore`, both realm files and the
home-lab README are modifications. The task documents state their real operations; all genuinely
new Compose, Caddy, seed-container and workflow files remain in their master tasks.

## Global constraints

- Running the seed CLI without `--demo` performs database-role convergence and migrations only. It
  does not read SMTP demo settings, call Keycloak's Admin API, insert domain rows or send email.
- `--reseed` is destructive and is rejected before any database or network side effect unless
  `--demo` is present and `EVENTBOOKING_ALLOW_RESEED=true` exactly.
- Demo upserts use natural keys: reference-data code, staff user id, attendee email, and
  location/date/start-time for proposal or event scenarios. Stable GUIDs are payload, never the
  sole match condition.
- LAB is active and managed. ESC is active and unmanaged. DOC is inactive. Seeded events and open
  proposals list only MED, FIT, IND and LAB.
- The realm is `eventbooking`; the public client is `eventbooking-web`; access tokens live for five
  minutes and carry flat `roles`, `staff_id`, `name` and the client audience.
- Every runtime image has a non-root final user and uses the repository-root `.dockerignore`.
- Local development may publish PostgreSQL, Keycloak, Mailpit, API, Web and MCP ports. The home-lab
  database and API publish no host port; only containers on the private or external edge networks
  can reach them.
- Every new GitHub Action reference is pinned to a full commit SHA. Image tags are `latest` and
  `sha-<seven characters>` on `main`, and the exact `vX.Y.Z` ref on version tags.
- Migrations are forward-only and expand-before-contract for one release. Rollback changes the
  image tag only; no down migration is run.
- Phase 6 is hand-authored. Every reported build, test, image-user, smoke, install, upgrade and
  restore result must come from the executor's run, never from this document.

## Review focus

1. A typo, unknown option, `--reanchor` without `--demo`, or unauthorized `--reseed` fails before
   roles, migrations, Keycloak, truncation, demo insertion or email.
2. Re-running `--demo` preserves exact natural-key row counts and does not duplicate invitations,
   role mappings or email deliveries; reanchoring moves only the demo scenarios it owns.
3. No event or open proposal lists ESC or DOC, while coverage still proves type counts one, two,
   three and four and acceptance states one-of-two and two-of-four.
4. Compose health ordering waits for PostgreSQL, Keycloak and Mailpit, and the smoke workflow proves
   readiness, API discovery and at least one captured invitation rather than only parsing YAML.
5. A home-lab restore targets a fresh volume, remains under the two-hour RTO, and leaves the old
   volume untouched until the restored stack passes readiness.

## Before you start

Phase 6 starts only after the Phase 5 pull request has merged. Pull request #31 merged on
22 September 2026; still fetch and start from the latest `origin/main` when executing:

```bash
git fetch origin
test -z "$(git status --porcelain)"
git switch -c codex/phase-6-seed-and-deployment origin/main
export EXECUTOR_COAUTHOR="Your Harness <harness@example.invalid>"
```

Before Task 28, run the full solution once. Phases 3–5 are hand-authored and may expose a compile
failure the plan author could not observe. Use superpowers:systematic-debugging for a real failure;
do not weaken Task 28 or silently fold an unrelated repair into its commit.

## Task order

| Order | Task | Document | Commit message |
| --- | --- | --- | --- |
| 1 | Task 28 — migrate-only CLI, generalised demo dataset and clock retirement | [phase-6a-seed-cli.md](phase-6a-seed-cli.md) | `feat(seed): migrate-only default and generalised demo dataset` |
| 2 | Task 29 — local Compose stack and smoke workflow | [phase-6b-local-compose.md](phase-6b-local-compose.md) | `feat(deploy): local Compose stack without MinIO` |
| 3 | Task 30 — home-lab deployment, installer and recovery runbook | [phase-6c-home-lab.md](phase-6c-home-lab.md) | `feat(deploy): home-lab deployment with reference install script` |
| 4 | Task 31 — image and release pipeline plus phase gate | [phase-6d-images-and-release.md](phase-6d-images-and-release.md) | `ci: publish images and migrations bundle` |

## Artifact ownership

| Task | Owns |
| --- | --- |
| 28 | CLI modes, natural-key demo data, Keycloak user convergence, demo invitations, clock retirement |
| 29 | Root Compose topology, local realm, project Dockerfiles, nginx local image, Compose smoke CI |
| 30 | Home-lab Compose topology, Caddy layers, environment contract, installer, backup/restore runbook |
| 31 | GHCR matrix, immutable tags, release-time migrations bundle, Phase 6 pull-request gate |

## Verification evidence

No verified count exists for Phase 6. Like Phases 3–5, it is hand-authored and has never been
executed. The last measured checkpoint remains Task 11: Domain 360, Application 421,
Infrastructure 206, API 232, MCP 35, Web 241 and SeedData 75; total 1570. Tasks 12–27 deliberately
make that comparison non-linear. Each task records the executor's own before/after result instead
of copying 1570 forward.

## Per-task authoring checks

Run the hand-authored gap and async sweeps from [HANDOVER section 6a](HANDOVER.md#6a-the-loop-for-one-hand-authored-task)
after each task document is applied. The C# gap sweep must report no missing file stem and the async
sweep must print a non-zero match count with zero no-await failures. Those checks do not inspect
Compose, Dockerfile, JSON, shell or workflow blocks; run the format-specific commands in Tasks
29–31 as well.

## Pull request

Task 31 carries the phase gate. Phase 6 is decision-bearing because it fixes the seed defaults,
settles the sixth managed type, closes the single-zone transition and establishes both deployment
shapes. The body therefore carries the `narrative-required` label, the three exact Narrative
headings and the current AI-Fingerprint. Do not merge it; code-owner approval and CI remain
mandatory.
