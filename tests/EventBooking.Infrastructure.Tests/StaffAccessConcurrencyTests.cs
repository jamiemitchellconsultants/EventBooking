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
            new EfAuditLogger(context, new FixedClock()),
            new AppointmentTypeRepository(context));

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
