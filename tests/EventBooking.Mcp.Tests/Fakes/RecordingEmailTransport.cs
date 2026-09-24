using EventBooking.Infrastructure.Email;

namespace EventBooking.Mcp.Tests.Fakes;

/// <summary>A send the fake provider accepted.</summary>
/// <param name="Recipient">The recipient address.</param>
/// <param name="Subject">The subject.</param>
/// <param name="TextBody">The text body.</param>
/// <param name="HtmlBody">The HTML body.</param>
public sealed record CapturedMail(string Recipient, string Subject, string TextBody, string HtmlBody);

/// <summary>
/// Stands in for AWS SES in every MCP test. Constructing the real
/// <c>AmazonSimpleEmailServiceV2Client</c> throws immediately outside an AWS environment (no
/// RegionEndpoint or ServiceURL configured), which is exactly where CI runs — so no test may
/// depend on it, directly or through a tool that happens to send an email.
/// </summary>
public sealed class RecordingEmailTransport : IEmailTransport
{
    /// <summary>Sends the fake provider accepted.</summary>
    public List<CapturedMail> Sent { get; } = [];

    /// <summary>The outcome every send reports until reassigned.</summary>
    public EmailSendOutcome Next { get; set; } = EmailSendOutcome.Sent;

    /// <inheritdoc />
    public Task<EmailSendOutcome> SendAsync(
        string recipient, string subject, string textBody, string htmlBody,
        CancellationToken cancellationToken)
    {
        if (Next == EmailSendOutcome.Sent)
        {
            Sent.Add(new CapturedMail(recipient, subject, textBody, htmlBody));
        }

        return Task.FromResult(Next);
    }
}
