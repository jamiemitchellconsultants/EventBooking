# 02a — Deterministic attendee links and the token version counter, edits 21 (Task 9a)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs — 1/1

<!-- retirement-file: {"id":51,"file":"tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs","beforeSha":"eccb19c794cb70aa3594d0269aa4262aa0da80b674a5da6e47fa80c1edc33e06","afterSha":"2bf63dfbee5d2259b1298e8614f7e8733761d60ce75561388f6e1e93e7588a04","side":"before","part":1,"parts":1} -->

`````csharp
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

public class ExpireInvitesHandlerTests
{
    private static readonly AttendeePortalOptions Portal = new(
        "https://booking.example.com", "recruitment@corp.com");

    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryAttendeeGroupRepository _groups = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemorySystemSettingsRepository _settings = new();
    private readonly RecordingEmailSender _email = new();
    private readonly InMemoryEmailDeliveryRepository _deliveries = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly Attendee _attendee;

    private ExpireInvitesHandler Handler => new(
        _invites,
        _attendees,
        _settings,
        new InviteIssuer(
            _invites, _groups, new EligibleEventFinder(_events, _clock), _settings,
            new FakeTokenService(), EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
            _audit, _clock, Portal),
        EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
        _audit,
        _unitOfWork,
        _clock);

    public ExpireInvitesHandlerTests()
    {
        var pilots = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        _groups.Items.Add(pilots);
        _attendee = Attendee.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", pilots, ProposalFixture.Now);
        _attendees.Add(_attendee);
        AddThreeEvents();
    }

    [Fact]
    public async Task AnInviteThatHasNotExpiredIsLeftAlone()
    {
        GivePendingInvite(expiresInDays: 4, retryCount: 0);

        var summary = await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(0, summary.Expired);
        Assert.Equal(InviteStatus.Pending, _invites.Items.Single().Status);
    }

    [Fact]
    public async Task AnExpiredInviteUnderTheCeilingIsReIssuedWithTheCountIncremented()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: 0);

        var summary = await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(1, summary.Expired);
        Assert.Equal(1, summary.ReIssued);
        Assert.Equal(0, summary.FlaggedForFollowUp);

        Assert.Equal(2, _invites.Items.Count);
        Assert.Equal(InviteStatus.Expired, _invites.Items[0].Status);
        Assert.Equal(InviteStatus.Pending, _invites.Items[1].Status);
        Assert.Equal(1, _invites.Items[1].RetryCount);
        Assert.Equal(AttendeeStatus.Invited, _attendee.Status);
    }

    [Fact]
    public async Task TheReIssuedInviteUsesTheReminderTemplate()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: 0);

        await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(EmailTemplate.AttendeeReinvite, _email.Sent.Single().Template);
    }

    [Fact]
    public async Task AtTheCeilingTheAttendeeIsFlaggedForFollowUpAndNothingIsSent()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: _settings.Settings.MaxAutoRetryCount);

        var summary = await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(1, summary.Expired);
        Assert.Equal(0, summary.ReIssued);
        Assert.Equal(1, summary.FlaggedForFollowUp);

        Assert.Single(_invites.Items);
        Assert.Equal(InviteStatus.Expired, _invites.Items.Single().Status);
        Assert.Equal(AttendeeStatus.NoResponseNeedsFollowUp, _attendee.Status);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task RepeatedSweepsDoNotChaseAAttendeeWhoIsAlreadyFlagged()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: _settings.Settings.MaxAutoRetryCount);
        await Handler.HandleAsync(CancellationToken.None);

        var summary = await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(0, summary.Expired);
        Assert.Equal(AttendeeStatus.NoResponseNeedsFollowUp, _attendee.Status);
    }

    [Fact]
    public async Task AReIssueWithNoEligibleEventsNeedsFollowUp()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: 0);
        _events.Items.Clear();

        var summary = await Handler.HandleAsync(CancellationToken.None);

        // FR-5.7, and design 01's closed table: an invited attendee never drops back to
        // AwaitingAvailability. A failed automatic re-issue is a follow-up for the Coordinator.
        Assert.Equal(1, summary.Expired);
        Assert.Equal(0, summary.ReIssued);
        Assert.Equal(AttendeeStatus.NoResponseNeedsFollowUp, _attendee.Status);
    }

    [Fact]
    public async Task AFailedReIssueFlagsTheAttendeeForFollowUp()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: 0);
        _groups.Items.Clear();
        _groups.Items.Add(AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]));

        var summary = await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(1, summary.Expired);
        Assert.Equal(0, summary.ReIssued);
        Assert.Equal(1, summary.FlaggedForFollowUp);
        Assert.Equal(InviteStatus.Expired, _invites.Items.Single().Status);
        Assert.Equal(AttendeeStatus.NoResponseNeedsFollowUp, _attendee.Status);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task ExpiryIsAudited()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: 0);

        await Handler.HandleAsync(CancellationToken.None);

        var expiry = Assert.Single(_audit.Entries, e => e.Action == AuditAction.InviteExpired);
        Assert.Equal(ActorType.System, expiry.ActorType);
        Assert.Null(expiry.ActorId);
    }

    /// <summary>Verifies expiry persists the business change and delivery result once each.</summary>
    [Fact]
    public async Task TheWholeSweepSavesOnce()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: 0);

        await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(2, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnExpiredRecoveryInviteLeavesTheAttendeeBooked()
    {
        _attendee.MarkInvited(ProposalFixture.Now);
        _attendee.MarkBooked(ProposalFixture.Now);
        _invites.Add(Invite.CreateRecovery(
            Guid.NewGuid(),
            _attendee.Id,
            Guid.NewGuid(),
            $"hash-{Guid.NewGuid():N}",
            _clock.UtcNow.AddDays(-1),
            ProposalFixture.LocationId,
            null,
            _events.Items.Take(3).Select(s => s.Id),
            [AppointmentTypeIds.DrugAndAlcoholTesting]));

        var summary = await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(1, summary.Expired);
        Assert.Equal(0, summary.ReIssued);
        Assert.Equal(0, summary.FlaggedForFollowUp);
        Assert.Equal(InviteStatus.Expired, _invites.Items.Single().Status);
        Assert.Equal(AttendeeStatus.Booked, _attendee.Status);
        Assert.Single(_invites.Items);
        Assert.Empty(_email.Sent);
        Assert.True(_audit.Contains(AuditAction.InviteExpired));
    }

    private void GivePendingInvite(int expiresInDays, int retryCount)
    {
        _attendee.MarkInvited(ProposalFixture.Now);
        _invites.Add(Invite.CreateInitial(
            Guid.NewGuid(),
            _attendee.Id,
            $"hash-{Guid.NewGuid():N}",
            _clock.UtcNow.AddDays(expiresInDays),
            [ProposalFixture.LocationId],
            _events.Items.Take(3).Select(s => s.Id),
            _attendee.RequiredAppointmentTypeIds,
            retryCount));
    }

    private void AddThreeEvents()
    {
        foreach (var day in new[] { 10, 12, 14 })
        {
            var proposal = ProposalFixture.Create(
                Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0), 240),
                Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            _events.Add(Event.CreateFrom(Guid.NewGuid(), proposal));
        }
    }
}
`````

