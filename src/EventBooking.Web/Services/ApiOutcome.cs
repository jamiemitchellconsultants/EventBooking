namespace EventBooking.Web.Services;

public sealed record ApiOutcome<T>(bool IsSuccess, T? Value, int StatusCode, ApiProblem? Problem)
{
    public string? ErrorCode => Problem?.Type;
    public string? ErrorMessage => Problem?.Detail ?? Problem?.Title;

    public static ApiOutcome<T> Success(T? value) => new(true, value, 200, null);
    public static ApiOutcome<T> Success(T? value, int status) => new(true, value, status, null);
    public static ApiOutcome<T> Failure(ApiProblem problem) => new(false, default, problem.Status, problem);

    public static ApiOutcome<T> Failure(string message) =>
        Failure(new ApiProblem("unexpected", null, 500, message, [], null, null, null, null));
}
