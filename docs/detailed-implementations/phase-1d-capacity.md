# 01d — Capacity generalised to N rows (Task 7)

[← Phase overview](phase-1-domain.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This task follows Task 6 on the Phase 1 branch. The predecessor charged three capacity counters together in a fixed order; an `Event` now carries one `EventCapacity` per listed `AppointmentType`, a booking charges only the types it requires, and the order rows are locked in becomes a pure function rather than an accident of how the code was written.

> Use superpowers:executing-plans. Complete changed types and exact before/after files are embedded
> in the numbered companion volumes; apply them with the script in Step 3, never by hand.

**Goal:** An `Event` charges and releases exactly the `AppointmentType`s named, all or nothing; a headcount adjustment is judged against that type's active-booking count and reports the minimum it would accept; cancellation is refused once the `EventWindow` has started; and the lock order is computable from the keys alone.

**Architecture:** The aggregate owns the arithmetic and the caller supplies the facts. Charging resolves every row before moving any, so a refusal leaves the `Event` exactly as it was — a partial charge would leave an `Attendee` holding capacity for some of their requirements and none for the rest. A headcount adjustment that is below the active-booking count is returned as an outcome, not thrown, because FR-3.6 has to tell the Manager the minimum and the current server values. The lock order is a static function of keys so a caller can sort rows it has not loaded yet.

**Tech Stack:** .NET 10, xUnit, EF Core, PostgreSQL Testcontainers, bUnit.

**Spec:** [Master Task 7](../superpowers/plans/2026-09-19-eventbooking-implementation.md), [domain model](../design/01-domain-model.md), [functional requirements](../design/02-functional-requirements.md), [ontology](../ontology.md).

## Global constraints

Stay on the Phase 1 branch. No schema change belongs here: the numbers, their bounds and the database check constraint are unchanged, and Task 9 writes the fresh schema. Do not rework the booking and cancellation handlers to route through the new charge and release methods; they still operate on the rows their repository locked, and Task 10 owns the ordered-lock helpers that let them adopt these. The zone a window is read in is still the transitional location's until Phase 3 gives each handler the `Event`'s own `Location`.

## Review focus

STOP AND CHECK four things. A charge is all or nothing: the exhausted type is named and nothing has moved, which the test proves by reading every other row afterwards. A type the `Event` does not list is refused rather than silently skipped. A headcount below the active-booking count comes back as a refusal carrying the minimum, where a non-positive total or one above 1000 still throws — a bad request and a legitimate business refusal are not the same thing. And cancellation is now judged on the window's start instant, not on its date, so an `Event` that began earlier today can no longer be cancelled while one later today still can.

### Task 7: Capacity generalised to N rows

**Files:**

- Modify: src/EventBooking.Application/Events/AdjustEventCapacityHandler.cs
- Modify: src/EventBooking.Application/Events/CancelEventHandler.cs
- Create: src/EventBooking.Domain/Events/CapacityAdjustment.cs
- Modify: src/EventBooking.Domain/Events/Event.cs
- Modify: src/EventBooking.Domain/Events/EventCapacity.cs
- Create: src/EventBooking.Domain/Events/EventCapacityKey.cs
- Modify: tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/ConfirmBookingHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/InviteOptionReplacementTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/ViewInviteHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/AdjustEventCapacityHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/EventCancellationConcurrencyTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/GetManagerEventBoardHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs
- Modify: tests/EventBooking.Application.Tests/Invites/EligibleEventFinderTests.cs
- Modify: tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs
- Modify: tests/EventBooking.Domain.Tests/Events/EventCancellationTests.cs
- Modify: tests/EventBooking.Domain.Tests/Events/EventCapacityHeadcountAdjustmentTests.cs
- Modify: tests/EventBooking.Domain.Tests/Events/EventCapacityTests.cs
- Test: tests/EventBooking.Domain.Tests/Events/NTypeCapacityTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyHarness.cs
- Modify: tests/EventBooking.Infrastructure.Tests/DashboardQueryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/RepositoryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs
- Modify: tests/TestSupport/ProposalFixture.cs

**Interfaces:**

These complete types define the changed public boundary and its domain behavior. Apply them with the after files at Step 3, not before the failing test.

```csharp
namespace EventBooking.Domain.Events;

/// <summary>
/// The key of one <see cref="EventCapacity"/> row. A command that touches several rows sorts its
/// keys with <see cref="Event.CapacityLockOrder"/> before locking, so two commands whose type sets
/// overlap can never deadlock (FR-3.3; design 01 — lock ordering).
/// </summary>
/// <param name="EventId">The event whose capacity row this is.</param>
/// <param name="AppointmentTypeId">The appointment type the row counts.</param>
public readonly record struct EventCapacityKey(Guid EventId, Guid AppointmentTypeId);
```

```csharp
namespace EventBooking.Domain.Events;

/// <summary>What a headcount adjustment did, or why it was refused (FR-3.5, FR-3.6).</summary>
public enum CapacityAdjustmentStatus
{
    /// <summary>The total and the remaining count both moved by the same delta.</summary>
    Adjusted,

    /// <summary>The submitted total equalled the current one: nothing changed, so nothing is audited.</summary>
    Unchanged,

    /// <summary>The submitted total was below the type's active-booking count; nothing changed.</summary>
    BelowActiveBookings,
}

/// <summary>
/// The outcome of a headcount adjustment. A refusal carries the minimum the row would accept and
/// the current server values, which is what FR-3.6 requires the Manager to be told.
/// </summary>
/// <param name="Status">What the adjustment did, or why it was refused.</param>
/// <param name="MinimumTotalHeadcount">The lowest total this row would accept.</param>
/// <param name="TotalHeadcount">The total after the call.</param>
/// <param name="RemainingCapacity">The remaining count after the call.</param>
public readonly record struct CapacityAdjustment(
    CapacityAdjustmentStatus Status,
    int MinimumTotalHeadcount,
    int TotalHeadcount,
    int RemainingCapacity)
{
    /// <summary>Whether anything moved, and so whether there is anything to audit.</summary>
    public bool Changed => Status == CapacityAdjustmentStatus.Adjusted;
}
```

```csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Events;

/// <summary>
/// Remaining bookable headcount for one appointment type on one event. This row is the single
/// source of truth for booking eligibility, and the row the confirm transaction locks.
/// </summary>
public sealed class EventCapacity
{
    /// <summary>The largest total headcount any one type may carry (design 08 — boundary values).</summary>
    public const int MaximumTotalHeadcount = 1000;

    private EventCapacity()
    {
    }

    /// <summary>Defines event id for the current use case.</summary>
    public Guid EventId { get; private set; }

    /// <summary>Defines appointment type id for the current use case.</summary>
    public Guid AppointmentTypeId { get; private set; }

    /// <summary>Defines total headcount for the current use case.</summary>
    public int TotalHeadcount { get; private set; }

    /// <summary>Defines remaining capacity for the current use case.</summary>
    public int RemainingCapacity { get; private set; }

    /// <summary>Defines has spare for the current use case.</summary>
    public bool HasSpare => RemainingCapacity > 0;

    /// <summary>Defines occupied capacity for the current use case.</summary>
    public int OccupiedCapacity => TotalHeadcount - RemainingCapacity;

    /// <summary>This row's key, for sorting a command's rows into lock order.</summary>
    public EventCapacityKey Key => new(EventId, AppointmentTypeId);

    internal static EventCapacity Initialise(Guid eventId, Guid appointmentTypeId, int totalHeadcount)
    {
        AppointmentTypeIdsGuard(appointmentTypeId);

        var total = Guard.Positive(totalHeadcount, "totalHeadcount");

        return new EventCapacity
        {
            EventId = eventId,
            AppointmentTypeId = appointmentTypeId,
            TotalHeadcount = total,
            RemainingCapacity = total,
        };
    }

    /// <summary>Charges one place against this row, refusing once the row is exhausted.</summary>
    public void Decrement()
    {
        Guard.Against(RemainingCapacity <= 0, ExhaustedMessage(AppointmentTypeId));

        RemainingCapacity -= 1;
    }

    /// <summary>Returns one charged place to this row, refusing to exceed the accepted total.</summary>
    public void Increment()
    {
        Guard.Against(
            RemainingCapacity >= TotalHeadcount,
            "Remaining capacity cannot exceed the headcount the manager accepted.");

        RemainingCapacity += 1;
    }

    /// <summary>
    /// Replaces the total this type's Manager accepted, applying the same delta to the remaining
    /// count (FR-3.5). A total below the type's active-booking count is refused rather than
    /// thrown, because FR-3.6 wants the Manager told the minimum and the current server values.
    /// </summary>
    /// <param name="totalHeadcount">The new total: positive, and at most <see cref="MaximumTotalHeadcount"/>.</param>
    /// <param name="activeBookingCount">Active bookings requiring this type on this event.</param>
    public CapacityAdjustment AdjustTotalHeadcount(int totalHeadcount, int activeBookingCount)
    {
        var next = Guard.Positive(totalHeadcount, "totalHeadcount");
        Guard.Against(
            next > MaximumTotalHeadcount,
            $"totalHeadcount must not exceed {MaximumTotalHeadcount}.");
        var active = Guard.NotNegative(activeBookingCount, "activeBookingCount");

        if (next == TotalHeadcount)
        {
            return new CapacityAdjustment(
                CapacityAdjustmentStatus.Unchanged, active, TotalHeadcount, RemainingCapacity);
        }

        // The caller's count and this row's own occupancy should agree. Taking the larger of the
        // two keeps 0 <= remainingCapacity <= totalHeadcount true even if they have drifted, and
        // reports a minimum the next attempt will actually be allowed to use.
        var minimum = Math.Max(active, OccupiedCapacity);
        if (next < minimum)
        {
            return new CapacityAdjustment(
                CapacityAdjustmentStatus.BelowActiveBookings, minimum, TotalHeadcount, RemainingCapacity);
        }

        var delta = next - TotalHeadcount;
        TotalHeadcount = next;
        RemainingCapacity += delta;

        return new CapacityAdjustment(
            CapacityAdjustmentStatus.Adjusted, minimum, TotalHeadcount, RemainingCapacity);
    }

    internal static string ExhaustedMessage(Guid appointmentTypeId) =>
        $"capacity-exhausted: appointment type {appointmentTypeId} has no remaining capacity "
        + "on this event.";

    private static void AppointmentTypeIdsGuard(Guid appointmentTypeId) =>
        AppointmentTypes.AppointmentTypeIds.EnsureKnown(appointmentTypeId);
}
```

```csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Time;

namespace EventBooking.Domain.Events;

/// <summary>Defines event for the current use case.</summary>
public sealed class Event
{
    private readonly List<EventCapacity> _capacities = [];

    private Event()
    {
        // Required by the persistence layer's constructor binding.
        Window = null!;
    }

    /// <summary>Defines id for the current use case.</summary>
    public Guid Id { get; private set; }

    /// <summary>Defines proposal id for the current use case.</summary>
    public Guid ProposalId { get; private set; }

    /// <summary>The location hosting the event, carried from its proposal.</summary>
    public Guid LocationId { get; private set; }

    /// <summary>Defines window for the current use case.</summary>
    public EventWindow Window { get; private set; }

    /// <summary>Defines status for the current use case.</summary>
    public EventStatus Status { get; private set; } = EventStatus.Active;

    /// <summary>Defines capacities for the current use case.</summary>
    public IReadOnlyList<EventCapacity> Capacities => _capacities;

    /// <summary>
    /// The only way a eventItem is created. Marks the proposal confirmed in the same call, so
    /// a proposal can never back a second eventItem.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="proposal">The proposal.</param>
    public static Event CreateFrom(Guid id, EventProposal proposal)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(proposal is null, "proposal must be supplied.");

        proposal!.MarkConfirmed();

        var eventItem = new Event
        {
            Id = id,
            ProposalId = proposal.Id,
            LocationId = proposal.LocationId,
            Window = proposal.Window,
            Status = EventStatus.Active,
        };

        foreach (var acceptance in proposal.Acceptances.OrderBy(a => a.AppointmentTypeId))
        {
            eventItem._capacities.Add(
                EventCapacity.Initialise(id, acceptance.AppointmentTypeId, acceptance.Headcount));
        }

        return eventItem;
    }

    /// <summary>
    /// Sorts capacity keys into the order every command must take their row locks in: ascending
    /// event id, then ascending appointment type id (FR-3.3). Pure, so a caller can sort keys it
    /// has not loaded yet, and duplicates collapse.
    /// </summary>
    /// <param name="keys">The keys to sort, in any order and with any duplicates.</param>
    public static IReadOnlyList<EventCapacityKey> CapacityLockOrder(IEnumerable<EventCapacityKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        return
        [
            .. keys
                .Distinct()
                .OrderBy(key => key.EventId)
                .ThenBy(key => key.AppointmentTypeId),
        ];
    }

    /// <summary>Defines capacity for for the current use case.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    public EventCapacity CapacityFor(Guid appointmentTypeId)
    {
        var capacity = _capacities.SingleOrDefault(c => c.AppointmentTypeId == appointmentTypeId);
        Guard.Against(capacity is null, $"This event has no capacity counter for {appointmentTypeId}.");

        return capacity!;
    }

    /// <summary>
    /// Charges one place against each named type, and only those: an event may list types the
    /// attendee does not require, and those rows are untouched (FR-3.4). All or nothing — every
    /// row is checked before any row moves, so a refusal leaves the event exactly as it was.
    /// </summary>
    /// <param name="appointmentTypeIds">The types the attendee requires; every one must be listed.</param>
    public void ChargeRequiredTypes(IEnumerable<Guid> appointmentTypeIds)
    {
        var rows = RowsFor(appointmentTypeIds);

        Guard.Against(
            Status != EventStatus.Active,
            "A cancelled event cannot be charged.");

        if (rows.Find(row => !row.HasSpare) is { } exhausted)
        {
            throw new DomainException(EventCapacity.ExhaustedMessage(exhausted.AppointmentTypeId));
        }

        foreach (var row in rows)
        {
            row.Decrement();
        }
    }

    /// <summary>
    /// Returns one place to each named type. A cancelled event still releases: cancelling voids
    /// its bookings and gives their capacity back in the same transaction.
    /// </summary>
    /// <param name="appointmentTypeIds">The types to release; every one must be listed.</param>
    public void ReleaseTypes(IEnumerable<Guid> appointmentTypeIds)
    {
        var rows = RowsFor(appointmentTypeIds);

        if (rows.Find(row => row.RemainingCapacity >= row.TotalHeadcount) is { } full)
        {
            throw new DomainException(
                $"Appointment type {full.AppointmentTypeId} has nothing charged to release on this event.");
        }

        foreach (var row in rows)
        {
            row.Increment();
        }
    }

    /// <summary>Defines has spare capacity for all for the current use case.</summary>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    public bool HasSpareCapacityForAll(IEnumerable<Guid> appointmentTypeIds) =>
        Status == EventStatus.Active
        && appointmentTypeIds.All(id => CapacityFor(id).HasSpare);

    /// <summary>
    /// Cancels the event. Refused once the window has started: the appointments have begun, so
    /// voiding the bookings and re-inviting would misdescribe what happened.
    /// </summary>
    /// <param name="zones">The zone abstraction.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    /// <param name="now">The current instant.</param>
    public void Cancel(IEventWindowZones zones, string timeZoneId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(zones);

        Guard.Against(Status == EventStatus.Cancelled, "This event has already been cancelled.");
        Guard.Against(
            Window.HasStarted(zones, timeZoneId, now),
            "An event whose window has started cannot be cancelled.");

        Status = EventStatus.Cancelled;
    }

    private List<EventCapacity> RowsFor(IEnumerable<Guid> appointmentTypeIds)
    {
        ArgumentNullException.ThrowIfNull(appointmentTypeIds);

        var ids = appointmentTypeIds.Distinct().Order().ToList();
        Guard.Against(ids.Count == 0, "At least one appointment type must be named.");

        // CapacityFor refuses a type this event does not list, which is the check that keeps an
        // attendee whose requirements have drifted from silently booking a partial event.
        return [.. ids.Select(CapacityFor)];
    }
}
```

**Context you need**

- FR-3.1: the system holds `0 <= remainingCapacity <= totalHeadcount` for every `EventCapacity` at all times, enforced by a transactional check and by a database check constraint.
- FR-3.2: concurrent confirmations whose requirements overlap on an `AppointmentType` with fewer places than confirmations are accepted up to the places available and the rest rejected as capacity-exhausted. Never overbook, never deadlock.
- FR-3.3 (new): every capacity-changing command locks the affected `EventCapacity` rows in ascending (eventId, appointmentTypeId) order before reading them.
- FR-3.4 (new): a booking decrements exactly the `InviteRequirement` types. Types listed on the `Event` but not required are untouched.
- FR-3.5: while an `Event` is `Active` and its window has not started, the current Manager of a listed type may set that type's totalHeadcount to any positive value no lower than the type's active-booking count; remainingCapacity changes by the same delta, audited as `CapacityAdjusted` only on a real change.
- FR-3.6: an adjustment that would go below the active-booking count is rejected, returning the minimum allowed value and the current server values.
- Design 01 (Capacity): a booking charges only the types the attendee requires — “An `Event` listing medical, fitting and induction can host an attendee who needs only induction; the other two rows are untouched.”
- Design 01 (Lock ordering): `Attendee`, then `EventProposal`, then `Event` rows by ascending id, then `EventCapacity` rows by (eventId, appointmentTypeId) ascending. The predecessor always touched exactly three rows in a fixed order, so it never needed a rule; overlapping subsets of a variable set do.
- Design 08 (boundary values): `ProposalAcceptance.headcount` and `EventCapacity.totalHeadcount` both run 1 to 1000.
- The ontology already states that cancellation is refused once the `EventWindow` has started, and that every read-check-write row-locks the affected `EventCapacity` rows in ascending appointment type order. Task 4 supplied the window's start instant; this task is the first caller to use it for an `Event`.
- The event-cancellation sequence diagram in design 03b locked the `Event` first, against the documented order. It is corrected in the plan repository alongside this task, together with the matching row in design 01's cross-aggregate table; neither file exists in the executor's checkout, so there is nothing to apply here.

- [ ] **Step 1: Write the failing tests**

Create these complete files before applying production changes.

tests/EventBooking.Domain.Tests/Events/NTypeCapacityTests.cs

```csharp
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;
using EventBooking.Domain.Time;

namespace EventBooking.Domain.Tests.Events;

/// <summary>
/// Task 7: capacity is N rows, a booking charges only the types it requires, a headcount
/// adjustment is judged against the type's active-booking count, cancellation is refused once the
/// window has started, and the lock order is a pure function of the keys
/// (FR-3.1 to FR-3.6; design 01 — lock ordering and capacity).
/// </summary>
public class NTypeCapacityTests
{
    private static readonly Guid Medical = Guid.Parse("a0000001-0000-0000-0000-000000000001");
    private static readonly Guid Fitting = Guid.Parse("a0000002-0000-0000-0000-000000000002");
    private static readonly Guid Induction = Guid.Parse("a0000003-0000-0000-0000-000000000003");

    private static readonly Guid London = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Proposer = Guid.Parse("c0000001-0000-0000-0000-000000000001");

    private const string TimeZoneId = "Europe/London";
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly EventWindow Window = new(new DateOnly(2026, 10, 14), new TimeOnly(9, 30), 90);
    private static readonly AlwaysUniqueZones Zones = new();

    private static Event EventOf(params (Guid TypeId, int Headcount)[] listed)
    {
        var proposal = EventProposal.Propose(
            Guid.NewGuid(),
            London,
            locationIsActive: true,
            TimeZoneId,
            Window,
            Zones,
            Now,
            [.. listed.Select(l => new ProposableAppointmentType(l.TypeId, l.TypeId.ToString(), true, true))],
            listed[0].TypeId,
            Proposer,
            listed[0].Headcount);

        foreach (var (typeId, headcount) in listed.Skip(1))
        {
            proposal.Accept(typeId, Guid.NewGuid(), headcount);
        }

        return Event.CreateFrom(Guid.NewGuid(), proposal);
    }

    [Fact]
    public void ChargingOneRequiredTypeLeavesEveryOtherListedTypeUntouched()
    {
        var eventItem = EventOf((Medical, 4), (Fitting, 6), (Induction, 5));

        eventItem.ChargeRequiredTypes([Induction]);

        Assert.Equal(4, eventItem.CapacityFor(Medical).RemainingCapacity);
        Assert.Equal(6, eventItem.CapacityFor(Fitting).RemainingCapacity);
        Assert.Equal(4, eventItem.CapacityFor(Induction).RemainingCapacity);
    }

    [Fact]
    public void AChargeIsAllOrNothingAndNamesTheExhaustedType()
    {
        var eventItem = EventOf((Medical, 4), (Fitting, 6), (Induction, 1));
        eventItem.ChargeRequiredTypes([Induction]);

        var exception = Assert.Throws<DomainException>(
            () => eventItem.ChargeRequiredTypes([Medical, Induction]));

        Assert.Contains("capacity-exhausted", exception.Message, StringComparison.Ordinal);
        Assert.Contains(Induction.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Equal(4, eventItem.CapacityFor(Medical).RemainingCapacity);
        Assert.Equal(0, eventItem.CapacityFor(Induction).RemainingCapacity);
    }

    [Fact]
    public void ChargingATypeTheEventDoesNotListIsRefused()
    {
        var eventItem = EventOf((Medical, 4), (Induction, 5));

        var exception = Assert.Throws<DomainException>(
            () => eventItem.ChargeRequiredTypes([Medical, Fitting]));

        Assert.Contains(Fitting.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Equal(4, eventItem.CapacityFor(Medical).RemainingCapacity);
    }

    [Fact]
    public void AChargeIsRefusedOnceTheEventIsCancelled()
    {
        var eventItem = EventOf((Medical, 4), (Induction, 5));
        eventItem.Cancel(Zones, TimeZoneId, Now);

        Assert.Throws<DomainException>(() => eventItem.ChargeRequiredTypes([Medical]));
        Assert.Equal(4, eventItem.CapacityFor(Medical).RemainingCapacity);
    }

    [Fact]
    public void ReleaseRestoresExactlyTheChargedTypes()
    {
        var eventItem = EventOf((Medical, 4), (Fitting, 6), (Induction, 5));
        eventItem.ChargeRequiredTypes([Medical, Induction]);

        eventItem.ReleaseTypes([Medical, Induction]);

        Assert.Equal(4, eventItem.CapacityFor(Medical).RemainingCapacity);
        Assert.Equal(6, eventItem.CapacityFor(Fitting).RemainingCapacity);
        Assert.Equal(5, eventItem.CapacityFor(Induction).RemainingCapacity);
    }

    [Fact]
    public void ReleasingMoreThanWasChargedIsRefusedWithoutMutation()
    {
        var eventItem = EventOf((Medical, 4), (Induction, 5));
        eventItem.ChargeRequiredTypes([Medical]);

        Assert.Throws<DomainException>(() => eventItem.ReleaseTypes([Medical, Induction]));

        Assert.Equal(3, eventItem.CapacityFor(Medical).RemainingCapacity);
        Assert.Equal(5, eventItem.CapacityFor(Induction).RemainingCapacity);
    }

    [Fact]
    public void ReducingTheTotalAppliesTheSameDeltaToRemaining()
    {
        var capacity = EventOf((Medical, 6)).CapacityFor(Medical);

        var adjustment = capacity.AdjustTotalHeadcount(4, activeBookingCount: 3);

        Assert.Equal(CapacityAdjustmentStatus.Adjusted, adjustment.Status);
        Assert.True(adjustment.Changed);
        Assert.Equal(4, capacity.TotalHeadcount);
        Assert.Equal(4, capacity.RemainingCapacity);
        Assert.Equal(4, adjustment.TotalHeadcount);
        Assert.Equal(4, adjustment.RemainingCapacity);
    }

    [Fact]
    public void ATotalBelowTheActiveBookingCountIsRefusedAndReturnsTheMinimum()
    {
        var capacity = EventOf((Medical, 6)).CapacityFor(Medical);

        var adjustment = capacity.AdjustTotalHeadcount(2, activeBookingCount: 3);

        Assert.Equal(CapacityAdjustmentStatus.BelowActiveBookings, adjustment.Status);
        Assert.False(adjustment.Changed);
        Assert.Equal(3, adjustment.MinimumTotalHeadcount);
        Assert.Equal(6, adjustment.TotalHeadcount);
        Assert.Equal(6, adjustment.RemainingCapacity);
        Assert.Equal(6, capacity.TotalHeadcount);
        Assert.Equal(6, capacity.RemainingCapacity);
    }

    [Fact]
    public void ResubmittingTheCurrentTotalReportsNoChange()
    {
        var capacity = EventOf((Medical, 6)).CapacityFor(Medical);

        var adjustment = capacity.AdjustTotalHeadcount(6, activeBookingCount: 3);

        Assert.Equal(CapacityAdjustmentStatus.Unchanged, adjustment.Status);
        Assert.False(adjustment.Changed);
        Assert.Equal(6, capacity.TotalHeadcount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void ATotalOutsideTheAllowedRangeIsRefused(int totalHeadcount)
    {
        var capacity = EventOf((Medical, 6)).CapacityFor(Medical);

        Assert.Throws<DomainException>(
            () => capacity.AdjustTotalHeadcount(totalHeadcount, activeBookingCount: 0));

        Assert.Equal(6, capacity.TotalHeadcount);
        Assert.Equal(6, capacity.RemainingCapacity);
    }

    [Fact]
    public void CancellationIsRefusedOnceTheWindowHasStarted()
    {
        var eventItem = EventOf((Medical, 4));
        var started = Window.StartInstant(Zones, TimeZoneId);

        var exception = Assert.Throws<DomainException>(
            () => eventItem.Cancel(Zones, TimeZoneId, started));

        Assert.Equal("An event whose window has started cannot be cancelled.", exception.Message);
        Assert.Equal(EventStatus.Active, eventItem.Status);
    }

    [Fact]
    public void TheLockOrderSortsByEventThenAppointmentType()
    {
        var firstEvent = Guid.Parse("e0000001-0000-0000-0000-000000000001");
        var secondEvent = Guid.Parse("e0000002-0000-0000-0000-000000000002");

        var ordered = Event.CapacityLockOrder(
        [
            new(secondEvent, Fitting),
            new(firstEvent, Induction),
            new(secondEvent, Medical),
            new(firstEvent, Medical),
            new(firstEvent, Induction),
        ]);

        Assert.Equal(
            [
                new EventCapacityKey(firstEvent, Medical),
                new EventCapacityKey(firstEvent, Induction),
                new EventCapacityKey(secondEvent, Medical),
                new EventCapacityKey(secondEvent, Fitting),
            ],
            ordered);
    }

    private sealed class AlwaysUniqueZones : IEventWindowZones
    {
        public bool IsKnownZone(string timeZoneId) => true;

        public LocalTimeValidity ValidityOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            LocalTimeValidity.Unique;

        public DateTimeOffset InstantOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            new(date.ToDateTime(time), TimeSpan.Zero);

        public DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId) =>
            DateOnly.FromDateTime(instant.UtcDateTime);

        public string AbbreviationOf(DateTimeOffset instant, string timeZoneId) => "BST";
    }
}
```

- [ ] **Step 2: Verify the tests fail**

```bash
dotnet test tests/EventBooking.Domain.Tests --filter FullyQualifiedName~NTypeCapacityTests
```

Expected: The suite does not compile: the charge and release methods, the capacity key, the adjustment outcome, the active-booking parameter and the three-argument cancel do not exist yet. A Docker startup failure is not the intended failure.

- [ ] **Step 3: Apply the exact implementation and regression edits**

The 9 phase-1d-edits-NNN.md files supply 28 complete before/after changes. The script validates every payload and current file before writing. It accepts an already-applied after state, refuses unrelated edits, and only deletes explicitly listed files whose before hash matches.

```bash
node --input-type=module <<'TASK_PAYLOAD'
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
const root=fs.realpathSync('.'), plan='docs/detailed-implementations';
const sha=s=>crypto.createHash('sha256').update(s).digest('hex');
const names=fs.readdirSync(plan).filter(n=>n.startsWith('phase-1d-edits-')&&n.endsWith('.md')).sort();
if(names.length!==9)throw Error('Incomplete edit volumes.');
const entries=new Map();
for(const name of names){
 const text=fs.readFileSync(path.join(plan,name),'utf8');
 const pattern=/<!-- retirement-file: (.+) -->\n\n`{5}[^\n]*\n([\s\S]*?)\n`{5}/g;
 for(const match of text.matchAll(pattern)){
  const m=JSON.parse(match[1]);
  if(path.isAbsolute(m.file)||m.file.split('/').includes('..'))throw Error('Unsafe path.');
  const e=entries.get(m.id)??{...m,before:new Map(),after:new Map(),counts:{}};
  if(e.file!==m.file||e.beforeSha!==m.beforeSha||e.afterSha!==m.afterSha||e[m.side].has(m.part))throw Error('Conflicting metadata.');
  e[m.side].set(m.part,match[2]+'\n');e.counts[m.side]=m.parts;entries.set(m.id,e);
 }
}
if(entries.size!==28)throw Error('Incomplete operation set.');
const actions=[];
for(const e of entries.values()){
 for(const side of ['before','after']){
  if(e[side+'Sha']===null)continue;
  if(e[side].size!==e.counts[side])throw Error('Missing parts.');
  const parts=Array.from({length:e.counts[side]},(_,i)=>e[side].get(i+1));
  if(parts.some(p=>p===undefined))throw Error('Missing part number.');
  e[side+'Text']=parts.join('');
  if(sha(e[side+'Text'])!==e[side+'Sha'])throw Error('Payload checksum mismatch.');
 }
 const target=path.join(root,e.file);
 let parent=path.dirname(target);while(!fs.existsSync(parent))parent=path.dirname(parent);
 const resolved=fs.realpathSync(parent);
 if(resolved!==root&&!resolved.startsWith(root+path.sep))throw Error('Parent escapes checkout.');
 if(fs.existsSync(target)&&fs.lstatSync(target).isSymbolicLink())throw Error('Symlink target.');
 const actual=fs.existsSync(target)?sha(fs.readFileSync(target)):null;
 if(actual!==e.beforeSha&&actual!==e.afterSha)throw Error('Unrelated edit: '+e.file);
 actions.push({target,body:e.afterText,remove:e.afterSha===null});
}
for(const action of actions){
 if(action.remove){if(fs.existsSync(action.target))fs.unlinkSync(action.target);}
 else{fs.mkdirSync(path.dirname(action.target),{recursive:true});fs.writeFileSync(action.target,action.body);}
}
console.log('Applied '+actions.length+' verified file changes.');
TASK_PAYLOAD
```

No schema migration belongs to this task.

- [ ] **Step 4: Verify the targeted tests pass**

```bash
dotnet test tests/EventBooking.Domain.Tests --filter FullyQualifiedName~NTypeCapacityTests
```

Expected: all targeted cases pass, with zero skipped tests.

- [ ] **Step 5: Build and validate the complete solution**

```bash
dotnet build EventBooking.sln -warnaserror
dotnet test EventBooking.sln
```

Expected: zero warnings, zero errors and zero failed or skipped tests. The verified checkpoint contains 1500 tests: Domain 320, Application 424, Infrastructure 173, API 232, MCP 35, Web 241 and SeedData 75.

- [ ] **Step 6: Commit and push**

No ontology change belongs to this task. `docs/ontology.ttl` already carries the capacity invariants, the ascending lock order and the rule that cancellation is refused once the window has started; this task only puts them in code. If you find a concept that is genuinely missing, edit the source and regenerate before committing.

```bash
git add -- \
  'src/EventBooking.Application/Events/AdjustEventCapacityHandler.cs' \
  'src/EventBooking.Application/Events/CancelEventHandler.cs' \
  'src/EventBooking.Domain/Events/CapacityAdjustment.cs' \
  'src/EventBooking.Domain/Events/Event.cs' \
  'src/EventBooking.Domain/Events/EventCapacity.cs' \
  'src/EventBooking.Domain/Events/EventCapacityKey.cs' \
  'tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/ConfirmBookingHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/InviteOptionReplacementTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/ViewInviteHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/AdjustEventCapacityHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/EventCancellationConcurrencyTests.cs' \
  'tests/EventBooking.Application.Tests/Events/GetManagerEventBoardHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/EligibleEventFinderTests.cs' \
  'tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/EventCancellationTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/EventCapacityHeadcountAdjustmentTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/EventCapacityTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/NTypeCapacityTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyHarness.cs' \
  'tests/EventBooking.Infrastructure.Tests/DashboardQueryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/RepositoryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs' \
  'tests/TestSupport/ProposalFixture.cs'
git diff --cached --name-only
git diff --cached
node scripts/check-ontology-terms.mjs
: "${EXECUTOR_COAUTHOR:?Set the executing harness co-author identity}"
git commit -m "feat(domain): N-row EventCapacity charging only required types" -m "Co-authored-by: $EXECUTOR_COAUTHOR"
git push -u origin HEAD
```

Continue to Task 8 on the same branch.
