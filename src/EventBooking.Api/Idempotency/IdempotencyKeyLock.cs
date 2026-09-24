using Npgsql;

namespace EventBooking.Api.Idempotency;

/// <summary>
/// Holds a PostgreSQL session advisory lock for one caller/route/key triple. It spans the
/// handler and retention write, so two API processes cannot both create a resource before
/// either can replay the retained response.
/// </summary>
public sealed class IdempotencyKeyLock(NpgsqlDataSource dataSource)
{
    public async Task<IAsyncDisposable> AcquireAsync(
        Guid staffUserId, string route, string key, CancellationToken ct)
    {
        var connection = await dataSource.OpenConnectionAsync(ct);
        try
        {
            await using var command = new NpgsqlCommand(
                "SELECT pg_advisory_lock(hashtextextended(@key, 0));", connection);
            command.Parameters.AddWithValue("key", $"{staffUserId:N}:{route}:{key}");
            await command.ExecuteNonQueryAsync(ct);
            return new Lease(connection);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private sealed class Lease(NpgsqlConnection connection) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await using var command = new NpgsqlCommand(
                "SELECT pg_advisory_unlock_all();", connection);
            await command.ExecuteNonQueryAsync();
            await connection.DisposeAsync();
        }
    }
}
