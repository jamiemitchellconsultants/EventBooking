using System.Net;

namespace EventBooking.Api.Tests;

[Collection("api")]
public sealed class CorsTests(ApiFactory factory)
{
    private const string WebOrigin = "https://localhost:5002";

    [Fact]
    public async Task AllowedWebOriginIsReturnedOnAnAnonymousResponse()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/booking/manage/nonsense");
        request.Headers.Add("Origin", WebOrigin);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(WebOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task JsonPostPreflightAllowsTheConfiguredWebOrigin()
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Options,
            "/api/booking/manage/nonsense/cancel");
        request.Headers.Add("Origin", WebOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(WebOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains(
            "POST",
            response.Headers.GetValues("Access-Control-Allow-Methods").Single(),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "content-type",
            response.Headers.GetValues("Access-Control-Allow-Headers").Single(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AllowedWebOriginResponseExposesContentDispositionForDownloads()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/booking/manage/nonsense");
        request.Headers.Add("Origin", WebOrigin);

        using var response = await client.SendAsync(request);

        Assert.Contains(
            "Content-Disposition",
            response.Headers.GetValues("Access-Control-Expose-Headers").Single(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnlistedOriginReceivesNoCorsPermission()
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "https://unlisted.example");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
