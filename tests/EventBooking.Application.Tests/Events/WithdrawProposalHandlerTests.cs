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

        _proposal = ProposalFixture.Create(
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
    public async Task AManagerOfAnotherListedTypeCannotWithdrawIt()
    {
        // FR-2.9: the whole proposal belongs to the proposing type. Another listed type may only
        // withdraw its own acceptance.
        var result = await Handler.HandleAsync(
            new WithdrawProposalCommand(OtherManager, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EventProposalStatus.Open, _proposal.Status);
        Assert.False(_audit.Contains(AuditAction.ProposalWithdrawn));
    }

    [Fact]
    public async Task AnAdminCannotWithdrawIt()
    {
        // Admin holds no negotiation capability in the design's matrix, and has no appointment
        // type to act for; the predecessor's Admin fallback is retired.
        var result = await Handler.HandleAsync(
            new WithdrawProposalCommand(Admin, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EventProposalStatus.Open, _proposal.Status);
    }

    [Fact]
    public async Task AlreadyWithdrawnIsAConflict()
    {
        _proposal.Withdraw(ProposalFixture.ProposerType);

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
