using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Slots;
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
    private static readonly CandidatePortalOptions Portal = new(
        "https://booking.example.com", "Corporate HQ", "recruitment@corp.com");

    private readonly InMemoryCandidateRepository _candidates = new();
    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryBookingRepository _bookings = new();
    private readonly InMemoryBookingAppointmentRepository _appointments;
    private readonly InMemoryConfirmedSlotRepository _slots = new();
    private readonly InMemoryEmailDeliveryRepository _deliveries = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly RecordingEmailSender _sender = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly RotatingTokenService _tokens = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.Zero));
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Candidate _candidate;

    /// <summary>Initializes one authorized candidate and three available slots.</summary>
    public RetryEmailHandlerTests()
    {
        _appointments = new InMemoryBookingAppointmentRepository(_bookings);
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _roles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));
        _candidate = Candidate.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "a.novak@mail.com",
            EmployeeGroup.Define(
                Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting]));
        _candidates.Add(_candidate);
        AddSlot(10);
        AddSlot(12);
        AddSlot(14);
    }

    /// <summary>Booking-confirmation retry rotates the management hash and sends the right template.</summary>
    [Fact]
    public async Task BookingConfirmationRetryRotatesTheManageHashAndUsesTheConfirmationTemplate()
    {
        var inviteId = Guid.NewGuid();
        var inviteToken = _tokens.Issue(inviteId);
        var invite = Invite.CreateInitial(
            inviteId,
            _candidate.Id,
            inviteToken.TokenHash,
            _clock.UtcNow.AddDays(4),
            _slots.Items.Select(slot => slot.Id),
            _candidate.RequiredAppointmentTypeIds,
            0);
        _invites.Add(invite);
        _candidate.MarkInvited();
        var bookingId = Guid.NewGuid();
        var manage = _tokens.Issue(bookingId);
        var booking = Booking.Create(bookingId, invite, _slots.Items[0].Id, manage.TokenHash, _clock.UtcNow);
        _bookings.Add(booking);
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), bookingId, AppointmentTypeIds.DrugAndAlcoholTesting));
        invite.MarkUsed();
        _candidate.MarkBooked();
        var oldHash = booking.ManageTokenHash;
        AddFailedDelivery(EmailTemplate.BookingConfirmation, bookingId: bookingId);

        var result = await Handler().HandleAsync(
            new RetryEmailCommand(Coordinator, _candidate.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Sent", result.Value.DeliveryStatus);
        Assert.NotEqual(oldHash, booking.ManageTokenHash);
        Assert.Contains($"/manage/token-for-{booking.Id:N}-", _sender.LastOf(EmailTemplate.BookingConfirmation).TextBody);
        Assert.Equal(EmailStatus.Resolved, _deliveries.Items[0].Status);
        Assert.Equal(EmailStatus.Sent, _deliveries.Items[1].Status);
        Assert.True(_deliveries.Items[1].SentAt > _deliveries.Items[0].SentAt);
    }

    /// <summary>An administrator is denied candidate delivery recovery by the candidate-data boundary.</summary>
    [Fact]
    public async Task AdministratorCannotRetryCandidateEmail()
    {
        var result = await Handler().HandleAsync(
            new RetryEmailCommand(Admin, _candidate.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Empty(_sender.Sent);
    }

    /// <summary>Cancellation retry sends only its recorded cancellation template.</summary>
    [Fact]
    public async Task CancellationRetryDoesNotCreateOrSendAnInvite()
    {
        var slot = _slots.Items[0];
        slot.Cancel();
        _candidate.MarkAwaitingAvailability();
        var booking = GivenCancelledBooking(slot.Id);
        AddFailedDelivery(
            EmailTemplate.SlotCancelledRebookingNeeded,
            bookingId: booking.Id,
            confirmedSlotId: slot.Id);

        var result = await Handler().HandleAsync(
            new RetryEmailCommand(Coordinator, _candidate.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(_sender.Sent);
        Assert.Equal(EmailTemplate.SlotCancelledRebookingNeeded, _sender.Sent[0].Template);
        Assert.Empty(_invites.Items);
    }

    /// <summary>Invite retry rotates the pending invite hash and keeps the invite template.</summary>
    [Fact]
    public async Task CandidateInviteRetryRotatesThePendingInviteHash()
    {
        var inviteId = Guid.NewGuid();
        var issued = _tokens.Issue(inviteId);
        var invite = Invite.CreateInitial(
            inviteId,
            _candidate.Id,
            issued.TokenHash,
            _clock.UtcNow.AddDays(4),
            _slots.Items.Select(slot => slot.Id),
            _candidate.RequiredAppointmentTypeIds,
            0);
        _invites.Add(invite);
        _candidate.MarkInvited();
        AddFailedDelivery(EmailTemplate.CandidateInvite, inviteId: invite.Id);
        var oldHash = invite.TokenHash;

        var result = await Handler().HandleAsync(
            new RetryEmailCommand(Coordinator, _candidate.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(oldHash, invite.TokenHash);
        Assert.Equal(EmailTemplate.CandidateInvite, Assert.Single(_sender.Sent).Template);
    }

    /// <summary>Candidate re-invite recovery preserves the reminder template.</summary>
    [Fact]
    public async Task CandidateReinviteRetryUsesTheReminderTemplate()
    {
        var inviteId = Guid.NewGuid();
        var issued = _tokens.Issue(inviteId);
        var invite = Invite.CreateInitial(
            inviteId,
            _candidate.Id,
            issued.TokenHash,
            _clock.UtcNow.AddDays(4),
            _slots.Items.Select(slot => slot.Id),
            _candidate.RequiredAppointmentTypeIds,
            1);
        _invites.Add(invite);
        _candidate.MarkInvited();
        AddFailedDelivery(EmailTemplate.CandidateReinvite, inviteId: invite.Id);

        var result = await Handler().HandleAsync(
            new RetryEmailCommand(Coordinator, _candidate.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EmailTemplate.CandidateReinvite, Assert.Single(_sender.Sent).Template);
    }

    /// <summary>Pending invite and re-invite attempts are recoverable with their original template.</summary>
    [Theory]
    [InlineData(EmailTemplate.CandidateInvite, 0)]
    [InlineData(EmailTemplate.CandidateReinvite, 1)]
    public async Task PendingInviteTemplateRetrySupersedesTheOutstandingAttempt(
        EmailTemplate template,
        int reminderCount)
    {
        var inviteId = Guid.NewGuid();
        var issued = _tokens.Issue(inviteId);
        var invite = Invite.CreateInitial(
            inviteId,
            _candidate.Id,
            issued.TokenHash,
            _clock.UtcNow.AddDays(4),
            _slots.Items.Select(slot => slot.Id),
            _candidate.RequiredAppointmentTypeIds,
            reminderCount);
        _invites.Add(invite);
        _candidate.MarkInvited();
        AddPendingDelivery(template, inviteId: invite.Id);

        var result = await Handler().HandleAsync(
            new RetryEmailCommand(Coordinator, _candidate.Id), CancellationToken.None);

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
            _candidate.Id,
            inviteToken.TokenHash,
            _clock.UtcNow.AddDays(4),
            _slots.Items.Select(slot => slot.Id),
            _candidate.RequiredAppointmentTypeIds,
            0);
        _invites.Add(invite);
        _candidate.MarkInvited();
        var bookingId = Guid.NewGuid();
        var manage = _tokens.Issue(bookingId);
        var booking = Booking.Create(bookingId, invite, _slots.Items[0].Id, manage.TokenHash, _clock.UtcNow);
        _bookings.Add(booking);
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), bookingId, AppointmentTypeIds.DrugAndAlcoholTesting));
        invite.MarkUsed();
        _candidate.MarkBooked();
        AddPendingDelivery(EmailTemplate.BookingConfirmation, bookingId: booking.Id);

        var result = await Handler().HandleAsync(
            new RetryEmailCommand(Coordinator, _candidate.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EmailTemplate.BookingConfirmation, Assert.Single(_sender.Sent).Template);
        Assert.Equal(EmailStatus.Resolved, _deliveries.Items[0].Status);
        Assert.Equal(EmailStatus.Sent, _deliveries.Items[1].Status);
    }

    /// <summary>Pending cancellation recovery is actionable and records a terminal result.</summary>
    [Fact]
    public async Task PendingCancellationRetryCompletesThePendingDelivery()
    {
        var slot = _slots.Items[0];
        slot.Cancel();
        _candidate.MarkAwaitingAvailability();
        var booking = GivenCancelledBooking(slot.Id);
        _deliveries.Add(EmailLog.RecordPending(
            Guid.NewGuid(),
            _candidate.Id,
            EmailTemplate.SlotCancelledRebookingNeeded,
            _clock.UtcNow,
            bookingId: booking.Id,
            confirmedSlotId: slot.Id));

        var result = await Handler().HandleAsync(
            new RetryEmailCommand(Coordinator, _candidate.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Sent", result.Value.DeliveryStatus);
        Assert.Single(_sender.Sent);
        Assert.Equal(EmailStatus.Sent, _deliveries.Items[^1].Status);
    }

    /// <summary>An active slot makes a historical cancellation notification non-actionable.</summary>
    [Fact]
    public async Task CancellationRetryForAnActiveSlotReturnsConflictWithoutSending()
    {
        AddFailedDelivery(
            EmailTemplate.SlotCancelledRebookingNeeded,
            confirmedSlotId: _slots.Items[0].Id);

        var result = await Handler().HandleAsync(
            new RetryEmailCommand(Coordinator, _candidate.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Empty(_sender.Sent);
        Assert.Single(_deliveries.Items);
    }

    /// <summary>A cancellation notice is stale once the candidate has booked again.</summary>
    [Fact]
    public async Task CancellationRetryAfterCandidateBooksAgainReturnsConflictWithoutSending()
    {
        var slot = _slots.Items[0];
        slot.Cancel();
        _candidate.MarkInvited();
        _candidate.MarkBooked();
        AddFailedDelivery(
            EmailTemplate.SlotCancelledRebookingNeeded,
            confirmedSlotId: slot.Id);

        var result = await Handler().HandleAsync(
            new RetryEmailCommand(Coordinator, _candidate.Id), CancellationToken.None);

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
            new RetryEmailCommand(Coordinator, _candidate.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Empty(_sender.Sent);
        Assert.Single(_deliveries.Items);
    }

    /// <summary>A sent latest delivery is a stable conflict and cannot be resent.</summary>
    [Fact]
    public async Task ADeliveredLatestEmailCannotBeRetried()
    {
        AddFailedDelivery(EmailTemplate.SlotCancelledRebookingNeeded, confirmedSlotId: _slots.Items[0].Id);
        _deliveries.Items[0].MarkSent(_clock.UtcNow);

        var result = await Handler().HandleAsync(
            new RetryEmailCommand(Coordinator, _candidate.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Empty(_sender.Sent);
    }

    /// <summary>Two retry attempts share one latest-row claim and only one reaches the provider.</summary>
    [Fact]
    public async Task ConcurrentRetriesProduceOneReplacementSend()
    {
        var slot = _slots.Items[0];
        slot.Cancel();
        _candidate.MarkAwaitingAvailability();
        var booking = GivenCancelledBooking(slot.Id);
        var repository = new SerializedRetryDeliveryRepository();
        var failed = EmailLog.RecordPending(
            Guid.NewGuid(),
            _candidate.Id,
            EmailTemplate.SlotCancelledRebookingNeeded,
            _clock.UtcNow,
            bookingId: booking.Id,
            confirmedSlotId: slot.Id);
        failed.MarkFailed(_clock.UtcNow);
        repository.Add(failed);

        var first = Handler(repository).HandleAsync(
            new RetryEmailCommand(Coordinator, _candidate.Id), CancellationToken.None);
        await repository.FirstLatestLockAcquired;
        var second = Handler(repository).HandleAsync(
            new RetryEmailCommand(Coordinator, _candidate.Id), CancellationToken.None);

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
    public async Task RegeneratedBookingContentSurvivesCandidateGroupChange()
    {
        var inviteId = Guid.NewGuid();
        var inviteToken = _tokens.Issue(inviteId);
        var invite = Invite.CreateInitial(
            inviteId,
            _candidate.Id,
            inviteToken.TokenHash,
            _clock.UtcNow.AddDays(4),
            _slots.Items.Select(slot => slot.Id),
            _candidate.RequiredAppointmentTypeIds,
            0);
        _invites.Add(invite);
        _candidate.MarkInvited();
        var bookingId = Guid.NewGuid();
        var manage = _tokens.Issue(bookingId);
        var booking = Booking.Create(bookingId, invite, _slots.Items[0].Id, manage.TokenHash, _clock.UtcNow);
        _bookings.Add(booking);
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), bookingId, AppointmentTypeIds.DrugAndAlcoholTesting));
        invite.MarkUsed();
        _candidate.MarkBooked();
        AddFailedDelivery(EmailTemplate.BookingConfirmation, bookingId: bookingId);

        _candidate.AssignEmployeeGroup(EmployeeGroup.Define(
            EmployeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
            [AppointmentTypeIds.MedicalCheckUp]));

        var result = await Handler().HandleAsync(
            new RetryEmailCommand(Coordinator, _candidate.Id), CancellationToken.None);

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
            _candidate.Id,
            issued.TokenHash,
            _clock.UtcNow.AddDays(4),
            _slots.Items.Select(slot => slot.Id),
            _candidate.RequiredAppointmentTypeIds,
            0);
        _invites.Add(invite);
        _candidate.MarkInvited();
        invite.MarkSuperseded();
        AddFailedDelivery(EmailTemplate.CandidateInvite, inviteId: invite.Id);

        var result = await Handler().HandleAsync(
            new RetryEmailCommand(Coordinator, _candidate.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Empty(_sender.Sent);
        Assert.Single(_deliveries.Items);
    }

    private Booking GivenCancelledBooking(Guid slotId)
    {
        var inviteId = Guid.NewGuid();
        var issued = _tokens.Issue(inviteId);
        var invite = Invite.CreateInitial(
            inviteId,
            _candidate.Id,
            issued.TokenHash,
            _clock.UtcNow.AddDays(4),
            _slots.Items.Select(slot => slot.Id),
            _candidate.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(Guid.NewGuid(), invite, slotId, "hash", _clock.UtcNow);
        booking.Cancel();
        _bookings.Add(booking);
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting));
        return booking;
    }

    private RetryEmailHandler Handler(IEmailDeliveryRepository? repository = null) => new(
        _roles,
        _candidates,
        _invites,
        _bookings,
        _slots,
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
        Guid? confirmedSlotId = null)
    {
        AddPendingDelivery(template, inviteId, bookingId, confirmedSlotId);
        _deliveries.Items[^1].MarkFailed(_clock.UtcNow);
    }

    private void AddPendingDelivery(
        EmailTemplate template,
        Guid? inviteId = null,
        Guid? bookingId = null,
        Guid? confirmedSlotId = null)
    {
        _deliveries.Add(EmailLog.RecordPending(
            Guid.NewGuid(),
            _candidate.Id,
            template,
            _clock.UtcNow,
            inviteId,
            bookingId,
            confirmedSlotId));
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

        public async Task<EmailLog?> LockLatestForCandidateAsync(
            Guid candidateId,
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
                .Where(item => item.CandidateId == candidateId)
                .OrderByDescending(item => item.SentAt)
                .ThenByDescending(item => Items.IndexOf(item))
                .FirstOrDefault();
        }

        public Task<EmailLog?> GetLatestForCandidateAsync(
            Guid candidateId,
            EmailTemplate template,
            CancellationToken cancellationToken) =>
            Task.FromResult(Items
                .Where(item => item.CandidateId == candidateId && item.TemplateName == template)
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

    private ConfirmedSlot AddSlot(int day)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);
        _slots.Add(slot);
        return slot;
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
