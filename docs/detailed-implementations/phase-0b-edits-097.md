# 00b — Vocabulary edits 97 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — tests/EventBooking.Infrastructure.Tests/DurableEmailDeliveryTests.cs — 1/1

<!-- vocabulary-file: {"id":335,"oldPath":"tests/EventBooking.Infrastructure.Tests/DurableEmailDeliveryTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/DurableEmailDeliveryTests.cs","beforeSha":"57fea72360599cfcfb502afbf885a5fa15b2fa6dcf69adc5d140d2e6011409b5","afterSha":"23d03264172c863ccc419c3b4a468a924a481d4c2efbc07eb58bfae74a10ed71","side":"before","part":1,"parts":1} -->

`````csharp
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
`````

## after — tests/EventBooking.Infrastructure.Tests/DurableEmailDeliveryTests.cs — 1/1

<!-- vocabulary-file: {"id":335,"oldPath":"tests/EventBooking.Infrastructure.Tests/DurableEmailDeliveryTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/DurableEmailDeliveryTests.cs","beforeSha":"57fea72360599cfcfb502afbf885a5fa15b2fa6dcf69adc5d140d2e6011409b5","afterSha":"23d03264172c863ccc419c3b4a468a924a481d4c2efbc07eb58bfae74a10ed71","side":"after","part":1,"parts":1} -->

`````csharp
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
        var delivery = service.StagePending(Guid.NewGuid(), EmailTemplate.AttendeeInvite);
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
        var attendeeId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();

        await using (var seed = fixture.NewContext())
        {
            seed.EmailLogs.Add(EmailLog.RecordPending(
                deliveryId,
                attendeeId,
                EmailTemplate.AttendeeInvite,
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
            Message(attendeeId),
            CancellationToken.None);
        await transport.SendStarted;

        var secondTask = second.DispatchAsync(
            deliveryId,
            Message(attendeeId),
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
        var attendeeId = Guid.NewGuid();
        var outstanding = EmailLog.RecordPending(
            Guid.NewGuid(), attendeeId, EmailTemplate.AttendeeInvite, Now);
        outstanding.MarkFailed(Now);
        var laterSent = EmailLog.Record(
            Guid.NewGuid(),
            attendeeId,
            EmailTemplate.EventCancelledRebookingNeeded,
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
                .LockLatestForAttendeeAsync(attendeeId, CancellationToken.None);
            Assert.Equal(outstanding.Id, selected?.Id);
            selected!.MarkResolved(Now.AddMinutes(2));
            await retry.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        await using var terminalRead = fixture.NewContext();
        await using var terminalTransaction = await terminalRead.Database.BeginTransactionAsync();
        var terminal = await new EmailDeliveryRepository(terminalRead)
            .LockLatestForAttendeeAsync(attendeeId, CancellationToken.None);
        Assert.Equal(laterSent.Id, terminal?.Id);
        await terminalTransaction.CommitAsync();
    }

    private static EmailMessage Message(Guid attendeeId) =>
        new(
            attendeeId,
            "attendee@example.com",
            "Attendee",
            EmailTemplate.AttendeeInvite,
            "Choose a time",
            "body",
            "<p>body</p>");

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;

        /// <summary>Gets the fixed instant; this clock treats UTC as transitional-location time.</summary>
        public DateTimeOffset NowAtTransitionalLocation => now;

        public DateOnly TodayAtTransitionalLocation => DateOnly.FromDateTime(now.UtcDateTime);

        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) =>
            DateOnly.FromDateTime(instant.UtcDateTime);

        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => instant.ToUniversalTime();
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
`````

## before — tests/EventBooking.Infrastructure.Tests/EfAuditLoggerTests.cs — 1/1

