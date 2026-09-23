using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies invite options are topped up to three and shortfalls flag follow-up (Issue #242).</summary>
public class InviteOptionReplacementTests
{
    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTokenService _tokens = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly Attendee _attendee;
    private readonly Invite _invite;
    private readonly string _token;

    private ViewInviteHandler Handler => new(
        _invites, _attendees, _events, new EligibleEventFinder(_events, _clock),
        _audit, _unitOfWork, _tokens, _clock);

    public InviteOptionReplacementTests()
    {
        _attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com",
            AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
        _attendees.Add(_attendee);

        var eventIds = new[] { AddEvent(10, 9), AddEvent(11, 13), AddEvent(13, 9) };

        var inviteId = Guid.NewGuid();
        var issued = _tokens.Issue(inviteId);
        _token = issued.Token;
        _invite = Invite.CreateInitial(
            inviteId, _attendee.Id, issued.TokenHash, _clock.UtcNow.AddDays(4), eventIds,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting], 0);
        _invites.Add(_invite);
        _attendee.MarkInvited();
    }

    /// <summary>A cancelled option is replaced so the attendee still sees three live options.</summary>
    [Fact]
    public async Task View_ReplacesCancelledOption_ToRestoreThreeOptions()
    {
        _events.Items[1].CancelBeforeStart();
        var spareId = AddEvent(14, 9);

        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Options.Count);
        Assert.Contains(spareId, result.Value.Options.Select(o => o.EventId));
        Assert.DoesNotContain(
            _events.Items[1].Id, result.Value.Options.Select(o => o.EventId));
        Assert.Contains(spareId, _invite.OfferedEventIds);
        Assert.True(_audit.Contains(AuditAction.InviteOptionReplaced));
    }

    /// <summary>With no replacement available the attendee is flagged for coordinator follow-up.</summary>
    [Fact]
    public async Task View_WithNoReplacementAvailable_FlagsAttendeeForFollowUp()
    {
        _events.Items[1].CancelBeforeStart();
        _events.Items[2].CancelBeforeStart();

        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Options);
        Assert.Equal(AttendeeStatus.NoResponseNeedsFollowUp, _attendee.Status);
        Assert.True(_audit.Contains(AuditAction.InviteOptionReplaced));
    }

    private Guid AddEvent(int day, int hour)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, day), new TimeOnly(hour, 0), 240),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Add(eventItem);
        return eventItem.Id;
    }
}
