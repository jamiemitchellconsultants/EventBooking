using EventBooking.Api.Auth;
using EventBooking.Domain.Access;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EventBooking.Api.Tests;

public class LocalAuthenticationExtensionsTests
{
    [Fact]
    public void ProviderNeutralRegistrationReadsAuthorityAndAudienceFromAuth()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Authority"] = "https://keycloak.local:8081/realms/eventbooking",
                ["Auth:Audience"] = "eventbooking-web",
            })
            .Build();

        services.AddEventBookingAuthentication(configuration);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        var jwtOptions = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, options.DefaultScheme);
        Assert.Equal("https://keycloak.local:8081/realms/eventbooking", jwtOptions.Authority);
        Assert.Equal("eventbooking-web", jwtOptions.Audience);
        Assert.False(jwtOptions.MapInboundClaims);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void HttpsMetadataDefaultsToRequiredUnlessTheLocalStackOptsOut(
        string? configured, bool expected)
    {
        var services = new ServiceCollection();
        // Reading the options validates the authority against the HTTPS requirement, so
        // the opted-out case keeps the plain-HTTP local realm while the required cases
        // use HTTPS.
        var values = new Dictionary<string, string?>
        {
            ["Auth:Authority"] = configured == "false"
                ? "http://keycloak.local:8081/realms/eventbooking"
                : "https://keycloak.local:8081/realms/eventbooking",
            ["Auth:Audience"] = "eventbooking-web",
        };
        if (configured is not null)
        {
            values["Auth:RequireHttpsMetadata"] = configured;
        }

        services.AddEventBookingAuthentication(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build());

        var jwtOptions = services.BuildServiceProvider()
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.Equal(expected, jwtOptions.RequireHttpsMetadata);
    }

    [Fact]
    public void ClaimOptionsBindFromAuthClaimsAndIdentityPattern()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Authority"] = "https://login.example.com/tenant",
                ["Auth:Audience"] = "eventbooking",
                ["Auth:Claims:StaffId"] = "employee_no",
                ["Auth:Claims:Name"] = "display_name",
                ["Auth:Claims:Roles"] = "app_roles",
                ["Identity:StaffIdPattern"] = "^[UN][0-9]{6}$",
            })
            .Build();

        services.AddEventBookingAuth(configuration);

        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<AuthClaimOptions>>().Value;

        Assert.Equal("employee_no", bound.StaffIdClaim);
        Assert.Equal("display_name", bound.NameClaim);
        Assert.Equal("app_roles", bound.RolesClaim);
        Assert.Equal("^[UN][0-9]{6}$", bound.StaffIdPattern);
    }

    [Fact]
    public void ClaimOptionsFallBackToDefaultsWhenUnconfigured()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Authority"] = "https://login.example.com/tenant",
                ["Auth:Audience"] = "eventbooking",
            })
            .Build();

        services.AddEventBookingAuth(configuration);

        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<AuthClaimOptions>>().Value;

        Assert.Equal("staff_id", bound.StaffIdClaim);
        Assert.Equal("name", bound.NameClaim);
        Assert.Equal("roles", bound.RolesClaim);
        Assert.Equal(StaffId.DefaultPattern, bound.StaffIdPattern);
    }
}
