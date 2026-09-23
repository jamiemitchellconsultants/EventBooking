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
