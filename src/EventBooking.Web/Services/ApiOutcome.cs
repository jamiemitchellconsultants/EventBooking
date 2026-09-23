namespace EventBooking.Web.Services;

/// <summary>Represents the success value or safe failure returned by an API call.</summary>
/// <typeparam name="T">The successful response body type.</typeparam>
/// <param name="IsSuccess">Whether the API call completed successfully.</param>
/// <param name="Value">The successful response body, or null after failure.</param>
/// <param name="ErrorMessage">The safe user-facing failure message, or null after success.</param>
/// <param name="StatusCode">The HTTP response status code.</param>
/// <param name="ErrorCode">The stable machine-readable problem code, when supplied.</param>
public sealed record ApiOutcome<T>(
    bool IsSuccess,
    T? Value,
    string? ErrorMessage,
    int StatusCode,
    string? ErrorCode = null)
{
    /// <summary>Creates a successful outcome with its response body and HTTP status.</summary>
    public static ApiOutcome<T> Success(T value, int statusCode) =>
        new(true, value, null, statusCode, null);

    /// <summary>Creates a failed outcome with a safe message and optional machine-readable code.</summary>
    public static ApiOutcome<T> Failure(
        string message,
        int statusCode,
        string? errorCode = null) =>
        new(false, default, message, statusCode, errorCode);
}
