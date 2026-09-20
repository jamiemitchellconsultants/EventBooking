# 00f — Retire the single-site configuration, edits 6 (Task 3d)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.Application.Tests/Bookings/ConfirmBookingHandlerTests.cs — 1/1

<!-- retirement-file: {"id":22,"file":"tests/EventBooking.Application.Tests/Bookings/ConfirmBookingHandlerTests.cs","beforeSha":"22ccd513e460a7b1eeaebf95ea06ca1efdea4ed4c726f0ed679535820e79aa55","afterSha":"c5685efc414b8787e0f9aad53fc8b7a36851f9792a13740cc7ddd3aef580b2c7","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies booking confirmation behavior and its attendee lifecycle lock order.</summary>
public class ConfirmBookingHandlerTests
{
    private static readonly AttendeePortalOptions Portal = new(
        "https://booking.example.com", "recruitment@corp.com");

    private static readonly Guid StaffUserId = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly TransactionOperationLog _operations = new();
    private readonly InMemoryInviteRepository _invites;
    private readonly InMemoryAttendeeRepository _attendees;
    private readonly InMemoryEventRepository _events;
    private readonly InMemoryBookingRepository _bookings;
    private readonly InMemoryBookingAppointmentRepository _appointments;
    private readonly RecordingEmailSender _email = new();
    private readonly InMemoryEmailDeliveryRepository _deliveries = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork;
    private readonly FakeTokenService _tokens = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly Attendee _attendee;
    private readonly Invite _invite;
    private readonly string _token;
    private readonly Event _chosen;

    private ConfirmBookingHandler Handler
    {
        get
        {
            var capacities = new InMemoryEventCapacityRepository(_events, _operations);
            return new ConfirmBookingHandler(
                _invites, _attendees, _events, _bookings,
                _appointments, capacities,
                new EligibleEventFinder(_events, _clock), _tokens,
                EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock), _audit,
                _unitOfWork, _clock, Portal);
        }
    }

    public ConfirmBookingHandlerTests()
    {
        _invites = new InMemoryInviteRepository(_operations);
        _attendees = new InMemoryAttendeeRepository(_operations);
        _bookings = new InMemoryBookingRepository(_operations);
        _events = new InMemoryEventRepository(_operations);
        _appointments = new InMemoryBookingAppointmentRepository(_bookings, _operations);
        _unitOfWork = new FakeUnitOfWork(_operations);

        var pilots = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        _attendee = Attendee.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", pilots);
        _attendees.Add(_attendee);

        _chosen = AddEvent(10, 9);
        var others = new[] { AddEvent(11, 13).Id, AddEvent(13, 9).Id };

        var inviteId = Guid.NewGuid();
        var issued = _tokens.Issue(inviteId);
        _token = issued.Token;
        _invite = Invite.CreateInitial(
            inviteId, _attendee.Id, issued.TokenHash, _clock.UtcNow.AddDays(4),
            [_chosen.Id, others[0], others[1]],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting], 0);
        _invites.Add(_invite);
        _attendee.MarkInvited();
    }

    private Task<Result<ConfirmBookingOutcome>> Confirm(Guid? eventId = null) =>
        Handler.HandleAsync(
            new ConfirmBookingCommand(_token, eventId ?? _chosen.Id), CancellationToken.None);

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
        Assert.Equal(_attendee.Id, booking.AttendeeId);
        Assert.Equal(_chosen.Id, booking.EventId);
        Assert.Equal(_invite.Id, booking.InviteId);
        Assert.Equal(BookingStatus.Active, booking.Status);

        Assert.Equal(InviteStatus.Used, _invite.Status);
        Assert.Equal(AttendeeStatus.Booked, _attendee.Status);
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
    /// The attendee lifecycle lock precedes invite, active-booking, eventItem, and capacity locks so
    /// disjoint tokens cannot make two bookings for the same attendee.
    /// </summary>
    [Fact]
    public async Task ConfirmationUsesTheAttendeeLifecycleLockOrder()
    {
        await Confirm();

        Assert.Equal(
            ["transaction-begun", "attendee-locked", "invite-locked", "active-booking-locked", "event-guard-locked", "capacity-locked"],
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
        Assert.DoesNotContain("Corporate HQ", message.TextBody);
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
            e => Assert.Equal(ActorType.AttendeeToken, e.ActorType));
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
        var other = AddEvent(20, 9);

        var result = await Confirm(other.Id);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("That time is not one of your options.", result.Error.Message);
        Assert.Empty(_bookings.Items);
    }

    [Fact]
    public async Task AnExhaustedOptionIsDroppedAndReplaced()
    {
        // Fill the chosen event's drug and alcohol capacity between offer and click.
        var capacity = _chosen.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);
        var remainingCapacity = capacity.RemainingCapacity;
        for (var i = 0; i < remainingCapacity; i++)
        {
            capacity.Decrement();
        }

        var replacement = AddEvent(15, 9);

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
    /// With no replacement available the option is dropped and the attendee is flagged for
    /// coordinator follow-up rather than left with a silently shrinking choice (Issue #242).
    /// </summary>
    [Fact]
    public async Task WithNoReplacementAvailableTheAttendeeIsFlaggedForFollowUp()
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
        Assert.Equal(AttendeeStatus.NoResponseNeedsFollowUp, _attendee.Status);
        Assert.True(_audit.Contains(AuditAction.InviteOptionReplaced));
    }

    [Fact]
    public async Task ChoosingAEventThatWasCancelledIsTreatedTheSameWay()
    {
        _chosen.Cancel();

        var result = await Confirm();

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.False(_invite.Offers(_chosen.Id));
    }

    /// <summary>Ensures a stale option cannot consume capacity and is replaced when possible.</summary>
    [Fact]
    public async Task ChoosingAnOptionOnTheTransitionalLocationDateDropsItAndAddsAFutureReplacement()
    {
        var stale = AddEvent(3, 9);
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
        _attendee.AssignAttendeeGroup(AttendeeGroup.Define(
            AttendeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
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
        Assert.Equal(AttendeeStatus.Booked, _attendee.Status);
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
    /// Recovery confirmation locks Attendee, Invite, original Booking, active recovery,
    /// eventItem, then capacities — the global lifecycle order with the journey locks included.
    /// </summary>
    [Fact]
    public async Task RecoveryConfirmationUsesTheAttendeeLifecycleLockOrder()
    {
        var (_, _, token) = BookWithRecoverableNoShow();

        await Handler.HandleAsync(
            new ConfirmBookingCommand(token, _chosen.Id), CancellationToken.None);

        Assert.Equal(
            [
                "transaction-begun",
                "attendee-locked",
                "invite-locked",
                "original-booking-locked",
                "active-recovery-locked",
                "event-guard-locked",
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
        _attendee.MarkBooked();

        foreach (var typeId in _attendee.RequiredAppointmentTypeIds)
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
            recoveryId, _attendee.Id, original.Id, issued.TokenHash, _clock.UtcNow.AddDays(4),
            [_chosen.Id, AddEvent(15, 9).Id, AddEvent(17, 9).Id],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        _invites.Add(recovery);

        return (original, recovery, issued.Token);
    }

    private Event AddEvent(int day, int hour)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, day), new TimeOnly(hour, 0)),
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

## before — tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs — 1/1

<!-- retirement-file: {"id":23,"file":"tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs","beforeSha":"a3efe4403b85b99dbcbfab71964fd74fc860378ee3041388703be76a1f7d61a1","afterSha":"b2519f2b12ac757318fe4aac67c6301baed6c83587d0b2654f6d3d8ba7ffc2b7","side":"before","part":1,"parts":1} -->

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
        "https://booking.example.com", "Corporate HQ", "recruitment@corp.com");

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

## after — tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs — 1/1

<!-- retirement-file: {"id":23,"file":"tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs","beforeSha":"a3efe4403b85b99dbcbfab71964fd74fc860378ee3041388703be76a1f7d61a1","afterSha":"b2519f2b12ac757318fe4aac67c6301baed6c83587d0b2654f6d3d8ba7ffc2b7","side":"after","part":1,"parts":1} -->

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
