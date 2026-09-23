using EventBooking.Infrastructure.Email;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Infrastructure.Email;

public static class LocalInfrastructureExtensions
{
    public static IServiceCollection AddLocalEmailTransport(
        this IServiceCollection services, EmailOptions options, SmtpOptions smtp)
    {
        services.AddSingleton(options);
        services.AddSingleton(smtp);
        services.AddScoped<IEmailTransport, SmtpEmailTransport>();

        return services;
    }
}
