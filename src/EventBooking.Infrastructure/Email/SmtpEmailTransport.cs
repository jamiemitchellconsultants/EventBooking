using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace EventBooking.Infrastructure.Email;

/// <summary>
/// The only place in the system that touches MailKit. Provider errors become outcomes:
/// authentication, mailbox-syntax and 5xx mailbox refusals are permanent (none heals on
/// retry); everything else is transient. Cancellation is never converted: a cancelled
/// caller must observe cancellation.
/// </summary>
public sealed class SmtpEmailTransport(SmtpOptions options, EmailOptions email) : IEmailTransport
{
    /// <inheritdoc />
    public async Task<EmailSendOutcome> SendAsync(
        string recipient, string subject, string textBody, string htmlBody,
        CancellationToken cancellationToken)
    {
        try
        {
            var mime = new MimeMessage();
            mime.From.Add(new MailboxAddress(email.FromName, email.FromAddress));
            mime.To.Add(MailboxAddress.Parse(recipient));
            mime.Subject = subject;
            mime.Body = new BodyBuilder
            {
                TextBody = textBody,
                HtmlBody = htmlBody,
            }.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(options.Host, options.Port, false, cancellationToken);
            await client.SendAsync(mime, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            return EmailSendOutcome.Sent;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AuthenticationException)
        {
            return EmailSendOutcome.PermanentFailure;
        }
        catch (ParseException)
        {
            return EmailSendOutcome.PermanentFailure;
        }
        catch (SmtpCommandException ex) when (IsPermanentRefusal(ex.StatusCode))
        {
            return EmailSendOutcome.PermanentFailure;
        }
        catch (Exception)
        {
            return EmailSendOutcome.TransientFailure;
        }
    }

    private static bool IsPermanentRefusal(SmtpStatusCode status)
    {
        var code = (int)status;
        return code >= 500 && code < 600;
    }
}
