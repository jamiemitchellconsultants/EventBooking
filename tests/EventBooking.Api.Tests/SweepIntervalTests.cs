using Microsoft.Extensions.Configuration;

namespace EventBooking.Api.Tests;

/// <summary>Locks the sweep interval default and its configuration override.</summary>
public sealed class SweepIntervalTests
{
    [Fact]
    public void Default_is_fifteen_minutes()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.Equal(TimeSpan.FromMinutes(15), InviteSweepService.ResolveInterval(configuration));
    }

    [Fact]
    public void Override_is_honoured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jobs:SweepInterval"] = "00:05:00",
            })
            .Build();

        Assert.Equal(TimeSpan.FromMinutes(5), InviteSweepService.ResolveInterval(configuration));
    }
}
