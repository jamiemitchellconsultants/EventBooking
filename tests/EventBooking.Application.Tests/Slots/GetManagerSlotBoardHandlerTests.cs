using EventBooking.Application.Slots;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Slots;

public class GetManagerSlotBoardHandlerTests
{
    private static readonly Guid DrugAndAlcoholManager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid MedicalManager = Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid UniformManager = Guid.Parse("c0000003-0000-0000-0000-000000000003");

    private readonly InMemorySlotProposalRepository _proposals = new();
    private readonly InMemoryConfirmedSlotRepository _confirmedSlots = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    private GetManagerSlotBoardHandler Handler =>
        new(_proposals, _confirmedSlots, _roles, _clock);

    public GetManagerSlotBoardHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            DrugAndAlcoholManager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(
            MedicalManager, Role.Manager, AppointmentTypeIds.MedicalCheckUp));
        _roles.Add(StaffAccessProfile.Create(
            UniformManager, Role.Manager, AppointmentTypeIds.UniformFitting));
    }

    [Fact]
    public async Task OpenProposalsShowWhoHasAcceptedAndWhetherIHave()
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            DrugAndAlcoholManager);
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        _proposals.Add(proposal);

        var board = (await Handler.HandleAsync(
            new GetManagerSlotBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        var view = Assert.Single(board.OpenProposals);
        Assert.Equal(new DateOnly(2026, 9, 10), view.Date);
        Assert.Equal(new TimeOnly(9, 0), view.StartTime);
        Assert.Equal(new TimeOnly(13, 0), view.EndTime);
        Assert.Equal(
            new[] { "Drug & Alcohol Testing", "Medical Check-up" },
            view.AcceptedByAppointmentTypeNames);
        Assert.True(view.AcceptedByMe);
        Assert.True(view.CreatedByMe);
    }

    [Fact]
    public async Task AProposalIAmYetToAcceptIsFlaggedAsSuch()
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, 12), new TimeOnly(13, 0)),
            UniformManager);
        proposal.Accept(AppointmentTypeIds.UniformFitting, UniformManager, 8);
        _proposals.Add(proposal);

        var board = (await Handler.HandleAsync(
            new GetManagerSlotBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        var view = Assert.Single(board.OpenProposals);
        Assert.False(view.AcceptedByMe);
        Assert.False(view.CreatedByMe);
        Assert.Equal(new[] { "Uniform Fitting" }, view.AcceptedByAppointmentTypeNames);
    }

    [Fact]
    public async Task OpenProposalsAreOrderedEarliestFirst()
    {
        foreach (var day in new[] { 14, 10, 12 })
        {
            _proposals.Add(SlotProposal.Create(
                Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0)),
                UniformManager));
        }

        var board = (await Handler.HandleAsync(
            new GetManagerSlotBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        Assert.Equal(
            new[] { new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 14) },
            board.OpenProposals.Select(p => p.Date));
    }

    [Fact]
    public async Task ConfirmedSlotsShowOnlyMyOwnHeadcountAndRemainder()
    {
        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());
        slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        _confirmedSlots.Add(slot);

        var board = (await Handler.HandleAsync(
            new GetManagerSlotBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        var view = Assert.Single(board.ConfirmedSlots);
        Assert.Equal(10, view.MyHeadcount);
        Assert.Equal(8, view.MyRemainingCapacity);
    }

    [Fact]
    public async Task CancelledAndPastSlotsAreNotOnTheBoard()
    {
        var cancelled = ConfirmedSlot.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());
        cancelled.Cancel();
        _confirmedSlots.Add(cancelled);
        _confirmedSlots.Add(ConfirmedSlot.CreateFrom(
            Guid.NewGuid(), FullyAcceptedProposal(new DateOnly(2026, 8, 30))));

        var board = (await Handler.HandleAsync(
            new GetManagerSlotBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        Assert.Empty(board.ConfirmedSlots);
    }

    [Fact]
    public async Task ANonManagerIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new GetManagerSlotBoardQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    private static SlotProposal FullyAcceptedProposal(DateOnly? date = null)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(date ?? new DateOnly(2026, 9, 8), new TimeOnly(9, 0)),
            DrugAndAlcoholManager);
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, UniformManager, 8);
        return proposal;
    }
}
