using System.Net;
using System.Text;
using System.Text.Json;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests.Pages.Admin;

public sealed class AdminClientTests
{
    [Fact]
    public async Task CreateLocationUsesOneIdempotencyKeyAndTask22bBody()
    {
        var handler = new SpyHandler(new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = Json("""{"id":"10000000-0000-0000-0000-000000000001","code":"LONDON_HQ","name":"London HQ","address":"1 Example St","timeZoneId":"Europe/London","isActive":true,"version":1,"_links":{}}"""),
        });
        var client = new AdminClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.example") });
        var submission = IdempotencySubmission.Start();

        var result = await client.CreateLocationAsync(
            "LONDON_HQ", "London HQ", "1 Example St", "Europe/London", submission,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("/api/locations", handler.Request.RequestUri!.AbsolutePath);
        Assert.Equal(submission.Key, handler.Request.Headers.GetValues("Idempotency-Key").Single());
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal("Europe/London", body.RootElement.GetProperty("timeZoneId").GetString());
    }

    [Fact]
    public async Task EveryReferenceListReadsThePageEnvelope()
    {
        var handler = new SpyHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = Json("""{"items":[],"nextCursor":null}"""),
        });
        var client = new AdminClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.example") });

        var result = await client.ListLocationsAsync(true, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Items);
        Assert.Contains("includeInactive=true", handler.Request!.RequestUri!.Query);
    }

    private static StringContent Json(string value) => new(value, Encoding.UTF8, "application/json");

    private sealed class SpyHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return response;
        }
    }
}
