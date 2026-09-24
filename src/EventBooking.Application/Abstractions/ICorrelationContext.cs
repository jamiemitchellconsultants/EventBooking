namespace EventBooking.Application.Abstractions;

/// <summary>
/// The correlation identifier the current work is running under. Set once per request by the
/// API middleware, and once per run by the sweep and the outbox dispatcher. It lives in the
/// Application project so that anything staging an outbox row can read it without the
/// Infrastructure or Application projects referencing the API project.
/// </summary>
public interface ICorrelationContext
{
    /// <summary>Gets the identifier the current work is running under.</summary>
    string CorrelationId { get; }

    /// <summary>Runs the enclosing scope under an identifier.</summary>
    /// <param name="correlationId">The identifier to adopt.</param>
    /// <returns>A handle that restores the previous identifier when disposed.</returns>
    IDisposable Begin(string correlationId);
}

/// <summary>
/// The ambient implementation. Registered as a singleton: the value is carried by the
/// execution context, not by the instance, so a scoped registration would buy nothing and a
/// background run would see an empty value.
/// </summary>
public sealed class AsyncLocalCorrelationContext : ICorrelationContext
{
    private static readonly AsyncLocal<string?> Current = new();

    /// <summary>The identifier used when nothing has begun a scope.</summary>
    public const string None = "none";

    /// <inheritdoc />
    public string CorrelationId => Current.Value ?? None;

    /// <inheritdoc />
    public IDisposable Begin(string correlationId)
    {
        var previous = Current.Value;
        Current.Value = correlationId;
        return new Scope(previous);
    }

    private sealed class Scope(string? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
