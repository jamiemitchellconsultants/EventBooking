using System.Net;

namespace EventBooking.Api.Tests.Conventions;

/// <summary>The two probes and the metrics endpoint design 08 names.</summary>
[Collection("api")]
public sealed class HealthAndMetricsTests(ApiFactory factory)
{
    [Fact]
    public async Task LivenessAnswersAnonymously()
    {
        factory.SignedInAs = null;

        var response = await factory.CreateClient().GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Readiness reaches the database, so it can only pass with the container up.</summary>
    [Fact]
    public async Task ReadinessReportsTheDatabase()
    {
        factory.SignedInAs = null;

        var response = await factory.CreateClient().GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("database", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MetricsAreExposedInPrometheusTextFormat()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();
        await client.GetAsync("/health/live");

        var response = await client.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("# TYPE eventbooking_http_requests_total counter", body, StringComparison.Ordinal);
        Assert.Contains("eventbooking_http_request_duration_seconds", body, StringComparison.Ordinal);
        Assert.Contains("eventbooking_capacity_exhausted_total", body, StringComparison.Ordinal);
    }

    /// <summary>A metric label never carries a route parameter value.</summary>
    [Fact]
    public async Task MetricLabelsCarryTheRoutePatternRatherThanItsValues()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();
        var token = "b" + Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant();
        await client.GetAsync($"/api/booking/{token}");

        var body = await client.GetStringAsync("/metrics");

        Assert.DoesNotContain(token, body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/booking/{token}", body, StringComparison.Ordinal);
    }
}
