using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Attendees;
using EventBooking.Application.Dashboards;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Tests.Access;

public class AdminAttendeeDataIsolationTests
{
    [Fact]
    public async Task AdminAttendeeListIsDeniedBeforeRepositoryInvocation()
    {
        var admin = Guid.NewGuid();
        var profiles = Profiles(StaffAccessProfile.Create(admin, [Role.Admin], null));
        var attendees = new CountingAttendeeRepository();
        var handler = new ListAttendeesHandler(attendees, new InMemoryAttendeeGroupRepository(), new StaffAccessAuthorizer(profiles));

        var result = await handler.HandleAsync(
            new ListAttendeesQuery(admin, null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(0, attendees.Calls);
    }

    [Fact]
    public async Task AdminDashboardIsDeniedBeforeAnyQueryInvocation()
    {
        var admin = Guid.NewGuid();
        var profiles = Profiles(StaffAccessProfile.Create(admin, [Role.Admin], null));
        var queries = new CountingDashboardQueries();
        var handler = new GetDashboardsHandler(queries, new StaffAccessAuthorizer(profiles));

        var result = await handler.HandleAsync(
            new GetDashboardsQuery(admin), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(0, queries.Calls);
    }

    [Fact]
    public async Task CoordinatorAttendeeListStillRuns()
    {
        var coordinator = Guid.NewGuid();
        var profiles = Profiles(StaffAccessProfile.Create(coordinator, [Role.Coordinator], null));
        var attendees = new CountingAttendeeRepository();
        var handler = new ListAttendeesHandler(attendees, new InMemoryAttendeeGroupRepository(), new StaffAccessAuthorizer(profiles));

        var result = await handler.HandleAsync(
            new ListAttendeesQuery(coordinator, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, attendees.Calls);
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

    private sealed class CountingAttendeeRepository : IAttendeeRepository
    {
        public int Calls { get; private set; }

        public Task<Attendee?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<Attendee?>(null);
        }

        public Task<Attendee?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<Attendee?>(null);
        }

        public Task<Attendee?> GetByEmailAsync(string email, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<Attendee?>(null);
        }

        public Task<IReadOnlyList<Attendee>> LockByGroupForUpdateAsync(Guid groupId, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<Attendee>>([]);
        }

        public Task<IReadOnlyList<Attendee>> ListAsync(
            AttendeeStatus? status,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<Attendee>>([]);
        }

        public void Add(Attendee attendee) => Calls++;
        public void Remove(Attendee attendee) => Calls++;
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
