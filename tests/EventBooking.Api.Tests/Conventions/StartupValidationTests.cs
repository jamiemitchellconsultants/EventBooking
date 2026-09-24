using EventBooking.Api;
using Microsoft.Extensions.Configuration;

namespace EventBooking.Api.Tests.Conventions;

/// <summary>
/// Startup validation runs before the host binds a port, so these cases build configuration
/// directly rather than through the factory: a host that refuses to start has no client.
/// </summary>
public sealed class StartupValidationTests
{
    [Fact]
    public void EveryMissingKeyIsNamedAtOnce()
    {
        var empty = new ConfigurationBuilder().Build();

        var ex = Assert.Throws<InvalidOperationException>(() => EventBookingConfiguration.Read(empty));

        foreach (var key in new[]
        {
            "ConnectionStrings:EventBooking",
            "Auth:Authority",
            "Auth:Audience",
            "Tokens:SigningKey",
            "Email:Smtp:Host",
            "Email:FromAddress",
            "Portal:BaseUrl",
            "Portal:CoordinatorContact",
            "Cors:AllowedOrigins",
        })
        {
            Assert.Contains(key, ex.Message);
        }
    }

    [Fact]
    public void AMissingPortalBaseUrlIsNamedOnItsOwn()
    {
        var configuration = Complete(remove: "Portal:BaseUrl");

        var ex = Assert.Throws<InvalidOperationException>(
            () => EventBookingConfiguration.Read(configuration));

        Assert.Contains("Portal:BaseUrl", ex.Message);
        Assert.DoesNotContain("Tokens:SigningKey", ex.Message);
    }

    [Theory]
    [InlineData("change-me")]
    [InlineData("CHANGE_ME")]
    [InlineData("development-signing-key-development-signing-key")]
    [InlineData("insecure-development-key-insecure-development-key")]
    public void APlaceholderSigningKeyIsRejected(string key)
    {
        var configuration = Complete(signingKey: key);

        var ex = Assert.Throws<InvalidOperationException>(
            () => EventBookingConfiguration.Read(configuration));

        Assert.Contains("Tokens:SigningKey", ex.Message);
        Assert.Contains("placeholder", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ASigningKeyShorterThanThirtyTwoBytesIsRejected()
    {
        var configuration = Complete(signingKey: new string('k', 31));

        var ex = Assert.Throws<InvalidOperationException>(
            () => EventBookingConfiguration.Read(configuration));

        Assert.Contains("32", ex.Message);
    }

    [Fact]
    public void ACompleteConfigurationIsAccepted()
    {
        var settings = EventBookingConfiguration.Read(Complete());

        Assert.Equal("https://portal.example.com", settings.Portal.BaseUrl);
        Assert.Equal(30, settings.RateLimits.AttendeePerMinute);
        Assert.Equal(10, settings.RateLimits.TokenPerMinute);
        Assert.Equal(300, settings.RateLimits.StaffPerMinute);
    }

    [Fact]
    public void TheAttendeeLimitIsConfigurable()
    {
        var configuration = Complete(extra: new() { ["RateLimiting:AttendeePerMinute"] = "5" });

        var settings = EventBookingConfiguration.Read(configuration);

        Assert.Equal(5, settings.RateLimits.AttendeePerMinute);
    }

    /// <summary>Every required key present and valid, with one override or removal applied.</summary>
    private static IConfiguration Complete(
        string? remove = null,
        string signingKey = "a-signing-key-that-is-at-least-32-bytes-long",
        Dictionary<string, string?>? extra = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:EventBooking"] = "Host=localhost;Database=x;Username=x;Password=x",
            ["Auth:Authority"] = "https://id.example.com/realms/eventbooking",
            ["Auth:Audience"] = "eventbooking-api",
            ["Tokens:SigningKey"] = signingKey,
            ["Email:Smtp:Host"] = "mail.example.com",
            ["Email:Smtp:Port"] = "1025",
            ["Email:FromAddress"] = "events@example.com",
            ["Email:FromName"] = "Events Team",
            ["Portal:BaseUrl"] = "https://portal.example.com",
            ["Portal:CoordinatorContact"] = "events@example.com",
            ["Cors:AllowedOrigins:0"] = "https://portal.example.com",
        };

        if (remove is not null)
        {
            values.Remove(remove);
        }

        foreach (var pair in extra ?? [])
        {
            values[pair.Key] = pair.Value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
