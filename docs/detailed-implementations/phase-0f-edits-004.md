# 00f — Retire the single-site configuration, edits 4 (Task 3d)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs — 1/1

<!-- retirement-file: {"id":19,"file":"tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs","beforeSha":"9fd8464eac7af500787829d2db4eed8b484b1207933398a1da05f4050ca73f41","afterSha":"0c88a040f50f7a474086038b31f274b6aed241c1f8ad13609fba31c7d45f8774","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies booking confirmation snapshots required operational appointments atomically.</summary>
public sealed class BookingAppointmentSnapshotTests
{
    /// <summary>Verifies one Expected appointment is created for each current attendee requirement.</summary>
    [Fact]
    public async Task ConfirmationCreatesOneAppointmentPerRequirementBeforeTheSingleSave()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 7, 9, 0, 0, TimeSpan.Zero));
        var tokens = new FakeTokenService();
        var attendees = new InMemoryAttendeeRepository();
        var pilots = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "amara@example.com",
            pilots);
        attendees.Add(attendee);
        attendee.MarkInvited();

        var events = new InMemoryEventRepository();
        var selected = AddEvent(events, new DateOnly(2026, 9, 8));
        var second = AddEvent(events, new DateOnly(2026, 9, 9));
        var third = AddEvent(events, new DateOnly(2026, 9, 10));
        var inviteId = Guid.NewGuid();
        var token = tokens.Issue(inviteId);
        var invites = new InMemoryInviteRepository();
        invites.Add(Invite.CreateInitial(
            inviteId,
            attendee.Id,
            token.TokenHash,
            clock.UtcNow.AddDays(4),
            [selected.Id, second.Id, third.Id],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            0));

        var bookings = new InMemoryBookingRepository();
        var appointments = new InMemoryBookingAppointmentRepository(bookings);
        var unitOfWork = new FakeUnitOfWork();
        var deliveries = EmailDeliveryTestFactory.Create(
            new InMemoryEmailDeliveryRepository(),
            new RecordingEmailSender(),
            new FakeUnitOfWork(),
            clock);
        var handler = new ConfirmBookingHandler(
            invites,
            attendees,
            events,
            bookings,
            appointments,
            new InMemoryEventCapacityRepository(events),
            new EligibleEventFinder(events, clock),
            tokens,
            deliveries,
            new RecordingAuditLogger(),
            unitOfWork,
            clock,
            new AttendeePortalOptions(
                "https://booking.example.com", "Head office", "help@example.com"));

        var result = await handler.HandleAsync(
            new ConfirmBookingCommand(token.Token, selected.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, appointments.Items.Count);
        Assert.All(appointments.Items, value =>
        {
            Assert.Equal(result.Value.BookingId, value.BookingId);
            Assert.Equal(BookingAppointmentStatus.Expected, value.Status);
            Assert.Equal(1, value.Version);
        });
        Assert.Equal(
            attendee.RequiredAppointmentTypeIds.OrderBy(value => value),
            appointments.Items.Select(value => value.AppointmentTypeId).OrderBy(value => value));
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Equal(1, unitOfWork.CommitCount);
    }

    /// <summary>Verifies one-, two-, and three-type snapshots each produce their exact set.</summary>
    [Theory]
    [InlineData("MED")]
    [InlineData("DAT,UNI")]
    [InlineData("DAT,MED,UNI")]
    public async Task ConfirmationCreatesTheExactSnapshotSet(string codes)
    {
        var byCode = new Dictionary<string, Guid>(StringComparer.Ordinal)
        {
            ["DAT"] = AppointmentTypeIds.DrugAndAlcoholTesting,
            ["MED"] = AppointmentTypeIds.MedicalCheckUp,
            ["UNI"] = AppointmentTypeIds.UniformFitting,
        };
        var snapshot = codes.Split(',').Select(code => byCode[code]).ToList();

        var clock = new FakeClock(new DateTimeOffset(2026, 9, 7, 9, 0, 0, TimeSpan.Zero));
        var tokens = new FakeTokenService();
        var attendees = new InMemoryAttendeeRepository();
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "amara@example.com",
            AttendeeGroup.Define(
                Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting]));
        attendees.Add(attendee);
        attendee.MarkInvited();

        var events = new InMemoryEventRepository();
        var selected = AddEvent(events, new DateOnly(2026, 9, 8));
        var inviteId = Guid.NewGuid();
        var token = tokens.Issue(inviteId);
        var invites = new InMemoryInviteRepository();
        invites.Add(Invite.CreateInitial(
            inviteId, attendee.Id, token.TokenHash, clock.UtcNow.AddDays(4),
            [selected.Id, AddEvent(events, new DateOnly(2026, 9, 9)).Id,
                AddEvent(events, new DateOnly(2026, 9, 10)).Id],
            snapshot, 0));

        var bookings = new InMemoryBookingRepository();
        var appointments = new InMemoryBookingAppointmentRepository(bookings);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ConfirmBookingHandler(
            invites,
            attendees,
            events,
            bookings,
            appointments,
            new InMemoryEventCapacityRepository(events),
            new EligibleEventFinder(events, clock),
            tokens,
            EmailDeliveryTestFactory.Create(
                new InMemoryEmailDeliveryRepository(),
                new RecordingEmailSender(),
                new FakeUnitOfWork(),
                clock),
            new RecordingAuditLogger(),
            unitOfWork,
            clock,
            new AttendeePortalOptions(
                "https://booking.example.com", "Head office", "help@example.com"));

        // Each snapshot size needs its matching group so confirmation proceeds.
        var group = snapshot.Count switch
        {
            1 => AttendeeGroup.Define(
                AttendeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
                [AppointmentTypeIds.MedicalCheckUp]),
            2 => AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]),
            _ => AttendeeGroup.Define(
                AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                    AppointmentTypeIds.UniformFitting]),
        };
        attendee.AssignAttendeeGroup(group);

        var result = await handler.HandleAsync(
            new ConfirmBookingCommand(token.Token, selected.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            snapshot.OrderBy(value => value),
            appointments.Items.Select(value => value.AppointmentTypeId).OrderBy(value => value));
    }

    private static Event AddEvent(
        InMemoryEventRepository events,
        DateOnly date)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(9, 0)), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 10);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        events.Add(eventItem);
        return eventItem;
    }
}
`````

## after — tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs — 1/1

<!-- retirement-file: {"id":19,"file":"tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs","beforeSha":"9fd8464eac7af500787829d2db4eed8b484b1207933398a1da05f4050ca73f41","afterSha":"0c88a040f50f7a474086038b31f274b6aed241c1f8ad13609fba31c7d45f8774","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies booking confirmation snapshots required operational appointments atomically.</summary>
public sealed class BookingAppointmentSnapshotTests
{
    /// <summary>Verifies one Expected appointment is created for each current attendee requirement.</summary>
    [Fact]
    public async Task ConfirmationCreatesOneAppointmentPerRequirementBeforeTheSingleSave()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 7, 9, 0, 0, TimeSpan.Zero));
        var tokens = new FakeTokenService();
        var attendees = new InMemoryAttendeeRepository();
        var pilots = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "amara@example.com",
            pilots);
        attendees.Add(attendee);
        attendee.MarkInvited();

        var events = new InMemoryEventRepository();
        var selected = AddEvent(events, new DateOnly(2026, 9, 8));
        var second = AddEvent(events, new DateOnly(2026, 9, 9));
        var third = AddEvent(events, new DateOnly(2026, 9, 10));
        var inviteId = Guid.NewGuid();
        var token = tokens.Issue(inviteId);
        var invites = new InMemoryInviteRepository();
        invites.Add(Invite.CreateInitial(
            inviteId,
            attendee.Id,
            token.TokenHash,
            clock.UtcNow.AddDays(4),
            [selected.Id, second.Id, third.Id],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            0));

        var bookings = new InMemoryBookingRepository();
        var appointments = new InMemoryBookingAppointmentRepository(bookings);
        var unitOfWork = new FakeUnitOfWork();
        var deliveries = EmailDeliveryTestFactory.Create(
            new InMemoryEmailDeliveryRepository(),
            new RecordingEmailSender(),
            new FakeUnitOfWork(),
            clock);
        var handler = new ConfirmBookingHandler(
            invites,
            attendees,
            events,
            bookings,
            appointments,
            new InMemoryEventCapacityRepository(events),
            new EligibleEventFinder(events, clock),
            tokens,
            deliveries,
            new RecordingAuditLogger(),
            unitOfWork,
            clock,
            new AttendeePortalOptions(
                "https://booking.example.com", "help@example.com"));

        var result = await handler.HandleAsync(
            new ConfirmBookingCommand(token.Token, selected.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, appointments.Items.Count);
        Assert.All(appointments.Items, value =>
        {
            Assert.Equal(result.Value.BookingId, value.BookingId);
            Assert.Equal(BookingAppointmentStatus.Expected, value.Status);
            Assert.Equal(1, value.Version);
        });
        Assert.Equal(
            attendee.RequiredAppointmentTypeIds.OrderBy(value => value),
            appointments.Items.Select(value => value.AppointmentTypeId).OrderBy(value => value));
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Equal(1, unitOfWork.CommitCount);
    }

    /// <summary>Verifies one-, two-, and three-type snapshots each produce their exact set.</summary>
    [Theory]
    [InlineData("MED")]
    [InlineData("DAT,UNI")]
    [InlineData("DAT,MED,UNI")]
    public async Task ConfirmationCreatesTheExactSnapshotSet(string codes)
    {
        var byCode = new Dictionary<string, Guid>(StringComparer.Ordinal)
        {
            ["DAT"] = AppointmentTypeIds.DrugAndAlcoholTesting,
            ["MED"] = AppointmentTypeIds.MedicalCheckUp,
            ["UNI"] = AppointmentTypeIds.UniformFitting,
        };
        var snapshot = codes.Split(',').Select(code => byCode[code]).ToList();

        var clock = new FakeClock(new DateTimeOffset(2026, 9, 7, 9, 0, 0, TimeSpan.Zero));
        var tokens = new FakeTokenService();
        var attendees = new InMemoryAttendeeRepository();
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "amara@example.com",
            AttendeeGroup.Define(
                Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting]));
        attendees.Add(attendee);
        attendee.MarkInvited();

        var events = new InMemoryEventRepository();
        var selected = AddEvent(events, new DateOnly(2026, 9, 8));
        var inviteId = Guid.NewGuid();
        var token = tokens.Issue(inviteId);
        var invites = new InMemoryInviteRepository();
        invites.Add(Invite.CreateInitial(
            inviteId, attendee.Id, token.TokenHash, clock.UtcNow.AddDays(4),
            [selected.Id, AddEvent(events, new DateOnly(2026, 9, 9)).Id,
                AddEvent(events, new DateOnly(2026, 9, 10)).Id],
            snapshot, 0));

        var bookings = new InMemoryBookingRepository();
        var appointments = new InMemoryBookingAppointmentRepository(bookings);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ConfirmBookingHandler(
            invites,
            attendees,
            events,
            bookings,
            appointments,
            new InMemoryEventCapacityRepository(events),
            new EligibleEventFinder(events, clock),
            tokens,
            EmailDeliveryTestFactory.Create(
                new InMemoryEmailDeliveryRepository(),
                new RecordingEmailSender(),
                new FakeUnitOfWork(),
                clock),
            new RecordingAuditLogger(),
            unitOfWork,
            clock,
            new AttendeePortalOptions(
                "https://booking.example.com", "help@example.com"));

        // Each snapshot size needs its matching group so confirmation proceeds.
        var group = snapshot.Count switch
        {
            1 => AttendeeGroup.Define(
                AttendeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
                [AppointmentTypeIds.MedicalCheckUp]),
            2 => AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]),
            _ => AttendeeGroup.Define(
                AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                    AppointmentTypeIds.UniformFitting]),
        };
        attendee.AssignAttendeeGroup(group);

        var result = await handler.HandleAsync(
            new ConfirmBookingCommand(token.Token, selected.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            snapshot.OrderBy(value => value),
            appointments.Items.Select(value => value.AppointmentTypeId).OrderBy(value => value));
    }

    private static Event AddEvent(
        InMemoryEventRepository events,
        DateOnly date)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(9, 0)), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 10);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        events.Add(eventItem);
        return eventItem;
    }
}
`````

