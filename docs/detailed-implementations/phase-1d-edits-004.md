# 01d — Capacity generalised to N rows, edits 4 (Task 7)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.Application.Tests/Bookings/ViewInviteHandlerTests.cs — 1/1

<!-- retirement-file: {"id":9,"file":"tests/EventBooking.Application.Tests/Bookings/ViewInviteHandlerTests.cs","beforeSha":"d765307eb0809e6183ffd7380f2f320130566018a495adf194522be07443ccb4","afterSha":"cd9eca6fcd558de852a00703a6b859e9d9a625de1975172887fb846133dbd717","side":"after","part":1,"parts":1} -->

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
        _events.Items[1].CancelBeforeStart();

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
        _events.Items[0].CancelBeforeStart();
        _events.Items[1].CancelBeforeStart();
        _invite.AddOption(AddEvent(3, 9));
        _invite.AddOption(AddEvent(2, 13));

        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { new DateOnly(2026, 9, 13) }, result.Value.Options.Select(o => o.Date));
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
`````

## before — tests/EventBooking.Application.Tests/Events/AdjustEventCapacityHandlerTests.cs — 1/1

<!-- retirement-file: {"id":10,"file":"tests/EventBooking.Application.Tests/Events/AdjustEventCapacityHandlerTests.cs","beforeSha":"ac82ea6025c2e63f23330cfea9274b0c970ba134c8f2df8186ebaecc3fee87da","afterSha":"8b0cf6e8082db87e5b56548d0dbdad5f97faad3852f4c078a2ac1ad21a75f290","side":"before","part":1,"parts":1} -->

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

        var proposal = ProposalFixture.Create(
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

## after — tests/EventBooking.Application.Tests/Events/AdjustEventCapacityHandlerTests.cs — 1/1

<!-- retirement-file: {"id":10,"file":"tests/EventBooking.Application.Tests/Events/AdjustEventCapacityHandlerTests.cs","beforeSha":"ac82ea6025c2e63f23330cfea9274b0c970ba134c8f2df8186ebaecc3fee87da","afterSha":"8b0cf6e8082db87e5b56548d0dbdad5f97faad3852f4c078a2ac1ad21a75f290","side":"after","part":1,"parts":1} -->

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

        var proposal = ProposalFixture.Create(
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
        _event.CancelBeforeStart();

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

## before — tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs — 1/1

<!-- retirement-file: {"id":11,"file":"tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs","beforeSha":"aa1c7a4e71e6f0c4982f4ee4d2158c75abbfe9beb5ccd8a859351d9d491342c0","afterSha":"4112f0e0cdbddd2292a274609114e3de7a50670d342c941c0dd869ff59e019da","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

public class CancelEventHandlerTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly AttendeePortalOptions Portal = new(
        "https://booking.example.com", "recruitment@corp.com");

    private readonly TransactionOperationLog _operations = new();
    private readonly InMemoryEventRepository _events;
    private readonly InMemoryBookingRepository _bookings;
    private readonly InMemoryBookingAppointmentRepository _appointments;
    private readonly InMemoryAttendeeRepository _attendees;
    private readonly InMemoryInviteRepository _invites;
    private readonly InMemoryAttendeeGroupRepository _groups = new();
    private readonly InMemorySystemSettingsRepository _settings = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly RecordingEmailSender _email = new();
    private readonly InMemoryEmailDeliveryRepository _deliveries = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork;
    private readonly FakeTokenService _tokens = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly Event _event;

    private CancelEventHandler Handler => new(
        _events,
        _bookings,
        _invites,
        _attendees,
        _roles,
        new BookingCanceller(_appointments, new InMemoryEventCapacityRepository(_events), _audit),
        new InviteIssuer(
            _invites, _groups, new EligibleEventFinder(_events, _clock), _settings,
            _tokens, EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
            _audit, _clock, Portal),
        new EligibleEventFinder(_events, _clock),
        _appointments,
        EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
        _audit,
        _clock,
        _unitOfWork);

    public CancelEventHandlerTests()
    {
        _invites = new InMemoryInviteRepository(_operations);
        _events = new InMemoryEventRepository(_operations);
        _bookings = new InMemoryBookingRepository(_operations);
        _appointments = new InMemoryBookingAppointmentRepository(_bookings, _operations);
        _attendees = new InMemoryAttendeeRepository(_operations);
        _unitOfWork = new FakeUnitOfWork(_operations);
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));

        _event = AddEvent(10);
        AddEvent(12);
        AddEvent(14);
        AddEvent(16);
    }

    [Fact]
    public async Task APastEventCannotBeCancelled()
    {
        var past = AddEvent(1);

        var result = await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, past.Id, true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(EventStatus.Active, past.Status);
    }

    [Fact]
    public async Task AEventWithNoBookingsIsCancelledWithoutConfirmation()
    {
        var result = await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, _event.Id, false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.BookingsVoided);
        Assert.Equal(EventStatus.Cancelled, _event.Status);
        Assert.True(_audit.Contains(AuditAction.EventCancelled));
    }

    [Fact]
    public async Task AppointmentStaffIsDeniedBeforeTransactionOrEventLock()
    {
        var appointmentStaff = Guid.NewGuid();
        ((IStaffAccessProfileRepository)_roles).Add(StaffAccessProfile.Create(
            appointmentStaff,
            [Role.AppointmentStaff],
            AppointmentTypeIds.DrugAndAlcoholTesting));

        var result = await Handler.HandleAsync(
            new CancelEventCommand(appointmentStaff, _event.Id, true),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.DoesNotContain("transaction-begun", _operations.Events);
        Assert.DoesNotContain("event-guard-locked", _operations.Events);
        Assert.Equal(EventStatus.Active, _event.Status);
    }

    [Fact]
    public async Task AEventWithBookingsNeedsConfirmationAndSaysHowMany()
    {
        BookAAttendee("Amara Novak", "a.novak@mail.com");
        BookAAttendee("B. Chen", "b.chen@mail.com");

        var result = await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, _event.Id, false), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(
            "Cancelling this event will cancel 2 confirmed bookings. Affected attendees will be notified and re-invited. Confirm to proceed.",
            result.Error.Message);
        Assert.Equal(EventStatus.Active, _event.Status);
    }

    /// <summary>Verifies cancellation uses one business commit and one delivery result commit per message.</summary>
    [Fact]
    public async Task ConfirmedCancellationVoidsEveryBookingAndReInvitesEveryAttendee()
    {
        var amara = BookAAttendee("Amara Novak", "a.novak@mail.com");
        var chen = BookAAttendee("B. Chen", "b.chen@mail.com");

        var result = await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, _event.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.BookingsVoided);
        Assert.Equal(2, result.Value.AttendeesReinvited);

        Assert.All(_bookings.Items, b => Assert.Equal(BookingStatus.Cancelled, b.Status));
        Assert.Equal(AttendeeStatus.Invited, amara.Status);
        Assert.Equal(AttendeeStatus.Invited, chen.Status);
        Assert.Equal(EventStatus.Cancelled, _event.Status);
        Assert.Equal(10, _event.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(5, _unitOfWork.CommitCount);
    }

    /// <summary>Verifies attendee lifecycle locks are acquired before the event guard.</summary>
    [Fact]
    public async Task CancellationTakesAttendeeLifecycleLocksBeforeTheEventGuard()
    {
        BookAAttendee("Amara Novak", "a.novak@mail.com");

        await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, _event.Id, true), CancellationToken.None);

        Assert.Equal(
            ["active-attendee-ids-snapshotted", "transaction-begun", "attendee-locked", "pending-invites-locked", "original-booking-locked", "active-recovery-locked", "event-guard-locked", "active-bookings-listed"],
            _operations.Events.Take(8));
    }

    [Fact]
    public async Task EachAffectedAttendeeGetsACancellationEmailThenAnInvite()
    {
        BookAAttendee("Amara Novak", "a.novak@mail.com");

        await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, _event.Id, true), CancellationToken.None);

        Assert.Equal(2, _email.Sent.Count);
        Assert.Equal(EmailTemplate.AttendeeInvite, _email.Sent[0].Template);
        Assert.Equal(EmailTemplate.EventCancelledRebookingNeeded, _email.Sent[1].Template);
    }

    /// <summary>Failed replacement delivery produces neutral cancellation wording.</summary>
    [Fact]
    public async Task AFailedReplacementDoesNotPromiseThatAnInviteIsOnItsWay()
    {
        BookAAttendee("Amara Novak", "a.novak@mail.com");
        _email.FailNextSend = true;

        var result = await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, _event.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var cancellation = Assert.Single(_email.Sent, message =>
            message.Template == EmailTemplate.EventCancelledRebookingNeeded);
        Assert.Contains("recruitment team will contact you", cancellation.TextBody);
        Assert.DoesNotContain("on its way", cancellation.TextBody);
    }

    [Fact]
    public async Task TheCancelledEventIsNeverOfferedInTheReplacementInvites()
    {
        BookAAttendee("Amara Novak", "a.novak@mail.com");

        await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, _event.Id, true), CancellationToken.None);

        var reissued = _invites.Items.Single(i => i.Status == InviteStatus.Pending);
        Assert.DoesNotContain(_event.Id, reissued.OfferedEventIds);
    }

    [Fact]
    public async Task CancellingAnAlreadyCancelledEventIsAConflict()
    {
        _event.Cancel();

        var result = await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, _event.Id, true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("This event has already been cancelled.", result.Error.Message);
    }

    [Fact]
    public async Task AnUnknownEventIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, Guid.NewGuid(), true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task AUserWithNoRoleIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new CancelEventCommand(Guid.NewGuid(), _event.Id, true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    private Attendee BookAAttendee(string name, string email)
    {
        var pilots = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        if (!_groups.Items.Any(group => group.Id == pilots.Id))
        {
            _groups.Items.Add(pilots);
        }

        var attendee = Attendee.Create(Guid.NewGuid(), name, email, pilots);
        _attendees.Add(attendee);

        var inviteId = Guid.NewGuid();
        var issued = _tokens.Issue(inviteId);
        var invite = Invite.CreateInitial(
            inviteId, attendee.Id, issued.TokenHash, _clock.UtcNow.AddDays(4),
            [_event.Id, _events.Items[1].Id, _events.Items[2].Id],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting], 0);
        _invites.Add(invite);
        attendee.MarkInvited();

        var bookingId = Guid.NewGuid();
        var manage = _tokens.Issue(bookingId);
        _bookings.Add(Booking.Create(bookingId, invite, _event.Id, manage.TokenHash, _clock.UtcNow));
        invite.MarkUsed();
        attendee.MarkBooked();

        _event.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), bookingId, AppointmentTypeIds.DrugAndAlcoholTesting));
        _event.CapacityFor(AppointmentTypeIds.UniformFitting).Decrement();
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), bookingId, AppointmentTypeIds.UniformFitting));

        return attendee;
    }

    [Fact]
    public async Task CancellingAEventHoldingAnActiveRecoveryIssuesAReplacement()
    {
        var attendee = BookAAttendee("Amara Novak", "a.novak@mail.com");
        var original = _bookings.Items.Single();
        var recoveryEvent = _events.Items[1];
        var recovery = AddActiveRecoveryOn(attendee, original, recoveryEvent);

        var result = await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, recoveryEvent.Id, true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.BookingsVoided);
        Assert.Equal(1, result.Value.AttendeesReinvited);
        Assert.Equal(BookingStatus.Cancelled, recovery.Status);
        Assert.Equal(BookingStatus.Active, original.Status);
        Assert.Equal(AttendeeStatus.Booked, attendee.Status);
        Assert.Equal(
            10, recoveryEvent.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);

        var replacement = Assert.Single(
            _invites.Items,
            i => i.RecoveryOfBookingId == original.Id && i.Status == InviteStatus.Pending);
        Assert.Equal(InviteStatus.Pending, replacement.Status);
        Assert.Equal([AppointmentTypeIds.DrugAndAlcoholTesting], replacement.RequiredAppointmentTypeIds);
        Assert.Equal(Invite.RequiredOptionCount, replacement.OfferedEventIds.Count);
        Assert.Contains(_event.Id, replacement.OfferedEventIds);

        Assert.True(_audit.Contains(AuditAction.BookingCancelled));
        Assert.True(_audit.Contains(AuditAction.RecoveryInviteCreated));
        Assert.Equal(2, _email.Sent.Count);
    }

    [Fact]
    public async Task WithoutReplacementEventsTheRecoveryStaysAvailable()
    {
        var attendee = BookAAttendee("Amara Novak", "a.novak@mail.com");
        var original = _bookings.Items.Single();
        var recoveryEvent = _events.Items[1];
        var recovery = AddActiveRecoveryOn(attendee, original, recoveryEvent);

        foreach (var eventItem in _events.Items.Where(s => s.Id != recoveryEvent.Id))
        {
            var capacity = eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);
            var remainingCapacity = capacity.RemainingCapacity;
            for (var i = 0; i < remainingCapacity; i++)
            {
                capacity.Decrement();
            }
        }

        var result = await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, recoveryEvent.Id, true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.BookingsVoided);
        Assert.Equal(0, result.Value.AttendeesReinvited);
        Assert.Equal(BookingStatus.Cancelled, recovery.Status);
        Assert.Equal(BookingStatus.Active, original.Status);
        Assert.Equal(AttendeeStatus.Booked, attendee.Status);
        Assert.DoesNotContain(
            _invites.Items,
            i => i.RecoveryOfBookingId == original.Id && i.Status == InviteStatus.Pending);
        Assert.Single(_email.Sent);
    }

    private Booking AddActiveRecoveryOn(
        Attendee attendee, Booking original, Event recoveryEvent)
    {
        _appointments.Items
            .Single(a => a.BookingId == original.Id
                && a.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting)
            .TransitionTo(BookingAppointmentStatus.NoShow, Coordinator, _clock.UtcNow, false, true);

        var recoveryInviteId = Guid.NewGuid();
        var issued = _tokens.Issue(recoveryInviteId);
        var recoveryInvite = Invite.CreateRecovery(
            recoveryInviteId, attendee.Id, original.Id, issued.TokenHash,
            _clock.UtcNow.AddDays(4),
            [recoveryEvent.Id, _events.Items[2].Id, _events.Items[3].Id],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        _invites.Add(recoveryInvite);

        var recoveryId = Guid.NewGuid();
        var manage = _tokens.Issue(recoveryId);
        var recovery = Booking.CreateRecovery(
            recoveryId, recoveryInvite, original, recoveryEvent.Id,
            manage.TokenHash, _clock.UtcNow);
        _bookings.Add(recovery);
        recoveryInvite.MarkUsed();

        recoveryEvent.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.DrugAndAlcoholTesting));

        return recovery;
    }

    private Event AddEvent(int day)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0), 240),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Add(eventItem);
        return eventItem;
    }
}
`````

