# 00d — Retire direct event import, edits 13 (Task 3b)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs — 1/1

<!-- retirement-file: {"id":37,"file":"tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs","beforeSha":"018db007708853ac1bf7a21e59615aa0c7c7613e28a51be0749213d01cb6f0c0","afterSha":"68b738d73de373bf8f6b7fbc29552993a4ef68fa9207f3ac1cad56c0a3fde08c","side":"after","part":1,"parts":1} -->

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
        var eventItem = EventFixture.Create(
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

## before — tests/EventBooking.Application.Tests/Bookings/BookingSnapshotCancellationTests.cs — 1/1

<!-- retirement-file: {"id":38,"file":"tests/EventBooking.Application.Tests/Bookings/BookingSnapshotCancellationTests.cs","beforeSha":"25d9cb041f22526436d84f76310cf4f72aed46d21b941938655b75f2153a5968","afterSha":"b3f67365cff6757b944f24099e12db59f4ffd6ec716bfd2376055edb8ba7f2a2","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies cancellation returns capacity only for the addressed Booking snapshot.</summary>
public sealed class BookingSnapshotCancellationTests
{
    /// <summary>A one-type Booking returns only that type even when Attendee now has three.</summary>
    [Fact]
    public async Task CancellationUsesBookingAppointmentsInsteadOfAttendeeRequirements()
    {
        var eventItem = Event.CreateImported(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 10, 10), new TimeOnly(9, 0)),
            new Dictionary<Guid, int>
            {
                [AppointmentTypeIds.DrugAndAlcoholTesting] = 5,
                [AppointmentTypeIds.MedicalCheckUp] = 5,
                [AppointmentTypeIds.UniformFitting] = 5,
            });
        eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).Decrement();
        var group = AttendeeGroup.Define(
            AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting]);
        var attendee = Attendee.Create(Guid.NewGuid(), "Amara", "amara@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, "hash", DateTimeOffset.UtcNow.AddDays(1),
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        var booking = Booking.Create(Guid.NewGuid(), invite, eventItem.Id, "manage", DateTimeOffset.UtcNow);
        var appointments = new InMemoryBookingAppointmentRepository(new InMemoryBookingRepository());
        appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.MedicalCheckUp));
        var events = new InMemoryEventRepository();
        events.Items.Add(eventItem);
        var capacities = new InMemoryEventCapacityRepository(events);
        var canceller = new BookingCanceller(
            appointments, capacities, new RecordingAuditLogger());

        var result = await canceller.CancelLockedAsync(
            booking, eventItem, EventBooking.Domain.Audit.ActorType.System, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([AppointmentTypeIds.MedicalCheckUp], result.Value);
        Assert.Equal(5, eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(5, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(5, eventItem.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
    }

    /// <summary>A Booking addressing a dropped capacity row fails with the conflict code.</summary>
    [Fact]
    public async Task CancellationForDroppedTypeFailsLikeFullCapacity()
    {
        var eventItem = Event.CreateImported(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 10, 10), new TimeOnly(9, 0)),
            new Dictionary<Guid, int>
            {
                [AppointmentTypeIds.DrugAndAlcoholTesting] = 5,
                [AppointmentTypeIds.MedicalCheckUp] = 5,
                [AppointmentTypeIds.UniformFitting] = 5,
            });
        eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).Decrement();
        var group = AttendeeGroup.Define(
            AttendeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT", "Ground Ops", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        var attendee = Attendee.Create(Guid.NewGuid(), "Amara", "amara@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, "hash", DateTimeOffset.UtcNow.AddDays(1),
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp], 0);
        var booking = Booking.Create(Guid.NewGuid(), invite, eventItem.Id, "manage", DateTimeOffset.UtcNow);
        var bookings = new InMemoryBookingRepository();
        bookings.Add(booking);
        var appointments = new InMemoryBookingAppointmentRepository(bookings);
        appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting));
        appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.MedicalCheckUp));
        var capacities = new RowDroppingCapacityRepository(
            eventItem, AppointmentTypeIds.MedicalCheckUp);
        var canceller = new BookingCanceller(
            appointments, capacities, new RecordingAuditLogger());

        var result = await canceller.CancelLockedAsync(
            booking, eventItem, ActorType.System, null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(BookingStatus.Active, booking.Status);
        Assert.Equal(4, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(4, eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
    }

    /// <summary>Simulates the capacity store losing one row the aggregate still references.</summary>
    private sealed class RowDroppingCapacityRepository(Event eventItem, Guid droppedTypeId)
        : EventBooking.Application.Abstractions.IEventCapacityRepository
    {
        public Task<IReadOnlyList<EventCapacity>> LockForUpdateAsync(
            Guid eventId,
            IReadOnlyCollection<Guid> appointmentTypeIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EventCapacity>>(
                appointmentTypeIds
                    .Where(id => id != droppedTypeId)
                    .OrderBy(id => id)
                    .Select(eventItem.CapacityFor)
                    .ToList());
    }
}
`````

## after — tests/EventBooking.Application.Tests/Bookings/BookingSnapshotCancellationTests.cs — 1/1

<!-- retirement-file: {"id":38,"file":"tests/EventBooking.Application.Tests/Bookings/BookingSnapshotCancellationTests.cs","beforeSha":"25d9cb041f22526436d84f76310cf4f72aed46d21b941938655b75f2153a5968","afterSha":"b3f67365cff6757b944f24099e12db59f4ffd6ec716bfd2376055edb8ba7f2a2","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies cancellation returns capacity only for the addressed Booking snapshot.</summary>
public sealed class BookingSnapshotCancellationTests
{
    /// <summary>A one-type Booking returns only that type even when Attendee now has three.</summary>
    [Fact]
    public async Task CancellationUsesBookingAppointmentsInsteadOfAttendeeRequirements()
    {
        var eventItem = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 10, 10), new TimeOnly(9, 0)),
            new Dictionary<Guid, int>
            {
                [AppointmentTypeIds.DrugAndAlcoholTesting] = 5,
                [AppointmentTypeIds.MedicalCheckUp] = 5,
                [AppointmentTypeIds.UniformFitting] = 5,
            });
        eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).Decrement();
        var group = AttendeeGroup.Define(
            AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting]);
        var attendee = Attendee.Create(Guid.NewGuid(), "Amara", "amara@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, "hash", DateTimeOffset.UtcNow.AddDays(1),
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        var booking = Booking.Create(Guid.NewGuid(), invite, eventItem.Id, "manage", DateTimeOffset.UtcNow);
        var appointments = new InMemoryBookingAppointmentRepository(new InMemoryBookingRepository());
        appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.MedicalCheckUp));
        var events = new InMemoryEventRepository();
        events.Items.Add(eventItem);
        var capacities = new InMemoryEventCapacityRepository(events);
        var canceller = new BookingCanceller(
            appointments, capacities, new RecordingAuditLogger());

        var result = await canceller.CancelLockedAsync(
            booking, eventItem, EventBooking.Domain.Audit.ActorType.System, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([AppointmentTypeIds.MedicalCheckUp], result.Value);
        Assert.Equal(5, eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(5, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(5, eventItem.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
    }

    /// <summary>A Booking addressing a dropped capacity row fails with the conflict code.</summary>
    [Fact]
    public async Task CancellationForDroppedTypeFailsLikeFullCapacity()
    {
        var eventItem = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 10, 10), new TimeOnly(9, 0)),
            new Dictionary<Guid, int>
            {
                [AppointmentTypeIds.DrugAndAlcoholTesting] = 5,
                [AppointmentTypeIds.MedicalCheckUp] = 5,
                [AppointmentTypeIds.UniformFitting] = 5,
            });
        eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).Decrement();
        var group = AttendeeGroup.Define(
            AttendeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT", "Ground Ops", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        var attendee = Attendee.Create(Guid.NewGuid(), "Amara", "amara@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, "hash", DateTimeOffset.UtcNow.AddDays(1),
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp], 0);
        var booking = Booking.Create(Guid.NewGuid(), invite, eventItem.Id, "manage", DateTimeOffset.UtcNow);
        var bookings = new InMemoryBookingRepository();
        bookings.Add(booking);
        var appointments = new InMemoryBookingAppointmentRepository(bookings);
        appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting));
        appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.MedicalCheckUp));
        var capacities = new RowDroppingCapacityRepository(
            eventItem, AppointmentTypeIds.MedicalCheckUp);
        var canceller = new BookingCanceller(
            appointments, capacities, new RecordingAuditLogger());

        var result = await canceller.CancelLockedAsync(
            booking, eventItem, ActorType.System, null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(BookingStatus.Active, booking.Status);
        Assert.Equal(4, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(4, eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
    }

    /// <summary>Simulates the capacity store losing one row the aggregate still references.</summary>
    private sealed class RowDroppingCapacityRepository(Event eventItem, Guid droppedTypeId)
        : EventBooking.Application.Abstractions.IEventCapacityRepository
    {
        public Task<IReadOnlyList<EventCapacity>> LockForUpdateAsync(
            Guid eventId,
            IReadOnlyCollection<Guid> appointmentTypeIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EventCapacity>>(
                appointmentTypeIds
                    .Where(id => id != droppedTypeId)
                    .OrderBy(id => id)
                    .Select(eventItem.CapacityFor)
                    .ToList());
    }
}
`````

## before — tests/EventBooking.Application.Tests/Bookings/InviteSnapshotAuthorityTests.cs — 1/1

<!-- retirement-file: {"id":39,"file":"tests/EventBooking.Application.Tests/Bookings/InviteSnapshotAuthorityTests.cs","beforeSha":"35b4fb4c75b8949fbfdb8f246095a16701873b2bee0f0ed6a902cb7698008a2a","afterSha":"d096f23fc81074156ce3f72433ac59da4b9508c146789adbd1404b1cc535c853","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies portal and confirmation treat Invite Requirements as immutable authority.</summary>
public sealed class InviteSnapshotAuthorityTests
{
    /// <summary>The portal displays snapshot names rather than a later Attendee collection.</summary>
    [Fact]
    public async Task ViewInviteUsesPersistedSnapshot()
    {
        var group = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        var attendee = Attendee.Create(Guid.NewGuid(), "Amara", "amara@example.com", group);
        var attendees = new InMemoryAttendeeRepository();
        attendees.Add(attendee);
        var events = new InMemoryEventRepository();
        var options = Enumerable.Range(0, 3).Select(index =>
            Event.CreateImported(
                Guid.NewGuid(), new EventWindow(new DateOnly(2026, 10, 10 + index), new TimeOnly(9, 0)),
                new Dictionary<Guid, int>
                {
                    [AppointmentTypeIds.DrugAndAlcoholTesting] = 5,
                    [AppointmentTypeIds.MedicalCheckUp] = 5,
                    [AppointmentTypeIds.UniformFitting] = 5,
                })).ToList();
        events.Items.AddRange(options);
        var tokens = new FakeTokenService();
        var issued = tokens.Issue(Guid.NewGuid());
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, issued.TokenHash, DateTimeOffset.Parse("2026-10-01T00:00:00Z"),
            options.Select(eventItem => eventItem.Id), [AppointmentTypeIds.MedicalCheckUp], 0);
        var invites = new InMemoryInviteRepository();
        invites.Add(invite);
        var clock = new FakeClock(DateTimeOffset.Parse("2026-09-20T00:00:00Z"));

        var result = await new ViewInviteHandler(
                invites, attendees, events, new EligibleEventFinder(events, clock),
                new RecordingAuditLogger(), new FakeUnitOfWork(), tokens, clock)
            .HandleAsync(new ViewInviteQuery(issued.Token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["Medical Check-up"], result.Value.AppointmentTypeNames);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Bookings/InviteSnapshotAuthorityTests.cs — 1/1

<!-- retirement-file: {"id":39,"file":"tests/EventBooking.Application.Tests/Bookings/InviteSnapshotAuthorityTests.cs","beforeSha":"35b4fb4c75b8949fbfdb8f246095a16701873b2bee0f0ed6a902cb7698008a2a","afterSha":"d096f23fc81074156ce3f72433ac59da4b9508c146789adbd1404b1cc535c853","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies portal and confirmation treat Invite Requirements as immutable authority.</summary>
public sealed class InviteSnapshotAuthorityTests
{
    /// <summary>The portal displays snapshot names rather than a later Attendee collection.</summary>
    [Fact]
    public async Task ViewInviteUsesPersistedSnapshot()
    {
        var group = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        var attendee = Attendee.Create(Guid.NewGuid(), "Amara", "amara@example.com", group);
        var attendees = new InMemoryAttendeeRepository();
        attendees.Add(attendee);
        var events = new InMemoryEventRepository();
        var options = Enumerable.Range(0, 3).Select(index =>
            EventFixture.Create(
                Guid.NewGuid(), new EventWindow(new DateOnly(2026, 10, 10 + index), new TimeOnly(9, 0)),
                new Dictionary<Guid, int>
                {
                    [AppointmentTypeIds.DrugAndAlcoholTesting] = 5,
                    [AppointmentTypeIds.MedicalCheckUp] = 5,
                    [AppointmentTypeIds.UniformFitting] = 5,
                })).ToList();
        events.Items.AddRange(options);
        var tokens = new FakeTokenService();
        var issued = tokens.Issue(Guid.NewGuid());
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, issued.TokenHash, DateTimeOffset.Parse("2026-10-01T00:00:00Z"),
            options.Select(eventItem => eventItem.Id), [AppointmentTypeIds.MedicalCheckUp], 0);
        var invites = new InMemoryInviteRepository();
        invites.Add(invite);
        var clock = new FakeClock(DateTimeOffset.Parse("2026-09-20T00:00:00Z"));

        var result = await new ViewInviteHandler(
                invites, attendees, events, new EligibleEventFinder(events, clock),
                new RecordingAuditLogger(), new FakeUnitOfWork(), tokens, clock)
            .HandleAsync(new ViewInviteQuery(issued.Token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["Medical Check-up"], result.Value.AppointmentTypeNames);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Events/EventImportParserTests.cs — 1/1

<!-- retirement-file: {"id":40,"file":"tests/EventBooking.Application.Tests/Events/EventImportParserTests.cs","beforeSha":"b710abf09497c47cf1c7fad69836a6d96e1169bfb138b9e5b0aa072c7a3fe818","afterSha":null,"side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Events;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Tests.Events;

public class EventImportParserTests
{
    private const string Header = "date,startTime,DAT,MED,UNI";

    [Fact]
    public void AGoodFileParsesEveryRow()
    {
        var result = EventImportParser.Parse(
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
        var result = EventImportParser.Parse($"{Header}\n\n2026-09-10,09:00,10,6,8\n\n");

        Assert.Empty(result.Errors);
        Assert.Single(result.Rows);
    }

    [Fact]
    public void AnEmptyFileIsRejected()
    {
        var result = EventImportParser.Parse("");

        Assert.Empty(result.Rows);
        Assert.Equal("The file is empty.", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void AWrongHeaderIsRejected()
    {
        var result = EventImportParser.Parse("date,startTime,types\n2026-09-10,09:00,DAT");

        Assert.Contains("header line must read exactly", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void AFileOfOnlyAHeaderIsRejected()
    {
        var result = EventImportParser.Parse(Header);

        Assert.Equal("The file contains no event rows.", Assert.Single(result.Errors).Message);
    }

    [Theory]
    [InlineData("2026-09-10,09:00,10,6")]
    [InlineData("2026-09-10,09:00,10,6,8,1")]
    public void AWrongFieldCountIsRejected(string line)
    {
        var result = EventImportParser.Parse($"{Header}\n{line}");

        Assert.Contains("Expected 5 comma-separated fields", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void AnUnparseableDateIsRejected()
    {
        var result = EventImportParser.Parse($"{Header}\n10-09-2026,09:00,10,6,8");

        Assert.Equal(2, Assert.Single(result.Errors).LineNumber);
        Assert.Contains("date", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void AnUnparseableStartTimeIsRejected()
    {
        var result = EventImportParser.Parse($"{Header}\n2026-09-10,9am,10,6,8");

        Assert.Contains("startTime", Assert.Single(result.Errors).Message);
    }

    [Theory]
    [InlineData("0,6,8")]
    [InlineData("-1,6,8")]
    [InlineData("abc,6,8")]
    [InlineData(",6,8")]
    public void ANonPositiveOrUnparseableHeadcountIsRejected(string counts)
    {
        var result = EventImportParser.Parse($"{Header}\n2026-09-10,09:00,{counts}");

        Assert.Equal(2, Assert.Single(result.Errors).LineNumber);
        Assert.Contains("DAT", result.Errors[0].Message);
    }

    [Fact]
    public void ADuplicateWindowWithinTheFileIsRejected()
    {
        var result = EventImportParser.Parse(
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
        var result = EventImportParser.Parse($"{Header}\n{string.Join('\n', rows)}");

        Assert.Empty(result.Rows);
        Assert.Contains("at most 200 rows", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void ALateStartTimeIsARowErrorNotAnException()
    {
        var result = EventImportParser.Parse(
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
        var result = EventImportParser.Parse(
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
        var result = EventImportParser.Parse(
            $"{Header}\n2026-09-10,09:00,10,6,8",
            new DateOnly(2026, 9, 3));

        Assert.Empty(result.Errors);
        Assert.Single(result.Rows);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Events/ImportEventsHandlerTests.cs — 1/1

<!-- retirement-file: {"id":41,"file":"tests/EventBooking.Application.Tests/Events/ImportEventsHandlerTests.cs","beforeSha":"5ddd85b346c67d51ca19a3971b470d74af2300a842e8af8bea5d8589f364a900","afterSha":null,"side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Common;
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;

namespace EventBooking.Application.Tests.Events;

public class ImportEventsHandlerTests
{
    private const string Header = "date,startTime,DAT,MED,UNI";
    private static readonly Guid Admin = Guid.Parse("c0000008-0000-0000-0000-000000000008");
    private static readonly Guid Manager = Guid.Parse("c0000001-0000-0000-0000-000000000001");

    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();

    private ImportEventsHandler Handler => new(_events, _roles, _unitOfWork, _audit, new FakeClock());

    public ImportEventsHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));
        _roles.Add(StaffAccessProfile.Create(
            Manager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
    }

    private Task<Result<EventImportOutcome>> Import(string csv, Guid? actor = null) =>
        Handler.HandleAsync(new ImportEventsCommand(actor ?? Admin, csv), CancellationToken.None);

    [Fact]
    public async Task AGoodFileCreatesOneEventPerRowWithNoProposal()
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

        Assert.Equal(2, _events.Items.Count);
        Assert.All(_events.Items, s => Assert.Null(s.ProposalId));
        var first = _events.Items.Single(s => s.Window.Date == new DateOnly(2026, 9, 10));
        Assert.Equal(10, first.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task EveryImportedEventWritesExactlyOneEventImportedEntry()
    {
        await Import(
            $"""
             {Header}
             2026-09-10,09:00,10,6,8
             2026-09-11,13:00,4,4,4
             """);

        Assert.Equal(2, _audit.Entries.Count(e => e.Action == AuditAction.EventImported));
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
        Assert.Empty(_events.Items);
        Assert.Empty(_audit.Entries);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AManagerIsForbidden()
    {
        var result = await Import($"{Header}\n2026-09-10,09:00,10,6,8", Manager);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Empty(_events.Items);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Events/SharedEventAuthorizationTests.cs — 1/1

<!-- retirement-file: {"id":42,"file":"tests/EventBooking.Application.Tests/Events/SharedEventAuthorizationTests.cs","beforeSha":"1723fd0ca0ba05d10441400ba9510173fab7dbf23a1fea5df3b9ea0ca37e9f79","afterSha":null,"side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Application.Tests/Fixtures/EventFixture.cs — 1/1

<!-- retirement-file: {"id":43,"file":"tests/EventBooking.Application.Tests/Fixtures/EventFixture.cs","beforeSha":null,"afterSha":"0c13090f3c6ad44a0345bbc0bdf398e7e8c71acbc46ef53a8d49dba1a3cfcb05","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;

internal static class EventFixture
{
    public static Event Create(Guid id, EventWindow window, IReadOnlyDictionary<Guid, int> headcounts)
    {
        var manager = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var proposal = EventProposal.Create(Guid.NewGuid(), window, manager);
        foreach (var type in AppointmentTypeIds.All)
            proposal.Accept(type, manager, headcounts[type]);
        return Event.CreateFrom(id, proposal);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs — 1/1

<!-- retirement-file: {"id":44,"file":"tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs","beforeSha":"0b13eaece3a6ac428da4ec1897b355335b6075dcea2dda263edd399ea02b514a","afterSha":"8a273303486db47e018dff7c99b5807d4b687a07c4910989794dd9172179d70c","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Notifications;

/// <summary>Verifies attendee emails name exactly their persisted requirement snapshot.</summary>
public sealed class SnapshotEmailAuthorityTests
{
    private static readonly Attendee Attendee = Attendee.Create(
        Guid.NewGuid(), "Amara", "amara@example.com",
        AttendeeGroup.Define(
            AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting]));
    private static readonly Event Event = Event.CreateImported(
        Guid.NewGuid(), new EventWindow(new DateOnly(2026, 10, 10), new TimeOnly(9, 0)),
        new Dictionary<Guid, int>
        {
            [AppointmentTypeIds.DrugAndAlcoholTesting] = 10,
            [AppointmentTypeIds.MedicalCheckUp] = 10,
            [AppointmentTypeIds.UniformFitting] = 10,
        });
    private static readonly AttendeePortalOptions Portal = new(
        "https://booking.example", "Head Office", "recruitment@example.com");

    /// <summary>An Invite names only a one-type snapshot and uses singular recovery copy.</summary>
    [Fact]
    public void RecoveryInviteUsesSnapshotAndSingularCopy()
    {
        var email = AttendeeEmailComposer.Invite(
            Attendee,
            [AppointmentTypeIds.MedicalCheckUp],
            [Event, Event, Event],
            "https://booking.example/book/token",
            isReinvite: false,
            isRecovery: true);

        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.DoesNotContain("Uniform Fitting", email.TextBody);
        Assert.Contains("missed appointment", email.TextBody, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Booking confirmation names a two-type Booking snapshot, not all Attendee types.</summary>
    [Fact]
    public void ConfirmationUsesBookingSnapshot()
    {
        var email = AttendeeEmailComposer.BookingConfirmation(
            Attendee,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            Event,
            "https://booking.example/manage/token",
            Portal);

        Assert.Contains("Drug & Alcohol Testing", email.TextBody);
        Assert.Contains("Uniform Fitting", email.TextBody);
        Assert.DoesNotContain("Medical Check-up", email.TextBody);
    }

    /// <summary>Cancellation names only the affected Booking snapshot.</summary>
    [Fact]
    public void CancellationUsesAffectedBookingSnapshot()
    {
        var email = AttendeeEmailComposer.EventCancelled(
            Attendee, [AppointmentTypeIds.MedicalCheckUp], Event);

        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.DoesNotContain("Drug & Alcohol Testing", email.TextBody);
    }

    /// <summary>A three-type Invite names every snapshot type in code order.</summary>
    [Fact]
    public void ThreeTypeInviteNamesEverySnapshotType()
    {
        var email = AttendeeEmailComposer.Invite(
            Attendee,
            [AppointmentTypeIds.UniformFitting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.DrugAndAlcoholTesting],
            [Event, Event, Event],
            "https://booking.example/book/token",
            isReinvite: true,
            isRecovery: false);

        Assert.Contains("Drug & Alcohol Testing", email.TextBody);
        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.Contains("Uniform Fitting", email.TextBody);
        Assert.Contains("We have not heard back", email.TextBody);
    }

    /// <summary>A two-type cancellation names both snapshot types with plural copy.</summary>
    [Fact]
    public void TwoTypeCancellationUsesPluralCopy()
    {
        var email = AttendeeEmailComposer.EventCancelled(
            Attendee,
            [AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.DrugAndAlcoholTesting],
            Event);

        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.Contains("Drug & Alcohol Testing", email.TextBody);
        Assert.DoesNotContain("Uniform Fitting", email.TextBody);
        Assert.Contains("your appointments on", email.TextBody);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs — 1/1

<!-- retirement-file: {"id":44,"file":"tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs","beforeSha":"0b13eaece3a6ac428da4ec1897b355335b6075dcea2dda263edd399ea02b514a","afterSha":"8a273303486db47e018dff7c99b5807d4b687a07c4910989794dd9172179d70c","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Notifications;

/// <summary>Verifies attendee emails name exactly their persisted requirement snapshot.</summary>
public sealed class SnapshotEmailAuthorityTests
{
    private static readonly Attendee Attendee = Attendee.Create(
        Guid.NewGuid(), "Amara", "amara@example.com",
        AttendeeGroup.Define(
            AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting]));
    private static readonly Event Event = EventFixture.Create(
        Guid.NewGuid(), new EventWindow(new DateOnly(2026, 10, 10), new TimeOnly(9, 0)),
        new Dictionary<Guid, int>
        {
            [AppointmentTypeIds.DrugAndAlcoholTesting] = 10,
            [AppointmentTypeIds.MedicalCheckUp] = 10,
            [AppointmentTypeIds.UniformFitting] = 10,
        });
    private static readonly AttendeePortalOptions Portal = new(
        "https://booking.example", "Head Office", "recruitment@example.com");

    /// <summary>An Invite names only a one-type snapshot and uses singular recovery copy.</summary>
    [Fact]
    public void RecoveryInviteUsesSnapshotAndSingularCopy()
    {
        var email = AttendeeEmailComposer.Invite(
            Attendee,
            [AppointmentTypeIds.MedicalCheckUp],
            [Event, Event, Event],
            "https://booking.example/book/token",
            isReinvite: false,
            isRecovery: true);

        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.DoesNotContain("Uniform Fitting", email.TextBody);
        Assert.Contains("missed appointment", email.TextBody, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Booking confirmation names a two-type Booking snapshot, not all Attendee types.</summary>
    [Fact]
    public void ConfirmationUsesBookingSnapshot()
    {
        var email = AttendeeEmailComposer.BookingConfirmation(
            Attendee,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            Event,
            "https://booking.example/manage/token",
            Portal);

        Assert.Contains("Drug & Alcohol Testing", email.TextBody);
        Assert.Contains("Uniform Fitting", email.TextBody);
        Assert.DoesNotContain("Medical Check-up", email.TextBody);
    }

    /// <summary>Cancellation names only the affected Booking snapshot.</summary>
    [Fact]
    public void CancellationUsesAffectedBookingSnapshot()
    {
        var email = AttendeeEmailComposer.EventCancelled(
            Attendee, [AppointmentTypeIds.MedicalCheckUp], Event);

        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.DoesNotContain("Drug & Alcohol Testing", email.TextBody);
    }

    /// <summary>A three-type Invite names every snapshot type in code order.</summary>
    [Fact]
    public void ThreeTypeInviteNamesEverySnapshotType()
    {
        var email = AttendeeEmailComposer.Invite(
            Attendee,
            [AppointmentTypeIds.UniformFitting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.DrugAndAlcoholTesting],
            [Event, Event, Event],
            "https://booking.example/book/token",
            isReinvite: true,
            isRecovery: false);

        Assert.Contains("Drug & Alcohol Testing", email.TextBody);
        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.Contains("Uniform Fitting", email.TextBody);
        Assert.Contains("We have not heard back", email.TextBody);
    }

    /// <summary>A two-type cancellation names both snapshot types with plural copy.</summary>
    [Fact]
    public void TwoTypeCancellationUsesPluralCopy()
    {
        var email = AttendeeEmailComposer.EventCancelled(
            Attendee,
            [AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.DrugAndAlcoholTesting],
            Event);

        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.Contains("Drug & Alcohol Testing", email.TextBody);
        Assert.DoesNotContain("Uniform Fitting", email.TextBody);
        Assert.Contains("your appointments on", email.TextBody);
    }
}
`````
