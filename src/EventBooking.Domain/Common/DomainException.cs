namespace EventBooking.Domain.Common;

/// <summary>
/// The failure mode of the domain layer: an invariant was violated. Reference-data refusals that
/// must name what is blocking them derive from this rather than inventing a second failure mode.
/// </summary>
public class DomainException : Exception
{
    /// <summary>Creates a refusal carrying the rule that was broken.</summary>
    /// <param name="message">The message.</param>
    public DomainException(string message) : base(message)
    {
    }
}
