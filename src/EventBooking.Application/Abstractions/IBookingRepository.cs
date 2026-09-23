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

    /// <summary>
    /// Locates the immutable event identifier needed to take the event guard. It is not
    /// authoritative booking state: callers must lock and re-read the booking after that guard.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Guid?> GetEventIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Locates the attendee lifecycle identifier needed to take the attendee guard before a
    /// cancellation or rebooking. Callers must re-read and lock the booking inside the transaction.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Guid?> GetAttendeeIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Takes a row-level write lock on the booking with the given id belonging to the given
    /// attendee, or returns null when no such booking exists. Must be called inside the
    /// staff-cancellation transaction before checking whether the booking remains active.
    /// </summary>
    /// <param name="bookingId">The booking identifier.</param>
    /// <param name="attendeeId">The owning attendee identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The locked booking, or null for an unknown id or a booking of another attendee.</returns>
    Task<Booking?> LockByIdForAttendeeAsync(
        Guid bookingId,
        Guid attendeeId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Takes a row-level write lock on the attendee's current active booking. Attendee deletion
    /// and confirmation use this after the attendee and invite locks to prevent duplicate active
    /// bookings from legacy or externally written data.
    /// </summary>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Booking?> LockActiveForAttendeeAsync(Guid attendeeId, CancellationToken cancellationToken);

    /// <summary>Provides get active for attendee async within this contract.</summary>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Booking?> GetActiveForAttendeeAsync(Guid attendeeId, CancellationToken cancellationToken);

    /// <summary>Locks the Attendee's active original Booking after Attendee and Invite locks.</summary>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Booking?> LockActiveOriginalForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Takes a non-authoritative snapshot of attendee identifiers with an active booking on the
    /// supplied eventItem. Callers must lock each attendee and re-read its booking before mutating
    /// any lifecycle state.
    /// </summary>
    /// <param name="eventId">The event id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Guid>> ListActiveAttendeeIdsForEventAsync(
        Guid eventId,
        CancellationToken cancellationToken);

    /// <summary>Provides list active for event async within this contract.</summary>
    /// <param name="eventId">The event id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Booking>> ListActiveForEventAsync(Guid eventId, CancellationToken cancellationToken);

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
