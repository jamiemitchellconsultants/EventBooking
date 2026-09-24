using EventBooking.Application.Abstractions;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Tokens;
using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

/// <summary>Checks configuration needed for demo links and SMTP delivery.</summary>
public sealed class DemoEmailOptionsTests
{
    /// <summary>Default tokens are readable with the local API's configured key.</summary>
    [Fact]
    public void DefaultsMatchLocalApiAndMailpit()
    {
        var options = DemoEmailOptions.From(_ => null);
        var seed = new HmacTokenService(options.Tokens);
        var api = new HmacTokenService(new TokenOptions(
            "a-local-signing-key-that-is-at-least-32-characters"));
        var id = Guid.NewGuid();
        Assert.True(api.TryRead(seed.Issue(TokenPurpose.Book, id, 1), out var read));
        Assert.Equal(new TokenReference(TokenPurpose.Book, id, 1), read);
        Assert.Equal("http://localhost:5002", options.Portal.BaseUrl);
        Assert.Equal("localhost", options.Smtp.Host);
        Assert.Equal(1025, options.Smtp.Port);
        Assert.Equal(EmailProvider.Smtp, options.Sender.Provider);
    }

    /// <summary>Explicit deployment settings drive usable links and email transport.</summary>
    [Fact]
    public void OverridesUseConfiguredKeyAndNormalizePortal()
    {
        var values = Complete();
        values["Portal__BaseUrl"] = "https://demo.example.test/portal/";
        values["Email__Smtp__Port"] = "2525";
        values["Email__FromAddress"] = "demo@example.com";
        values["Email__FromName"] = "Demo recruitment";
        values["Portal__CoordinatorContact"] = "help@example.com";
        var options = DemoEmailOptions.From(values.GetValueOrDefault);
        var token = new HmacTokenService(options.Tokens)
            .Issue(TokenPurpose.Book, Guid.NewGuid(), 1);
        var api = new HmacTokenService(new TokenOptions(values["Tokens__SigningKey"]!));
        Assert.True(api.TryRead(token, out _));
        Assert.Equal("https://demo.example.test/portal", options.Portal.BaseUrl);
        Assert.Equal("mailpit", options.Smtp.Host);
        Assert.Equal(2525, options.Smtp.Port);
        Assert.Equal("demo@example.com", options.Sender.FromAddress);
        Assert.Equal("Demo recruitment", options.Sender.FromName);
        Assert.Equal("help@example.com", options.Portal.CoordinatorContact);
        Assert.DoesNotContain(values["Tokens__SigningKey"]!, options.ToString());
    }

    /// <summary>Non-local links never fall back to the local API's development secret or SMTP host.</summary>
    [Theory]
    [InlineData("Tokens__SigningKey")]
    [InlineData("Email__Smtp__Host")]
    public void NonLocalPortalRequiresExplicitSetting(string missing)
    {
        var values = Complete();
        values.Remove(missing);
        var error = Assert.Throws<SeedException>(() =>
            DemoEmailOptions.From(values.GetValueOrDefault));
        Assert.Contains(missing, error.Message);
    }

    /// <summary>Invalid values fail by setting name without exposing the supplied value.</summary>
    [Theory]
    [InlineData("Portal__BaseUrl", "not-a-url")]
    [InlineData("Portal__BaseUrl", "ftp://demo.example.test")]
    [InlineData("Portal__BaseUrl", "https://user:secret@demo.example.test")]
    [InlineData("Portal__BaseUrl", "https://demo.example.test?secret=x")]
    [InlineData("Portal__BaseUrl", "https://demo.example.test#fragment")]
    [InlineData("Tokens__SigningKey", "short-secret")]
    [InlineData("Email__Smtp__Port", "0")]
    [InlineData("Email__Smtp__Port", "65536")]
    [InlineData("Email__Smtp__Port", "invalid-port")]
    [InlineData("Email__Smtp__Host", " ")]
    [InlineData("Email__FromAddress", "not-an-email")]
    public void InvalidConfigurationFailsWithoutValueDisclosure(string key, string value)
    {
        var values = Complete();
        values[key] = value;
        var error = Assert.Throws<SeedException>(() =>
            DemoEmailOptions.From(values.GetValueOrDefault));
        Assert.Contains(key, error.Message);
        if (!string.IsNullOrWhiteSpace(value) && value.Length > 1)
            Assert.DoesNotContain(value, error.Message);
    }

    private static Dictionary<string, string?> Complete() => new()
    {
        ["Portal__BaseUrl"] = "https://demo.example.test",
        ["Tokens__SigningKey"] = "a-test-signing-key-with-at-least-32-characters",
        ["Email__Smtp__Host"] = "mailpit",
    };
}
