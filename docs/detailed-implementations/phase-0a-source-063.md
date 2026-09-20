# 00a — Port source 63 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## tests/EventBooking.Application.Tests/Candidates/SaveCandidateHandlerTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Candidates/SaveCandidateHandlerTests.cs","encoding":"utf8","sha256":"48c111e95b3879b33e152d75fcad710dcf12f6c8fb3c2c9dab79addf3a2ca650","parts":1,"part":1} -->

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

## tests/EventBooking.Application.Tests/Common/ResultTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Common/ResultTests.cs","encoding":"utf8","sha256":"3d0bfa2eb98ca36827fe60eb082932ecb71703b39ec293b0580a67692c55105e","parts":1,"part":1} -->

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

## tests/EventBooking.Application.Tests/Dashboards/AuditPortShapeTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Dashboards/AuditPortShapeTests.cs","encoding":"utf8","sha256":"abcedb270adc5cc1cf314131d9f9d8aa6e4203cfdecf14c43ed11b886b4f39c1","parts":1,"part":1} -->

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

## tests/EventBooking.Application.Tests/Dashboards/GetAuditSearchHandlerTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Dashboards/GetAuditSearchHandlerTests.cs","encoding":"utf8","sha256":"adb8b8e3b3927504a44121f2ade04d5a9c26935cfb3c83866e9f3d08eb0abc52","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Application.Dashboards;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;

namespace EventBooking.Application.Tests.Dashboards;

public class GetAuditSearchHandlerTests
{
    private sealed class FakeAuthorizer(bool candidate, bool slot) : IStaffAccessAuthorizer
    {
        public Task<Result<StaffAccessContext>> AuthorizeAsync(
            Guid staffUserId,
            StaffCapability capability,
            Guid? requiredAppointmentTypeId,
            CancellationToken cancellationToken)
        {
            var granted = capability == StaffCapability.ViewCandidateAudit ? candidate : slot;
            return Task.FromResult(granted
                ? Result<StaffAccessContext>.Success(
                    new StaffAccessContext(staffUserId, new HashSet<Role>(), null))
                : Result<StaffAccessContext>.Failure(Error.Forbidden("denied")));
        }
    }

    private sealed class SpyQueries : IAuditQueries
    {
        public AuditSearchFilter? LastFilter { get; private set; }

        public int Calls { get; private set; }

        public Task<IReadOnlyList<AuditHistoryRow>> ForEntityAsync(string e, Guid id, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AuditHistoryRow>>([]);

        public Task<IReadOnlyList<AuditHistoryRow>> ForCandidateAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AuditHistoryRow>>([]);

        public Task<AuditSearchPage> SearchAsync(AuditSearchFilter filter, CancellationToken ct)
        {
            Calls++;
            LastFilter = filter;
            return Task.FromResult(new AuditSearchPage([], null));
        }
    }

    private static GetAuditSearchQuery Query(string? entityType = null) =>
        new(Guid.NewGuid(), null, null, null, null, null, entityType, null, 50);

