namespace EventBooking.Application.Common;

/// <summary>Defines result for the current use case.</summary>
public sealed class Result
{
    private Result(bool isSuccess, Error error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>Defines is success for the current use case.</summary>
    public bool IsSuccess { get; }

    /// <summary>Defines is failure for the current use case.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>Defines error for the current use case.</summary>
    public Error Error { get; }

    /// <summary>Defines success for the current use case.</summary>
    public static Result Success() => new(true, Error.None);

    /// <summary>Defines failure for the current use case.</summary>
    /// <param name="error">The error.</param>
    public static Result Failure(Error error) => new(false, error);
}

/// <summary>Defines result for the current use case.</summary>
public sealed class Result<TValue>
{
    private readonly TValue? _value;

    private Result(bool isSuccess, TValue? value, Error error)
    {
        IsSuccess = isSuccess;
        _value = value;
        Error = error;
    }

    /// <summary>Defines is success for the current use case.</summary>
    public bool IsSuccess { get; }

    /// <summary>Defines is failure for the current use case.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>Defines error for the current use case.</summary>
    public Error Error { get; }

    /// <summary>Defines value for the current use case.</summary>
    public TValue Value =>
        IsSuccess ? _value! : throw new InvalidOperationException("A failed result has no value.");

    /// <summary>Defines success for the current use case.</summary>
    /// <param name="value">The value.</param>
    public static Result<TValue> Success(TValue value) => new(true, value, Error.None);

    /// <summary>Defines failure for the current use case.</summary>
    /// <param name="error">The error.</param>
    public static Result<TValue> Failure(Error error) => new(false, default, error);
}
