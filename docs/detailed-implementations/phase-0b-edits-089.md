# 00b — Vocabulary edits 89 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — tests/EventBooking.Application.Tests/Slots/SharedSlotAuthorizationTests.cs — 1/1

<!-- vocabulary-file: {"id":298,"oldPath":"tests/EventBooking.Application.Tests/Slots/SharedSlotAuthorizationTests.cs","newPath":"tests/EventBooking.Application.Tests/Events/SharedEventAuthorizationTests.cs","beforeSha":"a73b7e5a988e73f734642f88cc87930735072d9b0272217b4cc64399ef640737","afterSha":"1723fd0ca0ba05d10441400ba9510173fab7dbf23a1fea5df3b9ea0ca37e9f79","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Slots;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Tests.Slots;

public class SharedSlotAuthorizationTests
{
    private const string Csv =
        "date,startTime,DAT,MED,UNI\n2026-09-10,09:00,10,6,8";

    [Theory]
    [InlineData(Role.Admin)]
    [InlineData(Role.Coordinator)]
    public async Task AdminAndCoordinatorCanImportConfirmedSlots(Role role)
    {
        var user = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(user, [role], null));
        var slots = new InMemoryConfirmedSlotRepository();
        var handler = new ImportConfirmedSlotsHandler(
            slots,
            new StaffAccessAuthorizer(profiles),
            new FakeUnitOfWork(),
            new RecordingAuditLogger(),
            new FakeClock());

        var result = await handler.HandleAsync(
            new ImportConfirmedSlotsCommand(user, Csv), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Accepted);
        Assert.Single(slots.Items);
    }

    [Fact]
    public async Task AppointmentStaffCannotImportConfirmedSlots()
    {
        var user = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            user,
            [Role.AppointmentStaff],
            Domain.AppointmentTypes.AppointmentTypeIds.DrugAndAlcoholTesting));
        var slots = new InMemoryConfirmedSlotRepository();
        var handler = new ImportConfirmedSlotsHandler(
            slots,
            new StaffAccessAuthorizer(profiles),
            new FakeUnitOfWork(),
            new RecordingAuditLogger(),
            new FakeClock());

        var result = await handler.HandleAsync(
            new ImportConfirmedSlotsCommand(user, Csv), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(slots.Items);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Events/SharedEventAuthorizationTests.cs — 1/1

<!-- vocabulary-file: {"id":298,"oldPath":"tests/EventBooking.Application.Tests/Slots/SharedSlotAuthorizationTests.cs","newPath":"tests/EventBooking.Application.Tests/Events/SharedEventAuthorizationTests.cs","beforeSha":"a73b7e5a988e73f734642f88cc87930735072d9b0272217b4cc64399ef640737","afterSha":"1723fd0ca0ba05d10441400ba9510173fab7dbf23a1fea5df3b9ea0ca37e9f79","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Tests.Events;

public class SharedEventAuthorizationTests
{
    private const string Csv =
        "date,startTime,DAT,MED,UNI\n2026-09-10,09:00,10,6,8";

    [Theory]
    [InlineData(Role.Admin)]
    [InlineData(Role.Coordinator)]
    public async Task AdminAndCoordinatorCanImportEvents(Role role)
    {
        var user = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(user, [role], null));
        var events = new InMemoryEventRepository();
        var handler = new ImportEventsHandler(
            events,
            new StaffAccessAuthorizer(profiles),
            new FakeUnitOfWork(),
            new RecordingAuditLogger(),
            new FakeClock());

        var result = await handler.HandleAsync(
            new ImportEventsCommand(user, Csv), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Accepted);
        Assert.Single(events.Items);
    }

    [Fact]
    public async Task AppointmentStaffCannotImportEvents()
    {
        var user = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            user,
            [Role.AppointmentStaff],
            Domain.AppointmentTypes.AppointmentTypeIds.DrugAndAlcoholTesting));
        var events = new InMemoryEventRepository();
        var handler = new ImportEventsHandler(
            events,
            new StaffAccessAuthorizer(profiles),
            new FakeUnitOfWork(),
            new RecordingAuditLogger(),
            new FakeClock());

        var result = await handler.HandleAsync(
            new ImportEventsCommand(user, Csv), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(events.Items);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Slots/SlotCancellationConcurrencyTests.cs — 1/1

<!-- vocabulary-file: {"id":299,"oldPath":"tests/EventBooking.Application.Tests/Slots/SlotCancellationConcurrencyTests.cs","newPath":"tests/EventBooking.Application.Tests/Events/EventCancellationConcurrencyTests.cs","beforeSha":"c16fe59954ccade298c7539644889e284e8ebc5bffaf61b041692007a9eabe55","afterSha":"bc46cb4ce18a1e623701c711bad1bc63ede6426abe9751e24ddf77f4c62e82c3","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Slots;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Slots;

/// <summary>Verifies slot and candidate lifecycle cancellation interleavings in the fake lock model.</summary>
public class SlotCancellationConcurrencyTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly CandidatePortalOptions Portal = new(
        "https://booking.example.com", "Corporate HQ", "recruitment@corp.com");

    /// <summary>Verifies slot cancellation takes its candidate locks before competing confirmation.</summary>
    [Fact]
    public async Task CancellationTakesCandidateLocksBeforeConfirmationCanReadTheSlot()
    {
        var scenario = new ConcurrentScenario();
        scenario.Candidates.BlockNextGetFor(scenario.BookedCandidate.Id);

        var cancellation = Task.Run(() => scenario.SlotCancellation.HandleAsync(
            new CancelConfirmedSlotCommand(Coordinator, scenario.Slot.Id, true), CancellationToken.None));

        await scenario.Candidates.WaitUntilBlockedAsync();

        scenario.Candidates.ReleaseBlockedGet();
        await scenario.Locks.WaitUntilHeldAsync(scenario.Slot.Id);

        var confirmation = Task.Run(() => scenario.Confirmation.HandleAsync(
            new ConfirmBookingCommand(scenario.ConfirmationToken, scenario.Slot.Id), CancellationToken.None));

        var cancellationResult = await cancellation;
        var confirmationResult = await confirmation;

        Assert.True(cancellationResult.IsSuccess);
        Assert.True(confirmationResult.IsFailure);
        Assert.Equal("conflict", confirmationResult.Error.Code);
        Assert.Equal(ConfirmedSlotStatus.Cancelled, scenario.Slot.Status);
        Assert.DoesNotContain(scenario.Bookings.Items, booking =>
            booking.ConfirmedSlotId == scenario.Slot.Id && booking.Status == BookingStatus.Active);
    }

    /// <summary>
    /// Candidate-first cancellation serializes behind the slot-cancellation cascade on the shared
    /// candidate, so only the cascade can release the booking's capacity.
    /// </summary>
    /// <summary>Verifies slot cancellation and individual cancellation release capacity once.</summary>
    [Fact]
    public async Task CandidateLifecycleSerializationPreventsDoubleCapacityRelease()
    {
        var scenario = new ConcurrentScenario();
        scenario.Candidates.BlockNextGetFor(scenario.BookedCandidate.Id);

        var cancellation = Task.Run(() => scenario.SlotCancellation.HandleAsync(
            new CancelConfirmedSlotCommand(Coordinator, scenario.Slot.Id, true), CancellationToken.None));

        await scenario.Candidates.WaitUntilBlockedAsync();
        scenario.Candidates.ReleaseBlockedGet();
        await scenario.Locks.WaitUntilHeldAsync(scenario.Slot.Id);

        var individualCancellation = Task.Run(() => scenario.IndividualCancellation.HandleAsync(
            new CancelBookingCommand(scenario.ManageToken, false), CancellationToken.None));

        var cancellationResult = await cancellation;
        var individualResult = await individualCancellation;

        var capacity = scenario.Slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);

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
        public TransactionalSlotLockCoordinator Locks { get; } = new();
        public InMemoryConfirmedSlotRepository Slots { get; }
        public InMemoryBookingRepository Bookings { get; } = new();
        public InMemoryBookingAppointmentRepository Appointments { get; }
        public BlockingCandidateRepository Candidates { get; } = new();
        public InMemoryInviteRepository Invites { get; } = new();
        public InMemoryEmployeeGroupRepository Groups { get; } = new();
        public InMemorySystemSettingsRepository Settings { get; } = new();
        public InMemoryStaffAccessProfileRepository Roles { get; } = new();
        public RecordingEmailSender Email { get; } = new();
        /// <summary>Durable delivery rows shared by both cancellation paths in this scenario.</summary>
        public InMemoryEmailDeliveryRepository Deliveries { get; } = new();
        public RecordingAuditLogger Audit { get; } = new();
        public FakeTokenService Tokens { get; } = new();
        public FakeClock Clock { get; } = new(
            new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
        public ConfirmedSlot Slot { get; }
        public Candidate BookedCandidate { get; }
        public string ManageToken { get; }
        public string ConfirmationToken { get; }

        private readonly InMemorySlotCapacityRepository _capacities;
        private readonly FakeUnitOfWork _slotCancellationUnitOfWork;
        private readonly FakeUnitOfWork _confirmationUnitOfWork;
        private readonly FakeUnitOfWork _individualCancellationUnitOfWork;

        public CancelConfirmedSlotHandler SlotCancellation => new(
            Slots,
            Bookings,
            Invites,
            Candidates,
            Roles,
            new BookingCanceller(Appointments, _capacities, Audit),
            Issuer,
            new EligibleSlotFinder(Slots, Clock),
            Appointments,
            EmailDeliveryTestFactory.Create(Deliveries, Email, _slotCancellationUnitOfWork, Clock),
            Audit,
            Clock,
            _slotCancellationUnitOfWork);

        public ConfirmBookingHandler Confirmation => new(
            Invites,
            Candidates,
            Slots,
            Bookings,
            new InMemoryBookingAppointmentRepository(Bookings),
            _capacities,
            new EligibleSlotFinder(Slots, Clock),
            Tokens,
            EmailDeliveryTestFactory.Create(Deliveries, Email, _confirmationUnitOfWork, Clock),
            Audit,
            _confirmationUnitOfWork,
            Clock,
            Portal);

        public CancelBookingHandler IndividualCancellation => new(
            Bookings,
            Slots,
            Candidates,
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
            new EligibleSlotFinder(Slots, Clock),
            Settings,
            Tokens,
            EmailDeliveryTestFactory.Create(Deliveries, Email, _slotCancellationUnitOfWork, Clock),
            Audit,
            Clock,
            Portal);

        public ConcurrentScenario()
        {
            Slots = new InMemoryConfirmedSlotRepository(locks: Locks);
            Appointments = new InMemoryBookingAppointmentRepository(Bookings);
            _capacities = new InMemorySlotCapacityRepository(Slots);
            _slotCancellationUnitOfWork = new FakeUnitOfWork(locks: Locks);
            _confirmationUnitOfWork = new FakeUnitOfWork(locks: Locks);
            _individualCancellationUnitOfWork = new FakeUnitOfWork(locks: Locks);

            Roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));

            Slot = AddSlot(10);
            AddSlot(12);
            AddSlot(14);
            AddSlot(16);

            var pilots = EmployeeGroup.Define(
                EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
            Groups.Items.Add(pilots);
            BookedCandidate = Candidate.Create(
                Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", pilots);
            Candidates.Add(BookedCandidate);

            var bookingInviteId = Guid.NewGuid();
            var bookingInviteToken = Tokens.Issue(bookingInviteId);
            var bookingInvite = Invite.CreateInitial(
                bookingInviteId, BookedCandidate.Id, bookingInviteToken.TokenHash, Clock.UtcNow.AddDays(4),
                [Slot.Id, Slots.Items[1].Id, Slots.Items[2].Id],
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting], 0);
            Invites.Add(bookingInvite);
            BookedCandidate.MarkInvited();

            var bookingId = Guid.NewGuid();
            var issuedManageToken = Tokens.Issue(bookingId);
            ManageToken = issuedManageToken.Token;
            Bookings.Add(Booking.Create(
                bookingId, bookingInvite, Slot.Id, issuedManageToken.TokenHash, Clock.UtcNow));
            bookingInvite.MarkUsed();
            BookedCandidate.MarkBooked();
            Slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
            Appointments.Add(BookingAppointment.Create(
                Guid.NewGuid(), bookingId, AppointmentTypeIds.DrugAndAlcoholTesting));
            Slot.CapacityFor(AppointmentTypeIds.UniformFitting).Decrement();
            Appointments.Add(BookingAppointment.Create(
                Guid.NewGuid(), bookingId, AppointmentTypeIds.UniformFitting));

            var confirmingCandidate = Candidate.Create(
                Guid.NewGuid(), "B. Chen", "b.chen@mail.com",
                EmployeeGroup.Define(
                    Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
                    [AppointmentTypeIds.DrugAndAlcoholTesting]));
            Candidates.Add(confirmingCandidate);

            var confirmationInviteId = Guid.NewGuid();
            var issuedConfirmationToken = Tokens.Issue(confirmationInviteId);
            ConfirmationToken = issuedConfirmationToken.Token;
            var confirmationInvite = Invite.CreateInitial(
                confirmationInviteId,
                confirmingCandidate.Id,
                issuedConfirmationToken.TokenHash,
                Clock.UtcNow.AddDays(4),
                [Slot.Id, Slots.Items[1].Id, Slots.Items[2].Id],
                [AppointmentTypeIds.DrugAndAlcoholTesting], 0);
            Invites.Add(confirmationInvite);
            confirmingCandidate.MarkInvited();
        }

        private ConfirmedSlot AddSlot(int day)
        {
            var proposal = SlotProposal.Create(
                Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0)),
                Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);
            Slots.Add(slot);
            return slot;
        }
    }
}
`````

## after — tests/EventBooking.Application.Tests/Events/EventCancellationConcurrencyTests.cs — 1/1

<!-- vocabulary-file: {"id":299,"oldPath":"tests/EventBooking.Application.Tests/Slots/SlotCancellationConcurrencyTests.cs","newPath":"tests/EventBooking.Application.Tests/Events/EventCancellationConcurrencyTests.cs","beforeSha":"c16fe59954ccade298c7539644889e284e8ebc5bffaf61b041692007a9eabe55","afterSha":"bc46cb4ce18a1e623701c711bad1bc63ede6426abe9751e24ddf77f4c62e82c3","side":"after","part":1,"parts":1} -->

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
        "https://booking.example.com", "Corporate HQ", "recruitment@corp.com");

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
            var proposal = EventProposal.Create(
                Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0)),
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

## before — tests/EventBooking.Application.Tests/Slots/WithdrawAcceptanceHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":300,"oldPath":"tests/EventBooking.Application.Tests/Slots/WithdrawAcceptanceHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Events/WithdrawAcceptanceHandlerTests.cs","beforeSha":"fee2b0140b113d90912ae8f7d13fb33325c52ea3bdc238570319a2740f7b3d0b","afterSha":"6e79232d852d105bf1ee95c4506f0809f042f99cc1ec2007033f00441bd22f66","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Slots;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Slots;

public class WithdrawAcceptanceHandlerTests
{
    private static readonly Guid DrugAndAlcoholManager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid MedicalManager = Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid UniformManager = Guid.Parse("c0000003-0000-0000-0000-000000000003");

    private readonly InMemorySlotProposalRepository _proposals = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly SlotProposal _proposal;

    private WithdrawAcceptanceHandler Handler => new(_proposals, _roles, _unitOfWork, _audit);

    public WithdrawAcceptanceHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            DrugAndAlcoholManager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(
            MedicalManager, Role.Manager, AppointmentTypeIds.MedicalCheckUp));
        _roles.Add(StaffAccessProfile.Create(
            UniformManager, Role.Manager, AppointmentTypeIds.UniformFitting));

        _proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
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
        ConfirmedSlot.CreateFrom(Guid.NewGuid(), _proposal);

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

<!-- vocabulary-file: {"id":300,"oldPath":"tests/EventBooking.Application.Tests/Slots/WithdrawAcceptanceHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Events/WithdrawAcceptanceHandlerTests.cs","beforeSha":"fee2b0140b113d90912ae8f7d13fb33325c52ea3bdc238570319a2740f7b3d0b","afterSha":"6e79232d852d105bf1ee95c4506f0809f042f99cc1ec2007033f00441bd22f66","side":"after","part":1,"parts":1} -->

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
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
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

## before — tests/EventBooking.Application.Tests/Slots/WithdrawProposalHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":301,"oldPath":"tests/EventBooking.Application.Tests/Slots/WithdrawProposalHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Events/WithdrawProposalHandlerTests.cs","beforeSha":"ec0ed06fd335de097602bb8aa91a464f0947d4cc5aedd0b10e37511d87169ca8","afterSha":"e0864048f25ed5c082e0d96d28b59079ac0c9eeb1e3199dda49b69318a25fbca","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Slots;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Slots;

public class WithdrawProposalHandlerTests
{
    private static readonly Guid Creator = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid OtherManager = Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid Admin = Guid.Parse("a0000001-0000-0000-0000-000000000001");

    private readonly InMemorySlotProposalRepository _proposals = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly SlotProposal _proposal;

    private WithdrawProposalHandler Handler => new(_proposals, _roles, _unitOfWork, _audit);

    public WithdrawProposalHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            Creator, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(
            OtherManager, Role.Manager, AppointmentTypeIds.MedicalCheckUp));
        _roles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));

        _proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            Creator);
        _proposals.Add(_proposal);
    }

    [Fact]
    public async Task TheCreatorCanWithdrawItAndItLeavesTheOpenList()
    {
        var result = await Handler.HandleAsync(
            new WithdrawProposalCommand(Creator, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SlotProposalStatus.Withdrawn, _proposal.Status);
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
        Assert.Equal(SlotProposalStatus.Withdrawn, _proposal.Status);
        Assert.True(_audit.Contains(AuditAction.ProposalWithdrawn));
    }

    [Fact]
    public async Task AnAdminCanWithdrawIt()
    {
        var result = await Handler.HandleAsync(
            new WithdrawProposalCommand(Admin, _proposal.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SlotProposalStatus.Withdrawn, _proposal.Status);
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

## after — tests/EventBooking.Application.Tests/Events/WithdrawProposalHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":301,"oldPath":"tests/EventBooking.Application.Tests/Slots/WithdrawProposalHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Events/WithdrawProposalHandlerTests.cs","beforeSha":"ec0ed06fd335de097602bb8aa91a464f0947d4cc5aedd0b10e37511d87169ca8","afterSha":"e0864048f25ed5c082e0d96d28b59079ac0c9eeb1e3199dda49b69318a25fbca","side":"after","part":1,"parts":1} -->

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
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
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

## before — tests/EventBooking.Domain.Tests/Access/StaffAccessAuditVocabularyTests.cs — 1/1

<!-- vocabulary-file: {"id":302,"oldPath":"tests/EventBooking.Domain.Tests/Access/StaffAccessAuditVocabularyTests.cs","newPath":"tests/EventBooking.Domain.Tests/Access/StaffAccessAuditVocabularyTests.cs","beforeSha":"6c6e30b75639b0546355d3ac9e832bc794b4598ab622f6f2275aa7d472895d7d","afterSha":"a3f6989b7f2405a790a9b395f278b0e1de2bf928f9b1c1af78212c7363a1bc7a","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Audit;

namespace EventBooking.Domain.Tests.Access;

public class StaffAccessAuditVocabularyTests
{
    [Fact]
    public void StaffAccessActionsAppendWithoutRenumberingExistingActions()
    {
        Assert.Equal(16, (int)AuditAction.SlotImported);
        Assert.Equal(17, (int)AuditAction.StaffAccessChanged);
        Assert.Equal(18, (int)AuditAction.StaffAccessRemoved);
    }

    [Fact]
    public void StaffAccessProfileIsAnAuditedEntityType()
    {
        Assert.Equal("StaffAccessProfile", AuditEntityTypes.StaffAccessProfile);
        Assert.Contains(AuditEntityTypes.StaffAccessProfile, AuditEntityTypes.All);
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Access/StaffAccessAuditVocabularyTests.cs — 1/1

<!-- vocabulary-file: {"id":302,"oldPath":"tests/EventBooking.Domain.Tests/Access/StaffAccessAuditVocabularyTests.cs","newPath":"tests/EventBooking.Domain.Tests/Access/StaffAccessAuditVocabularyTests.cs","beforeSha":"6c6e30b75639b0546355d3ac9e832bc794b4598ab622f6f2275aa7d472895d7d","afterSha":"a3f6989b7f2405a790a9b395f278b0e1de2bf928f9b1c1af78212c7363a1bc7a","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Audit;

namespace EventBooking.Domain.Tests.Access;

public class StaffAccessAuditVocabularyTests
{
    [Fact]
    public void StaffAccessActionsAppendWithoutRenumberingExistingActions()
    {
        Assert.Equal(16, (int)AuditAction.EventImported);
        Assert.Equal(17, (int)AuditAction.StaffAccessChanged);
        Assert.Equal(18, (int)AuditAction.StaffAccessRemoved);
    }

    [Fact]
    public void StaffAccessProfileIsAnAuditedEntityType()
    {
        Assert.Equal("StaffAccessProfile", AuditEntityTypes.StaffAccessProfile);
        Assert.Contains(AuditEntityTypes.StaffAccessProfile, AuditEntityTypes.All);
    }
}
`````

