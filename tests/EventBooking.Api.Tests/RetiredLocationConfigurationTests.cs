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
            ["Clock:TimeZoneId"] = "Europe/London",
            ["Tokens:SigningKey"] = "a-test-signing-key-that-is-at-least-32-characters",
            ["Email:FromAddress"] = "test@example.test",
            ["Email:FromName"] = "Test sender",
            ["Email:Provider"] = "Smtp",
            ["Auth:Provider"] = "Local",
            ["Portal:BaseUrl"] = "http://localhost:5002",
            ["Portal:CoordinatorContact"] = "help@example.test",
        }).Build();

        var values = EventBookingConfiguration.Read(configuration);

        Assert.Equal("http://localhost:5002", values.Portal.BaseUrl);
        Assert.DoesNotContain(typeof(SystemClock).Assembly.GetTypes(),
            type => type.Name is "TransitionalLocationOptions" or "HeadOfficeOptions");
        Assert.DoesNotContain(typeof(AttendeePortalOptions).GetProperties(),
            property => property.Name.EndsWith("LocationAddress", StringComparison.Ordinal));
    }
}
