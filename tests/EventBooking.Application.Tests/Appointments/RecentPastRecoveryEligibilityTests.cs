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
