using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Common;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Domain.Tests.AttendeeGroups;

/// <summary>Verifies Attendee Group invariants and Attendee requirement derivation.</summary>
public sealed class AttendeeGroupAttendeeTests
{
    /// <summary>Every approved mapping produces exactly the Issue 91 requirement set.</summary>
    [Theory]
    [MemberData(nameof(ApprovedMappings))]
    public void ApprovedMappingsAreDerived(
        Guid groupId,
        string code,
        string name,
        Guid[] expected)
    {
        var group = AttendeeGroup.Define(groupId, code, name, true, expected);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "amara@example.com", group);

        Assert.Equal(groupId, attendee.AttendeeGroupId);
        Assert.Equal(expected.Order(), attendee.RequiredAppointmentTypeIds.Order());
    }

    /// <summary>Changing between equal mappings changes only the assigned group.</summary>
    [Fact]
    public void SetEquivalentAssignmentPreservesTheMaterializedSet()
    {
        var engineering = AttendeeGroup.Define(
            AttendeeGroupIds.Engineering,
            "ENGINEERING",
            "Engineering",
            true,
            [AppointmentTypeIds.MedicalCheckUp]);
        var groundOperations = AttendeeGroup.Define(
            AttendeeGroupIds.GroundOperationsAgent,
            "GROUND_OPERATIONS_AGENT",
            "Ground Operations Agent",
            true,
            [AppointmentTypeIds.MedicalCheckUp]);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "amara@example.com", engineering);

        var changed = attendee.AssignAttendeeGroup(groundOperations);

        Assert.False(changed);
        Assert.Equal(AttendeeGroupIds.GroundOperationsAgent, attendee.AttendeeGroupId);
        Assert.Equal([AppointmentTypeIds.MedicalCheckUp], attendee.RequiredAppointmentTypeIds);
    }

    /// <summary>Inactive, empty, duplicate, and unknown mappings cannot become assignment authority.</summary>
    [Fact]
    public void InvalidReferenceDataIsRejected()
    {
        Assert.Throws<DomainException>(() => AttendeeGroup.Define(
            Guid.NewGuid(), "CABIN_CREW", "Cabin Crew", false,
            [AppointmentTypeIds.DrugAndAlcoholTesting]));
        Assert.Throws<DomainException>(() => AttendeeGroup.Define(
            Guid.NewGuid(), "CABIN_CREW", "Cabin Crew", true, []));
        Assert.Throws<DomainException>(() => AttendeeGroup.Define(
            Guid.NewGuid(), "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.MedicalCheckUp]));
        Assert.Throws<DomainException>(() => AttendeeGroup.Define(
            Guid.NewGuid(), "not-canonical", "Cabin Crew", true, [Guid.NewGuid()]));
    }

    /// <summary>Provides the exact five approved group mappings.</summary>
    public static TheoryData<Guid, string, string, Guid[]> ApprovedMappings => new()
    {
        { AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew",
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.UniformFitting] },
        { AttendeeGroupIds.Pilots, "PILOTS", "Pilots",
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting] },
        { AttendeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT", "Ground Operations Agent",
            [AppointmentTypeIds.MedicalCheckUp] },
        { AttendeeGroupIds.Engineering, "ENGINEERING", "Engineering",
            [AppointmentTypeIds.MedicalCheckUp] },
        { AttendeeGroupIds.GroundTransportServices, "GROUND_TRANSPORT_SERVICES", "Ground Transport Services",
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.UniformFitting] },
    };
}
