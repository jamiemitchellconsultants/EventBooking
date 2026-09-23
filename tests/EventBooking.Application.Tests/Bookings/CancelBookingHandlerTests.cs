using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies booking cancellation behavior and its candidate lifecycle lock order.</summary>
public class CancelBookingHandlerTests
{
    private static readonly CandidatePortalOptions Portal = new(
        "https://booking.example.com", "Corporate HQ", "recruitment@corp.com");

    private readonly TransactionOperationLog _operations = new();
    private readonly InMemoryBookingRepository _bookings;
    private readonly InMemoryCandidateRepository _candidates;
    private readonly InMemoryConfirmedSlotRepository _slots;
    private readonly InMemorySlotCapacityRepository _capacities;
    private readonly InMemoryBookingAppointmentRepository _appointments;
    private readonly InMemoryInviteRepository _invites;
    private readonly InMemoryEmployeeGroupRepository _groups = new();
    private readonly InMemorySystemSettingsRepository _settings = new();
    private readonly RecordingEmailSender _email = new();
    private readonly InMemoryEmailDeliveryRepository _deliveries = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork;
    private readonly FakeTokenService _tokens = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly Candidate _candidate;
    private readonly ConfirmedSlot _booked;
    private readonly string _manageToken;

    private CancelBookingHandler Handler => new(
        _bookings,
        _slots,
        _candidates,
        _invites,
        new BookingCanceller(_appointments, _capacities, _audit),
        new InviteIssuer(
            _invites, _groups, new EligibleSlotFinder(_slots, _clock), _settings,
            _tokens, EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
            _audit, _clock, Portal),
        EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
        _tokens,
        _clock,
        _unitOfWork);

