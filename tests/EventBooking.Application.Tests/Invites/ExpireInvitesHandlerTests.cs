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
            _invites, _groups, new EligibleEventFinder(_events, _events, _clock), _settings,
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
