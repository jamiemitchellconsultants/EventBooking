using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class RepositoryTests(PostgresFixture fixture)
{
    [Fact]
    public async Task ActiveEventsAreFilteredByStatusAndDate()
    {
        await fixture.ResetAsync();

        await using (var write = fixture.NewContext())
        {
            write.EventProposals.Add(ProposalOn(new DateOnly(2026, 9, 1), out var pastEvent));
            write.Events.Add(pastEvent);

            write.EventProposals.Add(ProposalOn(new DateOnly(2026, 9, 20), out var cancelled));
            cancelled.Cancel();
            write.Events.Add(cancelled);

            write.EventProposals.Add(ProposalOn(new DateOnly(2026, 9, 21), out var live));
            write.Events.Add(live);

            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var repository = new EventRepository(read);

        var result = await repository.ListActiveAsync(new DateOnly(2026, 9, 3), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(new DateOnly(2026, 9, 21), result[0].Window.Date);
        Assert.Equal(3, result[0].Capacities.Count);
    }

    [Fact]
    public async Task AAttendeeIsFoundByEmailWithTheirRequirements()
    {
        await fixture.ResetAsync();

        await using (var write = fixture.NewContext())
        {
            var pilots = write.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
            var attendee = Attendee.Create(
                Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", pilots);
            write.Attendees.Add(attendee);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var found = await new AttendeeRepository(read)
            .GetByEmailAsync("a.novak@mail.com", CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal("Amara Novak", found!.Name);
        Assert.Equal(2, found.Requirements.Count);
    }

    [Fact]
    public async Task TheSettingsSingletonIsAlwaysThere()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var settings = await new SystemSettingsRepository(context).GetAsync(CancellationToken.None);

        Assert.Equal(4, settings.InviteExpiryDays);
    }

    private static EventProposal ProposalOn(DateOnly date, out Event eventItem)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(9, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        return proposal;
    }
}
