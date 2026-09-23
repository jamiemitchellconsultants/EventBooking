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
