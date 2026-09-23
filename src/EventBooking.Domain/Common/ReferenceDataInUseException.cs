namespace EventBooking.Domain.Common;

/// <summary>
/// Reference data is never hard-deleted, and never changed out from under live use. This refusal
/// names what is blocking, and how much of it, so a screen can say "3 open proposals" rather than
/// "not allowed" (FR-1.2, FR-1.5, FR-1.6).
/// </summary>
public sealed class ReferenceDataInUseException : DomainException
{
    /// <summary>Creates a refusal carrying its blocking counts.</summary>
    /// <param name="message">What was refused.</param>
    /// <param name="blocking">Each kind of live use, with its count.</param>
    public ReferenceDataInUseException(string message, IReadOnlyDictionary<string, int> blocking)
        : base(message) => Blocking = blocking;

    /// <summary>The live uses that blocked the change, keyed by kind.</summary>
    public IReadOnlyDictionary<string, int> Blocking { get; }
}
