using EventBooking.Api.Auth;
using Microsoft.AspNetCore.Authorization;

namespace EventBooking.Api.Auth;

public static class AuthenticationExtensions
{
    public const string StaffPolicy = "staff";
    public const string AuthenticatedPolicy = "authenticated";

    public static IServiceCollection AddEventBookingAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddScoped<ICallerAccessor, HttpContextCallerAccessor>();

        services.AddLocalAuthentication(configuration.GetSection("Auth:Local"));

        services.AddScoped<IAuthorizationHandler, StaffRequirementHandler>();

        services.AddAuthorizationBuilder()
            .AddPolicy(StaffPolicy, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new StaffRequirement());
            })
            .AddPolicy(AuthenticatedPolicy, policy => policy.RequireAuthenticatedUser());

        return services;
    }
}
