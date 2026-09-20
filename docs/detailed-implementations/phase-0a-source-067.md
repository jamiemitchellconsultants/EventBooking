# 00a — Port source 67 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## tests/EventBooking.Application.Tests/Slots/CancelConfirmedSlotHandlerTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Slots/CancelConfirmedSlotHandlerTests.cs","encoding":"utf8","sha256":"4e7a2f19a71ef1bc3d7abd905a5dcf241318e5846a77acafcc76051b7d2dc57a","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
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

public class CancelConfirmedSlotHandlerTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly CandidatePortalOptions Portal = new(
        "https://booking.example.com", "Corporate HQ", "recruitment@corp.com");

    private readonly TransactionOperationLog _operations = new();
    private readonly InMemoryConfirmedSlotRepository _slots;
    private readonly InMemoryBookingRepository _bookings;
    private readonly InMemoryBookingAppointmentRepository _appointments;
    private readonly InMemoryCandidateRepository _candidates;
    private readonly InMemoryInviteRepository _invites;
    private readonly InMemoryEmployeeGroupRepository _groups = new();
    private readonly InMemorySystemSettingsRepository _settings = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly RecordingEmailSender _email = new();
    private readonly InMemoryEmailDeliveryRepository _deliveries = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork;
    private readonly FakeTokenService _tokens = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly ConfirmedSlot _slot;

    private CancelConfirmedSlotHandler Handler => new(
        _slots,
        _bookings,
        _invites,
        _candidates,
        _roles,
        new BookingCanceller(_appointments, new InMemorySlotCapacityRepository(_slots), _audit),
        new InviteIssuer(
            _invites, _groups, new EligibleSlotFinder(_slots, _clock), _settings,
            _tokens, EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
            _audit, _clock, Portal),
        new EligibleSlotFinder(_slots, _clock),
        _appointments,
        EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
        _audit,
        _clock,
        _unitOfWork);

    public CancelConfirmedSlotHandlerTests()
    {
        _invites = new InMemoryInviteRepository(_operations);
        _slots = new InMemoryConfirmedSlotRepository(_operations);
        _bookings = new InMemoryBookingRepository(_operations);
        _appointments = new InMemoryBookingAppointmentRepository(_bookings, _operations);
        _candidates = new InMemoryCandidateRepository(_operations);
        _unitOfWork = new FakeUnitOfWork(_operations);
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));

        _slot = AddSlot(10);
        AddSlot(12);
        AddSlot(14);
        AddSlot(16);
    }

    [Fact]
    public async Task APastSlotCannotBeCancelled()
    {
        var past = AddSlot(1);

        var result = await Handler.HandleAsync(
            new CancelConfirmedSlotCommand(Coordinator, past.Id, true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(ConfirmedSlotStatus.Active, past.Status);
    }

    [Fact]
    public async Task ASlotWithNoBookingsIsCancelledWithoutConfirmation()
    {
        var result = await Handler.HandleAsync(
            new CancelConfirmedSlotCommand(Coordinator, _slot.Id, false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.BookingsVoided);
        Assert.Equal(ConfirmedSlotStatus.Cancelled, _slot.Status);
        Assert.True(_audit.Contains(AuditAction.SlotCancelled));
    }

    [Fact]
    public async Task AppointmentStaffIsDeniedBeforeTransactionOrSlotLock()
    {
        var appointmentStaff = Guid.NewGuid();
        ((IStaffAccessProfileRepository)_roles).Add(StaffAccessProfile.Create(
            appointmentStaff,
            [Role.AppointmentStaff],
            AppointmentTypeIds.DrugAndAlcoholTesting));

        var result = await Handler.HandleAsync(
            new CancelConfirmedSlotCommand(appointmentStaff, _slot.Id, true),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.DoesNotContain("transaction-begun", _operations.Events);
        Assert.DoesNotContain("slot-guard-locked", _operations.Events);
        Assert.Equal(ConfirmedSlotStatus.Active, _slot.Status);
    }

    [Fact]
    public async Task ASlotWithBookingsNeedsConfirmationAndSaysHowMany()
    {
        BookACandidate("Amara Novak", "a.novak@mail.com");
        BookACandidate("B. Chen", "b.chen@mail.com");

        var result = await Handler.HandleAsync(
            new CancelConfirmedSlotCommand(Coordinator, _slot.Id, false), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(
            "Cancelling this slot will cancel 2 confirmed bookings. Affected candidates will be notified and re-invited. Confirm to proceed.",
            result.Error.Message);
        Assert.Equal(ConfirmedSlotStatus.Active, _slot.Status);
    }

    /// <summary>Verifies cancellation uses one business commit and one delivery result commit per message.</summary>
    [Fact]
    public async Task ConfirmedCancellationVoidsEveryBookingAndReInvitesEveryCandidate()
    {
        var amara = BookACandidate("Amara Novak", "a.novak@mail.com");
        var chen = BookACandidate("B. Chen", "b.chen@mail.com");

        var result = await Handler.HandleAsync(
            new CancelConfirmedSlotCommand(Coordinator, _slot.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.BookingsVoided);
        Assert.Equal(2, result.Value.CandidatesReinvited);

        Assert.All(_bookings.Items, b => Assert.Equal(BookingStatus.Cancelled, b.Status));
        Assert.Equal(CandidateStatus.Invited, amara.Status);
        Assert.Equal(CandidateStatus.Invited, chen.Status);
        Assert.Equal(ConfirmedSlotStatus.Cancelled, _slot.Status);
        Assert.Equal(10, _slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(5, _unitOfWork.CommitCount);
    }

    /// <summary>Verifies candidate lifecycle locks are acquired before the confirmed-slot guard.</summary>
    [Fact]
    public async Task CancellationTakesCandidateLifecycleLocksBeforeTheSlotGuard()
    {
        BookACandidate("Amara Novak", "a.novak@mail.com");

        await Handler.HandleAsync(
            new CancelConfirmedSlotCommand(Coordinator, _slot.Id, true), CancellationToken.None);

        Assert.Equal(
            ["active-candidate-ids-snapshotted", "transaction-begun", "candidate-locked", "pending-invites-locked", "original-booking-locked", "active-recovery-locked", "slot-guard-locked", "active-bookings-listed"],
            _operations.Events.Take(8));
    }

    [Fact]
    public async Task EachAffectedCandidateGetsACancellationEmailThenAnInvite()
    {
        BookACandidate("Amara Novak", "a.novak@mail.com");

        await Handler.HandleAsync(
            new CancelConfirmedSlotCommand(Coordinator, _slot.Id, true), CancellationToken.None);

        Assert.Equal(2, _email.Sent.Count);
        Assert.Equal(EmailTemplate.CandidateInvite, _email.Sent[0].Template);
        Assert.Equal(EmailTemplate.SlotCancelledRebookingNeeded, _email.Sent[1].Template);
    }

    /// <summary>Failed replacement delivery produces neutral cancellation wording.</summary>
    [Fact]
    public async Task AFailedReplacementDoesNotPromiseThatAnInviteIsOnItsWay()
    {
        BookACandidate("Amara Novak", "a.novak@mail.com");
        _email.FailNextSend = true;

        var result = await Handler.HandleAsync(
            new CancelConfirmedSlotCommand(Coordinator, _slot.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var cancellation = Assert.Single(_email.Sent, message =>
            message.Template == EmailTemplate.SlotCancelledRebookingNeeded);
        Assert.Contains("recruitment team will contact you", cancellation.TextBody);
        Assert.DoesNotContain("on its way", cancellation.TextBody);
    }

    [Fact]
    public async Task TheCancelledSlotIsNeverOfferedInTheReplacementInvites()
    {
        BookACandidate("Amara Novak", "a.novak@mail.com");

        await Handler.HandleAsync(
            new CancelConfirmedSlotCommand(Coordinator, _slot.Id, true), CancellationToken.None);

        var reissued = _invites.Items.Single(i => i.Status == InviteStatus.Pending);
        Assert.DoesNotContain(_slot.Id, reissued.OfferedSlotIds);
    }

    [Fact]
    public async Task CancellingAnAlreadyCancelledSlotIsAConflict()
    {
        _slot.Cancel();

        var result = await Handler.HandleAsync(
            new CancelConfirmedSlotCommand(Coordinator, _slot.Id, true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("This slot has already been cancelled.", result.Error.Message);
    }

    [Fact]
    public async Task AnUnknownSlotIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new CancelConfirmedSlotCommand(Coordinator, Guid.NewGuid(), true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task AUserWithNoRoleIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new CancelConfirmedSlotCommand(Guid.NewGuid(), _slot.Id, true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    private Candidate BookACandidate(string name, string email)
    {
        var pilots = EmployeeGroup.Define(
            EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        if (!_groups.Items.Any(group => group.Id == pilots.Id))
        {
            _groups.Items.Add(pilots);
        }

        var candidate = Candidate.Create(Guid.NewGuid(), name, email, pilots);
        _candidates.Add(candidate);

        var inviteId = Guid.NewGuid();
        var issued = _tokens.Issue(inviteId);
        var invite = Invite.CreateInitial(
            inviteId, candidate.Id, issued.TokenHash, _clock.UtcNow.AddDays(4),
            [_slot.Id, _slots.Items[1].Id, _slots.Items[2].Id],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting], 0);
        _invites.Add(invite);
        candidate.MarkInvited();

        var bookingId = Guid.NewGuid();
        var manage = _tokens.Issue(bookingId);
        _bookings.Add(Booking.Create(bookingId, invite, _slot.Id, manage.TokenHash, _clock.UtcNow));
        invite.MarkUsed();
        candidate.MarkBooked();

        _slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), bookingId, AppointmentTypeIds.DrugAndAlcoholTesting));
        _slot.CapacityFor(AppointmentTypeIds.UniformFitting).Decrement();
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), bookingId, AppointmentTypeIds.UniformFitting));

        return candidate;
    }

    [Fact]
    public async Task CancellingASlotHoldingAnActiveRecoveryIssuesAReplacement()
    {
        var candidate = BookACandidate("Amara Novak", "a.novak@mail.com");
        var original = _bookings.Items.Single();
        var recoverySlot = _slots.Items[1];
        var recovery = AddActiveRecoveryOn(candidate, original, recoverySlot);

        var result = await Handler.HandleAsync(
            new CancelConfirmedSlotCommand(Coordinator, recoverySlot.Id, true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.BookingsVoided);
        Assert.Equal(1, result.Value.CandidatesReinvited);
        Assert.Equal(BookingStatus.Cancelled, recovery.Status);
        Assert.Equal(BookingStatus.Active, original.Status);
        Assert.Equal(CandidateStatus.Booked, candidate.Status);
        Assert.Equal(
            10, recoverySlot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);

        var replacement = Assert.Single(
            _invites.Items,
            i => i.RecoveryOfBookingId == original.Id && i.Status == InviteStatus.Pending);
        Assert.Equal(InviteStatus.Pending, replacement.Status);
        Assert.Equal([AppointmentTypeIds.DrugAndAlcoholTesting], replacement.RequiredAppointmentTypeIds);
        Assert.Equal(Invite.RequiredOptionCount, replacement.OfferedSlotIds.Count);
        Assert.Contains(_slot.Id, replacement.OfferedSlotIds);

        Assert.True(_audit.Contains(AuditAction.BookingCancelled));
        Assert.True(_audit.Contains(AuditAction.RecoveryInviteCreated));
        Assert.Equal(2, _email.Sent.Count);
    }

    [Fact]
    public async Task WithoutReplacementSlotsTheRecoveryStaysAvailable()
    {
        var candidate = BookACandidate("Amara Novak", "a.novak@mail.com");
        var original = _bookings.Items.Single();
        var recoverySlot = _slots.Items[1];
        var recovery = AddActiveRecoveryOn(candidate, original, recoverySlot);

        foreach (var slot in _slots.Items.Where(s => s.Id != recoverySlot.Id))
        {
            var capacity = slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);
            var remainingCapacity = capacity.RemainingCapacity;
            for (var i = 0; i < remainingCapacity; i++)
            {
                capacity.Decrement();
            }
        }

        var result = await Handler.HandleAsync(
            new CancelConfirmedSlotCommand(Coordinator, recoverySlot.Id, true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.BookingsVoided);
        Assert.Equal(0, result.Value.CandidatesReinvited);
        Assert.Equal(BookingStatus.Cancelled, recovery.Status);
        Assert.Equal(BookingStatus.Active, original.Status);
        Assert.Equal(CandidateStatus.Booked, candidate.Status);
        Assert.DoesNotContain(
            _invites.Items,
            i => i.RecoveryOfBookingId == original.Id && i.Status == InviteStatus.Pending);
        Assert.Single(_email.Sent);
    }

    private Booking AddActiveRecoveryOn(
        Candidate candidate, Booking original, ConfirmedSlot recoverySlot)
    {
        _appointments.Items
            .Single(a => a.BookingId == original.Id
                && a.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting)
            .TransitionTo(BookingAppointmentStatus.NoShow, Coordinator, _clock.UtcNow, false, true);

        var recoveryInviteId = Guid.NewGuid();
        var issued = _tokens.Issue(recoveryInviteId);
        var recoveryInvite = Invite.CreateRecovery(
            recoveryInviteId, candidate.Id, original.Id, issued.TokenHash,
            _clock.UtcNow.AddDays(4),
            [recoverySlot.Id, _slots.Items[2].Id, _slots.Items[3].Id],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        _invites.Add(recoveryInvite);

        var recoveryId = Guid.NewGuid();
        var manage = _tokens.Issue(recoveryId);
        var recovery = Booking.CreateRecovery(
            recoveryId, recoveryInvite, original, recoverySlot.Id,
            manage.TokenHash, _clock.UtcNow);
        _bookings.Add(recovery);
        recoveryInvite.MarkUsed();

        recoverySlot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.DrugAndAlcoholTesting));

        return recovery;
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
        _slots.Add(slot);
        return slot;
    }
}
`````

## tests/EventBooking.Application.Tests/Slots/CombinedManagerAuthorizationTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Slots/CombinedManagerAuthorizationTests.cs","encoding":"utf8","sha256":"439435d19539465470232ad6a7214806b0ba00e87b6ec2847a174f884676e7f5","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Slots;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Tests.Slots;

public class CombinedManagerAuthorizationTests
{
    [Fact]
    public async Task ACoordinatorManagerCanProposeUsingTheManagerCapability()
    {
        var staffUserId = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            staffUserId,
            [Role.Coordinator, Role.Manager],
            AppointmentTypeIds.DrugAndAlcoholTesting));
        var proposals = new InMemorySlotProposalRepository();
        var handler = new ProposeSlotHandler(
            proposals,
            new StaffAccessAuthorizer(profiles),
            new FakeUnitOfWork(),
            new RecordingAuditLogger(),
            new FakeClock());

        var result = await handler.HandleAsync(
            new ProposeSlotCommand(staffUserId, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(proposals.Items);
    }

    [Fact]
    public async Task AppointmentStaffWithoutManagerCannotPropose()
    {
        var staffUserId = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            staffUserId,
            [Role.AppointmentStaff],
            AppointmentTypeIds.DrugAndAlcoholTesting));
        var proposals = new InMemorySlotProposalRepository();
        var handler = new ProposeSlotHandler(
            proposals,
            new StaffAccessAuthorizer(profiles),
            new FakeUnitOfWork(),
            new RecordingAuditLogger(),
            new FakeClock());

        var result = await handler.HandleAsync(
            new ProposeSlotCommand(staffUserId, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(proposals.Items);
    }
}
`````

## tests/EventBooking.Application.Tests/Slots/ConfirmedSlotImportParserTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Slots/ConfirmedSlotImportParserTests.cs","encoding":"utf8","sha256":"97ab3fb4ebc45c65f4ff53e0c4d89414fc2ac35b8665c63b4e41d2768f54ff60","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Slots;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Tests.Slots;

public class ConfirmedSlotImportParserTests
{
    private const string Header = "date,startTime,DAT,MED,UNI";

    [Fact]
    public void AGoodFileParsesEveryRow()
    {
        var result = ConfirmedSlotImportParser.Parse(
            $"""
             {Header}
             2026-09-10,09:00,10,6,8
             2026-09-11,13:00,4,4,4
             """);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Rows.Count);

        var first = result.Rows[0];
        Assert.Equal(2, first.LineNumber);
        Assert.Equal(new DateOnly(2026, 9, 10), first.Window.Date);
        Assert.Equal(new TimeOnly(9, 0), first.Window.StartTime);
        Assert.Equal(10, first.HeadcountsByAppointmentType[AppointmentTypeIds.DrugAndAlcoholTesting]);
        Assert.Equal(6, first.HeadcountsByAppointmentType[AppointmentTypeIds.MedicalCheckUp]);
        Assert.Equal(8, first.HeadcountsByAppointmentType[AppointmentTypeIds.UniformFitting]);
    }

    [Fact]
    public void BlankLinesAreTolerated()
    {
        var result = ConfirmedSlotImportParser.Parse($"{Header}\n\n2026-09-10,09:00,10,6,8\n\n");

        Assert.Empty(result.Errors);
        Assert.Single(result.Rows);
    }

    [Fact]
    public void AnEmptyFileIsRejected()
    {
        var result = ConfirmedSlotImportParser.Parse("");

        Assert.Empty(result.Rows);
        Assert.Equal("The file is empty.", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void AWrongHeaderIsRejected()
    {
        var result = ConfirmedSlotImportParser.Parse("date,startTime,types\n2026-09-10,09:00,DAT");

        Assert.Contains("header line must read exactly", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void AFileOfOnlyAHeaderIsRejected()
    {
        var result = ConfirmedSlotImportParser.Parse(Header);

        Assert.Equal("The file contains no slot rows.", Assert.Single(result.Errors).Message);
    }

    [Theory]
    [InlineData("2026-09-10,09:00,10,6")]
    [InlineData("2026-09-10,09:00,10,6,8,1")]
    public void AWrongFieldCountIsRejected(string line)
    {
        var result = ConfirmedSlotImportParser.Parse($"{Header}\n{line}");

        Assert.Contains("Expected 5 comma-separated fields", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void AnUnparseableDateIsRejected()
    {
        var result = ConfirmedSlotImportParser.Parse($"{Header}\n10-09-2026,09:00,10,6,8");

        Assert.Equal(2, Assert.Single(result.Errors).LineNumber);
        Assert.Contains("date", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void AnUnparseableStartTimeIsRejected()
    {
        var result = ConfirmedSlotImportParser.Parse($"{Header}\n2026-09-10,9am,10,6,8");

        Assert.Contains("startTime", Assert.Single(result.Errors).Message);
    }

    [Theory]
    [InlineData("0,6,8")]
    [InlineData("-1,6,8")]
    [InlineData("abc,6,8")]
    [InlineData(",6,8")]
    public void ANonPositiveOrUnparseableHeadcountIsRejected(string counts)
    {
        var result = ConfirmedSlotImportParser.Parse($"{Header}\n2026-09-10,09:00,{counts}");

        Assert.Equal(2, Assert.Single(result.Errors).LineNumber);
        Assert.Contains("DAT", result.Errors[0].Message);
    }

    [Fact]
    public void ADuplicateWindowWithinTheFileIsRejected()
    {
        var result = ConfirmedSlotImportParser.Parse(
            $"""
             {Header}
             2026-09-10,09:00,10,6,8
             2026-09-10,09:00,1,1,1
             """);

        var error = Assert.Single(result.Errors);
        Assert.Equal(3, error.LineNumber);
        Assert.Contains("line 2", error.Message);
    }

    [Fact]
    public void MoreThanTwoHundredRowsIsRejectedWithoutRowErrors()
    {
        var rows = Enumerable.Range(0, 201)
            .Select(i => $"2026-01-{(i % 27) + 1:D2},{(i % 20) + 1:D2}:00,1,1,1");
        var result = ConfirmedSlotImportParser.Parse($"{Header}\n{string.Join('\n', rows)}");

        Assert.Empty(result.Rows);
        Assert.Contains("at most 200 rows", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void ALateStartTimeIsARowErrorNotAnException()
    {
        var result = ConfirmedSlotImportParser.Parse(
            $"{Header}\n2026-09-10,21:00,10,6,8",
            new DateOnly(2026, 9, 3));

        Assert.Empty(result.Rows);
        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.LineNumber);
        Assert.Contains("4-hour window", error.Message);
    }

    [Fact]
    public void APastDatedRowIsRejected()
    {
        var result = ConfirmedSlotImportParser.Parse(
            $"{Header}\n2026-09-01,09:00,10,6,8",
            new DateOnly(2026, 9, 3));

        Assert.Empty(result.Rows);
        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.LineNumber);
        Assert.Contains("future", error.Message);
    }

    [Fact]
    public void AFutureDatedRowParsesWhenTodayIsSupplied()
    {
        var result = ConfirmedSlotImportParser.Parse(
            $"{Header}\n2026-09-10,09:00,10,6,8",
            new DateOnly(2026, 9, 3));

        Assert.Empty(result.Errors);
        Assert.Single(result.Rows);
    }
}
`````

## tests/EventBooking.Application.Tests/Slots/GetManagerSlotBoardHandlerTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Slots/GetManagerSlotBoardHandlerTests.cs","encoding":"utf8","sha256":"ca7b87cb3066e174e9dcbcaf8670854b75a71d75e44d79e020692c84cdea4cc3","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Slots;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Slots;

public class GetManagerSlotBoardHandlerTests
{
    private static readonly Guid DrugAndAlcoholManager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid MedicalManager = Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid UniformManager = Guid.Parse("c0000003-0000-0000-0000-000000000003");

    private readonly InMemorySlotProposalRepository _proposals = new();
    private readonly InMemoryConfirmedSlotRepository _confirmedSlots = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    private GetManagerSlotBoardHandler Handler =>
        new(_proposals, _confirmedSlots, _roles, _clock);

    public GetManagerSlotBoardHandlerTests()
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
        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            DrugAndAlcoholManager);
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        _proposals.Add(proposal);

        var board = (await Handler.HandleAsync(
            new GetManagerSlotBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

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
        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, 12), new TimeOnly(13, 0)),
            UniformManager);
        proposal.Accept(AppointmentTypeIds.UniformFitting, UniformManager, 8);
        _proposals.Add(proposal);

        var board = (await Handler.HandleAsync(
            new GetManagerSlotBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

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
            _proposals.Add(SlotProposal.Create(
                Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0)),
                UniformManager));
        }

        var board = (await Handler.HandleAsync(
            new GetManagerSlotBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        Assert.Equal(
            new[] { new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 14) },
            board.OpenProposals.Select(p => p.Date));
    }

    [Fact]
    public async Task ConfirmedSlotsShowOnlyMyOwnHeadcountAndRemainder()
    {
        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());
        slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        _confirmedSlots.Add(slot);

        var board = (await Handler.HandleAsync(
            new GetManagerSlotBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        var view = Assert.Single(board.ConfirmedSlots);
        Assert.Equal(10, view.MyHeadcount);
        Assert.Equal(8, view.MyRemainingCapacity);
    }

    [Fact]
    public async Task CancelledAndPastSlotsAreNotOnTheBoard()
    {
        var cancelled = ConfirmedSlot.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());
        cancelled.Cancel();
        _confirmedSlots.Add(cancelled);
        _confirmedSlots.Add(ConfirmedSlot.CreateFrom(
            Guid.NewGuid(), FullyAcceptedProposal(new DateOnly(2026, 8, 30))));

        var board = (await Handler.HandleAsync(
            new GetManagerSlotBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        Assert.Empty(board.ConfirmedSlots);
    }

    [Fact]
    public async Task ANonManagerIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new GetManagerSlotBoardQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    private static SlotProposal FullyAcceptedProposal(DateOnly? date = null)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(date ?? new DateOnly(2026, 9, 8), new TimeOnly(9, 0)),
            DrugAndAlcoholManager);
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, UniformManager, 8);
        return proposal;
    }
}
`````

## tests/EventBooking.Application.Tests/Slots/ImportConfirmedSlotsHandlerTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Slots/ImportConfirmedSlotsHandlerTests.cs","encoding":"utf8","sha256":"ceb63f221b96cb7fc4515e3b403345781acd0c5d55d1fb8da6e55ca7b9dc2e55","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Common;
using EventBooking.Application.Slots;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;

namespace EventBooking.Application.Tests.Slots;

public class ImportConfirmedSlotsHandlerTests
{
    private const string Header = "date,startTime,DAT,MED,UNI";
    private static readonly Guid Admin = Guid.Parse("c0000008-0000-0000-0000-000000000008");
    private static readonly Guid Manager = Guid.Parse("c0000001-0000-0000-0000-000000000001");

    private readonly InMemoryConfirmedSlotRepository _confirmedSlots = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();

    private ImportConfirmedSlotsHandler Handler => new(_confirmedSlots, _roles, _unitOfWork, _audit, new FakeClock());

    public ImportConfirmedSlotsHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));
        _roles.Add(StaffAccessProfile.Create(
            Manager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
    }

    private Task<Result<ConfirmedSlotImportOutcome>> Import(string csv, Guid? actor = null) =>
        Handler.HandleAsync(new ImportConfirmedSlotsCommand(actor ?? Admin, csv), CancellationToken.None);

    [Fact]
    public async Task AGoodFileCreatesOneConfirmedSlotPerRowWithNoProposal()
    {
        var result = await Import(
            $"""
             {Header}
             2026-09-10,09:00,10,6,8
             2026-09-11,13:00,4,4,4
             """);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Accepted);
        Assert.Equal(2, result.Value.ImportedCount);
        Assert.Empty(result.Value.Errors);

        Assert.Equal(2, _confirmedSlots.Items.Count);
        Assert.All(_confirmedSlots.Items, s => Assert.Null(s.ProposalId));
        var first = _confirmedSlots.Items.Single(s => s.Window.Date == new DateOnly(2026, 9, 10));
        Assert.Equal(10, first.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task EveryImportedSlotWritesExactlyOneSlotImportedEntry()
    {
        await Import(
            $"""
             {Header}
             2026-09-10,09:00,10,6,8
             2026-09-11,13:00,4,4,4
             """);

        Assert.Equal(2, _audit.Entries.Count(e => e.Action == AuditAction.SlotImported));
        Assert.All(_audit.Entries, e => Assert.Equal(Admin.ToString(), e.ActorId));
        Assert.Contains(_audit.Entries, e => e.Details != null && e.Details.Contains("line 2"));
    }

    [Fact]
    public async Task ARejectedFileCreatesNothingAndAuditsNothing()
    {
        var result = await Import($"{Header}\n2026-09-10,09:00,0,6,8");

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Accepted);
        Assert.Single(result.Value.Errors);
        Assert.Empty(_confirmedSlots.Items);
        Assert.Empty(_audit.Entries);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AManagerIsForbidden()
    {
        var result = await Import($"{Header}\n2026-09-10,09:00,10,6,8", Manager);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Empty(_confirmedSlots.Items);
    }
}
`````

## tests/EventBooking.Application.Tests/Slots/ManagerSlotBoardHeadcountRevisionTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Slots/ManagerSlotBoardHeadcountRevisionTests.cs","encoding":"utf8","sha256":"93447ed79ec3fdcc6adb16e5afc4a6c2c5f0514f0a4d45cfef0eba05d0bdc88b","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Slots;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Slots;

public class ManagerSlotBoardHeadcountRevisionTests
{
    private static readonly Guid CurrentManager =
        Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid FormerManager =
        Guid.Parse("c0000011-0000-0000-0000-000000000011");

    private readonly InMemorySlotProposalRepository _proposals = new();
    private readonly InMemoryConfirmedSlotRepository _confirmedSlots = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeClock _clock =
        new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    private GetManagerSlotBoardHandler Handler =>
        new(_proposals, _confirmedSlots, _roles, _clock);

    public ManagerSlotBoardHeadcountRevisionTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            CurrentManager,
            Role.Manager,
            AppointmentTypeIds.DrugAndAlcoholTesting));
    }

    private SlotProposal AddProposal()
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
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
            new GetManagerSlotBoardQuery(CurrentManager),
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
            new GetManagerSlotBoardQuery(CurrentManager),
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

## tests/EventBooking.Application.Tests/Slots/ProposeSlotHandlerTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Slots/ProposeSlotHandlerTests.cs","encoding":"utf8","sha256":"29770e05189501fd0a6b4d2b7ab293a7c8d160beda656afa060fea71adff73de","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Slots;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Slots;

public class ProposeSlotHandlerTests
{
    private static readonly Guid Manager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemorySlotProposalRepository _proposals = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    private ProposeSlotHandler Handler => new(_proposals, _roles, _unitOfWork, _audit, _clock);

    public ProposeSlotHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            Manager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
    }

    [Fact]
    public async Task AManagerCanProposeAFutureWindow()
    {
        var result = await Handler.HandleAsync(
            new ProposeSlotCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var proposal = Assert.Single(_proposals.Items);
        Assert.Equal(result.Value, proposal.Id);
        Assert.Equal(SlotProposalStatus.Open, proposal.Status);
        Assert.Equal(new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)), proposal.Window);
        Assert.Equal(Manager, proposal.CreatedByManagerUserId);
        Assert.Empty(proposal.Acceptances);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ProposingWritesAnAuditEntry()
    {
        await Handler.HandleAsync(
            new ProposeSlotCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AuditEntityTypes.SlotProposal, entry.EntityType);
        Assert.Equal(AuditAction.ProposalCreated, entry.Action);
        Assert.Equal(ActorType.Staff, entry.ActorType);
        Assert.Equal(Manager.ToString(), entry.ActorId);
    }

    [Fact]
    public async Task ACoordinatorCannotProposeASlot()
    {
        var result = await Handler.HandleAsync(
            new ProposeSlotCommand(Coordinator, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal("This staff profile cannot perform this operation.", result.Error.Message);
        Assert.Empty(_proposals.Items);
    }

    [Fact]
    public async Task AnUnknownUserCannotProposeASlot()
    {
        var result = await Handler.HandleAsync(
            new ProposeSlotCommand(Guid.NewGuid(), new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
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
            new ProposeSlotCommand(Manager, new DateOnly(year, month, day), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal("A slot must be proposed for a future date.", result.Error.Message);
    }

    [Fact]
    public async Task AWindowThatWouldRunPastMidnightIsRejectedByTheDomain()
    {
        var result = await Handler.HandleAsync(
            new ProposeSlotCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(22, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal(
            "startTime must leave room for the full 4-hour window on the same day.",
            result.Error.Message);
    }

    [Fact]
    public async Task ASecondOpenProposalForTheSameWindowIsAConflict()
    {
        var command = new ProposeSlotCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(9, 0));
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
            new ProposeSlotCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        var result = await Handler.HandleAsync(
            new ProposeSlotCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(13, 0)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _proposals.Items.Count);
    }
}
`````

## tests/EventBooking.Application.Tests/Slots/SharedSlotAuthorizationTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Slots/SharedSlotAuthorizationTests.cs","encoding":"utf8","sha256":"a73b7e5a988e73f734642f88cc87930735072d9b0272217b4cc64399ef640737","parts":1,"part":1} -->

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
