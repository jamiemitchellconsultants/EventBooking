using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Slots;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class ConfirmedSlotPersistenceTests(PostgresFixture fixture)
{
    private static Dictionary<Guid, int> FullHeadcounts() => new()
    {
        [AppointmentTypeIds.DrugAndAlcoholTesting] = 10,
        [AppointmentTypeIds.MedicalCheckUp] = 6,
        [AppointmentTypeIds.UniformFitting] = 8,
    };

    [Fact]
    public async Task AnImportedSlotPersistsWithANullProposalId()
    {
        await using var context = fixture.NewContext();
        var window = new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0));
        var slot = ConfirmedSlot.CreateImported(Guid.NewGuid(), window, FullHeadcounts());

        context.ConfirmedSlots.Add(slot);
        await context.SaveChangesAsync();

        await using var reload = fixture.NewContext();
        var reloaded = await reload.ConfirmedSlots
            .Include(s => s.Capacities)
            .SingleAsync(s => s.Id == slot.Id);

        Assert.Null(reloaded.ProposalId);
        Assert.Equal(3, reloaded.Capacities.Count);

        await fixture.ResetAsync();
    }

    [Fact]
    public async Task TwoImportedSlotsCanBothHaveANullProposalId()
    {
        await using var context = fixture.NewContext();
        var first = ConfirmedSlot.CreateImported(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, 12), new TimeOnly(9, 0)), FullHeadcounts());
        var second = ConfirmedSlot.CreateImported(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, 13), new TimeOnly(9, 0)), FullHeadcounts());

        context.ConfirmedSlots.AddRange(first, second);

        // Proves the existing unique index on proposal_id treats a missing value as distinct
        // (standard SQL and Postgres semantics) rather than colliding two imported slots together.
        await context.SaveChangesAsync();

        await fixture.ResetAsync();
    }
}
