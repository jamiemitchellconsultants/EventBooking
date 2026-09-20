# 02c — Ordered row locks, and the harness that proves they do not deadlock, edits 2 (Task 10)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — src/EventBooking.Infrastructure/Persistence/UnitOfWork.cs — 1/1

<!-- retirement-file: {"id":7,"file":"src/EventBooking.Infrastructure/Persistence/UnitOfWork.cs","beforeSha":"23646c1353020c981c30a23454f394e92ab28b7341b107d538df2402cd788f26","afterSha":"1d3f5f196d822946d41fe1f613270d1002b61430b1720541846e123c7ac68f76","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace EventBooking.Infrastructure.Persistence;

/// <summary>Commits one DbContext unit of work and maps uniqueness backstops to application errors.</summary>
public sealed class UnitOfWork(EventBookingDbContext context) : IUnitOfWork
{
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

        var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        return new EfTransactionScope(transaction);
    }

    private sealed class EfTransactionScope(IDbContextTransaction transaction) : ITransactionScope
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken) =>
            transaction.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }

    private sealed class JoinedScope : ITransactionScope
    {
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RollbackAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
`````

## after — src/EventBooking.Infrastructure/Persistence/UnitOfWork.cs — 1/1

<!-- retirement-file: {"id":7,"file":"src/EventBooking.Infrastructure/Persistence/UnitOfWork.cs","beforeSha":"23646c1353020c981c30a23454f394e92ab28b7341b107d538df2402cd788f26","afterSha":"1d3f5f196d822946d41fe1f613270d1002b61430b1720541846e123c7ac68f76","side":"after","part":1,"parts":1} -->

`````csharp
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
`````

## after — tests/EventBooking.Infrastructure.Tests/Concurrency/BookingConcurrencyHarness.cs — 1/1

<!-- retirement-file: {"id":8,"file":"tests/EventBooking.Infrastructure.Tests/Concurrency/BookingConcurrencyHarness.cs","beforeSha":null,"afterSha":"c9ac63749a925a880a813c37a2e9fec0e02cbb6701c02d32c73c62b73a013b20","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Locking;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventBooking.Infrastructure.Tests.Concurrency;

/// <summary>What one attempt did.</summary>
public enum ConcurrentOutcome
{
    /// <summary>Every required row had a place and the transaction committed.</summary>
    Booked,

    /// <summary>A required type had nothing left. Nothing moved.</summary>
    CapacityExhausted,

    /// <summary>A headcount adjustment the rows would not accept. Nothing moved.</summary>
    AdjustmentRefused,

    /// <summary>PostgreSQL chose this transaction as a deadlock victim. The bug this suite hunts.</summary>
    Deadlocked,
}

/// <summary>One attempt's result.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="ExhaustedTypeId">The type that had nothing left, when one did.</param>
/// <param name="MinimumAccepted">The minimum a refused adjustment would have accepted.</param>
public sealed record ConcurrentAttempt(
    ConcurrentOutcome Outcome,
    Guid? ExhaustedTypeId = null,
    int? MinimumAccepted = null);

/// <summary>
/// Drives the lock helpers from many connections at once. It deliberately does **not** go through
/// an Application handler: none exists until Task 15, and the point here is to prove the ordering
/// the helpers impose, not the booking rules on top of it. Task 15 replaces this routine with the
/// real handler and keeps these scenarios.
/// </summary>
/// <param name="fixture">The PostgreSQL fixture whose container every connection uses.</param>
public sealed class BookingConcurrencyHarness(PostgresFixture fixture)
{
    /// <summary>
    /// Locks the event, then its capacity rows for the required types in the domain's order, and
    /// charges them all or nothing. Every attempt gets its own context, connection and transaction,
    /// which is what makes the lock order observable at all.
    /// </summary>
    /// <param name="eventIds">The events to charge, named in whatever order the caller received.</param>
    /// <param name="appointmentTypeIds">The types each event must charge.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<ConcurrentAttempt> TryChargeAsync(
        IReadOnlyList<Guid> eventIds,
        IReadOnlyList<Guid> appointmentTypeIds,
        CancellationToken cancellationToken)
    {
        await using var context = NewContext();
        var locks = new TransactionLocks(enforced: true);
        var rowLocks = new RowLocks(context, locks);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await rowLocks.LockEventsAsync(eventIds, cancellationToken);

            var keys = eventIds
                .SelectMany(eventId => appointmentTypeIds
                    .Select(typeId => new EventCapacityKey(eventId, typeId)));
            var rows = await rowLocks.LockCapacitiesAsync(keys, cancellationToken);

            if (rows.FirstOrDefault(row => !row.HasSpare) is { } exhausted)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new ConcurrentAttempt(
                    ConcurrentOutcome.CapacityExhausted, exhausted.AppointmentTypeId);
            }

            foreach (var row in rows)
            {
                row.Decrement();
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ConcurrentAttempt(ConcurrentOutcome.Booked);
        }
        catch (Exception exception) when (IsDeadlock(exception))
        {
            await SafeRollbackAsync(transaction, cancellationToken);
            return new ConcurrentAttempt(ConcurrentOutcome.Deadlocked);
        }
    }

    /// <summary>
    /// Charges capacity rows across several events **without** taking the event locks first, so
    /// the capacity ordering is the only thing between two attempts and a deadlock. The event lock
    /// would otherwise serialise them before they ever reached a capacity row, which is correct in
    /// production and useless for proving the row order.
    /// </summary>
    /// <param name="eventIds">The events to charge, in the order the caller received them.</param>
    /// <param name="appointmentTypeIds">The types each event must charge.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<ConcurrentAttempt> TryChargeCapacitiesOnlyAsync(
        IReadOnlyList<Guid> eventIds,
        IReadOnlyList<Guid> appointmentTypeIds,
        CancellationToken cancellationToken)
    {
        await using var context = NewContext();
        var rowLocks = new RowLocks(context, new TransactionLocks(enforced: true));

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var keys = eventIds
                .SelectMany(eventId => appointmentTypeIds
                    .Select(typeId => new EventCapacityKey(eventId, typeId)));
            var rows = await rowLocks.LockCapacitiesAsync(keys, cancellationToken);

            if (rows.FirstOrDefault(row => !row.HasSpare) is { } exhausted)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new ConcurrentAttempt(
                    ConcurrentOutcome.CapacityExhausted, exhausted.AppointmentTypeId);
            }

            foreach (var row in rows)
            {
                row.Decrement();
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ConcurrentAttempt(ConcurrentOutcome.Booked);
        }
        catch (Exception exception) when (IsDeadlock(exception))
        {
            await SafeRollbackAsync(transaction, cancellationToken);
            return new ConcurrentAttempt(ConcurrentOutcome.Deadlocked);
        }
    }

    /// <summary>
    /// Adjusts one capacity row's total under the same lock order a booking takes, so the two race
    /// on the row rather than on a read the adjustment took earlier.
    /// </summary>
    /// <param name="eventId">The event id.</param>
    /// <param name="appointmentTypeId">The type whose row is adjusted.</param>
    /// <param name="totalHeadcount">The new total.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<ConcurrentAttempt> TryAdjustAsync(
        Guid eventId,
        Guid appointmentTypeId,
        int totalHeadcount,
        CancellationToken cancellationToken)
    {
        await using var context = NewContext();
        var locks = new TransactionLocks(enforced: true);
        var rowLocks = new RowLocks(context, locks);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await rowLocks.LockEventsAsync([eventId], cancellationToken);
            var row = (await rowLocks.LockCapacitiesAsync(
                eventId, [appointmentTypeId], cancellationToken)).Single();

            // Under lock, so the count cannot move between reading it and deciding on it.
            var adjustment = row.AdjustTotalHeadcount(totalHeadcount, row.OccupiedCapacity);
            if (adjustment.Status == CapacityAdjustmentStatus.BelowActiveBookings)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new ConcurrentAttempt(
                    ConcurrentOutcome.AdjustmentRefused,
                    MinimumAccepted: adjustment.MinimumTotalHeadcount);
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ConcurrentAttempt(ConcurrentOutcome.Booked);
        }
        catch (Exception exception) when (IsDeadlock(exception))
        {
            await SafeRollbackAsync(transaction, cancellationToken);
            return new ConcurrentAttempt(ConcurrentOutcome.Deadlocked);
        }
    }

    /// <summary>Reads every capacity row of an event, for the assertions after a race.</summary>
    /// <param name="eventId">The event id.</param>
    public async Task<IReadOnlyList<EventCapacity>> CapacitiesAsync(Guid eventId)
    {
        await using var context = NewContext();
        return await context.EventCapacities
            .AsNoTracking()
            .Where(row => row.EventId == eventId)
            .OrderBy(row => row.AppointmentTypeId)
            .ToListAsync();
    }

    private EventBookingDbContext NewContext() =>
        new(new DbContextOptionsBuilder<EventBookingDbContext>()
            .UseNpgsql(new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
            {
                // Each attempt gets a real backend of its own, not a pooled one another attempt
                // may still be holding a transaction open on.
                Pooling = false,
            }.ConnectionString)
            .Options);

    /// <summary>SQLSTATE 40P01. The one outcome none of these scenarios may produce.</summary>
    private static bool IsDeadlock(Exception exception) =>
        exception is PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected }
            || exception.InnerException is PostgresException
                { SqlState: PostgresErrorCodes.DeadlockDetected };

    private static async Task SafeRollbackAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        catch (Exception)
        {
            // The deadlock already aborted this transaction; the rollback is a formality.
        }
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/Concurrency/BookingConcurrencyTests.cs — 1/1

<!-- retirement-file: {"id":9,"file":"tests/EventBooking.Infrastructure.Tests/Concurrency/BookingConcurrencyTests.cs","beforeSha":null,"afterSha":"130a7efa67a9da8fdc80b361b553790ad59031a10f3b77aef0eb4e4348bce9f0","side":"after","part":1,"parts":1} -->

`````csharp
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
`````

## after — tests/EventBooking.Infrastructure.Tests/Concurrency/LockOrderTests.cs — 1/1

<!-- retirement-file: {"id":10,"file":"tests/EventBooking.Infrastructure.Tests/Concurrency/LockOrderTests.cs","beforeSha":null,"afterSha":"42acd925e52afa9feaa6471e22d6ea33ac3248090cec059f205e1a17efaf3d17","side":"after","part":1,"parts":1} -->

`````csharp
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
`````
