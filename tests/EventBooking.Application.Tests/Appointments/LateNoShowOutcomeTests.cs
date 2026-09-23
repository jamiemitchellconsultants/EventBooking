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

/// <summary>Verifies late outcomes on past events follow the existing timing and correction rules.</summary>
public sealed class LateNoShowOutcomeTests
{
    /// <summary>Verifies Expected to NoShow succeeds the day after the event date with version and audit.</summary>
    [Fact]
    public async Task NoShowDayAfterEventDateSucceeds()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 8, 9, 0, 0, TimeSpan.Zero));

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.NoShow, 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingAppointmentStatus.NoShow, result.Value.Status);
        Assert.Equal(2, result.Value.Version);
        Assert.NotNull(result.Value.OutcomeAt);
        Assert.Null(result.Value.CheckedInAt);
        var entry = Assert.Single(scenario.Audit.Entries);
        Assert.Equal(AuditAction.AppointmentMarkedNoShow, entry.Action);
    }

    /// <summary>Verifies check-in is rejected once the event date has passed.</summary>
    [Fact]
    public async Task CheckInAfterEventDateIsRejected()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 8, 9, 0, 0, TimeSpan.Zero));

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(BookingAppointmentStatus.Expected, scenario.Appointment.Status);
    }

    /// <summary>Verifies a late NoShow corrects back to Expected while parents remain active.</summary>
    [Fact]
    public async Task LateNoShowCorrectsToExpected()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 8, 9, 0, 0, TimeSpan.Zero));
        await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.NoShow, 1),
            CancellationToken.None);

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.Expected, 2),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingAppointmentStatus.Expected, result.Value.Status);
        Assert.Equal(3, result.Value.Version);
        Assert.Null(result.Value.OutcomeAt);
        Assert.Contains(
            scenario.Audit.Entries, e => e.Action == AuditAction.AppointmentStatusCorrected);
    }

    private static UpdateBookingAppointmentStatusCommand Command(
        Scenario scenario,
        BookingAppointmentStatus status,
        long version) => new()
    {
        StaffUserId = scenario.StaffUserId,
        BookingAppointmentId = scenario.Appointment.Id,
        Status = status,
        ExpectedVersion = version,
    };

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
            staff, attendee, eventItem, appointment, booking, audit, operations, handler);
    }

    private sealed record Scenario(
        Guid StaffUserId,
        Attendee Attendee,
        Event Event,
        BookingAppointment Appointment,
        Booking Booking,
        RecordingAuditLogger Audit,
        TransactionOperationLog Operations,
        UpdateBookingAppointmentStatusHandler Handler);
}
