using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
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

    public async Task<Guid> GivenEventAsync(int totalHeadcount)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), new TimeOnly(9, 0), 240),
            Guid.NewGuid());
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), totalHeadcount);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 20);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 20);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        await using var context = _fixture.NewContext();
        context.EventProposals.Add(proposal);
        context.Events.Add(eventItem);
        await context.SaveChangesAsync();
        return eventItem.Id;
    }

    public async Task<HeldCapacityChange> HoldAdjustmentAsync(
        Guid eventId,
        int totalHeadcount)
    {
        var context = _fixture.NewContext();
        var transaction = await context.Database.BeginTransactionAsync();
        var capacity = await LockAsync(context, eventId);
        capacity.AdjustTotalHeadcount(totalHeadcount);
        await context.SaveChangesAsync();
        return new HeldCapacityChange(context, transaction);
    }

    public async Task<HeldCapacityChange> HoldBookingAsync(Guid eventId)
    {
        var context = _fixture.NewContext();
        var transaction = await context.Database.BeginTransactionAsync();
        var capacity = await LockAsync(context, eventId);
        capacity.Decrement();
        await context.SaveChangesAsync();
        return new HeldCapacityChange(context, transaction);
    }

    public async Task BookAsync(Guid eventId)
    {
        await using var context = _fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var capacity = await LockAsync(context, eventId);
        capacity.Decrement();
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task AdjustAsync(Guid eventId, int totalHeadcount)
    {
        await using var context = _fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var capacity = await LockAsync(context, eventId);
        capacity.AdjustTotalHeadcount(totalHeadcount);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task<CapacitySnapshot> ReadAsync(Guid eventId)
    {
        await using var context = _fixture.NewContext();
        var capacity = await context.EventCapacities.SingleAsync(
            item => item.EventId == eventId
                && item.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting);
        return new CapacitySnapshot(capacity.TotalHeadcount, capacity.RemainingCapacity);
    }

    private static async Task<EventCapacity> LockAsync(
        EventBookingDbContext context,
        Guid eventId)
    {
        var rows = await new EventCapacityRepository(context).LockForUpdateAsync(
            eventId,
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