    [Fact]
    public async Task NeitherCapabilityIsForbiddenWithoutQuerying()
    {
        var queries = new SpyQueries();
        var handler = new GetAuditSearchHandler(queries, new FakeAuthorizer(false, false));
        var result = await handler.HandleAsync(Query(), CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Equal(0, queries.Calls);
    }

    [Fact]
    public async Task SlotOnlyCallerSeesOperationalBucketOnly()
    {
        var queries = new SpyQueries();
        var handler = new GetAuditSearchHandler(queries, new FakeAuthorizer(false, true));
        var result = await handler.HandleAsync(Query(), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, queries.Calls);
        Assert.Equal(
            [AuditEntityTypes.SlotProposal, AuditEntityTypes.ConfirmedSlot, AuditEntityTypes.StaffAccessProfile],
            queries.LastFilter!.AllowedEntityTypes);
    }

    [Fact]
    public async Task CoordinatorSeesEveryEntityType()
    {
        var queries = new SpyQueries();
        var handler = new GetAuditSearchHandler(queries, new FakeAuthorizer(true, true));
        var result = await handler.HandleAsync(Query(), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(AuditEntityTypes.All, queries.LastFilter!.AllowedEntityTypes);
    }

    [Fact]
    public async Task OutOfBucketEntityTypeIsForbiddenWithoutQuerying()
    {
        var queries = new SpyQueries();
        var handler = new GetAuditSearchHandler(queries, new FakeAuthorizer(false, true));
        var result = await handler.HandleAsync(Query(AuditEntityTypes.Booking), CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Equal(0, queries.Calls);
    }

    [Fact]
    public async Task InBucketEntityTypeIsPassedThrough()
    {
        var queries = new SpyQueries();
        var handler = new GetAuditSearchHandler(queries, new FakeAuthorizer(false, true));
        var result = await handler.HandleAsync(Query(AuditEntityTypes.ConfirmedSlot), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(AuditEntityTypes.ConfirmedSlot, queries.LastFilter!.EntityType);
    }

    [Fact]
    public async Task CandidateOnlyCallerSeesCandidateBucketOnly()
    {
        var queries = new SpyQueries();
        var handler = new GetAuditSearchHandler(queries, new FakeAuthorizer(true, false));
        var result = await handler.HandleAsync(Query(), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(
            [
                AuditEntityTypes.Candidate,
                AuditEntityTypes.Invite,
                AuditEntityTypes.Booking,
                AuditEntityTypes.BookingAppointment
            ],
            queries.LastFilter!.AllowedEntityTypes);
    }

    [Fact]
    public async Task PageSizeIsClampedToTwoHundred()
    {
        var queries = new SpyQueries();
        var handler = new GetAuditSearchHandler(queries, new FakeAuthorizer(true, true));
        var result = await handler.HandleAsync(
            new GetAuditSearchQuery(Guid.NewGuid(), null, null, null, null, null, null, null, 5000),
            CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(200, queries.LastFilter!.PageSize);
    }
}
`````

## tests/EventBooking.Application.Tests/Dashboards/GetDashboardsHandlerTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Dashboards/GetDashboardsHandlerTests.cs","encoding":"utf8","sha256":"b8ad88e0c668a0a00933ba00c4bcbe446934ae7e8ba042fc61aa1b686111fa45","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Dashboards;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Tests.Dashboards;

public class GetDashboardsHandlerTests
{
    private sealed class DashboardQueries : IDashboardQueries
    {
        public Task<IReadOnlyList<AwaitingAvailabilityRow>> AwaitingAvailabilityAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AwaitingAvailabilityRow>>([]);

        public Task<IReadOnlyList<NoResponseRow>> NoResponseAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<NoResponseRow>>([]);

        public Task<IReadOnlyList<SlotOverviewRow>> SlotsOverviewAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SlotOverviewRow>>([]);

        public Task<IReadOnlyList<CandidateEmailStatusRow>> LatestEmailStatusAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CandidateEmailStatusRow>>([]);
    }

    [Fact]
    public async Task CoordinatorsCanReadTheDashboards()
    {
        var userId = Guid.NewGuid();
        var roles = new InMemoryStaffAccessProfileRepository();
        roles.Add(StaffAccessProfile.Create(userId, Role.Coordinator, null));
        var handler = new GetDashboardsHandler(new DashboardQueries(), roles);

        var result = await handler.HandleAsync(new GetDashboardsQuery(userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.AwaitingAvailability);
        Assert.Empty(result.Value.NoResponse);
        Assert.Empty(result.Value.Slots);
    }

    [Fact]
    public async Task AdminsAreForbiddenTheDashboards()
    {
        var userId = Guid.NewGuid();
        var roles = new InMemoryStaffAccessProfileRepository();
        roles.Add(StaffAccessProfile.Create(userId, Role.Admin, null));
        var handler = new GetDashboardsHandler(new DashboardQueries(), roles);

        var result = await handler.HandleAsync(new GetDashboardsQuery(userId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Fact]
    public async Task AppointmentStaffAreForbiddenTheDashboards()
    {
        var userId = Guid.NewGuid();
        var roles = new InMemoryStaffAccessProfileRepository();
        ((IStaffAccessProfileRepository)roles).Add(StaffAccessProfile.Create(
            userId, [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting));
        var handler = new GetDashboardsHandler(new DashboardQueries(), roles);

        var result = await handler.HandleAsync(new GetDashboardsQuery(userId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ManagersAndUnassignedStaffAreForbidden(bool isManager)
    {
        var userId = Guid.NewGuid();
        var roles = new InMemoryStaffAccessProfileRepository();
        if (isManager)
        {
            roles.Add(StaffAccessProfile.Create(
                userId, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        }

        var handler = new GetDashboardsHandler(new DashboardQueries(), roles);

        var result = await handler.HandleAsync(new GetDashboardsQuery(userId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }
}
`````

## tests/EventBooking.Application.Tests/Dashboards/GetSlotOperationsHandlerTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Dashboards/GetSlotOperationsHandlerTests.cs","encoding":"utf8","sha256":"d7021acb63853ac0866942d56041b51e0338248266ebf9e17b730f1210699c6f","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Application.Dashboards;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Tests.Dashboards;

public class GetSlotOperationsHandlerTests
{
    private sealed class CaptureAuthorizer(bool granted) : IStaffAccessAuthorizer
    {
        public StaffCapability? Seen { get; private set; }

        public Task<Result<StaffAccessContext>> AuthorizeAsync(
            Guid staffUserId,
            StaffCapability capability,
            Guid? requiredAppointmentTypeId,
            CancellationToken cancellationToken)
        {
            Seen = capability;
            return Task.FromResult(granted
                ? Result<StaffAccessContext>.Success(
                    new StaffAccessContext(staffUserId, new HashSet<Role>(), null))
                : Result<StaffAccessContext>.Failure(Error.Forbidden("Forbidden.")));
        }
    }

    private sealed class CountingQueries(IReadOnlyList<SlotOverviewRow> slots) : IDashboardQueries
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<AwaitingAvailabilityRow>> AwaitingAvailabilityAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AwaitingAvailabilityRow>>([]);

        public Task<IReadOnlyList<NoResponseRow>> NoResponseAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<NoResponseRow>>([]);

        public Task<IReadOnlyList<SlotOverviewRow>> SlotsOverviewAsync(CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(slots);
        }

        public Task<IReadOnlyList<CandidateEmailStatusRow>> LatestEmailStatusAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CandidateEmailStatusRow>>([]);
    }

    private static CountingQueries QueriesWithOneSlot() => new(
    [
        new SlotOverviewRow(
            Guid.NewGuid(),
            new DateOnly(2026, 9, 10),
            new TimeOnly(9, 0),
            new TimeOnly(13, 0),
            [new SlotCapacityRow("DAT", 10, 9)],
            1),
    ]);

    [Fact]
    public async Task AuthorizedCallerGetsEverySlotRow()
    {
        var queries = QueriesWithOneSlot();
        var handler = new GetSlotOperationsHandler(queries, new CaptureAuthorizer(true));

        var result = await handler.HandleAsync(
            new GetSlotOperationsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var slot = Assert.Single(result.Value.Slots);
        Assert.Equal(new DateOnly(2026, 9, 10), slot.Date);
        Assert.Equal(1, slot.ActiveBookings);
    }

    [Fact]
    public async Task UsesViewSlotOperationsCapability()
    {
        var authorizer = new CaptureAuthorizer(true);
        var handler = new GetSlotOperationsHandler(QueriesWithOneSlot(), authorizer);

        await handler.HandleAsync(new GetSlotOperationsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(StaffCapability.ViewSlotOperations, authorizer.Seen);
    }

    [Fact]
    public async Task TheQueryIsNotScopedToOneAppointmentType()
    {
        var authorizer = new CaptureAuthorizer(true);
        var handler = new GetSlotOperationsHandler(QueriesWithOneSlot(), authorizer);

        var result = await handler.HandleAsync(
            new GetSlotOperationsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task DeniedCallerGetsForbiddenWithoutSlotData()
    {
        var queries = new CountingQueries([]);
        var handler = new GetSlotOperationsHandler(queries, new CaptureAuthorizer(false));

        var result = await handler.HandleAsync(
            new GetSlotOperationsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(0, queries.Calls);
    }

    [Fact]
    public void SlotOperationsViewCarriesNoCandidateShapedProperty()
    {
        var names = string.Join(
            ",",
            typeof(SlotOperationsView).GetProperties().Select(p => p.Name)
                .Concat(typeof(SlotOverviewRow).GetProperties().Select(p => p.Name)));

        Assert.DoesNotContain("CandidateId", names);
        Assert.DoesNotContain("Name", names);
        Assert.DoesNotContain("Email", names);
    }
}
`````

## tests/EventBooking.Application.Tests/EventBooking.Application.Tests.csproj — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/EventBooking.Application.Tests.csproj","encoding":"utf8","sha256":"69043a951b906a0b2edd068dfd0e109e06b743dbc5107cbcf21b27bb0cefd51d","parts":1,"part":1} -->

`````text
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\EventBooking.Application\EventBooking.Application.csproj" />
    <ProjectReference Include="..\..\src\EventBooking.Domain\EventBooking.Domain.csproj" />
  </ItemGroup>

</Project>
`````

## tests/EventBooking.Application.Tests/Fakes/EmailDeliveryTestFactory.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Fakes/EmailDeliveryTestFactory.cs","encoding":"utf8","sha256":"6baea134673c2e4627ae671f268c3c57f5b0bf4e61ab4412dbabc2e54d9cb4d9","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventBooking.Application.Tests.Fakes;

/// <summary>Builds the durable delivery coordinator used by application tests.</summary>
public static class EmailDeliveryTestFactory
{
    /// <summary>Creates a coordinator backed by the supplied delivery repository and fakes.</summary>
    public static EmailDeliveryService Create(
        IEmailDeliveryRepository deliveries,
        IEmailSender sender,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<EmailDeliveryService>? logger = null) =>
        new(deliveries, sender, unitOfWork, clock, logger ?? NullLogger<EmailDeliveryService>.Instance);
}
`````

## tests/EventBooking.Application.Tests/Fakes/FakeClock.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Fakes/FakeClock.cs","encoding":"utf8","sha256":"62b66fbb5fbda744da0b7a77821277841cdecb5e188ac0c4750b48777e87b668","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;

namespace EventBooking.Application.Tests.Fakes;

public sealed class FakeClock : IClock
{
    public FakeClock(DateTimeOffset? utcNow = null)
    {
        UtcNow = utcNow ?? new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);
    }

    public DateTimeOffset UtcNow { get; set; }

    /// <inheritdoc/>
    public DateTimeOffset NowAtHeadOffice => UtcNow;

    public DateOnly TodayAtHeadOffice => DateAtHeadOffice(UtcNow);

    public DateOnly DateAtHeadOffice(DateTimeOffset instant) => DateOnly.FromDateTime(instant.UtcDateTime);

    /// <inheritdoc/>
    // This fake treats head office as UTC, exactly as NowAtHeadOffice and DateAtHeadOffice do, so
    // application-layer tests stay deterministic without a real time-zone database.
    public DateTimeOffset InstantAtHeadOffice(DateTimeOffset instant) => instant.ToUniversalTime();

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}
`````

## tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs","encoding":"utf8","sha256":"7e768a09219be66462e79a6f8d8ada0a1caad188cb1e2c7301fade57909df6e6","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Fakes;

public class FakesSelfTests
{
    [Fact]
    public void TheClockCanBeMoved()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

        clock.Advance(TimeSpan.FromDays(5));

        Assert.Equal(new DateOnly(2026, 9, 8), clock.TodayAtHeadOffice);
    }

    [Fact]
    public async Task TheCandidateRepositoryFiltersByStatus()
    {
        var repository = new InMemoryCandidateRepository();
        var uniformOnly = EmployeeGroup.Define(
            Guid.NewGuid(), "UNI_ONLY", "UNI only", true, [AppointmentTypeIds.UniformFitting]);
        var invited = Candidate.Create(
            Guid.NewGuid(), "B. Chen", "b.chen@mail.com", uniformOnly);
        invited.MarkInvited();
        repository.Add(invited);
        repository.Add(Candidate.Create(
            Guid.NewGuid(), "A. Novak", "a.novak@mail.com", uniformOnly));

        var result = await repository.ListAsync(CandidateStatus.Invited, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("b.chen@mail.com", result[0].Email);
    }

    [Fact]
    public async Task TheSlotRepositoryHidesCancelledAndPastSlots()
    {
        var repository = new InMemoryConfirmedSlotRepository();
        repository.Add(SlotFor(new DateOnly(2026, 9, 1)));
        var cancelled = SlotFor(new DateOnly(2026, 9, 20));
        cancelled.Cancel();
        repository.Add(cancelled);
        repository.Add(SlotFor(new DateOnly(2026, 9, 21)));

        var result = await repository.ListActiveAsync(new DateOnly(2026, 9, 3), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(new DateOnly(2026, 9, 21), result[0].Window.Date);
    }

    [Fact]
    public async Task TheCapacityRepositoryReturnsRowsInAppointmentTypeOrder()
    {
        var slots = new InMemoryConfirmedSlotRepository();
        var slot = SlotFor(new DateOnly(2026, 9, 21));
        slots.Add(slot);
        var capacities = new InMemorySlotCapacityRepository(slots);

        var locked = await capacities
            .LockForUpdateAsync(slot.Id, AppointmentTypeIds.All, CancellationToken.None);

        Assert.Equal(3, locked.Count);
        Assert.Equal(
            locked.Select(c => c.AppointmentTypeId).OrderBy(id => id),
            locked.Select(c => c.AppointmentTypeId));
        Assert.Equal(1, capacities.LockCallCount);
    }

    [Fact]
    public void TheTokenServiceRoundTripsAnIdentifier()
    {
        var service = new FakeTokenService();
        var id = Guid.NewGuid();

        var issued = service.Issue(id);

        Assert.True(service.TryRead(issued.Token, out var read));
        Assert.Equal(id, read);
        Assert.Equal(issued.TokenHash, service.Hash(issued.Token));
        Assert.False(service.TryRead("nonsense", out _));
    }

    [Fact]
    public void TheAuditLoggerRecordsWhatItIsGiven()
    {
        var logger = new RecordingAuditLogger();

        logger.Record(
            AuditEntityTypes.Booking, Guid.NewGuid(), AuditAction.BookingCreated,
            ActorType.CandidateToken, "invite-1", "chose option 2");

        Assert.True(logger.Contains(AuditAction.BookingCreated));
        Assert.Equal("chose option 2", logger.Entries.Single().Details);
    }

    private static ConfirmedSlot SlotFor(DateOnly date)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(date, new TimeOnly(9, 0)), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        return ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);
    }
}
`````

## tests/EventBooking.Application.Tests/Fakes/FakeTokenService.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Fakes/FakeTokenService.cs","encoding":"utf8","sha256":"68d42921416295617bff79a9524616f8eb513078b055a6abe53966a99c4d5bf2","parts":1,"part":1} -->

`````csharp
using System.Security.Cryptography;
using System.Text;
using EventBooking.Application.Abstractions;

namespace EventBooking.Application.Tests.Fakes;

/// <summary>
/// A deterministic stand-in for the real HMAC service in Task 50. Tokens remain readable enough to
/// recover their entity identifiers, while hashes model the opaque values persisted by the system.
/// </summary>
public sealed class FakeTokenService : ITokenService
{
    public IssuedToken Issue(Guid entityId)
    {
        var token = $"token-for-{entityId:N}";
        return new IssuedToken(token, Hash(token));
    }

    public bool TryRead(string? token, out Guid entityId)
    {
        entityId = Guid.Empty;

        if (token is null || !token.StartsWith("token-for-", StringComparison.Ordinal))
        {
            return false;
        }

        return Guid.TryParseExact(token["token-for-".Length..], "N", out entityId);
    }

    public string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
`````

## tests/EventBooking.Application.Tests/Fakes/FakeUnitOfWork.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Fakes/FakeUnitOfWork.cs","encoding":"utf8","sha256":"4959d4b04e5d017bbc2db9d1bd690cd25166a5e28efea54cf41d0a0e439c809c","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using System.Collections.Concurrent;

namespace EventBooking.Application.Tests.Fakes;

public sealed class TransactionOperationLog
{
    public List<string> Events { get; } = [];

    public void Record(string operation) => Events.Add(operation);
}

/// <summary>
/// Models database row-lock ownership for application interleaving tests. A lock acquired by a
/// repository remains unavailable until its owning fake transaction commits, rolls back, or is
/// disposed.
/// </summary>
public sealed class TransactionalSlotLockCoordinator
{
    private readonly ConcurrentDictionary<Guid, SlotLock> _locks = new();
    private readonly AsyncLocal<TransactionSession?> _currentSession = new();

    public TransactionSession BeginTransaction()
    {
        var session = new TransactionSession(this, _currentSession.Value);
        _currentSession.Value = session;
        return session;
    }

    public async Task AcquireAsync(Guid confirmedSlotId, CancellationToken cancellationToken)
    {
        var session = _currentSession.Value
            ?? throw new InvalidOperationException("A slot guard requires an active transaction.");
        var slotLock = _locks.GetOrAdd(confirmedSlotId, _ => new SlotLock());

        if (!slotLock.Gate.Wait(0))
        {
            slotLock.Waiting.TrySetResult(true);
            await slotLock.Gate.WaitAsync(cancellationToken);
        }

        session.Hold(slotLock);
        slotLock.Held.TrySetResult(true);
    }

    public Task WaitUntilHeldAsync(Guid confirmedSlotId) =>
        _locks.GetOrAdd(confirmedSlotId, _ => new SlotLock()).Held.Task;

    public Task WaitUntilWaitingAsync(Guid confirmedSlotId) =>
        _locks.GetOrAdd(confirmedSlotId, _ => new SlotLock()).Waiting.Task;

    public sealed class TransactionSession
    {
        private readonly TransactionalSlotLockCoordinator _owner;
        private readonly TransactionSession? _previous;
        private readonly List<SlotLock> _held = [];
        private bool _released;

        internal TransactionSession(
            TransactionalSlotLockCoordinator owner,
            TransactionSession? previous)
        {
            _owner = owner;
            _previous = previous;
        }

        internal void Hold(SlotLock slotLock) => _held.Add(slotLock);

        public void Release()
        {
            if (_released)
            {
                return;
            }

            _released = true;

            foreach (var slotLock in _held)
            {
                slotLock.Gate.Release();
            }

            if (_owner._currentSession.Value == this)
            {
                _owner._currentSession.Value = _previous;
            }
        }
    }

    internal sealed class SlotLock
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public TaskCompletionSource<bool> Held { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Waiting { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

public sealed class FakeUnitOfWork(
    TransactionOperationLog? operations = null,
    TransactionalSlotLockCoordinator? locks = null) : IUnitOfWork
{
    /// <summary>When true, the next commit throws after the transaction has been staged.</summary>
    public bool ThrowOnCommit { get; set; }

    public int SaveCount { get; private set; }

    public int CommitCount { get; private set; }

    public int RollbackCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.FromResult(0);
    }

    public Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        operations?.Record("transaction-begun");
        return Task.FromResult<ITransactionScope>(new Scope(this, locks?.BeginTransaction()));
    }

    private sealed class Scope(
        FakeUnitOfWork owner,
        TransactionalSlotLockCoordinator.TransactionSession? lockSession) : ITransactionScope
    {
        public Task CommitAsync(CancellationToken cancellationToken)
        {
            owner.CommitCount++;
            if (owner.ThrowOnCommit)
            {
                owner.ThrowOnCommit = false;
                throw new InvalidOperationException("simulated commit failure");
            }

            lockSession?.Release();
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken)
        {
            owner.RollbackCount++;
            lockSession?.Release();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            lockSession?.Release();
            return ValueTask.CompletedTask;
        }
    }
}
`````

## tests/EventBooking.Application.Tests/Fakes/InMemoryQueries.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Fakes/InMemoryQueries.cs","encoding":"utf8","sha256":"2bb45fbcb6ba2e137a741ddcea9d08d1090929d31463756a6a215670407db4ca","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Candidates;

namespace EventBooking.Application.Tests.Fakes;

/// <summary>Serves one canned readiness snapshot while counting query executions.</summary>
public sealed class InMemoryQueries : ICandidateReadinessQueries
{
    /// <summary>Gets the snapshot returned for any candidate.</summary>
    public CandidateReadinessSnapshot? Snapshot { get; set; }

    /// <summary>Gets how many times the snapshot was requested.</summary>
    public int QueryCount { get; private set; }

    /// <inheritdoc />
    public Task<CandidateReadinessSnapshot?> GetSnapshotAsync(
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        QueryCount++;
        return Task.FromResult(Snapshot);
    }
}


/// <summary>Returns a fixed active-booking listing for the staff cancellation workflow.</summary>
public sealed class InMemoryCandidateBookingQueries : ICandidateBookingQueries
{
    /// <summary>Gets the rows returned for any candidate; null stands for an unknown candidate.</summary>
    public IReadOnlyList<CandidateBookingSummary>? Rows { get; set; } = [];

    /// <summary>Gets how many times the listing was requested.</summary>
    public int QueryCount { get; private set; }

    /// <inheritdoc />
    public Task<IReadOnlyList<CandidateBookingSummary>?> ListActiveForCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        QueryCount++;
        return Task.FromResult(Rows);
    }
}
`````
