using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Slots;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class RepositoryTests(PostgresFixture fixture)
{
    [Fact]
    public async Task ActiveSlotsAreFilteredByStatusAndDate()
    {
        await fixture.ResetAsync();

        await using (var write = fixture.NewContext())
        {
            write.SlotProposals.Add(ProposalOn(new DateOnly(2026, 9, 1), out var pastSlot));
            write.ConfirmedSlots.Add(pastSlot);

            write.SlotProposals.Add(ProposalOn(new DateOnly(2026, 9, 20), out var cancelled));
            cancelled.Cancel();
            write.ConfirmedSlots.Add(cancelled);

            write.SlotProposals.Add(ProposalOn(new DateOnly(2026, 9, 21), out var live));
            write.ConfirmedSlots.Add(live);

            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var repository = new ConfirmedSlotRepository(read);

        var result = await repository.ListActiveAsync(new DateOnly(2026, 9, 3), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(new DateOnly(2026, 9, 21), result[0].Window.Date);
        Assert.Equal(3, result[0].Capacities.Count);
    }

    [Fact]
    public async Task ACandidateIsFoundByEmailWithTheirRequirements()
    {
        await fixture.ResetAsync();

        await using (var write = fixture.NewContext())
        {
            var pilots = write.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.Pilots);
            var candidate = Candidate.Create(
                Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", pilots);
            write.Candidates.Add(candidate);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var found = await new CandidateRepository(read)
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

    private static SlotProposal ProposalOn(DateOnly date, out ConfirmedSlot slot)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(date, new TimeOnly(9, 0)), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);
        return proposal;
    }
}
