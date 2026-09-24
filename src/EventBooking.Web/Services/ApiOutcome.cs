namespace EventBooking.Web.Services;

public sealed record ApiOutcome<T>(bool IsSuccess, T? Value, int StatusCode, ApiProblem? Problem)
{
    public string? ErrorCode => Problem?.Type;
    public string? ErrorMessage => Problem?.Detail ?? Problem?.Title;

    public static ApiOutcome<T> Success(T? value) => new(true, value, 200, null);
    public static ApiOutcome<T> Success(T? value, int status) => new(true, value, status, null);
    public static ApiOutcome<T> Failure(ApiProblem problem) => new(false, default, problem.Status, problem);

    public static ApiOutcome<T> Failure(string message) => Failure(message, 500);

    // Compatibility for the predecessor clients Task 25-27 have not replaced yet.
    // They pass a bare message triple rather than a parsed problem body; it maps to
    // the same shape so ErrorCode/ErrorMessage keep their meaning.
    public static ApiOutcome<T> Failure(string message, int statusCode, string? errorCode = null) =>
        new(false, default, statusCode, new ApiProblem(
            errorCode ?? "unexpected", null, statusCode, message, [],
            null, null, null, null));
}
