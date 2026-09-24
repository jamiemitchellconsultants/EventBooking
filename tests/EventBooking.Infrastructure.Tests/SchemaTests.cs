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
    public async Task TheSchemaAppliesToAnEmptyDatabaseFromItsMigrations()
    {
        var databaseName = $"eventbooking_fresh_{Guid.NewGuid():N}";
        var connectionString = ConnectionTo(databaseName);

        try
        {
            await CreateDatabaseAsync(databaseName);
            await ApplyRolesScriptAsync(connectionString);

            await using var context = NewContext(connectionString);
            // One initial schema, the migration that makes the derived start instant
            // required once Task 11's repository computes it, the migration adding the
            // invite settings snapshots, the migration adding the outbox backoff and
            // correlation columns, the migration replacing the attendee status
            // index with the list page's composite, the migration adding the
            // Idempotency-Key retention table, the migration retaining the replayed
            // response's Location and content type, and the migration retiring the
            // predecessor's fixed reference seeds. Committed migrations are never
            // rewritten: the chain is what keeps the schema regenerable.
            Assert.Equal(
                ["20260920120000_InitialSchema", "20260920145721_RequireEventStartInstant",
                    "20260923202304_InviteSettingsSnapshots", "20260924040652_EmailOutboxColumns",
                    "20260924045221_AttendeeListIndex", "20260924095643_IdempotencyRetention",
                    "20260924123603_IdempotencyReplayHeaders", "20260924201133_RetireTransitionalSeedRows"],
                (await context.Database.GetPendingMigrationsAsync()).ToArray());

            await context.Database.MigrateAsync();

            Assert.Empty(await context.Database.GetPendingMigrationsAsync());
            Assert.Empty(await context.AppointmentTypes.ToListAsync());
            Assert.Empty(await context.AttendeeGroups.ToListAsync());
            Assert.Empty(await context.Locations.ToListAsync());
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
    public async Task OpenProposalsAtDifferentLocationsMayShareALocalWindow()
    {
        await fixture.ResetAsync();

        var listedTypes = new[]
        {
            new ProposableAppointmentType(AppointmentTypeIds.DrugAndAlcoholTesting, "DAT", true, true),
            new ProposableAppointmentType(AppointmentTypeIds.MedicalCheckUp, "MED", true, true),
        };
        EventProposal ProposalAt(Guid locationId) => EventProposal.Propose(
            Guid.NewGuid(), locationId, true, ProposalFixture.TimeZoneId,
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            ProposalFixture.Zones, ProposalFixture.Now, listedTypes,
            AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 1);

        await using (var write = fixture.NewContext())
        {
            write.EventProposals.AddRange(
                ProposalAt(ProposalFixture.LocationId), ProposalAt(Guid.NewGuid()));
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        Assert.Equal(2, await read.EventProposals.CountAsync());
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
    public async Task TheDatabaseRefusesAnEventWithoutAStartInstant()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var eventId = await CreateEventWithoutCapacitiesAsync(context);

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE event SET start_utc = NULL WHERE id = {0};",
                [eventId]));

        Assert.Contains("start_utc", ex.ToString(), StringComparison.Ordinal);
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
