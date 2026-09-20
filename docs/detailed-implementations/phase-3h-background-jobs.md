# 03h — Advisory-locked invite sweep (Task 19)

[← Phase overview](phase-3-application.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This task follows Task 18. Background work becomes a hosted sweep running the Task 14 expiry
step, the started-proposal withdrawal and the recovery-booking conclusion — each item in its
own transaction, the whole run under an advisory lock.

> Use superpowers:executing-plans. This task is hand-authored: complete code and complete tests
> are written straight into this document, with no prototype. Compile and test-drive them
> yourself. The test counts below are what you should expect to reach, not figures observed by
> the author — nothing here has been run.

**Goal:** A sweep run that, under `pg_try_advisory_lock`, executes three steps each item in
its own transaction: ExpireInvites (Task 14), WithdrawStartedProposals (FR-2.12, audited as
`System`), ConcludeRecoveryBookings (FR-9.5). Interval from `Jobs__SweepInterval`, default 15
minutes. Metrics: runs, failures, items processed.

**Architecture:** The sweep is a .NET hosted service in the Api process; the Mcp container
runs neither job. A second concurrent run skips when the lock is held. An item that throws is
logged and counted and the rest of the run continues; the next run proceeds after a failed
run. Each step reuses its task's handler (expiry through the Task 14 step, conclusion through
the Task 16 outcome) rather than restating the rule. No domain entity leaves the handler.

**Tech Stack:** .NET 10, xUnit, EF Core, PostgreSQL Testcontainers.

**Spec:** [Master Task 19](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[functional requirements](../design/02-functional-requirements.md),
[solution architecture](../design/04-solution-architecture.md), [ontology](../ontology.md).

## Global constraints

One transaction per item, never one for the run. Withdrawal judges the window's start instant
in the location's zone (an open proposal whose window started 1 minute ago is withdrawn with
actor `System`). A recovery booking concludes only when all its appointments are terminal.
The predecessor's serverless sweep entry point stays deleted; there is nothing to port, only
the hosted service to write.

## Review focus

STOP AND CHECK three things. The two-concurrent-runs test proves the second skips via the
advisory lock against real PostgreSQL. The failing-item test proves the run continues and the
failure is counted. And the withdrawal test's clock sits exactly 1 minute past the window
start — proving the start-instant judgement, not a date comparison.

### Task 19: Background jobs

**Files:**

- Modify: src/EventBooking.Api/InviteSweepService.cs (15-minute hosted service)
- Create: src/EventBooking.Infrastructure/Jobs/AdvisoryLock.cs
- Create: src/EventBooking.Application/Jobs/SweepSteps.cs
- Test: tests/EventBooking.Infrastructure.Tests/Jobs/SweepServiceTests.cs

**Interfaces:**

These complete types define the changed public boundary and its domain behavior. Apply them
after the failing test, not before.

```csharp
namespace EventBooking.Application.Jobs;

// One sweep run executes three steps. Each item commits or rolls back on
// its own; the run aggregates metrics only.
public sealed record SweepMetrics(
    int ExpiredInvites,
    int WithdrawnProposals,
    int ConcludedRecoveries,
    int Failures);

public interface ISweepSteps
{
    // One expired invite through the Task 14 step, in its own transaction.
    Task ExpireOneAsync(Guid inviteId, CancellationToken ct);
    // One started open proposal withdrawn as System, in its own transaction.
    Task WithdrawOneAsync(Guid proposalId, CancellationToken ct);
    // One recovery booking concluded when terminal, in its own transaction.
    Task ConcludeOneAsync(Guid bookingId, CancellationToken ct);
}
```

```csharp
namespace EventBooking.Infrastructure.Jobs;

// Advisory-lock guard: at most one sweep runs at a time. The lock key is
// fixed for the sweep; a held lock skips the run without error.
public static class AdvisoryLock
{
    // Tries the fixed sweep key; returns false when another run holds it.
    public static Task<bool> TryAcquireSweepLockAsync(
        DbContext context, CancellationToken ct);
}
```

