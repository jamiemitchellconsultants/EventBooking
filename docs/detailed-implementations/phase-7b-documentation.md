# 07b — Application and operator documentation (Task 33)

[← Phase overview](phase-7-verification-and-documentation.md) · [Previous task](phase-7a-load-test.md) · [Ontology](../ontology.md)

This task is the final documentation layer of the master plan. It turns the local Compose contract
into a fresh-clone demonstration, finishes the home-lab operator procedure and reconciles the
design package with the six-type seed and the booking lock-hold measurement.

> Use superpowers:executing-plans. Apply this document after Task 32 on the Phase 7 branch.

**Goal:** Make a first local run and the home-lab install, upgrade, rollback, backup and recovery
procedures reproducible from checked-in instructions.

**Architecture:** The root README is the application's entry point. The demo runbook fixes the
order of Compose, seed and user actions; the operator runbook keeps Task 30's isolated topology and
fresh-volume restore rule. A documentation contract test pins commands and safety-critical text.

**Tech Stack:** Markdown, Docker Compose, Bash, xUnit and the Task 29 local stack.

**Spec:** [Master Task 33](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[deployment](../design/07-deployment.md), [verification](../design/08-nonfunctional-requirements.md)
and [Phase 7 overview](phase-7-verification-and-documentation.md).

### Task 33: README, demo runbook and final operator guide

**Files:**

- Modify: README.md
- Create: docs/runbooks/demo.md
- Modify: deploy/home-lab/README.md
- Modify: docs/design/07-deployment.md
- Modify: docs/design/08-nonfunctional-requirements.md
- Test: tests/EventBooking.Api.Tests/Documentation/RunbookContractTests.cs

The five Help guides under `src/EventBooking.Web/wwwroot/help/` describe role actions and attendee
outcomes. Task 28 added LAB as managed reference data, not a new UI action; Tasks 29–31 changed
deployment, not the role flows. None of the five guides needs a Task 33 edit. In particular, do not
hard-code a list of seeded types into a role guide.

**Interfaces:**

```csharp
namespace EventBooking.Api.Tests.Documentation;

public sealed class RunbookContractTests
{
    // Consumes Task 29's Compose commands and Task 28's seeded user contract.
    public void Local_runbook_keeps_the_three_ordered_Task_29_commands() { /* Step 1 */ }
    public void Demo_identifies_real_seed_users_and_future_date_limit() { /* Step 1 */ }
    // Consumes Task 30's install, backup, restore and image-tag contract.
    public void Operator_guide_preserves_safe_recovery_and_release_procedure() { /* Step 1 */ }
    // Produces the final entry-point and design-package checks.
    public void Entry_point_and_design_match_six_managed_and_unmanaged_types() { /* Step 1 */ }
}
```

- [ ] **Step 1: Write the failing test**

Create the following complete xUnit test file. It reads the files that the next step writes, so
its first run must fail on the missing runbook and stale root README. This test protects the safety
critical commands, not the prose style.

```csharp
// tests/EventBooking.Api.Tests/Documentation/RunbookContractTests.cs (complete)
using Xunit;

namespace EventBooking.Api.Tests.Documentation;

public sealed class RunbookContractTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "../../../../../"));

    private static string Read(string path) => File.ReadAllText(Path.Combine(Root, path));

    [Fact]
    public void Local_runbook_keeps_the_three_ordered_Task_29_commands()
    {
        var demo = Read("docs/runbooks/demo.md");
        var infrastructure = demo.IndexOf(
            "docker compose up --detach --build --wait postgres keycloak mailpit",
            StringComparison.Ordinal);
        var seed = demo.IndexOf(
            "docker compose --profile seed run --rm seed --demo --reanchor",
            StringComparison.Ordinal);
        var apps = demo.IndexOf(
            "docker compose up --detach --build --wait api mcp web",
            StringComparison.Ordinal);

        Assert.True(infrastructure >= 0 && seed > infrastructure && apps > seed);
        Assert.Contains("http://localhost:5001/health/ready", demo);
        Assert.Contains("http://localhost:5002", demo);
        Assert.Contains("http://localhost:8025", demo);
    }

    [Fact]
    public void Demo_identifies_real_seed_users_and_future_date_limit()
    {
        var demo = Read("docs/runbooks/demo.md");
        foreach (var user in new[] { "admin", "coordinator", "coordinator.med", "manager.fit",
                     "manager.ind", "manager.lab", "appointment.med", "appointment.unscoped" })
            Assert.Contains(user, demo);
        Assert.Contains("EventBooking1!", demo);
        Assert.Contains("demo.attendee.01@example.test", demo);
        Assert.Contains("local date", demo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Operator_guide_preserves_safe_recovery_and_release_procedure()
    {
        var guide = Read("deploy/home-lab/README.md");
        Assert.Contains("EVENTBOOKING_IMAGE_TAG", guide);
        Assert.Contains("--profile backup", guide);
        Assert.Contains("pg_restore", guide);
        Assert.Contains("fresh volume", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not run a down migration", guide);
        Assert.Contains("/health/ready", guide);
    }

    [Fact]
    public void Entry_point_and_design_match_six_managed_and_unmanaged_types()
    {
        var readme = Read("README.md");
        var design = Read("docs/design/07-deployment.md");
        Assert.Contains("docs/runbooks/demo.md", readme);
        Assert.Contains("docs/design/README.md", readme);
        Assert.Contains("LAB", design);
        Assert.Contains("6 `AppointmentType`s", design);
        Assert.Contains("--load-fixture", design);
        Assert.Contains("EVENTBOOKING_ENABLE_LOAD_FIXTURE", design);
        Assert.Contains("ESC is active but has no Manager", design);
        Assert.Contains("DOC is inactive", design);
    }
}
```

Run:

```bash
dotnet test tests/EventBooking.Api.Tests --filter FullyQualifiedName~RunbookContractTests
```

Expected: FAIL because the demo runbook does not exist and the root README has not been replaced.
If an earlier hand-authored task does not compile, investigate that independent failure first; do
not count a compile error as this test's red state.

- [ ] **Step 2: Write the application documents**

Replace the target application's root README with this complete file. The documentation-only plan
repository has a different root README; do not overwrite it while authoring this task document.

````markdown
# EventBooking

EventBooking coordinates multi-type events, invitations, bookings and appointments. Managers
propose event times and commit capacity for their own appointment types. Coordinators invite
attendees and manage follow-up. Attendees book from a private link. Staff work within their
assigned roles and appointment-type scope.

## Local quick start

Requires Docker with Compose. From a fresh clone:

```bash
docker compose up --detach --build --wait postgres keycloak mailpit
docker compose --profile seed run --rm seed --demo --reanchor
docker compose up --detach --build --wait api mcp web
curl --fail http://localhost:5001/health/ready
```

Open the Web app at <http://localhost:5002>, the API index at
<http://localhost:5001/api>, and Mailpit at <http://localhost:8025>. The demo seed is opt-in;
running the seed container without `--demo` applies database roles and migrations only. The
local-only demo password is `EventBooking1!`.

Follow the [demo runbook](docs/runbooks/demo.md) for the seeded users and a complete walkthrough.
For the 500-confirmation capacity race, follow [the load-test guide](tests/load/README.md).

## Documentation

- [Design package](docs/design/README.md) — requirements, architecture, security, deployment and
  non-functional targets.
- [Application ontology](docs/ontology.md) — canonical domain terminology.
- [Detailed implementation plans](docs/detailed-implementations/README.md) — task-by-task build
  instructions and their verification status.
- [Home-lab operator runbook](deploy/home-lab/README.md) — install, upgrade, backup and recovery.

## Development checks

```bash
dotnet build EventBooking.sln -warnaserror
dotnet test EventBooking.sln
node scripts/build-ontology.mjs --check
node scripts/check-ontology-terms.mjs
```

Docker must be running for the PostgreSQL container tests. A change that touches domain concepts
starts in `docs/ontology.ttl`; regenerate `docs/ontology.md` with
`node scripts/build-ontology.mjs`. Pull requests follow [AGENTS.md](AGENTS.md), including
code-owner review, the narrative decision sections when applicable and the AI fingerprint.
````

Create the complete demo runbook below. Its fresh-clone path never assumes an event occurs on the
current local date: Task 28 deliberately anchors events days into the future. The seeded checked-in
appointment is a read-only demonstration until its own event date.

````markdown
# Local demo walkthrough

Run this on a disposable local Compose project. The seed uses example identities and Mailpit; do
not use the local password or signing key in a real deployment. Start from a fresh clone with Docker
running and ports 5432, 8081, 1025, 8025, 5001, 5002 and 5003 available.

## Start the stack

From the repository root, run these commands in this order:

```bash
docker compose up --detach --build --wait postgres keycloak mailpit
docker compose --profile seed run --rm seed --demo --reanchor
docker compose up --detach --build --wait api mcp web
curl --fail http://localhost:5001/health/ready
curl --fail http://localhost:5001/api
curl --fail http://localhost:5003/health/ready
```

The seed job must report reference-data and attendee counts, invitation delivery and Keycloak
convergence. If it fails, stop and inspect
`docker compose logs seed postgres keycloak mailpit`; do not continue with a partial
dataset. Re-running `--demo --reanchor` is idempotent. Running the seed without `--demo` migrates
only. Use `--reseed` solely in a disposable local environment with
`EVENTBOOKING_ALLOW_RESEED=true` and `--demo`.

## Open the system

Open <http://localhost:5002> for Web, <http://localhost:5001/api> for API discovery and
<http://localhost:8025> for Mailpit. The local Keycloak realm is `eventbooking`. All eight demo
staff accounts use the local-only password `EventBooking1!`:

| Username | Role and scope | What to inspect |
| --- | --- | --- |
| `admin` | Admin | Locations, six appointment types, groups, settings and staff access |
| `coordinator` | Coordinator | Attendees, invitations, dashboards and audit |
| `coordinator.med` | Coordinator and Manager of MED | Both Coordinator and MED Manager routes |
| `manager.fit` | Manager of FIT | FIT proposal acceptance and capacity |
| `manager.ind` | Manager of IND | IND proposal acceptance and capacity |
| `manager.lab` | Manager of LAB | LAB proposal acceptance and capacity |
| `appointment.med` | AppointmentStaff for MED | MED appointment workspace |
| `appointment.unscoped` | AppointmentStaff without a type | Awaiting-assignment state |

Sign in as `admin` and open `/admin/appointment-types`. MED, FIT, IND and LAB are active and have
Managers. ESC is active without a Manager and cannot be selected for a proposal; DOC is inactive.
Open `/admin/locations` to see active London and Dublin locations and inactive Manchester. Open
`/admin/attendee-groups` to see the IND-only and CORE_THREE examples.

## Follow an invitation through booking

Sign out and sign in as `coordinator`. At `/attendees`, find Nia New,
`demo.attendee.01@example.test`, and inspect her IND requirement and invitation status. Open
Mailpit and find the invitation to that address. Use the link in that email in an anonymous browser
window. It opens `/book/{token}` with event choices and local time and location; choose an offered
event and confirm once. The confirmation displays a manage link. Copy it into the same anonymous
window and inspect the booking at `/manage/{token}`. Keep both links private and do not paste them
into logs or issue reports.

Return to the Coordinator session and refresh `/attendees`. Nia is booked. Open `/dashboards` and
`/audit` to inspect the changed counts and event history. A second use of the same book link is a
refusal naming the existing booking; it creates no second booking. Do not run the 500-way load
scenario on this general demo dataset; it has its own 100-place fixture.

## Inspect negotiation and appointments

Sign in as `coordinator.med` and open `/events/negotiate`. The seeded proposals show one of two
and two of four accepted types. Propose a new future London event listing MED and FIT, then sign
in as `manager.fit` and accept FIT with a positive headcount. The proposal becomes an active event
after both types accept. The proposing type is fixed; each Manager edits only their own headcount.

Sign in as `appointment.med` and open `/appointments`. Inspect the MED roster and the seeded
checked-in example. `appointment.unscoped` instead sees awaiting assignment. A new check-in is
allowed only on that event's local date, and a no-show only after its window ends. The reanchored
seed events are future dated, so this fresh-clone walkthrough does not claim a new check-in can be
performed immediately. Return on the relevant local date to exercise that transition.

## Close and troubleshoot

For a failed health check, inspect `docker compose ps` and `docker compose logs api postgres
keycloak mailpit`. For a missing invitation, inspect the seed command's result and Mailpit; do not
substitute a fabricated book token. Use `docker compose down` to stop the demo while retaining the
database volume. `docker compose down --volumes` deletes this local project's data.
````

- [ ] **Step 3: Finish the home-lab runbook and design package**

Task 30 already created `deploy/home-lab/README.md`. Keep its Install, Upgrade and rollback,
Backup, Restore into a fresh volume and Rehearsal evidence sections. Apply the exact additions
below after its Rehearsal evidence section; do not erase an executor's observed rehearsal record.
The original seven-step installer, image tag and restore commands remain the source of truth.

````markdown
## Verification before and after a release

Record the previous and target `EVENTBOOKING_IMAGE_TAG` values, the backup filename and the
operator before changing the tag. Verify that the existing stack passes `/health/ready` and that
a recent custom-format dump is present outside the host. Run the [local load scenario](../../tests/load/README.md)
against the local Compose topology for the release candidate; it is not a home-lab traffic
generator. Complete the manual attendee keyboard and screen-reader pass required by design 08.

After a version upgrade, check `docker compose ps`, the API readiness route through the private
network, the public Web route, `/api` and `/mcp` through ingress, one authenticated staff read and
one anonymous invitation view. Check the outbox backlog and recent sweep failures in metrics and
structured logs. Do not write tokens or attendee addresses into the release record.

If the new image fails, restore the previous `EVENTBOOKING_IMAGE_TAG` and run
`docker compose up -d`. Do not run a down migration. If the database is damaged, follow the
fresh-volume restore above and prove readiness and a read-only journey before repointing the
application. Keep the original volume until the recovery has been accepted.

## Routine operations

Monitor `/health/ready`, sweep failures, pending outbox age and capacity-exhausted counts.
Investigate an outbox item pending longer than 15 minutes or two consecutive failed sweeps. Check
the nightly backup container's exit status and verify a restorable dump exists every day; a backup
file alone is not a successful restore rehearsal. The deployment target is at most 24 hours of
data loss and two hours to validated readiness after a restore declaration.
````

Update design 07's seed-data enumeration and the local quick-start block. The canonical complete
replacement for the affected lines is:

````markdown
```bash
docker compose up --detach --build --wait postgres keycloak mailpit
docker compose --profile seed run --rm seed --demo --reanchor
docker compose up --detach --build --wait api mcp web
```

- **3 `Location`s:** London and Manchester in `Europe/London`, and Dublin in `Europe/Dublin`.
  Manchester is inactive.
- **6 `AppointmentType`s:** MED, FIT, IND, LAB, ESC and DOC. LAB is active and managed, so a
  four-type event can include it. ESC is active but has no Manager, to demonstrate the
  disabled picker state. DOC is inactive.
- **Keycloak demo users:** one Admin; one Coordinator; one Coordinator who is also Manager of MED;
  Managers of FIT, IND and LAB; two AppointmentStaff, one of them unscoped.
````

Add this row to the seed CLI options table and the paragraph after it:

````markdown
| `--load-fixture` | Disposable local load test only: after `--demo`, create a 100-place event and 500 invitations. Requires `EVENTBOOKING_ENABLE_LOAD_FIXTURE=true` and an absolute `EVENTBOOKING_LOAD_FIXTURE_PATH` for the private token manifest |

The fixture mode is used only with `tests/load/compose.load.yml`. That override allows 600
attendee requests per IP per minute for the 500-way burst; the normal local and home-lab limit
remains 30. The per-token limit remains enabled.
````

In design 08's Observability metrics list add one bullet after the capacity-exhausted signal:

````markdown
  - booking capacity-lock hold duration, including cumulative counts under 50 ms, used by the
    500-way release load test to assert NFR-P3 from the booking handler's measurement;
````

- [ ] **Step 4: Run the tests and documentation checks**

```bash
dotnet test tests/EventBooking.Api.Tests --filter FullyQualifiedName~RunbookContractTests
dotnet build EventBooking.sln -warnaserror
dotnet test EventBooking.sln
git add README.md docs/runbooks/demo.md deploy/home-lab/README.md \
  docs/design/07-deployment.md docs/design/08-nonfunctional-requirements.md \
  tests/EventBooking.Api.Tests/Documentation/RunbookContractTests.cs
git diff --cached --name-only
git diff --cached
node scripts/check-ontology-terms.mjs
```

Expected: the focused test turns green, the full build and suite pass with no skipped tests, and
the ontology check is clean. No Phase 7 count is known in advance. Record only the executor's
observed result. Do not copy Task 11's 1,570 into this task's result.

- [ ] **Step 5: Walk the demo runbook end to end on a fresh clone**

Use a disposable fresh clone and its own Compose project. Execute every command and verify every
visible state in `docs/runbooks/demo.md`, including the Mailpit link and post-booking refresh. This
authoring checkout has no implementation of Tasks 12–32 and no running local stack. The author
has **not** walked this runbook; this step is an unobserved execution gate, not a claim that prose
review is equivalent to a run. If any step fails, use superpowers:systematic-debugging, correct
the instruction or implementation and repeat from a clean fixture before committing.

**Pull-request gate after the task commit:** This is the last task in the master plan. The new
observability port and load profile make its pull request decision-bearing. Draft the body with
`## Narrative Context`, `## Narrative Decision` and `## Narrative Consequences`, apply
`narrative-required`, and compute AI-Fingerprint from the merge base and pushed HEAD. Lint the
drafted body with `node scripts/check-ontology-terms.mjs --also /path/to/drafted-body.md`.
The authoring handoff requires asking the user before opening this pull request. An executor
with that authorization may open it after the full gate and leave code-owner review and CI intact.

- [ ] **Step 6: Commit and push**

Stage only Task 33's declared files. Check the staged list and diff once more; unstage anything
outside them. The following generic executor commands are required by the repository's detailed
plan contract; inspect them before the commit, and include the executing harness's co-author
footer as required by the plans overview.

```bash
git add -A
git diff --cached --name-only
git diff --cached
node scripts/check-ontology-terms.mjs
git commit -m "docs: README, demo runbook and operator guide"
git push
```
