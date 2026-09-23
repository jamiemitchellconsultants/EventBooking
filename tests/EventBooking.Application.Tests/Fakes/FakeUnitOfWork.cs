using EventBooking.Application.Abstractions;
using System.Collections.Concurrent;

namespace EventBooking.Application.Tests.Fakes;

public sealed class TransactionOperationLog
{
    public List<string> Events { get; } = [];

    public void Record(string operation) => Events.Add(operation);
}

/// <summary>
/// Models database row-lock ownership for application interleaving tests. A lock acquired by a
/// repository remains unavailable until its owning fake transaction commits, rolls back, or is
/// disposed.
/// </summary>
public sealed class TransactionalEventLockCoordinator
{
    private readonly ConcurrentDictionary<Guid, EventLock> _locks = new();
    private readonly AsyncLocal<TransactionSession?> _currentSession = new();

    public TransactionSession BeginTransaction()
    {
        var session = new TransactionSession(this, _currentSession.Value);
        _currentSession.Value = session;
        return session;
    }

    public async Task AcquireAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var session = _currentSession.Value
            ?? throw new InvalidOperationException("A event guard requires an active transaction.");
        var eventLock = _locks.GetOrAdd(eventId, _ => new EventLock());

        if (!eventLock.Gate.Wait(0))
        {
            eventLock.Waiting.TrySetResult(true);
            await eventLock.Gate.WaitAsync(cancellationToken);
        }

        session.Hold(eventLock);
        eventLock.Held.TrySetResult(true);
    }

    public Task WaitUntilHeldAsync(Guid eventId) =>
        _locks.GetOrAdd(eventId, _ => new EventLock()).Held.Task;

    public Task WaitUntilWaitingAsync(Guid eventId) =>
        _locks.GetOrAdd(eventId, _ => new EventLock()).Waiting.Task;

    public sealed class TransactionSession
    {
        private readonly TransactionalEventLockCoordinator _owner;
        private readonly TransactionSession? _previous;
        private readonly List<EventLock> _held = [];
        private bool _released;

        internal TransactionSession(
            TransactionalEventLockCoordinator owner,
            TransactionSession? previous)
        {
            _owner = owner;
            _previous = previous;
        }

        internal void Hold(EventLock eventLock) => _held.Add(eventLock);

        public void Release()
        {
            if (_released)
            {
                return;
            }

            _released = true;

            foreach (var eventLock in _held)
            {
                eventLock.Gate.Release();
            }

            if (_owner._currentSession.Value == this)
            {
                _owner._currentSession.Value = _previous;
            }
        }
    }

    internal sealed class EventLock
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public TaskCompletionSource<bool> Held { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Waiting { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

public sealed class FakeUnitOfWork(
    TransactionOperationLog? operations = null,
    TransactionalEventLockCoordinator? locks = null) : IUnitOfWork
{
    /// <summary>When true, the next commit throws after the transaction has been staged.</summary>
    public bool ThrowOnCommit { get; set; }

    public int SaveCount { get; private set; }

    public int CommitCount { get; private set; }

    public int RollbackCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.FromResult(0);
    }

    public Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        operations?.Record("transaction-begun");
        return Task.FromResult<ITransactionScope>(new Scope(this, locks?.BeginTransaction()));
    }

    private sealed class Scope(
        FakeUnitOfWork owner,
        TransactionalEventLockCoordinator.TransactionSession? lockSession) : ITransactionScope
    {
        public Task CommitAsync(CancellationToken cancellationToken)
        {
            owner.CommitCount++;
            if (owner.ThrowOnCommit)
            {
                owner.ThrowOnCommit = false;
                throw new InvalidOperationException("simulated commit failure");
            }

            lockSession?.Release();
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken)
        {
            owner.RollbackCount++;
            lockSession?.Release();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            lockSession?.Release();
            return ValueTask.CompletedTask;
        }
    }
}
