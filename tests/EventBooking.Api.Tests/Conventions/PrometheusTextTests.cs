using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using EventBooking.Api.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests.Conventions;

/// <summary>
/// The duration histogram in the exposition form Prometheus reads: cumulative _bucket series
/// with an le label ending at +Inf, plus _sum and _count. A single bare sample under TYPE
/// histogram is either rejected by a strict parser or gives histogram_quantile nothing to use.
/// </summary>
public sealed class PrometheusTextTests
{
    /// <summary>
    /// The listener is process-wide, so the series is isolated by a route label no other test
    /// can produce, and only that series is read back.
    /// </summary>
    [Fact]
    public void ADurationIsRenderedAsCumulativeBucketsWithSumAndCount()
    {
        using var text = new PrometheusText();
        using var services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        using var metrics = new EventBookingMetrics(services.GetRequiredService<IMeterFactory>());
        var route = $"/unit/{Guid.NewGuid():N}";
        var tags = new TagList { { "route", route } };

        metrics.Duration.Record(0.003, tags);
        metrics.Duration.Record(0.2, tags);
        metrics.Duration.Record(7, tags);

        var lines = text.Render().Split('\n');
        const string name = "eventbooking_http_request_duration_seconds";
        Assert.Contains($"# TYPE {name} histogram", lines);
        Assert.DoesNotContain(lines, line => line.StartsWith($"{name}{{route=\"{route}\"}} ", StringComparison.Ordinal));

        double Sample(string series) => double.Parse(
            Assert.Single(lines, line => line.StartsWith(series + " ", StringComparison.Ordinal))
                .Split(' ')[1],
            CultureInfo.InvariantCulture);

        Assert.Equal(1, Sample($"{name}_bucket{{route=\"{route}\",le=\"0.005\"}}"));
        Assert.Equal(1, Sample($"{name}_bucket{{route=\"{route}\",le=\"0.1\"}}"));
        Assert.Equal(2, Sample($"{name}_bucket{{route=\"{route}\",le=\"0.25\"}}"));
        Assert.Equal(2, Sample($"{name}_bucket{{route=\"{route}\",le=\"5\"}}"));
        Assert.Equal(3, Sample($"{name}_bucket{{route=\"{route}\",le=\"10\"}}"));
        Assert.Equal(3, Sample($"{name}_bucket{{route=\"{route}\",le=\"+Inf\"}}"));
        Assert.Equal(3, Sample($"{name}_count{{route=\"{route}\"}}"));
        Assert.Equal(7.203, Sample($"{name}_sum{{route=\"{route}\"}}"), 9);
    }

    /// <summary>A counter keeps its plain single-sample form.</summary>
    [Fact]
    public void ACounterIsStillOneSample()
    {
        using var text = new PrometheusText();
        using var services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        using var metrics = new EventBookingMetrics(services.GetRequiredService<IMeterFactory>());
        var route = $"/unit/{Guid.NewGuid():N}";

        metrics.Requests.Add(2, new TagList { { "route", route } });

        var lines = text.Render().Split('\n');
        Assert.Contains(
            $"eventbooking_http_requests_total{{route=\"{route}\"}} 2", lines);
    }
}
