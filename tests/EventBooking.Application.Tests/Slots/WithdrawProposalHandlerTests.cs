using EventBooking.Application.Slots;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Slots;

public class WithdrawProposalHandlerTests
{
    private static readonly Guid Creator = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid OtherManager = Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid Admin = Guid.Parse("a0000001-0000-0000-0000-000000000001");

    private readonly InMemorySlotProposalRepository _proposals = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly SlotProposal _proposal;

    private WithdrawProposalHandler Handler => new(_proposals, _roles, _unitOfWork, _audit);

    public WithdrawProposalHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            Creator, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(
            OtherManager, Role.Manager, AppointmentTypeIds.MedicalCheckUp));
        _roles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));

        _proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            Creator);
        _proposals.Add(_proposal);
    }

    [Fact]
    public async Task TheCreatorCanWithdrawItAndItLeavesTheOpenList()
    {
        var result = await Handler.HandleAsync(
            new WithdrawProposalCommand(Creator, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SlotProposalStatus.Withdrawn, _proposal.Status);
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
        Assert.Equal(SlotProposalStatus.Withdrawn, _proposal.Status);
        Assert.True(_audit.Contains(AuditAction.ProposalWithdrawn));
    }

    [Fact]
    public async Task AnAdminCanWithdrawIt()
    {
        var result = await Handler.HandleAsync(
            new WithdrawProposalCommand(Admin, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SlotProposalStatus.Withdrawn, _proposal.Status);
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
