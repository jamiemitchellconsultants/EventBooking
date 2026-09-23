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
