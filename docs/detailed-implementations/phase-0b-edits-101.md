# 00b — Vocabulary edits 101 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — tests/EventBooking.Infrastructure.Tests/RepositoryTests.cs — 1/1

<!-- vocabulary-file: {"id":346,"oldPath":"tests/EventBooking.Infrastructure.Tests/RepositoryTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/RepositoryTests.cs","beforeSha":"bb8b54304832e8e5d95b23ee4e91b85baed6ed12fb0da7acf93dd9d2372ffcd2","afterSha":"ad943ef3034941f590c2b32cf9a5c3ccb4cf1cb2ede7692a34aba3be3316be3e","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Slots;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class RepositoryTests(PostgresFixture fixture)
{
    [Fact]
    public async Task ActiveSlotsAreFilteredByStatusAndDate()
    {
        await fixture.ResetAsync();

        await using (var write = fixture.NewContext())
        {
            write.SlotProposals.Add(ProposalOn(new DateOnly(2026, 9, 1), out var pastSlot));
            write.ConfirmedSlots.Add(pastSlot);

            write.SlotProposals.Add(ProposalOn(new DateOnly(2026, 9, 20), out var cancelled));
            cancelled.Cancel();
            write.ConfirmedSlots.Add(cancelled);

            write.SlotProposals.Add(ProposalOn(new DateOnly(2026, 9, 21), out var live));
            write.ConfirmedSlots.Add(live);

            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var repository = new ConfirmedSlotRepository(read);

        var result = await repository.ListActiveAsync(new DateOnly(2026, 9, 3), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(new DateOnly(2026, 9, 21), result[0].Window.Date);
        Assert.Equal(3, result[0].Capacities.Count);
    }

    [Fact]
    public async Task ACandidateIsFoundByEmailWithTheirRequirements()
    {
        await fixture.ResetAsync();

        await using (var write = fixture.NewContext())
        {
            var pilots = write.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.Pilots);
            var candidate = Candidate.Create(
                Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", pilots);
            write.Candidates.Add(candidate);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var found = await new CandidateRepository(read)
            .GetByEmailAsync("a.novak@mail.com", CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal("Amara Novak", found!.Name);
        Assert.Equal(2, found.Requirements.Count);
    }

    [Fact]
    public async Task TheSettingsSingletonIsAlwaysThere()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var settings = await new SystemSettingsRepository(context).GetAsync(CancellationToken.None);

        Assert.Equal(4, settings.InviteExpiryDays);
    }

    private static SlotProposal ProposalOn(DateOnly date, out ConfirmedSlot slot)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(date, new TimeOnly(9, 0)), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);
        return proposal;
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/RepositoryTests.cs — 1/1

<!-- vocabulary-file: {"id":346,"oldPath":"tests/EventBooking.Infrastructure.Tests/RepositoryTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/RepositoryTests.cs","beforeSha":"bb8b54304832e8e5d95b23ee4e91b85baed6ed12fb0da7acf93dd9d2372ffcd2","afterSha":"ad943ef3034941f590c2b32cf9a5c3ccb4cf1cb2ede7692a34aba3be3316be3e","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class RepositoryTests(PostgresFixture fixture)
{
    [Fact]
    public async Task ActiveEventsAreFilteredByStatusAndDate()
    {
        await fixture.ResetAsync();

        await using (var write = fixture.NewContext())
        {
            write.EventProposals.Add(ProposalOn(new DateOnly(2026, 9, 1), out var pastEvent));
            write.Events.Add(pastEvent);

            write.EventProposals.Add(ProposalOn(new DateOnly(2026, 9, 20), out var cancelled));
            cancelled.Cancel();
            write.Events.Add(cancelled);

            write.EventProposals.Add(ProposalOn(new DateOnly(2026, 9, 21), out var live));
            write.Events.Add(live);

            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var repository = new EventRepository(read);

        var result = await repository.ListActiveAsync(new DateOnly(2026, 9, 3), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(new DateOnly(2026, 9, 21), result[0].Window.Date);
        Assert.Equal(3, result[0].Capacities.Count);
    }

    [Fact]
    public async Task AAttendeeIsFoundByEmailWithTheirRequirements()
    {
        await fixture.ResetAsync();

        await using (var write = fixture.NewContext())
        {
            var pilots = write.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
            var attendee = Attendee.Create(
                Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", pilots);
            write.Attendees.Add(attendee);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var found = await new AttendeeRepository(read)
            .GetByEmailAsync("a.novak@mail.com", CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal("Amara Novak", found!.Name);
        Assert.Equal(2, found.Requirements.Count);
    }

    [Fact]
    public async Task TheSettingsSingletonIsAlwaysThere()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var settings = await new SystemSettingsRepository(context).GetAsync(CancellationToken.None);

        Assert.Equal(4, settings.InviteExpiryDays);
    }

    private static EventProposal ProposalOn(DateOnly date, out Event eventItem)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(9, 0)), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        return proposal;
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/SchemaTests.cs — 1/1

<!-- vocabulary-file: {"id":347,"oldPath":"tests/EventBooking.Infrastructure.Tests/SchemaTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/SchemaTests.cs","beforeSha":"3b9a4949b57b56f261033e124cefffe61b9d19ef8425eb32c126e71bf0718135","afterSha":"953495c69ab5b3bc95c9207b894cfa4a4e82096ee3258d95fd6df4b3c63b8f87","side":"before","part":1,"parts":1} -->

`````csharp
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
`````

## after — tests/EventBooking.Infrastructure.Tests/SchemaTests.cs — 1/1

<!-- vocabulary-file: {"id":347,"oldPath":"tests/EventBooking.Infrastructure.Tests/SchemaTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/SchemaTests.cs","beforeSha":"3b9a4949b57b56f261033e124cefffe61b9d19ef8425eb32c126e71bf0718135","afterSha":"953495c69ab5b3bc95c9207b894cfa4a4e82096ee3258d95fd6df4b3c63b8f87","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class SchemaTests(PostgresFixture fixture)
{
    private const string ReleaseOneMigration = "20260909120000_AddRecoveryBookings";

    [Fact]
    public async Task CleanDatabaseMigrationsSeedFixedRowsAndAddTheAttendeeStatusTimestamp()
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
                      AND table_name = 'attendee'
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
    public async Task AttendeeStatusTimestampMigrationBackfillsExistingAttendeesWithoutLeavingADefault()
    {
        var databaseName = $"eventbooking_status_backfill_{Guid.NewGuid():N}";
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Database = databaseName,
            Pooling = false,
        }.ConnectionString;
        var attendeeId = Guid.NewGuid();

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
                    INSERT INTO attendee (id, name, email, status)
                    VALUES (@attendee_id, 'Legacy Attendee', 'legacy.attendee@mail.com', @status);
                    INSERT INTO attendee_requirement (attendee_id, appointment_type_id)
                    VALUES (@attendee_id, @appointment_type_id);
                    """;
                command.Parameters.AddWithValue("attendee_id", attendeeId);
                command.Parameters.AddWithValue("status", (int)AttendeeStatus.NotYetInvited);
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
                    UPDATE attendee SET attendee_group_id = @group_id WHERE id = @attendee_id;
                    INSERT INTO attendee_requirement (attendee_id, appointment_type_id)
                    VALUES (@attendee_id, @uniform_type_id);
                    """;
                command.Parameters.AddWithValue("attendee_id", attendeeId);
                command.Parameters.AddWithValue("group_id", AttendeeGroupIds.Pilots);
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
                    FROM attendee AS c
                    CROSS JOIN information_schema.columns AS col
                    WHERE c.id = @attendee_id
                      AND col.table_schema = 'public'
                      AND col.table_name = 'attendee'
                      AND col.column_name = 'status_changed_at';
                    """;
                command.Parameters.AddWithValue("attendee_id", attendeeId);

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

        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);

        await using (var write = fixture.NewContext())
        {
            write.EventProposals.Add(proposal);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var loaded = await read.EventProposals
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
    public async Task AEventRoundTripsWithItsThreeCapacityRows()
    {
        await fixture.ResetAsync();

        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        await using (var write = fixture.NewContext())
        {
            write.EventProposals.Add(proposal);
            write.Events.Add(eventItem);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var loaded = await read.Events
            .Include(s => s.Capacities)
            .SingleAsync(s => s.Id == eventItem.Id);

        Assert.Equal(3, loaded.Capacities.Count);
        Assert.Equal(6, loaded.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
    }

    [Fact]
    public async Task TheDatabaseRefusesNegativeRemainingCapacity()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var eventId = await CreateEventWithoutCapacitiesAsync(context);

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO event_capacity
                    (event_id, appointment_type_id, total_headcount, remaining_capacity)
                VALUES ({0}, {1}, 5, -1);
                """,
                [eventId, AppointmentTypeIds.DrugAndAlcoholTesting]));

        Assert.Contains("ck_event_capacity_within_bounds", ex.ToString());
    }

    [Fact]
    public async Task TheDatabaseRefusesRemainingCapacityAboveTotalHeadcount()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var eventId = await CreateEventWithoutCapacitiesAsync(context);

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO event_capacity
                    (event_id, appointment_type_id, total_headcount, remaining_capacity)
                VALUES ({0}, {1}, 5, 6);
                """,
                [eventId, AppointmentTypeIds.DrugAndAlcoholTesting]));

        Assert.Contains("ck_event_capacity_within_bounds", ex.ToString());
    }

    private static async Task<Guid> CreateEventWithoutCapacitiesAsync(EventBookingDbContext context)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
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
`````

## before — tests/EventBooking.Infrastructure.Tests/SlotCapacityRepositoryTests.cs — 1/1

<!-- vocabulary-file: {"id":348,"oldPath":"tests/EventBooking.Infrastructure.Tests/SlotCapacityRepositoryTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/EventCapacityRepositoryTests.cs","beforeSha":"1711e4c04b656c1e6770ded1e0987d8b156c92369a33b7257a2bed8936907d09","afterSha":"fc9971aebdb3e6449910e1a349b3d23095e10cbf69ed1a90ed7943b9c829dc98","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Slots;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class SlotCapacityRepositoryTests(PostgresFixture fixture)
{
    [Fact]
    public async Task OnlyTheRequestedTypesAreReturnedAndTheyComeBackInOrder()
    {
        var slotId = await GivenASlot();

        await using var context = fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var locked = await new SlotCapacityRepository(context).LockForUpdateAsync(
            slotId,
            [AppointmentTypeIds.UniformFitting, AppointmentTypeIds.DrugAndAlcoholTesting],
            CancellationToken.None);

        Assert.Equal(2, locked.Count);
        Assert.Equal(
            locked.Select(c => c.AppointmentTypeId).OrderBy(id => id),
            locked.Select(c => c.AppointmentTypeId));
        Assert.DoesNotContain(AppointmentTypeIds.MedicalCheckUp, locked.Select(c => c.AppointmentTypeId));
    }

    [Fact]
    public async Task TheReturnedRowsAreTrackedSoDecrementsPersist()
    {
        var slotId = await GivenASlot();

        await using (var context = fixture.NewContext())
        {
            await using var transaction = await context.Database.BeginTransactionAsync();

            var locked = await new SlotCapacityRepository(context).LockForUpdateAsync(
                slotId, [AppointmentTypeIds.DrugAndAlcoholTesting], CancellationToken.None);

            locked.Single().Decrement();
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        await using var read = fixture.NewContext();
        var slot = await read.ConfirmedSlots.Include(s => s.Capacities).SingleAsync(s => s.Id == slotId);
        Assert.Equal(9, slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
    }

    [Fact]
    public async Task ASecondTransactionWaitsOnTheCapacityRowLockBeforeItCanComplete()
    {
        var slotId = await GivenASlot();

        await using var first = fixture.NewContext();
        await using var firstTransaction = await first.Database.BeginTransactionAsync();
        await new SlotCapacityRepository(first).LockForUpdateAsync(
            slotId, [AppointmentTypeIds.DrugAndAlcoholTesting], CancellationToken.None);

        var waitingBackend = NewBarrier();
        var secondLock = LockCapacityAsync(slotId, waitingBackend);
        var secondPid = await waitingBackend.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await WaitUntilBlockedOnDatabaseLockAsync(first, secondPid);
        await firstTransaction.CommitAsync();

        var locked = await secondLock.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(slotId, locked.ConfirmedSlotId);
        Assert.Equal(AppointmentTypeIds.DrugAndAlcoholTesting, locked.AppointmentTypeId);
    }

    [Fact]
    public async Task AnUnknownSlotReturnsNothingRatherThanThrowing()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var locked = await new SlotCapacityRepository(context).LockForUpdateAsync(
            Guid.NewGuid(), AppointmentTypeIds.All, CancellationToken.None);

        Assert.Empty(locked);
    }

    private async Task<Guid> GivenASlot()
    {
        await fixture.ResetAsync();

        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);

        await using var context = fixture.NewContext();
        context.SlotProposals.Add(proposal);
        context.ConfirmedSlots.Add(slot);
        await context.SaveChangesAsync();

        return slot.Id;
    }

    private async Task<SlotCapacity> LockCapacityAsync(
        Guid slotId,
        TaskCompletionSource<int> waitingBackend)
    {
        await using var context = fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        waitingBackend.SetResult(await GetBackendPidAsync(context));

        var locked = await new SlotCapacityRepository(context).LockForUpdateAsync(
            slotId, [AppointmentTypeIds.DrugAndAlcoholTesting], CancellationToken.None);
        await transaction.CommitAsync();

        return Assert.Single(locked);
    }

    private static TaskCompletionSource<int> NewBarrier() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task<int> GetBackendPidAsync(EventBookingDbContext context)
    {
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = (NpgsqlTransaction)context.Database.CurrentTransaction!
            .GetDbTransaction();
        command.CommandText = "SELECT pg_backend_pid();";
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static async Task WaitUntilBlockedOnDatabaseLockAsync(
        EventBookingDbContext lockOwner,
        int waitingBackendPid)
    {
        var connection = (NpgsqlConnection)lockOwner.Database.GetDbConnection();

        for (var attempt = 0; attempt < 200; attempt++)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (NpgsqlTransaction)lockOwner.Database.CurrentTransaction!
                .GetDbTransaction();
            command.CommandText =
                "SELECT wait_event_type FROM pg_stat_activity WHERE pid = @waiting_backend_pid;";
            command.Parameters.AddWithValue("waiting_backend_pid", waitingBackendPid);

            if (string.Equals(
                    await command.ExecuteScalarAsync() as string,
                    "Lock",
                    StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.Fail($"PostgreSQL backend {waitingBackendPid} never waited on the capacity row lock.");
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/EventCapacityRepositoryTests.cs — 1/1

<!-- vocabulary-file: {"id":348,"oldPath":"tests/EventBooking.Infrastructure.Tests/SlotCapacityRepositoryTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/EventCapacityRepositoryTests.cs","beforeSha":"1711e4c04b656c1e6770ded1e0987d8b156c92369a33b7257a2bed8936907d09","afterSha":"fc9971aebdb3e6449910e1a349b3d23095e10cbf69ed1a90ed7943b9c829dc98","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class EventCapacityRepositoryTests(PostgresFixture fixture)
{
    [Fact]
    public async Task OnlyTheRequestedTypesAreReturnedAndTheyComeBackInOrder()
    {
        var eventId = await GivenAEvent();

        await using var context = fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var locked = await new EventCapacityRepository(context).LockForUpdateAsync(
            eventId,
            [AppointmentTypeIds.UniformFitting, AppointmentTypeIds.DrugAndAlcoholTesting],
            CancellationToken.None);

        Assert.Equal(2, locked.Count);
        Assert.Equal(
            locked.Select(c => c.AppointmentTypeId).OrderBy(id => id),
            locked.Select(c => c.AppointmentTypeId));
        Assert.DoesNotContain(AppointmentTypeIds.MedicalCheckUp, locked.Select(c => c.AppointmentTypeId));
    }

    [Fact]
    public async Task TheReturnedRowsAreTrackedSoDecrementsPersist()
    {
        var eventId = await GivenAEvent();

        await using (var context = fixture.NewContext())
        {
            await using var transaction = await context.Database.BeginTransactionAsync();

            var locked = await new EventCapacityRepository(context).LockForUpdateAsync(
                eventId, [AppointmentTypeIds.DrugAndAlcoholTesting], CancellationToken.None);

            locked.Single().Decrement();
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        await using var read = fixture.NewContext();
        var eventItem = await read.Events.Include(s => s.Capacities).SingleAsync(s => s.Id == eventId);
        Assert.Equal(9, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
    }

    [Fact]
    public async Task ASecondTransactionWaitsOnTheCapacityRowLockBeforeItCanComplete()
    {
        var eventId = await GivenAEvent();

        await using var first = fixture.NewContext();
        await using var firstTransaction = await first.Database.BeginTransactionAsync();
        await new EventCapacityRepository(first).LockForUpdateAsync(
            eventId, [AppointmentTypeIds.DrugAndAlcoholTesting], CancellationToken.None);

        var waitingBackend = NewBarrier();
        var secondLock = LockCapacityAsync(eventId, waitingBackend);
        var secondPid = await waitingBackend.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await WaitUntilBlockedOnDatabaseLockAsync(first, secondPid);
        await firstTransaction.CommitAsync();

        var locked = await secondLock.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(eventId, locked.EventId);
        Assert.Equal(AppointmentTypeIds.DrugAndAlcoholTesting, locked.AppointmentTypeId);
    }

    [Fact]
    public async Task AnUnknownEventReturnsNothingRatherThanThrowing()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var locked = await new EventCapacityRepository(context).LockForUpdateAsync(
            Guid.NewGuid(), AppointmentTypeIds.All, CancellationToken.None);

        Assert.Empty(locked);
    }

    private async Task<Guid> GivenAEvent()
    {
        await fixture.ResetAsync();

        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        await using var context = fixture.NewContext();
        context.EventProposals.Add(proposal);
        context.Events.Add(eventItem);
        await context.SaveChangesAsync();

        return eventItem.Id;
    }

    private async Task<EventCapacity> LockCapacityAsync(
        Guid eventId,
        TaskCompletionSource<int> waitingBackend)
    {
        await using var context = fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        waitingBackend.SetResult(await GetBackendPidAsync(context));

        var locked = await new EventCapacityRepository(context).LockForUpdateAsync(
            eventId, [AppointmentTypeIds.DrugAndAlcoholTesting], CancellationToken.None);
        await transaction.CommitAsync();

        return Assert.Single(locked);
    }

    private static TaskCompletionSource<int> NewBarrier() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task<int> GetBackendPidAsync(EventBookingDbContext context)
    {
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = (NpgsqlTransaction)context.Database.CurrentTransaction!
            .GetDbTransaction();
        command.CommandText = "SELECT pg_backend_pid();";
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static async Task WaitUntilBlockedOnDatabaseLockAsync(
        EventBookingDbContext lockOwner,
        int waitingBackendPid)
    {
        var connection = (NpgsqlConnection)lockOwner.Database.GetDbConnection();

        for (var attempt = 0; attempt < 200; attempt++)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (NpgsqlTransaction)lockOwner.Database.CurrentTransaction!
                .GetDbTransaction();
            command.CommandText =
                "SELECT wait_event_type FROM pg_stat_activity WHERE pid = @waiting_backend_pid;";
            command.Parameters.AddWithValue("waiting_backend_pid", waitingBackendPid);

            if (string.Equals(
                    await command.ExecuteScalarAsync() as string,
                    "Lock",
                    StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.Fail($"PostgreSQL backend {waitingBackendPid} never waited on the capacity row lock.");
    }
}
`````
