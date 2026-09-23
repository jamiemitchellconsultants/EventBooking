using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Proves the scope-constraint migration widens valid state without changing old rows.</summary>
[Collection("postgres")]
public sealed class StaffAccessProfileMigrationRegressionTests(PostgresFixture fixture)
{
    private const string Migration = "20260911120000_RelaxStaffAccessProfileScopeConstraint";

    /// <summary>Applies the migration over representative Issue #71 rows and preserves them exactly.</summary>
    [Fact]
    public async Task ExistingProfilesSurviveAndOnlyTheIntendedConstraintDirectionRelaxes()
    {
        var databaseName = $"eventbooking_role_scope_{Guid.NewGuid():N}";
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Database = databaseName,
            Pooling = false,
        }.ConnectionString;
        var admin = Guid.NewGuid();
        var coordinator = Guid.NewGuid();
        var manager = Guid.NewGuid();
        var combined = Guid.NewGuid();

        try
        {
            await CreateDatabaseAsync(databaseName);
            await using (var previous = NewContext(connectionString))
            {
                var migrations = previous.Database.GetMigrations().ToList();
                var index = migrations.IndexOf(Migration);
                Assert.True(index > 0, $"Cannot locate the migration before {Migration}.");
                await previous.Database.MigrateAsync(migrations[index - 1]);
            }

            await ExecuteAsync(
                connectionString,
                """
                INSERT INTO staff_access_profile
                    (staff_user_id, is_admin, is_coordinator, is_manager,
                     is_appointment_staff, appointment_type_id, version)
                VALUES
                    (@admin, true, false, false, false, NULL, 4),
                    (@coordinator, false, true, false, false, NULL, 2),
                    (@manager, false, false, true, false, @medical, 7),
                    (@combined, false, true, true, true, @uniform, 9);
                """,
                ("admin", admin),
                ("coordinator", coordinator),
                ("manager", manager),
                ("combined", combined),
                ("medical", AppointmentTypeIds.MedicalCheckUp),
                ("uniform", AppointmentTypeIds.UniformFitting));

            await using (var latest = NewContext(connectionString))
            {
                await latest.Database.MigrateAsync();
                var profiles = await latest.StaffAccessProfiles
                    .OrderBy(value => value.StaffUserId)
                    .ToListAsync();

                Assert.Equal(4, profiles.Count);
                Assert.Equal(4, profiles.Single(value => value.StaffUserId == admin).Version);
                Assert.True(profiles.Single(value => value.StaffUserId == coordinator).IsCoordinator);
                Assert.Equal(
                    AppointmentTypeIds.MedicalCheckUp,
                    profiles.Single(value => value.StaffUserId == manager).AppointmentTypeId);
                Assert.Equal(
                    [Role.Manager, Role.Coordinator, Role.AppointmentStaff],
                    profiles.Single(value => value.StaffUserId == combined).Roles.OrderBy(value => value));

                latest.StaffAccessProfiles.Add(StaffAccessProfile.Create(
                    Guid.NewGuid(), [Role.Manager], null));
                await latest.SaveChangesAsync();
            }

            await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
                connectionString,
                """
                INSERT INTO staff_access_profile
                    (staff_user_id, is_admin, is_coordinator, is_manager,
                     is_appointment_staff, appointment_type_id, version)
                VALUES (@id, false, true, false, false, @type, 1);
                """,
                ("id", Guid.NewGuid()),
                ("type", AppointmentTypeIds.DrugAndAlcoholTesting)));
        }
        finally
        {
            await DropDatabaseAsync(databaseName);
        }
    }

    private static EventBookingDbContext NewContext(string connectionString) => new(
        new DbContextOptionsBuilder<EventBookingDbContext>()
            .UseNpgsql(connectionString)
            .Options);

    private async Task CreateDatabaseAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
        await command.ExecuteNonQueryAsync();
    }

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

    private async Task DropDatabaseAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }
}
