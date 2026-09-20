# 01c — Negotiation across any number of types, edits 18 (Task 6)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs — 1/1

<!-- retirement-file: {"id":53,"file":"tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs","beforeSha":"3341a5d128788e688258e8dc5c9643a097fd45e78a63835105e88493b383dae8","afterSha":"55e38aae9a67dd387b9e8fe6c55dd416ee88445d84bf39a2d4b7fc5d15d2609d","side":"after","part":1,"parts":1} -->

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

## before — tests/EventBooking.Domain.Tests/EventBooking.Domain.Tests.csproj — 1/1

<!-- retirement-file: {"id":54,"file":"tests/EventBooking.Domain.Tests/EventBooking.Domain.Tests.csproj","beforeSha":"047860c4691e9482fa016626f3f05aa1c1bbbb6d7f3461ab53a0342f98e3084a","afterSha":"5952d616ddc4418bb761efdae1e5314d8dd8e9fc632d38dfabcb8f5eb63f6d8f","side":"before","part":1,"parts":1} -->

`````text
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\EventBooking.Domain\EventBooking.Domain.csproj" />
  </ItemGroup>

</Project>
`````

## after — tests/EventBooking.Domain.Tests/EventBooking.Domain.Tests.csproj — 1/1

<!-- retirement-file: {"id":54,"file":"tests/EventBooking.Domain.Tests/EventBooking.Domain.Tests.csproj","beforeSha":"047860c4691e9482fa016626f3f05aa1c1bbbb6d7f3461ab53a0342f98e3084a","afterSha":"5952d616ddc4418bb761efdae1e5314d8dd8e9fc632d38dfabcb8f5eb63f6d8f","side":"after","part":1,"parts":1} -->

`````text
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\EventBooking.Domain\EventBooking.Domain.csproj" />
  </ItemGroup>

  <ItemGroup>
    <!-- One proposal builder shared by every suite: proposals now need a location, a listed
         type set and a proposing type, and no suite should reinvent that shape. -->
    <Compile Include="..\TestSupport\ProposalFixture.cs" Link="TestSupport\ProposalFixture.cs" />
    <Using Include="EventBooking.TestSupport" />
  </ItemGroup>

</Project>
`````

## before — tests/EventBooking.Domain.Tests/Events/EventCancellationTests.cs — 1/1

