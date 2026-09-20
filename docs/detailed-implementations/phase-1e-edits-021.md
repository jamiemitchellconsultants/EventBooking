# 01e — Location-restricted invites and closed attendee transitions, edits 21 (Task 8)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — tests/EventBooking.Application.Tests/Events/EventCancellationConcurrencyTests.cs — 1/1

<!-- retirement-file: {"id":54,"file":"tests/EventBooking.Application.Tests/Events/EventCancellationConcurrencyTests.cs","beforeSha":"b855673a7202f40f453c79d3e5b283782463f84c402dad41f43e4666fe2a0ed3","afterSha":"c223220f4417a92350f4e10bdcb00a66444c6c9b6421351542d1b81cbca848d4","side":"before","part":1,"parts":1} -->

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
            ProposalFixture.Zones,
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

## after — tests/EventBooking.Application.Tests/Events/EventCancellationConcurrencyTests.cs — 1/1

<!-- retirement-file: {"id":54,"file":"tests/EventBooking.Application.Tests/Events/EventCancellationConcurrencyTests.cs","beforeSha":"b855673a7202f40f453c79d3e5b283782463f84c402dad41f43e4666fe2a0ed3","afterSha":"c223220f4417a92350f4e10bdcb00a66444c6c9b6421351542d1b81cbca848d4","side":"after","part":1,"parts":1} -->

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
            ProposalFixture.Zones,
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
                Guid.NewGuid(),
                "Amara Novak",
                "a.novak@mail.com",
                pilots,
                ProposalFixture.Now);
            Attendees.Add(BookedAttendee);

            var bookingInviteId = Guid.NewGuid();
            var bookingInviteToken = Tokens.Issue(bookingInviteId);
            var bookingInvite = Invite.CreateInitial(
                bookingInviteId,
                BookedAttendee.Id,
                bookingInviteToken.TokenHash,
                Clock.UtcNow.AddDays(4),
                [ProposalFixture.LocationId],
                [Event.Id, Events.Items[1].Id, Events.Items[2].Id],
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
                0);
            Invites.Add(bookingInvite);
            BookedAttendee.MarkInvited(ProposalFixture.Now);

            var bookingId = Guid.NewGuid();
            var issuedManageToken = Tokens.Issue(bookingId);
            ManageToken = issuedManageToken.Token;
            Bookings.Add(Booking.Create(
                bookingId, bookingInvite, Event.Id, issuedManageToken.TokenHash, Clock.UtcNow));
            bookingInvite.MarkUsed();
            BookedAttendee.MarkBooked(ProposalFixture.Now);
            Event.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
            Appointments.Add(BookingAppointment.Create(
                Guid.NewGuid(), bookingId, AppointmentTypeIds.DrugAndAlcoholTesting));
            Event.CapacityFor(AppointmentTypeIds.UniformFitting).Decrement();
            Appointments.Add(BookingAppointment.Create(
                Guid.NewGuid(), bookingId, AppointmentTypeIds.UniformFitting));

            var confirmingAttendee = Attendee.Create(
                Guid.NewGuid(),
                "B. Chen",
                "b.chen@mail.com",
                AttendeeGroup.Define(
                    Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
                    [AppointmentTypeIds.DrugAndAlcoholTesting]),
                ProposalFixture.Now);
            Attendees.Add(confirmingAttendee);

            var confirmationInviteId = Guid.NewGuid();
            var issuedConfirmationToken = Tokens.Issue(confirmationInviteId);
            ConfirmationToken = issuedConfirmationToken.Token;
            var confirmationInvite = Invite.CreateInitial(
                confirmationInviteId,
                confirmingAttendee.Id,
                issuedConfirmationToken.TokenHash,
                Clock.UtcNow.AddDays(4),
                [ProposalFixture.LocationId],
                [Event.Id, Events.Items[1].Id, Events.Items[2].Id],
                [AppointmentTypeIds.DrugAndAlcoholTesting],
                0);
            Invites.Add(confirmationInvite);
            confirmingAttendee.MarkInvited(ProposalFixture.Now);
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

## before — tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs — 1/1

<!-- retirement-file: {"id":55,"file":"tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs","beforeSha":"6c2bb89d9bc0af4313b0e9943eb848c3a48f3f7d3ea1f043b5d50de98aeca150","afterSha":"4aa85abc280d7c95eeaeaafd06e070ea0309e5ff5219e740f24130df5c6a318d","side":"before","part":1,"parts":1} -->

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
        cancelled.CancelBeforeStart();
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

## after — tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs — 1/1

<!-- retirement-file: {"id":55,"file":"tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs","beforeSha":"6c2bb89d9bc0af4313b0e9943eb848c3a48f3f7d3ea1f043b5d50de98aeca150","afterSha":"4aa85abc280d7c95eeaeaafd06e070ea0309e5ff5219e740f24130df5c6a318d","side":"after","part":1,"parts":1} -->

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
            Guid.NewGuid(),
            "B. Chen",
            "b.chen@mail.com",
            uniformOnly,
            ProposalFixture.Now);
        invited.MarkInvited(ProposalFixture.Now);
        repository.Add(invited);
        repository.Add(Attendee.Create(
            Guid.NewGuid(),
            "A. Novak",
            "a.novak@mail.com",
            uniformOnly,
            ProposalFixture.Now));

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
        cancelled.CancelBeforeStart();
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

## before — tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs — 1/1

<!-- retirement-file: {"id":56,"file":"tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs","beforeSha":"671d2663e7eec638e661c0355381d6d33768424b5aa556cb3d0bbed2ea25d2f2","afterSha":"eccb19c794cb70aa3594d0269aa4262aa0da80b674a5da6e47fa80c1edc33e06","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs — 1/1

<!-- retirement-file: {"id":56,"file":"tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs","beforeSha":"671d2663e7eec638e661c0355381d6d33768424b5aa556cb3d0bbed2ea25d2f2","afterSha":"eccb19c794cb70aa3594d0269aa4262aa0da80b674a5da6e47fa80c1edc33e06","side":"after","part":1,"parts":1} -->

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
