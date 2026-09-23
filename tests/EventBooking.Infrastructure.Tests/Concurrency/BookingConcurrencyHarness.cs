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
/// The two races that stay below the Application layer: the capacities-only shuffle, which the
/// real handler cannot drive because it always takes the event lock first, and the headcount
/// adjustment racing real bookings. Booking attempts themselves go through the real
/// ConfirmBookingHandler via <see cref="ConcurrencyHarness"/>.
/// </summary>
/// <param name="fixture">The PostgreSQL fixture whose container every connection uses.</param>
public sealed class BookingConcurrencyHarness(PostgresFixture fixture)
{
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
