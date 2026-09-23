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
