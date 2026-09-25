using System.Diagnostics.Metrics;
using EventBooking.Application.Abstractions;

namespace EventBooking.Api.Observability;

/// <summary>
/// The instruments design 08 names, on one meter. System.Diagnostics.Metrics is the
/// OpenTelemetry metrics API in .NET, so this is an OpenTelemetry meter without taking a
/// dependency on a pre-release exporter package; PrometheusText renders it.
/// </summary>
public sealed class EventBookingMetrics : ICapacityLockHoldObserver, IDisposable
{
    /// <summary>The meter name every instrument here is published under.</summary>
    public const string MeterName = "EventBooking.Api";

    private readonly Meter _meter;

    /// <summary>Creates the meter and its instruments.</summary>
    /// <param name="factory">The meter factory.</param>
    public EventBookingMetrics(IMeterFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _meter = factory.Create(MeterName);
        Requests = _meter.CreateCounter<long>(
            "eventbooking_http_requests_total", "requests",
            "Requests served, by route pattern, method and status.");
        Duration = _meter.CreateHistogram<double>(
            "eventbooking_http_request_duration_seconds", "s",
            "Request duration, by route pattern.");
        CapacityExhausted = _meter.CreateCounter<long>(
            "eventbooking_capacity_exhausted_total", "refusals",
            "Bookings refused for want of capacity — a business signal of undersupply.");
        RateLimited = _meter.CreateCounter<long>(
            "eventbooking_rate_limited_total", "rejections",
            "Requests rejected by a rate limiter, by policy.");
        // NFR-P3's bound is 50 ms exclusive, and a bucket counts values at or below its bound,
        // so this histogram alone sets its 50 ms bucket just under it.
        CapacityLockHoldDuration = _meter.CreateHistogram<double>(
            "eventbooking_capacity_lock_hold_duration_seconds", "s",
            "Time from the event capacity lock through transaction release.",
            tags: null,
            advice: new InstrumentAdvice<double>
            {
                HistogramBucketBoundaries = [0.005, 0.01, 0.025, 0.049999, 0.1, 0.25, 0.5, 1, 2.5, 5, 10],
            });

        // Declare the business series even before the first refusal: a counter no request
        // has touched yet would otherwise be absent from the scrape rather than zero.
        CapacityExhausted.Add(0);
    }

    /// <summary>Gets the request counter.</summary>
    public Counter<long> Requests { get; }

    /// <summary>Gets the request-duration histogram.</summary>
    public Histogram<double> Duration { get; }

    /// <summary>Gets the capacity-refusal counter.</summary>
    public Counter<long> CapacityExhausted { get; }

    /// <summary>Gets the rate-limiter rejection counter.</summary>
    public Counter<long> RateLimited { get; }

    /// <summary>Gets the capacity-lock hold duration histogram.</summary>
    public Histogram<double> CapacityLockHoldDuration { get; }

    /// <summary>Records one capacity-lock hold interval in seconds, without labels.</summary>
    /// <param name="elapsed">The time from the event capacity lock through release.</param>
    public void Record(TimeSpan elapsed) =>
        CapacityLockHoldDuration.Record(elapsed.TotalSeconds);

    /// <inheritdoc />
    public void Dispose() => _meter.Dispose();
}
