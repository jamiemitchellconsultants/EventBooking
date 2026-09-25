// tests/EventBooking.Api.Tests/Observability/CapacityLockHoldHistogramTests.cs (complete)
using EventBooking.Api.Observability;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventBooking.Api.Tests.Observability;

[CollectionDefinition("capacity-lock-histogram", DisableParallelization = true)]
public sealed class CapacityLockHoldHistogramCollection;

[Collection("capacity-lock-histogram")]
public sealed class CapacityLockHoldHistogramTests
{
    [Fact]
    public void Lock_duration_has_cumulative_50ms_bucket_count_and_sum()
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        using var provider = services.BuildServiceProvider();
        using var exposition = new PrometheusText();
        using var metrics = new EventBookingMetrics(
            provider.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>());
        metrics.Record(TimeSpan.FromMilliseconds(20));
        metrics.Record(TimeSpan.FromMilliseconds(75));
        var output = exposition.Render();
        Assert.Contains("# TYPE eventbooking_capacity_lock_hold_duration_seconds histogram", output);
        Assert.Contains(
            "eventbooking_capacity_lock_hold_duration_seconds_bucket{le=\"0.049999\"} 1", output);
        Assert.Contains(
            "eventbooking_capacity_lock_hold_duration_seconds_bucket{le=\"+Inf\"} 2", output);
        Assert.Contains("eventbooking_capacity_lock_hold_duration_seconds_count 2", output);
        Assert.Contains("eventbooking_capacity_lock_hold_duration_seconds_sum ", output);
    }
}
