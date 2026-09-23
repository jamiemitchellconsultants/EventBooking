using EventBooking.Domain.Events;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines ievent proposal repository for the current use case.</summary>
public interface IEventProposalRepository
{
    /// <summary>Provides get async within this contract.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<EventProposal?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Takes the row-level write lock for an existing proposal and loads its acceptances. Proposal
    /// lifecycle mutations must call this inside their unit-of-work transaction before observing
    /// status or changing an acceptance.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<EventProposal?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Provides list open async within this contract.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<EventProposal>> ListOpenAsync(CancellationToken cancellationToken);

    /// <summary>Provides add within this contract.</summary>
    /// <param name="proposal">The proposal.</param>
    void Add(EventProposal proposal);
}
