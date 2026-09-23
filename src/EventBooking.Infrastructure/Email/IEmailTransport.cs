using EventBooking.Application.Abstractions;

namespace EventBooking.Infrastructure.Email;

/// <summary>
/// The one-method seam over the mail provider. Throws when the provider rejects the message; the
/// sender above it turns that into a logged failure.
/// </summary>
public interface IEmailTransport
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
