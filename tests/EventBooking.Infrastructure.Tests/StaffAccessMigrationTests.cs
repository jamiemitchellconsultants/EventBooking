using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class StaffAccessMigrationTests(PostgresFixture fixture)
{
    private const string LegacyMigration = "20260905192057_MakeConfirmedSlotProposalIdNullable";

    [Fact]
    public async Task LegacyRowsBecomeVersionOneProfilesAndLegacyStateIsRemoved()
    {
        var databaseName = $"eventbooking_access_{Guid.NewGuid():N}";
        var connectionString = await CreateLegacyDatabaseAsync(databaseName);
        var admin = Guid.NewGuid();
        var coordinator = Guid.NewGuid();
        var manager = Guid.NewGuid();

        try
        {
            await ExecuteAsync(
                connectionString,
                """
                INSERT INTO user_role_assignment (entra_object_id, role, appointment_type_id)
                VALUES (@admin, 3, NULL), (@coordinator, 2, NULL), (@manager, 1, @type);
                UPDATE appointment_type SET manager_user_id = @manager WHERE id = @type;
                """,
                ("admin", admin),
                ("coordinator", coordinator),
                ("manager", manager),
                ("type", AppointmentTypeIds.MedicalCheckUp));

            await using (var latest = NewContext(connectionString))
            {
                await latest.Database.MigrateAsync();
            }

            await using var read = NewContext(connectionString);
            var profiles = await read.StaffAccessProfiles
                .OrderBy(profile => profile.StaffUserId)
                .ToListAsync();

            Assert.Equal(3, profiles.Count);
            Assert.True(profiles.Single(profile => profile.StaffUserId == admin).IsAdmin);
            Assert.True(profiles.Single(profile => profile.StaffUserId == coordinator).IsCoordinator);
            var migratedManager = profiles.Single(profile => profile.StaffUserId == manager);
            Assert.True(migratedManager.IsManager);
            Assert.Equal(AppointmentTypeIds.MedicalCheckUp, migratedManager.AppointmentTypeId);
            Assert.All(profiles, profile => Assert.Equal(1, profile.Version));

            Assert.False(await TableExistsAsync(connectionString, "user_role_assignment"));
            Assert.False(await ColumnExistsAsync(
                connectionString, "appointment_type", "manager_user_id"));
        }
        finally
        {
            await DropDatabaseAsync(databaseName);
        }
    }

    [Fact]
    public async Task ConflictingLegacyManagerRepresentationsAbortBeforeDroppingData()
    {
        var databaseName = $"eventbooking_access_mismatch_{Guid.NewGuid():N}";
        var connectionString = await CreateLegacyDatabaseAsync(databaseName);

        try
        {
            await ExecuteAsync(
                connectionString,
                """
                INSERT INTO user_role_assignment (entra_object_id, role, appointment_type_id)
                VALUES (@assignment_manager, 1, @type);
                UPDATE appointment_type SET manager_user_id = @entity_manager WHERE id = @type;
                """,
                ("assignment_manager", Guid.NewGuid()),
                ("entity_manager", Guid.NewGuid()),
                ("type", AppointmentTypeIds.UniformFitting));

            await using var latest = NewContext(connectionString);
            var exception = await Assert.ThrowsAnyAsync<Exception>(
                () => latest.Database.MigrateAsync());

            Assert.Contains("legacy manager representations disagree", exception.ToString());
            Assert.True(await TableExistsAsync(connectionString, "user_role_assignment"));
        }
        finally
        {
            await DropDatabaseAsync(databaseName);
        }
    }

    private async Task<string> CreateLegacyDatabaseAsync(string databaseName)
    {
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Database = databaseName,
            Pooling = false,
        }.ConnectionString;

        await using (var connection = new NpgsqlConnection(fixture.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            await command.ExecuteNonQueryAsync();
        }

        await using var legacy = NewContext(connectionString);
        await legacy.Database.MigrateAsync(LegacyMigration);
        return connectionString;
    }

    private static EventBookingDbContext NewContext(string connectionString) => new(
        new DbContextOptionsBuilder<EventBookingDbContext>()
            .UseNpgsql(connectionString)
            .Options);

    private static async Task ExecuteAsync(
        string connectionString,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> TableExistsAsync(string connectionString, string table)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables "
            + "WHERE table_schema = 'public' AND table_name = @name)";
        command.Parameters.AddWithValue("name", table);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<bool> ColumnExistsAsync(
        string connectionString,
        string table,
        string column)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT EXISTS (SELECT 1 FROM information_schema.columns "
            + "WHERE table_schema = 'public' AND table_name = @table AND column_name = @column)";
        command.Parameters.AddWithValue("table", table);
        command.Parameters.AddWithValue("column", column);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private async Task DropDatabaseAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }
}
