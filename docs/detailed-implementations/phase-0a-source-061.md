# 00a — Port source 61 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## tests/EventBooking.Application.Tests/Bookings/CancelCandidateBookingHandlerTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Bookings/CancelCandidateBookingHandlerTests.cs","encoding":"utf8","sha256":"f43346d2252b32d41773fc35e3f1254aab3c33cefc83c10bacf53e10976125a1","parts":1,"part":1} -->

`````csharp
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
`````

## tests/EventBooking.Application.Tests/Bookings/ConfirmBookingHandlerTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Bookings/ConfirmBookingHandlerTests.cs","encoding":"utf8","sha256":"853fc3d225c0ff32947e6d2592ae8d78b094d7714046be31d054313cf272b0b9","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies booking confirmation behavior and its candidate lifecycle lock order.</summary>
public class ConfirmBookingHandlerTests
{
    private static readonly CandidatePortalOptions Portal = new(
        "https://booking.example.com", "Corporate HQ", "recruitment@corp.com");

    private static readonly Guid StaffUserId = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly TransactionOperationLog _operations = new();
    private readonly InMemoryInviteRepository _invites;
    private readonly InMemoryCandidateRepository _candidates;
    private readonly InMemoryConfirmedSlotRepository _slots;
    private readonly InMemoryBookingRepository _bookings;
    private readonly InMemoryBookingAppointmentRepository _appointments;
    private readonly RecordingEmailSender _email = new();
    private readonly InMemoryEmailDeliveryRepository _deliveries = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork;
    private readonly FakeTokenService _tokens = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly Candidate _candidate;
    private readonly Invite _invite;
    private readonly string _token;
    private readonly ConfirmedSlot _chosen;

