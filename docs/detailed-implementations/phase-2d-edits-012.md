# 02d — The invite eligibility query, and the start instant it orders on, edits 12 (Task 11)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.Application.Tests/Invites/EligibleEventFinderTests.cs — 1/1

<!-- retirement-file: {"id":24,"file":"tests/EventBooking.Application.Tests/Invites/EligibleEventFinderTests.cs","beforeSha":"6637bd80483a3aa4e8f0f08f7d57cb63e6e7baa0575bd76032c943c8d6cc95ea","afterSha":"be0b8c51b538e6e66abc40ffa0efb2f0dcba2cd3ab395d4c6f6790df5863c2ed","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Events;
using EventBooking.Application.Invites;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Invites;

/// <summary>
/// The rule this used to hold moved into the database in Task 11, and is proved there against a
/// real PostgreSQL. What is left here is the adapter: which filters it hands the eligibility port,
/// and that it hydrates the identifiers the port returns without re-imposing an order of its own.
/// </summary>
public class EligibleEventFinderTests
{
    private static readonly Guid[] NeedsTwo =
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting];

    private readonly InMemoryEventRepository _events = new();
    private readonly RecordingEligibilityQuery _eligibility = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    private EligibleEventFinder Finder => new(_eligibility, _events, _clock);

    [Fact]
    public async Task TheFiltersGoStraightToTheQueryWithTheClocksInstant()
    {
        var excluded = Guid.NewGuid();

        await Finder.FindAsync(NeedsTwo, 3, [excluded], CancellationToken.None);

        Assert.Equal(NeedsTwo, _eligibility.RequiredAppointmentTypeIds);
        Assert.Equal([excluded], _eligibility.ExcludeEventIds);
        Assert.Equal(3, _eligibility.Count);
        Assert.Equal(_clock.UtcNow, _eligibility.AsOf);
    }

    [Fact]
    public async Task OnlyTheTransitionalLocationIsAskedForUntilTaskFourteen()
    {
        await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Equal([TransitionalLocation.Id], _eligibility.LocationIds);
    }

    [Fact]
    public async Task TheEventsComeBackInTheOrderTheQueryChose()
    {
        var first = AddEvent(new DateOnly(2026, 9, 16));
        var second = AddEvent(new DateOnly(2026, 9, 10));
        var third = AddEvent(new DateOnly(2026, 9, 12));
        _eligibility.Answer = [third.Id, first.Id, second.Id];

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Equal([third.Id, first.Id, second.Id], result.Select(s => s.Id));
    }

    [Fact]
    public async Task NoEligibleEventMeansNoLookup()
    {
        AddEvent(new DateOnly(2026, 9, 10));

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task TheCountUsesTheSameFiltersWithoutALimit()
    {
        _eligibility.Answer = [Guid.NewGuid(), Guid.NewGuid()];

        var counted = await Finder.CountAsync(NeedsTwo, [], CancellationToken.None);

        Assert.Equal(2, counted);
        Assert.Equal([TransitionalLocation.Id], _eligibility.LocationIds);
        Assert.Null(_eligibility.Count);
    }

    private Event AddEvent(DateOnly date)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(9, 0), 240), Guid.NewGuid());
        foreach (var type in AppointmentTypeIds.All)
        {
            proposal.Accept(type, Guid.NewGuid(), 10);
        }

        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Add(eventItem);
        return eventItem;
    }

    /// <summary>Records what the adapter asked for, and answers with whatever it is told to.</summary>
    private sealed class RecordingEligibilityQuery : IEventEligibilityQuery
    {
        public IReadOnlyList<Guid> Answer { get; set; } = [];

        public IReadOnlyCollection<Guid>? RequiredAppointmentTypeIds { get; private set; }

        public IReadOnlyCollection<Guid>? LocationIds { get; private set; }

        public IReadOnlyCollection<Guid>? ExcludeEventIds { get; private set; }

        public int? Count { get; private set; }

        public DateTimeOffset? AsOf { get; private set; }

        public Task<IReadOnlyList<Guid>> FindEligibleEventsAsync(
            IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
            IReadOnlyCollection<Guid> locationIds,
            IReadOnlyCollection<Guid> excludeEventIds,
            int count,
            DateTimeOffset asOf,
            CancellationToken cancellationToken)
        {
            Record(requiredAppointmentTypeIds, locationIds, excludeEventIds, asOf);
            Count = count;
            return Task.FromResult(Answer);
        }

        public Task<int> CountEligibleEventsAsync(
            IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
            IReadOnlyCollection<Guid> locationIds,
            IReadOnlyCollection<Guid> excludeEventIds,
            DateTimeOffset asOf,
            CancellationToken cancellationToken)
        {
            Record(requiredAppointmentTypeIds, locationIds, excludeEventIds, asOf);
            return Task.FromResult(Answer.Count);
        }

        private void Record(
            IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
            IReadOnlyCollection<Guid> locationIds,
            IReadOnlyCollection<Guid> excludeEventIds,
            DateTimeOffset asOf)
        {
            RequiredAppointmentTypeIds = requiredAppointmentTypeIds;
            LocationIds = locationIds;
            ExcludeEventIds = excludeEventIds;
            AsOf = asOf;
        }
    }
}
`````

## before — tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs — 1/1

<!-- retirement-file: {"id":25,"file":"tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs","beforeSha":"2bf63dfbee5d2259b1298e8614f7e8733761d60ce75561388f6e1e93e7588a04","afterSha":"af1a197d14d3c062062cfd62ed808439f02f6ef6b6cab4753e1cad8a7363766b","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs — 1/1

<!-- retirement-file: {"id":25,"file":"tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs","beforeSha":"2bf63dfbee5d2259b1298e8614f7e8733761d60ce75561388f6e1e93e7588a04","afterSha":"af1a197d14d3c062062cfd62ed808439f02f6ef6b6cab4753e1cad8a7363766b","side":"after","part":1,"parts":1} -->

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
`````

## before — tests/EventBooking.Application.Tests/Invites/InviteIssuerTests.cs — 1/1

<!-- retirement-file: {"id":26,"file":"tests/EventBooking.Application.Tests/Invites/InviteIssuerTests.cs","beforeSha":"df88a139b00a78cc5c37f4adb67dc9f48a4c969d91c0648b1b088160247090ed","afterSha":"bf08fec000b42bb23b0e117616e971260fb064d0fca32a4444ebf452a7a9c04a","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Application.Tests/Invites/InviteIssuerTests.cs — 1/1

<!-- retirement-file: {"id":26,"file":"tests/EventBooking.Application.Tests/Invites/InviteIssuerTests.cs","beforeSha":"df88a139b00a78cc5c37f4adb67dc9f48a4c969d91c0648b1b088160247090ed","afterSha":"bf08fec000b42bb23b0e117616e971260fb064d0fca32a4444ebf452a7a9c04a","side":"after","part":1,"parts":1} -->

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
        new EligibleEventFinder(_events, _events, _clock),
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
