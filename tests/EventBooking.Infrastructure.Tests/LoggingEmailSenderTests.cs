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