- [ ] **Step 1: Write the failing tests.** Create the Infrastructure job suite against real
  PostgreSQL. Required cases, one test per rule:

  ```csharp
  // tests/EventBooking.Infrastructure.Tests/Jobs/SweepServiceTests.cs
  // (the whole suite — one file follows the hosted service end to end)
  using EventBooking.Application.Jobs;

  namespace EventBooking.Infrastructure.Tests.Jobs;

  public sealed class SweepServiceTests
  {
      // Two concurrent sweep runs: the second skips via the advisory lock.
      [Fact]
      public async Task Second_concurrent_run_skips()
      {
          var fixture = SweepFixture.Create();
          using var first = await fixture.StartRunAsync();
          var second = await fixture.TryStartRunAsync();

          Assert.False(second.Started);
          Assert.Equal(0, second.Metrics.ExpiredInvites);
      }

      // An item that throws is logged and counted; the rest continues.
      [Fact]
      public async Task Failing_item_does_not_stop_run()
      {
          var fixture = SweepFixture.Create()
              .WithExpiredInvites(3)
              .WithFailingItem(1);

          var metrics = await fixture.RunOnceAsync();

          Assert.Equal(2, metrics.ExpiredInvites);
          Assert.Equal(1, metrics.Failures);
          Assert.True(fixture.Logs.Contain("sweep item failed"));
      }

      // An open proposal whose window started 1 minute ago is withdrawn
      // with actor System.
      [Fact]
      public async Task Started_proposal_withdrawn_as_system()
      {
          var fixture = SweepFixture.Create()
              .WithOpenProposal(startedAMinuteAgo: true);

          var metrics = await fixture.RunOnceAsync();

          Assert.Equal(1, metrics.WithdrawnProposals);
          Assert.Equal("System", fixture.WithdrawalAudit.ActorType);
      }
  }
  ```

  The suite also covers: the next run proceeds after a failed run; a recovery booking with
  all appointments terminal is concluded; the interval defaults to 15 minutes and follows
  `Jobs__SweepInterval` when set.

- [ ] **Step 2: Run.** Expected: FAIL — the sweep service does not exist.

  ```bash
  dotnet test tests/EventBooking.Infrastructure.Tests --filter "FullyQualifiedName~Jobs"
  ```

- [ ] **Step 3: Implement.** Create the hosted service, the advisory-lock guard and the sweep
  steps. Wire the interval from configuration with the 15-minute default. Emit runs,
  failures and items-processed metrics.

- [ ] **Step 4: Run.** Expected: PASS — the new suite plus the full solution.

  ```bash
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  ```

  Expect the Infrastructure count to rise. A count that does not match after the change is a
  signal to read the diff, not to adjust the number.

- [ ] **Step 5: Commit and push.**

  ```bash
  test -z "$(git status --porcelain --ignored=no | grep -v '^??')"
  node --input-type=module <<'LINT_PLANS'
  import fs from 'node:fs';
  import {execFileSync} from 'node:child_process';
  const directory = 'docs/detailed-implementations';
  const files = fs.readdirSync(directory).filter(name => name.endsWith('.md')).map(name => directory + '/' + name);
  process.stdout.write(execFileSync('node', ['scripts/check-ontology-terms.mjs', '--also', ...files], {encoding:'utf8', maxBuffer:1e7}));
  LINT_PLANS
  git add docs/detailed-implementations/phase-3h-background-jobs.md docs/detailed-implementations/phase-3-application.md docs/detailed-implementations/HANDOVER.md
  git diff --cached --name-only
  git diff --cached
  test -n "$EXECUTOR_COAUTHOR"
  git commit -m "docs(plans): Task 19 advisory-locked invite sweep

  Co-authored-by: $EXECUTOR_COAUTHOR"
  git push
  ```
