# 03h — Advisory-locked invite sweep (Task 19)

[← Phase overview](phase-3-application.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This task follows Task 18. Background work becomes a hosted sweep running the Task 14 expiry
step, the started-proposal withdrawal and the recovery-booking conclusion — each item in its
own transaction, the whole run under an advisory lock. It replaces the ported hourly sweep
and its batch expiry handler, both deleted here.

> Use superpowers:executing-plans. This task is hand-authored: complete code and complete tests
> are written straight into this document, with no prototype. Compile and test-drive them
> yourself. The test counts below are what you should expect to reach, not figures observed by
> the author — nothing here has been run.

**Goal:** A sweep run that, under `pg_try_advisory_lock`, executes three steps each item in
its own transaction: ExpireInvite (Task 14's per-item handler), WithdrawStartedProposals
(FR-2.12, audited as `System`), ConcludeRecoveryBookings (FR-9.5, through Task 16's conclude
handler). Interval from `Jobs__SweepInterval`, default 15 minutes. Metrics: runs, failures,
items processed.

**Architecture:** The sweep is a .NET hosted service in the Api process; the Mcp container
runs neither job. A second concurrent run skips when the lock is held. An item that throws is
logged and counted and the rest of the run continues; the next run proceeds after a failed
run. Each step reuses its task's handler rather than restating the rule. The withdrawal
judges the window's start instant in the proposal's location zone. No domain entity leaves
the handler.

**Tech Stack:** .NET 10, xUnit, EF Core, PostgreSQL Testcontainers.

**Spec:** [Master Task 19](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[functional requirements](../design/02-functional-requirements.md),
[solution architecture](../design/04-solution-architecture.md), [ontology](../ontology.md).

## Global constraints

One transaction per item, never one for the run. Withdrawal judges the window's start instant
in the location's zone (an open proposal whose window started 1 minute ago is withdrawn with
actor `System`). A recovery booking concludes only when all its appointments are terminal.
The predecessor's hourly sweep and its batch handler stay deleted; there is nothing to port,
only the hosted service to write.

## Review focus

STOP AND CHECK three things. The two-concurrent-runs test proves the second skips via the
advisory lock against real PostgreSQL. The failing-item test proves the run continues and the
failure is counted. And the withdrawal test's clock sits exactly 1 minute past the window
start — proving the start-instant judgement, not a date comparison.

### Task 19: Background jobs

**Files:**

- Modify: src/EventBooking.Api/InviteSweepService.cs (rewritten: interval from
  configuration, advisory lock, three steps, metrics)
- Create: src/EventBooking.Infrastructure/Jobs/AdvisoryLock.cs
- Create: src/EventBooking.Application/Jobs/SweepSteps.cs (ISweepSteps plus SweepRunner:
  one RunOnceAsync pass over the three item sets, aggregating metrics)
- Delete: the ported batch expiry handler if it still exists after Task 14 (it was deleted
  there; if any reference remains, remove it here — the sweep calls the per-item handler)
- Test: tests/EventBooking.Infrastructure.Tests/Jobs/SweepServiceTests.cs

**Interfaces:**

```csharp
namespace EventBooking.Application.Jobs;

// One sweep run executes three steps. Each item commits or rolls back on its own; the run
// aggregates metrics only. Steps delegate to their task handlers: expiry to Task 14's
// per-item handler, withdrawal through TryWithdrawStarted under the proposal lock,
// conclusion to Task 16's conclude handler.
public sealed record SweepMetrics(
    int ExpiredInvites, int WithdrawnProposals, int ConcludedRecoveries, int Failures);

public interface ISweepSteps
{
    Task ExpireOneAsync(Guid inviteId, CancellationToken ct);
    Task WithdrawOneAsync(Guid proposalId, CancellationToken ct);
    Task ConcludeOneAsync(Guid bookingId, CancellationToken ct);
}
```

```csharp
namespace EventBooking.Infrastructure.Jobs;

using Microsoft.EntityFrameworkCore;

// Advisory-lock guard: at most one sweep runs at a time. The lock key is fixed for the
// sweep; a held lock skips the run without error.
public static class AdvisoryLock
{
    public const long SweepKey = 840101;
    public static async Task<bool> TryAcquireSweepLockAsync(
        EventBookingDbContext context, CancellationToken ct)
    {
        var connection = context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock(@key)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@key";
        parameter.Value = SweepKey;
        command.Parameters.Add(parameter);
        return (bool?)await command.ExecuteScalarAsync(ct) == true;
    }
}
```

The lock key is a fixed constant namespaced far from PostgreSQL's internal uses; document it
beside the constant. The lock releases when the run's connection closes (session-level
lock); the sweep holds its scope for the whole run.

- [ ] **Step 1: Write the failing tests.** Create the Infrastructure job suite against real
  PostgreSQL, extending the Task 9b fixture. The harness seeds invites past expiry, an
  open proposal whose window started 1 minute ago (location zone observed), and a recovery
  booking with all appointments terminal; it runs `SweepRunner.RunOnceAsync` (the hosted
  service's single pass, factored for tests) with a controllable clock.

  ```csharp
  // tests/EventBooking.Infrastructure.Tests/Jobs/SweepServiceTests.cs (complete)
  using EventBooking.Application.Jobs;

  namespace EventBooking.Infrastructure.Tests.Jobs;

  public sealed class SweepServiceTests : PostgresSweepHarness
  {
      [Fact]
      public async Task Second_concurrent_run_skips()
      {
          using var first = await AcquireRunAsync();
          var second = await TryRunOnceAsync();

          Assert.False(second.Started);
          Assert.Equal(new SweepMetrics(0, 0, 0, 0), second.Metrics);
      }

      [Fact]
      public async Task Failing_item_does_not_stop_run()
      {
          var good = await SeedExpiredInviteAsync();
          await SeedFailingItemAsync();
          var another = await SeedExpiredInviteAsync();

          var metrics = await (await RunOnceAsync()).Metrics;

          Assert.Equal(2, metrics.ExpiredInvites);
          Assert.Equal(1, metrics.Failures);
          Assert.True(LogContains("sweep item failed"));
      }

      [Fact]
      public async Task Started_proposal_withdrawn_as_system_one_minute_past_start()
      {
          var proposal = await SeedOpenProposalStartingAMinuteAgoAsync();

          var metrics = await (await RunOnceAsync()).Metrics;

          Assert.Equal(1, metrics.WithdrawnProposals);
          var entry = Assert.Single(AuditFor(proposal, "ProposalWithdrawn"));
          Assert.Equal("System", entry.ActorType.ToString());
      }

      [Fact]
      public async Task Terminal_recovery_booking_concludes()
      {
          var booking = await SeedTerminalRecoveryBookingAsync();

          var metrics = await (await RunOnceAsync()).Metrics;

          Assert.Equal(1, metrics.ConcludedRecoveries);
          Assert.Equal("Concluded", await BookingStatusAsync(booking));
      }

      [Fact]
      public async Task Next_run_proceeds_after_failed_run()
      {
          await SeedFailingItemAsync();
          await (await RunOnceAsync()).Metrics;

          var metrics = await (await RunOnceAsync()).Metrics;

          Assert.Equal(1, metrics.Failures);
      }
  }
  ```

  PostgresSweepHarness seeds through the real repositories and exposes the lock the same
  way production does; AuditFor filters audit rows by entity id and action name;
  BookingStatusAsync returns the status name. SeedFailingItemAsync plants an invite row
  whose handler throws (expired invite with a missing attendee — the handler's NotFound
  becomes the item failure the run counts and logs). The interval default (15 minutes) and
  the `Jobs__SweepInterval` override are asserted against the service's options binding, not
  the database.

- [ ] **Step 2: Run.** Expected: FAIL to compile — the sweep service rewrite does not exist.

  ```bash
  dotnet test tests/EventBooking.Infrastructure.Tests --filter "FullyQualifiedName~Jobs"
  ```

- [ ] **Step 3: Implement.** Rewrite InviteSweepService around PeriodicTimer with the
  interval from `IConfiguration["Jobs:SweepInterval"]` defaulting to 15 minutes; each tick
  opens a scope, tries the advisory lock (skip on failure, counting a skipped run
  distinctly from a zero-item run), lists the three item sets (expired pending invites via
  ListPendingExpiredAsync, open proposals, active recovery bookings with terminal
  appointments — the last through the bookings and appointments ports), and runs each item
  through ISweepSteps in its own scope and transaction, catching per item into the
  failure count with a log line. Emit runs, failures and items-processed metrics through
  the injected logger scopes (counts on the returned metrics record, which the tests
  assert). Wire the interval from configuration with the 15-minute default.

- [ ] **Step 4: Run.** Expected: PASS — the new suite plus the full solution.

  ```bash
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  ```

  Expect the Infrastructure count to rise. A count that does not match the executor's own
  before/after diff is a signal to read the diff, not to adjust the number.

- [ ] **Step 5: Commit and push** the executor's code — not the plan documents — under the
  master plan's message:

  ```bash
  test -z "$(git status --porcelain --ignored=no | grep -v '^??')"
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  git add src/EventBooking.Api/InviteSweepService.cs src/EventBooking.Infrastructure/Jobs/AdvisoryLock.cs src/EventBooking.Application/Jobs/SweepSteps.cs tests/EventBooking.Infrastructure.Tests/Jobs/SweepServiceTests.cs
  git diff --cached --name-only
  git diff --cached
  test -n "$EXECUTOR_COAUTHOR"
  git commit -m "feat(jobs): advisory-locked invite sweep

  Co-authored-by: $EXECUTOR_COAUTHOR"
  git push
  ```