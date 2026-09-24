using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Attendees;
using EventBooking.Application.Dashboards;
using EventBooking.Application.ReadModels;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Time;

namespace EventBooking.Application.Tests.Access;

public class AdminAttendeeDataIsolationTests
{
    [Fact]
    public async Task AdminAttendeeListIsDeniedBeforeQueryInvocation()
    {
        var admin = Guid.NewGuid();
        var profiles = Profiles(StaffAccessProfile.Create(admin, [Role.Admin], null));
        var queries = new CountingAttendeeListQueries();
        var handler = new ListAttendeesHandler(queries, profiles);

        var result = await handler.HandleAsync(
            new ListAttendeesQuery(admin, null, 50, null, null, null, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(0, queries.Calls);
    }

    [Fact]
    public async Task AdminDashboardIsDeniedBeforeAnyQueryInvocation()
    {
        var admin = Guid.NewGuid();
        var profiles = Profiles(StaffAccessProfile.Create(admin, [Role.Admin], null));
        var queries = new CountingDashboardQueries();
        var handler = new GetDashboardsHandler(
            queries, profiles, new FakeClock(), ProposalFixture.Zones);

        var result = await handler.HandleAsync(
            new GetDashboardsQuery(admin, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(0, queries.Calls);
    }

    [Fact]
    public async Task CoordinatorAttendeeListStillRuns()
    {
        var coordinator = Guid.NewGuid();
        var profiles = Profiles(StaffAccessProfile.Create(coordinator, [Role.Coordinator], null));
        var queries = new CountingAttendeeListQueries();
        var handler = new ListAttendeesHandler(queries, profiles);

        var result = await handler.HandleAsync(
            new ListAttendeesQuery(coordinator, null, 50, null, null, null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, queries.Calls);
    }

    [Fact]
    public async Task AdminCanReadEventAuditButNotAttendeeAudit()
    {
        var admin = Guid.NewGuid();
        var profiles = Profiles(StaffAccessProfile.Create(admin, [Role.Admin], null));
        var queries = new CountingAuditQueries();
        var handler = new GetAuditHistoryHandler(queries, new StaffAccessAuthorizer(profiles));

        var attendee = await handler.HandleAsync(
            new GetAuditHistoryQuery(admin, null, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(attendee.IsFailure);
        Assert.Equal(0, queries.Calls);

        var eventItem = await handler.HandleAsync(
            new GetAuditHistoryQuery(
                admin, AuditEntityTypes.Event, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(eventItem.IsSuccess);
        Assert.Equal(1, queries.Calls);
    }

    private static InMemoryStaffAccessProfileRepository Profiles(StaffAccessProfile profile)
    {
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(profile);
        return profiles;
    }

    private sealed class CountingAttendeeListQueries : IAttendeeListQueries
    {
        public int Calls { get; private set; }

        public Task<AttendeeListView> ListAttendeesAsync(
            CallerShape shape, string? cursor, int limit, string? status,
            Guid? attendeeGroupId, string? readiness, string? nameOrEmailPrefix,
            CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new AttendeeListView([], null));
        }
    }

    private sealed class CountingDashboardQueries : IDashboardQueries
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<AwaitingAvailabilityRow>> AwaitingAvailabilityAsync(
            CancellationToken cancellationToken) => Return<AwaitingAvailabilityRow>();

        public Task<IReadOnlyList<NoResponseRow>> NoResponseAsync(
            CancellationToken cancellationToken) => Return<NoResponseRow>();

        public Task<IReadOnlyList<EventOverviewRow>> EventsOverviewAsync(
            CancellationToken cancellationToken) => Return<EventOverviewRow>();

        public Task<IReadOnlyList<AttendeeEmailStatusRow>> LatestEmailStatusAsync(
            CancellationToken cancellationToken) => Return<AttendeeEmailStatusRow>();

        public Task<DashboardsView> GetDashboardsAsync(
            CallerShape shape, Guid? locationId, DateTimeOffset now,
            IEventWindowZones zones, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new DashboardsView(
                new AwaitingAvailabilityTab(0, []), new NoResponseTab(0, []),
                new EventsTab(0, []), 0, 0));
        }

        private Task<IReadOnlyList<T>> Return<T>()
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<T>>([]);
        }
    }

    private sealed class CountingAuditQueries : IAuditQueries
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<AuditHistoryRow>> ForEntityAsync(
            string entityType,
            Guid entityId,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<AuditHistoryRow>>([]);
        }

        public Task<IReadOnlyList<AuditHistoryRow>> ForAttendeeAsync(
            Guid attendeeId,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<AuditHistoryRow>>([]);
        }

        public Task<AuditSearchPage> SearchAsync(
            AuditSearchFilter filter,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new AuditSearchPage([], null));
        }
    }
}
