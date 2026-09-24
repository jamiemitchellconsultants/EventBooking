using System.Net;
using System.Text;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public sealed class MeClientTests
{
    [Fact]
    public async Task ReadsCallerSpecificCollectionAffordancesFromMe()
    {
        var handler = new StubHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"displayName":"Alex","staffId":"A1","roles":["Admin"],"scopeAppointmentTypeId":null,"scopeAppointmentTypeCode":null,"scopeAppointmentTypeName":null,"capabilities":["ManageReferenceData"],"problem":null,"_links":{"createLocation":{"href":"/api/locations","method":"POST","operationId":"createLocation"}}}""",
                Encoding.UTF8, "application/json"),
        });
        var client = new MeClient(new HttpClient(handler)
            { BaseAddress = new Uri("https://api.example") });

        var result = await client.GetAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        var link = Assert.Single(result.Value!.Links).Value;
        Assert.Equal("createLocation", link.OperationId);
        Assert.Equal("/api/me", handler.Path);
    }

    [Fact]
    public async Task ASuccessfulIdentityIsFetchedOncePerClient()
    {
        var handler = new SequenceHandler(Ok, Ok);
        var client = new MeClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.example") });

        await client.GetAsync(CancellationToken.None);
        var second = await client.GetAsync(CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task AFailedIdentityIsNotRemembered()
    {
        var handler = new SequenceHandler(
            () => new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""{"type":"unexpected"}""", Encoding.UTF8, "application/problem+json"),
            },
            Ok);
        var client = new MeClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.example") });

        var first = await client.GetAsync(CancellationToken.None);
        var second = await client.GetAsync(CancellationToken.None);

        Assert.False(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task ClientsThatNeedTheIdentityShareTheCachedFetch()
    {
        var handler = new SequenceHandler(Ok);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example") };
        var me = new MeClient(http);
        await me.GetAsync(CancellationToken.None);

        var context = await new AppointmentsClient(http, me).GetContextAsync(CancellationToken.None);

        Assert.True(context.IsSuccess);
        Assert.Equal(1, handler.Calls);
    }

    private static HttpResponseMessage Ok() => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            """{"displayName":"Alex","staffId":"A1","roles":["AppointmentStaff"],"scopeAppointmentTypeId":"90000000-0000-0000-0000-000000000009","scopeAppointmentTypeCode":"MED","scopeAppointmentTypeName":"Medical check","capabilities":[],"problem":null,"_links":{}}""",
            Encoding.UTF8, "application/json"),
    };

    private sealed class SequenceHandler(params Func<HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(responses[Calls++]());
    }

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public string? Path { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            Path = request.RequestUri!.AbsolutePath;
            return Task.FromResult(response);
        }
    }
}
