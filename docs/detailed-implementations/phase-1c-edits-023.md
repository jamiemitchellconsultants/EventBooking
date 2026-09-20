# 01c — Negotiation across any number of types, edits 23 (Task 6)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — tests/EventBooking.Infrastructure.Tests/RepositoryTests.cs — 1/1

<!-- retirement-file: {"id":72,"file":"tests/EventBooking.Infrastructure.Tests/RepositoryTests.cs","beforeSha":"e15b8189a80d840f400d2cb3a7d52442f0319d014b14cc383f247abf0269def7","afterSha":"69136955d0a89fe1869c0e88a473056aa9d77ee41ceead6ed5f7ecfdf706238c","side":"before","part":1,"parts":1} -->

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

        Assert.Equal(7, settings.InviteExpiryDays);
        Assert.Equal(3, settings.InviteOptionCount);
    }

    private static EventProposal ProposalOn(DateOnly date, out Event eventItem)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(9, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        return proposal;
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/RepositoryTests.cs — 1/1

<!-- retirement-file: {"id":72,"file":"tests/EventBooking.Infrastructure.Tests/RepositoryTests.cs","beforeSha":"e15b8189a80d840f400d2cb3a7d52442f0319d014b14cc383f247abf0269def7","afterSha":"69136955d0a89fe1869c0e88a473056aa9d77ee41ceead6ed5f7ecfdf706238c","side":"after","part":1,"parts":1} -->

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

        Assert.Equal(7, settings.InviteExpiryDays);
        Assert.Equal(3, settings.InviteOptionCount);
    }

    private static EventProposal ProposalOn(DateOnly date, out Event eventItem)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(9, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        return proposal;
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/SchemaTests.cs — 1/1

<!-- retirement-file: {"id":73,"file":"tests/EventBooking.Infrastructure.Tests/SchemaTests.cs","beforeSha":"fa0f4412c7456f1e29fc50242fef54907321a3d52422797f823361889741117f","afterSha":"a13870b242c63abae438fe324bee4bbac5ac58f5846268b6e07ef581df52ffc8","side":"before","part":1,"parts":1} -->

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
                Assert.Equal(7, settings.InviteExpiryDays);
                Assert.Equal(3, settings.InviteOptionCount);
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

        Assert.Equal(7, settings.InviteExpiryDays);
        Assert.Equal(3, settings.InviteOptionCount);
        Assert.Equal(2, settings.MaxAutoRetryCount);
    }

    [Fact]
    public async Task AProposalRoundTripsWithItsWindowAndAcceptances()
    {
        await fixture.ResetAsync();

        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
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
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
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

<!-- retirement-file: {"id":73,"file":"tests/EventBooking.Infrastructure.Tests/SchemaTests.cs","beforeSha":"fa0f4412c7456f1e29fc50242fef54907321a3d52422797f823361889741117f","afterSha":"a13870b242c63abae438fe324bee4bbac5ac58f5846268b6e07ef581df52ffc8","side":"after","part":1,"parts":1} -->

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
                Assert.Equal(7, settings.InviteExpiryDays);
                Assert.Equal(3, settings.InviteOptionCount);
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

        Assert.Equal(7, settings.InviteExpiryDays);
        Assert.Equal(3, settings.InviteOptionCount);
        Assert.Equal(2, settings.MaxAutoRetryCount);
    }

    [Fact]
    public async Task AProposalRoundTripsWithItsWindowAndAcceptances()
    {
        await fixture.ResetAsync();

        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
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

        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
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

## before — tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs — 1/1

<!-- retirement-file: {"id":74,"file":"tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs","beforeSha":"70be5aeda75afbf2e137014f7260beefaff1ff3269be4e7dfeb20ea73f81257a","afterSha":"2ba421efe230d4f65d2d64ed6a231ab7f59ba9b95ee919f96d410bc51aa9ae86","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class TransactionLockTests(PostgresFixture fixture)
{
    [Fact]
    public async Task EventCancellationCommitsBeforeAWaitingConfirmationCanCreateABooking()
    {
        var scenario = await GivenScenarioAsync(withBooking: false);

        await using var cancellationContext = fixture.NewContext();
        await using var cancellationTransaction =
            await cancellationContext.Database.BeginTransactionAsync();
        var lockedEvent = await new EventRepository(cancellationContext)
            .LockForUpdateAsync(scenario.EventId, CancellationToken.None);
        Assert.NotNull(lockedEvent);

        // This is the authoritative booking read for whole-event cancellation. It deliberately
        // happens after the shared event guard has been taken.
        var activeBookings = await cancellationContext.Bookings
            .Where(b => b.EventId == scenario.EventId && b.Status == BookingStatus.Active)
            .ToListAsync();
        Assert.Empty(activeBookings);

        var waitingBackend = NewBarrier();
        var confirmation = ConfirmAfterEventGuardAsync(scenario, waitingBackend);
        var confirmationPid = await waitingBackend.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await WaitUntilBlockedOnDatabaseLockAsync(cancellationContext, confirmationPid);

        lockedEvent!.Cancel();
        await cancellationContext.SaveChangesAsync();
        await cancellationTransaction.CommitAsync();

        await confirmation.WaitAsync(TimeSpan.FromSeconds(10));

        await using var read = fixture.NewContext();
        Assert.Equal(
            EventStatus.Cancelled,
            (await read.Events.SingleAsync(s => s.Id == scenario.EventId)).Status);
        Assert.False(await read.Bookings.AnyAsync(
            b => b.EventId == scenario.EventId && b.Status == BookingStatus.Active));
    }

    [Fact]
    public async Task EventCancellationAndAWaitingBookingCancellationReleaseCapacityExactlyOnce()
    {
        var scenario = await GivenScenarioAsync(withBooking: true);

        await using var eventCancellationContext = fixture.NewContext();
        await using var eventCancellationTransaction =
            await eventCancellationContext.Database.BeginTransactionAsync();
        var lockedEvent = await new EventRepository(eventCancellationContext)
            .LockForUpdateAsync(scenario.EventId, CancellationToken.None);
        Assert.NotNull(lockedEvent);

        // As in the application handler, this authoritative read occurs only after the event lock.
        var activeBooking = await eventCancellationContext.Bookings.SingleAsync(
            b => b.EventId == scenario.EventId && b.Status == BookingStatus.Active);

        var waitingBackend = NewBarrier();
        var individualCancellation = CancelBookingAfterEventGuardAsync(scenario, waitingBackend);
        var cancellationPid = await waitingBackend.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await WaitUntilBlockedOnDatabaseLockAsync(eventCancellationContext, cancellationPid);

        lockedEvent!.Cancel();
        activeBooking.Cancel();
        await IncrementCapacityAsync(eventCancellationContext, scenario.EventId);
        await eventCancellationContext.SaveChangesAsync();
        await eventCancellationTransaction.CommitAsync();

        await individualCancellation.WaitAsync(TimeSpan.FromSeconds(10));

        await using var read = fixture.NewContext();
        var capacity = await read.EventCapacities.SingleAsync(
            c => c.EventId == scenario.EventId
                 && c.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting);
        var booking = await read.Bookings.SingleAsync(b => b.Id == scenario.BookingId);

        Assert.Equal(capacity.TotalHeadcount, capacity.RemainingCapacity);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
    }

    [Fact]
    public async Task InviteLockRemainsHeldUntilTheOwningTransactionCommits()
    {
        var scenario = await GivenScenarioAsync(withBooking: false);

        await using var first = fixture.NewContext();
        await using var firstTransaction = await first.Database.BeginTransactionAsync();
        Assert.NotNull(await new InviteRepository(first).LockByTokenHashForUpdateAsync(
            scenario.InviteTokenHash, CancellationToken.None));

        var waitingBackend = NewBarrier();
        var secondLock = LockInviteAsync(scenario.InviteTokenHash, waitingBackend);
        var secondPid = await waitingBackend.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await WaitUntilBlockedOnDatabaseLockAsync(first, secondPid);

        await firstTransaction.CommitAsync();

        Assert.Equal(scenario.InviteId, await secondLock.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task BookingLockRemainsHeldUntilTheOwningTransactionCommits()
    {
        var scenario = await GivenScenarioAsync(withBooking: true);

        await using var first = fixture.NewContext();
        await using var firstTransaction = await first.Database.BeginTransactionAsync();
        Assert.NotNull(await new BookingRepository(first).LockByManageTokenHashForUpdateAsync(
            scenario.ManageTokenHash, CancellationToken.None));

        var waitingBackend = NewBarrier();
        var secondLock = LockBookingAsync(scenario.ManageTokenHash, waitingBackend);
        var secondPid = await waitingBackend.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await WaitUntilBlockedOnDatabaseLockAsync(first, secondPid);

        await firstTransaction.CommitAsync();

        Assert.Equal(scenario.BookingId, await secondLock.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    private async Task ConfirmAfterEventGuardAsync(
        Scenario scenario,
        TaskCompletionSource<int> waitingBackend)
    {
        await using var context = fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        waitingBackend.SetResult(await GetBackendPidAsync(context));

        var eventItem = await new EventRepository(context)
            .LockForUpdateAsync(scenario.EventId, CancellationToken.None);
        Assert.NotNull(eventItem);

        if (eventItem!.Status == EventStatus.Active)
        {
            var invite = await new InviteRepository(context).LockByTokenHashForUpdateAsync(
                scenario.InviteTokenHash, CancellationToken.None);
            Assert.NotNull(invite);

            var booking = Booking.Create(
                Guid.NewGuid(),
                invite!,
                scenario.EventId,
                "confirmation-manage-hash",
                DateTimeOffset.UtcNow);
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();
        }

        await transaction.CommitAsync();
    }

    private async Task CancelBookingAfterEventGuardAsync(
        Scenario scenario,
        TaskCompletionSource<int> waitingBackend)
    {
        await using var context = fixture.NewContext();
        var bookings = new BookingRepository(context);

        // This lookup is intentionally preliminary and untracked. The booking is re-read under a
        // row lock only after this transaction acquires the shared event guard.
        var eventId = await bookings.GetEventIdByManageTokenHashAsync(
            scenario.ManageTokenHash, CancellationToken.None);
        Assert.Equal(scenario.EventId, eventId);

        await using var transaction = await context.Database.BeginTransactionAsync();
        waitingBackend.SetResult(await GetBackendPidAsync(context));

        Assert.NotNull(await new EventRepository(context)
            .LockForUpdateAsync(eventId!.Value, CancellationToken.None));
        var booking = await bookings.LockByManageTokenHashForUpdateAsync(
            scenario.ManageTokenHash, CancellationToken.None);
        Assert.NotNull(booking);

        if (booking!.Status == BookingStatus.Active)
        {
            booking.Cancel();
            await IncrementCapacityAsync(context, scenario.EventId);
            await context.SaveChangesAsync();
        }

        await transaction.CommitAsync();
    }

    private async Task<Guid> LockInviteAsync(
        string tokenHash,
        TaskCompletionSource<int> waitingBackend)
    {
        await using var context = fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        waitingBackend.SetResult(await GetBackendPidAsync(context));

        var invite = await new InviteRepository(context)
            .LockByTokenHashForUpdateAsync(tokenHash, CancellationToken.None);
        await transaction.CommitAsync();
        return Assert.IsType<Invite>(invite).Id;
    }

    private async Task<Guid> LockBookingAsync(
        string tokenHash,
        TaskCompletionSource<int> waitingBackend)
    {
        await using var context = fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        waitingBackend.SetResult(await GetBackendPidAsync(context));

        var booking = await new BookingRepository(context)
            .LockByManageTokenHashForUpdateAsync(tokenHash, CancellationToken.None);
        await transaction.CommitAsync();
        return Assert.IsType<Booking>(booking).Id;
    }

    private async Task<Scenario> GivenScenarioAsync(bool withBooking)
    {
        await fixture.ResetAsync();

        var proposals = new List<EventProposal>();
        var events = new List<Event>();
        for (var offset = 0; offset < Invite.RequiredOptionCount; offset++)
        {
            var proposal = EventProposal.Create(
                Guid.NewGuid(),
                new EventWindow(new DateOnly(2026, 9, 10 + offset), new TimeOnly(9, 0), 240),
                Guid.NewGuid());
            proposal.Accept(
                AppointmentTypeIds.DrugAndAlcoholTesting,
                Guid.NewGuid(),
                1);
            proposal.Accept(
                AppointmentTypeIds.MedicalCheckUp,
                Guid.NewGuid(),
                1);
            proposal.Accept(
                AppointmentTypeIds.UniformFitting,
                Guid.NewGuid(),
                1);
            proposals.Add(proposal);
            events.Add(Event.CreateFrom(Guid.NewGuid(), proposal));
        }

        var pilots = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "amara@example.com",
            pilots);
        attendee.MarkInvited();

        const string inviteTokenHash = "invite-token-hash";
        const string manageTokenHash = "manage-token-hash";
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            inviteTokenHash,
            DateTimeOffset.UtcNow.AddDays(4),
            events.Select(s => s.Id),
            attendee.RequiredAppointmentTypeIds,
            retryCount: 0);

        Booking? booking = null;
        if (withBooking)
        {
            booking = Booking.Create(
                Guid.NewGuid(), invite, events[0].Id, manageTokenHash, DateTimeOffset.UtcNow);
            events[0].CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
            invite.MarkUsed();
            attendee.MarkBooked();
        }

        await using var write = fixture.NewContext();
        write.EventProposals.AddRange(proposals);
        write.Events.AddRange(events);
        write.Attendees.Add(attendee);
        write.Invites.Add(invite);
        if (booking is not null)
        {
            write.Bookings.Add(booking);
        }

        await write.SaveChangesAsync();

        return new Scenario(
            events[0].Id,
            invite.Id,
            inviteTokenHash,
            booking?.Id ?? Guid.Empty,
            manageTokenHash);
    }

    private static async Task IncrementCapacityAsync(
        EventBookingDbContext context,
        Guid eventId)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE event_capacity
             SET remaining_capacity = remaining_capacity + 1
             WHERE event_id = {eventId}
               AND appointment_type_id = {AppointmentTypeIds.DrugAndAlcoholTesting}
             """);
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

        Assert.Fail($"PostgreSQL backend {waitingBackendPid} never waited on the row lock.");
    }

    private sealed record Scenario(
        Guid EventId,
        Guid InviteId,
        string InviteTokenHash,
        Guid BookingId,
        string ManageTokenHash);
}
`````
