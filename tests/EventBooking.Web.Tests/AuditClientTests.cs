using System.Net;
using System.Text;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the audit search client builds its query string and reads the page back.</summary>
public class AuditClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(Response);
        }
    }

    private static (AuditClient Client, StubHandler Handler) Given()
    {
        var handler = new StubHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        return (new AuditClient(http), handler);
    }

    private static HttpResponseMessage JsonResponse(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    private static AuditFilters EmptyFilter() =>
        new(null, null, null, null, null, null, null);

    [Fact]
    public async Task SearchBuildsQueryStringAndReadsPage()
    {
        var (client, handler) = Given();
        handler.Response = JsonResponse("""{"items":[],"nextCursor":null}""");

        var outcome = await client.SearchAsync(
            EmptyFilter() with { Action = "EventConfirmed" }, null, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.NotNull(outcome.Value);
        Assert.Null(outcome.Value!.NextCursor);
        Assert.Equal("/api/audit", handler.Request!.RequestUri!.AbsolutePath);
        Assert.Contains("action=EventConfirmed", handler.Request.RequestUri.Query);
    }

    [Fact]
    public async Task SearchSendsCursorForNextPage()
    {
        var (client, handler) = Given();
        handler.Response = JsonResponse("""{"items":[],"nextCursor":null}""");

        await client.SearchAsync(EmptyFilter(), "abc123", CancellationToken.None);

        Assert.Contains("cursor=abc123", handler.Request!.RequestUri!.Query);
    }

    [Fact]
    public async Task SearchOmitsAbsentCriteria()
    {
        var (client, handler) = Given();
        handler.Response = JsonResponse("""{"items":[],"nextCursor":null}""");

        await client.SearchAsync(EmptyFilter(), null, CancellationToken.None);

        var query = handler.Request!.RequestUri!.Query;
        Assert.DoesNotContain("from=", query);
        Assert.DoesNotContain("to=", query);
        Assert.DoesNotContain("actorType=", query);
        Assert.DoesNotContain("action=", query);
        Assert.DoesNotContain("entityType=", query);
        Assert.DoesNotContain("entityId=", query);
        Assert.DoesNotContain("actorId=", query);
        Assert.DoesNotContain("cursor=", query);
    }

    [Fact]
    public async Task SearchSendsBothTimestampBoundsWhenGiven()
    {
        var (client, handler) = Given();
        handler.Response = JsonResponse("""{"items":[],"nextCursor":null}""");
        var from = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.Zero);
        var entityId = Guid.NewGuid();

        await client.SearchAsync(
            new AuditFilters(from, to, "Staff", null, "Event", entityId, "staff-9"),
            null, CancellationToken.None);

        var query = Uri.UnescapeDataString(handler.Request!.RequestUri!.Query);
        Assert.Contains(from.ToString("O"), query);
        Assert.Contains(to.ToString("O"), query);
        Assert.Contains("actorType=Staff", query);
        Assert.Contains($"entityId={entityId}", query);
        Assert.Contains("actorId=staff-9", query);
        Assert.Contains("entityType=Event", query);
    }

    [Fact]
    public async Task SearchReadsRowsFromThePage()
    {
        var (client, handler) = Given();
        handler.Response = JsonResponse(
            """
            {"items":[{"id":"22222222-2222-2222-2222-222222222222","timestamp":"2026-09-03T12:00:00+00:00","entityType":"Event",
            "entityId":"11111111-1111-1111-1111-111111111111","action":"EventConfirmed",
            "actorType":"Staff","actorId":"staff-1","actorDisplay":null,"details":"6 headcount"}],"nextCursor":"next"}
            """);

        var outcome = await client.SearchAsync(EmptyFilter(), null, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        var row = Assert.Single(outcome.Value!.Items);
        Assert.Equal("EventConfirmed", row.Action);
        Assert.Equal("6 headcount", row.Details);
        Assert.Equal("next", outcome.Value.NextCursor);
    }

    [Fact]
    public async Task HistoryForAnAttendeeHitsTheAttendeeRoute()
    {
        var (client, handler) = Given();
        var attendeeId = Guid.NewGuid();
        handler.Response = JsonResponse("""{"items":[],"nextCursor":null}""");

        var outcome = await client.ForAttendeeAsync(attendeeId, "cursor-1", CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal($"/api/audit/attendees/{attendeeId}",
            handler.Request!.RequestUri!.AbsolutePath);
        Assert.Contains("cursor=cursor-1", handler.Request.RequestUri.Query);
    }

    [Fact]
    public async Task HistoryForAnEventHitsTheEventRoute()
    {
        var (client, handler) = Given();
        var eventId = Guid.NewGuid();
        handler.Response = JsonResponse("""{"items":[],"nextCursor":null}""");

        var outcome = await client.ForEventAsync(eventId, null, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal($"/api/audit/events/{eventId}",
            handler.Request!.RequestUri!.AbsolutePath);
        Assert.Equal("", handler.Request.RequestUri.Query);
    }
}
