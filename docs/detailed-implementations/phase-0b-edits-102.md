# 00b — Vocabulary edits 102 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — tests/EventBooking.Infrastructure.Tests/StaffAccessConcurrencyTests.cs — 1/1

<!-- vocabulary-file: {"id":349,"oldPath":"tests/EventBooking.Infrastructure.Tests/StaffAccessConcurrencyTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/StaffAccessConcurrencyTests.cs","beforeSha":"a5f3db62037973a61a55c8652fb3a5e76d982cbfb0167671064cc3adea25de0a","afterSha":"db140ba52b343e96b809a28012c8555fda1bd190494406e4207afec5bdfefd21","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Infrastructure.Tests/StaffAccessConcurrencyTests.cs — 1/1

<!-- vocabulary-file: {"id":349,"oldPath":"tests/EventBooking.Infrastructure.Tests/StaffAccessConcurrencyTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/StaffAccessConcurrencyTests.cs","beforeSha":"a5f3db62037973a61a55c8652fb3a5e76d982cbfb0167671064cc3adea25de0a","afterSha":"db140ba52b343e96b809a28012c8555fda1bd190494406e4207afec5bdfefd21","side":"after","part":1,"parts":1} -->

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
        /// <summary>Gets the fixed instant; this clock treats UTC as transitional-location time.</summary>
        public DateTimeOffset NowAtTransitionalLocation => UtcNow;
        public DateOnly TodayAtTransitionalLocation => new(2026, 9, 6);
        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) =>
            DateOnly.FromDateTime(instant.UtcDateTime);

        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => instant.ToUniversalTime();
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/StaffAccessMigrationTests.cs — 1/1

