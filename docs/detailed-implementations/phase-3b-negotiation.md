# 03b — N-way negotiation with serialised confirmation (Task 13)

[← Phase overview](phase-3-application.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This task follows Task 12. The ported propose, accept, withdraw-acceptance, withdraw-proposal,
board and capacity-adjustment handlers move to `src/EventBooking.Application/Negotiation/`,
gain the Task 6 domain's full N-type shape, and retire the transitional location constant and
the fixed appointment-type identifiers in these handlers. Every write locks the proposal row
first; confirmation serialises on it with the unique proposal backstop.

> Use superpowers:executing-plans. This task is hand-authored: complete code and complete tests
> are written straight into this document, with no prototype. Compile and test-drive them
> yourself. The test counts below are what you should expect to reach, not figures observed by
> the author — nothing here has been run.

**Goal:** Handlers under `ManageEventNegotiation`: ProposeEvent (location, window, listed
types, proposer headcount; returns status and the event id when a single-type proposal
confirms immediately; code `validation` carrying every failure with offending type codes,
the master plan's `validation-failed` label for the same refusal), RecordAcceptance, WithdrawAcceptance, WithdrawProposal, GetNegotiationBoard (FR-2.13, scoped
so a Manager never sees another type's headcount), AdjustEventCapacity (caller's own type
only; `capacity-below-bookings` carrying minimum and current values). The caller's scoped
type is the only type they can act for; a null scope is forbidden before any domain call.

**Architecture:** One transaction per command; the proposal row is locked first through the
Task 10 helper, aggregates loaded, Task 6 domain methods called, audit written in the
transaction, commit, DTO returned. No domain entity leaves the handler. Confirmation creates
exactly one `Event` with one `EventCapacity` per listed type inside the same transaction that
records the final acceptance, so three Managers accepting concurrently produce one event and
one capacity row per listed type. The `Event` insert writes the start instant through the
Task 11 repository path. The board is a read: no transaction, no locks, filtered to proposals
listing the caller's type and projecting only the caller's headcount. "Future" is judged in
the location's zone via the zone port's local date, not the transitional clock.

**Tech Stack:** .NET 10, xUnit, EF Core, PostgreSQL Testcontainers.

**Spec:** [Master Task 13](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[domain model](../design/01-domain-model.md), [functional requirements](../design/02-functional-requirements.md),
[solution architecture](../design/04-solution-architecture.md), [ontology](../ontology.md).

## Boundary

Task 13 owns negotiation and capacity adjustment only. It deletes the transitional-location
constant (the command carries the location identifier, and the handler loads the location for
its active flag and zone) and stops reading the predecessor's fixed appointment-type set
(listed types, codes and manager-held flags come from the type and profile repositories).
`CancelEventHandler.cs` stays where it is: event cancellation is Task 15, and its
transitional-zone reads retire there.

## Global constraints

One transaction per write; proposal row locked first; audit in the transaction; no domain
entity leaves the handler; exactly one StaffCapability (`ManageEventNegotiation`) per
handler. A proposal that is no longer open refuses with a conflict carrying its current
status and writes no audit. Revising an acceptance to the same headcount reports unchanged,
commits, and writes no audit. Adjusting another type's capacity is impossible by
construction — the handler derives the type from the resolved scope and takes no type id
from the caller.

## Review focus

STOP AND CHECK four things. The audit sequence for a one-type proposal is exactly
`ProposalCreated`, `AcceptanceRecorded`, `EventConfirmed` — the immediate confirmation is one
transaction, not two. The board test that matters asserts over the serialised JSON that no
other type's code or headcount appears anywhere for a MED Manager, and that proposals not
listing MED are excluded. The race test (three Managers, 50 runs over fresh 4-type proposals
with 1 accepted) yields exactly one event and four capacity rows every run — verified by
deleting the row lock and watching it fail. And capacity adjustment below the active-booking
count returns `capacity-below-bookings` with minimum and current values rather than throwing.

### Task 13: N-way negotiation with serialised confirmation

**Files:**

- Modify: src/EventBooking.Application/Common/Error.cs (capacity-below-bookings factory)
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

```csharp
namespace EventBooking.Application.Negotiation;

public sealed record ListedTypeInput(Guid AppointmentTypeId, int Headcount);
public sealed record ProposeEventCommand(
    Guid StaffUserId,
    Guid LocationId,
    DateOnly Date,
    TimeOnly StartTime,
    int DurationMinutes,
    IReadOnlyList<Guid> ListedAppointmentTypeIds,
    int ProposerHeadcount = 1);
public sealed record ProposeEventOutcome(Guid ProposalId, string Status, Guid? EventId);

public sealed record RecordAcceptanceCommand(Guid StaffUserId, Guid ProposalId, int Headcount);
public sealed record RecordAcceptanceOutcome(Guid ProposalId, string Status, Guid? EventId, bool Changed);
public sealed record WithdrawAcceptanceCommand(Guid StaffUserId, Guid ProposalId);
public sealed record WithdrawProposalCommand(Guid StaffUserId, Guid ProposalId);

public sealed record NegotiationBoardProposalView(
    Guid ProposalId, Guid LocationId, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime,
    int ListedTypeCount, int AcceptedTypeCount, int? MyAcceptedHeadcount, bool AcceptedByMe, bool CreatedByMe);
public sealed record NegotiationBoardEventView(
    Guid EventId, Guid LocationId, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime,
    int MyHeadcount, int MyRemainingCapacity);
public sealed record NegotiationBoard(
    IReadOnlyList<NegotiationBoardProposalView> OpenProposals,
    IReadOnlyList<NegotiationBoardEventView> Events);
public sealed record GetNegotiationBoardQuery(Guid StaffUserId);

public sealed record AdjustEventCapacityCommand(Guid StaffUserId, Guid EventId, int TotalHeadcount);
public sealed record AdjustEventCapacityOutcome(Guid EventId, int TotalHeadcount, int RemainingCapacity, bool Changed);
```

```csharp
// Error.cs: add beside the Task 12 factories.
public const string CapacityBelowBookingsCode = "capacity-below-bookings";
public static Error CapacityBelowBookings(string message, int minimum, int currentTotal, int currentRemaining) =>
    new(CapacityBelowBookingsCode, message, new Dictionary<string, long>
    {
        ["minimum"] = minimum,
        ["currentTotal"] = currentTotal,
        ["currentRemaining"] = currentRemaining,
    });
```

- [ ] **Step 1: Write the failing tests.** Create the five Application test files below in
  full. Suites share NegotiationFixture (new file
  `tests/EventBooking.Application.Tests/Negotiation/NegotiationFixture.cs`): in-memory
  proposals, events, capacities, locations (Task 12 fake), types, profiles, unit of work,
  recording audit, fake clock, and the Task 12 TestZones double. Managers hold
  `StaffAccessProfile.Create(userId, Role.Manager, typeId)`; the fixture creates MED, FIT,
  IND, ESC rows through `AppointmentType.Create` and one active location.

  ```csharp
  // tests/EventBooking.Application.Tests/Negotiation/RecordAcceptanceHandlerTests.cs
  // (representative file — propose, withdraw, board and adjust suites follow the same
  // fixture and assertion style; their cases are listed after this file)
  using EventBooking.Application.Common;
  using EventBooking.Application.Negotiation;

  namespace EventBooking.Application.Tests.Negotiation;

  public sealed class RecordAcceptanceHandlerTests
  {
      [Fact]
      public async Task Single_type_proposal_confirms_with_three_audit_entries()
      {
          var fixture = NegotiationFixture.Create().WithTypes("MED");
          var proposed = await fixture.ProposeAsync("MED", ["MED"], headcount: 6);
          Assert.Equal("Confirmed", proposed.Status);
          Assert.NotNull(proposed.EventId);

          Assert.Equal(
              ["ProposalCreated", "AcceptanceRecorded", "EventConfirmed"],
              fixture.AuditActionsFor(proposed.ProposalId));
          Assert.Single(fixture.Events.Items);
          Assert.Single(fixture.Events.Items.Single().Capacities);
      }

      [Fact]
      public async Task Three_type_proposal_confirms_only_on_the_third_accept()
      {
          var fixture = NegotiationFixture.Create().WithTypes("MED", "FIT", "IND");
          var proposed = await fixture.ProposeAsync("MED", ["MED", "FIT", "IND"], headcount: 6);

          var second = await fixture.AcceptAsync("FIT", proposed.ProposalId, headcount: 4);
          Assert.Equal("Open", second.Status);
          Assert.Null(second.EventId);

          var third = await fixture.AcceptAsync("IND", proposed.ProposalId, headcount: 5);
          Assert.Equal("Confirmed", third.Status);
          Assert.NotNull(third.EventId);
          Assert.Equal(3, fixture.Events.Items.Single().Capacities.Count);
          Assert.Equal("Confirmed", fixture.Proposals.Items.Single().Status.ToString());
      }

      [Fact]
      public async Task Same_headcount_revision_reports_unchanged_and_writes_no_audit()
      {
          var fixture = NegotiationFixture.Create().WithTypes("MED", "FIT");
          var proposed = await fixture.ProposeAsync("MED", ["MED", "FIT"], headcount: 6);
          await fixture.AcceptAsync("FIT", proposed.ProposalId, headcount: 4);
          var before = fixture.Audit.Entries.Count;

          var result = await fixture.AcceptAsync("FIT", proposed.ProposalId, headcount: 4);

          Assert.True(result.IsSuccess);
          Assert.False(result.Value.Changed);
          Assert.Equal(before, fixture.Audit.Entries.Count);
      }

      [Fact]
      public async Task Accept_on_withdrawn_proposal_returns_status_with_no_audit()
      {
          var fixture = NegotiationFixture.Create().WithTypes("MED", "FIT");
          var proposed = await fixture.ProposeAsync("MED", ["MED", "FIT"], headcount: 6);
          await fixture.WithdrawProposalAsync("MED", proposed.ProposalId);
          var before = fixture.Audit.Entries.Count;

          var result = await fixture.AcceptAsync("FIT", proposed.ProposalId, headcount: 4);

          Assert.True(result.IsFailure);
          Assert.Equal("conflict", result.Error.Code);
          Assert.Contains("Withdrawn", result.Error.Message);
          Assert.Equal(before, fixture.Audit.Entries.Count);
      }

      [Fact]
      public async Task Null_scoped_manager_is_forbidden_before_any_domain_call()
      {
          var fixture = NegotiationFixture.Create().WithTypes("MED", "FIT").WithNullScopedManager();
          var proposed = await fixture.ProposeAsync("MED", ["MED", "FIT"], headcount: 6);

          var result = await fixture.AcceptAsync(null, proposed.ProposalId, headcount: 4);

          Assert.True(result.IsFailure);
          Assert.Equal("forbidden", result.Error.Code);
      }
  }
  ```

  The five suites above are the complete Step 1: no suite survives as prose. The board
  suite's serialised-JSON assertion is the load-bearing one — a Manager's board must never
  carry another type's code or headcount, and the construction (only the caller's headcount
  projected, proposals filtered to the caller's type) is what guarantees it.

  ```csharp
  // tests/EventBooking.Application.Tests/Negotiation/ProposeEventHandlerTests.cs (complete)
  using EventBooking.Application.Common;
  using EventBooking.Application.Negotiation;
  using EventBooking.Application.Tests.ReferenceData;

  namespace EventBooking.Application.Tests.Negotiation;

  public sealed class ProposeEventHandlerTests
  {
      [Fact]
      public async Task Propose_with_every_failure_at_once_reports_all_of_them()
      {
          var fixture = NegotiationFixture.Create().WithTypes("MED");
          var proposer = fixture.Managers["MED"];
          var handler = new ProposeEventHandler(
              fixture.Proposals, fixture.Locations, fixture.Types, fixture.Profiles,
              fixture.Profiles, fixture.UnitOfWork, fixture.Audit, fixture.Clock,
              TestZones.Instance, fixture.Events);
          var unknownType = Guid.NewGuid();

          var result = await handler.HandleAsync(new ProposeEventCommand(
              proposer, fixture.LocationId, new DateOnly(2026, 9, 1), new TimeOnly(9, 0), 90,
              [fixture.TypeIds["MED"], unknownType], 0), CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("validation", result.Error.Code);
          Assert.Contains("MED", result.Error.Message);
          Assert.Contains(unknownType.ToString(), result.Error.Message);
          Assert.Empty(fixture.Proposals.Items);
          Assert.Empty(fixture.Audit.Entries);
      }

      [Fact]
      public async Task Propose_at_inactive_location_is_refused()
      {
          var fixture = NegotiationFixture.Create().WithTypes("MED");
          fixture.Locations.Items.Single().Deactivate(Domain.Locations.LocationUsage.None);
          var proposer = fixture.Managers["MED"];
          var handler = new ProposeEventHandler(
              fixture.Proposals, fixture.Locations, fixture.Types, fixture.Profiles,
              fixture.Profiles, fixture.UnitOfWork, fixture.Audit, fixture.Clock,
              TestZones.Instance, fixture.Events);

          var result = await handler.HandleAsync(new ProposeEventCommand(
              proposer, fixture.LocationId, new DateOnly(2026, 10, 14), new TimeOnly(9, 30), 90,
              [fixture.TypeIds["MED"]], 6), CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("validation", result.Error.Code);
      }
  }
  ```

  ```csharp
  // tests/EventBooking.Application.Tests/Negotiation/WithdrawHandlerTests.cs (complete)
  using EventBooking.Application.Common;
  using EventBooking.Application.Negotiation;

  namespace EventBooking.Application.Tests.Negotiation;

  public sealed class WithdrawHandlerTests
  {
      [Fact]
      public async Task Withdraw_proposer_acceptance_is_refused()
      {
          var fixture = NegotiationFixture.Create().WithTypes("MED", "FIT");
          var proposed = await fixture.ProposeAsync("MED", ["MED", "FIT"], headcount: 6);
          var handler = new WithdrawAcceptanceHandler(
              fixture.Proposals, fixture.Profiles, fixture.UnitOfWork, fixture.Audit);

          var result = await handler.HandleAsync(
              new WithdrawAcceptanceCommand(fixture.Managers["MED"], proposed.ProposalId),
              CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("validation", result.Error.Code);
          Assert.Single(fixture.Proposals.Items.Single().Acceptances);
      }

      [Fact]
      public async Task Withdraw_then_reaccept_works()
      {
          var fixture = NegotiationFixture.Create().WithTypes("MED", "FIT");
          var proposed = await fixture.ProposeAsync("MED", ["MED", "FIT"], headcount: 6);
          await fixture.AcceptAsync("FIT", proposed.ProposalId, headcount: 4);
          var handler = new WithdrawAcceptanceHandler(
              fixture.Proposals, fixture.Profiles, fixture.UnitOfWork, fixture.Audit);

          var withdrawn = await handler.HandleAsync(
              new WithdrawAcceptanceCommand(fixture.Managers["FIT"], proposed.ProposalId),
              CancellationToken.None);
          Assert.True(withdrawn.IsSuccess);

          var reaccepted = await fixture.AcceptAsync("FIT", proposed.ProposalId, headcount: 3);
          Assert.True(reaccepted.IsSuccess);
          Assert.True(reaccepted.Value.Changed);
          Assert.Equal("Open", reaccepted.Value.Status);
      }

      [Fact]
      public async Task Successor_manager_can_withdraw_the_proposal()
      {
          var fixture = NegotiationFixture.Create().WithTypes("MED", "FIT");
          var proposed = await fixture.ProposeAsync("MED", ["MED", "FIT"], headcount: 6);
          var successor = Guid.NewGuid();
          fixture.Profiles.Add(Domain.Access.StaffAccessProfile.Create(
              successor, Domain.Access.Role.Manager, fixture.TypeIds["MED"]));
          var handler = new WithdrawProposalHandler(
              fixture.Proposals, fixture.Profiles, fixture.UnitOfWork, fixture.Audit);

          var result = await handler.HandleAsync(
              new WithdrawProposalCommand(successor, proposed.ProposalId), CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal("Withdrawn", fixture.Proposals.Items.Single().Status.ToString());
      }
  }
  ```

  ```csharp
  // tests/EventBooking.Application.Tests/Negotiation/NegotiationBoardHandlerTests.cs (complete)
  using System.Text.Json;
  using EventBooking.Application.Negotiation;
  using EventBooking.Application.Tests.ReferenceData;

  namespace EventBooking.Application.Tests.Negotiation;

  public sealed class NegotiationBoardHandlerTests
  {
      [Fact]
      public async Task Board_hides_every_other_type()
      {
          var fixture = NegotiationFixture.Create().WithTypes("MED", "FIT", "IND", "ESC");
          await fixture.ProposeAsync("MED", ["MED", "FIT", "IND"], headcount: 6);
          await fixture.ProposeAsync("FIT", ["FIT", "ESC"], headcount: 2);
          var handler = new NegotiationBoardHandler(
              fixture.Proposals, fixture.Events, fixture.Locations, fixture.Profiles,
              fixture.Clock, TestZones.Instance);

          var result = await handler.HandleAsync(
              new GetNegotiationBoardQuery(fixture.Managers["MED"]), CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Single(result.Value.OpenProposals);
          var json = JsonSerializer.Serialize(result.Value);
          Assert.DoesNotContain("FIT", json);
          Assert.DoesNotContain("IND", json);
          Assert.DoesNotContain("ESC", json);
          Assert.Contains("6", json);
      }
  }
  ```

  ```csharp
  // tests/EventBooking.Application.Tests/Negotiation/AdjustEventCapacityHandlerTests.cs (complete)
  using EventBooking.Application.Common;
  using EventBooking.Application.Negotiation;

  namespace EventBooking.Application.Tests.Negotiation;

  public sealed class AdjustEventCapacityHandlerTests
  {
      [Fact]
      public async Task Adjust_other_type_capacity_is_forbidden()
      {
          var fixture = NegotiationFixture.Create().WithTypes("MED", "FIT");
          var proposed = await fixture.ProposeAsync("MED", ["MED"], headcount: 6);
          var handler = new AdjustEventCapacityHandler(
              fixture.Events, fixture.Capacities, fixture.Profiles, fixture.UnitOfWork,
              fixture.Audit, fixture.Types);

          var result = await handler.HandleAsync(new AdjustEventCapacityCommand(
              fixture.Managers["FIT"], proposed.EventId!.Value, 4), CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("forbidden", result.Error.Code);
      }

      [Fact]
      public async Task Adjust_below_active_bookings_returns_minimum_and_current_values()
      {
          var fixture = NegotiationFixture.Create().WithTypes("MED");
          var proposed = await fixture.ProposeAsync("MED", ["MED"], headcount: 6);
          fixture.Occupy("MED", proposed.EventId!.Value, occupied: 5);
          var handler = new AdjustEventCapacityHandler(
              fixture.Events, fixture.Capacities, fixture.Profiles, fixture.UnitOfWork,
              fixture.Audit, fixture.Types);

          var result = await handler.HandleAsync(new AdjustEventCapacityCommand(
              fixture.Managers["MED"], proposed.EventId.Value, 4), CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("capacity-below-bookings", result.Error.Code);
          Assert.Equal(5L, result.Error.Data!["minimum"]);
          Assert.Equal(6L, result.Error.Data!["currentTotal"]);
      }

      [Fact]
      public async Task Unchanged_adjustment_writes_no_audit()
      {
          var fixture = NegotiationFixture.Create().WithTypes("MED");
          var proposed = await fixture.ProposeAsync("MED", ["MED"], headcount: 6);
          var before = fixture.Audit.Entries.Count;
          var handler = new AdjustEventCapacityHandler(
              fixture.Events, fixture.Capacities, fixture.Profiles, fixture.UnitOfWork,
              fixture.Audit, fixture.Types);

          var result = await handler.HandleAsync(new AdjustEventCapacityCommand(
              fixture.Managers["MED"], proposed.EventId!.Value, 6), CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.False(result.Value.Changed);
          Assert.Equal(before, fixture.Audit.Entries.Count);
      }
  }
  ```

  The adjust suite needs `fixture.Occupy(typeCode, eventId, occupied)`, defined on the
  fixture above: it decrements the row `occupied` times through the domain Decrement
  (failing the test if the row exhausts early).

- [ ] **Step 2: Write the failing race test** against real PostgreSQL 16, following the
  Task 10 harness pattern (parallel tasks, each in its own connection):

  ```csharp
  // tests/EventBooking.Infrastructure.Tests/Concurrency/AcceptanceRaceTests.cs
  using EventBooking.Application.Negotiation;

  namespace EventBooking.Infrastructure.Tests.Concurrency;

  public sealed class AcceptanceRaceTests : PostgresConcurrencyHarness
  {
      // A 4-type proposal with 1 accepted; three Managers accept concurrently, 50 runs over
      // fresh proposals; each run yields exactly one Event, exactly 4 capacity rows, and a
      // Confirmed proposal. Verifies the pin by deleting the proposal row lock and watching
      // it fail (duplicate events or deadlocks within seconds).
      [Fact]
      public async Task Concurrent_final_acceptances_confirm_exactly_one_event()
      {
          for (var run = 0; run < 50; run++)
          {
              var proposalId = await SeedProposalAsync(["MED", "FIT", "IND", "ESC"], accepted: ["MED"]);
              var outcomes = await Task.WhenAll(
                  AcceptOnFreshScopeAsync("FIT", proposalId),
                  AcceptOnFreshScopeAsync("IND", proposalId),
                  AcceptOnFreshScopeAsync("ESC", proposalId));

              Assert.All(outcomes, o => Assert.True(o.IsSuccess));
              Assert.Equal("Confirmed", await ProposalStatusAsync(proposalId));
              Assert.Single(await EventsForProposalAsync(proposalId));
              Assert.Equal(4, await CapacityRowCountAsync(proposalId));
          }
      }
  }
  ```

  PostgresConcurrencyHarness is the Task 10 harness base (connections, seeding helpers);
  extend it with proposal seeding and per-scope handler resolution. Each accept runs the
  real RecordAcceptanceHandler on its own scope and connection.

- [ ] **Step 3: Run.** Expected: FAIL to compile — the Negotiation namespace does not exist.

  ```bash
  dotnet test tests/EventBooking.Application.Tests --filter "FullyQualifiedName~Negotiation"
  ```

- [ ] **Step 4: Implement.** Create the six handler files below in full, extend the error
  catalogue with the below-bookings factory, and delete the seven ported Events files. No
  business rule lives anywhere else.

  ```csharp
  // src/EventBooking.Application/Negotiation/ProposeEventHandler.cs
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Access;
  using EventBooking.Application.Common;
  using EventBooking.Domain.Access;
  using EventBooking.Domain.AppointmentTypes;
  using EventBooking.Domain.Audit;
  using EventBooking.Domain.Common;
  using EventBooking.Domain.Events;
  using EventBooking.Domain.Time;

  namespace EventBooking.Application.Negotiation;

  public sealed class ProposeEventHandler(
      IEventProposalRepository proposals,
      ILocationRepository locations,
      IAppointmentTypeRepository types,
      IStaffAccessProfileRepository profiles,
      IStaffAccessAuthorizer access,
      IUnitOfWork unitOfWork,
      IAuditLogger audit,
      IClock clock,
      IEventWindowZones zones)
  {
      public async Task<Result<ProposeEventOutcome>> HandleAsync(
          ProposeEventCommand command, CancellationToken ct)
      {
          var authorized = await access.AuthorizeAsync(
              command.StaffUserId, StaffCapability.ManageEventNegotiation, null, ct);
          if (authorized.IsFailure) return Result<ProposeEventOutcome>.Failure(authorized.Error);
          if (authorized.Value.AppointmentTypeId is not { } proposerTypeId)
              return Result<ProposeEventOutcome>.Failure(
                  Error.Forbidden("Proposing needs an assigned appointment type."));

          EventWindow window;
          try
          {
              window = new EventWindow(command.Date, command.StartTime, command.DurationMinutes);
          }
          catch (DomainException ex)
          {
              return Result<ProposeEventOutcome>.Failure(Error.Validation(ex.Message));
          }

          var location = await locations.GetAsync(command.LocationId, ct);
          if (location is null) return Result<ProposeEventOutcome>.Failure(Error.NotFound("No such location."));

          await using var transaction = await unitOfWork.BeginTransactionAsync(ct);

          var listed = await types.ListAsync(ct);
          var listedById = listed.ToDictionary(t => t.Id);
          var managerTypeIds = (await profiles.ListAsync(ct))
              .Where(p => p.IsManager && p.AppointmentTypeId is not null)
              .Select(p => p.AppointmentTypeId!.Value)
              .ToHashSet();

          var requested = command.ListedAppointmentTypeIds.Distinct().ToList();
          var offer = requested
              .Select(id => listedById.TryGetValue(id, out var t)
                  ? new ProposableAppointmentType(id, t.Code, t.IsActive, managerTypeIds.Contains(id))
                  : new ProposableAppointmentType(id, id.ToString(), false, false))
              .ToList();

          EventProposal proposal;
          try
          {
              proposal = EventProposal.Propose(
                  Guid.NewGuid(), location.Id, location.IsActive, location.TimeZoneId,
                  window, zones, clock.UtcNow, offer, proposerTypeId, command.StaffUserId, command.ProposerHeadcount);
              proposals.Add(proposal);
              audit.Record(AuditEntityTypes.EventProposal, proposal.Id, AuditAction.ProposalCreated,
                  ActorType.Staff, command.StaffUserId.ToString(), $"types {offer.Count}; zone {location.TimeZoneId}");
              await unitOfWork.SaveChangesAsync(ct);
              await transaction.CommitAsync(ct);
          }
          catch (ProposalValidationException ex)
          {
              await transaction.RollbackAsync(ct);
              return Result<ProposeEventOutcome>.Failure(Error.Validation(string.Join("; ", ex.Failures)));
          }
          catch (DomainException ex)
          {
              await transaction.RollbackAsync(ct);
              return Result<ProposeEventOutcome>.Failure(Error.Validation(ex.Message));
          }
          catch (UniqueConstraintViolationException)
          {
              await transaction.RollbackAsync(ct);
              return Result<ProposeEventOutcome>.Failure(Error.Conflict("An open proposal already exists for that window."));
          }

          // Success path: see the completion below (single-type confirmation creates the
          // event in the same transaction).
      }
  }
  ```

  The success path needs `IEventRepository events` as the last constructor parameter. A single-type proposal is fully accepted by the proposer's own acceptance, so
  the event is created in the same transaction (Task 6: single-type proposals confirm during
  propose); `Event.CreateFrom` marks the proposal `Confirmed`:

  ```csharp
  // Success path, replacing the trailing comment in the handler above:
  Guid? eventId = null;
  if (proposal.IsFullyAccepted)
  {
      var created = Event.CreateFrom(Guid.NewGuid(), proposal);
      await events.AddAsync(created, ct);
      eventId = created.Id;
      audit.Record(AuditEntityTypes.Event, created.Id, AuditAction.EventConfirmed,
          ActorType.Staff, command.StaffUserId.ToString(), window.ToString());
  }

  await unitOfWork.SaveChangesAsync(ct);
  await transaction.CommitAsync(ct);
  return Result<ProposeEventOutcome>.Success(new ProposeEventOutcome(
      proposal.Id, proposal.Status.ToString(), eventId));
  ```

  The open-window check the ported handler did in memory is retained: inside the
  transaction, list open proposals for the location and refuse on an equal window with the
  same conflict as below. The acceptance path is the serialised one (proposal row lock);
  the unique backstop on `event.proposal_id` guards confirmation, and any residual
  constraint violation maps to the conflict below.

  ```csharp
  // src/EventBooking.Application/Negotiation/RecordAcceptanceHandler.cs
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Access;
  using EventBooking.Application.Common;
  using EventBooking.Domain.Access;
  using EventBooking.Domain.Audit;
  using EventBooking.Domain.Common;
  using EventBooking.Domain.Events;

  namespace EventBooking.Application.Negotiation;

  public sealed class RecordAcceptanceHandler(
      IEventProposalRepository proposals,
      IEventRepository events,
      IStaffAccessAuthorizer access,
      IUnitOfWork unitOfWork,
      IAuditLogger audit)
  {
      public async Task<Result<RecordAcceptanceOutcome>> HandleAsync(
          RecordAcceptanceCommand command, CancellationToken ct)
      {
          var authorized = await access.AuthorizeAsync(
              command.StaffUserId, StaffCapability.ManageEventNegotiation, null, ct);
          if (authorized.IsFailure) return Result<RecordAcceptanceOutcome>.Failure(authorized.Error);
          if (authorized.Value.AppointmentTypeId is not { } actingType)
              return Result<RecordAcceptanceOutcome>.Failure(
                  Error.Forbidden("Accepting needs an assigned appointment type."));

          await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
          var proposal = await proposals.LockForUpdateAsync(command.ProposalId, ct);
          if (proposal is null)
              return Result<RecordAcceptanceOutcome>.Failure(Error.NotFound("No such proposal."));

          var previous = proposal.Acceptances
              .SingleOrDefault(a => a.AppointmentTypeId == actingType)?.Headcount;

          bool changed;
          try
          {
              changed = proposal.Accept(actingType, command.StaffUserId, command.Headcount);
          }
          catch (ProposalNotOpenException ex)
          {
              return Result<RecordAcceptanceOutcome>.Failure(
                  Error.Conflict($"The proposal is {ex.CurrentStatus} and can no longer be changed."));
          }
          catch (DomainException ex)
          {
              return Result<RecordAcceptanceOutcome>.Failure(Error.Validation(ex.Message));
          }

          if (!changed)
          {
              await transaction.CommitAsync(ct);
              return Result<RecordAcceptanceOutcome>.Success(new RecordAcceptanceOutcome(
                  proposal.Id, proposal.Status.ToString(), null, false));
          }

          var typeCode = (await TypeCodeAsync(proposal, actingType, ct));
          audit.Record(AuditEntityTypes.EventProposal, proposal.Id, AuditAction.AcceptanceRecorded,
              ActorType.Staff, command.StaffUserId.ToString(),
              previous is null ? $"{typeCode} headcount {command.Headcount}"
                  : $"{typeCode} headcount {previous} -> {command.Headcount}");

          Guid? eventId = null;
          if (proposal.IsFullyAccepted)
          {
              var created = Event.CreateFrom(Guid.NewGuid(), proposal);
              await events.AddAsync(created, ct);
              eventId = created.Id;
              audit.Record(AuditEntityTypes.Event, created.Id, AuditAction.EventConfirmed,
                  ActorType.Staff, command.StaffUserId.ToString(), proposal.Window.ToString());
          }

          await unitOfWork.SaveChangesAsync(ct);
          await transaction.CommitAsync(ct);
          return Result<RecordAcceptanceOutcome>.Success(new RecordAcceptanceOutcome(
              proposal.Id, proposal.Status.ToString(), eventId, true));
      }

      // The audit names the type the way a screen does. The code travels on the listed
      // set the handler already loaded for Propose; acceptance-only paths resolve it from
      // the type repository — pass IAppointmentTypeRepository on the constructor and look
      // the code up, falling back to the identifier string when the row is gone.
      private async Task<string> TypeCodeAsync(EventProposal proposal, Guid actingType, CancellationToken ct) =>
          actingType.ToString();
  }
  ```

  The TypeCodeAsync stub above is honest about what it does but wrong about where the
  code comes from: replace it with a real lookup. Append `IAppointmentTypeRepository types`
  as the last constructor parameter and replace the method:

  ```csharp
  private async Task<string> TypeCodeAsync(Guid actingType, CancellationToken ct) =>
      (await types.GetAsync(actingType, ct))?.Code ?? actingType.ToString();
  ```

  and call it as `TypeCodeAsync(actingType, ct)`.

  ```csharp
  // src/EventBooking.Application/Negotiation/WithdrawAcceptanceHandler.cs
  // src/EventBooking.Application/Negotiation/WithdrawProposalHandler.cs
  // Same shape as the ported handlers (authorize, null-scope forbidden, lock the proposal
  // row, call WithdrawAcceptance / Withdraw with the scoped type, audit AcceptanceWithdrawn /
  // ProposalWithdrawn, save, commit), except both catch ProposalNotOpenException into a
  // conflict carrying the current status instead of checking the status by hand:
  catch (ProposalNotOpenException ex)
  {
      return Result.Failure(
          Error.Conflict($"The proposal is {ex.CurrentStatus} and can no longer be changed."));
  }
  // WithdrawProposal additionally maps the non-proposer refusal (DomainException from
  // Withdraw) to Error.Forbidden("Only the proposing appointment type may withdraw the proposal.").
  ```

  ```csharp
  // src/EventBooking.Application/Negotiation/NegotiationBoardHandler.cs
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Access;
  using EventBooking.Application.Common;
  using EventBooking.Domain.Access;
  using EventBooking.Domain.Events;

  namespace EventBooking.Application.Negotiation;

  public sealed class NegotiationBoardHandler(
      IEventProposalRepository proposals,
      IEventRepository events,
      ILocationRepository locations,
      IStaffAccessAuthorizer access,
      IClock clock,
      IEventWindowZones zones)
  {
      public async Task<Result<NegotiationBoard>> HandleAsync(
          GetNegotiationBoardQuery query, CancellationToken ct)
      {
          var authorized = await access.AuthorizeAsync(
              query.StaffUserId, StaffCapability.ManageEventNegotiation, null, ct);
          if (authorized.IsFailure) return Result<NegotiationBoard>.Failure(authorized.Error);
          if (authorized.Value.AppointmentTypeId is not { } myType)
              return Result<NegotiationBoard>.Failure(
                  Error.Forbidden("The board needs an assigned appointment type."));

          var zoneByLocation = (await locations.ListAsync(ct)).ToDictionary(l => l.Id, l => l.TimeZoneId);
          var now = clock.UtcNow;

          var open = (await proposals.ListOpenAsync(ct))
              .Where(p => p.ListedAppointmentTypeIds.Contains(myType))
              .OrderBy(p => p.Window)
              .Select(p =>
              {
                  var mine = p.Acceptances.SingleOrDefault(a =>
                      a.AppointmentTypeId == myType && a.ManagerUserId == query.StaffUserId);
                  return new NegotiationBoardProposalView(p.Id, p.LocationId,
                      p.Window.Date, p.Window.StartTime, p.Window.EndTime,
                      p.ListedAppointmentTypeIds.Count, p.Acceptances.Count,
                      mine?.Headcount, mine is not null, p.CreatedByManagerUserId == query.StaffUserId);
              })
              .ToList();

          var confirmed = (await events.ListActiveAsync(DateOnly.MinValue, ct))
              .Where(e => e.Status == EventStatus.Active
                  && e.Capacities.Any(c => c.AppointmentTypeId == myType)
                  && zoneByLocation.TryGetValue(e.LocationId, out var zone)
                  && !e.Window.HasEnded(zones, zone, now))
              .OrderBy(e => e.Window)
              .Select(e => new NegotiationBoardEventView(e.Id, e.LocationId,
                  e.Window.Date, e.Window.StartTime, e.Window.EndTime,
                  e.CapacityFor(myType).TotalHeadcount, e.CapacityFor(myType).RemainingCapacity))
              .ToList();

          return Result<NegotiationBoard>.Success(new NegotiationBoard(open, confirmed));
      }
  }
  ```

  ```csharp
  // src/EventBooking.Application/Negotiation/AdjustEventCapacityHandler.cs
  // Same shape as the ported handler (authorize, scope type only, lock the caller's capacity
  // row, NotFound when the event does not list it, refuse cancelled events, adjust through
  // AdjustTotalHeadcount against OccupiedCapacity, unchanged commits silently, audit
  // CapacityAdjusted with old -> new totals), except the below-bookings refusal carries its
  // values in the error:
  if (adjustment.Status == CapacityAdjustmentStatus.BelowActiveBookings)
      return Result<AdjustEventCapacityOutcome>.Failure(Error.CapacityBelowBookings(
          $"Headcount cannot be lower than the active-booking count of {adjustment.MinimumTotalHeadcount}.",
          adjustment.MinimumTotalHeadcount, capacity.TotalHeadcount, capacity.RemainingCapacity));
  // Replace the ported AppointmentTypeIds.NameOf audit naming with a type-repository lookup
  // (append IAppointmentTypeRepository types as the last constructor parameter), falling
  // back to the identifier string.
  ```

  ```csharp
  // tests/EventBooking.Application.Tests/Negotiation/NegotiationFixture.cs (complete)
  using EventBooking.Application.Negotiation;
  using EventBooking.Application.Tests.Fakes;
  using EventBooking.Application.Tests.ReferenceData;
  using EventBooking.Domain.Access;
  using EventBooking.Domain.AppointmentTypes;
  using EventBooking.Domain.Audit;
  using EventBooking.Domain.Locations;

  namespace EventBooking.Application.Tests.Negotiation;

  public sealed class NegotiationFixture
  {
      public InMemoryEventProposalRepository Proposals = new();
      public InMemoryEventRepository Events = new();
      public InMemoryEventCapacityRepository Capacities = null!;
      public InMemoryLocationRepository Locations = new();
      public InMemoryAppointmentTypeRepository Types = new();
      public InMemoryStaffAccessProfileRepository Profiles = new();
      public FakeUnitOfWork UnitOfWork = new();
      public RecordingAuditLogger Audit = new();
      public FakeClock Clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

      public Dictionary<string, Guid> TypeIds = new();
      public Dictionary<string, Guid> Managers = new();
      public Guid LocationId;
      public Guid NullScopedManager = Guid.Parse("c0000009-0000-0000-0000-000000000009");

      public static NegotiationFixture Create()
      {
          var fixture = new NegotiationFixture();
          fixture.Capacities = new InMemoryEventCapacityRepository(fixture.Events);
          fixture.Types.Items.Clear();
          var location = Location.Create(Guid.NewGuid(), "LONDON_HQ", "London HQ", "1 High St", "Europe/London", TestZones.Instance);
          fixture.Locations.Items.Add(location);
          fixture.LocationId = location.Id;
          fixture.Profiles.Add(StaffAccessProfile.Create(fixture.NullScopedManager, Role.Manager, null));
          return fixture;
      }

      public NegotiationFixture WithTypes(params string[] codes)
      {
          foreach (var code in codes)
          {
              var type = AppointmentType.Create(Guid.NewGuid(), code, code);
              Types.Items.Add(type);
              TypeIds[code] = type.Id;
              var manager = Guid.NewGuid();
              Profiles.Add(StaffAccessProfile.Create(manager, Role.Manager, type.Id));
              Managers[code] = manager;
          }

          return this;
      }

      public NegotiationFixture WithNullScopedManager() => this;

      private ProposeEventHandler Proposer => new(
          Proposals, Locations, Types, Profiles, Profiles, UnitOfWork, Audit, Clock, TestZones.Instance, Events);
      private RecordAcceptanceHandler Accepter => new(
          Proposals, Events, Profiles, UnitOfWork, Audit, Types);

      public async Task<ProposeEventOutcome> ProposeAsync(string proposerCode, string[] listed, int headcount)
      {
          var result = await Proposer.HandleAsync(new ProposeEventCommand(
              Managers[proposerCode], LocationId, new DateOnly(2026, 10, 14), new TimeOnly(9, 30), 90,
              listed.Select(code => TypeIds[code]).ToList(), headcount), CancellationToken.None);
          Assert.True(result.IsSuccess);
          return result.Value;
      }

      public async Task<Result<RecordAcceptanceOutcome>> AcceptAsync(string? typeCode, Guid proposalId, int headcount)
      {
          var user = typeCode is null ? NullScopedManager : Managers[typeCode];
          return await Accepter.HandleAsync(new RecordAcceptanceCommand(user, proposalId, headcount), CancellationToken.None);
      }

      public async Task WithdrawProposalAsync(string typeCode, Guid proposalId)
      {
          var handler = new WithdrawProposalHandler(Proposals, Profiles, UnitOfWork, Audit);
          var result = await handler.HandleAsync(new WithdrawProposalCommand(Managers[typeCode], proposalId), CancellationToken.None);
          Assert.True(result.IsSuccess);
      }

      public IReadOnlyList<string> AuditActionsFor(Guid proposalId) =>
          Audit.Entries.Where(e => e.EntityId == proposalId).Select(e => e.Action.ToString()).ToList();

      public void Occupy(string typeCode, Guid eventId, int occupied)
      {
          var row = Events.Items.Single(e => e.Id == eventId).CapacityFor(TypeIds[typeCode]);
          for (var i = 0; i < occupied; i++) row.Decrement();
      }
  }
  ```

  TestZones is the Task 12 double (`ReferenceDataTestDoubles.cs`); the Task 13 test
  project references it from the same test assembly. InMemoryLocationRepository,
  type Add, and TestZones/MemoryBlocking all land in Task 12 — Task 13 consumes them.
  RecordAcceptanceHandler's constructor takes the type repository last, matching the
  implementation above.

- [ ] **Step 4: Run.** Expected: PASS — the new suites plus the full solution.

  ```bash
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  ```

  Expect the Application count to rise (five new suites replacing the Events suites) and
  Infrastructure to rise (the race suite). A count that does not match the executor's own
  before/after diff is a signal to read the diff, not to adjust the number.

- [ ] **Step 5: Delete nothing by hand beyond the seven listed files**, then commit and push
  the executor's code — not the plan documents — under the master plan's message:

  ```bash
  test -z "$(git status --porcelain --ignored=no | grep -v '^??')"
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  git add src/EventBooking.Application/Common/Error.cs src/EventBooking.Application/Negotiation/ src/EventBooking.Application/Events/ tests/EventBooking.Application.Tests/Negotiation/ tests/EventBooking.Infrastructure.Tests/Concurrency/AcceptanceRaceTests.cs
  git diff --cached --name-only
  git diff --cached
  test -n "$EXECUTOR_COAUTHOR"
  git commit -m "feat(app): N-way negotiation with serialised confirmation

  Co-authored-by: $EXECUTOR_COAUTHOR"
  git push
  ```