## after — tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs — 1/1

<!-- retirement-file: {"id":51,"file":"tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs","beforeSha":"eccb19c794cb70aa3594d0269aa4262aa0da80b674a5da6e47fa80c1edc33e06","afterSha":"2bf63dfbee5d2259b1298e8614f7e8733761d60ce75561388f6e1e93e7588a04","side":"after","part":1,"parts":1} -->

`````csharp
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

public class ExpireInvitesHandlerTests
{
    private static readonly AttendeePortalOptions Portal = new(
        "https://booking.example.com", "recruitment@corp.com");

    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryAttendeeGroupRepository _groups = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemorySystemSettingsRepository _settings = new();
    private readonly RecordingEmailSender _email = new();
    private readonly InMemoryEmailDeliveryRepository _deliveries = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly Attendee _attendee;

    private ExpireInvitesHandler Handler => new(
        _invites,
        _attendees,
        _settings,
        new InviteIssuer(
            _invites, _groups, new EligibleEventFinder(_events, _clock), _settings,
            new FakeTokenService(), EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
            _audit, _clock, Portal),
        EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
        _audit,
        _unitOfWork,
        _clock);

    public ExpireInvitesHandlerTests()
    {
        var pilots = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        _groups.Items.Add(pilots);
        _attendee = Attendee.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", pilots, ProposalFixture.Now);
        _attendees.Add(_attendee);
        AddThreeEvents();
    }

    [Fact]
    public async Task AnInviteThatHasNotExpiredIsLeftAlone()
    {
        GivePendingInvite(expiresInDays: 4, retryCount: 0);

        var summary = await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(0, summary.Expired);
        Assert.Equal(InviteStatus.Pending, _invites.Items.Single().Status);
    }

    [Fact]
    public async Task AnExpiredInviteUnderTheCeilingIsReIssuedWithTheCountIncremented()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: 0);

        var summary = await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(1, summary.Expired);
        Assert.Equal(1, summary.ReIssued);
        Assert.Equal(0, summary.FlaggedForFollowUp);

        Assert.Equal(2, _invites.Items.Count);
        Assert.Equal(InviteStatus.Expired, _invites.Items[0].Status);
        Assert.Equal(InviteStatus.Pending, _invites.Items[1].Status);
        Assert.Equal(1, _invites.Items[1].RetryCount);
        Assert.Equal(AttendeeStatus.Invited, _attendee.Status);
    }

    [Fact]
    public async Task TheReIssuedInviteUsesTheReminderTemplate()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: 0);

        await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(EmailTemplate.AttendeeReinvite, _email.Sent.Single().Template);
    }

    [Fact]
    public async Task AtTheCeilingTheAttendeeIsFlaggedForFollowUpAndNothingIsSent()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: _settings.Settings.MaxAutoRetryCount);

        var summary = await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(1, summary.Expired);
        Assert.Equal(0, summary.ReIssued);
        Assert.Equal(1, summary.FlaggedForFollowUp);

        Assert.Single(_invites.Items);
        Assert.Equal(InviteStatus.Expired, _invites.Items.Single().Status);
        Assert.Equal(AttendeeStatus.NoResponseNeedsFollowUp, _attendee.Status);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task RepeatedSweepsDoNotChaseAAttendeeWhoIsAlreadyFlagged()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: _settings.Settings.MaxAutoRetryCount);
        await Handler.HandleAsync(CancellationToken.None);

        var summary = await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(0, summary.Expired);
        Assert.Equal(AttendeeStatus.NoResponseNeedsFollowUp, _attendee.Status);
    }

    [Fact]
    public async Task AReIssueWithNoEligibleEventsNeedsFollowUp()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: 0);
        _events.Items.Clear();

        var summary = await Handler.HandleAsync(CancellationToken.None);

        // FR-5.7, and design 01's closed table: an invited attendee never drops back to
        // AwaitingAvailability. A failed automatic re-issue is a follow-up for the Coordinator.
        Assert.Equal(1, summary.Expired);
        Assert.Equal(0, summary.ReIssued);
        Assert.Equal(AttendeeStatus.NoResponseNeedsFollowUp, _attendee.Status);
    }

    [Fact]
    public async Task AFailedReIssueFlagsTheAttendeeForFollowUp()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: 0);
        _groups.Items.Clear();
        _groups.Items.Add(AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]));

        var summary = await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(1, summary.Expired);
        Assert.Equal(0, summary.ReIssued);
        Assert.Equal(1, summary.FlaggedForFollowUp);
        Assert.Equal(InviteStatus.Expired, _invites.Items.Single().Status);
        Assert.Equal(AttendeeStatus.NoResponseNeedsFollowUp, _attendee.Status);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task ExpiryIsAudited()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: 0);

        await Handler.HandleAsync(CancellationToken.None);

        var expiry = Assert.Single(_audit.Entries, e => e.Action == AuditAction.InviteExpired);
        Assert.Equal(ActorType.System, expiry.ActorType);
        Assert.Null(expiry.ActorId);
    }

    /// <summary>Verifies expiry persists the business change and delivery result once each.</summary>
    [Fact]
    public async Task TheWholeSweepSavesOnce()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: 0);

        await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(2, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnExpiredRecoveryInviteLeavesTheAttendeeBooked()
    {
        _attendee.MarkInvited(ProposalFixture.Now);
        _attendee.MarkBooked(ProposalFixture.Now);
        _invites.Add(Invite.CreateRecovery(
            Guid.NewGuid(),
            _attendee.Id,
            Guid.NewGuid(),
            _clock.UtcNow.AddDays(-1),
            ProposalFixture.LocationId,
            null,
            _events.Items.Take(3).Select(s => s.Id),
            [AppointmentTypeIds.DrugAndAlcoholTesting]));

        var summary = await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(1, summary.Expired);
        Assert.Equal(0, summary.ReIssued);
        Assert.Equal(0, summary.FlaggedForFollowUp);
        Assert.Equal(InviteStatus.Expired, _invites.Items.Single().Status);
        Assert.Equal(AttendeeStatus.Booked, _attendee.Status);
        Assert.Single(_invites.Items);
        Assert.Empty(_email.Sent);
        Assert.True(_audit.Contains(AuditAction.InviteExpired));
    }

    private void GivePendingInvite(int expiresInDays, int retryCount)
    {
        _attendee.MarkInvited(ProposalFixture.Now);
        _invites.Add(Invite.CreateInitial(
            Guid.NewGuid(),
            _attendee.Id,
            _clock.UtcNow.AddDays(expiresInDays),
            [ProposalFixture.LocationId],
            _events.Items.Take(3).Select(s => s.Id),
            _attendee.RequiredAppointmentTypeIds,
            retryCount));
    }

    private void AddThreeEvents()
    {
        foreach (var day in new[] { 10, 12, 14 })
        {
            var proposal = ProposalFixture.Create(
                Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0), 240),
                Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            _events.Add(Event.CreateFrom(Guid.NewGuid(), proposal));
        }
    }
}
`````

