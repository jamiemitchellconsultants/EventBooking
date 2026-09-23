using EventBooking.Application.Abstractions;
using EventBooking.Infrastructure.Email;
using MailKit.Net.Smtp;
using MimeKit;

namespace EventBooking.Infrastructure.Email;

/// <summary>
/// The only place in the system that touches MailKit. No branching lives here on purpose: anything
/// worth testing belongs one layer up, in the sender. Mailpit accepts unauthenticated, unencrypted
/// SMTP, so no credentials or TLS options are configured.
/// </summary>
public sealed class SmtpEmailTransport(SmtpOptions options, EmailOptions email) : IEmailTransport
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(email.FromName, email.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.ToAddress));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder
        {
            TextBody = message.TextBody,
            HtmlBody = message.HtmlBody,
        }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(options.Host, options.Port, false, cancellationToken);
        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
