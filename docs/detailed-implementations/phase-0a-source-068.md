# 00a — Port source 68 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## tests/EventBooking.Application.Tests/Slots/SlotCancellationConcurrencyTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Slots/SlotCancellationConcurrencyTests.cs","encoding":"utf8","sha256":"c16fe59954ccade298c7539644889e284e8ebc5bffaf61b041692007a9eabe55","parts":1,"part":1} -->

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

## tests/EventBooking.Application.Tests/Slots/WithdrawAcceptanceHandlerTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Slots/WithdrawAcceptanceHandlerTests.cs","encoding":"utf8","sha256":"fee2b0140b113d90912ae8f7d13fb33325c52ea3bdc238570319a2740f7b3d0b","parts":1,"part":1} -->

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

## tests/EventBooking.Application.Tests/Slots/WithdrawProposalHandlerTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Slots/WithdrawProposalHandlerTests.cs","encoding":"utf8","sha256":"ec0ed06fd335de097602bb8aa91a464f0947d4cc5aedd0b10e37511d87169ca8","parts":1,"part":1} -->

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

## tests/EventBooking.Domain.Tests/Access/StaffAccessAuditVocabularyTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Domain.Tests/Access/StaffAccessAuditVocabularyTests.cs","encoding":"utf8","sha256":"6c6e30b75639b0546355d3ac9e832bc794b4598ab622f6f2275aa7d472895d7d","parts":1,"part":1} -->

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

## tests/EventBooking.Domain.Tests/Access/StaffAccessProfileTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Domain.Tests/Access/StaffAccessProfileTests.cs","encoding":"utf8","sha256":"d4eb99b07d9637851d3873fb07d691e1bd6994b536744126bac3e2f9e699434e","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Tests.Access;

