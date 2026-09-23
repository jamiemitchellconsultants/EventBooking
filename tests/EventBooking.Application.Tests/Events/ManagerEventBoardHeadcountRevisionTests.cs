using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

public class ManagerEventBoardHeadcountRevisionTests
{
    private static readonly Guid CurrentManager =
        Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid FormerManager =
        Guid.Parse("c0000011-0000-0000-0000-000000000011");

    private readonly InMemoryEventProposalRepository _proposals = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeClock _clock =
        new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    private GetManagerEventBoardHandler Handler =>
        new(_proposals, _events, _roles, _clock);

    public ManagerEventBoardHeadcountRevisionTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            CurrentManager,
            Role.Manager,
            AppointmentTypeIds.DrugAndAlcoholTesting));
    }

    private EventProposal AddProposal()
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CurrentManager);
        _proposals.Add(proposal);
        return proposal;
    }

    [Fact]
    public async Task MyOpenProposalReturnsMyCurrentAcceptedHeadcount()
    {
        var proposal = AddProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            CurrentManager,
            12);

        var result = await Handler.HandleAsync(
            new GetManagerEventBoardQuery(CurrentManager),
            CancellationToken.None);

        var view = Assert.Single(result.Value.OpenProposals);
        Assert.True(view.AcceptedByMe);
        Assert.Equal(12, view.MyAcceptedHeadcount);
    }

    [Fact]
    public async Task AnotherManagersHeadcountIsNotReturnedAsMine()
    {
        var proposal = AddProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            FormerManager,
            10);

        var result = await Handler.HandleAsync(
            new GetManagerEventBoardQuery(CurrentManager),
            CancellationToken.None);

        var view = Assert.Single(result.Value.OpenProposals);
        Assert.False(view.AcceptedByMe);
        Assert.Null(view.MyAcceptedHeadcount);
        Assert.Equal(
            new[] { "Drug & Alcohol Testing" },
            view.AcceptedByAppointmentTypeNames);
    }
}
