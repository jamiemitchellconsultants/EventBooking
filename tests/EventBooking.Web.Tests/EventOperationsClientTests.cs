using System.Net;
using System.Net.Http.Json;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public class EventOperationsClientTests
{
    [Fact]
    public async Task ImportPostsTheCsvToTheNeutralRoute()
    {
        var handler = new RecordingHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new EventImportOutcomeDto(true, 2, [])),
            },
        };
        var client = new EventOperationsClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.com"),
        });

        var result = await client.ImportAsync(
            "date,startTime,DAT,MED,UNI\n2026-09-10,09:00,10,6,8",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.ImportedCount);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("/api/events/import", handler.Request.RequestUri!.AbsolutePath);
        Assert.Equal("text/csv", handler.Request.Content!.Headers.ContentType!.MediaType);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public HttpResponseMessage Response { get; init; } = new(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(Response);
        }
    }
}