<!-- retirement-file: {"id":55,"file":"tests/EventBooking.Domain.Tests/Events/EventCancellationTests.cs","beforeSha":"a4aebe7b768744af1a7a3cd1c986fd6bd61af47795adefbebf48c47ec5fad1cf","afterSha":"b4dccc6adac689ed3c34dea97f40153884e4fd8bcc5cc6f01eed45b910336a00","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventCancellationTests
{
    private static Event ActiveEvent()
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 11), new TimeOnly(13, 0), 240),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        return Event.CreateFrom(Guid.NewGuid(), proposal);
    }

    [Fact]
    public void CancellingMarksTheEventCancelled()
    {
        var eventItem = ActiveEvent();

        eventItem.Cancel();

        Assert.Equal(EventStatus.Cancelled, eventItem.Status);
    }

    [Fact]
    public void ACancelledEventOffersNoSpareCapacityEvenWhenItsCountersAreFull()
    {
        var eventItem = ActiveEvent();
        Assert.True(eventItem.HasSpareCapacityForAll(AppointmentTypeIds.All));

        eventItem.Cancel();

        Assert.False(eventItem.HasSpareCapacityForAll(AppointmentTypeIds.All));
        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
    }

    [Fact]
    public void CancellingTwiceIsRejected()
    {
        var eventItem = ActiveEvent();
        eventItem.Cancel();

        var ex = Assert.Throws<DomainException>(() => eventItem.Cancel());
        Assert.Equal("This event has already been cancelled.", ex.Message);
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Events/EventCancellationTests.cs — 1/1

<!-- retirement-file: {"id":55,"file":"tests/EventBooking.Domain.Tests/Events/EventCancellationTests.cs","beforeSha":"a4aebe7b768744af1a7a3cd1c986fd6bd61af47795adefbebf48c47ec5fad1cf","afterSha":"b4dccc6adac689ed3c34dea97f40153884e4fd8bcc5cc6f01eed45b910336a00","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventCancellationTests
{
    private static Event ActiveEvent()
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 11), new TimeOnly(13, 0), 240),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        return Event.CreateFrom(Guid.NewGuid(), proposal);
    }

    [Fact]
    public void CancellingMarksTheEventCancelled()
    {
        var eventItem = ActiveEvent();

        eventItem.Cancel();

        Assert.Equal(EventStatus.Cancelled, eventItem.Status);
    }

    [Fact]
    public void ACancelledEventOffersNoSpareCapacityEvenWhenItsCountersAreFull()
    {
        var eventItem = ActiveEvent();
        Assert.True(eventItem.HasSpareCapacityForAll(AppointmentTypeIds.All));

        eventItem.Cancel();

        Assert.False(eventItem.HasSpareCapacityForAll(AppointmentTypeIds.All));
        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
    }

    [Fact]
    public void CancellingTwiceIsRejected()
    {
        var eventItem = ActiveEvent();
        eventItem.Cancel();

        var ex = Assert.Throws<DomainException>(() => eventItem.Cancel());
        Assert.Equal("This event has already been cancelled.", ex.Message);
    }
}
`````

## before — tests/EventBooking.Domain.Tests/Events/EventCapacityHeadcountAdjustmentTests.cs — 1/1

<!-- retirement-file: {"id":56,"file":"tests/EventBooking.Domain.Tests/Events/EventCapacityHeadcountAdjustmentTests.cs","beforeSha":"197b828df92f425cd9a4861cfc0fa10605ffc67ca0f8669b538632c7ac36d6dc","afterSha":"1c1d57a49c01945271da427d0b6c72812b9025c81beba79c8d302b324b45ddbd","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventCapacityHeadcountAdjustmentTests
{
    private static EventCapacity CapacityOf(int totalHeadcount, int occupied = 0)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            Guid.NewGuid());
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), totalHeadcount);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 20);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 20);

        var capacity = Event
            .CreateFrom(Guid.NewGuid(), proposal)
            .CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);

        for (var index = 0; index < occupied; index++)
        {
            capacity.Decrement();
        }

        return capacity;
    }

    [Fact]
    public void IncreasingTheTotalIncreasesRemainingByTheSameDelta()
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 6);

        var changed = capacity.AdjustTotalHeadcount(12);

        Assert.True(changed);
        Assert.Equal(12, capacity.TotalHeadcount);
        Assert.Equal(6, capacity.RemainingCapacity);
        Assert.Equal(6, capacity.OccupiedCapacity);
    }

    [Fact]
    public void DecreasingTheTotalDecreasesRemainingByTheSameDelta()
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 6);

        var changed = capacity.AdjustTotalHeadcount(8);

        Assert.True(changed);
        Assert.Equal(8, capacity.TotalHeadcount);
        Assert.Equal(2, capacity.RemainingCapacity);
        Assert.Equal(6, capacity.OccupiedCapacity);
    }

    [Fact]
    public void TheTotalMayEqualOccupiedCapacity()
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 6);

        capacity.AdjustTotalHeadcount(6);

        Assert.Equal(6, capacity.TotalHeadcount);
        Assert.Equal(0, capacity.RemainingCapacity);
        Assert.False(capacity.HasSpare);
    }

    [Fact]
    public void ATotalBelowOccupiedCapacityIsRejectedWithoutMutation()
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 6);

        var exception = Assert.Throws<DomainException>(
            () => capacity.AdjustTotalHeadcount(5));

        Assert.Equal(
            "totalHeadcount cannot be lower than occupied capacity.",
            exception.Message);
        Assert.Equal(10, capacity.TotalHeadcount);
        Assert.Equal(4, capacity.RemainingCapacity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ATotalMustRemainPositive(int totalHeadcount)
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 2);

        var exception = Assert.Throws<DomainException>(
            () => capacity.AdjustTotalHeadcount(totalHeadcount));

        Assert.Equal("totalHeadcount must be greater than zero.", exception.Message);
        Assert.Equal(10, capacity.TotalHeadcount);
        Assert.Equal(8, capacity.RemainingCapacity);
    }

    [Fact]
    public void ResubmittingTheCurrentTotalReportsNoChange()
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 6);

        var changed = capacity.AdjustTotalHeadcount(10);

        Assert.False(changed);
        Assert.Equal(10, capacity.TotalHeadcount);
        Assert.Equal(4, capacity.RemainingCapacity);
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Events/EventCapacityHeadcountAdjustmentTests.cs — 1/1

<!-- retirement-file: {"id":56,"file":"tests/EventBooking.Domain.Tests/Events/EventCapacityHeadcountAdjustmentTests.cs","beforeSha":"197b828df92f425cd9a4861cfc0fa10605ffc67ca0f8669b538632c7ac36d6dc","afterSha":"1c1d57a49c01945271da427d0b6c72812b9025c81beba79c8d302b324b45ddbd","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventCapacityHeadcountAdjustmentTests
{
    private static EventCapacity CapacityOf(int totalHeadcount, int occupied = 0)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            Guid.NewGuid());
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), totalHeadcount);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 20);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 20);

        var capacity = Event
            .CreateFrom(Guid.NewGuid(), proposal)
            .CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);

        for (var index = 0; index < occupied; index++)
        {
            capacity.Decrement();
        }

        return capacity;
    }

    [Fact]
    public void IncreasingTheTotalIncreasesRemainingByTheSameDelta()
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 6);

        var changed = capacity.AdjustTotalHeadcount(12);

        Assert.True(changed);
        Assert.Equal(12, capacity.TotalHeadcount);
        Assert.Equal(6, capacity.RemainingCapacity);
        Assert.Equal(6, capacity.OccupiedCapacity);
    }

    [Fact]
    public void DecreasingTheTotalDecreasesRemainingByTheSameDelta()
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 6);

        var changed = capacity.AdjustTotalHeadcount(8);

        Assert.True(changed);
        Assert.Equal(8, capacity.TotalHeadcount);
        Assert.Equal(2, capacity.RemainingCapacity);
        Assert.Equal(6, capacity.OccupiedCapacity);
    }

    [Fact]
    public void TheTotalMayEqualOccupiedCapacity()
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 6);

        capacity.AdjustTotalHeadcount(6);

        Assert.Equal(6, capacity.TotalHeadcount);
        Assert.Equal(0, capacity.RemainingCapacity);
        Assert.False(capacity.HasSpare);
    }

    [Fact]
    public void ATotalBelowOccupiedCapacityIsRejectedWithoutMutation()
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 6);

        var exception = Assert.Throws<DomainException>(
            () => capacity.AdjustTotalHeadcount(5));

        Assert.Equal(
            "totalHeadcount cannot be lower than occupied capacity.",
            exception.Message);
        Assert.Equal(10, capacity.TotalHeadcount);
        Assert.Equal(4, capacity.RemainingCapacity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ATotalMustRemainPositive(int totalHeadcount)
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 2);

        var exception = Assert.Throws<DomainException>(
            () => capacity.AdjustTotalHeadcount(totalHeadcount));

        Assert.Equal("totalHeadcount must be greater than zero.", exception.Message);
        Assert.Equal(10, capacity.TotalHeadcount);
        Assert.Equal(8, capacity.RemainingCapacity);
    }

    [Fact]
    public void ResubmittingTheCurrentTotalReportsNoChange()
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 6);

        var changed = capacity.AdjustTotalHeadcount(10);

        Assert.False(changed);
        Assert.Equal(10, capacity.TotalHeadcount);
        Assert.Equal(4, capacity.RemainingCapacity);
    }
}
`````

