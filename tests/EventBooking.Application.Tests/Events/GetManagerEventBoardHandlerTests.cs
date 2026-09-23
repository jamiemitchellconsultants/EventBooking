using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

public class GetManagerEventBoardHandlerTests
{
    private static readonly Guid DrugAndAlcoholManager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid MedicalManager = Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid UniformManager = Guid.Parse("c0000003-0000-0000-0000-000000000003");

    private readonly InMemoryEventProposalRepository _proposals = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    private GetManagerEventBoardHandler Handler =>
        new(_proposals, _events, _roles, _clock);

    public GetManagerEventBoardHandlerTests()
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
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            DrugAndAlcoholManager);
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        _proposals.Add(proposal);

        var board = (await Handler.HandleAsync(
            new GetManagerEventBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

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
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 12), new TimeOnly(13, 0), 240),
            UniformManager, AppointmentTypeIds.UniformFitting);
        proposal.Accept(AppointmentTypeIds.UniformFitting, UniformManager, 8);
        _proposals.Add(proposal);

        var board = (await Handler.HandleAsync(
            new GetManagerEventBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

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
            _proposals.Add(ProposalFixture.Create(
                Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0), 240),
                UniformManager));
        }

        var board = (await Handler.HandleAsync(
            new GetManagerEventBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        Assert.Equal(
            new[] { new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 14) },
            board.OpenProposals.Select(p => p.Date));
    }

    [Fact]
    public async Task EventsShowOnlyMyOwnHeadcountAndRemainder()
    {
        var eventItem = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());
        eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        _events.Add(eventItem);

        var board = (await Handler.HandleAsync(
            new GetManagerEventBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        var view = Assert.Single(board.Events);
        Assert.Equal(10, view.MyHeadcount);
        Assert.Equal(8, view.MyRemainingCapacity);
    }

    [Fact]
    public async Task CancelledAndPastEventsAreNotOnTheBoard()
    {
        var cancelled = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());
        cancelled.Cancel();
        _events.Add(cancelled);
        _events.Add(Event.CreateFrom(
            Guid.NewGuid(), FullyAcceptedProposal(new DateOnly(2026, 8, 30))));

        var board = (await Handler.HandleAsync(
            new GetManagerEventBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        Assert.Empty(board.Events);
    }

    [Fact]
    public async Task ANonManagerIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new GetManagerEventBoardQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    private static EventProposal FullyAcceptedProposal(DateOnly? date = null)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(date ?? new DateOnly(2026, 9, 8), new TimeOnly(9, 0), 240),
            DrugAndAlcoholManager);
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, UniformManager, 8);
        return proposal;
    }
}
