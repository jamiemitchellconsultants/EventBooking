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

/// <summary>Verifies candidate requirements cannot drift away from an active booking snapshot.</summary>
public sealed class ActiveBookingRequirementTests
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

    /// <summary>Verifies a changed set conflicts before candidate details or requirements mutate.</summary>
    [Fact]
    public async Task ChangedRequirementsAreRejectedBeforeAnyCandidateMutation()
    {
        var (handler, candidate, coordinator) = GivenActiveBooking();

        var result = await handler.UpdateAsync(
            new UpdateCandidateCommand(
                coordinator,
                candidate.Id,
                "Changed Name",
                "changed@example.com",
                Pilots.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("candidate_group_active_booking_conflict", result.Error.Code);
        Assert.Equal(
            "Appointment requirements cannot change while the candidate has an active booking. Cancel and rebook first.",
            result.Error.Message);
        Assert.Equal("Amara Novak", candidate.Name);
        Assert.Equal("amara@example.com", candidate.Email);
        Assert.Equal(
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting],
            candidate.RequiredAppointmentTypeIds);
    }

    /// <summary>Verifies a set-equivalent group still permits name and email correction.</summary>
    [Fact]
    public async Task SameRequirementSetInAnotherGroupAllowsDetailCorrection()
    {
        var (handler, candidate, coordinator) = GivenActiveBooking();

        var result = await handler.UpdateAsync(
            new UpdateCandidateCommand(
                coordinator,
                candidate.Id,
                "Amara N. Novak",
                "amara.novak@example.com",
                GroundTransport.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Amara N. Novak", candidate.Name);
        Assert.Equal("amara.novak@example.com", candidate.Email);
    }

    private static (SaveCandidateHandler Handler, Candidate Candidate, Guid Coordinator)
        GivenActiveBooking()
    {
        var coordinator = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(coordinator, [Role.Coordinator], null));
        var groups = new InMemoryEmployeeGroupRepository();
        groups.Items.AddRange([CabinCrew, GroundTransport, Pilots]);
        var candidates = new InMemoryCandidateRepository();
        var candidate = Candidate.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "amara@example.com",
            CabinCrew);
        candidates.Add(candidate);
        var bookings = new InMemoryBookingRepository();
        bookings.Add(NewBooking(candidate));

        return (
            new SaveCandidateHandler(
                candidates,
                groups,
                new InMemoryInviteRepository(),
                bookings,
                new StaffAccessAuthorizer(profiles),
                new RecordingAuditLogger(),
                new FakeUnitOfWork()),
            candidate,
            coordinator);
    }

    private static Booking NewBooking(Candidate candidate)
    {
        var slotId = Guid.NewGuid();
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            candidate.Id,
            "invite-token-hash",
            DateTimeOffset.UtcNow.AddDays(1),
            [slotId, Guid.NewGuid(), Guid.NewGuid()],
            candidate.RequiredAppointmentTypeIds,
            0);
        return Booking.Create(
            Guid.NewGuid(), invite, slotId, "manage-token-hash", DateTimeOffset.UtcNow);
    }
}
