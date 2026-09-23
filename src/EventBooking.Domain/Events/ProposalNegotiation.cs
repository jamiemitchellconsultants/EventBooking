using EventBooking.Domain.Common;

namespace EventBooking.Domain.Events;

/// <summary>
/// One appointment type as a proposer offers it, with the two facts the proposal has to check:
/// whether it is still active, and whether a Manager currently holds it. The code travels so a
/// refusal can name the type the way a screen does.
/// </summary>
/// <param name="Id">The appointment type identifier.</param>
/// <param name="Code">The canonical code, for refusal messages.</param>
/// <param name="IsActive">Whether the type may be listed on a new proposal.</param>
/// <param name="HasCurrentManager">Whether a Manager currently holds the type.</param>
public readonly record struct ProposableAppointmentType(
    Guid Id, string Code, bool IsActive, bool HasCurrentManager);

/// <summary>
/// Every reason a proposal was refused, not just the first. A Manager filling in a form should see
/// all of them at once (FR-2.2).
/// </summary>
public sealed class ProposalValidationException : DomainException
{
    /// <summary>Creates a refusal listing every failure.</summary>
    /// <param name="failures">The failure codes, each optionally naming the offending type codes.</param>
    public ProposalValidationException(IReadOnlyList<string> failures)
        : base("The proposal was refused: " + string.Join(", ", failures)) => Failures = failures;

    /// <summary>The failure codes, in a stable order.</summary>
    public IReadOnlyList<string> Failures { get; }
}

/// <summary>
/// An acceptance, revision or withdrawal reached a proposal that is no longer open. The current
/// status travels so the caller can report it instead of guessing (FR-2.11).
/// </summary>
public sealed class ProposalNotOpenException : DomainException
{
    /// <summary>Creates a refusal carrying the proposal's current status.</summary>
    /// <param name="currentStatus">The status the proposal actually has.</param>
    public ProposalNotOpenException(EventProposalStatus currentStatus)
        : base($"The proposal is {currentStatus} and can no longer be changed.") =>
        CurrentStatus = currentStatus;

    /// <summary>The status the proposal actually has.</summary>
    public EventProposalStatus CurrentStatus { get; }
}