<!-- vocabulary-file: {"id":350,"oldPath":"tests/EventBooking.Infrastructure.Tests/StaffAccessMigrationTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/StaffAccessMigrationTests.cs","beforeSha":"172c26a0e667b143fa0f586d46958d43f57c087bf4d022c89d2a92da7a3dfdcc","afterSha":"868b60f2b01712c06d3338a9733bf99ab1236ecee56110d1cd81386e057e9e29","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Infrastructure.Tests/StaffAccessMigrationTests.cs — 1/1

<!-- vocabulary-file: {"id":350,"oldPath":"tests/EventBooking.Infrastructure.Tests/StaffAccessMigrationTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/StaffAccessMigrationTests.cs","beforeSha":"172c26a0e667b143fa0f586d46958d43f57c087bf4d022c89d2a92da7a3dfdcc","afterSha":"868b60f2b01712c06d3338a9733bf99ab1236ecee56110d1cd81386e057e9e29","side":"after","part":1,"parts":1} -->

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

## before — tests/EventBooking.Infrastructure.Tests/SystemClockTests.cs — 1/1

<!-- vocabulary-file: {"id":351,"oldPath":"tests/EventBooking.Infrastructure.Tests/SystemClockTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/SystemClockTests.cs","beforeSha":"1605c316ec6443580c2d1f8ffb3a6619dfae56998cb482465d0de58d8e1320d7","afterSha":"95fa8ab08604bd9874bcaefe796d3a45a54aefd2b33ab46302b3b5caacb69b6a","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Infrastructure.Time;

namespace EventBooking.Infrastructure.Tests;

public class SystemClockTests
{
    private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    [Fact]
    public void TheLocalDateFollowsTheHeadOfficeZoneNotUtc()
    {
        // 23:30 UTC on 9 September is already 00:30 on 10 September in British Summer Time.
        var instant = new DateTimeOffset(2026, 9, 9, 23, 30, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 9, 10), SystemClock.LocalDateOf(instant, London));
    }

    [Fact]
    public void InWinterTheZoneMatchesUtc()
    {
        var instant = new DateTimeOffset(2026, 1, 9, 23, 30, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 1, 9), SystemClock.LocalDateOf(instant, London));
    }

    [Fact]
    public void TheClockReportsAUtcInstant()
    {
        var clock = new SystemClock(new HeadOfficeOptions("Europe/London"));

        Assert.Equal(TimeSpan.Zero, clock.UtcNow.Offset);
        Assert.InRange(
            clock.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public void AnUnknownTimeZoneFailsAtConstructionNotAtUseTime()
    {
        Assert.ThrowsAny<Exception>(() => new SystemClock(new HeadOfficeOptions("Mars/Olympus_Mons")));
    }

    /// <summary>Verifies a UTC instant renders with the head-office offset in British Summer Time.</summary>
    [Fact]
    public void InstantAtHeadOfficeConvertsAUtcInstantToLondonLocalTime()
    {
        var clock = new SystemClock(new HeadOfficeOptions("Europe/London"));
        var instant = new DateTimeOffset(2026, 9, 15, 8, 30, 0, TimeSpan.Zero);

        var local = clock.InstantAtHeadOffice(instant);

        Assert.Equal(TimeSpan.FromHours(1), local.Offset);
        Assert.Equal(new TimeOnly(9, 30), TimeOnly.FromDateTime(local.DateTime));
    }

    /// <summary>Verifies the London local date can run ahead of the UTC date late in the evening.</summary>
    [Fact]
    public void InstantAtHeadOfficeLocalDateCanDifferFromTheUtcDate()
    {
        var clock = new SystemClock(new HeadOfficeOptions("Europe/London"));
        // 23:30 UTC on 15 September is already 00:30 on 16 September in British Summer Time.
        var instant = new DateTimeOffset(2026, 9, 15, 23, 30, 0, TimeSpan.Zero);

        var local = clock.InstantAtHeadOffice(instant);

        Assert.Equal(new DateOnly(2026, 9, 16), DateOnly.FromDateTime(local.DateTime));
    }

    /// <summary>Verifies conversion re-expresses an instant rather than shifting it.</summary>
    [Fact]
    public void InstantAtHeadOfficePreservesTheSameInstant()
    {
        var clock = new SystemClock(new HeadOfficeOptions("Europe/London"));
        var instant = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.FromHours(-4));

        Assert.Equal(instant.UtcDateTime, clock.InstantAtHeadOffice(instant).UtcDateTime);
    }

    /// <summary>Verifies a winter instant carries the zero London offset.</summary>
    [Fact]
    public void InstantAtHeadOfficeUsesTheZeroOffsetInLondonWinter()
    {
        var clock = new SystemClock(new HeadOfficeOptions("Europe/London"));
        var instant = new DateTimeOffset(2026, 1, 15, 8, 30, 0, TimeSpan.Zero);

        var local = clock.InstantAtHeadOffice(instant);

        Assert.Equal(TimeSpan.Zero, local.Offset);
        Assert.Equal(new TimeOnly(8, 30), TimeOnly.FromDateTime(local.DateTime));
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/SystemClockTests.cs — 1/1

<!-- vocabulary-file: {"id":351,"oldPath":"tests/EventBooking.Infrastructure.Tests/SystemClockTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/SystemClockTests.cs","beforeSha":"1605c316ec6443580c2d1f8ffb3a6619dfae56998cb482465d0de58d8e1320d7","afterSha":"95fa8ab08604bd9874bcaefe796d3a45a54aefd2b33ab46302b3b5caacb69b6a","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Infrastructure.Time;

namespace EventBooking.Infrastructure.Tests;

public class SystemClockTests
{
    private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    [Fact]
    public void TheLocalDateFollowsTheTransitionalLocationZoneNotUtc()
    {
        // 23:30 UTC on 9 September is already 00:30 on 10 September in British Summer Time.
        var instant = new DateTimeOffset(2026, 9, 9, 23, 30, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 9, 10), SystemClock.LocalDateOf(instant, London));
    }

    [Fact]
    public void InWinterTheZoneMatchesUtc()
    {
        var instant = new DateTimeOffset(2026, 1, 9, 23, 30, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 1, 9), SystemClock.LocalDateOf(instant, London));
    }

    [Fact]
    public void TheClockReportsAUtcInstant()
    {
        var clock = new SystemClock(new TransitionalLocationOptions("Europe/London"));

        Assert.Equal(TimeSpan.Zero, clock.UtcNow.Offset);
        Assert.InRange(
            clock.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public void AnUnknownTimeZoneFailsAtConstructionNotAtUseTime()
    {
        Assert.ThrowsAny<Exception>(() => new SystemClock(new TransitionalLocationOptions("Mars/Olympus_Mons")));
    }

    /// <summary>Verifies a UTC instant renders with the transitional-location offset in British Summer Time.</summary>
    [Fact]
    public void InstantAtTransitionalLocationConvertsAUtcInstantToLondonLocalTime()
    {
        var clock = new SystemClock(new TransitionalLocationOptions("Europe/London"));
        var instant = new DateTimeOffset(2026, 9, 15, 8, 30, 0, TimeSpan.Zero);

        var local = clock.InstantAtTransitionalLocation(instant);

        Assert.Equal(TimeSpan.FromHours(1), local.Offset);
        Assert.Equal(new TimeOnly(9, 30), TimeOnly.FromDateTime(local.DateTime));
    }

    /// <summary>Verifies the London local date can run ahead of the UTC date late in the evening.</summary>
    [Fact]
    public void InstantAtTransitionalLocationLocalDateCanDifferFromTheUtcDate()
    {
        var clock = new SystemClock(new TransitionalLocationOptions("Europe/London"));
        // 23:30 UTC on 15 September is already 00:30 on 16 September in British Summer Time.
        var instant = new DateTimeOffset(2026, 9, 15, 23, 30, 0, TimeSpan.Zero);

        var local = clock.InstantAtTransitionalLocation(instant);

        Assert.Equal(new DateOnly(2026, 9, 16), DateOnly.FromDateTime(local.DateTime));
    }

    /// <summary>Verifies conversion re-expresses an instant rather than shifting it.</summary>
    [Fact]
    public void InstantAtTransitionalLocationPreservesTheSameInstant()
    {
        var clock = new SystemClock(new TransitionalLocationOptions("Europe/London"));
        var instant = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.FromHours(-4));

        Assert.Equal(instant.UtcDateTime, clock.InstantAtTransitionalLocation(instant).UtcDateTime);
    }

    /// <summary>Verifies a winter instant carries the zero London offset.</summary>
    [Fact]
    public void InstantAtTransitionalLocationUsesTheZeroOffsetInLondonWinter()
    {
        var clock = new SystemClock(new TransitionalLocationOptions("Europe/London"));
        var instant = new DateTimeOffset(2026, 1, 15, 8, 30, 0, TimeSpan.Zero);

        var local = clock.InstantAtTransitionalLocation(instant);

        Assert.Equal(TimeSpan.Zero, local.Offset);
        Assert.Equal(new TimeOnly(8, 30), TimeOnly.FromDateTime(local.DateTime));
    }
}
`````
