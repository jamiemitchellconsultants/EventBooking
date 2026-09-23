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
    public async Task AnEventPersistsWithItsProposalId()
    {
        await using var context = fixture.NewContext();
        var window = new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240);
        var eventItem = EventFixture.Create(Guid.NewGuid(), window, FullHeadcounts());

        context.Events.Add(eventItem);
        await context.SaveChangesAsync();

        await using var reload = fixture.NewContext();
        var reloaded = await reload.Events
            .Include(s => s.Capacities)
            .SingleAsync(s => s.Id == eventItem.Id);

        Assert.Equal(eventItem.ProposalId, reloaded.ProposalId);
        Assert.NotEqual(Guid.Empty, reloaded.ProposalId);
        Assert.Equal(3, reloaded.Capacities.Count);

        await fixture.ResetAsync();
    }

    [Fact]
    public async Task TwoEventsHaveDistinctProposalIds()
    {
        await using var context = fixture.NewContext();
        var first = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 12), new TimeOnly(9, 0), 240), FullHeadcounts());
        var second = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 13), new TimeOnly(9, 0), 240), FullHeadcounts());

        context.Events.AddRange(first, second);

        Assert.NotEqual(Guid.Empty, first.ProposalId);
        Assert.NotEqual(Guid.Empty, second.ProposalId);
        Assert.NotEqual(first.ProposalId, second.ProposalId);
        await context.SaveChangesAsync();

        await fixture.ResetAsync();
    }
}
