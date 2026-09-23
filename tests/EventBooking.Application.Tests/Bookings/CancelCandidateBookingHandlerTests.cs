using EventBooking.Application.Access;
using EventBooking.Application.Bookings;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies a coordinator cancelling one candidate booking reuses the candidate path exactly.</summary>
public class CancelCandidateBookingHandlerTests
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
    private readonly Guid _bookingId;
    private readonly Guid _staffUserId = Guid.NewGuid();

    /// <summary>Grants or denies exactly the capability the handler is expected to demand.</summary>
    private sealed class FakeAuthorizer(bool granted) : IStaffAccessAuthorizer
    {
        public StaffCapability? Seen { get; private set; }

        public Task<Result<StaffAccessContext>> AuthorizeAsync(
            Guid staffUserId,
            StaffCapability capability,
            Guid? requiredAppointmentTypeId,
            CancellationToken cancellationToken)
        {
            Seen = capability;
            return Task.FromResult(granted
                ? Result<StaffAccessContext>.Success(
                    new StaffAccessContext(staffUserId, new HashSet<Role> { Role.Coordinator }, null))
                : Result<StaffAccessContext>.Failure(
                    Error.Forbidden("This staff profile cannot perform this operation.")));
        }
    }

    private CancelCandidateBookingHandler HandlerFor(IStaffAccessAuthorizer access) => new(
        access,
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
        _clock,
        _unitOfWork);

    private CancelCandidateBookingHandler Handler => HandlerFor(new FakeAuthorizer(true));

    public CancelCandidateBookingHandlerTests()
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

        _bookingId = Guid.NewGuid();
        var manage = _tokens.Issue(_bookingId);
        _bookings.Add(Booking.Create(_bookingId, invite, _booked.Id, manage.TokenHash, _clock.UtcNow));
        invite.MarkUsed();
        _candidate.MarkBooked();

        foreach (var typeId in _candidate.RequiredAppointmentTypeIds)
        {
            _booked.CapacityFor(typeId).Decrement();
            _appointments.Add(BookingAppointment.Create(Guid.NewGuid(), _bookingId, typeId));
        }
    }

    private CancelCandidateBookingCommand Command(Guid bookingId, bool rebook = false) =>
        new(_staffUserId, _candidate.Id, bookingId, rebook);

    [Fact]
    public async Task CancellingAnOriginalWithNoActiveRecoveryVoidsItResetsTheCandidateAndAuditsStaffAttribution()
    {
        var result = await Handler.HandleAsync(Command(_bookingId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Reinvited);
        Assert.Equal(BookingStatus.Cancelled, _bookings.Items.Single().Status);
        Assert.Equal(10, _booked.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(8, _booked.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
        Assert.Equal(CandidateStatus.NotYetInvited, _candidate.Status);
        Assert.Equal(1, _unitOfWork.CommitCount);
        Assert.Empty(_email.Sent);

        var cancelled = Assert.Single(_audit.Entries, e => e.Action == AuditAction.BookingCancelled);
        Assert.Equal(ActorType.Staff, cancelled.ActorType);
        Assert.Equal(_staffUserId.ToString(), cancelled.ActorId);
    }

    [Fact]
    public async Task CancellingAnOriginalWithAnActiveRecoveryCascadesOntoTheRecoveryFirst()
    {
        var recovery = AddActiveRecovery();

        var result = await Handler.HandleAsync(Command(_bookingId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Cancelled, recovery.Status);
        Assert.Equal(BookingStatus.Cancelled, _bookings.Items.Single(b => b.IsOriginal).Status);
        Assert.Equal(
            10, _slots.Items[1].CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(10, _booked.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(CandidateStatus.NotYetInvited, _candidate.Status);

        var cancellations = _audit.Entries.Where(e => e.Action == AuditAction.BookingCancelled).ToList();
        Assert.Equal(2, cancellations.Count);
        Assert.All(cancellations, e => Assert.Equal(ActorType.Staff, e.ActorType));
        Assert.All(cancellations, e => Assert.Equal(_staffUserId.ToString(), e.ActorId));
    }

    [Fact]
    public async Task CancellingAnOriginalSupersedesItsPendingRecoveryInvite()
    {
        var pendingRecovery = Invite.CreateRecovery(
            Guid.NewGuid(), _candidate.Id, _bookingId, "hash-recovery-pending",
            _clock.UtcNow.AddDays(4),
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        _invites.Add(pendingRecovery);

        var result = await Handler.HandleAsync(Command(_bookingId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InviteStatus.Superseded, pendingRecovery.Status);
    }

    [Fact]
    public async Task CancellingARecoveryBookingAloneLeavesTheOriginalAndItsCapacityUntouched()
    {
        var recovery = AddActiveRecovery();
        var originalDat = _booked.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity;
        var originalUni = _booked.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity;

        var result = await Handler.HandleAsync(Command(recovery.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Reinvited);
        Assert.Equal(BookingStatus.Cancelled, recovery.Status);
        Assert.Equal(BookingStatus.Active, _bookings.Items.Single(b => b.IsOriginal).Status);
        Assert.Equal(
            10, _slots.Items[1].CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(originalDat, _booked.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(originalUni, _booked.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
        Assert.Equal(CandidateStatus.Booked, _candidate.Status);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task RebookTrueOnAnOriginalIssuesAReplacementInviteAndReportsReinvited()
    {
        var result = await Handler.HandleAsync(Command(_bookingId, rebook: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Reinvited);
        Assert.True(result.Value.InviteCreated);
        Assert.Equal(CandidateStatus.Invited, _candidate.Status);
        Assert.Single(_email.Sent);
        Assert.Equal(2, _invites.Items.Count);
        Assert.Equal(InviteStatus.Pending, _invites.Items[1].Status);
        Assert.Contains(_booked.Id, _invites.Items[1].OfferedSlotIds);
    }

    [Fact]
    public async Task RebookTrueOnARecoveryBookingIsAConflictAndMutatesNothing()
    {
        var recovery = AddActiveRecovery();

        var result = await Handler.HandleAsync(
            Command(recovery.Id, rebook: true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(BookingStatus.Active, recovery.Status);
        Assert.Equal(BookingStatus.Active, _bookings.Items.Single(b => b.IsOriginal).Status);
        Assert.Equal(CandidateStatus.Booked, _candidate.Status);
        Assert.Equal(0, _unitOfWork.CommitCount);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task UnknownBookingIdIsNotFound()
    {
        var result = await Handler.HandleAsync(Command(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Equal(BookingStatus.Active, _bookings.Items.Single().Status);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task CancellingAfterTheSlotDateIsRefused()
    {
        _clock.UtcNow = new DateTimeOffset(2026, 9, 11, 9, 0, 0, TimeSpan.Zero);

        var result = await Handler.HandleAsync(Command(_bookingId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(BookingStatus.Active, _bookings.Items.Single().Status);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AnotherCandidatesBookingIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new CancelCandidateBookingCommand(_staffUserId, Guid.NewGuid(), _bookingId, false),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Equal(BookingStatus.Active, _bookings.Items.Single().Status);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task ANonActiveBookingIsNotFound()
    {
        await Handler.HandleAsync(Command(_bookingId), CancellationToken.None);

        var result = await Handler.HandleAsync(Command(_bookingId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Equal(10, _booked.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
    }

    [Fact]
    public async Task CallerWithoutManageCandidatesIsForbiddenAndMutatesNothing()
    {
        var authorizer = new FakeAuthorizer(false);

        var result = await HandlerFor(authorizer).HandleAsync(Command(_bookingId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(StaffCapability.ManageCandidates, authorizer.Seen);
        Assert.Equal(BookingStatus.Active, _bookings.Items.Single().Status);
        Assert.Equal(CandidateStatus.Booked, _candidate.Status);
        Assert.Equal(0, _unitOfWork.CommitCount);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task CancellationTakesTheCandidateLifecycleLockOrder()
    {
        await Handler.HandleAsync(Command(_bookingId), CancellationToken.None);

        Assert.Equal(
            [
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

    private Booking AddActiveRecovery()
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
