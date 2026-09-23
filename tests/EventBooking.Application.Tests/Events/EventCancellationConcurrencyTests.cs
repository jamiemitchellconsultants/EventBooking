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
            new EligibleEventFinder(Events, Events, Clock),
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
            new EligibleEventFinder(Events, Events, Clock),
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
            new EligibleEventFinder(Events, Events, Clock),
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
            var bookingInviteToken = Tokens.Issue(TokenPurpose.Book, bookingInviteId, Invite.InitialTokenVersion);
            var bookingInvite = Invite.CreateInitial(
                bookingInviteId,
                BookedAttendee.Id,
                Clock.UtcNow.AddDays(4),
                [ProposalFixture.LocationId],
                [Event.Id, Events.Items[1].Id, Events.Items[2].Id],
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
                0);
            Invites.Add(bookingInvite);
            BookedAttendee.MarkInvited(ProposalFixture.Now);

            var bookingId = Guid.NewGuid();
            var issuedManageToken = Tokens.Issue(TokenPurpose.Manage, bookingId, Booking.InitialManageTokenVersion);
            ManageToken = issuedManageToken;
            Bookings.Add(Booking.Create(
                bookingId, bookingInvite, Event.Id, Clock.UtcNow));
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
            var issuedConfirmationToken = Tokens.Issue(TokenPurpose.Book, confirmationInviteId, Invite.InitialTokenVersion);
            ConfirmationToken = issuedConfirmationToken;
            var confirmationInvite = Invite.CreateInitial(
                confirmationInviteId,
                confirmingAttendee.Id,
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