## before — tests/EventBooking.Application.Tests/Bookings/CancelAttendeeBookingHandlerTests.cs — 1/1

<!-- retirement-file: {"id":20,"file":"tests/EventBooking.Application.Tests/Bookings/CancelAttendeeBookingHandlerTests.cs","beforeSha":"0ee38326509737f03a9087817cb04324f047cf123f5285b096f13617c13d7351","afterSha":"7ef417acb8f3e010af0c41080a50afdd0af33790bf2f115610ea27894cc90643","side":"before","part":1,"parts":1} -->

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
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies a coordinator cancelling one attendee booking reuses the attendee path exactly.</summary>
public class CancelAttendeeBookingHandlerTests
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

    private CancelAttendeeBookingHandler HandlerFor(IStaffAccessAuthorizer access) => new(
        access,
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
        _clock,
        _unitOfWork);

    private CancelAttendeeBookingHandler Handler => HandlerFor(new FakeAuthorizer(true));

    public CancelAttendeeBookingHandlerTests()
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

        _bookingId = Guid.NewGuid();
        var manage = _tokens.Issue(_bookingId);
        _bookings.Add(Booking.Create(_bookingId, invite, _booked.Id, manage.TokenHash, _clock.UtcNow));
        invite.MarkUsed();
        _attendee.MarkBooked();

        foreach (var typeId in _attendee.RequiredAppointmentTypeIds)
        {
            _booked.CapacityFor(typeId).Decrement();
            _appointments.Add(BookingAppointment.Create(Guid.NewGuid(), _bookingId, typeId));
        }
    }

    private CancelAttendeeBookingCommand Command(Guid bookingId, bool rebook = false) =>
        new(_staffUserId, _attendee.Id, bookingId, rebook);

    [Fact]
    public async Task CancellingAnOriginalWithNoActiveRecoveryVoidsItResetsTheAttendeeAndAuditsStaffAttribution()
    {
        var result = await Handler.HandleAsync(Command(_bookingId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Reinvited);
        Assert.Equal(BookingStatus.Cancelled, _bookings.Items.Single().Status);
        Assert.Equal(10, _booked.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(8, _booked.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
        Assert.Equal(AttendeeStatus.NotYetInvited, _attendee.Status);
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
            10, _events.Items[1].CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(10, _booked.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(AttendeeStatus.NotYetInvited, _attendee.Status);

        var cancellations = _audit.Entries.Where(e => e.Action == AuditAction.BookingCancelled).ToList();
        Assert.Equal(2, cancellations.Count);
        Assert.All(cancellations, e => Assert.Equal(ActorType.Staff, e.ActorType));
        Assert.All(cancellations, e => Assert.Equal(_staffUserId.ToString(), e.ActorId));
    }

    [Fact]
    public async Task CancellingAnOriginalSupersedesItsPendingRecoveryInvite()
    {
        var pendingRecovery = Invite.CreateRecovery(
            Guid.NewGuid(), _attendee.Id, _bookingId, "hash-recovery-pending",
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
            10, _events.Items[1].CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(originalDat, _booked.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(originalUni, _booked.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
        Assert.Equal(AttendeeStatus.Booked, _attendee.Status);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task RebookTrueOnAnOriginalIssuesAReplacementInviteAndReportsReinvited()
    {
        var result = await Handler.HandleAsync(Command(_bookingId, rebook: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Reinvited);
        Assert.True(result.Value.InviteCreated);
        Assert.Equal(AttendeeStatus.Invited, _attendee.Status);
        Assert.Single(_email.Sent);
        Assert.Equal(2, _invites.Items.Count);
        Assert.Equal(InviteStatus.Pending, _invites.Items[1].Status);
        Assert.Contains(_booked.Id, _invites.Items[1].OfferedEventIds);
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
        Assert.Equal(AttendeeStatus.Booked, _attendee.Status);
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
    public async Task CancellingAfterTheEventDateIsRefused()
    {
        _clock.UtcNow = new DateTimeOffset(2026, 9, 11, 9, 0, 0, TimeSpan.Zero);

        var result = await Handler.HandleAsync(Command(_bookingId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(BookingStatus.Active, _bookings.Items.Single().Status);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AnotherAttendeesBookingIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new CancelAttendeeBookingCommand(_staffUserId, Guid.NewGuid(), _bookingId, false),
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
    public async Task CallerWithoutManageAttendeesIsForbiddenAndMutatesNothing()
    {
        var authorizer = new FakeAuthorizer(false);

        var result = await HandlerFor(authorizer).HandleAsync(Command(_bookingId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(StaffCapability.ManageAttendees, authorizer.Seen);
        Assert.Equal(BookingStatus.Active, _bookings.Items.Single().Status);
        Assert.Equal(AttendeeStatus.Booked, _attendee.Status);
        Assert.Equal(0, _unitOfWork.CommitCount);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task CancellationTakesTheAttendeeLifecycleLockOrder()
    {
        await Handler.HandleAsync(Command(_bookingId), CancellationToken.None);

        Assert.Equal(
            [
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

    private Booking AddActiveRecovery()
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

## after — tests/EventBooking.Application.Tests/Bookings/CancelAttendeeBookingHandlerTests.cs — 1/1

<!-- retirement-file: {"id":20,"file":"tests/EventBooking.Application.Tests/Bookings/CancelAttendeeBookingHandlerTests.cs","beforeSha":"0ee38326509737f03a9087817cb04324f047cf123f5285b096f13617c13d7351","afterSha":"7ef417acb8f3e010af0c41080a50afdd0af33790bf2f115610ea27894cc90643","side":"after","part":1,"parts":1} -->

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
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies a coordinator cancelling one attendee booking reuses the attendee path exactly.</summary>
public class CancelAttendeeBookingHandlerTests
{
    private static readonly AttendeePortalOptions Portal = new(
        "https://booking.example.com", "recruitment@corp.com");

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

    private CancelAttendeeBookingHandler HandlerFor(IStaffAccessAuthorizer access) => new(
        access,
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
        _clock,
        _unitOfWork);

    private CancelAttendeeBookingHandler Handler => HandlerFor(new FakeAuthorizer(true));

    public CancelAttendeeBookingHandlerTests()
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

        _bookingId = Guid.NewGuid();
        var manage = _tokens.Issue(_bookingId);
        _bookings.Add(Booking.Create(_bookingId, invite, _booked.Id, manage.TokenHash, _clock.UtcNow));
        invite.MarkUsed();
        _attendee.MarkBooked();

        foreach (var typeId in _attendee.RequiredAppointmentTypeIds)
        {
            _booked.CapacityFor(typeId).Decrement();
            _appointments.Add(BookingAppointment.Create(Guid.NewGuid(), _bookingId, typeId));
        }
    }

    private CancelAttendeeBookingCommand Command(Guid bookingId, bool rebook = false) =>
        new(_staffUserId, _attendee.Id, bookingId, rebook);

    [Fact]
    public async Task CancellingAnOriginalWithNoActiveRecoveryVoidsItResetsTheAttendeeAndAuditsStaffAttribution()
    {
        var result = await Handler.HandleAsync(Command(_bookingId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Reinvited);
        Assert.Equal(BookingStatus.Cancelled, _bookings.Items.Single().Status);
        Assert.Equal(10, _booked.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(8, _booked.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
        Assert.Equal(AttendeeStatus.NotYetInvited, _attendee.Status);
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
            10, _events.Items[1].CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(10, _booked.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(AttendeeStatus.NotYetInvited, _attendee.Status);

        var cancellations = _audit.Entries.Where(e => e.Action == AuditAction.BookingCancelled).ToList();
        Assert.Equal(2, cancellations.Count);
        Assert.All(cancellations, e => Assert.Equal(ActorType.Staff, e.ActorType));
        Assert.All(cancellations, e => Assert.Equal(_staffUserId.ToString(), e.ActorId));
    }

    [Fact]
    public async Task CancellingAnOriginalSupersedesItsPendingRecoveryInvite()
    {
        var pendingRecovery = Invite.CreateRecovery(
            Guid.NewGuid(), _attendee.Id, _bookingId, "hash-recovery-pending",
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
            10, _events.Items[1].CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(originalDat, _booked.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(originalUni, _booked.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
        Assert.Equal(AttendeeStatus.Booked, _attendee.Status);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task RebookTrueOnAnOriginalIssuesAReplacementInviteAndReportsReinvited()
    {
        var result = await Handler.HandleAsync(Command(_bookingId, rebook: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Reinvited);
        Assert.True(result.Value.InviteCreated);
        Assert.Equal(AttendeeStatus.Invited, _attendee.Status);
        Assert.Single(_email.Sent);
        Assert.Equal(2, _invites.Items.Count);
        Assert.Equal(InviteStatus.Pending, _invites.Items[1].Status);
        Assert.Contains(_booked.Id, _invites.Items[1].OfferedEventIds);
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
        Assert.Equal(AttendeeStatus.Booked, _attendee.Status);
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
    public async Task CancellingAfterTheEventDateIsRefused()
    {
        _clock.UtcNow = new DateTimeOffset(2026, 9, 11, 9, 0, 0, TimeSpan.Zero);

        var result = await Handler.HandleAsync(Command(_bookingId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(BookingStatus.Active, _bookings.Items.Single().Status);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AnotherAttendeesBookingIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new CancelAttendeeBookingCommand(_staffUserId, Guid.NewGuid(), _bookingId, false),
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
    public async Task CallerWithoutManageAttendeesIsForbiddenAndMutatesNothing()
    {
        var authorizer = new FakeAuthorizer(false);

        var result = await HandlerFor(authorizer).HandleAsync(Command(_bookingId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(StaffCapability.ManageAttendees, authorizer.Seen);
        Assert.Equal(BookingStatus.Active, _bookings.Items.Single().Status);
        Assert.Equal(AttendeeStatus.Booked, _attendee.Status);
        Assert.Equal(0, _unitOfWork.CommitCount);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task CancellationTakesTheAttendeeLifecycleLockOrder()
    {
        await Handler.HandleAsync(Command(_bookingId), CancellationToken.None);

        Assert.Equal(
            [
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

    private Booking AddActiveRecovery()
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
