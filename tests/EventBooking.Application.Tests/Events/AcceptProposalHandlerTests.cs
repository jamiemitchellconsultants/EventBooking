using EventBooking.Application.Common;
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

public class AcceptProposalHandlerTests
{
    private static readonly Guid DrugAndAlcoholManager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid MedicalManager = Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid UniformManager = Guid.Parse("c0000003-0000-0000-0000-000000000003");
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemoryEventProposalRepository _proposals = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly EventProposal _proposal;

    private AcceptProposalHandler Handler =>
        new(_proposals, _events, _roles, _unitOfWork, _audit);

    public AcceptProposalHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            DrugAndAlcoholManager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(
            MedicalManager, Role.Manager, AppointmentTypeIds.MedicalCheckUp));
        _roles.Add(StaffAccessProfile.Create(
            UniformManager, Role.Manager, AppointmentTypeIds.UniformFitting));
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));

        _proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            DrugAndAlcoholManager);
        _proposals.Add(_proposal);
    }

    private Task<Result<AcceptProposalOutcome>> Accept(Guid manager, int headcount) =>
        Handler.HandleAsync(
            new AcceptProposalCommand(manager, _proposal.Id, headcount), CancellationToken.None);

    [Fact]
    public async Task TheFirstAcceptRecordsAHeadcountAndConfirmsNothing()
    {
        var result = await Accept(DrugAndAlcoholManager, 10);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.EventId);
        Assert.Equal(_proposal.Id, result.Value.ProposalId);

        var acceptance = Assert.Single(_proposal.Acceptances);
        Assert.Equal(AppointmentTypeIds.DrugAndAlcoholTesting, acceptance.AppointmentTypeId);
        Assert.Equal(10, acceptance.Headcount);
        Assert.Empty(_events.Items);
        Assert.Equal(EventProposalStatus.Open, _proposal.Status);
    }

    [Fact]
    public async Task TheThirdAcceptConfirmsTheEventAndInitialisesCapacity()
    {
        await Accept(DrugAndAlcoholManager, 10);
        await Accept(MedicalManager, 6);

        var result = await Accept(UniformManager, 8);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.EventId);

        var eventItem = Assert.Single(_events.Items);
        Assert.Equal(result.Value.EventId, eventItem.Id);
        Assert.Equal(_proposal.Id, eventItem.ProposalId);
        Assert.Equal(_proposal.Window, eventItem.Window);
        Assert.Equal(EventStatus.Active, eventItem.Status);
        Assert.Equal(EventProposalStatus.Confirmed, _proposal.Status);

        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(6, eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(8, eventItem.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
    }

    [Fact]
    public async Task ConfirmationWritesBothAnAcceptanceAndAConfirmationAuditEntry()
    {
        await Accept(DrugAndAlcoholManager, 10);
        await Accept(MedicalManager, 6);
        await Accept(UniformManager, 8);

        Assert.Equal(3, _audit.Entries.Count(e => e.Action == AuditAction.AcceptanceRecorded));
        var confirmation = Assert.Single(_audit.Entries, e => e.Action == AuditAction.EventConfirmed);
        Assert.Equal(AuditEntityTypes.Event, confirmation.EntityType);
        Assert.Equal(ActorType.Staff, confirmation.ActorType);
    }

    [Fact]
    public async Task AManagerCanReviseTheirAcceptanceWhileTheProposalIsOpen()
    {
        await Accept(DrugAndAlcoholManager, 10);

        var result = await Accept(DrugAndAlcoholManager, 12);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.EventId);
        Assert.Equal(12, Assert.Single(_proposal.Acceptances).Headcount);
    }

    [Fact]
    public async Task ACoordinatorCannotAccept()
    {
        var result = await Accept(Coordinator, 10);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal("This staff profile cannot perform this operation.", result.Error.Message);
    }

    [Fact]
    public async Task AnInvalidHeadcountIsRejected()
    {
        var result = await Accept(DrugAndAlcoholManager, 0);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal("headcount must be greater than zero.", result.Error.Message);
    }

    [Fact]
    public async Task AnUnknownProposalIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new AcceptProposalCommand(DrugAndAlcoholManager, Guid.NewGuid(), 10),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Equal("No such proposal.", result.Error.Message);
    }

    [Fact]
    public async Task AWithdrawnProposalCannotBeAccepted()
    {
        _proposal.Withdraw(DrugAndAlcoholManager);

        var result = await Accept(MedicalManager, 6);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal("Only an open proposal can be accepted.", result.Error.Message);
    }

    [Fact]
    public async Task EachAcceptSavesOnce()
    {
        await Accept(DrugAndAlcoholManager, 10);
        await Accept(MedicalManager, 6);

        Assert.Equal(2, _unitOfWork.SaveCount);
    }
}
