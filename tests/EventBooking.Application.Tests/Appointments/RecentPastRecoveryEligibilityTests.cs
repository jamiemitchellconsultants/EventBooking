using EventBooking.Application.Access;
using EventBooking.Application.Appointments;
using EventBooking.Application.Candidates;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;

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
        var readiness = new CandidateReadinessCalculator().Calculate(
            new CandidateReadinessSnapshot(
                scenario.Candidate.Id,
                Guid.NewGuid(),
                [AppointmentTypeIds.DrugAndAlcoholTesting],
                scenario.Booking.Id,
                [
                    new CandidateReadinessAttempt(
                        scenario.Appointment.Id,
                        AppointmentTypeIds.DrugAndAlcoholTesting,
                        scenario.Appointment.Status,
                        scenario.Booking.Id,
                        scenario.Booking.Status,
                        outcome.Value.OutcomeAt!.Value),
                ]));

        Assert.Equal(CandidateReadinessCode.AppointmentsOutstanding, readiness.Code);
        var outstanding = Assert.Single(readiness.OutstandingAppointmentTypes);
        Assert.True(outstanding.IsRecoverable);
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
        var handler = new UpdateBookingAppointmentStatusHandler(
            new StaffAccessAuthorizer(profiles),
            appointments,
            bookings,
            candidates,
            invites,
            slots,
            new RecoveryBookingOutcomeCoordinator(),
            new RecordingAuditLogger(),
            new FakeUnitOfWork(operations),
            new FakeClock(now));
        return new Scenario(staff, candidate, booking, appointment, handler);
    }

    private sealed record Scenario(
        Guid StaffUserId,
        Candidate Candidate,
        Booking Booking,
        BookingAppointment Appointment,
        UpdateBookingAppointmentStatusHandler Handler);
}
