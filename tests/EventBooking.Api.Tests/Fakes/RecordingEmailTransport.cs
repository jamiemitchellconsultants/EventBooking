using EventBooking.Application.Abstractions;
using EventBooking.Infrastructure.Email;

namespace EventBooking.Api.Tests.Fakes;

/// <summary>
/// Stands in for AWS SES in every API test. Constructing the real
/// <c>AmazonSimpleEmailServiceV2Client</c> throws immediately outside an AWS environment (no
/// RegionEndpoint or ServiceURL configured), which is exactly where CI runs — so no test may
/// depend on it, directly or through a handler that happens to send an email.
/// </summary>
public sealed class RecordingEmailTransport : IEmailTransport
{
    /// <summary>Messages accepted by this fake provider.</summary>
    public List<EmailMessage> Sent { get; } = [];

    /// <summary>Makes the next provider call fail, modelling an SES rejection.</summary>
    public bool FailNextSend { get; set; }

    /// <inheritdoc />
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (FailNextSend)
        {
            FailNextSend = false;
            throw new InvalidOperationException("simulated provider failure");
        }

        Sent.Add(message);
        return Task.CompletedTask;
    }
}
