# 00b — Vocabulary edits 86 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":286,"oldPath":"tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs","beforeSha":"c9184938bd166d37f5be41ed3932e526131918be175dbd88593f1e8a04505891","afterSha":"a8ce30f6f61c8b54dfe683673e49ad0a5ff0f3903bc7fdc2837e55b364c19c4d","side":"after","part":1,"parts":1} -->

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
        "https://booking.example.com", "Corporate HQ", "recruitment@corp.com");

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
                [AppointmentTypeIds.DrugAndAlcoholTesting]));
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
            _events.Items.Select(eventItem => eventItem.Id),
            _attendee.RequiredAppointmentTypeIds,
            0);
        _invites.Add(invite);
        _attendee.MarkInvited();
        var bookingId = Guid.NewGuid();
        var manage = _tokens.Issue(bookingId);
        var booking = Booking.Create(bookingId, invite, _events.Items[0].Id, manage.TokenHash, _clock.UtcNow);
        _bookings.Add(booking);
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), bookingId, AppointmentTypeIds.DrugAndAlcoholTesting));
        invite.MarkUsed();
        _attendee.MarkBooked();
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
        eventItem.Cancel();
        _attendee.MarkAwaitingAvailability();
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
            _events.Items.Select(eventItem => eventItem.Id),
            _attendee.RequiredAppointmentTypeIds,
            0);
        _invites.Add(invite);
        _attendee.MarkInvited();
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
            _events.Items.Select(eventItem => eventItem.Id),
            _attendee.RequiredAppointmentTypeIds,
            1);
        _invites.Add(invite);
        _attendee.MarkInvited();
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
            _events.Items.Select(eventItem => eventItem.Id),
            _attendee.RequiredAppointmentTypeIds,
            reminderCount);
        _invites.Add(invite);
        _attendee.MarkInvited();
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
            _events.Items.Select(eventItem => eventItem.Id),
            _attendee.RequiredAppointmentTypeIds,
            0);
        _invites.Add(invite);
        _attendee.MarkInvited();
        var bookingId = Guid.NewGuid();
        var manage = _tokens.Issue(bookingId);
        var booking = Booking.Create(bookingId, invite, _events.Items[0].Id, manage.TokenHash, _clock.UtcNow);
        _bookings.Add(booking);
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), bookingId, AppointmentTypeIds.DrugAndAlcoholTesting));
        invite.MarkUsed();
        _attendee.MarkBooked();
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
        eventItem.Cancel();
        _attendee.MarkAwaitingAvailability();
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
        eventItem.Cancel();
        _attendee.MarkInvited();
        _attendee.MarkBooked();
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
        eventItem.Cancel();
        _attendee.MarkAwaitingAvailability();
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
            _events.Items.Select(eventItem => eventItem.Id),
            _attendee.RequiredAppointmentTypeIds,
            0);
        _invites.Add(invite);
        _attendee.MarkInvited();
        var bookingId = Guid.NewGuid();
        var manage = _tokens.Issue(bookingId);
        var booking = Booking.Create(bookingId, invite, _events.Items[0].Id, manage.TokenHash, _clock.UtcNow);
        _bookings.Add(booking);
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), bookingId, AppointmentTypeIds.DrugAndAlcoholTesting));
        invite.MarkUsed();
        _attendee.MarkBooked();
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
            _events.Items.Select(eventItem => eventItem.Id),
            _attendee.RequiredAppointmentTypeIds,
            0);
        _invites.Add(invite);
        _attendee.MarkInvited();
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
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0)),
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

