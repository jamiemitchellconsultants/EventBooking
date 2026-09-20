# 01a — Variable-length windows in the location's zone, edits 15 (Task 4)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — tests/EventBooking.Application.Tests/Bookings/ViewInviteHandlerTests.cs — 1/1

<!-- retirement-file: {"id":38,"file":"tests/EventBooking.Application.Tests/Bookings/ViewInviteHandlerTests.cs","beforeSha":"45574863712b9f45549cb9df4f03bc65c076d801b73a145568bd9a4e6264f082","afterSha":"adfdbd61472877acc92c35831029131440f53ebd832d9911e5308f271a209d0e","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Bookings;

public class ViewInviteHandlerTests
{
    private const string InvalidLink = "This booking link is no longer valid.";

    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly FakeTokenService _tokens = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Attendee _attendee;
    private readonly Invite _invite;
    private readonly string _token;

    private ViewInviteHandler Handler => new(
        _invites, _attendees, _events, new EligibleEventFinder(_events, _clock),
        _audit, _unitOfWork, _tokens, _clock);

    public ViewInviteHandlerTests()
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

    [Fact]
    public async Task AValidTokenReturnsTheAttendeeTheirTypesAndThreeOptions()
    {
        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(_invite.Id, result.Value.InviteId);
        Assert.Equal("Amara Novak", result.Value.AttendeeName);
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
    public async Task AnOptionWhoseEventHasBeenCancelledIsNotShown()
    {
        _events.Items[1].Cancel();

        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.Equal(2, result.Value.Options.Count);
        Assert.DoesNotContain(
            new DateOnly(2026, 9, 11), result.Value.Options.Select(o => o.Date));
    }

    /// <summary>Ensures options on the transitional-location date or earlier are not projected.</summary>
    [Fact]
    public async Task OptionsOnTodayAndEarlierAreNotShown()
    {
        _invite.RemoveOption(_events.Items[0].Id);
        _invite.RemoveOption(_events.Items[1].Id);
        _events.Items[0].Cancel();
        _events.Items[1].Cancel();
        _invite.AddOption(AddEvent(3, 9));
        _invite.AddOption(AddEvent(2, 13));

        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { new DateOnly(2026, 9, 13) }, result.Value.Options.Select(o => o.Date));
    }

    private Guid AddEvent(int day, int hour)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, day), new TimeOnly(hour, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Add(eventItem);
        return eventItem.Id;
    }
}
`````

## after — tests/EventBooking.Application.Tests/Bookings/ViewInviteHandlerTests.cs — 1/1

<!-- retirement-file: {"id":38,"file":"tests/EventBooking.Application.Tests/Bookings/ViewInviteHandlerTests.cs","beforeSha":"45574863712b9f45549cb9df4f03bc65c076d801b73a145568bd9a4e6264f082","afterSha":"adfdbd61472877acc92c35831029131440f53ebd832d9911e5308f271a209d0e","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Bookings;

public class ViewInviteHandlerTests
{
    private const string InvalidLink = "This booking link is no longer valid.";

    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly FakeTokenService _tokens = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Attendee _attendee;
    private readonly Invite _invite;
    private readonly string _token;

    private ViewInviteHandler Handler => new(
        _invites, _attendees, _events, new EligibleEventFinder(_events, _clock),
        _audit, _unitOfWork, _tokens, _clock);

    public ViewInviteHandlerTests()
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

    [Fact]
    public async Task AValidTokenReturnsTheAttendeeTheirTypesAndThreeOptions()
    {
        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(_invite.Id, result.Value.InviteId);
        Assert.Equal("Amara Novak", result.Value.AttendeeName);
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
    public async Task AnOptionWhoseEventHasBeenCancelledIsNotShown()
    {
        _events.Items[1].Cancel();

        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.Equal(2, result.Value.Options.Count);
        Assert.DoesNotContain(
            new DateOnly(2026, 9, 11), result.Value.Options.Select(o => o.Date));
    }

    /// <summary>Ensures options on the transitional-location date or earlier are not projected.</summary>
    [Fact]
    public async Task OptionsOnTodayAndEarlierAreNotShown()
    {
        _invite.RemoveOption(_events.Items[0].Id);
        _invite.RemoveOption(_events.Items[1].Id);
        _events.Items[0].Cancel();
        _events.Items[1].Cancel();
        _invite.AddOption(AddEvent(3, 9));
        _invite.AddOption(AddEvent(2, 13));

        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { new DateOnly(2026, 9, 13) }, result.Value.Options.Select(o => o.Date));
    }

    private Guid AddEvent(int day, int hour)
    {
        var proposal = EventProposal.Create(
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
`````

## before — tests/EventBooking.Application.Tests/Events/AcceptProposalHandlerTests.cs — 1/1

