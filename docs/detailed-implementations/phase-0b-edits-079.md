# 00b — Vocabulary edits 79 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — tests/EventBooking.Application.Tests/Candidates/CandidateEmployeeGroupFlowTests.cs — 1/1

<!-- vocabulary-file: {"id":260,"oldPath":"tests/EventBooking.Application.Tests/Candidates/CandidateEmployeeGroupFlowTests.cs","newPath":"tests/EventBooking.Application.Tests/Attendees/AttendeeAttendeeGroupFlowTests.cs","beforeSha":"93c145145752d2550a739de1738cd72ad2f68c775efe3c5302a1d97dfa449162","afterSha":"f985e975736e39d6fa2c750f7620c3b1fbd0cdfdbe73e858dc3a10aa81757e9f","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Application.Tests/Attendees/AttendeeAttendeeGroupFlowTests.cs — 1/1

<!-- vocabulary-file: {"id":260,"oldPath":"tests/EventBooking.Application.Tests/Candidates/CandidateEmployeeGroupFlowTests.cs","newPath":"tests/EventBooking.Application.Tests/Attendees/AttendeeAttendeeGroupFlowTests.cs","beforeSha":"93c145145752d2550a739de1738cd72ad2f68c775efe3c5302a1d97dfa449162","afterSha":"f985e975736e39d6fa2c750f7620c3b1fbd0cdfdbe73e858dc3a10aa81757e9f","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Attendees;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Application.Tests.Attendees;

/// <summary>Verifies Attendee application inputs resolve and derive Attendee Group mappings.</summary>
public sealed class AttendeeAttendeeGroupFlowTests
{
    private static readonly Guid Coordinator = Guid.NewGuid();
    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryAttendeeGroupRepository _groups = new();
    private readonly InMemoryStaffAccessProfileRepository _profiles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    /// <summary>Initializes one Coordinator and the approved reference groups.</summary>
    public AttendeeAttendeeGroupFlowTests()
    {
        _profiles.Add(StaffAccessProfile.Create(Coordinator, [Role.Coordinator], null));
        _groups.Items.Add(AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
    }

    /// <summary>Create stores the group and derives requirements without requirement input.</summary>
    [Fact]
    public async Task CreateDerivesThePersistedGroupMapping()
    {
        var handler = new SaveAttendeeHandler(
            _attendees,
            _groups,
            new InMemoryInviteRepository(),
            new InMemoryBookingRepository(),
            new StaffAccessAuthorizer(_profiles),
            new RecordingAuditLogger(),
            _unitOfWork);

        var result = await handler.CreateAsync(
            new CreateAttendeeCommand(
                Coordinator, "Amara Novak", "amara@example.com", AttendeeGroupIds.Pilots),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var attendee = Assert.Single(_attendees.Items);
        Assert.Equal(AttendeeGroupIds.Pilots, attendee.AttendeeGroupId);
        Assert.Equal(
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            attendee.RequiredAppointmentTypeIds);
    }

    /// <summary>Unknown and absent groups return stable validation without saving.</summary>
    [Theory]
    [InlineData(null, "attendee_group_required")]
    public async Task InvalidGroupDoesNotCreateAttendee(Guid? groupId, string code)
    {
        var handler = new SaveAttendeeHandler(
            _attendees,
            _groups,
            new InMemoryInviteRepository(),
            new InMemoryBookingRepository(),
            new StaffAccessAuthorizer(_profiles),
            new RecordingAuditLogger(),
            _unitOfWork);

        var result = await handler.CreateAsync(
            new CreateAttendeeCommand(Coordinator, "Amara", "amara@example.com", groupId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(code, result.Error.Code);
        Assert.Empty(_attendees.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    /// <summary>The new CSV contract carries one case-insensitive Attendee Group code.</summary>
    [Fact]
    public void CsvParsesAttendeeGroupRatherThanAppointmentTypes()
    {
        var parsed = AttendeeCsvParser.Parse(
            "name,email,attendee_group\nAmara Novak,amara@example.com, pilots ");

        Assert.Empty(parsed.Errors);
        Assert.Equal("PILOTS", Assert.Single(parsed.Rows).AttendeeGroupCode);
        var old = AttendeeCsvParser.Parse(
            "name,email,appointment_types\nAmara Novak,amara@example.com,DAT;UNI");
        Assert.Equal(1, Assert.Single(old.Errors).LineNumber);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Candidates/CandidateReadinessCalculatorTests.cs — 1/1

<!-- vocabulary-file: {"id":261,"oldPath":"tests/EventBooking.Application.Tests/Candidates/CandidateReadinessCalculatorTests.cs","newPath":"tests/EventBooking.Application.Tests/Attendees/AttendeeReadinessCalculatorTests.cs","beforeSha":"4d889dbc99b6ec0a1503736705d7791df14d3a3f24f81e5f2d4dffc24c120191","afterSha":"9eeefb71008758e62fe664b243b999b9805484bfc06dd334a05a9d1d1166d110","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Application.Tests/Attendees/AttendeeReadinessCalculatorTests.cs — 1/1

<!-- vocabulary-file: {"id":261,"oldPath":"tests/EventBooking.Application.Tests/Candidates/CandidateReadinessCalculatorTests.cs","newPath":"tests/EventBooking.Application.Tests/Attendees/AttendeeReadinessCalculatorTests.cs","beforeSha":"4d889dbc99b6ec0a1503736705d7791df14d3a3f24f81e5f2d4dffc24c120191","afterSha":"9eeefb71008758e62fe664b243b999b9805484bfc06dd334a05a9d1d1166d110","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Attendees;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Tests.Attendees;

/// <summary>Verifies readiness precedence and latest-attempt semantics.</summary>
public sealed class AttendeeReadinessCalculatorTests
{
    private readonly AttendeeReadinessCalculator _calculator = new();

    /// <summary>A later Completed recovery attempt supersedes the original NoShow.</summary>
    [Fact]
    public void RecoveryCompletionMakesAttendeeReady()
    {
        var attendeeId = Guid.NewGuid();
        var original = Guid.NewGuid();
        var recovery = Guid.NewGuid();
        var type = AppointmentTypeIds.MedicalCheckUp;
        var snapshot = new AttendeeReadinessSnapshot(
            attendeeId,
            Guid.NewGuid(),
            [type],
            original,
            [
                Attempt(type, original, BookingStatus.Active, BookingAppointmentStatus.NoShow, 1),
                Attempt(type, recovery, BookingStatus.Concluded, BookingAppointmentStatus.Completed, 2),
            ]);

        var actual = _calculator.Calculate(snapshot);

        Assert.Equal(AttendeeReadinessCode.Ready, actual.Code);
        Assert.Empty(actual.OutstandingAppointmentTypes);
    }

    /// <summary>Any non-cancelled Completed attempt satisfies the type despite a later NoShow.</summary>
    [Fact]
    public void AnyCompletedAttemptControlsOutcome()
    {
        var attendeeId = Guid.NewGuid();
        var original = Guid.NewGuid();
        var recovery = Guid.NewGuid();
        var type = AppointmentTypeIds.UniformFitting;
        var snapshot = new AttendeeReadinessSnapshot(
            attendeeId,
            Guid.NewGuid(),
            [type],
            original,
            [
                Attempt(type, original, BookingStatus.Active, BookingAppointmentStatus.Completed, 1),
                Attempt(type, recovery, BookingStatus.Concluded, BookingAppointmentStatus.NoShow, 2),
            ]);

        var actual = _calculator.Calculate(snapshot);

        Assert.Equal(AttendeeReadinessCode.Ready, actual.Code);
        Assert.Empty(actual.OutstandingAppointmentTypes);
    }

    /// <summary>A latest NoShow with no completion is outstanding and recoverable.</summary>
    [Fact]
    public void LatestNoShowIsRecoverable()
    {
        var original = Guid.NewGuid();
        var type = AppointmentTypeIds.MedicalCheckUp;
        var actual = _calculator.Calculate(new AttendeeReadinessSnapshot(
            Guid.NewGuid(), Guid.NewGuid(), [type], original,
            [Attempt(type, original, BookingStatus.Active, BookingAppointmentStatus.NoShow, 1)]));

        Assert.Equal(AttendeeReadinessCode.AppointmentsOutstanding, actual.Code);
        var outstanding = Assert.Single(actual.OutstandingAppointmentTypes);
        Assert.Equal("MED", outstanding.Code);
        Assert.True(outstanding.IsRecoverable);
    }

    /// <summary>Unassigned reconciliation state wins over missing Booking state.</summary>
    [Fact]
    public void UnassignedGroupHasHighestFailurePrecedence()
    {
        var actual = _calculator.Calculate(new AttendeeReadinessSnapshot(
            Guid.NewGuid(), null, [], null, []));

        Assert.Equal(AttendeeReadinessCode.AttendeeGroupUnassigned, actual.Code);
    }

    private static AttendeeReadinessAttempt Attempt(
        Guid typeId,
        Guid bookingId,
        BookingStatus bookingStatus,
        BookingAppointmentStatus status,
        int day) => new(
            Guid.NewGuid(), typeId, status, bookingId, bookingStatus,
            DateTimeOffset.Parse($"2026-09-{day:00}T09:00:00Z"));
}
`````

## before — tests/EventBooking.Application.Tests/Candidates/DeleteCandidateHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":262,"oldPath":"tests/EventBooking.Application.Tests/Candidates/DeleteCandidateHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Attendees/DeleteAttendeeHandlerTests.cs","beforeSha":"2af0600c0b20a1bbb3f1e646627df5da02c689606636197eb698babe5cb9575a","afterSha":"89b67e54ff0b40bf31370c79974800d6a87d2b32d33af91f5c8a26469cb6c984","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Application.Tests/Attendees/DeleteAttendeeHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":262,"oldPath":"tests/EventBooking.Application.Tests/Candidates/DeleteCandidateHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Attendees/DeleteAttendeeHandlerTests.cs","beforeSha":"2af0600c0b20a1bbb3f1e646627df5da02c689606636197eb698babe5cb9575a","afterSha":"89b67e54ff0b40bf31370c79974800d6a87d2b32d33af91f5c8a26469cb6c984","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Attendees;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Attendees;

public class DeleteAttendeeHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryBookingRepository _bookings = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryBookingAppointmentRepository _appointments;

    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly Attendee _attendee;

    private DeleteAttendeeHandler Handler => new(
        _attendees, _invites, _bookings, _events, _roles,
        new BookingCanceller(_appointments, new InMemoryEventCapacityRepository(_events), _audit),
        _audit,
        _unitOfWork);

    public DeleteAttendeeHandlerTests()
    {
        _appointments = new InMemoryBookingAppointmentRepository(_bookings);
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com",
            AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
        _attendees.Add(_attendee);
    }

    [Fact]
    public async Task AAttendeeWithNothingOutstandingIsDeletedOutright()
    {
        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, _attendee.Id, false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_attendees.Items);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnUnconfirmedDeleteOfAAttendeeWithABookingIsRefusedWithAWarning()
    {
        GiveTheAttendeeABooking();

        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, _attendee.Id, false), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(
            "Deleting this attendee will cancel 1 booking and 1 pending invite, and free the capacity they hold. Confirm to proceed.",
            result.Error.Message);
        Assert.Single(_attendees.Items);
    }

    [Fact]
    public async Task AConfirmedDeleteVoidsTheBookingAndGivesTheCapacityBack()
    {
        var eventItem = GiveTheAttendeeABooking();
        Assert.Equal(9, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);

        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, _attendee.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_attendees.Items);
        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(8, eventItem.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
        Assert.Equal(6, eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(BookingStatus.Cancelled, _bookings.Items.Single().Status);
        Assert.Equal(InviteStatus.Superseded, _invites.Items.Single().Status);
        Assert.True(_audit.Contains(AuditAction.BookingCancelled));
        Assert.True(_audit.Contains(AuditAction.CapacityIncremented));
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AConfirmedDeleteCascadesToTheActiveRecoveryBookingAndAuditsTheDeletion()
    {
        var eventItem = GiveTheAttendeeABooking();
        var recoveryEvent = GiveTheAttendeeARecoveryBooking();
        Assert.Equal(9, recoveryEvent.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);

        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, _attendee.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_attendees.Items);
        Assert.Equal(2, _bookings.Items.Count);
        Assert.All(_bookings.Items, booking => Assert.Equal(BookingStatus.Cancelled, booking.Status));
        Assert.Equal(10, recoveryEvent.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.True(_audit.Contains(AuditAction.AttendeeDeleted));
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AnUnknownAttendeeIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, Guid.NewGuid(), true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task ANonCoordinatorIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Guid.NewGuid(), _attendee.Id, true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    private Event GiveTheAttendeeARecoveryBooking()
    {
        var original = _bookings.Items.Single();
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 11), new TimeOnly(9, 0)), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 10);
        var recoveryEvent = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Add(recoveryEvent);

        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(), _attendee.Id, original.Id, "recovery-hash", Now.AddDays(4),
            [recoveryEvent.Id, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        _invites.Add(recoveryInvite);

        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, recoveryEvent.Id, "recovery-manage-hash", Now);
        _bookings.Add(recovery);
        recoveryInvite.MarkUsed();

        recoveryEvent.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.DrugAndAlcoholTesting));

        return recoveryEvent;
    }

    private Event GiveTheAttendeeABooking()
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Add(eventItem);

        var invite = Invite.CreateInitial(
            Guid.NewGuid(), _attendee.Id, "hash", Now.AddDays(4),
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()], _attendee.RequiredAppointmentTypeIds, 0);
        _invites.Add(invite);

        var booking = Booking.Create(Guid.NewGuid(), invite, eventItem.Id, "manage-hash", Now);
        _bookings.Add(booking);

        foreach (var typeId in _attendee.RequiredAppointmentTypeIds)
        {
            eventItem.CapacityFor(typeId).Decrement();
            _appointments.Add(BookingAppointment.Create(Guid.NewGuid(), booking.Id, typeId));
        }

        return eventItem;
    }
}
`````

## before — tests/EventBooking.Application.Tests/Candidates/EmployeeGroupLifecycleTests.cs — 1/1

<!-- vocabulary-file: {"id":263,"oldPath":"tests/EventBooking.Application.Tests/Candidates/EmployeeGroupLifecycleTests.cs","newPath":"tests/EventBooking.Application.Tests/Attendees/AttendeeGroupLifecycleTests.cs","beforeSha":"98fab90be0d8bf82d236bb2d0bce51a3f0aa5f676c9dc6e2a66035a3419c7c8c","afterSha":"831575b691191a34d578980dd8851d189433de103a92decb281a60edda894828","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Application.Tests/Attendees/AttendeeGroupLifecycleTests.cs — 1/1

<!-- vocabulary-file: {"id":263,"oldPath":"tests/EventBooking.Application.Tests/Candidates/EmployeeGroupLifecycleTests.cs","newPath":"tests/EventBooking.Application.Tests/Attendees/AttendeeGroupLifecycleTests.cs","beforeSha":"98fab90be0d8bf82d236bb2d0bce51a3f0aa5f676c9dc6e2a66035a3419c7c8c","afterSha":"831575b691191a34d578980dd8851d189433de103a92decb281a60edda894828","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Attendees;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;

namespace EventBooking.Application.Tests.Attendees;

/// <summary>Verifies locked Attendee Group changes follow every Attendee lifecycle rule.</summary>
public sealed class AttendeeGroupLifecycleTests
{
    private static readonly AttendeeGroup CabinCrew = AttendeeGroup.Define(
        AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
            AppointmentTypeIds.UniformFitting]);
    private static readonly AttendeeGroup GroundTransport = AttendeeGroup.Define(
        AttendeeGroupIds.GroundTransportServices, "GROUND_TRANSPORT_SERVICES",
        "Ground Transport Services", true,
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
            AppointmentTypeIds.UniformFitting]);
    private static readonly AttendeeGroup Pilots = AttendeeGroup.Define(
        AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);

    /// <summary>A set-equivalent active-Booking update preserves all lifecycle state.</summary>
    [Fact]
    public async Task EquivalentGroupPreservesActiveBookingAndUsedInviteHistory()
    {
        var fixture = GivenAttendee(CabinCrew, AttendeeStatus.Booked, activeBooking: true);

        var result = await fixture.Handler.UpdateAsync(
            new UpdateAttendeeCommand(
                fixture.Coordinator, fixture.Attendee.Id, "Amara N.",
                "amara.n@example.com", GroundTransport.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(GroundTransport.Id, fixture.Attendee.AttendeeGroupId);
        Assert.Equal(AttendeeStatus.Booked, fixture.Attendee.Status);
        Assert.Equal(InviteStatus.Used, fixture.Invite!.Status);
        Assert.Equal(
            ["attendee-locked", "initial-invite-locked", "original-booking-locked"],
            fixture.Operations.Events);
    }

    /// <summary>A set-changing active-Booking update fails before any Attendee mutation.</summary>
    [Fact]
    public async Task ChangedGroupConflictsWithActiveOriginalBooking()
    {
        var fixture = GivenAttendee(CabinCrew, AttendeeStatus.Booked, activeBooking: true);

        var result = await fixture.Handler.UpdateAsync(
            new UpdateAttendeeCommand(
                fixture.Coordinator, fixture.Attendee.Id, "Changed", "changed@example.com", Pilots.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("attendee_group_active_booking_conflict", result.Error.Code);
        Assert.Equal(CabinCrew.Id, fixture.Attendee.AttendeeGroupId);
        Assert.Equal("Amara", fixture.Attendee.Name);
    }

    /// <summary>A set-changing pending Invite is superseded without automatic replacement.</summary>
    [Fact]
    public async Task ChangedGroupSupersedesPendingInviteAndResetsStatus()
    {
        var fixture = GivenAttendee(CabinCrew, AttendeeStatus.Invited, activeBooking: false);

        var result = await fixture.Handler.UpdateAsync(
            new UpdateAttendeeCommand(
                fixture.Coordinator, fixture.Attendee.Id, "Amara", "amara@example.com", Pilots.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InviteStatus.Superseded, fixture.Invite!.Status);
        Assert.Equal(AttendeeStatus.NotYetInvited, fixture.Attendee.Status);
        Assert.Equal(Pilots.RequiredAppointmentTypeIds, fixture.Attendee.RequiredAppointmentTypeIds);
        Assert.Equal(1, fixture.Audit.Entries.Count(entry =>
            entry.Action == EventBooking.Domain.Audit.AuditAction.AttendeeGroupReassigned));
    }

    /// <summary>A repeated identical request performs no save and writes no audit row.</summary>
    [Fact]
    public async Task IdenticalUpdateIsANoOp()
    {
        var fixture = GivenAttendee(CabinCrew, AttendeeStatus.NotYetInvited, activeBooking: false,
            includeInvite: false);

        var result = await fixture.Handler.UpdateAsync(
            new UpdateAttendeeCommand(
                fixture.Coordinator, fixture.Attendee.Id, "Amara", "amara@example.com", CabinCrew.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
        Assert.Empty(fixture.Audit.Entries);
    }

    private static Fixture GivenAttendee(
        AttendeeGroup group,
        AttendeeStatus status,
        bool activeBooking,
        bool includeInvite = true)
    {
        var coordinator = Guid.NewGuid();
        var operations = new TransactionOperationLog();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(coordinator, [Role.Coordinator], null));
        var groups = new InMemoryAttendeeGroupRepository();
        groups.Items.AddRange([CabinCrew, GroundTransport, Pilots]);
        var attendee = Attendee.Create(Guid.NewGuid(), "Amara", "amara@example.com", group);
        if (status == AttendeeStatus.Invited || status == AttendeeStatus.Booked)
        {
            attendee.MarkInvited();
        }
        if (status == AttendeeStatus.Booked)
        {
            attendee.MarkBooked();
        }
        var attendees = new InMemoryAttendeeRepository(operations);
        attendees.Add(attendee);
        var invites = new InMemoryInviteRepository(operations);
        Invite? invite = null;
        if (includeInvite)
        {
            var eventId = Guid.NewGuid();
            invite = Invite.CreateInitial(
                Guid.NewGuid(), attendee.Id, "token", DateTimeOffset.UtcNow.AddDays(1),
                [eventId, Guid.NewGuid(), Guid.NewGuid()], attendee.RequiredAppointmentTypeIds, 0);
            invites.Add(invite);
        }
        var bookings = new InMemoryBookingRepository(operations);
        if (activeBooking)
        {
            bookings.Add(Booking.Create(
                Guid.NewGuid(), invite!, invite!.OfferedEventIds[0], "manage", DateTimeOffset.UtcNow));
            invite!.MarkUsed();
        }
        var audit = new RecordingAuditLogger();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new SaveAttendeeHandler(
            attendees, groups, invites, bookings, new StaffAccessAuthorizer(profiles), audit, unitOfWork);
        return new Fixture(handler, attendee, invite, coordinator, operations, audit, unitOfWork);
    }

    private sealed record Fixture(
        SaveAttendeeHandler Handler,
        Attendee Attendee,
        Invite? Invite,
        Guid Coordinator,
        TransactionOperationLog Operations,
        RecordingAuditLogger Audit,
        FakeUnitOfWork UnitOfWork);
}
`````

## before — tests/EventBooking.Application.Tests/Candidates/GetCandidateBookingsHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":264,"oldPath":"tests/EventBooking.Application.Tests/Candidates/GetCandidateBookingsHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Attendees/GetAttendeeBookingsHandlerTests.cs","beforeSha":"b85791c0774054d553ef282d15a8f295cada52bc99fffcf971cd29bf21c9bbaa","afterSha":"8c92eb2408eeae81b72af20dfb92b4e44cb0dba8a3153e077a357274d656c615","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Application.Tests/Attendees/GetAttendeeBookingsHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":264,"oldPath":"tests/EventBooking.Application.Tests/Candidates/GetCandidateBookingsHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Attendees/GetAttendeeBookingsHandlerTests.cs","beforeSha":"b85791c0774054d553ef282d15a8f295cada52bc99fffcf971cd29bf21c9bbaa","afterSha":"8c92eb2408eeae81b72af20dfb92b4e44cb0dba8a3153e077a357274d656c615","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Attendees;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Tests.Attendees;

/// <summary>Verifies staff authorization and shape of the attendee active-booking listing.</summary>
public sealed class GetAttendeeBookingsHandlerTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly Guid Manager = Guid.Parse("c0000007-0000-0000-0000-000000000007");

    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly InMemoryAttendeeBookingQueries _queries = new();

    private GetAttendeeBookingsHandler Handler =>
        new(new StaffAccessAuthorizer(_roles), _queries);

    public GetAttendeeBookingsHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _roles.Add(StaffAccessProfile.Create(
            Manager, Role.Manager, Domain.AppointmentTypes.AppointmentTypeIds.MedicalCheckUp));
    }

    [Fact]
    public async Task CoordinatorGetsNoRowsWhenTheAttendeeHasNoActiveBooking()
    {
        _queries.Rows = [];

        var result = await Handler.HandleAsync(
            new GetAttendeeBookingsQuery(Coordinator, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task CoordinatorGetsOneActiveBookingWithItsEventWindow()
    {
        var bookingId = Guid.NewGuid();
        _queries.Rows =
        [
            new AttendeeBookingSummary(
                bookingId, true, new DateOnly(2026, 9, 10), new TimeOnly(9, 0), new TimeOnly(13, 0)),
        ];

        var result = await Handler.HandleAsync(
            new GetAttendeeBookingsQuery(Coordinator, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value);
        Assert.Equal(bookingId, row.BookingId);
        Assert.True(row.IsOriginal);
        Assert.Equal(new DateOnly(2026, 9, 10), row.EventDate);
        Assert.Equal(new TimeOnly(9, 0), row.EventStartTime);
        Assert.Equal(new TimeOnly(13, 0), row.EventEndTime);
    }

    [Fact]
    public async Task CoordinatorGetsBothAnOriginalAndAnActiveRecoveryOnDifferentEvents()
    {
        var originalId = Guid.NewGuid();
        var recoveryId = Guid.NewGuid();
        _queries.Rows =
        [
            new AttendeeBookingSummary(
                originalId, true, new DateOnly(2026, 9, 10), new TimeOnly(9, 0), new TimeOnly(13, 0)),
            new AttendeeBookingSummary(
                recoveryId, false, new DateOnly(2026, 9, 12), new TimeOnly(13, 0), new TimeOnly(17, 0)),
        ];

        var result = await Handler.HandleAsync(
            new GetAttendeeBookingsQuery(Coordinator, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.True(result.Value[0].IsOriginal);
        Assert.False(result.Value[1].IsOriginal);
        Assert.Equal(new DateOnly(2026, 9, 12), result.Value[1].EventDate);
        Assert.Equal(new TimeOnly(17, 0), result.Value[1].EventEndTime);
    }

    [Fact]
    public async Task UnknownAttendeeIsNotFound()
    {
        _queries.Rows = null;

        var result = await Handler.HandleAsync(
            new GetAttendeeBookingsQuery(Coordinator, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task CallerWithoutManageAttendeesIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new GetAttendeeBookingsQuery(Manager, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(0, _queries.QueryCount);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Candidates/GetCandidateReadinessHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":265,"oldPath":"tests/EventBooking.Application.Tests/Candidates/GetCandidateReadinessHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Attendees/GetAttendeeReadinessHandlerTests.cs","beforeSha":"e21351543207e34512eb40ce77c8da69975d896327eee16705e3f5abbf2fe2c4","afterSha":"72ff42f771256207a2c55aec51d81b0acc62c91ba07cc266467cb9e9f0bd94bb","side":"before","part":1,"parts":1} -->

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
