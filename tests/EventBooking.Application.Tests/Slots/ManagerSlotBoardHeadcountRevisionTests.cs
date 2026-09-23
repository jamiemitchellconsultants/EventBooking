using EventBooking.Application.Slots;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Slots;

public class ManagerSlotBoardHeadcountRevisionTests
{
    private static readonly Guid CurrentManager =
        Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid FormerManager =
        Guid.Parse("c0000011-0000-0000-0000-000000000011");

    private readonly InMemorySlotProposalRepository _proposals = new();
    private readonly InMemoryConfirmedSlotRepository _confirmedSlots = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeClock _clock =
        new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    private GetManagerSlotBoardHandler Handler =>
        new(_proposals, _confirmedSlots, _roles, _clock);

    public ManagerSlotBoardHeadcountRevisionTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            CurrentManager,
            Role.Manager,
            AppointmentTypeIds.DrugAndAlcoholTesting));
    }

    private SlotProposal AddProposal()
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
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
            new GetManagerSlotBoardQuery(CurrentManager),
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
            new GetManagerSlotBoardQuery(CurrentManager),
            CancellationToken.None);

        var view = Assert.Single(result.Value.OpenProposals);
        Assert.False(view.AcceptedByMe);
        Assert.Null(view.MyAcceptedHeadcount);
        Assert.Equal(
            new[] { "Drug & Alcohol Testing" },
            view.AcceptedByAppointmentTypeNames);
    }
}
