using EventBooking.Application.Access;
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Tests.Events;

public class CombinedManagerAuthorizationTests
{
    [Fact]
    public async Task ACoordinatorManagerCanProposeUsingTheManagerCapability()
    {
        var staffUserId = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            staffUserId,
            [Role.Coordinator, Role.Manager],
            AppointmentTypeIds.DrugAndAlcoholTesting));
        var proposals = new InMemoryEventProposalRepository();
        var handler = new ProposeEventHandler(
            proposals,
            new StaffAccessAuthorizer(profiles),
            new FakeUnitOfWork(),
            new RecordingAuditLogger(),
            new FakeClock(),
            ProposalFixture.Zones);

        var result = await handler.HandleAsync(
            new ProposeEventCommand(staffUserId, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(proposals.Items);
    }

    [Fact]
    public async Task AppointmentStaffWithoutManagerCannotPropose()
    {
        var staffUserId = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            staffUserId,
            [Role.AppointmentStaff],
            AppointmentTypeIds.DrugAndAlcoholTesting));
        var proposals = new InMemoryEventProposalRepository();
        var handler = new ProposeEventHandler(
            proposals,
            new StaffAccessAuthorizer(profiles),
            new FakeUnitOfWork(),
            new RecordingAuditLogger(),
            new FakeClock(),
            ProposalFixture.Zones);

        var result = await handler.HandleAsync(
            new ProposeEventCommand(staffUserId, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(proposals.Items);
    }
}
