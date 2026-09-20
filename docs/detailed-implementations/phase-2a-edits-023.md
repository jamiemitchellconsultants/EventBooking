# 02a — Deterministic attendee links and the token version counter, edits 23 (Task 9a)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs — 1/1

<!-- retirement-file: {"id":54,"file":"tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs","beforeSha":"a557857f1de30947457d12e5fd590c3942f2c9e5a1dba25fc0cfdace7fde46af","afterSha":"1b4df612b23a140e0654d9b75cac39c504a8d7d14889d88e2a793575553772da","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;
using System.Security.Cryptography;
using System.Text;

namespace EventBooking.Application.Tests.Notifications;

/// <summary>Verifies template-aware retries, link reuse, and stale-state conflicts.</summary>
public class RetryEmailHandlerTests
{
    private static readonly Guid Coordinator =
        Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly Guid Admin =
        Guid.Parse("a0000009-0000-0000-0000-000000000009");
    private static readonly AttendeePortalOptions Portal = new(
        "https://booking.example.com", "recruitment@corp.com");

    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryBookingRepository _bookings = new();
    private readonly InMemoryBookingAppointmentRepository _appointments;
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryEmailDeliveryRepository _deliveries = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly RecordingEmailSender _sender = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeTokenService _tokens = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.Zero));
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Attendee _attendee;

    /// <summary>Initializes one authorized attendee and three available events.</summary>
    public RetryEmailHandlerTests()
    {
        _appointments = new InMemoryBookingAppointmentRepository(_bookings);
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _roles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));
        _attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "a.novak@mail.com",
            AttendeeGroup.Define(
                Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting]),
            ProposalFixture.Now);
        _attendees.Add(_attendee);
        AddEvent(10);
        AddEvent(12);
        AddEvent(14);
    }

    /// <summary>Booking-confirmation retry reuses the manage link and sends the right template.</summary>
    [Fact]
    public async Task BookingConfirmationRetryReusesTheManageLinkAndUsesTheConfirmationTemplate()
    {
        var inviteId = Guid.NewGuid();
        var inviteToken = _tokens.Issue(TokenPurpose.Book, inviteId, Invite.InitialTokenVersion);
        var invite = Invite.CreateInitial(
            inviteId,
            _attendee.Id,
            _clock.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            _events.Items.Select(eventItem => eventItem.Id),
            _attendee.RequiredAppointmentTypeIds,
            0);
        _invites.Add(invite);
        _attendee.MarkInvited(ProposalFixture.Now);
        var bookingId = Guid.NewGuid();
        var manage = _tokens.Issue(TokenPurpose.Manage, bookingId, Booking.InitialManageTokenVersion);
        var booking = Booking.Create(bookingId, invite, _events.Items[0].Id, _clock.UtcNow);
        _bookings.Add(booking);
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), bookingId, AppointmentTypeIds.DrugAndAlcoholTesting));
        invite.MarkUsed();
        _attendee.MarkBooked(ProposalFixture.Now);
        var issuedBefore = booking.ManageTokenVersion;
        AddFailedDelivery(EmailTemplate.BookingConfirmation, bookingId: bookingId);

        var result = await Handler().HandleAsync(
            new RetryEmailCommand(Coordinator, _attendee.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Sent", result.Value.DeliveryStatus);
        Assert.Equal(issuedBefore, booking.ManageTokenVersion);
        Assert.Contains($"/manage/{manage}", _sender.LastOf(EmailTemplate.BookingConfirmation).TextBody);
        Assert.Equal(EmailStatus.Resolved, _deliveries.Items[0].Status);
        Assert.Equal(EmailStatus.Sent, _deliveries.Items[1].Status);
        Assert.True(_deliveries.Items[1].SentAt > _deliveries.Items[0].SentAt);
    }

    /// <summary>An administrator is denied attendee delivery recovery by the attendee-data boundary.</summary>
    [Fact]
    public async Task AdministratorCannotRetryAttendeeEmail()
    {
        var result = await Handler().HandleAsync(
            new RetryEmailCommand(Admin, _attendee.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Empty(_sender.Sent);
    }

    /// <summary>Cancellation retry sends only its recorded cancellation template.</summary>
    [Fact]
    public async Task CancellationRetryDoesNotCreateOrSendAnInvite()
    {
        var eventItem = _events.Items[0];
        eventItem.CancelBeforeStart();
        _attendee.MarkAwaitingAvailability(ProposalFixture.Now);
        var booking = GivenCancelledBooking(eventItem.Id);
        AddFailedDelivery(
            EmailTemplate.EventCancelledRebookingNeeded,
            bookingId: booking.Id,
            eventId: eventItem.Id);

        var result = await Handler().HandleAsync(
            new RetryEmailCommand(Coordinator, _attendee.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(_sender.Sent);
        Assert.Equal(EmailTemplate.EventCancelledRebookingNeeded, _sender.Sent[0].Template);
        Assert.Empty(_invites.Items);
    }

    /// <summary>Invite retry reuses the pending invite link and keeps the invite template.</summary>
    [Fact]
    public async Task AttendeeInviteRetryReusesThePendingInviteLink()
    {
        var inviteId = Guid.NewGuid();
        var issued = _tokens.Issue(TokenPurpose.Book, inviteId, Invite.InitialTokenVersion);
        var invite = Invite.CreateInitial(
            inviteId,
            _attendee.Id,
            _clock.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            _events.Items.Select(eventItem => eventItem.Id),
            _attendee.RequiredAppointmentTypeIds,
            0);
        _invites.Add(invite);
        _attendee.MarkInvited(ProposalFixture.Now);
        AddFailedDelivery(EmailTemplate.AttendeeInvite, inviteId: invite.Id);
        var versionBefore = invite.TokenVersion;

        var result = await Handler().HandleAsync(
            new RetryEmailCommand(Coordinator, _attendee.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(versionBefore, invite.TokenVersion);
        Assert.Contains($"/book/{issued}", Assert.Single(_sender.Sent).TextBody);
        Assert.Equal(EmailTemplate.AttendeeInvite, Assert.Single(_sender.Sent).Template);
    }

    /// <summary>Attendee re-invite recovery preserves the reminder template.</summary>
    [Fact]
    public async Task AttendeeReinviteRetryUsesTheReminderTemplate()
    {
        var inviteId = Guid.NewGuid();
        var issued = _tokens.Issue(TokenPurpose.Book, inviteId, Invite.InitialTokenVersion);
        var invite = Invite.CreateInitial(
            inviteId,
            _attendee.Id,
            _clock.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            _events.Items.Select(eventItem => eventItem.Id),
            _attendee.RequiredAppointmentTypeIds,
            1);
        _invites.Add(invite);
        _attendee.MarkInvited(ProposalFixture.Now);
        AddFailedDelivery(EmailTemplate.AttendeeReinvite, inviteId: invite.Id);

        var result = await Handler().HandleAsync(
            new RetryEmailCommand(Coordinator, _attendee.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EmailTemplate.AttendeeReinvite, Assert.Single(_sender.Sent).Template);
    }

    /// <summary>Pending invite and re-invite attempts are recoverable with their original template.</summary>
    [Theory]
    [InlineData(EmailTemplate.AttendeeInvite, 0)]
    [InlineData(EmailTemplate.AttendeeReinvite, 1)]
    public async Task PendingInviteTemplateRetrySupersedesTheOutstandingAttempt(
        EmailTemplate template,
        int reminderCount)
    {
        var inviteId = Guid.NewGuid();
        var issued = _tokens.Issue(TokenPurpose.Book, inviteId, Invite.InitialTokenVersion);
        var invite = Invite.CreateInitial(
            inviteId,
            _attendee.Id,
            _clock.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            _events.Items.Select(eventItem => eventItem.Id),
            _attendee.RequiredAppointmentTypeIds,
            reminderCount);
        _invites.Add(invite);
        _attendee.MarkInvited(ProposalFixture.Now);
        AddPendingDelivery(template, inviteId: invite.Id);

        var result = await Handler().HandleAsync(
            new RetryEmailCommand(Coordinator, _attendee.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(template, Assert.Single(_sender.Sent).Template);
        Assert.Equal(EmailStatus.Resolved, _deliveries.Items[0].Status);
        Assert.Equal(EmailStatus.Sent, _deliveries.Items[1].Status);
    }

    /// <summary>A pending booking confirmation remains recoverable using fresh management credentials.</summary>
    [Fact]
    public async Task PendingBookingConfirmationRetrySupersedesTheOutstandingAttempt()
    {
        var inviteId = Guid.NewGuid();
        var inviteToken = _tokens.Issue(TokenPurpose.Book, inviteId, Invite.InitialTokenVersion);
        var invite = Invite.CreateInitial(
            inviteId,
            _attendee.Id,
            _clock.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            _events.Items.Select(eventItem => eventItem.Id),
            _attendee.RequiredAppointmentTypeIds,
            0);
        _invites.Add(invite);
        _attendee.MarkInvited(ProposalFixture.Now);
        var bookingId = Guid.NewGuid();
        var manage = _tokens.Issue(TokenPurpose.Manage, bookingId, Booking.InitialManageTokenVersion);
        var booking = Booking.Create(bookingId, invite, _events.Items[0].Id, _clock.UtcNow);
        _bookings.Add(booking);
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), bookingId, AppointmentTypeIds.DrugAndAlcoholTesting));
        invite.MarkUsed();
        _attendee.MarkBooked(ProposalFixture.Now);
        AddPendingDelivery(EmailTemplate.BookingConfirmation, bookingId: booking.Id);

        var result = await Handler().HandleAsync(
            new RetryEmailCommand(Coordinator, _attendee.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EmailTemplate.BookingConfirmation, Assert.Single(_sender.Sent).Template);
        Assert.Equal(EmailStatus.Resolved, _deliveries.Items[0].Status);
        Assert.Equal(EmailStatus.Sent, _deliveries.Items[1].Status);
    }

    /// <summary>Pending cancellation recovery is actionable and records a terminal result.</summary>
    [Fact]
    public async Task PendingCancellationRetryCompletesThePendingDelivery()
    {
        var eventItem = _events.Items[0];
        eventItem.CancelBeforeStart();
        _attendee.MarkAwaitingAvailability(ProposalFixture.Now);
        var booking = GivenCancelledBooking(eventItem.Id);
        _deliveries.Add(EmailLog.RecordPending(
            Guid.NewGuid(),
            _attendee.Id,
            EmailTemplate.EventCancelledRebookingNeeded,
            _clock.UtcNow,
            bookingId: booking.Id,
            eventId: eventItem.Id));

        var result = await Handler().HandleAsync(
            new RetryEmailCommand(Coordinator, _attendee.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Sent", result.Value.DeliveryStatus);
        Assert.Single(_sender.Sent);
        Assert.Equal(EmailStatus.Sent, _deliveries.Items[^1].Status);
    }

    /// <summary>An active event makes a historical cancellation notification non-actionable.</summary>
    [Fact]
    public async Task CancellationRetryForAnActiveEventReturnsConflictWithoutSending()
    {
        AddFailedDelivery(
            EmailTemplate.EventCancelledRebookingNeeded,
            eventId: _events.Items[0].Id);

        var result = await Handler().HandleAsync(
            new RetryEmailCommand(Coordinator, _attendee.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Empty(_sender.Sent);
        Assert.Single(_deliveries.Items);
    }

    /// <summary>A cancellation notice is stale once the attendee has booked again.</summary>
    [Fact]
    public async Task CancellationRetryAfterAttendeeBooksAgainReturnsConflictWithoutSending()
    {
        var eventItem = _events.Items[0];
        eventItem.CancelBeforeStart();
        _attendee.MarkInvited(ProposalFixture.Now);
        _attendee.MarkBooked(ProposalFixture.Now);
        AddFailedDelivery(
            EmailTemplate.EventCancelledRebookingNeeded,
            eventId: eventItem.Id);

        var result = await Handler().HandleAsync(
            new RetryEmailCommand(Coordinator, _attendee.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Empty(_sender.Sent);
        Assert.Single(_deliveries.Items);
    }

    /// <summary>A failed delivery whose booking no longer exists returns a stable conflict.</summary>
    [Fact]
    public async Task BookingRetryWithStaleStateReturnsConflictWithoutSending()
    {
        AddFailedDelivery(EmailTemplate.BookingConfirmation, bookingId: Guid.NewGuid());

        var result = await Handler().HandleAsync(
            new RetryEmailCommand(Coordinator, _attendee.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Empty(_sender.Sent);
        Assert.Single(_deliveries.Items);
    }

    /// <summary>A sent latest delivery is a stable conflict and cannot be resent.</summary>
    [Fact]
    public async Task ADeliveredLatestEmailCannotBeRetried()
    {
        AddFailedDelivery(EmailTemplate.EventCancelledRebookingNeeded, eventId: _events.Items[0].Id);
        _deliveries.Items[0].MarkSent(_clock.UtcNow);

        var result = await Handler().HandleAsync(
            new RetryEmailCommand(Coordinator, _attendee.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Empty(_sender.Sent);
    }

    /// <summary>Two retry attempts share one latest-row claim and only one reaches the provider.</summary>
    [Fact]
    public async Task ConcurrentRetriesProduceOneReplacementSend()
    {
        var eventItem = _events.Items[0];
        eventItem.CancelBeforeStart();
        _attendee.MarkAwaitingAvailability(ProposalFixture.Now);
        var booking = GivenCancelledBooking(eventItem.Id);
        var repository = new SerializedRetryDeliveryRepository();
        var failed = EmailLog.RecordPending(
            Guid.NewGuid(),
            _attendee.Id,
            EmailTemplate.EventCancelledRebookingNeeded,
            _clock.UtcNow,
            bookingId: booking.Id,
            eventId: eventItem.Id);
        failed.MarkFailed(_clock.UtcNow);
        repository.Add(failed);

        var first = Handler(repository).HandleAsync(
            new RetryEmailCommand(Coordinator, _attendee.Id), CancellationToken.None);
        await repository.FirstLatestLockAcquired;
        var second = Handler(repository).HandleAsync(
            new RetryEmailCommand(Coordinator, _attendee.Id), CancellationToken.None);

        var firstResult = await first;
        var secondResult = await second;

        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsFailure);
        Assert.Equal("conflict", secondResult.Error.Code);
        Assert.Single(_sender.Sent);
        Assert.Equal(EmailStatus.Sent, repository.Items.Single(item => item.Id == firstResult.Value.DeliveryId).Status);
    }

    /// <summary>Regenerated content follows the Booking snapshot after a group change.</summary>
    [Fact]
    public async Task RegeneratedBookingContentSurvivesAttendeeGroupChange()
    {
        var inviteId = Guid.NewGuid();
        var inviteToken = _tokens.Issue(TokenPurpose.Book, inviteId, Invite.InitialTokenVersion);
        var invite = Invite.CreateInitial(
            inviteId,
            _attendee.Id,
            _clock.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            _events.Items.Select(eventItem => eventItem.Id),
            _attendee.RequiredAppointmentTypeIds,
            0);
        _invites.Add(invite);
        _attendee.MarkInvited(ProposalFixture.Now);
        var bookingId = Guid.NewGuid();
        var manage = _tokens.Issue(TokenPurpose.Manage, bookingId, Booking.InitialManageTokenVersion);
        var booking = Booking.Create(bookingId, invite, _events.Items[0].Id, _clock.UtcNow);
        _bookings.Add(booking);
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), bookingId, AppointmentTypeIds.DrugAndAlcoholTesting));
        invite.MarkUsed();
        _attendee.MarkBooked(ProposalFixture.Now);
        AddFailedDelivery(EmailTemplate.BookingConfirmation, bookingId: bookingId);

        _attendee.AssignAttendeeGroup(AttendeeGroup.Define(
            AttendeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
            [AppointmentTypeIds.MedicalCheckUp]));

        var result = await Handler().HandleAsync(
            new RetryEmailCommand(Coordinator, _attendee.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var sent = Assert.Single(_sender.Sent);
        Assert.Contains("Drug & Alcohol Testing", sent.TextBody);
        Assert.DoesNotContain("Medical Check-up", sent.TextBody);
    }

    /// <summary>A terminal Invite cannot be retried and stages no replacement delivery.</summary>
    [Fact]
    public async Task TerminalInviteRetryCreatesNoReplacementDelivery()
    {
        var inviteId = Guid.NewGuid();
        var issued = _tokens.Issue(TokenPurpose.Book, inviteId, Invite.InitialTokenVersion);
        var invite = Invite.CreateInitial(
            inviteId,
            _attendee.Id,
            _clock.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            _events.Items.Select(eventItem => eventItem.Id),
            _attendee.RequiredAppointmentTypeIds,
            0);
        _invites.Add(invite);
        _attendee.MarkInvited(ProposalFixture.Now);
        invite.MarkSuperseded();
        AddFailedDelivery(EmailTemplate.AttendeeInvite, inviteId: invite.Id);

        var result = await Handler().HandleAsync(
            new RetryEmailCommand(Coordinator, _attendee.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Empty(_sender.Sent);
        Assert.Single(_deliveries.Items);
    }

    private Booking GivenCancelledBooking(Guid eventId)
    {
        var inviteId = Guid.NewGuid();
        var issued = _tokens.Issue(TokenPurpose.Book, inviteId, Invite.InitialTokenVersion);
        var invite = Invite.CreateInitial(
            inviteId,
            _attendee.Id,
            _clock.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            _events.Items.Select(eventItem => eventItem.Id),
            _attendee.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(Guid.NewGuid(), invite, eventId, _clock.UtcNow);
        booking.Cancel();
        _bookings.Add(booking);
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting));
        return booking;
    }

    private RetryEmailHandler Handler(IEmailDeliveryRepository? repository = null) => new(
        _roles,
        _attendees,
        _invites,
        _bookings,
        _events,
        _appointments,
        repository ?? _deliveries,
        EmailDeliveryTestFactory.Create(repository ?? _deliveries, _sender, _unitOfWork, _clock),
        _tokens,
        _unitOfWork,
        _clock,
        Portal);

    private void AddFailedDelivery(
        EmailTemplate template,
        Guid? inviteId = null,
        Guid? bookingId = null,
        Guid? eventId = null)
    {
        AddPendingDelivery(template, inviteId, bookingId, eventId);
        _deliveries.Items[^1].MarkFailed(_clock.UtcNow);
    }

    private void AddPendingDelivery(
        EmailTemplate template,
        Guid? inviteId = null,
        Guid? bookingId = null,
        Guid? eventId = null)
    {
        _deliveries.Add(EmailLog.RecordPending(
            Guid.NewGuid(),
            _attendee.Id,
            template,
            _clock.UtcNow,
            inviteId,
            bookingId,
            eventId));
    }

    private sealed class SerializedRetryDeliveryRepository : IEmailDeliveryRepository
    {
        private readonly TaskCompletionSource<bool> _firstLatestLockAcquired =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _replacementStaged =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _latestLockCalls;

        public List<EmailLog> Items { get; } = [];

        public Task FirstLatestLockAcquired => _firstLatestLockAcquired.Task;

        public Task<EmailLog?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(item => item.Id == id));

        public Task<EmailLog?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(item => item.Id == id));

        public async Task<EmailLog?> LockLatestForAttendeeAsync(
            Guid attendeeId,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _latestLockCalls) == 1)
            {
                _firstLatestLockAcquired.TrySetResult(true);
            }
            else
            {
                await _replacementStaged.Task.WaitAsync(cancellationToken);
            }

            return Items
                .Where(item => item.AttendeeId == attendeeId)
                .OrderByDescending(item => item.SentAt)
                .ThenByDescending(item => Items.IndexOf(item))
                .FirstOrDefault();
        }

        public Task<EmailLog?> GetLatestForAttendeeAsync(
            Guid attendeeId,
            EmailTemplate template,
            CancellationToken cancellationToken) =>
            Task.FromResult(Items
                .Where(item => item.AttendeeId == attendeeId && item.TemplateName == template)
                .OrderByDescending(item => item.SentAt)
                .ThenByDescending(item => Items.IndexOf(item))
                .FirstOrDefault());

        public void Add(EmailLog delivery)
        {
            Items.Add(delivery);
            if (Items.Count > 1)
            {
                _replacementStaged.TrySetResult(true);
            }
        }
    }

    private Event AddEvent(int day)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0), 240),
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

## before — tests/EventBooking.Domain.Tests/Bookings/BookingTests.cs — 1/1

<!-- retirement-file: {"id":55,"file":"tests/EventBooking.Domain.Tests/Bookings/BookingTests.cs","beforeSha":"6f0354359eb447e30668a1bbc4ae266a4e85293f144cde7f5fb15636b75dd57b","afterSha":"dde13dd89532f525786c966e79d1e62aa0cd240138b60151375d4b890600f1cf","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Tests.Bookings;

public class BookingTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid EventA = Guid.Parse("50000001-0000-0000-0000-000000000001");
    private static readonly Guid EventB = Guid.Parse("50000002-0000-0000-0000-000000000002");
    private static readonly Guid EventC = Guid.Parse("50000003-0000-0000-0000-000000000003");
    private static readonly Guid EventNotOffered = Guid.Parse("50000009-0000-0000-0000-000000000009");

    private static Invite NewInvite() =>
        Invite.CreateInitial(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "invite-token-hash",
            Now.AddDays(4),
            [ProposalFixture.LocationId],
            [EventA, EventB, EventC],
            [AppointmentTypeIds.DrugAndAlcoholTesting],
            0);

    private static Booking NewBooking(Invite invite) =>
        Booking.Create(Guid.NewGuid(), invite, EventB, "manage-token-hash", Now);

    [Fact]
    public void ABookingCarriesTheAttendeeEventAndInvite()
    {
        var invite = NewInvite();

        var booking = NewBooking(invite);

        Assert.Equal(invite.AttendeeId, booking.AttendeeId);
        Assert.Equal(EventB, booking.EventId);
        Assert.Equal(invite.Id, booking.InviteId);
        Assert.Equal(Now, booking.CreatedAt);
        Assert.Equal(BookingStatus.Active, booking.Status);
        Assert.Equal("manage-token-hash", booking.ManageTokenHash);
    }

    [Fact]
    public void BookingAEventTheInviteNeverOfferedIsRejected()
    {
        var invite = NewInvite();

        var ex = Assert.Throws<DomainException>(
            () => Booking.Create(Guid.NewGuid(), invite, EventNotOffered, "manage-token-hash", Now));
        Assert.Equal("The chosen eventItem is not one of this invite's options.", ex.Message);
    }

    [Fact]
    public void BookingAnInviteThatIsNoLongerPendingIsRejected()
    {
        var invite = NewInvite();
        invite.MarkExpired();

        var ex = Assert.Throws<DomainException>(() => NewBooking(invite));
        Assert.Equal("This invite can no longer be used.", ex.Message);
    }

    [Fact]
    public void ABookingWithoutAManageTokenHashIsRejected()
    {
        var invite = NewInvite();

        var ex = Assert.Throws<DomainException>(
            () => Booking.Create(Guid.NewGuid(), invite, EventB, " ", Now));
        Assert.Equal("manageTokenHash must not be blank.", ex.Message);
    }

    [Fact]
    public void CreatingABookingDoesNotConsumeTheInvite()
    {
        var invite = NewInvite();

        NewBooking(invite);

        Assert.Equal(InviteStatus.Pending, invite.Status);
    }

    [Fact]
    public void CancellingMarksTheBookingCancelled()
    {
        var booking = NewBooking(NewInvite());

        booking.Cancel();

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
    }

    [Fact]
    public void CancellingTwiceIsRejected()
    {
        var booking = NewBooking(NewInvite());
        booking.Cancel();

        var ex = Assert.Throws<DomainException>(() => booking.Cancel());
        Assert.Equal("This booking has already been cancelled.", ex.Message);
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Bookings/BookingTests.cs — 1/1

<!-- retirement-file: {"id":55,"file":"tests/EventBooking.Domain.Tests/Bookings/BookingTests.cs","beforeSha":"6f0354359eb447e30668a1bbc4ae266a4e85293f144cde7f5fb15636b75dd57b","afterSha":"dde13dd89532f525786c966e79d1e62aa0cd240138b60151375d4b890600f1cf","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Tests.Bookings;

public class BookingTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid EventA = Guid.Parse("50000001-0000-0000-0000-000000000001");
    private static readonly Guid EventB = Guid.Parse("50000002-0000-0000-0000-000000000002");
    private static readonly Guid EventC = Guid.Parse("50000003-0000-0000-0000-000000000003");
    private static readonly Guid EventNotOffered = Guid.Parse("50000009-0000-0000-0000-000000000009");

    private static Invite NewInvite() =>
        Invite.CreateInitial(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddDays(4),
            [ProposalFixture.LocationId],
            [EventA, EventB, EventC],
            [AppointmentTypeIds.DrugAndAlcoholTesting],
            0);

    private static Booking NewBooking(Invite invite) =>
        Booking.Create(Guid.NewGuid(), invite, EventB, Now);

    [Fact]
    public void ABookingCarriesTheAttendeeEventAndInvite()
    {
        var invite = NewInvite();

        var booking = NewBooking(invite);

        Assert.Equal(invite.AttendeeId, booking.AttendeeId);
        Assert.Equal(EventB, booking.EventId);
        Assert.Equal(invite.Id, booking.InviteId);
        Assert.Equal(Now, booking.CreatedAt);
        Assert.Equal(BookingStatus.Active, booking.Status);
        Assert.Equal(Booking.InitialManageTokenVersion, booking.ManageTokenVersion);
    }

    [Fact]
    public void BookingAEventTheInviteNeverOfferedIsRejected()
    {
        var invite = NewInvite();

        var ex = Assert.Throws<DomainException>(
            () => Booking.Create(Guid.NewGuid(), invite, EventNotOffered, Now));
        Assert.Equal("The chosen eventItem is not one of this invite's options.", ex.Message);
    }

    [Fact]
    public void BookingAnInviteThatIsNoLongerPendingIsRejected()
    {
        var invite = NewInvite();
        invite.MarkExpired();

        var ex = Assert.Throws<DomainException>(() => NewBooking(invite));
        Assert.Equal("This invite can no longer be used.", ex.Message);
    }

    [Fact]
    public void RotatingAnActiveBookingsManageTokenMovesToTheNextVersion()
    {
        var booking = NewBooking(NewInvite());

        booking.RotateManageToken();

        Assert.Equal(Booking.InitialManageTokenVersion + 1, booking.ManageTokenVersion);
    }

    [Fact]
    public void RotatingTheManageTokenOfACancelledBookingIsRejected()
    {
        var booking = NewBooking(NewInvite());
        booking.Cancel();

        var ex = Assert.Throws<DomainException>(booking.RotateManageToken);
        Assert.Equal("Only an active booking token can be rotated.", ex.Message);
    }

    [Fact]
    public void CreatingABookingDoesNotConsumeTheInvite()
    {
        var invite = NewInvite();

        NewBooking(invite);

        Assert.Equal(InviteStatus.Pending, invite.Status);
    }

    [Fact]
    public void CancellingMarksTheBookingCancelled()
    {
        var booking = NewBooking(NewInvite());

        booking.Cancel();

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
    }

    [Fact]
    public void CancellingTwiceIsRejected()
    {
        var booking = NewBooking(NewInvite());
        booking.Cancel();

        var ex = Assert.Throws<DomainException>(() => booking.Cancel());
        Assert.Equal("This booking has already been cancelled.", ex.Message);
    }
}
`````

## before — tests/EventBooking.Domain.Tests/Bookings/RecoveryBookingTests.cs — 1/1

<!-- retirement-file: {"id":56,"file":"tests/EventBooking.Domain.Tests/Bookings/RecoveryBookingTests.cs","beforeSha":"7cdf471358f30e9215924c5a0058bb52f7df9b165aac6b7fdb12da8925a0c3bf","afterSha":"308d2b4b7fe3894e9737388ebae40153e681614b9b696420d6fbad194472e64a","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Tests.Bookings;

/// <summary>Verifies every recovery points directly to an immutable active journey root.</summary>
public sealed class RecoveryBookingTests
{
    /// <summary>A recovery Booking copies the original root and can conclude and reopen.</summary>
    [Fact]
    public void RecoveryLifecyclePreservesOriginalRoot()
    {
        var attendeeId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var initialInvite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendeeId,
            "initial",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp],
            0);
        var original = Booking.Create(
            Guid.NewGuid(), initialInvite, eventId, "manage-original", DateTimeOffset.UtcNow);
        var recoveryEvent = Guid.NewGuid();
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(),
            attendeeId,
            original.Id,
            "recovery",
            DateTimeOffset.UtcNow.AddDays(2),
            ProposalFixture.LocationId,
            null,
            [recoveryEvent, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp]);

        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, recoveryEvent,
            "manage-recovery", DateTimeOffset.UtcNow.AddHours(1));
        recovery.Conclude();
        recovery.Reopen();

        Assert.Null(original.RecoveryOfBookingId);
        Assert.Equal(BookingStatus.Active, original.Status);
        Assert.Equal(original.Id, recovery.RecoveryOfBookingId);
        Assert.Equal(BookingStatus.Active, recovery.Status);
    }

    /// <summary>A recovery cannot point at another recovery Booking.</summary>
    [Fact]
    public void RecoveryChainIsRejected()
    {
        var attendeeId = Guid.NewGuid();
        var rootEvent = Guid.NewGuid();
        var initial = Invite.CreateInitial(
            Guid.NewGuid(),
            attendeeId,
            "initial",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [rootEvent, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp],
            0);
        var root = Booking.Create(Guid.NewGuid(), initial, rootEvent, "root", DateTimeOffset.UtcNow);
        var firstEvent = Guid.NewGuid();
        var firstInvite = Invite.CreateRecovery(
            Guid.NewGuid(),
            attendeeId,
            root.Id,
            "first",
            DateTimeOffset.UtcNow.AddDays(1),
            ProposalFixture.LocationId,
            null,
            [firstEvent, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp]);
        var first = Booking.CreateRecovery(
            Guid.NewGuid(), firstInvite, root, firstEvent, "first-manage", DateTimeOffset.UtcNow);
        var secondEvent = Guid.NewGuid();
        var secondInvite = Invite.CreateRecovery(
            Guid.NewGuid(),
            attendeeId,
            root.Id,
            "second",
            DateTimeOffset.UtcNow.AddDays(1),
            ProposalFixture.LocationId,
            null,
            [secondEvent, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp]);

        Assert.Throws<EventBooking.Domain.Common.DomainException>(() => Booking.CreateRecovery(
            Guid.NewGuid(), secondInvite, first, secondEvent, "second-manage", DateTimeOffset.UtcNow));
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Bookings/RecoveryBookingTests.cs — 1/1

<!-- retirement-file: {"id":56,"file":"tests/EventBooking.Domain.Tests/Bookings/RecoveryBookingTests.cs","beforeSha":"7cdf471358f30e9215924c5a0058bb52f7df9b165aac6b7fdb12da8925a0c3bf","afterSha":"308d2b4b7fe3894e9737388ebae40153e681614b9b696420d6fbad194472e64a","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Tests.Bookings;

/// <summary>Verifies every recovery points directly to an immutable active journey root.</summary>
public sealed class RecoveryBookingTests
{
    /// <summary>A recovery Booking copies the original root and can conclude and reopen.</summary>
    [Fact]
    public void RecoveryLifecyclePreservesOriginalRoot()
    {
        var attendeeId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var initialInvite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendeeId,
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp],
            0);
        var original = Booking.Create(
            Guid.NewGuid(), initialInvite, eventId, DateTimeOffset.UtcNow);
        var recoveryEvent = Guid.NewGuid();
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(),
            attendeeId,
            original.Id,
            DateTimeOffset.UtcNow.AddDays(2),
            ProposalFixture.LocationId,
            null,
            [recoveryEvent, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp]);

        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, recoveryEvent, DateTimeOffset.UtcNow.AddHours(1));
        recovery.Conclude();
        recovery.Reopen();

        Assert.Null(original.RecoveryOfBookingId);
        Assert.Equal(BookingStatus.Active, original.Status);
        Assert.Equal(original.Id, recovery.RecoveryOfBookingId);
        Assert.Equal(BookingStatus.Active, recovery.Status);
    }

    /// <summary>A recovery cannot point at another recovery Booking.</summary>
    [Fact]
    public void RecoveryChainIsRejected()
    {
        var attendeeId = Guid.NewGuid();
        var rootEvent = Guid.NewGuid();
        var initial = Invite.CreateInitial(
            Guid.NewGuid(),
            attendeeId,
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [rootEvent, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp],
            0);
        var root = Booking.Create(Guid.NewGuid(), initial, rootEvent, DateTimeOffset.UtcNow);
        var firstEvent = Guid.NewGuid();
        var firstInvite = Invite.CreateRecovery(
            Guid.NewGuid(),
            attendeeId,
            root.Id,
            DateTimeOffset.UtcNow.AddDays(1),
            ProposalFixture.LocationId,
            null,
            [firstEvent, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp]);
        var first = Booking.CreateRecovery(
            Guid.NewGuid(), firstInvite, root, firstEvent, DateTimeOffset.UtcNow);
        var secondEvent = Guid.NewGuid();
        var secondInvite = Invite.CreateRecovery(
            Guid.NewGuid(),
            attendeeId,
            root.Id,
            DateTimeOffset.UtcNow.AddDays(1),
            ProposalFixture.LocationId,
            null,
            [secondEvent, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp]);

        Assert.Throws<EventBooking.Domain.Common.DomainException>(() => Booking.CreateRecovery(
            Guid.NewGuid(), secondInvite, first, secondEvent, DateTimeOffset.UtcNow));
    }
}
`````

## before — tests/EventBooking.Domain.Tests/Invites/InviteLocationTests.cs — 1/1

<!-- retirement-file: {"id":57,"file":"tests/EventBooking.Domain.Tests/Invites/InviteLocationTests.cs","beforeSha":"ce178527b6e4e05d4bbe8993d4d8a28b0134326b8a0ac727493263e1bab89984","afterSha":"3d93d6d4bf165b4e3d5b9350927088dd6c7218f121a10870ece6c518f63ddac6","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Tests.Invites;

/// <summary>
/// Task 8: an invite is restricted to the locations the Coordinator chose, and every later offer
/// is drawn from that same set (FR-5.1, FR-5.2, FR-5.6, FR-5.9).
/// </summary>
public class InviteLocationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid London = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Dublin = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid Tokyo = Guid.Parse("10000000-0000-0000-0000-000000000003");

    private static readonly Guid EventA = Guid.Parse("50000001-0000-0000-0000-000000000001");
    private static readonly Guid EventB = Guid.Parse("50000002-0000-0000-0000-000000000002");
    private static readonly Guid EventC = Guid.Parse("50000003-0000-0000-0000-000000000003");
    private static readonly Guid EventD = Guid.Parse("50000004-0000-0000-0000-000000000004");
    private static readonly Guid EventE = Guid.Parse("50000005-0000-0000-0000-000000000005");
    private static readonly Guid EventF = Guid.Parse("50000006-0000-0000-0000-000000000006");

    private static Invite Initial(IEnumerable<Guid>? locationIds = null, int retryCount = 0) =>
        Invite.CreateInitial(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "hash-of-the-token",
            Now.AddDays(4),
            locationIds ?? [London, Dublin],
            [EventA, EventB, EventC],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp],
            retryCount);

    [Fact]
    public void AnInviteSnapshotsTheChosenLocations()
    {
        var invite = Initial();

        Assert.Equal([London, Dublin], invite.LocationIds.Order());
        Assert.All(invite.Locations, location => Assert.Equal(invite.Id, location.InviteId));
    }

    [Fact]
    public void AnInviteWithNoLocationsIsRefused()
    {
        var exception = Assert.Throws<DomainException>(() => Initial([]));

        Assert.Contains("at least one location", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnInviteWithMoreThanFiftyLocationsIsRefused()
    {
        var tooMany = Enumerable.Range(0, Invite.MaximumLocationCount + 1)
            .Select(_ => Guid.NewGuid())
            .ToList();

        var exception = Assert.Throws<DomainException>(() => Initial(tooMany));

        Assert.Contains(
            Invite.MaximumLocationCount.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FiftyLocationsIsAccepted()
    {
        var most = Enumerable.Range(0, Invite.MaximumLocationCount)
            .Select(_ => Guid.NewGuid())
            .ToList();

        Assert.Equal(Invite.MaximumLocationCount, Initial(most).Locations.Count);
    }

    [Fact]
    public void ARepeatedLocationIsRefused()
    {
        var exception = Assert.Throws<DomainException>(() => Initial([London, London]));

        Assert.Contains("same location twice", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AReissueCarriesTheLocationSetAndCountsTheRetry()
    {
        var original = Initial([London, Dublin, Tokyo], retryCount: 1);

        var reissued = Invite.Reissue(
            Guid.NewGuid(), original, "hash-of-the-next-token", Now.AddDays(11),
            [EventD, EventE, EventF]);

        Assert.Equal(original.LocationIds.Order(), reissued.LocationIds.Order());
        Assert.Equal(original.AttendeeId, reissued.AttendeeId);
        Assert.Equal(original.RequiredAppointmentTypeIds, reissued.RequiredAppointmentTypeIds);
        Assert.Equal(2, reissued.RetryCount);
        Assert.Equal(InviteStatus.Pending, reissued.Status);
        Assert.Equal([EventD, EventE, EventF], reissued.OfferedEventIds);
        Assert.All(reissued.Locations, location => Assert.Equal(reissued.Id, location.InviteId));
    }

    [Fact]
    public void AReissueOfARecoveryInviteStaysARecoveryInvite()
    {
        var bookingId = Guid.NewGuid();
        var original = Recovery(bookingId);

        var reissued = Invite.Reissue(
            Guid.NewGuid(), original, "hash-of-the-next-token", Now.AddDays(11),
            [EventD, EventE, EventF]);

        Assert.Equal(bookingId, reissued.RecoveryOfBookingId);
        Assert.Equal(1, reissued.RetryCount);
    }

    [Fact]
    public void ARecoveryInviteDefaultsToTheOriginalBookingsLocation()
    {
        var invite = Recovery(Guid.NewGuid());

        Assert.Equal([London], invite.LocationIds);
        Assert.Equal(0, invite.RetryCount);
    }

    [Fact]
    public void ARecoveryInviteUnionsAdditionalLocationsWithoutDuplicates()
    {
        var invite = Recovery(Guid.NewGuid(), [Dublin, London, Tokyo, Dublin]);

        Assert.Equal([London, Dublin, Tokyo], invite.LocationIds.Order());
    }

    [Fact]
    public void AnInviteSnapshotsMoreThanThreeRequirements()
    {
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "hash-of-the-token",
            Now.AddDays(4),
            [London],
            [EventA, EventB, EventC],
            AppointmentTypeIds.All,
            0);

        Assert.Equal(AppointmentTypeIds.All.Order(), invite.RequiredAppointmentTypeIds);
    }

    private static Invite Recovery(Guid bookingId, IEnumerable<Guid>? additionalLocationIds = null) =>
        Invite.CreateRecovery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            bookingId,
            "hash-of-the-token",
            Now.AddDays(4),
            London,
            additionalLocationIds,
            [EventA, EventB, EventC],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
}
`````

## after — tests/EventBooking.Domain.Tests/Invites/InviteLocationTests.cs — 1/1

<!-- retirement-file: {"id":57,"file":"tests/EventBooking.Domain.Tests/Invites/InviteLocationTests.cs","beforeSha":"ce178527b6e4e05d4bbe8993d4d8a28b0134326b8a0ac727493263e1bab89984","afterSha":"3d93d6d4bf165b4e3d5b9350927088dd6c7218f121a10870ece6c518f63ddac6","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Tests.Invites;

/// <summary>
/// Task 8: an invite is restricted to the locations the Coordinator chose, and every later offer
/// is drawn from that same set (FR-5.1, FR-5.2, FR-5.6, FR-5.9).
/// </summary>
public class InviteLocationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid London = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Dublin = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid Tokyo = Guid.Parse("10000000-0000-0000-0000-000000000003");

    private static readonly Guid EventA = Guid.Parse("50000001-0000-0000-0000-000000000001");
    private static readonly Guid EventB = Guid.Parse("50000002-0000-0000-0000-000000000002");
    private static readonly Guid EventC = Guid.Parse("50000003-0000-0000-0000-000000000003");
    private static readonly Guid EventD = Guid.Parse("50000004-0000-0000-0000-000000000004");
    private static readonly Guid EventE = Guid.Parse("50000005-0000-0000-0000-000000000005");
    private static readonly Guid EventF = Guid.Parse("50000006-0000-0000-0000-000000000006");

    private static Invite Initial(IEnumerable<Guid>? locationIds = null, int retryCount = 0) =>
        Invite.CreateInitial(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddDays(4),
            locationIds ?? [London, Dublin],
            [EventA, EventB, EventC],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp],
            retryCount);

    [Fact]
    public void AnInviteSnapshotsTheChosenLocations()
    {
        var invite = Initial();

        Assert.Equal([London, Dublin], invite.LocationIds.Order());
        Assert.All(invite.Locations, location => Assert.Equal(invite.Id, location.InviteId));
    }

    [Fact]
    public void AnInviteWithNoLocationsIsRefused()
    {
        var exception = Assert.Throws<DomainException>(() => Initial([]));

        Assert.Contains("at least one location", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnInviteWithMoreThanFiftyLocationsIsRefused()
    {
        var tooMany = Enumerable.Range(0, Invite.MaximumLocationCount + 1)
            .Select(_ => Guid.NewGuid())
            .ToList();

        var exception = Assert.Throws<DomainException>(() => Initial(tooMany));

        Assert.Contains(
            Invite.MaximumLocationCount.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FiftyLocationsIsAccepted()
    {
        var most = Enumerable.Range(0, Invite.MaximumLocationCount)
            .Select(_ => Guid.NewGuid())
            .ToList();

        Assert.Equal(Invite.MaximumLocationCount, Initial(most).Locations.Count);
    }

    [Fact]
    public void ARepeatedLocationIsRefused()
    {
        var exception = Assert.Throws<DomainException>(() => Initial([London, London]));

        Assert.Contains("same location twice", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AReissueCarriesTheLocationSetAndCountsTheRetry()
    {
        var original = Initial([London, Dublin, Tokyo], retryCount: 1);

        var reissued = Invite.Reissue(
            Guid.NewGuid(), original, Now.AddDays(11),
            [EventD, EventE, EventF]);

        Assert.Equal(original.LocationIds.Order(), reissued.LocationIds.Order());
        Assert.Equal(original.AttendeeId, reissued.AttendeeId);
        Assert.Equal(original.RequiredAppointmentTypeIds, reissued.RequiredAppointmentTypeIds);
        Assert.Equal(2, reissued.RetryCount);
        Assert.Equal(InviteStatus.Pending, reissued.Status);
        Assert.Equal([EventD, EventE, EventF], reissued.OfferedEventIds);
        Assert.All(reissued.Locations, location => Assert.Equal(reissued.Id, location.InviteId));
    }

    [Fact]
    public void AReissueOfARecoveryInviteStaysARecoveryInvite()
    {
        var bookingId = Guid.NewGuid();
        var original = Recovery(bookingId);

        var reissued = Invite.Reissue(
            Guid.NewGuid(), original, Now.AddDays(11),
            [EventD, EventE, EventF]);

        Assert.Equal(bookingId, reissued.RecoveryOfBookingId);
        Assert.Equal(1, reissued.RetryCount);
    }

    [Fact]
    public void ARecoveryInviteDefaultsToTheOriginalBookingsLocation()
    {
        var invite = Recovery(Guid.NewGuid());

        Assert.Equal([London], invite.LocationIds);
        Assert.Equal(0, invite.RetryCount);
    }

    [Fact]
    public void ARecoveryInviteUnionsAdditionalLocationsWithoutDuplicates()
    {
        var invite = Recovery(Guid.NewGuid(), [Dublin, London, Tokyo, Dublin]);

        Assert.Equal([London, Dublin, Tokyo], invite.LocationIds.Order());
    }

    [Fact]
    public void AnInviteSnapshotsMoreThanThreeRequirements()
    {
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddDays(4),
            [London],
            [EventA, EventB, EventC],
            AppointmentTypeIds.All,
            0);

        Assert.Equal(AppointmentTypeIds.All.Order(), invite.RequiredAppointmentTypeIds);
    }

    private static Invite Recovery(Guid bookingId, IEnumerable<Guid>? additionalLocationIds = null) =>
        Invite.CreateRecovery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            bookingId,
            Now.AddDays(4),
            London,
            additionalLocationIds,
            [EventA, EventB, EventC],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
}
`````

## before — tests/EventBooking.Domain.Tests/Invites/InviteRequirementSnapshotTests.cs — 1/1

<!-- retirement-file: {"id":58,"file":"tests/EventBooking.Domain.Tests/Invites/InviteRequirementSnapshotTests.cs","beforeSha":"a99b3194121189784c72d0e5f72f24fcd378551ba41488bf954e9448d0806eaf","afterSha":"293f26421c1019c6cbcdbe43031200711fc37fd41aca9c67c04e9e6d5e4add69","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Tests.Invites;

/// <summary>Verifies initial and recovery Invites own immutable requirement snapshots.</summary>
public sealed class InviteRequirementSnapshotTests
{
    private static readonly Guid[] Options = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];

    /// <summary>An initial Invite snapshots distinct known requirements in stable order.</summary>
    [Fact]
    public void InitialInviteSnapshotsRequirements()
    {
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "hash",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            Options,
            [AppointmentTypeIds.UniformFitting, AppointmentTypeIds.DrugAndAlcoholTesting],
            0);

        Assert.Null(invite.RecoveryOfBookingId);
        Assert.Equal(
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            invite.RequiredAppointmentTypeIds);
    }

    /// <summary>A recovery Invite retains its root Booking and can be explicitly cancelled.</summary>
    [Fact]
    public void RecoveryInviteLinksTheRootAndCancels()
    {
        var root = Guid.NewGuid();
        var invite = Invite.CreateRecovery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            root,
            "hash",
            DateTimeOffset.UtcNow.AddDays(1),
            ProposalFixture.LocationId,
            null,
            Options,
            [AppointmentTypeIds.MedicalCheckUp]);

        invite.CancelRecovery();

        Assert.Equal(root, invite.RecoveryOfBookingId);
        Assert.Equal(InviteStatus.Cancelled, invite.Status);
    }

    /// <summary>Empty, duplicate, unknown, and oversized snapshots are rejected.</summary>
    [Theory]
    [MemberData(nameof(InvalidSnapshots))]
    public void InvalidSnapshotsCannotBeCreated(Guid[] snapshot)
    {
        Assert.Throws<DomainException>(() => Invite.CreateInitial(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "hash",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            Options,
            snapshot,
            0));
    }

    /// <summary>Provides every invalid snapshot shape.</summary>
    public static TheoryData<Guid[]> InvalidSnapshots => new()
    {
        { [] },
        { [AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.MedicalCheckUp] },
        { [Guid.NewGuid()] },
        { [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
            AppointmentTypeIds.UniformFitting, AppointmentTypeIds.DrugAndAlcoholTesting] },
    };
}
`````
