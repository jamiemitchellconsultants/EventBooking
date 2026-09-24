using System.Text;
using EventBooking.Api.Auth;
using EventBooking.Application.Notifications;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;

namespace EventBooking.Api;

/// <summary>Everything the host needs, validated before it binds a port.</summary>
/// <param name="ConnectionString">The application-user PostgreSQL connection string.</param>
/// <param name="Clock">The transitional single-zone clock options.</param>
/// <param name="Tokens">The attendee-token signing options.</param>
/// <param name="Email">The sender identity and provider.</param>
/// <param name="Smtp">The SMTP endpoint.</param>
/// <param name="Portal">The public portal origin and contact.</param>
/// <param name="AllowedOrigins">The web origins CORS permits; never a wildcard.</param>
/// <param name="ProxyNetworks">The CIDR networks forwarded headers are trusted from.</param>
/// <param name="RateLimits">The three per-minute allowances.</param>
/// <param name="SweepInterval">The invite-sweep schedule.</param>
public sealed record EventBookingSettings(
    string ConnectionString,
    ClockOptions Clock,
    TokenOptions Tokens,
    EmailOptions Email,
    SmtpOptions Smtp,
    AttendeePortalOptions Portal,
    IReadOnlyList<string> AllowedOrigins,
    IReadOnlyList<string> ProxyNetworks,
    RateLimitSettings RateLimits,
    TimeSpan SweepInterval);

/// <summary>Reads and validates the configuration required to start the EventBooking API.</summary>
public static class EventBookingConfiguration
{
    /// <summary>
    /// Signing keys that have appeared in a sample, a README or a container default. A key on
    /// this list is worse than a missing one: it starts, and every attendee link is forgeable.
    /// Matched case-insensitively after trimming.
    /// </summary>
    public static readonly IReadOnlySet<string> PlaceholderSigningKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "change-me",
            "changeme",
            "change_me",
            "secret",
            "development-signing-key-development-signing-key",
            "insecure-development-key-insecure-development-key",
            "a-test-signing-key-that-is-long-enough-here",
        };

    /// <summary>The smallest signing key design 06 accepts.</summary>
    public const int MinimumSigningKeyBytes = 32;

    /// <summary>Reads every setting, or throws naming all that are missing or invalid.</summary>
    /// <param name="configuration">The bound configuration.</param>
    /// <returns>The validated settings.</returns>
    /// <exception cref="InvalidOperationException">Thrown when validation fails.</exception>
    public static EventBookingSettings Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var missing = new List<string>();
        var invalid = new List<string>();

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
        Required("Auth:Authority");
        Required("Auth:Audience");
        var signingKey = Required("Tokens:SigningKey");
        var smtpHost = Required("Email:Smtp:Host");
        var fromAddress = Required("Email:FromAddress");
        var baseUrl = Required("Portal:BaseUrl");
        var coordinatorContact = Required("Portal:CoordinatorContact");

        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (allowedOrigins.Length == 0)
        {
            missing.Add("Cors:AllowedOrigins");
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "The following configuration values are missing: " + string.Join(", ", missing));
        }

        // Checked after the missing-key check, not folded into it: a key that is present but
        // holds an unusable value is a different failure from one that was never set.
        if (PlaceholderSigningKeys.Contains(signingKey.Trim()))
        {
            invalid.Add(
                "Tokens:SigningKey is a known placeholder value. Generate a real key: " +
                "openssl rand -base64 48");
        }
        else if (SigningKeyBytes(signingKey) < MinimumSigningKeyBytes)
        {
            invalid.Add(
                $"Tokens:SigningKey must be at least {MinimumSigningKeyBytes} bytes.");
        }

        if (allowedOrigins.Contains("*"))
        {
            invalid.Add("Cors:AllowedOrigins must name origins, never a wildcard.");
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out _))
        {
            invalid.Add("Portal:BaseUrl must be an absolute URI.");
        }

        if (invalid.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", invalid));
        }

        return new EventBookingSettings(
            connectionString,
            new ClockOptions(configuration["Clock:TimeZoneId"] ?? "Etc/UTC"),
            new TokenOptions(signingKey),
            new EmailOptions(
                fromAddress, configuration["Email:FromName"] ?? "EventBooking", EmailProvider.Smtp),
            new SmtpOptions(
                smtpHost,
                int.TryParse(configuration["Email:Smtp:Port"], out var port) ? port : 25),
            new AttendeePortalOptions(baseUrl, coordinatorContact),
            allowedOrigins,
            configuration.GetSection("Proxy:Networks").Get<string[]>() ?? [],
            new RateLimitSettings
            {
                AttendeePerMinute = Positive(configuration, "RateLimiting:AttendeePerMinute", 30),
                TokenPerMinute = Positive(configuration, "RateLimiting:TokenPerMinute", 10),
                StaffPerMinute = Positive(configuration, "RateLimiting:StaffPerMinute", 300),
            },
            TimeSpan.TryParse(configuration["Jobs:SweepInterval"], out var interval)
                ? interval
                : TimeSpan.FromMinutes(15));
    }

    /// <summary>
    /// The documented form is base64, and that is what an operator running the openssl command
    /// above will paste. A value that is not base64 is taken as UTF-8 rather than rejected, so
    /// a pasted passphrase becomes a working key of its own length — it is still held to the
    /// same 32-byte floor, so this cannot admit a weak key by accident.
    /// </summary>
    private static int SigningKeyBytes(string value)
    {
        Span<byte> decoded = stackalloc byte[value.Length];
        return Convert.TryFromBase64String(value, decoded, out var written)
            ? written
            : Encoding.UTF8.GetByteCount(value);
    }

    private static int Positive(IConfiguration configuration, string key, int fallback) =>
        int.TryParse(configuration[key], out var value) && value > 0 ? value : fallback;
}
