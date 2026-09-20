# 00b — Vocabulary edits 80 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — tests/EventBooking.Application.Tests/Attendees/GetAttendeeReadinessHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":265,"oldPath":"tests/EventBooking.Application.Tests/Candidates/GetCandidateReadinessHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Attendees/GetAttendeeReadinessHandlerTests.cs","beforeSha":"e21351543207e34512eb40ce77c8da69975d896327eee16705e3f5abbf2fe2c4","afterSha":"72ff42f771256207a2c55aec51d81b0acc62c91ba07cc266467cb9e9f0bd94bb","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Attendees;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Tests.Attendees;

/// <summary>Verifies readiness authorization, not-found handling, and coordinator success.</summary>
public sealed class GetAttendeeReadinessHandlerTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly Guid Admin = Guid.Parse("c0000008-0000-0000-0000-000000000008");

    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly InMemoryQueries _queries = new();

    private GetAttendeeReadinessHandler Handler => new(
        new StaffAccessAuthorizer(_roles), _queries, new AttendeeReadinessCalculator());

    public GetAttendeeReadinessHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _roles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));
    }

    /// <summary>An unknown attendee fails before any readiness is calculated.</summary>
    [Fact]
    public async Task UnknownAttendeeIsNotFound()
    {
        _queries.Snapshot = null;

        var result = await Handler.HandleAsync(
            new GetAttendeeReadinessQuery(Coordinator, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    /// <summary>A coordinator receives the calculated readiness.</summary>
    [Fact]
    public async Task CoordinatorReceivesCalculatedReadiness()
    {
        var attendeeId = Guid.NewGuid();
        _queries.Snapshot = new AttendeeReadinessSnapshot(
            attendeeId,
            Guid.NewGuid(),
            [AppointmentTypeIds.MedicalCheckUp],
            null,
            []);

        var result = await Handler.HandleAsync(
            new GetAttendeeReadinessQuery(Coordinator, attendeeId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(attendeeId, result.Value.AttendeeId);
        Assert.Equal(AttendeeReadinessCode.NoActiveBooking, result.Value.Code);
    }

    /// <summary>An admin is forbidden before the readiness query runs.</summary>
    [Fact]
    public async Task AdminRunsNoReadinessQuery()
    {
        var result = await Handler.HandleAsync(
            new GetAttendeeReadinessQuery(Admin, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(0, _queries.QueryCount);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Candidates/ImportCandidatesHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":266,"oldPath":"tests/EventBooking.Application.Tests/Candidates/ImportCandidatesHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Attendees/ImportAttendeesHandlerTests.cs","beforeSha":"c84a2845868b54c580eca081a7ab8b47d006d761f217c3c8ed85bad07d468583","afterSha":"8a57c7c930523e8fdfa170d070bebf3c06b70e5e30975cff72b9b71494ea99fa","side":"before","part":1,"parts":1} -->

`````csharp
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
`````

## after — tests/EventBooking.Application.Tests/Attendees/ImportAttendeesHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":266,"oldPath":"tests/EventBooking.Application.Tests/Candidates/ImportCandidatesHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Attendees/ImportAttendeesHandlerTests.cs","beforeSha":"c84a2845868b54c580eca081a7ab8b47d006d761f217c3c8ed85bad07d468583","afterSha":"8a57c7c930523e8fdfa170d070bebf3c06b70e5e30975cff72b9b71494ea99fa","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Attendees;
using EventBooking.Application.Common;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Application.Tests.Attendees;

public class ImportAttendeesHandlerTests
{
    private const string Header = "name,email,attendee_group";
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly Guid Admin = Guid.Parse("c0000008-0000-0000-0000-000000000008");
    private static readonly Guid Manager = Guid.Parse("c0000001-0000-0000-0000-000000000001");

    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryAttendeeGroupRepository _groups = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private ImportAttendeesHandler Handler => new(
        _attendees, _groups, new StaffAccessAuthorizer(_roles), _unitOfWork);

    public ImportAttendeesHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _roles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));
        _roles.Add(StaffAccessProfile.Create(
            Manager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _groups.Items.Add(AttendeeGroup.Define(
            AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting]));
        _groups.Items.Add(AttendeeGroup.Define(
            AttendeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
            [AppointmentTypeIds.MedicalCheckUp]));
        _groups.Items.Add(AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
    }

    private Task<Result<AttendeeImportOutcome>> Import(string csv, Guid? actor = null) =>
        Handler.HandleAsync(
            new ImportAttendeesCommand(actor ?? Coordinator, csv), CancellationToken.None);

    [Fact]
    public async Task AGoodFileCreatesOneAttendeePerRow()
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

        Assert.Equal(2, _attendees.Items.Count);
        var novak = _attendees.Items.Single(c => c.Email == "a.novak@mail.com");
        Assert.Equal("Amara Novak", novak.Name);
        Assert.Equal(AttendeeStatus.NotYetInvited, novak.Status);
        Assert.Equal(AttendeeGroupIds.CabinCrew, novak.AttendeeGroupId);
        Assert.Equal(3, novak.Requirements.Count);
        var chen = _attendees.Items.Single(c => c.Email == "b.chen@mail.com");
        Assert.Equal(AttendeeGroupIds.Engineering, chen.AttendeeGroupId);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnAdminCannotImport()
    {
        var result = await Import($"{Header}\nAmara Novak,a.novak@mail.com,PILOTS", Admin);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Empty(_attendees.Items);
    }

    [Fact]
    public async Task AManagerCannotImport()
    {
        var result = await Import($"{Header}\nAmara Novak,a.novak@mail.com,PILOTS", Manager);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal("This staff profile cannot perform this operation.", result.Error.Message);
        Assert.Empty(_attendees.Items);
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
        Assert.Equal("UNKNOWN_GROUP is not a known attendee group code.", error.Message);
        Assert.Empty(_attendees.Items);
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
        Assert.Empty(_attendees.Items);
    }

    [Fact]
    public async Task AnEmailThatAlreadyExistsIsReportedAgainstItsLine()
    {
        _attendees.Add(Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com",
            _groups.Items.Single(group => group.Id == AttendeeGroupIds.Engineering)));

        var result = await Import($"{Header}\nAmara N,a.novak@mail.com,PILOTS");

        Assert.False(result.Value.Accepted);
        var error = Assert.Single(result.Value.Errors);
        Assert.Equal(2, error.LineNumber);
        Assert.Equal("a.novak@mail.com is already a attendee.", error.Message);
        Assert.Single(_attendees.Items);
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
`````

## before — tests/EventBooking.Application.Tests/Candidates/ListCandidatesHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":267,"oldPath":"tests/EventBooking.Application.Tests/Candidates/ListCandidatesHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Attendees/ListAttendeesHandlerTests.cs","beforeSha":"24237b977cacdb2571bde03483530c4d49461bef02446d9c16c054c4d41c8e16","afterSha":"dbc262fe1b172ed395df550746ab36bf51d5ad8e133b63db49fbcc642357c8cd","side":"before","part":1,"parts":1} -->

`````csharp
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
`````

## after — tests/EventBooking.Application.Tests/Attendees/ListAttendeesHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":267,"oldPath":"tests/EventBooking.Application.Tests/Candidates/ListCandidatesHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Attendees/ListAttendeesHandlerTests.cs","beforeSha":"24237b977cacdb2571bde03483530c4d49461bef02446d9c16c054c4d41c8e16","afterSha":"dbc262fe1b172ed395df550746ab36bf51d5ad8e133b63db49fbcc642357c8cd","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Attendees;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Application.Tests.Attendees;

public class ListAttendeesHandlerTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryAttendeeGroupRepository _groups = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();

    private ListAttendeesHandler Handler => new(
        _attendees, _groups, new StaffAccessAuthorizer(_roles));

    public ListAttendeesHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _groups.Items.Add(AttendeeGroup.Define(
            AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting]));
        _groups.Items.Add(AttendeeGroup.Define(
            AttendeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT",
            "Ground Operations Agent", true, [AppointmentTypeIds.MedicalCheckUp]));

        _attendees.Add(Attendee.Create(
            Guid.NewGuid(), "A. Novak", "a.novak@mail.com",
            _groups.Items.Single(group => group.Id == AttendeeGroupIds.CabinCrew)));

        var chen = Attendee.Create(
            Guid.NewGuid(), "B. Chen", "b.chen@mail.com",
            _groups.Items.Single(group => group.Id == AttendeeGroupIds.GroundOperationsAgent));
        chen.MarkInvited();
        _attendees.Add(chen);

        var diallo = Attendee.Create(
            Guid.NewGuid(), "C. Diallo", "c.diallo@mail.com",
            _groups.Items.Single(group => group.Id == AttendeeGroupIds.GroundOperationsAgent));
        diallo.MarkAwaitingAvailability();
        _attendees.Add(diallo);
    }

    [Fact]
    public async Task EveryAttendeeIsListedWithGroupAndDisplayStatus()
    {
        var result = await Handler.HandleAsync(
            new ListAttendeesQuery(Coordinator, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);

        var novak = result.Value.Single(c => c.Email == "a.novak@mail.com");
        Assert.Equal(AttendeeGroupIds.CabinCrew, novak.AttendeeGroupId);
        Assert.Equal("CABIN_CREW", novak.AttendeeGroupCode);
        Assert.Equal("Cabin Crew", novak.AttendeeGroupName);
        Assert.False(novak.RequiresAttendeeGroupReconciliation);
        Assert.Equal(
            new[] { "DAT", "MED", "UNI" },
            novak.RequiredAppointmentTypes.Select(summary => summary.Code));
        Assert.Equal(AttendeeStatus.NotYetInvited, novak.Status);
        Assert.Equal("Not yet invited", novak.StatusDisplay);

        var diallo = result.Value.Single(c => c.Email == "c.diallo@mail.com");
        Assert.Equal(AttendeeGroupIds.GroundOperationsAgent, diallo.AttendeeGroupId);
        Assert.Equal("GROUND_OPERATIONS_AGENT", diallo.AttendeeGroupCode);
        Assert.Equal("Ground Operations Agent", diallo.AttendeeGroupName);
        Assert.False(diallo.RequiresAttendeeGroupReconciliation);

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
            new ListAttendeesQuery(Coordinator, null, null), CancellationToken.None);

        Assert.Equal(new[] { "A. Novak", "B. Chen", "C. Diallo" }, result.Value.Select(c => c.Name));
    }

    [Fact]
    public async Task FilteringByStatusNarrowsTheList()
    {
        var result = await Handler.HandleAsync(
            new ListAttendeesQuery(Coordinator, AttendeeStatus.Invited, null), CancellationToken.None);

        Assert.Equal("b.chen@mail.com", Assert.Single(result.Value).Email);
    }

    [Theory]
    [InlineData("diallo")]
    [InlineData("DIALLO")]
    [InlineData("c.diallo@mail.com")]
    public async Task SearchMatchesNameOrEmailCaseInsensitively(string search)
    {
        var result = await Handler.HandleAsync(
            new ListAttendeesQuery(Coordinator, null, search), CancellationToken.None);

        Assert.Equal("C. Diallo", Assert.Single(result.Value).Name);
    }

    [Fact]
    public async Task AnEmptySearchIsIgnored()
    {
        var result = await Handler.HandleAsync(
            new ListAttendeesQuery(Coordinator, null, "   "), CancellationToken.None);

        Assert.Equal(3, result.Value.Count);
    }

    [Fact]
    public async Task ANonCoordinatorIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new ListAttendeesQuery(Guid.NewGuid(), null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Candidates/SaveCandidateHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":268,"oldPath":"tests/EventBooking.Application.Tests/Candidates/SaveCandidateHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Attendees/SaveAttendeeHandlerTests.cs","beforeSha":"48c111e95b3879b33e152d75fcad710dcf12f6c8fb3c2c9dab79addf3a2ca650","afterSha":"5392acf8b31232db9bd7432036aa629a4b79d09372c416b52b29fa8d066ba30d","side":"before","part":1,"parts":1} -->

`````csharp
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
`````

## after — tests/EventBooking.Application.Tests/Attendees/SaveAttendeeHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":268,"oldPath":"tests/EventBooking.Application.Tests/Candidates/SaveCandidateHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Attendees/SaveAttendeeHandlerTests.cs","beforeSha":"48c111e95b3879b33e152d75fcad710dcf12f6c8fb3c2c9dab79addf3a2ca650","afterSha":"5392acf8b31232db9bd7432036aa629a4b79d09372c416b52b29fa8d066ba30d","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Attendees;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Application.Tests.Attendees;

public class SaveAttendeeHandlerTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly Guid Manager = Guid.Parse("c0000001-0000-0000-0000-000000000001");

    private static readonly AttendeeGroup Pilots = AttendeeGroup.Define(
        AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
    private static readonly AttendeeGroup Engineering = AttendeeGroup.Define(
        AttendeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
        [AppointmentTypeIds.MedicalCheckUp]);

    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryAttendeeGroupRepository _groups = new();
    private readonly InMemoryBookingRepository _bookings = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private SaveAttendeeHandler Handler => new(
        _attendees,
        _groups,
        new InMemoryInviteRepository(),
        _bookings,
        new StaffAccessAuthorizer(_roles),
        new RecordingAuditLogger(),
        _unitOfWork);

    public SaveAttendeeHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _roles.Add(StaffAccessProfile.Create(
            Manager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _groups.Items.Add(Pilots);
        _groups.Items.Add(Engineering);
    }

    [Fact]
    public async Task ACoordinatorCanCreateAAttendee()
    {
        var result = await Handler.CreateAsync(
            new CreateAttendeeCommand(
                Coordinator, "Amara Novak", "a.novak@mail.com", AttendeeGroupIds.Pilots),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var attendee = Assert.Single(_attendees.Items);
        Assert.Equal(result.Value, attendee.Id);
        Assert.Equal("a.novak@mail.com", attendee.Email);
        Assert.Equal(AttendeeGroupIds.Pilots, attendee.AttendeeGroupId);
        Assert.Equal(2, attendee.Requirements.Count);
        Assert.Equal(AttendeeStatus.NotYetInvited, attendee.Status);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AManagerCannotCreateAAttendee()
    {
        var result = await Handler.CreateAsync(
            new CreateAttendeeCommand(
                Manager, "Amara Novak", "a.novak@mail.com", AttendeeGroupIds.Pilots),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Theory]
    [InlineData(null, "attendee_group_required")]
    public async Task AnAbsentGroupIsRequiredOnCreate(Guid? groupId, string code)
    {
        var result = await Handler.CreateAsync(
            new CreateAttendeeCommand(Coordinator, "Amara Novak", "a.novak@mail.com", groupId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(code, result.Error.Code);
        Assert.Empty(_attendees.Items);
    }

    [Fact]
    public async Task AnUnknownGroupIsRejectedOnCreate()
    {
        var result = await Handler.CreateAsync(
            new CreateAttendeeCommand(
                Coordinator, "Amara Novak", "a.novak@mail.com", Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("attendee_group_unknown", result.Error.Code);
        Assert.Empty(_attendees.Items);
    }

    [Fact]
    public async Task ADuplicateEmailIsAConflict()
    {
        _attendees.Add(Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", Pilots));

        var result = await Handler.CreateAsync(
            new CreateAttendeeCommand(
                Coordinator, "Someone Else", "A.Novak@Mail.com", AttendeeGroupIds.Pilots),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("a.novak@mail.com is already a attendee.", result.Error.Message);
        Assert.Single(_attendees.Items);
    }

    [Fact]
    public async Task DomainValidationSurfacesAsAValidationFailure()
    {
        var result = await Handler.CreateAsync(
            new CreateAttendeeCommand(
                Coordinator, "Amara Novak", "nope", AttendeeGroupIds.Pilots),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal("email is not a valid email address.", result.Error.Message);
    }

    [Fact]
    public async Task UpdatingChangesTheDetailsAndTheGroup()
    {
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", Pilots);
        _attendees.Add(attendee);

        var result = await Handler.UpdateAsync(
            new UpdateAttendeeCommand(
                Coordinator, attendee.Id, "Amara N. Novak", "amara@mail.com",
                AttendeeGroupIds.Engineering),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Amara N. Novak", attendee.Name);
        Assert.Equal("amara@mail.com", attendee.Email);
        Assert.Equal(AttendeeGroupIds.Engineering, attendee.AttendeeGroupId);
        Assert.Equal([AppointmentTypeIds.MedicalCheckUp], attendee.RequiredAppointmentTypeIds);
    }

    [Fact]
    public async Task UpdatingToAnotherAttendeesEmailIsAConflict()
    {
        var first = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", Pilots);
        var second = Attendee.Create(
            Guid.NewGuid(), "B. Chen", "b.chen@mail.com", Pilots);
        _attendees.Add(first);
        _attendees.Add(second);

        var result = await Handler.UpdateAsync(
            new UpdateAttendeeCommand(
                Coordinator, second.Id, "B. Chen", "a.novak@mail.com",
                AttendeeGroupIds.Pilots),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("b.chen@mail.com", second.Email);
    }

    [Fact]
    public async Task KeepingTheSameEmailOnUpdateIsNotAConflict()
    {
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", Pilots);
        _attendees.Add(attendee);

        var result = await Handler.UpdateAsync(
            new UpdateAttendeeCommand(
                Coordinator, attendee.Id, "Amara Novak", "a.novak@mail.com",
                AttendeeGroupIds.Engineering),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UpdatingAnUnknownAttendeeIsNotFound()
    {
        var result = await Handler.UpdateAsync(
            new UpdateAttendeeCommand(
                Coordinator, Guid.NewGuid(), "X", "x@mail.com", AttendeeGroupIds.Pilots),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task UpdatingWithoutAGroupIsRequired()
    {
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", Pilots);
        _attendees.Add(attendee);

        var result = await Handler.UpdateAsync(
            new UpdateAttendeeCommand(
                Coordinator, attendee.Id, "Amara Novak", "a.novak@mail.com", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("attendee_group_required", result.Error.Code);
        Assert.Equal(AttendeeGroupIds.Pilots, attendee.AttendeeGroupId);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Common/ResultTests.cs — 1/1

<!-- vocabulary-file: {"id":269,"oldPath":"tests/EventBooking.Application.Tests/Common/ResultTests.cs","newPath":"tests/EventBooking.Application.Tests/Common/ResultTests.cs","beforeSha":"3d0bfa2eb98ca36827fe60eb082932ecb71703b39ec293b0580a67692c55105e","afterSha":"3aed21817d7e23c941b53daa701a7126a9daea6a83f047d037a89f56335e48ad","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Common;

namespace EventBooking.Application.Tests.Common;

public class ResultTests
{
    [Fact]
    public void ASuccessCarriesNoError()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void AFailureCarriesTheError()
    {
        var error = Error.Conflict("No remaining capacity.");

        var result = Result.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("No remaining capacity.", result.Error.Message);
    }

    [Fact]
    public void AValueResultExposesItsValueOnSuccess()
    {
        var id = Guid.NewGuid();

        var result = Result<Guid>.Success(id);

        Assert.True(result.IsSuccess);
        Assert.Equal(id, result.Value);
    }

    [Fact]
    public void ReadingTheValueOfAFailureThrows()
    {
        var result = Result<Guid>.Failure(Error.NotFound("No such candidate."));

        var ex = Assert.Throws<InvalidOperationException>(() => result.Value);
        Assert.Equal("A failed result has no value.", ex.Message);
    }

    [Fact]
    public void TheErrorFactoriesUseTheAgreedCodes()
    {
        Assert.Equal("validation", Error.Validation("x").Code);
        Assert.Equal("not_found", Error.NotFound("x").Code);
        Assert.Equal("conflict", Error.Conflict("x").Code);
        Assert.Equal("forbidden", Error.Forbidden("x").Code);
        Assert.Equal(string.Empty, Error.None.Code);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Common/ResultTests.cs — 1/1

<!-- vocabulary-file: {"id":269,"oldPath":"tests/EventBooking.Application.Tests/Common/ResultTests.cs","newPath":"tests/EventBooking.Application.Tests/Common/ResultTests.cs","beforeSha":"3d0bfa2eb98ca36827fe60eb082932ecb71703b39ec293b0580a67692c55105e","afterSha":"3aed21817d7e23c941b53daa701a7126a9daea6a83f047d037a89f56335e48ad","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Common;

namespace EventBooking.Application.Tests.Common;

public class ResultTests
{
    [Fact]
    public void ASuccessCarriesNoError()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void AFailureCarriesTheError()
    {
        var error = Error.Conflict("No remaining capacity.");

        var result = Result.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("No remaining capacity.", result.Error.Message);
    }

    [Fact]
    public void AValueResultExposesItsValueOnSuccess()
    {
        var id = Guid.NewGuid();

        var result = Result<Guid>.Success(id);

        Assert.True(result.IsSuccess);
        Assert.Equal(id, result.Value);
    }

    [Fact]
    public void ReadingTheValueOfAFailureThrows()
    {
        var result = Result<Guid>.Failure(Error.NotFound("No such attendee."));

        var ex = Assert.Throws<InvalidOperationException>(() => result.Value);
        Assert.Equal("A failed result has no value.", ex.Message);
    }

    [Fact]
    public void TheErrorFactoriesUseTheAgreedCodes()
    {
        Assert.Equal("validation", Error.Validation("x").Code);
        Assert.Equal("not_found", Error.NotFound("x").Code);
        Assert.Equal("conflict", Error.Conflict("x").Code);
        Assert.Equal("forbidden", Error.Forbidden("x").Code);
        Assert.Equal(string.Empty, Error.None.Code);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Dashboards/AuditPortShapeTests.cs — 1/1

<!-- vocabulary-file: {"id":270,"oldPath":"tests/EventBooking.Application.Tests/Dashboards/AuditPortShapeTests.cs","newPath":"tests/EventBooking.Application.Tests/Dashboards/AuditPortShapeTests.cs","beforeSha":"abcedb270adc5cc1cf314131d9f9d8aa6e4203cfdecf14c43ed11b886b4f39c1","afterSha":"9404cc16a6f8ca983832b14ad834628efed6db7208087e3aff7892d0f34feccf","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;

namespace EventBooking.Application.Tests.Dashboards;

public class AuditPortShapeTests
{
    private sealed class StubQueries : IAuditQueries
    {
        public Task<IReadOnlyList<AuditHistoryRow>> ForEntityAsync(string entityType, Guid entityId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AuditHistoryRow>>([]);

        public Task<IReadOnlyList<AuditHistoryRow>> ForCandidateAsync(Guid candidateId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AuditHistoryRow>>([]);

        public Task<AuditSearchPage> SearchAsync(AuditSearchFilter filter, CancellationToken cancellationToken) =>
            Task.FromResult(new AuditSearchPage([], null));
    }

    [Fact]
    public async Task SearchAsyncReturnsRowsAndCursor()
    {
        IAuditQueries queries = new StubQueries();
        var filter = new AuditSearchFilter(null, null, null, null, null, ["ConfirmedSlot"], null, null, 50);
        var page = await queries.SearchAsync(filter, CancellationToken.None);
        Assert.NotNull(page.Rows);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public void FilterCarriesDefaults()
    {
        var filter = new AuditSearchFilter(null, null, null, null, null, [], null, null, 50);
        Assert.Equal(50, filter.PageSize);
        Assert.Empty(filter.AllowedEntityTypes);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Dashboards/AuditPortShapeTests.cs — 1/1

<!-- vocabulary-file: {"id":270,"oldPath":"tests/EventBooking.Application.Tests/Dashboards/AuditPortShapeTests.cs","newPath":"tests/EventBooking.Application.Tests/Dashboards/AuditPortShapeTests.cs","beforeSha":"abcedb270adc5cc1cf314131d9f9d8aa6e4203cfdecf14c43ed11b886b4f39c1","afterSha":"9404cc16a6f8ca983832b14ad834628efed6db7208087e3aff7892d0f34feccf","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;

namespace EventBooking.Application.Tests.Dashboards;

public class AuditPortShapeTests
{
    private sealed class StubQueries : IAuditQueries
    {
        public Task<IReadOnlyList<AuditHistoryRow>> ForEntityAsync(string entityType, Guid entityId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AuditHistoryRow>>([]);

        public Task<IReadOnlyList<AuditHistoryRow>> ForAttendeeAsync(Guid attendeeId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AuditHistoryRow>>([]);

        public Task<AuditSearchPage> SearchAsync(AuditSearchFilter filter, CancellationToken cancellationToken) =>
            Task.FromResult(new AuditSearchPage([], null));
    }

    [Fact]
    public async Task SearchAsyncReturnsRowsAndCursor()
    {
        IAuditQueries queries = new StubQueries();
        var filter = new AuditSearchFilter(null, null, null, null, null, ["Event"], null, null, 50);
        var page = await queries.SearchAsync(filter, CancellationToken.None);
        Assert.NotNull(page.Rows);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public void FilterCarriesDefaults()
    {
        var filter = new AuditSearchFilter(null, null, null, null, null, [], null, null, 50);
        Assert.Equal(50, filter.PageSize);
        Assert.Empty(filter.AllowedEntityTypes);
    }
}
`````
