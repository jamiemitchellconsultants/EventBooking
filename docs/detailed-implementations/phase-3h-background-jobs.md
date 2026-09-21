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
- Modify: src/EventBooking.Application/Abstractions/IBookingRepository.cs
  (ListConcludingCandidatesAsync: active non-original bookings)
- Modify: src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs (same method)
- Modify: tests/EventBooking.Application.Tests/Fakes/InMemoryRepositories.cs (same method)
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

- [ ] **Step 3: Implement.** Add the production code below in full. No placeholders:
  every file below is complete.

  ```csharp
  // src/EventBooking.Application/Jobs/SweepSteps.cs (complete: steps plus the runner)
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Common;
  using EventBooking.Application.Invites;
  using EventBooking.Application.Recovery;
  using EventBooking.Domain.Audit;
  using EventBooking.Domain.Bookings;
  using EventBooking.Domain.Common;
  using EventBooking.Domain.Events;
  using EventBooking.Domain.Time;
  using Microsoft.Extensions.Logging;

  namespace EventBooking.Application.Jobs;

  public sealed class SweepSteps(
      IInviteRepository invites,
      IAttendeeRepository attendees,
      IEventProposalRepository proposals,
      ILocationRepository locations,
      IBookingRepository bookings,
      IBookingAppointmentRepository appointments,
      IEventEligibilityQuery eligibility,
      ISystemSettingsRepository settings,
      IEmailDeliveryRepository emails,
      IUnitOfWork unitOfWork,
      IAuditLogger audit,
      IClock clock,
      IEventWindowZones zones,
      IInviteIssuer issuer,
      ILogger<SweepSteps> logger) : ISweepSteps
  {
      public async Task ExpireOneAsync(Guid inviteId, CancellationToken ct)
      {
          var handler = new ExpireInviteHandler(invites, attendees, eligibility, settings,
              emails, unitOfWork, audit, clock, issuer);
          var result = await handler.HandleAsync(new ExpireInviteCommand(inviteId), ct);
          if (result.IsFailure)
              throw new DomainException(result.Error.Message);
      }

      public async Task WithdrawOneAsync(Guid proposalId, CancellationToken ct)
      {
          await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
          var proposal = await proposals.LockForUpdateAsync(proposalId, ct);
          if (proposal is null) return;
          var location = await locations.GetAsync(proposal.LocationId, ct);
          if (location is null) return;
          if (proposal.TryWithdrawStarted(zones, location.TimeZoneId, clock.UtcNow))
          {
              audit.Record(AuditEntityTypes.EventProposal, proposal.Id,
                  AuditAction.ProposalWithdrawn, ActorType.System, null, "window started");
              await unitOfWork.SaveChangesAsync(ct);
          }

          await transaction.CommitAsync(ct);
      }

      public async Task ConcludeOneAsync(Guid bookingId, CancellationToken ct)
      {
          var handler = new ConcludeRecoveryHandler(bookings, appointments, unitOfWork, audit);
          var result = await handler.HandleAsync(bookingId, ct);
          if (result.IsFailure)
              throw new DomainException(result.Error.Message);
      }
  }

  public sealed class SweepRunner(ISweepSteps steps, ILogger<SweepRunner> logger)
  {
      public async Task<(bool Started, SweepMetrics Metrics)> RunOnceAsync(
          Func<CancellationToken, Task<bool>> tryAcquire,
          Func<CancellationToken, Task<IReadOnlyList<Guid>>> expiredInvites,
          Func<CancellationToken, Task<IReadOnlyList<Guid>>> openProposals,
          Func<CancellationToken, Task<IReadOnlyList<Guid>>> concludingBookings,
          CancellationToken ct)
      {
          if (!await tryAcquire(ct))
              return (false, new SweepMetrics(0, 0, 0, 0));

          var metrics = new SweepMetrics(0, 0, 0, 0);
          metrics = await RunItemsAsync(expiredInvites,
              (id, token) => steps.ExpireOneAsync(id, token), ct, metrics,
              (m, n) => m with { ExpiredInvites = n });
          metrics = await RunItemsAsync(openProposals,
              (id, token) => steps.WithdrawOneAsync(id, token), ct, metrics,
              (m, n) => m with { WithdrawnProposals = n });
          metrics = await RunItemsAsync(concludingBookings,
              (id, token) => steps.ConcludeOneAsync(id, token), ct, metrics,
              (m, n) => m with { ConcludedRecoveries = n });
          return (true, metrics);
      }

      private async Task<SweepMetrics> RunItemsAsync(
          Func<CancellationToken, Task<IReadOnlyList<Guid>>> list,
          Func<Guid, CancellationToken, Task> run,
          CancellationToken ct, SweepMetrics metrics,
          Func<SweepMetrics, int, SweepMetrics> set)
      {
          var done = 0;
          foreach (var id in await list(ct))
          {
              try
              {
                  await run(id, ct);
                  done++;
              }
              catch (Exception ex)
              {
                  logger.LogError(ex, "Sweep item failed.");
                  metrics = metrics with { Failures = metrics.Failures + 1 };
              }
          }

          return set(metrics, done);
      }
  }
  ```

  Item failures surface as exceptions from the steps (a handled refusal stays a result
  inside the handler; a throw is the unexpected case the run counts and logs).
  SweepMetrics is a record: the `with` expressions accumulate per step. The unused
  logger on SweepSteps is intentional — per-item failures log in the runner, step-level
  context logs here as the steps grow; if the unused-parameter warning bites, drop the
  parameter (it is not load-bearing).

  ```csharp
  // src/EventBooking.Api/InviteSweepService.cs (complete rewrite)
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Jobs;
  using EventBooking.Domain.Bookings;
  using EventBooking.Infrastructure.Jobs;
  using EventBooking.Infrastructure.Persistence;
  using Microsoft.Extensions.Configuration;
  using Microsoft.Extensions.DependencyInjection;
  using Microsoft.Extensions.Hosting;
  using Microsoft.Extensions.Logging;

  namespace EventBooking.Api;

  public sealed class InviteSweepService(
      IServiceScopeFactory scopes,
      IConfiguration configuration,
      ILogger<InviteSweepService> logger) : BackgroundService
  {
      private static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(15);

      protected override async Task ExecuteAsync(CancellationToken stoppingToken)
      {
          var interval = configuration.GetValue<TimeSpan?>("Jobs:SweepInterval") ?? DefaultInterval;
          using var timer = new PeriodicTimer(interval);
          do
          {
              try
              {
                  using var scope = scopes.CreateScope();
                  var runner = scope.ServiceProvider.GetRequiredService<SweepRunner>();
                  var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
                  var invites = scope.ServiceProvider.GetRequiredService<IInviteRepository>();
                  var proposals = scope.ServiceProvider.GetRequiredService<IEventProposalRepository>();
                  var bookings = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
                  var appointments =
                      scope.ServiceProvider.GetRequiredService<IBookingAppointmentRepository>();
                  var clock = scope.ServiceProvider.GetRequiredService<IClock>();

                  var (started, metrics) = await runner.RunOnceAsync(
                      token => AdvisoryLock.TryAcquireSweepLockAsync(context, token),
                      async token => (await invites.ListPendingExpiredAsync(clock.UtcNow, token))
                          .Select(i => i.Id).ToList(),
                      async token => (await proposals.ListOpenAsync(token))
                          .Select(p => p.Id).ToList(),
                      token => ConcludingAsync(bookings, appointments, token),
                      stoppingToken);

                  if (started)
                      logger.LogInformation(
                          "Sweep: {Expired} expired, {Withdrawn} withdrawn, {Concluded} concluded, {Failures} failed.",
                          metrics.ExpiredInvites, metrics.WithdrawnProposals,
                          metrics.ConcludedRecoveries, metrics.Failures);
              }
              catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
              {
                  break;
              }
              catch (Exception ex)
              {
                  logger.LogError(ex, "The invite sweep failed.");
              }
          }
          while (await timer.WaitForNextTickAsync(stoppingToken));
      }

      // Recovery bookings that are still active with every appointment terminal. Read
      // without locks here; each item re-validates under its own lock in its transaction.
      private static async Task<IReadOnlyList<Guid>> ConcludingAsync(
          IBookingRepository bookings,
          IBookingAppointmentRepository appointments,
          CancellationToken ct)
      {
          var ids = new List<Guid>();
          foreach (var booking in await bookings.ListConcludingCandidatesAsync(ct))
          {
              var rows = await appointments.ListForBookingAsync(booking.Id, ct);
              if (rows.Count > 0 && rows.All(a =>
                      a.Status is BookingAppointmentStatus.Completed or BookingAppointmentStatus.NoShow))
                  ids.Add(booking.Id);
          }

          return ids;
      }
  }
  ```

  `BookingAppointmentStatus` needs its domain namespace import (on the file above).
  ListConcludingCandidatesAsync is a new booking port method; all three files are
  complete below.

  ```csharp
  // src/EventBooking.Application/Abstractions/IBookingRepository.cs: add
  /// <summary>
  /// Active recovery bookings — every non-original row still open. The sweep then asks the
  /// appointment port which of them have nothing left outstanding; the terminal-appointment
  /// test is not expressible here without joining a second aggregate.
  /// </summary>
  /// <param name="cancellationToken">The cancellation token.</param>
  Task<IReadOnlyList<Booking>> ListConcludingCandidatesAsync(CancellationToken cancellationToken);
  ```

  ```csharp
  // src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs — on
  // BookingRepository. Unlocked by design: the sweep re-validates each item under its own
  // lock in its own transaction, so a row that concludes between the read and the write is
  // refused there rather than double-concluded.
  /// <inheritdoc/>
  public async Task<IReadOnlyList<Booking>> ListConcludingCandidatesAsync(
      CancellationToken cancellationToken) =>
      await context.Bookings
          .AsNoTracking()
          .Where(b => b.Status == BookingStatus.Active && b.RecoveryOfBookingId != null)
          .OrderBy(b => b.Id)
          .ToListAsync(cancellationToken);
  ```

  ```csharp
  // tests/EventBooking.Application.Tests/Fakes/InMemoryRepositories.cs — on
  // InMemoryBookingRepository, ordered the same way so a sweep test sees a stable sequence.
  public Task<IReadOnlyList<Booking>> ListConcludingCandidatesAsync(
      CancellationToken cancellationToken) =>
      Task.FromResult<IReadOnlyList<Booking>>(
          [.. Items
              .Where(b => b.Status == BookingStatus.Active && b.RecoveryOfBookingId != null)
              .OrderBy(b => b.Id)]);
  ```

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
  git add src/EventBooking.Api/InviteSweepService.cs src/EventBooking.Infrastructure/Jobs/AdvisoryLock.cs src/EventBooking.Application/Jobs/SweepSteps.cs src/EventBooking.Application/Abstractions/IBookingRepository.cs src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs tests/EventBooking.Application.Tests/Fakes/InMemoryRepositories.cs tests/EventBooking.Infrastructure.Tests/Jobs/SweepServiceTests.cs
  git diff --cached --name-only
  git diff --cached
  test -n "$EXECUTOR_COAUTHOR"
  git commit -m "feat(jobs): advisory-locked invite sweep

  Co-authored-by: $EXECUTOR_COAUTHOR"
  git push
  ```