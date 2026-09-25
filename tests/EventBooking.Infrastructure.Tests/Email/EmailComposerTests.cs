using EventBooking.Infrastructure.Email;

namespace EventBooking.Infrastructure.Tests.Email;

public sealed class EmailComposerTests
{
    private static EmailContext LondonJuly(params string[] codes) => new(
        codes, "London HQ", "1 High St", "Tue 14 Jul 2026, 09:30-11:00 BST",
        "https://portal.example.invalid/book/token", "coordinator@example.invalid",
        false, false, codes.Length);

    [Fact]
    public void Invite_renders_zone_abbreviation_and_sorted_types()
    {
        var message = EmailComposer.Compose("AttendeeInvite", LondonJuly("IND", "FIT", "MED"));

        Assert.Contains("BST", message.TextBody);
        Assert.Contains("London HQ", message.TextBody);
        Assert.Contains("1 High St", message.TextBody);
        Assert.True(message.TextBody.IndexOf("FIT", StringComparison.Ordinal)
            < message.TextBody.IndexOf("IND", StringComparison.Ordinal));
        Assert.True(message.TextBody.IndexOf("IND", StringComparison.Ordinal)
            < message.TextBody.IndexOf("MED", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(1, "appointment remains")]
    [InlineData(2, "appointments remain")]
    public void Recovery_wording_follows_count(int count, string expected)
    {
        var context = LondonJuly("MED") with { IsRecovery = true, OutstandingCount = count };

        var message = EmailComposer.Compose("AttendeeInvite", context);

        Assert.Contains(expected, message.TextBody);
    }

    [Theory]
    [InlineData(true, "a replacement has been created")]
    [InlineData(false, "no replacement could be created")]
    public void Cancellation_ending_follows_replacement_flag(bool created, string expected)
    {
        var context = LondonJuly("MED") with { ReplacementCreated = created };

        var message = EmailComposer.Compose("EventCancelledRebookingNeeded", context);

        Assert.Contains(expected, message.TextBody);
    }

    [Fact]
    public void Html_carries_the_same_lines_as_text()
    {
        var message = EmailComposer.Compose("BookingConfirmation", LondonJuly("MED"));

        Assert.Equal(
            message.TextBody.Split('\n').Select(l => l.Trim()),
            message.HtmlBody.Split('\n').Select(l => l.Trim()));
    }

    [Fact]
    public void Self_registration_confirmation_carries_confirmed_subject_and_manage_url()
    {
        var message = EmailComposer.Compose("SelfRegistrationConfirmation", LondonJuly("MED"));

        Assert.StartsWith("Confirmed:", message.Subject, StringComparison.Ordinal);
        Assert.Contains("https://portal.example.invalid/book/token", message.TextBody);
        Assert.Contains("https://portal.example.invalid/book/token", message.HtmlBody);
    }

    [Fact]
    public void Unknown_template_is_refused()
    {
        Assert.Throws<ArgumentException>(() =>
            EmailComposer.Compose("NoSuchTemplate", LondonJuly("MED")));
    }
}
