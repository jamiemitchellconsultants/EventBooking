using System.Net;
using System.Net.Http.Json;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public class MeClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(Response);
        }
    }

    private static (MeClient Client, StubHandler Handler) Given()
    {
        var handler = new StubHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        return (new MeClient(http), handler);
    }

    [Fact]
    public async Task TheCallerIsFetchedFromTheMeRoute()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new MeDto(["Manager", "Coordinator"], Guid.NewGuid(), "Drug & Alcohol Testing")),
        };

        var outcome = await client.GetAsync(CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal("/api/me", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal(["Manager", "Coordinator"], outcome.Value!.Roles);
    }

    [Fact]
    public async Task AnUnassignedCallerGetsAnEmptyRoleSet()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new MeDto([], null, null)),
        };

        var outcome = await client.GetAsync(CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Empty(outcome.Value!.Roles);
    }
}
