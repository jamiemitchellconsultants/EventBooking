using System.Net;
using System.Text;
using System.Text.Json;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public sealed class PublicEventGroupsClientTests
{
    [Fact]
    public async Task RequestUsesPublicRouteAndOnlySubmittedFields()
    {
        var handler = new SpyHandler();
        var client = new PublicEventGroupsClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example"),
        });
        var groupId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var attendeeGroupId = Guid.NewGuid();

        var result = await client.RequestAsync(groupId, eventId, "Amara Novak",
            "amara@example.test", attendeeGroupId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal($"/api/public/event-groups/{groupId}/events/{eventId}/registrations",
            handler.Request!.RequestUri!.AbsolutePath);
        Assert.False(handler.Request.Headers.Contains("Authorization"));
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal("Amara Novak", body.RootElement.GetProperty("name").GetString());
        Assert.Equal("amara@example.test", body.RootElement.GetProperty("email").GetString());
        Assert.Equal(attendeeGroupId.ToString(),
            body.RootElement.GetProperty("attendeeGroupId").GetString());
    }

    private sealed class SpyHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent("true", Encoding.UTF8, "application/json"),
            };
        }
    }
}
