namespace EventBooking.Api.Auth;

/// <summary>
/// Renders the catalogue's forbidden body when authorization refused the request without one.
/// Endpoint failures already carry a problem body through ResultResponses; this covers only the
/// bare 403 the authorization middleware writes itself.
/// </summary>
/// <param name="next">The following middleware.</param>
public sealed class ForbiddenResponseMiddleware(RequestDelegate next)
{
    /// <summary>Runs the request, rendering a bare 403 as the catalogue's forbidden body.</summary>
    /// <param name="context">The request context.</param>
    /// <returns>A task tracking the request.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);

        if (context.Response is { StatusCode: StatusCodes.Status403Forbidden, HasStarted: false }
            && string.IsNullOrEmpty(context.Response.ContentType))
        {
            await Endpoints.ResultResponses.Forbidden().ExecuteAsync(context);
        }
    }
}
