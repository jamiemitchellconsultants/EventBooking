using EventBooking.Application.Access;
using EventBooking.Application.Candidates;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;

namespace EventBooking.Application.Tests.Candidates;

public class ListCandidatesHandlerTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemoryCandidateRepository _candidates = new();
    private readonly InMemoryEmployeeGroupRepository _groups = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();

    private ListCandidatesHandler Handler => new(
        _candidates, _groups, new StaffAccessAuthorizer(_roles));

    public ListCandidatesHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _groups.Items.Add(EmployeeGroup.Define(
            EmployeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting]));
        _groups.Items.Add(EmployeeGroup.Define(
            EmployeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT",
            "Ground Operations Agent", true, [AppointmentTypeIds.MedicalCheckUp]));

        _candidates.Add(Candidate.Create(
            Guid.NewGuid(), "A. Novak", "a.novak@mail.com",
            _groups.Items.Single(group => group.Id == EmployeeGroupIds.CabinCrew)));

        var chen = Candidate.Create(
            Guid.NewGuid(), "B. Chen", "b.chen@mail.com",
            _groups.Items.Single(group => group.Id == EmployeeGroupIds.GroundOperationsAgent));
        chen.MarkInvited();
        _candidates.Add(chen);

        var diallo = Candidate.Create(
            Guid.NewGuid(), "C. Diallo", "c.diallo@mail.com",
            _groups.Items.Single(group => group.Id == EmployeeGroupIds.GroundOperationsAgent));
        diallo.MarkAwaitingAvailability();
        _candidates.Add(diallo);
    }

    [Fact]
    public async Task EveryCandidateIsListedWithGroupAndDisplayStatus()
    {
        var result = await Handler.HandleAsync(
            new ListCandidatesQuery(Coordinator, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);

        var novak = result.Value.Single(c => c.Email == "a.novak@mail.com");
        Assert.Equal(EmployeeGroupIds.CabinCrew, novak.EmployeeGroupId);
        Assert.Equal("CABIN_CREW", novak.EmployeeGroupCode);
        Assert.Equal("Cabin Crew", novak.EmployeeGroupName);
        Assert.False(novak.RequiresEmployeeGroupReconciliation);
        Assert.Equal(
            new[] { "DAT", "MED", "UNI" },
            novak.RequiredAppointmentTypes.Select(summary => summary.Code));
        Assert.Equal(CandidateStatus.NotYetInvited, novak.Status);
        Assert.Equal("Not yet invited", novak.StatusDisplay);

        var diallo = result.Value.Single(c => c.Email == "c.diallo@mail.com");
        Assert.Equal(EmployeeGroupIds.GroundOperationsAgent, diallo.EmployeeGroupId);
        Assert.Equal("GROUND_OPERATIONS_AGENT", diallo.EmployeeGroupCode);
        Assert.Equal("Ground Operations Agent", diallo.EmployeeGroupName);
        Assert.False(diallo.RequiresEmployeeGroupReconciliation);

        Assert.Equal(
            "Invited (pending response)",
            result.Value.Single(c => c.Email == "b.chen@mail.com").StatusDisplay);
        Assert.Equal(
            "Awaiting availability",
            result.Value.Single(c => c.Email == "c.diallo@mail.com").StatusDisplay);
    }

    [Fact]
    public async Task TheListIsOrderedByName()
    {
        var result = await Handler.HandleAsync(
            new ListCandidatesQuery(Coordinator, null, null), CancellationToken.None);

        Assert.Equal(new[] { "A. Novak", "B. Chen", "C. Diallo" }, result.Value.Select(c => c.Name));
    }

    [Fact]
    public async Task FilteringByStatusNarrowsTheList()
    {
        var result = await Handler.HandleAsync(
            new ListCandidatesQuery(Coordinator, CandidateStatus.Invited, null), CancellationToken.None);

        Assert.Equal("b.chen@mail.com", Assert.Single(result.Value).Email);
    }

    [Theory]
    [InlineData("diallo")]
    [InlineData("DIALLO")]
    [InlineData("c.diallo@mail.com")]
    public async Task SearchMatchesNameOrEmailCaseInsensitively(string search)
    {
        var result = await Handler.HandleAsync(
            new ListCandidatesQuery(Coordinator, null, search), CancellationToken.None);

        Assert.Equal("C. Diallo", Assert.Single(result.Value).Name);
    }

    [Fact]
    public async Task AnEmptySearchIsIgnored()
    {
        var result = await Handler.HandleAsync(
            new ListCandidatesQuery(Coordinator, null, "   "), CancellationToken.None);

        Assert.Equal(3, result.Value.Count);
    }

    [Fact]
    public async Task ANonCoordinatorIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new ListCandidatesQuery(Guid.NewGuid(), null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }
}
