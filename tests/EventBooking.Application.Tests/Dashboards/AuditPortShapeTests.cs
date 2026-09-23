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
