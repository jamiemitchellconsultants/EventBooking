using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Common;
using EventBooking.Domain.EmployeeGroups;

namespace EventBooking.Domain.Tests.EmployeeGroups;

/// <summary>Verifies Employee Group invariants and Candidate requirement derivation.</summary>
public sealed class EmployeeGroupCandidateTests
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
        var group = EmployeeGroup.Define(groupId, code, name, true, expected);
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", "amara@example.com", group);

        Assert.Equal(groupId, candidate.EmployeeGroupId);
        Assert.Equal(expected.Order(), candidate.RequiredAppointmentTypeIds.Order());
    }

    /// <summary>Changing between equal mappings changes only the assigned group.</summary>
    [Fact]
    public void SetEquivalentAssignmentPreservesTheMaterializedSet()
    {
        var engineering = EmployeeGroup.Define(
            EmployeeGroupIds.Engineering,
            "ENGINEERING",
            "Engineering",
            true,
            [AppointmentTypeIds.MedicalCheckUp]);
        var groundOperations = EmployeeGroup.Define(
            EmployeeGroupIds.GroundOperationsAgent,
            "GROUND_OPERATIONS_AGENT",
            "Ground Operations Agent",
            true,
            [AppointmentTypeIds.MedicalCheckUp]);
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", "amara@example.com", engineering);

        var changed = candidate.AssignEmployeeGroup(groundOperations);

        Assert.False(changed);
        Assert.Equal(EmployeeGroupIds.GroundOperationsAgent, candidate.EmployeeGroupId);
        Assert.Equal([AppointmentTypeIds.MedicalCheckUp], candidate.RequiredAppointmentTypeIds);
    }

    /// <summary>Inactive, empty, duplicate, and unknown mappings cannot become assignment authority.</summary>
    [Fact]
    public void InvalidReferenceDataIsRejected()
    {
        Assert.Throws<DomainException>(() => EmployeeGroup.Define(
            Guid.NewGuid(), "CABIN_CREW", "Cabin Crew", false,
            [AppointmentTypeIds.DrugAndAlcoholTesting]));
        Assert.Throws<DomainException>(() => EmployeeGroup.Define(
            Guid.NewGuid(), "CABIN_CREW", "Cabin Crew", true, []));
        Assert.Throws<DomainException>(() => EmployeeGroup.Define(
            Guid.NewGuid(), "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.MedicalCheckUp]));
        Assert.Throws<DomainException>(() => EmployeeGroup.Define(
            Guid.NewGuid(), "not-canonical", "Cabin Crew", true, [Guid.NewGuid()]));
    }

    /// <summary>Provides the exact five approved group mappings.</summary>
    public static TheoryData<Guid, string, string, Guid[]> ApprovedMappings => new()
    {
        { EmployeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew",
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.UniformFitting] },
        { EmployeeGroupIds.Pilots, "PILOTS", "Pilots",
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting] },
        { EmployeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT", "Ground Operations Agent",
            [AppointmentTypeIds.MedicalCheckUp] },
        { EmployeeGroupIds.Engineering, "ENGINEERING", "Engineering",
            [AppointmentTypeIds.MedicalCheckUp] },
        { EmployeeGroupIds.GroundTransportServices, "GROUND_TRANSPORT_SERVICES", "Ground Transport Services",
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.UniformFitting] },
    };
}
