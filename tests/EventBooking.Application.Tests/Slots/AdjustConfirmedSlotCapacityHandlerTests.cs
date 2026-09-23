using EventBooking.Application.Slots;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Slots;

public class AdjustConfirmedSlotCapacityHandlerTests
{
    private static readonly Guid DrugAndAlcoholManager =
        Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid Coordinator =
        Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemoryConfirmedSlotRepository _slots = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly ConfirmedSlot _slot;
    private readonly InMemorySlotCapacityRepository _capacities;

    private AdjustConfirmedSlotCapacityHandler Handler =>
        new(_slots, _capacities, _roles, _unitOfWork, _audit);

    public AdjustConfirmedSlotCapacityHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            DrugAndAlcoholManager,
            Role.Manager,
            AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));

        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            DrugAndAlcoholManager);
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        _slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);
        _slots.Add(_slot);
        _capacities = new InMemorySlotCapacityRepository(_slots);
    }

    private Task<EventBooking.Application.Common.Result<AdjustConfirmedSlotCapacityOutcome>>
        Adjust(int totalHeadcount) =>
        Handler.HandleAsync(
            new AdjustConfirmedSlotCapacityCommand(
                DrugAndAlcoholManager, _slot.Id, totalHeadcount),
            CancellationToken.None);

    private void Occupy(int count)
    {
        var capacity = _slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);
        for (var index = 0; index < count; index++)
        {
            capacity.Decrement();
        }
    }

    [Fact]
    public async Task IncreasingTheTotalMovesRemainingAndWritesOneAudit()
    {
        Occupy(6);

        var result = await Adjust(12);

        Assert.True(result.IsSuccess);
        Assert.Equal(12, result.Value.TotalHeadcount);
        Assert.Equal(6, result.Value.RemainingCapacity);
        Assert.Equal(1, _capacities.LockCallCount);
        Assert.Equal(1, _unitOfWork.SaveCount);
        Assert.Equal(1, _unitOfWork.CommitCount);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AuditAction.CapacityAdjusted, entry.Action);
        Assert.Equal(AuditEntityTypes.ConfirmedSlot, entry.EntityType);
        Assert.Equal(
            "Drug & Alcohol Testing total 10 -> 12; remaining 4 -> 6",
            entry.Details);
    }

    [Fact]
    public async Task AValidDecreaseMovesRemainingByTheSameDelta()
    {
        Occupy(6);

        var result = await Adjust(8);

        Assert.True(result.IsSuccess);
        Assert.Equal(8, result.Value.TotalHeadcount);
        Assert.Equal(2, result.Value.RemainingCapacity);
    }

    [Fact]
    public async Task ATotalBelowActiveBookingsIsAConflictAndChangesNothing()
    {
        Occupy(6);

        var result = await Adjust(5);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(
            "Headcount cannot be lower than the active-booking count of 6.",
            result.Error.Message);
        var capacity = _slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);
        Assert.Equal(10, capacity.TotalHeadcount);
        Assert.Equal(4, capacity.RemainingCapacity);
        Assert.Equal(0, _unitOfWork.SaveCount);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task ACurrentTotalIsASuccessfulNoOp()
    {
        Occupy(6);

        var result = await Adjust(10);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value.TotalHeadcount);
        Assert.Equal(4, result.Value.RemainingCapacity);
        Assert.Equal(0, _unitOfWork.SaveCount);
        Assert.Equal(1, _unitOfWork.CommitCount);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task ATotalMustBePositive()
    {
        var result = await Adjust(0);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal("totalHeadcount must be greater than zero.", result.Error.Message);
        Assert.Equal(10, _slot.CapacityFor(
            AppointmentTypeIds.DrugAndAlcoholTesting).TotalHeadcount);
        Assert.Equal(0, _capacities.LockCallCount);
        Assert.Equal(0, _unitOfWork.SaveCount);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task TheCurrentRoleSelectsTheOnlyCapacityThatChanges()
    {
        var result = await Adjust(12);

        Assert.True(result.IsSuccess);
        Assert.Equal(12, _slot.CapacityFor(
            AppointmentTypeIds.DrugAndAlcoholTesting).TotalHeadcount);
        Assert.Equal(6, _slot.CapacityFor(
            AppointmentTypeIds.MedicalCheckUp).TotalHeadcount);
        Assert.Equal(8, _slot.CapacityFor(
            AppointmentTypeIds.UniformFitting).TotalHeadcount);
    }

    [Fact]
    public async Task ACoordinatorCannotAdjustCapacity()
    {
        var result = await Handler.HandleAsync(
            new AdjustConfirmedSlotCapacityCommand(Coordinator, _slot.Id, 12),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(0, _capacities.LockCallCount);
    }

    [Fact]
    public async Task ACancelledSlotCannotBeAdjusted()
    {
        _slot.Cancel();

        var result = await Adjust(12);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("A cancelled slot cannot have its capacity adjusted.", result.Error.Message);
        Assert.Equal(1, _capacities.LockCallCount);
    }

    [Fact]
    public async Task AnUnknownSlotIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new AdjustConfirmedSlotCapacityCommand(
                DrugAndAlcoholManager, Guid.NewGuid(), 12),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Equal(1, _capacities.LockCallCount);
    }
}
