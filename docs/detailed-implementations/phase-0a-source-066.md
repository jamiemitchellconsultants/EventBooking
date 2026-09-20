# 00a — Port source 66 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs","encoding":"utf8","sha256":"c9184938bd166d37f5be41ed3932e526131918be175dbd88593f1e8a04505891","parts":1,"part":1} -->

`````csharp
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
`````

## tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs","encoding":"utf8","sha256":"963659ee58acfcc2455cea64d30fc31a1ef213da2ece01e9ea2e94540b788730","parts":1,"part":1} -->

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

## tests/EventBooking.Application.Tests/Settings/AdminSettingsAccessProfileTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Settings/AdminSettingsAccessProfileTests.cs","encoding":"utf8","sha256":"db855a441dc957141dd7b68efbd392f6a3a16cae7564601bbb31a7ab239d33f2","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Settings;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Tests.Settings;

public class AdminSettingsAccessProfileTests
{
    [Fact]
    public async Task SettingsDerivesEachManagerFromTheAccessProfile()
    {
        var admin = Guid.NewGuid();
        var manager = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(admin, [Role.Admin], null));
        profiles.Add(StaffAccessProfile.Create(
            manager, [Role.Manager], AppointmentTypeIds.MedicalCheckUp));
        var handler = new AdminSettingsHandler(
            new InMemorySystemSettingsRepository(),
            new InMemoryAppointmentTypeRepository(),
            profiles,
            new InMemoryStaffIdentityRepository(),
            new StaffAccessAuthorizer(profiles),
            new FakeUnitOfWork());

