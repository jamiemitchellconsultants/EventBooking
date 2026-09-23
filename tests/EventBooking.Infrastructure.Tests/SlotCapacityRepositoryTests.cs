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
