# 02a — Deterministic attendee links and the token version counter, edits 14 (Task 9a)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.Application.Tests/Appointments/RecentPastRecoveryEligibilityTests.cs — 1/1

<!-- retirement-file: {"id":31,"file":"tests/EventBooking.Application.Tests/Appointments/RecentPastRecoveryEligibilityTests.cs","beforeSha":"eee9ebd55ca5ca74125a05296ce41251af6fb91c4dad331cd5eb42d088b83469","afterSha":"b302dfd207a867f52b4fa1550ae9fadce3c912391e58bc747c8cafdc4a54b92d","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Appointments;
using EventBooking.Application.Attendees;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Appointments;

/// <summary>Verifies a late-recorded outcome makes its requirement recoverable.</summary>
public sealed class RecentPastRecoveryEligibilityTests
{
    /// <summary>Verifies the late NoShow latest attempt reads as outstanding and recoverable.</summary>
    [Fact]
    public async Task LateNoShowMakesRequirementRecoverable()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 8, 9, 0, 0, TimeSpan.Zero));
        var outcome = await scenario.Handler.HandleAsync(
            new UpdateBookingAppointmentStatusCommand
            {
                StaffUserId = scenario.StaffUserId,
                BookingAppointmentId = scenario.Appointment.Id,
                Status = BookingAppointmentStatus.NoShow,
                ExpectedVersion = 1,
            },
            CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        var readiness = new AttendeeReadinessCalculator().Calculate(
            new AttendeeReadinessSnapshot(
                scenario.Attendee.Id,
                Guid.NewGuid(),
                [AppointmentTypeIds.DrugAndAlcoholTesting],
                scenario.Booking.Id,
                [
                    new AttendeeReadinessAttempt(
                        scenario.Appointment.Id,
                        AppointmentTypeIds.DrugAndAlcoholTesting,
                        scenario.Appointment.Status,
                        scenario.Booking.Id,
                        scenario.Booking.Status,
                        outcome.Value.OutcomeAt!.Value),
                ]));

        Assert.Equal(AttendeeReadinessCode.AppointmentsOutstanding, readiness.Code);
        var outstanding = Assert.Single(readiness.OutstandingAppointmentTypes);
        Assert.True(outstanding.IsRecoverable);
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
            Guid.NewGuid(),
            "Amara Novak",
            "amara@example.com",
            DatOnly(),
            ProposalFixture.Now);
        var operations = new TransactionOperationLog();
        var attendees = new InMemoryAttendeeRepository(operations);
        attendees.Add(attendee);
        var eventItem = EventFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 7), new TimeOnly(9, 0), 240),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 10));
        var events = new InMemoryEventRepository(operations);
        events.Add(eventItem);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            now.AddDays(1),
            [ProposalFixture.LocationId],
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        var invites = new InMemoryInviteRepository(operations);
        invites.Add(invite);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, now.AddDays(-1));
        var bookings = new InMemoryBookingRepository(operations);
        bookings.Add(booking);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting);
        var appointments = new InMemoryBookingAppointmentRepository(bookings, operations);
        appointments.Add(appointment);
        var handler = new UpdateBookingAppointmentStatusHandler(
            new StaffAccessAuthorizer(profiles),
            appointments,
            bookings,
            attendees,
            invites,
            events,
            new RecoveryBookingOutcomeCoordinator(),
            new RecordingAuditLogger(),
            new FakeUnitOfWork(operations),
            new FakeClock(now));
        return new Scenario(staff, attendee, booking, appointment, handler);
    }

    private sealed record Scenario(
        Guid StaffUserId,
        Attendee Attendee,
        Booking Booking,
        BookingAppointment Appointment,
        UpdateBookingAppointmentStatusHandler Handler);
}
`````

## before — tests/EventBooking.Application.Tests/Appointments/RecoveryBookingOutcomeCoordinatorTests.cs — 1/1

<!-- retirement-file: {"id":32,"file":"tests/EventBooking.Application.Tests/Appointments/RecoveryBookingOutcomeCoordinatorTests.cs","beforeSha":"f0cf8492d69e801f0ce5ced07d9a0404f14e72713cc11078c1b5ffce9febfc86","afterSha":"658a29be70e9510ad0e74a115f164625ee240aed24eb0bf74fa6852f141f0fa7","side":"before","part":1,"parts":1} -->

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
            Guid.NewGuid(),
            Guid.NewGuid(),
            "initial",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [originalEvent, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp],
            0);
        var original = Booking.Create(
            Guid.NewGuid(), initial, originalEvent, "original", DateTimeOffset.UtcNow);
        var recoveryEvent = Guid.NewGuid();
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(),
            original.AttendeeId,
            original.Id,
            "recovery",
            DateTimeOffset.UtcNow.AddDays(2),
            ProposalFixture.LocationId,
            null,
            [recoveryEvent, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp]);
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
            Guid.NewGuid(),
            Guid.NewGuid(),
            "initial",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [originalEvent, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp],
            0);
        var original = Booking.Create(Guid.NewGuid(), initial, originalEvent, "root", DateTimeOffset.UtcNow);
        var recoveryEvent = Guid.NewGuid();
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(),
            original.AttendeeId,
            original.Id,
            "recovery",
            DateTimeOffset.UtcNow.AddDays(1),
            ProposalFixture.LocationId,
            null,
            [recoveryEvent, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp]);
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

## after — tests/EventBooking.Application.Tests/Appointments/RecoveryBookingOutcomeCoordinatorTests.cs — 1/1

<!-- retirement-file: {"id":32,"file":"tests/EventBooking.Application.Tests/Appointments/RecoveryBookingOutcomeCoordinatorTests.cs","beforeSha":"f0cf8492d69e801f0ce5ced07d9a0404f14e72713cc11078c1b5ffce9febfc86","afterSha":"658a29be70e9510ad0e74a115f164625ee240aed24eb0bf74fa6852f141f0fa7","side":"after","part":1,"parts":1} -->

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
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [originalEvent, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp],
            0);
        var original = Booking.Create(
            Guid.NewGuid(), initial, originalEvent, DateTimeOffset.UtcNow);
        var recoveryEvent = Guid.NewGuid();
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(),
            original.AttendeeId,
            original.Id,
            DateTimeOffset.UtcNow.AddDays(2),
            ProposalFixture.LocationId,
            null,
            [recoveryEvent, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp]);
        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, recoveryEvent, DateTimeOffset.UtcNow);
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
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [originalEvent, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp],
            0);
        var original = Booking.Create(Guid.NewGuid(), initial, originalEvent, DateTimeOffset.UtcNow);
        var recoveryEvent = Guid.NewGuid();
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(),
            original.AttendeeId,
            original.Id,
            DateTimeOffset.UtcNow.AddDays(1),
            ProposalFixture.LocationId,
            null,
            [recoveryEvent, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp]);
        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, recoveryEvent, DateTimeOffset.UtcNow);
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

<!-- retirement-file: {"id":33,"file":"tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs","beforeSha":"20d9bbc81567898fa7e6d7da65c0ed44e47df433a4ac0f57a76c31a3315ae7ee","afterSha":"3d4add95149d895ef60bc06f7c61a3b5b7794cd971a35702ed4c0e7644a2e5df","side":"before","part":1,"parts":1} -->

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
        scenario.Event.CancelBeforeStart();

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
            Guid.NewGuid(),
            scenario.Attendee.Id,
            scenario.Booking.Id,
            "pending-recovery",
            new DateTimeOffset(2026, 9, 9, 9, 0, 0, TimeSpan.Zero),
            ProposalFixture.LocationId,
            null,
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
            Guid.NewGuid(),
            scenario.Attendee.Id,
            scenario.Booking.Id,
            $"recovery-{Guid.NewGuid():N}",
            recoveryCreatedAt.AddDays(2),
            ProposalFixture.LocationId,
            null,
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
            Guid.NewGuid(),
            "Amara Novak",
            "amara@example.com",
            DatOnly(),
            ProposalFixture.Now);
        var operations = new TransactionOperationLog();
        var attendees = new InMemoryAttendeeRepository(operations);
        attendees.Add(attendee);
        var eventItem = EventFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 7), new TimeOnly(9, 0), 240),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 10));
        var events = new InMemoryEventRepository(operations);
        events.Add(eventItem);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            "invite-token",
            now.AddDays(1),
            [ProposalFixture.LocationId],
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
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

## after — tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs — 1/1

<!-- retirement-file: {"id":33,"file":"tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs","beforeSha":"20d9bbc81567898fa7e6d7da65c0ed44e47df433a4ac0f57a76c31a3315ae7ee","afterSha":"3d4add95149d895ef60bc06f7c61a3b5b7794cd971a35702ed4c0e7644a2e5df","side":"after","part":1,"parts":1} -->

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
        scenario.Event.CancelBeforeStart();

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
            Guid.NewGuid(),
            scenario.Attendee.Id,
            scenario.Booking.Id,
            new DateTimeOffset(2026, 9, 9, 9, 0, 0, TimeSpan.Zero),
            ProposalFixture.LocationId,
            null,
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
            Guid.NewGuid(),
            scenario.Attendee.Id,
            scenario.Booking.Id,
            recoveryCreatedAt.AddDays(2),
            ProposalFixture.LocationId,
            null,
            [scenario.Event.Id, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        scenario.Invites.Add(recoveryInvite);
        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, scenario.Booking, scenario.Event.Id, recoveryCreatedAt);
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
            Guid.NewGuid(),
            "Amara Novak",
            "amara@example.com",
            DatOnly(),
            ProposalFixture.Now);
        var operations = new TransactionOperationLog();
        var attendees = new InMemoryAttendeeRepository(operations);
        attendees.Add(attendee);
        var eventItem = EventFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 7), new TimeOnly(9, 0), 240),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 10));
        var events = new InMemoryEventRepository(operations);
        events.Add(eventItem);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            now.AddDays(1),
            [ProposalFixture.LocationId],
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        var invites = new InMemoryInviteRepository(operations);
        invites.Add(invite);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, now.AddDays(-1));
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

## before — tests/EventBooking.Application.Tests/Attendees/ActiveBookingRequirementTests.cs — 1/1

<!-- retirement-file: {"id":34,"file":"tests/EventBooking.Application.Tests/Attendees/ActiveBookingRequirementTests.cs","beforeSha":"dd64c4955b92e8785f8208a581caaf575bf7ee99a9aa11eabc9c2984820c2280","afterSha":"823047658429423a6c55aec004765f0d78c5415f01da8ef6f0e20995ffdcd00c","side":"before","part":1,"parts":1} -->

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

/// <summary>Verifies attendee requirements cannot drift away from an active booking snapshot.</summary>
public sealed class ActiveBookingRequirementTests
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

    /// <summary>Verifies a changed set conflicts before attendee details or requirements mutate.</summary>
    [Fact]
    public async Task ChangedRequirementsAreRejectedBeforeAnyAttendeeMutation()
    {
        var (handler, attendee, coordinator) = GivenActiveBooking();

        var result = await handler.UpdateAsync(
            new UpdateAttendeeCommand(
                coordinator,
                attendee.Id,
                "Changed Name",
                "changed@example.com",
                Pilots.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("attendee_group_active_booking_conflict", result.Error.Code);
        Assert.Equal(
            "Appointment requirements cannot change while the attendee has an active booking. Cancel and rebook first.",
            result.Error.Message);
        Assert.Equal("Amara Novak", attendee.Name);
        Assert.Equal("amara@example.com", attendee.Email);
        Assert.Equal(
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting],
            attendee.RequiredAppointmentTypeIds);
    }

    /// <summary>Verifies a set-equivalent group still permits name and email correction.</summary>
    [Fact]
    public async Task SameRequirementSetInAnotherGroupAllowsDetailCorrection()
    {
        var (handler, attendee, coordinator) = GivenActiveBooking();

        var result = await handler.UpdateAsync(
            new UpdateAttendeeCommand(
                coordinator,
                attendee.Id,
                "Amara N. Novak",
                "amara.novak@example.com",
                GroundTransport.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Amara N. Novak", attendee.Name);
        Assert.Equal("amara.novak@example.com", attendee.Email);
    }

    private static (SaveAttendeeHandler Handler, Attendee Attendee, Guid Coordinator)
        GivenActiveBooking()
    {
        var coordinator = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(coordinator, [Role.Coordinator], null));
        var groups = new InMemoryAttendeeGroupRepository();
        groups.Items.AddRange([CabinCrew, GroundTransport, Pilots]);
        var attendees = new InMemoryAttendeeRepository();
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "amara@example.com",
            CabinCrew,
            ProposalFixture.Now);
        attendees.Add(attendee);
        var bookings = new InMemoryBookingRepository();
        bookings.Add(NewBooking(attendee));

        return (
            new SaveAttendeeHandler(
                attendees,
                groups,
                new InMemoryInviteRepository(),
                bookings,
                new StaffAccessAuthorizer(profiles),
                new RecordingAuditLogger(),
                new FakeClock(),
                new FakeUnitOfWork()),
            attendee,
            coordinator);
    }

    private static Booking NewBooking(Attendee attendee)
    {
        var eventId = Guid.NewGuid();
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            "invite-token-hash",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        return Booking.Create(
            Guid.NewGuid(), invite, eventId, "manage-token-hash", DateTimeOffset.UtcNow);
    }
}
`````
