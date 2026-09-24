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
