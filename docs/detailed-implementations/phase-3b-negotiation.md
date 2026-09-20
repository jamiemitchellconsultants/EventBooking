# 03b — N-way negotiation with serialised confirmation (Task 13)

[← Phase overview](phase-3-application.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This task follows Task 12. The ported propose, accept, withdraw-acceptance, withdraw-proposal,
board and capacity-adjustment handlers move to `src/EventBooking.Application/Negotiation/`,
gain the Task 6 domain's full N-type shape, and retire the transitional location constant. Every
write locks the proposal row first; confirmation serialises on it with the unique proposal
backstop.

> Use superpowers:executing-plans. This task is hand-authored: complete code and complete tests
> are written straight into this document, with no prototype. Compile and test-drive them
> yourself. The test counts below are what you should expect to reach, not figures observed by
> the author — nothing here has been run.

**Goal:** Handlers under `ManageEventNegotiation`: ProposeEvent (location, window, listed types,
proposer headcount; returns status and the event id when a single-type proposal confirms
immediately; `validation-failed` carrying every failure with offending type codes),
RecordAcceptance, WithdrawAcceptance, WithdrawProposal, GetNegotiationBoard (FR-2.13, scoped so
a Manager never sees another type's headcount), AdjustEventCapacity (caller's own type only;
`capacity-below-bookings` carrying `minimum` and current values). The caller's scoped type is
the only type they can act for; a null scope is forbidden before any domain call.

**Architecture:** One transaction per command; the proposal row is locked first
(lock-for-update), aggregates loaded, Task 6 domain methods called, audit written in the
transaction, commit, DTO returned. No domain entity leaves the handler. Confirmation creates
exactly one `Event` with one `EventCapacity` per listed type inside the same transaction that
records the final acceptance, so three Managers accepting concurrently produce one event, four
capacity rows and a `Confirmed` proposal. The `Event` insert writes `start_utc` through the
Task 11 repository path. The board is a read: no transaction, no locks, filtered to proposals
listing the caller's type and projecting only the caller's headcount.

**Tech Stack:** .NET 10, xUnit, EF Core, PostgreSQL Testcontainers.

**Spec:** [Master Task 13](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[domain model](../design/01-domain-model.md), [functional requirements](../design/02-functional-requirements.md),
[solution architecture](../design/04-solution-architecture.md), [ontology](../ontology.md).

## Boundary

Task 13 owns negotiation and capacity adjustment only. It retires the transitional-location
constant (deleted; the command carries the location identifier, and the handler
loads the `Location` for its active flag and zone) and the predecessor's fixed appointment-type
identifiers in these handlers (listed types and names come from the `AppointmentType`
repository, not from the fixed set). Event cancellation stays with Task 15 — the
transitional-zone reads in the cancellation handler are not this task's.

## Global constraints

One transaction per write; proposal row locked first; audit in the transaction; no domain
entity leaves the handler; exactly one StaffCapability (`ManageEventNegotiation`) per handler.
A proposal that is no longer open refuses with a conflict carrying its current status and
writes no audit. Revising an acceptance to the same headcount reports unchanged, commits, and
writes no audit. Adjusting another type's capacity is forbidden — the handler never takes a
type id from the caller, only from the resolved scope.

## Review focus

STOP AND CHECK four things. The audit sequence for a one-type proposal is exactly
`ProposalCreated`, `AcceptanceRecorded`, `EventConfirmed` — the immediate confirmation is one
transaction, not two. The board test that matters asserts over the serialised JSON that no FIT
or IND headcount appears anywhere for a MED Manager, and that proposals not listing MED are
excluded. The race test (three Managers, 50 runs over fresh 4-type proposals with 1 accepted)
yields exactly one event and four capacity rows every run — verified by deleting the row lock
and watching it fail. And capacity adjustment below the active-booking count returns
`capacity-below-bookings` with `minimum` and current values rather than throwing.

### Task 13: N-way negotiation with serialised confirmation

**Files:**

- Create: src/EventBooking.Application/Negotiation/ProposeEventHandler.cs
- Create: src/EventBooking.Application/Negotiation/RecordAcceptanceHandler.cs
- Create: src/EventBooking.Application/Negotiation/WithdrawAcceptanceHandler.cs
- Create: src/EventBooking.Application/Negotiation/WithdrawProposalHandler.cs
- Create: src/EventBooking.Application/Negotiation/NegotiationBoardHandler.cs
- Create: src/EventBooking.Application/Negotiation/AdjustEventCapacityHandler.cs
- Delete: src/EventBooking.Application/Events/TransitionalLocation.cs
- Delete: src/EventBooking.Application/Events/ProposeEventHandler.cs
- Delete: src/EventBooking.Application/Events/AcceptProposalHandler.cs
- Delete: src/EventBooking.Application/Events/WithdrawAcceptanceHandler.cs
- Delete: src/EventBooking.Application/Events/WithdrawProposalHandler.cs
- Delete: src/EventBooking.Application/Events/GetManagerEventBoardHandler.cs
- Delete: src/EventBooking.Application/Events/AdjustEventCapacityHandler.cs
- Test: tests/EventBooking.Application.Tests/Negotiation/ProposeEventHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Negotiation/RecordAcceptanceHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Negotiation/WithdrawHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Negotiation/NegotiationBoardHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Negotiation/AdjustEventCapacityHandlerTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/Concurrency/AcceptanceRaceTests.cs

**Interfaces:**

These complete types define the changed public boundary and its domain behavior. Apply them
after the failing test, not before.

```csharp
namespace EventBooking.Application.Negotiation;

// Proposing names everything the Task 6 domain judges: the location, the
// window, the listed types with headcounts, and the proposer's own type
// (taken from the resolved scope, never from the caller).
public sealed record ListedTypeInput(
    Guid AppointmentTypeId,
    int Headcount); // 1-1000 per type

public sealed record ProposeEventCommand(
    Guid StaffUserId,
    Guid LocationId,
    DateOnly Date,
    TimeOnly StartTime,
    int DurationMinutes,                 // 15-720, multiple of 15, same-date end
    IReadOnlyList<ListedTypeInput> ListedTypes); // 1-20, distinct

public sealed record ProposeEventOutcome(
    Guid ProposalId,
    string Status,      // "Open" or "Confirmed"
    Guid? EventId);     // set only when a single-type proposal confirms at once

// Acceptance records or revises the caller's own type. The scope type is the
// only type acted for; there is no type parameter.
public sealed record RecordAcceptanceCommand(
    Guid StaffUserId,
    Guid ProposalId,
    int Headcount);     // 1-1000

public sealed record RecordAcceptanceOutcome(
    Guid ProposalId,
    string Status,      // "Open" or "Confirmed"
    Guid? EventId,      // set only when this acceptance completes the set
    bool Changed);      // false when the headcount was already this value

public sealed record WithdrawAcceptanceCommand(
    Guid StaffUserId,
    Guid ProposalId);

public sealed record WithdrawProposalCommand(
    Guid StaffUserId,
    Guid ProposalId);

// The board shows only what the caller's type may see (FR-2.13): proposals
// listing the caller's type, with only the caller's headcount projected.
// Serialising a board must never emit another type's headcount.
public sealed record NegotiationBoardProposalView(
    Guid ProposalId,
    Guid LocationId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int ListedTypeCount,
    int AcceptedTypeCount,
    int? MyAcceptedHeadcount,
    bool AcceptedByMe,
    bool CreatedByMe);

public sealed record NegotiationBoardEventView(
    Guid EventId,
    Guid LocationId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int MyHeadcount,
    int MyRemainingCapacity);

public sealed record NegotiationBoard(
    IReadOnlyList<NegotiationBoardProposalView> OpenProposals,
    IReadOnlyList<NegotiationBoardEventView> Events);

public sealed record GetNegotiationBoardQuery(Guid StaffUserId);

// Capacity adjustment touches the caller's own type row only. Below-bookings
// is a returned outcome with the minimum and current values, not a throw.
public sealed record AdjustEventCapacityCommand(
    Guid StaffUserId,
    Guid EventId,
    int TotalHeadcount); // positive, at most 1000

public sealed record AdjustEventCapacityOutcome(
    Guid EventId,
    int TotalHeadcount,
    int RemainingCapacity,
    bool Changed);

public sealed record CapacityBelowBookings(
    int Minimum,
    int CurrentTotal,
    int CurrentRemaining);
```

- [ ] **Step 1: Write the failing application tests.** Create the five Application test files.
  Each suite follows the handler pattern (fake ports, fake unit of work, recording audit
  logger). Required cases, one test per rule:

  ```csharp
  // tests/EventBooking.Application.Tests/Negotiation/RecordAcceptanceHandlerTests.cs
  // (representative file — the other four follow the same shape)
  using EventBooking.Application.Common;
  using EventBooking.Application.Negotiation;

  namespace EventBooking.Application.Tests.Negotiation;

  public sealed class RecordAcceptanceHandlerTests
  {
      // A 1-type proposal confirms at once: the audit sequence is exactly
      // ProposalCreated, AcceptanceRecorded, EventConfirmed.
      [Fact]
      public async Task Single_type_proposal_confirms_with_three_audit_entries()
      {
          var fixture = NegotiationFixture.Create().WithTypes("MED");
          var proposal = await fixture.ProposeAsync("MED", ["MED"]);
          var result = await fixture.AcceptAsync("MED", proposal, headcount: 6);

          Assert.True(result.IsSuccess);
          Assert.Equal("Confirmed", result.Value.Status);
          Assert.NotNull(result.Value.EventId);
          Assert.Equal(
              ["ProposalCreated", "AcceptanceRecorded", "EventConfirmed"],
              fixture.Audit.ActionsFor(proposal));
      }

      // Revising to the same headcount reports unchanged and writes no audit.
      [Fact]
      public async Task Same_headcount_revision_writes_no_audit()
      {
          var fixture = NegotiationFixture.Create().WithTypes("MED", "FIT");
          var proposal = await fixture.ProposeAsync("MED", ["MED", "FIT"]);
          await fixture.AcceptAsync("FIT", proposal, headcount: 4);
          var before = fixture.Audit.Entries.Count;

          var result = await fixture.AcceptAsync("FIT", proposal, headcount: 4);

          Assert.True(result.IsSuccess);
          Assert.False(result.Value.Changed);
          Assert.Equal(before, fixture.Audit.Entries.Count);
      }

      // A proposal-not-open conflict returns the current status and no audit.
      [Fact]
      public async Task Accept_on_withdrawn_proposal_returns_status_with_no_audit()
      {
          var fixture = NegotiationFixture.Create().WithTypes("MED", "FIT");
          var proposal = await fixture.ProposeAsync("MED", ["MED", "FIT"]);
          await fixture.WithdrawProposalAsync("MED", proposal);
          var before = fixture.Audit.Entries.Count;

          var result = await fixture.AcceptAsync("FIT", proposal, headcount: 4);

          Assert.Equal("conflict", result.Error.Type);
          Assert.Equal("Withdrawn", result.Error.CurrentStatus);
          Assert.Equal(before, fixture.Audit.Entries.Count);
      }
  }
  ```

  The remaining suites cover: ProposeEvent returns `validation-failed` with every failure and
  offending type codes at once; the board for a MED Manager serialises with no FIT or IND
  headcount anywhere and excludes proposals not listing MED; adjusting another type's
  capacity is forbidden (the handler derives the type from scope — assert a FIT-scoped caller
  cannot touch the MED row even when naming its event); adjusting below the active-booking
  count returns `capacity-below-bookings` with `minimum` and current values.

- [ ] **Step 2: Write the failing race test.** Create AcceptanceRaceTests against real
  PostgreSQL: a 4-type proposal with 1 accepted; three Managers accept concurrently, 50 runs
  over fresh proposals; each run yields exactly one `Event`, exactly 4 capacity rows, and a
  `Confirmed` proposal. Verify it pins the lock by removing the proposal row lock and
  watching it fail.

- [ ] **Step 3: Run.** Expected: FAIL — the Negotiation namespace does not exist.

  ```bash
  dotnet test tests/EventBooking.Application.Tests --filter "FullyQualifiedName~Negotiation"
  ```

- [ ] **Step 4: Implement.** Create the six handler files; delete the seven Events files
  including the transitional constant. Propose loads the location (active flag, zone) and the
  listed types (active flag, Manager held) from repositories; the fixed-identifier set is no
  longer referenced. Every write locks the proposal row first. The board filters to proposals
  listing the caller's type and projects only the caller's headcount. Adjust loads only the
  caller's capacity row under lock.

- [ ] **Step 5: Run.** Expected: PASS — the new suites plus the full solution.

  ```bash
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  ```

  Expect the Application count to rise (five new suites replacing the Events suites) and
  Infrastructure to rise (the race suite). A count that does not match after the change is a
  signal to read the diff, not to adjust the number.

- [ ] **Step 6: Commit and push.**

  ```bash
  test -z "$(git status --porcelain --ignored=no | grep -v '^??')"
  node --input-type=module <<'LINT_PLANS'
  import fs from 'node:fs';
  import {execFileSync} from 'node:child_process';
  const directory = 'docs/detailed-implementations';
  const files = fs.readdirSync(directory).filter(name => name.endsWith('.md')).map(name => directory + '/' + name);
  process.stdout.write(execFileSync('node', ['scripts/check-ontology-terms.mjs', '--also', ...files], {encoding:'utf8', maxBuffer:1e7}));
  LINT_PLANS
  git add docs/detailed-implementations/phase-3b-negotiation.md docs/detailed-implementations/phase-3-application.md docs/detailed-implementations/HANDOVER.md
  git diff --cached --name-only
  git diff --cached
  test -n "$EXECUTOR_COAUTHOR"
  git commit -m "docs(plans): Task 13 negotiation and capacity handlers

  Co-authored-by: $EXECUTOR_COAUTHOR"
  git push
  ```
