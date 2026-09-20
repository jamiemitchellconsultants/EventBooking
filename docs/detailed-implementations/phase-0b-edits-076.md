# 00b — Vocabulary edits 76 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — tests/EventBooking.Application.Tests/Bookings/BookingSnapshotCancellationTests.cs — 1/1

<!-- vocabulary-file: {"id":250,"oldPath":"tests/EventBooking.Application.Tests/Bookings/BookingSnapshotCancellationTests.cs","newPath":"tests/EventBooking.Application.Tests/Bookings/BookingSnapshotCancellationTests.cs","beforeSha":"c30eaffe860c8a2d1e358df212e4ce3fadf2f2ed0243ab8ae0f75d1c821fe861","afterSha":"25d9cb041f22526436d84f76310cf4f72aed46d21b941938655b75f2153a5968","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies cancellation returns capacity only for the addressed Booking snapshot.</summary>
public sealed class BookingSnapshotCancellationTests
{
    /// <summary>A one-type Booking returns only that type even when Candidate now has three.</summary>
    [Fact]
    public async Task CancellationUsesBookingAppointmentsInsteadOfCandidateRequirements()
    {
        var slot = ConfirmedSlot.CreateImported(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 10, 10), new TimeOnly(9, 0)),
            new Dictionary<Guid, int>
            {
                [AppointmentTypeIds.DrugAndAlcoholTesting] = 5,
                [AppointmentTypeIds.MedicalCheckUp] = 5,
                [AppointmentTypeIds.UniformFitting] = 5,
            });
        slot.CapacityFor(AppointmentTypeIds.MedicalCheckUp).Decrement();
        var group = EmployeeGroup.Define(
            EmployeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting]);
        var candidate = Candidate.Create(Guid.NewGuid(), "Amara", "amara@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidate.Id, "hash", DateTimeOffset.UtcNow.AddDays(1),
            [slot.Id, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        var booking = Booking.Create(Guid.NewGuid(), invite, slot.Id, "manage", DateTimeOffset.UtcNow);
        var appointments = new InMemoryBookingAppointmentRepository(new InMemoryBookingRepository());
        appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.MedicalCheckUp));
        var slots = new InMemoryConfirmedSlotRepository();
        slots.Items.Add(slot);
        var capacities = new InMemorySlotCapacityRepository(slots);
        var canceller = new BookingCanceller(
            appointments, capacities, new RecordingAuditLogger());

        var result = await canceller.CancelLockedAsync(
            booking, slot, EventBooking.Domain.Audit.ActorType.System, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([AppointmentTypeIds.MedicalCheckUp], result.Value);
        Assert.Equal(5, slot.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(5, slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(5, slot.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
    }

    /// <summary>A Booking addressing a dropped capacity row fails with the conflict code.</summary>
    [Fact]
    public async Task CancellationForDroppedTypeFailsLikeFullCapacity()
    {
        var slot = ConfirmedSlot.CreateImported(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 10, 10), new TimeOnly(9, 0)),
            new Dictionary<Guid, int>
            {
                [AppointmentTypeIds.DrugAndAlcoholTesting] = 5,
                [AppointmentTypeIds.MedicalCheckUp] = 5,
                [AppointmentTypeIds.UniformFitting] = 5,
            });
        slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        slot.CapacityFor(AppointmentTypeIds.MedicalCheckUp).Decrement();
        var group = EmployeeGroup.Define(
            EmployeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT", "Ground Ops", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        var candidate = Candidate.Create(Guid.NewGuid(), "Amara", "amara@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidate.Id, "hash", DateTimeOffset.UtcNow.AddDays(1),
            [slot.Id, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp], 0);
        var booking = Booking.Create(Guid.NewGuid(), invite, slot.Id, "manage", DateTimeOffset.UtcNow);
        var bookings = new InMemoryBookingRepository();
        bookings.Add(booking);
        var appointments = new InMemoryBookingAppointmentRepository(bookings);
        appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting));
        appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.MedicalCheckUp));
        var capacities = new RowDroppingCapacityRepository(
            slot, AppointmentTypeIds.MedicalCheckUp);
        var canceller = new BookingCanceller(
            appointments, capacities, new RecordingAuditLogger());

        var result = await canceller.CancelLockedAsync(
            booking, slot, ActorType.System, null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(BookingStatus.Active, booking.Status);
        Assert.Equal(4, slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(4, slot.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
    }

    /// <summary>Simulates the capacity store losing one row the aggregate still references.</summary>
    private sealed class RowDroppingCapacityRepository(ConfirmedSlot slot, Guid droppedTypeId)
        : EventBooking.Application.Abstractions.ISlotCapacityRepository
    {
        public Task<IReadOnlyList<SlotCapacity>> LockForUpdateAsync(
            Guid confirmedSlotId,
            IReadOnlyCollection<Guid> appointmentTypeIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SlotCapacity>>(
                appointmentTypeIds
                    .Where(id => id != droppedTypeId)
                    .OrderBy(id => id)
                    .Select(slot.CapacityFor)
                    .ToList());
    }
}
`````

## after — tests/EventBooking.Application.Tests/Bookings/BookingSnapshotCancellationTests.cs — 1/1

<!-- vocabulary-file: {"id":250,"oldPath":"tests/EventBooking.Application.Tests/Bookings/BookingSnapshotCancellationTests.cs","newPath":"tests/EventBooking.Application.Tests/Bookings/BookingSnapshotCancellationTests.cs","beforeSha":"c30eaffe860c8a2d1e358df212e4ce3fadf2f2ed0243ab8ae0f75d1c821fe861","afterSha":"25d9cb041f22526436d84f76310cf4f72aed46d21b941938655b75f2153a5968","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies cancellation returns capacity only for the addressed Booking snapshot.</summary>
public sealed class BookingSnapshotCancellationTests
{
    /// <summary>A one-type Booking returns only that type even when Attendee now has three.</summary>
    [Fact]
    public async Task CancellationUsesBookingAppointmentsInsteadOfAttendeeRequirements()
    {
        var eventItem = Event.CreateImported(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 10, 10), new TimeOnly(9, 0)),
            new Dictionary<Guid, int>
            {
                [AppointmentTypeIds.DrugAndAlcoholTesting] = 5,
                [AppointmentTypeIds.MedicalCheckUp] = 5,
                [AppointmentTypeIds.UniformFitting] = 5,
            });
        eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).Decrement();
        var group = AttendeeGroup.Define(
            AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting]);
        var attendee = Attendee.Create(Guid.NewGuid(), "Amara", "amara@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, "hash", DateTimeOffset.UtcNow.AddDays(1),
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        var booking = Booking.Create(Guid.NewGuid(), invite, eventItem.Id, "manage", DateTimeOffset.UtcNow);
        var appointments = new InMemoryBookingAppointmentRepository(new InMemoryBookingRepository());
        appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.MedicalCheckUp));
        var events = new InMemoryEventRepository();
        events.Items.Add(eventItem);
        var capacities = new InMemoryEventCapacityRepository(events);
        var canceller = new BookingCanceller(
            appointments, capacities, new RecordingAuditLogger());

        var result = await canceller.CancelLockedAsync(
            booking, eventItem, EventBooking.Domain.Audit.ActorType.System, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([AppointmentTypeIds.MedicalCheckUp], result.Value);
        Assert.Equal(5, eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(5, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(5, eventItem.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
    }

    /// <summary>A Booking addressing a dropped capacity row fails with the conflict code.</summary>
    [Fact]
    public async Task CancellationForDroppedTypeFailsLikeFullCapacity()
    {
        var eventItem = Event.CreateImported(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 10, 10), new TimeOnly(9, 0)),
            new Dictionary<Guid, int>
            {
                [AppointmentTypeIds.DrugAndAlcoholTesting] = 5,
                [AppointmentTypeIds.MedicalCheckUp] = 5,
                [AppointmentTypeIds.UniformFitting] = 5,
            });
        eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).Decrement();
        var group = AttendeeGroup.Define(
            AttendeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT", "Ground Ops", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        var attendee = Attendee.Create(Guid.NewGuid(), "Amara", "amara@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, "hash", DateTimeOffset.UtcNow.AddDays(1),
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp], 0);
        var booking = Booking.Create(Guid.NewGuid(), invite, eventItem.Id, "manage", DateTimeOffset.UtcNow);
        var bookings = new InMemoryBookingRepository();
        bookings.Add(booking);
        var appointments = new InMemoryBookingAppointmentRepository(bookings);
        appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting));
        appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.MedicalCheckUp));
        var capacities = new RowDroppingCapacityRepository(
            eventItem, AppointmentTypeIds.MedicalCheckUp);
        var canceller = new BookingCanceller(
            appointments, capacities, new RecordingAuditLogger());

        var result = await canceller.CancelLockedAsync(
            booking, eventItem, ActorType.System, null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(BookingStatus.Active, booking.Status);
        Assert.Equal(4, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(4, eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
    }

    /// <summary>Simulates the capacity store losing one row the aggregate still references.</summary>
    private sealed class RowDroppingCapacityRepository(Event eventItem, Guid droppedTypeId)
        : EventBooking.Application.Abstractions.IEventCapacityRepository
    {
        public Task<IReadOnlyList<EventCapacity>> LockForUpdateAsync(
            Guid eventId,
            IReadOnlyCollection<Guid> appointmentTypeIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EventCapacity>>(
                appointmentTypeIds
                    .Where(id => id != droppedTypeId)
                    .OrderBy(id => id)
                    .Select(eventItem.CapacityFor)
                    .ToList());
    }
}
`````

## before — tests/EventBooking.Application.Tests/Bookings/CancelBookingHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":251,"oldPath":"tests/EventBooking.Application.Tests/Bookings/CancelBookingHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Bookings/CancelBookingHandlerTests.cs","beforeSha":"5292af1413b931518822619b454a3d31a870177eb396e4481143061ab1344bde","afterSha":"f4b814ba7079e730c211d0cf6da9b732773bf5f3f795f9c950b59fe858dcc7cf","side":"before","part":1,"parts":1} -->

`````csharp
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
`````

## after — tests/EventBooking.Application.Tests/Bookings/CancelBookingHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":251,"oldPath":"tests/EventBooking.Application.Tests/Bookings/CancelBookingHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Bookings/CancelBookingHandlerTests.cs","beforeSha":"5292af1413b931518822619b454a3d31a870177eb396e4481143061ab1344bde","afterSha":"f4b814ba7079e730c211d0cf6da9b732773bf5f3f795f9c950b59fe858dcc7cf","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies booking cancellation behavior and its attendee lifecycle lock order.</summary>
public class CancelBookingHandlerTests
{
    private static readonly AttendeePortalOptions Portal = new(
        "https://booking.example.com", "Corporate HQ", "recruitment@corp.com");

    private readonly TransactionOperationLog _operations = new();
    private readonly InMemoryBookingRepository _bookings;
    private readonly InMemoryAttendeeRepository _attendees;
    private readonly InMemoryEventRepository _events;
    private readonly InMemoryEventCapacityRepository _capacities;
    private readonly InMemoryBookingAppointmentRepository _appointments;
    private readonly InMemoryInviteRepository _invites;
    private readonly InMemoryAttendeeGroupRepository _groups = new();
    private readonly InMemorySystemSettingsRepository _settings = new();
    private readonly RecordingEmailSender _email = new();
    private readonly InMemoryEmailDeliveryRepository _deliveries = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork;
    private readonly FakeTokenService _tokens = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly Attendee _attendee;
    private readonly Event _booked;
    private readonly string _manageToken;

    private CancelBookingHandler Handler => new(
        _bookings,
        _events,
        _attendees,
        _invites,
        new BookingCanceller(_appointments, _capacities, _audit),
        new InviteIssuer(
            _invites, _groups, new EligibleEventFinder(_events, _clock), _settings,
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
        _attendees = new InMemoryAttendeeRepository(_operations);
        _events = new InMemoryEventRepository(_operations);
        _capacities = new InMemoryEventCapacityRepository(_events, _operations);
        _appointments = new InMemoryBookingAppointmentRepository(_bookings, _operations);
        _unitOfWork = new FakeUnitOfWork(_operations);

        var pilots = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        _groups.Items.Add(pilots);
        _attendee = Attendee.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", pilots);
        _attendees.Add(_attendee);

        _booked = AddEvent(10);
        AddEvent(12);
        AddEvent(14);
        AddEvent(16);

        var inviteId = Guid.NewGuid();
        var issued = _tokens.Issue(inviteId);
        var invite = Invite.CreateInitial(
            inviteId, _attendee.Id, issued.TokenHash, _clock.UtcNow.AddDays(4),
            [_booked.Id, _events.Items[1].Id, _events.Items[2].Id],
            _attendee.RequiredAppointmentTypeIds, 0);
        _invites.Add(invite);
        _attendee.MarkInvited();

        var bookingId = Guid.NewGuid();
        var manage = _tokens.Issue(bookingId);
        _manageToken = manage.Token;
        _bookings.Add(Booking.Create(bookingId, invite, _booked.Id, manage.TokenHash, _clock.UtcNow));
        invite.MarkUsed();
        _attendee.MarkBooked();

        foreach (var typeId in _attendee.RequiredAppointmentTypeIds)
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
        Assert.Equal(AttendeeStatus.NotYetInvited, _attendee.Status);
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
    public async Task CancellingAfterTheEventDateIsRefused()
    {
        _clock.UtcNow = new DateTimeOffset(2026, 9, 11, 9, 0, 0, TimeSpan.Zero);

        var result = await Handler.HandleAsync(
            new CancelBookingCommand(_manageToken, false), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(BookingStatus.Active, _bookings.Items.Single().Status);
    }

    /// <summary>
    /// Cancellation takes the attendee lifecycle lock before its active booking, eventItem, and
    /// capacity rows so rebooking cannot race a concurrent confirmation for the attendee.
    /// </summary>
    [Fact]
    public async Task CancellingUsesTheAttendeeLifecycleLockOrder()
    {
        await Handler.HandleAsync(new CancelBookingCommand(_manageToken, false), CancellationToken.None);

        Assert.Equal(
            [
                "booking-event-located",
                "transaction-begun",
                "attendee-locked",
                "pending-invites-locked",
                "booking-locked",
                "active-recovery-locked",
                "event-guard-locked",
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
        Assert.Equal(AttendeeStatus.Invited, _attendee.Status);
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
    public async Task TheFreedEventCanBeOfferedAgainImmediately()
    {
        var result = await Handler.HandleAsync(
            new CancelBookingCommand(_manageToken, true), CancellationToken.None);

        Assert.True(result.Value.Reinvited);
        Assert.Contains(_booked.Id, _invites.Items[1].OfferedEventIds);
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
            10, _events.Items[1].CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(originalDat, _booked.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(originalUni, _booked.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
        Assert.Equal(AttendeeStatus.Booked, _attendee.Status);
        Assert.Equal(2, _invites.Items.Count);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task CancellingAnOriginalSupersedesItsPendingRecoveryInvite()
    {
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(), _attendee.Id, _bookings.Items.Single().Id, "hash-recovery-pending",
            _clock.UtcNow.AddDays(4),
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        _invites.Add(recoveryInvite);

        var result = await Handler.HandleAsync(
            new CancelBookingCommand(_manageToken, false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InviteStatus.Superseded, recoveryInvite.Status);
        Assert.Equal(BookingStatus.Cancelled, _bookings.Items.Single().Status);
        Assert.Equal(AttendeeStatus.NotYetInvited, _attendee.Status);
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
            10, _events.Items[1].CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(
            10, _booked.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(
            8, _booked.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
        Assert.Equal(AttendeeStatus.NotYetInvited, _attendee.Status);
        Assert.Equal(2, _audit.Entries.Count(e => e.Action == AuditAction.BookingCancelled));
    }

    private (Booking Recovery, string RecoveryManageToken) AddActiveRecovery()
    {
        var original = _bookings.Items.Single();
        var recoveryInviteId = Guid.NewGuid();
        var issued = _tokens.Issue(recoveryInviteId);
        var recoveryInvite = Invite.CreateRecovery(
            recoveryInviteId, _attendee.Id, original.Id, issued.TokenHash,
            _clock.UtcNow.AddDays(4),
            [_events.Items[1].Id, _events.Items[2].Id, _events.Items[3].Id],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        _invites.Add(recoveryInvite);

        var recoveryId = Guid.NewGuid();
        var manage = _tokens.Issue(recoveryId);
        var recovery = Booking.CreateRecovery(
            recoveryId, recoveryInvite, original, _events.Items[1].Id,
            manage.TokenHash, _clock.UtcNow);
        _bookings.Add(recovery);
        recoveryInvite.MarkUsed();

        _events.Items[1].CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.DrugAndAlcoholTesting));

        return (recovery, manage.Token);
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

## before — tests/EventBooking.Application.Tests/Bookings/CancelCandidateBookingHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":252,"oldPath":"tests/EventBooking.Application.Tests/Bookings/CancelCandidateBookingHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Bookings/CancelAttendeeBookingHandlerTests.cs","beforeSha":"f43346d2252b32d41773fc35e3f1254aab3c33cefc83c10bacf53e10976125a1","afterSha":"0ee38326509737f03a9087817cb04324f047cf123f5285b096f13617c13d7351","side":"before","part":1,"parts":1} -->

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
