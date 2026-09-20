# 01c — Negotiation across any number of types, edits 15 (Task 6)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.Application.Tests/Events/WithdrawProposalHandlerTests.cs — 1/1

<!-- retirement-file: {"id":44,"file":"tests/EventBooking.Application.Tests/Events/WithdrawProposalHandlerTests.cs","beforeSha":"38b246754fbdb34702bc43212c15519ba485414989b4c6684adc88666eb4de07","afterSha":"d614798a933de539a74f2d638d3924651261e9bfaa93e353c4e6d36502d2b222","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

public class WithdrawProposalHandlerTests
{
    private static readonly Guid Creator = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid OtherManager = Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid Admin = Guid.Parse("a0000001-0000-0000-0000-000000000001");

    private readonly InMemoryEventProposalRepository _proposals = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly EventProposal _proposal;

    private WithdrawProposalHandler Handler => new(_proposals, _roles, _unitOfWork, _audit);

    public WithdrawProposalHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            Creator, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(
            OtherManager, Role.Manager, AppointmentTypeIds.MedicalCheckUp));
        _roles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));

        _proposal = ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            Creator);
        _proposals.Add(_proposal);
    }

    [Fact]
    public async Task TheCreatorCanWithdrawItAndItLeavesTheOpenList()
    {
        var result = await Handler.HandleAsync(
            new WithdrawProposalCommand(Creator, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EventProposalStatus.Withdrawn, _proposal.Status);
        Assert.Empty(await _proposals.ListOpenAsync(CancellationToken.None));
        Assert.True(_audit.Contains(AuditAction.ProposalWithdrawn));
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AManagerOfAnotherListedTypeCannotWithdrawIt()
    {
        // FR-2.9: the whole proposal belongs to the proposing type. Another listed type may only
        // withdraw its own acceptance.
        var result = await Handler.HandleAsync(
            new WithdrawProposalCommand(OtherManager, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EventProposalStatus.Open, _proposal.Status);
        Assert.False(_audit.Contains(AuditAction.ProposalWithdrawn));
    }

    [Fact]
    public async Task AnAdminCannotWithdrawIt()
    {
        // Admin holds no negotiation capability in the design's matrix, and has no appointment
        // type to act for; the predecessor's Admin fallback is retired.
        var result = await Handler.HandleAsync(
            new WithdrawProposalCommand(Admin, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EventProposalStatus.Open, _proposal.Status);
    }

    [Fact]
    public async Task AlreadyWithdrawnIsAConflict()
    {
        _proposal.Withdraw(ProposalFixture.ProposerType);

        var result = await Handler.HandleAsync(
            new WithdrawProposalCommand(Creator, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("Only an open proposal can be withdrawn.", result.Error.Message);
    }

    [Fact]
    public async Task AnUnknownProposalIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new WithdrawProposalCommand(Creator, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs — 1/1

<!-- retirement-file: {"id":45,"file":"tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs","beforeSha":"03352c0fafa322f59ff041a5918110d95f9040daad0234e05df39659c4f9ef96","afterSha":"e3e79753da28e354f3908d1677509cf1b98907787ac66727d1269694639400ba","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Fakes;

public class FakesSelfTests
{
    [Fact]
    public void TheClockCanBeMoved()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

        clock.Advance(TimeSpan.FromDays(5));

        Assert.Equal(new DateOnly(2026, 9, 8), clock.TodayAtTransitionalLocation);
    }

    [Fact]
    public async Task TheAttendeeRepositoryFiltersByStatus()
    {
        var repository = new InMemoryAttendeeRepository();
        var uniformOnly = AttendeeGroup.Define(
            Guid.NewGuid(), "UNI_ONLY", "UNI only", true, [AppointmentTypeIds.UniformFitting]);
        var invited = Attendee.Create(
            Guid.NewGuid(), "B. Chen", "b.chen@mail.com", uniformOnly);
        invited.MarkInvited();
        repository.Add(invited);
        repository.Add(Attendee.Create(
            Guid.NewGuid(), "A. Novak", "a.novak@mail.com", uniformOnly));

        var result = await repository.ListAsync(AttendeeStatus.Invited, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("b.chen@mail.com", result[0].Email);
    }

    [Fact]
    public async Task TheEventRepositoryHidesCancelledAndPastEvents()
    {
        var repository = new InMemoryEventRepository();
        repository.Add(EventFor(new DateOnly(2026, 9, 1)));
        var cancelled = EventFor(new DateOnly(2026, 9, 20));
        cancelled.Cancel();
        repository.Add(cancelled);
        repository.Add(EventFor(new DateOnly(2026, 9, 21)));

        var result = await repository.ListActiveAsync(new DateOnly(2026, 9, 3), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(new DateOnly(2026, 9, 21), result[0].Window.Date);
    }

    [Fact]
    public async Task TheCapacityRepositoryReturnsRowsInAppointmentTypeOrder()
    {
        var events = new InMemoryEventRepository();
        var eventItem = EventFor(new DateOnly(2026, 9, 21));
        events.Add(eventItem);
        var capacities = new InMemoryEventCapacityRepository(events);

        var locked = await capacities
            .LockForUpdateAsync(eventItem.Id, AppointmentTypeIds.All, CancellationToken.None);

        Assert.Equal(3, locked.Count);
        Assert.Equal(
            locked.Select(c => c.AppointmentTypeId).OrderBy(id => id),
            locked.Select(c => c.AppointmentTypeId));
        Assert.Equal(1, capacities.LockCallCount);
    }

    [Fact]
    public void TheTokenServiceRoundTripsAnIdentifier()
    {
        var service = new FakeTokenService();
        var id = Guid.NewGuid();

        var issued = service.Issue(id);

        Assert.True(service.TryRead(issued.Token, out var read));
        Assert.Equal(id, read);
        Assert.Equal(issued.TokenHash, service.Hash(issued.Token));
        Assert.False(service.TryRead("nonsense", out _));
    }

    [Fact]
    public void TheAuditLoggerRecordsWhatItIsGiven()
    {
        var logger = new RecordingAuditLogger();

        logger.Record(
            AuditEntityTypes.Booking, Guid.NewGuid(), AuditAction.BookingCreated,
            ActorType.AttendeeToken, "invite-1", "chose option 2");

        Assert.True(logger.Contains(AuditAction.BookingCreated));
        Assert.Equal("chose option 2", logger.Entries.Single().Details);
    }

    private static Event EventFor(DateOnly date)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(9, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        return Event.CreateFrom(Guid.NewGuid(), proposal);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs — 1/1

<!-- retirement-file: {"id":45,"file":"tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs","beforeSha":"03352c0fafa322f59ff041a5918110d95f9040daad0234e05df39659c4f9ef96","afterSha":"e3e79753da28e354f3908d1677509cf1b98907787ac66727d1269694639400ba","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Fakes;

public class FakesSelfTests
{
    [Fact]
    public void TheClockCanBeMoved()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

        clock.Advance(TimeSpan.FromDays(5));

        Assert.Equal(new DateOnly(2026, 9, 8), clock.TodayAtTransitionalLocation);
    }

    [Fact]
    public async Task TheAttendeeRepositoryFiltersByStatus()
    {
        var repository = new InMemoryAttendeeRepository();
        var uniformOnly = AttendeeGroup.Define(
            Guid.NewGuid(), "UNI_ONLY", "UNI only", true, [AppointmentTypeIds.UniformFitting]);
        var invited = Attendee.Create(
            Guid.NewGuid(), "B. Chen", "b.chen@mail.com", uniformOnly);
        invited.MarkInvited();
        repository.Add(invited);
        repository.Add(Attendee.Create(
            Guid.NewGuid(), "A. Novak", "a.novak@mail.com", uniformOnly));

        var result = await repository.ListAsync(AttendeeStatus.Invited, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("b.chen@mail.com", result[0].Email);
    }

    [Fact]
    public async Task TheEventRepositoryHidesCancelledAndPastEvents()
    {
        var repository = new InMemoryEventRepository();
        repository.Add(EventFor(new DateOnly(2026, 9, 1)));
        var cancelled = EventFor(new DateOnly(2026, 9, 20));
        cancelled.Cancel();
        repository.Add(cancelled);
        repository.Add(EventFor(new DateOnly(2026, 9, 21)));

        var result = await repository.ListActiveAsync(new DateOnly(2026, 9, 3), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(new DateOnly(2026, 9, 21), result[0].Window.Date);
    }

    [Fact]
    public async Task TheCapacityRepositoryReturnsRowsInAppointmentTypeOrder()
    {
        var events = new InMemoryEventRepository();
        var eventItem = EventFor(new DateOnly(2026, 9, 21));
        events.Add(eventItem);
        var capacities = new InMemoryEventCapacityRepository(events);

        var locked = await capacities
            .LockForUpdateAsync(eventItem.Id, AppointmentTypeIds.All, CancellationToken.None);

        Assert.Equal(3, locked.Count);
        Assert.Equal(
            locked.Select(c => c.AppointmentTypeId).OrderBy(id => id),
            locked.Select(c => c.AppointmentTypeId));
        Assert.Equal(1, capacities.LockCallCount);
    }

    [Fact]
    public void TheTokenServiceRoundTripsAnIdentifier()
    {
        var service = new FakeTokenService();
        var id = Guid.NewGuid();

        var issued = service.Issue(id);

        Assert.True(service.TryRead(issued.Token, out var read));
        Assert.Equal(id, read);
        Assert.Equal(issued.TokenHash, service.Hash(issued.Token));
        Assert.False(service.TryRead("nonsense", out _));
    }

    [Fact]
    public void TheAuditLoggerRecordsWhatItIsGiven()
    {
        var logger = new RecordingAuditLogger();

        logger.Record(
            AuditEntityTypes.Booking, Guid.NewGuid(), AuditAction.BookingCreated,
            ActorType.AttendeeToken, "invite-1", "chose option 2");

        Assert.True(logger.Contains(AuditAction.BookingCreated));
        Assert.Equal("chose option 2", logger.Entries.Single().Details);
    }

    private static Event EventFor(DateOnly date)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(9, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        return Event.CreateFrom(Guid.NewGuid(), proposal);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Fixtures/EventFixture.cs — 1/1

<!-- retirement-file: {"id":46,"file":"tests/EventBooking.Application.Tests/Fixtures/EventFixture.cs","beforeSha":"0c13090f3c6ad44a0345bbc0bdf398e7e8c71acbc46ef53a8d49dba1a3cfcb05","afterSha":"634f2ccbe40f83af3e57516aaa4ed776ca00e78b7bad7d56c2d9e9b169071d39","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;

internal static class EventFixture
{
    public static Event Create(Guid id, EventWindow window, IReadOnlyDictionary<Guid, int> headcounts)
    {
        var manager = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var proposal = EventProposal.Create(Guid.NewGuid(), window, manager);
        foreach (var type in AppointmentTypeIds.All)
            proposal.Accept(type, manager, headcounts[type]);
        return Event.CreateFrom(id, proposal);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Fixtures/EventFixture.cs — 1/1

<!-- retirement-file: {"id":46,"file":"tests/EventBooking.Application.Tests/Fixtures/EventFixture.cs","beforeSha":"0c13090f3c6ad44a0345bbc0bdf398e7e8c71acbc46ef53a8d49dba1a3cfcb05","afterSha":"634f2ccbe40f83af3e57516aaa4ed776ca00e78b7bad7d56c2d9e9b169071d39","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;

internal static class EventFixture
{
    public static Event Create(Guid id, EventWindow window, IReadOnlyDictionary<Guid, int> headcounts)
    {
        var manager = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var proposal = ProposalFixture.Create(Guid.NewGuid(), window, manager);
        foreach (var type in AppointmentTypeIds.All)
            proposal.Accept(type, manager, headcounts[type]);
        return Event.CreateFrom(id, proposal);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Invites/EligibleEventFinderTests.cs — 1/1

<!-- retirement-file: {"id":47,"file":"tests/EventBooking.Application.Tests/Invites/EligibleEventFinderTests.cs","beforeSha":"3272968ee17bcd67ef9cc48acdd3b13dead800e9005227973ebb2471d7e85324","afterSha":"48b195393be3fcdf881f8a0fa99d73981d6d58975adf63f33c06205cb936a626","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Invites;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Invites;

public class EligibleEventFinderTests
{
    private static readonly Guid[] NeedsTwo =
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting];

    private readonly InMemoryEventRepository _events = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    private EligibleEventFinder Finder => new(_events, _clock);

    [Fact]
    public async Task TheThreeEarliestQualifyingEventsAreReturnedInOrder()
    {
        AddEvent(new DateOnly(2026, 9, 14));
        AddEvent(new DateOnly(2026, 9, 10));
        AddEvent(new DateOnly(2026, 9, 12));
        AddEvent(new DateOnly(2026, 9, 16));

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.Equal(
            new[] { new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 14) },
            result.Select(s => s.Window.Date));
    }

    [Fact]
    public async Task TwoWindowsOnOneDayAreOrderedByStartTime()
    {
        AddEvent(new DateOnly(2026, 9, 10), startHour: 13);
        AddEvent(new DateOnly(2026, 9, 10), startHour: 9);

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Equal(
            new[] { new TimeOnly(9, 0), new TimeOnly(13, 0) },
            result.Select(s => s.Window.StartTime));
    }

    [Fact]
    public async Task AEventFullInOneRequiredTypeIsNotEligibleEvenIfTheOthersHaveRoom()
    {
        var eventItem = AddEvent(new DateOnly(2026, 9, 10), drugAndAlcoholHeadcount: 1);
        eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task AEventFullOnlyInATypeTheAttendeeDoesNotNeedIsStillEligible()
    {
        var eventItem = AddEvent(new DateOnly(2026, 9, 10), medicalHeadcount: 1);
        eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).Decrement();

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Single(result);
    }

    [Fact]
    public async Task CancelledEventsAreNeverEligible()
    {
        var eventItem = AddEvent(new DateOnly(2026, 9, 10));
        eventItem.Cancel();

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task TodaysAndPastWindowsAreNeverEligible()
    {
        AddEvent(new DateOnly(2026, 9, 3));
        AddEvent(new DateOnly(2026, 9, 1));

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ExcludedEventsAreSkipped()
    {
        var first = AddEvent(new DateOnly(2026, 9, 10));
        AddEvent(new DateOnly(2026, 9, 12));

        var result = await Finder.FindAsync(NeedsTwo, 3, [first.Id], CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 9, 12), Assert.Single(result).Window.Date);
    }

    [Fact]
    public async Task FewerQualifyingEventsThanAskedForReturnsWhatThereIs()
    {
        AddEvent(new DateOnly(2026, 9, 10));
        AddEvent(new DateOnly(2026, 9, 12));

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Equal(2, result.Count);
    }

    private Event AddEvent(
        DateOnly date,
        int startHour = 9,
        int drugAndAlcoholHeadcount = 10,
        int medicalHeadcount = 6,
        int uniformHeadcount = 8)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(startHour, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), drugAndAlcoholHeadcount);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), medicalHeadcount);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), uniformHeadcount);

        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Add(eventItem);
        return eventItem;
    }
}
`````

## after — tests/EventBooking.Application.Tests/Invites/EligibleEventFinderTests.cs — 1/1

<!-- retirement-file: {"id":47,"file":"tests/EventBooking.Application.Tests/Invites/EligibleEventFinderTests.cs","beforeSha":"3272968ee17bcd67ef9cc48acdd3b13dead800e9005227973ebb2471d7e85324","afterSha":"48b195393be3fcdf881f8a0fa99d73981d6d58975adf63f33c06205cb936a626","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Invites;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Invites;

public class EligibleEventFinderTests
{
    private static readonly Guid[] NeedsTwo =
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting];

    private readonly InMemoryEventRepository _events = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    private EligibleEventFinder Finder => new(_events, _clock);

    [Fact]
    public async Task TheThreeEarliestQualifyingEventsAreReturnedInOrder()
    {
        AddEvent(new DateOnly(2026, 9, 14));
        AddEvent(new DateOnly(2026, 9, 10));
        AddEvent(new DateOnly(2026, 9, 12));
        AddEvent(new DateOnly(2026, 9, 16));

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.Equal(
            new[] { new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 14) },
            result.Select(s => s.Window.Date));
    }

    [Fact]
    public async Task TwoWindowsOnOneDayAreOrderedByStartTime()
    {
        AddEvent(new DateOnly(2026, 9, 10), startHour: 13);
        AddEvent(new DateOnly(2026, 9, 10), startHour: 9);

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Equal(
            new[] { new TimeOnly(9, 0), new TimeOnly(13, 0) },
            result.Select(s => s.Window.StartTime));
    }

    [Fact]
    public async Task AEventFullInOneRequiredTypeIsNotEligibleEvenIfTheOthersHaveRoom()
    {
        var eventItem = AddEvent(new DateOnly(2026, 9, 10), drugAndAlcoholHeadcount: 1);
        eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task AEventFullOnlyInATypeTheAttendeeDoesNotNeedIsStillEligible()
    {
        var eventItem = AddEvent(new DateOnly(2026, 9, 10), medicalHeadcount: 1);
        eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).Decrement();

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Single(result);
    }

    [Fact]
    public async Task CancelledEventsAreNeverEligible()
    {
        var eventItem = AddEvent(new DateOnly(2026, 9, 10));
        eventItem.Cancel();

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task TodaysAndPastWindowsAreNeverEligible()
    {
        AddEvent(new DateOnly(2026, 9, 3));
        AddEvent(new DateOnly(2026, 9, 1));

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ExcludedEventsAreSkipped()
    {
        var first = AddEvent(new DateOnly(2026, 9, 10));
        AddEvent(new DateOnly(2026, 9, 12));

        var result = await Finder.FindAsync(NeedsTwo, 3, [first.Id], CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 9, 12), Assert.Single(result).Window.Date);
    }

    [Fact]
    public async Task FewerQualifyingEventsThanAskedForReturnsWhatThereIs()
    {
        AddEvent(new DateOnly(2026, 9, 10));
        AddEvent(new DateOnly(2026, 9, 12));

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Equal(2, result.Count);
    }

    private Event AddEvent(
        DateOnly date,
        int startHour = 9,
        int drugAndAlcoholHeadcount = 10,
        int medicalHeadcount = 6,
        int uniformHeadcount = 8)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(startHour, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), drugAndAlcoholHeadcount);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), medicalHeadcount);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), uniformHeadcount);

        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Add(eventItem);
        return eventItem;
    }
}
`````

## before — tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs — 1/1

<!-- retirement-file: {"id":48,"file":"tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs","beforeSha":"d6b2924aa290999f8ac69c5d288c6eac4eb7cf76546a20c33456e9bef5701ffd","afterSha":"671d2663e7eec638e661c0355381d6d33768424b5aa556cb3d0bbed2ea25d2f2","side":"before","part":1,"parts":1} -->

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
        _attendee = Attendee.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", pilots);
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
    public async Task AReIssueWithNoEligibleEventsFlagsAwaitingAvailabilityInstead()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: 0);
        _events.Items.Clear();

        var summary = await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(1, summary.Expired);
        Assert.Equal(0, summary.ReIssued);
        Assert.Equal(AttendeeStatus.AwaitingAvailability, _attendee.Status);
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
        _attendee.MarkInvited();
        _attendee.MarkBooked();
        _invites.Add(Invite.CreateRecovery(
            Guid.NewGuid(),
            _attendee.Id,
            Guid.NewGuid(),
            $"hash-{Guid.NewGuid():N}",
            _clock.UtcNow.AddDays(-1),
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
        _attendee.MarkInvited();
        _invites.Add(Invite.CreateInitial(
            Guid.NewGuid(),
            _attendee.Id,
            $"hash-{Guid.NewGuid():N}",
            _clock.UtcNow.AddDays(expiresInDays),
            _events.Items.Take(3).Select(s => s.Id),
            _attendee.RequiredAppointmentTypeIds,
            retryCount));
    }

    private void AddThreeEvents()
    {
        foreach (var day in new[] { 10, 12, 14 })
        {
            var proposal = EventProposal.Create(
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

<!-- retirement-file: {"id":48,"file":"tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs","beforeSha":"d6b2924aa290999f8ac69c5d288c6eac4eb7cf76546a20c33456e9bef5701ffd","afterSha":"671d2663e7eec638e661c0355381d6d33768424b5aa556cb3d0bbed2ea25d2f2","side":"after","part":1,"parts":1} -->

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
        _attendee = Attendee.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", pilots);
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
    public async Task AReIssueWithNoEligibleEventsFlagsAwaitingAvailabilityInstead()
    {
        GivePendingInvite(expiresInDays: -1, retryCount: 0);
        _events.Items.Clear();

        var summary = await Handler.HandleAsync(CancellationToken.None);

        Assert.Equal(1, summary.Expired);
        Assert.Equal(0, summary.ReIssued);
        Assert.Equal(AttendeeStatus.AwaitingAvailability, _attendee.Status);
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
        _attendee.MarkInvited();
        _attendee.MarkBooked();
        _invites.Add(Invite.CreateRecovery(
            Guid.NewGuid(),
            _attendee.Id,
            Guid.NewGuid(),
            $"hash-{Guid.NewGuid():N}",
            _clock.UtcNow.AddDays(-1),
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
        _attendee.MarkInvited();
        _invites.Add(Invite.CreateInitial(
            Guid.NewGuid(),
            _attendee.Id,
            $"hash-{Guid.NewGuid():N}",
            _clock.UtcNow.AddDays(expiresInDays),
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

<!-- retirement-file: {"id":49,"file":"tests/EventBooking.Application.Tests/Invites/InviteIssuerTests.cs","beforeSha":"a1770865d83647ce5b9af1459b1384ac6013d0a59c00728dd265d6126806afa0","afterSha":"dd41b1e83b6959252d8308582773314f92f5e2844b336bdac0c91090d29727f3","side":"before","part":1,"parts":1} -->

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
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(9, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        _events.Add(Event.CreateFrom(Guid.NewGuid(), proposal));
    }
}
`````