public class StaffAccessProfileTests
{
    public static TheoryData<Role[], Guid?> ValidShapes => new()
    {
        { [Role.Admin], null },
        { [Role.Coordinator], null },
        { [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting },
        { [Role.AppointmentStaff], AppointmentTypeIds.MedicalCheckUp },
        { [Role.Coordinator, Role.Manager], AppointmentTypeIds.UniformFitting },
        { [Role.Coordinator, Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting },
        { [Role.Manager, Role.AppointmentStaff], AppointmentTypeIds.MedicalCheckUp },
        { [Role.Coordinator, Role.Manager, Role.AppointmentStaff], AppointmentTypeIds.UniformFitting },
    };

    [Theory]
    [MemberData(nameof(ValidShapes))]
    public void EveryApprovedShapeIsAccepted(Role[] roles, Guid? scope)
    {
        var profile = StaffAccessProfile.Create(Guid.NewGuid(), roles, scope);

        Assert.True(roles.ToHashSet().SetEquals(profile.Roles));
        Assert.Equal(scope, profile.AppointmentTypeId);
        Assert.Equal(1, profile.Version);
        Assert.True(profile.IsValid());
    }

    [Theory]
    [MemberData(nameof(InvalidShapes))]
    public void EveryForbiddenShapeIsRejected(Role[] roles, Guid? scope)
    {
        Assert.Throws<DomainException>(() =>
            StaffAccessProfile.Create(Guid.NewGuid(), roles, scope));
    }

    public static TheoryData<Role[], Guid?> InvalidShapes => new()
    {
        { [], null },
        { [Role.Admin, Role.Coordinator], null },
        { [Role.Admin, Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting },
        { [Role.Coordinator], AppointmentTypeIds.DrugAndAlcoholTesting },
        { [(Role)999], null },
        { [Role.Manager], Guid.NewGuid() },
    };

    [Fact]
    public void EmptyStaffIdentifierIsRejected()
    {
        Assert.Throws<DomainException>(() =>
            StaffAccessProfile.Create(Guid.Empty, [Role.Coordinator], null));
    }

    public static TheoryData<Role[], Guid?> TransitionalShapesWithoutScope => new()
    {
        { [Role.Manager], null },
        { [Role.AppointmentStaff], null },
        { [Role.Coordinator, Role.Manager], null },
        { [Role.Coordinator, Role.AppointmentStaff], null },
        { [Role.Manager, Role.AppointmentStaff], null },
        { [Role.Coordinator, Role.Manager, Role.AppointmentStaff], null },
    };

    [Theory]
    [MemberData(nameof(TransitionalShapesWithoutScope))]
    public void EveryTransitionalShapeIsAcceptedWithNullScope(Role[] roles, Guid? scope)
    {
        var profile = StaffAccessProfile.Create(Guid.NewGuid(), roles, scope);

        Assert.True(roles.ToHashSet().SetEquals(profile.Roles));
        Assert.Null(profile.AppointmentTypeId);
        Assert.True(profile.IsValid());
    }

    [Fact]
    public void ScopeIsStillForbiddenWithoutAScopedRole()
    {
        Assert.Throws<DomainException>(() => StaffAccessProfile.Create(
            Guid.NewGuid(), [Role.Coordinator], AppointmentTypeIds.DrugAndAlcoholTesting));
    }

    [Fact]
    public void ReplacingAProfileChangesTheWholeShapeAndAdvancesVersion()
    {
        var profile = StaffAccessProfile.Create(Guid.NewGuid(), [Role.Coordinator], null);

        profile.Replace(
            [Role.Coordinator, Role.Manager, Role.AppointmentStaff],
            AppointmentTypeIds.MedicalCheckUp);

        Assert.True(new HashSet<Role>
        {
            Role.Manager,
            Role.Coordinator,
            Role.AppointmentStaff,
        }.SetEquals(profile.Roles));
        Assert.Equal(AppointmentTypeIds.MedicalCheckUp, profile.AppointmentTypeId);
        Assert.Equal(2, profile.Version);
    }

    [Fact]
    public void RejectedReplacementLeavesTheOldProfileUntouched()
    {
        var profile = StaffAccessProfile.Create(Guid.NewGuid(), [Role.Coordinator], null);

        Assert.Throws<DomainException>(() =>
            profile.Replace([Role.Admin, Role.Coordinator], null));

        Assert.True(new HashSet<Role> { Role.Coordinator }.SetEquals(profile.Roles));
        Assert.Null(profile.AppointmentTypeId);
        Assert.Equal(1, profile.Version);
    }

    [Fact]
    public void RemovingManagerPreservesAppointmentStaffAndItsSharedScope()
    {
        var profile = StaffAccessProfile.Create(
            Guid.NewGuid(),
            [Role.Coordinator, Role.Manager, Role.AppointmentStaff],
            AppointmentTypeIds.DrugAndAlcoholTesting);

        var empty = profile.RemoveManagerRole();

        Assert.False(empty);
        Assert.True(new HashSet<Role>
        {
            Role.Coordinator,
            Role.AppointmentStaff,
        }.SetEquals(profile.Roles));
        Assert.Equal(AppointmentTypeIds.DrugAndAlcoholTesting, profile.AppointmentTypeId);
        Assert.Equal(2, profile.Version);
    }

    [Fact]
    public void RemovingTheOnlyManagerReportsThatTheProfileIsEmpty()
    {
        var profile = StaffAccessProfile.Create(
            Guid.NewGuid(), [Role.Manager], AppointmentTypeIds.UniformFitting);

        Assert.True(profile.RemoveManagerRole());
        Assert.Empty(profile.Roles);
        Assert.Null(profile.AppointmentTypeId);
        Assert.Equal(2, profile.Version);
    }
}
`````

## tests/EventBooking.Domain.Tests/Access/StaffIdTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Domain.Tests/Access/StaffIdTests.cs","encoding":"utf8","sha256":"f47b88a3ffe623c782d30905c6d944208dd17a6d860d8cc4735180fdb9907d16","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Access;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Tests.Access;

/// <summary>Verifies canonical enterprise staff-number validation and identity invariants.</summary>
public sealed class StaffIdTests
{
    /// <summary>Verifies accepted values are exposed in the canonical uppercase form.</summary>
    [Theory]
    [InlineData("U000000", "U000000")]
    [InlineData("N999999", "N999999")]
    [InlineData("u123456", "U123456")]
    [InlineData("n654321", "N654321")]
    public void ValidValuesAreCanonicalised(string input, string expected)
    {
        var staffId = new StaffId(input);

        Assert.Equal(expected, staffId.Value);
        Assert.Equal(expected, staffId.ToString());
    }

    /// <summary>Verifies malformed or absent values cannot enter the domain.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" U123456")]
    [InlineData("U123456 ")]
    [InlineData("X123456")]
    [InlineData("U12345")]
    [InlineData("U1234567")]
    [InlineData("U12345A")]
    public void InvalidValuesCannotBeConstructed(string? input)
    {
        Assert.Throws<DomainException>(() => new StaffId(input!));
        Assert.False(StaffId.TryParse(input, out var parsed));
        Assert.Null(parsed);
    }

    /// <summary>Verifies record equality observes canonical rather than input casing.</summary>
    [Fact]
    public void EqualityUsesTheCanonicalValue()
    {
        Assert.Equal(new StaffId("u123456"), new StaffId("U123456"));
    }

    /// <summary>Verifies the identity pair is immutable while its approximate observation advances.</summary>
    [Fact]
    public void StaffIdentityKeepsThePairAndRefreshesLastSeenOnly()
    {
        var userId = Guid.NewGuid();
        var firstSeen = DateTimeOffset.Parse("2026-09-08T09:00:00Z");
        var identity = StaffIdentity.Create(userId, new StaffId("N123456"), null, firstSeen);

        identity.MarkSeen(null, firstSeen.AddHours(1));

        Assert.Equal(userId, identity.StaffUserId);
        Assert.Equal(new StaffId("N123456"), identity.StaffId);
        Assert.Equal(firstSeen.AddHours(1), identity.LastSeenAt);
    }

    /// <summary>Verifies an identity observed without a name claim mirrors no display name.</summary>
    [Fact]
    public void CreateStoresNullDisplayName()
    {
        var identity = StaffIdentity.Create(
            Guid.NewGuid(), new StaffId("U000002"), null, DateTimeOffset.UtcNow);

        Assert.Null(identity.DisplayName);
    }

    /// <summary>Verifies an observed name is mirrored onto the identity.</summary>
    [Fact]
    public void CreateStoresNonNullDisplayName()
    {
        var identity = StaffIdentity.Create(
            Guid.NewGuid(), new StaffId("U000003"), "Dana Datson", DateTimeOffset.UtcNow);

        Assert.Equal("Dana Datson", identity.DisplayName);
    }

    /// <summary>Verifies a later token without a name clears the mirrored value.</summary>
    [Fact]
    public void MarkSeenOverwritesDisplayNameBackToNull()
    {
        var start = DateTimeOffset.UtcNow;
        var identity = StaffIdentity.Create(
            Guid.NewGuid(), new StaffId("U000004"), "Meddy Medson", start);

        identity.MarkSeen(null, start.AddMinutes(1));

        Assert.Null(identity.DisplayName);
    }

    /// <summary>Verifies a rename observed at the next refresh replaces the mirrored value.</summary>
    [Fact]
    public void MarkSeenUpdatesDisplayNameToNewValue()
    {
        var start = DateTimeOffset.UtcNow;
        var identity = StaffIdentity.Create(Guid.NewGuid(), new StaffId("U000005"), "Old Name", start);

        identity.MarkSeen("New Name", start.AddMinutes(1));

        Assert.Equal("New Name", identity.DisplayName);
        Assert.Equal(start.AddMinutes(1), identity.LastSeenAt);
    }

    /// <summary>Verifies the observation time still refuses to move backwards.</summary>
    [Fact]
    public void MarkSeenStillRejectsBackwardsTime()
    {
        var start = DateTimeOffset.UtcNow;
        var identity = StaffIdentity.Create(Guid.NewGuid(), new StaffId("U000006"), "Old Name", start);

        Assert.Throws<DomainException>(() => identity.MarkSeen("New Name", start.AddMinutes(-1)));
    }
}
`````

## tests/EventBooking.Domain.Tests/AppointmentTypes/AppointmentTypeTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Domain.Tests/AppointmentTypes/AppointmentTypeTests.cs","encoding":"utf8","sha256":"0dcbf98cfc08f0a30fbcf212753ddfe7ea99acfb2abc8877242aada65474711f","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Tests.AppointmentTypes;

public class AppointmentTypeTests
{
    [Fact]
    public void ThereAreExactlyThreeAppointmentTypes()
    {
        Assert.Equal(3, AppointmentTypeIds.All.Count);
        Assert.Equal(
            new[]
            {
                AppointmentTypeIds.DrugAndAlcoholTesting,
                AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting,
            },
            AppointmentTypeIds.All);
    }

    [Fact]
    public void TheIdentifiersAreStableConstants()
    {
        Assert.Equal(Guid.Parse("a0000001-0000-0000-0000-000000000001"), AppointmentTypeIds.DrugAndAlcoholTesting);
        Assert.Equal(Guid.Parse("a0000002-0000-0000-0000-000000000002"), AppointmentTypeIds.MedicalCheckUp);
        Assert.Equal(Guid.Parse("a0000003-0000-0000-0000-000000000003"), AppointmentTypeIds.UniformFitting);
    }

    [Theory]
    [InlineData("DAT")]
    [InlineData("dat")]
    [InlineData(" Dat ")]
    public void CodeLookupIsCaseAndWhitespaceInsensitive(string code)
    {
        Assert.True(AppointmentTypeIds.TryFromCode(code, out var id));
        Assert.Equal(AppointmentTypeIds.DrugAndAlcoholTesting, id);
    }

    [Theory]
    [InlineData("XYZ")]
    [InlineData("")]
    [InlineData(null)]
    public void UnknownCodesAreRejected(string? code)
    {
        Assert.False(AppointmentTypeIds.TryFromCode(code, out var id));
        Assert.Equal(Guid.Empty, id);
    }

    [Fact]
    public void NamesMatchTheSpecWording()
    {
        Assert.Equal("Drug & Alcohol Testing", AppointmentTypeIds.NameOf(AppointmentTypeIds.DrugAndAlcoholTesting));
        Assert.Equal("Medical Check-up", AppointmentTypeIds.NameOf(AppointmentTypeIds.MedicalCheckUp));
        Assert.Equal("Uniform Fitting", AppointmentTypeIds.NameOf(AppointmentTypeIds.UniformFitting));
    }

    [Fact]
    public void NameOfRejectsAnUnknownIdentifier()
    {
        Assert.Throws<DomainException>(() => AppointmentTypeIds.NameOf(Guid.NewGuid()));
    }

    [Fact]
    public void TheFixedSetCarriesIdentityCodeAndName()
    {
        var types = AppointmentType.CreateFixedSet();

        Assert.Equal(3, types.Count);
        Assert.Equal(
            new[] { "DAT", "MED", "UNI" },
            types.Select(t => t.Code));
        Assert.Equal(
            new[] { "Drug & Alcohol Testing", "Medical Check-up", "Uniform Fitting" },
            types.Select(t => t.Name));
        Assert.Equal(AppointmentTypeIds.All, types.Select(t => t.Id).ToList());
    }
}
`````

## tests/EventBooking.Domain.Tests/Audit/AuditLogTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Domain.Tests/Audit/AuditLogTests.cs","encoding":"utf8","sha256":"18836e8f01a7f34c1047ea4b224ac4ff09e67a65ff52685eb37c61d7ec840195","parts":1,"part":1} -->

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

## tests/EventBooking.Domain.Tests/Bookings/BookingAppointmentAuditVocabularyTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Domain.Tests/Bookings/BookingAppointmentAuditVocabularyTests.cs","encoding":"utf8","sha256":"cef00ad92e7f80dc3a145cefe2e8e51ea14c1a3681e21d7ff59860e5a0d3b21a","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Audit;

namespace EventBooking.Domain.Tests.Bookings;

/// <summary>Verifies stable audit names and numeric values for appointment delivery.</summary>
public sealed class BookingAppointmentAuditVocabularyTests
{
    /// <summary>Verifies appointment actions append after Issue #71 without renumbering history.</summary>
    [Fact]
    public void AppointmentActionsAppendAfterStaffAccessActions()
    {
        Assert.Equal(18, (int)AuditAction.StaffAccessRemoved);
        Assert.Equal(19, (int)AuditAction.AppointmentCheckedIn);
        Assert.Equal(20, (int)AuditAction.AppointmentCompleted);
        Assert.Equal(21, (int)AuditAction.AppointmentMarkedNoShow);
        Assert.Equal(22, (int)AuditAction.AppointmentStatusCorrected);
    }

    /// <summary>Verifies booking appointments can be correlated through the shared audit logger.</summary>
    [Fact]
    public void BookingAppointmentIsAnAuditedEntityType()
    {
        Assert.Equal("BookingAppointment", AuditEntityTypes.BookingAppointment);
        Assert.Single(
            AuditEntityTypes.All,
            value => value == AuditEntityTypes.BookingAppointment);
    }
}
`````

## tests/EventBooking.Domain.Tests/Bookings/BookingAppointmentTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Domain.Tests/Bookings/BookingAppointmentTests.cs","encoding":"utf8","sha256":"444f171059185712885c07b6ee1a3a0b9c0d017e0631b0019997a8921a7e352a","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Tests.Bookings;

/// <summary>Verifies the ontology-approved lifecycle of one booking appointment.</summary>
public sealed class BookingAppointmentTests
{
    private static readonly DateTimeOffset CheckInTime =
        new(2026, 9, 7, 8, 55, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset OutcomeTime = CheckInTime.AddHours(2);

    /// <summary>Verifies that a new record represents an untouched expected appointment.</summary>
    [Fact]
    public void NewAppointmentStartsExpectedWithNoOperationalTimestamps()
    {
        var appointment = NewAppointment();

        Assert.Equal(BookingAppointmentStatus.Expected, appointment.Status);
        Assert.Null(appointment.CheckedInAt);
        Assert.Null(appointment.OutcomeAt);
        Assert.Null(appointment.LastChangedByStaffUserId);
        Assert.Null(appointment.LastChangedAt);
        Assert.Equal(1, appointment.Version);
    }

    /// <summary>Verifies normal check-in and completion timestamp semantics.</summary>
    [Fact]
    public void CheckInThenCompletionPreservesBothOperationalInstants()
    {
        var staff = Guid.NewGuid();
        var appointment = NewAppointment();

        Assert.True(appointment.TransitionTo(
            BookingAppointmentStatus.CheckedIn, staff, CheckInTime, true, false));
        Assert.True(appointment.TransitionTo(
            BookingAppointmentStatus.Completed, staff, OutcomeTime, false, false));

        Assert.Equal(BookingAppointmentStatus.Completed, appointment.Status);
        Assert.Equal(CheckInTime, appointment.CheckedInAt);
        Assert.Equal(OutcomeTime, appointment.OutcomeAt);
        Assert.Equal(staff, appointment.LastChangedByStaffUserId);
        Assert.Equal(OutcomeTime, appointment.LastChangedAt);
        Assert.Equal(3, appointment.Version);
    }

    /// <summary>Verifies that no-show is a terminal alternative without check-in.</summary>
    [Fact]
    public void NoShowRecordsOnlyTheOutcomeInstant()
    {
        var appointment = NewAppointment();

        appointment.TransitionTo(
            BookingAppointmentStatus.NoShow,
            Guid.NewGuid(),
            OutcomeTime,
            checkInAllowed: false,
            noShowAllowed: true);

        Assert.Equal(BookingAppointmentStatus.NoShow, appointment.Status);
        Assert.Null(appointment.CheckedInAt);
        Assert.Equal(OutcomeTime, appointment.OutcomeAt);
        Assert.Equal(2, appointment.Version);
    }

    /// <summary>Verifies each correction clears only the timestamp invalidated by that reversal.</summary>
    [Fact]
    public void BoundedCorrectionsClearOnlyInvalidatedTimestamps()
    {
        var staff = Guid.NewGuid();
        var appointment = NewAppointment();
        appointment.TransitionTo(
            BookingAppointmentStatus.CheckedIn, staff, CheckInTime, true, false);
        appointment.TransitionTo(
            BookingAppointmentStatus.Completed, staff, OutcomeTime, false, false);

        appointment.TransitionTo(
            BookingAppointmentStatus.CheckedIn, staff, OutcomeTime.AddMinutes(1), false, false);
        Assert.Equal(CheckInTime, appointment.CheckedInAt);
        Assert.Null(appointment.OutcomeAt);

        appointment.TransitionTo(
            BookingAppointmentStatus.Expected, staff, OutcomeTime.AddMinutes(2), false, false);
        Assert.Null(appointment.CheckedInAt);
        Assert.Null(appointment.OutcomeAt);

        appointment.TransitionTo(
            BookingAppointmentStatus.NoShow, staff, OutcomeTime.AddMinutes(3), false, true);
        appointment.TransitionTo(
            BookingAppointmentStatus.Expected, staff, OutcomeTime.AddMinutes(4), false, false);
        Assert.Null(appointment.CheckedInAt);
        Assert.Null(appointment.OutcomeAt);
        Assert.Equal(7, appointment.Version);
    }

    /// <summary>Verifies that retrying the stored state changes no observable field.</summary>
    [Fact]
    public void RepeatingTheCurrentStateIsIdempotent()
    {
        var staff = Guid.NewGuid();
        var appointment = NewAppointment();
        appointment.TransitionTo(
            BookingAppointmentStatus.CheckedIn, staff, CheckInTime, true, false);

        var changed = appointment.TransitionTo(
            BookingAppointmentStatus.CheckedIn,
            Guid.NewGuid(),
            OutcomeTime,
            checkInAllowed: false,
            noShowAllowed: false);

        Assert.False(changed);
        Assert.Equal(staff, appointment.LastChangedByStaffUserId);
        Assert.Equal(CheckInTime, appointment.LastChangedAt);
        Assert.Equal(2, appointment.Version);
    }

    /// <summary>Verifies timing gates and transitions outside the approved graph are rejected.</summary>
    [Fact]
    public void InvalidOrPrematureTransitionsAreRejectedWithoutMutation()
    {
        var staff = Guid.NewGuid();
        var appointment = NewAppointment();

        Assert.Throws<DomainException>(() => appointment.TransitionTo(
            BookingAppointmentStatus.CheckedIn, staff, CheckInTime, false, false));
        Assert.Throws<DomainException>(() => appointment.TransitionTo(
            BookingAppointmentStatus.NoShow, staff, OutcomeTime, false, false));
        Assert.Throws<DomainException>(() => appointment.TransitionTo(
            BookingAppointmentStatus.Completed, staff, OutcomeTime, true, true));

        Assert.Equal(BookingAppointmentStatus.Expected, appointment.Status);
        Assert.Equal(1, appointment.Version);
    }

    /// <summary>Verifies identifiers and fixed appointment-type membership at creation.</summary>
    [Fact]
    public void CreationRejectsEmptyOrUnknownIdentifiers()
    {
        Assert.Throws<DomainException>(() => BookingAppointment.Create(
            Guid.Empty, Guid.NewGuid(), AppointmentTypeIds.DrugAndAlcoholTesting));
        Assert.Throws<DomainException>(() => BookingAppointment.Create(
            Guid.NewGuid(), Guid.Empty, AppointmentTypeIds.DrugAndAlcoholTesting));
        Assert.Throws<DomainException>(() => BookingAppointment.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
    }

    private static BookingAppointment NewAppointment() => BookingAppointment.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        AppointmentTypeIds.DrugAndAlcoholTesting);
}
`````

## tests/EventBooking.Domain.Tests/Bookings/BookingTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Domain.Tests/Bookings/BookingTests.cs","encoding":"utf8","sha256":"683d0a4a461174b92c221e35af1a451b93a68887e236a97da1527ea8e1807c7e","parts":1,"part":1} -->

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

## tests/EventBooking.Domain.Tests/Bookings/RecoveryBookingTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Domain.Tests/Bookings/RecoveryBookingTests.cs","encoding":"utf8","sha256":"cc440e7867220064ebd81c68fe48ab91e127602b140526ce26875d5301b413d5","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Tests.Bookings;

/// <summary>Verifies every recovery points directly to an immutable active journey root.</summary>
public sealed class RecoveryBookingTests
{
    /// <summary>A recovery Booking copies the original root and can conclude and reopen.</summary>
    [Fact]
    public void RecoveryLifecyclePreservesOriginalRoot()
    {
        var candidateId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var initialInvite = Invite.CreateInitial(
            Guid.NewGuid(), candidateId, "initial", DateTimeOffset.UtcNow.AddDays(1),
            [slotId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        var original = Booking.Create(
            Guid.NewGuid(), initialInvite, slotId, "manage-original", DateTimeOffset.UtcNow);
        var recoverySlot = Guid.NewGuid();
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(), candidateId, original.Id, "recovery", DateTimeOffset.UtcNow.AddDays(2),
            [recoverySlot, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);

        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, recoverySlot,
            "manage-recovery", DateTimeOffset.UtcNow.AddHours(1));
        recovery.Conclude();
        recovery.Reopen();

        Assert.Null(original.RecoveryOfBookingId);
        Assert.Equal(BookingStatus.Active, original.Status);
        Assert.Equal(original.Id, recovery.RecoveryOfBookingId);
        Assert.Equal(BookingStatus.Active, recovery.Status);
    }

    /// <summary>A recovery cannot point at another recovery Booking.</summary>
    [Fact]
    public void RecoveryChainIsRejected()
    {
        var candidateId = Guid.NewGuid();
        var rootSlot = Guid.NewGuid();
        var initial = Invite.CreateInitial(
            Guid.NewGuid(), candidateId, "initial", DateTimeOffset.UtcNow.AddDays(1),
            [rootSlot, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        var root = Booking.Create(Guid.NewGuid(), initial, rootSlot, "root", DateTimeOffset.UtcNow);
        var firstSlot = Guid.NewGuid();
        var firstInvite = Invite.CreateRecovery(
            Guid.NewGuid(), candidateId, root.Id, "first", DateTimeOffset.UtcNow.AddDays(1),
            [firstSlot, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);
        var first = Booking.CreateRecovery(
            Guid.NewGuid(), firstInvite, root, firstSlot, "first-manage", DateTimeOffset.UtcNow);
        var secondSlot = Guid.NewGuid();
        var secondInvite = Invite.CreateRecovery(
            Guid.NewGuid(), candidateId, root.Id, "second", DateTimeOffset.UtcNow.AddDays(1),
            [secondSlot, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);

        Assert.Throws<EventBooking.Domain.Common.DomainException>(() => Booking.CreateRecovery(
            Guid.NewGuid(), secondInvite, first, secondSlot, "second-manage", DateTimeOffset.UtcNow));
    }
}
`````
