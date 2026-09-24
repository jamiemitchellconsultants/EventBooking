using EventBooking.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace EventBooking.Api.Endpoints;

/// <summary>One field or row error inside an RFC 9457 body.</summary>
/// <param name="Field">The request field at fault, when one can be named.</param>
/// <param name="Line">The CSV line at fault, when one can be named.</param>
/// <param name="Code">The machine-readable reason.</param>
/// <param name="Message">The caller-safe explanation.</param>
public sealed record ProblemError(string? Field, int? Line, string Code, string Message);

/// <summary>Maps application results to the API's RFC 9457 response contract.</summary>
public static class ResultResponses
{
    /// <summary>Returns no content for success or problem details for failure.</summary>
    /// <param name="result">The application result.</param>
    /// <returns>The HTTP result.</returns>
    public static IResult ToResponse(this Result result) =>
        result.IsSuccess ? Results.NoContent() : Problem(result.Error);

    /// <summary>Returns the result value for success or problem details for failure.</summary>
    /// <param name="result">The application result.</param>
    /// <returns>The HTTP result.</returns>
    public static IResult ToResponse<T>(this Result<T> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);

    /// <summary>Projects a successful value before returning it, or renders the failure.</summary>
    /// <param name="result">The application result.</param>
    /// <param name="projection">The response projection.</param>
    /// <returns>The HTTP result.</returns>
    public static IResult ToResponse<T, TResponse>(
        this Result<T> result, Func<T, TResponse> projection) =>
        result.IsSuccess ? Results.Ok(projection(result.Value)) : Problem(result.Error);

    /// <summary>Returns a created result for success or problem details for failure.</summary>
    /// <param name="result">The application result.</param>
    /// <param name="location">The location of the created resource.</param>
    /// <returns>The HTTP result.</returns>
    public static IResult ToCreated<T>(this Result<T> result, Func<T, string> location) =>
        result.IsSuccess
            ? Results.Created(location(result.Value), result.Value)
            : Problem(result.Error);

    /// <summary>Renders the catalogue's forbidden body for an authorization refusal.</summary>
    /// <returns>The HTTP result.</returns>
    public static IResult Forbidden() => Problem(Error.Forbidden("You cannot do that."));

    /// <summary>Renders one field error as the catalogue's validation-failed body.</summary>
    /// <param name="field">The field at fault.</param>
    /// <param name="code">The machine-readable reason.</param>
    /// <param name="message">The caller-safe explanation.</param>
    /// <returns>The HTTP result.</returns>
    public static IResult ValidationFailed(string field, string code, string message)
    {
        var shape = ProblemCatalogue.For(ProblemCatalogue.ValidationCode);
        var body = Body(shape, message, [new ProblemError(field, null, code, message)]);
        return Results.Problem(body);
    }

    /// <summary>Renders several field or row errors as the catalogue's validation-failed body.</summary>
    /// <param name="message">The caller-safe summary.</param>
    /// <param name="errors">The errors, carrying line numbers for CSV rows.</param>
    /// <returns>The HTTP result.</returns>
    public static IResult ValidationFailed(string message, IReadOnlyList<ProblemError> errors)
    {
        var shape = ProblemCatalogue.For(ProblemCatalogue.ValidationCode);
        var body = Body(shape, message, errors);
        return Results.Problem(body);
    }

    /// <summary>Renders the first call of a two-step action, carrying its consequence.</summary>
    /// <param name="detail">The caller-safe explanation.</param>
    /// <param name="consequence">The effects confirming would have.</param>
    /// <returns>The HTTP result.</returns>
    public static IResult ConfirmationRequired(
        string detail, IReadOnlyDictionary<string, long> consequence)
    {
        var shape = ProblemCatalogue.For(Error.ConfirmationRequiredCode);
        var body = Body(shape, detail, [new ProblemError(null, null, shape.Type, detail)]);
        body.Extensions["consequence"] = consequence;
        return Results.Problem(body);
    }

    /// <summary>Renders one application error as its catalogued problem body.</summary>
    /// <param name="error">The application error.</param>
    /// <returns>The problem body, for a test or a caller that needs it before writing.</returns>
    public static ProblemDetails ProblemBodyFor(Error error)
    {
        var shape = ProblemCatalogue.For(error.Code);
        var body = Body(
            shape, error.Message, [new ProblemError(null, null, shape.Type, error.Message)]);

        if (shape.DataMember is not null && error.Data is { Count: > 0 } data)
        {
            body.Extensions[shape.DataMember] = shape.ScalarKey is { } key
                ? data.TryGetValue(key, out var scalar) ? scalar : null
                : data;
            if (shape.ObjectMember is not null && shape.ObjectKeys is { Count: > 0 } keys)
            {
                body.Extensions[shape.ObjectMember] = keys
                    .Where(pair => data.ContainsKey(pair.Value))
                    .ToDictionary(pair => pair.Key, pair => data[pair.Value]);
            }
        }

        if (error.RelatedId is not null)
            body.Extensions["relatedId"] = error.RelatedId;

        return body;
    }

    private static IResult Problem(Error error) => Results.Problem(ProblemBodyFor(error));

    private static ProblemDetails Body(
        ProblemShape shape, string detail, IReadOnlyList<ProblemError> errors)
    {
        var body = new ProblemDetails
        {
            Type = shape.Type,
            Title = shape.Title,
            Status = shape.Status,
            Detail = detail,
        };
        body.Extensions["errors"] = errors;
        return body;
    }
}
