# 00b — Vocabulary edits 85 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — tests/EventBooking.Application.Tests/Notifications/AttendeeEmailComposerTests.cs — 1/1

<!-- vocabulary-file: {"id":284,"oldPath":"tests/EventBooking.Application.Tests/Notifications/CandidateEmailComposerTests.cs","newPath":"tests/EventBooking.Application.Tests/Notifications/AttendeeEmailComposerTests.cs","beforeSha":"7c15daccb0d4800b3fb97da110ec109a4d5250e724de43c6a142200ad6860110","afterSha":"4b64643236a00230585d46e5a185e5ce604356b74051c221932e4f8bf19e0039","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Notifications;

public class AttendeeEmailComposerTests
{
    private static readonly AttendeePortalOptions Portal = new(
        "https://booking.example.com", "Corporate HQ, 1 Example Street", "recruitment@corp.com");

    private static readonly Attendee Amara = Attendee.Create(
        Guid.NewGuid(), "Amara Novak", "a.novak@mail.com",
        AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));

    [Fact]
    public void AWindowIsFormattedForAHumanReader()
    {
        var window = new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0));

        Assert.Equal("Thursday 10 Sep 2026, 09:00-13:00", AttendeeEmailComposer.FormatWindow(window));
    }

    [Fact]
    public void TheInviteNamesTheAttendeeTheirTypesAndAllThreeOptions()
    {
        var options = new[] { EventOn(10, 9), EventOn(11, 13), EventOn(13, 9) };

        var message = AttendeeEmailComposer.Invite(
            Amara, Amara.RequiredAppointmentTypeIds, options, "https://booking.example.com/book/abc",
            isReinvite: false, isRecovery: false);

        Assert.Equal(Amara.Id, message.AttendeeId);
        Assert.Equal("a.novak@mail.com", message.ToAddress);
        Assert.Equal("Amara Novak", message.ToName);
        Assert.Equal(EmailTemplate.AttendeeInvite, message.Template);
        Assert.Equal("Choose a time for your appointments", message.Subject);

        Assert.Contains("Hi Amara Novak", message.TextBody);
        Assert.Contains("Drug & Alcohol Testing", message.TextBody);
        Assert.Contains("Uniform Fitting", message.TextBody);
        Assert.DoesNotContain("Medical Check-up", message.TextBody);
        Assert.Contains("Thursday 10 Sep 2026, 09:00-13:00", message.TextBody);
        Assert.Contains("Friday 11 Sep 2026, 13:00-17:00", message.TextBody);
        Assert.Contains("Sunday 13 Sep 2026, 09:00-13:00", message.TextBody);
        Assert.Contains("https://booking.example.com/book/abc", message.TextBody);
        Assert.Contains("https://booking.example.com/book/abc", message.HtmlBody);
    }

    [Fact]
    public void TheInviteNeverLeaksCapacityNumbers()
    {
        var eventItem = EventOn(10, 9);

        var message = AttendeeEmailComposer.Invite(
            Amara, Amara.RequiredAppointmentTypeIds, [eventItem, EventOn(11, 13), EventOn(13, 9)],
            "https://x/book/abc", isReinvite: false, isRecovery: false);

        Assert.DoesNotContain("remaining", message.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("capacity", message.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("10", message.Subject);
    }

    [Fact]
    public void AReInviteUsesItsOwnTemplateAndSubject()
    {
        var message = AttendeeEmailComposer.Invite(
            Amara, Amara.RequiredAppointmentTypeIds, [EventOn(10, 9), EventOn(11, 13), EventOn(13, 9)],
            "https://x/book/abc", isReinvite: true, isRecovery: false);

        Assert.Equal(EmailTemplate.AttendeeReinvite, message.Template);
        Assert.Equal("Reminder: choose a time for your appointments", message.Subject);
        Assert.Contains("We have not heard back", message.TextBody);
    }

    [Fact]
    public void TheConfirmationCarriesTheChosenTimeTheAddressAndTheManageLink()
    {
        var message = AttendeeEmailComposer.BookingConfirmation(
            Amara, Amara.RequiredAppointmentTypeIds, EventOn(11, 13),
            "https://booking.example.com/manage/xyz", Portal);

        Assert.Equal(EmailTemplate.BookingConfirmation, message.Template);
        Assert.Equal("Your appointment is confirmed", message.Subject);
        Assert.Contains("Friday 11 Sep 2026, 13:00-17:00", message.TextBody);
        Assert.Contains("Corporate HQ, 1 Example Street", message.TextBody);
        Assert.Contains("https://booking.example.com/manage/xyz", message.TextBody);
        Assert.Contains("recruitment@corp.com", message.TextBody);
    }

    [Fact]
    public void TheCancellationApologisesAndPromisesANewInvite()
    {
        var message = AttendeeEmailComposer.EventCancelled(Amara, Amara.RequiredAppointmentTypeIds, EventOn(11, 13), replacementInviteSent: true);

        Assert.Equal(EmailTemplate.EventCancelledRebookingNeeded, message.Template);
        Assert.Equal("Your appointment time has been cancelled", message.Subject);
        Assert.Contains("Friday 11 Sep 2026, 13:00-17:00", message.TextBody);
        Assert.Contains("new invitation", message.TextBody);
    }

    /// <summary>Cancellation wording stays neutral until replacement delivery succeeds.</summary>
    [Fact]
    public void TheCancellationDoesNotPromiseAUnsentReplacement()
    {
        var message = AttendeeEmailComposer.EventCancelled(Amara, Amara.RequiredAppointmentTypeIds, EventOn(11, 13));

        Assert.Contains("recruitment team will contact you", message.TextBody);
        Assert.DoesNotContain("on its way", message.TextBody);
    }

    [Fact]
    public void EveryMessageHasBothATextAndAnHtmlBody()
    {
        var messages = new[]
        {
            AttendeeEmailComposer.Invite(
                Amara, Amara.RequiredAppointmentTypeIds, [EventOn(10, 9), EventOn(11, 13), EventOn(13, 9)],
                "https://x/b", false, false),
            AttendeeEmailComposer.BookingConfirmation(
                Amara, Amara.RequiredAppointmentTypeIds, EventOn(11, 13), "https://x/m", Portal),
            AttendeeEmailComposer.EventCancelled(Amara, Amara.RequiredAppointmentTypeIds, EventOn(11, 13)),
        };

        Assert.All(messages, m => Assert.False(string.IsNullOrWhiteSpace(m.TextBody)));
        Assert.All(messages, m => Assert.Contains("<html", m.HtmlBody, StringComparison.OrdinalIgnoreCase));
    }

    private static Event EventOn(int day, int hour)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, day), new TimeOnly(hour, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        return Event.CreateFrom(Guid.NewGuid(), proposal);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Notifications/EmailDeliveryServiceTests.cs — 1/1

<!-- vocabulary-file: {"id":285,"oldPath":"tests/EventBooking.Application.Tests/Notifications/EmailDeliveryServiceTests.cs","newPath":"tests/EventBooking.Application.Tests/Notifications/EmailDeliveryServiceTests.cs","beforeSha":"a10b3d1302f9144b89fbf3ab1a054c076cf452195aaee245cd0d334a0bea7f9b","afterSha":"a478aa59a969c0265df614a240019fe05a16297e472299c2dfad7c8eb1427ebb","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace EventBooking.Application.Tests.Notifications;

/// <summary>Verifies claim, provider, and durable outcome behavior for pending deliveries.</summary>
public class EmailDeliveryServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeClock _clock = new(Now);
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly InMemoryEmailDeliveryRepository _deliveries = new();
    private readonly RecordingEmailSender _sender = new();

    /// <summary>A successful provider attempt transitions a pending row to Sent.</summary>
    [Fact]
    public async Task SuccessfulDispatchMarksTheDeliverySent()
    {
        var service = NewService();
        var staged = service.StagePending(Guid.NewGuid(), EmailTemplate.CandidateInvite);

        var result = await service.DispatchAsync(staged.Id, Message(staged.CandidateId), CancellationToken.None);

        Assert.Equal(EmailStatus.Sent, result);
        Assert.Equal(EmailStatus.Sent, staged.Status);
        Assert.Single(_sender.Sent);
    }

    /// <summary>A provider rejection is durable and leaves the business state untouched.</summary>
    [Fact]
    public async Task ProviderFailureMarksTheDeliveryFailed()
    {
        _sender.FailNextSend = true;
        var service = NewService();
        var staged = service.StagePending(Guid.NewGuid(), EmailTemplate.CandidateInvite);

        var result = await service.DispatchAsync(staged.Id, Message(staged.CandidateId), CancellationToken.None);

        Assert.Equal(EmailStatus.Failed, result);
        Assert.Equal(EmailStatus.Failed, staged.Status);
        Assert.Empty(_sender.Sent);
    }

    /// <summary>A failed claim commit prevents any provider call and retains Pending.</summary>
    [Fact]
    public async Task AFailedClaimCommitDoesNotCallTheTransport()
    {
        _unitOfWork.ThrowOnCommit = true;
        var service = NewService();
        var staged = service.StagePending(Guid.NewGuid(), EmailTemplate.CandidateInvite);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DispatchAsync(
            staged.Id, Message(staged.CandidateId), CancellationToken.None));

        Assert.Empty(_sender.Sent);
        Assert.Equal(EmailStatus.Pending, staged.Status);
        Assert.Equal(1, _unitOfWork.RollbackCount);
    }

    /// <summary>A fresh claim held by another worker returns Pending without a duplicate send.</summary>
    [Fact]
    public async Task AFreshClaimDoesNotSendTheSameDeliveryTwice()
    {
        var service = NewService();
        var staged = service.StagePending(Guid.NewGuid(), EmailTemplate.CandidateInvite);
        Assert.True(staged.TryClaim(Now, TimeSpan.FromMinutes(5)));

        var result = await service.DispatchAsync(staged.Id, Message(staged.CandidateId), CancellationToken.None);

        Assert.Equal(EmailStatus.Pending, result);
        Assert.Empty(_sender.Sent);
    }

    /// <summary>A resolved attempt is terminal and cannot be dispatched again.</summary>
    [Fact]
    public async Task AResolvedDeliveryDoesNotCallTheTransport()
    {
        var service = NewService();
        var staged = service.StagePending(Guid.NewGuid(), EmailTemplate.CandidateInvite);
        staged.MarkResolved(Now);

        var result = await service.DispatchAsync(
            staged.Id, Message(staged.CandidateId), CancellationToken.None);

        Assert.Equal(EmailStatus.Resolved, result);
        Assert.Empty(_sender.Sent);
    }

    /// <summary>Staging order remains the latest-delivery order after claims share one clock tick.</summary>
    [Fact]
    public void ClaimsDoNotMakeSameTickDeliveriesAmbiguous()
    {
        var candidateId = Guid.NewGuid();
        var service = NewService();
        var first = service.StagePending(candidateId, EmailTemplate.CandidateInvite);
        service.ClaimForDispatch(first);
        var second = service.StagePending(candidateId, EmailTemplate.SlotCancelledRebookingNeeded);
        service.ClaimForDispatch(second);

        Assert.Equal(second.Id, _deliveries.Items
            .Where(delivery => delivery.CandidateId == candidateId)
            .OrderByDescending(delivery => delivery.SentAt)
            .ThenByDescending(delivery => delivery.Id)
            .First()
            .Id);
    }

    /// <summary>A failed post-send audit callback is logged while the sent outcome remains durable.</summary>
    [Fact]
    public async Task AuditCallbackFailureIsLoggedWithoutReopeningTheDelivery()
    {
        var logger = new RecordingLogger<EmailDeliveryService>();
        var service = EmailDeliveryTestFactory.Create(
            _deliveries, _sender, _unitOfWork, _clock, logger);
        var staged = service.StagePending(Guid.NewGuid(), EmailTemplate.CandidateInvite);

        var result = await service.DispatchAsync(
            staged.Id,
            Message(staged.CandidateId),
            CancellationToken.None,
            () => throw new InvalidOperationException("audit unavailable"));

        Assert.Equal(EmailStatus.Sent, result);
        Assert.Equal(EmailStatus.Sent, staged.Status);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.IsType<InvalidOperationException>(entry.Exception);
        Assert.Contains(staged.Id.ToString(), entry.Message, StringComparison.OrdinalIgnoreCase);
    }

    private EmailDeliveryService NewService() =>
        EmailDeliveryTestFactory.Create(_deliveries, _sender, _unitOfWork, _clock);

    private static EmailMessage Message(Guid candidateId) =>
        new(
            candidateId,
            "candidate@example.com",
            "Candidate",
            EmailTemplate.CandidateInvite,
            "Choose a time",
            "body",
            "<p>body</p>");

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, Exception? Exception, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, exception, formatter(state, exception)));
    }
}
`````

## after — tests/EventBooking.Application.Tests/Notifications/EmailDeliveryServiceTests.cs — 1/1

<!-- vocabulary-file: {"id":285,"oldPath":"tests/EventBooking.Application.Tests/Notifications/EmailDeliveryServiceTests.cs","newPath":"tests/EventBooking.Application.Tests/Notifications/EmailDeliveryServiceTests.cs","beforeSha":"a10b3d1302f9144b89fbf3ab1a054c076cf452195aaee245cd0d334a0bea7f9b","afterSha":"a478aa59a969c0265df614a240019fe05a16297e472299c2dfad7c8eb1427ebb","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace EventBooking.Application.Tests.Notifications;

/// <summary>Verifies claim, provider, and durable outcome behavior for pending deliveries.</summary>
public class EmailDeliveryServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeClock _clock = new(Now);
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly InMemoryEmailDeliveryRepository _deliveries = new();
    private readonly RecordingEmailSender _sender = new();

    /// <summary>A successful provider attempt transitions a pending row to Sent.</summary>
    [Fact]
    public async Task SuccessfulDispatchMarksTheDeliverySent()
    {
        var service = NewService();
        var staged = service.StagePending(Guid.NewGuid(), EmailTemplate.AttendeeInvite);

        var result = await service.DispatchAsync(staged.Id, Message(staged.AttendeeId), CancellationToken.None);

        Assert.Equal(EmailStatus.Sent, result);
        Assert.Equal(EmailStatus.Sent, staged.Status);
        Assert.Single(_sender.Sent);
    }

    /// <summary>A provider rejection is durable and leaves the business state untouched.</summary>
    [Fact]
    public async Task ProviderFailureMarksTheDeliveryFailed()
    {
        _sender.FailNextSend = true;
        var service = NewService();
        var staged = service.StagePending(Guid.NewGuid(), EmailTemplate.AttendeeInvite);

        var result = await service.DispatchAsync(staged.Id, Message(staged.AttendeeId), CancellationToken.None);

        Assert.Equal(EmailStatus.Failed, result);
        Assert.Equal(EmailStatus.Failed, staged.Status);
        Assert.Empty(_sender.Sent);
    }

    /// <summary>A failed claim commit prevents any provider call and retains Pending.</summary>
    [Fact]
    public async Task AFailedClaimCommitDoesNotCallTheTransport()
    {
        _unitOfWork.ThrowOnCommit = true;
        var service = NewService();
        var staged = service.StagePending(Guid.NewGuid(), EmailTemplate.AttendeeInvite);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DispatchAsync(
            staged.Id, Message(staged.AttendeeId), CancellationToken.None));

        Assert.Empty(_sender.Sent);
        Assert.Equal(EmailStatus.Pending, staged.Status);
        Assert.Equal(1, _unitOfWork.RollbackCount);
    }

    /// <summary>A fresh claim held by another worker returns Pending without a duplicate send.</summary>
    [Fact]
    public async Task AFreshClaimDoesNotSendTheSameDeliveryTwice()
    {
        var service = NewService();
        var staged = service.StagePending(Guid.NewGuid(), EmailTemplate.AttendeeInvite);
        Assert.True(staged.TryClaim(Now, TimeSpan.FromMinutes(5)));

        var result = await service.DispatchAsync(staged.Id, Message(staged.AttendeeId), CancellationToken.None);

        Assert.Equal(EmailStatus.Pending, result);
        Assert.Empty(_sender.Sent);
    }

    /// <summary>A resolved attempt is terminal and cannot be dispatched again.</summary>
    [Fact]
    public async Task AResolvedDeliveryDoesNotCallTheTransport()
    {
        var service = NewService();
        var staged = service.StagePending(Guid.NewGuid(), EmailTemplate.AttendeeInvite);
        staged.MarkResolved(Now);

        var result = await service.DispatchAsync(
            staged.Id, Message(staged.AttendeeId), CancellationToken.None);

        Assert.Equal(EmailStatus.Resolved, result);
        Assert.Empty(_sender.Sent);
    }

    /// <summary>Staging order remains the latest-delivery order after claims share one clock tick.</summary>
    [Fact]
    public void ClaimsDoNotMakeSameTickDeliveriesAmbiguous()
    {
        var attendeeId = Guid.NewGuid();
        var service = NewService();
        var first = service.StagePending(attendeeId, EmailTemplate.AttendeeInvite);
        service.ClaimForDispatch(first);
        var second = service.StagePending(attendeeId, EmailTemplate.EventCancelledRebookingNeeded);
        service.ClaimForDispatch(second);

        Assert.Equal(second.Id, _deliveries.Items
            .Where(delivery => delivery.AttendeeId == attendeeId)
            .OrderByDescending(delivery => delivery.SentAt)
            .ThenByDescending(delivery => delivery.Id)
            .First()
            .Id);
    }

    /// <summary>A failed post-send audit callback is logged while the sent outcome remains durable.</summary>
    [Fact]
    public async Task AuditCallbackFailureIsLoggedWithoutReopeningTheDelivery()
    {
        var logger = new RecordingLogger<EmailDeliveryService>();
        var service = EmailDeliveryTestFactory.Create(
            _deliveries, _sender, _unitOfWork, _clock, logger);
        var staged = service.StagePending(Guid.NewGuid(), EmailTemplate.AttendeeInvite);

        var result = await service.DispatchAsync(
            staged.Id,
            Message(staged.AttendeeId),
            CancellationToken.None,
            () => throw new InvalidOperationException("audit unavailable"));

        Assert.Equal(EmailStatus.Sent, result);
        Assert.Equal(EmailStatus.Sent, staged.Status);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.IsType<InvalidOperationException>(entry.Exception);
        Assert.Contains(staged.Id.ToString(), entry.Message, StringComparison.OrdinalIgnoreCase);
    }

    private EmailDeliveryService NewService() =>
        EmailDeliveryTestFactory.Create(_deliveries, _sender, _unitOfWork, _clock);

    private static EmailMessage Message(Guid attendeeId) =>
        new(
            attendeeId,
            "attendee@example.com",
            "Attendee",
            EmailTemplate.AttendeeInvite,
            "Choose a time",
            "body",
            "<p>body</p>");

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, Exception? Exception, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, exception, formatter(state, exception)));
    }
}
`````

## before — tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":286,"oldPath":"tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs","beforeSha":"c9184938bd166d37f5be41ed3932e526131918be175dbd88593f1e8a04505891","afterSha":"a8ce30f6f61c8b54dfe683673e49ad0a5ff0f3903bc7fdc2837e55b364c19c4d","side":"before","part":1,"parts":1} -->

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
