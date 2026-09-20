# 01c — Negotiation across any number of types, edits 14 (Task 6)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.Application.Tests/Events/EventCancellationConcurrencyTests.cs — 1/1

<!-- retirement-file: {"id":39,"file":"tests/EventBooking.Application.Tests/Events/EventCancellationConcurrencyTests.cs","beforeSha":"1b64bf3d79ff5ecc8b24eee057a26ab7e547f8e94f86d32e96acb0c38b038fad","afterSha":"e4d90d9e0c32e3f85fd5498a7fd86693f396b02c9c4fa41ddef3c682d8f538fa","side":"after","part":1,"parts":1} -->

`````csharp
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

/// <summary>Verifies event and attendee lifecycle cancellation interleavings in the fake lock model.</summary>
public class EventCancellationConcurrencyTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly AttendeePortalOptions Portal = new(
        "https://booking.example.com", "recruitment@corp.com");

    /// <summary>Verifies event cancellation takes its attendee locks before competing confirmation.</summary>
    [Fact]
    public async Task CancellationTakesAttendeeLocksBeforeConfirmationCanReadTheEvent()
    {
        var scenario = new ConcurrentScenario();
        scenario.Attendees.BlockNextGetFor(scenario.BookedAttendee.Id);

        var cancellation = Task.Run(() => scenario.EventCancellation.HandleAsync(
            new CancelEventCommand(Coordinator, scenario.Event.Id, true), CancellationToken.None));

        await scenario.Attendees.WaitUntilBlockedAsync();

        scenario.Attendees.ReleaseBlockedGet();
        await scenario.Locks.WaitUntilHeldAsync(scenario.Event.Id);

        var confirmation = Task.Run(() => scenario.Confirmation.HandleAsync(
            new ConfirmBookingCommand(scenario.ConfirmationToken, scenario.Event.Id), CancellationToken.None));

        var cancellationResult = await cancellation;
        var confirmationResult = await confirmation;

        Assert.True(cancellationResult.IsSuccess);
        Assert.True(confirmationResult.IsFailure);
        Assert.Equal("conflict", confirmationResult.Error.Code);
        Assert.Equal(EventStatus.Cancelled, scenario.Event.Status);
        Assert.DoesNotContain(scenario.Bookings.Items, booking =>
            booking.EventId == scenario.Event.Id && booking.Status == BookingStatus.Active);
    }

    /// <summary>
    /// Attendee-first cancellation serializes behind the event-cancellation cascade on the shared
    /// attendee, so only the cascade can release the booking's capacity.
    /// </summary>
    /// <summary>Verifies event cancellation and individual cancellation release capacity once.</summary>
    [Fact]
    public async Task AttendeeLifecycleSerializationPreventsDoubleCapacityRelease()
    {
        var scenario = new ConcurrentScenario();
        scenario.Attendees.BlockNextGetFor(scenario.BookedAttendee.Id);

        var cancellation = Task.Run(() => scenario.EventCancellation.HandleAsync(
            new CancelEventCommand(Coordinator, scenario.Event.Id, true), CancellationToken.None));

        await scenario.Attendees.WaitUntilBlockedAsync();
        scenario.Attendees.ReleaseBlockedGet();
        await scenario.Locks.WaitUntilHeldAsync(scenario.Event.Id);

        var individualCancellation = Task.Run(() => scenario.IndividualCancellation.HandleAsync(
            new CancelBookingCommand(scenario.ManageToken, false), CancellationToken.None));

        var cancellationResult = await cancellation;
        var individualResult = await individualCancellation;

        var capacity = scenario.Event.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);

        Assert.True(cancellationResult.IsSuccess);
        Assert.True(individualResult.IsFailure);
        Assert.Equal(BookingStatus.Cancelled, scenario.Bookings.Items.Single().Status);
        Assert.Equal(capacity.TotalHeadcount, capacity.RemainingCapacity);
        Assert.InRange(capacity.RemainingCapacity, 0, capacity.TotalHeadcount);
        // One increment audit per required type proves the single release.
        Assert.Equal(2, scenario.Audit.Entries.Count(entry =>
            entry.Action == AuditAction.CapacityIncremented));
    }

    private sealed class ConcurrentScenario
    {
        public TransactionalEventLockCoordinator Locks { get; } = new();
        public InMemoryEventRepository Events { get; }
        public InMemoryBookingRepository Bookings { get; } = new();
        public InMemoryBookingAppointmentRepository Appointments { get; }
        public BlockingAttendeeRepository Attendees { get; } = new();
        public InMemoryInviteRepository Invites { get; } = new();
        public InMemoryAttendeeGroupRepository Groups { get; } = new();
        public InMemorySystemSettingsRepository Settings { get; } = new();
        public InMemoryStaffAccessProfileRepository Roles { get; } = new();
        public RecordingEmailSender Email { get; } = new();
        /// <summary>Durable delivery rows shared by both cancellation paths in this scenario.</summary>
        public InMemoryEmailDeliveryRepository Deliveries { get; } = new();
        public RecordingAuditLogger Audit { get; } = new();
        public FakeTokenService Tokens { get; } = new();
        public FakeClock Clock { get; } = new(
            new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
        public Event Event { get; }
        public Attendee BookedAttendee { get; }
        public string ManageToken { get; }
        public string ConfirmationToken { get; }

        private readonly InMemoryEventCapacityRepository _capacities;
        private readonly FakeUnitOfWork _eventCancellationUnitOfWork;
        private readonly FakeUnitOfWork _confirmationUnitOfWork;
        private readonly FakeUnitOfWork _individualCancellationUnitOfWork;

        public CancelEventHandler EventCancellation => new(
            Events,
            Bookings,
            Invites,
            Attendees,
            Roles,
            new BookingCanceller(Appointments, _capacities, Audit),
            Issuer,
            new EligibleEventFinder(Events, Clock),
            Appointments,
            EmailDeliveryTestFactory.Create(Deliveries, Email, _eventCancellationUnitOfWork, Clock),
            Audit,
            Clock,
            _eventCancellationUnitOfWork);

        public ConfirmBookingHandler Confirmation => new(
            Invites,
            Attendees,
            Events,
            Bookings,
            new InMemoryBookingAppointmentRepository(Bookings),
            _capacities,
            new EligibleEventFinder(Events, Clock),
            Tokens,
            EmailDeliveryTestFactory.Create(Deliveries, Email, _confirmationUnitOfWork, Clock),
            Audit,
            _confirmationUnitOfWork,
            Clock,
            Portal);

        public CancelBookingHandler IndividualCancellation => new(
            Bookings,
            Events,
            Attendees,
            Invites,
            new BookingCanceller(Appointments, _capacities, Audit),
            Issuer,
            EmailDeliveryTestFactory.Create(Deliveries, Email, _individualCancellationUnitOfWork, Clock),
            Tokens,
            Clock,
            _individualCancellationUnitOfWork);

        private InviteIssuer Issuer => new(
            Invites,
            Groups,
            new EligibleEventFinder(Events, Clock),
            Settings,
            Tokens,
            EmailDeliveryTestFactory.Create(Deliveries, Email, _eventCancellationUnitOfWork, Clock),
            Audit,
            Clock,
            Portal);

        public ConcurrentScenario()
        {
            Events = new InMemoryEventRepository(locks: Locks);
            Appointments = new InMemoryBookingAppointmentRepository(Bookings);
            _capacities = new InMemoryEventCapacityRepository(Events);
            _eventCancellationUnitOfWork = new FakeUnitOfWork(locks: Locks);
            _confirmationUnitOfWork = new FakeUnitOfWork(locks: Locks);
            _individualCancellationUnitOfWork = new FakeUnitOfWork(locks: Locks);

            Roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));

            Event = AddEvent(10);
            AddEvent(12);
            AddEvent(14);
            AddEvent(16);

            var pilots = AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
            Groups.Items.Add(pilots);
            BookedAttendee = Attendee.Create(
                Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", pilots);
            Attendees.Add(BookedAttendee);

            var bookingInviteId = Guid.NewGuid();
            var bookingInviteToken = Tokens.Issue(bookingInviteId);
            var bookingInvite = Invite.CreateInitial(
                bookingInviteId, BookedAttendee.Id, bookingInviteToken.TokenHash, Clock.UtcNow.AddDays(4),
                [Event.Id, Events.Items[1].Id, Events.Items[2].Id],
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting], 0);
            Invites.Add(bookingInvite);
            BookedAttendee.MarkInvited();

            var bookingId = Guid.NewGuid();
            var issuedManageToken = Tokens.Issue(bookingId);
            ManageToken = issuedManageToken.Token;
            Bookings.Add(Booking.Create(
                bookingId, bookingInvite, Event.Id, issuedManageToken.TokenHash, Clock.UtcNow));
            bookingInvite.MarkUsed();
            BookedAttendee.MarkBooked();
            Event.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
            Appointments.Add(BookingAppointment.Create(
                Guid.NewGuid(), bookingId, AppointmentTypeIds.DrugAndAlcoholTesting));
            Event.CapacityFor(AppointmentTypeIds.UniformFitting).Decrement();
            Appointments.Add(BookingAppointment.Create(
                Guid.NewGuid(), bookingId, AppointmentTypeIds.UniformFitting));

            var confirmingAttendee = Attendee.Create(
                Guid.NewGuid(), "B. Chen", "b.chen@mail.com",
                AttendeeGroup.Define(
                    Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
                    [AppointmentTypeIds.DrugAndAlcoholTesting]));
            Attendees.Add(confirmingAttendee);

            var confirmationInviteId = Guid.NewGuid();
            var issuedConfirmationToken = Tokens.Issue(confirmationInviteId);
            ConfirmationToken = issuedConfirmationToken.Token;
            var confirmationInvite = Invite.CreateInitial(
                confirmationInviteId,
                confirmingAttendee.Id,
                issuedConfirmationToken.TokenHash,
                Clock.UtcNow.AddDays(4),
                [Event.Id, Events.Items[1].Id, Events.Items[2].Id],
                [AppointmentTypeIds.DrugAndAlcoholTesting], 0);
            Invites.Add(confirmationInvite);
            confirmingAttendee.MarkInvited();
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
            Events.Add(eventItem);
            return eventItem;
        }
    }
}
`````

