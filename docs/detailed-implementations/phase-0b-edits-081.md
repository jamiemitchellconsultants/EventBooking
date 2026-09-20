# 00b — Vocabulary edits 81 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — tests/EventBooking.Application.Tests/Dashboards/GetAuditSearchHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":271,"oldPath":"tests/EventBooking.Application.Tests/Dashboards/GetAuditSearchHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Dashboards/GetAuditSearchHandlerTests.cs","beforeSha":"adb8b8e3b3927504a44121f2ade04d5a9c26935cfb3c83866e9f3d08eb0abc52","afterSha":"cc12c2a751b953fd5f915abd494fcfb0750c8fe2a57923de93a877ee5e0f348e","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Application.Tests/Dashboards/GetAuditSearchHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":271,"oldPath":"tests/EventBooking.Application.Tests/Dashboards/GetAuditSearchHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Dashboards/GetAuditSearchHandlerTests.cs","beforeSha":"adb8b8e3b3927504a44121f2ade04d5a9c26935cfb3c83866e9f3d08eb0abc52","afterSha":"cc12c2a751b953fd5f915abd494fcfb0750c8fe2a57923de93a877ee5e0f348e","side":"after","part":1,"parts":1} -->

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
    private sealed class FakeAuthorizer(bool attendee, bool eventItem) : IStaffAccessAuthorizer
    {
        public Task<Result<StaffAccessContext>> AuthorizeAsync(
            Guid staffUserId,
            StaffCapability capability,
            Guid? requiredAppointmentTypeId,
            CancellationToken cancellationToken)
        {
            var granted = capability == StaffCapability.ViewAttendeeAudit ? attendee : eventItem;
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

        public Task<IReadOnlyList<AuditHistoryRow>> ForAttendeeAsync(Guid id, CancellationToken ct) =>
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
    public async Task EventOnlyCallerSeesOperationalBucketOnly()
    {
        var queries = new SpyQueries();
        var handler = new GetAuditSearchHandler(queries, new FakeAuthorizer(false, true));
        var result = await handler.HandleAsync(Query(), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, queries.Calls);
        Assert.Equal(
            [AuditEntityTypes.EventProposal, AuditEntityTypes.Event, AuditEntityTypes.StaffAccessProfile],
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
        var result = await handler.HandleAsync(Query(AuditEntityTypes.Event), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(AuditEntityTypes.Event, queries.LastFilter!.EntityType);
    }

    [Fact]
    public async Task AttendeeOnlyCallerSeesAttendeeBucketOnly()
    {
        var queries = new SpyQueries();
        var handler = new GetAuditSearchHandler(queries, new FakeAuthorizer(true, false));
        var result = await handler.HandleAsync(Query(), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(
            [
                AuditEntityTypes.Attendee,
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

## before — tests/EventBooking.Application.Tests/Dashboards/GetDashboardsHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":272,"oldPath":"tests/EventBooking.Application.Tests/Dashboards/GetDashboardsHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Dashboards/GetDashboardsHandlerTests.cs","beforeSha":"b8ad88e0c668a0a00933ba00c4bcbe446934ae7e8ba042fc61aa1b686111fa45","afterSha":"6cfd45942de786daa9f9de66c9d0a888c272738eda886bf1ccce91821a66dbd7","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Application.Tests/Dashboards/GetDashboardsHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":272,"oldPath":"tests/EventBooking.Application.Tests/Dashboards/GetDashboardsHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Dashboards/GetDashboardsHandlerTests.cs","beforeSha":"b8ad88e0c668a0a00933ba00c4bcbe446934ae7e8ba042fc61aa1b686111fa45","afterSha":"6cfd45942de786daa9f9de66c9d0a888c272738eda886bf1ccce91821a66dbd7","side":"after","part":1,"parts":1} -->

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

        public Task<IReadOnlyList<EventOverviewRow>> EventsOverviewAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EventOverviewRow>>([]);

        public Task<IReadOnlyList<AttendeeEmailStatusRow>> LatestEmailStatusAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AttendeeEmailStatusRow>>([]);
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
        Assert.Empty(result.Value.Events);
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

## before — tests/EventBooking.Application.Tests/Dashboards/GetSlotOperationsHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":273,"oldPath":"tests/EventBooking.Application.Tests/Dashboards/GetSlotOperationsHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Dashboards/GetEventOperationsHandlerTests.cs","beforeSha":"d7021acb63853ac0866942d56041b51e0338248266ebf9e17b730f1210699c6f","afterSha":"3fe86198f4194a997dc9d5468eb3f71deee6802b6a8a8b570396b217cb21b6fc","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Application.Tests/Dashboards/GetEventOperationsHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":273,"oldPath":"tests/EventBooking.Application.Tests/Dashboards/GetSlotOperationsHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Dashboards/GetEventOperationsHandlerTests.cs","beforeSha":"d7021acb63853ac0866942d56041b51e0338248266ebf9e17b730f1210699c6f","afterSha":"3fe86198f4194a997dc9d5468eb3f71deee6802b6a8a8b570396b217cb21b6fc","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Application.Dashboards;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Tests.Dashboards;

public class GetEventOperationsHandlerTests
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

    private sealed class CountingQueries(IReadOnlyList<EventOverviewRow> events) : IDashboardQueries
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<AwaitingAvailabilityRow>> AwaitingAvailabilityAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AwaitingAvailabilityRow>>([]);

        public Task<IReadOnlyList<NoResponseRow>> NoResponseAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<NoResponseRow>>([]);

        public Task<IReadOnlyList<EventOverviewRow>> EventsOverviewAsync(CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(events);
        }

        public Task<IReadOnlyList<AttendeeEmailStatusRow>> LatestEmailStatusAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AttendeeEmailStatusRow>>([]);
    }

    private static CountingQueries QueriesWithOneEvent() => new(
    [
        new EventOverviewRow(
            Guid.NewGuid(),
            new DateOnly(2026, 9, 10),
            new TimeOnly(9, 0),
            new TimeOnly(13, 0),
            [new EventCapacityRow("DAT", 10, 9)],
            1),
    ]);

    [Fact]
    public async Task AuthorizedCallerGetsEveryEventRow()
    {
        var queries = QueriesWithOneEvent();
        var handler = new GetEventOperationsHandler(queries, new CaptureAuthorizer(true));

        var result = await handler.HandleAsync(
            new GetEventOperationsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var eventItem = Assert.Single(result.Value.Events);
        Assert.Equal(new DateOnly(2026, 9, 10), eventItem.Date);
        Assert.Equal(1, eventItem.ActiveBookings);
    }

    [Fact]
    public async Task UsesViewEventOperationsCapability()
    {
        var authorizer = new CaptureAuthorizer(true);
        var handler = new GetEventOperationsHandler(QueriesWithOneEvent(), authorizer);

        await handler.HandleAsync(new GetEventOperationsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(StaffCapability.ViewEventOperations, authorizer.Seen);
    }

    [Fact]
    public async Task TheQueryIsNotScopedToOneAppointmentType()
    {
        var authorizer = new CaptureAuthorizer(true);
        var handler = new GetEventOperationsHandler(QueriesWithOneEvent(), authorizer);

        var result = await handler.HandleAsync(
            new GetEventOperationsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task DeniedCallerGetsForbiddenWithoutEventData()
    {
        var queries = new CountingQueries([]);
        var handler = new GetEventOperationsHandler(queries, new CaptureAuthorizer(false));

        var result = await handler.HandleAsync(
            new GetEventOperationsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(0, queries.Calls);
    }

    [Fact]
    public void EventOperationsViewCarriesNoAttendeeShapedProperty()
    {
        var names = string.Join(
            ",",
            typeof(EventOperationsView).GetProperties().Select(p => p.Name)
                .Concat(typeof(EventOverviewRow).GetProperties().Select(p => p.Name)));

        Assert.DoesNotContain("AttendeeId", names);
        Assert.DoesNotContain("Name", names);
        Assert.DoesNotContain("Email", names);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Fakes/FakeClock.cs — 1/1

<!-- vocabulary-file: {"id":274,"oldPath":"tests/EventBooking.Application.Tests/Fakes/FakeClock.cs","newPath":"tests/EventBooking.Application.Tests/Fakes/FakeClock.cs","beforeSha":"62b66fbb5fbda744da0b7a77821277841cdecb5e188ac0c4750b48777e87b668","afterSha":"a31aad7a16708937b4bd72a844aa661ea7d5824acdd4e8ea1537bba426e1bb29","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Application.Tests/Fakes/FakeClock.cs — 1/1

<!-- vocabulary-file: {"id":274,"oldPath":"tests/EventBooking.Application.Tests/Fakes/FakeClock.cs","newPath":"tests/EventBooking.Application.Tests/Fakes/FakeClock.cs","beforeSha":"62b66fbb5fbda744da0b7a77821277841cdecb5e188ac0c4750b48777e87b668","afterSha":"a31aad7a16708937b4bd72a844aa661ea7d5824acdd4e8ea1537bba426e1bb29","side":"after","part":1,"parts":1} -->

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
    public DateTimeOffset NowAtTransitionalLocation => UtcNow;

    public DateOnly TodayAtTransitionalLocation => DateAtTransitionalLocation(UtcNow);

    public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) => DateOnly.FromDateTime(instant.UtcDateTime);

    /// <inheritdoc/>
    // This fake treats transitional location as UTC, exactly as NowAtTransitionalLocation and DateAtTransitionalLocation do, so
    // application-layer tests stay deterministic without a real time-zone database.
    public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => instant.ToUniversalTime();

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}
`````

## before — tests/EventBooking.Application.Tests/Fakes/FakeUnitOfWork.cs — 1/1

<!-- vocabulary-file: {"id":275,"oldPath":"tests/EventBooking.Application.Tests/Fakes/FakeUnitOfWork.cs","newPath":"tests/EventBooking.Application.Tests/Fakes/FakeUnitOfWork.cs","beforeSha":"4959d4b04e5d017bbc2db9d1bd690cd25166a5e28efea54cf41d0a0e439c809c","afterSha":"49906b73042352129a362ee64c56b9c5bc0738a5019d5dfc557f0bb9edc12ee7","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Application.Tests/Fakes/FakeUnitOfWork.cs — 1/1

<!-- vocabulary-file: {"id":275,"oldPath":"tests/EventBooking.Application.Tests/Fakes/FakeUnitOfWork.cs","newPath":"tests/EventBooking.Application.Tests/Fakes/FakeUnitOfWork.cs","beforeSha":"4959d4b04e5d017bbc2db9d1bd690cd25166a5e28efea54cf41d0a0e439c809c","afterSha":"49906b73042352129a362ee64c56b9c5bc0738a5019d5dfc557f0bb9edc12ee7","side":"after","part":1,"parts":1} -->

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
public sealed class TransactionalEventLockCoordinator
{
    private readonly ConcurrentDictionary<Guid, EventLock> _locks = new();
    private readonly AsyncLocal<TransactionSession?> _currentSession = new();

    public TransactionSession BeginTransaction()
    {
        var session = new TransactionSession(this, _currentSession.Value);
        _currentSession.Value = session;
        return session;
    }

    public async Task AcquireAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var session = _currentSession.Value
            ?? throw new InvalidOperationException("A event guard requires an active transaction.");
        var eventLock = _locks.GetOrAdd(eventId, _ => new EventLock());

        if (!eventLock.Gate.Wait(0))
        {
            eventLock.Waiting.TrySetResult(true);
            await eventLock.Gate.WaitAsync(cancellationToken);
        }

        session.Hold(eventLock);
        eventLock.Held.TrySetResult(true);
    }

    public Task WaitUntilHeldAsync(Guid eventId) =>
        _locks.GetOrAdd(eventId, _ => new EventLock()).Held.Task;

    public Task WaitUntilWaitingAsync(Guid eventId) =>
        _locks.GetOrAdd(eventId, _ => new EventLock()).Waiting.Task;

    public sealed class TransactionSession
    {
        private readonly TransactionalEventLockCoordinator _owner;
        private readonly TransactionSession? _previous;
        private readonly List<EventLock> _held = [];
        private bool _released;

        internal TransactionSession(
            TransactionalEventLockCoordinator owner,
            TransactionSession? previous)
        {
            _owner = owner;
            _previous = previous;
        }

        internal void Hold(EventLock eventLock) => _held.Add(eventLock);

        public void Release()
        {
            if (_released)
            {
                return;
            }

            _released = true;

            foreach (var eventLock in _held)
            {
                eventLock.Gate.Release();
            }

            if (_owner._currentSession.Value == this)
            {
                _owner._currentSession.Value = _previous;
            }
        }
    }

    internal sealed class EventLock
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public TaskCompletionSource<bool> Held { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Waiting { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

public sealed class FakeUnitOfWork(
    TransactionOperationLog? operations = null,
    TransactionalEventLockCoordinator? locks = null) : IUnitOfWork
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
        TransactionalEventLockCoordinator.TransactionSession? lockSession) : ITransactionScope
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

## before — tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs — 1/1

<!-- vocabulary-file: {"id":276,"oldPath":"tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs","newPath":"tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs","beforeSha":"7e768a09219be66462e79a6f8d8ada0a1caad188cb1e2c7301fade57909df6e6","afterSha":"170ef7534fd0bc180f57d1a33295cee9257dd49ffcaf3e8e6e560b8eb93e0f27","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs — 1/1

<!-- vocabulary-file: {"id":276,"oldPath":"tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs","newPath":"tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs","beforeSha":"7e768a09219be66462e79a6f8d8ada0a1caad188cb1e2c7301fade57909df6e6","afterSha":"170ef7534fd0bc180f57d1a33295cee9257dd49ffcaf3e8e6e560b8eb93e0f27","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Fakes;

public class FakesSelfTests
{
    [Fact]
    public void TheClockCanBeMoved()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

        clock.Advance(TimeSpan.FromDays(5));

        Assert.Equal(new DateOnly(2026, 9, 8), clock.TodayAtTransitionalLocation);
    }

    [Fact]
    public async Task TheAttendeeRepositoryFiltersByStatus()
    {
        var repository = new InMemoryAttendeeRepository();
        var uniformOnly = AttendeeGroup.Define(
            Guid.NewGuid(), "UNI_ONLY", "UNI only", true, [AppointmentTypeIds.UniformFitting]);
        var invited = Attendee.Create(
            Guid.NewGuid(), "B. Chen", "b.chen@mail.com", uniformOnly);
        invited.MarkInvited();
        repository.Add(invited);
        repository.Add(Attendee.Create(
            Guid.NewGuid(), "A. Novak", "a.novak@mail.com", uniformOnly));

        var result = await repository.ListAsync(AttendeeStatus.Invited, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("b.chen@mail.com", result[0].Email);
    }

    [Fact]
    public async Task TheEventRepositoryHidesCancelledAndPastEvents()
    {
        var repository = new InMemoryEventRepository();
        repository.Add(EventFor(new DateOnly(2026, 9, 1)));
        var cancelled = EventFor(new DateOnly(2026, 9, 20));
        cancelled.Cancel();
        repository.Add(cancelled);
        repository.Add(EventFor(new DateOnly(2026, 9, 21)));

        var result = await repository.ListActiveAsync(new DateOnly(2026, 9, 3), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(new DateOnly(2026, 9, 21), result[0].Window.Date);
    }

    [Fact]
    public async Task TheCapacityRepositoryReturnsRowsInAppointmentTypeOrder()
    {
        var events = new InMemoryEventRepository();
        var eventItem = EventFor(new DateOnly(2026, 9, 21));
        events.Add(eventItem);
        var capacities = new InMemoryEventCapacityRepository(events);

        var locked = await capacities
            .LockForUpdateAsync(eventItem.Id, AppointmentTypeIds.All, CancellationToken.None);

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
            ActorType.AttendeeToken, "invite-1", "chose option 2");

        Assert.True(logger.Contains(AuditAction.BookingCreated));
        Assert.Equal("chose option 2", logger.Entries.Single().Details);
    }

    private static Event EventFor(DateOnly date)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(9, 0)), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        return Event.CreateFrom(Guid.NewGuid(), proposal);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Fakes/InMemoryQueries.cs — 1/1

<!-- vocabulary-file: {"id":277,"oldPath":"tests/EventBooking.Application.Tests/Fakes/InMemoryQueries.cs","newPath":"tests/EventBooking.Application.Tests/Fakes/InMemoryQueries.cs","beforeSha":"2bb45fbcb6ba2e137a741ddcea9d08d1090929d31463756a6a215670407db4ca","afterSha":"5557e7d04631425ddc8535d240abc8d28dbc884f53ee68f3a92bf2527ced962b","side":"before","part":1,"parts":1} -->

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