<!-- retirement-file: {"id":39,"file":"tests/EventBooking.Application.Tests/Events/AcceptProposalHandlerTests.cs","beforeSha":"421878855433fe4b2b4a18f0b2be205b1035bfd5286f87ddb7b85180b953f480","afterSha":"57544c640f168de3f24d880e04f322c9aefbbe9af52cc6706e9b6deffa027cd6","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Common;
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

public class AcceptProposalHandlerTests
{
    private static readonly Guid DrugAndAlcoholManager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid MedicalManager = Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid UniformManager = Guid.Parse("c0000003-0000-0000-0000-000000000003");
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemoryEventProposalRepository _proposals = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly EventProposal _proposal;

    private AcceptProposalHandler Handler =>
        new(_proposals, _events, _roles, _unitOfWork, _audit);

    public AcceptProposalHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            DrugAndAlcoholManager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(
            MedicalManager, Role.Manager, AppointmentTypeIds.MedicalCheckUp));
        _roles.Add(StaffAccessProfile.Create(
            UniformManager, Role.Manager, AppointmentTypeIds.UniformFitting));
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));

        _proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
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
        Assert.Null(result.Value.EventId);
        Assert.Equal(_proposal.Id, result.Value.ProposalId);

        var acceptance = Assert.Single(_proposal.Acceptances);
        Assert.Equal(AppointmentTypeIds.DrugAndAlcoholTesting, acceptance.AppointmentTypeId);
        Assert.Equal(10, acceptance.Headcount);
        Assert.Empty(_events.Items);
        Assert.Equal(EventProposalStatus.Open, _proposal.Status);
    }

    [Fact]
    public async Task TheThirdAcceptConfirmsTheEventAndInitialisesCapacity()
    {
        await Accept(DrugAndAlcoholManager, 10);
        await Accept(MedicalManager, 6);

        var result = await Accept(UniformManager, 8);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.EventId);

        var eventItem = Assert.Single(_events.Items);
        Assert.Equal(result.Value.EventId, eventItem.Id);
        Assert.Equal(_proposal.Id, eventItem.ProposalId);
        Assert.Equal(_proposal.Window, eventItem.Window);
        Assert.Equal(EventStatus.Active, eventItem.Status);
        Assert.Equal(EventProposalStatus.Confirmed, _proposal.Status);

        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(6, eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(8, eventItem.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
    }

    [Fact]
    public async Task ConfirmationWritesBothAnAcceptanceAndAConfirmationAuditEntry()
    {
        await Accept(DrugAndAlcoholManager, 10);
        await Accept(MedicalManager, 6);
        await Accept(UniformManager, 8);

        Assert.Equal(3, _audit.Entries.Count(e => e.Action == AuditAction.AcceptanceRecorded));
        var confirmation = Assert.Single(_audit.Entries, e => e.Action == AuditAction.EventConfirmed);
        Assert.Equal(AuditEntityTypes.Event, confirmation.EntityType);
        Assert.Equal(ActorType.Staff, confirmation.ActorType);
    }

    [Fact]
    public async Task AManagerCanReviseTheirAcceptanceWhileTheProposalIsOpen()
    {
        await Accept(DrugAndAlcoholManager, 10);

        var result = await Accept(DrugAndAlcoholManager, 12);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.EventId);
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

## after — tests/EventBooking.Application.Tests/Events/AcceptProposalHandlerTests.cs — 1/1

<!-- retirement-file: {"id":39,"file":"tests/EventBooking.Application.Tests/Events/AcceptProposalHandlerTests.cs","beforeSha":"421878855433fe4b2b4a18f0b2be205b1035bfd5286f87ddb7b85180b953f480","afterSha":"57544c640f168de3f24d880e04f322c9aefbbe9af52cc6706e9b6deffa027cd6","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Common;
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

public class AcceptProposalHandlerTests
{
    private static readonly Guid DrugAndAlcoholManager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid MedicalManager = Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid UniformManager = Guid.Parse("c0000003-0000-0000-0000-000000000003");
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemoryEventProposalRepository _proposals = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly EventProposal _proposal;

    private AcceptProposalHandler Handler =>
        new(_proposals, _events, _roles, _unitOfWork, _audit);

    public AcceptProposalHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            DrugAndAlcoholManager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(
            MedicalManager, Role.Manager, AppointmentTypeIds.MedicalCheckUp));
        _roles.Add(StaffAccessProfile.Create(
            UniformManager, Role.Manager, AppointmentTypeIds.UniformFitting));
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));

        _proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
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
        Assert.Null(result.Value.EventId);
        Assert.Equal(_proposal.Id, result.Value.ProposalId);

        var acceptance = Assert.Single(_proposal.Acceptances);
        Assert.Equal(AppointmentTypeIds.DrugAndAlcoholTesting, acceptance.AppointmentTypeId);
        Assert.Equal(10, acceptance.Headcount);
        Assert.Empty(_events.Items);
        Assert.Equal(EventProposalStatus.Open, _proposal.Status);
    }

    [Fact]
    public async Task TheThirdAcceptConfirmsTheEventAndInitialisesCapacity()
    {
        await Accept(DrugAndAlcoholManager, 10);
        await Accept(MedicalManager, 6);

        var result = await Accept(UniformManager, 8);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.EventId);

        var eventItem = Assert.Single(_events.Items);
        Assert.Equal(result.Value.EventId, eventItem.Id);
        Assert.Equal(_proposal.Id, eventItem.ProposalId);
        Assert.Equal(_proposal.Window, eventItem.Window);
        Assert.Equal(EventStatus.Active, eventItem.Status);
        Assert.Equal(EventProposalStatus.Confirmed, _proposal.Status);

        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(6, eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(8, eventItem.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
    }

    [Fact]
    public async Task ConfirmationWritesBothAnAcceptanceAndAConfirmationAuditEntry()
    {
        await Accept(DrugAndAlcoholManager, 10);
        await Accept(MedicalManager, 6);
        await Accept(UniformManager, 8);

        Assert.Equal(3, _audit.Entries.Count(e => e.Action == AuditAction.AcceptanceRecorded));
        var confirmation = Assert.Single(_audit.Entries, e => e.Action == AuditAction.EventConfirmed);
        Assert.Equal(AuditEntityTypes.Event, confirmation.EntityType);
        Assert.Equal(ActorType.Staff, confirmation.ActorType);
    }

    [Fact]
    public async Task AManagerCanReviseTheirAcceptanceWhileTheProposalIsOpen()
    {
        await Accept(DrugAndAlcoholManager, 10);

        var result = await Accept(DrugAndAlcoholManager, 12);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.EventId);
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

## before — tests/EventBooking.Application.Tests/Events/AcceptProposalHeadcountRevisionTests.cs — 1/1

<!-- retirement-file: {"id":40,"file":"tests/EventBooking.Application.Tests/Events/AcceptProposalHeadcountRevisionTests.cs","beforeSha":"2da4b34a7423fa55e0d4d7eb3ed2e0e0581ca267c9941f7796a1a3a9f4b5d544","afterSha":"15c9c8049e469d4f090bfa3774e82dd294c3b5df8147c5b476ed03f0bb253a6d","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Common;
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

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

    private readonly InMemoryEventProposalRepository _proposals = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly EventProposal _proposal;

    private AcceptProposalHandler Handler =>
        new(_proposals, _events, _roles, _unitOfWork, _audit);

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

        _proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
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
        Assert.Null(revised.Value.EventId);
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
        Assert.Null(repeated.Value.EventId);
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
        Assert.NotNull(confirmed.Value.EventId);
        var eventItem = Assert.Single(_events.Items);
        Assert.Equal(
            12,
            eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).TotalHeadcount);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Events/AcceptProposalHeadcountRevisionTests.cs — 1/1

<!-- retirement-file: {"id":40,"file":"tests/EventBooking.Application.Tests/Events/AcceptProposalHeadcountRevisionTests.cs","beforeSha":"2da4b34a7423fa55e0d4d7eb3ed2e0e0581ca267c9941f7796a1a3a9f4b5d544","afterSha":"15c9c8049e469d4f090bfa3774e82dd294c3b5df8147c5b476ed03f0bb253a6d","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Common;
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

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

    private readonly InMemoryEventProposalRepository _proposals = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly EventProposal _proposal;

    private AcceptProposalHandler Handler =>
        new(_proposals, _events, _roles, _unitOfWork, _audit);

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

        _proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
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
        Assert.Null(revised.Value.EventId);
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
        Assert.Null(repeated.Value.EventId);
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
        Assert.NotNull(confirmed.Value.EventId);
        var eventItem = Assert.Single(_events.Items);
        Assert.Equal(
            12,
            eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).TotalHeadcount);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Events/AdjustEventCapacityHandlerTests.cs — 1/1

<!-- retirement-file: {"id":41,"file":"tests/EventBooking.Application.Tests/Events/AdjustEventCapacityHandlerTests.cs","beforeSha":"4d85b414d66172bc4fc206a799dbcf26455572682c2fffeea02084be1df85870","afterSha":"b7e024c27bff5cd9ec98110ac119d310795635e1c666ac6f304d15ba1e7ea72c","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

public class AdjustEventCapacityHandlerTests
{
    private static readonly Guid DrugAndAlcoholManager =
        Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid Coordinator =
        Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly Event _event;
    private readonly InMemoryEventCapacityRepository _capacities;

    private AdjustEventCapacityHandler Handler =>
        new(_events, _capacities, _roles, _unitOfWork, _audit);

    public AdjustEventCapacityHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            DrugAndAlcoholManager,
            Role.Manager,
            AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));

        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            DrugAndAlcoholManager);
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        _event = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Add(_event);
        _capacities = new InMemoryEventCapacityRepository(_events);
    }

    private Task<EventBooking.Application.Common.Result<AdjustEventCapacityOutcome>>
        Adjust(int totalHeadcount) =>
        Handler.HandleAsync(
            new AdjustEventCapacityCommand(
                DrugAndAlcoholManager, _event.Id, totalHeadcount),
            CancellationToken.None);

    private void Occupy(int count)
    {
        var capacity = _event.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);
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
        Assert.Equal(AuditEntityTypes.Event, entry.EntityType);
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
        var capacity = _event.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);
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
        Assert.Equal(10, _event.CapacityFor(
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
        Assert.Equal(12, _event.CapacityFor(
            AppointmentTypeIds.DrugAndAlcoholTesting).TotalHeadcount);
        Assert.Equal(6, _event.CapacityFor(
            AppointmentTypeIds.MedicalCheckUp).TotalHeadcount);
        Assert.Equal(8, _event.CapacityFor(
            AppointmentTypeIds.UniformFitting).TotalHeadcount);
    }

    [Fact]
    public async Task ACoordinatorCannotAdjustCapacity()
    {
        var result = await Handler.HandleAsync(
            new AdjustEventCapacityCommand(Coordinator, _event.Id, 12),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(0, _capacities.LockCallCount);
    }

    [Fact]
    public async Task ACancelledEventCannotBeAdjusted()
    {
        _event.Cancel();

        var result = await Adjust(12);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("A cancelled event cannot have its capacity adjusted.", result.Error.Message);
        Assert.Equal(1, _capacities.LockCallCount);
    }

    [Fact]
    public async Task AnUnknownEventIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new AdjustEventCapacityCommand(
                DrugAndAlcoholManager, Guid.NewGuid(), 12),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Equal(1, _capacities.LockCallCount);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Events/AdjustEventCapacityHandlerTests.cs — 1/1

<!-- retirement-file: {"id":41,"file":"tests/EventBooking.Application.Tests/Events/AdjustEventCapacityHandlerTests.cs","beforeSha":"4d85b414d66172bc4fc206a799dbcf26455572682c2fffeea02084be1df85870","afterSha":"b7e024c27bff5cd9ec98110ac119d310795635e1c666ac6f304d15ba1e7ea72c","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

public class AdjustEventCapacityHandlerTests
{
    private static readonly Guid DrugAndAlcoholManager =
        Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid Coordinator =
        Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly Event _event;
    private readonly InMemoryEventCapacityRepository _capacities;

    private AdjustEventCapacityHandler Handler =>
        new(_events, _capacities, _roles, _unitOfWork, _audit);

    public AdjustEventCapacityHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            DrugAndAlcoholManager,
            Role.Manager,
            AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));

        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            DrugAndAlcoholManager);
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        _event = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Add(_event);
        _capacities = new InMemoryEventCapacityRepository(_events);
    }

    private Task<EventBooking.Application.Common.Result<AdjustEventCapacityOutcome>>
        Adjust(int totalHeadcount) =>
        Handler.HandleAsync(
            new AdjustEventCapacityCommand(
                DrugAndAlcoholManager, _event.Id, totalHeadcount),
            CancellationToken.None);

    private void Occupy(int count)
    {
        var capacity = _event.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);
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
        Assert.Equal(AuditEntityTypes.Event, entry.EntityType);
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
        var capacity = _event.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);
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
        Assert.Equal(10, _event.CapacityFor(
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
        Assert.Equal(12, _event.CapacityFor(
            AppointmentTypeIds.DrugAndAlcoholTesting).TotalHeadcount);
        Assert.Equal(6, _event.CapacityFor(
            AppointmentTypeIds.MedicalCheckUp).TotalHeadcount);
        Assert.Equal(8, _event.CapacityFor(
            AppointmentTypeIds.UniformFitting).TotalHeadcount);
    }

    [Fact]
    public async Task ACoordinatorCannotAdjustCapacity()
    {
        var result = await Handler.HandleAsync(
            new AdjustEventCapacityCommand(Coordinator, _event.Id, 12),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(0, _capacities.LockCallCount);
    }

    [Fact]
    public async Task ACancelledEventCannotBeAdjusted()
    {
        _event.Cancel();

        var result = await Adjust(12);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("A cancelled event cannot have its capacity adjusted.", result.Error.Message);
        Assert.Equal(1, _capacities.LockCallCount);
    }

    [Fact]
    public async Task AnUnknownEventIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new AdjustEventCapacityCommand(
                DrugAndAlcoholManager, Guid.NewGuid(), 12),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Equal(1, _capacities.LockCallCount);
    }
}
`````
