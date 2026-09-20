# 00b — Vocabulary edits 84 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — tests/EventBooking.Application.Tests/Invites/RecoveryInviteHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":282,"oldPath":"tests/EventBooking.Application.Tests/Invites/RecoveryInviteHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Invites/RecoveryInviteHandlerTests.cs","beforeSha":"81f8da893ff841dd1303ff0161c14a6de998980454cf64b9298992b9282d1d24","afterSha":"7d5ce1827c505b269bbd700996a98d61e11602c7bb9cb57fdbcc1716448e2609","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
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

namespace EventBooking.Application.Tests.Invites;

public sealed class RecoveryInviteHandlerTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly Guid Admin = Guid.Parse("a0000009-0000-0000-0000-000000000009");
    private static readonly CandidatePortalOptions Portal = new(
        "https://booking.example.com", "Corporate HQ", "recruitment@corp.com");

    private readonly TransactionOperationLog _operations = new();
    private readonly InMemoryCandidateRepository _candidates;
    private readonly InMemoryInviteRepository _invites;
    private readonly InMemoryBookingRepository _bookings;
    private readonly InMemoryBookingAppointmentRepository _appointments;
    private readonly InMemoryEmployeeGroupRepository _groups = new();
    private readonly InMemoryConfirmedSlotRepository _slots = new();
    private readonly InMemorySystemSettingsRepository _settings = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly RecordingEmailSender _email = new();
    private readonly InMemoryEmailDeliveryRepository _deliveries = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork;
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    public RecoveryInviteHandlerTests()
    {
        _unitOfWork = new FakeUnitOfWork(_operations);
        _candidates = new InMemoryCandidateRepository(_operations);
        _invites = new InMemoryInviteRepository(_operations);
        _bookings = new InMemoryBookingRepository(_operations);
        _appointments = new InMemoryBookingAppointmentRepository(_bookings, _operations);

        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _roles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));

        var pilots = EmployeeGroup.Define(
            EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        _groups.Items.Add(pilots);
    }

    [Fact]
    public async Task ACoordinatorCanStartRecoveryForAMissedAppointment()
    {
        var (candidate, original, _) = SeedBookedCandidateWithNoShow();
        AddThreeSlots();

        var result = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, candidate.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([AppointmentTypeIds.DrugAndAlcoholTesting], result.Value.AppointmentTypeIds);

        var recovery = Assert.Single(
            _invites.Items, i => i.RecoveryOfBookingId == original.Id);
        Assert.Equal(InviteStatus.Pending, recovery.Status);
        Assert.Equal([AppointmentTypeIds.DrugAndAlcoholTesting], recovery.RequiredAppointmentTypeIds);
        Assert.Equal(Invite.RequiredOptionCount, recovery.OfferedSlotIds.Count);
        Assert.Equal(result.Value.InviteId, recovery.Id);

        Assert.Single(_email.Sent);
        Assert.True(_audit.Contains(AuditAction.RecoveryInviteCreated));
        Assert.Equal(CandidateStatus.Booked, candidate.Status);
        Assert.Equal(BookingStatus.Active, original.Status);
    }

    [Fact]
    public async Task StartingRecoveryTwiceReportsAlreadyPending()
    {
        var (candidate, _, _) = SeedBookedCandidateWithNoShow();
        AddThreeSlots();

        var first = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, candidate.Id), CancellationToken.None);
        Assert.True(first.IsSuccess);

        var second = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, candidate.Id), CancellationToken.None);

        Assert.True(second.IsFailure);
        Assert.Equal("recovery_already_pending", second.Error.Code);
        Assert.Single(_invites.Items, i => i.RecoveryOfBookingId.HasValue);
    }

    [Fact]
    public async Task WithoutNoShowsRecoveryIsNotAvailable()
    {
        var (candidate, _, _) = SeedBookedCandidateWithNoShow();
        foreach (var appointment in _appointments.Items)
        {
            appointment.TransitionTo(
                BookingAppointmentStatus.Expected, Coordinator, _clock.UtcNow, false, false);
        }

        AddThreeSlots();

        var result = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, candidate.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("recovery_not_available", result.Error.Code);
        Assert.DoesNotContain(_invites.Items, i => i.RecoveryOfBookingId.HasValue);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task WithoutSlotsTheRecoveryIsAwaitingAvailability()
    {
        var (candidate, _, _) = SeedBookedCandidateWithNoShow();

        var result = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, candidate.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Guid.Empty, result.Value.InviteId);
        Assert.Equal([AppointmentTypeIds.DrugAndAlcoholTesting], result.Value.AppointmentTypeIds);
        Assert.False(result.Value.EmailSent);
        Assert.DoesNotContain(_invites.Items, i => i.RecoveryOfBookingId.HasValue);
        Assert.Empty(_email.Sent);
        Assert.Equal(CandidateStatus.Booked, candidate.Status);
    }

    [Fact]
    public async Task AnAdminCannotStartRecovery()
    {
        var (candidate, _, _) = SeedBookedCandidateWithNoShow();
        AddThreeSlots();

        var result = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Admin, candidate.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.DoesNotContain(_invites.Items, i => i.RecoveryOfBookingId.HasValue);
    }

    [Fact]
    public async Task AnUnknownCandidateIsNotFound()
    {
        var result = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task ACoordinatorCanCancelAPendingRecoveryInvite()
    {
        var (candidate, _, _) = SeedBookedCandidateWithNoShow();
        AddThreeSlots();
        var started = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, candidate.Id), CancellationToken.None);
        Assert.True(started.IsSuccess);
        var invite = _invites.Items.Single(i => i.Id == started.Value.InviteId);
        var staleHash = invite.TokenHash;
        var remainingBefore = _slots.Items
            .Select(slot => slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity)
            .ToList();

        var result = await CancelHandler().HandleAsync(
            new CancelRecoveryInviteCommand(Coordinator, candidate.Id, invite.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InviteStatus.Cancelled, invite.Status);
        Assert.NotEqual(staleHash, invite.TokenHash);
        Assert.Null(await _invites.GetByTokenHashAsync(staleHash, CancellationToken.None));
        Assert.True(_audit.Contains(AuditAction.RecoveryInviteCancelled));
        Assert.Equal(CandidateStatus.Booked, candidate.Status);
        Assert.Equal(
            remainingBefore,
            _slots.Items
                .Select(slot => slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity)
                .ToList());
        Assert.Contains(
            _appointments.Items,
            a => a.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting
                && a.Status == BookingAppointmentStatus.NoShow);
        Assert.Contains(
            _appointments.Items,
            a => a.AppointmentTypeId == AppointmentTypeIds.UniformFitting
                && a.Status == BookingAppointmentStatus.Expected);
    }

    [Fact]
    public async Task CancellingTwiceReportsAStaleConflict()
    {
        var (candidate, _, _) = SeedBookedCandidateWithNoShow();
        AddThreeSlots();
        var started = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, candidate.Id), CancellationToken.None);
        var first = await CancelHandler().HandleAsync(
            new CancelRecoveryInviteCommand(Coordinator, candidate.Id, started.Value.InviteId),
            CancellationToken.None);
        Assert.True(first.IsSuccess);

        var second = await CancelHandler().HandleAsync(
            new CancelRecoveryInviteCommand(Coordinator, candidate.Id, started.Value.InviteId),
            CancellationToken.None);

        Assert.True(second.IsFailure);
        Assert.Equal("conflict", second.Error.Code);
    }

    [Fact]
    public async Task AnInitialInviteCannotBeCancelledAsRecovery()
    {
        var (candidate, _, _) = SeedBookedCandidateWithNoShow();
        var initial = Invite.CreateInitial(
            Guid.NewGuid(), candidate.Id, "hash-initial-pending",
            _clock.UtcNow.AddDays(4), [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting], 0);
        _invites.Add(initial);

        var result = await CancelHandler().HandleAsync(
            new CancelRecoveryInviteCommand(Coordinator, candidate.Id, initial.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(InviteStatus.Pending, initial.Status);
    }

    [Fact]
    public async Task AnAdminCannotCancelRecovery()
    {
        var (candidate, _, _) = SeedBookedCandidateWithNoShow();
        AddThreeSlots();
        var started = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, candidate.Id), CancellationToken.None);

        var result = await CancelHandler().HandleAsync(
            new CancelRecoveryInviteCommand(Admin, candidate.Id, started.Value.InviteId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(
            InviteStatus.Pending,
            _invites.Items.Single(i => i.Id == started.Value.InviteId).Status);
    }

    [Fact]
    public async Task CancellingForTheWrongCandidateIsRejected()
    {
        SeedBookedCandidateWithNoShow();
        AddThreeSlots();
        var candidate = _candidates.Items.Single();
        var started = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, candidate.Id), CancellationToken.None);
        var stranger = Candidate.Create(
            Guid.NewGuid(), "Bo Vance", "b.vance@mail.com",
            _groups.Items.Single(g => g.Id == EmployeeGroupIds.Pilots));
        _candidates.Add(stranger);

        var result = await CancelHandler().HandleAsync(
            new CancelRecoveryInviteCommand(Coordinator, stranger.Id, started.Value.InviteId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(
            InviteStatus.Pending,
            _invites.Items.Single(i => i.Id == started.Value.InviteId).Status);
    }

    /// <summary>
    /// Recovery issuance takes the candidate lifecycle lock before pending invites, the
    /// original booking, and the active recovery so a concurrent correction serializes first.
    /// </summary>
    [Fact]
    public async Task RecoveryIssuanceUsesTheCandidateLifecycleLockOrder()
    {
        var (candidate, _, _) = SeedBookedCandidateWithNoShow();
        AddThreeSlots();

        await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, candidate.Id), CancellationToken.None);

        Assert.Equal(
            [
                "transaction-begun",
                "candidate-locked",
                "pending-invites-locked",
                "original-booking-locked",
                "active-recovery-locked",
            ],
            _operations.Events.Take(5));
    }

    /// <summary>
    /// A no-show correction that commits between the preflight read and the lifecycle locks
    /// leaves the handler observing changed eligibility instead of issuing a stale invite.
    /// </summary>
    [Fact]
    public async Task ACorrectionRacingIssuanceReportsStateChanged()
    {
        var (candidate, _, missed) = SeedBookedCandidateWithNoShow();
        AddThreeSlots();
        var correcting = new CorrectingAppointmentRepository(_appointments, missed.Id);

        var result = await StartHandler(correcting).HandleAsync(
            new StartRecoveryCommand(Coordinator, candidate.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("recovery_state_changed", result.Error.Code);
        Assert.DoesNotContain(_invites.Items, i => i.RecoveryOfBookingId.HasValue);
        Assert.Empty(_email.Sent);
    }

    private StartRecoveryHandler StartHandler(
        IBookingAppointmentRepository? appointmentOverride = null) => new(
        _candidates,
        _roles,
        _invites,
        _bookings,
        appointmentOverride ?? _appointments,
        new InviteIssuer(
            _invites, _groups, new EligibleSlotFinder(_slots, _clock), _settings,
            new FakeTokenService(), EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
            _audit, _clock, Portal),
        new EligibleSlotFinder(_slots, _clock),
        EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
        _unitOfWork);

    private CancelRecoveryInviteHandler CancelHandler() => new(
        _candidates, _roles, _invites, _bookings, _audit, _unitOfWork);

    private (Candidate Candidate, Booking Original, BookingAppointment Missed) SeedBookedCandidateWithNoShow()
    {
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com",
            _groups.Items.Single(g => g.Id == EmployeeGroupIds.Pilots));
        candidate.MarkInvited();
        candidate.MarkBooked();
        _candidates.Add(candidate);

        var slotIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var initial = Invite.CreateInitial(
            Guid.NewGuid(), candidate.Id, "hash-initial",
            _clock.UtcNow.AddDays(4), slotIds,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting], 0);
        var original = Booking.Create(
            Guid.NewGuid(), initial, slotIds[0], "manage-original", _clock.UtcNow);
        initial.MarkUsed();
        _invites.Add(initial);
        _bookings.Add(original);

        var missed = BookingAppointment.Create(
            Guid.NewGuid(), original.Id, AppointmentTypeIds.DrugAndAlcoholTesting);
        missed.TransitionTo(
            BookingAppointmentStatus.NoShow, Coordinator, _clock.UtcNow, false, true);
        _appointments.Add(missed);
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), original.Id, AppointmentTypeIds.UniformFitting));

        return (candidate, original, missed);
    }

    private void AddThreeSlots()
    {
        foreach (var day in new[] { 10, 12, 14 })
        {
            var proposal = SlotProposal.Create(
                Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0)),
                Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            _slots.Add(ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal));
        }
    }

    /// <summary>
    /// Simulates a no-show correction committing between the handler's preflight journey read
    /// and its post-lock re-read by correcting the seeded attempt on the second read.
    /// </summary>
    private sealed class CorrectingAppointmentRepository(
        InMemoryBookingAppointmentRepository inner,
        Guid correctedAppointmentId) : IBookingAppointmentRepository
    {
        private int _reads;

        public void Add(BookingAppointment appointment) => inner.Add(appointment);

        public Task<BookingAppointmentLocator?> FindLocatorInScopeAsync(
            Guid id,
            Guid appointmentTypeId,
            CancellationToken cancellationToken) =>
            inner.FindLocatorInScopeAsync(id, appointmentTypeId, cancellationToken);

        public Task<BookingAppointment?> LockForUpdateAsync(
            Guid id,
            Guid appointmentTypeId,
            CancellationToken cancellationToken) =>
            inner.LockForUpdateAsync(id, appointmentTypeId, cancellationToken);

        public Task<IReadOnlyList<BookingAppointment>> ListForBookingAsync(
            Guid bookingId,
            CancellationToken cancellationToken) =>
            inner.ListForBookingAsync(bookingId, cancellationToken);

        public Task<IReadOnlyList<BookingAppointment>> LockForBookingAsync(
            Guid bookingId,
            CancellationToken cancellationToken) =>
            inner.LockForBookingAsync(bookingId, cancellationToken);

        public Task<IReadOnlyList<BookingAppointment>> ListForBookingsAsync(
            IReadOnlyCollection<Guid> bookingIds,
            CancellationToken cancellationToken)
        {
            if (++_reads == 2)
            {
                inner.Items
                    .Single(a => a.Id == correctedAppointmentId)
                    .TransitionTo(BookingAppointmentStatus.Expected, Coordinator, DateTimeOffset.UtcNow, false, false);
            }

            return inner.ListForBookingsAsync(bookingIds, cancellationToken);
        }
    }
}
`````

## after — tests/EventBooking.Application.Tests/Invites/RecoveryInviteHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":282,"oldPath":"tests/EventBooking.Application.Tests/Invites/RecoveryInviteHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Invites/RecoveryInviteHandlerTests.cs","beforeSha":"81f8da893ff841dd1303ff0161c14a6de998980454cf64b9298992b9282d1d24","afterSha":"7d5ce1827c505b269bbd700996a98d61e11602c7bb9cb57fdbcc1716448e2609","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Invites;

public sealed class RecoveryInviteHandlerTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly Guid Admin = Guid.Parse("a0000009-0000-0000-0000-000000000009");
    private static readonly AttendeePortalOptions Portal = new(
        "https://booking.example.com", "Corporate HQ", "recruitment@corp.com");

    private readonly TransactionOperationLog _operations = new();
    private readonly InMemoryAttendeeRepository _attendees;
    private readonly InMemoryInviteRepository _invites;
    private readonly InMemoryBookingRepository _bookings;
    private readonly InMemoryBookingAppointmentRepository _appointments;
    private readonly InMemoryAttendeeGroupRepository _groups = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemorySystemSettingsRepository _settings = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly RecordingEmailSender _email = new();
    private readonly InMemoryEmailDeliveryRepository _deliveries = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork;
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    public RecoveryInviteHandlerTests()
    {
        _unitOfWork = new FakeUnitOfWork(_operations);
        _attendees = new InMemoryAttendeeRepository(_operations);
        _invites = new InMemoryInviteRepository(_operations);
        _bookings = new InMemoryBookingRepository(_operations);
        _appointments = new InMemoryBookingAppointmentRepository(_bookings, _operations);

        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _roles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));

        var pilots = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        _groups.Items.Add(pilots);
    }

    [Fact]
    public async Task ACoordinatorCanStartRecoveryForAMissedAppointment()
    {
        var (attendee, original, _) = SeedBookedAttendeeWithNoShow();
        AddThreeEvents();

        var result = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, attendee.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([AppointmentTypeIds.DrugAndAlcoholTesting], result.Value.AppointmentTypeIds);

        var recovery = Assert.Single(
            _invites.Items, i => i.RecoveryOfBookingId == original.Id);
        Assert.Equal(InviteStatus.Pending, recovery.Status);
        Assert.Equal([AppointmentTypeIds.DrugAndAlcoholTesting], recovery.RequiredAppointmentTypeIds);
        Assert.Equal(Invite.RequiredOptionCount, recovery.OfferedEventIds.Count);
        Assert.Equal(result.Value.InviteId, recovery.Id);

        Assert.Single(_email.Sent);
        Assert.True(_audit.Contains(AuditAction.RecoveryInviteCreated));
        Assert.Equal(AttendeeStatus.Booked, attendee.Status);
        Assert.Equal(BookingStatus.Active, original.Status);
    }

    [Fact]
    public async Task StartingRecoveryTwiceReportsAlreadyPending()
    {
        var (attendee, _, _) = SeedBookedAttendeeWithNoShow();
        AddThreeEvents();

        var first = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, attendee.Id), CancellationToken.None);
        Assert.True(first.IsSuccess);

        var second = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, attendee.Id), CancellationToken.None);

        Assert.True(second.IsFailure);
        Assert.Equal("recovery_already_pending", second.Error.Code);
        Assert.Single(_invites.Items, i => i.RecoveryOfBookingId.HasValue);
    }

    [Fact]
    public async Task WithoutNoShowsRecoveryIsNotAvailable()
    {
        var (attendee, _, _) = SeedBookedAttendeeWithNoShow();
        foreach (var appointment in _appointments.Items)
        {
            appointment.TransitionTo(
                BookingAppointmentStatus.Expected, Coordinator, _clock.UtcNow, false, false);
        }

        AddThreeEvents();

        var result = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, attendee.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("recovery_not_available", result.Error.Code);
        Assert.DoesNotContain(_invites.Items, i => i.RecoveryOfBookingId.HasValue);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task WithoutEventsTheRecoveryIsAwaitingAvailability()
    {
        var (attendee, _, _) = SeedBookedAttendeeWithNoShow();

        var result = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, attendee.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Guid.Empty, result.Value.InviteId);
        Assert.Equal([AppointmentTypeIds.DrugAndAlcoholTesting], result.Value.AppointmentTypeIds);
        Assert.False(result.Value.EmailSent);
        Assert.DoesNotContain(_invites.Items, i => i.RecoveryOfBookingId.HasValue);
        Assert.Empty(_email.Sent);
        Assert.Equal(AttendeeStatus.Booked, attendee.Status);
    }

    [Fact]
    public async Task AnAdminCannotStartRecovery()
    {
        var (attendee, _, _) = SeedBookedAttendeeWithNoShow();
        AddThreeEvents();

        var result = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Admin, attendee.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.DoesNotContain(_invites.Items, i => i.RecoveryOfBookingId.HasValue);
    }

    [Fact]
    public async Task AnUnknownAttendeeIsNotFound()
    {
        var result = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task ACoordinatorCanCancelAPendingRecoveryInvite()
    {
        var (attendee, _, _) = SeedBookedAttendeeWithNoShow();
        AddThreeEvents();
        var started = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, attendee.Id), CancellationToken.None);
        Assert.True(started.IsSuccess);
        var invite = _invites.Items.Single(i => i.Id == started.Value.InviteId);
        var staleHash = invite.TokenHash;
        var remainingBefore = _events.Items
            .Select(eventItem => eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity)
            .ToList();

        var result = await CancelHandler().HandleAsync(
            new CancelRecoveryInviteCommand(Coordinator, attendee.Id, invite.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InviteStatus.Cancelled, invite.Status);
        Assert.NotEqual(staleHash, invite.TokenHash);
        Assert.Null(await _invites.GetByTokenHashAsync(staleHash, CancellationToken.None));
        Assert.True(_audit.Contains(AuditAction.RecoveryInviteCancelled));
        Assert.Equal(AttendeeStatus.Booked, attendee.Status);
        Assert.Equal(
            remainingBefore,
            _events.Items
                .Select(eventItem => eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity)
                .ToList());
        Assert.Contains(
            _appointments.Items,
            a => a.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting
                && a.Status == BookingAppointmentStatus.NoShow);
        Assert.Contains(
            _appointments.Items,
            a => a.AppointmentTypeId == AppointmentTypeIds.UniformFitting
                && a.Status == BookingAppointmentStatus.Expected);
    }

    [Fact]
    public async Task CancellingTwiceReportsAStaleConflict()
    {
        var (attendee, _, _) = SeedBookedAttendeeWithNoShow();
        AddThreeEvents();
        var started = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, attendee.Id), CancellationToken.None);
        var first = await CancelHandler().HandleAsync(
            new CancelRecoveryInviteCommand(Coordinator, attendee.Id, started.Value.InviteId),
            CancellationToken.None);
        Assert.True(first.IsSuccess);

        var second = await CancelHandler().HandleAsync(
            new CancelRecoveryInviteCommand(Coordinator, attendee.Id, started.Value.InviteId),
            CancellationToken.None);

        Assert.True(second.IsFailure);
        Assert.Equal("conflict", second.Error.Code);
    }

    [Fact]
    public async Task AnInitialInviteCannotBeCancelledAsRecovery()
    {
        var (attendee, _, _) = SeedBookedAttendeeWithNoShow();
        var initial = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, "hash-initial-pending",
            _clock.UtcNow.AddDays(4), [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting], 0);
        _invites.Add(initial);

        var result = await CancelHandler().HandleAsync(
            new CancelRecoveryInviteCommand(Coordinator, attendee.Id, initial.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(InviteStatus.Pending, initial.Status);
    }

    [Fact]
    public async Task AnAdminCannotCancelRecovery()
    {
        var (attendee, _, _) = SeedBookedAttendeeWithNoShow();
        AddThreeEvents();
        var started = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, attendee.Id), CancellationToken.None);

        var result = await CancelHandler().HandleAsync(
            new CancelRecoveryInviteCommand(Admin, attendee.Id, started.Value.InviteId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(
            InviteStatus.Pending,
            _invites.Items.Single(i => i.Id == started.Value.InviteId).Status);
    }

    [Fact]
    public async Task CancellingForTheWrongAttendeeIsRejected()
    {
        SeedBookedAttendeeWithNoShow();
        AddThreeEvents();
        var attendee = _attendees.Items.Single();
        var started = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, attendee.Id), CancellationToken.None);
        var stranger = Attendee.Create(
            Guid.NewGuid(), "Bo Vance", "b.vance@mail.com",
            _groups.Items.Single(g => g.Id == AttendeeGroupIds.Pilots));
        _attendees.Add(stranger);

        var result = await CancelHandler().HandleAsync(
            new CancelRecoveryInviteCommand(Coordinator, stranger.Id, started.Value.InviteId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(
            InviteStatus.Pending,
            _invites.Items.Single(i => i.Id == started.Value.InviteId).Status);
    }

    /// <summary>
    /// Recovery issuance takes the attendee lifecycle lock before pending invites, the
    /// original booking, and the active recovery so a concurrent correction serializes first.
    /// </summary>
    [Fact]
    public async Task RecoveryIssuanceUsesTheAttendeeLifecycleLockOrder()
    {
        var (attendee, _, _) = SeedBookedAttendeeWithNoShow();
        AddThreeEvents();

        await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, attendee.Id), CancellationToken.None);

        Assert.Equal(
            [
                "transaction-begun",
                "attendee-locked",
                "pending-invites-locked",
                "original-booking-locked",
                "active-recovery-locked",
            ],
            _operations.Events.Take(5));
    }

    /// <summary>
    /// A no-show correction that commits between the preflight read and the lifecycle locks
    /// leaves the handler observing changed eligibility instead of issuing a stale invite.
    /// </summary>
    [Fact]
    public async Task ACorrectionRacingIssuanceReportsStateChanged()
    {
        var (attendee, _, missed) = SeedBookedAttendeeWithNoShow();
        AddThreeEvents();
        var correcting = new CorrectingAppointmentRepository(_appointments, missed.Id);

        var result = await StartHandler(correcting).HandleAsync(
            new StartRecoveryCommand(Coordinator, attendee.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("recovery_state_changed", result.Error.Code);
        Assert.DoesNotContain(_invites.Items, i => i.RecoveryOfBookingId.HasValue);
        Assert.Empty(_email.Sent);
    }

    private StartRecoveryHandler StartHandler(
        IBookingAppointmentRepository? appointmentOverride = null) => new(
        _attendees,
        _roles,
        _invites,
        _bookings,
        appointmentOverride ?? _appointments,
        new InviteIssuer(
            _invites, _groups, new EligibleEventFinder(_events, _clock), _settings,
            new FakeTokenService(), EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
            _audit, _clock, Portal),
        new EligibleEventFinder(_events, _clock),
        EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
        _unitOfWork);

    private CancelRecoveryInviteHandler CancelHandler() => new(
        _attendees, _roles, _invites, _bookings, _audit, _unitOfWork);

    private (Attendee Attendee, Booking Original, BookingAppointment Missed) SeedBookedAttendeeWithNoShow()
    {
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com",
            _groups.Items.Single(g => g.Id == AttendeeGroupIds.Pilots));
        attendee.MarkInvited();
        attendee.MarkBooked();
        _attendees.Add(attendee);

        var eventIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var initial = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, "hash-initial",
            _clock.UtcNow.AddDays(4), eventIds,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting], 0);
        var original = Booking.Create(
            Guid.NewGuid(), initial, eventIds[0], "manage-original", _clock.UtcNow);
        initial.MarkUsed();
        _invites.Add(initial);
        _bookings.Add(original);

        var missed = BookingAppointment.Create(
            Guid.NewGuid(), original.Id, AppointmentTypeIds.DrugAndAlcoholTesting);
        missed.TransitionTo(
            BookingAppointmentStatus.NoShow, Coordinator, _clock.UtcNow, false, true);
        _appointments.Add(missed);
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), original.Id, AppointmentTypeIds.UniformFitting));

        return (attendee, original, missed);
    }

    private void AddThreeEvents()
    {
        foreach (var day in new[] { 10, 12, 14 })
        {
            var proposal = EventProposal.Create(
                Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0)),
                Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            _events.Add(Event.CreateFrom(Guid.NewGuid(), proposal));
        }
    }

    /// <summary>
    /// Simulates a no-show correction committing between the handler's preflight journey read
    /// and its post-lock re-read by correcting the seeded attempt on the second read.
    /// </summary>
    private sealed class CorrectingAppointmentRepository(
        InMemoryBookingAppointmentRepository inner,
        Guid correctedAppointmentId) : IBookingAppointmentRepository
    {
        private int _reads;

        public void Add(BookingAppointment appointment) => inner.Add(appointment);

        public Task<BookingAppointmentLocator?> FindLocatorInScopeAsync(
            Guid id,
            Guid appointmentTypeId,
            CancellationToken cancellationToken) =>
            inner.FindLocatorInScopeAsync(id, appointmentTypeId, cancellationToken);

        public Task<BookingAppointment?> LockForUpdateAsync(
            Guid id,
            Guid appointmentTypeId,
            CancellationToken cancellationToken) =>
            inner.LockForUpdateAsync(id, appointmentTypeId, cancellationToken);

        public Task<IReadOnlyList<BookingAppointment>> ListForBookingAsync(
            Guid bookingId,
            CancellationToken cancellationToken) =>
            inner.ListForBookingAsync(bookingId, cancellationToken);

        public Task<IReadOnlyList<BookingAppointment>> LockForBookingAsync(
            Guid bookingId,
            CancellationToken cancellationToken) =>
            inner.LockForBookingAsync(bookingId, cancellationToken);

        public Task<IReadOnlyList<BookingAppointment>> ListForBookingsAsync(
            IReadOnlyCollection<Guid> bookingIds,
            CancellationToken cancellationToken)
        {
            if (++_reads == 2)
            {
                inner.Items
                    .Single(a => a.Id == correctedAppointmentId)
                    .TransitionTo(BookingAppointmentStatus.Expected, Coordinator, DateTimeOffset.UtcNow, false, false);
            }

            return inner.ListForBookingsAsync(bookingIds, cancellationToken);
        }
    }
}
`````

