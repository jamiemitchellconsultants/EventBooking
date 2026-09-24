using EventBooking.Application.Abstractions;
using EventBooking.Application.Dashboards;
using EventBooking.Application.ReadModels;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.Time;

namespace EventBooking.Application.Tests.Dashboards;

public sealed class DashboardHandlerTests
{
    [Fact]
    public async Task Events_tab_is_bounded_and_location_filtered()
    {
        var queries = new MemoryDashboardQueries()
            .WithEventEnding(daysFromNow: -8)
            .WithEventEnding(daysFromNow: 61)
            .WithEventEnding(daysFromNow: 30, location: "TOKYO");
        var (handler, staff) = Handler(queries);

        var result = await handler.HandleAsync(
            new GetDashboardsQuery(staff, queries.LondonLocationId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        // Both out-of-bound events and the Tokyo one are gone, so the tab is empty and
        // its count agrees with its rows — the count is not computed separately.
        Assert.Empty(result.Value.Events.Rows);
        Assert.Equal(0, result.Value.Events.Count);
    }

    [Fact]
    public async Task An_event_inside_the_bounds_at_the_named_location_is_kept()
    {
        var queries = new MemoryDashboardQueries()
            .WithEventEnding(daysFromNow: 30);
        var (handler, staff) = Handler(queries);

        var result = await handler.HandleAsync(
            new GetDashboardsQuery(staff, queries.LondonLocationId),
            CancellationToken.None);

        var row = Assert.Single(result.Value.Events.Rows);
        Assert.Equal(queries.LondonLocationId, row.LocationId);
        Assert.Equal(1, result.Value.Events.Count);
    }

    [Fact]
    public async Task The_attendee_tabs_are_one_status_each_and_carry_their_counts()
    {
        var queries = new MemoryDashboardQueries()
            .WithAwaitingAvailability("Amy", waitingSince: -12)
            .WithAwaitingAvailability("Ben", waitingSince: -3)
            .WithNoResponse("Cat");
        var (handler, staff) = Handler(queries);

        var result = await handler.HandleAsync(
            new GetDashboardsQuery(staff, null), CancellationToken.None);

        Assert.Equal(2, result.Value.AwaitingAvailability.Count);
        Assert.Equal(2, result.Value.AwaitingAvailability.Rows.Count);
        // Longest wait first, and the days-waiting figure is derived from the stamp.
        Assert.Equal("Amy", result.Value.AwaitingAvailability.Rows[0].Name);
        Assert.Equal(12, result.Value.AwaitingAvailability.Rows[0].DaysWaiting);
        Assert.Equal(1, result.Value.NoResponse.Count);
        Assert.Equal("Cat", Assert.Single(result.Value.NoResponse.Rows).Name);
    }

    [Fact]
    public async Task A_location_filter_does_not_narrow_the_attendee_tabs()
    {
        // An attendee awaiting availability has no event and therefore no location.
        // Filtering them by one would need a relationship the model does not have.
        var queries = new MemoryDashboardQueries()
            .WithAwaitingAvailability("Amy", waitingSince: -1);
        var (handler, staff) = Handler(queries);

        var result = await handler.HandleAsync(
            new GetDashboardsQuery(staff, queries.LondonLocationId),
            CancellationToken.None);

        Assert.Equal(1, result.Value.AwaitingAvailability.Count);
    }

    [Fact]
    public async Task Admin_without_a_coordinator_profile_is_forbidden()
    {
        // Admin is exclusive with every other role, so no profile can pass the
        // handler's Coordinator gate while Admin-shaped: the handler refuses here, and
        // the Infrastructure suite proves the read model refuses an Admin shape
        // presented to it directly.
        var queries = new OpenDashboardQueries();
        var profiles = new InMemoryStaffAccessProfileRepository();
        var staff = Guid.NewGuid();
        profiles.Add(StaffAccessProfile.Create(staff, Role.Admin, null));
        var handler = new GetDashboardsHandler(
            queries, profiles, new FakeClock(), DashboardTestZones.Instance);

        var result = await handler.HandleAsync(
            new GetDashboardsQuery(staff, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Email_counts_travel_with_the_tabs()
    {
        var queries = new MemoryDashboardQueries().WithEmails(failed: 2, pending: 5);
        var (handler, staff) = Handler(queries);

        var result = await handler.HandleAsync(
            new GetDashboardsQuery(staff, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.FailedEmails);
        Assert.Equal(5, result.Value.PendingEmails);
    }

    private static (GetDashboardsHandler Handler, Guid Staff) Handler(IDashboardQueries queries)
    {
        var profiles = new InMemoryStaffAccessProfileRepository();
        var staff = Guid.NewGuid();
        profiles.Add(StaffAccessProfile.Create(staff, Role.Coordinator, null));
        return (new GetDashboardsHandler(queries, profiles, new FakeClock(), DashboardTestZones.Instance), staff);
    }

    private sealed class MemoryDashboardQueries : IDashboardQueries
    {
        private readonly List<(int EndOffsetDays, Guid LocationId)> _events = [];
        private readonly List<(string Name, int WaitingSinceDays)> _awaiting = [];
        private readonly List<string> _noResponse = [];
        private int _failed;
        private int _pending;

        public Guid LondonLocationId { get; } = Guid.NewGuid();

        public Guid TokyoLocationId { get; } = Guid.NewGuid();

        public MemoryDashboardQueries WithEventEnding(int daysFromNow, string location = "LONDON")
        {
            _events.Add((daysFromNow, location == "LONDON" ? LondonLocationId : TokyoLocationId));
            return this;
        }

        public MemoryDashboardQueries WithAwaitingAvailability(string name, int waitingSince)
        {
            _awaiting.Add((name, waitingSince));
            return this;
        }

        public MemoryDashboardQueries WithNoResponse(string name)
        {
            _noResponse.Add(name);
            return this;
        }

        public MemoryDashboardQueries WithEmails(int failed, int pending)
        {
            _failed = failed;
            _pending = pending;
            return this;
        }

        public Task<DashboardsView> GetDashboardsAsync(
            CallerShape shape, Guid? locationId, DateTimeOffset now,
            IEventWindowZones zones, CancellationToken ct)
        {
            if (shape.IsAdmin)
            {
                return Task.FromResult(new DashboardsView(
                    new AwaitingAvailabilityTab(0, []), new NoResponseTab(0, []),
                    new EventsTab(0, []), 0, 0));
            }

            var from = now.AddDays(-7);
            var to = now.AddDays(60);
            var today = DateOnly.FromDateTime(now.UtcDateTime);
            var events = _events
                .Select(e => (End: now.AddDays(e.EndOffsetDays), e.LocationId))
                .Where(e => e.End >= from && e.End <= to)
                .Where(e => locationId is null || e.LocationId == locationId)
                .Select(e => new EventOverviewRow(
                    Guid.NewGuid(),
                    e.LocationId,
                    e.LocationId == LondonLocationId ? "London" : "Tokyo",
                    DateOnly.FromDateTime(e.End.UtcDateTime),
                    new TimeOnly(9, 0),
                    new TimeOnly(13, 0),
                    [],
                    0,
                    "Europe/London",
                    240))
                .OrderBy(e => e.Date)
                .ThenBy(e => e.StartTime)
                .ThenBy(e => e.EventId)
                .ToList();
            var awaiting = _awaiting
                .Select(a =>
                {
                    var since = today.AddDays(a.WaitingSinceDays);
                    return new AwaitingAvailabilityRow(
                        Guid.NewGuid(),
                        a.Name,
                        $"{a.Name.ToLowerInvariant()}@example.invalid",
                        ["MED"],
                        since,
                        today.DayNumber - since.DayNumber);
                })
                .OrderByDescending(r => r.DaysWaiting)
                .ToList();
            var noResponse = _noResponse
                .Select(name => new NoResponseRow(
                    Guid.NewGuid(),
                    name,
                    $"{name.ToLowerInvariant()}@example.invalid",
                    ["MED"],
                    today))
                .ToList();

            return Task.FromResult(new DashboardsView(
                new AwaitingAvailabilityTab(awaiting.Count, awaiting),
                new NoResponseTab(noResponse.Count, noResponse),
                new EventsTab(events.Count, events),
                _failed,
                _pending));
        }

        public Task<IReadOnlyList<AwaitingAvailabilityRow>> AwaitingAvailabilityAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("The dashboard handler reads through GetDashboardsAsync.");

        public Task<IReadOnlyList<NoResponseRow>> NoResponseAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("The dashboard handler reads through GetDashboardsAsync.");

        public Task<IReadOnlyList<EventOverviewRow>> EventsOverviewAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("The dashboard handler reads through GetDashboardsAsync.");

        public Task<IReadOnlyList<AttendeeEmailStatusRow>> LatestEmailStatusAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("The dashboard handler reads through GetDashboardsAsync.");
    }

    private sealed class OpenDashboardQueries : IDashboardQueries
    {
        public Task<DashboardsView> GetDashboardsAsync(
            CallerShape shape, Guid? locationId, DateTimeOffset now,
            IEventWindowZones zones, CancellationToken ct) =>
            Task.FromResult(new DashboardsView(
                new AwaitingAvailabilityTab(0, []), new NoResponseTab(0, []),
                new EventsTab(0, []), 0, 0));

        public Task<IReadOnlyList<AwaitingAvailabilityRow>> AwaitingAvailabilityAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("The dashboard handler reads through GetDashboardsAsync.");

        public Task<IReadOnlyList<NoResponseRow>> NoResponseAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("The dashboard handler reads through GetDashboardsAsync.");

        public Task<IReadOnlyList<EventOverviewRow>> EventsOverviewAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("The dashboard handler reads through GetDashboardsAsync.");

        public Task<IReadOnlyList<AttendeeEmailStatusRow>> LatestEmailStatusAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("The dashboard handler reads through GetDashboardsAsync.");
    }

    private sealed class DashboardTestZones : IEventWindowZones
    {
        public static DashboardTestZones Instance { get; } = new();

        public bool IsKnownZone(string timeZoneId) => true;

        public LocalTimeValidity ValidityOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            LocalTimeValidity.Unique;

        public DateTimeOffset InstantOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            new(date.ToDateTime(time), TimeSpan.Zero);

        public DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId) =>
            DateOnly.FromDateTime(instant.UtcDateTime);

        public string AbbreviationOf(DateTimeOffset instant, string timeZoneId) => "UTC";
    }
}
