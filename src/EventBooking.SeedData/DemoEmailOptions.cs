using System.Globalization;
using System.Net.Mail;
using EventBooking.Application.Notifications;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Tokens;

namespace EventBooking.SeedData;

/// <summary>Validated demo SMTP settings and the API-compatible attendee link configuration.</summary>
public sealed class DemoEmailOptions
{
    private DemoEmailOptions(AttendeePortalOptions portal, TokenOptions tokens,
        EmailOptions sender, SmtpOptions smtp)
    {
        Portal = portal;
        Tokens = tokens;
        Sender = sender;
        Smtp = smtp;
    }

    /// <summary>Gets the public attendee portal URL and coordinator contact.</summary>
    public AttendeePortalOptions Portal { get; }
    /// <summary>Gets the signing key that must match the API validating attendee tokens.</summary>
    public TokenOptions Tokens { get; }
    /// <summary>Gets the sender identity used for demo messages over SMTP.</summary>
    public EmailOptions Sender { get; }
    /// <summary>Gets the Mailpit SMTP host and port reachable from this process.</summary>
    public SmtpOptions Smtp { get; }

    /// <summary>Reads environment-style settings, with local defaults and explicit non-local keys.</summary>
    /// <param name="readSetting">Returns a setting value, or null when the key is absent.</param>
    /// <returns>Configuration validated before the host starts issuing demo invitations.</returns>
    /// <exception cref="SeedException">A required setting is missing, blank or invalid.</exception>
    public static DemoEmailOptions From(Func<string, string?> readSetting)
    {
        string Read(string key, string? fallback)
        {
            var value = readSetting(key) ?? fallback;
            if (string.IsNullOrWhiteSpace(value))
                throw Invalid(key);
            return value;
        }

        var baseUrl = Read("Portal__BaseUrl", "http://localhost:5002");
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw Invalid("Portal__BaseUrl");

        var signingKey = Read("Tokens__SigningKey", uri.IsLoopback
            ? "a-local-signing-key-that-is-at-least-32-characters" : null);
        if (signingKey.Length < 32)
            throw Invalid("Tokens__SigningKey");
        var smtpHost = Read("Email__Smtp__Host", uri.IsLoopback ? "localhost" : null);
        var portText = Read("Email__Smtp__Port", "1025");
        if (!int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            || port < 1 || port > 65535)
            throw Invalid("Email__Smtp__Port");
        var address = Read("Email__FromAddress", "recruitment@example.com");
        if (!MailAddress.TryCreate(address, out var mailbox)
            || !string.Equals(mailbox.Address, address, StringComparison.Ordinal))
            throw Invalid("Email__FromAddress");
        var name = Read("Email__FromName", "Recruitment Team");
        return new DemoEmailOptions(
            new AttendeePortalOptions(uri.AbsoluteUri.TrimEnd('/'),
                Read("Portal__CoordinatorContact", "recruitment@example.com")),
            new TokenOptions(signingKey),
            new EmailOptions(address, name, EmailProvider.Smtp),
            new SmtpOptions(smtpHost, port));
    }

    private static SeedException Invalid(string key) =>
        new($"Demo email setting '{key}' is missing or invalid.");

    /// <summary>Describes the configuration without exposing credentials or deployment values.</summary>
    public override string ToString() => "Demo email configuration (values redacted)";
}
