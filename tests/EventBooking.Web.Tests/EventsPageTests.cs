using System.Net;
using System.Net.Http.Json;
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

public class EventsPageTests : BunitContext
{

    [Fact]
    public void RendersEventSectionWithCancelControls()
    {
        var eventId = Guid.NewGuid();
        var handler = new RoutingHandler();
        handler.Enqueue("/api/events/operations", OperationsJson([Event(eventId, activeBookings: 1)]));
        GivenClients(handler);

        var cut = Render<EventOperations>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#event-operations tbody tr")));
        Assert.Contains("2026-09-10", cut.Markup);
        Assert.Contains("Cancel event", cut.Markup);
        Assert.Contains("DAT 9/10", cut.Markup);
    }

    [Fact]
    public async Task CancelWithNoBookingsSucceedsOnTheFirstClick()
    {
        var eventId = Guid.NewGuid();
        var handler = new RoutingHandler();
        handler.Enqueue("/api/events/operations", OperationsJson([Event(eventId, activeBookings: 0)]));
        handler.Enqueue($"/api/events/{eventId}", new HttpResponseMessage(HttpStatusCode.NoContent));
        handler.Enqueue("/api/events/operations", OperationsJson([]));
        GivenClients(handler);

        var cut = Render<EventOperations>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#event-operations tbody tr")));

        await cut.InvokeAsync(() => cut.Find("button.button-danger").Click());

        cut.WaitForAssertion(() => Assert.Contains("No events yet", cut.Markup));
        var delete = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Delete);
        Assert.Contains("confirm=false", delete.RequestUri!.Query);
    }

    [Fact]
    public async Task CancelWithBookingsRequiresASecondConfirmClick()
    {
        var eventId = Guid.NewGuid();
        var handler = new RoutingHandler();
        handler.Enqueue("/api/events/operations", OperationsJson([Event(eventId, activeBookings: 6)]));
        handler.Enqueue($"/api/events/{eventId}", Conflict(
            "Cancelling this event will cancel 6 confirmed bookings."));
        handler.Enqueue($"/api/events/{eventId}", new HttpResponseMessage(HttpStatusCode.NoContent));
        handler.Enqueue("/api/events/operations", OperationsJson([]));
        GivenClients(handler);

        var cut = Render<EventOperations>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#event-operations tbody tr")));

        await cut.InvokeAsync(() => cut.Find("button.button-danger").Click());

        cut.WaitForAssertion(() => Assert.Contains("Confirm cancel", cut.Markup));
        Assert.Contains("6 confirmed bookings", cut.Find("[role=alert]").TextContent);
        Assert.Contains("Press Confirm cancel to proceed", cut.Find("[role=alert]").TextContent);
        Assert.Single(cut.FindAll("#event-operations tbody tr"));

        await cut.InvokeAsync(() => cut.Find("button.button-danger").Click());

        cut.WaitForAssertion(() => Assert.Contains("No events yet", cut.Markup));
        var deletes = handler.Requests.Where(r => r.Method == HttpMethod.Delete).ToList();
        Assert.Equal(2, deletes.Count);
        Assert.Contains("confirm=false", deletes[0].RequestUri!.Query);
        Assert.Contains("confirm=true", deletes[1].RequestUri!.Query);
    }

    [Fact]
    public async Task TheEventSectionNeverCallsDashboards()
    {
        var eventId = Guid.NewGuid();
        var handler = new RoutingHandler();
        handler.Enqueue("/api/events/operations", OperationsJson([Event(eventId, activeBookings: 0)]));
        handler.Enqueue($"/api/events/{eventId}", new HttpResponseMessage(HttpStatusCode.NoContent));
        handler.Enqueue("/api/events/operations", OperationsJson([]));
        GivenClients(handler);

        var cut = Render<EventOperations>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#event-operations tbody tr")));
        await cut.InvokeAsync(() => cut.Find("button.button-danger").Click());
        cut.WaitForAssertion(() => Assert.Contains("No events yet", cut.Markup));

        Assert.DoesNotContain(
            handler.Requests,
            r => r.RequestUri!.AbsolutePath.StartsWith("/api/dashboards", StringComparison.Ordinal));
    }

    [Fact]
    public void AForbiddenEventLoadShowsTheSafeMessage()
    {
        var handler = new RoutingHandler();
        handler.Enqueue("/api/events/operations", new HttpResponseMessage(HttpStatusCode.Forbidden));
        GivenClients(handler);

        var cut = Render<EventOperations>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("p.banner.error[role=alert]")));
        Assert.Empty(cut.FindAll("#event-operations"));
    }

    private void GivenClients(RoutingHandler handler)
    {
        Services.AddSingleton(new EventsClient(NewHttpClient(handler)));
    }

    private static HttpClient NewHttpClient(HttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("https://api.example.com") };

    private static EventOperationDto Event(Guid eventId, int activeBookings) => new(
        eventId,
        new DateOnly(2026, 9, 10),
        new TimeOnly(9, 0),
        new TimeOnly(13, 0),
        [new EventOperationCapacityDto("DAT", 10, 9)],
        activeBookings);

    private static HttpResponseMessage OperationsJson(IReadOnlyList<EventOperationDto> events) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(new EventOperationsDto(events)) };

    private static HttpResponseMessage Conflict(string detail) =>
        new(HttpStatusCode.Conflict)
        {
            Content = JsonContent.Create(new { type = "conflict", title = "conflict", detail, status = 409 }),
        };

    /// <summary>Answers per requested path, in the order each path's responses were enqueued.</summary>
    private sealed class RoutingHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, Queue<HttpResponseMessage>> _byPath = [];

        public List<HttpRequestMessage> Requests { get; } = [];

        public void Enqueue(string absolutePath, HttpResponseMessage response)
        {
            if (!_byPath.TryGetValue(absolutePath, out var queue))
            {
                queue = new Queue<HttpResponseMessage>();
                _byPath[absolutePath] = queue;
            }

            queue.Enqueue(response);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var path = request.RequestUri!.AbsolutePath;
            if (!_byPath.TryGetValue(path, out var queue) || queue.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No stubbed response queued for {request.Method} {path}.");
            }

            return Task.FromResult(queue.Dequeue());
        }
    }
}
