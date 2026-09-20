# 00b — Vocabulary edits 75 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — tests/EventBooking.Application.Tests/Appointments/RecoveryBookingOutcomeCoordinatorTests.cs — 1/1

<!-- vocabulary-file: {"id":247,"oldPath":"tests/EventBooking.Application.Tests/Appointments/RecoveryBookingOutcomeCoordinatorTests.cs","newPath":"tests/EventBooking.Application.Tests/Appointments/RecoveryBookingOutcomeCoordinatorTests.cs","beforeSha":"27342724cb7e2b0a8f1adadb2f8939167ac373c3dfae4d10d7d88e8fbfc9a521","afterSha":"f2b55fd02a870208a0444b88b9d0328e1ec833c10b4747c6bf5cd1a2ed1bdb83","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Appointments;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;

namespace EventBooking.Application.Tests.Appointments;

/// <summary>Verifies recovery Booking status follows only its own appointment outcomes.</summary>
public sealed class RecoveryBookingOutcomeCoordinatorTests
{
    /// <summary>All terminal outcomes conclude a recovery while leaving its root untouched.</summary>
    [Fact]
    public void TerminalRecoveryAppointmentsConcludeRecoveryOnly()
    {
        var staff = Guid.NewGuid();
        var originalEvent = Guid.NewGuid();
        var initial = Invite.CreateInitial(
            Guid.NewGuid(), Guid.NewGuid(), "initial", DateTimeOffset.UtcNow.AddDays(1),
            [originalEvent, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        var original = Booking.Create(
            Guid.NewGuid(), initial, originalEvent, "original", DateTimeOffset.UtcNow);
        var recoveryEvent = Guid.NewGuid();
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(), original.AttendeeId, original.Id, "recovery",
            DateTimeOffset.UtcNow.AddDays(2),
            [recoveryEvent, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);
        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, recoveryEvent, "manage", DateTimeOffset.UtcNow);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.MedicalCheckUp);
        appointment.TransitionTo(
            BookingAppointmentStatus.NoShow, staff, DateTimeOffset.UtcNow,
            checkInAllowed: false, noShowAllowed: true);

        var changed = new RecoveryBookingOutcomeCoordinator()
            .Synchronize(recovery, [appointment], laterRecoveryExists: false);

        Assert.True(changed);
        Assert.Equal(BookingStatus.Concluded, recovery.Status);
        Assert.Equal(BookingStatus.Active, original.Status);
    }

    /// <summary>A corrected non-terminal appointment cannot reopen behind a later recovery.</summary>
    [Fact]
    public void LaterRecoveryPreventsReopen()
    {
        var originalEvent = Guid.NewGuid();
        var initial = Invite.CreateInitial(
            Guid.NewGuid(), Guid.NewGuid(), "initial", DateTimeOffset.UtcNow.AddDays(1),
            [originalEvent, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        var original = Booking.Create(Guid.NewGuid(), initial, originalEvent, "root", DateTimeOffset.UtcNow);
        var recoveryEvent = Guid.NewGuid();
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(), original.AttendeeId, original.Id, "recovery",
            DateTimeOffset.UtcNow.AddDays(1),
            [recoveryEvent, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);
        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, recoveryEvent, "manage", DateTimeOffset.UtcNow);
        recovery.Conclude();
        var expected = BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.MedicalCheckUp);

        Assert.Throws<EventBooking.Domain.Common.DomainException>(() =>
            new RecoveryBookingOutcomeCoordinator()
                .Synchronize(recovery, [expected], laterRecoveryExists: true));
    }
}
`````

## before — tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":248,"oldPath":"tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs","beforeSha":"1d8bfdc59bb9cd0e1c74d4ea4f46184e815def766872004e624c438db48dba95","afterSha":"018db007708853ac1bf7a21e59615aa0c7c7613e28a51be0749213d01cb6f0c0","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Appointments;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Appointments;

/// <summary>Verifies scoped, versioned, idempotent appointment status command behavior.</summary>
public sealed class UpdateBookingAppointmentStatusHandlerTests
{
    /// <summary>Verifies a real check-in commits one state and one safe audit entry.</summary>
    [Fact]
    public async Task CheckInUpdatesAndAuditsOnce()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingAppointmentStatus.CheckedIn, result.Value.Status);
        Assert.Equal(2, result.Value.Version);
        var entry = Assert.Single(scenario.Audit.Entries);
        Assert.Equal(AuditAction.AppointmentCheckedIn, entry.Action);
        Assert.DoesNotContain("Amara", entry.Details);
        Assert.DoesNotContain("amara@example.com", entry.Details);
        Assert.Equal(1, scenario.UnitOfWork.CommitCount);
    }

    /// <summary>
    /// Verifies mutation shares the candidate-first lifecycle lock order used by every
    /// journey transition: Candidate, pending Invites, original Booking, addressed Booking,
    /// Confirmed Slot, then ordered Booking Appointments.
    /// </summary>
    [Fact]
    public async Task LocksCandidateLifecycleBeforeSlotAndAppointments()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [
                "transaction-begun",
                "candidate-locked",
                "pending-invites-locked",
                "booking-locked",
                "booking-locked",
                "slot-guard-locked",
                "appointments-locked",
            ],
            scenario.Operations.Events.TakeLast(7));
    }

    /// <summary>Verifies retrying the committed state with the old version is a clean success.</summary>
    [Fact]
    public async Task SameStateRetryWithOldVersionCreatesNoSecondWriteOrAudit()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        var retry = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        Assert.True(retry.IsSuccess);
        Assert.Equal(2, retry.Value.Version);
        Assert.Single(scenario.Audit.Entries);
        Assert.Equal(1, scenario.UnitOfWork.SaveCount);
        Assert.Equal(1, scenario.UnitOfWork.CommitCount);
    }

    /// <summary>Verifies a different state carrying a stale version returns current safe state.</summary>
    [Fact]
    public async Task DistinctStaleTransitionConflictsWithoutMutation()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        var stale = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.Completed, 1),
            CancellationToken.None);

        Assert.True(stale.IsFailure);
        Assert.Equal("appointment_version_conflict", stale.Error.Code);
        Assert.Contains("version 2", stale.Error.Message);
        Assert.Equal(BookingAppointmentStatus.CheckedIn, scenario.Appointment.Status);
        Assert.Single(scenario.Audit.Entries);
    }

    /// <summary>Verifies no-show is rejected until the four-hour window has ended.</summary>
    [Fact]
    public async Task NoShowBeforeWindowEndIsRejected()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 12, 59, 0, TimeSpan.Zero));

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.NoShow, 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Empty(scenario.Audit.Entries);
    }

    /// <summary>Verifies check-in is rejected outside the confirmed-slot head-office date.</summary>
    [Fact]
    public async Task CheckInOnAnotherHeadOfficeDateIsRejected()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 8, 9, 5, 0, TimeSpan.Zero));

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(BookingAppointmentStatus.Expected, scenario.Appointment.Status);
    }

    /// <summary>Verifies cancelled parent state blocks an otherwise valid transition.</summary>
    [Fact]
    public async Task CancelledBookingRejectsTheUpdate()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        scenario.Booking.Cancel();

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Empty(scenario.Audit.Entries);
    }

    /// <summary>Verifies a cancelled slot blocks an otherwise valid transition.</summary>
    [Fact]
    public async Task CancelledSlotRejectsTheUpdate()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        scenario.Slot.Cancel();

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Empty(scenario.Audit.Entries);
    }

    /// <summary>Verifies terminal outcomes on the original Booking leave it Active.</summary>
    [Fact]
    public async Task OriginalTerminalOutcomesStayActive()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.Completed, 2),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Active, scenario.Booking.Status);
        Assert.DoesNotContain(scenario.Audit.Entries, e => e.Action == AuditAction.RecoveryBookingConcluded);
    }

    /// <summary>Verifies mixed recovery outcomes leave the recovery Booking Active.</summary>
    [Fact]
    public async Task MixedRecoveryOutcomesLeaveRecoveryActive()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        var (_, first) = AddRecovery(scenario, scenario.Booking.CreatedAt.AddHours(1));
        first.TransitionTo(
            BookingAppointmentStatus.CheckedIn, scenario.StaffUserId,
            new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero), true, false);
        first.TransitionTo(
            BookingAppointmentStatus.Completed, scenario.StaffUserId,
            new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero), false, false);
        var other = BookingAppointment.Create(
            Guid.NewGuid(), scenario.Bookings.Items.Single(b => !b.IsOriginal).Id,
            AppointmentTypeIds.DrugAndAlcoholTesting);
        scenario.Appointments.Add(other);

        var result = await scenario.Handler.HandleAsync(
            ForAppointment(
                scenario, other.Id, BookingAppointmentStatus.CheckedIn, other.Version),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            BookingStatus.Active,
            scenario.Bookings.Items.Single(b => !b.IsOriginal).Status);
        Assert.DoesNotContain(scenario.Audit.Entries, e => e.Action == AuditAction.RecoveryBookingConcluded);
    }

    /// <summary>Verifies the final terminal outcome concludes a recovery Booking.</summary>
    [Fact]
    public async Task FinalOutcomeConcludesRecovery()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 14, 0, 0, TimeSpan.Zero));
        var (_, appointment) = AddRecovery(scenario, scenario.Booking.CreatedAt.AddHours(1));

        var result = await scenario.Handler.HandleAsync(
            ForAppointment(
                scenario, appointment.Id, BookingAppointmentStatus.NoShow, appointment.Version),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            BookingStatus.Concluded,
            scenario.Bookings.Items.Single(b => !b.IsOriginal).Status);
        Assert.Equal(BookingStatus.Active, scenario.Booking.Status);
        Assert.True(scenario.Audit.Contains(AuditAction.RecoveryBookingConcluded));
        Assert.True(scenario.Audit.Contains(AuditAction.AppointmentMarkedNoShow));
    }

    /// <summary>Verifies correcting a concluded recovery reopens it with fixed statuses.</summary>
    [Fact]
    public async Task AllowedCorrectionReopensConcludedRecovery()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        var (recovery, appointment) = AddRecovery(scenario, scenario.Booking.CreatedAt.AddHours(1));
        appointment.TransitionTo(
            BookingAppointmentStatus.NoShow, scenario.StaffUserId,
            new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero), false, true);
        recovery.Conclude();

        var result = await scenario.Handler.HandleAsync(
            ForAppointment(
                scenario, appointment.Id, BookingAppointmentStatus.Expected, appointment.Version),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Active, recovery.Status);
        var entry = Assert.Single(
            scenario.Audit.Entries, e => e.Action == AuditAction.AppointmentStatusCorrected);
        Assert.Contains(";booking:Concluded->Active", entry.Details);
        Assert.DoesNotContain(scenario.Audit.Entries, e => e.Action == AuditAction.RecoveryBookingConcluded);
    }

    /// <summary>Verifies a correction behind a pending recovery invite is rejected.</summary>
    [Fact]
    public async Task GuardedCorrectionBehindPendingInviteRequiresCancellingRecoveryFirst()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        var (recovery, appointment) = AddRecovery(scenario, scenario.Booking.CreatedAt.AddHours(1));
        appointment.TransitionTo(
            BookingAppointmentStatus.NoShow, scenario.StaffUserId,
            new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero), false, true);
        scenario.Invites.Add(Invite.CreateRecovery(
            Guid.NewGuid(), scenario.Candidate.Id, scenario.Booking.Id, "pending-recovery",
            new DateTimeOffset(2026, 9, 9, 9, 0, 0, TimeSpan.Zero),
            [scenario.Slot.Id, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting]));

        var result = await scenario.Handler.HandleAsync(
            ForAppointment(
                scenario, appointment.Id, BookingAppointmentStatus.Expected, appointment.Version),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Contains("Cancel the recovery first", result.Error.Message);
        Assert.Equal(BookingAppointmentStatus.NoShow, appointment.Status);
        Assert.Equal(BookingStatus.Active, recovery.Status);
    }

    /// <summary>Verifies a correction behind a later recovery Booking is rejected.</summary>
    [Fact]
    public async Task GuardedCorrectionBehindLaterBookingRequiresCancellingRecoveryFirst()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        scenario.Appointment.TransitionTo(
            BookingAppointmentStatus.NoShow, scenario.StaffUserId,
            new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero), false, true);
        AddRecovery(scenario, scenario.Booking.CreatedAt.AddHours(1));

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.Expected, scenario.Appointment.Version),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Contains("Cancel the recovery first", result.Error.Message);
        Assert.Equal(BookingAppointmentStatus.NoShow, scenario.Appointment.Status);
    }

    /// <summary>Verifies capability denial occurs before the scoped appointment locator.</summary>
    [Fact]
    public async Task AdminIsDeniedBeforeAppointmentDataAccess()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        var admin = Guid.NewGuid();
        scenario.Profiles.Add(StaffAccessProfile.Create(admin, [Role.Admin], null));

        var result = await scenario.Handler.HandleAsync(
            new UpdateBookingAppointmentStatusCommand
            {
                StaffUserId = admin,
                BookingAppointmentId = scenario.Appointment.Id,
                Status = BookingAppointmentStatus.CheckedIn,
                ExpectedVersion = 1,
            },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.DoesNotContain("appointment-located", scenario.Operations.Events);
    }

    /// <summary>Verifies another appointment type is hidden behind the stable not-found result.</summary>
    [Fact]
    public async Task CrossTypeRecordIsNotFound()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        var otherStaff = Guid.NewGuid();
        scenario.Profiles.Add(StaffAccessProfile.Create(
            otherStaff, [Role.Manager], AppointmentTypeIds.MedicalCheckUp));

        var result = await scenario.Handler.HandleAsync(
            new UpdateBookingAppointmentStatusCommand
            {
                StaffUserId = otherStaff,
                BookingAppointmentId = scenario.Appointment.Id,
                Status = BookingAppointmentStatus.CheckedIn,
                ExpectedVersion = 1,
            },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    private static UpdateBookingAppointmentStatusCommand Command(
        Scenario scenario,
        BookingAppointmentStatus status,
        long version) => ForAppointment(scenario, scenario.Appointment.Id, status, version);

    private static UpdateBookingAppointmentStatusCommand ForAppointment(
        Scenario scenario,
        Guid appointmentId,
        BookingAppointmentStatus status,
        long version) => new()
    {
        StaffUserId = scenario.StaffUserId,
        BookingAppointmentId = appointmentId,
        Status = status,
        ExpectedVersion = version,
    };

    private static (Booking Recovery, BookingAppointment Appointment) AddRecovery(
        Scenario scenario,
        DateTimeOffset recoveryCreatedAt)
    {
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(), scenario.Candidate.Id, scenario.Booking.Id, $"recovery-{Guid.NewGuid():N}",
            recoveryCreatedAt.AddDays(2),
            [scenario.Slot.Id, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        scenario.Invites.Add(recoveryInvite);
        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, scenario.Booking, scenario.Slot.Id,
            $"manage-{Guid.NewGuid():N}", recoveryCreatedAt);
        scenario.Bookings.Add(recovery);
        recoveryInvite.MarkUsed();
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.DrugAndAlcoholTesting);
        scenario.Appointments.Add(appointment);

        return (recovery, appointment);
    }

    /// <summary>Builds a DAT-only group; the scenario needs a mapping, not an identity.</summary>
    private static EmployeeGroup DatOnly() =>
        EmployeeGroup.Define(
            Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);

    private static Scenario GivenScenario(DateTimeOffset now)
    {
        var staff = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            staff, [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting));
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", "amara@example.com", DatOnly());
        var operations = new TransactionOperationLog();
        var candidates = new InMemoryCandidateRepository(operations);
        candidates.Add(candidate);
        var slot = ConfirmedSlot.CreateImported(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 7), new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 10));
        var slots = new InMemoryConfirmedSlotRepository(operations);
        slots.Add(slot);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidate.Id, "invite-token", now.AddDays(1),
            [slot.Id, Guid.NewGuid(), Guid.NewGuid()], candidate.RequiredAppointmentTypeIds, 0);
        var invites = new InMemoryInviteRepository(operations);
        invites.Add(invite);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, slot.Id, "manage-token", now.AddDays(-1));
        var bookings = new InMemoryBookingRepository(operations);
        bookings.Add(booking);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting);
        var appointments = new InMemoryBookingAppointmentRepository(bookings, operations);
        appointments.Add(appointment);
        var audit = new RecordingAuditLogger();
        var unitOfWork = new FakeUnitOfWork(operations);
        var clock = new FakeClock(now);
        var handler = new UpdateBookingAppointmentStatusHandler(
            new StaffAccessAuthorizer(profiles),
            appointments,
            bookings,
            candidates,
            invites,
            slots,
            new RecoveryBookingOutcomeCoordinator(),
            audit,
            unitOfWork,
            clock);
        return new Scenario(
            staff, profiles, candidate, slot, appointment, booking,
            candidates, invites, bookings, appointments,
            audit, unitOfWork, operations, handler);
    }

    private sealed record Scenario(
        Guid StaffUserId,
        InMemoryStaffAccessProfileRepository Profiles,
        Candidate Candidate,
        ConfirmedSlot Slot,
        BookingAppointment Appointment,
        Booking Booking,
        InMemoryCandidateRepository Candidates,
        InMemoryInviteRepository Invites,
        InMemoryBookingRepository Bookings,
        InMemoryBookingAppointmentRepository Appointments,
        RecordingAuditLogger Audit,
        FakeUnitOfWork UnitOfWork,
        TransactionOperationLog Operations,
        UpdateBookingAppointmentStatusHandler Handler);
}
`````

## after — tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":248,"oldPath":"tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs","beforeSha":"1d8bfdc59bb9cd0e1c74d4ea4f46184e815def766872004e624c438db48dba95","afterSha":"018db007708853ac1bf7a21e59615aa0c7c7613e28a51be0749213d01cb6f0c0","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Appointments;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Appointments;

/// <summary>Verifies scoped, versioned, idempotent appointment status command behavior.</summary>
public sealed class UpdateBookingAppointmentStatusHandlerTests
{
    /// <summary>Verifies a real check-in commits one state and one safe audit entry.</summary>
    [Fact]
    public async Task CheckInUpdatesAndAuditsOnce()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingAppointmentStatus.CheckedIn, result.Value.Status);
        Assert.Equal(2, result.Value.Version);
        var entry = Assert.Single(scenario.Audit.Entries);
        Assert.Equal(AuditAction.AppointmentCheckedIn, entry.Action);
        Assert.DoesNotContain("Amara", entry.Details);
        Assert.DoesNotContain("amara@example.com", entry.Details);
        Assert.Equal(1, scenario.UnitOfWork.CommitCount);
    }

    /// <summary>
    /// Verifies mutation shares the attendee-first lifecycle lock order used by every
    /// journey transition: Attendee, pending Invites, original Booking, addressed Booking,
    /// Confirmed Event, then ordered Booking Appointments.
    /// </summary>
    [Fact]
    public async Task LocksAttendeeLifecycleBeforeEventAndAppointments()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [
                "transaction-begun",
                "attendee-locked",
                "pending-invites-locked",
                "booking-locked",
                "booking-locked",
                "event-guard-locked",
                "appointments-locked",
            ],
            scenario.Operations.Events.TakeLast(7));
    }

    /// <summary>Verifies retrying the committed state with the old version is a clean success.</summary>
    [Fact]
    public async Task SameStateRetryWithOldVersionCreatesNoSecondWriteOrAudit()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        var retry = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        Assert.True(retry.IsSuccess);
        Assert.Equal(2, retry.Value.Version);
        Assert.Single(scenario.Audit.Entries);
        Assert.Equal(1, scenario.UnitOfWork.SaveCount);
        Assert.Equal(1, scenario.UnitOfWork.CommitCount);
    }

    /// <summary>Verifies a different state carrying a stale version returns current safe state.</summary>
    [Fact]
    public async Task DistinctStaleTransitionConflictsWithoutMutation()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        var stale = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.Completed, 1),
            CancellationToken.None);

        Assert.True(stale.IsFailure);
        Assert.Equal("appointment_version_conflict", stale.Error.Code);
        Assert.Contains("version 2", stale.Error.Message);
        Assert.Equal(BookingAppointmentStatus.CheckedIn, scenario.Appointment.Status);
        Assert.Single(scenario.Audit.Entries);
    }

    /// <summary>Verifies no-show is rejected until the four-hour window has ended.</summary>
    [Fact]
    public async Task NoShowBeforeWindowEndIsRejected()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 12, 59, 0, TimeSpan.Zero));

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.NoShow, 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Empty(scenario.Audit.Entries);
    }

    /// <summary>Verifies check-in is rejected outside the event transitional-location date.</summary>
    [Fact]
    public async Task CheckInOnAnotherTransitionalLocationDateIsRejected()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 8, 9, 5, 0, TimeSpan.Zero));

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(BookingAppointmentStatus.Expected, scenario.Appointment.Status);
    }

    /// <summary>Verifies cancelled parent state blocks an otherwise valid transition.</summary>
    [Fact]
    public async Task CancelledBookingRejectsTheUpdate()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        scenario.Booking.Cancel();

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Empty(scenario.Audit.Entries);
    }

    /// <summary>Verifies a cancelled event blocks an otherwise valid transition.</summary>
    [Fact]
    public async Task CancelledEventRejectsTheUpdate()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        scenario.Event.Cancel();

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Empty(scenario.Audit.Entries);
    }

    /// <summary>Verifies terminal outcomes on the original Booking leave it Active.</summary>
    [Fact]
    public async Task OriginalTerminalOutcomesStayActive()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.Completed, 2),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Active, scenario.Booking.Status);
        Assert.DoesNotContain(scenario.Audit.Entries, e => e.Action == AuditAction.RecoveryBookingConcluded);
    }

    /// <summary>Verifies mixed recovery outcomes leave the recovery Booking Active.</summary>
    [Fact]
    public async Task MixedRecoveryOutcomesLeaveRecoveryActive()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        var (_, first) = AddRecovery(scenario, scenario.Booking.CreatedAt.AddHours(1));
        first.TransitionTo(
            BookingAppointmentStatus.CheckedIn, scenario.StaffUserId,
            new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero), true, false);
        first.TransitionTo(
            BookingAppointmentStatus.Completed, scenario.StaffUserId,
            new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero), false, false);
        var other = BookingAppointment.Create(
            Guid.NewGuid(), scenario.Bookings.Items.Single(b => !b.IsOriginal).Id,
            AppointmentTypeIds.DrugAndAlcoholTesting);
        scenario.Appointments.Add(other);

        var result = await scenario.Handler.HandleAsync(
            ForAppointment(
                scenario, other.Id, BookingAppointmentStatus.CheckedIn, other.Version),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            BookingStatus.Active,
            scenario.Bookings.Items.Single(b => !b.IsOriginal).Status);
        Assert.DoesNotContain(scenario.Audit.Entries, e => e.Action == AuditAction.RecoveryBookingConcluded);
    }

    /// <summary>Verifies the final terminal outcome concludes a recovery Booking.</summary>
    [Fact]
    public async Task FinalOutcomeConcludesRecovery()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 14, 0, 0, TimeSpan.Zero));
        var (_, appointment) = AddRecovery(scenario, scenario.Booking.CreatedAt.AddHours(1));

        var result = await scenario.Handler.HandleAsync(
            ForAppointment(
                scenario, appointment.Id, BookingAppointmentStatus.NoShow, appointment.Version),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            BookingStatus.Concluded,
            scenario.Bookings.Items.Single(b => !b.IsOriginal).Status);
        Assert.Equal(BookingStatus.Active, scenario.Booking.Status);
        Assert.True(scenario.Audit.Contains(AuditAction.RecoveryBookingConcluded));
        Assert.True(scenario.Audit.Contains(AuditAction.AppointmentMarkedNoShow));
    }

    /// <summary>Verifies correcting a concluded recovery reopens it with fixed statuses.</summary>
    [Fact]
    public async Task AllowedCorrectionReopensConcludedRecovery()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        var (recovery, appointment) = AddRecovery(scenario, scenario.Booking.CreatedAt.AddHours(1));
        appointment.TransitionTo(
            BookingAppointmentStatus.NoShow, scenario.StaffUserId,
            new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero), false, true);
        recovery.Conclude();

        var result = await scenario.Handler.HandleAsync(
            ForAppointment(
                scenario, appointment.Id, BookingAppointmentStatus.Expected, appointment.Version),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Active, recovery.Status);
        var entry = Assert.Single(
            scenario.Audit.Entries, e => e.Action == AuditAction.AppointmentStatusCorrected);
        Assert.Contains(";booking:Concluded->Active", entry.Details);
        Assert.DoesNotContain(scenario.Audit.Entries, e => e.Action == AuditAction.RecoveryBookingConcluded);
    }

    /// <summary>Verifies a correction behind a pending recovery invite is rejected.</summary>
    [Fact]
    public async Task GuardedCorrectionBehindPendingInviteRequiresCancellingRecoveryFirst()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        var (recovery, appointment) = AddRecovery(scenario, scenario.Booking.CreatedAt.AddHours(1));
        appointment.TransitionTo(
            BookingAppointmentStatus.NoShow, scenario.StaffUserId,
            new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero), false, true);
        scenario.Invites.Add(Invite.CreateRecovery(
            Guid.NewGuid(), scenario.Attendee.Id, scenario.Booking.Id, "pending-recovery",
            new DateTimeOffset(2026, 9, 9, 9, 0, 0, TimeSpan.Zero),
            [scenario.Event.Id, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting]));

        var result = await scenario.Handler.HandleAsync(
            ForAppointment(
                scenario, appointment.Id, BookingAppointmentStatus.Expected, appointment.Version),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Contains("Cancel the recovery first", result.Error.Message);
        Assert.Equal(BookingAppointmentStatus.NoShow, appointment.Status);
        Assert.Equal(BookingStatus.Active, recovery.Status);
    }

    /// <summary>Verifies a correction behind a later recovery Booking is rejected.</summary>
    [Fact]
    public async Task GuardedCorrectionBehindLaterBookingRequiresCancellingRecoveryFirst()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        scenario.Appointment.TransitionTo(
            BookingAppointmentStatus.NoShow, scenario.StaffUserId,
            new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero), false, true);
        AddRecovery(scenario, scenario.Booking.CreatedAt.AddHours(1));

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.Expected, scenario.Appointment.Version),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Contains("Cancel the recovery first", result.Error.Message);
        Assert.Equal(BookingAppointmentStatus.NoShow, scenario.Appointment.Status);
    }

    /// <summary>Verifies capability denial occurs before the scoped appointment locator.</summary>
    [Fact]
    public async Task AdminIsDeniedBeforeAppointmentDataAccess()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        var admin = Guid.NewGuid();
        scenario.Profiles.Add(StaffAccessProfile.Create(admin, [Role.Admin], null));

        var result = await scenario.Handler.HandleAsync(
            new UpdateBookingAppointmentStatusCommand
            {
                StaffUserId = admin,
                BookingAppointmentId = scenario.Appointment.Id,
                Status = BookingAppointmentStatus.CheckedIn,
                ExpectedVersion = 1,
            },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.DoesNotContain("appointment-located", scenario.Operations.Events);
    }

    /// <summary>Verifies another appointment type is hidden behind the stable not-found result.</summary>
    [Fact]
    public async Task CrossTypeRecordIsNotFound()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        var otherStaff = Guid.NewGuid();
        scenario.Profiles.Add(StaffAccessProfile.Create(
            otherStaff, [Role.Manager], AppointmentTypeIds.MedicalCheckUp));

        var result = await scenario.Handler.HandleAsync(
            new UpdateBookingAppointmentStatusCommand
            {
                StaffUserId = otherStaff,
                BookingAppointmentId = scenario.Appointment.Id,
                Status = BookingAppointmentStatus.CheckedIn,
                ExpectedVersion = 1,
            },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    private static UpdateBookingAppointmentStatusCommand Command(
        Scenario scenario,
        BookingAppointmentStatus status,
        long version) => ForAppointment(scenario, scenario.Appointment.Id, status, version);

    private static UpdateBookingAppointmentStatusCommand ForAppointment(
        Scenario scenario,
        Guid appointmentId,
        BookingAppointmentStatus status,
        long version) => new()
    {
        StaffUserId = scenario.StaffUserId,
        BookingAppointmentId = appointmentId,
        Status = status,
        ExpectedVersion = version,
    };

    private static (Booking Recovery, BookingAppointment Appointment) AddRecovery(
        Scenario scenario,
        DateTimeOffset recoveryCreatedAt)
    {
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(), scenario.Attendee.Id, scenario.Booking.Id, $"recovery-{Guid.NewGuid():N}",
            recoveryCreatedAt.AddDays(2),
            [scenario.Event.Id, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        scenario.Invites.Add(recoveryInvite);
        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, scenario.Booking, scenario.Event.Id,
            $"manage-{Guid.NewGuid():N}", recoveryCreatedAt);
        scenario.Bookings.Add(recovery);
        recoveryInvite.MarkUsed();
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.DrugAndAlcoholTesting);
        scenario.Appointments.Add(appointment);

        return (recovery, appointment);
    }

    /// <summary>Builds a DAT-only group; the scenario needs a mapping, not an identity.</summary>
    private static AttendeeGroup DatOnly() =>
        AttendeeGroup.Define(
            Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);

    private static Scenario GivenScenario(DateTimeOffset now)
    {
        var staff = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            staff, [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting));
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "amara@example.com", DatOnly());
        var operations = new TransactionOperationLog();
        var attendees = new InMemoryAttendeeRepository(operations);
        attendees.Add(attendee);
        var eventItem = Event.CreateImported(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 7), new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 10));
        var events = new InMemoryEventRepository(operations);
        events.Add(eventItem);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, "invite-token", now.AddDays(1),
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()], attendee.RequiredAppointmentTypeIds, 0);
        var invites = new InMemoryInviteRepository(operations);
        invites.Add(invite);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, "manage-token", now.AddDays(-1));
        var bookings = new InMemoryBookingRepository(operations);
        bookings.Add(booking);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting);
        var appointments = new InMemoryBookingAppointmentRepository(bookings, operations);
        appointments.Add(appointment);
        var audit = new RecordingAuditLogger();
        var unitOfWork = new FakeUnitOfWork(operations);
        var clock = new FakeClock(now);
        var handler = new UpdateBookingAppointmentStatusHandler(
            new StaffAccessAuthorizer(profiles),
            appointments,
            bookings,
            attendees,
            invites,
            events,
            new RecoveryBookingOutcomeCoordinator(),
            audit,
            unitOfWork,
            clock);
        return new Scenario(
            staff, profiles, attendee, eventItem, appointment, booking,
            attendees, invites, bookings, appointments,
            audit, unitOfWork, operations, handler);
    }

    private sealed record Scenario(
        Guid StaffUserId,
        InMemoryStaffAccessProfileRepository Profiles,
        Attendee Attendee,
        Event Event,
        BookingAppointment Appointment,
        Booking Booking,
        InMemoryAttendeeRepository Attendees,
        InMemoryInviteRepository Invites,
        InMemoryBookingRepository Bookings,
        InMemoryBookingAppointmentRepository Appointments,
        RecordingAuditLogger Audit,
        FakeUnitOfWork UnitOfWork,
        TransactionOperationLog Operations,
        UpdateBookingAppointmentStatusHandler Handler);
}
`````

## before — tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs — 1/1

<!-- vocabulary-file: {"id":249,"oldPath":"tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs","newPath":"tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs","beforeSha":"d067f51765a63bef4028031b2892a862ef0c317d6ff8382971ffab02ce5ecd4a","afterSha":"9fd8464eac7af500787829d2db4eed8b484b1207933398a1da05f4050ca73f41","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies booking confirmation snapshots required operational appointments atomically.</summary>
public sealed class BookingAppointmentSnapshotTests
{
    /// <summary>Verifies one Expected appointment is created for each current candidate requirement.</summary>
    [Fact]
    public async Task ConfirmationCreatesOneAppointmentPerRequirementBeforeTheSingleSave()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 7, 9, 0, 0, TimeSpan.Zero));
        var tokens = new FakeTokenService();
        var candidates = new InMemoryCandidateRepository();
        var pilots = EmployeeGroup.Define(
            EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        var candidate = Candidate.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "amara@example.com",
            pilots);
        candidates.Add(candidate);
        candidate.MarkInvited();

        var slots = new InMemoryConfirmedSlotRepository();
        var selected = AddSlot(slots, new DateOnly(2026, 9, 8));
        var second = AddSlot(slots, new DateOnly(2026, 9, 9));
        var third = AddSlot(slots, new DateOnly(2026, 9, 10));
        var inviteId = Guid.NewGuid();
        var token = tokens.Issue(inviteId);
        var invites = new InMemoryInviteRepository();
        invites.Add(Invite.CreateInitial(
            inviteId,
            candidate.Id,
            token.TokenHash,
            clock.UtcNow.AddDays(4),
            [selected.Id, second.Id, third.Id],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            0));

        var bookings = new InMemoryBookingRepository();
        var appointments = new InMemoryBookingAppointmentRepository(bookings);
        var unitOfWork = new FakeUnitOfWork();
        var deliveries = EmailDeliveryTestFactory.Create(
            new InMemoryEmailDeliveryRepository(),
            new RecordingEmailSender(),
            new FakeUnitOfWork(),
            clock);
        var handler = new ConfirmBookingHandler(
            invites,
            candidates,
            slots,
            bookings,
            appointments,
            new InMemorySlotCapacityRepository(slots),
            new EligibleSlotFinder(slots, clock),
            tokens,
            deliveries,
            new RecordingAuditLogger(),
            unitOfWork,
            clock,
            new CandidatePortalOptions(
                "https://booking.example.com", "Head office", "help@example.com"));

        var result = await handler.HandleAsync(
            new ConfirmBookingCommand(token.Token, selected.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, appointments.Items.Count);
        Assert.All(appointments.Items, value =>
        {
            Assert.Equal(result.Value.BookingId, value.BookingId);
            Assert.Equal(BookingAppointmentStatus.Expected, value.Status);
            Assert.Equal(1, value.Version);
        });
        Assert.Equal(
            candidate.RequiredAppointmentTypeIds.OrderBy(value => value),
            appointments.Items.Select(value => value.AppointmentTypeId).OrderBy(value => value));
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Equal(1, unitOfWork.CommitCount);
    }

    /// <summary>Verifies one-, two-, and three-type snapshots each produce their exact set.</summary>
    [Theory]
    [InlineData("MED")]
    [InlineData("DAT,UNI")]
    [InlineData("DAT,MED,UNI")]
    public async Task ConfirmationCreatesTheExactSnapshotSet(string codes)
    {
        var byCode = new Dictionary<string, Guid>(StringComparer.Ordinal)
        {
            ["DAT"] = AppointmentTypeIds.DrugAndAlcoholTesting,
            ["MED"] = AppointmentTypeIds.MedicalCheckUp,
            ["UNI"] = AppointmentTypeIds.UniformFitting,
        };
        var snapshot = codes.Split(',').Select(code => byCode[code]).ToList();

        var clock = new FakeClock(new DateTimeOffset(2026, 9, 7, 9, 0, 0, TimeSpan.Zero));
        var tokens = new FakeTokenService();
        var candidates = new InMemoryCandidateRepository();
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", "amara@example.com",
            EmployeeGroup.Define(
                Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting]));
        candidates.Add(candidate);
        candidate.MarkInvited();

        var slots = new InMemoryConfirmedSlotRepository();
        var selected = AddSlot(slots, new DateOnly(2026, 9, 8));
        var inviteId = Guid.NewGuid();
        var token = tokens.Issue(inviteId);
        var invites = new InMemoryInviteRepository();
        invites.Add(Invite.CreateInitial(
            inviteId, candidate.Id, token.TokenHash, clock.UtcNow.AddDays(4),
            [selected.Id, AddSlot(slots, new DateOnly(2026, 9, 9)).Id,
                AddSlot(slots, new DateOnly(2026, 9, 10)).Id],
            snapshot, 0));

        var bookings = new InMemoryBookingRepository();
        var appointments = new InMemoryBookingAppointmentRepository(bookings);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ConfirmBookingHandler(
            invites,
            candidates,
            slots,
            bookings,
            appointments,
            new InMemorySlotCapacityRepository(slots),
            new EligibleSlotFinder(slots, clock),
            tokens,
            EmailDeliveryTestFactory.Create(
                new InMemoryEmailDeliveryRepository(),
                new RecordingEmailSender(),
                new FakeUnitOfWork(),
                clock),
            new RecordingAuditLogger(),
            unitOfWork,
            clock,
            new CandidatePortalOptions(
                "https://booking.example.com", "Head office", "help@example.com"));

        // Each snapshot size needs its matching group so confirmation proceeds.
        var group = snapshot.Count switch
        {
            1 => EmployeeGroup.Define(
                EmployeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
                [AppointmentTypeIds.MedicalCheckUp]),
            2 => EmployeeGroup.Define(
                EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]),
            _ => EmployeeGroup.Define(
                EmployeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                    AppointmentTypeIds.UniformFitting]),
        };
        candidate.AssignEmployeeGroup(group);

        var result = await handler.HandleAsync(
            new ConfirmBookingCommand(token.Token, selected.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            snapshot.OrderBy(value => value),
            appointments.Items.Select(value => value.AppointmentTypeId).OrderBy(value => value));
    }

    private static ConfirmedSlot AddSlot(
        InMemoryConfirmedSlotRepository slots,
        DateOnly date)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(date, new TimeOnly(9, 0)), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 10);
        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);
        slots.Add(slot);
        return slot;
    }
}
`````

