using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Auth;

public static class LocalAuthenticationExtensions
{
    /// <summary>Provider-neutral OIDC Bearer [REDACTED] the Auth configuration section
    /// (Authority, Audience, RequireHttpsMetadata). Claim mapping reads the configured names,
    /// so inbound claim mapping stays off: it would rename the very claims
    /// AuthClaimOptions is configured to find.</summary>
    public static AuthenticationBuilder AddEventBookingAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        var auth = configuration.GetSection("Auth");

        return services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = auth["Authority"];
                options.Audience = auth["Audience"];

                // Defaults to requiring HTTPS. A local realm opts out explicitly through
                // Auth__RequireHttpsMetadata=false; nothing opts out by omission.
                options.RequireHttpsMetadata =
                    !bool.TryParse(auth["RequireHttpsMetadata"], out var required) || required;

                // Left off deliberately: inbound claim mapping renames the very claims
                // AuthClaimOptions is configured to find.
                options.MapInboundClaims = false;
            });
    }
}
