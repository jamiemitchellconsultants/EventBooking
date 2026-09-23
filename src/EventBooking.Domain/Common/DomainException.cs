namespace EventBooking.Domain.Common;

/// <summary>The single failure mode of the domain layer: an invariant was violated.</summary>
public sealed class DomainException : Exception
{
    /// <summary>Defines domain exception for the current use case.</summary>
    /// <param name="message">The message.</param>
    public DomainException(string message) : base(message)
    {
    }
}