## before — tests/EventBooking.Domain.Tests/Events/EventCapacityTests.cs — 1/1

<!-- retirement-file: {"id":57,"file":"tests/EventBooking.Domain.Tests/Events/EventCapacityTests.cs","beforeSha":"ddff62bfcd11d53612fedf9c1b646f99eeed6cb22ca844899ac77e66d547e2fc","afterSha":"5fabb400a6cbce6815afc21786a75fc2f6c2ca260021bb1fdff66fec76cedf1e","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventCapacityTests
{
    private static EventCapacity CapacityOf(int headcount)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), headcount);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), headcount);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), headcount);

        return Event
            .CreateFrom(Guid.NewGuid(), proposal)
            .CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);
    }

    [Fact]
    public void DecrementReducesTheRemainingCountByOne()
    {
        var capacity = CapacityOf(3);

        capacity.Decrement();

        Assert.Equal(2, capacity.RemainingCapacity);
        Assert.Equal(3, capacity.TotalHeadcount);
        Assert.True(capacity.HasSpare);
    }

    [Fact]
    public void DecrementingToZeroLeavesNoSpare()
    {
        var capacity = CapacityOf(1);

        capacity.Decrement();

        Assert.Equal(0, capacity.RemainingCapacity);
        Assert.False(capacity.HasSpare);
    }

    [Fact]
    public void DecrementingPastZeroIsRejected()
    {
        var capacity = CapacityOf(1);
        capacity.Decrement();

        var ex = Assert.Throws<DomainException>(() => capacity.Decrement());
        Assert.Equal("No remaining capacity for this appointment type on this eventItem.", ex.Message);
        Assert.Equal(0, capacity.RemainingCapacity);
    }

    [Fact]
    public void IncrementGivesTheHeadcountBack()
    {
        var capacity = CapacityOf(2);
        capacity.Decrement();

        capacity.Increment();

        Assert.Equal(2, capacity.RemainingCapacity);
    }

    [Fact]
    public void IncrementingAboveTheAcceptedHeadcountIsRejected()
    {
        var capacity = CapacityOf(2);

        var ex = Assert.Throws<DomainException>(() => capacity.Increment());
        Assert.Equal("Remaining capacity cannot exceed the headcount the manager accepted.", ex.Message);
        Assert.Equal(2, capacity.RemainingCapacity);
    }

    [Fact]
    public void RemainingCapacityStaysWithinBoundsAcrossManyOperations()
    {
        var capacity = CapacityOf(5);

        for (var i = 0; i < 5; i++)
        {
            capacity.Decrement();
            Assert.InRange(capacity.RemainingCapacity, 0, 5);
        }

        for (var i = 0; i < 5; i++)
        {
            capacity.Increment();
            Assert.InRange(capacity.RemainingCapacity, 0, 5);
        }

        Assert.Equal(5, capacity.RemainingCapacity);
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Events/EventCapacityTests.cs — 1/1

<!-- retirement-file: {"id":57,"file":"tests/EventBooking.Domain.Tests/Events/EventCapacityTests.cs","beforeSha":"ddff62bfcd11d53612fedf9c1b646f99eeed6cb22ca844899ac77e66d547e2fc","afterSha":"5fabb400a6cbce6815afc21786a75fc2f6c2ca260021bb1fdff66fec76cedf1e","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventCapacityTests
{
    private static EventCapacity CapacityOf(int headcount)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), headcount);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), headcount);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), headcount);

        return Event
            .CreateFrom(Guid.NewGuid(), proposal)
            .CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);
    }

    [Fact]
    public void DecrementReducesTheRemainingCountByOne()
    {
        var capacity = CapacityOf(3);

        capacity.Decrement();

        Assert.Equal(2, capacity.RemainingCapacity);
        Assert.Equal(3, capacity.TotalHeadcount);
        Assert.True(capacity.HasSpare);
    }

    [Fact]
    public void DecrementingToZeroLeavesNoSpare()
    {
        var capacity = CapacityOf(1);

        capacity.Decrement();

        Assert.Equal(0, capacity.RemainingCapacity);
        Assert.False(capacity.HasSpare);
    }

    [Fact]
    public void DecrementingPastZeroIsRejected()
    {
        var capacity = CapacityOf(1);
        capacity.Decrement();

        var ex = Assert.Throws<DomainException>(() => capacity.Decrement());
        Assert.Equal("No remaining capacity for this appointment type on this eventItem.", ex.Message);
        Assert.Equal(0, capacity.RemainingCapacity);
    }

    [Fact]
    public void IncrementGivesTheHeadcountBack()
    {
        var capacity = CapacityOf(2);
        capacity.Decrement();

        capacity.Increment();

        Assert.Equal(2, capacity.RemainingCapacity);
    }

    [Fact]
    public void IncrementingAboveTheAcceptedHeadcountIsRejected()
    {
        var capacity = CapacityOf(2);

        var ex = Assert.Throws<DomainException>(() => capacity.Increment());
        Assert.Equal("Remaining capacity cannot exceed the headcount the manager accepted.", ex.Message);
        Assert.Equal(2, capacity.RemainingCapacity);
    }

    [Fact]
    public void RemainingCapacityStaysWithinBoundsAcrossManyOperations()
    {
        var capacity = CapacityOf(5);

        for (var i = 0; i < 5; i++)
        {
            capacity.Decrement();
            Assert.InRange(capacity.RemainingCapacity, 0, 5);
        }

        for (var i = 0; i < 5; i++)
        {
            capacity.Increment();
            Assert.InRange(capacity.RemainingCapacity, 0, 5);
        }

        Assert.Equal(5, capacity.RemainingCapacity);
    }
}
`````

## before — tests/EventBooking.Domain.Tests/Events/EventProposalAcceptanceTests.cs — 1/1

<!-- retirement-file: {"id":58,"file":"tests/EventBooking.Domain.Tests/Events/EventProposalAcceptanceTests.cs","beforeSha":"7605c056e10cba87a5a9ef6302acabbe392439271e435d506230cb0167034685","afterSha":"3b2ec599408653819c73f99ba2a4a84f9d5226426cab15f28169364bbee4be31","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventProposalAcceptanceTests
{
    private static readonly Guid DrugAndAlcohol = AppointmentTypeIds.DrugAndAlcoholTesting;
    private static readonly Guid Medical = AppointmentTypeIds.MedicalCheckUp;
    private static readonly Guid DrugAndAlcoholManager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid MedicalManager = Guid.Parse("c0000002-0000-0000-0000-000000000002");

    private static EventProposal NewProposal() =>
        EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            DrugAndAlcoholManager);

    [Fact]
    public void AcceptingRecordsTheManagerAndTheirHeadcount()
    {
        var proposal = NewProposal();

        proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, 10);

        var acceptance = Assert.Single(proposal.Acceptances);
        Assert.Equal(proposal.Id, acceptance.ProposalId);
        Assert.Equal(DrugAndAlcohol, acceptance.AppointmentTypeId);
        Assert.Equal(DrugAndAlcoholManager, acceptance.ManagerUserId);
        Assert.Equal(10, acceptance.Headcount);
        Assert.True(proposal.IsAcceptedBy(DrugAndAlcohol));
        Assert.False(proposal.IsAcceptedBy(Medical));
    }

    [Fact]
    public void TwoManagersAcceptIndependently()
    {
        var proposal = NewProposal();

        proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, 10);
        proposal.Accept(Medical, MedicalManager, 6);

        Assert.Equal(2, proposal.Acceptances.Count);
        Assert.Equal(6, proposal.Acceptances.Single(a => a.AppointmentTypeId == Medical).Headcount);
    }

    [Fact]
    public void AcceptingTwiceForTheSameAppointmentTypeRevisesTheHeadcount()
    {
        var proposal = NewProposal();
        proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, 10);

        proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, 12);

        var acceptance = Assert.Single(proposal.Acceptances);
        Assert.Equal(12, acceptance.Headcount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void AcceptingWithoutARealHeadcountIsRejected(int headcount)
    {
        var proposal = NewProposal();

        var ex = Assert.Throws<DomainException>(
            () => proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, headcount));
        Assert.Equal("headcount must be greater than zero.", ex.Message);
        Assert.Empty(proposal.Acceptances);
    }

    [Fact]
    public void AcceptingForAnUnknownAppointmentTypeIsRejected()
    {
        var proposal = NewProposal();

        Assert.Throws<DomainException>(
            () => proposal.Accept(Guid.NewGuid(), DrugAndAlcoholManager, 10));
    }

    [Fact]
    public void WithdrawingAnUnknownAppointmentTypeIsRejected()
    {
        var proposal = NewProposal();
        proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, 10);
        var unknownAppointmentType = Guid.Parse("a0000004-0000-0000-0000-000000000004");

        var ex = Assert.Throws<DomainException>(
            () => proposal.WithdrawAcceptance(unknownAppointmentType, DrugAndAlcoholManager));

        Assert.Equal($"{unknownAppointmentType} is not one of the 3 appointment types.", ex.Message);
        Assert.Single(proposal.Acceptances);
    }

    [Fact]
    public void AWithdrawnProposalCannotBeAccepted()
    {
        var proposal = NewProposal();
        proposal.Withdraw(DrugAndAlcoholManager);

        var ex = Assert.Throws<DomainException>(
            () => proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, 10));
        Assert.Equal("Only an open proposal can be accepted.", ex.Message);
    }

    [Fact]
    public void AManagerCanWithdrawTheirOwnAcceptance()
    {
        var proposal = NewProposal();
        proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, 10);
        proposal.Accept(Medical, MedicalManager, 6);

        proposal.WithdrawAcceptance(Medical, MedicalManager);

        Assert.Single(proposal.Acceptances);
        Assert.False(proposal.IsAcceptedBy(Medical));
    }

    [Fact]
    public void AReplacementManagerCanWithdrawTheFormerManagersAcceptance()
    {
        var proposal = NewProposal();
        proposal.Accept(Medical, MedicalManager, 6);

        proposal.WithdrawAcceptance(Medical, DrugAndAlcoholManager);

        Assert.Empty(proposal.Acceptances);
    }

    [Fact]
    public void WithdrawingAnAcceptanceThatWasNeverGivenIsRejected()
    {
        var proposal = NewProposal();

        var ex = Assert.Throws<DomainException>(
            () => proposal.WithdrawAcceptance(Medical, MedicalManager));
        Assert.Equal("This appointment type has not accepted the proposal.", ex.Message);
    }
}
`````
