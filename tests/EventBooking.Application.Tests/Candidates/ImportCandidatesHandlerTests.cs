using EventBooking.Application.Access;
using EventBooking.Application.Candidates;
using EventBooking.Application.Common;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;

namespace EventBooking.Application.Tests.Candidates;

public class ImportCandidatesHandlerTests
{
    private const string Header = "name,email,employee_group";
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly Guid Admin = Guid.Parse("c0000008-0000-0000-0000-000000000008");
    private static readonly Guid Manager = Guid.Parse("c0000001-0000-0000-0000-000000000001");

    private readonly InMemoryCandidateRepository _candidates = new();
    private readonly InMemoryEmployeeGroupRepository _groups = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private ImportCandidatesHandler Handler => new(
        _candidates, _groups, new StaffAccessAuthorizer(_roles), _unitOfWork);

    public ImportCandidatesHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _roles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));
        _roles.Add(StaffAccessProfile.Create(
            Manager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _groups.Items.Add(EmployeeGroup.Define(
            EmployeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting]));
        _groups.Items.Add(EmployeeGroup.Define(
            EmployeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
            [AppointmentTypeIds.MedicalCheckUp]));
        _groups.Items.Add(EmployeeGroup.Define(
            EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
    }

    private Task<Result<CandidateImportOutcome>> Import(string csv, Guid? actor = null) =>
        Handler.HandleAsync(
            new ImportCandidatesCommand(actor ?? Coordinator, csv), CancellationToken.None);

    [Fact]
    public async Task AGoodFileCreatesOneCandidatePerRow()
    {
        var result = await Import(
            $"""
             {Header}
             Amara Novak,a.novak@mail.com,CABIN_CREW
             B. Chen,b.chen@mail.com,engineering
             """);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Accepted);
        Assert.Equal(2, result.Value.ImportedCount);
        Assert.Empty(result.Value.Errors);

        Assert.Equal(2, _candidates.Items.Count);
        var novak = _candidates.Items.Single(c => c.Email == "a.novak@mail.com");
        Assert.Equal("Amara Novak", novak.Name);
        Assert.Equal(CandidateStatus.NotYetInvited, novak.Status);
        Assert.Equal(EmployeeGroupIds.CabinCrew, novak.EmployeeGroupId);
        Assert.Equal(3, novak.Requirements.Count);
        var chen = _candidates.Items.Single(c => c.Email == "b.chen@mail.com");
        Assert.Equal(EmployeeGroupIds.Engineering, chen.EmployeeGroupId);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnAdminCannotImport()
    {
        var result = await Import($"{Header}\nAmara Novak,a.novak@mail.com,PILOTS", Admin);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Empty(_candidates.Items);
    }

    [Fact]
    public async Task AManagerCannotImport()
    {
        var result = await Import($"{Header}\nAmara Novak,a.novak@mail.com,PILOTS", Manager);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal("This staff profile cannot perform this operation.", result.Error.Message);
        Assert.Empty(_candidates.Items);
    }

    [Fact]
    public async Task OneBadRowRejectsTheWholeFile()
    {
        var result = await Import(
            $"""
             {Header}
             Amara Novak,a.novak@mail.com,PILOTS
             B. Chen,b.chen@mail.com,UNKNOWN_GROUP
             """);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Accepted);
        Assert.Equal(0, result.Value.ImportedCount);
        var error = Assert.Single(result.Value.Errors);
        Assert.Equal(3, error.LineNumber);
        Assert.Equal("UNKNOWN_GROUP is not a known employee group code.", error.Message);
        Assert.Empty(_candidates.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ABadEmailIsCaughtByTheDomainAndReportedAgainstItsLine()
    {
        var result = await Import(
            $"""
             {Header}
             Amara Novak,a.novak@mail.com,PILOTS
             B. Chen,not-an-email,ENGINEERING
             """);

        Assert.False(result.Value.Accepted);
        var error = Assert.Single(result.Value.Errors);
        Assert.Equal(3, error.LineNumber);
        Assert.Equal("email is not a valid email address.", error.Message);
        Assert.Empty(_candidates.Items);
    }

    [Fact]
    public async Task AnEmailThatAlreadyExistsIsReportedAgainstItsLine()
    {
        _candidates.Add(Candidate.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com",
            _groups.Items.Single(group => group.Id == EmployeeGroupIds.Engineering)));

        var result = await Import($"{Header}\nAmara N,a.novak@mail.com,PILOTS");

        Assert.False(result.Value.Accepted);
        var error = Assert.Single(result.Value.Errors);
        Assert.Equal(2, error.LineNumber);
        Assert.Equal("a.novak@mail.com is already a candidate.", error.Message);
        Assert.Single(_candidates.Items);
    }

    [Fact]
    public async Task AnEmptyUploadIsReportedNotCrashed()
    {
        var result = await Import("");

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Accepted);
        Assert.Equal("The file is empty.", Assert.Single(result.Value.Errors).Message);
    }

    [Fact]
    public async Task EveryBadRowIsListedTogether()
    {
        var result = await Import(
            $"""
             {Header}
             ,a.novak@mail.com,PILOTS
             B. Chen,also-not-an-email,ENGINEERING
             C. Diallo,c.diallo@mail.com,CABIN_CREW
             """);

        Assert.False(result.Value.Accepted);
        Assert.Equal(2, result.Value.Errors.Count);
        Assert.Equal([2, 3], result.Value.Errors.Select(e => e.LineNumber));
    }
}