        var result = await handler.GetAsync(admin, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            manager,
            result.Value.AppointmentTypes.Single(
                type => type.Id == AppointmentTypeIds.MedicalCheckUp).ManagerUserId);
    }

    /// <summary>Verifies an observed manager name reaches the settings view with its staff number.</summary>
    [Fact]
    public async Task GetPopulatesManagerDisplayNameWhenKnown()
    {
        var admin = Guid.NewGuid();
        var manager = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(admin, [Role.Admin], null));
        profiles.Add(StaffAccessProfile.Create(
            manager, [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting));
        var identities = new InMemoryStaffIdentityRepository();
        await identities.UpsertAsync(
            manager, new StaffId("U000002"), "Dana Datson",
            DateTimeOffset.Parse("2026-09-08T10:00:00Z"), CancellationToken.None);
        var handler = new AdminSettingsHandler(
            new InMemorySystemSettingsRepository(),
            new InMemoryAppointmentTypeRepository(),
            profiles,
            identities,
            new StaffAccessAuthorizer(profiles),
            new FakeUnitOfWork());

        var result = await handler.GetAsync(admin, CancellationToken.None);

        var view = result.Value.AppointmentTypes.Single(
            type => type.Id == AppointmentTypeIds.DrugAndAlcoholTesting);
        Assert.Equal("U000002", view.ManagerStaffId);
        Assert.Equal("Dana Datson", view.ManagerDisplayName);
    }

    /// <summary>Verifies an identity without a name still surfaces its staff number.</summary>
    [Fact]
    public async Task GetLeavesManagerDisplayNameNullWhenUnknown()
    {
        var admin = Guid.NewGuid();
        var manager = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(admin, [Role.Admin], null));
        profiles.Add(StaffAccessProfile.Create(
            manager, [Role.Manager], AppointmentTypeIds.UniformFitting));
        var identities = new InMemoryStaffIdentityRepository();
        await identities.UpsertAsync(
            manager, new StaffId("U000003"), null,
            DateTimeOffset.Parse("2026-09-08T10:00:00Z"), CancellationToken.None);
        var handler = new AdminSettingsHandler(
            new InMemorySystemSettingsRepository(),
            new InMemoryAppointmentTypeRepository(),
            profiles,
            identities,
            new StaffAccessAuthorizer(profiles),
            new FakeUnitOfWork());

        var result = await handler.GetAsync(admin, CancellationToken.None);

        var view = result.Value.AppointmentTypes.Single(
            type => type.Id == AppointmentTypeIds.UniformFitting);
        Assert.Equal("U000003", view.ManagerStaffId);
        Assert.Null(view.ManagerDisplayName);
    }
}
`````

## tests/EventBooking.Application.Tests/Slots/AcceptProposalHandlerTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Slots/AcceptProposalHandlerTests.cs","encoding":"utf8","sha256":"1f51180093165fbe9d13b4917cc73a334422befeeb7c46e702a25ab82e19d7f9","parts":1,"part":1} -->

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

## tests/EventBooking.Application.Tests/Slots/AcceptProposalHeadcountRevisionTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Slots/AcceptProposalHeadcountRevisionTests.cs","encoding":"utf8","sha256":"31217553120a23103c91e79845b0549c3de2767f8d6620e3f5875d87403d4ada","parts":1,"part":1} -->

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

## tests/EventBooking.Application.Tests/Slots/AdjustConfirmedSlotCapacityHandlerTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Slots/AdjustConfirmedSlotCapacityHandlerTests.cs","encoding":"utf8","sha256":"98fc8a9b1effde4776a562d4fde75b174a428625dfdde2719593292097675461","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Slots;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Slots;

public class AdjustConfirmedSlotCapacityHandlerTests
{
    private static readonly Guid DrugAndAlcoholManager =
        Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid Coordinator =
        Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemoryConfirmedSlotRepository _slots = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly ConfirmedSlot _slot;
    private readonly InMemorySlotCapacityRepository _capacities;

    private AdjustConfirmedSlotCapacityHandler Handler =>
        new(_slots, _capacities, _roles, _unitOfWork, _audit);

    public AdjustConfirmedSlotCapacityHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            DrugAndAlcoholManager,
            Role.Manager,
            AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));

        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            DrugAndAlcoholManager);
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        _slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);
        _slots.Add(_slot);
        _capacities = new InMemorySlotCapacityRepository(_slots);
    }

    private Task<EventBooking.Application.Common.Result<AdjustConfirmedSlotCapacityOutcome>>
        Adjust(int totalHeadcount) =>
        Handler.HandleAsync(
            new AdjustConfirmedSlotCapacityCommand(
                DrugAndAlcoholManager, _slot.Id, totalHeadcount),
            CancellationToken.None);

    private void Occupy(int count)
    {
        var capacity = _slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);
        for (var index = 0; index < count; index++)
        {
            capacity.Decrement();
        }
    }

    [Fact]
    public async Task IncreasingTheTotalMovesRemainingAndWritesOneAudit()
    {
        Occupy(6);

        var result = await Adjust(12);

        Assert.True(result.IsSuccess);
        Assert.Equal(12, result.Value.TotalHeadcount);
        Assert.Equal(6, result.Value.RemainingCapacity);
        Assert.Equal(1, _capacities.LockCallCount);
        Assert.Equal(1, _unitOfWork.SaveCount);
        Assert.Equal(1, _unitOfWork.CommitCount);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AuditAction.CapacityAdjusted, entry.Action);
        Assert.Equal(AuditEntityTypes.ConfirmedSlot, entry.EntityType);
        Assert.Equal(
            "Drug & Alcohol Testing total 10 -> 12; remaining 4 -> 6",
            entry.Details);
    }

    [Fact]
    public async Task AValidDecreaseMovesRemainingByTheSameDelta()
    {
        Occupy(6);

        var result = await Adjust(8);

        Assert.True(result.IsSuccess);
        Assert.Equal(8, result.Value.TotalHeadcount);
        Assert.Equal(2, result.Value.RemainingCapacity);
    }

    [Fact]
    public async Task ATotalBelowActiveBookingsIsAConflictAndChangesNothing()
    {
        Occupy(6);

        var result = await Adjust(5);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(
            "Headcount cannot be lower than the active-booking count of 6.",
            result.Error.Message);
        var capacity = _slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);
        Assert.Equal(10, capacity.TotalHeadcount);
        Assert.Equal(4, capacity.RemainingCapacity);
        Assert.Equal(0, _unitOfWork.SaveCount);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task ACurrentTotalIsASuccessfulNoOp()
    {
        Occupy(6);

        var result = await Adjust(10);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value.TotalHeadcount);
        Assert.Equal(4, result.Value.RemainingCapacity);
        Assert.Equal(0, _unitOfWork.SaveCount);
        Assert.Equal(1, _unitOfWork.CommitCount);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task ATotalMustBePositive()
    {
        var result = await Adjust(0);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal("totalHeadcount must be greater than zero.", result.Error.Message);
        Assert.Equal(10, _slot.CapacityFor(
            AppointmentTypeIds.DrugAndAlcoholTesting).TotalHeadcount);
        Assert.Equal(0, _capacities.LockCallCount);
        Assert.Equal(0, _unitOfWork.SaveCount);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task TheCurrentRoleSelectsTheOnlyCapacityThatChanges()
    {
        var result = await Adjust(12);

        Assert.True(result.IsSuccess);
        Assert.Equal(12, _slot.CapacityFor(
            AppointmentTypeIds.DrugAndAlcoholTesting).TotalHeadcount);
        Assert.Equal(6, _slot.CapacityFor(
            AppointmentTypeIds.MedicalCheckUp).TotalHeadcount);
        Assert.Equal(8, _slot.CapacityFor(
            AppointmentTypeIds.UniformFitting).TotalHeadcount);
    }

    [Fact]
    public async Task ACoordinatorCannotAdjustCapacity()
    {
        var result = await Handler.HandleAsync(
            new AdjustConfirmedSlotCapacityCommand(Coordinator, _slot.Id, 12),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(0, _capacities.LockCallCount);
    }

    [Fact]
    public async Task ACancelledSlotCannotBeAdjusted()
    {
        _slot.Cancel();

        var result = await Adjust(12);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("A cancelled slot cannot have its capacity adjusted.", result.Error.Message);
        Assert.Equal(1, _capacities.LockCallCount);
    }

    [Fact]
    public async Task AnUnknownSlotIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new AdjustConfirmedSlotCapacityCommand(
                DrugAndAlcoholManager, Guid.NewGuid(), 12),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Equal(1, _capacities.LockCallCount);
    }
}
`````