    public CancelBookingHandlerTests()
    {
        _invites = new InMemoryInviteRepository(_operations);
        _bookings = new InMemoryBookingRepository(_operations);
        _candidates = new InMemoryCandidateRepository(_operations);
        _slots = new InMemoryConfirmedSlotRepository(_operations);
        _capacities = new InMemorySlotCapacityRepository(_slots, _operations);
        _appointments = new InMemoryBookingAppointmentRepository(_bookings, _operations);
        _unitOfWork = new FakeUnitOfWork(_operations);

        var pilots = EmployeeGroup.Define(
            EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        _groups.Items.Add(pilots);
        _candidate = Candidate.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", pilots);
        _candidates.Add(_candidate);

        _booked = AddSlot(10);
        AddSlot(12);
        AddSlot(14);
        AddSlot(16);

        var inviteId = Guid.NewGuid();
        var issued = _tokens.Issue(inviteId);
        var invite = Invite.CreateInitial(
            inviteId, _candidate.Id, issued.TokenHash, _clock.UtcNow.AddDays(4),
            [_booked.Id, _slots.Items[1].Id, _slots.Items[2].Id],
            _candidate.RequiredAppointmentTypeIds, 0);
        _invites.Add(invite);
        _candidate.MarkInvited();

        var bookingId = Guid.NewGuid();
        var manage = _tokens.Issue(bookingId);
        _manageToken = manage.Token;
        _bookings.Add(Booking.Create(bookingId, invite, _booked.Id, manage.TokenHash, _clock.UtcNow));
        invite.MarkUsed();
        _candidate.MarkBooked();

        foreach (var typeId in _candidate.RequiredAppointmentTypeIds)
        {
            _booked.CapacityFor(typeId).Decrement();
            _appointments.Add(BookingAppointment.Create(Guid.NewGuid(), bookingId, typeId));
        }
    }

    [Fact]
    public async Task CancellingVoidsTheBookingAndGivesTheCapacityBack()
    {
        var result = await Handler.HandleAsync(
            new CancelBookingCommand(_manageToken, false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Reinvited);

        Assert.Equal(BookingStatus.Cancelled, _bookings.Items.Single().Status);
        Assert.Equal(10, _booked.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(8, _booked.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
        Assert.Equal(CandidateStatus.NotYetInvited, _candidate.Status);
        Assert.True(_audit.Contains(AuditAction.BookingCancelled));
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task CancellingWithoutRebookingSendsNoEmail()
    {
        await Handler.HandleAsync(new CancelBookingCommand(_manageToken, false), CancellationToken.None);

        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task CancellingAfterTheSlotDateIsRefused()
    {
        _clock.UtcNow = new DateTimeOffset(2026, 9, 11, 9, 0, 0, TimeSpan.Zero);

        var result = await Handler.HandleAsync(
            new CancelBookingCommand(_manageToken, false), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(BookingStatus.Active, _bookings.Items.Single().Status);
    }

    /// <summary>
    /// Cancellation takes the candidate lifecycle lock before its active booking, slot, and
    /// capacity rows so rebooking cannot race a concurrent confirmation for the candidate.
    /// </summary>
    [Fact]
    public async Task CancellingUsesTheCandidateLifecycleLockOrder()
    {
        await Handler.HandleAsync(new CancelBookingCommand(_manageToken, false), CancellationToken.None);

        Assert.Equal(
            [
                "booking-slot-located",
                "transaction-begun",
                "candidate-locked",
                "pending-invites-locked",
                "booking-locked",
                "active-recovery-locked",
                "slot-guard-locked",
                "capacity-locked"
            ],
            _operations.Events);
    }

    [Fact]
    public async Task CancelAndRebookImmediatelyIssuesAFreshInvite()
    {
        var result = await Handler.HandleAsync(
            new CancelBookingCommand(_manageToken, true), CancellationToken.None);

        Assert.True(result.Value.Reinvited);
        Assert.Equal(CandidateStatus.Invited, _candidate.Status);
        Assert.Single(_email.Sent);
        Assert.Equal(2, _invites.Items.Count);
        Assert.Equal(InviteStatus.Pending, _invites.Items[1].Status);
        Assert.Equal(0, _invites.Items[1].RetryCount);
    }

    /// <summary>
    /// A commit failure after the cancellation and replacement invite have been staged must not
    /// allow a provider side effect to escape the rolled-back transaction.
    /// </summary>
    [Fact]
    public async Task ACommitFailureDoesNotCallTheEmailTransport()
    {
        _unitOfWork.ThrowOnCommit = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => Handler.HandleAsync(
            new CancelBookingCommand(_manageToken, true), CancellationToken.None));

        Assert.Empty(_email.Sent);
        Assert.Equal(1, _unitOfWork.RollbackCount);
    }

    [Fact]
    public async Task TheFreedSlotCanBeOfferedAgainImmediately()
    {
        var result = await Handler.HandleAsync(
            new CancelBookingCommand(_manageToken, true), CancellationToken.None);

        Assert.True(result.Value.Reinvited);
        Assert.Contains(_booked.Id, _invites.Items[1].OfferedSlotIds);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-token")]
    public async Task AMalformedTokenIsRejectedWithTheGenericMessage(string? token)
    {
        var result = await Handler.HandleAsync(new CancelBookingCommand(token, false), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Equal("This booking link is no longer valid.", result.Error.Message);
    }

    [Fact]
    public async Task ACancelledBookingIsRejectedWithoutReleasingCapacityAgain()
    {
        await Handler.HandleAsync(new CancelBookingCommand(_manageToken, false), CancellationToken.None);

        var result = await Handler.HandleAsync(
            new CancelBookingCommand(_manageToken, false), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Equal("This booking link is no longer valid.", result.Error.Message);
        Assert.Equal(10, _booked.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(1, _capacities.LockCallCount);
    }

    [Fact]
    public async Task CancellingARecoveryBookingReturnsOnlyItsOwnCapacity()
    {
        var (_, recoveryToken) = AddActiveRecovery();
        var originalDat = _booked.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity;
        var originalUni = _booked.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity;

        var result = await Handler.HandleAsync(
            new CancelBookingCommand(recoveryToken, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Reinvited);
        Assert.Equal(BookingStatus.Cancelled, _bookings.Items.Single(b => !b.IsOriginal).Status);
        Assert.Equal(BookingStatus.Active, _bookings.Items.Single(b => b.IsOriginal).Status);
        Assert.Equal(
            10, _slots.Items[1].CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(originalDat, _booked.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(originalUni, _booked.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
        Assert.Equal(CandidateStatus.Booked, _candidate.Status);
        Assert.Equal(2, _invites.Items.Count);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task CancellingAnOriginalSupersedesItsPendingRecoveryInvite()
    {
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(), _candidate.Id, _bookings.Items.Single().Id, "hash-recovery-pending",
            _clock.UtcNow.AddDays(4),
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        _invites.Add(recoveryInvite);

        var result = await Handler.HandleAsync(
            new CancelBookingCommand(_manageToken, false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InviteStatus.Superseded, recoveryInvite.Status);
        Assert.Equal(BookingStatus.Cancelled, _bookings.Items.Single().Status);
        Assert.Equal(CandidateStatus.NotYetInvited, _candidate.Status);
    }

    [Fact]
    public async Task CancellingAnOriginalCancelsItsActiveRecovery()
    {
        var (recovery, _) = AddActiveRecovery();

        var result = await Handler.HandleAsync(
            new CancelBookingCommand(_manageToken, false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Cancelled, recovery.Status);
        Assert.Equal(BookingStatus.Cancelled, _bookings.Items.Single(b => b.IsOriginal).Status);
        Assert.Equal(
            10, _slots.Items[1].CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(
            10, _booked.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(
            8, _booked.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
        Assert.Equal(CandidateStatus.NotYetInvited, _candidate.Status);
        Assert.Equal(2, _audit.Entries.Count(e => e.Action == AuditAction.BookingCancelled));
    }

    private (Booking Recovery, string RecoveryManageToken) AddActiveRecovery()
    {
        var original = _bookings.Items.Single();
        var recoveryInviteId = Guid.NewGuid();
        var issued = _tokens.Issue(recoveryInviteId);
        var recoveryInvite = Invite.CreateRecovery(
            recoveryInviteId, _candidate.Id, original.Id, issued.TokenHash,
            _clock.UtcNow.AddDays(4),
            [_slots.Items[1].Id, _slots.Items[2].Id, _slots.Items[3].Id],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        _invites.Add(recoveryInvite);

        var recoveryId = Guid.NewGuid();
        var manage = _tokens.Issue(recoveryId);
        var recovery = Booking.CreateRecovery(
            recoveryId, recoveryInvite, original, _slots.Items[1].Id,
            manage.TokenHash, _clock.UtcNow);
        _bookings.Add(recovery);
        recoveryInvite.MarkUsed();

        _slots.Items[1].CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.DrugAndAlcoholTesting));

        return (recovery, manage.Token);
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
