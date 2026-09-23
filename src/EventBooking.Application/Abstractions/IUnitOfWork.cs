namespace EventBooking.Application.Abstractions;

/// <summary>Defines itransaction scope for the current use case.</summary>
public interface ITransactionScope : IAsyncDisposable
{
    /// <summary>Provides commit async within this contract.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task CommitAsync(CancellationToken cancellationToken);

    /// <summary>Provides rollback async within this contract.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task RollbackAsync(CancellationToken cancellationToken);
}

/// <summary>Defines iunit of work for the current use case.</summary>
public interface IUnitOfWork
{
    /// <summary>Provides save changes async within this contract.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Opens an explicit transaction. Only the use cases that touch capacity need one; everything
    /// else relies on the implicit transaction around a single save.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken);
}
