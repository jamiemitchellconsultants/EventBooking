# 02a — Deterministic attendee links and the token version counter, edits 1 (Task 9a)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — src/EventBooking.Application/Abstractions/IBookingRepository.cs — 1/1

<!-- retirement-file: {"id":0,"file":"src/EventBooking.Application/Abstractions/IBookingRepository.cs","beforeSha":"d3a7422d0070ddc9f859a8f0914d50986177a5f778a38680b5dd2a9c8a3077f6","afterSha":"2b3175499e9c574f4794b6317240994d9d6daf1c8bbf0fd09811d7eb30127034","side":"before","part":1,"parts":1} -->

`````csharp
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
    /// Locates the immutable event identifier needed to take the event guard. It is not
    /// authoritative booking state: callers must lock and re-read the booking after that guard.
    /// </summary>
    /// <param name="manageTokenHash">The manage token hash.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Guid?> GetEventIdByManageTokenHashAsync(
        string manageTokenHash,
        CancellationToken cancellationToken);

    /// <summary>
    /// Locates the attendee lifecycle identifier needed to take the attendee guard before a
    /// cancellation or rebooking. Callers must re-read and lock the booking inside the transaction.
    /// </summary>
    /// <param name="manageTokenHash">The manage token hash.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Guid?> GetAttendeeIdByManageTokenHashAsync(
        string manageTokenHash,
        CancellationToken cancellationToken);

    /// <summary>
    /// Takes a row-level write lock on the booking identified by its manage-token hash and returns
    /// it. Must be called inside the attendee-cancellation transaction before checking whether
    /// the booking remains active or returning its capacity.
    /// </summary>
    /// <param name="manageTokenHash">The manage token hash.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Booking?> LockByManageTokenHashForUpdateAsync(
        string manageTokenHash,
        CancellationToken cancellationToken);

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
`````

## after — src/EventBooking.Application/Abstractions/IBookingRepository.cs — 1/1

<!-- retirement-file: {"id":0,"file":"src/EventBooking.Application/Abstractions/IBookingRepository.cs","beforeSha":"d3a7422d0070ddc9f859a8f0914d50986177a5f778a38680b5dd2a9c8a3077f6","afterSha":"2b3175499e9c574f4794b6317240994d9d6daf1c8bbf0fd09811d7eb30127034","side":"after","part":1,"parts":1} -->

`````csharp
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
`````

## before — src/EventBooking.Application/Abstractions/IInviteRepository.cs — 1/1

