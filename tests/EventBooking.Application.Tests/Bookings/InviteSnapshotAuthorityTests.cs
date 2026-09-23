using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies portal and confirmation treat Invite Requirements as immutable authority.</summary>
public sealed class InviteSnapshotAuthorityTests
{
    /// <summary>The portal displays snapshot names rather than a later Candidate collection.</summary>
    [Fact]
    public async Task ViewInviteUsesPersistedSnapshot()
    {
        var group = EmployeeGroup.Define(
            EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        var candidate = Candidate.Create(Guid.NewGuid(), "Amara", "amara@example.com", group);
        var candidates = new InMemoryCandidateRepository();
        candidates.Add(candidate);
        var slots = new InMemoryConfirmedSlotRepository();
        var options = Enumerable.Range(0, 3).Select(index =>
            ConfirmedSlot.CreateImported(
                Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 10, 10 + index), new TimeOnly(9, 0)),
                new Dictionary<Guid, int>
                {
                    [AppointmentTypeIds.DrugAndAlcoholTesting] = 5,
                    [AppointmentTypeIds.MedicalCheckUp] = 5,
                    [AppointmentTypeIds.UniformFitting] = 5,
                })).ToList();
        slots.Items.AddRange(options);
        var tokens = new FakeTokenService();
        var issued = tokens.Issue(Guid.NewGuid());
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidate.Id, issued.TokenHash, DateTimeOffset.Parse("2026-10-01T00:00:00Z"),
            options.Select(slot => slot.Id), [AppointmentTypeIds.MedicalCheckUp], 0);
        var invites = new InMemoryInviteRepository();
        invites.Add(invite);
        var clock = new FakeClock(DateTimeOffset.Parse("2026-09-20T00:00:00Z"));

        var result = await new ViewInviteHandler(
                invites, candidates, slots, new EligibleSlotFinder(slots, clock),
                new RecordingAuditLogger(), new FakeUnitOfWork(), tokens, clock)
            .HandleAsync(new ViewInviteQuery(issued.Token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["Medical Check-up"], result.Value.AppointmentTypeNames);
    }
}
