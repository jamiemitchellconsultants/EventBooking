using System.Data;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Jobs;

/// <summary>Advisory-lock guard: at most one sweep runs at a time. The lock key is fixed for the sweep; a held lock skips the run without error.</summary>
public static class AdvisoryLock
{
    /// <summary>The fixed lock key for the sweep, namespaced far from PostgreSQL's internal uses.</summary>
    public const long SweepKey = 840101;

    /// <summary>Tries to acquire the sweep lock on the run's connection.</summary>
    /// <param name="context">The run's context.</param>
    /// <param name="ct">The cancellation token.</param>
    public static async Task<bool> TryAcquireSweepLockAsync(
        EventBookingDbContext context, CancellationToken ct)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await context.Database.OpenConnectionAsync(ct);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock(@key)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@key";
        parameter.Value = SweepKey;
        command.Parameters.Add(parameter);
        return (bool?)await command.ExecuteScalarAsync(ct) == true;
    }

    /// <summary>
    /// Releases the sweep lock on the run's connection. Explicit, because pooled sessions
    /// survive the connection close — a close alone would leave the lock held on a pooled
    /// session and every later run on another session would skip forever.
    /// </summary>
    /// <param name="context">The run's context.</param>
    /// <param name="ct">The cancellation token.</param>
    public static async Task ReleaseSweepLockAsync(
        EventBookingDbContext context, CancellationToken ct)
    {
        var connection = context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_advisory_unlock(@key)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@key";
        parameter.Value = SweepKey;
        command.Parameters.Add(parameter);
        await command.ExecuteScalarAsync(ct);
    }
}
