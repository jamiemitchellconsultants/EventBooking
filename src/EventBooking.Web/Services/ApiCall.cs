using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace EventBooking.Web.Services;

/// <summary>Reads API responses into success values or safe structured failures.</summary>
public static class ApiCall
{
    private const string Generic = "Something went wrong. Please try again.";
    private const string Forbidden = "You do not have permission to do that.";

    private sealed record ProblemDetailsBody(string? Title, string? Detail, int? Status);
    private sealed record FailureDetails(string Message, string? ErrorCode);

    /// <summary>Reads a response that must contain a JSON body when successful.</summary>
    public static async Task<ApiOutcome<T>> ReadAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;

        if (response.IsSuccessStatusCode)
        {
            var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
            return value is null
                ? ApiOutcome<T>.Failure(Generic, status)
                : ApiOutcome<T>.Success(value, status);
        }

        var failure = await FailureOf(response, cancellationToken);
        return ApiOutcome<T>.Failure(failure.Message, status, failure.ErrorCode);
    }

    /// <summary>Reads a response whose successful result carries no body.</summary>
    public static async Task<ApiOutcome<bool>> ReadNoContentAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;

        if (response.IsSuccessStatusCode)
        {
            return ApiOutcome<bool>.Success(true, status);
        }

        var failure = await FailureOf(response, cancellationToken);
        return ApiOutcome<bool>.Failure(failure.Message, status, failure.ErrorCode);
    }

    /// <summary>Reads a successful plain-text body plus one response header value.</summary>
    /// <param name="response">The HTTP response to read.</param>
    /// <param name="headerName">The response header to capture, for example Content-Disposition.</param>
    /// <param name="cancellationToken">Cancels reading the body.</param>
    /// <returns>The body text and header value on success, or the parsed problem failure.</returns>
    public static async Task<ApiOutcome<TextWithHeader>> ReadTextWithHeaderAsync(
        HttpResponseMessage response,
        string headerName,
        CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;

        if (!response.IsSuccessStatusCode)
        {
            var failure = await FailureOf(response, cancellationToken);
            return ApiOutcome<TextWithHeader>.Failure(failure.Message, status, failure.ErrorCode);
        }

        // Deliberately not the JSON path: the successful body here is raw text, not JSON.
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var header =
            (response.Content.Headers.TryGetValues(headerName, out var contentValues)
                ? contentValues.FirstOrDefault()
                : null)
            ?? (response.Headers.TryGetValues(headerName, out var values)
                ? values.FirstOrDefault()
                : null)
            ?? string.Empty;

        return ApiOutcome<TextWithHeader>.Success(new TextWithHeader(body, header), status);
    }

    private static async Task<FailureDetails> FailureOf(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return new FailureDetails(Forbidden, "forbidden");
        }

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsBody>(cancellationToken);
            var message = string.IsNullOrWhiteSpace(problem?.Detail) ? Generic : problem.Detail;
            var errorCode = string.IsNullOrWhiteSpace(problem?.Title) ? null : problem.Title;
            return new FailureDetails(message, errorCode);
        }
        catch (JsonException)
        {
            return new FailureDetails(Generic, null);
        }
        catch (NotSupportedException)
        {
            return new FailureDetails(Generic, null);
        }
    }
}

/// <summary>A successful plain-text response body together with one captured response header.</summary>
/// <param name="Text">The raw response body.</param>
/// <param name="Header">The captured header value, or empty when the header was absent.</param>
public sealed record TextWithHeader(string Text, string Header);
