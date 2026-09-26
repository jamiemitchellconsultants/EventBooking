using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.EventGroups;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests.EventGroups;

[Collection("postgres")]
public sealed class EventGroupPersistenceTests(PostgresFixture fixture)
    : PostgresBlockingHarness(fixture)
{
    [Fact]
    public async Task MembershipGateAndGroupVersionRoundTrip()
    {
        var location = await SeedLocationAsync("GROUPSITE", "Europe/London");
        var eventItem = await SeedFutureEventAsync(location.Id);
        var groups = new Dictionary<Guid, IReadOnlyCollection<Guid>>
        {
            [AttendeeGroupIds.CabinCrew] = [
                AppointmentTypeIds.DrugAndAlcoholTesting,
                AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting],
        };
        var group = EventGroup.Create(Guid.NewGuid(), "Open days", "Choose a date", groups);
        group.AddEvent(eventItem.Id, eventItem.Capacities.Select(x => x.AppointmentTypeId).ToArray(),
            groups, true);
        group.SetEventOpen(eventItem.Id, true);
        Context.EventGroups.Add(group);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var reloaded = await Context.EventGroups.Include(x => x.AttendeeGroups)
            .Include(x => x.Events).SingleAsync(x => x.Id == group.Id);
        Assert.Single(reloaded.AttendeeGroups);
        Assert.True(reloaded.Events.Single().IsOpen);
        Assert.False(reloaded.IsOpen);
        Assert.True(reloaded.Version > 1);
    }
}
