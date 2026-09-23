using EventBooking.Application.Bookings;
using EventBooking.Application.Candidates;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Candidates;

public class DeleteCandidateHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemoryCandidateRepository _candidates = new();
    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryBookingRepository _bookings = new();
    private readonly InMemoryConfirmedSlotRepository _slots = new();
    private readonly InMemoryBookingAppointmentRepository _appointments;

    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly Candidate _candidate;

    private DeleteCandidateHandler Handler => new(
        _candidates, _invites, _bookings, _slots, _roles,
        new BookingCanceller(_appointments, new InMemorySlotCapacityRepository(_slots), _audit),
        _audit,
        _unitOfWork);

    public DeleteCandidateHandlerTests()
    {
        _appointments = new InMemoryBookingAppointmentRepository(_bookings);
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com",
            EmployeeGroup.Define(
                EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
        _candidates.Add(_candidate);
    }

    [Fact]
    public async Task ACandidateWithNothingOutstandingIsDeletedOutright()
    {
        var result = await Handler.HandleAsync(
            new DeleteCandidateCommand(Coordinator, _candidate.Id, false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_candidates.Items);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnUnconfirmedDeleteOfACandidateWithABookingIsRefusedWithAWarning()
    {
        GiveTheCandidateABooking();

        var result = await Handler.HandleAsync(
            new DeleteCandidateCommand(Coordinator, _candidate.Id, false), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(
            "Deleting this candidate will cancel 1 booking and 1 pending invite, and free the capacity they hold. Confirm to proceed.",
            result.Error.Message);
        Assert.Single(_candidates.Items);
    }

    [Fact]
    public async Task AConfirmedDeleteVoidsTheBookingAndGivesTheCapacityBack()
    {
        var slot = GiveTheCandidateABooking();
        Assert.Equal(9, slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);

        var result = await Handler.HandleAsync(
            new DeleteCandidateCommand(Coordinator, _candidate.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_candidates.Items);
        Assert.Equal(10, slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(8, slot.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
        Assert.Equal(6, slot.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(BookingStatus.Cancelled, _bookings.Items.Single().Status);
        Assert.Equal(InviteStatus.Superseded, _invites.Items.Single().Status);
        Assert.True(_audit.Contains(AuditAction.BookingCancelled));
        Assert.True(_audit.Contains(AuditAction.CapacityIncremented));
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AConfirmedDeleteCascadesToTheActiveRecoveryBookingAndAuditsTheDeletion()
    {
        var slot = GiveTheCandidateABooking();
        var recoverySlot = GiveTheCandidateARecoveryBooking();
        Assert.Equal(9, recoverySlot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);

        var result = await Handler.HandleAsync(
            new DeleteCandidateCommand(Coordinator, _candidate.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_candidates.Items);
        Assert.Equal(2, _bookings.Items.Count);
        Assert.All(_bookings.Items, booking => Assert.Equal(BookingStatus.Cancelled, booking.Status));
        Assert.Equal(10, recoverySlot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(10, slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.True(_audit.Contains(AuditAction.CandidateDeleted));
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AnUnknownCandidateIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new DeleteCandidateCommand(Coordinator, Guid.NewGuid(), true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task ANonCoordinatorIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new DeleteCandidateCommand(Guid.NewGuid(), _candidate.Id, true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    private ConfirmedSlot GiveTheCandidateARecoveryBooking()
    {
        var original = _bookings.Items.Single();
        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, 11), new TimeOnly(9, 0)), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 10);
        var recoverySlot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);
        _slots.Add(recoverySlot);

        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(), _candidate.Id, original.Id, "recovery-hash", Now.AddDays(4),
            [recoverySlot.Id, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        _invites.Add(recoveryInvite);

        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, recoverySlot.Id, "recovery-manage-hash", Now);
        _bookings.Add(recovery);
        recoveryInvite.MarkUsed();

        recoverySlot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.DrugAndAlcoholTesting));

        return recoverySlot;
    }

    private ConfirmedSlot GiveTheCandidateABooking()
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);
        _slots.Add(slot);

        var invite = Invite.CreateInitial(
            Guid.NewGuid(), _candidate.Id, "hash", Now.AddDays(4),
            [slot.Id, Guid.NewGuid(), Guid.NewGuid()], _candidate.RequiredAppointmentTypeIds, 0);
        _invites.Add(invite);

        var booking = Booking.Create(Guid.NewGuid(), invite, slot.Id, "manage-hash", Now);
        _bookings.Add(booking);

        foreach (var typeId in _candidate.RequiredAppointmentTypeIds)
        {
            slot.CapacityFor(typeId).Decrement();
            _appointments.Add(BookingAppointment.Create(Guid.NewGuid(), booking.Id, typeId));
        }

        return slot;
    }
}
