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
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        return Booking.Create(
            Guid.NewGuid(), invite, eventId, DateTimeOffset.UtcNow);
    }
}