## before — tests/EventBooking.Application.Tests/Events/GetManagerEventBoardHandlerTests.cs — 1/1

<!-- retirement-file: {"id":40,"file":"tests/EventBooking.Application.Tests/Events/GetManagerEventBoardHandlerTests.cs","beforeSha":"9173bd10d8740a72be5456802476ad4ab287a34babf2b9b124f5d5c33b78ac4a","afterSha":"942090dab0cb5de520d4c03190b7032f1e9540671702e17ee88c0241d9ddf943","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

public class GetManagerEventBoardHandlerTests
{
    private static readonly Guid DrugAndAlcoholManager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid MedicalManager = Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid UniformManager = Guid.Parse("c0000003-0000-0000-0000-000000000003");

    private readonly InMemoryEventProposalRepository _proposals = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    private GetManagerEventBoardHandler Handler =>
        new(_proposals, _events, _roles, _clock);

    public GetManagerEventBoardHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            DrugAndAlcoholManager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(
            MedicalManager, Role.Manager, AppointmentTypeIds.MedicalCheckUp));
        _roles.Add(StaffAccessProfile.Create(
            UniformManager, Role.Manager, AppointmentTypeIds.UniformFitting));
    }

    [Fact]
    public async Task OpenProposalsShowWhoHasAcceptedAndWhetherIHave()
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            DrugAndAlcoholManager);
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        _proposals.Add(proposal);

        var board = (await Handler.HandleAsync(
            new GetManagerEventBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        var view = Assert.Single(board.OpenProposals);
        Assert.Equal(new DateOnly(2026, 9, 10), view.Date);
        Assert.Equal(new TimeOnly(9, 0), view.StartTime);
        Assert.Equal(new TimeOnly(13, 0), view.EndTime);
        Assert.Equal(
            new[] { "Drug & Alcohol Testing", "Medical Check-up" },
            view.AcceptedByAppointmentTypeNames);
        Assert.True(view.AcceptedByMe);
        Assert.True(view.CreatedByMe);
    }

    [Fact]
    public async Task AProposalIAmYetToAcceptIsFlaggedAsSuch()
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 12), new TimeOnly(13, 0), 240),
            UniformManager);
        proposal.Accept(AppointmentTypeIds.UniformFitting, UniformManager, 8);
        _proposals.Add(proposal);

        var board = (await Handler.HandleAsync(
            new GetManagerEventBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        var view = Assert.Single(board.OpenProposals);
        Assert.False(view.AcceptedByMe);
        Assert.False(view.CreatedByMe);
        Assert.Equal(new[] { "Uniform Fitting" }, view.AcceptedByAppointmentTypeNames);
    }

    [Fact]
    public async Task OpenProposalsAreOrderedEarliestFirst()
    {
        foreach (var day in new[] { 14, 10, 12 })
        {
            _proposals.Add(EventProposal.Create(
                Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0), 240),
                UniformManager));
        }

        var board = (await Handler.HandleAsync(
            new GetManagerEventBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        Assert.Equal(
            new[] { new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 14) },
            board.OpenProposals.Select(p => p.Date));
    }

    [Fact]
    public async Task EventsShowOnlyMyOwnHeadcountAndRemainder()
    {
        var eventItem = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());
        eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        _events.Add(eventItem);

        var board = (await Handler.HandleAsync(
            new GetManagerEventBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        var view = Assert.Single(board.Events);
        Assert.Equal(10, view.MyHeadcount);
        Assert.Equal(8, view.MyRemainingCapacity);
    }

    [Fact]
    public async Task CancelledAndPastEventsAreNotOnTheBoard()
    {
        var cancelled = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());
        cancelled.Cancel();
        _events.Add(cancelled);
        _events.Add(Event.CreateFrom(
            Guid.NewGuid(), FullyAcceptedProposal(new DateOnly(2026, 8, 30))));

        var board = (await Handler.HandleAsync(
            new GetManagerEventBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        Assert.Empty(board.Events);
    }

    [Fact]
    public async Task ANonManagerIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new GetManagerEventBoardQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    private static EventProposal FullyAcceptedProposal(DateOnly? date = null)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(date ?? new DateOnly(2026, 9, 8), new TimeOnly(9, 0), 240),
            DrugAndAlcoholManager);
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, UniformManager, 8);
        return proposal;
    }
}
`````

## after — tests/EventBooking.Application.Tests/Events/GetManagerEventBoardHandlerTests.cs — 1/1

<!-- retirement-file: {"id":40,"file":"tests/EventBooking.Application.Tests/Events/GetManagerEventBoardHandlerTests.cs","beforeSha":"9173bd10d8740a72be5456802476ad4ab287a34babf2b9b124f5d5c33b78ac4a","afterSha":"942090dab0cb5de520d4c03190b7032f1e9540671702e17ee88c0241d9ddf943","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

public class GetManagerEventBoardHandlerTests
{
    private static readonly Guid DrugAndAlcoholManager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid MedicalManager = Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid UniformManager = Guid.Parse("c0000003-0000-0000-0000-000000000003");

    private readonly InMemoryEventProposalRepository _proposals = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    private GetManagerEventBoardHandler Handler =>
        new(_proposals, _events, _roles, _clock);

    public GetManagerEventBoardHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            DrugAndAlcoholManager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(
            MedicalManager, Role.Manager, AppointmentTypeIds.MedicalCheckUp));
        _roles.Add(StaffAccessProfile.Create(
            UniformManager, Role.Manager, AppointmentTypeIds.UniformFitting));
    }

    [Fact]
    public async Task OpenProposalsShowWhoHasAcceptedAndWhetherIHave()
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            DrugAndAlcoholManager);
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        _proposals.Add(proposal);

        var board = (await Handler.HandleAsync(
            new GetManagerEventBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        var view = Assert.Single(board.OpenProposals);
        Assert.Equal(new DateOnly(2026, 9, 10), view.Date);
        Assert.Equal(new TimeOnly(9, 0), view.StartTime);
        Assert.Equal(new TimeOnly(13, 0), view.EndTime);
        Assert.Equal(
            new[] { "Drug & Alcohol Testing", "Medical Check-up" },
            view.AcceptedByAppointmentTypeNames);
        Assert.True(view.AcceptedByMe);
        Assert.True(view.CreatedByMe);
    }

    [Fact]
    public async Task AProposalIAmYetToAcceptIsFlaggedAsSuch()
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 12), new TimeOnly(13, 0), 240),
            UniformManager, AppointmentTypeIds.UniformFitting);
        proposal.Accept(AppointmentTypeIds.UniformFitting, UniformManager, 8);
        _proposals.Add(proposal);

        var board = (await Handler.HandleAsync(
            new GetManagerEventBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        var view = Assert.Single(board.OpenProposals);
        Assert.False(view.AcceptedByMe);
        Assert.False(view.CreatedByMe);
        Assert.Equal(new[] { "Uniform Fitting" }, view.AcceptedByAppointmentTypeNames);
    }

    [Fact]
    public async Task OpenProposalsAreOrderedEarliestFirst()
    {
        foreach (var day in new[] { 14, 10, 12 })
        {
            _proposals.Add(ProposalFixture.Create(
                Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0), 240),
                UniformManager));
        }

        var board = (await Handler.HandleAsync(
            new GetManagerEventBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        Assert.Equal(
            new[] { new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 14) },
            board.OpenProposals.Select(p => p.Date));
    }

    [Fact]
    public async Task EventsShowOnlyMyOwnHeadcountAndRemainder()
    {
        var eventItem = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());
        eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        _events.Add(eventItem);

        var board = (await Handler.HandleAsync(
            new GetManagerEventBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        var view = Assert.Single(board.Events);
        Assert.Equal(10, view.MyHeadcount);
        Assert.Equal(8, view.MyRemainingCapacity);
    }

    [Fact]
    public async Task CancelledAndPastEventsAreNotOnTheBoard()
    {
        var cancelled = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());
        cancelled.Cancel();
        _events.Add(cancelled);
        _events.Add(Event.CreateFrom(
            Guid.NewGuid(), FullyAcceptedProposal(new DateOnly(2026, 8, 30))));

        var board = (await Handler.HandleAsync(
            new GetManagerEventBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        Assert.Empty(board.Events);
    }

    [Fact]
    public async Task ANonManagerIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new GetManagerEventBoardQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    private static EventProposal FullyAcceptedProposal(DateOnly? date = null)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(date ?? new DateOnly(2026, 9, 8), new TimeOnly(9, 0), 240),
            DrugAndAlcoholManager);
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, UniformManager, 8);
        return proposal;
    }
}
`````

