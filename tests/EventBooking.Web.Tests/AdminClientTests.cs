using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public class AdminClientTests
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

    private static (AdminClient Client, StubHandler Handler) Given()
    {
        var handler = new StubHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        return (new AdminClient(http), handler);
    }

    [Fact]
    public async Task TheSettingsAreReadFromTheAdminRoute()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new SettingsDto(4, 2, [])),
        };

        var outcome = await client.GetAsync(CancellationToken.None);

        Assert.Equal(4, outcome.Value!.InviteExpiryDays);
        Assert.Equal("/api/admin/settings", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task UpdatingPutsBothValues()
    {
        var (client, handler) = Given();

        await client.UpdateAsync(7, 0, CancellationToken.None);

        Assert.Equal(HttpMethod.Put, handler.Requests[0].Method);
        Assert.Contains("\"inviteExpiryDays\":7", handler.Bodies[0]);
        Assert.Contains("\"maxAutoRetryCount\":0", handler.Bodies[0]);
    }

    [Fact]
    public async Task AValidationFailureIsSurfaced()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                """{"type":"validation-failed","title":"validation","detail":"inviteExpiryDays must be greater than zero.","status":400}""",
                Encoding.UTF8,
                "application/problem+json"),
        };

        var outcome = await client.UpdateAsync(0, 2, CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal("inviteExpiryDays must be greater than zero.", outcome.ErrorMessage);
    }

    [Fact]
    public async Task EveryResponseIsDisposedOnlyAfterTheClientHasConsumedItsContent()
    {
        var (client, handler) = Given();
        var settingsContent = new TrackingContent(JsonSerializer.Serialize(new SettingsDto(4, 2, [])));
        var getResponse = new TrackingResponseMessage(HttpStatusCode.OK, settingsContent);
        var updateResponse = new TrackingResponseMessage(HttpStatusCode.NoContent);
        var responses = new[] { getResponse, updateResponse };

        foreach (var response in responses)
        {
            handler.Responses.Enqueue(response);
        }

        await client.GetAsync(CancellationToken.None);
        await client.UpdateAsync(4, 2, CancellationToken.None);

        Assert.All(responses, response => Assert.True(response.WasDisposed));
        Assert.True(settingsContent.WasRead);
        Assert.True(getResponse.WasDisposedAfterContentWasRead);
    }
}
