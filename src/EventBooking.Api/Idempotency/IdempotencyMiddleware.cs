using System.Security.Cryptography;
using System.Text;
using EventBooking.Api.Auth;
using EventBooking.Api.Endpoints;
using EventBooking.Application.Abstractions;

namespace EventBooking.Api.Idempotency;

/// <summary>
/// Replays the first response for a repeated Idempotency-Key on a create endpoint. A key
/// reused with a different body is a caller mistake, not a replay, and is refused rather than
/// answered with somebody else's result.
/// </summary>
/// <param name="next">The following middleware.</param>
public sealed class IdempotencyMiddleware(RequestDelegate next)
{
    /// <summary>The header design 05 names.</summary>
    public const string HeaderName = "Idempotency-Key";

    /// <summary>The longest key the retention table holds.</summary>
    public const int MaxKeyLength = 200;

    /// <summary>Applies the retention to one request.</summary>
    /// <param name="context">The request context.</param>
    /// <param name="store">The retention store.</param>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="clock">The clock.</param>
    /// <returns>A task tracking the request.</returns>
    public async Task InvokeAsync(
        HttpContext context, IIdempotencyStore store, IdempotencyKeyLock keyLock,
        ICallerAccessor caller, IClock clock)
    {
        if (!HttpMethods.IsPost(context.Request.Method) ||
            !context.Request.Headers.TryGetValue(HeaderName, out var header) ||
            string.IsNullOrWhiteSpace(header) ||
            caller.StaffUserId is not { } staffUserId)
        {
            await next(context);
            return;
        }

        var key = header.ToString();

        // Refused before the handler runs: a key the retention table cannot hold would fail
        // the write only after the create had committed, and the caller's retry would then
        // find nothing retained and create a second time.
        if (key.Length > MaxKeyLength)
        {
            await ResultResponses
                .ValidationFailed(
                    HeaderName,
                    "idempotency-key-too-long",
                    $"An Idempotency-Key may be at most {MaxKeyLength} characters.")
                .ExecuteAsync(context);
            return;
        }

        var route = context.GetEndpoint()?.DisplayName ?? context.Request.Path.Value ?? "/";
        context.Request.EnableBuffering();
        var requestHash = await HashBodyAsync(context.Request);
        var now = clock.UtcNow;
        await using var heldKey = await keyLock.AcquireAsync(
            staffUserId, route, key, context.RequestAborted);

        var retained = await store.TryGetAsync(staffUserId, route, key, now, context.RequestAborted);
        if (retained is not null)
        {
            if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(retained.RequestHash), Encoding.UTF8.GetBytes(requestHash)))
            {
                await ResultResponses
                    .ValidationFailed(
                        HeaderName,
                        "idempotency-key-reused",
                        "This Idempotency-Key was already used with a different request body.")
                    .ExecuteAsync(context);
                return;
            }

            context.Response.StatusCode = retained.StatusCode;
            context.Response.ContentType = retained.ContentType;
            if (retained.Location is not null)
            {
                context.Response.Headers.Location = retained.Location;
            }

            await context.Response.WriteAsync(retained.Body, context.RequestAborted);
            return;
        }

        // Buffer the response so a retained body is exactly what the caller received. Only a
        // success is retained: a failure is a state the caller can legitimately retry out of.
        var original = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = original;
        }

        buffer.Position = 0;
        var body = await new StreamReader(buffer).ReadToEndAsync(context.RequestAborted);
        if (context.Response.StatusCode is >= 200 and < 300)
        {
            var location = context.Response.Headers.Location.ToString();
            await store.SaveAsync(
                staffUserId, route, key,
                new IdempotentResponse(
                    requestHash, context.Response.StatusCode, body,
                    context.Response.ContentType,
                    string.IsNullOrEmpty(location) ? null : location),
                now, context.RequestAborted);
        }

        await context.Response.WriteAsync(body, context.RequestAborted);
    }

    private static async Task<string> HashBodyAsync(HttpRequest request)
    {
        request.Body.Position = 0;
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(request.Body, request.HttpContext.RequestAborted);
        request.Body.Position = 0;
        return Convert.ToHexString(hash);
    }
}