## before — tests/EventBooking.Application.Tests/Events/ManagerEventBoardHeadcountRevisionTests.cs — 1/1

<!-- retirement-file: {"id":41,"file":"tests/EventBooking.Application.Tests/Events/ManagerEventBoardHeadcountRevisionTests.cs","beforeSha":"2ca9bcce3ec17148d26a99cf6457170a55fa6e8ad3bbcad54b5c5934cbd8ea40","afterSha":"48ce22d456b60257a750658f16a0f0536c8e93f521800dc813ad724a4909d0c7","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

public class ManagerEventBoardHeadcountRevisionTests
{
    private static readonly Guid CurrentManager =
        Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid FormerManager =
        Guid.Parse("c0000011-0000-0000-0000-000000000011");

    private readonly InMemoryEventProposalRepository _proposals = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeClock _clock =
        new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    private GetManagerEventBoardHandler Handler =>
        new(_proposals, _events, _roles, _clock);

    public ManagerEventBoardHeadcountRevisionTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            CurrentManager,
            Role.Manager,
            AppointmentTypeIds.DrugAndAlcoholTesting));
    }

    private EventProposal AddProposal()
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            CurrentManager);
        _proposals.Add(proposal);
        return proposal;
    }

    [Fact]
    public async Task MyOpenProposalReturnsMyCurrentAcceptedHeadcount()
    {
        var proposal = AddProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            CurrentManager,
            12);

        var result = await Handler.HandleAsync(
            new GetManagerEventBoardQuery(CurrentManager),
            CancellationToken.None);

        var view = Assert.Single(result.Value.OpenProposals);
        Assert.True(view.AcceptedByMe);
        Assert.Equal(12, view.MyAcceptedHeadcount);
    }

    [Fact]
    public async Task AnotherManagersHeadcountIsNotReturnedAsMine()
    {
        var proposal = AddProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            FormerManager,
            10);

        var result = await Handler.HandleAsync(
            new GetManagerEventBoardQuery(CurrentManager),
            CancellationToken.None);

        var view = Assert.Single(result.Value.OpenProposals);
        Assert.False(view.AcceptedByMe);
        Assert.Null(view.MyAcceptedHeadcount);
        Assert.Equal(
            new[] { "Drug & Alcohol Testing" },
            view.AcceptedByAppointmentTypeNames);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Events/ManagerEventBoardHeadcountRevisionTests.cs — 1/1