## after — tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs — 1/1

<!-- retirement-file: {"id":11,"file":"tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs","beforeSha":"aa1c7a4e71e6f0c4982f4ee4d2158c75abbfe9beb5ccd8a859351d9d491342c0","afterSha":"4112f0e0cdbddd2292a274609114e3de7a50670d342c941c0dd869ff59e019da","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

public class CancelEventHandlerTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly AttendeePortalOptions Portal = new(
        "https://booking.example.com", "recruitment@corp.com");

    private readonly TransactionOperationLog _operations = new();
    private readonly InMemoryEventRepository _events;
    private readonly InMemoryBookingRepository _bookings;
    private readonly InMemoryBookingAppointmentRepository _appointments;
    private readonly InMemoryAttendeeRepository _attendees;
    private readonly InMemoryInviteRepository _invites;
    private readonly InMemoryAttendeeGroupRepository _groups = new();
    private readonly InMemorySystemSettingsRepository _settings = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly RecordingEmailSender _email = new();
    private readonly InMemoryEmailDeliveryRepository _deliveries = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork;
    private readonly FakeTokenService _tokens = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly Event _event;

    private CancelEventHandler Handler => new(
        _events,
        _bookings,
        _invites,
        _attendees,
        _roles,
        new BookingCanceller(_appointments, new InMemoryEventCapacityRepository(_events), _audit),
        new InviteIssuer(
            _invites, _groups, new EligibleEventFinder(_events, _clock), _settings,
            _tokens, EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
            _audit, _clock, Portal),
        new EligibleEventFinder(_events, _clock),
        _appointments,
        EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
        _audit,
        _clock,
        ProposalFixture.Zones,
        _unitOfWork);

    public CancelEventHandlerTests()
    {
        _invites = new InMemoryInviteRepository(_operations);
        _events = new InMemoryEventRepository(_operations);
        _bookings = new InMemoryBookingRepository(_operations);
        _appointments = new InMemoryBookingAppointmentRepository(_bookings, _operations);
        _attendees = new InMemoryAttendeeRepository(_operations);
        _unitOfWork = new FakeUnitOfWork(_operations);
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));

        _event = AddEvent(10);
        AddEvent(12);
        AddEvent(14);
        AddEvent(16);
    }

    [Fact]
    public async Task APastEventCannotBeCancelled()
    {
        var past = AddEvent(1);

        var result = await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, past.Id, true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(EventStatus.Active, past.Status);
    }

    [Fact]
    public async Task AEventWhoseWindowStartedEarlierTodayCannotBeCancelled()
    {
        var started = AddEventAt(new EventWindow(new DateOnly(2026, 9, 3), new TimeOnly(8, 0), 240));

        var result = await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, started.Id, true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(EventStatus.Active, started.Status);
    }

    [Fact]
    public async Task AEventLaterTodayCanStillBeCancelled()
    {
        var laterToday =
            AddEventAt(new EventWindow(new DateOnly(2026, 9, 3), new TimeOnly(14, 0), 240));

        var result = await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, laterToday.Id, false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EventStatus.Cancelled, laterToday.Status);
    }

    [Fact]
    public async Task AEventWithNoBookingsIsCancelledWithoutConfirmation()
    {
        var result = await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, _event.Id, false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.BookingsVoided);
        Assert.Equal(EventStatus.Cancelled, _event.Status);
        Assert.True(_audit.Contains(AuditAction.EventCancelled));
    }

    [Fact]
    public async Task AppointmentStaffIsDeniedBeforeTransactionOrEventLock()
    {
        var appointmentStaff = Guid.NewGuid();
        ((IStaffAccessProfileRepository)_roles).Add(StaffAccessProfile.Create(
            appointmentStaff,
            [Role.AppointmentStaff],
            AppointmentTypeIds.DrugAndAlcoholTesting));

        var result = await Handler.HandleAsync(
            new CancelEventCommand(appointmentStaff, _event.Id, true),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.DoesNotContain("transaction-begun", _operations.Events);
        Assert.DoesNotContain("event-guard-locked", _operations.Events);
        Assert.Equal(EventStatus.Active, _event.Status);
    }

    [Fact]
    public async Task AEventWithBookingsNeedsConfirmationAndSaysHowMany()
    {
        BookAAttendee("Amara Novak", "a.novak@mail.com");
        BookAAttendee("B. Chen", "b.chen@mail.com");

        var result = await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, _event.Id, false), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(
            "Cancelling this event will cancel 2 confirmed bookings. Affected attendees will be notified and re-invited. Confirm to proceed.",
            result.Error.Message);
        Assert.Equal(EventStatus.Active, _event.Status);
    }

    /// <summary>Verifies cancellation uses one business commit and one delivery result commit per message.</summary>
    [Fact]
    public async Task ConfirmedCancellationVoidsEveryBookingAndReInvitesEveryAttendee()
    {
        var amara = BookAAttendee("Amara Novak", "a.novak@mail.com");
        var chen = BookAAttendee("B. Chen", "b.chen@mail.com");

        var result = await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, _event.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.BookingsVoided);
        Assert.Equal(2, result.Value.AttendeesReinvited);

        Assert.All(_bookings.Items, b => Assert.Equal(BookingStatus.Cancelled, b.Status));
        Assert.Equal(AttendeeStatus.Invited, amara.Status);
        Assert.Equal(AttendeeStatus.Invited, chen.Status);
        Assert.Equal(EventStatus.Cancelled, _event.Status);
        Assert.Equal(10, _event.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(5, _unitOfWork.CommitCount);
    }

    /// <summary>Verifies attendee lifecycle locks are acquired before the event guard.</summary>
    [Fact]
    public async Task CancellationTakesAttendeeLifecycleLocksBeforeTheEventGuard()
    {
        BookAAttendee("Amara Novak", "a.novak@mail.com");

        await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, _event.Id, true), CancellationToken.None);

        Assert.Equal(
            ["active-attendee-ids-snapshotted", "transaction-begun", "attendee-locked", "pending-invites-locked", "original-booking-locked", "active-recovery-locked", "event-guard-locked", "active-bookings-listed"],
            _operations.Events.Take(8));
    }

    [Fact]
    public async Task EachAffectedAttendeeGetsACancellationEmailThenAnInvite()
    {
        BookAAttendee("Amara Novak", "a.novak@mail.com");

        await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, _event.Id, true), CancellationToken.None);

        Assert.Equal(2, _email.Sent.Count);
        Assert.Equal(EmailTemplate.AttendeeInvite, _email.Sent[0].Template);
        Assert.Equal(EmailTemplate.EventCancelledRebookingNeeded, _email.Sent[1].Template);
    }

    /// <summary>Failed replacement delivery produces neutral cancellation wording.</summary>
    [Fact]
    public async Task AFailedReplacementDoesNotPromiseThatAnInviteIsOnItsWay()
    {
        BookAAttendee("Amara Novak", "a.novak@mail.com");
        _email.FailNextSend = true;

        var result = await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, _event.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var cancellation = Assert.Single(_email.Sent, message =>
            message.Template == EmailTemplate.EventCancelledRebookingNeeded);
        Assert.Contains("recruitment team will contact you", cancellation.TextBody);
        Assert.DoesNotContain("on its way", cancellation.TextBody);
    }

    [Fact]
    public async Task TheCancelledEventIsNeverOfferedInTheReplacementInvites()
    {
        BookAAttendee("Amara Novak", "a.novak@mail.com");

        await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, _event.Id, true), CancellationToken.None);

        var reissued = _invites.Items.Single(i => i.Status == InviteStatus.Pending);
        Assert.DoesNotContain(_event.Id, reissued.OfferedEventIds);
    }

    [Fact]
    public async Task CancellingAnAlreadyCancelledEventIsAConflict()
    {
        _event.CancelBeforeStart();

        var result = await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, _event.Id, true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("This event has already been cancelled.", result.Error.Message);
    }

    [Fact]
    public async Task AnUnknownEventIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, Guid.NewGuid(), true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task AUserWithNoRoleIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new CancelEventCommand(Guid.NewGuid(), _event.Id, true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    private Attendee BookAAttendee(string name, string email)
    {
        var pilots = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        if (!_groups.Items.Any(group => group.Id == pilots.Id))
        {
            _groups.Items.Add(pilots);
        }

        var attendee = Attendee.Create(Guid.NewGuid(), name, email, pilots);
        _attendees.Add(attendee);

        var inviteId = Guid.NewGuid();
        var issued = _tokens.Issue(inviteId);
        var invite = Invite.CreateInitial(
            inviteId, attendee.Id, issued.TokenHash, _clock.UtcNow.AddDays(4),
            [_event.Id, _events.Items[1].Id, _events.Items[2].Id],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting], 0);
        _invites.Add(invite);
        attendee.MarkInvited();

        var bookingId = Guid.NewGuid();
        var manage = _tokens.Issue(bookingId);
        _bookings.Add(Booking.Create(bookingId, invite, _event.Id, manage.TokenHash, _clock.UtcNow));
        invite.MarkUsed();
        attendee.MarkBooked();

        _event.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), bookingId, AppointmentTypeIds.DrugAndAlcoholTesting));
        _event.CapacityFor(AppointmentTypeIds.UniformFitting).Decrement();
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), bookingId, AppointmentTypeIds.UniformFitting));

        return attendee;
    }

    [Fact]
    public async Task CancellingAEventHoldingAnActiveRecoveryIssuesAReplacement()
    {
        var attendee = BookAAttendee("Amara Novak", "a.novak@mail.com");
        var original = _bookings.Items.Single();
        var recoveryEvent = _events.Items[1];
        var recovery = AddActiveRecoveryOn(attendee, original, recoveryEvent);

        var result = await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, recoveryEvent.Id, true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.BookingsVoided);
        Assert.Equal(1, result.Value.AttendeesReinvited);
        Assert.Equal(BookingStatus.Cancelled, recovery.Status);
        Assert.Equal(BookingStatus.Active, original.Status);
        Assert.Equal(AttendeeStatus.Booked, attendee.Status);
        Assert.Equal(
            10, recoveryEvent.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);

        var replacement = Assert.Single(
            _invites.Items,
            i => i.RecoveryOfBookingId == original.Id && i.Status == InviteStatus.Pending);
        Assert.Equal(InviteStatus.Pending, replacement.Status);
        Assert.Equal([AppointmentTypeIds.DrugAndAlcoholTesting], replacement.RequiredAppointmentTypeIds);
        Assert.Equal(Invite.RequiredOptionCount, replacement.OfferedEventIds.Count);
        Assert.Contains(_event.Id, replacement.OfferedEventIds);

        Assert.True(_audit.Contains(AuditAction.BookingCancelled));
        Assert.True(_audit.Contains(AuditAction.RecoveryInviteCreated));
        Assert.Equal(2, _email.Sent.Count);
    }

    [Fact]
    public async Task WithoutReplacementEventsTheRecoveryStaysAvailable()
    {
        var attendee = BookAAttendee("Amara Novak", "a.novak@mail.com");
        var original = _bookings.Items.Single();
        var recoveryEvent = _events.Items[1];
        var recovery = AddActiveRecoveryOn(attendee, original, recoveryEvent);

        foreach (var eventItem in _events.Items.Where(s => s.Id != recoveryEvent.Id))
        {
            var capacity = eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);
            var remainingCapacity = capacity.RemainingCapacity;
            for (var i = 0; i < remainingCapacity; i++)
            {
                capacity.Decrement();
            }
        }

        var result = await Handler.HandleAsync(
            new CancelEventCommand(Coordinator, recoveryEvent.Id, true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.BookingsVoided);
        Assert.Equal(0, result.Value.AttendeesReinvited);
        Assert.Equal(BookingStatus.Cancelled, recovery.Status);
        Assert.Equal(BookingStatus.Active, original.Status);
        Assert.Equal(AttendeeStatus.Booked, attendee.Status);
        Assert.DoesNotContain(
            _invites.Items,
            i => i.RecoveryOfBookingId == original.Id && i.Status == InviteStatus.Pending);
        Assert.Single(_email.Sent);
    }

    private Booking AddActiveRecoveryOn(
        Attendee attendee, Booking original, Event recoveryEvent)
    {
        _appointments.Items
            .Single(a => a.BookingId == original.Id
                && a.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting)
            .TransitionTo(BookingAppointmentStatus.NoShow, Coordinator, _clock.UtcNow, false, true);

        var recoveryInviteId = Guid.NewGuid();
        var issued = _tokens.Issue(recoveryInviteId);
        var recoveryInvite = Invite.CreateRecovery(
            recoveryInviteId, attendee.Id, original.Id, issued.TokenHash,
            _clock.UtcNow.AddDays(4),
            [recoveryEvent.Id, _events.Items[2].Id, _events.Items[3].Id],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        _invites.Add(recoveryInvite);

        var recoveryId = Guid.NewGuid();
        var manage = _tokens.Issue(recoveryId);
        var recovery = Booking.CreateRecovery(
            recoveryId, recoveryInvite, original, recoveryEvent.Id,
            manage.TokenHash, _clock.UtcNow);
        _bookings.Add(recovery);
        recoveryInvite.MarkUsed();

        recoveryEvent.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.DrugAndAlcoholTesting));

        return recovery;
    }

    private Event AddEvent(int day) =>
        AddEventAt(new EventWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0), 240));

    private Event AddEventAt(EventWindow window)
    {
        var proposal = ProposalFixture.Create(Guid.NewGuid(), window, Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Add(eventItem);
        return eventItem;
    }
}
`````
