using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public class BookingClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string> Bodies { get; } = [];

        public Queue<HttpResponseMessage> Responses { get; } = [];

        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.NoContent);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
            return Responses.Count > 0 ? Responses.Dequeue() : Response;
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

    private static (BookingClient Client, StubHandler Handler) Given()
    {
        var handler = new StubHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        return (new BookingClient(http), handler);
    }

    [Fact]
    public async Task TheInviteIsFetchedByTokenAndTheTokenIsEscaped()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new InviteDto(Guid.NewGuid(), "Amara Novak", ["Uniform Fitting"], [])),
        };

        await client.GetInviteAsync("abc.def/ghi", CancellationToken.None);

        Assert.Equal("/api/booking/abc.def%2Fghi", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task AnExpiredLinkComesBackAsTheGenericMessage()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(
                """{"title":"not_found","detail":"This booking link is no longer valid.","status":404}""",
                Encoding.UTF8,
                "application/problem+json"),
        };

        var outcome = await client.GetInviteAsync("anything", CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal(404, outcome.StatusCode);
        Assert.Equal("This booking link is no longer valid.", outcome.ErrorMessage);
    }

    [Fact]
    public async Task ConfirmingPostsTheChosenEvent()
    {
        var (client, handler) = Given();
        var eventId = Guid.NewGuid();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new ConfirmedBookingDto(
                Guid.NewGuid(), new DateOnly(2026, 9, 11), new TimeOnly(13, 0), new TimeOnly(17, 0), "manage-token")),
        };

        var outcome = await client.ConfirmAsync("tok", eventId, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("/api/booking/tok/confirm", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Contains(eventId.ToString(), handler.Bodies[0]);
        Assert.Equal("manage-token", outcome.Value!.ManageToken);
    }

    [Fact]
    public async Task AnOptionThatFilledUpComesBackAsAConflictWithItsMessage()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(
                """{"title":"conflict","detail":"That time filled up while you were choosing. Please pick from the updated options.","status":409}""",
                Encoding.UTF8,
                "application/problem+json"),
        };

        var outcome = await client.ConfirmAsync("tok", Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(409, outcome.StatusCode);
        Assert.Contains("filled up", outcome.ErrorMessage);
    }

    [Fact]
    public async Task TheBookingIsFetchedFromTheManageRoute()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new BookingDto(
                new DateOnly(2026, 9, 11), new TimeOnly(13, 0), new TimeOnly(17, 0),
                "Friday 11 Sep 2026, 13:00-17:00", "Amara Novak")),
        };

        var outcome = await client.GetBookingAsync("mtok", CancellationToken.None);

        Assert.Equal("/api/booking/manage/mtok", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("Amara Novak", outcome.Value!.AttendeeName);
    }

    [Fact]
    public async Task CancellingSendsTheRebookFlag()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new CancelOutcomeDto(true)),
        };

        var outcome = await client.CancelAsync("mtok", true, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("/api/booking/manage/mtok/cancel", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Contains("true", handler.Bodies[0]);
        Assert.True(outcome.Value!.Reinvited);
    }

    [Fact]
    public async Task CancellingWithoutAvailableEventsReportsThatNoInviteWasSent()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new CancelOutcomeDto(false)),
        };

        var outcome = await client.CancelAsync("mtok", true, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.False(outcome.Value!.Reinvited);
    }

    [Fact]
    public async Task EveryResponseIsDisposedAfterEachAttendeeOperation()
    {
        var (client, handler) = Given();
        var inviteResponse = new TrackingResponseMessage(
            HttpStatusCode.OK,
            new TrackingContent("""{"inviteId":"00000000-0000-0000-0000-000000000001","attendeeName":"Amara Novak","appointmentTypeNames":["Uniform Fitting"],"options":[]}"""));
        var confirmedResponse = new TrackingResponseMessage(
            HttpStatusCode.OK,
            new TrackingContent("""{"bookingId":"00000000-0000-0000-0000-000000000002","date":"2026-09-11","startTime":"13:00:00","endTime":"17:00:00","manageToken":"manage-token"}"""));
        var bookingResponse = new TrackingResponseMessage(
            HttpStatusCode.OK,
            new TrackingContent("""{"date":"2026-09-11","startTime":"13:00:00","endTime":"17:00:00","display":"Friday 11 Sep 2026","attendeeName":"Amara Novak"}"""));
        var cancelResponse = new TrackingResponseMessage(
            HttpStatusCode.OK,
            new TrackingContent("""{"reinvited":false}"""));
        var responses = new[] { inviteResponse, confirmedResponse, bookingResponse, cancelResponse };

        foreach (var response in responses)
        {
            handler.Responses.Enqueue(response);
        }

        await client.GetInviteAsync("invite", CancellationToken.None);
        await client.ConfirmAsync("invite", Guid.NewGuid(), CancellationToken.None);
        await client.GetBookingAsync("manage", CancellationToken.None);
        await client.CancelAsync("manage", false, CancellationToken.None);

        Assert.All(responses, response => Assert.True(response.WasDisposed));
        Assert.True(inviteResponse.WasDisposedAfterContentWasRead);
        Assert.True(confirmedResponse.WasDisposedAfterContentWasRead);
        Assert.True(bookingResponse.WasDisposedAfterContentWasRead);
        Assert.True(cancelResponse.WasDisposedAfterContentWasRead);
    }
}