<!-- retirement-file: {"id":41,"file":"tests/EventBooking.Application.Tests/Events/ManagerEventBoardHeadcountRevisionTests.cs","beforeSha":"2ca9bcce3ec17148d26a99cf6457170a55fa6e8ad3bbcad54b5c5934cbd8ea40","afterSha":"48ce22d456b60257a750658f16a0f0536c8e93f521800dc813ad724a4909d0c7","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

public class ManagerEventBoardHeadcountRevisionTests
{
    private static readonly Guid CurrentManager =
        Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid FormerManager =
        Guid.Parse("c0000011-0000-0000-0000-000000000011");

    private readonly InMemoryEventProposalRepository _proposals = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeClock _clock =
        new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    private GetManagerEventBoardHandler Handler =>
        new(_proposals, _events, _roles, _clock);

    public ManagerEventBoardHeadcountRevisionTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            CurrentManager,
            Role.Manager,
            AppointmentTypeIds.DrugAndAlcoholTesting));
    }

    private EventProposal AddProposal()
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            CurrentManager);
        _proposals.Add(proposal);
        return proposal;
    }

    [Fact]
    public async Task MyOpenProposalReturnsMyCurrentAcceptedHeadcount()
    {
        var proposal = AddProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            CurrentManager,
            12);

        var result = await Handler.HandleAsync(
            new GetManagerEventBoardQuery(CurrentManager),
            CancellationToken.None);

        var view = Assert.Single(result.Value.OpenProposals);
        Assert.True(view.AcceptedByMe);
        Assert.Equal(12, view.MyAcceptedHeadcount);
    }

    [Fact]
    public async Task AnotherTypesHeadcountIsNotReturnedAsMine()
    {
        // Proposed by another type, so the caller's own type has not accepted at all: the board
        // must not surrender another team's headcount (FR-2.8).
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            FormerManager,
            AppointmentTypeIds.UniformFitting);
        _proposals.Add(proposal);
        proposal.Accept(
            AppointmentTypeIds.UniformFitting,
            FormerManager,
            10);

        var result = await Handler.HandleAsync(
            new GetManagerEventBoardQuery(CurrentManager),
            CancellationToken.None);

        var view = Assert.Single(result.Value.OpenProposals);
        Assert.False(view.AcceptedByMe);
        Assert.Null(view.MyAcceptedHeadcount);
        Assert.Equal(
            new[] { "Uniform Fitting" },
            view.AcceptedByAppointmentTypeNames);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Events/ProposeEventHandlerTests.cs — 1/1

