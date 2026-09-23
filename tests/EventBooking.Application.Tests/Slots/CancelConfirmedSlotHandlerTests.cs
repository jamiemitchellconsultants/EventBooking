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
