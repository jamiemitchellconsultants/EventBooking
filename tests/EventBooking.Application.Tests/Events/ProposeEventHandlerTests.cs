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
