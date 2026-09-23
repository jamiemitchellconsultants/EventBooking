using EventBooking.Application.Access;
using EventBooking.Application.Candidates;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.EmployeeGroups;

namespace EventBooking.Application.Tests.Candidates;

/// <summary>Verifies Candidate application inputs resolve and derive Employee Group mappings.</summary>
public sealed class CandidateEmployeeGroupFlowTests
{
    private static readonly Guid Coordinator = Guid.NewGuid();
    private readonly InMemoryCandidateRepository _candidates = new();
    private readonly InMemoryEmployeeGroupRepository _groups = new();
    private readonly InMemoryStaffAccessProfileRepository _profiles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    /// <summary>Initializes one Coordinator and the approved reference groups.</summary>
    public CandidateEmployeeGroupFlowTests()
    {
        _profiles.Add(StaffAccessProfile.Create(Coordinator, [Role.Coordinator], null));
        _groups.Items.Add(EmployeeGroup.Define(
            EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
    }

    /// <summary>Create stores the group and derives requirements without requirement input.</summary>
    [Fact]
    public async Task CreateDerivesThePersistedGroupMapping()
    {
        var handler = new SaveCandidateHandler(
            _candidates,
            _groups,
            new InMemoryInviteRepository(),
            new InMemoryBookingRepository(),
            new StaffAccessAuthorizer(_profiles),
            new RecordingAuditLogger(),
            _unitOfWork);

        var result = await handler.CreateAsync(
            new CreateCandidateCommand(
                Coordinator, "Amara Novak", "amara@example.com", EmployeeGroupIds.Pilots),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var candidate = Assert.Single(_candidates.Items);
        Assert.Equal(EmployeeGroupIds.Pilots, candidate.EmployeeGroupId);
        Assert.Equal(
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            candidate.RequiredAppointmentTypeIds);
    }

    /// <summary>Unknown and absent groups return stable validation without saving.</summary>
    [Theory]
    [InlineData(null, "employee_group_required")]
    public async Task InvalidGroupDoesNotCreateCandidate(Guid? groupId, string code)
    {
        var handler = new SaveCandidateHandler(
            _candidates,
            _groups,
            new InMemoryInviteRepository(),
            new InMemoryBookingRepository(),
            new StaffAccessAuthorizer(_profiles),
            new RecordingAuditLogger(),
            _unitOfWork);

        var result = await handler.CreateAsync(
            new CreateCandidateCommand(Coordinator, "Amara", "amara@example.com", groupId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(code, result.Error.Code);
        Assert.Empty(_candidates.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    /// <summary>The new CSV contract carries one case-insensitive Employee Group code.</summary>
    [Fact]
    public void CsvParsesEmployeeGroupRatherThanAppointmentTypes()
    {
        var parsed = CandidateCsvParser.Parse(
            "name,email,employee_group\nAmara Novak,amara@example.com, pilots ");

        Assert.Empty(parsed.Errors);
        Assert.Equal("PILOTS", Assert.Single(parsed.Rows).EmployeeGroupCode);
        var old = CandidateCsvParser.Parse(
            "name,email,appointment_types\nAmara Novak,amara@example.com,DAT;UNI");
        Assert.Equal(1, Assert.Single(old.Errors).LineNumber);
    }
}
