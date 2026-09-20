# 00b — Vocabulary edits 87 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — tests/EventBooking.Application.Tests/Events/AcceptProposalHeadcountRevisionTests.cs — 1/1

<!-- vocabulary-file: {"id":289,"oldPath":"tests/EventBooking.Application.Tests/Slots/AcceptProposalHeadcountRevisionTests.cs","newPath":"tests/EventBooking.Application.Tests/Events/AcceptProposalHeadcountRevisionTests.cs","beforeSha":"31217553120a23103c91e79845b0549c3de2767f8d6620e3f5875d87403d4ada","afterSha":"2da4b34a7423fa55e0d4d7eb3ed2e0e0581ca267c9941f7796a1a3a9f4b5d544","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Common;
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

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

    private readonly InMemoryEventProposalRepository _proposals = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly EventProposal _proposal;

    private AcceptProposalHandler Handler =>
        new(_proposals, _events, _roles, _unitOfWork, _audit);

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

        _proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
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
        Assert.Null(revised.Value.EventId);
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
        Assert.Null(repeated.Value.EventId);
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
        Assert.NotNull(confirmed.Value.EventId);
        var eventItem = Assert.Single(_events.Items);
        Assert.Equal(
            12,
            eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).TotalHeadcount);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Slots/AdjustConfirmedSlotCapacityHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":290,"oldPath":"tests/EventBooking.Application.Tests/Slots/AdjustConfirmedSlotCapacityHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Events/AdjustEventCapacityHandlerTests.cs","beforeSha":"98fc8a9b1effde4776a562d4fde75b174a428625dfdde2719593292097675461","afterSha":"4d85b414d66172bc4fc206a799dbcf26455572682c2fffeea02084be1df85870","side":"before","part":1,"parts":1} -->

`````csharp
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
`````

## after — tests/EventBooking.Application.Tests/Events/AdjustEventCapacityHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":290,"oldPath":"tests/EventBooking.Application.Tests/Slots/AdjustConfirmedSlotCapacityHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Events/AdjustEventCapacityHandlerTests.cs","beforeSha":"98fc8a9b1effde4776a562d4fde75b174a428625dfdde2719593292097675461","afterSha":"4d85b414d66172bc4fc206a799dbcf26455572682c2fffeea02084be1df85870","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

public class AdjustEventCapacityHandlerTests
{
    private static readonly Guid DrugAndAlcoholManager =
        Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid Coordinator =
        Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly Event _event;
    private readonly InMemoryEventCapacityRepository _capacities;

    private AdjustEventCapacityHandler Handler =>
        new(_events, _capacities, _roles, _unitOfWork, _audit);

