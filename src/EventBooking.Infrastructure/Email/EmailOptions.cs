using System.Net.Mail;

namespace EventBooking.Infrastructure.Email;

/// <summary>Which mail provider is configured for this deployment.</summary>
public enum EmailProvider { Smtp }

/// <summary>The verified identity messages are sent from, and which provider sends them.</summary>
public sealed record EmailOptions
{
    public EmailOptions(string fromAddress, string fromName, EmailProvider provider)
    {
        if (string.IsNullOrWhiteSpace(fromAddress) || !IsMailboxAddress(fromAddress))
        {
            throw new ArgumentException("A sender address is required.", nameof(fromAddress));
        }

        if (string.IsNullOrWhiteSpace(fromName))
        {
            throw new ArgumentException("A sender name is required.", nameof(fromName));
        }

        FromAddress = fromAddress;
        FromName = fromName;
        Provider = provider;
    }

    public string FromAddress { get; }

    public string FromName { get; }

    public EmailProvider Provider { get; }

    public override string ToString() => "EmailOptions { Sender = [REDACTED] }";

    private static bool IsMailboxAddress(string value)
    {
        try
        {
            return string.Equals(new MailAddress(value).Address, value, StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
