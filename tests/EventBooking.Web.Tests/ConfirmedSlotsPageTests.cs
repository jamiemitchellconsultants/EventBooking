using System.Net;
using System.Net.Http.Json;
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

public class ConfirmedSlotsPageTests : BunitContext
{
    [Fact]
    public async Task AcceptedImportShowsCountAndUsesNoCandidateClient()
    {
        var handler = new RoutingHandler();
        handler.Enqueue("/api/slots/operations", OperationsJson([]));
        handler.Enqueue("/api/confirmed-slots/import", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new SlotImportOutcomeDto(true, 4, [])),
        });
        GivenClients(handler);
        var cut = Render<ConfirmedSlots>();

        await cut.InvokeAsync(() => cut.Instance.ImportCsvForTestingAsync(
            "date,startTime,DAT,MED,UNI\n2026-09-10,09:00,10,6,8"));

        Assert.Contains("4 confirmed slots imported", cut.Markup);
        Assert.Contains(handler.Requests, r => r.RequestUri!.AbsolutePath == "/api/confirmed-slots/import");
        Assert.DoesNotContain(handler.Requests, r => r.RequestUri!.AbsolutePath.StartsWith("/api/candidates", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RejectedImportShowsEveryRowError()
    {
        var handler = new RoutingHandler();
        handler.Enqueue("/api/slots/operations", OperationsJson([]));
        handler.Enqueue("/api/confirmed-slots/import", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new SlotImportOutcomeDto(
                false,
                0,
                [new SlotImportErrorDto(3, "DAT must be a positive integer.")])),
        });
        GivenClients(handler);
        var cut = Render<ConfirmedSlots>();

        await cut.InvokeAsync(() => cut.Instance.ImportCsvForTestingAsync("bad"));

        Assert.Contains("Nothing was imported", cut.Markup);
        Assert.Contains("Line 3", cut.Markup);
        Assert.Contains("DAT must be a positive integer", cut.Markup);
    }

    [Fact]
    public void RendersSlotSectionWithCancelControls()
    {
        var slotId = Guid.NewGuid();
        var handler = new RoutingHandler();
        handler.Enqueue("/api/slots/operations", OperationsJson([Slot(slotId, activeBookings: 1)]));
        GivenClients(handler);

        var cut = Render<ConfirmedSlots>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#confirmed-slot-operations tbody tr")));
        Assert.Contains("2026-09-10", cut.Markup);
        Assert.Contains("Cancel slot", cut.Markup);
        Assert.Contains("DAT 9/10", cut.Markup);
    }

    [Fact]
    public async Task CancelWithNoBookingsSucceedsOnTheFirstClick()
    {
        var slotId = Guid.NewGuid();
        var handler = new RoutingHandler();
        handler.Enqueue("/api/slots/operations", OperationsJson([Slot(slotId, activeBookings: 0)]));
        handler.Enqueue($"/api/slots/confirmed/{slotId}", new HttpResponseMessage(HttpStatusCode.NoContent));
        handler.Enqueue("/api/slots/operations", OperationsJson([]));
        GivenClients(handler);

        var cut = Render<ConfirmedSlots>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#confirmed-slot-operations tbody tr")));

        await cut.InvokeAsync(() => cut.Find("button.button-danger").Click());

        cut.WaitForAssertion(() => Assert.Contains("No confirmed slots yet", cut.Markup));
        var delete = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Delete);
        Assert.Contains("confirm=false", delete.RequestUri!.Query);
    }

    [Fact]
    public async Task CancelWithBookingsRequiresASecondConfirmClick()
    {
        var slotId = Guid.NewGuid();
        var handler = new RoutingHandler();
        handler.Enqueue("/api/slots/operations", OperationsJson([Slot(slotId, activeBookings: 6)]));
        handler.Enqueue($"/api/slots/confirmed/{slotId}", Conflict(
            "Cancelling this slot will cancel 6 confirmed bookings."));
        handler.Enqueue($"/api/slots/confirmed/{slotId}", new HttpResponseMessage(HttpStatusCode.NoContent));
        handler.Enqueue("/api/slots/operations", OperationsJson([]));
        GivenClients(handler);

        var cut = Render<ConfirmedSlots>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#confirmed-slot-operations tbody tr")));

        await cut.InvokeAsync(() => cut.Find("button.button-danger").Click());

        cut.WaitForAssertion(() => Assert.Contains("Confirm cancel", cut.Markup));
        Assert.Contains("6 confirmed bookings", cut.Find("[role=alert]").TextContent);
        Assert.Contains("Press Confirm cancel to proceed", cut.Find("[role=alert]").TextContent);
        Assert.Single(cut.FindAll("#confirmed-slot-operations tbody tr"));

        await cut.InvokeAsync(() => cut.Find("button.button-danger").Click());

        cut.WaitForAssertion(() => Assert.Contains("No confirmed slots yet", cut.Markup));
        var deletes = handler.Requests.Where(r => r.Method == HttpMethod.Delete).ToList();
        Assert.Equal(2, deletes.Count);
        Assert.Contains("confirm=false", deletes[0].RequestUri!.Query);
        Assert.Contains("confirm=true", deletes[1].RequestUri!.Query);
    }

    [Fact]
    public async Task TheSlotSectionNeverCallsDashboards()
    {
        var slotId = Guid.NewGuid();
        var handler = new RoutingHandler();
        handler.Enqueue("/api/slots/operations", OperationsJson([Slot(slotId, activeBookings: 0)]));
        handler.Enqueue($"/api/slots/confirmed/{slotId}", new HttpResponseMessage(HttpStatusCode.NoContent));
        handler.Enqueue("/api/slots/operations", OperationsJson([]));
        GivenClients(handler);

        var cut = Render<ConfirmedSlots>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#confirmed-slot-operations tbody tr")));
        await cut.InvokeAsync(() => cut.Find("button.button-danger").Click());
        cut.WaitForAssertion(() => Assert.Contains("No confirmed slots yet", cut.Markup));

        Assert.DoesNotContain(
            handler.Requests,
            r => r.RequestUri!.AbsolutePath.StartsWith("/api/dashboards", StringComparison.Ordinal));
    }

    [Fact]
    public void AForbiddenSlotLoadShowsTheSafeMessage()
    {
        var handler = new RoutingHandler();
        handler.Enqueue("/api/slots/operations", new HttpResponseMessage(HttpStatusCode.Forbidden));
        GivenClients(handler);

        var cut = Render<ConfirmedSlots>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("p.banner.error[role=alert]")));
        Assert.Empty(cut.FindAll("#confirmed-slot-operations"));
    }

    private void GivenClients(RoutingHandler handler)
    {
        Services.AddSingleton(new ConfirmedSlotsClient(NewHttpClient(handler)));
        Services.AddSingleton(new SlotsClient(NewHttpClient(handler)));
    }

    private static HttpClient NewHttpClient(HttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("https://api.example.com") };

    private static SlotOperationDto Slot(Guid slotId, int activeBookings) => new(
        slotId,
        new DateOnly(2026, 9, 10),
        new TimeOnly(9, 0),
        new TimeOnly(13, 0),
        [new SlotOperationCapacityDto("DAT", 10, 9)],
        activeBookings);

    private static HttpResponseMessage OperationsJson(IReadOnlyList<SlotOperationDto> slots) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(new SlotOperationsDto(slots)) };

    private static HttpResponseMessage Conflict(string detail) =>
        new(HttpStatusCode.Conflict)
        {
            Content = JsonContent.Create(new { title = "conflict", detail, status = 409 }),
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
