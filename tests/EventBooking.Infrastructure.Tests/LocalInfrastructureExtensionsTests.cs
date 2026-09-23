using EventBooking.Infrastructure.Email;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Infrastructure.Tests;

public class LocalInfrastructureExtensionsTests
{
    [Fact]
    public void AddLocalEmailTransportRegistersTheSmtpTransport()
    {
        var services = new ServiceCollection();

        services.AddLocalEmailTransport(
            new EmailOptions("recruitment@example.com", "Recruitment Team", EmailProvider.Smtp),
            new SmtpOptions("localhost", 1025));

        var provider = services.BuildServiceProvider();
        Assert.IsType<SmtpEmailTransport>(provider.GetRequiredService<IEmailTransport>());
    }
}
