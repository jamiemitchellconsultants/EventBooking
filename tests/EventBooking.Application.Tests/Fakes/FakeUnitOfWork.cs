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
public sealed class TransactionalSlotLockCoordinator
{
    private readonly ConcurrentDictionary<Guid, SlotLock> _locks = new();
    private readonly AsyncLocal<TransactionSession?> _currentSession = new();

    public TransactionSession BeginTransaction()
    {
        var session = new TransactionSession(this, _currentSession.Value);
        _currentSession.Value = session;
        return session;
    }

    public async Task AcquireAsync(Guid confirmedSlotId, CancellationToken cancellationToken)
    {
        var session = _currentSession.Value
            ?? throw new InvalidOperationException("A slot guard requires an active transaction.");
        var slotLock = _locks.GetOrAdd(confirmedSlotId, _ => new SlotLock());

        if (!slotLock.Gate.Wait(0))
        {
            slotLock.Waiting.TrySetResult(true);
            await slotLock.Gate.WaitAsync(cancellationToken);
        }

        session.Hold(slotLock);
        slotLock.Held.TrySetResult(true);
    }

    public Task WaitUntilHeldAsync(Guid confirmedSlotId) =>
        _locks.GetOrAdd(confirmedSlotId, _ => new SlotLock()).Held.Task;

    public Task WaitUntilWaitingAsync(Guid confirmedSlotId) =>
        _locks.GetOrAdd(confirmedSlotId, _ => new SlotLock()).Waiting.Task;

    public sealed class TransactionSession
    {
        private readonly TransactionalSlotLockCoordinator _owner;
        private readonly TransactionSession? _previous;
        private readonly List<SlotLock> _held = [];
        private bool _released;

        internal TransactionSession(
            TransactionalSlotLockCoordinator owner,
            TransactionSession? previous)
        {
            _owner = owner;
            _previous = previous;
        }

        internal void Hold(SlotLock slotLock) => _held.Add(slotLock);

        public void Release()
        {
            if (_released)
            {
                return;
            }

            _released = true;

            foreach (var slotLock in _held)
            {
                slotLock.Gate.Release();
            }

            if (_owner._currentSession.Value == this)
            {
                _owner._currentSession.Value = _previous;
            }
        }
    }

    internal sealed class SlotLock
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public TaskCompletionSource<bool> Held { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Waiting { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

public sealed class FakeUnitOfWork(
    TransactionOperationLog? operations = null,
    TransactionalSlotLockCoordinator? locks = null) : IUnitOfWork
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
        TransactionalSlotLockCoordinator.TransactionSession? lockSession) : ITransactionScope
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
