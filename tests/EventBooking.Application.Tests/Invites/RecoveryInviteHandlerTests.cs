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
        "https://booking.example.com", "recruitment@corp.com");

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
        var staleVersion = invite.TokenVersion;
        var remainingBefore = _events.Items
            .Select(eventItem => eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity)
            .ToList();

        var result = await CancelHandler().HandleAsync(
            new CancelRecoveryInviteCommand(Coordinator, attendee.Id, invite.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InviteStatus.Cancelled, invite.Status);
        Assert.NotEqual(staleVersion, invite.TokenVersion);
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
            Guid.NewGuid(),
            attendee.Id,
            _clock.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            0);
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
            Guid.NewGuid(),
            "Bo Vance",
            "b.vance@mail.com",
            _groups.Items.Single(g => g.Id == AttendeeGroupIds.Pilots),
            ProposalFixture.Now);
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
        new LegacyInviteIssuer(
            _invites, _groups, new EligibleEventFinder(_events, _events, _clock), _settings,
            new FakeTokenService(), EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
            _audit, _clock, Portal),
        new EligibleEventFinder(_events, _events, _clock),
        EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
        _unitOfWork);

    private CancelRecoveryInviteHandler CancelHandler() => new(
        _attendees, _roles, _invites, _bookings, _audit, _unitOfWork);

    private (Attendee Attendee, Booking Original, BookingAppointment Missed) SeedBookedAttendeeWithNoShow()
    {
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "a.novak@mail.com",
            _groups.Items.Single(g => g.Id == AttendeeGroupIds.Pilots),
            ProposalFixture.Now);
        attendee.MarkInvited(ProposalFixture.Now);
        attendee.MarkBooked(ProposalFixture.Now);
        _attendees.Add(attendee);

        var eventIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var initial = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            _clock.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            eventIds,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            0);
        var original = Booking.Create(
            Guid.NewGuid(), initial, eventIds[0], _clock.UtcNow);
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
            var proposal = ProposalFixture.Create(
                Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0), 240),
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