<!-- vocabulary-file: {"id":287,"oldPath":"tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs","newPath":"tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs","beforeSha":"963659ee58acfcc2455cea64d30fc31a1ef213da2ece01e9ea2e94540b788730","afterSha":"0b13eaece3a6ac428da4ec1897b355335b6075dcea2dda263edd399ea02b514a","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Notifications;

/// <summary>Verifies candidate emails name exactly their persisted requirement snapshot.</summary>
public sealed class SnapshotEmailAuthorityTests
{
    private static readonly Candidate Candidate = Candidate.Create(
        Guid.NewGuid(), "Amara", "amara@example.com",
        EmployeeGroup.Define(
            EmployeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting]));
    private static readonly ConfirmedSlot Slot = ConfirmedSlot.CreateImported(
        Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 10, 10), new TimeOnly(9, 0)),
        new Dictionary<Guid, int>
        {
            [AppointmentTypeIds.DrugAndAlcoholTesting] = 10,
            [AppointmentTypeIds.MedicalCheckUp] = 10,
            [AppointmentTypeIds.UniformFitting] = 10,
        });
    private static readonly CandidatePortalOptions Portal = new(
        "https://booking.example", "Head Office", "recruitment@example.com");

    /// <summary>An Invite names only a one-type snapshot and uses singular recovery copy.</summary>
    [Fact]
    public void RecoveryInviteUsesSnapshotAndSingularCopy()
    {
        var email = CandidateEmailComposer.Invite(
            Candidate,
            [AppointmentTypeIds.MedicalCheckUp],
            [Slot, Slot, Slot],
            "https://booking.example/book/token",
            isReinvite: false,
            isRecovery: true);

        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.DoesNotContain("Uniform Fitting", email.TextBody);
        Assert.Contains("missed appointment", email.TextBody, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Booking confirmation names a two-type Booking snapshot, not all Candidate types.</summary>
    [Fact]
    public void ConfirmationUsesBookingSnapshot()
    {
        var email = CandidateEmailComposer.BookingConfirmation(
            Candidate,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            Slot,
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
        var email = CandidateEmailComposer.SlotCancelled(
            Candidate, [AppointmentTypeIds.MedicalCheckUp], Slot);

        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.DoesNotContain("Drug & Alcohol Testing", email.TextBody);
    }

    /// <summary>A three-type Invite names every snapshot type in code order.</summary>
    [Fact]
    public void ThreeTypeInviteNamesEverySnapshotType()
    {
        var email = CandidateEmailComposer.Invite(
            Candidate,
            [AppointmentTypeIds.UniformFitting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.DrugAndAlcoholTesting],
            [Slot, Slot, Slot],
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
        var email = CandidateEmailComposer.SlotCancelled(
            Candidate,
            [AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.DrugAndAlcoholTesting],
            Slot);

        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.Contains("Drug & Alcohol Testing", email.TextBody);
        Assert.DoesNotContain("Uniform Fitting", email.TextBody);
        Assert.Contains("your appointments on", email.TextBody);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs — 1/1

<!-- vocabulary-file: {"id":287,"oldPath":"tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs","newPath":"tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs","beforeSha":"963659ee58acfcc2455cea64d30fc31a1ef213da2ece01e9ea2e94540b788730","afterSha":"0b13eaece3a6ac428da4ec1897b355335b6075dcea2dda263edd399ea02b514a","side":"after","part":1,"parts":1} -->

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
    private static readonly Event Event = Event.CreateImported(
        Guid.NewGuid(), new EventWindow(new DateOnly(2026, 10, 10), new TimeOnly(9, 0)),
        new Dictionary<Guid, int>
        {
            [AppointmentTypeIds.DrugAndAlcoholTesting] = 10,
            [AppointmentTypeIds.MedicalCheckUp] = 10,
            [AppointmentTypeIds.UniformFitting] = 10,
        });
    private static readonly AttendeePortalOptions Portal = new(
        "https://booking.example", "Head Office", "recruitment@example.com");

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

## before — tests/EventBooking.Application.Tests/Slots/AcceptProposalHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":288,"oldPath":"tests/EventBooking.Application.Tests/Slots/AcceptProposalHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Events/AcceptProposalHandlerTests.cs","beforeSha":"1f51180093165fbe9d13b4917cc73a334422befeeb7c46e702a25ab82e19d7f9","afterSha":"421878855433fe4b2b4a18f0b2be205b1035bfd5286f87ddb7b85180b953f480","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Common;
using EventBooking.Application.Slots;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Slots;

public class AcceptProposalHandlerTests
{
    private static readonly Guid DrugAndAlcoholManager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid MedicalManager = Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid UniformManager = Guid.Parse("c0000003-0000-0000-0000-000000000003");
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemorySlotProposalRepository _proposals = new();
    private readonly InMemoryConfirmedSlotRepository _confirmedSlots = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly SlotProposal _proposal;

    private AcceptProposalHandler Handler =>
        new(_proposals, _confirmedSlots, _roles, _unitOfWork, _audit);

    public AcceptProposalHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            DrugAndAlcoholManager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(
            MedicalManager, Role.Manager, AppointmentTypeIds.MedicalCheckUp));
        _roles.Add(StaffAccessProfile.Create(
            UniformManager, Role.Manager, AppointmentTypeIds.UniformFitting));
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));

        _proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            DrugAndAlcoholManager);
        _proposals.Add(_proposal);
    }

    private Task<Result<AcceptProposalOutcome>> Accept(Guid manager, int headcount) =>
        Handler.HandleAsync(
            new AcceptProposalCommand(manager, _proposal.Id, headcount), CancellationToken.None);

    [Fact]
    public async Task TheFirstAcceptRecordsAHeadcountAndConfirmsNothing()
    {
        var result = await Accept(DrugAndAlcoholManager, 10);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ConfirmedSlotId);
        Assert.Equal(_proposal.Id, result.Value.ProposalId);

        var acceptance = Assert.Single(_proposal.Acceptances);
        Assert.Equal(AppointmentTypeIds.DrugAndAlcoholTesting, acceptance.AppointmentTypeId);
        Assert.Equal(10, acceptance.Headcount);
        Assert.Empty(_confirmedSlots.Items);
        Assert.Equal(SlotProposalStatus.Open, _proposal.Status);
    }

    [Fact]
    public async Task TheThirdAcceptConfirmsTheSlotAndInitialisesCapacity()
    {
        await Accept(DrugAndAlcoholManager, 10);
        await Accept(MedicalManager, 6);

        var result = await Accept(UniformManager, 8);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.ConfirmedSlotId);

        var slot = Assert.Single(_confirmedSlots.Items);
        Assert.Equal(result.Value.ConfirmedSlotId, slot.Id);
        Assert.Equal(_proposal.Id, slot.ProposalId);
        Assert.Equal(_proposal.Window, slot.Window);
        Assert.Equal(ConfirmedSlotStatus.Active, slot.Status);
        Assert.Equal(SlotProposalStatus.Confirmed, _proposal.Status);

        Assert.Equal(10, slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(6, slot.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(8, slot.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
    }

    [Fact]
    public async Task ConfirmationWritesBothAnAcceptanceAndAConfirmationAuditEntry()
    {
        await Accept(DrugAndAlcoholManager, 10);
        await Accept(MedicalManager, 6);
        await Accept(UniformManager, 8);

        Assert.Equal(3, _audit.Entries.Count(e => e.Action == AuditAction.AcceptanceRecorded));
        var confirmation = Assert.Single(_audit.Entries, e => e.Action == AuditAction.SlotConfirmed);
        Assert.Equal(AuditEntityTypes.ConfirmedSlot, confirmation.EntityType);
        Assert.Equal(ActorType.Staff, confirmation.ActorType);
    }

    [Fact]
    public async Task AManagerCanReviseTheirAcceptanceWhileTheProposalIsOpen()
    {
        await Accept(DrugAndAlcoholManager, 10);

        var result = await Accept(DrugAndAlcoholManager, 12);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ConfirmedSlotId);
        Assert.Equal(12, Assert.Single(_proposal.Acceptances).Headcount);
    }

    [Fact]
    public async Task ACoordinatorCannotAccept()
    {
        var result = await Accept(Coordinator, 10);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal("This staff profile cannot perform this operation.", result.Error.Message);
    }

    [Fact]
    public async Task AnInvalidHeadcountIsRejected()
    {
        var result = await Accept(DrugAndAlcoholManager, 0);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal("headcount must be greater than zero.", result.Error.Message);
    }

    [Fact]
    public async Task AnUnknownProposalIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new AcceptProposalCommand(DrugAndAlcoholManager, Guid.NewGuid(), 10),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Equal("No such proposal.", result.Error.Message);
    }

    [Fact]
    public async Task AWithdrawnProposalCannotBeAccepted()
    {
        _proposal.Withdraw(DrugAndAlcoholManager);

        var result = await Accept(MedicalManager, 6);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal("Only an open proposal can be accepted.", result.Error.Message);
    }

    [Fact]
    public async Task EachAcceptSavesOnce()
    {
        await Accept(DrugAndAlcoholManager, 10);
        await Accept(MedicalManager, 6);

        Assert.Equal(2, _unitOfWork.SaveCount);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Events/AcceptProposalHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":288,"oldPath":"tests/EventBooking.Application.Tests/Slots/AcceptProposalHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Events/AcceptProposalHandlerTests.cs","beforeSha":"1f51180093165fbe9d13b4917cc73a334422befeeb7c46e702a25ab82e19d7f9","afterSha":"421878855433fe4b2b4a18f0b2be205b1035bfd5286f87ddb7b85180b953f480","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Common;
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

public class AcceptProposalHandlerTests
{
    private static readonly Guid DrugAndAlcoholManager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid MedicalManager = Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid UniformManager = Guid.Parse("c0000003-0000-0000-0000-000000000003");
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemoryEventProposalRepository _proposals = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly EventProposal _proposal;

    private AcceptProposalHandler Handler =>
        new(_proposals, _events, _roles, _unitOfWork, _audit);

    public AcceptProposalHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            DrugAndAlcoholManager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(
            MedicalManager, Role.Manager, AppointmentTypeIds.MedicalCheckUp));
        _roles.Add(StaffAccessProfile.Create(
            UniformManager, Role.Manager, AppointmentTypeIds.UniformFitting));
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));

        _proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            DrugAndAlcoholManager);
        _proposals.Add(_proposal);
    }

    private Task<Result<AcceptProposalOutcome>> Accept(Guid manager, int headcount) =>
        Handler.HandleAsync(
            new AcceptProposalCommand(manager, _proposal.Id, headcount), CancellationToken.None);

    [Fact]
    public async Task TheFirstAcceptRecordsAHeadcountAndConfirmsNothing()
    {
        var result = await Accept(DrugAndAlcoholManager, 10);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.EventId);
        Assert.Equal(_proposal.Id, result.Value.ProposalId);

        var acceptance = Assert.Single(_proposal.Acceptances);
        Assert.Equal(AppointmentTypeIds.DrugAndAlcoholTesting, acceptance.AppointmentTypeId);
        Assert.Equal(10, acceptance.Headcount);
        Assert.Empty(_events.Items);
        Assert.Equal(EventProposalStatus.Open, _proposal.Status);
    }

    [Fact]
    public async Task TheThirdAcceptConfirmsTheEventAndInitialisesCapacity()
    {
        await Accept(DrugAndAlcoholManager, 10);
        await Accept(MedicalManager, 6);

        var result = await Accept(UniformManager, 8);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.EventId);

        var eventItem = Assert.Single(_events.Items);
        Assert.Equal(result.Value.EventId, eventItem.Id);
        Assert.Equal(_proposal.Id, eventItem.ProposalId);
        Assert.Equal(_proposal.Window, eventItem.Window);
        Assert.Equal(EventStatus.Active, eventItem.Status);
        Assert.Equal(EventProposalStatus.Confirmed, _proposal.Status);

        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(6, eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(8, eventItem.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
    }

    [Fact]
    public async Task ConfirmationWritesBothAnAcceptanceAndAConfirmationAuditEntry()
    {
        await Accept(DrugAndAlcoholManager, 10);
        await Accept(MedicalManager, 6);
        await Accept(UniformManager, 8);

        Assert.Equal(3, _audit.Entries.Count(e => e.Action == AuditAction.AcceptanceRecorded));
        var confirmation = Assert.Single(_audit.Entries, e => e.Action == AuditAction.EventConfirmed);
        Assert.Equal(AuditEntityTypes.Event, confirmation.EntityType);
        Assert.Equal(ActorType.Staff, confirmation.ActorType);
    }

    [Fact]
    public async Task AManagerCanReviseTheirAcceptanceWhileTheProposalIsOpen()
    {
        await Accept(DrugAndAlcoholManager, 10);

        var result = await Accept(DrugAndAlcoholManager, 12);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.EventId);
        Assert.Equal(12, Assert.Single(_proposal.Acceptances).Headcount);
    }

    [Fact]
    public async Task ACoordinatorCannotAccept()
    {
        var result = await Accept(Coordinator, 10);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal("This staff profile cannot perform this operation.", result.Error.Message);
    }

    [Fact]
    public async Task AnInvalidHeadcountIsRejected()
    {
        var result = await Accept(DrugAndAlcoholManager, 0);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal("headcount must be greater than zero.", result.Error.Message);
    }

    [Fact]
    public async Task AnUnknownProposalIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new AcceptProposalCommand(DrugAndAlcoholManager, Guid.NewGuid(), 10),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Equal("No such proposal.", result.Error.Message);
    }

    [Fact]
    public async Task AWithdrawnProposalCannotBeAccepted()
    {
        _proposal.Withdraw(DrugAndAlcoholManager);

        var result = await Accept(MedicalManager, 6);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal("Only an open proposal can be accepted.", result.Error.Message);
    }

    [Fact]
    public async Task EachAcceptSavesOnce()
    {
        await Accept(DrugAndAlcoholManager, 10);
        await Accept(MedicalManager, 6);

        Assert.Equal(2, _unitOfWork.SaveCount);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Slots/AcceptProposalHeadcountRevisionTests.cs — 1/1

<!-- vocabulary-file: {"id":289,"oldPath":"tests/EventBooking.Application.Tests/Slots/AcceptProposalHeadcountRevisionTests.cs","newPath":"tests/EventBooking.Application.Tests/Events/AcceptProposalHeadcountRevisionTests.cs","beforeSha":"31217553120a23103c91e79845b0549c3de2767f8d6620e3f5875d87403d4ada","afterSha":"2da4b34a7423fa55e0d4d7eb3ed2e0e0581ca267c9941f7796a1a3a9f4b5d544","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Common;
using EventBooking.Application.Slots;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Slots;

public class AcceptProposalHeadcountRevisionTests
{
    private static readonly Guid DrugAndAlcoholManager =
        Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid FormerDrugAndAlcoholManager =
        Guid.Parse("c0000011-0000-0000-0000-000000000011");
    private static readonly Guid MedicalManager =
        Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid UniformManager =
        Guid.Parse("c0000003-0000-0000-0000-000000000003");

    private readonly InMemorySlotProposalRepository _proposals = new();
    private readonly InMemoryConfirmedSlotRepository _confirmedSlots = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly SlotProposal _proposal;

    private AcceptProposalHandler Handler =>
        new(_proposals, _confirmedSlots, _roles, _unitOfWork, _audit);

    public AcceptProposalHeadcountRevisionTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            DrugAndAlcoholManager,
            Role.Manager,
            AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(
            MedicalManager,
            Role.Manager,
            AppointmentTypeIds.MedicalCheckUp));
        _roles.Add(StaffAccessProfile.Create(
            UniformManager,
            Role.Manager,
            AppointmentTypeIds.UniformFitting));

        _proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            DrugAndAlcoholManager);
        _proposals.Add(_proposal);
    }

    private Task<Result<AcceptProposalOutcome>> Accept(Guid managerUserId, int headcount) =>
        Handler.HandleAsync(
            new AcceptProposalCommand(managerUserId, _proposal.Id, headcount),
            CancellationToken.None);

    [Fact]
    public async Task RevisingAHeadcountUpdatesOneRowAndWritesOneChangeAudit()
    {
        var recorded = await Accept(DrugAndAlcoholManager, 10);
        var revised = await Accept(DrugAndAlcoholManager, 12);

        Assert.True(recorded.IsSuccess);
        Assert.True(revised.IsSuccess);
        Assert.Null(revised.Value.ConfirmedSlotId);
        Assert.Equal(12, Assert.Single(_proposal.Acceptances).Headcount);
        Assert.Equal(2, _unitOfWork.SaveCount);

        var entries = _audit.Entries
            .Where(entry => entry.Action == AuditAction.AcceptanceRecorded)
            .ToList();
        Assert.Equal(2, entries.Count);
        Assert.Equal("Drug & Alcohol Testing headcount 10 -> 12", entries[1].Details);
    }

    [Fact]
    public async Task ResubmittingTheCurrentHeadcountIsASuccessfulNoOp()
    {
        await Accept(DrugAndAlcoholManager, 10);

        var repeated = await Accept(DrugAndAlcoholManager, 10);

        Assert.True(repeated.IsSuccess);
        Assert.Null(repeated.Value.ConfirmedSlotId);
        Assert.Equal(1, _unitOfWork.SaveCount);
        Assert.Single(
            _audit.Entries,
            entry => entry.Action == AuditAction.AcceptanceRecorded);
    }

    [Fact]
    public async Task AReplacementManagerCanReviseTheFormerManagersAcceptance()
    {
        _proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            FormerDrugAndAlcoholManager,
            10);

        var result = await Accept(DrugAndAlcoholManager, 12);

        Assert.True(result.IsSuccess);
        Assert.Equal(12, Assert.Single(_proposal.Acceptances).Headcount);
        Assert.Equal(1, _unitOfWork.SaveCount);
        Assert.Single(
            _audit.Entries,
            entry => entry.Action == AuditAction.AcceptanceRecorded);
    }

    [Fact]
    public async Task AnInvalidRevisionChangesAndWritesNothing()
    {
        await Accept(DrugAndAlcoholManager, 10);

        var result = await Accept(DrugAndAlcoholManager, 0);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal("headcount must be greater than zero.", result.Error.Message);
        Assert.Equal(10, Assert.Single(_proposal.Acceptances).Headcount);
        Assert.Equal(1, _unitOfWork.SaveCount);
        Assert.Single(_audit.Entries);
    }

    [Fact]
    public async Task ConfirmationUsesTheLatestRevisedHeadcount()
    {
        await Accept(DrugAndAlcoholManager, 10);
        await Accept(DrugAndAlcoholManager, 12);
        await Accept(MedicalManager, 6);

        var confirmed = await Accept(UniformManager, 8);

        Assert.True(confirmed.IsSuccess);
        Assert.NotNull(confirmed.Value.ConfirmedSlotId);
        var slot = Assert.Single(_confirmedSlots.Items);
        Assert.Equal(
            12,
            slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).TotalHeadcount);
    }
}
`````
