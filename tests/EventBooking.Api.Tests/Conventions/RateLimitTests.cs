using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests.Conventions;

/// <summary>
/// Pins the three limits and the forwarded-header trust boundary. Each case uses its own
/// client address so one case cannot exhaust another's window.
/// </summary>
[Collection("api")]
public sealed class RateLimitTests(ApiFactory factory)
{
    [Fact]
    public async Task TheThirtyFirstAttendeeRequestInAMinuteFromOneAddressIs429WithRetryAfter()
    {
        using var host = WithRemoteAddress();
        factory.SignedInAs = null;
        var client = host.CreateClient();

        HttpResponseMessage? last = null;
        for (var attempt = 0; attempt < 31; attempt++)
        {
            // A fresh token each time. Thirty-one requests on one token would be refused by
            // the ten-per-minute token limit instead, and this case would pass without the
            // address limiter ever being consulted.
            last = await SendAsync(client, $"/api/booking/{UnknownToken()}", "203.0.113.10");
        }

        Assert.NotNull(last);
        Assert.Equal(HttpStatusCode.TooManyRequests, last.StatusCode);
        Assert.NotNull(last.Headers.RetryAfter);
        Assert.True(last.Headers.RetryAfter!.Delta > TimeSpan.Zero);
    }

    /// <summary>
    /// Thirty-one requests from one socket address, each claiming a different forwarded
    /// address. The proxy network is not configured, so the forwarded header is ignored and
    /// all thirty-one share one partition — which is what the 429 proves. Asserting only
    /// that a request succeeded would pass whether or not the header was trusted.
    /// </summary>
    [Fact]
    public async Task AForwardedAddressFromOutsideTheProxyNetworkIsIgnored()
    {
        using var host = WithRemoteAddress();
        factory.SignedInAs = null;
        var client = host.CreateClient();

        HttpResponseMessage? last = null;
        for (var attempt = 0; attempt < 31; attempt++)
        {
            last = await SendAsync(
                client, $"/api/booking/{UnknownToken()}", "203.0.113.20", $"198.51.100.{attempt}");
        }

        Assert.NotNull(last);
        Assert.Equal(HttpStatusCode.TooManyRequests, last.StatusCode);
    }

    /// <summary>
    /// The same thirty-one requests with the socket address inside the configured proxy
    /// network: now each forwarded address gets its own partition and none is rejected. This
    /// is the case that proves the setting is live rather than the limiter being generous.
    /// </summary>
    [Fact]
    public async Task AForwardedAddressFromInsideTheProxyNetworkPartitionsIndependently()
    {
        using var host = WithRemoteAddress(proxyNetwork: "203.0.113.0/24");
        factory.SignedInAs = null;
        var client = host.CreateClient();

        HttpResponseMessage? last = null;
        for (var attempt = 0; attempt < 31; attempt++)
        {
            last = await SendAsync(
                client, $"/api/booking/{UnknownToken()}", "203.0.113.30", $"198.51.100.{attempt}");
        }

        Assert.NotNull(last);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, last.StatusCode);
    }

    /// <summary>
    /// Eleven requests carrying one token prefix from eleven different addresses. The
    /// per-address allowance is nowhere near exhausted, so only the per-token limit can
    /// reject the eleventh.
    /// </summary>
    [Fact]
    public async Task TheEleventhRequestForOneTokenInAMinuteIs429()
    {
        using var host = WithRemoteAddress();
        factory.SignedInAs = null;
        var client = host.CreateClient();
        var token = UnknownToken();

        HttpResponseMessage? last = null;
        for (var attempt = 0; attempt < 11; attempt++)
        {
            last = await SendAsync(client, $"/api/booking/{token}", $"198.51.100.{100 + attempt}");
        }

        Assert.NotNull(last);
        Assert.Equal(HttpStatusCode.TooManyRequests, last.StatusCode);
    }

    [Fact]
    public async Task ARejectedRequestCarriesTheProblemBody()
    {
        using var host = WithRemoteAddress();
        factory.SignedInAs = null;
        var client = host.CreateClient();
        var token = UnknownToken();

        HttpResponseMessage? last = null;
        for (var attempt = 0; attempt < 31; attempt++)
        {
            last = await SendAsync(client, $"/api/booking/{token}", "203.0.113.40");
        }

        Assert.NotNull(last);
        Assert.Equal(
            "application/problem+json", last.Content.Headers.ContentType?.MediaType);
        Assert.Contains("rate-limited", await last.Content.ReadAsStringAsync());
    }

    private static string UnknownToken() =>
        "b" + Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant();

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, string url, string remote, string? forwarded = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(RemoteAddressStartupFilter.Header, remote);
        if (forwarded is not null)
        {
            request.Headers.Add("X-Forwarded-For", forwarded);
        }

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return await client.SendAsync(request);
    }

    private WebApplicationFactoryHost WithRemoteAddress(string? proxyNetwork = null) =>
        new(factory.WithWebHostBuilder(builder =>
        {
            if (proxyNetwork is not null)
            {
                builder.UseSetting("Proxy:Networks:0", proxyNetwork);
            }

            builder.ConfigureTestServices(services =>
                services.AddSingleton<
                    Microsoft.AspNetCore.Hosting.IStartupFilter, RemoteAddressStartupFilter>());
        }));

    /// <summary>Owns the derived host so each case disposes its own pipeline.</summary>
    private sealed class WebApplicationFactoryHost(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> inner) : IDisposable
    {
        public HttpClient CreateClient() => inner.CreateClient();

        public void Dispose() => inner.Dispose();
    }

    /// <summary>
    /// Test-only: the test server sets no client address, so the limiter would see every
    /// request as the one unknown partition. This copies the header onto the connection
    /// before anything else in the pipeline runs.
    /// </summary>
    private sealed class RemoteAddressStartupFilter : IStartupFilter
    {
        public const string Header = "X-Test-Remote-Ip";

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use(async (context, following) =>
                {
                    if (context.Request.Headers.TryGetValue(Header, out var value) &&
                        System.Net.IPAddress.TryParse(value.ToString(), out var address))
                    {
                        context.Connection.RemoteIpAddress = address;
                    }

                    await following(context);
                });
                next(app);
            };
    }
}
