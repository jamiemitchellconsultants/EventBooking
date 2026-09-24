using EventBooking.Api.Auth;
using Microsoft.AspNetCore.Authorization;

namespace EventBooking.Api.Auth;

public static class AuthenticationExtensions
{
    public const string StaffPolicy = "staff";
    public const string AuthenticatedPolicy = "authenticated";

    public static IServiceCollection AddEventBookingAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddScoped<ICallerAccessor, HttpContextCallerAccessor>();

        // AuthClaimOptions takes its names from Auth:Claims and its pattern from
        // Identity:StaffIdPattern; the option defaults cover deployments that set neither.
        services.Configure<AuthClaimOptions>(options =>
        {
            var claims = configuration.GetSection("Auth:Claims");
            if (claims["StaffId"] is { } staffId) options.StaffIdClaim = staffId;
            if (claims["Name"] is { } name) options.NameClaim = name;
            if (claims["Roles"] is { } roles) options.RolesClaim = roles;
            if (configuration["Identity:StaffIdPattern"] is { } pattern)
                options.StaffIdPattern = pattern;
        });

        services.AddEventBookingAuthentication(configuration);

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
