using EventBooking.Application.Notifications;
using EventBooking.Infrastructure.Time;
using Microsoft.Extensions.Configuration;

namespace EventBooking.Api.Tests;

public sealed class RetiredLocationConfigurationTests
{
    [Fact]
    public void Startup_configuration_needs_no_retired_location_section()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:EventBooking"] = "Host=localhost;Database=test;Username=test;Password=test",
            ["Tokens:SigningKey"] = "a-test-signing-key-that-is-at-least-32-characters",
            ["Auth:Authority"] = "https://issuer.example.test/",
            ["Auth:Audience"] = "event-booking-tests",
            ["Email:Smtp:Host"] = "smtp.example.test",
            ["Email:FromAddress"] = "test@example.test",
            ["Email:FromName"] = "Test sender",
            ["Portal:BaseUrl"] = "http://localhost:5002",
            ["Portal:CoordinatorContact"] = "help@example.test",
            ["Cors:AllowedOrigins:0"] = "https://web.example.test",
        }).Build();

        var values = EventBookingConfiguration.Read(configuration);

        Assert.Equal("http://localhost:5002", values.Portal.BaseUrl);
        Assert.DoesNotContain(typeof(SystemClock).Assembly.GetTypes(),
            type => type.Name is "TransitionalLocationOptions" or "HeadOfficeOptions");
        Assert.DoesNotContain(typeof(AttendeePortalOptions).GetProperties(),
            property => property.Name.EndsWith("LocationAddress", StringComparison.Ordinal));
    }
}
