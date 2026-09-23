using EventBooking.Application.Notifications;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;

namespace EventBooking.Api;

public static class EventBookingConfiguration
{
    /// <summary>
    /// Reads and validates the configuration required to start the EventBooking API.
    /// </summary>
    public static (
        string ConnectionString,
        HeadOfficeOptions HeadOffice,
        TokenOptions Tokens,
        EmailOptions Email,
        CandidatePortalOptions Portal) Read(IConfiguration configuration)
    {
        var missing = new List<string>();

        string Required(string key)
        {
            var value = configuration[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                missing.Add(key);
                return string.Empty;
            }

            return value;
        }

        var connectionString = Required("ConnectionStrings:EventBooking");
        var timeZone = Required("HeadOffice:TimeZoneId");
        var address = Required("HeadOffice:Address");
        var signingKey = Required("Tokens:SigningKey");
        var fromAddress = Required("Email:FromAddress");
        var fromName = Required("Email:FromName");
        var emailProviderRaw = Required("Email:Provider");
        var authProviderRaw = Required("Auth:Provider");
        var baseUrl = Required("Portal:BaseUrl");
        var coordinatorContact = Required("Portal:CoordinatorContact");

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "The following configuration values are missing: " + string.Join(", ", missing));
        }

        // Checked after the missing-key check, not folded into it: a key that is present but
        // holds an unrecognised value is a different failure from a key that was never set.
        if (emailProviderRaw != "Smtp")
        {
            throw new InvalidOperationException(
                $"Email:Provider must be 'Smtp', but was '{emailProviderRaw}'.");
        }

        var emailProvider = EmailProvider.Smtp;

        if (authProviderRaw != "Local")
        {
            throw new InvalidOperationException(
                $"Auth:Provider must be 'Local', but was '{authProviderRaw}'.");
        }

        return (
            connectionString,
            new HeadOfficeOptions(timeZone),
            new TokenOptions(signingKey),
            new EmailOptions(fromAddress, fromName, emailProvider),
            new CandidatePortalOptions(baseUrl, address, coordinatorContact));
    }
}
