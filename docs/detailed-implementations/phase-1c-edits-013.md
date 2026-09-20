# 01c — Negotiation across any number of types, edits 13 (Task 6)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs — 1/1

<!-- retirement-file: {"id":37,"file":"tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs","beforeSha":"e66a7fbbdbb747df1175ad132b266ea40263fc0f6c83ea11a7e16abc230ed67e","afterSha":"aa1c7a4e71e6f0c4982f4ee4d2158c75abbfe9beb5ccd8a859351d9d491342c0","side":"before","part":1,"parts":1} -->

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
        "https://booking.example.com", "recruitment@corp.com");

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
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0), 240),
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

## after — tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs — 1/1

<!-- retirement-file: {"id":37,"file":"tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs","beforeSha":"e66a7fbbdbb747df1175ad132b266ea40263fc0f6c83ea11a7e16abc230ed67e","afterSha":"aa1c7a4e71e6f0c4982f4ee4d2158c75abbfe9beb5ccd8a859351d9d491342c0","side":"after","part":1,"parts":1} -->

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
        "https://booking.example.com", "recruitment@corp.com");

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
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0), 240),
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

## before — tests/EventBooking.Application.Tests/Events/CombinedManagerAuthorizationTests.cs — 1/1

<!-- retirement-file: {"id":38,"file":"tests/EventBooking.Application.Tests/Events/CombinedManagerAuthorizationTests.cs","beforeSha":"d5c584e14c00d4ca85c320a171a699f85e098f752e0ac86623e2db8aecd00133","afterSha":"7dc2eafd9e404e50e203ebc2e9810ba473160d1f2a9d416675b891302b744b1c","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Tests.Events;

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
        var proposals = new InMemoryEventProposalRepository();
        var handler = new ProposeEventHandler(
            proposals,
            new StaffAccessAuthorizer(profiles),
            new FakeUnitOfWork(),
            new RecordingAuditLogger(),
            new FakeClock());

        var result = await handler.HandleAsync(
            new ProposeEventCommand(staffUserId, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
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
        var proposals = new InMemoryEventProposalRepository();
        var handler = new ProposeEventHandler(
            proposals,
            new StaffAccessAuthorizer(profiles),
            new FakeUnitOfWork(),
            new RecordingAuditLogger(),
            new FakeClock());

        var result = await handler.HandleAsync(
            new ProposeEventCommand(staffUserId, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(proposals.Items);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Events/CombinedManagerAuthorizationTests.cs — 1/1

<!-- retirement-file: {"id":38,"file":"tests/EventBooking.Application.Tests/Events/CombinedManagerAuthorizationTests.cs","beforeSha":"d5c584e14c00d4ca85c320a171a699f85e098f752e0ac86623e2db8aecd00133","afterSha":"7dc2eafd9e404e50e203ebc2e9810ba473160d1f2a9d416675b891302b744b1c","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Tests.Events;

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
        var proposals = new InMemoryEventProposalRepository();
        var handler = new ProposeEventHandler(
            proposals,
            new StaffAccessAuthorizer(profiles),
            new FakeUnitOfWork(),
            new RecordingAuditLogger(),
            new FakeClock(),
            ProposalFixture.Zones);

        var result = await handler.HandleAsync(
            new ProposeEventCommand(staffUserId, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
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
        var proposals = new InMemoryEventProposalRepository();
        var handler = new ProposeEventHandler(
            proposals,
            new StaffAccessAuthorizer(profiles),
            new FakeUnitOfWork(),
            new RecordingAuditLogger(),
            new FakeClock(),
            ProposalFixture.Zones);

        var result = await handler.HandleAsync(
            new ProposeEventCommand(staffUserId, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(proposals.Items);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Events/EventCancellationConcurrencyTests.cs — 1/1

<!-- retirement-file: {"id":39,"file":"tests/EventBooking.Application.Tests/Events/EventCancellationConcurrencyTests.cs","beforeSha":"1b64bf3d79ff5ecc8b24eee057a26ab7e547f8e94f86d32e96acb0c38b038fad","afterSha":"e4d90d9e0c32e3f85fd5498a7fd86693f396b02c9c4fa41ddef3c682d8f538fa","side":"before","part":1,"parts":1} -->

`````csharp
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

/// <summary>Verifies event and attendee lifecycle cancellation interleavings in the fake lock model.</summary>
public class EventCancellationConcurrencyTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly AttendeePortalOptions Portal = new(
        "https://booking.example.com", "recruitment@corp.com");

    /// <summary>Verifies event cancellation takes its attendee locks before competing confirmation.</summary>
    [Fact]
    public async Task CancellationTakesAttendeeLocksBeforeConfirmationCanReadTheEvent()
    {
        var scenario = new ConcurrentScenario();
        scenario.Attendees.BlockNextGetFor(scenario.BookedAttendee.Id);

        var cancellation = Task.Run(() => scenario.EventCancellation.HandleAsync(
            new CancelEventCommand(Coordinator, scenario.Event.Id, true), CancellationToken.None));

        await scenario.Attendees.WaitUntilBlockedAsync();

        scenario.Attendees.ReleaseBlockedGet();
        await scenario.Locks.WaitUntilHeldAsync(scenario.Event.Id);

        var confirmation = Task.Run(() => scenario.Confirmation.HandleAsync(
            new ConfirmBookingCommand(scenario.ConfirmationToken, scenario.Event.Id), CancellationToken.None));

        var cancellationResult = await cancellation;
        var confirmationResult = await confirmation;

        Assert.True(cancellationResult.IsSuccess);
        Assert.True(confirmationResult.IsFailure);
        Assert.Equal("conflict", confirmationResult.Error.Code);
        Assert.Equal(EventStatus.Cancelled, scenario.Event.Status);
        Assert.DoesNotContain(scenario.Bookings.Items, booking =>
            booking.EventId == scenario.Event.Id && booking.Status == BookingStatus.Active);
    }

    /// <summary>
    /// Attendee-first cancellation serializes behind the event-cancellation cascade on the shared
    /// attendee, so only the cascade can release the booking's capacity.
    /// </summary>
    /// <summary>Verifies event cancellation and individual cancellation release capacity once.</summary>
    [Fact]
    public async Task AttendeeLifecycleSerializationPreventsDoubleCapacityRelease()
    {
        var scenario = new ConcurrentScenario();
        scenario.Attendees.BlockNextGetFor(scenario.BookedAttendee.Id);

        var cancellation = Task.Run(() => scenario.EventCancellation.HandleAsync(
            new CancelEventCommand(Coordinator, scenario.Event.Id, true), CancellationToken.None));

        await scenario.Attendees.WaitUntilBlockedAsync();
        scenario.Attendees.ReleaseBlockedGet();
        await scenario.Locks.WaitUntilHeldAsync(scenario.Event.Id);

        var individualCancellation = Task.Run(() => scenario.IndividualCancellation.HandleAsync(
            new CancelBookingCommand(scenario.ManageToken, false), CancellationToken.None));

        var cancellationResult = await cancellation;
        var individualResult = await individualCancellation;

        var capacity = scenario.Event.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);

        Assert.True(cancellationResult.IsSuccess);
        Assert.True(individualResult.IsFailure);
        Assert.Equal(BookingStatus.Cancelled, scenario.Bookings.Items.Single().Status);
        Assert.Equal(capacity.TotalHeadcount, capacity.RemainingCapacity);
        Assert.InRange(capacity.RemainingCapacity, 0, capacity.TotalHeadcount);
        // One increment audit per required type proves the single release.
        Assert.Equal(2, scenario.Audit.Entries.Count(entry =>
            entry.Action == AuditAction.CapacityIncremented));
    }

    private sealed class ConcurrentScenario
    {
        public TransactionalEventLockCoordinator Locks { get; } = new();
        public InMemoryEventRepository Events { get; }
        public InMemoryBookingRepository Bookings { get; } = new();
        public InMemoryBookingAppointmentRepository Appointments { get; }
        public BlockingAttendeeRepository Attendees { get; } = new();
        public InMemoryInviteRepository Invites { get; } = new();
        public InMemoryAttendeeGroupRepository Groups { get; } = new();
        public InMemorySystemSettingsRepository Settings { get; } = new();
        public InMemoryStaffAccessProfileRepository Roles { get; } = new();
        public RecordingEmailSender Email { get; } = new();
        /// <summary>Durable delivery rows shared by both cancellation paths in this scenario.</summary>
        public InMemoryEmailDeliveryRepository Deliveries { get; } = new();
        public RecordingAuditLogger Audit { get; } = new();
        public FakeTokenService Tokens { get; } = new();
        public FakeClock Clock { get; } = new(
            new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
        public Event Event { get; }
        public Attendee BookedAttendee { get; }
        public string ManageToken { get; }
        public string ConfirmationToken { get; }

        private readonly InMemoryEventCapacityRepository _capacities;
        private readonly FakeUnitOfWork _eventCancellationUnitOfWork;
        private readonly FakeUnitOfWork _confirmationUnitOfWork;
        private readonly FakeUnitOfWork _individualCancellationUnitOfWork;

        public CancelEventHandler EventCancellation => new(
            Events,
            Bookings,
            Invites,
            Attendees,
            Roles,
            new BookingCanceller(Appointments, _capacities, Audit),
            Issuer,
            new EligibleEventFinder(Events, Clock),
            Appointments,
            EmailDeliveryTestFactory.Create(Deliveries, Email, _eventCancellationUnitOfWork, Clock),
            Audit,
            Clock,
            _eventCancellationUnitOfWork);

        public ConfirmBookingHandler Confirmation => new(
            Invites,
            Attendees,
            Events,
            Bookings,
            new InMemoryBookingAppointmentRepository(Bookings),
            _capacities,
            new EligibleEventFinder(Events, Clock),
            Tokens,
            EmailDeliveryTestFactory.Create(Deliveries, Email, _confirmationUnitOfWork, Clock),
            Audit,
            _confirmationUnitOfWork,
            Clock,
            Portal);

        public CancelBookingHandler IndividualCancellation => new(
            Bookings,
            Events,
            Attendees,
            Invites,
            new BookingCanceller(Appointments, _capacities, Audit),
            Issuer,
            EmailDeliveryTestFactory.Create(Deliveries, Email, _individualCancellationUnitOfWork, Clock),
            Tokens,
            Clock,
            _individualCancellationUnitOfWork);

        private InviteIssuer Issuer => new(
            Invites,
            Groups,
            new EligibleEventFinder(Events, Clock),
            Settings,
            Tokens,
            EmailDeliveryTestFactory.Create(Deliveries, Email, _eventCancellationUnitOfWork, Clock),
            Audit,
            Clock,
            Portal);

        public ConcurrentScenario()
        {
            Events = new InMemoryEventRepository(locks: Locks);
            Appointments = new InMemoryBookingAppointmentRepository(Bookings);
            _capacities = new InMemoryEventCapacityRepository(Events);
            _eventCancellationUnitOfWork = new FakeUnitOfWork(locks: Locks);
            _confirmationUnitOfWork = new FakeUnitOfWork(locks: Locks);
            _individualCancellationUnitOfWork = new FakeUnitOfWork(locks: Locks);

            Roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));

            Event = AddEvent(10);
            AddEvent(12);
            AddEvent(14);
            AddEvent(16);

            var pilots = AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
            Groups.Items.Add(pilots);
            BookedAttendee = Attendee.Create(
                Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", pilots);
            Attendees.Add(BookedAttendee);

            var bookingInviteId = Guid.NewGuid();
            var bookingInviteToken = Tokens.Issue(bookingInviteId);
            var bookingInvite = Invite.CreateInitial(
                bookingInviteId, BookedAttendee.Id, bookingInviteToken.TokenHash, Clock.UtcNow.AddDays(4),
                [Event.Id, Events.Items[1].Id, Events.Items[2].Id],
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting], 0);
            Invites.Add(bookingInvite);
            BookedAttendee.MarkInvited();

            var bookingId = Guid.NewGuid();
            var issuedManageToken = Tokens.Issue(bookingId);
            ManageToken = issuedManageToken.Token;
            Bookings.Add(Booking.Create(
                bookingId, bookingInvite, Event.Id, issuedManageToken.TokenHash, Clock.UtcNow));
            bookingInvite.MarkUsed();
            BookedAttendee.MarkBooked();
            Event.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
            Appointments.Add(BookingAppointment.Create(
                Guid.NewGuid(), bookingId, AppointmentTypeIds.DrugAndAlcoholTesting));
            Event.CapacityFor(AppointmentTypeIds.UniformFitting).Decrement();
            Appointments.Add(BookingAppointment.Create(
                Guid.NewGuid(), bookingId, AppointmentTypeIds.UniformFitting));

            var confirmingAttendee = Attendee.Create(
                Guid.NewGuid(), "B. Chen", "b.chen@mail.com",
                AttendeeGroup.Define(
                    Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
                    [AppointmentTypeIds.DrugAndAlcoholTesting]));
            Attendees.Add(confirmingAttendee);

            var confirmationInviteId = Guid.NewGuid();
            var issuedConfirmationToken = Tokens.Issue(confirmationInviteId);
            ConfirmationToken = issuedConfirmationToken.Token;
            var confirmationInvite = Invite.CreateInitial(
                confirmationInviteId,
                confirmingAttendee.Id,
                issuedConfirmationToken.TokenHash,
                Clock.UtcNow.AddDays(4),
                [Event.Id, Events.Items[1].Id, Events.Items[2].Id],
                [AppointmentTypeIds.DrugAndAlcoholTesting], 0);
            Invites.Add(confirmationInvite);
            confirmingAttendee.MarkInvited();
        }

        private Event AddEvent(int day)
        {
            var proposal = EventProposal.Create(
                Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0), 240),
                Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
            Events.Add(eventItem);
            return eventItem;
        }
    }
}
`````