<!-- retirement-file: {"id":42,"file":"tests/EventBooking.Application.Tests/Events/ProposeEventHandlerTests.cs","beforeSha":"b40237f9fdf179b64f37a7884e9744de96abc68805d8d3f593c5244823434ade","afterSha":"241d3cca7142a2f9d50e564185155ad14d08d97564a2f3a41225b47d6f7a72d6","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

public class ProposeEventHandlerTests
{
    private static readonly Guid Manager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemoryEventProposalRepository _proposals = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    private ProposeEventHandler Handler => new(_proposals, _roles, _unitOfWork, _audit, _clock);

    public ProposeEventHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            Manager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
    }

    [Fact]
    public async Task AManagerCanProposeAFutureWindow()
    {
        var result = await Handler.HandleAsync(
            new ProposeEventCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var proposal = Assert.Single(_proposals.Items);
        Assert.Equal(result.Value, proposal.Id);
        Assert.Equal(EventProposalStatus.Open, proposal.Status);
        Assert.Equal(new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240), proposal.Window);
        Assert.Equal(Manager, proposal.CreatedByManagerUserId);
        Assert.Empty(proposal.Acceptances);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ProposingWritesAnAuditEntry()
    {
        await Handler.HandleAsync(
            new ProposeEventCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AuditEntityTypes.EventProposal, entry.EntityType);
        Assert.Equal(AuditAction.ProposalCreated, entry.Action);
        Assert.Equal(ActorType.Staff, entry.ActorType);
        Assert.Equal(Manager.ToString(), entry.ActorId);
    }

    [Fact]
    public async Task ACoordinatorCannotProposeAEvent()
    {
        var result = await Handler.HandleAsync(
            new ProposeEventCommand(Coordinator, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal("This staff profile cannot perform this operation.", result.Error.Message);
        Assert.Empty(_proposals.Items);
    }

    [Fact]
    public async Task AnUnknownUserCannotProposeAEvent()
    {
        var result = await Handler.HandleAsync(
            new ProposeEventCommand(Guid.NewGuid(), new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Theory]
    [InlineData(2026, 9, 3)]
    [InlineData(2026, 9, 2)]
    public async Task ATodayOrPastWindowIsRejected(int year, int month, int day)
    {
        var result = await Handler.HandleAsync(
            new ProposeEventCommand(Manager, new DateOnly(year, month, day), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal("A event must be proposed for a future date.", result.Error.Message);
    }

    [Fact]
    public async Task AWindowThatWouldRunPastMidnightIsRejectedByTheDomain()
    {
        var result = await Handler.HandleAsync(
            new ProposeEventCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(22, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal(
            "The window must end on the local date it starts.",
            result.Error.Message);
    }

    [Fact]
    public async Task ASecondOpenProposalForTheSameWindowIsAConflict()
    {
        var command = new ProposeEventCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(9, 0));
        await Handler.HandleAsync(command, CancellationToken.None);

        var result = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("An open proposal already exists for that window.", result.Error.Message);
        Assert.Single(_proposals.Items);
    }

    [Fact]
    public async Task ADifferentWindowOnTheSameDayIsAllowed()
    {
        await Handler.HandleAsync(
            new ProposeEventCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        var result = await Handler.HandleAsync(
            new ProposeEventCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(13, 0)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _proposals.Items.Count);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Events/ProposeEventHandlerTests.cs — 1/1

<!-- retirement-file: {"id":42,"file":"tests/EventBooking.Application.Tests/Events/ProposeEventHandlerTests.cs","beforeSha":"b40237f9fdf179b64f37a7884e9744de96abc68805d8d3f593c5244823434ade","afterSha":"241d3cca7142a2f9d50e564185155ad14d08d97564a2f3a41225b47d6f7a72d6","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

public class ProposeEventHandlerTests
{
    private static readonly Guid Manager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemoryEventProposalRepository _proposals = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    private ProposeEventHandler Handler => new(_proposals, _roles, _unitOfWork, _audit, _clock, ProposalFixture.Zones);

    public ProposeEventHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            Manager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
    }

    [Fact]
    public async Task AManagerCanProposeAFutureWindow()
    {
        var result = await Handler.HandleAsync(
            new ProposeEventCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var proposal = Assert.Single(_proposals.Items);
        Assert.Equal(result.Value, proposal.Id);
        Assert.Equal(EventProposalStatus.Open, proposal.Status);
        Assert.Equal(new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240), proposal.Window);
        Assert.Equal(Manager, proposal.CreatedByManagerUserId);
        // The proposing Manager commits in the same step (FR-2.1).
        var acceptance = Assert.Single(proposal.Acceptances);
        Assert.Equal(AppointmentTypeIds.DrugAndAlcoholTesting, acceptance.AppointmentTypeId);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ProposingWritesAnAuditEntry()
    {
        await Handler.HandleAsync(
            new ProposeEventCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AuditEntityTypes.EventProposal, entry.EntityType);
        Assert.Equal(AuditAction.ProposalCreated, entry.Action);
        Assert.Equal(ActorType.Staff, entry.ActorType);
        Assert.Equal(Manager.ToString(), entry.ActorId);
    }

    [Fact]
    public async Task ACoordinatorCannotProposeAEvent()
    {
        var result = await Handler.HandleAsync(
            new ProposeEventCommand(Coordinator, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal("This staff profile cannot perform this operation.", result.Error.Message);
        Assert.Empty(_proposals.Items);
    }

    [Fact]
    public async Task AnUnknownUserCannotProposeAEvent()
    {
        var result = await Handler.HandleAsync(
            new ProposeEventCommand(Guid.NewGuid(), new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Theory]
    [InlineData(2026, 9, 3)]
    [InlineData(2026, 9, 2)]
    public async Task ATodayOrPastWindowIsRejected(int year, int month, int day)
    {
        var result = await Handler.HandleAsync(
            new ProposeEventCommand(Manager, new DateOnly(year, month, day), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal("A event must be proposed for a future date.", result.Error.Message);
    }

    [Fact]
    public async Task AWindowThatWouldRunPastMidnightIsRejectedByTheDomain()
    {
        var result = await Handler.HandleAsync(
            new ProposeEventCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(22, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal(
            "The window must end on the local date it starts.",
            result.Error.Message);
    }

    [Fact]
    public async Task ASecondOpenProposalForTheSameWindowIsAConflict()
    {
        var command = new ProposeEventCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(9, 0));
        await Handler.HandleAsync(command, CancellationToken.None);

        var result = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("An open proposal already exists for that window.", result.Error.Message);
        Assert.Single(_proposals.Items);
    }

    [Fact]
    public async Task ADifferentWindowOnTheSameDayIsAllowed()
    {
        await Handler.HandleAsync(
            new ProposeEventCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        var result = await Handler.HandleAsync(
            new ProposeEventCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(13, 0)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _proposals.Items.Count);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Events/WithdrawAcceptanceHandlerTests.cs — 1/1

<!-- retirement-file: {"id":43,"file":"tests/EventBooking.Application.Tests/Events/WithdrawAcceptanceHandlerTests.cs","beforeSha":"0eecf22581aabd43978e335ffc093a513ea831b676c86f34e7d39ea7c54092ae","afterSha":"51ce6264286c257ffdf0d7388fd8e7b8eab1fed47dcfda08df9e1c70993fd231","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

public class WithdrawAcceptanceHandlerTests
{
    private static readonly Guid DrugAndAlcoholManager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid MedicalManager = Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid UniformManager = Guid.Parse("c0000003-0000-0000-0000-000000000003");

    private readonly InMemoryEventProposalRepository _proposals = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly EventProposal _proposal;

    private WithdrawAcceptanceHandler Handler => new(_proposals, _roles, _unitOfWork, _audit);

    public WithdrawAcceptanceHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            DrugAndAlcoholManager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(
            MedicalManager, Role.Manager, AppointmentTypeIds.MedicalCheckUp));
        _roles.Add(StaffAccessProfile.Create(
            UniformManager, Role.Manager, AppointmentTypeIds.UniformFitting));

        _proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            DrugAndAlcoholManager);
        _proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        _proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        _proposals.Add(_proposal);
    }

    [Fact]
    public async Task AManagerCanTakeTheirOwnAcceptanceBack()
    {
        var result = await Handler.HandleAsync(
            new WithdrawAcceptanceCommand(MedicalManager, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(_proposal.Acceptances);
        Assert.False(_proposal.IsAcceptedBy(AppointmentTypeIds.MedicalCheckUp));
        Assert.Equal(1, _unitOfWork.SaveCount);
        Assert.True(_audit.Contains(AuditAction.AcceptanceWithdrawn));
    }

    [Fact]
    public async Task AManagerWhoNeverAcceptedGetsAValidationFailure()
    {
        var result = await Handler.HandleAsync(
            new WithdrawAcceptanceCommand(UniformManager, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal("This appointment type has not accepted the proposal.", result.Error.Message);
    }

    [Fact]
    public async Task AnAcceptanceCannotBeWithdrawnOnceTheProposalIsConfirmed()
    {
        _proposal.Accept(AppointmentTypeIds.UniformFitting, UniformManager, 8);
        Event.CreateFrom(Guid.NewGuid(), _proposal);

        var result = await Handler.HandleAsync(
            new WithdrawAcceptanceCommand(MedicalManager, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(
            "An acceptance can only be withdrawn while the proposal is still open.",
            result.Error.Message);
    }

    [Fact]
    public async Task AnUnknownProposalIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new WithdrawAcceptanceCommand(MedicalManager, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task ANonManagerIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new WithdrawAcceptanceCommand(Guid.NewGuid(), _proposal.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Events/WithdrawAcceptanceHandlerTests.cs — 1/1

<!-- retirement-file: {"id":43,"file":"tests/EventBooking.Application.Tests/Events/WithdrawAcceptanceHandlerTests.cs","beforeSha":"0eecf22581aabd43978e335ffc093a513ea831b676c86f34e7d39ea7c54092ae","afterSha":"51ce6264286c257ffdf0d7388fd8e7b8eab1fed47dcfda08df9e1c70993fd231","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

public class WithdrawAcceptanceHandlerTests
{
    private static readonly Guid DrugAndAlcoholManager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid MedicalManager = Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid UniformManager = Guid.Parse("c0000003-0000-0000-0000-000000000003");

    private readonly InMemoryEventProposalRepository _proposals = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly EventProposal _proposal;

    private WithdrawAcceptanceHandler Handler => new(_proposals, _roles, _unitOfWork, _audit);

    public WithdrawAcceptanceHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            DrugAndAlcoholManager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(
            MedicalManager, Role.Manager, AppointmentTypeIds.MedicalCheckUp));
        _roles.Add(StaffAccessProfile.Create(
            UniformManager, Role.Manager, AppointmentTypeIds.UniformFitting));

        _proposal = ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            DrugAndAlcoholManager);
        _proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        _proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        _proposals.Add(_proposal);
    }

    [Fact]
    public async Task AManagerCanTakeTheirOwnAcceptanceBack()
    {
        var result = await Handler.HandleAsync(
            new WithdrawAcceptanceCommand(MedicalManager, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(_proposal.Acceptances);
        Assert.False(_proposal.IsAcceptedBy(AppointmentTypeIds.MedicalCheckUp));
        Assert.Equal(1, _unitOfWork.SaveCount);
        Assert.True(_audit.Contains(AuditAction.AcceptanceWithdrawn));
    }

    [Fact]
    public async Task AManagerWhoNeverAcceptedGetsAValidationFailure()
    {
        var result = await Handler.HandleAsync(
            new WithdrawAcceptanceCommand(UniformManager, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal("This appointment type has not accepted the proposal.", result.Error.Message);
    }

    [Fact]
    public async Task AnAcceptanceCannotBeWithdrawnOnceTheProposalIsConfirmed()
    {
        _proposal.Accept(AppointmentTypeIds.UniformFitting, UniformManager, 8);
        Event.CreateFrom(Guid.NewGuid(), _proposal);

        var result = await Handler.HandleAsync(
            new WithdrawAcceptanceCommand(MedicalManager, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(
            "An acceptance can only be withdrawn while the proposal is still open.",
            result.Error.Message);
    }

    [Fact]
    public async Task AnUnknownProposalIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new WithdrawAcceptanceCommand(MedicalManager, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task ANonManagerIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new WithdrawAcceptanceCommand(Guid.NewGuid(), _proposal.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Events/WithdrawProposalHandlerTests.cs — 1/1

<!-- retirement-file: {"id":44,"file":"tests/EventBooking.Application.Tests/Events/WithdrawProposalHandlerTests.cs","beforeSha":"38b246754fbdb34702bc43212c15519ba485414989b4c6684adc88666eb4de07","afterSha":"d614798a933de539a74f2d638d3924651261e9bfaa93e353c4e6d36502d2b222","side":"before","part":1,"parts":1} -->

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

        _proposal = EventProposal.Create(
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
    public async Task AnotherManagerCanWithdrawIt()
    {
        var result = await Handler.HandleAsync(
            new WithdrawProposalCommand(OtherManager, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EventProposalStatus.Withdrawn, _proposal.Status);
        Assert.True(_audit.Contains(AuditAction.ProposalWithdrawn));
    }

    [Fact]
    public async Task AnAdminCanWithdrawIt()
    {
        var result = await Handler.HandleAsync(
            new WithdrawProposalCommand(Admin, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EventProposalStatus.Withdrawn, _proposal.Status);
        Assert.True(_audit.Contains(AuditAction.ProposalWithdrawn));
    }

    [Fact]
    public async Task AlreadyWithdrawnIsAConflict()
    {
        _proposal.Withdraw(Creator);

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