    public AdjustEventCapacityHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            DrugAndAlcoholManager,
            Role.Manager,
            AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));

        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            DrugAndAlcoholManager);
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        _event = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Add(_event);
        _capacities = new InMemoryEventCapacityRepository(_events);
    }

    private Task<EventBooking.Application.Common.Result<AdjustEventCapacityOutcome>>
        Adjust(int totalHeadcount) =>
        Handler.HandleAsync(
            new AdjustEventCapacityCommand(
                DrugAndAlcoholManager, _event.Id, totalHeadcount),
            CancellationToken.None);

    private void Occupy(int count)
    {
        var capacity = _event.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);
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
        Assert.Equal(AuditEntityTypes.Event, entry.EntityType);
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
        var capacity = _event.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);
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
        Assert.Equal(10, _event.CapacityFor(
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
        Assert.Equal(12, _event.CapacityFor(
            AppointmentTypeIds.DrugAndAlcoholTesting).TotalHeadcount);
        Assert.Equal(6, _event.CapacityFor(
            AppointmentTypeIds.MedicalCheckUp).TotalHeadcount);
        Assert.Equal(8, _event.CapacityFor(
            AppointmentTypeIds.UniformFitting).TotalHeadcount);
    }

    [Fact]
    public async Task ACoordinatorCannotAdjustCapacity()
    {
        var result = await Handler.HandleAsync(
            new AdjustEventCapacityCommand(Coordinator, _event.Id, 12),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(0, _capacities.LockCallCount);
    }

    [Fact]
    public async Task ACancelledEventCannotBeAdjusted()
    {
        _event.Cancel();

        var result = await Adjust(12);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("A cancelled event cannot have its capacity adjusted.", result.Error.Message);
        Assert.Equal(1, _capacities.LockCallCount);
    }

    [Fact]
    public async Task AnUnknownEventIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new AdjustEventCapacityCommand(
                DrugAndAlcoholManager, Guid.NewGuid(), 12),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Equal(1, _capacities.LockCallCount);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Slots/CancelConfirmedSlotHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":291,"oldPath":"tests/EventBooking.Application.Tests/Slots/CancelConfirmedSlotHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs","beforeSha":"4e7a2f19a71ef1bc3d7abd905a5dcf241318e5846a77acafcc76051b7d2dc57a","afterSha":"a3efe4403b85b99dbcbfab71964fd74fc860378ee3041388703be76a1f7d61a1","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Slots;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Slots;

public class CancelConfirmedSlotHandlerTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly CandidatePortalOptions Portal = new(
        "https://booking.example.com", "Corporate HQ", "recruitment@corp.com");

    private readonly TransactionOperationLog _operations = new();
    private readonly InMemoryConfirmedSlotRepository _slots;
    private readonly InMemoryBookingRepository _bookings;
    private readonly InMemoryBookingAppointmentRepository _appointments;
    private readonly InMemoryCandidateRepository _candidates;
    private readonly InMemoryInviteRepository _invites;
    private readonly InMemoryEmployeeGroupRepository _groups = new();
    private readonly InMemorySystemSettingsRepository _settings = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly RecordingEmailSender _email = new();
    private readonly InMemoryEmailDeliveryRepository _deliveries = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork;
    private readonly FakeTokenService _tokens = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly ConfirmedSlot _slot;

    private CancelConfirmedSlotHandler Handler => new(
        _slots,
        _bookings,
        _invites,
        _candidates,
        _roles,
        new BookingCanceller(_appointments, new InMemorySlotCapacityRepository(_slots), _audit),
        new InviteIssuer(
            _invites, _groups, new EligibleSlotFinder(_slots, _clock), _settings,
            _tokens, EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
            _audit, _clock, Portal),
        new EligibleSlotFinder(_slots, _clock),
        _appointments,
        EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
        _audit,
        _clock,
        _unitOfWork);

    public CancelConfirmedSlotHandlerTests()
    {
        _invites = new InMemoryInviteRepository(_operations);
        _slots = new InMemoryConfirmedSlotRepository(_operations);
        _bookings = new InMemoryBookingRepository(_operations);
        _appointments = new InMemoryBookingAppointmentRepository(_bookings, _operations);
        _candidates = new InMemoryCandidateRepository(_operations);
        _unitOfWork = new FakeUnitOfWork(_operations);
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));

        _slot = AddSlot(10);
        AddSlot(12);
        AddSlot(14);
        AddSlot(16);
    }

    [Fact]
    public async Task APastSlotCannotBeCancelled()
    {
        var past = AddSlot(1);

        var result = await Handler.HandleAsync(
            new CancelConfirmedSlotCommand(Coordinator, past.Id, true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(ConfirmedSlotStatus.Active, past.Status);
    }

    [Fact]
    public async Task ASlotWithNoBookingsIsCancelledWithoutConfirmation()
    {
        var result = await Handler.HandleAsync(
            new CancelConfirmedSlotCommand(Coordinator, _slot.Id, false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.BookingsVoided);
        Assert.Equal(ConfirmedSlotStatus.Cancelled, _slot.Status);
        Assert.True(_audit.Contains(AuditAction.SlotCancelled));
    }

    [Fact]
    public async Task AppointmentStaffIsDeniedBeforeTransactionOrSlotLock()
    {
        var appointmentStaff = Guid.NewGuid();
        ((IStaffAccessProfileRepository)_roles).Add(StaffAccessProfile.Create(
            appointmentStaff,
            [Role.AppointmentStaff],
            AppointmentTypeIds.DrugAndAlcoholTesting));

        var result = await Handler.HandleAsync(
            new CancelConfirmedSlotCommand(appointmentStaff, _slot.Id, true),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.DoesNotContain("transaction-begun", _operations.Events);
        Assert.DoesNotContain("slot-guard-locked", _operations.Events);
        Assert.Equal(ConfirmedSlotStatus.Active, _slot.Status);
    }

    [Fact]
    public async Task ASlotWithBookingsNeedsConfirmationAndSaysHowMany()
    {
        BookACandidate("Amara Novak", "a.novak@mail.com");
        BookACandidate("B. Chen", "b.chen@mail.com");

        var result = await Handler.HandleAsync(
            new CancelConfirmedSlotCommand(Coordinator, _slot.Id, false), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(
            "Cancelling this slot will cancel 2 confirmed bookings. Affected candidates will be notified and re-invited. Confirm to proceed.",
            result.Error.Message);
        Assert.Equal(ConfirmedSlotStatus.Active, _slot.Status);
    }

    /// <summary>Verifies cancellation uses one business commit and one delivery result commit per message.</summary>
    [Fact]
    public async Task ConfirmedCancellationVoidsEveryBookingAndReInvitesEveryCandidate()
    {
        var amara = BookACandidate("Amara Novak", "a.novak@mail.com");
        var chen = BookACandidate("B. Chen", "b.chen@mail.com");

        var result = await Handler.HandleAsync(
            new CancelConfirmedSlotCommand(Coordinator, _slot.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.BookingsVoided);
        Assert.Equal(2, result.Value.CandidatesReinvited);

        Assert.All(_bookings.Items, b => Assert.Equal(BookingStatus.Cancelled, b.Status));
        Assert.Equal(CandidateStatus.Invited, amara.Status);
        Assert.Equal(CandidateStatus.Invited, chen.Status);
        Assert.Equal(ConfirmedSlotStatus.Cancelled, _slot.Status);
        Assert.Equal(10, _slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(5, _unitOfWork.CommitCount);
    }

    /// <summary>Verifies candidate lifecycle locks are acquired before the confirmed-slot guard.</summary>
    [Fact]
    public async Task CancellationTakesCandidateLifecycleLocksBeforeTheSlotGuard()
    {
        BookACandidate("Amara Novak", "a.novak@mail.com");

        await Handler.HandleAsync(
            new CancelConfirmedSlotCommand(Coordinator, _slot.Id, true), CancellationToken.None);

        Assert.Equal(
            ["active-candidate-ids-snapshotted", "transaction-begun", "candidate-locked", "pending-invites-locked", "original-booking-locked", "active-recovery-locked", "slot-guard-locked", "active-bookings-listed"],
            _operations.Events.Take(8));
    }

    [Fact]
    public async Task EachAffectedCandidateGetsACancellationEmailThenAnInvite()
    {
        BookACandidate("Amara Novak", "a.novak@mail.com");

        await Handler.HandleAsync(
            new CancelConfirmedSlotCommand(Coordinator, _slot.Id, true), CancellationToken.None);

        Assert.Equal(2, _email.Sent.Count);
        Assert.Equal(EmailTemplate.CandidateInvite, _email.Sent[0].Template);
        Assert.Equal(EmailTemplate.SlotCancelledRebookingNeeded, _email.Sent[1].Template);
    }

    /// <summary>Failed replacement delivery produces neutral cancellation wording.</summary>
    [Fact]
    public async Task AFailedReplacementDoesNotPromiseThatAnInviteIsOnItsWay()
    {
        BookACandidate("Amara Novak", "a.novak@mail.com");
        _email.FailNextSend = true;

        var result = await Handler.HandleAsync(
            new CancelConfirmedSlotCommand(Coordinator, _slot.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var cancellation = Assert.Single(_email.Sent, message =>
            message.Template == EmailTemplate.SlotCancelledRebookingNeeded);
        Assert.Contains("recruitment team will contact you", cancellation.TextBody);
        Assert.DoesNotContain("on its way", cancellation.TextBody);
    }

    [Fact]
    public async Task TheCancelledSlotIsNeverOfferedInTheReplacementInvites()
    {
        BookACandidate("Amara Novak", "a.novak@mail.com");

        await Handler.HandleAsync(
            new CancelConfirmedSlotCommand(Coordinator, _slot.Id, true), CancellationToken.None);

        var reissued = _invites.Items.Single(i => i.Status == InviteStatus.Pending);
        Assert.DoesNotContain(_slot.Id, reissued.OfferedSlotIds);
    }

    [Fact]
    public async Task CancellingAnAlreadyCancelledSlotIsAConflict()
    {
        _slot.Cancel();

        var result = await Handler.HandleAsync(
            new CancelConfirmedSlotCommand(Coordinator, _slot.Id, true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("This slot has already been cancelled.", result.Error.Message);
    }

    [Fact]
    public async Task AnUnknownSlotIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new CancelConfirmedSlotCommand(Coordinator, Guid.NewGuid(), true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task AUserWithNoRoleIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new CancelConfirmedSlotCommand(Guid.NewGuid(), _slot.Id, true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    private Candidate BookACandidate(string name, string email)
    {
        var pilots = EmployeeGroup.Define(
            EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        if (!_groups.Items.Any(group => group.Id == pilots.Id))
        {
            _groups.Items.Add(pilots);
        }

        var candidate = Candidate.Create(Guid.NewGuid(), name, email, pilots);
        _candidates.Add(candidate);

        var inviteId = Guid.NewGuid();
        var issued = _tokens.Issue(inviteId);
        var invite = Invite.CreateInitial(
            inviteId, candidate.Id, issued.TokenHash, _clock.UtcNow.AddDays(4),
            [_slot.Id, _slots.Items[1].Id, _slots.Items[2].Id],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting], 0);
        _invites.Add(invite);
        candidate.MarkInvited();

        var bookingId = Guid.NewGuid();
        var manage = _tokens.Issue(bookingId);
        _bookings.Add(Booking.Create(bookingId, invite, _slot.Id, manage.TokenHash, _clock.UtcNow));
        invite.MarkUsed();
        candidate.MarkBooked();

        _slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), bookingId, AppointmentTypeIds.DrugAndAlcoholTesting));
        _slot.CapacityFor(AppointmentTypeIds.UniformFitting).Decrement();
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), bookingId, AppointmentTypeIds.UniformFitting));

        return candidate;
    }

    [Fact]
    public async Task CancellingASlotHoldingAnActiveRecoveryIssuesAReplacement()
    {
        var candidate = BookACandidate("Amara Novak", "a.novak@mail.com");
        var original = _bookings.Items.Single();
        var recoverySlot = _slots.Items[1];
        var recovery = AddActiveRecoveryOn(candidate, original, recoverySlot);

        var result = await Handler.HandleAsync(
            new CancelConfirmedSlotCommand(Coordinator, recoverySlot.Id, true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.BookingsVoided);
        Assert.Equal(1, result.Value.CandidatesReinvited);
        Assert.Equal(BookingStatus.Cancelled, recovery.Status);
        Assert.Equal(BookingStatus.Active, original.Status);
        Assert.Equal(CandidateStatus.Booked, candidate.Status);
        Assert.Equal(
            10, recoverySlot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);

        var replacement = Assert.Single(
            _invites.Items,
            i => i.RecoveryOfBookingId == original.Id && i.Status == InviteStatus.Pending);
        Assert.Equal(InviteStatus.Pending, replacement.Status);
        Assert.Equal([AppointmentTypeIds.DrugAndAlcoholTesting], replacement.RequiredAppointmentTypeIds);
        Assert.Equal(Invite.RequiredOptionCount, replacement.OfferedSlotIds.Count);
        Assert.Contains(_slot.Id, replacement.OfferedSlotIds);

        Assert.True(_audit.Contains(AuditAction.BookingCancelled));
        Assert.True(_audit.Contains(AuditAction.RecoveryInviteCreated));
        Assert.Equal(2, _email.Sent.Count);
    }

    [Fact]
    public async Task WithoutReplacementSlotsTheRecoveryStaysAvailable()
    {
        var candidate = BookACandidate("Amara Novak", "a.novak@mail.com");
        var original = _bookings.Items.Single();
        var recoverySlot = _slots.Items[1];
        var recovery = AddActiveRecoveryOn(candidate, original, recoverySlot);

        foreach (var slot in _slots.Items.Where(s => s.Id != recoverySlot.Id))
        {
            var capacity = slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);
            var remainingCapacity = capacity.RemainingCapacity;
            for (var i = 0; i < remainingCapacity; i++)
            {
                capacity.Decrement();
            }
        }

        var result = await Handler.HandleAsync(
            new CancelConfirmedSlotCommand(Coordinator, recoverySlot.Id, true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.BookingsVoided);
        Assert.Equal(0, result.Value.CandidatesReinvited);
        Assert.Equal(BookingStatus.Cancelled, recovery.Status);
        Assert.Equal(BookingStatus.Active, original.Status);
        Assert.Equal(CandidateStatus.Booked, candidate.Status);
        Assert.DoesNotContain(
            _invites.Items,
            i => i.RecoveryOfBookingId == original.Id && i.Status == InviteStatus.Pending);
        Assert.Single(_email.Sent);
    }

    private Booking AddActiveRecoveryOn(
        Candidate candidate, Booking original, ConfirmedSlot recoverySlot)
    {
        _appointments.Items
            .Single(a => a.BookingId == original.Id
                && a.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting)
            .TransitionTo(BookingAppointmentStatus.NoShow, Coordinator, _clock.UtcNow, false, true);

        var recoveryInviteId = Guid.NewGuid();
        var issued = _tokens.Issue(recoveryInviteId);
        var recoveryInvite = Invite.CreateRecovery(
            recoveryInviteId, candidate.Id, original.Id, issued.TokenHash,
            _clock.UtcNow.AddDays(4),
            [recoverySlot.Id, _slots.Items[2].Id, _slots.Items[3].Id],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        _invites.Add(recoveryInvite);

        var recoveryId = Guid.NewGuid();
        var manage = _tokens.Issue(recoveryId);
        var recovery = Booking.CreateRecovery(
            recoveryId, recoveryInvite, original, recoverySlot.Id,
            manage.TokenHash, _clock.UtcNow);
        _bookings.Add(recovery);
        recoveryInvite.MarkUsed();

        recoverySlot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.DrugAndAlcoholTesting));

        return recovery;
    }

    private ConfirmedSlot AddSlot(int day)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);
        _slots.Add(slot);
        return slot;
    }
}
`````

## after — tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":291,"oldPath":"tests/EventBooking.Application.Tests/Slots/CancelConfirmedSlotHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs","beforeSha":"4e7a2f19a71ef1bc3d7abd905a5dcf241318e5846a77acafcc76051b7d2dc57a","afterSha":"a3efe4403b85b99dbcbfab71964fd74fc860378ee3041388703be76a1f7d61a1","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

public class CancelEventHandlerTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly AttendeePortalOptions Portal = new(
        "https://booking.example.com", "Corporate HQ", "recruitment@corp.com");

    private readonly TransactionOperationLog _operations = new();
    private readonly InMemoryEventRepository _events;
    private readonly InMemoryBookingRepository _bookings;
    private readonly InMemoryBookingAppointmentRepository _appointments;
    private readonly InMemoryAttendeeRepository _attendees;
    private readonly InMemoryInviteRepository _invites;
    private readonly InMemoryAttendeeGroupRepository _groups = new();
    private readonly InMemorySystemSettingsRepository _settings = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly RecordingEmailSender _email = new();
    private readonly InMemoryEmailDeliveryRepository _deliveries = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork;
    private readonly FakeTokenService _tokens = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly Event _event;

    private CancelEventHandler Handler => new(
        _events,
        _bookings,
        _invites,
        _attendees,
        _roles,
        new BookingCanceller(_appointments, new InMemoryEventCapacityRepository(_events), _audit),
        new InviteIssuer(
            _invites, _groups, new EligibleEventFinder(_events, _clock), _settings,
            _tokens, EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
            _audit, _clock, Portal),
        new EligibleEventFinder(_events, _clock),
        _appointments,
        EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
        _audit,
        _clock,
        _unitOfWork);

    public CancelEventHandlerTests()
    {
        _invites = new InMemoryInviteRepository(_operations);
        _events = new InMemoryEventRepository(_operations);
        _bookings = new InMemoryBookingRepository(_operations);
        _appointments = new InMemoryBookingAppointmentRepository(_bookings, _operations);
        _attendees = new InMemoryAttendeeRepository(_operations);
        _unitOfWork = new FakeUnitOfWork(_operations);
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));

        _event = AddEvent(10);
        AddEvent(12);
        AddEvent(14);
        AddEvent(16);
    }

    [Fact]
    public async Task APastEventCannotBeCancelled()
    {
        var past = AddEvent(1);

        var result = await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, past.Id, true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(EventStatus.Active, past.Status);
    }

    [Fact]
    public async Task AEventWithNoBookingsIsCancelledWithoutConfirmation()
    {
        var result = await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, _event.Id, false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.BookingsVoided);
        Assert.Equal(EventStatus.Cancelled, _event.Status);
        Assert.True(_audit.Contains(AuditAction.EventCancelled));
    }

    [Fact]
    public async Task AppointmentStaffIsDeniedBeforeTransactionOrEventLock()
    {
        var appointmentStaff = Guid.NewGuid();
        ((IStaffAccessProfileRepository)_roles).Add(StaffAccessProfile.Create(
            appointmentStaff,
            [Role.AppointmentStaff],
            AppointmentTypeIds.DrugAndAlcoholTesting));

        var result = await Handler.HandleAsync(
            new CancelEventCommand(appointmentStaff, _event.Id, true),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.DoesNotContain("transaction-begun", _operations.Events);
        Assert.DoesNotContain("event-guard-locked", _operations.Events);
        Assert.Equal(EventStatus.Active, _event.Status);
    }

    [Fact]
    public async Task AEventWithBookingsNeedsConfirmationAndSaysHowMany()
    {
        BookAAttendee("Amara Novak", "a.novak@mail.com");
        BookAAttendee("B. Chen", "b.chen@mail.com");

        var result = await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, _event.Id, false), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(
            "Cancelling this event will cancel 2 confirmed bookings. Affected attendees will be notified and re-invited. Confirm to proceed.",
            result.Error.Message);
        Assert.Equal(EventStatus.Active, _event.Status);
    }

    /// <summary>Verifies cancellation uses one business commit and one delivery result commit per message.</summary>
    [Fact]
    public async Task ConfirmedCancellationVoidsEveryBookingAndReInvitesEveryAttendee()
    {
        var amara = BookAAttendee("Amara Novak", "a.novak@mail.com");
        var chen = BookAAttendee("B. Chen", "b.chen@mail.com");

        var result = await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, _event.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.BookingsVoided);
        Assert.Equal(2, result.Value.AttendeesReinvited);

        Assert.All(_bookings.Items, b => Assert.Equal(BookingStatus.Cancelled, b.Status));
        Assert.Equal(AttendeeStatus.Invited, amara.Status);
        Assert.Equal(AttendeeStatus.Invited, chen.Status);
        Assert.Equal(EventStatus.Cancelled, _event.Status);
        Assert.Equal(10, _event.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(5, _unitOfWork.CommitCount);
    }

    /// <summary>Verifies attendee lifecycle locks are acquired before the event guard.</summary>
    [Fact]
    public async Task CancellationTakesAttendeeLifecycleLocksBeforeTheEventGuard()
    {
        BookAAttendee("Amara Novak", "a.novak@mail.com");

        await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, _event.Id, true), CancellationToken.None);

        Assert.Equal(
            ["active-attendee-ids-snapshotted", "transaction-begun", "attendee-locked", "pending-invites-locked", "original-booking-locked", "active-recovery-locked", "event-guard-locked", "active-bookings-listed"],
            _operations.Events.Take(8));
    }

    [Fact]
    public async Task EachAffectedAttendeeGetsACancellationEmailThenAnInvite()
    {
        BookAAttendee("Amara Novak", "a.novak@mail.com");

        await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, _event.Id, true), CancellationToken.None);

        Assert.Equal(2, _email.Sent.Count);
        Assert.Equal(EmailTemplate.AttendeeInvite, _email.Sent[0].Template);
        Assert.Equal(EmailTemplate.EventCancelledRebookingNeeded, _email.Sent[1].Template);
    }

    /// <summary>Failed replacement delivery produces neutral cancellation wording.</summary>
    [Fact]
    public async Task AFailedReplacementDoesNotPromiseThatAnInviteIsOnItsWay()
    {
        BookAAttendee("Amara Novak", "a.novak@mail.com");
        _email.FailNextSend = true;

        var result = await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, _event.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var cancellation = Assert.Single(_email.Sent, message =>
            message.Template == EmailTemplate.EventCancelledRebookingNeeded);
        Assert.Contains("recruitment team will contact you", cancellation.TextBody);
        Assert.DoesNotContain("on its way", cancellation.TextBody);
    }

    [Fact]
    public async Task TheCancelledEventIsNeverOfferedInTheReplacementInvites()
    {
        BookAAttendee("Amara Novak", "a.novak@mail.com");

        await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, _event.Id, true), CancellationToken.None);

        var reissued = _invites.Items.Single(i => i.Status == InviteStatus.Pending);
        Assert.DoesNotContain(_event.Id, reissued.OfferedEventIds);
    }

    [Fact]
    public async Task CancellingAnAlreadyCancelledEventIsAConflict()
    {
        _event.Cancel();

        var result = await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, _event.Id, true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("This event has already been cancelled.", result.Error.Message);
    }

    [Fact]
    public async Task AnUnknownEventIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, Guid.NewGuid(), true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task AUserWithNoRoleIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new CancelEventCommand(Guid.NewGuid(), _event.Id, true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    private Attendee BookAAttendee(string name, string email)
    {
        var pilots = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        if (!_groups.Items.Any(group => group.Id == pilots.Id))
        {
            _groups.Items.Add(pilots);
        }

        var attendee = Attendee.Create(Guid.NewGuid(), name, email, pilots);
        _attendees.Add(attendee);

        var inviteId = Guid.NewGuid();
        var issued = _tokens.Issue(inviteId);
        var invite = Invite.CreateInitial(
            inviteId, attendee.Id, issued.TokenHash, _clock.UtcNow.AddDays(4),
            [_event.Id, _events.Items[1].Id, _events.Items[2].Id],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting], 0);
        _invites.Add(invite);
        attendee.MarkInvited();

        var bookingId = Guid.NewGuid();
        var manage = _tokens.Issue(bookingId);
        _bookings.Add(Booking.Create(bookingId, invite, _event.Id, manage.TokenHash, _clock.UtcNow));
        invite.MarkUsed();
        attendee.MarkBooked();

        _event.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), bookingId, AppointmentTypeIds.DrugAndAlcoholTesting));
        _event.CapacityFor(AppointmentTypeIds.UniformFitting).Decrement();
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), bookingId, AppointmentTypeIds.UniformFitting));

        return attendee;
    }

    [Fact]
    public async Task CancellingAEventHoldingAnActiveRecoveryIssuesAReplacement()
    {
        var attendee = BookAAttendee("Amara Novak", "a.novak@mail.com");
        var original = _bookings.Items.Single();
        var recoveryEvent = _events.Items[1];
        var recovery = AddActiveRecoveryOn(attendee, original, recoveryEvent);

        var result = await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, recoveryEvent.Id, true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.BookingsVoided);
        Assert.Equal(1, result.Value.AttendeesReinvited);
        Assert.Equal(BookingStatus.Cancelled, recovery.Status);
        Assert.Equal(BookingStatus.Active, original.Status);
        Assert.Equal(AttendeeStatus.Booked, attendee.Status);
        Assert.Equal(
            10, recoveryEvent.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);

        var replacement = Assert.Single(
            _invites.Items,
            i => i.RecoveryOfBookingId == original.Id && i.Status == InviteStatus.Pending);
        Assert.Equal(InviteStatus.Pending, replacement.Status);
        Assert.Equal([AppointmentTypeIds.DrugAndAlcoholTesting], replacement.RequiredAppointmentTypeIds);
        Assert.Equal(Invite.RequiredOptionCount, replacement.OfferedEventIds.Count);
        Assert.Contains(_event.Id, replacement.OfferedEventIds);

        Assert.True(_audit.Contains(AuditAction.BookingCancelled));
        Assert.True(_audit.Contains(AuditAction.RecoveryInviteCreated));
        Assert.Equal(2, _email.Sent.Count);
    }

    [Fact]
    public async Task WithoutReplacementEventsTheRecoveryStaysAvailable()
    {
        var attendee = BookAAttendee("Amara Novak", "a.novak@mail.com");
        var original = _bookings.Items.Single();
        var recoveryEvent = _events.Items[1];
        var recovery = AddActiveRecoveryOn(attendee, original, recoveryEvent);

        foreach (var eventItem in _events.Items.Where(s => s.Id != recoveryEvent.Id))
        {
            var capacity = eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);
            var remainingCapacity = capacity.RemainingCapacity;
            for (var i = 0; i < remainingCapacity; i++)
            {
                capacity.Decrement();
            }
        }

        var result = await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, recoveryEvent.Id, true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.BookingsVoided);
        Assert.Equal(0, result.Value.AttendeesReinvited);
        Assert.Equal(BookingStatus.Cancelled, recovery.Status);
        Assert.Equal(BookingStatus.Active, original.Status);
        Assert.Equal(AttendeeStatus.Booked, attendee.Status);
        Assert.DoesNotContain(
            _invites.Items,
            i => i.RecoveryOfBookingId == original.Id && i.Status == InviteStatus.Pending);
        Assert.Single(_email.Sent);
    }

    private Booking AddActiveRecoveryOn(
        Attendee attendee, Booking original, Event recoveryEvent)
    {
        _appointments.Items
            .Single(a => a.BookingId == original.Id
                && a.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting)
            .TransitionTo(BookingAppointmentStatus.NoShow, Coordinator, _clock.UtcNow, false, true);

        var recoveryInviteId = Guid.NewGuid();
        var issued = _tokens.Issue(recoveryInviteId);
        var recoveryInvite = Invite.CreateRecovery(
            recoveryInviteId, attendee.Id, original.Id, issued.TokenHash,
            _clock.UtcNow.AddDays(4),
            [recoveryEvent.Id, _events.Items[2].Id, _events.Items[3].Id],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        _invites.Add(recoveryInvite);

        var recoveryId = Guid.NewGuid();
        var manage = _tokens.Issue(recoveryId);
        var recovery = Booking.CreateRecovery(
            recoveryId, recoveryInvite, original, recoveryEvent.Id,
            manage.TokenHash, _clock.UtcNow);
        _bookings.Add(recovery);
        recoveryInvite.MarkUsed();

        recoveryEvent.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.DrugAndAlcoholTesting));

        return recovery;
    }

    private Event AddEvent(int day)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Add(eventItem);
        return eventItem;
    }
}
`````

## before — tests/EventBooking.Application.Tests/Slots/CombinedManagerAuthorizationTests.cs — 1/1

<!-- vocabulary-file: {"id":292,"oldPath":"tests/EventBooking.Application.Tests/Slots/CombinedManagerAuthorizationTests.cs","newPath":"tests/EventBooking.Application.Tests/Events/CombinedManagerAuthorizationTests.cs","beforeSha":"439435d19539465470232ad6a7214806b0ba00e87b6ec2847a174f884676e7f5","afterSha":"d5c584e14c00d4ca85c320a171a699f85e098f752e0ac86623e2db8aecd00133","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Slots;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Tests.Slots;

public class CombinedManagerAuthorizationTests
{
    [Fact]
    public async Task ACoordinatorManagerCanProposeUsingTheManagerCapability()
    {
        var staffUserId = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            staffUserId,
            [Role.Coordinator, Role.Manager],
            AppointmentTypeIds.DrugAndAlcoholTesting));
        var proposals = new InMemorySlotProposalRepository();
        var handler = new ProposeSlotHandler(
            proposals,
            new StaffAccessAuthorizer(profiles),
            new FakeUnitOfWork(),
            new RecordingAuditLogger(),
            new FakeClock());

        var result = await handler.HandleAsync(
            new ProposeSlotCommand(staffUserId, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(proposals.Items);
    }

    [Fact]
    public async Task AppointmentStaffWithoutManagerCannotPropose()
    {
        var staffUserId = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            staffUserId,
            [Role.AppointmentStaff],
            AppointmentTypeIds.DrugAndAlcoholTesting));
        var proposals = new InMemorySlotProposalRepository();
        var handler = new ProposeSlotHandler(
            proposals,
            new StaffAccessAuthorizer(profiles),
            new FakeUnitOfWork(),
            new RecordingAuditLogger(),
            new FakeClock());

        var result = await handler.HandleAsync(
            new ProposeSlotCommand(staffUserId, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(proposals.Items);
    }
}
`````
