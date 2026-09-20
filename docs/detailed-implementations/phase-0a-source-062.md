# 00a — Port source 62 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## tests/EventBooking.Application.Tests/Candidates/CandidateCsvParserTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Candidates/CandidateCsvParserTests.cs","encoding":"utf8","sha256":"590a26bda86c0af10d1fa118b418054326179fe0fa6435d1b162bc4b2d54f142","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Candidates;

namespace EventBooking.Application.Tests.Candidates;

public class CandidateCsvParserTests
{
    private const string Header = "name,email,employee_group";

    [Fact]
    public void AWellFormedFileParsesEveryRow()
    {
        var result = CandidateCsvParser.Parse(
            $"""
             {Header}
             Amara Novak,a.novak@mail.com,CABIN_CREW
             B. Chen,b.chen@mail.com,ENGINEERING
             """);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Rows.Count);

        Assert.Equal(2, result.Rows[0].LineNumber);
        Assert.Equal("Amara Novak", result.Rows[0].Name);
        Assert.Equal("a.novak@mail.com", result.Rows[0].Email);
        Assert.Equal("CABIN_CREW", result.Rows[0].EmployeeGroupCode);

        Assert.Equal(3, result.Rows[1].LineNumber);
        Assert.Equal("ENGINEERING", result.Rows[1].EmployeeGroupCode);
    }

    [Fact]
    public void GroupCodesAreCaseInsensitiveAndWhitespaceIsTrimmed()
    {
        var result = CandidateCsvParser.Parse($"{Header}\n  Amara Novak , a.novak@mail.com , pilots ");

        Assert.Empty(result.Errors);
        var row = Assert.Single(result.Rows);
        Assert.Equal("Amara Novak", row.Name);
        Assert.Equal("a.novak@mail.com", row.Email);
        Assert.Equal("PILOTS", row.EmployeeGroupCode);
    }

    [Fact]
    public void BlankLinesAreSkippedWithoutDisturbingLineNumbers()
    {
        var result = CandidateCsvParser.Parse($"{Header}\n\nAmara Novak,a.novak@mail.com,PILOTS\n\n");

        Assert.Empty(result.Errors);
        Assert.Equal(3, Assert.Single(result.Rows).LineNumber);
    }

    [Fact]
    public void AnEmptyFileIsAnError()
    {
        var result = CandidateCsvParser.Parse("");

        Assert.Empty(result.Rows);
        var error = Assert.Single(result.Errors);
        Assert.Equal(0, error.LineNumber);
        Assert.Equal("The file is empty.", error.Message);
    }

    [Fact]
    public void TheWrongHeaderIsAnError()
    {
        var result = CandidateCsvParser.Parse("name,email,types\nAmara Novak,a.novak@mail.com,PILOTS");

        var error = Assert.Single(result.Errors);
        Assert.Equal(1, error.LineNumber);
        Assert.Equal(
            "The header line must read exactly: name,email,employee_group",
            error.Message);
    }

    [Fact]
    public void TheOldRequirementHeaderIsAnError()
    {
        var result = CandidateCsvParser.Parse(
            "name,email,appointment_types\nAmara Novak,a.novak@mail.com,DAT;UNI");

        var error = Assert.Single(result.Errors);
        Assert.Equal(1, error.LineNumber);
    }

    [Fact]
    public void AFileWithOnlyAHeaderIsAnError()
    {
        var result = CandidateCsvParser.Parse(Header);

        var error = Assert.Single(result.Errors);
        Assert.Equal("The file contains no candidate rows.", error.Message);
    }

    [Theory]
    [InlineData("Amara Novak,a.novak@mail.com")]
    [InlineData("Amara Novak,a.novak@mail.com,PILOTS,extra")]
    public void TheWrongNumberOfFieldsIsARowError(string line)
    {
        var result = CandidateCsvParser.Parse($"{Header}\n{line}");

        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.LineNumber);
        Assert.Equal("Expected 3 comma-separated fields: name, email, employee_group.", error.Message);
    }

    [Fact]
    public void ABlankNameIsARowError()
    {
        var result = CandidateCsvParser.Parse($"{Header}\n ,a.novak@mail.com,PILOTS");

        Assert.Equal("Name is required.", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void ABlankEmailIsARowError()
    {
        var result = CandidateCsvParser.Parse($"{Header}\nAmara Novak, ,PILOTS");

        Assert.Equal("Email is required.", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void NoEmployeeGroupIsARowError()
    {
        var result = CandidateCsvParser.Parse($"{Header}\nAmara Novak,a.novak@mail.com, ");

        Assert.Equal("Employee group is required.", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void AnUnknownCodePassesStructuralValidationForTheImportHandler()
    {
        var result = CandidateCsvParser.Parse($"{Header}\nAmara Novak,a.novak@mail.com,UNKNOWN_GROUP");

        Assert.Empty(result.Errors);
        Assert.Equal("UNKNOWN_GROUP", Assert.Single(result.Rows).EmployeeGroupCode);
    }

    [Fact]
    public void ARepeatedEmailInTheFileIsARowErrorOnTheSecondOccurrence()
    {
        var result = CandidateCsvParser.Parse(
            $"""
             {Header}
             Amara Novak,a.novak@mail.com,PILOTS
             Someone Else,A.NOVAK@mail.com,ENGINEERING
             """);

        var error = Assert.Single(result.Errors);
        Assert.Equal(3, error.LineNumber);
        Assert.Equal("a.novak@mail.com appears more than once in this file.", error.Message);
    }

    [Fact]
    public void EveryBadRowIsReportedNotJustTheFirst()
    {
        var result = CandidateCsvParser.Parse(
            $"""
             {Header}
             ,a.novak@mail.com,PILOTS
             B. Chen,b.chen@mail.com,
             C. Diallo,c.diallo@mail.com,ENGINEERING
             """);

        Assert.Equal(2, result.Errors.Count);
        Assert.Equal([2, 3], result.Errors.Select(e => e.LineNumber));
        Assert.Single(result.Rows);
    }

    [Fact]
    public void WindowsLineEndingsAreHandled()
    {
        var result = CandidateCsvParser.Parse($"{Header}\r\nAmara Novak,a.novak@mail.com,PILOTS\r\n");

        Assert.Empty(result.Errors);
        Assert.Single(result.Rows);
    }
}
`````

## tests/EventBooking.Application.Tests/Candidates/CandidateEmployeeGroupFlowTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Candidates/CandidateEmployeeGroupFlowTests.cs","encoding":"utf8","sha256":"93c145145752d2550a739de1738cd72ad2f68c775efe3c5302a1d97dfa449162","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Candidates;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.EmployeeGroups;

namespace EventBooking.Application.Tests.Candidates;

/// <summary>Verifies Candidate application inputs resolve and derive Employee Group mappings.</summary>
public sealed class CandidateEmployeeGroupFlowTests
{
    private static readonly Guid Coordinator = Guid.NewGuid();
    private readonly InMemoryCandidateRepository _candidates = new();
    private readonly InMemoryEmployeeGroupRepository _groups = new();
    private readonly InMemoryStaffAccessProfileRepository _profiles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    /// <summary>Initializes one Coordinator and the approved reference groups.</summary>
    public CandidateEmployeeGroupFlowTests()
    {
        _profiles.Add(StaffAccessProfile.Create(Coordinator, [Role.Coordinator], null));
        _groups.Items.Add(EmployeeGroup.Define(
            EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
    }

    /// <summary>Create stores the group and derives requirements without requirement input.</summary>
    [Fact]
    public async Task CreateDerivesThePersistedGroupMapping()
    {
        var handler = new SaveCandidateHandler(
            _candidates,
            _groups,
            new InMemoryInviteRepository(),
            new InMemoryBookingRepository(),
            new StaffAccessAuthorizer(_profiles),
            new RecordingAuditLogger(),
            _unitOfWork);

        var result = await handler.CreateAsync(
            new CreateCandidateCommand(
                Coordinator, "Amara Novak", "amara@example.com", EmployeeGroupIds.Pilots),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var candidate = Assert.Single(_candidates.Items);
        Assert.Equal(EmployeeGroupIds.Pilots, candidate.EmployeeGroupId);
        Assert.Equal(
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            candidate.RequiredAppointmentTypeIds);
    }

    /// <summary>Unknown and absent groups return stable validation without saving.</summary>
    [Theory]
    [InlineData(null, "employee_group_required")]
    public async Task InvalidGroupDoesNotCreateCandidate(Guid? groupId, string code)
    {
        var handler = new SaveCandidateHandler(
            _candidates,
            _groups,
            new InMemoryInviteRepository(),
            new InMemoryBookingRepository(),
            new StaffAccessAuthorizer(_profiles),
            new RecordingAuditLogger(),
            _unitOfWork);

        var result = await handler.CreateAsync(
            new CreateCandidateCommand(Coordinator, "Amara", "amara@example.com", groupId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(code, result.Error.Code);
        Assert.Empty(_candidates.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    /// <summary>The new CSV contract carries one case-insensitive Employee Group code.</summary>
    [Fact]
    public void CsvParsesEmployeeGroupRatherThanAppointmentTypes()
    {
        var parsed = CandidateCsvParser.Parse(
            "name,email,employee_group\nAmara Novak,amara@example.com, pilots ");

        Assert.Empty(parsed.Errors);
        Assert.Equal("PILOTS", Assert.Single(parsed.Rows).EmployeeGroupCode);
        var old = CandidateCsvParser.Parse(
            "name,email,appointment_types\nAmara Novak,amara@example.com,DAT;UNI");
        Assert.Equal(1, Assert.Single(old.Errors).LineNumber);
    }
}
`````

## tests/EventBooking.Application.Tests/Candidates/CandidateReadinessCalculatorTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Candidates/CandidateReadinessCalculatorTests.cs","encoding":"utf8","sha256":"4d889dbc99b6ec0a1503736705d7791df14d3a3f24f81e5f2d4dffc24c120191","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Candidates;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Tests.Candidates;

/// <summary>Verifies readiness precedence and latest-attempt semantics.</summary>
public sealed class CandidateReadinessCalculatorTests
{
    private readonly CandidateReadinessCalculator _calculator = new();

    /// <summary>A later Completed recovery attempt supersedes the original NoShow.</summary>
    [Fact]
    public void RecoveryCompletionMakesCandidateReady()
    {
        var candidateId = Guid.NewGuid();
        var original = Guid.NewGuid();
        var recovery = Guid.NewGuid();
        var type = AppointmentTypeIds.MedicalCheckUp;
        var snapshot = new CandidateReadinessSnapshot(
            candidateId,
            Guid.NewGuid(),
            [type],
            original,
            [
                Attempt(type, original, BookingStatus.Active, BookingAppointmentStatus.NoShow, 1),
                Attempt(type, recovery, BookingStatus.Concluded, BookingAppointmentStatus.Completed, 2),
            ]);

        var actual = _calculator.Calculate(snapshot);

        Assert.Equal(CandidateReadinessCode.Ready, actual.Code);
        Assert.Empty(actual.OutstandingAppointmentTypes);
    }

    /// <summary>Any non-cancelled Completed attempt satisfies the type despite a later NoShow.</summary>
    [Fact]
    public void AnyCompletedAttemptControlsOutcome()
    {
        var candidateId = Guid.NewGuid();
        var original = Guid.NewGuid();
        var recovery = Guid.NewGuid();
        var type = AppointmentTypeIds.UniformFitting;
        var snapshot = new CandidateReadinessSnapshot(
            candidateId,
            Guid.NewGuid(),
            [type],
            original,
            [
                Attempt(type, original, BookingStatus.Active, BookingAppointmentStatus.Completed, 1),
                Attempt(type, recovery, BookingStatus.Concluded, BookingAppointmentStatus.NoShow, 2),
            ]);

        var actual = _calculator.Calculate(snapshot);

        Assert.Equal(CandidateReadinessCode.Ready, actual.Code);
        Assert.Empty(actual.OutstandingAppointmentTypes);
    }

    /// <summary>A latest NoShow with no completion is outstanding and recoverable.</summary>
    [Fact]
    public void LatestNoShowIsRecoverable()
    {
        var original = Guid.NewGuid();
        var type = AppointmentTypeIds.MedicalCheckUp;
        var actual = _calculator.Calculate(new CandidateReadinessSnapshot(
            Guid.NewGuid(), Guid.NewGuid(), [type], original,
            [Attempt(type, original, BookingStatus.Active, BookingAppointmentStatus.NoShow, 1)]));

        Assert.Equal(CandidateReadinessCode.AppointmentsOutstanding, actual.Code);
        var outstanding = Assert.Single(actual.OutstandingAppointmentTypes);
        Assert.Equal("MED", outstanding.Code);
        Assert.True(outstanding.IsRecoverable);
    }

    /// <summary>Unassigned reconciliation state wins over missing Booking state.</summary>
    [Fact]
    public void UnassignedGroupHasHighestFailurePrecedence()
    {
        var actual = _calculator.Calculate(new CandidateReadinessSnapshot(
            Guid.NewGuid(), null, [], null, []));

        Assert.Equal(CandidateReadinessCode.EmployeeGroupUnassigned, actual.Code);
    }

    private static CandidateReadinessAttempt Attempt(
        Guid typeId,
        Guid bookingId,
        BookingStatus bookingStatus,
        BookingAppointmentStatus status,
        int day) => new(
            Guid.NewGuid(), typeId, status, bookingId, bookingStatus,
            DateTimeOffset.Parse($"2026-09-{day:00}T09:00:00Z"));
}
`````

## tests/EventBooking.Application.Tests/Candidates/DeleteCandidateHandlerTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Candidates/DeleteCandidateHandlerTests.cs","encoding":"utf8","sha256":"2af0600c0b20a1bbb3f1e646627df5da02c689606636197eb698babe5cb9575a","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Candidates;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Candidates;

public class DeleteCandidateHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemoryCandidateRepository _candidates = new();
    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryBookingRepository _bookings = new();
    private readonly InMemoryConfirmedSlotRepository _slots = new();
    private readonly InMemoryBookingAppointmentRepository _appointments;

    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly Candidate _candidate;

    private DeleteCandidateHandler Handler => new(
        _candidates, _invites, _bookings, _slots, _roles,
        new BookingCanceller(_appointments, new InMemorySlotCapacityRepository(_slots), _audit),
        _audit,
        _unitOfWork);

    public DeleteCandidateHandlerTests()
    {
        _appointments = new InMemoryBookingAppointmentRepository(_bookings);
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com",
            EmployeeGroup.Define(
                EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
        _candidates.Add(_candidate);
    }

    [Fact]
    public async Task ACandidateWithNothingOutstandingIsDeletedOutright()
    {
        var result = await Handler.HandleAsync(
            new DeleteCandidateCommand(Coordinator, _candidate.Id, false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_candidates.Items);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnUnconfirmedDeleteOfACandidateWithABookingIsRefusedWithAWarning()
    {
        GiveTheCandidateABooking();

        var result = await Handler.HandleAsync(
            new DeleteCandidateCommand(Coordinator, _candidate.Id, false), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(
            "Deleting this candidate will cancel 1 booking and 1 pending invite, and free the capacity they hold. Confirm to proceed.",
            result.Error.Message);
        Assert.Single(_candidates.Items);
    }

    [Fact]
    public async Task AConfirmedDeleteVoidsTheBookingAndGivesTheCapacityBack()
    {
        var slot = GiveTheCandidateABooking();
        Assert.Equal(9, slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);

        var result = await Handler.HandleAsync(
            new DeleteCandidateCommand(Coordinator, _candidate.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_candidates.Items);
        Assert.Equal(10, slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(8, slot.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
        Assert.Equal(6, slot.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(BookingStatus.Cancelled, _bookings.Items.Single().Status);
        Assert.Equal(InviteStatus.Superseded, _invites.Items.Single().Status);
        Assert.True(_audit.Contains(AuditAction.BookingCancelled));
        Assert.True(_audit.Contains(AuditAction.CapacityIncremented));
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AConfirmedDeleteCascadesToTheActiveRecoveryBookingAndAuditsTheDeletion()
    {
        var slot = GiveTheCandidateABooking();
        var recoverySlot = GiveTheCandidateARecoveryBooking();
        Assert.Equal(9, recoverySlot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);

        var result = await Handler.HandleAsync(
            new DeleteCandidateCommand(Coordinator, _candidate.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_candidates.Items);
        Assert.Equal(2, _bookings.Items.Count);
        Assert.All(_bookings.Items, booking => Assert.Equal(BookingStatus.Cancelled, booking.Status));
        Assert.Equal(10, recoverySlot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(10, slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.True(_audit.Contains(AuditAction.CandidateDeleted));
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AnUnknownCandidateIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new DeleteCandidateCommand(Coordinator, Guid.NewGuid(), true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task ANonCoordinatorIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new DeleteCandidateCommand(Guid.NewGuid(), _candidate.Id, true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    private ConfirmedSlot GiveTheCandidateARecoveryBooking()
    {
        var original = _bookings.Items.Single();
        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, 11), new TimeOnly(9, 0)), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 10);
        var recoverySlot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);
        _slots.Add(recoverySlot);

        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(), _candidate.Id, original.Id, "recovery-hash", Now.AddDays(4),
            [recoverySlot.Id, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        _invites.Add(recoveryInvite);

        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, recoverySlot.Id, "recovery-manage-hash", Now);
        _bookings.Add(recovery);
        recoveryInvite.MarkUsed();

        recoverySlot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.DrugAndAlcoholTesting));

        return recoverySlot;
    }

    private ConfirmedSlot GiveTheCandidateABooking()
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);
        _slots.Add(slot);

        var invite = Invite.CreateInitial(
            Guid.NewGuid(), _candidate.Id, "hash", Now.AddDays(4),
            [slot.Id, Guid.NewGuid(), Guid.NewGuid()], _candidate.RequiredAppointmentTypeIds, 0);
        _invites.Add(invite);

        var booking = Booking.Create(Guid.NewGuid(), invite, slot.Id, "manage-hash", Now);
        _bookings.Add(booking);

        foreach (var typeId in _candidate.RequiredAppointmentTypeIds)
        {
            slot.CapacityFor(typeId).Decrement();
            _appointments.Add(BookingAppointment.Create(Guid.NewGuid(), booking.Id, typeId));
        }

        return slot;
    }
}
`````

## tests/EventBooking.Application.Tests/Candidates/EmployeeGroupLifecycleTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Candidates/EmployeeGroupLifecycleTests.cs","encoding":"utf8","sha256":"98fab90be0d8bf82d236bb2d0bce51a3f0aa5f676c9dc6e2a66035a3419c7c8c","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Candidates;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;

namespace EventBooking.Application.Tests.Candidates;

/// <summary>Verifies locked Employee Group changes follow every Candidate lifecycle rule.</summary>
public sealed class EmployeeGroupLifecycleTests
{
    private static readonly EmployeeGroup CabinCrew = EmployeeGroup.Define(
        EmployeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
            AppointmentTypeIds.UniformFitting]);
    private static readonly EmployeeGroup GroundTransport = EmployeeGroup.Define(
        EmployeeGroupIds.GroundTransportServices, "GROUND_TRANSPORT_SERVICES",
        "Ground Transport Services", true,
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
            AppointmentTypeIds.UniformFitting]);
    private static readonly EmployeeGroup Pilots = EmployeeGroup.Define(
        EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);

    /// <summary>A set-equivalent active-Booking update preserves all lifecycle state.</summary>
    [Fact]
    public async Task EquivalentGroupPreservesActiveBookingAndUsedInviteHistory()
    {
        var fixture = GivenCandidate(CabinCrew, CandidateStatus.Booked, activeBooking: true);

        var result = await fixture.Handler.UpdateAsync(
            new UpdateCandidateCommand(
                fixture.Coordinator, fixture.Candidate.Id, "Amara N.",
                "amara.n@example.com", GroundTransport.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(GroundTransport.Id, fixture.Candidate.EmployeeGroupId);
        Assert.Equal(CandidateStatus.Booked, fixture.Candidate.Status);
        Assert.Equal(InviteStatus.Used, fixture.Invite!.Status);
        Assert.Equal(
            ["candidate-locked", "initial-invite-locked", "original-booking-locked"],
            fixture.Operations.Events);
    }

    /// <summary>A set-changing active-Booking update fails before any Candidate mutation.</summary>
    [Fact]
    public async Task ChangedGroupConflictsWithActiveOriginalBooking()
    {
        var fixture = GivenCandidate(CabinCrew, CandidateStatus.Booked, activeBooking: true);

        var result = await fixture.Handler.UpdateAsync(
            new UpdateCandidateCommand(
                fixture.Coordinator, fixture.Candidate.Id, "Changed", "changed@example.com", Pilots.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("candidate_group_active_booking_conflict", result.Error.Code);
        Assert.Equal(CabinCrew.Id, fixture.Candidate.EmployeeGroupId);
        Assert.Equal("Amara", fixture.Candidate.Name);
    }

    /// <summary>A set-changing pending Invite is superseded without automatic replacement.</summary>
    [Fact]
    public async Task ChangedGroupSupersedesPendingInviteAndResetsStatus()
    {
        var fixture = GivenCandidate(CabinCrew, CandidateStatus.Invited, activeBooking: false);

        var result = await fixture.Handler.UpdateAsync(
            new UpdateCandidateCommand(
                fixture.Coordinator, fixture.Candidate.Id, "Amara", "amara@example.com", Pilots.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InviteStatus.Superseded, fixture.Invite!.Status);
        Assert.Equal(CandidateStatus.NotYetInvited, fixture.Candidate.Status);
        Assert.Equal(Pilots.RequiredAppointmentTypeIds, fixture.Candidate.RequiredAppointmentTypeIds);
        Assert.Equal(1, fixture.Audit.Entries.Count(entry =>
            entry.Action == EventBooking.Domain.Audit.AuditAction.EmployeeGroupChanged));
    }

    /// <summary>A repeated identical request performs no save and writes no audit row.</summary>
    [Fact]
    public async Task IdenticalUpdateIsANoOp()
    {
        var fixture = GivenCandidate(CabinCrew, CandidateStatus.NotYetInvited, activeBooking: false,
            includeInvite: false);

        var result = await fixture.Handler.UpdateAsync(
            new UpdateCandidateCommand(
                fixture.Coordinator, fixture.Candidate.Id, "Amara", "amara@example.com", CabinCrew.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
        Assert.Empty(fixture.Audit.Entries);
    }

    private static Fixture GivenCandidate(
        EmployeeGroup group,
        CandidateStatus status,
        bool activeBooking,
        bool includeInvite = true)
    {
        var coordinator = Guid.NewGuid();
        var operations = new TransactionOperationLog();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(coordinator, [Role.Coordinator], null));
        var groups = new InMemoryEmployeeGroupRepository();
        groups.Items.AddRange([CabinCrew, GroundTransport, Pilots]);
        var candidate = Candidate.Create(Guid.NewGuid(), "Amara", "amara@example.com", group);
        if (status == CandidateStatus.Invited || status == CandidateStatus.Booked)
        {
            candidate.MarkInvited();
        }
        if (status == CandidateStatus.Booked)
        {
            candidate.MarkBooked();
        }
        var candidates = new InMemoryCandidateRepository(operations);
        candidates.Add(candidate);
        var invites = new InMemoryInviteRepository(operations);
        Invite? invite = null;
        if (includeInvite)
        {
            var slotId = Guid.NewGuid();
            invite = Invite.CreateInitial(
                Guid.NewGuid(), candidate.Id, "token", DateTimeOffset.UtcNow.AddDays(1),
                [slotId, Guid.NewGuid(), Guid.NewGuid()], candidate.RequiredAppointmentTypeIds, 0);
            invites.Add(invite);
        }
        var bookings = new InMemoryBookingRepository(operations);
        if (activeBooking)
        {
            bookings.Add(Booking.Create(
                Guid.NewGuid(), invite!, invite!.OfferedSlotIds[0], "manage", DateTimeOffset.UtcNow));
            invite!.MarkUsed();
        }
        var audit = new RecordingAuditLogger();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new SaveCandidateHandler(
            candidates, groups, invites, bookings, new StaffAccessAuthorizer(profiles), audit, unitOfWork);
        return new Fixture(handler, candidate, invite, coordinator, operations, audit, unitOfWork);
    }

    private sealed record Fixture(
        SaveCandidateHandler Handler,
        Candidate Candidate,
        Invite? Invite,
        Guid Coordinator,
        TransactionOperationLog Operations,
        RecordingAuditLogger Audit,
        FakeUnitOfWork UnitOfWork);
}
`````

## tests/EventBooking.Application.Tests/Candidates/GetCandidateBookingsHandlerTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Candidates/GetCandidateBookingsHandlerTests.cs","encoding":"utf8","sha256":"b85791c0774054d553ef282d15a8f295cada52bc99fffcf971cd29bf21c9bbaa","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Candidates;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Tests.Candidates;

/// <summary>Verifies staff authorization and shape of the candidate active-booking listing.</summary>
public sealed class GetCandidateBookingsHandlerTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly Guid Manager = Guid.Parse("c0000007-0000-0000-0000-000000000007");

    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly InMemoryCandidateBookingQueries _queries = new();

    private GetCandidateBookingsHandler Handler =>
        new(new StaffAccessAuthorizer(_roles), _queries);

    public GetCandidateBookingsHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _roles.Add(StaffAccessProfile.Create(
            Manager, Role.Manager, Domain.AppointmentTypes.AppointmentTypeIds.MedicalCheckUp));
    }

    [Fact]
    public async Task CoordinatorGetsNoRowsWhenTheCandidateHasNoActiveBooking()
    {
        _queries.Rows = [];

        var result = await Handler.HandleAsync(
            new GetCandidateBookingsQuery(Coordinator, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task CoordinatorGetsOneActiveBookingWithItsSlotWindow()
    {
        var bookingId = Guid.NewGuid();
        _queries.Rows =
        [
            new CandidateBookingSummary(
                bookingId, true, new DateOnly(2026, 9, 10), new TimeOnly(9, 0), new TimeOnly(13, 0)),
        ];

        var result = await Handler.HandleAsync(
            new GetCandidateBookingsQuery(Coordinator, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value);
        Assert.Equal(bookingId, row.BookingId);
        Assert.True(row.IsOriginal);
        Assert.Equal(new DateOnly(2026, 9, 10), row.SlotDate);
        Assert.Equal(new TimeOnly(9, 0), row.SlotStartTime);
        Assert.Equal(new TimeOnly(13, 0), row.SlotEndTime);
    }

    [Fact]
    public async Task CoordinatorGetsBothAnOriginalAndAnActiveRecoveryOnDifferentSlots()
    {
        var originalId = Guid.NewGuid();
        var recoveryId = Guid.NewGuid();
        _queries.Rows =
        [
            new CandidateBookingSummary(
                originalId, true, new DateOnly(2026, 9, 10), new TimeOnly(9, 0), new TimeOnly(13, 0)),
            new CandidateBookingSummary(
                recoveryId, false, new DateOnly(2026, 9, 12), new TimeOnly(13, 0), new TimeOnly(17, 0)),
        ];

        var result = await Handler.HandleAsync(
            new GetCandidateBookingsQuery(Coordinator, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.True(result.Value[0].IsOriginal);
        Assert.False(result.Value[1].IsOriginal);
        Assert.Equal(new DateOnly(2026, 9, 12), result.Value[1].SlotDate);
        Assert.Equal(new TimeOnly(17, 0), result.Value[1].SlotEndTime);
    }

    [Fact]
    public async Task UnknownCandidateIsNotFound()
    {
        _queries.Rows = null;

        var result = await Handler.HandleAsync(
            new GetCandidateBookingsQuery(Coordinator, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task CallerWithoutManageCandidatesIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new GetCandidateBookingsQuery(Manager, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(0, _queries.QueryCount);
    }
}
`````

## tests/EventBooking.Application.Tests/Candidates/GetCandidateReadinessHandlerTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Candidates/GetCandidateReadinessHandlerTests.cs","encoding":"utf8","sha256":"e21351543207e34512eb40ce77c8da69975d896327eee16705e3f5abbf2fe2c4","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Candidates;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Tests.Candidates;

/// <summary>Verifies readiness authorization, not-found handling, and coordinator success.</summary>
public sealed class GetCandidateReadinessHandlerTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly Guid Admin = Guid.Parse("c0000008-0000-0000-0000-000000000008");

    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly InMemoryQueries _queries = new();

    private GetCandidateReadinessHandler Handler => new(
        new StaffAccessAuthorizer(_roles), _queries, new CandidateReadinessCalculator());

    public GetCandidateReadinessHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _roles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));
    }

    /// <summary>An unknown candidate fails before any readiness is calculated.</summary>
    [Fact]
    public async Task UnknownCandidateIsNotFound()
    {
        _queries.Snapshot = null;

        var result = await Handler.HandleAsync(
            new GetCandidateReadinessQuery(Coordinator, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    /// <summary>A coordinator receives the calculated readiness.</summary>
    [Fact]
    public async Task CoordinatorReceivesCalculatedReadiness()
    {
        var candidateId = Guid.NewGuid();
        _queries.Snapshot = new CandidateReadinessSnapshot(
            candidateId,
            Guid.NewGuid(),
            [AppointmentTypeIds.MedicalCheckUp],
            null,
            []);

        var result = await Handler.HandleAsync(
            new GetCandidateReadinessQuery(Coordinator, candidateId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(candidateId, result.Value.CandidateId);
        Assert.Equal(CandidateReadinessCode.NoActiveBooking, result.Value.Code);
    }

    /// <summary>An admin is forbidden before the readiness query runs.</summary>
    [Fact]
    public async Task AdminRunsNoReadinessQuery()
    {
        var result = await Handler.HandleAsync(
            new GetCandidateReadinessQuery(Admin, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(0, _queries.QueryCount);
    }
}
`````

## tests/EventBooking.Application.Tests/Candidates/ImportCandidatesHandlerTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Candidates/ImportCandidatesHandlerTests.cs","encoding":"utf8","sha256":"c84a2845868b54c580eca081a7ab8b47d006d761f217c3c8ed85bad07d468583","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Candidates;
using EventBooking.Application.Common;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;

namespace EventBooking.Application.Tests.Candidates;

public class ImportCandidatesHandlerTests
{
    private const string Header = "name,email,employee_group";
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly Guid Admin = Guid.Parse("c0000008-0000-0000-0000-000000000008");
    private static readonly Guid Manager = Guid.Parse("c0000001-0000-0000-0000-000000000001");

    private readonly InMemoryCandidateRepository _candidates = new();
    private readonly InMemoryEmployeeGroupRepository _groups = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private ImportCandidatesHandler Handler => new(
        _candidates, _groups, new StaffAccessAuthorizer(_roles), _unitOfWork);

    public ImportCandidatesHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _roles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));
        _roles.Add(StaffAccessProfile.Create(
            Manager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _groups.Items.Add(EmployeeGroup.Define(
            EmployeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting]));
        _groups.Items.Add(EmployeeGroup.Define(
            EmployeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
            [AppointmentTypeIds.MedicalCheckUp]));
        _groups.Items.Add(EmployeeGroup.Define(
            EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
    }

    private Task<Result<CandidateImportOutcome>> Import(string csv, Guid? actor = null) =>
        Handler.HandleAsync(
            new ImportCandidatesCommand(actor ?? Coordinator, csv), CancellationToken.None);

    [Fact]
    public async Task AGoodFileCreatesOneCandidatePerRow()
    {
        var result = await Import(
            $"""
             {Header}
             Amara Novak,a.novak@mail.com,CABIN_CREW
             B. Chen,b.chen@mail.com,engineering
             """);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Accepted);
        Assert.Equal(2, result.Value.ImportedCount);
        Assert.Empty(result.Value.Errors);

        Assert.Equal(2, _candidates.Items.Count);
        var novak = _candidates.Items.Single(c => c.Email == "a.novak@mail.com");
        Assert.Equal("Amara Novak", novak.Name);
        Assert.Equal(CandidateStatus.NotYetInvited, novak.Status);
        Assert.Equal(EmployeeGroupIds.CabinCrew, novak.EmployeeGroupId);
        Assert.Equal(3, novak.Requirements.Count);
        var chen = _candidates.Items.Single(c => c.Email == "b.chen@mail.com");
        Assert.Equal(EmployeeGroupIds.Engineering, chen.EmployeeGroupId);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnAdminCannotImport()
    {
        var result = await Import($"{Header}\nAmara Novak,a.novak@mail.com,PILOTS", Admin);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Empty(_candidates.Items);
    }

    [Fact]
    public async Task AManagerCannotImport()
    {
        var result = await Import($"{Header}\nAmara Novak,a.novak@mail.com,PILOTS", Manager);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal("This staff profile cannot perform this operation.", result.Error.Message);
        Assert.Empty(_candidates.Items);
    }

    [Fact]
    public async Task OneBadRowRejectsTheWholeFile()
    {
        var result = await Import(
            $"""
             {Header}
             Amara Novak,a.novak@mail.com,PILOTS
             B. Chen,b.chen@mail.com,UNKNOWN_GROUP
             """);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Accepted);
        Assert.Equal(0, result.Value.ImportedCount);
        var error = Assert.Single(result.Value.Errors);
        Assert.Equal(3, error.LineNumber);
        Assert.Equal("UNKNOWN_GROUP is not a known employee group code.", error.Message);
        Assert.Empty(_candidates.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ABadEmailIsCaughtByTheDomainAndReportedAgainstItsLine()
    {
        var result = await Import(
            $"""
             {Header}
             Amara Novak,a.novak@mail.com,PILOTS
             B. Chen,not-an-email,ENGINEERING
             """);

        Assert.False(result.Value.Accepted);
        var error = Assert.Single(result.Value.Errors);
        Assert.Equal(3, error.LineNumber);
        Assert.Equal("email is not a valid email address.", error.Message);
        Assert.Empty(_candidates.Items);
    }

    [Fact]
    public async Task AnEmailThatAlreadyExistsIsReportedAgainstItsLine()
    {
        _candidates.Add(Candidate.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com",
            _groups.Items.Single(group => group.Id == EmployeeGroupIds.Engineering)));

        var result = await Import($"{Header}\nAmara N,a.novak@mail.com,PILOTS");

        Assert.False(result.Value.Accepted);
        var error = Assert.Single(result.Value.Errors);
        Assert.Equal(2, error.LineNumber);
        Assert.Equal("a.novak@mail.com is already a candidate.", error.Message);
        Assert.Single(_candidates.Items);
    }

    [Fact]
    public async Task AnEmptyUploadIsReportedNotCrashed()
    {
        var result = await Import("");

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Accepted);
        Assert.Equal("The file is empty.", Assert.Single(result.Value.Errors).Message);
    }

    [Fact]
    public async Task EveryBadRowIsListedTogether()
    {
        var result = await Import(
            $"""
             {Header}
             ,a.novak@mail.com,PILOTS
             B. Chen,also-not-an-email,ENGINEERING
             C. Diallo,c.diallo@mail.com,CABIN_CREW
             """);

        Assert.False(result.Value.Accepted);
        Assert.Equal(2, result.Value.Errors.Count);
        Assert.Equal([2, 3], result.Value.Errors.Select(e => e.LineNumber));
    }
}
`````

## tests/EventBooking.Application.Tests/Candidates/ListCandidatesHandlerTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Candidates/ListCandidatesHandlerTests.cs","encoding":"utf8","sha256":"24237b977cacdb2571bde03483530c4d49461bef02446d9c16c054c4d41c8e16","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Candidates;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;

namespace EventBooking.Application.Tests.Candidates;

public class ListCandidatesHandlerTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemoryCandidateRepository _candidates = new();
    private readonly InMemoryEmployeeGroupRepository _groups = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();

    private ListCandidatesHandler Handler => new(
        _candidates, _groups, new StaffAccessAuthorizer(_roles));

    public ListCandidatesHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _groups.Items.Add(EmployeeGroup.Define(
            EmployeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting]));
        _groups.Items.Add(EmployeeGroup.Define(
            EmployeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT",
            "Ground Operations Agent", true, [AppointmentTypeIds.MedicalCheckUp]));

        _candidates.Add(Candidate.Create(
            Guid.NewGuid(), "A. Novak", "a.novak@mail.com",
            _groups.Items.Single(group => group.Id == EmployeeGroupIds.CabinCrew)));

        var chen = Candidate.Create(
            Guid.NewGuid(), "B. Chen", "b.chen@mail.com",
            _groups.Items.Single(group => group.Id == EmployeeGroupIds.GroundOperationsAgent));
        chen.MarkInvited();
        _candidates.Add(chen);

        var diallo = Candidate.Create(
            Guid.NewGuid(), "C. Diallo", "c.diallo@mail.com",
            _groups.Items.Single(group => group.Id == EmployeeGroupIds.GroundOperationsAgent));
        diallo.MarkAwaitingAvailability();
        _candidates.Add(diallo);
    }

    [Fact]
    public async Task EveryCandidateIsListedWithGroupAndDisplayStatus()
    {
        var result = await Handler.HandleAsync(
            new ListCandidatesQuery(Coordinator, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);

        var novak = result.Value.Single(c => c.Email == "a.novak@mail.com");
        Assert.Equal(EmployeeGroupIds.CabinCrew, novak.EmployeeGroupId);
        Assert.Equal("CABIN_CREW", novak.EmployeeGroupCode);
        Assert.Equal("Cabin Crew", novak.EmployeeGroupName);
        Assert.False(novak.RequiresEmployeeGroupReconciliation);
        Assert.Equal(
            new[] { "DAT", "MED", "UNI" },
            novak.RequiredAppointmentTypes.Select(summary => summary.Code));
        Assert.Equal(CandidateStatus.NotYetInvited, novak.Status);
        Assert.Equal("Not yet invited", novak.StatusDisplay);

        var diallo = result.Value.Single(c => c.Email == "c.diallo@mail.com");
        Assert.Equal(EmployeeGroupIds.GroundOperationsAgent, diallo.EmployeeGroupId);
        Assert.Equal("GROUND_OPERATIONS_AGENT", diallo.EmployeeGroupCode);
        Assert.Equal("Ground Operations Agent", diallo.EmployeeGroupName);
        Assert.False(diallo.RequiresEmployeeGroupReconciliation);

        Assert.Equal(
            "Invited (pending response)",
            result.Value.Single(c => c.Email == "b.chen@mail.com").StatusDisplay);
        Assert.Equal(
            "Awaiting availability",
            result.Value.Single(c => c.Email == "c.diallo@mail.com").StatusDisplay);
    }

    [Fact]
    public async Task TheListIsOrderedByName()
    {
        var result = await Handler.HandleAsync(
            new ListCandidatesQuery(Coordinator, null, null), CancellationToken.None);

        Assert.Equal(new[] { "A. Novak", "B. Chen", "C. Diallo" }, result.Value.Select(c => c.Name));
    }

    [Fact]
    public async Task FilteringByStatusNarrowsTheList()
    {
        var result = await Handler.HandleAsync(
            new ListCandidatesQuery(Coordinator, CandidateStatus.Invited, null), CancellationToken.None);

        Assert.Equal("b.chen@mail.com", Assert.Single(result.Value).Email);
    }

    [Theory]
    [InlineData("diallo")]
    [InlineData("DIALLO")]
    [InlineData("c.diallo@mail.com")]
    public async Task SearchMatchesNameOrEmailCaseInsensitively(string search)
    {
        var result = await Handler.HandleAsync(
            new ListCandidatesQuery(Coordinator, null, search), CancellationToken.None);

        Assert.Equal("C. Diallo", Assert.Single(result.Value).Name);
    }

    [Fact]
    public async Task AnEmptySearchIsIgnored()
    {
        var result = await Handler.HandleAsync(
            new ListCandidatesQuery(Coordinator, null, "   "), CancellationToken.None);

        Assert.Equal(3, result.Value.Count);
    }

    [Fact]
    public async Task ANonCoordinatorIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new ListCandidatesQuery(Guid.NewGuid(), null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }
}
`````
