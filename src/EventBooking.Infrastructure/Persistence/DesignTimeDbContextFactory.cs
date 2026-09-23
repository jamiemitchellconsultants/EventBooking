using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EventBooking.Infrastructure.Persistence;

/// <summary>
/// Used only by the dotnet-ef tooling, so migrations can be generated from the infrastructure
/// project alone without starting the API. The connection string here is never used at run time.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<EventBookingDbContext>
{
    public EventBookingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<EventBookingDbContext>()
            .UseNpgsql("Host=localhost;Database=eventbooking_design_time;Username=postgres;Password=postgres")
            .Options;

        return new EventBookingDbContext(options);
    }
}
