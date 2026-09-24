namespace EventBooking.Infrastructure.Email;

/// <summary>The provider outcome of one send. The sender never throws for a provider error:
/// every outcome is a value.</summary>
public enum EmailSendOutcome
{
    /// <summary>The provider accepted the message.</summary>
    Sent,

    /// <summary>A temporary failure worth retrying with backoff, including timeouts.</summary>
    TransientFailure,

    /// <summary>A failure that will not heal on retry.</summary>
    PermanentFailure,
}

/// <summary>
/// The one-method seam over the mail provider. Returns the provider outcome rather than
/// throwing: a bounced confirmation must never roll back a good booking.
/// </summary>
public interface IEmailTransport
{
    /// <summary>Sends one rendered email.</summary>
    /// <param name="recipient">The recipient address.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="textBody">The text body.</param>
    /// <param name="htmlBody">The HTML body.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<EmailSendOutcome> SendAsync(
        string recipient, string subject, string textBody, string htmlBody,
        CancellationToken cancellationToken);
}