<!-- retirement-file: {"id":1,"file":"src/EventBooking.Application/Abstractions/IInviteRepository.cs","beforeSha":"11cf32adcf3c2c881259190eb88fd172455196044ce6594fa2a9ecfea0a16be0","afterSha":"f38d7159bcfffde894829e1907961a321b2684041c23d1d2826b048d0af3ce0e","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Invites;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines iinvite repository for the current use case.</summary>
public interface IInviteRepository
{
    /// <summary>Provides get async within this contract.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Invite?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Takes a row-level write lock on the invite identified by its identifier and loads its
    /// offered events. Attendee deletion and expiry processing use it after the attendee lock.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Invite?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Provides get by token hash async within this contract.</summary>
    /// <param name="tokenHash">The token hash.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Invite?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>
    /// Takes a row-level write lock on the invite identified by its token hash and returns it.
    /// Must be called inside the booking-confirmation transaction before checking whether the
    /// invite remains usable or offers the selected eventItem.
    /// </summary>
    /// <param name="tokenHash">The token hash.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Invite?> LockByTokenHashForUpdateAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>
    /// Takes a row-level write lock on the attendee's current pending invite and loads its
    /// offered events. Callers hold the attendee lifecycle lock before calling this method.
    /// </summary>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Invite?> LockPendingForAttendeeAsync(Guid attendeeId, CancellationToken cancellationToken);

    /// <summary>Provides get pending for attendee async within this contract.</summary>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Invite?> GetPendingForAttendeeAsync(Guid attendeeId, CancellationToken cancellationToken);

    /// <summary>Locks the Attendee's pending initial Invite after the Attendee lifecycle lock.</summary>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Invite?> LockPendingInitialForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Takes row-level write locks on every pending Invite for the attendee, ordered by ID, and
    /// loads their offered events. Callers hold the attendee lifecycle lock before calling this
    /// method; recovery issuance reads the set authoritatively exactly once.
    /// </summary>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Invite>> LockPendingListForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken);

    /// <summary>Pending invites whose expiry has passed — the input to the sweep in Task 37.</summary>
    /// <param name="asAt">The as at.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Invite>> ListPendingExpiredAsync(DateTimeOffset asAt, CancellationToken cancellationToken);

    /// <summary>Provides add within this contract.</summary>
    /// <param name="invite">The invite.</param>
    void Add(Invite invite);
}
`````

## after — src/EventBooking.Application/Abstractions/IInviteRepository.cs — 1/1

<!-- retirement-file: {"id":1,"file":"src/EventBooking.Application/Abstractions/IInviteRepository.cs","beforeSha":"11cf32adcf3c2c881259190eb88fd172455196044ce6594fa2a9ecfea0a16be0","afterSha":"f38d7159bcfffde894829e1907961a321b2684041c23d1d2826b048d0af3ce0e","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Invites;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines iinvite repository for the current use case.</summary>
public interface IInviteRepository
{
    /// <summary>Provides get async within this contract.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Invite?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Takes a row-level write lock on the invite identified by its identifier and loads its
    /// offered events. Attendee deletion and expiry processing use it after the attendee lock.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Invite?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Takes a row-level write lock on the attendee's current pending invite and loads its
    /// offered events. Callers hold the attendee lifecycle lock before calling this method.
    /// </summary>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Invite?> LockPendingForAttendeeAsync(Guid attendeeId, CancellationToken cancellationToken);

    /// <summary>Provides get pending for attendee async within this contract.</summary>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Invite?> GetPendingForAttendeeAsync(Guid attendeeId, CancellationToken cancellationToken);

    /// <summary>Locks the Attendee's pending initial Invite after the Attendee lifecycle lock.</summary>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Invite?> LockPendingInitialForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Takes row-level write locks on every pending Invite for the attendee, ordered by ID, and
    /// loads their offered events. Callers hold the attendee lifecycle lock before calling this
    /// method; recovery issuance reads the set authoritatively exactly once.
    /// </summary>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Invite>> LockPendingListForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken);

    /// <summary>Pending invites whose expiry has passed — the input to the sweep in Task 37.</summary>
    /// <param name="asAt">The as at.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Invite>> ListPendingExpiredAsync(DateTimeOffset asAt, CancellationToken cancellationToken);

    /// <summary>Provides add within this contract.</summary>
    /// <param name="invite">The invite.</param>
    void Add(Invite invite);
}
`````

## before — src/EventBooking.Application/Abstractions/ITokenService.cs — 1/1

<!-- retirement-file: {"id":2,"file":"src/EventBooking.Application/Abstractions/ITokenService.cs","beforeSha":"40d9f9a07f10867ed08a454a007547d690acb989b6aa0afefd5dd2d6bd5c0adf","afterSha":"1e7401ae7fa60da88e36bb1bc7fe07000a32d9d5adc19617fa6fa3221f13ed3d","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Application.Abstractions;

/// <summary>The clear-text token goes in the email link; only its hash is ever stored.</summary>
/// <param name="Token">The token.</param>
/// <param name="TokenHash">The token hash.</param>
public sealed record IssuedToken(string Token, string TokenHash);

/// <summary>Defines itoken service for the current use case.</summary>
public interface ITokenService
{
    /// <summary>Issues a signed, single-use token bound to one invite or booking.</summary>
    /// <param name="entityId">The entity id.</param>
    IssuedToken Issue(Guid entityId);

    /// <summary>Verifies the signature and recovers the identifier. False if the token is malformed
    /// or the signature does not verify.</summary>
    /// <param name="token">The token.</param>
    /// <param name="entityId">The entity id.</param>
    bool TryRead(string? token, out Guid entityId);

    /// <summary>The hash to compare against the stored value once the signature has verified.</summary>
    /// <param name="token">The token.</param>
    string Hash(string token);
}
`````

## after — src/EventBooking.Application/Abstractions/ITokenService.cs — 1/1

<!-- retirement-file: {"id":2,"file":"src/EventBooking.Application/Abstractions/ITokenService.cs","beforeSha":"40d9f9a07f10867ed08a454a007547d690acb989b6aa0afefd5dd2d6bd5c0adf","afterSha":"1e7401ae7fa60da88e36bb1bc7fe07000a32d9d5adc19617fa6fa3221f13ed3d","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Application.Abstractions;

/// <summary>What an attendee link is for. Part of the signed payload, so a book token can never
/// be replayed as a manage token for the same identifier.</summary>
public enum TokenPurpose
{
    /// <summary>The link that opens an invite's options.</summary>
    Book = 1,

    /// <summary>The link that views, cancels or reschedules a booking.</summary>
    Manage = 2,
}

/// <summary>What a valid token names: the row to load and the version it must still be on.</summary>
/// <param name="Purpose">The purpose.</param>
/// <param name="EntityId">The invite or booking identifier.</param>
/// <param name="Version">The token version the link was issued against.</param>
public readonly record struct TokenReference(TokenPurpose Purpose, Guid EntityId, int Version);

/// <summary>
/// Deterministic attendee links (design 06). The token is reproducible from the row, so the
/// confirmation page and the confirmation email share one link and nothing derived from the token
/// is ever stored: the row keeps only the version counter that a link is revoked by incrementing.
/// </summary>
public interface ITokenService
{
    /// <summary>Issues the link for one purpose, identifier and version.</summary>
    /// <param name="purpose">The purpose.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="version">The current version counter on the row; one or more.</param>
    string Issue(TokenPurpose purpose, Guid entityId, int version);

    /// <summary>
    /// Verifies the signature in constant time and recovers what the token names, before any
    /// database access. False if the token is malformed or the signature does not verify.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="reference">What the token names, when it verifies.</param>
    bool TryRead(string? token, out TokenReference reference);
}
`````

## before — src/EventBooking.Application/Bookings/CancelBookingHandler.cs — 1/1

<!-- retirement-file: {"id":3,"file":"src/EventBooking.Application/Bookings/CancelBookingHandler.cs","beforeSha":"ff0d16eca2a24bd98ba115656a2776b2359f8eb23e904960b4f902a2416ace62","afterSha":"5c0282188759ae8acc349b0792cef26154b8801cd3b52aa7a007b48ccdf10d79","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Bookings;

/// <summary>Defines cancel booking command for the current use case.</summary>
/// <param name="ManageToken">The manage token.</param>
/// <param name="Rebook">The rebook.</param>
public sealed record CancelBookingCommand(string? ManageToken, bool Rebook);

/// <summary>Reports cancellation and any replacement-invite delivery outcome.</summary>
/// <param name="Reinvited">Whether a replacement invite was created.</param>
/// <param name="InviteCreated">The explicit replacement-invite creation state.</param>
/// <param name="DeliveryStatus">The provider outcome, or <c>Unavailable</c> when no invite exists.</param>
/// <param name="DeliveryId">The durable replacement delivery identifier, when one was staged.</param>
public sealed record CancelBookingOutcome(
    bool Reinvited,
    bool InviteCreated = false,
    string? DeliveryStatus = null,
    Guid? DeliveryId = null);

/// <summary>Cancels or rebooks under the attendee lifecycle lock before releasing event capacity.</summary>
/// <param name="deliveries">Stages and dispatches replacement invites after commit.</param>
/// <param name="bookings">The bookings.</param>
/// <param name="events">The events.</param>
/// <param name="attendees">The attendees.</param>
/// <param name="invites">The invites.</param>
/// <param name="bookingCanceller">The booking canceller.</param>
/// <param name="issuer">The issuer.</param>
/// <param name="tokens">The tokens.</param>
/// <param name="clock">The clock.</param>
/// <param name="unitOfWork">The unit of work.</param>
public sealed class CancelBookingHandler(
    IBookingRepository bookings,
    IEventRepository events,
    IAttendeeRepository attendees,
    IInviteRepository invites,
    BookingCanceller bookingCanceller,
    InviteIssuer issuer,
    EmailDeliveryService deliveries,
    ITokenService tokens,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    /// <summary>Cancels the managed booking and optionally issues a replacement invite atomically.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<CancelBookingOutcome>> HandleAsync(
        CancelBookingCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ManageToken is null || !tokens.TryRead(command.ManageToken, out _))
        {
            return Result<CancelBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        var eventId = await bookings.GetEventIdByManageTokenHashAsync(
            tokens.Hash(command.ManageToken),
            cancellationToken);
        var attendeeId = await bookings.GetAttendeeIdByManageTokenHashAsync(
            tokens.Hash(command.ManageToken),
            cancellationToken);
        if (eventId is null || attendeeId is null)
        {
            return Result<CancelBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var attendee = await attendees.LockForUpdateAsync(attendeeId.Value, cancellationToken);
        if (attendee is null)
        {
            return Result<CancelBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        var pending = await invites.LockPendingListForAttendeeAsync(attendee.Id, cancellationToken);

        var booking = await bookings.LockByManageTokenHashForUpdateAsync(
            tokens.Hash(command.ManageToken), cancellationToken);

        if (booking is null || booking.Status != BookingStatus.Active)
        {
            return Result<CancelBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        if (booking.AttendeeId != attendee.Id)
        {
            return Result<CancelBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        Booking? activeRecovery = null;
        if (booking.IsOriginal)
        {
            activeRecovery = await bookings.LockActiveRecoveryAsync(booking.Id, cancellationToken);
        }

        var eventIds = new List<Guid> { eventId.Value };
        if (activeRecovery is not null && activeRecovery.EventId != eventId.Value)
        {
            eventIds.Add(activeRecovery.EventId);
        }

        eventIds.Sort();
        var lockedEvents = new Dictionary<Guid, Event>();
        foreach (var lockedEventId in eventIds)
        {
            var locked = await events.LockForUpdateAsync(lockedEventId, cancellationToken);
            if (locked is null)
            {
                return Result<CancelBookingOutcome>.Failure(
                    Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
            }

            lockedEvents[lockedEventId] = locked;
        }

        var eventItem = lockedEvents[eventId.Value];

        // A booking can no longer be cancelled once its event date has started.
        if (lockedEvents.Values.Any(locked => locked.Window.Date < clock.TodayAtTransitionalLocation))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CancelBookingOutcome>.Failure(Error.Conflict(
                "This appointment has already taken place and can no longer be cancelled."));
        }

        var reinvited = false;
        InviteIssueResult? issued = null;

        try
        {
            if (!booking.IsOriginal)
            {
                var releasedRecovery = await bookingCanceller.CancelLockedAsync(
                    booking,
                    eventItem,
                    ActorType.AttendeeToken,
                    booking.Id.ToString(),
                    cancellationToken);
                if (releasedRecovery.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<CancelBookingOutcome>.Failure(releasedRecovery.Error);
                }
            }
            else
            {
                foreach (var pendingRecovery in pending.Where(invite => invite.RecoveryOfBookingId.HasValue && invite.Status == Domain.Invites.InviteStatus.Pending))
                {
                    pendingRecovery.MarkSuperseded();
                }

                if (activeRecovery is not null)
                {
                    var releasedActiveRecovery = await bookingCanceller.CancelLockedAsync(
                        activeRecovery,
                        lockedEvents[activeRecovery.EventId],
                        ActorType.AttendeeToken,
                        booking.Id.ToString(),
                        cancellationToken);
                    if (releasedActiveRecovery.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelBookingOutcome>.Failure(releasedActiveRecovery.Error);
                    }
                }

                var released = await bookingCanceller.CancelLockedAsync(
                    booking,
                    eventItem,
                    ActorType.AttendeeToken,
                    booking.Id.ToString(),
                    cancellationToken);
                if (released.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<CancelBookingOutcome>.Failure(released.Error);
                }

                attendee.ResetToNotYetInvited(clock.UtcNow);

                if (command.Rebook)
                {
                    var issueResult = await issuer.IssueInitialAsync(
                        attendee,
                        0,
                        ActorType.AttendeeToken,
                        booking.Id.ToString(),
                        isReinvite: false,
                        cancellationToken);
                    if (issueResult.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelBookingOutcome>.Failure(issueResult.Error);
                    }

                    issued = issueResult.Value;
                    reinvited = issued.Invited;
                }
            }
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CancelBookingOutcome>.Failure(Error.Conflict(ex.Message));
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        if (issued?.DispatchPlan is { } plan)
        {
            var status = await deliveries.DispatchClaimedAsync(
                plan.DeliveryId, plan.Message, cancellationToken, plan.OnSent);

            return Result<CancelBookingOutcome>.Success(
                new CancelBookingOutcome(
                    reinvited,
                    InviteCreated: reinvited,
                    status.ToString(),
                    plan.DeliveryId));
        }

        return Result<CancelBookingOutcome>.Success(
            new CancelBookingOutcome(reinvited, InviteCreated: reinvited, "Unavailable"));
    }
}
`````

## after — src/EventBooking.Application/Bookings/CancelBookingHandler.cs — 1/1

<!-- retirement-file: {"id":3,"file":"src/EventBooking.Application/Bookings/CancelBookingHandler.cs","beforeSha":"ff0d16eca2a24bd98ba115656a2776b2359f8eb23e904960b4f902a2416ace62","afterSha":"5c0282188759ae8acc349b0792cef26154b8801cd3b52aa7a007b48ccdf10d79","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Bookings;

/// <summary>Defines cancel booking command for the current use case.</summary>
/// <param name="ManageToken">The manage token.</param>
/// <param name="Rebook">The rebook.</param>
public sealed record CancelBookingCommand(string? ManageToken, bool Rebook);

/// <summary>Reports cancellation and any replacement-invite delivery outcome.</summary>
/// <param name="Reinvited">Whether a replacement invite was created.</param>
/// <param name="InviteCreated">The explicit replacement-invite creation state.</param>
/// <param name="DeliveryStatus">The provider outcome, or <c>Unavailable</c> when no invite exists.</param>
/// <param name="DeliveryId">The durable replacement delivery identifier, when one was staged.</param>
public sealed record CancelBookingOutcome(
    bool Reinvited,
    bool InviteCreated = false,
    string? DeliveryStatus = null,
    Guid? DeliveryId = null);

/// <summary>Cancels or rebooks under the attendee lifecycle lock before releasing event capacity.</summary>
/// <param name="deliveries">Stages and dispatches replacement invites after commit.</param>
/// <param name="bookings">The bookings.</param>
/// <param name="events">The events.</param>
/// <param name="attendees">The attendees.</param>
/// <param name="invites">The invites.</param>
/// <param name="bookingCanceller">The booking canceller.</param>
/// <param name="issuer">The issuer.</param>
/// <param name="tokens">The tokens.</param>
/// <param name="clock">The clock.</param>
/// <param name="unitOfWork">The unit of work.</param>
public sealed class CancelBookingHandler(
    IBookingRepository bookings,
    IEventRepository events,
    IAttendeeRepository attendees,
    IInviteRepository invites,
    BookingCanceller bookingCanceller,
    InviteIssuer issuer,
    EmailDeliveryService deliveries,
    ITokenService tokens,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    /// <summary>Cancels the managed booking and optionally issues a replacement invite atomically.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<CancelBookingOutcome>> HandleAsync(
        CancelBookingCommand command,
        CancellationToken cancellationToken)
    {
        if (!tokens.TryRead(command.ManageToken, out var link) || link.Purpose != TokenPurpose.Manage)
        {
            return Result<CancelBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        var eventId = await bookings.GetEventIdAsync(link.EntityId, cancellationToken);
        var attendeeId = await bookings.GetAttendeeIdAsync(link.EntityId, cancellationToken);
        if (eventId is null || attendeeId is null)
        {
            return Result<CancelBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var attendee = await attendees.LockForUpdateAsync(attendeeId.Value, cancellationToken);
        if (attendee is null)
        {
            return Result<CancelBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        var pending = await invites.LockPendingListForAttendeeAsync(attendee.Id, cancellationToken);

        var booking = await bookings.LockForUpdateAsync(link.EntityId, cancellationToken);

        if (booking is null
            || booking.ManageTokenVersion != link.Version
            || booking.Status != BookingStatus.Active)
        {
            return Result<CancelBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        if (booking.AttendeeId != attendee.Id)
        {
            return Result<CancelBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        Booking? activeRecovery = null;
        if (booking.IsOriginal)
        {
            activeRecovery = await bookings.LockActiveRecoveryAsync(booking.Id, cancellationToken);
        }

        var eventIds = new List<Guid> { eventId.Value };
        if (activeRecovery is not null && activeRecovery.EventId != eventId.Value)
        {
            eventIds.Add(activeRecovery.EventId);
        }

        eventIds.Sort();
        var lockedEvents = new Dictionary<Guid, Event>();
        foreach (var lockedEventId in eventIds)
        {
            var locked = await events.LockForUpdateAsync(lockedEventId, cancellationToken);
            if (locked is null)
            {
                return Result<CancelBookingOutcome>.Failure(
                    Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
            }

            lockedEvents[lockedEventId] = locked;
        }

        var eventItem = lockedEvents[eventId.Value];

        // A booking can no longer be cancelled once its event date has started.
        if (lockedEvents.Values.Any(locked => locked.Window.Date < clock.TodayAtTransitionalLocation))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CancelBookingOutcome>.Failure(Error.Conflict(
                "This appointment has already taken place and can no longer be cancelled."));
        }

        var reinvited = false;
        InviteIssueResult? issued = null;

        try
        {
            if (!booking.IsOriginal)
            {
                var releasedRecovery = await bookingCanceller.CancelLockedAsync(
                    booking,
                    eventItem,
                    ActorType.AttendeeToken,
                    booking.Id.ToString(),
                    cancellationToken);
                if (releasedRecovery.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<CancelBookingOutcome>.Failure(releasedRecovery.Error);
                }
            }
            else
            {
                foreach (var pendingRecovery in pending.Where(invite => invite.RecoveryOfBookingId.HasValue && invite.Status == Domain.Invites.InviteStatus.Pending))
                {
                    pendingRecovery.MarkSuperseded();
                }

                if (activeRecovery is not null)
                {
                    var releasedActiveRecovery = await bookingCanceller.CancelLockedAsync(
                        activeRecovery,
                        lockedEvents[activeRecovery.EventId],
                        ActorType.AttendeeToken,
                        booking.Id.ToString(),
                        cancellationToken);
                    if (releasedActiveRecovery.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelBookingOutcome>.Failure(releasedActiveRecovery.Error);
                    }
                }

                var released = await bookingCanceller.CancelLockedAsync(
                    booking,
                    eventItem,
                    ActorType.AttendeeToken,
                    booking.Id.ToString(),
                    cancellationToken);
                if (released.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<CancelBookingOutcome>.Failure(released.Error);
                }

                attendee.ResetToNotYetInvited(clock.UtcNow);

                if (command.Rebook)
                {
                    var issueResult = await issuer.IssueInitialAsync(
                        attendee,
                        0,
                        ActorType.AttendeeToken,
                        booking.Id.ToString(),
                        isReinvite: false,
                        cancellationToken);
                    if (issueResult.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelBookingOutcome>.Failure(issueResult.Error);
                    }

                    issued = issueResult.Value;
                    reinvited = issued.Invited;
                }
            }
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CancelBookingOutcome>.Failure(Error.Conflict(ex.Message));
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        if (issued?.DispatchPlan is { } plan)
        {
            var status = await deliveries.DispatchClaimedAsync(
                plan.DeliveryId, plan.Message, cancellationToken, plan.OnSent);

            return Result<CancelBookingOutcome>.Success(
                new CancelBookingOutcome(
                    reinvited,
                    InviteCreated: reinvited,
                    status.ToString(),
                    plan.DeliveryId));
        }

        return Result<CancelBookingOutcome>.Success(
            new CancelBookingOutcome(reinvited, InviteCreated: reinvited, "Unavailable"));
    }
}
`````

## before — src/EventBooking.Application/Bookings/ConfirmBookingHandler.cs — 1/1

<!-- retirement-file: {"id":4,"file":"src/EventBooking.Application/Bookings/ConfirmBookingHandler.cs","beforeSha":"6c33aaff72c5eaa4682db83316924bc5180311ba5cb7f714ce0fe829b422ce85","afterSha":"7d16a8cdfb3e49541150e7b4b4abc920c5af5483c261f10358dcb0cd45a119e9","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Common;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Bookings;

/// <summary>Defines confirm booking command for the current use case.</summary>
/// <param name="Token">The token.</param>
/// <param name="EventId">The event id.</param>
public sealed record ConfirmBookingCommand(string? Token, Guid EventId);

/// <summary>Returns the durable booking link and actual confirmation-email outcome.</summary>
/// <param name="BookingId">The newly created active booking identifier.</param>
/// <param name="Date">The event's transitional-location date.</param>
/// <param name="StartTime">The event's start time.</param>
/// <param name="EndTime">The derived four-hour end time.</param>
/// <param name="ManageToken">The raw management token returned once to the attendee.</param>
/// <param name="DeliveryStatus">The post-commit provider outcome.</param>
/// <param name="DeliveryId">The durable confirmation-delivery identifier.</param>
public sealed record ConfirmBookingOutcome(
    Guid BookingId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string ManageToken,
    string DeliveryStatus = "Pending",
    Guid? DeliveryId = null);

/// <summary>
/// Confirms one offered event while serializing the attendee lifecycle and capacity rows,
/// atomically creating one operational appointment per attendee requirement.
/// </summary>
/// <param name="bookings">Persists the new booking row.</param>
/// <param name="appointments">Snapshots one operational appointment per attendee requirement.</param>
/// <param name="deliveries">Stages and dispatches the post-commit confirmation email.</param>
/// <param name="invites">The invites.</param>
/// <param name="attendees">The attendees.</param>
/// <param name="events">The events.</param>
/// <param name="capacities">The capacities.</param>
/// <param name="eventFinder">The event finder.</param>
/// <param name="tokens">The tokens.</param>
/// <param name="audit">The audit.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="clock">The clock.</param>
/// <param name="portal">The portal.</param>
public sealed class ConfirmBookingHandler(
    IInviteRepository invites,
    IAttendeeRepository attendees,
    IEventRepository events,
    IBookingRepository bookings,
    IBookingAppointmentRepository appointments,
    IEventCapacityRepository capacities,
    EligibleEventFinder eventFinder,
    ITokenService tokens,
    EmailDeliveryService deliveries,
    IAuditLogger audit,
    IUnitOfWork unitOfWork,
    IClock clock,
    AttendeePortalOptions portal)
{
    private const string FilledUpMessage =
        "That time filled up while you were choosing. Please pick from the updated options.";

    /// <summary>
    /// Confirms a attendee's offered future event while holding the eventItem, invite and capacity locks,
    /// snapshotting one Expected operational appointment per attendee requirement in the same save.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<ConfirmBookingOutcome>> HandleAsync(
        ConfirmBookingCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Token is null || !tokens.TryRead(command.Token, out _))
        {
            return Result<ConfirmBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        // This pre-read locates only the attendee row that defines the lock order. Invite state
        // is re-read under lock below and this value must not be used as authority.
        var preflightInvite = await invites.GetByTokenHashAsync(tokens.Hash(command.Token), cancellationToken);
        if (preflightInvite is null)
        {
            return Result<ConfirmBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        // Lock order for every attendee lifecycle transition is Attendee -> Invite -> Booking
        // -> Event -> EventCapacity. The attendee lock also serializes disjoint, legacy
        // tokens that could otherwise book different events at the same time.
        var attendee = await attendees.LockForUpdateAsync(preflightInvite.AttendeeId, cancellationToken);
        if (attendee is null)
        {
            return Result<ConfirmBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        var invite = await invites.LockByTokenHashForUpdateAsync(
            tokens.Hash(command.Token),
            cancellationToken);
        if (invite is null || !invite.IsUsableAt(clock.UtcNow))
        {
            return Result<ConfirmBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        if (invite.AttendeeId != attendee.Id)
        {
            return Result<ConfirmBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        var isRecovery = invite.RecoveryOfBookingId.HasValue;
        IReadOnlyList<Guid> required;
        Booking? original = null;

        if (!isRecovery)
        {
            var existingBooking = await bookings.LockActiveForAttendeeAsync(attendee.Id, cancellationToken);
            if (existingBooking is not null)
            {
                return Result<ConfirmBookingOutcome>.Failure(Error.Conflict("This attendee is already booked."));
            }

            if (!attendee.RequiredAppointmentTypeIds
                .Order()
                .SequenceEqual(invite.RequiredAppointmentTypeIds.Order()))
            {
                invite.MarkSuperseded();
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return Result<ConfirmBookingOutcome>.Failure(
                    Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
            }

            required = invite.RequiredAppointmentTypeIds;
        }
        else
        {
            original = await bookings.LockActiveOriginalForAttendeeAsync(attendee.Id, cancellationToken);
            var activeRecovery = original is null
                ? null
                : await bookings.LockActiveRecoveryAsync(original.Id, cancellationToken);
            if (original is null
                || original.Id != invite.RecoveryOfBookingId
                || activeRecovery is not null)
            {
                return await StaleRecoveryAsync(invite, transaction, cancellationToken);
            }

            var journey = await bookings.ListJourneyAsync(original.Id, cancellationToken);
            var rows = await appointments.ListForBookingsAsync(
                journey.Select(entry => entry.Id).ToList(),
                cancellationToken);
            var validated = new RecoveryConfirmationValidator().Validate(
                invite,
                attendee.RequiredAppointmentTypeIds,
                RecoveryConfirmationValidator.BuildAttempts(journey, rows),
                []);
            if (validated.IsFailure)
            {
                return await StaleRecoveryAsync(invite, transaction, cancellationToken);
            }

            required = validated.Value;
        }

        if (!invite.Offers(command.EventId))
        {
            return Result<ConfirmBookingOutcome>.Failure(
                Error.Conflict("That time is not one of your options."));
        }

        var eventItem = await events.LockForUpdateAsync(command.EventId, cancellationToken);
        if (eventItem is null)
        {
            return Result<ConfirmBookingOutcome>.Failure(
                Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        var actorId = invite.Id.ToString();

        var locked = await capacities.LockForUpdateAsync(
            command.EventId,
            required,
            cancellationToken);

        var stillAvailable =
            eventItem.Status == EventStatus.Active
            && eventItem.Window.StartsAfter(clock.TodayAtTransitionalLocation)
            && locked.Count == required.Count
            && locked.All(c => c.HasSpare);

        if (!stillAvailable)
        {
            await DropAndReplaceOptionAsync(invite, attendee, required, eventItem.Id, actorId, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<ConfirmBookingOutcome>.Failure(Error.Conflict(FilledUpMessage));
        }

        var bookingId = Guid.NewGuid();
        var manageToken = tokens.Issue(bookingId);

        Booking booking;
        try
        {
            booking = isRecovery
                ? Booking.CreateRecovery(
                    bookingId, invite, original!, eventItem.Id, manageToken.TokenHash, clock.UtcNow)
                : Booking.Create(bookingId, invite, eventItem.Id, manageToken.TokenHash, clock.UtcNow);

            foreach (var capacity in locked)
            {
                capacity.Decrement();

                audit.Record(
                    AuditEntityTypes.Event,
                    eventItem.Id,
                    AuditAction.CapacityDecremented,
                    ActorType.AttendeeToken,
                    actorId,
                    $"{capacity.AppointmentTypeId} now {capacity.RemainingCapacity}");
            }

            invite.MarkUsed();
            if (!isRecovery)
            {
                attendee.MarkBooked(clock.UtcNow);
            }
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<ConfirmBookingOutcome>.Failure(Error.Conflict(ex.Message));
        }

        bookings.Add(booking);

        foreach (var appointmentTypeId in required)
        {
            appointments.Add(BookingAppointment.Create(
                Guid.NewGuid(), booking.Id, appointmentTypeId));
        }

        audit.Record(
            AuditEntityTypes.Booking,
            bookingId,
            isRecovery ? AuditAction.RecoveryBookingCreated : AuditAction.BookingCreated,
            ActorType.AttendeeToken,
            actorId,
            isRecovery ? $"root {original!.Id} {eventItem.Window}" : eventItem.Window.ToString());

        var delivery = deliveries.StagePending(
            attendee.Id,
            EmailTemplate.BookingConfirmation,
            bookingId: bookingId);
        deliveries.ClaimForDispatch(delivery);
        var message = AttendeeEmailComposer.BookingConfirmation(
            attendee, required, eventItem, $"{portal.BaseUrl}/manage/{manageToken.Token}", portal);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<ConfirmBookingOutcome>.Failure(Error.Conflict("This attendee is already booked."));
        }

        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        // The provider call is deliberately after the commit: a mail failure must not undo a good booking.
        var deliveryStatus = await deliveries.DispatchClaimedAsync(delivery.Id, message, cancellationToken);

        return Result<ConfirmBookingOutcome>.Success(
            new ConfirmBookingOutcome(
                bookingId,
                eventItem.Window.Date,
                eventItem.Window.StartTime,
                eventItem.Window.EndTime,
                manageToken.Token,
                deliveryStatus.ToString(),
                delivery.Id));
    }

    /// <summary>
    /// Supersedes a recovery Invite whose snapshot no longer matches locked journey state,
    /// keeping the attendee-facing invalid-link response free of internal eligibility detail.
    /// </summary>
    private async Task<Result<ConfirmBookingOutcome>> StaleRecoveryAsync(
        Domain.Invites.Invite invite,
        ITransactionScope transaction,
        CancellationToken cancellationToken)
    {
        invite.MarkSuperseded();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<ConfirmBookingOutcome>.Failure(
            Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
    }

    /// <summary>
    /// Drops an option that filled up, replacing it when capacity exists elsewhere. When no
    /// replacement exists and the invite is left short, flags the attendee for coordinator
    /// follow-up (Issue #242) instead of leaving them with a silently shrinking choice.
    /// </summary>
    private async Task DropAndReplaceOptionAsync(
        Domain.Invites.Invite invite,
        Attendee attendee,
        IReadOnlyList<Guid> requiredAppointmentTypeIds,
        Guid lostEventId,
        string actorId,
        CancellationToken cancellationToken)
    {
        invite.RemoveOption(lostEventId);

        var replacement = await eventFinder.FindAsync(
            requiredAppointmentTypeIds,
            1,
            invite.OfferedEventIds.Append(lostEventId).ToList(),
            cancellationToken);

        if (replacement.Count == 1)
        {
            invite.AddOption(replacement[0].Id);

            audit.Record(
                AuditEntityTypes.Invite,
                invite.Id,
                AuditAction.InviteOptionReplaced,
                ActorType.AttendeeToken,
                actorId,
                $"{lostEventId} replaced by {replacement[0].Id}");

            return;
        }

        audit.Record(
            AuditEntityTypes.Invite,
            invite.Id,
            AuditAction.InviteOptionReplaced,
            ActorType.AttendeeToken,
            actorId,
            $"{lostEventId} dropped, no replacement available");

        if (invite.OfferedEventIds.Count < Domain.Invites.Invite.RequiredOptionCount
            && attendee.Status == AttendeeStatus.Invited)
        {
            attendee.MarkNoResponse(clock.UtcNow);

            audit.Record(
                AuditEntityTypes.Invite,
                invite.Id,
                AuditAction.InviteOptionReplaced,
                ActorType.AttendeeToken,
                actorId,
                $"only {invite.OfferedEventIds.Count} live option(s) remain, attendee flagged for follow-up");
        }
    }
}
`````
