using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Auth;

public static class LocalAuthenticationExtensions
{
    /// <summary>A generic OIDC Bearer [REDACTED] against a Keycloak realm's issuer and
    /// audience — the Auth:Local configuration section (Authority, Audience). The local realm
    /// is served over plain HTTP, so HTTPS metadata is disabled; this provider is never used
    /// outside a local deployment.</summary>
    public static AuthenticationBuilder AddLocalAuthentication(
        this IServiceCollection services, IConfiguration authLocalSection) =>
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authLocalSection["Authority"];
                options.Audience = authLocalSection["Audience"];
                options.RequireHttpsMetadata = false;

                // Without this, JwtBearer remaps well-known claim names (notably "roles") to
                // legacy http://schemas.xmlsoap.org/... / ClaimTypes URIs via
                // JwtSecurityTokenHandler.DefaultInboundClaimTypeMap, so HttpContextCallerAccessor's
                // literal "roles" / "oid" / "staff_id" lookups silently find nothing.
                // Microsoft.Identity.Web (the EntraId provider) already disables this internally,
                // which is why only this local Keycloak path needs it here.
                options.MapInboundClaims = false;
            });
}
