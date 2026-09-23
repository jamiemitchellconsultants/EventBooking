namespace EventBooking.Application.Common;

/// <summary>
/// Signals that a database uniqueness backstop rejected a concurrent state transition. Handlers
/// convert this infrastructure-neutral exception to their stable conflict result instead of
/// exposing a database provider exception to API callers.
/// </summary>
public sealed class UniqueConstraintViolationException : Exception
{
    /// <summary>
    /// Creates a uniqueness-conflict signal while preserving the provider exception for logging.
    /// </summary>
    /// <param name="innerException">The inner exception.</param>
    public UniqueConstraintViolationException(Exception innerException)
        : base("A database uniqueness constraint rejected the operation.", innerException)
    {
    }
}
