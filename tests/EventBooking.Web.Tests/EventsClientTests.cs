using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public class EventsClientTests
{
    /// <summary>Records what was asked for and answers with whatever the test set up.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public Queue<HttpResponseMessage> Responses { get; } = [];

        public Func<HttpRequestMessage, HttpResponseMessage> ResponseFactory { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.NoContent);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(
                Responses.Count > 0 ? Responses.Dequeue() : ResponseFactory(request));
        }
    }

    private sealed class TrackingContent(string body) : HttpContent
    {
        public bool WasRead { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            WasRead = true;
            return stream.WriteAsync(Encoding.UTF8.GetBytes(body)).AsTask();
        }

        protected override bool TryComputeLength(out long length)
        {
            length = Encoding.UTF8.GetByteCount(body);
            return true;
        }
    }

    private sealed class TrackingResponseMessage : HttpResponseMessage
    {
        private readonly TrackingContent? _trackingContent;

        public TrackingResponseMessage(HttpStatusCode statusCode, TrackingContent? trackingContent = null)
            : base(statusCode)
        {
            _trackingContent = trackingContent;
            if (trackingContent is not null)
            {
                trackingContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                Content = trackingContent;
            }
        }

        public bool WasDisposed { get; private set; }

        public bool WasDisposedAfterContentWasRead { get; private set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                WasDisposed = true;
                WasDisposedAfterContentWasRead = _trackingContent is null || _trackingContent.WasRead;
            }

            base.Dispose(disposing);
        }
    }

    private static (EventsClient Client, StubHandler Handler) Given()
    {
        var handler = new StubHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        return (new EventsClient(http), handler);
    }

    [Fact]
    public async Task TheBoardIsFetchedFromTheBoardRoute()
    {
        var (client, handler) = Given();
        handler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new EventBoardDto([], [])),
        };

        var outcome = await client.GetBoardAsync(CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal("/api/events/board", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ProposingPostsTheDateAndStartTime()
    {
        var (client, handler) = Given();
        handler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = JsonContent.Create(Guid.NewGuid()),
        };

        await client.ProposeAsync(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), CancellationToken.None);

        var request = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/event-proposals", request.RequestUri!.AbsolutePath);
        var body = await request.Content!.ReadAsStringAsync();
        Assert.Contains("2026-09-10", body);
        Assert.Contains("09:00", body);
    }

    [Fact]
    public async Task AcceptingPostsTheHeadcountToTheAcceptanceRoute()
    {
        var (client, handler) = Given();
        var proposalId = Guid.NewGuid();

        await client.AcceptAsync(proposalId, 10, CancellationToken.None);

        var request = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"/api/event-proposals/{proposalId}/acceptance", request.RequestUri!.AbsolutePath);
        Assert.Contains("10", await request.Content!.ReadAsStringAsync());
    }

    [Fact]
    public async Task WithdrawingAnAcceptanceDeletesTheAcceptanceRoute()
    {
        var (client, handler) = Given();
        var proposalId = Guid.NewGuid();

        await client.WithdrawAcceptanceAsync(proposalId, CancellationToken.None);

        Assert.Equal(HttpMethod.Delete, handler.Requests[0].Method);
        Assert.Equal(
            $"/api/event-proposals/{proposalId}/acceptance",
            handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task WithdrawingAProposalDeletesTheProposalRoute()
    {
        var (client, handler) = Given();
        var proposalId = Guid.NewGuid();

        await client.WithdrawProposalAsync(proposalId, CancellationToken.None);

        Assert.Equal($"/api/event-proposals/{proposalId}", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task CancellingAEventPassesTheConfirmFlagInTheQueryString()
    {
        var (client, handler) = Given();
        var eventId = Guid.NewGuid();

        await client.CancelEventAsync(eventId, true, CancellationToken.None);

        Assert.Equal($"/api/events/{eventId}", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("?confirm=true", handler.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task TheFirstUnconfirmedCancelSurfacesTheWarningText()
    {
        var (client, handler) = Given();
        handler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(
                """{"title":"conflict","detail":"Cancelling this event will cancel 6 confirmed bookings. Affected attendees will be notified and re-invited. Confirm to proceed.","status":409}""",
                Encoding.UTF8,
                "application/problem+json"),
        };

        var outcome = await client.CancelEventAsync(Guid.NewGuid(), false, CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Contains("6 confirmed bookings", outcome.ErrorMessage);
    }

    [Fact]
    public async Task EveryResponseIsDisposedOnlyAfterTheClientHasConsumedItsContent()
    {
        var (client, handler) = Given();
        var boardContent = new TrackingContent(JsonSerializer.Serialize(new EventBoardDto([], [])));
        var proposalContent = new TrackingContent(JsonSerializer.Serialize(Guid.NewGuid()));
        var boardResponse = new TrackingResponseMessage(HttpStatusCode.OK, boardContent);
        var proposalResponse = new TrackingResponseMessage(HttpStatusCode.Created, proposalContent);
        var acceptanceResponse = new TrackingResponseMessage(HttpStatusCode.NoContent);
        var withdrawnAcceptanceResponse = new TrackingResponseMessage(HttpStatusCode.NoContent);
        var withdrawnProposalResponse = new TrackingResponseMessage(HttpStatusCode.NoContent);
        var cancelledEventResponse = new TrackingResponseMessage(HttpStatusCode.NoContent);
        var responses = new[]
        {
            boardResponse,
            proposalResponse,
            acceptanceResponse,
            withdrawnAcceptanceResponse,
            withdrawnProposalResponse,
            cancelledEventResponse,
        };

        foreach (var response in responses)
        {
            handler.Responses.Enqueue(response);
        }

        var proposalId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        await client.GetBoardAsync(CancellationToken.None);
        await client.ProposeAsync(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), CancellationToken.None);
        await client.AcceptAsync(proposalId, 1, CancellationToken.None);
        await client.WithdrawAcceptanceAsync(proposalId, CancellationToken.None);
        await client.WithdrawProposalAsync(proposalId, CancellationToken.None);
        await client.CancelEventAsync(eventId, false, CancellationToken.None);

        Assert.All(responses, response => Assert.True(response.WasDisposed));
        Assert.True(boardContent.WasRead);
        Assert.True(proposalContent.WasRead);
        Assert.True(boardResponse.WasDisposedAfterContentWasRead);
        Assert.True(proposalResponse.WasDisposedAfterContentWasRead);
    }

    [Fact]
    public async Task GetEventOperationsCallsTheOperationsRouteAndMapsRows()
    {
        var handler = new StubHandler();
        var eventId = Guid.NewGuid();
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new EventOperationsDto(
            [
                new EventOperationDto(
                    eventId,
                    new DateOnly(2026, 9, 10),
                    new TimeOnly(9, 0),
                    new TimeOnly(13, 0),
                    [new EventOperationCapacityDto("DAT", 10, 9)],
                    1),
            ])),
        });
        var client = new EventsClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") });

        var outcome = await client.GetEventOperationsAsync(CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        var eventItem = Assert.Single(outcome.Value!.Events);
        Assert.Equal("/api/events/operations", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal(eventId, eventItem.EventId);
        Assert.Equal(1, eventItem.ActiveBookings);
        Assert.Equal("DAT", Assert.Single(eventItem.Capacities).Code);
    }

    [Fact]
    public async Task GetEventOperationsSurfacesAForbiddenResponseAsAFailure()
    {
        var handler = new StubHandler();
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.Forbidden));
        var client = new EventsClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") });

        var outcome = await client.GetEventOperationsAsync(CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Forbidden, outcome.StatusCode);
    }
}
