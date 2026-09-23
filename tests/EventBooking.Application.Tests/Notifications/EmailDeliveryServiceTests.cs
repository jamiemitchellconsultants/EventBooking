using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace EventBooking.Application.Tests.Notifications;

/// <summary>Verifies claim, provider, and durable outcome behavior for pending deliveries.</summary>
public class EmailDeliveryServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeClock _clock = new(Now);
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly InMemoryEmailDeliveryRepository _deliveries = new();
    private readonly RecordingEmailSender _sender = new();

    /// <summary>A successful provider attempt transitions a pending row to Sent.</summary>
    [Fact]
    public async Task SuccessfulDispatchMarksTheDeliverySent()
    {
        var service = NewService();
        var staged = service.StagePending(Guid.NewGuid(), EmailTemplate.AttendeeInvite);

        var result = await service.DispatchAsync(staged.Id, Message(staged.AttendeeId), CancellationToken.None);

        Assert.Equal(EmailStatus.Sent, result);
        Assert.Equal(EmailStatus.Sent, staged.Status);
        Assert.Single(_sender.Sent);
    }

    /// <summary>A provider rejection is durable and leaves the business state untouched.</summary>
    [Fact]
    public async Task ProviderFailureMarksTheDeliveryFailed()
    {
        _sender.FailNextSend = true;
        var service = NewService();
        var staged = service.StagePending(Guid.NewGuid(), EmailTemplate.AttendeeInvite);

        var result = await service.DispatchAsync(staged.Id, Message(staged.AttendeeId), CancellationToken.None);

        Assert.Equal(EmailStatus.Failed, result);
        Assert.Equal(EmailStatus.Failed, staged.Status);
        Assert.Empty(_sender.Sent);
    }

    /// <summary>A failed claim commit prevents any provider call and retains Pending.</summary>
    [Fact]
    public async Task AFailedClaimCommitDoesNotCallTheTransport()
    {
        _unitOfWork.ThrowOnCommit = true;
        var service = NewService();
        var staged = service.StagePending(Guid.NewGuid(), EmailTemplate.AttendeeInvite);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DispatchAsync(
            staged.Id, Message(staged.AttendeeId), CancellationToken.None));

        Assert.Empty(_sender.Sent);
        Assert.Equal(EmailStatus.Pending, staged.Status);
        Assert.Equal(1, _unitOfWork.RollbackCount);
    }

    /// <summary>A fresh claim held by another worker returns Pending without a duplicate send.</summary>
    [Fact]
    public async Task AFreshClaimDoesNotSendTheSameDeliveryTwice()
    {
        var service = NewService();
        var staged = service.StagePending(Guid.NewGuid(), EmailTemplate.AttendeeInvite);
        Assert.True(staged.TryClaim(Now, TimeSpan.FromMinutes(5)));

        var result = await service.DispatchAsync(staged.Id, Message(staged.AttendeeId), CancellationToken.None);

        Assert.Equal(EmailStatus.Pending, result);
        Assert.Empty(_sender.Sent);
    }

    /// <summary>A resolved attempt is terminal and cannot be dispatched again.</summary>
    [Fact]
    public async Task AResolvedDeliveryDoesNotCallTheTransport()
    {
        var service = NewService();
        var staged = service.StagePending(Guid.NewGuid(), EmailTemplate.AttendeeInvite);
        staged.MarkResolved(Now);

        var result = await service.DispatchAsync(
            staged.Id, Message(staged.AttendeeId), CancellationToken.None);

        Assert.Equal(EmailStatus.Resolved, result);
        Assert.Empty(_sender.Sent);
    }

    /// <summary>Staging order remains the latest-delivery order after claims share one clock tick.</summary>
    [Fact]
    public void ClaimsDoNotMakeSameTickDeliveriesAmbiguous()
    {
        var attendeeId = Guid.NewGuid();
        var service = NewService();
        var first = service.StagePending(attendeeId, EmailTemplate.AttendeeInvite);
        service.ClaimForDispatch(first);
        var second = service.StagePending(attendeeId, EmailTemplate.EventCancelledRebookingNeeded);
        service.ClaimForDispatch(second);

        Assert.Equal(second.Id, _deliveries.Items
            .Where(delivery => delivery.AttendeeId == attendeeId)
            .OrderByDescending(delivery => delivery.SentAt)
            .ThenByDescending(delivery => delivery.Id)
            .First()
            .Id);
    }

    /// <summary>A failed post-send audit callback is logged while the sent outcome remains durable.</summary>
    [Fact]
    public async Task AuditCallbackFailureIsLoggedWithoutReopeningTheDelivery()
    {
        var logger = new RecordingLogger<EmailDeliveryService>();
        var service = EmailDeliveryTestFactory.Create(
            _deliveries, _sender, _unitOfWork, _clock, logger);
        var staged = service.StagePending(Guid.NewGuid(), EmailTemplate.AttendeeInvite);

        var result = await service.DispatchAsync(
            staged.Id,
            Message(staged.AttendeeId),
            CancellationToken.None,
            () => throw new InvalidOperationException("audit unavailable"));

        Assert.Equal(EmailStatus.Sent, result);
        Assert.Equal(EmailStatus.Sent, staged.Status);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.IsType<InvalidOperationException>(entry.Exception);
        Assert.Contains(staged.Id.ToString(), entry.Message, StringComparison.OrdinalIgnoreCase);
    }

    private EmailDeliveryService NewService() =>
        EmailDeliveryTestFactory.Create(_deliveries, _sender, _unitOfWork, _clock);

    private static EmailMessage Message(Guid attendeeId) =>
        new(
            attendeeId,
            "attendee@example.com",
            "Attendee",
            EmailTemplate.AttendeeInvite,
            "Choose a time",
            "body",
            "<p>body</p>");

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, Exception? Exception, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, exception, formatter(state, exception)));
    }
}
