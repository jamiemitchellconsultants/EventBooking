using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Common;
using EventBooking.Domain.EmployeeGroups;

namespace EventBooking.Domain.Tests.Candidates;

public class CandidateTests
{
    private static readonly Guid[] TwoTypes =
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting];

    /// <summary>Builds the Pilots group used across these fixtures for DAT+UNI.</summary>
    private static EmployeeGroup Pilots() =>
        EmployeeGroup.Define(EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true, TwoTypes);

    private static Candidate NewCandidate() =>
        Candidate.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", Pilots());

    [Fact]
    public void ANewCandidateStartsNotYetInvited()
    {
        var candidate = NewCandidate();

        Assert.Equal(CandidateStatus.NotYetInvited, candidate.Status);
        Assert.Equal("Amara Novak", candidate.Name);
        Assert.Equal("a.novak@mail.com", candidate.Email);
    }

    [Fact]
    public void NameAndEmailAreTrimmedAndTheEmailIsLowerCased()
    {
        var candidate = Candidate.Create(
            Guid.NewGuid(), "  Amara Novak  ", "  A.Novak@Mail.COM ", Pilots());

        Assert.Equal("Amara Novak", candidate.Name);
        Assert.Equal("a.novak@mail.com", candidate.Email);
    }

    [Fact]
    public void RequirementsAreRecordedAgainstTheCandidate()
    {
        var candidate = NewCandidate();

        Assert.Equal(2, candidate.Requirements.Count);
        Assert.All(candidate.Requirements, r => Assert.Equal(candidate.Id, r.CandidateId));
        Assert.Equal(
            TwoTypes.OrderBy(id => id),
            candidate.RequiredAppointmentTypeIds.OrderBy(id => id));
    }

    [Fact]
    public void AMissingNameIsRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => Candidate.Create(Guid.NewGuid(), "  ", "a.novak@mail.com", Pilots()));
        Assert.Equal("name must not be blank.", ex.Message);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("no@domain")]
    [InlineData("two@@at.com")]
    [InlineData("spaces in@mail.com")]
    [InlineData("")]
    [InlineData(null)]
    public void AnInvalidEmailIsRejected(string? email)
    {
        var ex = Assert.Throws<DomainException>(
            () => Candidate.Create(Guid.NewGuid(), "Amara Novak", email, Pilots()));
        Assert.Equal("email is not a valid email address.", ex.Message);
    }

    [Fact]
    public void AGroupWithNoMappedTypesIsRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => EmployeeGroup.Define(Guid.NewGuid(), "EMPTY", "Empty", true, []));
        Assert.Equal("An employee group must map at least one appointment type.", ex.Message);
    }

    [Fact]
    public void DuplicateMappedTypesAreRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => EmployeeGroup.Define(
                Guid.NewGuid(),
                "DUP",
                "Dup",
                true,
                [AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.MedicalCheckUp]));
        Assert.Equal("An employee group cannot map the same appointment type twice.", ex.Message);
    }

    [Fact]
    public void AnUnknownMappedTypeIsRejected()
    {
        Assert.Throws<DomainException>(
            () => EmployeeGroup.Define(Guid.NewGuid(), "UNKNOWN", "Unknown", true, [Guid.NewGuid()]));
    }

    [Fact]
    public void AllThreeAppointmentTypesAreAllowed()
    {
        var cabinCrew = EmployeeGroup.Define(
            EmployeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true, AppointmentTypeIds.All);
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", cabinCrew);

        Assert.Equal(3, candidate.Requirements.Count);
    }

    [Fact]
    public void UpdatingDetailsRevalidates()
    {
        var candidate = NewCandidate();

        candidate.UpdateDetails("Amara N. Novak", "amara@mail.com");
        Assert.Equal("Amara N. Novak", candidate.Name);
        Assert.Equal("amara@mail.com", candidate.Email);

        Assert.Throws<DomainException>(() => candidate.UpdateDetails("Amara", "broken"));
        Assert.Equal("amara@mail.com", candidate.Email);
    }

    [Fact]
    public void AssigningADifferentGroupReplacesThePreviousSet()
    {
        var candidate = NewCandidate();
        var groundOps = EmployeeGroup.Define(
            EmployeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT",
            "Ground Operations Agent", true, [AppointmentTypeIds.MedicalCheckUp]);

        Assert.True(candidate.AssignEmployeeGroup(groundOps));
        Assert.Equal([AppointmentTypeIds.MedicalCheckUp], candidate.RequiredAppointmentTypeIds);
    }

    [Fact]
    public void AssigningAnEquivalentGroupPreservesThePreviousSet()
    {
        var candidate = NewCandidate();
        var equivalent = EmployeeGroup.Define(
            Guid.NewGuid(), "PILOTS_EQUIVALENT", "Pilots equivalent", true, TwoTypes);

        Assert.False(candidate.AssignEmployeeGroup(equivalent));
        Assert.Equal(2, candidate.Requirements.Count);
    }
}
