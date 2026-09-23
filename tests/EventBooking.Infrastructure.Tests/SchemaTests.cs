using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Slots;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class SchemaTests(PostgresFixture fixture)
{
    private const string ReleaseOneMigration = "20260909120000_AddRecoveryBookings";

    [Fact]
    public async Task CleanDatabaseMigrationsSeedFixedRowsAndAddTheCandidateStatusTimestamp()
    {
        var databaseName = $"eventbooking_seed_{Guid.NewGuid():N}";
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Database = databaseName,
            Pooling = false,
        }.ConnectionString;

        try
        {
            await CreateDatabaseAsync(fixture.ConnectionString, databaseName);

            await using (var context = new EventBookingDbContext(
                             new DbContextOptionsBuilder<EventBookingDbContext>()
                                 .UseNpgsql(connectionString)
                                 .Options))
            {
                await context.Database.MigrateAsync();

                var types = await context.AppointmentTypes.OrderBy(t => t.Code).ToListAsync();
                Assert.Collection(
                    types,
                    type =>
                    {
                        Assert.Equal(AppointmentTypeIds.DrugAndAlcoholTesting, type.Id);
                        Assert.Equal("DAT", type.Code);
                        Assert.Equal("Drug & Alcohol Testing", type.Name);
                    },
                    type =>
                    {
                        Assert.Equal(AppointmentTypeIds.MedicalCheckUp, type.Id);
                        Assert.Equal("MED", type.Code);
                        Assert.Equal("Medical Check-up", type.Name);
                    },
                    type =>
                    {
                        Assert.Equal(AppointmentTypeIds.UniformFitting, type.Id);
                        Assert.Equal("UNI", type.Code);
                        Assert.Equal("Uniform Fitting", type.Name);
                    });

                var settings = await context.SystemSettings.SingleAsync();
                Assert.Equal(1, settings.Id);
                Assert.Equal(4, settings.InviteExpiryDays);
                Assert.Equal(2, settings.MaxAutoRetryCount);
            }

            await using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT data_type
                    FROM information_schema.columns
                    WHERE table_schema = 'public'
                      AND table_name = 'candidate'
                      AND column_name = 'status_changed_at';
                    """;

                Assert.Equal("timestamp with time zone", await command.ExecuteScalarAsync());
            }
        }
        finally
        {
            await DropDatabaseAsync(fixture.ConnectionString, databaseName);
        }
    }

    [Fact]
    public async Task CandidateStatusTimestampMigrationBackfillsExistingCandidatesWithoutLeavingADefault()
    {
        var databaseName = $"eventbooking_status_backfill_{Guid.NewGuid():N}";
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Database = databaseName,
            Pooling = false,
        }.ConnectionString;
        var candidateId = Guid.NewGuid();

        try
        {
            await CreateDatabaseAsync(fixture.ConnectionString, databaseName);

            await using (var initialContext = NewContext(connectionString))
            {
                await initialContext.Database.MigrateAsync("20260905060413_InitialSchema");
            }

            await using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    INSERT INTO candidate (id, name, email, status)
                    VALUES (@candidate_id, 'Legacy Candidate', 'legacy.candidate@mail.com', @status);
                    INSERT INTO candidate_requirement (candidate_id, appointment_type_id)
                    VALUES (@candidate_id, @appointment_type_id);
                    """;
                command.Parameters.AddWithValue("candidate_id", candidateId);
                command.Parameters.AddWithValue("status", (int)CandidateStatus.NotYetInvited);
                command.Parameters.AddWithValue(
                    "appointment_type_id", AppointmentTypeIds.DrugAndAlcoholTesting);
                await command.ExecuteNonQueryAsync();
            }

            await using (var releaseOneContext = NewContext(connectionString))
            {
                await releaseOneContext.Database.MigrateAsync(ReleaseOneMigration);
            }

            await using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    UPDATE candidate SET employee_group_id = @group_id WHERE id = @candidate_id;
                    INSERT INTO candidate_requirement (candidate_id, appointment_type_id)
                    VALUES (@candidate_id, @uniform_type_id);
                    """;
                command.Parameters.AddWithValue("candidate_id", candidateId);
                command.Parameters.AddWithValue("group_id", EmployeeGroupIds.Pilots);
                command.Parameters.AddWithValue(
                    "uniform_type_id", AppointmentTypeIds.UniformFitting);
                await command.ExecuteNonQueryAsync();
            }

            var migrationStartedAt = DateTimeOffset.UtcNow;
            await using (var latestContext = NewContext(connectionString))
            {
                await latestContext.Database.MigrateAsync();
            }
            var migrationFinishedAt = DateTimeOffset.UtcNow;

            await using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT c.status_changed_at, col.column_default
                    FROM candidate AS c
                    CROSS JOIN information_schema.columns AS col
                    WHERE c.id = @candidate_id
                      AND col.table_schema = 'public'
                      AND col.table_name = 'candidate'
                      AND col.column_name = 'status_changed_at';
                    """;
                command.Parameters.AddWithValue("candidate_id", candidateId);

                await using var reader = await command.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.False(reader.IsDBNull(0));
                var stampedAt = reader.GetFieldValue<DateTimeOffset>(0);
                Assert.InRange(stampedAt, migrationStartedAt.AddSeconds(-1), migrationFinishedAt.AddSeconds(1));
                Assert.True(stampedAt > DateTimeOffset.UnixEpoch);
                Assert.True(reader.IsDBNull(1));
            }
        }
        finally
        {
            await DropDatabaseAsync(fixture.ConnectionString, databaseName);
        }
    }

    [Fact]
    public async Task ResetAsyncReseedsTheFixedAppointmentTypes()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var types = await context.AppointmentTypes.OrderBy(t => t.Code).ToListAsync();

        Assert.Equal(3, types.Count);
        Assert.Equal(new[] { "DAT", "MED", "UNI" }, types.Select(t => t.Code));
        Assert.Equal(AppointmentTypeIds.DrugAndAlcoholTesting, types[0].Id);
    }

    [Fact]
    public async Task ResetAsyncReseedsTheDefaultSettingsRow()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var settings = await context.SystemSettings.SingleAsync();

        Assert.Equal(4, settings.InviteExpiryDays);
        Assert.Equal(2, settings.MaxAutoRetryCount);
    }

    [Fact]
    public async Task AProposalRoundTripsWithItsWindowAndAcceptances()
    {
        await fixture.ResetAsync();

        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);

        await using (var write = fixture.NewContext())
        {
            write.SlotProposals.Add(proposal);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var loaded = await read.SlotProposals
            .Include(p => p.Acceptances)
            .SingleAsync(p => p.Id == proposal.Id);

        Assert.Equal(new DateOnly(2026, 9, 10), loaded.Window.Date);
        Assert.Equal(new TimeOnly(9, 0), loaded.Window.StartTime);
        Assert.Equal(new TimeOnly(13, 0), loaded.Window.EndTime);
        Assert.Equal(2, loaded.Acceptances.Count);
        Assert.Equal(10, loaded.Acceptances.Single(
            a => a.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting).Headcount);
    }

    [Fact]
    public async Task AConfirmedSlotRoundTripsWithItsThreeCapacityRows()
    {
        await fixture.ResetAsync();

        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);

        await using (var write = fixture.NewContext())
        {
            write.SlotProposals.Add(proposal);
            write.ConfirmedSlots.Add(slot);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var loaded = await read.ConfirmedSlots
            .Include(s => s.Capacities)
            .SingleAsync(s => s.Id == slot.Id);

        Assert.Equal(3, loaded.Capacities.Count);
        Assert.Equal(6, loaded.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
    }

    [Fact]
    public async Task TheDatabaseRefusesNegativeRemainingCapacity()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var slotId = await CreateSlotWithoutCapacitiesAsync(context);

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO slot_capacity
                    (confirmed_slot_id, appointment_type_id, total_headcount, remaining_capacity)
                VALUES ({0}, {1}, 5, -1);
                """,
                [slotId, AppointmentTypeIds.DrugAndAlcoholTesting]));

        Assert.Contains("ck_slot_capacity_within_bounds", ex.ToString());
    }

    [Fact]
    public async Task TheDatabaseRefusesRemainingCapacityAboveTotalHeadcount()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var slotId = await CreateSlotWithoutCapacitiesAsync(context);

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO slot_capacity
                    (confirmed_slot_id, appointment_type_id, total_headcount, remaining_capacity)
                VALUES ({0}, {1}, 5, 6);
                """,
                [slotId, AppointmentTypeIds.DrugAndAlcoholTesting]));

        Assert.Contains("ck_slot_capacity_within_bounds", ex.ToString());
    }

    private static async Task<Guid> CreateSlotWithoutCapacitiesAsync(EventBookingDbContext context)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 1);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 1);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 1);
        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);

        context.SlotProposals.Add(proposal);
        context.ConfirmedSlots.Add(slot);
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM slot_capacity WHERE confirmed_slot_id = {slot.Id}");

        return slot.Id;
    }

    private static EventBookingDbContext NewContext(string connectionString) =>
        new(new DbContextOptionsBuilder<EventBookingDbContext>()
            .UseNpgsql(connectionString)
            .Options);

    private static async Task CreateDatabaseAsync(string connectionString, string databaseName)
    {
        await using var connection = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(connectionString)
            {
                Database = "postgres",
                Pooling = false,
            }.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE {databaseName};";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(string connectionString, string databaseName)
    {
        await using var connection = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(connectionString)
            {
                Database = "postgres",
                Pooling = false,
            }.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS {databaseName};";
        await command.ExecuteNonQueryAsync();
    }
}
