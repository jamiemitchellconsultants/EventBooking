using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies Release 2 refuses incomplete reconciliation before changing nullability.</summary>
[Collection("postgres")]
public sealed class EmployeeGroupRequiredMigrationTests(PostgresFixture fixture)
{
    private const string ReleaseOneMigration = "20260909120000_AddRecoveryBookings";

    /// <summary>An unassigned row blocks migration; explicit matching assignment then succeeds.</summary>
    [Fact]
    public async Task GuardBlocksUnassignedCandidateBeforeAlterColumn()
    {
        var databaseName = $"eventbooking_group_required_{Guid.NewGuid():N}";
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Database = databaseName,
            Pooling = false,
        }.ConnectionString;
        try
        {
            await ExecuteAdminAsync(fixture.ConnectionString, $"CREATE DATABASE \"{databaseName}\"");
            await using (var releaseOne = NewContext(connectionString))
            {
                await releaseOne.Database.MigrateAsync(ReleaseOneMigration);
            }
            var candidateId = Guid.NewGuid();
            await ExecuteAsync(connectionString,
                """
                INSERT INTO candidate (id, name, email, status, status_changed_at, employee_group_id)
                VALUES (@id, 'Legacy', 'legacy@example.com', @status, now(), NULL);
                """,
                ("id", candidateId), ("status", (int)CandidateStatus.NotYetInvited));

            await using (var blocked = NewContext(connectionString))
            {
                var error = await Assert.ThrowsAnyAsync<Exception>(() => blocked.Database.MigrateAsync());
                Assert.Contains("Employee Group reconciliation", error.ToString());
            }
            Assert.True(await IsNullableAsync(connectionString));

            await ExecuteAsync(connectionString,
                """
                UPDATE candidate SET employee_group_id = @group_id WHERE id = @id;
                INSERT INTO candidate_requirement (candidate_id, appointment_type_id)
                SELECT @id, appointment_type_id
                FROM employee_group_requirement
                WHERE employee_group_id = @group_id;
                """,
                ("id", candidateId), ("group_id", EmployeeGroupIds.Pilots));
            await using (var reconciled = NewContext(connectionString))
            {
                await reconciled.Database.MigrateAsync();
            }

            Assert.False(await IsNullableAsync(connectionString));
        }
        finally
        {
            await ExecuteAdminAsync(
                fixture.ConnectionString,
                $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)");
        }
    }

    private static EventBookingDbContext NewContext(string connectionString) => new(
        new DbContextOptionsBuilder<EventBookingDbContext>().UseNpgsql(connectionString).Options);

    private static async Task ExecuteAdminAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteAsync(
        string connectionString,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> IsNullableAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT is_nullable = 'YES'
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'candidate'
              AND column_name = 'employee_group_id';
            """, connection);
        return (bool)(await command.ExecuteScalarAsync())!;
    }
}
