using EventBooking.Infrastructure.Persistence;
using Npgsql;

namespace EventBooking.Infrastructure.Tests;

public sealed class ConnectionPoolingTests
{
    [Fact]
    public void Unsized_connection_string_gets_the_design_08_default()
    {
        var result = ConnectionPooling.WithDefaultMaxPoolSize("Host=postgres;Database=eventbooking");

        var parsed = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal(ConnectionPooling.DefaultMaxPoolSize, parsed.MaxPoolSize);
        Assert.Equal("postgres", parsed.Host);
        Assert.Equal("eventbooking", parsed.Database);
    }

    [Theory]
    [InlineData("Maximum Pool Size")]
    [InlineData("MaxPoolSize")]
    [InlineData("maximum pool size")]
    public void Explicit_pool_size_is_kept_in_any_npgsql_spelling(string keyword)
    {
        var configured = $"Host=postgres;Database=eventbooking;{keyword}=7";

        var result = ConnectionPooling.WithDefaultMaxPoolSize(configured);

        Assert.Equal(configured, result);
        Assert.Equal(7, new NpgsqlConnectionStringBuilder(result).MaxPoolSize);
    }
}
