using EventBooking.Application.Abstractions;
using EventBooking.Infrastructure.Email;

namespace EventBooking.Mcp.Tests.Fakes;

/// <summary>
/// Stands in for AWS SES in every MCP test. Constructing the real
/// <c>AmazonSimpleEmailServiceV2Client</c> throws immediately outside an AWS environment (no
/// RegionEndpoint or ServiceURL configured), which is exactly where CI runs — so no test may
/// depend on it, directly or through a tool that happens to send an email.
/// </summary>
public sealed class RecordingEmailTransport : IEmailTransport
{
    /// <summary>Messages accepted by this fake provider.</summary>
    public List<EmailMessage> Sent { get; } = [];

    /// <inheritdoc />
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Sent.Add(message);
        return Task.CompletedTask;
    }
}
