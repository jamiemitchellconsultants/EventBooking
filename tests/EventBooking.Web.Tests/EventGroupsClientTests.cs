using System.Net;
using System.Text;
using System.Text.Json;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public sealed class EventGroupsClientTests
{
    [Fact]
    public async Task CreateSendsSelectedGroupsAndIdempotencyKey()
    {
        var handler = new SpyHandler(new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent("""{"id":"10000000-0000-0000-0000-000000000001","title":"Autumn intake","description":"Choose a date","isOpen":false,"version":1,"attendeeGroupIds":[],"appointmentTypeIds":[],"events":[]}""",
                Encoding.UTF8, "application/json"),
        });
        var client = new EventGroupsClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example"),
        });
        var groupId = Guid.NewGuid();
        var submission = IdempotencySubmission.Start();

        var result = await client.CreateAsync("Autumn intake", "Choose a date",
            [groupId], submission, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("/api/event-groups", handler.Request!.RequestUri!.AbsolutePath);
        Assert.Equal(submission.Key, handler.Request.Headers.GetValues("Idempotency-Key").Single());
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal(groupId.ToString(), body.RootElement.GetProperty("attendeeGroupIds")[0].GetString());
    }

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