    private ConfirmBookingHandler Handler
    {
        get
        {
            var capacities = new InMemorySlotCapacityRepository(_slots, _operations);
            return new ConfirmBookingHandler(
                _invites, _candidates, _slots, _bookings,
                _appointments, capacities,
                new EligibleSlotFinder(_slots, _clock), _tokens,
                EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock), _audit,
                _unitOfWork, _clock, Portal);
        }
    }

    public ConfirmBookingHandlerTests()
    {
        _invites = new InMemoryInviteRepository(_operations);
        _candidates = new InMemoryCandidateRepository(_operations);
        _bookings = new InMemoryBookingRepository(_operations);
        _slots = new InMemoryConfirmedSlotRepository(_operations);
        _appointments = new InMemoryBookingAppointmentRepository(_bookings, _operations);
        _unitOfWork = new FakeUnitOfWork(_operations);

        var pilots = EmployeeGroup.Define(
            EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        _candidate = Candidate.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", pilots);
        _candidates.Add(_candidate);

        _chosen = AddSlot(10, 9);
        var others = new[] { AddSlot(11, 13).Id, AddSlot(13, 9).Id };

        var inviteId = Guid.NewGuid();
        var issued = _tokens.Issue(inviteId);
        _token = issued.Token;
        _invite = Invite.CreateInitial(
            inviteId, _candidate.Id, issued.TokenHash, _clock.UtcNow.AddDays(4),
            [_chosen.Id, others[0], others[1]],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting], 0);
        _invites.Add(_invite);
        _candidate.MarkInvited();
    }

    private Task<Result<ConfirmBookingOutcome>> Confirm(Guid? slotId = null) =>
        Handler.HandleAsync(
            new ConfirmBookingCommand(_token, slotId ?? _chosen.Id), CancellationToken.None);

    [Fact]
    public async Task ConfirmingCreatesABookingAndConsumesTheInvite()
    {
        var result = await Confirm();

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 9, 10), result.Value.Date);
        Assert.Equal(new TimeOnly(9, 0), result.Value.StartTime);
        Assert.Equal(new TimeOnly(13, 0), result.Value.EndTime);

        var booking = Assert.Single(_bookings.Items);
        Assert.Equal(result.Value.BookingId, booking.Id);
        Assert.Equal(_candidate.Id, booking.CandidateId);
        Assert.Equal(_chosen.Id, booking.ConfirmedSlotId);
        Assert.Equal(_invite.Id, booking.InviteId);
        Assert.Equal(BookingStatus.Active, booking.Status);

        Assert.Equal(InviteStatus.Used, _invite.Status);
        Assert.Equal(CandidateStatus.Booked, _candidate.Status);
    }

    [Fact]
    public async Task ConfirmingReturnsTheConfiguredHeadOfficeAddress()
    {
        var result = await Confirm();

        Assert.True(result.IsSuccess);
        Assert.Equal(Portal.HeadOfficeAddress, result.Value.HeadOfficeAddress);
    }

    [Fact]
    public async Task OnlyTheRequiredAppointmentTypesAreDecremented()
    {
        await Confirm();

        Assert.Equal(9, _chosen.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(7, _chosen.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
        Assert.Equal(6, _chosen.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
    }

    /// <summary>Verifies confirmation uses one business commit and one delivery result commit.</summary>
    [Fact]
    public async Task TheWholeConfirmRunsInOneCommittedTransaction()
    {
        await Confirm();

        Assert.Equal(2, _unitOfWork.CommitCount);
        Assert.Equal(0, _unitOfWork.RollbackCount);
    }

    /// <summary>
    /// The candidate lifecycle lock precedes invite, active-booking, slot, and capacity locks so
    /// disjoint tokens cannot make two bookings for the same candidate.
    /// </summary>
    [Fact]
    public async Task ConfirmationUsesTheCandidateLifecycleLockOrder()
    {
        await Confirm();

        Assert.Equal(
            ["transaction-begun", "candidate-locked", "invite-locked", "active-booking-locked", "slot-guard-locked", "capacity-locked"],
            _operations.Events.Take(6).ToList());
    }

    [Fact]
    public async Task AConfirmationEmailIsSentWithAManageLink()
    {
        var result = await Confirm();

        var message = Assert.Single(_email.Sent);
        Assert.Equal(EmailTemplate.BookingConfirmation, message.Template);
        Assert.Contains(
            $"https://booking.example.com/manage/{result.Value.ManageToken}", message.TextBody);
        Assert.Contains("Corporate HQ", message.TextBody);
    }

    [Fact]
    public async Task OnlyTheManageTokenHashIsStored()
    {
        var result = await Confirm();

        var booking = _bookings.Items.Single();
        Assert.Equal(_tokens.Hash(result.Value.ManageToken), booking.ManageTokenHash);
        Assert.NotEqual(result.Value.ManageToken, booking.ManageTokenHash);
    }

    [Fact]
    public async Task ConfirmingIsAudited()
    {
        await Confirm();

        Assert.True(_audit.Contains(AuditAction.BookingCreated));
        Assert.Equal(
            2, _audit.Entries.Count(e => e.Action == AuditAction.CapacityDecremented));
        Assert.All(
            _audit.Entries,
            e => Assert.Equal(ActorType.CandidateToken, e.ActorType));
    }

    [Fact]
    public async Task AFailedConfirmationEmailStillLeavesTheBookingInPlace()
    {
        _email.FailNextSend = true;

        var result = await Confirm();

        Assert.True(result.IsSuccess);
        Assert.Single(_bookings.Items);
        Assert.Equal("Failed", result.Value.DeliveryStatus);
        Assert.Equal(EmailStatus.Failed, Assert.Single(_deliveries.Items).Status);
    }

    [Fact]
    public async Task ChoosingAnOptionTheInviteNeverOfferedIsRejected()
    {
        var other = AddSlot(20, 9);

        var result = await Confirm(other.Id);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("That time is not one of your options.", result.Error.Message);
        Assert.Empty(_bookings.Items);
    }

    [Fact]
    public async Task AnExhaustedOptionIsDroppedAndReplaced()
    {
        // Fill the chosen slot's drug and alcohol capacity between offer and click.
        var capacity = _chosen.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);
        var remainingCapacity = capacity.RemainingCapacity;
        for (var i = 0; i < remainingCapacity; i++)
        {
            capacity.Decrement();
        }

        var replacement = AddSlot(15, 9);

        var result = await Confirm();

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(
            "That time filled up while you were choosing. Please pick from the updated options.",
            result.Error.Message);

        Assert.Empty(_bookings.Items);
        Assert.Equal(InviteStatus.Pending, _invite.Status);
        Assert.False(_invite.Offers(_chosen.Id));
        Assert.True(_invite.Offers(replacement.Id));
        Assert.Equal(3, _invite.Options.Count);
        Assert.True(_audit.Contains(AuditAction.InviteOptionReplaced));
    }

    /// <summary>
    /// With no replacement available the option is dropped and the candidate is flagged for
    /// coordinator follow-up rather than left with a silently shrinking choice (Issue #242).
    /// </summary>
    [Fact]
    public async Task WithNoReplacementAvailableTheCandidateIsFlaggedForFollowUp()
    {
        var capacity = _chosen.CapacityFor(AppointmentTypeIds.UniformFitting);
        var remainingCapacity = capacity.RemainingCapacity;
        for (var i = 0; i < remainingCapacity; i++)
        {
            capacity.Decrement();
        }

        var result = await Confirm();

        Assert.True(result.IsFailure);
        Assert.Equal(2, _invite.Options.Count);
        Assert.False(_invite.Offers(_chosen.Id));
        Assert.Equal(CandidateStatus.NoResponseNeedsFollowUp, _candidate.Status);
        Assert.True(_audit.Contains(AuditAction.InviteOptionReplaced));
    }

    [Fact]
    public async Task ChoosingASlotThatWasCancelledIsTreatedTheSameWay()
    {
        _chosen.Cancel();

        var result = await Confirm();

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.False(_invite.Offers(_chosen.Id));
    }

    /// <summary>Ensures a stale option cannot consume capacity and is replaced when possible.</summary>
    [Fact]
    public async Task ChoosingAnOptionOnTheHeadOfficeDateDropsItAndAddsAFutureReplacement()
    {
        var stale = AddSlot(3, 9);
        _invite.RemoveOption(_chosen.Id);
        _invite.AddOption(stale.Id);

        var result = await Confirm(stale.Id);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(
            "That time filled up while you were choosing. Please pick from the updated options.",
            result.Error.Message);
        Assert.Empty(_bookings.Items);
        Assert.Equal(10, stale.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(8, stale.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
        Assert.False(_invite.Offers(stale.Id));
        Assert.True(_invite.Offers(_chosen.Id));
        Assert.True(_audit.Contains(AuditAction.InviteOptionReplaced));
    }

    [Fact]
    public async Task AnExpiredInviteCannotBeConfirmed()
    {
        _clock.Advance(TimeSpan.FromDays(5));

        var result = await Confirm();

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Equal("This booking link is no longer valid.", result.Error.Message);
    }

    [Fact]
    public async Task AnInviteCannotBeConfirmedTwice()
    {
        await Confirm();

        var result = await Confirm();

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Single(_bookings.Items);
    }

    [Fact]
    public async Task ADriftedSnapshotIsSupersededWithoutDisclosure()
    {
        _candidate.AssignEmployeeGroup(EmployeeGroup.Define(
            EmployeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
            [AppointmentTypeIds.MedicalCheckUp]));

        var result = await Confirm();

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Equal("This booking link is no longer valid.", result.Error.Message);
        Assert.Equal(InviteStatus.Superseded, _invite.Status);
        Assert.Empty(_bookings.Items);
    }

    [Fact]
    public async Task ARecoveryInviteConfirmsARootLinkedBooking()
    {
        var (original, recovery, token) = BookWithRecoverableNoShow();

        var result = await Handler.HandleAsync(
            new ConfirmBookingCommand(token, _chosen.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var booking = Assert.Single(_bookings.Items, b => b.Id == result.Value.BookingId);
        Assert.Equal(original.Id, booking.RecoveryOfBookingId);
        Assert.False(booking.IsOriginal);
        Assert.Equal(BookingStatus.Active, booking.Status);
        Assert.Equal(BookingStatus.Active, original.Status);
        Assert.Equal(CandidateStatus.Booked, _candidate.Status);
        Assert.Equal(InviteStatus.Used, recovery.Status);

        var appointment = Assert.Single(_appointments.Items, a => a.BookingId == booking.Id);
        Assert.Equal(AppointmentTypeIds.DrugAndAlcoholTesting, appointment.AppointmentTypeId);
        Assert.Equal(BookingAppointmentStatus.Expected, appointment.Status);

        Assert.True(_audit.Contains(AuditAction.RecoveryBookingCreated));
        Assert.False(_audit.Contains(AuditAction.BookingCreated));
        Assert.Single(_email.Sent);

        Assert.Equal(8, _chosen.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(7, _chosen.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
        Assert.Equal(6, _chosen.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
    }

    [Fact]
    public async Task RecoveryConfirmationLeavesCompletedHistoryUntouched()
    {
        var (original, _, token) = BookWithRecoverableNoShow();
        var completed = _appointments.Items.Single(
            a => a.AppointmentTypeId == AppointmentTypeIds.UniformFitting);
        var version = completed.Version;

        await Handler.HandleAsync(
            new ConfirmBookingCommand(token, _chosen.Id), CancellationToken.None);

        Assert.Equal(BookingAppointmentStatus.Completed, completed.Status);
        Assert.Equal(version, completed.Version);
        Assert.Equal(2, _appointments.Items.Count(a => a.BookingId == original.Id));
        Assert.Equal(7, _chosen.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
        Assert.Equal(6, _chosen.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
    }

    [Fact]
    public async Task AStaleRecoverySnapshotIsSupersededWithoutDisclosure()
    {
        var (_, recovery, token) = BookWithRecoverableNoShow();
        _appointments.Items
            .Single(a => a.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting)
            .TransitionTo(BookingAppointmentStatus.Expected, StaffUserId, _clock.UtcNow, false, false);

        var result = await Handler.HandleAsync(
            new ConfirmBookingCommand(token, _chosen.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Equal("This booking link is no longer valid.", result.Error.Message);
        Assert.Equal(InviteStatus.Superseded, recovery.Status);
        Assert.DoesNotContain(_bookings.Items, b => !b.IsOriginal);
    }

    /// <summary>
    /// Recovery confirmation locks Candidate, Invite, original Booking, active recovery,
    /// slot, then capacities — the global lifecycle order with the journey locks included.
    /// </summary>
    [Fact]
    public async Task RecoveryConfirmationUsesTheCandidateLifecycleLockOrder()
    {
        var (_, _, token) = BookWithRecoverableNoShow();

        await Handler.HandleAsync(
            new ConfirmBookingCommand(token, _chosen.Id), CancellationToken.None);

        Assert.Equal(
            [
                "transaction-begun",
                "candidate-locked",
                "invite-locked",
                "original-booking-locked",
                "active-recovery-locked",
                "slot-guard-locked",
                "capacity-locked",
            ],
            _operations.Events.Take(7).ToList());
    }

    private (Booking Original, Invite Recovery, string RecoveryToken) BookWithRecoverableNoShow()
    {
        var originalId = Guid.NewGuid();
        var manage = _tokens.Issue(originalId);
        var original = Booking.Create(originalId, _invite, _chosen.Id, manage.TokenHash, _clock.UtcNow);
        _bookings.Add(original);
        _invite.MarkUsed();
        _candidate.MarkBooked();

        foreach (var typeId in _candidate.RequiredAppointmentTypeIds)
        {
            _chosen.CapacityFor(typeId).Decrement();
        }

        var missed = BookingAppointment.Create(
            Guid.NewGuid(), original.Id, AppointmentTypeIds.DrugAndAlcoholTesting);
        missed.TransitionTo(BookingAppointmentStatus.NoShow, StaffUserId, _clock.UtcNow, false, true);
        _appointments.Add(missed);

        var completed = BookingAppointment.Create(
            Guid.NewGuid(), original.Id, AppointmentTypeIds.UniformFitting);
        completed.TransitionTo(BookingAppointmentStatus.CheckedIn, StaffUserId, _clock.UtcNow, true, false);
        completed.TransitionTo(BookingAppointmentStatus.Completed, StaffUserId, _clock.UtcNow, false, false);
        _appointments.Add(completed);

        var recoveryId = Guid.NewGuid();
        var issued = _tokens.Issue(recoveryId);
        var recovery = Invite.CreateRecovery(
            recoveryId, _candidate.Id, original.Id, issued.TokenHash, _clock.UtcNow.AddDays(4),
            [_chosen.Id, AddSlot(15, 9).Id, AddSlot(17, 9).Id],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        _invites.Add(recovery);

        return (original, recovery, issued.Token);
    }

    private ConfirmedSlot AddSlot(int day, int hour)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, day), new TimeOnly(hour, 0)),
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

## tests/EventBooking.Application.Tests/Bookings/InviteOptionReplacementTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Bookings/InviteOptionReplacementTests.cs","encoding":"utf8","sha256":"b9489007ac4aa4ebd1c2903d0978c8d13ec832b5d7b375f560499529271589e7","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies invite options are topped up to three and shortfalls flag follow-up (Issue #242).</summary>
public class InviteOptionReplacementTests
{
    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryCandidateRepository _candidates = new();
    private readonly InMemoryConfirmedSlotRepository _slots = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTokenService _tokens = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly Candidate _candidate;
    private readonly Invite _invite;
    private readonly string _token;

    private ViewInviteHandler Handler => new(
        _invites, _candidates, _slots, new EligibleSlotFinder(_slots, _clock),
        _audit, _unitOfWork, _tokens, _clock);

    public InviteOptionReplacementTests()
    {
        _candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com",
            EmployeeGroup.Define(
                EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
        _candidates.Add(_candidate);

        var slotIds = new[] { AddSlot(10, 9), AddSlot(11, 13), AddSlot(13, 9) };

        var inviteId = Guid.NewGuid();
        var issued = _tokens.Issue(inviteId);
        _token = issued.Token;
        _invite = Invite.CreateInitial(
            inviteId, _candidate.Id, issued.TokenHash, _clock.UtcNow.AddDays(4), slotIds,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting], 0);
        _invites.Add(_invite);
        _candidate.MarkInvited();
    }

    /// <summary>A cancelled option is replaced so the candidate still sees three live options.</summary>
    [Fact]
    public async Task View_ReplacesCancelledOption_ToRestoreThreeOptions()
    {
        _slots.Items[1].Cancel();
        var spareId = AddSlot(14, 9);

        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Options.Count);
        Assert.Contains(spareId, result.Value.Options.Select(o => o.ConfirmedSlotId));
        Assert.DoesNotContain(
            _slots.Items[1].Id, result.Value.Options.Select(o => o.ConfirmedSlotId));
        Assert.Contains(spareId, _invite.OfferedSlotIds);
        Assert.True(_audit.Contains(AuditAction.InviteOptionReplaced));
    }

    /// <summary>With no replacement available the candidate is flagged for coordinator follow-up.</summary>
    [Fact]
    public async Task View_WithNoReplacementAvailable_FlagsCandidateForFollowUp()
    {
        _slots.Items[1].Cancel();
        _slots.Items[2].Cancel();

        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Options);
        Assert.Equal(CandidateStatus.NoResponseNeedsFollowUp, _candidate.Status);
        Assert.True(_audit.Contains(AuditAction.InviteOptionReplaced));
    }

    private Guid AddSlot(int day, int hour)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, day), new TimeOnly(hour, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);
        _slots.Add(slot);
        return slot.Id;
    }
}
`````

## tests/EventBooking.Application.Tests/Bookings/InviteSnapshotAuthorityTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Bookings/InviteSnapshotAuthorityTests.cs","encoding":"utf8","sha256":"6b6db78603ddcb8de22c702f7758c7fabebf10aacdd666597208b7da13611911","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies portal and confirmation treat Invite Requirements as immutable authority.</summary>
public sealed class InviteSnapshotAuthorityTests
{
    /// <summary>The portal displays snapshot names rather than a later Candidate collection.</summary>
    [Fact]
    public async Task ViewInviteUsesPersistedSnapshot()
    {
        var group = EmployeeGroup.Define(
            EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        var candidate = Candidate.Create(Guid.NewGuid(), "Amara", "amara@example.com", group);
        var candidates = new InMemoryCandidateRepository();
        candidates.Add(candidate);
        var slots = new InMemoryConfirmedSlotRepository();
        var options = Enumerable.Range(0, 3).Select(index =>
            ConfirmedSlot.CreateImported(
                Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 10, 10 + index), new TimeOnly(9, 0)),
                new Dictionary<Guid, int>
                {
                    [AppointmentTypeIds.DrugAndAlcoholTesting] = 5,
                    [AppointmentTypeIds.MedicalCheckUp] = 5,
                    [AppointmentTypeIds.UniformFitting] = 5,
                })).ToList();
        slots.Items.AddRange(options);
        var tokens = new FakeTokenService();
        var issued = tokens.Issue(Guid.NewGuid());
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidate.Id, issued.TokenHash, DateTimeOffset.Parse("2026-10-01T00:00:00Z"),
            options.Select(slot => slot.Id), [AppointmentTypeIds.MedicalCheckUp], 0);
        var invites = new InMemoryInviteRepository();
        invites.Add(invite);
        var clock = new FakeClock(DateTimeOffset.Parse("2026-09-20T00:00:00Z"));

        var result = await new ViewInviteHandler(
                invites, candidates, slots, new EligibleSlotFinder(slots, clock),
                new RecordingAuditLogger(), new FakeUnitOfWork(), tokens, clock)
            .HandleAsync(new ViewInviteQuery(issued.Token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["Medical Check-up"], result.Value.AppointmentTypeNames);
    }
}
`````

## tests/EventBooking.Application.Tests/Bookings/RecoveryBookingLifecycleTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Bookings/RecoveryBookingLifecycleTests.cs","encoding":"utf8","sha256":"cc5e3fba4c1ed641b1ba365488ab51c9bbd1ca8b02493fb1fed0d6dac14a8e23","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Specifies locked recovery snapshot validation before capacity mutation.</summary>
public sealed class RecoveryBookingLifecycleTests
{
    /// <summary>A snapshot that ceased to be recoverable fails before confirmation.</summary>
    [Fact]
    public void CompletedTypeMakesPendingRecoverySnapshotStale()
    {
        var candidateId = Guid.NewGuid();
        var originalId = Guid.NewGuid();
        var recoverySlot = Guid.NewGuid();
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(), candidateId, originalId, "recovery", DateTimeOffset.UtcNow.AddDays(2),
            [recoverySlot, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);
        var attempts = new[]
        {
            new EventBooking.Application.Invites.RecoveryAttempt(
                Guid.NewGuid(), AppointmentTypeIds.MedicalCheckUp,
                BookingAppointmentStatus.NoShow, DateTimeOffset.UtcNow.AddDays(-2)),
            new EventBooking.Application.Invites.RecoveryAttempt(
                Guid.NewGuid(), AppointmentTypeIds.MedicalCheckUp,
                BookingAppointmentStatus.Completed, DateTimeOffset.UtcNow.AddDays(-1)),
        };

        var result = new RecoveryConfirmationValidator().Validate(
            recoveryInvite,
            [AppointmentTypeIds.MedicalCheckUp],
            attempts,
            []);

        Assert.True(result.IsFailure);
        Assert.Equal("recovery_state_changed", result.Error.Code);
    }
}
`````

## tests/EventBooking.Application.Tests/Bookings/ViewInviteHandlerTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Bookings/ViewInviteHandlerTests.cs","encoding":"utf8","sha256":"05fdb5173405d028f2896017091b990eeb3f1b5c27658048ff35507d47e5f665","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Bookings;

public class ViewInviteHandlerTests
{
    private const string InvalidLink = "This booking link is no longer valid.";

    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryCandidateRepository _candidates = new();
    private readonly InMemoryConfirmedSlotRepository _slots = new();
    private readonly FakeTokenService _tokens = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Candidate _candidate;
    private readonly Invite _invite;
    private readonly string _token;

    private ViewInviteHandler Handler => new(
        _invites, _candidates, _slots, new EligibleSlotFinder(_slots, _clock),
        _audit, _unitOfWork, _tokens, _clock);

    public ViewInviteHandlerTests()
    {
        _candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com",
            EmployeeGroup.Define(
                EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
        _candidates.Add(_candidate);

        var slotIds = new[] { AddSlot(10, 9), AddSlot(11, 13), AddSlot(13, 9) };

        var inviteId = Guid.NewGuid();
        var issued = _tokens.Issue(inviteId);
        _token = issued.Token;
        _invite = Invite.CreateInitial(
            inviteId, _candidate.Id, issued.TokenHash, _clock.UtcNow.AddDays(4), slotIds,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting], 0);
        _invites.Add(_invite);
        _candidate.MarkInvited();
    }

    [Fact]
    public async Task AValidTokenReturnsTheCandidateTheirTypesAndThreeOptions()
    {
        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(_invite.Id, result.Value.InviteId);
        Assert.Equal("Amara Novak", result.Value.CandidateName);
        Assert.Equal(
            new[] { "Drug & Alcohol Testing", "Uniform Fitting" },
            result.Value.AppointmentTypeNames);
        Assert.Equal(3, result.Value.Options.Count);
    }

    [Fact]
    public async Task OptionsCarryTheirWindowAndADisplayString()
    {
        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        var first = result.Value.Options[0];
        Assert.Equal(new DateOnly(2026, 9, 10), first.Date);
        Assert.Equal(new TimeOnly(9, 0), first.StartTime);
        Assert.Equal(new TimeOnly(13, 0), first.EndTime);
        Assert.Equal("Thursday 10 Sep 2026, 09:00-13:00", first.Display);
    }

    [Fact]
    public async Task OptionsAreOrderedEarliestFirst()
    {
        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.Equal(
            new[] { new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 11), new DateOnly(2026, 9, 13) },
            result.Value.Options.Select(o => o.Date));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    public async Task AMalformedTokenGivesTheGenericMessage(string? token)
    {
        var result = await Handler.HandleAsync(new ViewInviteQuery(token), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Equal(InvalidLink, result.Error.Message);
    }

    [Fact]
    public async Task AWellFormedTokenForNoInviteGivesTheSameMessage()
    {
        var result = await Handler.HandleAsync(
            new ViewInviteQuery(_tokens.Issue(Guid.NewGuid()).Token), CancellationToken.None);

        Assert.Equal(InvalidLink, result.Error.Message);
    }

    [Fact]
    public async Task AnExpiredInviteGivesTheSameMessage()
    {
        _clock.Advance(TimeSpan.FromDays(5));

        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.Equal(InvalidLink, result.Error.Message);
    }

    [Fact]
    public async Task AUsedInviteGivesTheSameMessage()
    {
        _invite.MarkUsed();

        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.Equal(InvalidLink, result.Error.Message);
    }

    [Fact]
    public async Task AnOptionWhoseSlotHasBeenCancelledIsNotShown()
    {
        _slots.Items[1].Cancel();

        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.Equal(2, result.Value.Options.Count);
        Assert.DoesNotContain(
            new DateOnly(2026, 9, 11), result.Value.Options.Select(o => o.Date));
    }

    /// <summary>Ensures options on the head-office date or earlier are not projected.</summary>
    [Fact]
    public async Task OptionsOnTodayAndEarlierAreNotShown()
    {
        _invite.RemoveOption(_slots.Items[0].Id);
        _invite.RemoveOption(_slots.Items[1].Id);
        _slots.Items[0].Cancel();
        _slots.Items[1].Cancel();
        _invite.AddOption(AddSlot(3, 9));
        _invite.AddOption(AddSlot(2, 13));

        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { new DateOnly(2026, 9, 13) }, result.Value.Options.Select(o => o.Date));
    }

    private Guid AddSlot(int day, int hour)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, day), new TimeOnly(hour, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);
        _slots.Add(slot);
        return slot.Id;
    }
}
`````

## tests/EventBooking.Application.Tests/Candidates/ActiveBookingRequirementTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Candidates/ActiveBookingRequirementTests.cs","encoding":"utf8","sha256":"f09a04a03058119a198ca54e74476e7df63a271444f2c64062ef016af86ffd48","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Candidates;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;

namespace EventBooking.Application.Tests.Candidates;

/// <summary>Verifies candidate requirements cannot drift away from an active booking snapshot.</summary>
public sealed class ActiveBookingRequirementTests
{
    private static readonly EmployeeGroup CabinCrew = EmployeeGroup.Define(
        EmployeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
            AppointmentTypeIds.UniformFitting]);
    private static readonly EmployeeGroup GroundTransport = EmployeeGroup.Define(
        EmployeeGroupIds.GroundTransportServices, "GROUND_TRANSPORT_SERVICES",
        "Ground Transport Services", true,
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
            AppointmentTypeIds.UniformFitting]);
    private static readonly EmployeeGroup Pilots = EmployeeGroup.Define(
        EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);

    /// <summary>Verifies a changed set conflicts before candidate details or requirements mutate.</summary>
    [Fact]
    public async Task ChangedRequirementsAreRejectedBeforeAnyCandidateMutation()
    {
        var (handler, candidate, coordinator) = GivenActiveBooking();

        var result = await handler.UpdateAsync(
            new UpdateCandidateCommand(
                coordinator,
                candidate.Id,
                "Changed Name",
                "changed@example.com",
                Pilots.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("candidate_group_active_booking_conflict", result.Error.Code);
        Assert.Equal(
            "Appointment requirements cannot change while the candidate has an active booking. Cancel and rebook first.",
            result.Error.Message);
        Assert.Equal("Amara Novak", candidate.Name);
        Assert.Equal("amara@example.com", candidate.Email);
        Assert.Equal(
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting],
            candidate.RequiredAppointmentTypeIds);
    }

    /// <summary>Verifies a set-equivalent group still permits name and email correction.</summary>
    [Fact]
    public async Task SameRequirementSetInAnotherGroupAllowsDetailCorrection()
    {
        var (handler, candidate, coordinator) = GivenActiveBooking();

        var result = await handler.UpdateAsync(
            new UpdateCandidateCommand(
                coordinator,
                candidate.Id,
                "Amara N. Novak",
                "amara.novak@example.com",
                GroundTransport.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Amara N. Novak", candidate.Name);
        Assert.Equal("amara.novak@example.com", candidate.Email);
    }

    private static (SaveCandidateHandler Handler, Candidate Candidate, Guid Coordinator)
        GivenActiveBooking()
    {
        var coordinator = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(coordinator, [Role.Coordinator], null));
        var groups = new InMemoryEmployeeGroupRepository();
        groups.Items.AddRange([CabinCrew, GroundTransport, Pilots]);
        var candidates = new InMemoryCandidateRepository();
        var candidate = Candidate.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "amara@example.com",
            CabinCrew);
        candidates.Add(candidate);
        var bookings = new InMemoryBookingRepository();
        bookings.Add(NewBooking(candidate));

        return (
            new SaveCandidateHandler(
                candidates,
                groups,
                new InMemoryInviteRepository(),
                bookings,
                new StaffAccessAuthorizer(profiles),
                new RecordingAuditLogger(),
                new FakeUnitOfWork()),
            candidate,
            coordinator);
    }

    private static Booking NewBooking(Candidate candidate)
    {
        var slotId = Guid.NewGuid();
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            candidate.Id,
            "invite-token-hash",
            DateTimeOffset.UtcNow.AddDays(1),
            [slotId, Guid.NewGuid(), Guid.NewGuid()],
            candidate.RequiredAppointmentTypeIds,
            0);
        return Booking.Create(
            Guid.NewGuid(), invite, slotId, "manage-token-hash", DateTimeOffset.UtcNow);
    }
}
`````