## before — tests/EventBooking.Application.Tests/Invites/TriggerInviteHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":283,"oldPath":"tests/EventBooking.Application.Tests/Invites/TriggerInviteHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Invites/TriggerInviteHandlerTests.cs","beforeSha":"8ca71ef6bb5868325b56081a7324a7a33ecc5f61881975de532524ff2c623a26","afterSha":"d211d5f37b0ce2a6a09128f532c24c6369c4f18f5f4d015b70ea1f3d4b091b76","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Invites;

public class TriggerInviteHandlerTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly Guid Manager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly CandidatePortalOptions Portal = new(
        "https://booking.example.com", "Corporate HQ", "recruitment@corp.com");

    private readonly InMemoryCandidateRepository _candidates = new();
    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryEmployeeGroupRepository _groups = new();
    private readonly InMemoryConfirmedSlotRepository _slots = new();
    private readonly InMemorySystemSettingsRepository _settings = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly RecordingEmailSender _email = new();
    private readonly InMemoryEmailDeliveryRepository _deliveries = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly Candidate _candidate;

    private TriggerInviteHandler Handler => new(
        _candidates,
        _roles,
        new InviteIssuer(
            _invites, _groups, new EligibleSlotFinder(_slots, _clock), _settings,
            new FakeTokenService(), EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
            _audit, _clock, Portal),
        EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
        _unitOfWork);

    public TriggerInviteHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _roles.Add(StaffAccessProfile.Create(
            Manager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));

        var pilots = EmployeeGroup.Define(
            EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        _groups.Items.Add(pilots);
        _candidate = Candidate.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", pilots);
        _candidates.Add(_candidate);
    }

    /// <summary>Verifies invite issuance persists the business change and delivery result once each.</summary>
    [Fact]
    public async Task ACoordinatorCanTriggerAnInvite()
    {
        AddThreeSlots();

        var result = await Handler.HandleAsync(
            new TriggerInviteCommand(Coordinator, _candidate.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Invited);
        Assert.Single(_invites.Items);
        Assert.Single(_email.Sent);
        Assert.Equal(2, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task TheSystemCanTriggerWithoutAStaffIdentity()
    {
        AddThreeSlots();

        var result = await Handler.HandleAsync(
            new TriggerInviteCommand(null, _candidate.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Invited);
    }

    [Fact]
    public async Task AManagerCannotTriggerAnInvite()
    {
        AddThreeSlots();

        var result = await Handler.HandleAsync(
            new TriggerInviteCommand(Manager, _candidate.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Empty(_invites.Items);
    }

    [Fact]
    public async Task WithoutEnoughSlotsTheCandidateIsFlaggedAndStillSaved()
    {
        var result = await Handler.HandleAsync(
            new TriggerInviteCommand(Coordinator, _candidate.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Invited);
        Assert.Equal(CandidateStatus.AwaitingAvailability, _candidate.Status);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ReInvitingACandidateWhoNeverRespondedResetsTheRetryCount()
    {
        AddThreeSlots();
        _candidate.MarkInvited();
        _candidate.MarkNoResponse();

        var result = await Handler.HandleAsync(
            new TriggerInviteCommand(Coordinator, _candidate.Id), CancellationToken.None);

        Assert.True(result.Value.Invited);
        Assert.Equal(0, _invites.Items.Single().RetryCount);
        Assert.Equal(CandidateStatus.Invited, _candidate.Status);
    }

    [Fact]
    public async Task ABookedCandidateCannotBeReInvited()
    {
        AddThreeSlots();
        _candidate.MarkInvited();
        _candidate.MarkBooked();

        var result = await Handler.HandleAsync(
            new TriggerInviteCommand(Coordinator, _candidate.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("This candidate is already booked.", result.Error.Message);
    }

    [Fact]
    public async Task AnUnknownCandidateIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new TriggerInviteCommand(Coordinator, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    private void AddThreeSlots()
    {
        foreach (var day in new[] { 10, 12, 14 })
        {
            var proposal = SlotProposal.Create(
                Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0)),
                Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            _slots.Add(ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal));
        }
    }
}
`````

## after — tests/EventBooking.Application.Tests/Invites/TriggerInviteHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":283,"oldPath":"tests/EventBooking.Application.Tests/Invites/TriggerInviteHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Invites/TriggerInviteHandlerTests.cs","beforeSha":"8ca71ef6bb5868325b56081a7324a7a33ecc5f61881975de532524ff2c623a26","afterSha":"d211d5f37b0ce2a6a09128f532c24c6369c4f18f5f4d015b70ea1f3d4b091b76","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Invites;

public class TriggerInviteHandlerTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly Guid Manager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly AttendeePortalOptions Portal = new(
        "https://booking.example.com", "Corporate HQ", "recruitment@corp.com");

    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryAttendeeGroupRepository _groups = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemorySystemSettingsRepository _settings = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly RecordingEmailSender _email = new();
    private readonly InMemoryEmailDeliveryRepository _deliveries = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly Attendee _attendee;

    private TriggerInviteHandler Handler => new(
        _attendees,
        _roles,
        new InviteIssuer(
            _invites, _groups, new EligibleEventFinder(_events, _clock), _settings,
            new FakeTokenService(), EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
            _audit, _clock, Portal),
        EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
        _unitOfWork);

    public TriggerInviteHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _roles.Add(StaffAccessProfile.Create(
            Manager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));

        var pilots = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        _groups.Items.Add(pilots);
        _attendee = Attendee.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", pilots);
        _attendees.Add(_attendee);
    }

    /// <summary>Verifies invite issuance persists the business change and delivery result once each.</summary>
    [Fact]
    public async Task ACoordinatorCanTriggerAnInvite()
    {
        AddThreeEvents();

        var result = await Handler.HandleAsync(
            new TriggerInviteCommand(Coordinator, _attendee.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Invited);
        Assert.Single(_invites.Items);
        Assert.Single(_email.Sent);
        Assert.Equal(2, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task TheSystemCanTriggerWithoutAStaffIdentity()
    {
        AddThreeEvents();

        var result = await Handler.HandleAsync(
            new TriggerInviteCommand(null, _attendee.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Invited);
    }

    [Fact]
    public async Task AManagerCannotTriggerAnInvite()
    {
        AddThreeEvents();

        var result = await Handler.HandleAsync(
            new TriggerInviteCommand(Manager, _attendee.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Empty(_invites.Items);
    }

    [Fact]
    public async Task WithoutEnoughEventsTheAttendeeIsFlaggedAndStillSaved()
    {
        var result = await Handler.HandleAsync(
            new TriggerInviteCommand(Coordinator, _attendee.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Invited);
        Assert.Equal(AttendeeStatus.AwaitingAvailability, _attendee.Status);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ReInvitingAAttendeeWhoNeverRespondedResetsTheRetryCount()
    {
        AddThreeEvents();
        _attendee.MarkInvited();
        _attendee.MarkNoResponse();

        var result = await Handler.HandleAsync(
            new TriggerInviteCommand(Coordinator, _attendee.Id), CancellationToken.None);

        Assert.True(result.Value.Invited);
        Assert.Equal(0, _invites.Items.Single().RetryCount);
        Assert.Equal(AttendeeStatus.Invited, _attendee.Status);
    }

    [Fact]
    public async Task ABookedAttendeeCannotBeReInvited()
    {
        AddThreeEvents();
        _attendee.MarkInvited();
        _attendee.MarkBooked();

        var result = await Handler.HandleAsync(
            new TriggerInviteCommand(Coordinator, _attendee.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("This attendee is already booked.", result.Error.Message);
    }

    [Fact]
    public async Task AnUnknownAttendeeIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new TriggerInviteCommand(Coordinator, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    private void AddThreeEvents()
    {
        foreach (var day in new[] { 10, 12, 14 })
        {
            var proposal = EventProposal.Create(
                Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0)),
                Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            _events.Add(Event.CreateFrom(Guid.NewGuid(), proposal));
        }
    }
}
`````

## before — tests/EventBooking.Application.Tests/Notifications/CandidateEmailComposerTests.cs — 1/1

<!-- vocabulary-file: {"id":284,"oldPath":"tests/EventBooking.Application.Tests/Notifications/CandidateEmailComposerTests.cs","newPath":"tests/EventBooking.Application.Tests/Notifications/AttendeeEmailComposerTests.cs","beforeSha":"7c15daccb0d4800b3fb97da110ec109a4d5250e724de43c6a142200ad6860110","afterSha":"4b64643236a00230585d46e5a185e5ce604356b74051c221932e4f8bf19e0039","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Notifications;

public class CandidateEmailComposerTests
{
    private static readonly CandidatePortalOptions Portal = new(
        "https://booking.example.com", "Corporate HQ, 1 Example Street", "recruitment@corp.com");

    private static readonly Candidate Amara = Candidate.Create(
        Guid.NewGuid(), "Amara Novak", "a.novak@mail.com",
        EmployeeGroup.Define(
            EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));

    [Fact]
    public void AWindowIsFormattedForAHumanReader()
    {
        var window = new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0));

        Assert.Equal("Thursday 10 Sep 2026, 09:00-13:00", CandidateEmailComposer.FormatWindow(window));
    }

    [Fact]
    public void TheInviteNamesTheCandidateTheirTypesAndAllThreeOptions()
    {
        var options = new[] { SlotOn(10, 9), SlotOn(11, 13), SlotOn(13, 9) };

        var message = CandidateEmailComposer.Invite(
            Amara, Amara.RequiredAppointmentTypeIds, options, "https://booking.example.com/book/abc",
            isReinvite: false, isRecovery: false);

        Assert.Equal(Amara.Id, message.CandidateId);
        Assert.Equal("a.novak@mail.com", message.ToAddress);
        Assert.Equal("Amara Novak", message.ToName);
        Assert.Equal(EmailTemplate.CandidateInvite, message.Template);
        Assert.Equal("Choose a time for your appointments", message.Subject);

        Assert.Contains("Hi Amara Novak", message.TextBody);
        Assert.Contains("Drug & Alcohol Testing", message.TextBody);
        Assert.Contains("Uniform Fitting", message.TextBody);
        Assert.DoesNotContain("Medical Check-up", message.TextBody);
        Assert.Contains("Thursday 10 Sep 2026, 09:00-13:00", message.TextBody);
        Assert.Contains("Friday 11 Sep 2026, 13:00-17:00", message.TextBody);
        Assert.Contains("Sunday 13 Sep 2026, 09:00-13:00", message.TextBody);
        Assert.Contains("https://booking.example.com/book/abc", message.TextBody);
        Assert.Contains("https://booking.example.com/book/abc", message.HtmlBody);
    }

    [Fact]
    public void TheInviteNeverLeaksCapacityNumbers()
    {
        var slot = SlotOn(10, 9);

        var message = CandidateEmailComposer.Invite(
            Amara, Amara.RequiredAppointmentTypeIds, [slot, SlotOn(11, 13), SlotOn(13, 9)],
            "https://x/book/abc", isReinvite: false, isRecovery: false);

        Assert.DoesNotContain("remaining", message.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("capacity", message.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("10", message.Subject);
    }

    [Fact]
    public void AReInviteUsesItsOwnTemplateAndSubject()
    {
        var message = CandidateEmailComposer.Invite(
            Amara, Amara.RequiredAppointmentTypeIds, [SlotOn(10, 9), SlotOn(11, 13), SlotOn(13, 9)],
            "https://x/book/abc", isReinvite: true, isRecovery: false);

        Assert.Equal(EmailTemplate.CandidateReinvite, message.Template);
        Assert.Equal("Reminder: choose a time for your appointments", message.Subject);
        Assert.Contains("We have not heard back", message.TextBody);
    }

    [Fact]
    public void TheConfirmationCarriesTheChosenTimeTheAddressAndTheManageLink()
    {
        var message = CandidateEmailComposer.BookingConfirmation(
            Amara, Amara.RequiredAppointmentTypeIds, SlotOn(11, 13),
            "https://booking.example.com/manage/xyz", Portal);

        Assert.Equal(EmailTemplate.BookingConfirmation, message.Template);
        Assert.Equal("Your appointment is confirmed", message.Subject);
        Assert.Contains("Friday 11 Sep 2026, 13:00-17:00", message.TextBody);
        Assert.Contains("Corporate HQ, 1 Example Street", message.TextBody);
        Assert.Contains("https://booking.example.com/manage/xyz", message.TextBody);
        Assert.Contains("recruitment@corp.com", message.TextBody);
    }

    [Fact]
    public void TheCancellationApologisesAndPromisesANewInvite()
    {
        var message = CandidateEmailComposer.SlotCancelled(Amara, Amara.RequiredAppointmentTypeIds, SlotOn(11, 13), replacementInviteSent: true);

        Assert.Equal(EmailTemplate.SlotCancelledRebookingNeeded, message.Template);
        Assert.Equal("Your appointment time has been cancelled", message.Subject);
        Assert.Contains("Friday 11 Sep 2026, 13:00-17:00", message.TextBody);
        Assert.Contains("new invitation", message.TextBody);
    }

    /// <summary>Cancellation wording stays neutral until replacement delivery succeeds.</summary>
    [Fact]
    public void TheCancellationDoesNotPromiseAUnsentReplacement()
    {
        var message = CandidateEmailComposer.SlotCancelled(Amara, Amara.RequiredAppointmentTypeIds, SlotOn(11, 13));

        Assert.Contains("recruitment team will contact you", message.TextBody);
        Assert.DoesNotContain("on its way", message.TextBody);
    }

    [Fact]
    public void EveryMessageHasBothATextAndAnHtmlBody()
    {
        var messages = new[]
        {
            CandidateEmailComposer.Invite(
                Amara, Amara.RequiredAppointmentTypeIds, [SlotOn(10, 9), SlotOn(11, 13), SlotOn(13, 9)],
                "https://x/b", false, false),
            CandidateEmailComposer.BookingConfirmation(
                Amara, Amara.RequiredAppointmentTypeIds, SlotOn(11, 13), "https://x/m", Portal),
            CandidateEmailComposer.SlotCancelled(Amara, Amara.RequiredAppointmentTypeIds, SlotOn(11, 13)),
        };

        Assert.All(messages, m => Assert.False(string.IsNullOrWhiteSpace(m.TextBody)));
        Assert.All(messages, m => Assert.Contains("<html", m.HtmlBody, StringComparison.OrdinalIgnoreCase));
    }

    private static ConfirmedSlot SlotOn(int day, int hour)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, day), new TimeOnly(hour, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        return ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);
    }
}
`````