<!-- vocabulary-file: {"id":336,"oldPath":"tests/EventBooking.Infrastructure.Tests/EfAuditLoggerTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/EfAuditLoggerTests.cs","beforeSha":"a2a208b4f93d162bb240b866dfc02414108d3d9cc5ce71165c5b07eb9ae5c22a","afterSha":"84aec44104797b859cd68579e753e26014f69e49d80b8deefc2bcd74c6230676","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Audit;
using EventBooking.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class EfAuditLoggerTests(PostgresFixture fixture)
{
    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;

        /// <summary>Gets the fixed instant; this clock treats UTC as head-office time.</summary>
        public DateTimeOffset NowAtHeadOffice => now;

        public DateOnly TodayAtHeadOffice => DateAtHeadOffice(now);

        public DateOnly DateAtHeadOffice(DateTimeOffset instant) => DateOnly.FromDateTime(instant.UtcDateTime);

        public DateTimeOffset InstantAtHeadOffice(DateTimeOffset instant) => instant.ToUniversalTime();
    }

    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AStagedEntryIsWrittenWhenTheUnitOfWorkSaves()
    {
        await fixture.ResetAsync();
        var entityId = Guid.NewGuid();

        await using (var context = fixture.NewContext())
        {
            new EfAuditLogger(context, new FixedClock(Now)).Record(
                AuditEntityTypes.Booking, entityId, AuditAction.BookingCreated,
                ActorType.CandidateToken, "invite-1", "chose option 2");

            await context.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var entry = await read.AuditLogs.SingleAsync();
        Assert.Equal(AuditEntityTypes.Booking, entry.EntityType);
        Assert.Equal(entityId, entry.EntityId);
        Assert.Equal(AuditAction.BookingCreated, entry.Action);
        Assert.Equal(ActorType.CandidateToken, entry.ActorType);
        Assert.Equal("invite-1", entry.ActorId);
        Assert.Equal(Now, entry.Timestamp);
        Assert.Equal("chose option 2", entry.Details);
    }

    [Fact]
    public async Task AStagedEntryIsLostWhenTheTransactionRollsBack()
    {
        await fixture.ResetAsync();

        await using (var context = fixture.NewContext())
        {
            await using var transaction = await context.Database.BeginTransactionAsync();

            new EfAuditLogger(context, new FixedClock(Now)).Record(
                AuditEntityTypes.ConfirmedSlot, Guid.NewGuid(), AuditAction.SlotCancelled,
                ActorType.Staff, Guid.NewGuid().ToString());

            await context.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        await using var read = fixture.NewContext();
        Assert.Equal(0, await read.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task ASystemActorNeedsNoIdentifier()
    {
        await fixture.ResetAsync();

        await using (var context = fixture.NewContext())
        {
            new EfAuditLogger(context, new FixedClock(Now)).Record(
                AuditEntityTypes.Invite, Guid.NewGuid(), AuditAction.InviteExpired,
                ActorType.System, null);

            await context.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        Assert.Null((await read.AuditLogs.SingleAsync()).ActorId);
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/EfAuditLoggerTests.cs — 1/1

<!-- vocabulary-file: {"id":336,"oldPath":"tests/EventBooking.Infrastructure.Tests/EfAuditLoggerTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/EfAuditLoggerTests.cs","beforeSha":"a2a208b4f93d162bb240b866dfc02414108d3d9cc5ce71165c5b07eb9ae5c22a","afterSha":"84aec44104797b859cd68579e753e26014f69e49d80b8deefc2bcd74c6230676","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Audit;
using EventBooking.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class EfAuditLoggerTests(PostgresFixture fixture)
{
    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;

        /// <summary>Gets the fixed instant; this clock treats UTC as transitional-location time.</summary>
        public DateTimeOffset NowAtTransitionalLocation => now;

        public DateOnly TodayAtTransitionalLocation => DateAtTransitionalLocation(now);

        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) => DateOnly.FromDateTime(instant.UtcDateTime);

        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => instant.ToUniversalTime();
    }

    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AStagedEntryIsWrittenWhenTheUnitOfWorkSaves()
    {
        await fixture.ResetAsync();
        var entityId = Guid.NewGuid();

        await using (var context = fixture.NewContext())
        {
            new EfAuditLogger(context, new FixedClock(Now)).Record(
                AuditEntityTypes.Booking, entityId, AuditAction.BookingCreated,
                ActorType.AttendeeToken, "invite-1", "chose option 2");

            await context.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var entry = await read.AuditLogs.SingleAsync();
        Assert.Equal(AuditEntityTypes.Booking, entry.EntityType);
        Assert.Equal(entityId, entry.EntityId);
        Assert.Equal(AuditAction.BookingCreated, entry.Action);
        Assert.Equal(ActorType.AttendeeToken, entry.ActorType);
        Assert.Equal("invite-1", entry.ActorId);
        Assert.Equal(Now, entry.Timestamp);
        Assert.Equal("chose option 2", entry.Details);
    }

    [Fact]
    public async Task AStagedEntryIsLostWhenTheTransactionRollsBack()
    {
        await fixture.ResetAsync();

        await using (var context = fixture.NewContext())
        {
            await using var transaction = await context.Database.BeginTransactionAsync();

            new EfAuditLogger(context, new FixedClock(Now)).Record(
                AuditEntityTypes.Event, Guid.NewGuid(), AuditAction.EventCancelled,
                ActorType.Staff, Guid.NewGuid().ToString());

            await context.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        await using var read = fixture.NewContext();
        Assert.Equal(0, await read.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task ASystemActorNeedsNoIdentifier()
    {
        await fixture.ResetAsync();

        await using (var context = fixture.NewContext())
        {
            new EfAuditLogger(context, new FixedClock(Now)).Record(
                AuditEntityTypes.Invite, Guid.NewGuid(), AuditAction.InviteExpired,
                ActorType.System, null);

            await context.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        Assert.Null((await read.AuditLogs.SingleAsync()).ActorId);
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/EmployeeGroupPersistenceTests.cs — 1/1

<!-- vocabulary-file: {"id":337,"oldPath":"tests/EventBooking.Infrastructure.Tests/EmployeeGroupPersistenceTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/AttendeeGroupPersistenceTests.cs","beforeSha":"7b2469ea66b29c65e9b9294e6a0342dda710d59c1e73beeebcfd76a4c8b4a47b","afterSha":"8b16e90361820917657a38266d2674b77a8188708aaeaf37a28488d8b673d98a","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies deterministic Employee Group persistence and nullable Candidate rollout.</summary>
[Collection("postgres")]
public sealed class EmployeeGroupPersistenceTests(PostgresFixture fixture)
{
    /// <summary>The database contains exactly the approved five groups and ten mappings.</summary>
    [Fact]
    public async Task SeededGroupsMatchTheApprovedReferenceData()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();
        var repository = new EmployeeGroupRepository(context);

        var groups = await repository.ListActiveAsync(CancellationToken.None);

        Assert.Equal(5, groups.Count);
        Assert.Equal(10, groups.Sum(group => group.Requirements.Count));
        var pilots = Assert.Single(groups, group => group.Code == "PILOTS");
        Assert.Equal(
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            pilots.RequiredAppointmentTypeIds);
        var lowerCaseLookup = await repository.GetByCodeAsync(" pilots ", CancellationToken.None);
        Assert.Equal(EmployeeGroupIds.Pilots, lowerCaseLookup!.Id);
    }

    /// <summary>Release 2 requires the Employee Group on every Candidate row.</summary>
    [Fact]
    public async Task CandidateAssociationIsRequiredAfterReleaseTwo()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();
        var nullable = context.Model.FindEntityType("EventBooking.Domain.Candidates.Candidate")!
            .FindProperty("EmployeeGroupId")!.IsNullable;

        Assert.False(nullable);
        var relationship = context.Model.FindEntityType("EventBooking.Domain.Candidates.Candidate")!
            .GetForeignKeys()
            .Single(key => key.Properties.Single().Name == "EmployeeGroupId");
        Assert.Equal(DeleteBehavior.Restrict, relationship.DeleteBehavior);
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/AttendeeGroupPersistenceTests.cs — 1/1

<!-- vocabulary-file: {"id":337,"oldPath":"tests/EventBooking.Infrastructure.Tests/EmployeeGroupPersistenceTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/AttendeeGroupPersistenceTests.cs","beforeSha":"7b2469ea66b29c65e9b9294e6a0342dda710d59c1e73beeebcfd76a4c8b4a47b","afterSha":"8b16e90361820917657a38266d2674b77a8188708aaeaf37a28488d8b673d98a","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies deterministic Attendee Group persistence and nullable Attendee rollout.</summary>
[Collection("postgres")]
public sealed class AttendeeGroupPersistenceTests(PostgresFixture fixture)
{
    /// <summary>The database contains exactly the approved five groups and ten mappings.</summary>
    [Fact]
    public async Task SeededGroupsMatchTheApprovedReferenceData()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();
        var repository = new AttendeeGroupRepository(context);

        var groups = await repository.ListActiveAsync(CancellationToken.None);

        Assert.Equal(5, groups.Count);
        Assert.Equal(10, groups.Sum(group => group.Requirements.Count));
        var pilots = Assert.Single(groups, group => group.Code == "PILOTS");
        Assert.Equal(
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            pilots.RequiredAppointmentTypeIds);
        var lowerCaseLookup = await repository.GetByCodeAsync(" pilots ", CancellationToken.None);
        Assert.Equal(AttendeeGroupIds.Pilots, lowerCaseLookup!.Id);
    }

    /// <summary>Release 2 requires the Attendee Group on every Attendee row.</summary>
    [Fact]
    public async Task AttendeeAssociationIsRequiredAfterReleaseTwo()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();
        var nullable = context.Model.FindEntityType("EventBooking.Domain.Attendees.Attendee")!
            .FindProperty("AttendeeGroupId")!.IsNullable;

        Assert.False(nullable);
        var relationship = context.Model.FindEntityType("EventBooking.Domain.Attendees.Attendee")!
            .GetForeignKeys()
            .Single(key => key.Properties.Single().Name == "AttendeeGroupId");
        Assert.Equal(DeleteBehavior.Restrict, relationship.DeleteBehavior);
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/EmployeeGroupRequiredMigrationTests.cs — 1/1

<!-- vocabulary-file: {"id":338,"oldPath":"tests/EventBooking.Infrastructure.Tests/EmployeeGroupRequiredMigrationTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/AttendeeGroupRequiredMigrationTests.cs","beforeSha":"dc7b6d2ea600737da23b2a42b7ef777b245d78a4bdf8520387b2d892c0934db1","afterSha":"39c683f118d9428cd2e923a49310edf17d0ec27696ec7a7467ed54bf74757d98","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies Release 2 refuses incomplete reconciliation before changing nullability.</summary>
[Collection("postgres")]
public sealed class EmployeeGroupRequiredMigrationTests(PostgresFixture fixture)
{
    private const string ReleaseOneMigration = "20260909120000_AddRecoveryBookings";

    /// <summary>An unassigned row blocks migration; explicit matching assignment then succeeds.</summary>
    [Fact]
    public async Task GuardBlocksUnassignedCandidateBeforeAlterColumn()
    {
        var databaseName = $"eventbooking_group_required_{Guid.NewGuid():N}";
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Database = databaseName,
            Pooling = false,
        }.ConnectionString;
        try
        {
            await ExecuteAdminAsync(fixture.ConnectionString, $"CREATE DATABASE \"{databaseName}\"");
            await using (var releaseOne = NewContext(connectionString))
            {
                await releaseOne.Database.MigrateAsync(ReleaseOneMigration);
            }
            var candidateId = Guid.NewGuid();
            await ExecuteAsync(connectionString,
                """
                INSERT INTO candidate (id, name, email, status, status_changed_at, employee_group_id)
                VALUES (@id, 'Legacy', 'legacy@example.com', @status, now(), NULL);
                """,
                ("id", candidateId), ("status", (int)CandidateStatus.NotYetInvited));

            await using (var blocked = NewContext(connectionString))
            {
                var error = await Assert.ThrowsAnyAsync<Exception>(() => blocked.Database.MigrateAsync());
                Assert.Contains("Employee Group reconciliation", error.ToString());
            }
            Assert.True(await IsNullableAsync(connectionString));

            await ExecuteAsync(connectionString,
                """
                UPDATE candidate SET employee_group_id = @group_id WHERE id = @id;
                INSERT INTO candidate_requirement (candidate_id, appointment_type_id)
                SELECT @id, appointment_type_id
                FROM employee_group_requirement
                WHERE employee_group_id = @group_id;
                """,
                ("id", candidateId), ("group_id", EmployeeGroupIds.Pilots));
            await using (var reconciled = NewContext(connectionString))
            {
                await reconciled.Database.MigrateAsync();
            }

            Assert.False(await IsNullableAsync(connectionString));
        }
        finally
        {
            await ExecuteAdminAsync(
                fixture.ConnectionString,
                $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)");
        }
    }

    private static EventBookingDbContext NewContext(string connectionString) => new(
        new DbContextOptionsBuilder<EventBookingDbContext>().UseNpgsql(connectionString).Options);

    private static async Task ExecuteAdminAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteAsync(
        string connectionString,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> IsNullableAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT is_nullable = 'YES'
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'candidate'
              AND column_name = 'employee_group_id';
            """, connection);
        return (bool)(await command.ExecuteScalarAsync())!;
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/AttendeeGroupRequiredMigrationTests.cs — 1/1

<!-- vocabulary-file: {"id":338,"oldPath":"tests/EventBooking.Infrastructure.Tests/EmployeeGroupRequiredMigrationTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/AttendeeGroupRequiredMigrationTests.cs","beforeSha":"dc7b6d2ea600737da23b2a42b7ef777b245d78a4bdf8520387b2d892c0934db1","afterSha":"39c683f118d9428cd2e923a49310edf17d0ec27696ec7a7467ed54bf74757d98","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies Release 2 refuses incomplete reconciliation before changing nullability.</summary>
[Collection("postgres")]
public sealed class AttendeeGroupRequiredMigrationTests(PostgresFixture fixture)
{
    private const string ReleaseOneMigration = "20260909120000_AddRecoveryBookings";

    /// <summary>An unassigned row blocks migration; explicit matching assignment then succeeds.</summary>
    [Fact]
    public async Task GuardBlocksUnassignedAttendeeBeforeAlterColumn()
    {
        var databaseName = $"eventbooking_group_required_{Guid.NewGuid():N}";
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Database = databaseName,
            Pooling = false,
        }.ConnectionString;
        try
        {
            await ExecuteAdminAsync(fixture.ConnectionString, $"CREATE DATABASE \"{databaseName}\"");
            await using (var releaseOne = NewContext(connectionString))
            {
                await releaseOne.Database.MigrateAsync(ReleaseOneMigration);
            }
            var attendeeId = Guid.NewGuid();
            await ExecuteAsync(connectionString,
                """
                INSERT INTO attendee (id, name, email, status, status_changed_at, attendee_group_id)
                VALUES (@id, 'Legacy', 'legacy@example.com', @status, now(), NULL);
                """,
                ("id", attendeeId), ("status", (int)AttendeeStatus.NotYetInvited));

            await using (var blocked = NewContext(connectionString))
            {
                var error = await Assert.ThrowsAnyAsync<Exception>(() => blocked.Database.MigrateAsync());
                Assert.Contains("Attendee Group reconciliation", error.ToString());
            }
            Assert.True(await IsNullableAsync(connectionString));

            await ExecuteAsync(connectionString,
                """
                UPDATE attendee SET attendee_group_id = @group_id WHERE id = @id;
                INSERT INTO attendee_requirement (attendee_id, appointment_type_id)
                SELECT @id, appointment_type_id
                FROM attendee_group_requirement
                WHERE attendee_group_id = @group_id;
                """,
                ("id", attendeeId), ("group_id", AttendeeGroupIds.Pilots));
            await using (var reconciled = NewContext(connectionString))
            {
                await reconciled.Database.MigrateAsync();
            }

            Assert.False(await IsNullableAsync(connectionString));
        }
        finally
        {
            await ExecuteAdminAsync(
                fixture.ConnectionString,
                $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)");
        }
    }

    private static EventBookingDbContext NewContext(string connectionString) => new(
        new DbContextOptionsBuilder<EventBookingDbContext>().UseNpgsql(connectionString).Options);

    private static async Task ExecuteAdminAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteAsync(
        string connectionString,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> IsNullableAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT is_nullable = 'YES'
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'attendee'
              AND column_name = 'attendee_group_id';
            """, connection);
        return (bool)(await command.ExecuteScalarAsync())!;
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/InviteRequirementPersistenceTests.cs — 1/1

<!-- vocabulary-file: {"id":339,"oldPath":"tests/EventBooking.Infrastructure.Tests/InviteRequirementPersistenceTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/InviteRequirementPersistenceTests.cs","beforeSha":"d35b7e65a4bd14839e2927e5e7c0fc9abac9f4ebeb30a3bc228759310c03afcb","afterSha":"c671baabe3fa508ac17920d13b38a847559acca08a2d5aaf99f40dc561e7d684","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies Invite requirement snapshots round-trip with restrictive constraints.</summary>
[Collection("postgres")]
public sealed class InviteRequirementPersistenceTests(PostgresFixture fixture)
{
    /// <summary>An Invite reloads options, requirements, and recovery linkage together.</summary>
    [Fact]
    public async Task SnapshotRoundTrips()
    {
        await fixture.ResetAsync();
        var group = EmployeeGroup.Define(
            EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        var candidate = Candidate.Create(Guid.NewGuid(), "Amara", "amara@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidate.Id, "hash", DateTimeOffset.UtcNow.AddDays(1),
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()], candidate.RequiredAppointmentTypeIds, 0);

        await using (var write = fixture.NewContext())
        {
            write.Candidates.Add(candidate);
            write.Invites.Add(invite);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var actual = await read.Invites
            .Include(value => value.Options)
            .Include(value => value.Requirements)
            .SingleAsync(value => value.Id == invite.Id);
        Assert.Equal(3, actual.Options.Count);
        Assert.Equal(candidate.RequiredAppointmentTypeIds, actual.RequiredAppointmentTypeIds);
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/InviteRequirementPersistenceTests.cs — 1/1

<!-- vocabulary-file: {"id":339,"oldPath":"tests/EventBooking.Infrastructure.Tests/InviteRequirementPersistenceTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/InviteRequirementPersistenceTests.cs","beforeSha":"d35b7e65a4bd14839e2927e5e7c0fc9abac9f4ebeb30a3bc228759310c03afcb","afterSha":"c671baabe3fa508ac17920d13b38a847559acca08a2d5aaf99f40dc561e7d684","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies Invite requirement snapshots round-trip with restrictive constraints.</summary>
[Collection("postgres")]
public sealed class InviteRequirementPersistenceTests(PostgresFixture fixture)
{
    /// <summary>An Invite reloads options, requirements, and recovery linkage together.</summary>
    [Fact]
    public async Task SnapshotRoundTrips()
    {
        await fixture.ResetAsync();
        var group = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        var attendee = Attendee.Create(Guid.NewGuid(), "Amara", "amara@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, "hash", DateTimeOffset.UtcNow.AddDays(1),
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()], attendee.RequiredAppointmentTypeIds, 0);

        await using (var write = fixture.NewContext())
        {
            write.Attendees.Add(attendee);
            write.Invites.Add(invite);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var actual = await read.Invites
            .Include(value => value.Options)
            .Include(value => value.Requirements)
            .SingleAsync(value => value.Id == invite.Id);
        Assert.Equal(3, actual.Options.Count);
        Assert.Equal(attendee.RequiredAppointmentTypeIds, actual.RequiredAppointmentTypeIds);
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/LoggingEmailSenderTests.cs — 1/1

<!-- vocabulary-file: {"id":340,"oldPath":"tests/EventBooking.Infrastructure.Tests/LoggingEmailSenderTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/LoggingEmailSenderTests.cs","beforeSha":"881b1305b7bc47f000501cc7c0182de94d45c9f0ddb3b7bf49b8a78b2931db4b","afterSha":"3b6d4dd100f78b59aebb62cd444cb15b8e374bfb767f8833a0abb1d5f5e9e9de","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Notifications;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class LoggingEmailSenderTests(PostgresFixture fixture)
{
    [Theory]
    [InlineData("not-an-email", "EventBooking")]
    [InlineData("sender@example.com", " ")]
    public void SenderOptionsRejectIncompleteValues(string fromAddress, string fromName)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new EmailOptions(fromAddress, fromName, EmailProvider.Smtp));

        if (!string.IsNullOrWhiteSpace(fromAddress))
        {
            Assert.DoesNotContain(fromAddress, exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SenderOptionsDoNotExposeTheirValuesWhenFormatted()
    {
        var options = new EmailOptions("sender@example.com", "EventBooking", EmailProvider.Smtp);

        var formatted = options.ToString();

        Assert.DoesNotContain(options.FromAddress, formatted, StringComparison.Ordinal);
        Assert.DoesNotContain(options.FromName, formatted, StringComparison.Ordinal);
    }

    private sealed class FakeTransport : IEmailTransport
    {
        public List<EmailMessage> Sent { get; } = [];

        public bool Throw { get; set; }

        public bool Cancel { get; set; }

        public Action? AfterSend { get; set; }

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            if (Cancel)
            {
                return Task.FromCanceled(cancellationToken);
            }

            if (Throw)
            {
                throw new InvalidOperationException("the provider rejected the message");
            }

            Sent.Add(message);
            AfterSend?.Invoke();
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;

        /// <summary>Gets the fixed instant; this clock treats UTC as head-office time.</summary>
        public DateTimeOffset NowAtHeadOffice => now;

        public DateOnly TodayAtHeadOffice => DateAtHeadOffice(now);

        public DateOnly DateAtHeadOffice(DateTimeOffset instant) => DateOnly.FromDateTime(instant.UtcDateTime);

        public DateTimeOffset InstantAtHeadOffice(DateTimeOffset instant) => instant.ToUniversalTime();
    }

    [Fact]
    public async Task ASuccessfulSendIsLoggedAsSent()
    {
        var (sender, transport, candidateId) = await Given();

        var sent = await sender.SendAsync(MessageFor(candidateId), CancellationToken.None);

        Assert.True(sent);
        Assert.Single(transport.Sent);

        await using var context = fixture.NewContext();
        var log = await context.EmailLogs.SingleAsync();
        Assert.Equal(candidateId, log.CandidateId);
        Assert.Equal(EmailTemplate.CandidateInvite, log.TemplateName);
        Assert.Equal(EmailStatus.Sent, log.Status);
    }

    [Fact]
    public async Task AFailedSendReturnsFalseAndIsLoggedAsFailed()
    {
        var (sender, transport, candidateId) = await Given();
        transport.Throw = true;

        var sent = await sender.SendAsync(MessageFor(candidateId), CancellationToken.None);

        Assert.False(sent);
        Assert.Empty(transport.Sent);

        await using var context = fixture.NewContext();
        var log = await context.EmailLogs.SingleAsync();
        Assert.Equal(EmailStatus.Failed, log.Status);
    }

    [Fact]
    public async Task ThePublishedTimeComesFromTheClock()
    {
        var now = new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
        var (sender, _, candidateId) = await Given(now);

        await sender.SendAsync(MessageFor(candidateId), CancellationToken.None);

        await using var context = fixture.NewContext();
        Assert.Equal(now, (await context.EmailLogs.SingleAsync()).SentAt);
    }

    [Fact]
    public async Task ACancelledTransportPropagatesCancellationWithoutWritingAnEmailLog()
    {
        var (sender, transport, candidateId) = await Given();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        transport.Cancel = true;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sender.SendAsync(MessageFor(candidateId), cancellation.Token));

        await using var context = fixture.NewContext();
        Assert.Empty(await context.EmailLogs.ToListAsync());
    }

    [Fact]
    public async Task ACancelledAuditSavePropagatesCancellationWithoutWritingAnEmailLog()
    {
        var (sender, transport, candidateId) = await Given();
        using var cancellation = new CancellationTokenSource();
        transport.AfterSend = cancellation.Cancel;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sender.SendAsync(MessageFor(candidateId), cancellation.Token));

        await using var context = fixture.NewContext();
        Assert.Empty(await context.EmailLogs.ToListAsync());
    }

    [Fact]
    public async Task AnAuditContextFailureIsWarnedAndDoesNotFailTheSend()
    {
        var (_, _, candidateId) = await Given();
        var logger = new RecordingLogger<LoggingEmailSender>();
        var sender = new LoggingEmailSender(
            new FakeTransport(),
            new ThrowingContextFactory(),
            new FixedClock(DateTimeOffset.UtcNow),
            logger);

        var sent = await sender.SendAsync(MessageFor(candidateId), CancellationToken.None);

        Assert.True(sent);
        Assert.Contains(LogLevel.Warning, logger.LogLevels);

        await using var context = fixture.NewContext();
        Assert.Empty(await context.EmailLogs.ToListAsync());
    }

    private async Task<(LoggingEmailSender Sender, FakeTransport Transport, Guid CandidateId)> Given(
        DateTimeOffset? now = null)
    {
        await fixture.ResetAsync();

        var candidateId = Guid.NewGuid();
        await using (var write = fixture.NewContext())
        {
            var pilots = write.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.Pilots);
            var candidate = Candidate.Create(
                candidateId, "Amara Novak", "a.novak@mail.com", pilots);
            write.Candidates.Add(candidate);
            await write.SaveChangesAsync();
        }

        var transport = new FakeTransport();
        var sender = new LoggingEmailSender(
            transport,
            new TestContextFactory(fixture),
            new FixedClock(now ?? DateTimeOffset.UtcNow),
            NullLogger<LoggingEmailSender>.Instance);

        return (sender, transport, candidateId);
    }

    private static EmailMessage MessageFor(Guid candidateId) =>
        new(candidateId, "a.novak@mail.com", "Amara Novak", EmailTemplate.CandidateInvite,
            "Choose a time", "text", "<html></html>");

    private sealed class TestContextFactory(PostgresFixture fixture)
        : IDbContextFactory<EventBookingDbContext>
    {
        public EventBookingDbContext CreateDbContext() => fixture.NewContext();
    }

    private sealed class ThrowingContextFactory : IDbContextFactory<EventBookingDbContext>
    {
        public EventBookingDbContext CreateDbContext() =>
            throw new InvalidOperationException("the audit database is unavailable");
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogLevel> LogLevels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LogLevels.Add(logLevel);
        }
    }
}
`````
