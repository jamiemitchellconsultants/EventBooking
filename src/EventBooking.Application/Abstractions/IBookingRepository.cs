using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines ibooking repository for the current use case.</summary>
public interface IBookingRepository
{
    /// <summary>Provides get async within this contract.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Booking?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Locks one booking row before rotating its management-token hash.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Booking?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Provides get by manage token hash async within this contract.</summary>
    /// <param name="manageTokenHash">The manage token hash.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Booking?> GetByManageTokenHashAsync(string manageTokenHash, CancellationToken cancellationToken);

    /// <summary>
    /// Locates the immutable confirmed-slot identifier needed to take the slot guard. It is not
    /// authoritative booking state: callers must lock and re-read the booking after that guard.
    /// </summary>
    /// <param name="manageTokenHash">The manage token hash.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Guid?> GetConfirmedSlotIdByManageTokenHashAsync(
        string manageTokenHash,
        CancellationToken cancellationToken);

    /// <summary>
    /// Locates the candidate lifecycle identifier needed to take the candidate guard before a
    /// cancellation or rebooking. Callers must re-read and lock the booking inside the transaction.
    /// </summary>
    /// <param name="manageTokenHash">The manage token hash.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Guid?> GetCandidateIdByManageTokenHashAsync(
        string manageTokenHash,
        CancellationToken cancellationToken);

    /// <summary>
    /// Takes a row-level write lock on the booking identified by its manage-token hash and returns
    /// it. Must be called inside the candidate-cancellation transaction before checking whether
    /// the booking remains active or returning its capacity.
    /// </summary>
    /// <param name="manageTokenHash">The manage token hash.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Booking?> LockByManageTokenHashForUpdateAsync(
        string manageTokenHash,
        CancellationToken cancellationToken);

    /// <summary>
    /// Takes a row-level write lock on the booking with the given id belonging to the given
    /// candidate, or returns null when no such booking exists. Must be called inside the
    /// staff-cancellation transaction before checking whether the booking remains active.
    /// </summary>
    /// <param name="bookingId">The booking identifier.</param>
    /// <param name="candidateId">The owning candidate identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The locked booking, or null for an unknown id or a booking of another candidate.</returns>
    Task<Booking?> LockByIdForCandidateAsync(
        Guid bookingId,
        Guid candidateId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Takes a row-level write lock on the candidate's current active booking. Candidate deletion
    /// and confirmation use this after the candidate and invite locks to prevent duplicate active
    /// bookings from legacy or externally written data.
    /// </summary>
    /// <param name="candidateId">The candidate id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Booking?> LockActiveForCandidateAsync(Guid candidateId, CancellationToken cancellationToken);

    /// <summary>Provides get active for candidate async within this contract.</summary>
    /// <param name="candidateId">The candidate id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Booking?> GetActiveForCandidateAsync(Guid candidateId, CancellationToken cancellationToken);

    /// <summary>Locks the Candidate's active original Booking after Candidate and Invite locks.</summary>
    /// <param name="candidateId">The candidate id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Booking?> LockActiveOriginalForCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Takes a non-authoritative snapshot of candidate identifiers with an active booking on the
    /// supplied slot. Callers must lock each candidate and re-read its booking before mutating
    /// any lifecycle state.
    /// </summary>
    /// <param name="confirmedSlotId">The confirmed slot id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Guid>> ListActiveCandidateIdsForSlotAsync(
        Guid confirmedSlotId,
        CancellationToken cancellationToken);

    /// <summary>Provides list active for slot async within this contract.</summary>
    /// <param name="confirmedSlotId">The confirmed slot id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Booking>> ListActiveForSlotAsync(Guid confirmedSlotId, CancellationToken cancellationToken);

    /// <summary>Lists the original and all direct recovery Bookings ordered by creation and ID.</summary>
    /// <param name="originalBookingId">The original booking id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Booking>> ListJourneyAsync(
        Guid originalBookingId,
        CancellationToken cancellationToken);

    /// <summary>Locks the non-cancelled recovery Booking for a root, if one remains Active.</summary>
    /// <param name="originalBookingId">The original booking id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Booking?> LockActiveRecoveryAsync(
        Guid originalBookingId,
        CancellationToken cancellationToken);

    /// <summary>Provides add within this contract.</summary>
    /// <param name="booking">The booking.</param>
    void Add(Booking booking);
}
