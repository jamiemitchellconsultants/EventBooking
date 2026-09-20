# 01a — Variable-length windows in the location's zone, edits 17 (Task 4)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — tests/EventBooking.Application.Tests/Events/GetManagerEventBoardHandlerTests.cs — 1/1

<!-- retirement-file: {"id":44,"file":"tests/EventBooking.Application.Tests/Events/GetManagerEventBoardHandlerTests.cs","beforeSha":"71ab39bb008ddf287ebb6e0ba7cea26bbfc65e61bc9a3fbe06ed7e508c838cd9","afterSha":"9173bd10d8740a72be5456802476ad4ab287a34babf2b9b124f5d5c33b78ac4a","side":"before","part":1,"parts":1} -->

`````csharp
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
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
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
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 12), new TimeOnly(13, 0)),
            UniformManager);
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
            _proposals.Add(EventProposal.Create(
                Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0)),
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
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(date ?? new DateOnly(2026, 9, 8), new TimeOnly(9, 0)),
            DrugAndAlcoholManager);
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, UniformManager, 8);
        return proposal;
    }
}
`````

## after — tests/EventBooking.Application.Tests/Events/GetManagerEventBoardHandlerTests.cs — 1/1

<!-- retirement-file: {"id":44,"file":"tests/EventBooking.Application.Tests/Events/GetManagerEventBoardHandlerTests.cs","beforeSha":"71ab39bb008ddf287ebb6e0ba7cea26bbfc65e61bc9a3fbe06ed7e508c838cd9","afterSha":"9173bd10d8740a72be5456802476ad4ab287a34babf2b9b124f5d5c33b78ac4a","side":"after","part":1,"parts":1} -->

`````csharp
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
        var proposal = EventProposal.Create(
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
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 12), new TimeOnly(13, 0), 240),
            UniformManager);
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
            _proposals.Add(EventProposal.Create(
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
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(date ?? new DateOnly(2026, 9, 8), new TimeOnly(9, 0), 240),
            DrugAndAlcoholManager);
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, UniformManager, 8);
        return proposal;
    }
}
`````

## before — tests/EventBooking.Application.Tests/Events/ManagerEventBoardHeadcountRevisionTests.cs — 1/1

<!-- retirement-file: {"id":45,"file":"tests/EventBooking.Application.Tests/Events/ManagerEventBoardHeadcountRevisionTests.cs","beforeSha":"b701588adefada6ced00d1e80f6e14203fcef49c7b78ff74b56d61f30980f179","afterSha":"2ca9bcce3ec17148d26a99cf6457170a55fa6e8ad3bbcad54b5c5934cbd8ea40","side":"before","part":1,"parts":1} -->

`````csharp
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
`````

## after — tests/EventBooking.Application.Tests/Events/ManagerEventBoardHeadcountRevisionTests.cs — 1/1

<!-- retirement-file: {"id":45,"file":"tests/EventBooking.Application.Tests/Events/ManagerEventBoardHeadcountRevisionTests.cs","beforeSha":"b701588adefada6ced00d1e80f6e14203fcef49c7b78ff74b56d61f30980f179","afterSha":"2ca9bcce3ec17148d26a99cf6457170a55fa6e8ad3bbcad54b5c5934cbd8ea40","side":"after","part":1,"parts":1} -->

`````csharp
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
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
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
`````

## before — tests/EventBooking.Application.Tests/Events/ProposeEventHandlerTests.cs — 1/1

