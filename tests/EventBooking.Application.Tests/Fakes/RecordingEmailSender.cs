using EventBooking.Application.Abstractions;
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Tests.Fakes;

public sealed class RecordingEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];

    /// <summary>Set to make the next send report failure, as a bounced or rejected address would.</summary>
    public bool FailNextSend { get; set; }

    public Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (FailNextSend)
        {
            FailNextSend = false;
            return Task.FromResult(false);
        }

        Sent.Add(message);
        return Task.FromResult(true);
    }

    public EmailMessage LastOf(EmailTemplate template) =>
        Sent.Last(m => m.Template == template);
}
