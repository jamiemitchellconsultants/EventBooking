using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Application.Dashboards;
using EventBooking.Application.ReadModels;
using EventBooking.Domain.Access;
using EventBooking.Domain.Time;

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

        public Task<DashboardsView> GetDashboardsAsync(
            CallerShape shape, Guid? locationId, DateTimeOffset now,
            IEventWindowZones zones, CancellationToken ct) =>
            throw new NotSupportedException("The operations handler reads through EventsOverviewAsync.");
    }

    private static CountingQueries QueriesWithOneEvent() => new(
    [
        new EventOverviewRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "London",
            new DateOnly(2026, 9, 10),
            new TimeOnly(9, 0),
            new TimeOnly(13, 0),
            [new EventCapacityRow("DAT", 10, 9)],
            1,
            "Europe/London",
            240),
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
        // LocationName is the event's site, not attendee PII: it is carved out before
        // the guard runs so a future attendee Name or Email still fails loudly.
        var names = string.Join(
            ",",
            typeof(EventOperationsView).GetProperties().Select(p => p.Name)
                .Concat(typeof(EventOverviewRow).GetProperties().Select(p => p.Name))
                .Where(name => name is not "LocationName"));

        Assert.DoesNotContain("AttendeeId", names);
        Assert.DoesNotContain("Name", names);
        Assert.DoesNotContain("Email", names);
    }
}
