using EventBooking.Application.Common;
using EventBooking.Application.Slots;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Slots;

public class AcceptProposalHeadcountRevisionTests
{
    private static readonly Guid DrugAndAlcoholManager =
        Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid FormerDrugAndAlcoholManager =
        Guid.Parse("c0000011-0000-0000-0000-000000000011");
    private static readonly Guid MedicalManager =
        Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid UniformManager =
        Guid.Parse("c0000003-0000-0000-0000-000000000003");

    private readonly InMemorySlotProposalRepository _proposals = new();
    private readonly InMemoryConfirmedSlotRepository _confirmedSlots = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly SlotProposal _proposal;

    private AcceptProposalHandler Handler =>
        new(_proposals, _confirmedSlots, _roles, _unitOfWork, _audit);

    public AcceptProposalHeadcountRevisionTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            DrugAndAlcoholManager,
            Role.Manager,
            AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(
            MedicalManager,
            Role.Manager,
            AppointmentTypeIds.MedicalCheckUp));
        _roles.Add(StaffAccessProfile.Create(
            UniformManager,
            Role.Manager,
            AppointmentTypeIds.UniformFitting));

        _proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            DrugAndAlcoholManager);
        _proposals.Add(_proposal);
    }

    private Task<Result<AcceptProposalOutcome>> Accept(Guid managerUserId, int headcount) =>
        Handler.HandleAsync(
            new AcceptProposalCommand(managerUserId, _proposal.Id, headcount),
            CancellationToken.None);

    [Fact]
    public async Task RevisingAHeadcountUpdatesOneRowAndWritesOneChangeAudit()
    {
        var recorded = await Accept(DrugAndAlcoholManager, 10);
        var revised = await Accept(DrugAndAlcoholManager, 12);

        Assert.True(recorded.IsSuccess);
        Assert.True(revised.IsSuccess);
        Assert.Null(revised.Value.ConfirmedSlotId);
        Assert.Equal(12, Assert.Single(_proposal.Acceptances).Headcount);
        Assert.Equal(2, _unitOfWork.SaveCount);

        var entries = _audit.Entries
            .Where(entry => entry.Action == AuditAction.AcceptanceRecorded)
            .ToList();
        Assert.Equal(2, entries.Count);
        Assert.Equal("Drug & Alcohol Testing headcount 10 -> 12", entries[1].Details);
    }

    [Fact]
    public async Task ResubmittingTheCurrentHeadcountIsASuccessfulNoOp()
    {
        await Accept(DrugAndAlcoholManager, 10);

        var repeated = await Accept(DrugAndAlcoholManager, 10);

        Assert.True(repeated.IsSuccess);
        Assert.Null(repeated.Value.ConfirmedSlotId);
        Assert.Equal(1, _unitOfWork.SaveCount);
        Assert.Single(
            _audit.Entries,
            entry => entry.Action == AuditAction.AcceptanceRecorded);
    }

    [Fact]
    public async Task AReplacementManagerCanReviseTheFormerManagersAcceptance()
    {
        _proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            FormerDrugAndAlcoholManager,
            10);

        var result = await Accept(DrugAndAlcoholManager, 12);

        Assert.True(result.IsSuccess);
        Assert.Equal(12, Assert.Single(_proposal.Acceptances).Headcount);
        Assert.Equal(1, _unitOfWork.SaveCount);
        Assert.Single(
            _audit.Entries,
            entry => entry.Action == AuditAction.AcceptanceRecorded);
    }

    [Fact]
    public async Task AnInvalidRevisionChangesAndWritesNothing()
    {
        await Accept(DrugAndAlcoholManager, 10);

        var result = await Accept(DrugAndAlcoholManager, 0);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal("headcount must be greater than zero.", result.Error.Message);
        Assert.Equal(10, Assert.Single(_proposal.Acceptances).Headcount);
        Assert.Equal(1, _unitOfWork.SaveCount);
        Assert.Single(_audit.Entries);
    }

    [Fact]
    public async Task ConfirmationUsesTheLatestRevisedHeadcount()
    {
        await Accept(DrugAndAlcoholManager, 10);
        await Accept(DrugAndAlcoholManager, 12);
        await Accept(MedicalManager, 6);

        var confirmed = await Accept(UniformManager, 8);

        Assert.True(confirmed.IsSuccess);
        Assert.NotNull(confirmed.Value.ConfirmedSlotId);
        var slot = Assert.Single(_confirmedSlots.Items);
        Assert.Equal(
            12,
            slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).TotalHeadcount);
    }
}
