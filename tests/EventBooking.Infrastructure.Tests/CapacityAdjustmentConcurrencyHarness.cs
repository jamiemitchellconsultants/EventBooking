using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Slots;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EventBooking.Infrastructure.Tests;

public sealed record CapacitySnapshot(int TotalHeadcount, int RemainingCapacity);

public sealed class CapacityAdjustmentConcurrencyHarness : IAsyncDisposable
{
    private readonly PostgresFixture _fixture;

    private CapacityAdjustmentConcurrencyHarness(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    public static async Task<CapacityAdjustmentConcurrencyHarness> CreateAsync(
        PostgresFixture fixture)
    {
        await fixture.ResetAsync();
        return new CapacityAdjustmentConcurrencyHarness(fixture);
    }

    public async Task<Guid> GivenSlotAsync(int totalHeadcount)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), totalHeadcount);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 20);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 20);
        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);

        await using var context = _fixture.NewContext();
        context.SlotProposals.Add(proposal);
        context.ConfirmedSlots.Add(slot);
        await context.SaveChangesAsync();
        return slot.Id;
    }

    public async Task<HeldCapacityChange> HoldAdjustmentAsync(
        Guid slotId,
        int totalHeadcount)
    {
        var context = _fixture.NewContext();
        var transaction = await context.Database.BeginTransactionAsync();
        var capacity = await LockAsync(context, slotId);
        capacity.AdjustTotalHeadcount(totalHeadcount);
        await context.SaveChangesAsync();
        return new HeldCapacityChange(context, transaction);
    }

    public async Task<HeldCapacityChange> HoldBookingAsync(Guid slotId)
    {
        var context = _fixture.NewContext();
        var transaction = await context.Database.BeginTransactionAsync();
        var capacity = await LockAsync(context, slotId);
        capacity.Decrement();
        await context.SaveChangesAsync();
        return new HeldCapacityChange(context, transaction);
    }

    public async Task BookAsync(Guid slotId)
    {
        await using var context = _fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var capacity = await LockAsync(context, slotId);
        capacity.Decrement();
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task AdjustAsync(Guid slotId, int totalHeadcount)
    {
        await using var context = _fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var capacity = await LockAsync(context, slotId);
        capacity.AdjustTotalHeadcount(totalHeadcount);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task<CapacitySnapshot> ReadAsync(Guid slotId)
    {
        await using var context = _fixture.NewContext();
        var capacity = await context.SlotCapacities.SingleAsync(
            item => item.ConfirmedSlotId == slotId
                && item.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting);
        return new CapacitySnapshot(capacity.TotalHeadcount, capacity.RemainingCapacity);
    }

    private static async Task<SlotCapacity> LockAsync(
        EventBookingDbContext context,
        Guid slotId)
    {
        var rows = await new SlotCapacityRepository(context).LockForUpdateAsync(
            slotId,
            [AppointmentTypeIds.DrugAndAlcoholTesting],
            CancellationToken.None);
        return Assert.Single(rows);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class HeldCapacityChange(
    EventBookingDbContext context,
    IDbContextTransaction transaction) : IAsyncDisposable
{
    private bool _committed;

    public async Task CommitAsync()
    {
        await transaction.CommitAsync();
        _committed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_committed)
        {
            await transaction.RollbackAsync();
        }

        await transaction.DisposeAsync();
        await context.DisposeAsync();
    }
}