## after — tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs — 1/1

<!-- vocabulary-file: {"id":249,"oldPath":"tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs","newPath":"tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs","beforeSha":"d067f51765a63bef4028031b2892a862ef0c317d6ff8382971ffab02ce5ecd4a","afterSha":"9fd8464eac7af500787829d2db4eed8b484b1207933398a1da05f4050ca73f41","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies booking confirmation snapshots required operational appointments atomically.</summary>
public sealed class BookingAppointmentSnapshotTests
{
    /// <summary>Verifies one Expected appointment is created for each current attendee requirement.</summary>
    [Fact]
    public async Task ConfirmationCreatesOneAppointmentPerRequirementBeforeTheSingleSave()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 7, 9, 0, 0, TimeSpan.Zero));
        var tokens = new FakeTokenService();
        var attendees = new InMemoryAttendeeRepository();
        var pilots = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "amara@example.com",
            pilots);
        attendees.Add(attendee);
        attendee.MarkInvited();

        var events = new InMemoryEventRepository();
        var selected = AddEvent(events, new DateOnly(2026, 9, 8));
        var second = AddEvent(events, new DateOnly(2026, 9, 9));
        var third = AddEvent(events, new DateOnly(2026, 9, 10));
        var inviteId = Guid.NewGuid();
        var token = tokens.Issue(inviteId);
        var invites = new InMemoryInviteRepository();
        invites.Add(Invite.CreateInitial(
            inviteId,
            attendee.Id,
            token.TokenHash,
            clock.UtcNow.AddDays(4),
            [selected.Id, second.Id, third.Id],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            0));

        var bookings = new InMemoryBookingRepository();
        var appointments = new InMemoryBookingAppointmentRepository(bookings);
        var unitOfWork = new FakeUnitOfWork();
        var deliveries = EmailDeliveryTestFactory.Create(
            new InMemoryEmailDeliveryRepository(),
            new RecordingEmailSender(),
            new FakeUnitOfWork(),
            clock);
        var handler = new ConfirmBookingHandler(
            invites,
            attendees,
            events,
            bookings,
            appointments,
            new InMemoryEventCapacityRepository(events),
            new EligibleEventFinder(events, clock),
            tokens,
            deliveries,
            new RecordingAuditLogger(),
            unitOfWork,
            clock,
            new AttendeePortalOptions(
                "https://booking.example.com", "Head office", "help@example.com"));

        var result = await handler.HandleAsync(
            new ConfirmBookingCommand(token.Token, selected.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, appointments.Items.Count);
        Assert.All(appointments.Items, value =>
        {
            Assert.Equal(result.Value.BookingId, value.BookingId);
            Assert.Equal(BookingAppointmentStatus.Expected, value.Status);
            Assert.Equal(1, value.Version);
        });
        Assert.Equal(
            attendee.RequiredAppointmentTypeIds.OrderBy(value => value),
            appointments.Items.Select(value => value.AppointmentTypeId).OrderBy(value => value));
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Equal(1, unitOfWork.CommitCount);
    }

    /// <summary>Verifies one-, two-, and three-type snapshots each produce their exact set.</summary>
    [Theory]
    [InlineData("MED")]
    [InlineData("DAT,UNI")]
    [InlineData("DAT,MED,UNI")]
    public async Task ConfirmationCreatesTheExactSnapshotSet(string codes)
    {
        var byCode = new Dictionary<string, Guid>(StringComparer.Ordinal)
        {
            ["DAT"] = AppointmentTypeIds.DrugAndAlcoholTesting,
            ["MED"] = AppointmentTypeIds.MedicalCheckUp,
            ["UNI"] = AppointmentTypeIds.UniformFitting,
        };
        var snapshot = codes.Split(',').Select(code => byCode[code]).ToList();

        var clock = new FakeClock(new DateTimeOffset(2026, 9, 7, 9, 0, 0, TimeSpan.Zero));
        var tokens = new FakeTokenService();
        var attendees = new InMemoryAttendeeRepository();
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "amara@example.com",
            AttendeeGroup.Define(
                Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting]));
        attendees.Add(attendee);
        attendee.MarkInvited();

        var events = new InMemoryEventRepository();
        var selected = AddEvent(events, new DateOnly(2026, 9, 8));
        var inviteId = Guid.NewGuid();
        var token = tokens.Issue(inviteId);
        var invites = new InMemoryInviteRepository();
        invites.Add(Invite.CreateInitial(
            inviteId, attendee.Id, token.TokenHash, clock.UtcNow.AddDays(4),
            [selected.Id, AddEvent(events, new DateOnly(2026, 9, 9)).Id,
                AddEvent(events, new DateOnly(2026, 9, 10)).Id],
            snapshot, 0));

        var bookings = new InMemoryBookingRepository();
        var appointments = new InMemoryBookingAppointmentRepository(bookings);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ConfirmBookingHandler(
            invites,
            attendees,
            events,
            bookings,
            appointments,
            new InMemoryEventCapacityRepository(events),
            new EligibleEventFinder(events, clock),
            tokens,
            EmailDeliveryTestFactory.Create(
                new InMemoryEmailDeliveryRepository(),
                new RecordingEmailSender(),
                new FakeUnitOfWork(),
                clock),
            new RecordingAuditLogger(),
            unitOfWork,
            clock,
            new AttendeePortalOptions(
                "https://booking.example.com", "Head office", "help@example.com"));

        // Each snapshot size needs its matching group so confirmation proceeds.
        var group = snapshot.Count switch
        {
            1 => AttendeeGroup.Define(
                AttendeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
                [AppointmentTypeIds.MedicalCheckUp]),
            2 => AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]),
            _ => AttendeeGroup.Define(
                AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                    AppointmentTypeIds.UniformFitting]),
        };
        attendee.AssignAttendeeGroup(group);

        var result = await handler.HandleAsync(
            new ConfirmBookingCommand(token.Token, selected.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            snapshot.OrderBy(value => value),
            appointments.Items.Select(value => value.AppointmentTypeId).OrderBy(value => value));
    }

    private static Event AddEvent(
        InMemoryEventRepository events,
        DateOnly date)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(9, 0)), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 10);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        events.Add(eventItem);
        return eventItem;
    }
}
`````
