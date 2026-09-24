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
                    builder.Append(group.Key).Append(series.Labels).Append(' ')
                        .Append(series.Value.ToString("G17", CultureInfo.InvariantCulture))
                        .Append('\n');
                }
            }
        }

        return builder.ToString();
    }

    /// <inheritdoc />
    public void Dispose() => _listener.Dispose();

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

        public double Value { get; set; }
    }
}
