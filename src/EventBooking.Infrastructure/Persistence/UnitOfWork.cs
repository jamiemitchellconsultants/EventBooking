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
        return new EfTransactionScope(transaction, locks, context);
    }

    private sealed class EfTransactionScope(
        IDbContextTransaction transaction, TransactionLocks locks, EventBookingDbContext context)
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

            // Everything read or changed in the rolled-back transaction is void. Left tracked,
            // it would be served back as current by a retry on this scoped context, and any
            // in-memory change would be saved by the next SaveChanges.
            context.ChangeTracker.Clear();
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
