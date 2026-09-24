using EventBooking.Domain.Notifications;
using EventBooking.Infrastructure.Email;

namespace EventBooking.Infrastructure.Tests.Email;

[Collection("postgres")]
public sealed class OutboxDispatcherTests(PostgresFixture fixture) : PostgresEmailHarness(fixture)
{
    [Fact]
    public async Task Two_dispatchers_never_send_the_same_row_twice()
    {
        var rowId = await StagePendingAsync(EmailTemplate.AttendeeInvite);
        var first = Dispatcher(transportA);
        var second = Dispatcher(transportB);

        await Task.WhenAll(first.DispatchOnceAsync(), second.DispatchOnceAsync());

        Assert.Equal(1, transportA.Sent.Count + transportB.Sent.Count);
        Assert.Equal(EmailStatus.Sent, await StatusOfAsync(rowId));
    }

    [Fact]
    public async Task Six_minute_old_claim_is_reclaimed()
    {
        var rowId = await StagePendingAsync(EmailTemplate.AttendeeInvite);
        await CrashClaimAsync(rowId, claimedMinutesAgo: 6);

        await Dispatcher(transportA).DispatchOnceAsync();

        Assert.Single(transportA.Sent);
        Assert.Equal(EmailStatus.Sent, await StatusOfAsync(rowId));
    }

    [Fact]
    public async Task Transient_times_three_ends_failed_with_no_error_text()
    {
        var rowId = await StagePendingAsync(EmailTemplate.AttendeeInvite);
        transportA.Next = EmailSendOutcome.TransientFailure;

        // Each pass parks the row behind its backoff (2 then 4 minutes), so the clock
        // moves past each backoff before the next pass reclaims the row.
        await Dispatcher(transportA).DispatchOnceAsync();
        Advance(TimeSpan.FromMinutes(3));
        await Dispatcher(transportA).DispatchOnceAsync();
        Advance(TimeSpan.FromMinutes(5));
        await Dispatcher(transportA).DispatchOnceAsync();

        Assert.Equal(EmailStatus.Failed, await StatusOfAsync(rowId));
        Assert.Empty(transportA.Sent);
        Assert.DoesNotContain("failure", await AllColumnsAsync(rowId));
        Assert.DoesNotContain("transient", await AllColumnsAsync(rowId));
    }

    [Fact]
    public async Task A_staged_request_correlation_survives_claim_and_send()
    {
        var rowId = await StagePendingAsync(EmailTemplate.AttendeeInvite);
        await StampCorrelationAsync(rowId, "request-123");

        await Dispatcher(transportA).DispatchOnceAsync();

        Assert.Equal(EmailStatus.Sent, await StatusOfAsync(rowId));
        Assert.Contains("request-123", await AllColumnsAsync(rowId));
    }

    [Fact]
    public async Task Resent_email_carries_the_same_link_as_original()
    {
        var rowId = await StagePendingAsync(EmailTemplate.AttendeeInvite);
        await Dispatcher(transportA).DispatchOnceAsync();
        var firstLink = transportA.Sent.Single();

        // A late bounce reports the delivered row failed; only a failed row is retryable.
        await FailAsync(rowId);
        await RetryAsync(rowId);
        await Dispatcher(transportA).DispatchOnceAsync();

        Assert.Equal(2, transportA.Sent.Count);
        Assert.Equal(firstLink, transportA.Sent[1]);
    }

    [Fact]
    public async Task Outbox_rows_carry_no_personal_data_tokens_or_urls()
    {
        var rowId = await StagePendingAsync(EmailTemplate.AttendeeInvite);

        var columns = await AllColumnsAsync(rowId);

        Assert.DoesNotContain("amy@example.invalid", columns);
        Assert.DoesNotContain("Amy", columns);
        Assert.DoesNotContain("http", columns);
    }

    [Fact]
    public async Task Confirmation_renders_booking_snapshot_and_manage_link()
    {
        var rowId = await StageConfirmationAsync();

        await Dispatcher(transportA).DispatchOnceAsync();

        var sent = Assert.Single(transportA.Sent);
        Assert.Contains("DAT", sent.Subject);
        Assert.Contains("MED", sent.Subject);
        Assert.True(sent.Subject.IndexOf("DAT", StringComparison.Ordinal)
            < sent.Subject.IndexOf("MED", StringComparison.Ordinal));
        Assert.Contains("https://portal.example.invalid/manage/", sent.TextBody);
        Assert.Equal(EmailStatus.Sent, await StatusOfAsync(rowId));
    }

    [Theory]
    [InlineData(true, "a replacement has been created")]
    [InlineData(false, "no replacement could be created")]
    public async Task Cancellation_ending_follows_replacement_presence(
        bool withReplacement, string expected)
    {
        var rowId = await StageCancellationAsync(withReplacement);

        await Dispatcher(transportA).DispatchOnceAsync();

        var sent = Assert.Single(transportA.Sent);
        Assert.Contains(expected, sent.TextBody);
        Assert.Equal(EmailStatus.Sent, await StatusOfAsync(rowId));
    }
}
