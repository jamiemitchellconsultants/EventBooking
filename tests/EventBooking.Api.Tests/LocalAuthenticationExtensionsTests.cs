using EventBooking.Api.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EventBooking.Api.Tests;

public class LocalAuthenticationExtensionsTests
{
    [Fact]
    public void AddLocalAuthenticationSetsJwtBearerAsTheDefaultSchemeAndReadsAuthorityAndAudience()
    {
        var services = new ServiceCollection();
        var authLocalSection = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authority"] = "http://keycloak.local:8081/realms/eventbooking",
                ["Audience"] = "eventbooking-web",
            })
            .Build();

        services.AddLocalAuthentication(authLocalSection);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        var jwtOptions = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, options.DefaultScheme);
        Assert.Equal("http://keycloak.local:8081/realms/eventbooking", jwtOptions.Authority);
        Assert.Equal("eventbooking-web", jwtOptions.Audience);
    }
}
