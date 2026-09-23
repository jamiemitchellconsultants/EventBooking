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
