using System.Net;
using Microsoft.Extensions.Configuration;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class HealthTests(ApiFactory factory)
{
    [Fact]
    public async Task TheHostStartsAndAnswersHealthAnonymously()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void MissingConfigurationNamesEverySettingThatIsAbsent()
    {
        var empty = new ConfigurationBuilder().Build();

        var ex = Assert.Throws<InvalidOperationException>(() => EventBookingConfiguration.Read(empty));

        Assert.Contains("ConnectionStrings:EventBooking", ex.Message);
        Assert.Contains("Tokens:SigningKey", ex.Message);
        Assert.Contains("Portal:BaseUrl", ex.Message);
        Assert.Contains("Auth:Provider", ex.Message);
        Assert.Contains("Email:Provider", ex.Message);
    }

    /// <summary>Ensures only the documented exact email-provider literals are accepted.</summary>
    [Theory]
    [InlineData("Fax")]
    [InlineData("999")]
    [InlineData("ses")]
    [InlineData("smtp")]
    public void AnInvalidEmailProviderIsRejectedWithAReadableMessage(string emailProvider)
    {
        var configuration = ConfigurationWith(emailProvider: emailProvider);

        var ex = Assert.Throws<InvalidOperationException>(
            () => EventBookingConfiguration.Read(configuration));

        Assert.Contains("Email:Provider", ex.Message);
    }

    [Fact]
    public void AnInvalidAuthProviderIsRejectedWithAReadableMessage()
    {
        var configuration = ConfigurationWith(authProvider: "Auth0");

        var ex = Assert.Throws<InvalidOperationException>(
            () => EventBookingConfiguration.Read(configuration));

        Assert.Contains("Auth:Provider", ex.Message);
    }

    /// <summary>Every required key present and valid, except the one override under test.</summary>
    private static IConfiguration ConfigurationWith(
        string emailProvider = "Smtp", string authProvider = "Local") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:EventBooking"] = "Host=localhost;Database=x;Username=x;Password=x",
                ["Clock:TimeZoneId"] = "Europe/London",
                ["Tokens:SigningKey"] = "a-signing-key-that-is-long-enough-to-be-safe",
                ["Email:FromAddress"] = "recruitment@example.com",
                ["Email:FromName"] = "Recruitment Team",
                ["Email:Provider"] = emailProvider,
                ["Auth:Provider"] = authProvider,
                ["Portal:BaseUrl"] = "https://localhost:5001",
                ["Portal:CoordinatorContact"] = "recruitment@example.com",
            })
            .Build();
}
