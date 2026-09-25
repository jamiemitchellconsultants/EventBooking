using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text;

namespace EventBooking.Api.Observability;

/// <summary>
/// Renders the meter's instruments in Prometheus text format. A listener collects every
/// measurement as it is taken and keeps the running totals; rendering is then a read of that
/// state, so a scrape costs nothing on the request path.
/// </summary>
public sealed class PrometheusText : IDisposable
{
    /// <summary>
    /// The request-duration bucket upper bounds, in seconds: the Prometheus client libraries'
    /// defaults, which span a fast read to a slow export, except the 50 ms bound is set
    /// just below it so the 500-way release load test can assert the NFR-P3 percentile
    /// from the booking handler's measurement.
    /// </summary>
    private static readonly double[] BucketBounds =
        [0.005, 0.01, 0.025, 0.049999, 0.1, 0.25, 0.5, 1, 2.5, 5, 10];

    private readonly MeterListener _listener = new();
    private readonly Dictionary<string, Series> _series = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <summary>Starts listening to the API's meter.</summary>
    public PrometheusText()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == EventBookingMetrics.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>(
            (instrument, measurement, tags, _) => Record(instrument, measurement, tags));
        _listener.SetMeasurementEventCallback<double>(
            (instrument, measurement, tags, _) => Record(instrument, measurement, tags));
        _listener.Start();
    }

    /// <summary>Renders every collected series.</summary>
    /// <returns>The Prometheus exposition text.</returns>
    public string Render()
    {
        var builder = new StringBuilder();
        lock (_gate)
        {
            foreach (var group in _series.Values.GroupBy(x => x.Name, StringComparer.Ordinal)
                .OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var first = group.First();
                builder.Append("# HELP ").Append(group.Key).Append(' ')
                    .Append(first.Description).Append('\n');
                builder.Append("# TYPE ").Append(group.Key).Append(' ')
                    .Append(first.Kind).Append('\n');
                foreach (var series in group.OrderBy(x => x.Labels, StringComparer.Ordinal))
                {
                    if (series.Buckets is { } buckets)
                    {
                        AppendHistogram(builder, group.Key, series, buckets);
                        continue;
                    }

                    AppendSample(builder, group.Key, series.Labels, series.Value);
                }
            }
        }

        return builder.ToString();
    }

    /// <inheritdoc />
    public void Dispose() => _listener.Dispose();

    /// <summary>
    /// The exposition form Prometheus reads a histogram in: one cumulative _bucket per upper
    /// bound, ending at +Inf, then _sum and _count. histogram_quantile needs the buckets.
    /// </summary>
    private static void AppendHistogram(
        StringBuilder builder, string name, Series series, long[] buckets)
    {
        for (var index = 0; index < BucketBounds.Length; index++)
        {
            AppendSample(
                builder, name + "_bucket",
                WithLabel(series.Labels, "le", BucketBounds[index].ToString(CultureInfo.InvariantCulture)),
                buckets[index]);
        }

        AppendSample(builder, name + "_bucket", WithLabel(series.Labels, "le", "+Inf"), series.Count);
        AppendSample(builder, name + "_sum", series.Labels, series.Value);
        AppendSample(builder, name + "_count", series.Labels, series.Count);
    }

    private static void AppendSample(StringBuilder builder, string name, string labels, double value) =>
        builder.Append(name).Append(labels).Append(' ')
            .Append(value.ToString("G17", CultureInfo.InvariantCulture))
            .Append('\n');

    /// <summary>Adds one label to an already formatted label set.</summary>
    private static string WithLabel(string labels, string key, string value) =>
        labels.Length == 0
            ? $"{{{key}=\"{value}\"}}"
            : $"{labels[..^1]},{key}=\"{value}\"}}";

    private void Record(Instrument instrument, double measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var labels = Format(tags);
        var key = instrument.Name + labels;
        lock (_gate)
        {
            if (!_series.TryGetValue(key, out var series))
            {
                series = new Series(
                    instrument.Name,
                    labels,
                    instrument is Histogram<double> ? "histogram" : "counter",
                    instrument.Description ?? instrument.Name);
                _series[key] = series;
            }

            series.Value += measurement;
            series.Count++;
            if (series.Buckets is { } buckets)
            {
                // Cumulative at record time: each bound counts every observation at or below it.
                for (var index = 0; index < BucketBounds.Length; index++)
                {
                    if (measurement <= BucketBounds[index])
                    {
                        buckets[index]++;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Label values are escaped, not trusted. Every tag this API emits is a route pattern, a
    /// method, a status or a policy name — never a route parameter value — and the metrics
    /// suite asserts that an attendee token never reaches the output.
    /// </summary>
    private static string Format(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (tags.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder("{");
        for (var index = 0; index < tags.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append(tags[index].Key).Append("=\"")
                .Append((tags[index].Value?.ToString() ?? string.Empty)
                    .Replace("\\", "\\\\", StringComparison.Ordinal)
                    .Replace("\"", "\\\"", StringComparison.Ordinal)
                    .Replace("\n", "\\n", StringComparison.Ordinal))
                .Append('"');
        }

        return builder.Append('}').ToString();
    }

    private sealed class Series(string name, string labels, string kind, string description)
    {
        public string Name { get; } = name;

        public string Labels { get; } = labels;

        public string Kind { get; } = kind;

        public string Description { get; } = description;

        /// <summary>Gets or sets the counter's total, or the histogram's sum.</summary>
        public double Value { get; set; }

        /// <summary>Gets or sets how many measurements were recorded.</summary>
        public long Count { get; set; }

        /// <summary>Gets the cumulative count per upper bound, on a histogram only.</summary>
        public long[]? Buckets { get; } = kind == "histogram" ? new long[BucketBounds.Length] : null;
    }
}
