using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Invites;

public class ExpireInvitesHandlerTests
{
    private static readonly CandidatePortalOptions Portal = new(
        "https://booking.example.com", "Corporate HQ", "recruitment@corp.com");

    private readonly InMemoryCandidateRepository _candidates = new();
    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryEmployeeGroupRepository _groups = new();
    private readonly InMemoryConfirmedSlotRepository _slots = new();
    private readonly InMemorySystemSettingsRepository _settings = new();
    private readonly RecordingEmailSender _email = new();
    private readonly InMemoryEmailDeliveryRepository _deliveries = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly Candidate _candidate;

    private ExpireInvitesHandler Handler => new(
        _invites,
        _candidates,
        _settings,
        new InviteIssuer(
            _invites, _groups, new EligibleSlotFinder(_slots, _clock), _settings,
            new FakeTokenService(), EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
            _audit, _clock, Portal),
        EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
        _audit,
        _unitOfWork,
        _clock);

    public ExpireInvitesHandlerTests()
    {
        var pilots = EmployeeGroup.Define(
            EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        _groups.Items.Add(pilots);
        _candidate = Candidate.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", pilots);
        _candidates.Add(_candidate);
        AddThreeSlots();
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
        Assert.Equal(CandidateStatus.Invited, _candidate.Status);
    }

    [Fact]
    public async Task TheReIssuedInviteUsesTheReminderTemplate()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: 0);

        await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(EmailTemplate.CandidateReinvite, _email.Sent.Single().Template);
    }

    [Fact]
    public async Task AtTheCeilingTheCandidateIsFlaggedForFollowUpAndNothingIsSent()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: _settings.Settings.MaxAutoRetryCount);

        var summary = await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(1, summary.Expired);
        Assert.Equal(0, summary.ReIssued);
        Assert.Equal(1, summary.FlaggedForFollowUp);

        Assert.Single(_invites.Items);
        Assert.Equal(InviteStatus.Expired, _invites.Items.Single().Status);
        Assert.Equal(CandidateStatus.NoResponseNeedsFollowUp, _candidate.Status);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task RepeatedSweepsDoNotChaseACandidateWhoIsAlreadyFlagged()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: _settings.Settings.MaxAutoRetryCount);
        await Handler.HandleAsync(CancellationToken.None);

        var summary = await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(0, summary.Expired);
        Assert.Equal(CandidateStatus.NoResponseNeedsFollowUp, _candidate.Status);
    }

    [Fact]
    public async Task AReIssueWithNoEligibleSlotsFlagsAwaitingAvailabilityInstead()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: 0);
        _slots.Items.Clear();

        var summary = await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(1, summary.Expired);
        Assert.Equal(0, summary.ReIssued);
        Assert.Equal(CandidateStatus.AwaitingAvailability, _candidate.Status);
    }

    [Fact]
    public async Task AFailedReIssueFlagsTheCandidateForFollowUp()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: 0);
        _groups.Items.Clear();
        _groups.Items.Add(EmployeeGroup.Define(
            EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]));

        var summary = await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(1, summary.Expired);
        Assert.Equal(0, summary.ReIssued);
        Assert.Equal(1, summary.FlaggedForFollowUp);
        Assert.Equal(InviteStatus.Expired, _invites.Items.Single().Status);
        Assert.Equal(CandidateStatus.NoResponseNeedsFollowUp, _candidate.Status);
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
    public async Task AnExpiredRecoveryInviteLeavesTheCandidateBooked()
    {
        _candidate.MarkInvited();
        _candidate.MarkBooked();
        _invites.Add(Invite.CreateRecovery(
            Guid.NewGuid(),
            _candidate.Id,
            Guid.NewGuid(),
            $"hash-{Guid.NewGuid():N}",
            _clock.UtcNow.AddDays(-1),
            _slots.Items.Take(3).Select(s => s.Id),
            [AppointmentTypeIds.DrugAndAlcoholTesting]));

        var summary = await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(1, summary.Expired);
        Assert.Equal(0, summary.ReIssued);
        Assert.Equal(0, summary.FlaggedForFollowUp);
        Assert.Equal(InviteStatus.Expired, _invites.Items.Single().Status);
        Assert.Equal(CandidateStatus.Booked, _candidate.Status);
        Assert.Single(_invites.Items);
        Assert.Empty(_email.Sent);
        Assert.True(_audit.Contains(AuditAction.InviteExpired));
    }

    private void GivePendingInvite(int expiresInDays, int retryCount)
    {
        _candidate.MarkInvited();
        _invites.Add(Invite.CreateInitial(
            Guid.NewGuid(),
            _candidate.Id,
            $"hash-{Guid.NewGuid():N}",
            _clock.UtcNow.AddDays(expiresInDays),
            _slots.Items.Take(3).Select(s => s.Id),
            _candidate.RequiredAppointmentTypeIds,
            retryCount));
    }

    private void AddThreeSlots()
    {
        foreach (var day in new[] { 10, 12, 14 })
        {
            var proposal = SlotProposal.Create(
                Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0)),
                Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            _slots.Add(ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal));
        }
    }
}
