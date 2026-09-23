using EventBooking.Application.Common;
using EventBooking.Application.Negotiation;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using EventBooking.Domain.Locations;
using EventBooking.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Infrastructure.Tests.Concurrency;

[Collection("postgres")]
public sealed class AcceptanceRaceTests(PostgresFixture fixture)
{
    private static readonly Guid DatManagerId = Guid.Parse("c0000011-0000-0000-0000-000000000001");
    private static readonly Guid MedManagerId = Guid.Parse("c0000012-0000-0000-0000-000000000002");
    private static readonly Guid UniManagerId = Guid.Parse("c0000013-0000-0000-0000-000000000003");
    private static readonly Guid EscManagerId = Guid.Parse("c0000014-0000-0000-0000-000000000004");
    private static readonly Guid EscTypeId = Guid.Parse("e0000001-0000-0000-0000-000000000001");

    // A 4-type proposal with 1 accepted; three Managers accept concurrently, 50 runs over
    // fresh proposals; each run yields exactly one Event, exactly 4 capacity rows, and a
    // Confirmed proposal. The pin is the proposal row lock: delete it and duplicate events
    // (or conflicts) appear within seconds.
    [Fact]
    public async Task Concurrent_final_acceptances_confirm_exactly_one_event()
    {
        await using var harness = await ConcurrencyHarness.CreateAsync(fixture);
        await SeedRolesAsync();

        for (var run = 0; run < 50; run++)
        {
            var proposalId = await SeedProposalAsync(run);
            var outcomes = await Task.WhenAll(
                AcceptOnFreshScopeAsync(harness, MedManagerId, proposalId, 6),
                AcceptOnFreshScopeAsync(harness, UniManagerId, proposalId, 8),
                AcceptOnFreshScopeAsync(harness, EscManagerId, proposalId, 12));

            Assert.All(outcomes, o => Assert.True(o.IsSuccess));
            Assert.Equal(EventProposalStatus.Confirmed, await ProposalStatusAsync(proposalId));
            Assert.Single(await EventsForProposalAsync(proposalId));
            Assert.Equal([6, 8, 10, 12], await CapacitiesForProposalAsync(proposalId));
        }
    }

    private async Task SeedRolesAsync()
    {
        await using var context = fixture.NewContext();
        context.AppointmentTypes.Add(AppointmentType.Create(EscTypeId, "ESC", "Escalation"));
        context.StaffAccessProfiles.AddRange(
            StaffAccessProfile.Create(DatManagerId, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting),
            StaffAccessProfile.Create(MedManagerId, Role.Manager, AppointmentTypeIds.MedicalCheckUp),
            StaffAccessProfile.Create(UniManagerId, Role.Manager, AppointmentTypeIds.UniformFitting),
            StaffAccessProfile.Create(EscManagerId, Role.Manager, EscTypeId));
        await context.SaveChangesAsync();
    }

    private async Task<Guid> SeedProposalAsync(int run)
    {
        var zones = new NodaTimeEventWindowZones();
        var proposal = EventProposal.Propose(
            Guid.NewGuid(),
            TransitionalLocation.Id,
            locationIsActive: true,
            TransitionalLocation.TimeZoneId,
            new EventWindow(
                DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30 + run), new TimeOnly(9, 0), 240),
            zones,
            DateTimeOffset.UtcNow,
            [
                new(AppointmentTypeIds.DrugAndAlcoholTesting, "DAT", true, true),
                new(AppointmentTypeIds.MedicalCheckUp, "MED", true, true),
                new(AppointmentTypeIds.UniformFitting, "UNI", true, true),
                new(EscTypeId, "ESC", true, true),
            ],
            AppointmentTypeIds.DrugAndAlcoholTesting,
            DatManagerId,
            headcount: 10);
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, DatManagerId, 10);

        await using var context = fixture.NewContext();
        context.EventProposals.Add(proposal);
        await context.SaveChangesAsync();
        return proposal.Id;
    }

    private static async Task<Result<RecordAcceptanceOutcome>> AcceptOnFreshScopeAsync(
        ConcurrencyHarness harness, Guid managerId, Guid proposalId, int headcount)
    {
        using var scope = harness.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<RecordAcceptanceHandler>();
        return await handler.HandleAsync(
            new RecordAcceptanceCommand(managerId, proposalId, headcount), CancellationToken.None);
    }

    private async Task<EventProposalStatus> ProposalStatusAsync(Guid proposalId)
    {
        await using var context = fixture.NewContext();
        return (await context.EventProposals.SingleAsync(p => p.Id == proposalId)).Status;
    }

    private async Task<IReadOnlyList<Guid>> EventsForProposalAsync(Guid proposalId)
    {
        await using var context = fixture.NewContext();
        return await context.Events
            .Where(e => e.ProposalId == proposalId)
            .Select(e => e.Id)
            .ToListAsync();
    }

    private async Task<IReadOnlyList<int>> CapacitiesForProposalAsync(Guid proposalId)
    {
        await using var context = fixture.NewContext();
        var capacities = await context.Events
            .Where(e => e.ProposalId == proposalId)
            .SelectMany(e => e.Capacities)
            .Select(c => c.TotalHeadcount)
            .ToListAsync();
        return capacities.Order().ToList();
    }
}
