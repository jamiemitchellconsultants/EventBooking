using System.Data.Common;
using Npgsql;

namespace EventBooking.Infrastructure.Persistence;

/// <summary>
/// Design 08 sizes the Npgsql pool to 20 per replica by default, so that PostgreSQL's
/// <c>max_connections</c> of replica count × 20 + 10 always admits every pool. Npgsql's own
/// default is 100, which on its own meets the server's default limit and turns a burst into
/// "too many clients" errors instead of a short wait for a pooled connection.
/// </summary>
public static class ConnectionPooling
{
    /// <summary>The per-replica pool size design 08 assumes.</summary>
    public const int DefaultMaxPoolSize = 20;

    /// <summary>Npgsql's spellings of the maximum pool size keyword.</summary>
    private static readonly string[] MaxPoolSizeKeywords =
        ["Maximum Pool Size", "MaxPoolSize"];

    /// <summary>Applies the default pool size unless the connection string names one.</summary>
    /// <param name="connectionString">The configured connection string.</param>
    /// <returns>The connection string with a maximum pool size.</returns>
    public static string WithDefaultMaxPoolSize(string connectionString)
    {
        var configured = new DbConnectionStringBuilder { ConnectionString = connectionString };
        if (MaxPoolSizeKeywords.Any(configured.ContainsKey))
            return connectionString;

        return new NpgsqlConnectionStringBuilder(connectionString)
        {
            MaxPoolSize = DefaultMaxPoolSize,
        }.ConnectionString;
    }
}
