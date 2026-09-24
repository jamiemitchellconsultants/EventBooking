using System.Diagnostics;
using EventBooking.Application.Abstractions;

namespace EventBooking.Api.Observability;

/// <summary>
/// Adopts the caller's trace identifier, or mints one, and puts it on the logging scope, the
/// response and the ambient correlation context. Design 08: a correlation id taken from
/// traceparent or generated, propagated to background work and written into outbox rows.
/// </summary>
/// <param name="next">The following middleware.</param>
/// <param name="correlation">The ambient correlation context.</param>
/// <param name="logger">The logger the scope is opened on.</param>
public sealed class CorrelationMiddleware(
    RequestDelegate next,
    ICorrelationContext correlation,
    ILogger<CorrelationMiddleware> logger)
{
    /// <summary>The response header carrying the identifier back to the caller.</summary>
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>Runs the request under a correlation identifier.</summary>
    /// <param name="context">The request context.</param>
    /// <returns>A task tracking the request.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = FromTraceparent(context) ?? Activity.Current?.TraceId.ToString()
            ?? Guid.NewGuid().ToString("n");

        context.Response.Headers[HeaderName] = correlationId;
        using var ambient = correlation.Begin(correlationId);
        // RoutePattern is metadata such as /api/booking/{token}; Request.Path would copy a
        // bearer-equivalent attendee token into every structured log scope.
        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["correlationId"] = correlationId,
            ["route"] = route,
        });

        await next(context);

        // One line per request, from a category this application owns. With the framework's
        // own request logging silenced and scopes unprinted, this is where an operator finds
        // the correlation identifier — and every member of it is metadata, never request data.
        logger.LogInformation(
            "Handled {Method} {Route} as {Status} under {CorrelationId}.",
            context.Request.Method, route, context.Response.StatusCode, correlationId);
    }

    /// <summary>
    /// The W3C form is version-traceid-spanid-flags. Only the trace id is adopted: a span id
    /// identifies the caller's own operation, and reusing it would make two requests from one
    /// trace indistinguishable in this service's logs.
    /// </summary>
    private static string? FromTraceparent(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("traceparent", out var header))
        {
            return null;
        }

        var parts = header.ToString().Split('-');
        return parts.Length >= 3 && parts[1].Length == 32 ? parts[1] : null;
    }
}
