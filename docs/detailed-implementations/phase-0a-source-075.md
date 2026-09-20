# 00a — Port source 75 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## tests/EventBooking.Infrastructure.Tests/SchemaTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/SchemaTests.cs","encoding":"utf8","sha256":"3b9a4949b57b56f261033e124cefffe61b9d19ef8425eb32c126e71bf0718135","parts":1,"part":1} -->

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

## tests/EventBooking.Infrastructure.Tests/SlotCapacityRepositoryTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/SlotCapacityRepositoryTests.cs","encoding":"utf8","sha256":"1711e4c04b656c1e6770ded1e0987d8b156c92369a33b7257a2bed8936907d09","parts":1,"part":1} -->

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

## tests/EventBooking.Infrastructure.Tests/StaffAccessConcurrencyTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/StaffAccessConcurrencyTests.cs","encoding":"utf8","sha256":"a5f3db62037973a61a55c8652fb3a5e76d982cbfb0167671064cc3adea25de0a","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Infrastructure.Audit;
using EventBooking.Domain.Audit;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class StaffAccessConcurrencyTests(PostgresFixture fixture)
{
    [Fact]
    public async Task ProfileSetDecisionsSerializeEvenWhenNoProfileRowExists()
    {
        await fixture.ResetAsync();

        await using var first = fixture.NewContext();
        await using var firstTransaction = await first.Database.BeginTransactionAsync();
        Assert.Empty(await new StaffAccessProfileRepository(first)
            .LockAllAsync(CancellationToken.None));

        var waitingBackend = new TaskCompletionSource<int>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondLock = LockAllInSecondTransactionAsync(waitingBackend);
        var secondPid = await waitingBackend.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await WaitUntilBlockedOnDatabaseLockAsync(first, secondPid);
        Assert.False(secondLock.IsCompleted);

        await firstTransaction.CommitAsync();

        Assert.Empty(await secondLock.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task ConcurrentManagerInsertsCannotBothCommitForOneType()
    {
        await fixture.ResetAsync();

        await using var first = fixture.NewContext();
        await using var firstTransaction = await first.Database.BeginTransactionAsync();
        first.StaffAccessProfiles.Add(StaffAccessProfile.Create(
            Guid.NewGuid(), [Role.Manager], AppointmentTypeIds.MedicalCheckUp));
        await first.SaveChangesAsync();

        var waitingBackend = new TaskCompletionSource<int>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondCommit = InsertCompetingManagerAsync(waitingBackend);
        var secondPid = await waitingBackend.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await WaitUntilBlockedOnDatabaseLockAsync(first, secondPid);

        await firstTransaction.CommitAsync();
        await Assert.ThrowsAsync<DbUpdateException>(
            () => secondCommit.WaitAsync(TimeSpan.FromSeconds(10)));

        await using var read = fixture.NewContext();
        Assert.Single(await read.StaffAccessProfiles.Where(profile =>
            profile.IsManager
            && profile.AppointmentTypeId == AppointmentTypeIds.MedicalCheckUp).ToListAsync());
    }

    [Fact]
    public async Task ConcurrentScopeClearsLeaveOneSuccessAndOneConflict()
    {
        await fixture.ResetAsync();
        var admin = Guid.NewGuid();
        var target = Guid.NewGuid();

        await using (var seed = fixture.NewContext())
        {
            seed.StaffAccessProfiles.AddRange(
                StaffAccessProfile.Create(admin, [Role.Admin], null),
                StaffAccessProfile.Create(
                    target, [Role.AppointmentStaff], AppointmentTypeIds.UniformFitting));
            await seed.SaveChangesAsync();
        }

        var results = await Task.WhenAll(
            ClearScopeAsync(admin, target),
            ClearScopeAsync(admin, target));

        Assert.Single(results, result => result.IsSuccess);
        Assert.Single(results, result => result.IsFailure);

        await using var read = fixture.NewContext();
        var profile = await read.StaffAccessProfiles
            .SingleAsync(value => value.StaffUserId == target);
        Assert.True(profile.IsAppointmentStaff);
        Assert.Null(profile.AppointmentTypeId);
    }

    [Fact]
    public async Task ConcurrentFirstRoleSyncsCreateOneProfileAndOneAuditEntry()
    {
        await fixture.ResetAsync();
        var staffUserId = Guid.NewGuid();

        await using var gate = fixture.NewContext();
        await using var gateTransaction = await gate.Database.BeginTransactionAsync();
        Assert.Empty(await new StaffAccessProfileRepository(gate)
            .LockAllAsync(CancellationToken.None));

        var firstReady = new TaskCompletionSource<int>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondReady = new TaskCompletionSource<int>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var first = SyncRolesAsync(staffUserId, firstReady);
        var second = SyncRolesAsync(staffUserId, secondReady);
        var firstPid = await firstReady.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var secondPid = await secondReady.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await WaitUntilBlockedOnDatabaseLockAsync(gate, firstPid);
        await WaitUntilBlockedOnDatabaseLockAsync(gate, secondPid);
        await gateTransaction.CommitAsync();

        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.All(results, result =>
        {
            Assert.NotNull(result);
            Assert.True(result!.IsManager);
        });

        await using var read = fixture.NewContext();
        Assert.Single(await read.StaffAccessProfiles
            .Where(value => value.StaffUserId == staffUserId)
            .ToListAsync());
        Assert.Single(await read.AuditLogs
            .Where(value => value.EntityId == staffUserId
                && value.Action == AuditAction.StaffRolesSynced)
            .ToListAsync());
    }

    private async Task<StaffAccessProfile?> SyncRolesAsync(
        Guid staffUserId,
        TaskCompletionSource<int> ready)
    {
        await using var context = fixture.NewContext();
        await context.Database.OpenConnectionAsync();
        await using (var command = context.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "SELECT pg_backend_pid();";
            ready.SetResult((int)(await command.ExecuteScalarAsync())!);
        }

        var handler = new SyncStaffAccessProfileRolesHandler(
            new StaffAccessProfileRepository(context),
            new UnitOfWork(context),
            new EfAuditLogger(context, new FixedClock()),
            NullLogger<SyncStaffAccessProfileRolesHandler>.Instance);
        return await handler.SyncAsync(
            staffUserId,
            new HashSet<Role> { Role.Manager },
            CancellationToken.None);
    }

    private async Task<IReadOnlyList<StaffAccessProfile>> LockAllInSecondTransactionAsync(
        TaskCompletionSource<int> waitingBackend)
    {
        await using var context = fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        waitingBackend.SetResult(await GetBackendPidAsync(context));

        var locked = await new StaffAccessProfileRepository(context)
            .LockAllAsync(CancellationToken.None);
        await transaction.CommitAsync();
        return locked;
    }

    private async Task InsertCompetingManagerAsync(TaskCompletionSource<int> waitingBackend)
    {
        await using var context = fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        waitingBackend.SetResult(await GetBackendPidAsync(context));
        context.StaffAccessProfiles.Add(StaffAccessProfile.Create(
            Guid.NewGuid(), [Role.Manager], AppointmentTypeIds.MedicalCheckUp));
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private async Task<Result> ClearScopeAsync(
        Guid actor,
        Guid target)
    {
        await using var context = fixture.NewContext();
        var repository = new StaffAccessProfileRepository(context);
        var handler = new StaffAccessHandler(
            repository,
            new StaffIdentityRepository(context),
            new StaffAccessAuthorizer(repository),
            new UnitOfWork(context),
            new EfAuditLogger(context, new FixedClock()));

        return await handler.ClearScopeAsync(
            new ClearStaffAccessProfileScopeCommand(actor, target, 1),
            CancellationToken.None);
    }

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

        Assert.Fail(
            $"PostgreSQL backend {waitingBackendPid} never waited on the staff-access lock.");
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
        /// <summary>Gets the fixed instant; this clock treats UTC as head-office time.</summary>
        public DateTimeOffset NowAtHeadOffice => UtcNow;
        public DateOnly TodayAtHeadOffice => new(2026, 9, 6);
        public DateOnly DateAtHeadOffice(DateTimeOffset instant) =>
            DateOnly.FromDateTime(instant.UtcDateTime);

        public DateTimeOffset InstantAtHeadOffice(DateTimeOffset instant) => instant.ToUniversalTime();
    }
}
`````

## tests/EventBooking.Infrastructure.Tests/StaffAccessMigrationTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/StaffAccessMigrationTests.cs","encoding":"utf8","sha256":"172c26a0e667b143fa0f586d46958d43f57c087bf4d022c89d2a92da7a3dfdcc","parts":1,"part":1} -->

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
`````

## tests/EventBooking.Infrastructure.Tests/StaffAccessProfileMigrationRegressionTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/StaffAccessProfileMigrationRegressionTests.cs","encoding":"utf8","sha256":"9bc55a69af59ee1e34afbca13a6d1f9392baaae366ab0dc9068ffe21618bf040","parts":1,"part":1} -->

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

## tests/EventBooking.Infrastructure.Tests/StaffAccessProfilePersistenceTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/StaffAccessProfilePersistenceTests.cs","encoding":"utf8","sha256":"4c4946573069c4c3ea6de19bd8eac5cb19804f37e87c193576474a74e5d23860","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class StaffAccessProfilePersistenceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task ACombinedProfileRoundTripsWithItsVersionAndScope()
    {
        await fixture.ResetAsync();
        var id = Guid.NewGuid();

        await using (var write = fixture.NewContext())
        {
            write.StaffAccessProfiles.Add(StaffAccessProfile.Create(
                id,
                [Role.Coordinator, Role.Manager, Role.AppointmentStaff],
                AppointmentTypeIds.MedicalCheckUp));
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var profile = await new StaffAccessProfileRepository(read)
            .GetAsync(id, CancellationToken.None);

        Assert.NotNull(profile);
        Assert.True(new HashSet<Role>
        {
            Role.Manager,
            Role.Coordinator,
            Role.AppointmentStaff,
        }.SetEquals(profile!.Roles));
        Assert.Equal(AppointmentTypeIds.MedicalCheckUp, profile.AppointmentTypeId);
        Assert.Equal(1, profile.Version);
    }

    [Fact]
    public async Task TheDatabaseRejectsTwoManagersForOneAppointmentType()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        context.StaffAccessProfiles.AddRange(
            StaffAccessProfile.Create(
                Guid.NewGuid(), [Role.Manager], AppointmentTypeIds.UniformFitting),
            StaffAccessProfile.Create(
                Guid.NewGuid(), [Role.Manager], AppointmentTypeIds.UniformFitting));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task MultipleAppointmentStaffForOneTypeAreAllowed()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        context.StaffAccessProfiles.AddRange(
            StaffAccessProfile.Create(
                Guid.NewGuid(), [Role.AppointmentStaff], AppointmentTypeIds.UniformFitting),
            StaffAccessProfile.Create(
                Guid.NewGuid(), [Role.AppointmentStaff], AppointmentTypeIds.UniformFitting));
        await context.SaveChangesAsync();

        Assert.Equal(2, await context.StaffAccessProfiles.CountAsync());
    }

    [Fact]
    public async Task TheDatabaseAcceptsAScopedRoleWithNullScope()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        context.StaffAccessProfiles.Add(StaffAccessProfile.Create(
            Guid.NewGuid(), [Role.Manager], null));

        // No exception: the relaxed constraint accepts this shape.
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task TheDatabaseStillRejectsScopeWithoutAScopedRole()
    {
        await fixture.ResetAsync();
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO staff_access_profile "
            + "(staff_user_id, is_admin, is_coordinator, is_manager, is_appointment_staff, "
            + "appointment_type_id, version) "
            + "VALUES (@id, false, true, false, false, @type, 1)";
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("type", AppointmentTypeIds.DrugAndAlcoholTesting);

        await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
    }
}
`````

## tests/EventBooking.Infrastructure.Tests/StaffIdentityPersistenceTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/StaffIdentityPersistenceTests.cs","encoding":"utf8","sha256":"b47ceace9ff92c36152f48bcd17d3e2dbd10e1a25f6915548c239bb9df2479aa","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Access;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies the database contract for the provider identity mirror.</summary>
[Collection("postgres")]
public sealed class StaffIdentityPersistenceTests(PostgresFixture fixture)
{
    /// <summary>Verifies a canonical staff number resolves its provider identity.</summary>
    [Fact]
    public async Task IdentityRoundTripsAndIsFoundByCanonicalStaffId()
    {
        await fixture.ResetAsync();
        var userId = Guid.NewGuid();
        await using (var write = fixture.NewContext())
        {
            await new StaffIdentityRepository(write).UpsertAsync(
                userId,
                new StaffId("u123456"),
                null,
                DateTimeOffset.Parse("2026-09-08T10:00:00Z"),
                CancellationToken.None);
        }

        await using var read = fixture.NewContext();
        var actual = await new StaffIdentityRepository(read)
            .GetByStaffIdAsync(new StaffId("U123456"), CancellationToken.None);

        Assert.Equal(userId, actual!.StaffUserId);
        Assert.Equal("U123456", actual.StaffId.Value);
    }

    /// <summary>Verifies a repeated provider key atomically refreshes the mirrored observation.</summary>
    [Fact]
    public async Task UpsertRefreshesTheExistingProviderIdentity()
    {
        await fixture.ResetAsync();
        var userId = Guid.NewGuid();
        await using var write = fixture.NewContext();
        var repository = new StaffIdentityRepository(write);
        var first = DateTimeOffset.Parse("2026-09-08T10:00:00Z");

        await repository.UpsertAsync(
            userId, new StaffId("U123456"), null, first, CancellationToken.None);
        await repository.UpsertAsync(
            userId, new StaffId("U123456"), null, first.AddHours(1), CancellationToken.None);

        await using var read = fixture.NewContext();
        var identities = await new StaffIdentityRepository(read).ListAsync(CancellationToken.None);
        var identity = Assert.Single(identities);
        Assert.Equal(first.AddHours(1), identity.LastSeenAt);
    }

    /// <summary>Verifies the database backstops accept lowercase shape but reject bad or reused values.</summary>
    [Fact]
    public async Task DatabaseAcceptsLowercaseButRejectsMalformedAndDuplicateStaffIds()
    {
        await fixture.ResetAsync();
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using (var valid = connection.CreateCommand())
        {
            valid.CommandText = "INSERT INTO staff_identity (staff_user_id, staff_id, last_seen_at) VALUES (@user, 'u123456', now())";
            valid.Parameters.AddWithValue("user", Guid.NewGuid());
            await valid.ExecuteNonQueryAsync();
        }

        var invalid = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO staff_identity (staff_user_id, staff_id, last_seen_at) VALUES (@user, 'X123456', now())";
            command.Parameters.AddWithValue("user", Guid.NewGuid());
            await command.ExecuteNonQueryAsync();
        });
        Assert.Equal(PostgresErrorCodes.CheckViolation, invalid.SqlState);

        var duplicate = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO staff_identity (staff_user_id, staff_id, last_seen_at) VALUES (@user, 'u123456', now())";
            command.Parameters.AddWithValue("user", Guid.NewGuid());
            await command.ExecuteNonQueryAsync();
        });
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
    }

    /// <summary>Verifies an observed name round-trips through the upsert and the listing.</summary>
    [Fact]
    public async Task UpsertWritesAndReadsBackDisplayName()
    {
        await fixture.ResetAsync();
        var userId = Guid.NewGuid();

        await using (var write = fixture.NewContext())
        {
            await new StaffIdentityRepository(write).UpsertAsync(
                userId,
                new StaffId("U000002"),
                "Dana Datson",
                DateTimeOffset.Parse("2026-09-08T10:00:00Z"),
                CancellationToken.None);
        }

        await using var read = fixture.NewContext();
        var identities = await new StaffIdentityRepository(read).ListAsync(CancellationToken.None);

        var identity = Assert.Single(identities);
        Assert.Equal("Dana Datson", identity.DisplayName);
    }

    /// <summary>Verifies a later token without a name clears the stored value.</summary>
    [Fact]
    public async Task UpsertOverwritesDisplayNameBackToNull()
    {
        await fixture.ResetAsync();
        var userId = Guid.NewGuid();
        var first = DateTimeOffset.Parse("2026-09-08T10:00:00Z");

        await using (var write = fixture.NewContext())
        {
            var repository = new StaffIdentityRepository(write);
            await repository.UpsertAsync(
                userId, new StaffId("U000003"), "Dana Datson", first, CancellationToken.None);
            await repository.UpsertAsync(
                userId, new StaffId("U000003"), null, first.AddHours(1), CancellationToken.None);
        }

        await using var read = fixture.NewContext();
        var identity = await new StaffIdentityRepository(read)
            .GetByStaffIdAsync(new StaffId("U000003"), CancellationToken.None);

        Assert.NotNull(identity);
        Assert.Null(identity!.DisplayName);
        Assert.Equal("U000003", identity.StaffId.Value);
        Assert.Equal(first.AddHours(1), identity.LastSeenAt);
    }

    /// <summary>Verifies an identity observed without a name stores and reads back a null column.</summary>
    [Fact]
    public async Task UpsertNullDisplayNameRoundTrips()
    {
        await fixture.ResetAsync();
        var userId = Guid.NewGuid();

        await using (var write = fixture.NewContext())
        {
            await new StaffIdentityRepository(write).UpsertAsync(
                userId,
                new StaffId("U000004"),
                null,
                DateTimeOffset.Parse("2026-09-08T10:00:00Z"),
                CancellationToken.None);
        }

        await using var read = fixture.NewContext();
        var identity = await new StaffIdentityRepository(read)
            .GetByStaffIdAsync(new StaffId("U000004"), CancellationToken.None);

        Assert.NotNull(identity);
        Assert.Equal(userId, identity!.StaffUserId);
        Assert.Null(identity.DisplayName);
    }

    /// <summary>Verifies a rename observed at the next refresh replaces the stored value.</summary>
    [Fact]
    public async Task UpsertReplacesAnEarlierDisplayName()
    {
        await fixture.ResetAsync();
        var userId = Guid.NewGuid();
        var first = DateTimeOffset.Parse("2026-09-08T10:00:00Z");

        await using (var write = fixture.NewContext())
        {
            var repository = new StaffIdentityRepository(write);
            await repository.UpsertAsync(
                userId, new StaffId("U000005"), "Old Name", first, CancellationToken.None);
            await repository.UpsertAsync(
                userId, new StaffId("U000005"), "New Name", first.AddHours(1), CancellationToken.None);
        }

        await using var read = fixture.NewContext();
        var identity = await new StaffIdentityRepository(read)
            .GetByStaffIdAsync(new StaffId("U000005"), CancellationToken.None);

        Assert.Equal("New Name", identity!.DisplayName);
    }
}
`````
