using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Application.Events;
using EventBooking.Application.ReadModels;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Tests.Events;

/// <summary>The scope a caller resolves to, the page bounds, and the refusals.</summary>
public sealed class EventReadHandlerTests
{
    private static readonly Guid Manager = Guid.NewGuid();
    private static readonly Guid Coordinator = Guid.NewGuid();
    private static readonly Guid Nobody = Guid.NewGuid();
    private static readonly Guid Both = Guid.NewGuid();
    private static readonly Guid MedicalType = Guid.NewGuid();

    [Fact]
    public async Task AManagerResolvesToTheirOwnTypesScope()
    {
        var queries = new RecordingQueries();
        var handler = new ListEventsHandler(new FakeAuthorizer(), queries, new FixedClock());

        var result = await handler.HandleAsync(Query(Manager), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(queries.LastScope!.AllTypes);
        Assert.Equal(MedicalType, queries.LastScope.AppointmentTypeId);
    }

    [Fact]
    public async Task ACoordinatorResolvesToEveryType()
    {
        var queries = new RecordingQueries();
        var handler = new ListEventsHandler(new FakeAuthorizer(), queries, new FixedClock());

        var result = await handler.HandleAsync(Query(Coordinator), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(queries.LastScope!.AllTypes);
        Assert.Null(queries.LastScope.AppointmentTypeId);
    }

    /// <summary>
    /// Negotiation wins over operations when a caller holds both: the matrix grants a Manager
    /// both capabilities, so operations-first would resolve every Manager to all types and the
    /// scope machinery would be dead.
    /// </summary>
    [Fact]
    public async Task AHolderOfBothCapabilitiesResolvesToTheirOwnType()
    {
        var queries = new RecordingQueries();
        var handler = new ListEventsHandler(new FakeAuthorizer(), queries, new FixedClock());

        var result = await handler.HandleAsync(Query(Both), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(queries.LastScope!.AllTypes);
        Assert.Equal(MedicalType, queries.LastScope.AppointmentTypeId);
    }

    [Fact]
    public async Task ACallerWithNeitherCapabilityIsForbidden()
    {
        var handler = new ListEventsHandler(
            new FakeAuthorizer(), new RecordingQueries(), new FixedClock());

        var result = await handler.HandleAsync(Query(Nobody), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task ALimitOutsideTheRangeIsRefused(int limit)
    {
        var handler = new ListEventsHandler(
            new FakeAuthorizer(), new RecordingQueries(), new FixedClock());

        var result = await handler.HandleAsync(
            Query(Coordinator) with { Limit = limit }, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
    }

    /// <summary>
    /// The query is asked for one row more than the page, and the extra row is what proves
    /// there is a next page. Asking for exactly the page size cannot distinguish a full last
    /// page from a full page with more behind it.
    /// </summary>
    [Fact]
    public async Task AFullPageWithMoreBehindItCarriesTheLastKeptRowsCursor()
    {
        var queries = new RecordingQueries { Rows = Rows(4) };
        var handler = new ListEventsHandler(new FakeAuthorizer(), queries, new FixedClock());

        var result = await handler.HandleAsync(
            Query(Coordinator) with { Limit = 3 }, CancellationToken.None);

        Assert.Equal(4, queries.LastLimit);
        Assert.Equal(3, result.Value.Items.Count);
        Assert.Equal(result.Value.Items[2].Cursor, result.Value.NextCursor);
    }

    [Fact]
    public async Task TheLastPageCarriesNoNextCursor()
    {
        var queries = new RecordingQueries { Rows = Rows(2) };
        var handler = new ListEventsHandler(new FakeAuthorizer(), queries, new FixedClock());

        var result = await handler.HandleAsync(
            Query(Coordinator) with { Limit = 3 }, CancellationToken.None);

        Assert.Equal(2, result.Value.Items.Count);
        Assert.Null(result.Value.NextCursor);
    }

    [Fact]
    public async Task TheCancellableListAsksForNotStartedEventsOnly()
    {
        var queries = new RecordingQueries();
        var handler = new ListCancellableEventsHandler(
            new FakeAuthorizer(), queries, new FixedClock());

        await handler.HandleAsync(
            new ListCancellableEventsQuery(Coordinator, null, null, null, null, 50),
            CancellationToken.None);

        Assert.True(queries.LastNotStartedOnly);
    }

    [Fact]
    public async Task AnEventOutsideTheCallersScopeReadsAsNotFound()
    {
        var handler = new GetEventHandler(
            new FakeAuthorizer(), new RecordingQueries { Rows = [] });

        var result = await handler.HandleAsync(
            new GetEventQuery(Manager, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    private static ListEventsQuery Query(Guid staffUserId) =>
        new(staffUserId, null, null, null, null, null, 50);

    private static List<EventView> Rows(int count) =>
        [.. Enumerable.Range(0, count).Select(index => new EventView(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "LON", "London", "Europe/London",
            new DateOnly(2026, 10, 14), new TimeOnly(9, 30), 90, "Active", [], 0,
            KeysetCursor.Encode($"2026-10-14T09:3{index}:00Z", Guid.NewGuid())))];

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 9, 21, 9, 0, 0, TimeSpan.Zero);

        public DateTimeOffset NowAtTransitionalLocation => UtcNow;

        public DateOnly TodayAtTransitionalLocation => DateOnly.FromDateTime(UtcNow.UtcDateTime);

        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) =>
            DateOnly.FromDateTime(instant.UtcDateTime);

        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => instant;
    }

    /// <summary>One Manager scoped to medical, one Coordinator, and one caller with nothing.</summary>
    private sealed class FakeAuthorizer : IStaffAccessAuthorizer
    {
        public Task<Result<StaffAccessContext>> AuthorizeAsync(
            Guid staffUserId, StaffCapability capability, Guid? requiredAppointmentTypeId,
            CancellationToken cancellationToken)
        {
            var granted = (staffUserId, capability) switch
            {
                _ when staffUserId == Coordinator &&
                    capability == StaffCapability.ViewEventOperations =>
                    new StaffAccessContext(staffUserId, Set(Role.Coordinator), null),
                _ when staffUserId == Manager &&
                    capability == StaffCapability.ManageEventNegotiation =>
                    new StaffAccessContext(staffUserId, Set(Role.Manager), MedicalType),
                _ when staffUserId == Both &&
                    capability == StaffCapability.ViewEventOperations =>
                    new StaffAccessContext(staffUserId, Set(Role.Manager), MedicalType),
                _ when staffUserId == Both &&
                    capability == StaffCapability.ManageEventNegotiation =>
                    new StaffAccessContext(staffUserId, Set(Role.Manager), MedicalType),
                _ => null,
            };

            return Task.FromResult(granted is null
                ? Result<StaffAccessContext>.Failure(Error.Forbidden("No."))
                : Result<StaffAccessContext>.Success(granted));
        }

        private static IReadOnlySet<Role> Set(Role role) => new HashSet<Role> { role };
    }

    private sealed class RecordingQueries : IEventReadQueries
    {
        public List<EventView> Rows { get; set; } = [];

        public EventScope? LastScope { get; private set; }

        public int LastLimit { get; private set; }

        public bool LastNotStartedOnly { get; private set; }

        public Task<IReadOnlyList<EventView>> ListAsync(
            ListEventsQuery query, EventScope scope, DateTimeOffset now, bool notStartedOnly,
            CancellationToken ct)
        {
            LastScope = scope;
            LastLimit = query.Limit;
            LastNotStartedOnly = notStartedOnly;
            return Task.FromResult<IReadOnlyList<EventView>>(Rows.Take(query.Limit).ToList());
        }

        public Task<EventView?> GetAsync(Guid eventId, EventScope scope, CancellationToken ct)
        {
            LastScope = scope;
            return Task.FromResult(Rows.FirstOrDefault());
        }
    }
}
