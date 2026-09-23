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
