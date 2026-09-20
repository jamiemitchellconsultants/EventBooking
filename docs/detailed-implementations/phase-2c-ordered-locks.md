# 02c — Ordered row locks, and the harness that proves they do not deadlock (Task 10)

[← Phase overview](phase-2-persistence.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This task follows Task 9b. Every row lock the application takes moves into one place, the transaction records the highest level it has taken, and a command that asks for a lock below one it already holds fails in a test rather than deadlocking in production. A harness then drives those helpers from forty connections at once to prove the claim rather than assert it.

> Use superpowers:executing-plans. Complete changed types and exact before/after files are embedded
> in the numbered companion volumes; apply them with the script in Step 3, never by hand.

**Goal:** One set of helpers locks an attendee by id, a proposal by id, events by ids in ascending order, and capacity rows in (event id, appointment type id) order. The unit of work tracks the highest level taken in the current transaction and throws on a descent in a Debug build. The three lifecycle repositories read through those helpers under a lock mode of none, update or skip locked.

**Architecture:** The order stops being a property of how each handler happens to be written and becomes a property of one class. That is what makes it testable: the tracker is a pure object, so the violation the master plan names is a unit test with no database at all, while the orderings that only appear under contention are proved by the harness. The capacity helper consumes Task 7's own ordering function rather than re-sorting, so the domain and the database agree on the order by construction. The helpers are deliberately reached through the existing repository methods too, so every handler written before this task gains the guard without being touched.

**Tech Stack:** .NET 10, xUnit, EF Core, PostgreSQL Testcontainers, bUnit.

**Spec:** [Master Task 10](../superpowers/plans/2026-09-19-eventbooking-implementation.md), [domain model](../design/01-domain-model.md), [functional requirements](../design/02-functional-requirements.md), [ontology](../ontology.md).

## Global constraints

The guard is on in a Debug build, which is what tests run, and off in a released one: a lock order no test has ever reached should not become a 500 for the attendee who happens to hit it, and the tracker still records the level either way. The harness drives the helpers directly, not an Application handler — none exists until Task 15, and the point here is the ordering, not the booking rules on top of it. Every attempt gets an unpooled connection of its own; a pooled one may still be holding another attempt's transaction open. Keep the four documented levels: `Invite` and `Booking` locks are not levels yet, because the design names only these four.

## Review focus

STOP AND CHECK four things. The violation is a unit test: an attendee lock after a capacity lock throws, and no database is involved. Forty parallel bookings on one event fill the binding type exactly once — every requirement subset in the rotation includes it, so ten succeed and thirty are told which type ran out. No attempt anywhere reports SQLSTATE 40P01, and no capacity row leaves the range the check constraint allows. And the shuffled two-event test that matters is the one with the event locks removed: with them in place the attempts serialise before they reach a capacity row, so that version passes whether or not the rows are ordered — it is the capacity-only variant that fails when the ordering goes.

### Task 10: The lock helpers, the order they enforce, and the concurrency harness

**Files:**

- Modify: src/EventBooking.Infrastructure/DependencyInjection.cs
- Create: src/EventBooking.Infrastructure/Persistence/Locking/LockLevel.cs
- Create: src/EventBooking.Infrastructure/Persistence/Locking/LockMode.cs
- Create: src/EventBooking.Infrastructure/Persistence/Locking/LockOrderViolationException.cs
- Create: src/EventBooking.Infrastructure/Persistence/Locking/RowLocks.cs
- Create: src/EventBooking.Infrastructure/Persistence/Locking/TransactionLocks.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs
- Modify: src/EventBooking.Infrastructure/Persistence/UnitOfWork.cs
- Test: tests/EventBooking.Infrastructure.Tests/Concurrency/BookingConcurrencyHarness.cs
- Test: tests/EventBooking.Infrastructure.Tests/Concurrency/BookingConcurrencyTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/Concurrency/LockOrderTests.cs

**Interfaces:**

These complete types define the changed public boundary and its domain behavior. Apply them with the after files at Step 3, not before the failing test.

```csharp
namespace EventBooking.Infrastructure.Persistence.Locking;

/// <summary>
/// The documented row-lock order, ascending. Every command takes its locks in this order, so two
/// commands whose row sets overlap wait for each other instead of deadlocking (design 01 — lock
/// ordering; FR-3.3). The numbers are the order itself, not identifiers: nothing persists them.
/// </summary>
public enum LockLevel
{
    /// <summary>The lifecycle root. Anything that changes what an attendee is doing starts here.</summary>
    Attendee = 1,

    /// <summary>The negotiation root.</summary>
    EventProposal = 2,

    /// <summary>One event, locked by id. Several events are locked in ascending id order.</summary>
    Event = 3,

    /// <summary>The capacity rows, locked last and in (event id, appointment type id) order.</summary>
    EventCapacity = 4,
}
```

```csharp
namespace EventBooking.Infrastructure.Persistence.Locking;

/// <summary>How a repository read should lock the rows it returns.</summary>
public enum LockMode
{
    /// <summary>No lock. The result is a snapshot and must not be used as authority for a write.</summary>
    None = 0,

    /// <summary>`FOR UPDATE`: wait for any conflicting lock, then hold the row until commit.</summary>
    Update = 1,

    /// <summary>
    /// `FOR UPDATE SKIP LOCKED`: take what is free and leave the rest. For work a second worker
    /// may simply process instead, never for a row the caller has to be certain about.
    /// </summary>
    SkipLocked = 2,
}
```

```csharp
namespace EventBooking.Infrastructure.Persistence.Locking;

/// <summary>
/// Records the highest <see cref="LockLevel"/> the current transaction has taken, and refuses a
/// descent. Scoped to one DbContext, and reset by the unit of work whenever a transaction begins
/// or ends: without the reset, one command's capacity lock would make every later attendee lock on
/// the same connection look like a violation.
/// </summary>
public sealed class TransactionLocks
{
    /// <summary>
    /// The guard is on in a Debug build, which is what tests run. A released build still records
    /// the level — the comparison is free — but does not turn a lock order it has never seen in a
    /// test into a 500 for the attendee who happened to hit it.
    /// </summary>
    public const bool EnforcedByDefault =
#if DEBUG
        true;
#else
        false;
#endif

    /// <summary>Creates a tracker enforcing the order according to the build.</summary>
    public TransactionLocks()
        : this(EnforcedByDefault)
    {
    }

    /// <summary>Creates a tracker, overriding whether a descent throws.</summary>
    /// <param name="enforced">True to throw on a descent; false to record it and carry on.</param>
    public TransactionLocks(bool enforced) => Enforced = enforced;

    /// <summary>Gets whether a descent throws.</summary>
    public bool Enforced { get; }

    /// <summary>Gets the highest level taken since the last reset, or null if none has been.</summary>
    public LockLevel? Highest { get; private set; }

    /// <summary>Records a lock about to be taken, refusing one below the level already held.</summary>
    /// <param name="level">The level being taken.</param>
    public void Enter(LockLevel level)
    {
        if (Highest is { } held && level < held)
        {
            if (Enforced)
            {
                throw new LockOrderViolationException(held, level);
            }

            return;
        }

        Highest = level;
    }

    /// <summary>Forgets what the last transaction held.</summary>
    public void Reset() => Highest = null;
}
```

```csharp
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Locking;

/// <summary>
/// Every row lock the application takes, in one place, so the order is a property of this class
/// rather than of how each handler happens to be written. Each helper records its level with the
/// transaction's <see cref="TransactionLocks"/> before issuing the statement, so a command that
/// descends fails in a test instead of deadlocking in production.
/// </summary>
/// <param name="context">The context whose connection holds the transaction.</param>
/// <param name="locks">The tracker for the current transaction.</param>
public sealed class RowLocks(EventBookingDbContext context, TransactionLocks locks)
{
    /// <summary>
    /// For a test or a tool driving one context directly. The tracker is its own, so the order is
    /// checked within this instance and not across a unit of work it does not share.
    /// </summary>
    /// <param name="context">The context whose connection holds the transaction.</param>
    public RowLocks(EventBookingDbContext context)
        : this(context, new TransactionLocks())
    {
    }

    /// <summary>Locks one attendee and loads the requirements lifecycle handlers read.</summary>
    /// <param name="id">The attendee id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Attendee?> LockAttendeeAsync(Guid id, CancellationToken cancellationToken)
    {
        locks.Enter(LockLevel.Attendee);

        var attendee = (await context.Attendees
            .FromSqlInterpolated($"SELECT * FROM attendee WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken))
            .SingleOrDefault();

        if (attendee is not null)
        {
            await context.Entry(attendee).Collection(item => item.Requirements)
                .LoadAsync(cancellationToken);
        }

        return attendee;
    }

    /// <summary>
    /// Takes the attendee row only if it is free, and returns null when another transaction holds
    /// it. For work a second worker may simply pick up instead — never for a row the caller has to
    /// be certain about, where null would be read as "no such attendee".
    /// </summary>
    /// <param name="id">The attendee id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Attendee?> SkipLockedAttendeeAsync(Guid id, CancellationToken cancellationToken)
    {
        locks.Enter(LockLevel.Attendee);

        var attendee = (await context.Attendees
            .FromSqlInterpolated(
                $"SELECT * FROM attendee WHERE id = {id} FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken))
            .SingleOrDefault();

        if (attendee is not null)
        {
            await context.Entry(attendee).Collection(item => item.Requirements)
                .LoadAsync(cancellationToken);
        }

        return attendee;
    }

    /// <summary>Locks one proposal and loads its acceptances and listed types.</summary>
    /// <param name="id">The proposal id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<EventProposal?> LockProposalAsync(Guid id, CancellationToken cancellationToken)
    {
        locks.Enter(LockLevel.EventProposal);

        var proposal = (await context.EventProposals
            .FromSqlInterpolated($"SELECT * FROM event_proposal WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken))
            .SingleOrDefault();

        if (proposal is not null)
        {
            await context.Entry(proposal).Collection(item => item.Acceptances)
                .LoadAsync(cancellationToken);
            await context.Entry(proposal).Collection(item => item.ListedTypes)
                .LoadAsync(cancellationToken);
        }

        return proposal;
    }

    /// <summary>Takes the proposal row only if it is free, returning null when it is held.</summary>
    /// <param name="id">The proposal id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<EventProposal?> SkipLockedProposalAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        locks.Enter(LockLevel.EventProposal);

        var proposal = (await context.EventProposals
            .FromSqlInterpolated(
                $"SELECT * FROM event_proposal WHERE id = {id} FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken))
            .SingleOrDefault();

        if (proposal is not null)
        {
            await context.Entry(proposal).Collection(item => item.Acceptances)
                .LoadAsync(cancellationToken);
            await context.Entry(proposal).Collection(item => item.ListedTypes)
                .LoadAsync(cancellationToken);
        }

        return proposal;
    }

    /// <summary>Takes the event row only if it is free, returning null when it is held.</summary>
    /// <param name="id">The event id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Event?> SkipLockedEventAsync(Guid id, CancellationToken cancellationToken)
    {
        locks.Enter(LockLevel.Event);

        var eventItem = (await context.Events
            .FromSqlInterpolated($"SELECT * FROM event WHERE id = {id} FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken))
            .SingleOrDefault();

        if (eventItem is not null)
        {
            await context.Entry(eventItem).Collection(item => item.Capacities)
                .LoadAsync(cancellationToken);
        }

        return eventItem;
    }

    /// <summary>Locks one event by id, with its capacity rows loaded.</summary>
    /// <param name="id">The event id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Event?> LockEventAsync(Guid id, CancellationToken cancellationToken) =>
        (await LockEventsAsync([id], cancellationToken)).SingleOrDefault();

    /// <summary>
    /// Locks events in ascending id order, whatever order the caller listed them in. A command
    /// that touches two events — a cancellation cascading onto a recovery booking, say — must not
    /// take them in the order its request happened to name.
    /// </summary>
    /// <param name="ids">The event ids, in any order and with any duplicates.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<IReadOnlyList<Event>> LockEventsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var ordered = ids.Distinct().Order().ToArray();
        if (ordered.Length == 0)
        {
            return [];
        }

        locks.Enter(LockLevel.Event);

        var events = await context.Events
            .FromSql(
                $"""
                 SELECT * FROM event
                 WHERE id = ANY({ordered})
                 ORDER BY id
                 FOR UPDATE
                 """)
            .ToListAsync(cancellationToken);

        foreach (var eventItem in events)
        {
            await context.Entry(eventItem).Collection(item => item.Capacities)
                .LoadAsync(cancellationToken);
        }

        return events;
    }

    /// <summary>Locks one event's capacity rows for the supplied appointment types.</summary>
    /// <param name="eventId">The event id.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task<IReadOnlyList<EventCapacity>> LockCapacitiesAsync(
        Guid eventId,
        IEnumerable<Guid> appointmentTypeIds,
        CancellationToken cancellationToken) =>
        LockCapacitiesAsync(
            appointmentTypeIds.Select(typeId => new EventCapacityKey(eventId, typeId)),
            cancellationToken);

    /// <summary>
    /// Locks capacity rows in (event id, appointment type id) order, using the domain's own
    /// ordering function. One statement per event, events ascending, rows within an event ordered
    /// by type: the same total order the domain names, taken one event at a time.
    /// </summary>
    /// <param name="keys">The rows to lock, in any order and with any duplicates.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<IReadOnlyList<EventCapacity>> LockCapacitiesAsync(
        IEnumerable<EventCapacityKey> keys,
        CancellationToken cancellationToken)
    {
        var ordered = Event.CapacityLockOrder(keys);
        if (ordered.Count == 0)
        {
            return [];
        }

        locks.Enter(LockLevel.EventCapacity);

        var rows = new List<EventCapacity>(ordered.Count);
        foreach (var group in ordered.GroupBy(key => key.EventId))
        {
            var eventId = group.Key;
            var typeIds = group.Select(key => key.AppointmentTypeId).ToArray();

            rows.AddRange(await context.EventCapacities
                .FromSql(
                    $"""
                     SELECT event_id, appointment_type_id, total_headcount, remaining_capacity
                     FROM event_capacity
                     WHERE event_id = {eventId}
                       AND appointment_type_id = ANY({typeIds})
                     ORDER BY appointment_type_id
                     FOR UPDATE
                     """)
                .ToListAsync(cancellationToken));
        }

        return rows;
    }
}
```

```csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Infrastructure.Persistence.Locking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace EventBooking.Infrastructure.Persistence;

/// <summary>
/// Commits one DbContext unit of work, maps uniqueness backstops to application errors, and owns
/// the lock-order tracker for the transaction it opens.
/// </summary>
public sealed class UnitOfWork(EventBookingDbContext context, TransactionLocks locks) : IUnitOfWork
{
    /// <summary>For a test driving one context directly, with a tracker of its own.</summary>
    /// <param name="context">The context to commit.</param>
    public UnitOfWork(EventBookingDbContext context)
        : this(context, new TransactionLocks())
    {
    }

    /// <summary>Saves pending changes or translates a PostgreSQL uniqueness violation.</summary>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new UniqueConstraintViolationException(exception);
        }
    }

    /// <summary>Begins the outer transaction or joins the transaction already owned by the context.</summary>
    public async Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        // A handler that calls a service which also opens a transaction must not start a second
        // one; the outermost caller owns the commit.
        if (context.Database.CurrentTransaction is not null)
        {
            return new JoinedScope();
        }

        // A fresh transaction holds nothing yet. Without this, one command's capacity lock would
        // make every later attendee lock on the same scoped context look like a descent.
        locks.Reset();

        var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        return new EfTransactionScope(transaction, locks);
    }

    private sealed class EfTransactionScope(IDbContextTransaction transaction, TransactionLocks locks)
        : ITransactionScope
    {
        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            await transaction.CommitAsync(cancellationToken);
            locks.Reset();
        }

        public async Task RollbackAsync(CancellationToken cancellationToken)
        {
            await transaction.RollbackAsync(cancellationToken);
            locks.Reset();
        }

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            locks.Reset();
        }
    }

    private sealed class JoinedScope : ITransactionScope
    {
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RollbackAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
```

**Context you need**

- Master plan Task 10: unit-of-work lock helpers — lock attendee by id; lock proposal by id; lock events by ids (ascending); lock capacity rows for (event id, type ids) ordered by (event id, appointment type id) with FOR UPDATE.
- Master plan Task 10: the unit of work records the highest lock level taken in the current transaction and throws, in Debug and in tests, if a lower-level lock is requested after a higher one. Repositories expose load with lock mode: none, update, skip locked.
- Master plan Task 10 (harness): 40 parallel bookings on one event with three types at ten each and requirement subsets in rotation — exactly ten succeed, the others report capacity exhausted, no row goes below zero, and no PostgreSQL deadlock is raised. Two events with locks taken in shuffled order: no deadlock. A capacity adjustment racing eight bookings: the arithmetic closes, and the adjustment is refused if the bookings got there first.
- Master plan Task 10: the harness drives the Application handlers once they exist; for now it drives a minimal in-test booking routine built only on the lock helpers, replaced by the real handler in Task 15.
- The documented order is `Attendee`, `EventProposal`, `Event`, `EventCapacity` (design 01 — lock ordering), settled by the user against the cancellation flow's sequence diagram, which locked the event first.
- Task 7 produced the pure ordering function over capacity keys. This task is its first infrastructure caller; the domain and the database must not sort differently.
- Task 7's charge and release methods are domain API the booking and cancellation handlers do not call yet. They still work on the rows their repository locked; Task 15 is where the real handler adopts both these helpers and those methods.
- The master plan writes the three appointment types as MED, FIT and IND. The prototype carries the predecessor's three seeded types until Phase 3, so the rotation uses those: the type every subset includes is the one that binds.

- [ ] **Step 1: Write the failing tests**

Create these complete files before applying production changes.

tests/EventBooking.Infrastructure.Tests/Concurrency/LockOrderTests.cs

```csharp
using EventBooking.Infrastructure.Persistence.Locking;

namespace EventBooking.Infrastructure.Tests.Concurrency;

/// <summary>
/// The documented order is `Attendee`, `EventProposal`, `Event`, `EventCapacity`. Two transactions
/// that take the same rows in different orders deadlock, and a deadlock is a 500 to whichever
/// attendee PostgreSQL picks. The tracker turns that runtime coin-toss into a failing test.
/// </summary>
public class LockOrderTests
{
    [Fact]
    public void AnAttendeeLockAfterACapacityLockIsALockOrderViolation()
    {
        var locks = new TransactionLocks(enforced: true);
        locks.Enter(LockLevel.EventCapacity);

        var ex = Assert.Throws<LockOrderViolationException>(() => locks.Enter(LockLevel.Attendee));

        Assert.Equal(LockLevel.EventCapacity, ex.Held);
        Assert.Equal(LockLevel.Attendee, ex.Requested);
        Assert.Contains("Attendee", ex.Message, StringComparison.Ordinal);
        Assert.Contains("EventCapacity", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDocumentedOrderIsAccepted()
    {
        var locks = new TransactionLocks(enforced: true);

        locks.Enter(LockLevel.Attendee);
        locks.Enter(LockLevel.EventProposal);
        locks.Enter(LockLevel.Event);
        locks.Enter(LockLevel.EventCapacity);

        Assert.Equal(LockLevel.EventCapacity, locks.Highest);
    }

    /// <summary>Several rows at one level is the normal case: three capacity rows, two events.</summary>
    [Fact]
    public void RepeatingTheLevelAlreadyHeldIsAllowed()
    {
        var locks = new TransactionLocks(enforced: true);

        locks.Enter(LockLevel.Event);
        locks.Enter(LockLevel.Event);

        Assert.Equal(LockLevel.Event, locks.Highest);
    }

    [Theory]
    [InlineData(LockLevel.EventProposal, LockLevel.Attendee)]
    [InlineData(LockLevel.Event, LockLevel.EventProposal)]
    [InlineData(LockLevel.Event, LockLevel.Attendee)]
    [InlineData(LockLevel.EventCapacity, LockLevel.Event)]
    public void EveryDescentIsRefused(LockLevel held, LockLevel requested)
    {
        var locks = new TransactionLocks(enforced: true);
        locks.Enter(held);

        Assert.Throws<LockOrderViolationException>(() => locks.Enter(requested));
    }

    /// <summary>
    /// The next transaction starts from nothing. Without the reset, one command's capacity lock
    /// would make every later attendee lock on the same connection look like a violation.
    /// </summary>
    [Fact]
    public void ResettingClearsWhatTheLastTransactionHeld()
    {
        var locks = new TransactionLocks(enforced: true);
        locks.Enter(LockLevel.EventCapacity);

        locks.Reset();

        Assert.Null(locks.Highest);
        locks.Enter(LockLevel.Attendee);
        Assert.Equal(LockLevel.Attendee, locks.Highest);
    }

    /// <summary>
    /// Production still records the order — the guard is what is off, so a released build pays a
    /// comparison rather than an exception on a path a test never reached.
    /// </summary>
    [Fact]
    public void AnUnenforcedTrackerRecordsWithoutThrowing()
    {
        var locks = new TransactionLocks(enforced: false);

        locks.Enter(LockLevel.EventCapacity);
        locks.Enter(LockLevel.Attendee);

        Assert.Equal(LockLevel.EventCapacity, locks.Highest);
    }

    [Fact]
    public void TheGuardIsOnByDefaultInADebugBuild()
    {
#if DEBUG
        Assert.True(new TransactionLocks().Enforced);
#else
        Assert.False(new TransactionLocks().Enforced);
#endif
    }
}
```

tests/EventBooking.Infrastructure.Tests/Concurrency/BookingConcurrencyTests.cs

```csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;

namespace EventBooking.Infrastructure.Tests.Concurrency;

/// <summary>
/// The scenarios the master plan names for Task 10, driven straight against the lock helpers from
/// many connections at once. Two things are being proved: no capacity row can go below zero, and
/// no combination of type sets or event orders can deadlock.
///
/// The master plan writes the three types as MED, FIT and IND. The prototype carries the
/// predecessor's three seeded types, so MED is the medical check-up, FIT the uniform fitting, and
/// IND the drug and alcohol test — the type every requirement subset includes, and therefore the
/// one that binds.
/// </summary>
[Collection("postgres")]
public class BookingConcurrencyTests(PostgresFixture fixture)
{
    private static readonly Guid Med = AppointmentTypeIds.MedicalCheckUp;
    private static readonly Guid Fit = AppointmentTypeIds.UniformFitting;
    private static readonly Guid Ind = AppointmentTypeIds.DrugAndAlcoholTesting;

    /// <summary>The rotation from the master plan. Every subset needs IND, so IND is the ceiling.</summary>
    private static readonly IReadOnlyList<Guid>[] Rotation =
    [
        [Med, Ind],
        [Ind],
        [Fit, Ind],
        [Med, Fit, Ind],
    ];

    private readonly BookingConcurrencyHarness _harness = new(fixture);

    [Fact]
    public async Task FortyParallelBookingsFillTheBindingTypeExactlyOnce()
    {
        await fixture.ResetAsync();
        var eventId = await GivenEventAsync(headcount: 10);

        var attempts = await Task.WhenAll(Enumerable.Range(0, 40).Select(index =>
            Task.Run(() => _harness.TryChargeAsync(
                [eventId], Rotation[index % Rotation.Length], CancellationToken.None))));

        Assert.DoesNotContain(attempts, attempt => attempt.Outcome == ConcurrentOutcome.Deadlocked);
        Assert.Equal(10, attempts.Count(attempt => attempt.Outcome == ConcurrentOutcome.Booked));
        Assert.Equal(30, attempts.Count(attempt => attempt.Outcome == ConcurrentOutcome.CapacityExhausted));
        Assert.All(
            attempts.Where(attempt => attempt.Outcome == ConcurrentOutcome.CapacityExhausted),
            attempt => Assert.Equal(Ind, attempt.ExhaustedTypeId));

        var rows = await _harness.CapacitiesAsync(eventId);
        Assert.All(rows, row => Assert.InRange(row.RemainingCapacity, 0, row.TotalHeadcount));
        Assert.Equal(0, Remaining(rows, Ind));
    }

    /// <summary>
    /// The ordering test. Each attempt names its two events in a different order, and the helpers
    /// have to sort them: without that, half the attempts take A then B and half take B then A,
    /// which is the textbook deadlock.
    /// </summary>
    [Fact]
    public async Task BookingsAcrossTwoEventsInShuffledOrderNeverDeadlock()
    {
        await fixture.ResetAsync();
        var first = await GivenEventAsync(headcount: 30);
        var second = await GivenEventAsync(headcount: 30);

        var attempts = await Task.WhenAll(Enumerable.Range(0, 40).Select(index =>
            Task.Run(() => _harness.TryChargeAsync(
                index % 2 == 0 ? [first, second] : [second, first],
                Rotation[index % Rotation.Length],
                CancellationToken.None))));

        Assert.DoesNotContain(attempts, attempt => attempt.Outcome == ConcurrentOutcome.Deadlocked);
        Assert.Equal(30, attempts.Count(attempt => attempt.Outcome == ConcurrentOutcome.Booked));

        foreach (var eventId in new[] { first, second })
        {
            var rows = await _harness.CapacitiesAsync(eventId);
            Assert.All(rows, row => Assert.InRange(row.RemainingCapacity, 0, row.TotalHeadcount));
            Assert.Equal(0, Remaining(rows, Ind));
        }
    }

    /// <summary>
    /// The same shuffle with the event locks removed, so the capacity row order is the only thing
    /// preventing a deadlock. This is the test that fails if <c>CapacityLockOrder</c> stops being
    /// applied: the one above passes either way, because the event lock serialises first.
    /// </summary>
    [Fact]
    public async Task CapacityLocksAcrossTwoEventsInShuffledOrderNeverDeadlock()
    {
        await fixture.ResetAsync();
        var first = await GivenEventAsync(headcount: 30);
        var second = await GivenEventAsync(headcount: 30);

        var attempts = await Task.WhenAll(Enumerable.Range(0, 40).Select(index =>
            Task.Run(() => _harness.TryChargeCapacitiesOnlyAsync(
                index % 2 == 0 ? [first, second] : [second, first],
                Rotation[index % Rotation.Length],
                CancellationToken.None))));

        Assert.DoesNotContain(attempts, attempt => attempt.Outcome == ConcurrentOutcome.Deadlocked);
        Assert.Equal(30, attempts.Count(attempt => attempt.Outcome == ConcurrentOutcome.Booked));

        foreach (var eventId in new[] { first, second })
        {
            var rows = await _harness.CapacitiesAsync(eventId);
            Assert.All(rows, row => Assert.InRange(row.RemainingCapacity, 0, row.TotalHeadcount));
            Assert.Equal(0, Remaining(rows, Ind));
        }
    }

    /// <summary>
    /// A headcount cut racing the bookings it would invalidate. Whichever order the rows grant the
    /// lock in, the arithmetic has to close: either the cut lands and the bookings that follow see
    /// the smaller total, or it is refused because the bookings got there first.
    /// </summary>
    [Fact]
    public async Task ACapacityCutRacingEightBookingsLeavesTheArithmeticIntact()
    {
        await fixture.ResetAsync();
        var eventId = await GivenEventAsync(headcount: 10);

        var work = new List<Task<ConcurrentAttempt>>
        {
            Task.Run(() => _harness.TryAdjustAsync(eventId, Ind, 5, CancellationToken.None)),
        };
        work.AddRange(Enumerable.Range(0, 8).Select(_ =>
            Task.Run(() => _harness.TryChargeAsync([eventId], [Ind], CancellationToken.None))));

        var results = await Task.WhenAll(work);
        var adjustment = results[0];
        var bookings = results[1..];

        Assert.DoesNotContain(results, attempt => attempt.Outcome == ConcurrentOutcome.Deadlocked);

        var booked = bookings.Count(attempt => attempt.Outcome == ConcurrentOutcome.Booked);
        var rows = await _harness.CapacitiesAsync(eventId);
        var row = rows.Single(item => item.AppointmentTypeId == Ind);

        Assert.Equal(row.TotalHeadcount - booked, row.RemainingCapacity);
        Assert.InRange(row.RemainingCapacity, 0, row.TotalHeadcount);

        if (adjustment.Outcome == ConcurrentOutcome.AdjustmentRefused)
        {
            // Refused only because more bookings than the new total were already in. The minimum
            // it reported is what was booked at that instant, so it is above the requested five
            // and no higher than the number that eventually got in.
            Assert.Equal(10, row.TotalHeadcount);
            Assert.InRange(adjustment.MinimumAccepted!.Value, 6, booked);
        }
        else
        {
            Assert.Equal(5, row.TotalHeadcount);
            Assert.True(booked <= 5);
        }
    }

    private static int Remaining(IEnumerable<EventCapacity> rows, Guid appointmentTypeId) =>
        rows.Single(row => row.AppointmentTypeId == appointmentTypeId).RemainingCapacity;

    private async Task<Guid> GivenEventAsync(int headcount)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 10, 12), new TimeOnly(9, 0), 240),
            Guid.NewGuid());
        proposal.Accept(Med, Guid.NewGuid(), headcount);
        proposal.Accept(Fit, Guid.NewGuid(), headcount);
        proposal.Accept(Ind, Guid.NewGuid(), headcount);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        await using var context = fixture.NewContext();
        context.EventProposals.Add(proposal);
        context.Events.Add(eventItem);
        await context.SaveChangesAsync();

        return eventItem.Id;
    }
}
```

- [ ] **Step 2: Verify the tests fail**

```bash
dotnet test tests/EventBooking.Infrastructure.Tests --filter FullyQualifiedName~LockOrderTests
dotnet test tests/EventBooking.Infrastructure.Tests --filter FullyQualifiedName~BookingConcurrencyTests
```

Expected: The suite does not compile: the lock level, the lock mode, the tracker and the helpers do not exist, and neither does the harness the scenarios drive. A Docker startup failure is not the intended failure.

- [ ] **Step 3: Apply the exact implementation and regression edits**

The 2 phase-2c-edits-NNN.md files supply 11 complete before/after changes. The script validates every payload and current file before writing. It accepts an already-applied after state, refuses unrelated edits, and only deletes explicitly listed files whose before hash matches.

```bash
node --input-type=module <<'TASK_PAYLOAD'
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
const root=fs.realpathSync('.'), plan='docs/detailed-implementations';
const sha=s=>crypto.createHash('sha256').update(s).digest('hex');
const names=fs.readdirSync(plan).filter(n=>n.startsWith('phase-2c-edits-')&&n.endsWith('.md')).sort();
if(names.length!==2)throw Error('Incomplete edit volumes.');
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
if(entries.size!==11)throw Error('Incomplete operation set.');
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
dotnet test tests/EventBooking.Infrastructure.Tests --filter FullyQualifiedName~LockOrderTests
dotnet test tests/EventBooking.Infrastructure.Tests --filter FullyQualifiedName~BookingConcurrencyTests
```

Expected: all targeted cases pass, with zero skipped tests.

- [ ] **Step 5: Build and validate the complete solution**

```bash
dotnet build EventBooking.sln -warnaserror
dotnet test EventBooking.sln
```

Expected: zero warnings, zero errors and zero failed or skipped tests. The verified checkpoint contains 1557 tests: Domain 360, Application 424, Infrastructure 190, API 232, MCP 35, Web 241 and SeedData 75.

- [ ] **Step 6: Commit and push**

No ontology change belongs to this task. Lock order is a persistence concern; `docs/ontology.ttl` already states the cross-aggregate ordering this code implements. If you find a concept that is genuinely missing, edit the source and regenerate before committing.

```bash
git add -- \
  'src/EventBooking.Infrastructure/DependencyInjection.cs' \
  'src/EventBooking.Infrastructure/Persistence/Locking/LockLevel.cs' \
  'src/EventBooking.Infrastructure/Persistence/Locking/LockMode.cs' \
  'src/EventBooking.Infrastructure/Persistence/Locking/LockOrderViolationException.cs' \
  'src/EventBooking.Infrastructure/Persistence/Locking/RowLocks.cs' \
  'src/EventBooking.Infrastructure/Persistence/Locking/TransactionLocks.cs' \
  'src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs' \
  'src/EventBooking.Infrastructure/Persistence/UnitOfWork.cs' \
  'tests/EventBooking.Infrastructure.Tests/Concurrency/BookingConcurrencyHarness.cs' \
  'tests/EventBooking.Infrastructure.Tests/Concurrency/BookingConcurrencyTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/Concurrency/LockOrderTests.cs'
git diff --cached --name-only
git diff --cached
node scripts/check-ontology-terms.mjs
: "${EXECUTOR_COAUTHOR:?Set the executing harness co-author identity}"
git commit -m "feat(persistence): ordered row-lock helpers and concurrency harness" -m "Co-authored-by: $EXECUTOR_COAUTHOR"
git push -u origin HEAD
```

Go to Task 11, the relational-division eligibility query, which closes Phase 2 and opens its pull request.
