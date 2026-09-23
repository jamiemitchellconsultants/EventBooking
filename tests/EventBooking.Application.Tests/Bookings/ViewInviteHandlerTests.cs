using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Bookings;

public class ViewInviteHandlerTests
{
    private const string InvalidLink = "This booking link is no longer valid.";

    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryCandidateRepository _candidates = new();
    private readonly InMemoryConfirmedSlotRepository _slots = new();
    private readonly FakeTokenService _tokens = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Candidate _candidate;
    private readonly Invite _invite;
    private readonly string _token;

    private ViewInviteHandler Handler => new(
        _invites, _candidates, _slots, new EligibleSlotFinder(_slots, _clock),
        _audit, _unitOfWork, _tokens, _clock);

    public ViewInviteHandlerTests()
    {
        _candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com",
            EmployeeGroup.Define(
                EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
        _candidates.Add(_candidate);

        var slotIds = new[] { AddSlot(10, 9), AddSlot(11, 13), AddSlot(13, 9) };

        var inviteId = Guid.NewGuid();
        var issued = _tokens.Issue(inviteId);
        _token = issued.Token;
        _invite = Invite.CreateInitial(
            inviteId, _candidate.Id, issued.TokenHash, _clock.UtcNow.AddDays(4), slotIds,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting], 0);
        _invites.Add(_invite);
        _candidate.MarkInvited();
    }

    [Fact]
    public async Task AValidTokenReturnsTheCandidateTheirTypesAndThreeOptions()
    {
        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(_invite.Id, result.Value.InviteId);
        Assert.Equal("Amara Novak", result.Value.CandidateName);
        Assert.Equal(
            new[] { "Drug & Alcohol Testing", "Uniform Fitting" },
            result.Value.AppointmentTypeNames);
        Assert.Equal(3, result.Value.Options.Count);
    }

    [Fact]
    public async Task OptionsCarryTheirWindowAndADisplayString()
    {
        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        var first = result.Value.Options[0];
        Assert.Equal(new DateOnly(2026, 9, 10), first.Date);
        Assert.Equal(new TimeOnly(9, 0), first.StartTime);
        Assert.Equal(new TimeOnly(13, 0), first.EndTime);
        Assert.Equal("Thursday 10 Sep 2026, 09:00-13:00", first.Display);
    }

    [Fact]
    public async Task OptionsAreOrderedEarliestFirst()
    {
        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.Equal(
            new[] { new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 11), new DateOnly(2026, 9, 13) },
            result.Value.Options.Select(o => o.Date));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    public async Task AMalformedTokenGivesTheGenericMessage(string? token)
    {
        var result = await Handler.HandleAsync(new ViewInviteQuery(token), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Equal(InvalidLink, result.Error.Message);
    }

    [Fact]
    public async Task AWellFormedTokenForNoInviteGivesTheSameMessage()
    {
        var result = await Handler.HandleAsync(
            new ViewInviteQuery(_tokens.Issue(Guid.NewGuid()).Token), CancellationToken.None);

        Assert.Equal(InvalidLink, result.Error.Message);
    }

    [Fact]
    public async Task AnExpiredInviteGivesTheSameMessage()
    {
        _clock.Advance(TimeSpan.FromDays(5));

        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.Equal(InvalidLink, result.Error.Message);
    }

    [Fact]
    public async Task AUsedInviteGivesTheSameMessage()
    {
        _invite.MarkUsed();

        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.Equal(InvalidLink, result.Error.Message);
    }

    [Fact]
    public async Task AnOptionWhoseSlotHasBeenCancelledIsNotShown()
    {
        _slots.Items[1].Cancel();

        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.Equal(2, result.Value.Options.Count);
        Assert.DoesNotContain(
            new DateOnly(2026, 9, 11), result.Value.Options.Select(o => o.Date));
    }

    /// <summary>Ensures options on the head-office date or earlier are not projected.</summary>
    [Fact]
    public async Task OptionsOnTodayAndEarlierAreNotShown()
    {
        _invite.RemoveOption(_slots.Items[0].Id);
        _invite.RemoveOption(_slots.Items[1].Id);
        _slots.Items[0].Cancel();
        _slots.Items[1].Cancel();
        _invite.AddOption(AddSlot(3, 9));
        _invite.AddOption(AddSlot(2, 13));

        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { new DateOnly(2026, 9, 13) }, result.Value.Options.Select(o => o.Date));
    }

    private Guid AddSlot(int day, int hour)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, day), new TimeOnly(hour, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);
        _slots.Add(slot);
        return slot.Id;
    }
}
