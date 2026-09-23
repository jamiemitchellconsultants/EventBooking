using EventBooking.Application.Common;

namespace EventBooking.Api.Endpoints;

/// <summary>Maps application results to the API's HTTP response contract.</summary>
public static class ResultResponses
{
    /// <summary>Returns the HTTP status associated with a machine-readable application error code.</summary>
    public static int StatusCodeFor(string errorCode) => errorCode switch
    {
        "validation" => StatusCodes.Status400BadRequest,
        "employee_group_required" => StatusCodes.Status400BadRequest,
        "employee_group_unknown" => StatusCodes.Status400BadRequest,
        "employee_group_inactive" => StatusCodes.Status400BadRequest,
        "employee_group_unmapped" => StatusCodes.Status409Conflict,
        "forbidden" => StatusCodes.Status403Forbidden,
        "not_found" => StatusCodes.Status404NotFound,
        "conflict" => StatusCodes.Status409Conflict,
        Error.AppointmentVersionConflictCode => StatusCodes.Status409Conflict,
        Error.CandidateGroupActiveBookingConflictCode => StatusCodes.Status409Conflict,
        Error.CandidateReconciliationRequiredCode => StatusCodes.Status409Conflict,
        Error.CandidateRequirementSnapshotMismatchCode => StatusCodes.Status409Conflict,
        Error.RecoveryNotAvailableCode => StatusCodes.Status409Conflict,
        Error.RecoveryAlreadyPendingCode => StatusCodes.Status409Conflict,
        Error.RecoveryStateChangedCode => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError,
    };

    /// <summary>Returns no content for success or problem details for failure.</summary>
    public static IResult ToResponse(this Result result) =>
        result.IsSuccess ? Results.NoContent() : Problem(result.Error);

    /// <summary>Returns the result value for success or problem details for failure.</summary>
    public static IResult ToResponse<T>(this Result<T> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);

    /// <summary>Returns a created result for success or problem details for failure.</summary>
    public static IResult ToCreated<T>(this Result<T> result, Func<T, string> location) =>
        result.IsSuccess
            ? Results.Created(location(result.Value), result.Value)
            : Problem(result.Error);

    private static IResult Problem(Error error) =>
        Results.Problem(
            detail: error.Message,
            statusCode: StatusCodeFor(error.Code),
            title: error.Code);
}