<!-- retirement-file: {"id":46,"file":"tests/EventBooking.Application.Tests/Events/ProposeEventHandlerTests.cs","beforeSha":"1a72f123b0c0b751edfab5694eb12fbd17c455befbb05bb03dbac62b7dc56f88","afterSha":"b40237f9fdf179b64f37a7884e9744de96abc68805d8d3f593c5244823434ade","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

public class ProposeEventHandlerTests
{
    private static readonly Guid Manager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemoryEventProposalRepository _proposals = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    private ProposeEventHandler Handler => new(_proposals, _roles, _unitOfWork, _audit, _clock);

    public ProposeEventHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            Manager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
    }

    [Fact]
    public async Task AManagerCanProposeAFutureWindow()
    {
        var result = await Handler.HandleAsync(
            new ProposeEventCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var proposal = Assert.Single(_proposals.Items);
        Assert.Equal(result.Value, proposal.Id);
        Assert.Equal(EventProposalStatus.Open, proposal.Status);
        Assert.Equal(new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)), proposal.Window);
        Assert.Equal(Manager, proposal.CreatedByManagerUserId);
        Assert.Empty(proposal.Acceptances);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ProposingWritesAnAuditEntry()
    {
        await Handler.HandleAsync(
            new ProposeEventCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AuditEntityTypes.EventProposal, entry.EntityType);
        Assert.Equal(AuditAction.ProposalCreated, entry.Action);
        Assert.Equal(ActorType.Staff, entry.ActorType);
        Assert.Equal(Manager.ToString(), entry.ActorId);
    }

    [Fact]
    public async Task ACoordinatorCannotProposeAEvent()
    {
        var result = await Handler.HandleAsync(
            new ProposeEventCommand(Coordinator, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal("This staff profile cannot perform this operation.", result.Error.Message);
        Assert.Empty(_proposals.Items);
    }

    [Fact]
    public async Task AnUnknownUserCannotProposeAEvent()
    {
        var result = await Handler.HandleAsync(
            new ProposeEventCommand(Guid.NewGuid(), new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Theory]
    [InlineData(2026, 9, 3)]
    [InlineData(2026, 9, 2)]
    public async Task ATodayOrPastWindowIsRejected(int year, int month, int day)
    {
        var result = await Handler.HandleAsync(
            new ProposeEventCommand(Manager, new DateOnly(year, month, day), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal("A event must be proposed for a future date.", result.Error.Message);
    }

    [Fact]
    public async Task AWindowThatWouldRunPastMidnightIsRejectedByTheDomain()
    {
        var result = await Handler.HandleAsync(
            new ProposeEventCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(22, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal(
            "startTime must leave room for the full 4-hour window on the same day.",
            result.Error.Message);
    }

    [Fact]
    public async Task ASecondOpenProposalForTheSameWindowIsAConflict()
    {
        var command = new ProposeEventCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(9, 0));
        await Handler.HandleAsync(command, CancellationToken.None);

        var result = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("An open proposal already exists for that window.", result.Error.Message);
        Assert.Single(_proposals.Items);
    }

    [Fact]
    public async Task ADifferentWindowOnTheSameDayIsAllowed()
    {
        await Handler.HandleAsync(
            new ProposeEventCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        var result = await Handler.HandleAsync(
            new ProposeEventCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(13, 0)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _proposals.Items.Count);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Events/ProposeEventHandlerTests.cs — 1/1

<!-- retirement-file: {"id":46,"file":"tests/EventBooking.Application.Tests/Events/ProposeEventHandlerTests.cs","beforeSha":"1a72f123b0c0b751edfab5694eb12fbd17c455befbb05bb03dbac62b7dc56f88","afterSha":"b40237f9fdf179b64f37a7884e9744de96abc68805d8d3f593c5244823434ade","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

public class ProposeEventHandlerTests
{
    private static readonly Guid Manager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemoryEventProposalRepository _proposals = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    private ProposeEventHandler Handler => new(_proposals, _roles, _unitOfWork, _audit, _clock);

    public ProposeEventHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            Manager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
    }

    [Fact]
    public async Task AManagerCanProposeAFutureWindow()
    {
        var result = await Handler.HandleAsync(
            new ProposeEventCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var proposal = Assert.Single(_proposals.Items);
        Assert.Equal(result.Value, proposal.Id);
        Assert.Equal(EventProposalStatus.Open, proposal.Status);
        Assert.Equal(new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240), proposal.Window);
        Assert.Equal(Manager, proposal.CreatedByManagerUserId);
        Assert.Empty(proposal.Acceptances);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ProposingWritesAnAuditEntry()
    {
        await Handler.HandleAsync(
            new ProposeEventCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AuditEntityTypes.EventProposal, entry.EntityType);
        Assert.Equal(AuditAction.ProposalCreated, entry.Action);
        Assert.Equal(ActorType.Staff, entry.ActorType);
        Assert.Equal(Manager.ToString(), entry.ActorId);
    }

    [Fact]
    public async Task ACoordinatorCannotProposeAEvent()
    {
        var result = await Handler.HandleAsync(
            new ProposeEventCommand(Coordinator, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal("This staff profile cannot perform this operation.", result.Error.Message);
        Assert.Empty(_proposals.Items);
    }

    [Fact]
    public async Task AnUnknownUserCannotProposeAEvent()
    {
        var result = await Handler.HandleAsync(
            new ProposeEventCommand(Guid.NewGuid(), new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Theory]
    [InlineData(2026, 9, 3)]
    [InlineData(2026, 9, 2)]
    public async Task ATodayOrPastWindowIsRejected(int year, int month, int day)
    {
        var result = await Handler.HandleAsync(
            new ProposeEventCommand(Manager, new DateOnly(year, month, day), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal("A event must be proposed for a future date.", result.Error.Message);
    }

    [Fact]
    public async Task AWindowThatWouldRunPastMidnightIsRejectedByTheDomain()
    {
        var result = await Handler.HandleAsync(
            new ProposeEventCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(22, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal(
            "The window must end on the local date it starts.",
            result.Error.Message);
    }

    [Fact]
    public async Task ASecondOpenProposalForTheSameWindowIsAConflict()
    {
        var command = new ProposeEventCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(9, 0));
        await Handler.HandleAsync(command, CancellationToken.None);

        var result = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("An open proposal already exists for that window.", result.Error.Message);
        Assert.Single(_proposals.Items);
    }

    [Fact]
    public async Task ADifferentWindowOnTheSameDayIsAllowed()
    {
        await Handler.HandleAsync(
            new ProposeEventCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        var result = await Handler.HandleAsync(
            new ProposeEventCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(13, 0)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _proposals.Items.Count);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Events/WithdrawAcceptanceHandlerTests.cs — 1/1

<!-- retirement-file: {"id":47,"file":"tests/EventBooking.Application.Tests/Events/WithdrawAcceptanceHandlerTests.cs","beforeSha":"6e79232d852d105bf1ee95c4506f0809f042f99cc1ec2007033f00441bd22f66","afterSha":"0eecf22581aabd43978e335ffc093a513ea831b676c86f34e7d39ea7c54092ae","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

public class WithdrawAcceptanceHandlerTests
{
    private static readonly Guid DrugAndAlcoholManager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid MedicalManager = Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid UniformManager = Guid.Parse("c0000003-0000-0000-0000-000000000003");

    private readonly InMemoryEventProposalRepository _proposals = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly EventProposal _proposal;

    private WithdrawAcceptanceHandler Handler => new(_proposals, _roles, _unitOfWork, _audit);

    public WithdrawAcceptanceHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            DrugAndAlcoholManager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(
            MedicalManager, Role.Manager, AppointmentTypeIds.MedicalCheckUp));
        _roles.Add(StaffAccessProfile.Create(
            UniformManager, Role.Manager, AppointmentTypeIds.UniformFitting));

        _proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            DrugAndAlcoholManager);
        _proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        _proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        _proposals.Add(_proposal);
    }

    [Fact]
    public async Task AManagerCanTakeTheirOwnAcceptanceBack()
    {
        var result = await Handler.HandleAsync(
            new WithdrawAcceptanceCommand(MedicalManager, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(_proposal.Acceptances);
        Assert.False(_proposal.IsAcceptedBy(AppointmentTypeIds.MedicalCheckUp));
        Assert.Equal(1, _unitOfWork.SaveCount);
        Assert.True(_audit.Contains(AuditAction.AcceptanceWithdrawn));
    }

    [Fact]
    public async Task AManagerWhoNeverAcceptedGetsAValidationFailure()
    {
        var result = await Handler.HandleAsync(
            new WithdrawAcceptanceCommand(UniformManager, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal("This appointment type has not accepted the proposal.", result.Error.Message);
    }

    [Fact]
    public async Task AnAcceptanceCannotBeWithdrawnOnceTheProposalIsConfirmed()
    {
        _proposal.Accept(AppointmentTypeIds.UniformFitting, UniformManager, 8);
        Event.CreateFrom(Guid.NewGuid(), _proposal);

        var result = await Handler.HandleAsync(
            new WithdrawAcceptanceCommand(MedicalManager, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(
            "An acceptance can only be withdrawn while the proposal is still open.",
            result.Error.Message);
    }

    [Fact]
    public async Task AnUnknownProposalIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new WithdrawAcceptanceCommand(MedicalManager, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task ANonManagerIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new WithdrawAcceptanceCommand(Guid.NewGuid(), _proposal.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Events/WithdrawAcceptanceHandlerTests.cs — 1/1

<!-- retirement-file: {"id":47,"file":"tests/EventBooking.Application.Tests/Events/WithdrawAcceptanceHandlerTests.cs","beforeSha":"6e79232d852d105bf1ee95c4506f0809f042f99cc1ec2007033f00441bd22f66","afterSha":"0eecf22581aabd43978e335ffc093a513ea831b676c86f34e7d39ea7c54092ae","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

public class WithdrawAcceptanceHandlerTests
{
    private static readonly Guid DrugAndAlcoholManager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid MedicalManager = Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid UniformManager = Guid.Parse("c0000003-0000-0000-0000-000000000003");

    private readonly InMemoryEventProposalRepository _proposals = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly EventProposal _proposal;

    private WithdrawAcceptanceHandler Handler => new(_proposals, _roles, _unitOfWork, _audit);

    public WithdrawAcceptanceHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            DrugAndAlcoholManager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(
            MedicalManager, Role.Manager, AppointmentTypeIds.MedicalCheckUp));
        _roles.Add(StaffAccessProfile.Create(
            UniformManager, Role.Manager, AppointmentTypeIds.UniformFitting));

        _proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            DrugAndAlcoholManager);
        _proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        _proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        _proposals.Add(_proposal);
    }

    [Fact]
    public async Task AManagerCanTakeTheirOwnAcceptanceBack()
    {
        var result = await Handler.HandleAsync(
            new WithdrawAcceptanceCommand(MedicalManager, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(_proposal.Acceptances);
        Assert.False(_proposal.IsAcceptedBy(AppointmentTypeIds.MedicalCheckUp));
        Assert.Equal(1, _unitOfWork.SaveCount);
        Assert.True(_audit.Contains(AuditAction.AcceptanceWithdrawn));
    }

    [Fact]
    public async Task AManagerWhoNeverAcceptedGetsAValidationFailure()
    {
        var result = await Handler.HandleAsync(
            new WithdrawAcceptanceCommand(UniformManager, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal("This appointment type has not accepted the proposal.", result.Error.Message);
    }

    [Fact]
    public async Task AnAcceptanceCannotBeWithdrawnOnceTheProposalIsConfirmed()
    {
        _proposal.Accept(AppointmentTypeIds.UniformFitting, UniformManager, 8);
        Event.CreateFrom(Guid.NewGuid(), _proposal);

        var result = await Handler.HandleAsync(
            new WithdrawAcceptanceCommand(MedicalManager, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(
            "An acceptance can only be withdrawn while the proposal is still open.",
            result.Error.Message);
    }

    [Fact]
    public async Task AnUnknownProposalIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new WithdrawAcceptanceCommand(MedicalManager, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task ANonManagerIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new WithdrawAcceptanceCommand(Guid.NewGuid(), _proposal.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Events/WithdrawProposalHandlerTests.cs — 1/1

<!-- retirement-file: {"id":48,"file":"tests/EventBooking.Application.Tests/Events/WithdrawProposalHandlerTests.cs","beforeSha":"e0864048f25ed5c082e0d96d28b59079ac0c9eeb1e3199dda49b69318a25fbca","afterSha":"38b246754fbdb34702bc43212c15519ba485414989b4c6684adc88666eb4de07","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

public class WithdrawProposalHandlerTests
{
    private static readonly Guid Creator = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid OtherManager = Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid Admin = Guid.Parse("a0000001-0000-0000-0000-000000000001");

    private readonly InMemoryEventProposalRepository _proposals = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly EventProposal _proposal;

    private WithdrawProposalHandler Handler => new(_proposals, _roles, _unitOfWork, _audit);

    public WithdrawProposalHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            Creator, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(
            OtherManager, Role.Manager, AppointmentTypeIds.MedicalCheckUp));
        _roles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));

        _proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            Creator);
        _proposals.Add(_proposal);
    }

    [Fact]
    public async Task TheCreatorCanWithdrawItAndItLeavesTheOpenList()
    {
        var result = await Handler.HandleAsync(
            new WithdrawProposalCommand(Creator, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EventProposalStatus.Withdrawn, _proposal.Status);
        Assert.Empty(await _proposals.ListOpenAsync(CancellationToken.None));
        Assert.True(_audit.Contains(AuditAction.ProposalWithdrawn));
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnotherManagerCanWithdrawIt()
    {
        var result = await Handler.HandleAsync(
            new WithdrawProposalCommand(OtherManager, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EventProposalStatus.Withdrawn, _proposal.Status);
        Assert.True(_audit.Contains(AuditAction.ProposalWithdrawn));
    }

    [Fact]
    public async Task AnAdminCanWithdrawIt()
    {
        var result = await Handler.HandleAsync(
            new WithdrawProposalCommand(Admin, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EventProposalStatus.Withdrawn, _proposal.Status);
        Assert.True(_audit.Contains(AuditAction.ProposalWithdrawn));
    }

    [Fact]
    public async Task AlreadyWithdrawnIsAConflict()
    {
        _proposal.Withdraw(Creator);

        var result = await Handler.HandleAsync(
            new WithdrawProposalCommand(Creator, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("Only an open proposal can be withdrawn.", result.Error.Message);
    }

    [Fact]
    public async Task AnUnknownProposalIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new WithdrawProposalCommand(Creator, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Events/WithdrawProposalHandlerTests.cs — 1/1

<!-- retirement-file: {"id":48,"file":"tests/EventBooking.Application.Tests/Events/WithdrawProposalHandlerTests.cs","beforeSha":"e0864048f25ed5c082e0d96d28b59079ac0c9eeb1e3199dda49b69318a25fbca","afterSha":"38b246754fbdb34702bc43212c15519ba485414989b4c6684adc88666eb4de07","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

public class WithdrawProposalHandlerTests
{
    private static readonly Guid Creator = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid OtherManager = Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid Admin = Guid.Parse("a0000001-0000-0000-0000-000000000001");

    private readonly InMemoryEventProposalRepository _proposals = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly EventProposal _proposal;

    private WithdrawProposalHandler Handler => new(_proposals, _roles, _unitOfWork, _audit);

    public WithdrawProposalHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            Creator, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(
            OtherManager, Role.Manager, AppointmentTypeIds.MedicalCheckUp));
        _roles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));

        _proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            Creator);
        _proposals.Add(_proposal);
    }

    [Fact]
    public async Task TheCreatorCanWithdrawItAndItLeavesTheOpenList()
    {
        var result = await Handler.HandleAsync(
            new WithdrawProposalCommand(Creator, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EventProposalStatus.Withdrawn, _proposal.Status);
        Assert.Empty(await _proposals.ListOpenAsync(CancellationToken.None));
        Assert.True(_audit.Contains(AuditAction.ProposalWithdrawn));
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnotherManagerCanWithdrawIt()
    {
        var result = await Handler.HandleAsync(
            new WithdrawProposalCommand(OtherManager, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EventProposalStatus.Withdrawn, _proposal.Status);
        Assert.True(_audit.Contains(AuditAction.ProposalWithdrawn));
    }

    [Fact]
    public async Task AnAdminCanWithdrawIt()
    {
        var result = await Handler.HandleAsync(
            new WithdrawProposalCommand(Admin, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EventProposalStatus.Withdrawn, _proposal.Status);
        Assert.True(_audit.Contains(AuditAction.ProposalWithdrawn));
    }

    [Fact]
    public async Task AlreadyWithdrawnIsAConflict()
    {
        _proposal.Withdraw(Creator);

        var result = await Handler.HandleAsync(
            new WithdrawProposalCommand(Creator, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("Only an open proposal can be withdrawn.", result.Error.Message);
    }

    [Fact]
    public async Task AnUnknownProposalIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new WithdrawProposalCommand(Creator, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs — 1/1

<!-- retirement-file: {"id":49,"file":"tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs","beforeSha":"170ef7534fd0bc180f57d1a33295cee9257dd49ffcaf3e8e6e560b8eb93e0f27","afterSha":"03352c0fafa322f59ff041a5918110d95f9040daad0234e05df39659c4f9ef96","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Fakes;

public class FakesSelfTests
{
    [Fact]
    public void TheClockCanBeMoved()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

        clock.Advance(TimeSpan.FromDays(5));

        Assert.Equal(new DateOnly(2026, 9, 8), clock.TodayAtTransitionalLocation);
    }

    [Fact]
    public async Task TheAttendeeRepositoryFiltersByStatus()
    {
        var repository = new InMemoryAttendeeRepository();
        var uniformOnly = AttendeeGroup.Define(
            Guid.NewGuid(), "UNI_ONLY", "UNI only", true, [AppointmentTypeIds.UniformFitting]);
        var invited = Attendee.Create(
            Guid.NewGuid(), "B. Chen", "b.chen@mail.com", uniformOnly);
        invited.MarkInvited();
        repository.Add(invited);
        repository.Add(Attendee.Create(
            Guid.NewGuid(), "A. Novak", "a.novak@mail.com", uniformOnly));

        var result = await repository.ListAsync(AttendeeStatus.Invited, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("b.chen@mail.com", result[0].Email);
    }

    [Fact]
    public async Task TheEventRepositoryHidesCancelledAndPastEvents()
    {
        var repository = new InMemoryEventRepository();
        repository.Add(EventFor(new DateOnly(2026, 9, 1)));
        var cancelled = EventFor(new DateOnly(2026, 9, 20));
        cancelled.Cancel();
        repository.Add(cancelled);
        repository.Add(EventFor(new DateOnly(2026, 9, 21)));

        var result = await repository.ListActiveAsync(new DateOnly(2026, 9, 3), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(new DateOnly(2026, 9, 21), result[0].Window.Date);
    }

    [Fact]
    public async Task TheCapacityRepositoryReturnsRowsInAppointmentTypeOrder()
    {
        var events = new InMemoryEventRepository();
        var eventItem = EventFor(new DateOnly(2026, 9, 21));
        events.Add(eventItem);
        var capacities = new InMemoryEventCapacityRepository(events);

        var locked = await capacities
            .LockForUpdateAsync(eventItem.Id, AppointmentTypeIds.All, CancellationToken.None);

        Assert.Equal(3, locked.Count);
        Assert.Equal(
            locked.Select(c => c.AppointmentTypeId).OrderBy(id => id),
            locked.Select(c => c.AppointmentTypeId));
        Assert.Equal(1, capacities.LockCallCount);
    }

    [Fact]
    public void TheTokenServiceRoundTripsAnIdentifier()
    {
        var service = new FakeTokenService();
        var id = Guid.NewGuid();

        var issued = service.Issue(id);

        Assert.True(service.TryRead(issued.Token, out var read));
        Assert.Equal(id, read);
        Assert.Equal(issued.TokenHash, service.Hash(issued.Token));
        Assert.False(service.TryRead("nonsense", out _));
    }

    [Fact]
    public void TheAuditLoggerRecordsWhatItIsGiven()
    {
        var logger = new RecordingAuditLogger();

        logger.Record(
            AuditEntityTypes.Booking, Guid.NewGuid(), AuditAction.BookingCreated,
            ActorType.AttendeeToken, "invite-1", "chose option 2");

        Assert.True(logger.Contains(AuditAction.BookingCreated));
        Assert.Equal("chose option 2", logger.Entries.Single().Details);
    }

    private static Event EventFor(DateOnly date)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(9, 0)), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        return Event.CreateFrom(Guid.NewGuid(), proposal);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs — 1/1

<!-- retirement-file: {"id":49,"file":"tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs","beforeSha":"170ef7534fd0bc180f57d1a33295cee9257dd49ffcaf3e8e6e560b8eb93e0f27","afterSha":"03352c0fafa322f59ff041a5918110d95f9040daad0234e05df39659c4f9ef96","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Fakes;

public class FakesSelfTests
{
    [Fact]
    public void TheClockCanBeMoved()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

        clock.Advance(TimeSpan.FromDays(5));

        Assert.Equal(new DateOnly(2026, 9, 8), clock.TodayAtTransitionalLocation);
    }

    [Fact]
    public async Task TheAttendeeRepositoryFiltersByStatus()
    {
        var repository = new InMemoryAttendeeRepository();
        var uniformOnly = AttendeeGroup.Define(
            Guid.NewGuid(), "UNI_ONLY", "UNI only", true, [AppointmentTypeIds.UniformFitting]);
        var invited = Attendee.Create(
            Guid.NewGuid(), "B. Chen", "b.chen@mail.com", uniformOnly);
        invited.MarkInvited();
        repository.Add(invited);
        repository.Add(Attendee.Create(
            Guid.NewGuid(), "A. Novak", "a.novak@mail.com", uniformOnly));

        var result = await repository.ListAsync(AttendeeStatus.Invited, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("b.chen@mail.com", result[0].Email);
    }

    [Fact]
    public async Task TheEventRepositoryHidesCancelledAndPastEvents()
    {
        var repository = new InMemoryEventRepository();
        repository.Add(EventFor(new DateOnly(2026, 9, 1)));
        var cancelled = EventFor(new DateOnly(2026, 9, 20));
        cancelled.Cancel();
        repository.Add(cancelled);
        repository.Add(EventFor(new DateOnly(2026, 9, 21)));

        var result = await repository.ListActiveAsync(new DateOnly(2026, 9, 3), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(new DateOnly(2026, 9, 21), result[0].Window.Date);
    }

    [Fact]
    public async Task TheCapacityRepositoryReturnsRowsInAppointmentTypeOrder()
    {
        var events = new InMemoryEventRepository();
        var eventItem = EventFor(new DateOnly(2026, 9, 21));
        events.Add(eventItem);
        var capacities = new InMemoryEventCapacityRepository(events);

        var locked = await capacities
            .LockForUpdateAsync(eventItem.Id, AppointmentTypeIds.All, CancellationToken.None);

        Assert.Equal(3, locked.Count);
        Assert.Equal(
            locked.Select(c => c.AppointmentTypeId).OrderBy(id => id),
            locked.Select(c => c.AppointmentTypeId));
        Assert.Equal(1, capacities.LockCallCount);
    }

    [Fact]
    public void TheTokenServiceRoundTripsAnIdentifier()
    {
        var service = new FakeTokenService();
        var id = Guid.NewGuid();

        var issued = service.Issue(id);

        Assert.True(service.TryRead(issued.Token, out var read));
        Assert.Equal(id, read);
        Assert.Equal(issued.TokenHash, service.Hash(issued.Token));
        Assert.False(service.TryRead("nonsense", out _));
    }

    [Fact]
    public void TheAuditLoggerRecordsWhatItIsGiven()
    {
        var logger = new RecordingAuditLogger();

        logger.Record(
            AuditEntityTypes.Booking, Guid.NewGuid(), AuditAction.BookingCreated,
            ActorType.AttendeeToken, "invite-1", "chose option 2");

        Assert.True(logger.Contains(AuditAction.BookingCreated));
        Assert.Equal("chose option 2", logger.Entries.Single().Details);
    }

    private static Event EventFor(DateOnly date)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(9, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        return Event.CreateFrom(Guid.NewGuid(), proposal);
    }
}
`````
