using System.Diagnostics.Metrics;
using EventBooking.Api.Observability;
using EventBooking.Application.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventBooking.Api.Tests.Conventions;

/// <summary>
/// A request that throws is still a request. It reaches the caller as a 500, so it is counted
/// as one and gets its correlation line: an error-rate alert that cannot see unhandled
/// failures undercounts exactly the failures it exists for.
/// </summary>
public sealed class ThrownRequestTests
{
    [Fact]
    public async Task AThrownRequestIsCountedAsAServerError()
    {
        using var text = new PrometheusText();
        using var services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        using var metrics = new EventBookingMetrics(services.GetRequiredService<IMeterFactory>());
        var (context, route) = RequestTo();
        var middleware = new RequestMetricsMiddleware(_ => throw new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context, metrics));

        Assert.Contains(
            $"eventbooking_http_requests_total{{route=\"{route}\",method=\"GET\",status=\"500\"}} 1",
            text.Render().Split('\n'));
    }

    [Fact]
    public async Task AThrownRequestStillWritesItsCorrelationLine()
    {
        var logger = new CapturingLogger();
        var (context, route) = RequestTo();
        var middleware = new CorrelationMiddleware(
            _ => throw new InvalidOperationException("boom"),
            new AsyncLocalCorrelationContext(),
            logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        var line = Assert.Single(logger.Messages);
        Assert.Contains(route.TrimStart('/'), line, StringComparison.Ordinal);
        Assert.Contains("as 500", line, StringComparison.Ordinal);
    }

    /// <summary>A GET matched to a route pattern no other test produces.</summary>
    private static (HttpContext Context, string Route) RequestTo()
    {
        var route = $"/unit/{Guid.NewGuid():N}";
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.SetEndpoint(new RouteEndpoint(
            _ => Task.CompletedTask, RoutePatternFactory.Parse(route), 0,
            EndpointMetadataCollection.Empty, "unit"));
        return (context, route);
    }

    private sealed class CapturingLogger : ILogger<CorrelationMiddleware>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
