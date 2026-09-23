using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Invites;

public class InviteIssuerTests
{
    private static readonly AttendeePortalOptions Portal = new(
        "https://booking.example.com", "recruitment@corp.com");

    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryAttendeeGroupRepository _groups = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemorySystemSettingsRepository _settings = new();
    private readonly FakeTokenService _tokens = new();
    private readonly RecordingEmailSender _email = new();
    private readonly InMemoryEmailDeliveryRepository _deliveries = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly Attendee _attendee;

    public InviteIssuerTests()
    {
        var pilots = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        _groups.Items.Add(pilots);
        _attendee = Attendee.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", pilots);
    }

    private InviteIssuer Issuer => new(
        _invites,
        _groups,
        new EligibleEventFinder(_events, _clock),
        _settings,
        _tokens,
        EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
        _audit,
        _clock,
        Portal);

    private async Task<InviteIssueResult> Issue(int retryCount = 0, bool isReinvite = false)
    {
        var outcome = await Issuer.IssueInitialAsync(
            _attendee, retryCount, ActorType.System, null, isReinvite, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        var result = outcome.Value;
        if (result.DispatchPlan is not { } plan)
        {
            return result;
        }

        var status = await EmailDeliveryTestFactory.Create(
                _deliveries, _email, _unitOfWork, _clock)
            .DispatchClaimedAsync(plan.DeliveryId, plan.Message, CancellationToken.None, plan.OnSent);
        return result with
        {
            EmailSent = status == EmailStatus.Sent,
            DeliveryStatus = status.ToString(),
        };
    }

    [Fact]
    public async Task ThreeEligibleEventsProduceAnInviteWithThreeOptions()
    {
        AddEvent(new DateOnly(2026, 9, 10));
        AddEvent(new DateOnly(2026, 9, 12));
        AddEvent(new DateOnly(2026, 9, 14));

        var result = await Issue();

        Assert.True(result.Invited);
        Assert.True(result.EmailSent);

        var invite = Assert.Single(_invites.Items);
        Assert.Equal(result.InviteId, invite.Id);
        Assert.Equal(_attendee.Id, invite.AttendeeId);
        Assert.Equal(3, invite.Options.Count);
        Assert.Equal(InviteStatus.Pending, invite.Status);
        Assert.Equal(0, invite.RetryCount);
        Assert.Equal(AttendeeStatus.Invited, _attendee.Status);
    }

    [Fact]
    public async Task TheExpiryComesFromSystemSettings()
    {
        AddThreeEvents();

        await Issue();

        Assert.Equal(
            _clock.UtcNow.AddDays(_settings.Settings.InviteExpiryDays),
            _invites.Items.Single().ExpiresAt);
    }

    [Fact]
    public async Task OnlyTheTokenHashIsStoredAndTheLinkCarriesTheToken()
    {
        AddThreeEvents();

        var result = await Issue();

        var invite = _invites.Items.Single();
        var expected = _tokens.Issue(result.InviteId!.Value);
        Assert.Equal(expected.TokenHash, invite.TokenHash);
        Assert.DoesNotContain(expected.Token, invite.TokenHash);
        Assert.Contains($"https://booking.example.com/book/{expected.Token}", _email.Sent.Single().TextBody);
    }

    [Fact]
    public async Task FewerThanThreeEligibleEventsMeansNoInviteAndNoEmail()
    {
        AddEvent(new DateOnly(2026, 9, 10));
        AddEvent(new DateOnly(2026, 9, 12));

        var result = await Issue();

        Assert.False(result.Invited);
        Assert.Null(result.InviteId);
        Assert.False(result.EmailSent);
        Assert.Empty(_invites.Items);
        Assert.Empty(_email.Sent);
        Assert.Equal(AttendeeStatus.AwaitingAvailability, _attendee.Status);
    }

    [Fact]
    public async Task APendingInviteIsSupersededByTheNewOne()
    {
        AddThreeEvents();
        var old = Invite.CreateInitial(
            Guid.NewGuid(), _attendee.Id, "old-hash", _clock.UtcNow.AddDays(4),
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
            _attendee.RequiredAppointmentTypeIds, 0);
        _invites.Add(old);
        _attendee.MarkInvited();

        await Issue();

        Assert.Equal(InviteStatus.Superseded, old.Status);
        Assert.Equal(2, _invites.Items.Count);
    }

    [Fact]
    public async Task TheRetryCountIsCarriedOntoTheNewInvite()
    {
        AddThreeEvents();

        await Issue(retryCount: 2);

        Assert.Equal(2, _invites.Items.Single().RetryCount);
    }

    [Fact]
    public async Task AReInviteUsesTheReminderTemplate()
    {
        AddThreeEvents();

        await Issue(isReinvite: true);

        Assert.Equal(EmailTemplate.AttendeeReinvite, _email.Sent.Single().Template);
    }

    [Fact]
    public async Task AFailedSendStillLeavesTheInviteInPlace()
    {
        AddThreeEvents();
        _email.FailNextSend = true;

        var result = await Issue();

        Assert.True(result.Invited);
        Assert.False(result.EmailSent);
        Assert.Single(_invites.Items);
        Assert.True(_audit.Contains(AuditAction.InviteCreated));
        Assert.False(_audit.Contains(AuditAction.InviteSent));
    }

    [Fact]
    public async Task ASuccessfulIssueWritesBothAuditEntries()
    {
        AddThreeEvents();

        await Issue();

        Assert.True(_audit.Contains(AuditAction.InviteCreated));
        Assert.True(_audit.Contains(AuditAction.InviteSent));
        Assert.All(
            _audit.Entries,
            e => Assert.Equal(AuditEntityTypes.Invite, e.EntityType));
        var sent = Assert.Single(_audit.Entries, e => e.Action == AuditAction.InviteSent);
        Assert.StartsWith("invite ", sent.Details, StringComparison.Ordinal);
        Assert.DoesNotContain("@", sent.Details, StringComparison.Ordinal);
    }

    private void AddThreeEvents()
    {
        AddEvent(new DateOnly(2026, 9, 10));
        AddEvent(new DateOnly(2026, 9, 12));
        AddEvent(new DateOnly(2026, 9, 14));
    }

    private void AddEvent(DateOnly date)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(9, 0)), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        _events.Add(Event.CreateFrom(Guid.NewGuid(), proposal));
    }
}
