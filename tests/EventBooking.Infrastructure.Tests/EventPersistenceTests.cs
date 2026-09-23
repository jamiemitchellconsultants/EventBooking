using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class EventPersistenceTests(PostgresFixture fixture)
{
    private static Dictionary<Guid, int> FullHeadcounts() => new()
    {
        [AppointmentTypeIds.DrugAndAlcoholTesting] = 10,
        [AppointmentTypeIds.MedicalCheckUp] = 6,
        [AppointmentTypeIds.UniformFitting] = 8,
    };

    [Fact]
    public async Task AnImportedEventPersistsWithANullProposalId()
    {
        await using var context = fixture.NewContext();
        var window = new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0));
        var eventItem = Event.CreateImported(Guid.NewGuid(), window, FullHeadcounts());

        context.Events.Add(eventItem);
        await context.SaveChangesAsync();

        await using var reload = fixture.NewContext();
        var reloaded = await reload.Events
            .Include(s => s.Capacities)
            .SingleAsync(s => s.Id == eventItem.Id);

        Assert.Null(reloaded.ProposalId);
        Assert.Equal(3, reloaded.Capacities.Count);

        await fixture.ResetAsync();
    }

    [Fact]
    public async Task TwoImportedEventsCanBothHaveANullProposalId()
    {
        await using var context = fixture.NewContext();
        var first = Event.CreateImported(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 12), new TimeOnly(9, 0)), FullHeadcounts());
        var second = Event.CreateImported(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 13), new TimeOnly(9, 0)), FullHeadcounts());

        context.Events.AddRange(first, second);

        // Proves the existing unique index on proposal_id treats a missing value as distinct
        // (standard SQL and Postgres semantics) rather than colliding two imported events together.
        await context.SaveChangesAsync();

        await fixture.ResetAsync();
    }
}
