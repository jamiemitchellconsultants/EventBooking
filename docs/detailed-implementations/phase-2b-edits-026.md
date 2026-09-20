# 02b — One fresh schema, and the roles that keep the audit trail append-only, edits 26 (Task 9b)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.Infrastructure.Tests/SchemaTests.cs — 1/1

<!-- retirement-file: {"id":63,"file":"tests/EventBooking.Infrastructure.Tests/SchemaTests.cs","beforeSha":"a13870b242c63abae438fe324bee4bbac5ac58f5846268b6e07ef581df52ffc8","afterSha":"549b746d8696a6c176578af08f6f0927b7bda91f5bc09b05538bc331c604b136","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using EventBooking.Domain.Locations;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventBooking.Infrastructure.Tests;

/// <summary>
/// The schema is one migration against a real PostgreSQL 16. Every constraint here is one the
/// application must not be able to violate even with a direct connection.
/// </summary>
[Collection("postgres")]
public class SchemaTests(PostgresFixture fixture)
{
    private const string CapacityBounds = "ck_event_capacity_bounds";

    [Fact]
    public async Task TheSchemaIsOneMigrationThatAppliesToAnEmptyDatabase()
    {
        var databaseName = $"eventbooking_fresh_{Guid.NewGuid():N}";
        var connectionString = ConnectionTo(databaseName);

        try
        {
            await CreateDatabaseAsync(databaseName);
            await ApplyRolesScriptAsync(connectionString);

            await using var context = NewContext(connectionString);
            Assert.Equal(
                ["20260920120000_InitialSchema"],
                (await context.Database.GetPendingMigrationsAsync()).ToArray());

            await context.Database.MigrateAsync();

            Assert.Empty(await context.Database.GetPendingMigrationsAsync());
            var types = await context.AppointmentTypes.OrderBy(t => t.Code).ToListAsync();
            Assert.Equal(["DAT", "MED", "UNI"], types.Select(t => t.Code));
            Assert.Equal(1, (await context.SystemSettings.SingleAsync()).Id);
        }
        finally
        {
            await DropDatabaseAsync(databaseName);
        }
    }

    [Theory]
    [InlineData(5, -1)]
    [InlineData(5, 6)]
    [InlineData(0, 0)]
    public async Task TheDatabaseRefusesACapacityRowOutsideItsBounds(int total, int remaining)
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var eventId = await CreateEventWithoutCapacitiesAsync(context);

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO event_capacity
                    (event_id, appointment_type_id, total_headcount, remaining_capacity)
                VALUES ({0}, {1}, {2}, {3});
                """,
                [eventId, AppointmentTypeIds.DrugAndAlcoholTesting, total, remaining]));

        Assert.Contains(CapacityBounds, ex.ToString());
    }

    [Fact]
    public async Task TheDatabaseRefusesASecondEventForOneProposal()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var eventId = await CreateEventWithoutCapacitiesAsync(context);
        var proposalId = await context.Events.Where(e => e.Id == eventId)
            .Select(e => e.ProposalId).SingleAsync();

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO event
                    (id, proposal_id, location_id, status, date, start_time, duration_minutes, start_utc)
                SELECT {0}, proposal_id, location_id, status, date, start_time, duration_minutes, start_utc
                  FROM event WHERE id = {1};
                """,
                [Guid.NewGuid(), eventId]));

        Assert.Contains("proposal_id", ex.ToString());
    }

    [Fact]
    public async Task TheDatabaseRefusesASecondManagerForOneAppointmentType()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var typeId = AppointmentTypeIds.MedicalCheckUp;
        await InsertManagerProfileAsync(context, typeId);

        var ex = await Assert.ThrowsAnyAsync<Exception>(
            () => InsertManagerProfileAsync(context, typeId));

        Assert.Contains("ux_staff_access_profile_manager_appointment_type", ex.ToString());
    }

    [Fact]
    public async Task TheApplicationRoleMayAppendToTheAuditTrailButNeverRewriteIt()
    {
        await fixture.ResetAsync();

        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, "SET ROLE eventbooking_app;");

        await ExecuteAsync(
            connection,
            """
            INSERT INTO audit_log (id, entity_type, entity_id, action, actor_type, actor_id, timestamp)
            VALUES (gen_random_uuid(), 'Event', gen_random_uuid(), 0, 2, 'schema-test', now());
            """);
        Assert.Equal(1L, await ScalarAsync(connection, "SELECT count(*) FROM audit_log;"));

        var update = await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteAsync(connection, "UPDATE audit_log SET actor_id = 'rewritten';"));
        Assert.Equal("42501", update.SqlState);

        var delete = await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteAsync(connection, "DELETE FROM audit_log;"));
        Assert.Equal("42501", delete.SqlState);
    }

    [Fact]
    public async Task TheApplicationRoleKeepsFullDmlOnEveryOtherTable()
    {
        await fixture.ResetAsync();

        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, "SET ROLE eventbooking_app;");

        await ExecuteAsync(
            connection,
            """
            INSERT INTO location (id, code, name, address, time_zone_id, is_active, version)
            VALUES (gen_random_uuid(), 'ROLE', 'Role check', 'Somewhere', 'Europe/London', true, 1);
            UPDATE location SET name = 'Renamed' WHERE code = 'ROLE';
            DELETE FROM location WHERE code = 'ROLE';
            """);

        Assert.Equal(0L, await ScalarAsync(connection, "SELECT count(*) FROM location WHERE code = 'ROLE';"));
    }

    [Fact]
    public async Task AStaleVersionOnALocationIsARefusedWrite()
    {
        await fixture.ResetAsync();

        var location = Location.Create(
            Guid.NewGuid(), "STALE", "Stale check", "Somewhere", "Europe/London", new NodaTimeEventWindowZones());
        await using (var seed = fixture.NewContext())
        {
            seed.Add(location);
            await seed.SaveChangesAsync();
        }

        await using var first = fixture.NewContext();
        await using var second = fixture.NewContext();
        var readByFirst = await first.Set<Location>().SingleAsync(l => l.Id == location.Id);
        var readBySecond = await second.Set<Location>().SingleAsync(l => l.Id == location.Id);

        readByFirst.Rename("Renamed first");
        await first.SaveChangesAsync();

        readBySecond.Rename("Renamed second");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Theory]
    [InlineData("location", "code")]
    [InlineData("appointment_type", "code")]
    [InlineData("attendee_group", "code")]
    public async Task ACodeIsUniqueWithinItsReferenceTable(string table, string column)
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var indexes = await IndexDefinitionsAsync(context, table);

        Assert.Contains(
            indexes,
            definition => definition.Contains("UNIQUE", StringComparison.Ordinal)
                && definition.Contains($"({column})", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnAttendeeEmailIsUniqueIgnoringCase()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var indexes = await IndexDefinitionsAsync(context, "attendee");

        Assert.Contains(
            indexes,
            definition => definition.Contains("UNIQUE", StringComparison.Ordinal)
                && definition.Contains("lower(", StringComparison.Ordinal)
                && definition.Contains("email", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheEligibilityQueryHasItsCoveringIndex()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var indexes = await IndexDefinitionsAsync(context, "event");

        Assert.Contains(
            indexes,
            definition => definition.Contains("(status, location_id, start_utc)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ADeliveryAttemptCarriesItsClaimAndItsClaimCount()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var columns = await ColumnNamesAsync(context, "email_log");

        Assert.Contains("claimed_at", columns);
        Assert.Contains("claim_count", columns);
    }

    private static async Task InsertManagerProfileAsync(EventBookingDbContext context, Guid typeId) =>
        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO staff_access_profile
                (staff_user_id, is_admin, is_coordinator, is_manager, is_appointment_staff,
                 appointment_type_id, version)
            VALUES ({0}, false, false, true, false, {1}, 1);
            """,
            [Guid.NewGuid(), typeId]);

    private static async Task<IReadOnlyList<string>> IndexDefinitionsAsync(
        EventBookingDbContext context, string table)
    {
        var rows = new List<string>();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        await context.Database.OpenConnectionAsync();
        command.CommandText =
            $"SELECT indexdef FROM pg_indexes WHERE schemaname = 'public' AND tablename = '{table}';";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<string>> ColumnNamesAsync(
        EventBookingDbContext context, string table)
    {
        var rows = new List<string>();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        await context.Database.OpenConnectionAsync();
        command.CommandText =
            $"""
            SELECT column_name FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = '{table}';
            """;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    private async Task ApplyRolesScriptAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, DatabaseRoles.Script);
    }

    private static async Task<Guid> CreateEventWithoutCapacitiesAsync(EventBookingDbContext context)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 1);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 1);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 1);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        context.EventProposals.Add(proposal);
        context.Events.Add(eventItem);
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM event_capacity WHERE event_id = {eventItem.Id}");

        return eventItem.Id;
    }

    private string ConnectionTo(string databaseName) =>
        new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Database = databaseName,
            Pooling = false,
        }.ConnectionString;

    private static EventBookingDbContext NewContext(string connectionString) =>
        new(new DbContextOptionsBuilder<EventBookingDbContext>()
            .UseNpgsql(connectionString)
            .Options);

    private async Task CreateDatabaseAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
            {
                Database = "postgres",
                Pooling = false,
            }.ConnectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, $"CREATE DATABASE {databaseName};");
    }

    private async Task DropDatabaseAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
            {
                Database = "postgres",
                Pooling = false,
            }.ConnectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, $"DROP DATABASE IF EXISTS {databaseName};");
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/StaffAccessMigrationTests.cs — 1/1

<!-- retirement-file: {"id":64,"file":"tests/EventBooking.Infrastructure.Tests/StaffAccessMigrationTests.cs","beforeSha":"868b60f2b01712c06d3338a9733bf99ab1236ecee56110d1cd81386e057e9e29","afterSha":null,"side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class StaffAccessMigrationTests(PostgresFixture fixture)
{
    private const string LegacyMigration = "20260905192057_MakeEventProposalIdNullable";

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
`````

## before — tests/EventBooking.Infrastructure.Tests/StaffAccessProfileMigrationRegressionTests.cs — 1/1

<!-- retirement-file: {"id":65,"file":"tests/EventBooking.Infrastructure.Tests/StaffAccessProfileMigrationRegressionTests.cs","beforeSha":"9bc55a69af59ee1e34afbca13a6d1f9392baaae366ab0dc9068ffe21618bf040","afterSha":null,"side":"before","part":1,"parts":1} -->

`````csharp
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
`````