## before — tests/EventBooking.Application.Tests/Invites/InviteIssuerTests.cs — 1/1

<!-- retirement-file: {"id":52,"file":"tests/EventBooking.Application.Tests/Invites/InviteIssuerTests.cs","beforeSha":"9799a4bff71504a6356067fc7b587210a2f301998133b7f977d54d87c73a449b","afterSha":"df88a139b00a78cc5c37f4adb67dc9f48a4c969d91c0648b1b088160247090ed","side":"before","part":1,"parts":1} -->

`````csharp
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
        _attendee = Attendee.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", pilots, ProposalFixture.Now);
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
            Guid.NewGuid(),
            _attendee.Id,
            "old-hash",
            _clock.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
            _attendee.RequiredAppointmentTypeIds,
            0);
        _invites.Add(old);
        _attendee.MarkInvited(ProposalFixture.Now);

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
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(9, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        _events.Add(Event.CreateFrom(Guid.NewGuid(), proposal));
    }
}
`````

## after — tests/EventBooking.Application.Tests/Invites/InviteIssuerTests.cs — 1/1

<!-- retirement-file: {"id":52,"file":"tests/EventBooking.Application.Tests/Invites/InviteIssuerTests.cs","beforeSha":"9799a4bff71504a6356067fc7b587210a2f301998133b7f977d54d87c73a449b","afterSha":"df88a139b00a78cc5c37f4adb67dc9f48a4c969d91c0648b1b088160247090ed","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
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
        _attendee = Attendee.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", pilots, ProposalFixture.Now);
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
        var expected = _tokens.Issue(TokenPurpose.Book, result.InviteId!.Value, Invite.InitialTokenVersion);
        Assert.Equal(Invite.InitialTokenVersion, invite.TokenVersion);
        Assert.Contains($"https://booking.example.com/book/{expected}", _email.Sent.Single().TextBody);
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
            Guid.NewGuid(),
            _attendee.Id,
            _clock.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
            _attendee.RequiredAppointmentTypeIds,
            0);
        _invites.Add(old);
        _attendee.MarkInvited(ProposalFixture.Now);

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
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(9, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        _events.Add(Event.CreateFrom(Guid.NewGuid(), proposal));
    }
}
`````

## before — tests/EventBooking.Application.Tests/Invites/RecoveryInviteHandlerTests.cs — 1/1

<!-- retirement-file: {"id":53,"file":"tests/EventBooking.Application.Tests/Invites/RecoveryInviteHandlerTests.cs","beforeSha":"738be9fada76858bc308c1f5e3b3b0306ff9ffc8579951ba0d532b34f7163fa2","afterSha":"949a451e86d12f12d4ed613ec03f6ed312d5172eb5ecb8af29c62f883f04e31c","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Invites;

public sealed class RecoveryInviteHandlerTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly Guid Admin = Guid.Parse("a0000009-0000-0000-0000-000000000009");
    private static readonly AttendeePortalOptions Portal = new(
        "https://booking.example.com", "recruitment@corp.com");

    private readonly TransactionOperationLog _operations = new();
    private readonly InMemoryAttendeeRepository _attendees;
    private readonly InMemoryInviteRepository _invites;
    private readonly InMemoryBookingRepository _bookings;
    private readonly InMemoryBookingAppointmentRepository _appointments;
    private readonly InMemoryAttendeeGroupRepository _groups = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemorySystemSettingsRepository _settings = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly RecordingEmailSender _email = new();
    private readonly InMemoryEmailDeliveryRepository _deliveries = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork;
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    public RecoveryInviteHandlerTests()
    {
        _unitOfWork = new FakeUnitOfWork(_operations);
        _attendees = new InMemoryAttendeeRepository(_operations);
        _invites = new InMemoryInviteRepository(_operations);
        _bookings = new InMemoryBookingRepository(_operations);
        _appointments = new InMemoryBookingAppointmentRepository(_bookings, _operations);

        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _roles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));

        var pilots = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        _groups.Items.Add(pilots);
    }

    [Fact]
    public async Task ACoordinatorCanStartRecoveryForAMissedAppointment()
    {
        var (attendee, original, _) = SeedBookedAttendeeWithNoShow();
        AddThreeEvents();

        var result = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, attendee.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([AppointmentTypeIds.DrugAndAlcoholTesting], result.Value.AppointmentTypeIds);

        var recovery = Assert.Single(
            _invites.Items, i => i.RecoveryOfBookingId == original.Id);
        Assert.Equal(InviteStatus.Pending, recovery.Status);
        Assert.Equal([AppointmentTypeIds.DrugAndAlcoholTesting], recovery.RequiredAppointmentTypeIds);
        Assert.Equal(Invite.RequiredOptionCount, recovery.OfferedEventIds.Count);
        Assert.Equal(result.Value.InviteId, recovery.Id);

        Assert.Single(_email.Sent);
        Assert.True(_audit.Contains(AuditAction.RecoveryInviteCreated));
        Assert.Equal(AttendeeStatus.Booked, attendee.Status);
        Assert.Equal(BookingStatus.Active, original.Status);
    }

    [Fact]
    public async Task StartingRecoveryTwiceReportsAlreadyPending()
    {
        var (attendee, _, _) = SeedBookedAttendeeWithNoShow();
        AddThreeEvents();

        var first = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, attendee.Id), CancellationToken.None);
        Assert.True(first.IsSuccess);

        var second = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, attendee.Id), CancellationToken.None);

        Assert.True(second.IsFailure);
        Assert.Equal("recovery_already_pending", second.Error.Code);
        Assert.Single(_invites.Items, i => i.RecoveryOfBookingId.HasValue);
    }

    [Fact]
    public async Task WithoutNoShowsRecoveryIsNotAvailable()
    {
        var (attendee, _, _) = SeedBookedAttendeeWithNoShow();
        foreach (var appointment in _appointments.Items)
        {
            appointment.TransitionTo(
                BookingAppointmentStatus.Expected, Coordinator, _clock.UtcNow, false, false);
        }

        AddThreeEvents();

        var result = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, attendee.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("recovery_not_available", result.Error.Code);
        Assert.DoesNotContain(_invites.Items, i => i.RecoveryOfBookingId.HasValue);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task WithoutEventsTheRecoveryIsAwaitingAvailability()
    {
        var (attendee, _, _) = SeedBookedAttendeeWithNoShow();

        var result = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, attendee.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Guid.Empty, result.Value.InviteId);
        Assert.Equal([AppointmentTypeIds.DrugAndAlcoholTesting], result.Value.AppointmentTypeIds);
        Assert.False(result.Value.EmailSent);
        Assert.DoesNotContain(_invites.Items, i => i.RecoveryOfBookingId.HasValue);
        Assert.Empty(_email.Sent);
        Assert.Equal(AttendeeStatus.Booked, attendee.Status);
    }

    [Fact]
    public async Task AnAdminCannotStartRecovery()
    {
        var (attendee, _, _) = SeedBookedAttendeeWithNoShow();
        AddThreeEvents();

        var result = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Admin, attendee.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.DoesNotContain(_invites.Items, i => i.RecoveryOfBookingId.HasValue);
    }

    [Fact]
    public async Task AnUnknownAttendeeIsNotFound()
    {
        var result = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task ACoordinatorCanCancelAPendingRecoveryInvite()
    {
        var (attendee, _, _) = SeedBookedAttendeeWithNoShow();
        AddThreeEvents();
        var started = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, attendee.Id), CancellationToken.None);
        Assert.True(started.IsSuccess);
        var invite = _invites.Items.Single(i => i.Id == started.Value.InviteId);
        var staleHash = invite.TokenHash;
        var remainingBefore = _events.Items
            .Select(eventItem => eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity)
            .ToList();

        var result = await CancelHandler().HandleAsync(
            new CancelRecoveryInviteCommand(Coordinator, attendee.Id, invite.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InviteStatus.Cancelled, invite.Status);
        Assert.NotEqual(staleHash, invite.TokenHash);
        Assert.Null(await _invites.GetByTokenHashAsync(staleHash, CancellationToken.None));
        Assert.True(_audit.Contains(AuditAction.RecoveryInviteCancelled));
        Assert.Equal(AttendeeStatus.Booked, attendee.Status);
        Assert.Equal(
            remainingBefore,
            _events.Items
                .Select(eventItem => eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity)
                .ToList());
        Assert.Contains(
            _appointments.Items,
            a => a.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting
                && a.Status == BookingAppointmentStatus.NoShow);
        Assert.Contains(
            _appointments.Items,
            a => a.AppointmentTypeId == AppointmentTypeIds.UniformFitting
                && a.Status == BookingAppointmentStatus.Expected);
    }

    [Fact]
    public async Task CancellingTwiceReportsAStaleConflict()
    {
        var (attendee, _, _) = SeedBookedAttendeeWithNoShow();
        AddThreeEvents();
        var started = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, attendee.Id), CancellationToken.None);
        var first = await CancelHandler().HandleAsync(
            new CancelRecoveryInviteCommand(Coordinator, attendee.Id, started.Value.InviteId),
            CancellationToken.None);
        Assert.True(first.IsSuccess);

        var second = await CancelHandler().HandleAsync(
            new CancelRecoveryInviteCommand(Coordinator, attendee.Id, started.Value.InviteId),
            CancellationToken.None);

        Assert.True(second.IsFailure);
        Assert.Equal("conflict", second.Error.Code);
    }

    [Fact]
    public async Task AnInitialInviteCannotBeCancelledAsRecovery()
    {
        var (attendee, _, _) = SeedBookedAttendeeWithNoShow();
        var initial = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            "hash-initial-pending",
            _clock.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            0);
        _invites.Add(initial);

        var result = await CancelHandler().HandleAsync(
            new CancelRecoveryInviteCommand(Coordinator, attendee.Id, initial.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(InviteStatus.Pending, initial.Status);
    }

    [Fact]
    public async Task AnAdminCannotCancelRecovery()
    {
        var (attendee, _, _) = SeedBookedAttendeeWithNoShow();
        AddThreeEvents();
        var started = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, attendee.Id), CancellationToken.None);

        var result = await CancelHandler().HandleAsync(
            new CancelRecoveryInviteCommand(Admin, attendee.Id, started.Value.InviteId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(
            InviteStatus.Pending,
            _invites.Items.Single(i => i.Id == started.Value.InviteId).Status);
    }

    [Fact]
    public async Task CancellingForTheWrongAttendeeIsRejected()
    {
        SeedBookedAttendeeWithNoShow();
        AddThreeEvents();
        var attendee = _attendees.Items.Single();
        var started = await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, attendee.Id), CancellationToken.None);
        var stranger = Attendee.Create(
            Guid.NewGuid(),
            "Bo Vance",
            "b.vance@mail.com",
            _groups.Items.Single(g => g.Id == AttendeeGroupIds.Pilots),
            ProposalFixture.Now);
        _attendees.Add(stranger);

        var result = await CancelHandler().HandleAsync(
            new CancelRecoveryInviteCommand(Coordinator, stranger.Id, started.Value.InviteId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(
            InviteStatus.Pending,
            _invites.Items.Single(i => i.Id == started.Value.InviteId).Status);
    }

    /// <summary>
    /// Recovery issuance takes the attendee lifecycle lock before pending invites, the
    /// original booking, and the active recovery so a concurrent correction serializes first.
    /// </summary>
    [Fact]
    public async Task RecoveryIssuanceUsesTheAttendeeLifecycleLockOrder()
    {
        var (attendee, _, _) = SeedBookedAttendeeWithNoShow();
        AddThreeEvents();

        await StartHandler().HandleAsync(
            new StartRecoveryCommand(Coordinator, attendee.Id), CancellationToken.None);

        Assert.Equal(
            [
                "transaction-begun",
                "attendee-locked",
                "pending-invites-locked",
                "original-booking-locked",
                "active-recovery-locked",
            ],
            _operations.Events.Take(5));
    }

    /// <summary>
    /// A no-show correction that commits between the preflight read and the lifecycle locks
    /// leaves the handler observing changed eligibility instead of issuing a stale invite.
    /// </summary>
    [Fact]
    public async Task ACorrectionRacingIssuanceReportsStateChanged()
    {
        var (attendee, _, missed) = SeedBookedAttendeeWithNoShow();
        AddThreeEvents();
        var correcting = new CorrectingAppointmentRepository(_appointments, missed.Id);

        var result = await StartHandler(correcting).HandleAsync(
            new StartRecoveryCommand(Coordinator, attendee.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("recovery_state_changed", result.Error.Code);
        Assert.DoesNotContain(_invites.Items, i => i.RecoveryOfBookingId.HasValue);
        Assert.Empty(_email.Sent);
    }

    private StartRecoveryHandler StartHandler(
        IBookingAppointmentRepository? appointmentOverride = null) => new(
        _attendees,
        _roles,
        _invites,
        _bookings,
        appointmentOverride ?? _appointments,
        new InviteIssuer(
            _invites, _groups, new EligibleEventFinder(_events, _clock), _settings,
            new FakeTokenService(), EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
            _audit, _clock, Portal),
        new EligibleEventFinder(_events, _clock),
        EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
        _unitOfWork);

    private CancelRecoveryInviteHandler CancelHandler() => new(
        _attendees, _roles, _invites, _bookings, _audit, _unitOfWork);

    private (Attendee Attendee, Booking Original, BookingAppointment Missed) SeedBookedAttendeeWithNoShow()
    {
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "a.novak@mail.com",
            _groups.Items.Single(g => g.Id == AttendeeGroupIds.Pilots),
            ProposalFixture.Now);
        attendee.MarkInvited(ProposalFixture.Now);
        attendee.MarkBooked(ProposalFixture.Now);
        _attendees.Add(attendee);

        var eventIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var initial = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            "hash-initial",
            _clock.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            eventIds,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            0);
        var original = Booking.Create(
            Guid.NewGuid(), initial, eventIds[0], "manage-original", _clock.UtcNow);
        initial.MarkUsed();
        _invites.Add(initial);
        _bookings.Add(original);

        var missed = BookingAppointment.Create(
            Guid.NewGuid(), original.Id, AppointmentTypeIds.DrugAndAlcoholTesting);
        missed.TransitionTo(
            BookingAppointmentStatus.NoShow, Coordinator, _clock.UtcNow, false, true);
        _appointments.Add(missed);
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), original.Id, AppointmentTypeIds.UniformFitting));

        return (attendee, original, missed);
    }

    private void AddThreeEvents()
    {
        foreach (var day in new[] { 10, 12, 14 })
        {
            var proposal = ProposalFixture.Create(
                Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0), 240),
                Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            _events.Add(Event.CreateFrom(Guid.NewGuid(), proposal));
        }
    }

    /// <summary>
    /// Simulates a no-show correction committing between the handler's preflight journey read
    /// and its post-lock re-read by correcting the seeded attempt on the second read.
    /// </summary>
    private sealed class CorrectingAppointmentRepository(
        InMemoryBookingAppointmentRepository inner,
        Guid correctedAppointmentId) : IBookingAppointmentRepository
    {
        private int _reads;

        public void Add(BookingAppointment appointment) => inner.Add(appointment);

        public Task<BookingAppointmentLocator?> FindLocatorInScopeAsync(
            Guid id,
            Guid appointmentTypeId,
            CancellationToken cancellationToken) =>
            inner.FindLocatorInScopeAsync(id, appointmentTypeId, cancellationToken);

        public Task<BookingAppointment?> LockForUpdateAsync(
            Guid id,
            Guid appointmentTypeId,
            CancellationToken cancellationToken) =>
            inner.LockForUpdateAsync(id, appointmentTypeId, cancellationToken);

        public Task<IReadOnlyList<BookingAppointment>> ListForBookingAsync(
            Guid bookingId,
            CancellationToken cancellationToken) =>
            inner.ListForBookingAsync(bookingId, cancellationToken);

        public Task<IReadOnlyList<BookingAppointment>> LockForBookingAsync(
            Guid bookingId,
            CancellationToken cancellationToken) =>
            inner.LockForBookingAsync(bookingId, cancellationToken);

        public Task<IReadOnlyList<BookingAppointment>> ListForBookingsAsync(
            IReadOnlyCollection<Guid> bookingIds,
            CancellationToken cancellationToken)
        {
            if (++_reads == 2)
            {
                inner.Items
                    .Single(a => a.Id == correctedAppointmentId)
                    .TransitionTo(BookingAppointmentStatus.Expected, Coordinator, DateTimeOffset.UtcNow, false, false);
            }

            return inner.ListForBookingsAsync(bookingIds, cancellationToken);
        }
    }
}
`````
