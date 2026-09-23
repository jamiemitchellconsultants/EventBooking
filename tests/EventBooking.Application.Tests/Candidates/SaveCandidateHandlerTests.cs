using EventBooking.Application.Access;
using EventBooking.Application.Candidates;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;

namespace EventBooking.Application.Tests.Candidates;

public class SaveCandidateHandlerTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly Guid Manager = Guid.Parse("c0000001-0000-0000-0000-000000000001");

    private static readonly EmployeeGroup Pilots = EmployeeGroup.Define(
        EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
    private static readonly EmployeeGroup Engineering = EmployeeGroup.Define(
        EmployeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
        [AppointmentTypeIds.MedicalCheckUp]);

    private readonly InMemoryCandidateRepository _candidates = new();
    private readonly InMemoryEmployeeGroupRepository _groups = new();
    private readonly InMemoryBookingRepository _bookings = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private SaveCandidateHandler Handler => new(
        _candidates,
        _groups,
        new InMemoryInviteRepository(),
        _bookings,
        new StaffAccessAuthorizer(_roles),
        new RecordingAuditLogger(),
        _unitOfWork);

    public SaveCandidateHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _roles.Add(StaffAccessProfile.Create(
            Manager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _groups.Items.Add(Pilots);
        _groups.Items.Add(Engineering);
    }

    [Fact]
    public async Task ACoordinatorCanCreateACandidate()
    {
        var result = await Handler.CreateAsync(
            new CreateCandidateCommand(
                Coordinator, "Amara Novak", "a.novak@mail.com", EmployeeGroupIds.Pilots),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var candidate = Assert.Single(_candidates.Items);
        Assert.Equal(result.Value, candidate.Id);
        Assert.Equal("a.novak@mail.com", candidate.Email);
        Assert.Equal(EmployeeGroupIds.Pilots, candidate.EmployeeGroupId);
        Assert.Equal(2, candidate.Requirements.Count);
        Assert.Equal(CandidateStatus.NotYetInvited, candidate.Status);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AManagerCannotCreateACandidate()
    {
        var result = await Handler.CreateAsync(
            new CreateCandidateCommand(
                Manager, "Amara Novak", "a.novak@mail.com", EmployeeGroupIds.Pilots),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Theory]
    [InlineData(null, "employee_group_required")]
    public async Task AnAbsentGroupIsRequiredOnCreate(Guid? groupId, string code)
    {
        var result = await Handler.CreateAsync(
            new CreateCandidateCommand(Coordinator, "Amara Novak", "a.novak@mail.com", groupId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(code, result.Error.Code);
        Assert.Empty(_candidates.Items);
    }

    [Fact]
    public async Task AnUnknownGroupIsRejectedOnCreate()
    {
        var result = await Handler.CreateAsync(
            new CreateCandidateCommand(
                Coordinator, "Amara Novak", "a.novak@mail.com", Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("employee_group_unknown", result.Error.Code);
        Assert.Empty(_candidates.Items);
    }

    [Fact]
    public async Task ADuplicateEmailIsAConflict()
    {
        _candidates.Add(Candidate.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", Pilots));

        var result = await Handler.CreateAsync(
            new CreateCandidateCommand(
                Coordinator, "Someone Else", "A.Novak@Mail.com", EmployeeGroupIds.Pilots),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("a.novak@mail.com is already a candidate.", result.Error.Message);
        Assert.Single(_candidates.Items);
    }

    [Fact]
    public async Task DomainValidationSurfacesAsAValidationFailure()
    {
        var result = await Handler.CreateAsync(
            new CreateCandidateCommand(
                Coordinator, "Amara Novak", "nope", EmployeeGroupIds.Pilots),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal("email is not a valid email address.", result.Error.Message);
    }

    [Fact]
    public async Task UpdatingChangesTheDetailsAndTheGroup()
    {
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", Pilots);
        _candidates.Add(candidate);

        var result = await Handler.UpdateAsync(
            new UpdateCandidateCommand(
                Coordinator, candidate.Id, "Amara N. Novak", "amara@mail.com",
                EmployeeGroupIds.Engineering),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Amara N. Novak", candidate.Name);
        Assert.Equal("amara@mail.com", candidate.Email);
        Assert.Equal(EmployeeGroupIds.Engineering, candidate.EmployeeGroupId);
        Assert.Equal([AppointmentTypeIds.MedicalCheckUp], candidate.RequiredAppointmentTypeIds);
    }

    [Fact]
    public async Task UpdatingToAnotherCandidatesEmailIsAConflict()
    {
        var first = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", Pilots);
        var second = Candidate.Create(
            Guid.NewGuid(), "B. Chen", "b.chen@mail.com", Pilots);
        _candidates.Add(first);
        _candidates.Add(second);

        var result = await Handler.UpdateAsync(
            new UpdateCandidateCommand(
                Coordinator, second.Id, "B. Chen", "a.novak@mail.com",
                EmployeeGroupIds.Pilots),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("b.chen@mail.com", second.Email);
    }

    [Fact]
    public async Task KeepingTheSameEmailOnUpdateIsNotAConflict()
    {
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", Pilots);
        _candidates.Add(candidate);

        var result = await Handler.UpdateAsync(
            new UpdateCandidateCommand(
                Coordinator, candidate.Id, "Amara Novak", "a.novak@mail.com",
                EmployeeGroupIds.Engineering),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UpdatingAnUnknownCandidateIsNotFound()
    {
        var result = await Handler.UpdateAsync(
            new UpdateCandidateCommand(
                Coordinator, Guid.NewGuid(), "X", "x@mail.com", EmployeeGroupIds.Pilots),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task UpdatingWithoutAGroupIsRequired()
    {
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", Pilots);
        _candidates.Add(candidate);

        var result = await Handler.UpdateAsync(
            new UpdateCandidateCommand(
                Coordinator, candidate.Id, "Amara Novak", "a.novak@mail.com", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("employee_group_required", result.Error.Code);
        Assert.Equal(EmployeeGroupIds.Pilots, candidate.EmployeeGroupId);
    }
}
