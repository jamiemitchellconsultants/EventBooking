using System.Diagnostics;

namespace EventBooking.Api.Observability;

/// <summary>
/// Counts and times every request. The route pattern is the label, never the request path: a
/// path carries attendee tokens and identifiers, and a metric label outlives the request.
/// </summary>
/// <param name="next">The following middleware.</param>
public sealed class RequestMetricsMiddleware(RequestDelegate next)
{
    /// <summary>Runs the request and records it.</summary>
    /// <param name="context">The request context.</param>
    /// <param name="metrics">The instruments.</param>
    /// <returns>A task tracking the request.</returns>
    public async Task InvokeAsync(HttpContext context, EventBookingMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(metrics);

        var started = Stopwatch.GetTimestamp();
        var threw = false;
        try
        {
            await next(context);
        }
        catch
        {
            threw = true;
            throw;
        }
        finally
        {
            // Recorded on the way out either way: a request that threw reaches the caller as
            // a 500, and an error-rate series that skipped it would undercount failures.
            var route = context.GetEndpoint() is RouteEndpoint endpoint
                ? "/" + endpoint.RoutePattern.RawText?.TrimStart('/')
                : "unmatched";
            var tags = new TagList
            {
                { "route", route },
                { "method", context.Request.Method },
                { "status", StatusOf(context, threw) },
            };
            metrics.Requests.Add(1, tags);
            metrics.Duration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds, tags);
        }
    }

    /// <summary>
    /// The status the caller receives. A throw before the response started becomes the
    /// server's 500, whatever the status code held when the exception left the pipeline.
    /// </summary>
    /// <param name="context">The request context.</param>
    /// <param name="threw">Whether the pipeline threw.</param>
    /// <returns>The status to record.</returns>
    internal static int StatusOf(HttpContext context, bool threw) =>
        threw && !context.Response.HasStarted
            ? StatusCodes.Status500InternalServerError
            : context.Response.StatusCode;
}
