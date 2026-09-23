using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Notifications;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies durable delivery rollback and PostgreSQL row-claim serialization.</summary>
[Collection("postgres")]
public class DurableEmailDeliveryTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);

    /// <summary>A business commit failure rolls back the staged delivery and never calls transport.</summary>
    [Fact]
    public async Task ACommitFailureAfterStagingLeavesNoDurableDeliveryAndCallsNoTransport()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();
        var transport = new RecordingSender();
        var unitOfWork = new FailingCommitUnitOfWork(context);
        var service = new EmailDeliveryService(
            new EmailDeliveryRepository(context), transport, unitOfWork, new FixedClock(Now),
            NullLogger<EmailDeliveryService>.Instance);

        await using var transaction = await unitOfWork.BeginTransactionAsync(CancellationToken.None);
        var delivery = service.StagePending(Guid.NewGuid(), EmailTemplate.CandidateInvite);
        service.ClaimForDispatch(delivery);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => transaction.CommitAsync(CancellationToken.None));
        await transaction.RollbackAsync(CancellationToken.None);

        await using var read = fixture.NewContext();
        Assert.Empty(await read.EmailLogs.ToListAsync());
        Assert.Empty(transport.Messages);
    }

    /// <summary>Only the worker holding the committed claim is allowed to call transport.</summary>
    [Fact]
    public async Task ConcurrentDispatchesSendOneMessageAndPersistOneSentOutcome()
    {
        await fixture.ResetAsync();
        var candidateId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();

        await using (var seed = fixture.NewContext())
        {
            seed.EmailLogs.Add(EmailLog.RecordPending(
                deliveryId,
                candidateId,
                EmailTemplate.CandidateInvite,
                Now));
            await seed.SaveChangesAsync();
        }

        await using var firstContext = fixture.NewContext();
        await using var secondContext = fixture.NewContext();
        var transport = new BlockingSender();
        var first = new EmailDeliveryService(
            new EmailDeliveryRepository(firstContext),
            transport,
            new UnitOfWork(firstContext),
            new FixedClock(Now),
            NullLogger<EmailDeliveryService>.Instance);
        var second = new EmailDeliveryService(
            new EmailDeliveryRepository(secondContext),
            transport,
            new UnitOfWork(secondContext),
            new FixedClock(Now),
            NullLogger<EmailDeliveryService>.Instance);

        var firstTask = first.DispatchAsync(
            deliveryId,
            Message(candidateId),
            CancellationToken.None);
        await transport.SendStarted;

        var secondTask = second.DispatchAsync(
            deliveryId,
            Message(candidateId),
            CancellationToken.None);

        Assert.Equal(EmailStatus.Pending, await secondTask);
        transport.Release();
        Assert.Equal(EmailStatus.Sent, await firstTask);

        await using var read = fixture.NewContext();
        var persisted = await read.EmailLogs.SingleAsync();
        Assert.Equal(EmailStatus.Sent, persisted.Status);
        Assert.Equal(1, transport.SendCount);
    }

    /// <summary>An unresolved attempt remains lockable until a retry resolves it.</summary>
    [Fact]
    public async Task RetryLockPrioritizesOutstandingDeliveryOverLaterTerminalHistory()
    {
        await fixture.ResetAsync();
        var candidateId = Guid.NewGuid();
        var outstanding = EmailLog.RecordPending(
            Guid.NewGuid(), candidateId, EmailTemplate.CandidateInvite, Now);
        outstanding.MarkFailed(Now);
        var laterSent = EmailLog.Record(
            Guid.NewGuid(),
            candidateId,
            EmailTemplate.SlotCancelledRebookingNeeded,
            Now.AddMinutes(3),
            EmailStatus.Sent);

        await using (var seed = fixture.NewContext())
        {
            seed.EmailLogs.AddRange(outstanding, laterSent);
            await seed.SaveChangesAsync();
        }

        await using (var retry = fixture.NewContext())
        await using (var transaction = await retry.Database.BeginTransactionAsync())
        {
            var selected = await new EmailDeliveryRepository(retry)
                .LockLatestForCandidateAsync(candidateId, CancellationToken.None);
            Assert.Equal(outstanding.Id, selected?.Id);
            selected!.MarkResolved(Now.AddMinutes(2));
            await retry.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        await using var terminalRead = fixture.NewContext();
        await using var terminalTransaction = await terminalRead.Database.BeginTransactionAsync();
        var terminal = await new EmailDeliveryRepository(terminalRead)
            .LockLatestForCandidateAsync(candidateId, CancellationToken.None);
        Assert.Equal(laterSent.Id, terminal?.Id);
        await terminalTransaction.CommitAsync();
    }

    private static EmailMessage Message(Guid candidateId) =>
        new(
            candidateId,
            "candidate@example.com",
            "Candidate",
            EmailTemplate.CandidateInvite,
            "Choose a time",
            "body",
            "<p>body</p>");

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;

        /// <summary>Gets the fixed instant; this clock treats UTC as head-office time.</summary>
        public DateTimeOffset NowAtHeadOffice => now;

        public DateOnly TodayAtHeadOffice => DateOnly.FromDateTime(now.UtcDateTime);

        public DateOnly DateAtHeadOffice(DateTimeOffset instant) =>
            DateOnly.FromDateTime(instant.UtcDateTime);

        public DateTimeOffset InstantAtHeadOffice(DateTimeOffset instant) => instant.ToUniversalTime();
    }

    private sealed class RecordingSender : IEmailSender
    {
        public List<EmailMessage> Messages { get; } = [];

        public Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.FromResult(true);
        }
    }

    private sealed class BlockingSender : IEmailSender
    {
        private readonly TaskCompletionSource<bool> _sendStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _sendCount;

        public Task SendStarted => _sendStarted.Task;

        public int SendCount => Volatile.Read(ref _sendCount);

        public Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _sendCount);
            _sendStarted.TrySetResult(true);
            return WaitForReleaseAsync(cancellationToken);
        }

        public void Release() => _release.TrySetResult(true);

        private async Task<bool> WaitForReleaseAsync(CancellationToken cancellationToken)
        {
            await _release.Task.WaitAsync(cancellationToken);
            return true;
        }
    }

    private sealed class FailingCommitUnitOfWork(EventBookingDbContext context) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
            context.SaveChangesAsync(cancellationToken);

        public async Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken)
        {
            var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            return new Scope(transaction);
        }

        private sealed class Scope(IDbContextTransaction transaction) : ITransactionScope
        {
            public Task CommitAsync(CancellationToken cancellationToken) =>
                Task.FromException(new InvalidOperationException("simulated commit failure"));

            public Task RollbackAsync(CancellationToken cancellationToken) =>
                transaction.RollbackAsync(cancellationToken);

            public ValueTask DisposeAsync() => transaction.DisposeAsync();
        }
    }
}