## before — tests/EventBooking.Domain.Tests/Audit/AuditLogTests.cs — 1/1

<!-- vocabulary-file: {"id":303,"oldPath":"tests/EventBooking.Domain.Tests/Audit/AuditLogTests.cs","newPath":"tests/EventBooking.Domain.Tests/Audit/AuditLogTests.cs","beforeSha":"18836e8f01a7f34c1047ea4b224ac4ff09e67a65ff52685eb37c61d7ec840195","afterSha":"10cdb9e38fd3ff72926054bdf03e1c8c2978b401417c78258801dbe246cfe59d","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Notifications;

namespace EventBooking.Domain.Tests.Audit;

public class AuditLogTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AnEntryRecordsWhoDidWhatToWhatAndWhen()
    {
        var slotId = Guid.NewGuid();
        var actor = Guid.NewGuid().ToString();

        var entry = AuditLog.Record(
            Guid.NewGuid(), AuditEntityTypes.ConfirmedSlot, slotId, AuditAction.SlotCancelled,
            ActorType.Staff, actor, Now, "6 bookings voided");

        Assert.Equal("ConfirmedSlot", entry.EntityType);
        Assert.Equal(slotId, entry.EntityId);
        Assert.Equal(AuditAction.SlotCancelled, entry.Action);
        Assert.Equal(ActorType.Staff, entry.ActorType);
        Assert.Equal(actor, entry.ActorId);
        Assert.Equal(Now, entry.Timestamp);
        Assert.Equal("6 bookings voided", entry.Details);
    }

    [Fact]
    public void ASystemActorNeedsNoIdentifier()
    {
        var entry = AuditLog.Record(
            Guid.NewGuid(), AuditEntityTypes.Invite, Guid.NewGuid(), AuditAction.InviteExpired,
            ActorType.System, null, Now, null);

        Assert.Null(entry.ActorId);
        Assert.Null(entry.Details);
    }

    [Fact]
    public void AStaffActorMustBeIdentified()
    {
        var ex = Assert.Throws<DomainException>(
            () => AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Booking, Guid.NewGuid(), AuditAction.BookingCreated,
                ActorType.Staff, null, Now, null));
        Assert.Equal("actorId must not be blank.", ex.Message);
    }

    [Fact]
    public void AnUnknownEntityTypeIsRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => AuditLog.Record(
                Guid.NewGuid(), "Sandwich", Guid.NewGuid(), AuditAction.BookingCreated,
                ActorType.System, null, Now, null));
        Assert.Equal("Sandwich is not an audited entity type.", ex.Message);
    }

    [Fact]
    public void AnEmailLogEntryRecordsTheSendAttempt()
    {
        var candidateId = Guid.NewGuid();

        var entry = EmailLog.Record(
            Guid.NewGuid(), candidateId, EmailTemplate.CandidateInvite, Now, EmailStatus.Failed);

        Assert.Equal(candidateId, entry.CandidateId);
        Assert.Equal(EmailTemplate.CandidateInvite, entry.TemplateName);
        Assert.Equal(Now, entry.SentAt);
        Assert.Equal(EmailStatus.Failed, entry.Status);
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Audit/AuditLogTests.cs — 1/1

<!-- vocabulary-file: {"id":303,"oldPath":"tests/EventBooking.Domain.Tests/Audit/AuditLogTests.cs","newPath":"tests/EventBooking.Domain.Tests/Audit/AuditLogTests.cs","beforeSha":"18836e8f01a7f34c1047ea4b224ac4ff09e67a65ff52685eb37c61d7ec840195","afterSha":"10cdb9e38fd3ff72926054bdf03e1c8c2978b401417c78258801dbe246cfe59d","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Notifications;

namespace EventBooking.Domain.Tests.Audit;

public class AuditLogTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AnEntryRecordsWhoDidWhatToWhatAndWhen()
    {
        var eventId = Guid.NewGuid();
        var actor = Guid.NewGuid().ToString();

        var entry = AuditLog.Record(
            Guid.NewGuid(), AuditEntityTypes.Event, eventId, AuditAction.EventCancelled,
            ActorType.Staff, actor, Now, "6 bookings voided");

        Assert.Equal("Event", entry.EntityType);
        Assert.Equal(eventId, entry.EntityId);
        Assert.Equal(AuditAction.EventCancelled, entry.Action);
        Assert.Equal(ActorType.Staff, entry.ActorType);
        Assert.Equal(actor, entry.ActorId);
        Assert.Equal(Now, entry.Timestamp);
        Assert.Equal("6 bookings voided", entry.Details);
    }

    [Fact]
    public void ASystemActorNeedsNoIdentifier()
    {
        var entry = AuditLog.Record(
            Guid.NewGuid(), AuditEntityTypes.Invite, Guid.NewGuid(), AuditAction.InviteExpired,
            ActorType.System, null, Now, null);

        Assert.Null(entry.ActorId);
        Assert.Null(entry.Details);
    }

    [Fact]
    public void AStaffActorMustBeIdentified()
    {
        var ex = Assert.Throws<DomainException>(
            () => AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Booking, Guid.NewGuid(), AuditAction.BookingCreated,
                ActorType.Staff, null, Now, null));
        Assert.Equal("actorId must not be blank.", ex.Message);
    }

    [Fact]
    public void AnUnknownEntityTypeIsRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => AuditLog.Record(
                Guid.NewGuid(), "Sandwich", Guid.NewGuid(), AuditAction.BookingCreated,
                ActorType.System, null, Now, null));
        Assert.Equal("Sandwich is not an audited entity type.", ex.Message);
    }

    [Fact]
    public void AnEmailLogEntryRecordsTheSendAttempt()
    {
        var attendeeId = Guid.NewGuid();

        var entry = EmailLog.Record(
            Guid.NewGuid(), attendeeId, EmailTemplate.AttendeeInvite, Now, EmailStatus.Failed);

        Assert.Equal(attendeeId, entry.AttendeeId);
        Assert.Equal(EmailTemplate.AttendeeInvite, entry.TemplateName);
        Assert.Equal(Now, entry.SentAt);
        Assert.Equal(EmailStatus.Failed, entry.Status);
    }
}
`````

## before — tests/EventBooking.Domain.Tests/Bookings/BookingTests.cs — 1/1

<!-- vocabulary-file: {"id":304,"oldPath":"tests/EventBooking.Domain.Tests/Bookings/BookingTests.cs","newPath":"tests/EventBooking.Domain.Tests/Bookings/BookingTests.cs","beforeSha":"683d0a4a461174b92c221e35af1a451b93a68887e236a97da1527ea8e1807c7e","afterSha":"676c1d2df6d57896fefeda6026b3526b874a5c245cd333de0c04e62cc1eccb48","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Tests.Bookings;

public class BookingTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid SlotA = Guid.Parse("50000001-0000-0000-0000-000000000001");
    private static readonly Guid SlotB = Guid.Parse("50000002-0000-0000-0000-000000000002");
    private static readonly Guid SlotC = Guid.Parse("50000003-0000-0000-0000-000000000003");
    private static readonly Guid SlotNotOffered = Guid.Parse("50000009-0000-0000-0000-000000000009");

    private static Invite NewInvite() =>
        Invite.CreateInitial(
            Guid.NewGuid(), Guid.NewGuid(), "invite-token-hash", Now.AddDays(4),
            [SlotA, SlotB, SlotC], [AppointmentTypeIds.DrugAndAlcoholTesting], 0);

    private static Booking NewBooking(Invite invite) =>
        Booking.Create(Guid.NewGuid(), invite, SlotB, "manage-token-hash", Now);

    [Fact]
    public void ABookingCarriesTheCandidateSlotAndInvite()
    {
        var invite = NewInvite();

        var booking = NewBooking(invite);

        Assert.Equal(invite.CandidateId, booking.CandidateId);
        Assert.Equal(SlotB, booking.ConfirmedSlotId);
        Assert.Equal(invite.Id, booking.InviteId);
        Assert.Equal(Now, booking.CreatedAt);
        Assert.Equal(BookingStatus.Active, booking.Status);
        Assert.Equal("manage-token-hash", booking.ManageTokenHash);
    }

    [Fact]
    public void BookingASlotTheInviteNeverOfferedIsRejected()
    {
        var invite = NewInvite();

        var ex = Assert.Throws<DomainException>(
            () => Booking.Create(Guid.NewGuid(), invite, SlotNotOffered, "manage-token-hash", Now));
        Assert.Equal("The chosen slot is not one of this invite's options.", ex.Message);
    }

    [Fact]
    public void BookingAnInviteThatIsNoLongerPendingIsRejected()
    {
        var invite = NewInvite();
        invite.MarkExpired();

        var ex = Assert.Throws<DomainException>(() => NewBooking(invite));
        Assert.Equal("This invite can no longer be used.", ex.Message);
    }

    [Fact]
    public void ABookingWithoutAManageTokenHashIsRejected()
    {
        var invite = NewInvite();

        var ex = Assert.Throws<DomainException>(
            () => Booking.Create(Guid.NewGuid(), invite, SlotB, " ", Now));
        Assert.Equal("manageTokenHash must not be blank.", ex.Message);
    }

    [Fact]
    public void CreatingABookingDoesNotConsumeTheInvite()
    {
        var invite = NewInvite();

        NewBooking(invite);

        Assert.Equal(InviteStatus.Pending, invite.Status);
    }

    [Fact]
    public void CancellingMarksTheBookingCancelled()
    {
        var booking = NewBooking(NewInvite());

        booking.Cancel();

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
    }

    [Fact]
    public void CancellingTwiceIsRejected()
    {
        var booking = NewBooking(NewInvite());
        booking.Cancel();

        var ex = Assert.Throws<DomainException>(() => booking.Cancel());
        Assert.Equal("This booking has already been cancelled.", ex.Message);
    }
}
`````
