# 01e — Location-restricted invites and closed attendee transitions, edits 15 (Task 8)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs — 1/1

<!-- retirement-file: {"id":36,"file":"tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs","beforeSha":"8b4e0bfa4a5085a4d3dd1298e09e24adf8b83200cd195cee6afa33c094d815bb","afterSha":"20d9bbc81567898fa7e6d7da65c0ed44e47df433a4ac0f57a76c31a3315ae7ee","side":"after","part":1,"parts":1} -->

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

## before — tests/EventBooking.Application.Tests/Attendees/ActiveBookingRequirementTests.cs — 1/1

<!-- retirement-file: {"id":37,"file":"tests/EventBooking.Application.Tests/Attendees/ActiveBookingRequirementTests.cs","beforeSha":"d8381a35eb5ad13d6dc05cd0e9c0f380f15100c2ead5836c2a03a5b68030b8ff","afterSha":"dd64c4955b92e8785f8208a581caaf575bf7ee99a9aa11eabc9c2984820c2280","side":"before","part":1,"parts":1} -->

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
            CabinCrew);
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
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        return Booking.Create(
            Guid.NewGuid(), invite, eventId, "manage-token-hash", DateTimeOffset.UtcNow);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Attendees/ActiveBookingRequirementTests.cs — 1/1

<!-- retirement-file: {"id":37,"file":"tests/EventBooking.Application.Tests/Attendees/ActiveBookingRequirementTests.cs","beforeSha":"d8381a35eb5ad13d6dc05cd0e9c0f380f15100c2ead5836c2a03a5b68030b8ff","afterSha":"dd64c4955b92e8785f8208a581caaf575bf7ee99a9aa11eabc9c2984820c2280","side":"after","part":1,"parts":1} -->

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

## before — tests/EventBooking.Application.Tests/Attendees/AttendeeAttendeeGroupFlowTests.cs — 1/1

<!-- retirement-file: {"id":38,"file":"tests/EventBooking.Application.Tests/Attendees/AttendeeAttendeeGroupFlowTests.cs","beforeSha":"f985e975736e39d6fa2c750f7620c3b1fbd0cdfdbe73e858dc3a10aa81757e9f","afterSha":"37780c6f7ad2f70593215c9b2030e1abb250ae8292421f3bfd33b406b02702c4","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Application.Tests/Attendees/AttendeeAttendeeGroupFlowTests.cs — 1/1

<!-- retirement-file: {"id":38,"file":"tests/EventBooking.Application.Tests/Attendees/AttendeeAttendeeGroupFlowTests.cs","beforeSha":"f985e975736e39d6fa2c750f7620c3b1fbd0cdfdbe73e858dc3a10aa81757e9f","afterSha":"37780c6f7ad2f70593215c9b2030e1abb250ae8292421f3bfd33b406b02702c4","side":"after","part":1,"parts":1} -->

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
            new FakeClock(),
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
            new FakeClock(),
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

## before — tests/EventBooking.Application.Tests/Attendees/AttendeeGroupLifecycleTests.cs — 1/1

<!-- retirement-file: {"id":39,"file":"tests/EventBooking.Application.Tests/Attendees/AttendeeGroupLifecycleTests.cs","beforeSha":"831575b691191a34d578980dd8851d189433de103a92decb281a60edda894828","afterSha":"900d6f09b69252d440319acbc96f07d7470cc4c4406ad7e1ae4495f012c6e234","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Application.Tests/Attendees/AttendeeGroupLifecycleTests.cs — 1/1

<!-- retirement-file: {"id":39,"file":"tests/EventBooking.Application.Tests/Attendees/AttendeeGroupLifecycleTests.cs","beforeSha":"831575b691191a34d578980dd8851d189433de103a92decb281a60edda894828","afterSha":"900d6f09b69252d440319acbc96f07d7470cc4c4406ad7e1ae4495f012c6e234","side":"after","part":1,"parts":1} -->

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
        var attendee = Attendee.Create(Guid.NewGuid(), "Amara", "amara@example.com", group, ProposalFixture.Now);
        if (status == AttendeeStatus.Invited || status == AttendeeStatus.Booked)
        {
            attendee.MarkInvited(ProposalFixture.Now);
        }
        if (status == AttendeeStatus.Booked)
        {
            attendee.MarkBooked(ProposalFixture.Now);
        }
        var attendees = new InMemoryAttendeeRepository(operations);
        attendees.Add(attendee);
        var invites = new InMemoryInviteRepository(operations);
        Invite? invite = null;
        if (includeInvite)
        {
            var eventId = Guid.NewGuid();
            invite = Invite.CreateInitial(
                Guid.NewGuid(),
                attendee.Id,
                "token",
                DateTimeOffset.UtcNow.AddDays(1),
                [ProposalFixture.LocationId],
                [eventId, Guid.NewGuid(), Guid.NewGuid()],
                attendee.RequiredAppointmentTypeIds,
                0);
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
            attendees, groups, invites, bookings, new StaffAccessAuthorizer(profiles), audit,
            new FakeClock(), unitOfWork);
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
