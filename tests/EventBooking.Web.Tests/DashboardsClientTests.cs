using System.Net;
using System.Net.Http.Headers;
using System.Text;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public class DashboardsClientTests
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

    private sealed class TrackingResponseMessage(HttpStatusCode statusCode, TrackingContent content)
        : HttpResponseMessage(statusCode)
    {
        public bool WasDisposed { get; private set; }

        public bool WasDisposedAfterContentWasRead { get; private set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                WasDisposed = true;
                WasDisposedAfterContentWasRead = content.WasRead;
            }

            base.Dispose(disposing);
        }
    }

    private static (DashboardsClient Client, StubHandler Handler) Given()
    {
        var handler = new StubHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        return (new DashboardsClient(http), handler);
    }

    [Fact]
    public async Task TheThreeDashboardViewsAreFetchedFromTheirSingleRoute()
    {
        var (client, handler) = Given();
        handler.Response = JsonResponse();

        var outcome = await client.GetAsync(CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Equal("/api/dashboards", handler.Request.RequestUri!.AbsolutePath);
        Assert.Empty(outcome.Value!.AwaitingAvailability.Rows);
        Assert.Empty(outcome.Value.NoResponse.Rows);
        Assert.Empty(outcome.Value.Events.Rows);
    }

    [Fact]
    public async Task TheDashboardResponseIsDisposedAfterItsBodyIsRead()
    {
        var (client, handler) = Given();
        var content = new TrackingContent("""{"awaitingAvailability":{"count":0,"rows":[]},"noResponse":{"count":0,"rows":[]},"events":{"count":0,"rows":[]},"failedEmails":0,"pendingEmails":0}""");
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        var response = new TrackingResponseMessage(HttpStatusCode.OK, content) { Content = content };
        handler.Response = response;

        await client.GetAsync(CancellationToken.None);

        Assert.True(response.WasDisposed);
        Assert.True(response.WasDisposedAfterContentWasRead);
    }

    private static HttpResponseMessage JsonResponse() => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            """{"awaitingAvailability":{"count":0,"rows":[]},"noResponse":{"count":0,"rows":[]},"events":{"count":0,"rows":[]},"failedEmails":0,"pendingEmails":0}""",
            Encoding.UTF8,
            "application/json"),
    };
}
