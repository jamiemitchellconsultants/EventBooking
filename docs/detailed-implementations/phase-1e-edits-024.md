# 01e — Location-restricted invites and closed attendee transitions, edits 24 (Task 8)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs — 1/1

<!-- retirement-file: {"id":61,"file":"tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs","beforeSha":"ede04c8bb2edd46c808c28d956c59c4fc752097ccef500d93922b6a6a7b9cb6a","afterSha":"a557857f1de30947457d12e5fd590c3942f2c9e5a1dba25fc0cfdace7fde46af","side":"after","part":1,"parts":1} -->

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

/// <summary>Verifies template-aware retries, token rotation, and stale-state conflicts.</summary>
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
    private readonly RotatingTokenService _tokens = new();
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

    /// <summary>Booking-confirmation retry rotates the management hash and sends the right template.</summary>
    [Fact]
    public async Task BookingConfirmationRetryRotatesTheManageHashAndUsesTheConfirmationTemplate()
    {
        var inviteId = Guid.NewGuid();
        var inviteToken = _tokens.Issue(inviteId);
        var invite = Invite.CreateInitial(
            inviteId,
            _attendee.Id,
            inviteToken.TokenHash,
            _clock.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            _events.Items.Select(eventItem => eventItem.Id),
            _attendee.RequiredAppointmentTypeIds,
            0);
        _invites.Add(invite);
        _attendee.MarkInvited(ProposalFixture.Now);
        var bookingId = Guid.NewGuid();
        var manage = _tokens.Issue(bookingId);
        var booking = Booking.Create(bookingId, invite, _events.Items[0].Id, manage.TokenHash, _clock.UtcNow);
        _bookings.Add(booking);
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), bookingId, AppointmentTypeIds.DrugAndAlcoholTesting));
        invite.MarkUsed();
        _attendee.MarkBooked(ProposalFixture.Now);
        var oldHash = booking.ManageTokenHash;
        AddFailedDelivery(EmailTemplate.BookingConfirmation, bookingId: bookingId);

        var result = await Handler().HandleAsync(
            new RetryEmailCommand(Coordinator, _attendee.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Sent", result.Value.DeliveryStatus);
        Assert.NotEqual(oldHash, booking.ManageTokenHash);
        Assert.Contains($"/manage/token-for-{booking.Id:N}-", _sender.LastOf(EmailTemplate.BookingConfirmation).TextBody);
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

    /// <summary>Invite retry rotates the pending invite hash and keeps the invite template.</summary>
    [Fact]
    public async Task AttendeeInviteRetryRotatesThePendingInviteHash()
    {
        var inviteId = Guid.NewGuid();
        var issued = _tokens.Issue(inviteId);
        var invite = Invite.CreateInitial(
            inviteId,
            _attendee.Id,
            issued.TokenHash,
            _clock.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            _events.Items.Select(eventItem => eventItem.Id),
            _attendee.RequiredAppointmentTypeIds,
            0);
        _invites.Add(invite);
        _attendee.MarkInvited(ProposalFixture.Now);
        AddFailedDelivery(EmailTemplate.AttendeeInvite, inviteId: invite.Id);
        var oldHash = invite.TokenHash;

        var result = await Handler().HandleAsync(
            new RetryEmailCommand(Coordinator, _attendee.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(oldHash, invite.TokenHash);
        Assert.Equal(EmailTemplate.AttendeeInvite, Assert.Single(_sender.Sent).Template);
    }

    /// <summary>Attendee re-invite recovery preserves the reminder template.</summary>
    [Fact]
    public async Task AttendeeReinviteRetryUsesTheReminderTemplate()
    {
        var inviteId = Guid.NewGuid();
        var issued = _tokens.Issue(inviteId);
        var invite = Invite.CreateInitial(
            inviteId,
            _attendee.Id,
            issued.TokenHash,
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
        var issued = _tokens.Issue(inviteId);
        var invite = Invite.CreateInitial(
            inviteId,
            _attendee.Id,
            issued.TokenHash,
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
        var inviteToken = _tokens.Issue(inviteId);
        var invite = Invite.CreateInitial(
            inviteId,
            _attendee.Id,
            inviteToken.TokenHash,
            _clock.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            _events.Items.Select(eventItem => eventItem.Id),
            _attendee.RequiredAppointmentTypeIds,
            0);
        _invites.Add(invite);
        _attendee.MarkInvited(ProposalFixture.Now);
        var bookingId = Guid.NewGuid();
        var manage = _tokens.Issue(bookingId);
        var booking = Booking.Create(bookingId, invite, _events.Items[0].Id, manage.TokenHash, _clock.UtcNow);
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
        var inviteToken = _tokens.Issue(inviteId);
        var invite = Invite.CreateInitial(
            inviteId,
            _attendee.Id,
            inviteToken.TokenHash,
            _clock.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            _events.Items.Select(eventItem => eventItem.Id),
            _attendee.RequiredAppointmentTypeIds,
            0);
        _invites.Add(invite);
        _attendee.MarkInvited(ProposalFixture.Now);
        var bookingId = Guid.NewGuid();
        var manage = _tokens.Issue(bookingId);
        var booking = Booking.Create(bookingId, invite, _events.Items[0].Id, manage.TokenHash, _clock.UtcNow);
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
        var issued = _tokens.Issue(inviteId);
        var invite = Invite.CreateInitial(
            inviteId,
            _attendee.Id,
            issued.TokenHash,
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
        var issued = _tokens.Issue(inviteId);
        var invite = Invite.CreateInitial(
            inviteId,
            _attendee.Id,
            issued.TokenHash,
            _clock.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            _events.Items.Select(eventItem => eventItem.Id),
            _attendee.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(Guid.NewGuid(), invite, eventId, "hash", _clock.UtcNow);
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

/// <summary>Issues distinct deterministic test tokens so rotation is observable.</summary>
internal sealed class RotatingTokenService : ITokenService
{
    private int _counter;

    /// <inheritdoc />
    public IssuedToken Issue(Guid entityId)
    {
        var token = $"token-for-{entityId:N}-{++_counter}";
        return new IssuedToken(token, Hash(token));
    }

    /// <inheritdoc />
    public bool TryRead(string? token, out Guid entityId)
    {
        entityId = Guid.Empty;
        return token is not null
            && token.StartsWith("token-for-", StringComparison.Ordinal)
            && Guid.TryParseExact(token["token-for-".Length..].Split('-')[0], "N", out entityId);
    }

    /// <inheritdoc />
    public string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
`````

## before — tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs — 1/1

<!-- retirement-file: {"id":62,"file":"tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs","beforeSha":"319566d5f29bf4f7dbf00bb2f550068027bb9f8d011f9f4d8fe1a82424d6c215","afterSha":"da7b01c33b25a955b16a8eea9b3b8b6f36da098f79106891a7dc3258008b3e92","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Notifications;

/// <summary>Verifies attendee emails name exactly their persisted requirement snapshot.</summary>
public sealed class SnapshotEmailAuthorityTests
{
    private static readonly Attendee Attendee = Attendee.Create(
        Guid.NewGuid(), "Amara", "amara@example.com",
        AttendeeGroup.Define(
            AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting]));
    private static readonly Event Event = EventFixture.Create(
        Guid.NewGuid(), new EventWindow(new DateOnly(2026, 10, 10), new TimeOnly(9, 0), 240),
        new Dictionary<Guid, int>
        {
            [AppointmentTypeIds.DrugAndAlcoholTesting] = 10,
            [AppointmentTypeIds.MedicalCheckUp] = 10,
            [AppointmentTypeIds.UniformFitting] = 10,
        });
    private static readonly AttendeePortalOptions Portal = new(
        "https://booking.example", "recruitment@example.com");

    /// <summary>An Invite names only a one-type snapshot and uses singular recovery copy.</summary>
    [Fact]
    public void RecoveryInviteUsesSnapshotAndSingularCopy()
    {
        var email = AttendeeEmailComposer.Invite(
            Attendee,
            [AppointmentTypeIds.MedicalCheckUp],
            [Event, Event, Event],
            "https://booking.example/book/token",
            isReinvite: false,
            isRecovery: true);

        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.DoesNotContain("Uniform Fitting", email.TextBody);
        Assert.Contains("missed appointment", email.TextBody, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Booking confirmation names a two-type Booking snapshot, not all Attendee types.</summary>
    [Fact]
    public void ConfirmationUsesBookingSnapshot()
    {
        var email = AttendeeEmailComposer.BookingConfirmation(
            Attendee,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            Event,
            "https://booking.example/manage/token",
            Portal);

        Assert.Contains("Drug & Alcohol Testing", email.TextBody);
        Assert.Contains("Uniform Fitting", email.TextBody);
        Assert.DoesNotContain("Medical Check-up", email.TextBody);
    }

    /// <summary>Cancellation names only the affected Booking snapshot.</summary>
    [Fact]
    public void CancellationUsesAffectedBookingSnapshot()
    {
        var email = AttendeeEmailComposer.EventCancelled(
            Attendee, [AppointmentTypeIds.MedicalCheckUp], Event);

        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.DoesNotContain("Drug & Alcohol Testing", email.TextBody);
    }

    /// <summary>A three-type Invite names every snapshot type in code order.</summary>
    [Fact]
    public void ThreeTypeInviteNamesEverySnapshotType()
    {
        var email = AttendeeEmailComposer.Invite(
            Attendee,
            [AppointmentTypeIds.UniformFitting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.DrugAndAlcoholTesting],
            [Event, Event, Event],
            "https://booking.example/book/token",
            isReinvite: true,
            isRecovery: false);

        Assert.Contains("Drug & Alcohol Testing", email.TextBody);
        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.Contains("Uniform Fitting", email.TextBody);
        Assert.Contains("We have not heard back", email.TextBody);
    }

    /// <summary>A two-type cancellation names both snapshot types with plural copy.</summary>
    [Fact]
    public void TwoTypeCancellationUsesPluralCopy()
    {
        var email = AttendeeEmailComposer.EventCancelled(
            Attendee,
            [AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.DrugAndAlcoholTesting],
            Event);

        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.Contains("Drug & Alcohol Testing", email.TextBody);
        Assert.DoesNotContain("Uniform Fitting", email.TextBody);
        Assert.Contains("your appointments on", email.TextBody);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs — 1/1

<!-- retirement-file: {"id":62,"file":"tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs","beforeSha":"319566d5f29bf4f7dbf00bb2f550068027bb9f8d011f9f4d8fe1a82424d6c215","afterSha":"da7b01c33b25a955b16a8eea9b3b8b6f36da098f79106891a7dc3258008b3e92","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Notifications;

/// <summary>Verifies attendee emails name exactly their persisted requirement snapshot.</summary>
public sealed class SnapshotEmailAuthorityTests
{
    private static readonly Attendee Attendee = Attendee.Create(
        Guid.NewGuid(),
        "Amara",
        "amara@example.com",
        AttendeeGroup.Define(
            AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting]),
        ProposalFixture.Now);
    private static readonly Event Event = EventFixture.Create(
        Guid.NewGuid(), new EventWindow(new DateOnly(2026, 10, 10), new TimeOnly(9, 0), 240),
        new Dictionary<Guid, int>
        {
            [AppointmentTypeIds.DrugAndAlcoholTesting] = 10,
            [AppointmentTypeIds.MedicalCheckUp] = 10,
            [AppointmentTypeIds.UniformFitting] = 10,
        });
    private static readonly AttendeePortalOptions Portal = new(
        "https://booking.example", "recruitment@example.com");

    /// <summary>An Invite names only a one-type snapshot and uses singular recovery copy.</summary>
    [Fact]
    public void RecoveryInviteUsesSnapshotAndSingularCopy()
    {
        var email = AttendeeEmailComposer.Invite(
            Attendee,
            [AppointmentTypeIds.MedicalCheckUp],
            [Event, Event, Event],
            "https://booking.example/book/token",
            isReinvite: false,
            isRecovery: true);

        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.DoesNotContain("Uniform Fitting", email.TextBody);
        Assert.Contains("missed appointment", email.TextBody, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Booking confirmation names a two-type Booking snapshot, not all Attendee types.</summary>
    [Fact]
    public void ConfirmationUsesBookingSnapshot()
    {
        var email = AttendeeEmailComposer.BookingConfirmation(
            Attendee,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            Event,
            "https://booking.example/manage/token",
            Portal);

        Assert.Contains("Drug & Alcohol Testing", email.TextBody);
        Assert.Contains("Uniform Fitting", email.TextBody);
        Assert.DoesNotContain("Medical Check-up", email.TextBody);
    }

    /// <summary>Cancellation names only the affected Booking snapshot.</summary>
    [Fact]
    public void CancellationUsesAffectedBookingSnapshot()
    {
        var email = AttendeeEmailComposer.EventCancelled(
            Attendee, [AppointmentTypeIds.MedicalCheckUp], Event);

        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.DoesNotContain("Drug & Alcohol Testing", email.TextBody);
    }

    /// <summary>A three-type Invite names every snapshot type in code order.</summary>
    [Fact]
    public void ThreeTypeInviteNamesEverySnapshotType()
    {
        var email = AttendeeEmailComposer.Invite(
            Attendee,
            [AppointmentTypeIds.UniformFitting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.DrugAndAlcoholTesting],
            [Event, Event, Event],
            "https://booking.example/book/token",
            isReinvite: true,
            isRecovery: false);

        Assert.Contains("Drug & Alcohol Testing", email.TextBody);
        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.Contains("Uniform Fitting", email.TextBody);
        Assert.Contains("We have not heard back", email.TextBody);
    }

    /// <summary>A two-type cancellation names both snapshot types with plural copy.</summary>
    [Fact]
    public void TwoTypeCancellationUsesPluralCopy()
    {
        var email = AttendeeEmailComposer.EventCancelled(
            Attendee,
            [AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.DrugAndAlcoholTesting],
            Event);

        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.Contains("Drug & Alcohol Testing", email.TextBody);
        Assert.DoesNotContain("Uniform Fitting", email.TextBody);
        Assert.Contains("your appointments on", email.TextBody);
    }
}
`````

## before — tests/EventBooking.Domain.Tests/AttendeeGroups/AttendeeGroupAttendeeTests.cs — 1/1

<!-- retirement-file: {"id":63,"file":"tests/EventBooking.Domain.Tests/AttendeeGroups/AttendeeGroupAttendeeTests.cs","beforeSha":"156767dfdcdca46316d3b3df83428f815db2a1059c78591e252b70a9b02e1637","afterSha":"e70e0a1112be01382eb3ddc6e25e5d77b47c31eab19e71dcfcafe711e2cd4e83","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Common;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Domain.Tests.AttendeeGroups;

/// <summary>Verifies Attendee Group invariants and Attendee requirement derivation.</summary>
public sealed class AttendeeGroupAttendeeTests
{
    /// <summary>Every approved mapping produces exactly the Issue 91 requirement set.</summary>
    [Theory]
    [MemberData(nameof(ApprovedMappings))]
    public void ApprovedMappingsAreDerived(
        Guid groupId,
        string code,
        string name,
        Guid[] expected)
    {
        var group = AttendeeGroup.Define(groupId, code, name, true, expected);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "amara@example.com", group);

        Assert.Equal(groupId, attendee.AttendeeGroupId);
        Assert.Equal(expected.Order(), attendee.RequiredAppointmentTypeIds.Order());
    }

    /// <summary>Changing between equal mappings changes only the assigned group.</summary>
    [Fact]
    public void SetEquivalentAssignmentPreservesTheMaterializedSet()
    {
        var engineering = AttendeeGroup.Define(
            AttendeeGroupIds.Engineering,
            "ENGINEERING",
            "Engineering",
            true,
            [AppointmentTypeIds.MedicalCheckUp]);
        var groundOperations = AttendeeGroup.Define(
            AttendeeGroupIds.GroundOperationsAgent,
            "GROUND_OPERATIONS_AGENT",
            "Ground Operations Agent",
            true,
            [AppointmentTypeIds.MedicalCheckUp]);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "amara@example.com", engineering);

        var changed = attendee.AssignAttendeeGroup(groundOperations);

        Assert.False(changed);
        Assert.Equal(AttendeeGroupIds.GroundOperationsAgent, attendee.AttendeeGroupId);
        Assert.Equal([AppointmentTypeIds.MedicalCheckUp], attendee.RequiredAppointmentTypeIds);
    }

    /// <summary>Inactive, empty, duplicate, and unknown mappings cannot become assignment authority.</summary>
    [Fact]
    public void InvalidReferenceDataIsRejected()
    {
        Assert.Throws<DomainException>(() => AttendeeGroup.Define(
            Guid.NewGuid(), "CABIN_CREW", "Cabin Crew", false,
            [AppointmentTypeIds.DrugAndAlcoholTesting]));
        Assert.Throws<DomainException>(() => AttendeeGroup.Define(
            Guid.NewGuid(), "CABIN_CREW", "Cabin Crew", true, []));
        Assert.Throws<DomainException>(() => AttendeeGroup.Define(
            Guid.NewGuid(), "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.MedicalCheckUp]));
        Assert.Throws<DomainException>(() => AttendeeGroup.Define(
            Guid.NewGuid(), "not-canonical", "Cabin Crew", true, [Guid.NewGuid()]));
    }

    /// <summary>Provides the exact five approved group mappings.</summary>
    public static TheoryData<Guid, string, string, Guid[]> ApprovedMappings => new()
    {
        { AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew",
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.UniformFitting] },
        { AttendeeGroupIds.Pilots, "PILOTS", "Pilots",
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting] },
        { AttendeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT", "Ground Operations Agent",
            [AppointmentTypeIds.MedicalCheckUp] },
        { AttendeeGroupIds.Engineering, "ENGINEERING", "Engineering",
            [AppointmentTypeIds.MedicalCheckUp] },
        { AttendeeGroupIds.GroundTransportServices, "GROUND_TRANSPORT_SERVICES", "Ground Transport Services",
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.UniformFitting] },
    };
}
`````

## after — tests/EventBooking.Domain.Tests/AttendeeGroups/AttendeeGroupAttendeeTests.cs — 1/1

<!-- retirement-file: {"id":63,"file":"tests/EventBooking.Domain.Tests/AttendeeGroups/AttendeeGroupAttendeeTests.cs","beforeSha":"156767dfdcdca46316d3b3df83428f815db2a1059c78591e252b70a9b02e1637","afterSha":"e70e0a1112be01382eb3ddc6e25e5d77b47c31eab19e71dcfcafe711e2cd4e83","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Common;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Domain.Tests.AttendeeGroups;

/// <summary>Verifies Attendee Group invariants and Attendee requirement derivation.</summary>
public sealed class AttendeeGroupAttendeeTests
{
    /// <summary>Every approved mapping produces exactly the Issue 91 requirement set.</summary>
    [Theory]
    [MemberData(nameof(ApprovedMappings))]
    public void ApprovedMappingsAreDerived(
        Guid groupId,
        string code,
        string name,
        Guid[] expected)
    {
        var group = AttendeeGroup.Define(groupId, code, name, true, expected);
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "amara@example.com",
            group,
            ProposalFixture.Now);

        Assert.Equal(groupId, attendee.AttendeeGroupId);
        Assert.Equal(expected.Order(), attendee.RequiredAppointmentTypeIds.Order());
    }

    /// <summary>Changing between equal mappings changes only the assigned group.</summary>
    [Fact]
    public void SetEquivalentAssignmentPreservesTheMaterializedSet()
    {
        var engineering = AttendeeGroup.Define(
            AttendeeGroupIds.Engineering,
            "ENGINEERING",
            "Engineering",
            true,
            [AppointmentTypeIds.MedicalCheckUp]);
        var groundOperations = AttendeeGroup.Define(
            AttendeeGroupIds.GroundOperationsAgent,
            "GROUND_OPERATIONS_AGENT",
            "Ground Operations Agent",
            true,
            [AppointmentTypeIds.MedicalCheckUp]);
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "amara@example.com",
            engineering,
            ProposalFixture.Now);

        var changed = attendee.AssignAttendeeGroup(groundOperations);

        Assert.False(changed);
        Assert.Equal(AttendeeGroupIds.GroundOperationsAgent, attendee.AttendeeGroupId);
        Assert.Equal([AppointmentTypeIds.MedicalCheckUp], attendee.RequiredAppointmentTypeIds);
    }

    /// <summary>Inactive, empty, duplicate, and unknown mappings cannot become assignment authority.</summary>
    [Fact]
    public void InvalidReferenceDataIsRejected()
    {
        Assert.Throws<DomainException>(() => AttendeeGroup.Define(
            Guid.NewGuid(), "CABIN_CREW", "Cabin Crew", false,
            [AppointmentTypeIds.DrugAndAlcoholTesting]));
        Assert.Throws<DomainException>(() => AttendeeGroup.Define(
            Guid.NewGuid(), "CABIN_CREW", "Cabin Crew", true, []));
        Assert.Throws<DomainException>(() => AttendeeGroup.Define(
            Guid.NewGuid(), "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.MedicalCheckUp]));
        Assert.Throws<DomainException>(() => AttendeeGroup.Define(
            Guid.NewGuid(), "not-canonical", "Cabin Crew", true, [Guid.NewGuid()]));
    }

    /// <summary>Provides the exact five approved group mappings.</summary>
    public static TheoryData<Guid, string, string, Guid[]> ApprovedMappings => new()
    {
        { AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew",
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.UniformFitting] },
        { AttendeeGroupIds.Pilots, "PILOTS", "Pilots",
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting] },
        { AttendeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT", "Ground Operations Agent",
            [AppointmentTypeIds.MedicalCheckUp] },
        { AttendeeGroupIds.Engineering, "ENGINEERING", "Engineering",
            [AppointmentTypeIds.MedicalCheckUp] },
        { AttendeeGroupIds.GroundTransportServices, "GROUND_TRANSPORT_SERVICES", "Ground Transport Services",
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.UniformFitting] },
    };
}
`````

## before — tests/EventBooking.Domain.Tests/Attendees/AttendeeStatusTests.cs — 1/1

<!-- retirement-file: {"id":64,"file":"tests/EventBooking.Domain.Tests/Attendees/AttendeeStatusTests.cs","beforeSha":"11f1c7504ce1cc37dc5b7f3e2b1002b5ddde01020ec4bfd61b8bd0fb1207e319","afterSha":"682cd3e1623357d3f8efce9b40f61f53d5bfc399fa2da7b9a81616bba4780c8b","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Common;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Domain.Tests.Attendees;

public class AttendeeStatusTests
{
    /// <summary>Builds a DAT-only group; lifecycle tests need a mapping, not an identity.</summary>
    private static AttendeeGroup DatOnly() =>
        AttendeeGroup.Define(
            Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);

    private static Attendee NewAttendee() =>
        Attendee.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", DatOnly());

    private static Attendee InvitedAttendee()
    {
        var attendee = NewAttendee();
        attendee.MarkInvited();
        return attendee;
    }

    [Fact]
    public void ANotYetInvitedAttendeeCanBeInvited()
    {
        var attendee = NewAttendee();

        attendee.MarkInvited();

        Assert.Equal(AttendeeStatus.Invited, attendee.Status);
    }

    [Fact]
    public void AAttendeeWithNoEligibleEventsBecomesAwaitingAvailability()
    {
        var attendee = NewAttendee();

        attendee.MarkAwaitingAvailability();

        Assert.Equal(AttendeeStatus.AwaitingAvailability, attendee.Status);
    }

    [Fact]
    public void AnAwaitingAttendeeCanBeInvitedOnceEventsAppear()
    {
        var attendee = NewAttendee();
        attendee.MarkAwaitingAvailability();

        attendee.MarkInvited();

        Assert.Equal(AttendeeStatus.Invited, attendee.Status);
    }

    [Fact]
    public void ReInvitingAnAlreadyInvitedAttendeeIsAllowed()
    {
        var attendee = InvitedAttendee();

        attendee.MarkInvited();

        Assert.Equal(AttendeeStatus.Invited, attendee.Status);
    }

    [Fact]
    public void OnlyAnInvitedAttendeeCanBecomeBooked()
    {
        var attendee = InvitedAttendee();
        attendee.MarkBooked();
        Assert.Equal(AttendeeStatus.Booked, attendee.Status);

        var notInvited = NewAttendee();
        var ex = Assert.Throws<DomainException>(() => notInvited.MarkBooked());
        Assert.Equal("A attendee cannot move from NotYetInvited to Booked.", ex.Message);
    }

    [Fact]
    public void OnlyAnInvitedAttendeeCanRunOutOfRetries()
    {
        var attendee = InvitedAttendee();
        attendee.MarkNoResponse();
        Assert.Equal(AttendeeStatus.NoResponseNeedsFollowUp, attendee.Status);

        var booked = InvitedAttendee();
        booked.MarkBooked();
        Assert.Throws<DomainException>(() => booked.MarkNoResponse());
    }

    [Fact]
    public void AFollowUpAttendeeCanBeManuallyReInvited()
    {
        var attendee = InvitedAttendee();
        attendee.MarkNoResponse();

        attendee.MarkInvited();

        Assert.Equal(AttendeeStatus.Invited, attendee.Status);
    }

    [Fact]
    public void CancellingABookingReturnsTheAttendeeToNotYetInvited()
    {
        var attendee = InvitedAttendee();
        attendee.MarkBooked();

        attendee.ResetToNotYetInvited();

        Assert.Equal(AttendeeStatus.NotYetInvited, attendee.Status);
    }

    [Fact]
    public void ANotYetInvitedAttendeeCannotBeResetAgain()
    {
        var attendee = NewAttendee();

        var ex = Assert.Throws<DomainException>(() => attendee.ResetToNotYetInvited());
        Assert.Equal("A attendee cannot move from NotYetInvited to NotYetInvited.", ex.Message);
    }

}
`````

## after — tests/EventBooking.Domain.Tests/Attendees/AttendeeStatusTests.cs — 1/1

<!-- retirement-file: {"id":64,"file":"tests/EventBooking.Domain.Tests/Attendees/AttendeeStatusTests.cs","beforeSha":"11f1c7504ce1cc37dc5b7f3e2b1002b5ddde01020ec4bfd61b8bd0fb1207e319","afterSha":"682cd3e1623357d3f8efce9b40f61f53d5bfc399fa2da7b9a81616bba4780c8b","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Common;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Domain.Tests.Attendees;

public class AttendeeStatusTests
{
    /// <summary>Builds a DAT-only group; lifecycle tests need a mapping, not an identity.</summary>
    private static AttendeeGroup DatOnly() =>
        AttendeeGroup.Define(
            Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);

    private static Attendee NewAttendee() =>
        Attendee.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", DatOnly(), ProposalFixture.Now);

    private static Attendee InvitedAttendee()
    {
        var attendee = NewAttendee();
        attendee.MarkInvited(ProposalFixture.Now);
        return attendee;
    }

    [Fact]
    public void ANotYetInvitedAttendeeCanBeInvited()
    {
        var attendee = NewAttendee();

        attendee.MarkInvited(ProposalFixture.Now);

        Assert.Equal(AttendeeStatus.Invited, attendee.Status);
    }

    [Fact]
    public void AAttendeeWithNoEligibleEventsBecomesAwaitingAvailability()
    {
        var attendee = NewAttendee();

        attendee.MarkAwaitingAvailability(ProposalFixture.Now);

        Assert.Equal(AttendeeStatus.AwaitingAvailability, attendee.Status);
    }

    [Fact]
    public void AnAwaitingAttendeeCanBeInvitedOnceEventsAppear()
    {
        var attendee = NewAttendee();
        attendee.MarkAwaitingAvailability(ProposalFixture.Now);

        attendee.MarkInvited(ProposalFixture.Now);

        Assert.Equal(AttendeeStatus.Invited, attendee.Status);
    }

    [Fact]
    public void ReInvitingAnAlreadyInvitedAttendeeIsAllowed()
    {
        var attendee = InvitedAttendee();

        attendee.MarkInvited(ProposalFixture.Now);

        Assert.Equal(AttendeeStatus.Invited, attendee.Status);
    }

    [Fact]
    public void OnlyAnInvitedAttendeeCanBecomeBooked()
    {
        var attendee = InvitedAttendee();
        attendee.MarkBooked(ProposalFixture.Now);
        Assert.Equal(AttendeeStatus.Booked, attendee.Status);

        var notInvited = NewAttendee();
        var ex = Assert.Throws<DomainException>(() => notInvited.MarkBooked(ProposalFixture.Now));
        Assert.Equal("A attendee cannot move from NotYetInvited to Booked.", ex.Message);
    }

    [Fact]
    public void OnlyAnInvitedAttendeeCanRunOutOfRetries()
    {
        var attendee = InvitedAttendee();
        attendee.MarkNoResponse(ProposalFixture.Now);
        Assert.Equal(AttendeeStatus.NoResponseNeedsFollowUp, attendee.Status);

        var booked = InvitedAttendee();
        booked.MarkBooked(ProposalFixture.Now);
        Assert.Throws<DomainException>(() => booked.MarkNoResponse(ProposalFixture.Now));
    }

    [Fact]
    public void AFollowUpAttendeeCanBeManuallyReInvited()
    {
        var attendee = InvitedAttendee();
        attendee.MarkNoResponse(ProposalFixture.Now);

        attendee.MarkInvited(ProposalFixture.Now);

        Assert.Equal(AttendeeStatus.Invited, attendee.Status);
    }

    [Fact]
    public void CancellingABookingReturnsTheAttendeeToNotYetInvited()
    {
        var attendee = InvitedAttendee();
        attendee.MarkBooked(ProposalFixture.Now);

        attendee.ResetToNotYetInvited(ProposalFixture.Now);

        Assert.Equal(AttendeeStatus.NotYetInvited, attendee.Status);
    }

    [Fact]
    public void ANotYetInvitedAttendeeCannotBeResetAgain()
    {
        var attendee = NewAttendee();

        var ex = Assert.Throws<DomainException>(() => attendee.ResetToNotYetInvited(ProposalFixture.Now));
        Assert.Equal("A attendee cannot move from NotYetInvited to NotYetInvited.", ex.Message);
    }

}
`````
