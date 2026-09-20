# 00b — Vocabulary edits 7 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — src/EventBooking.Application/Abstractions/IBookingRepository.cs — 1/1

<!-- vocabulary-file: {"id":29,"oldPath":"src/EventBooking.Application/Abstractions/IBookingRepository.cs","newPath":"src/EventBooking.Application/Abstractions/IBookingRepository.cs","beforeSha":"434acb2ea383249ec666af0267531b855b8ac7d704680b028e616cd16d34365b","afterSha":"d3a7422d0070ddc9f859a8f0914d50986177a5f778a38680b5dd2a9c8a3077f6","side":"before","part":1,"parts":1} -->

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
`````

## after — src/EventBooking.Application/Abstractions/IBookingRepository.cs — 1/1

<!-- vocabulary-file: {"id":29,"oldPath":"src/EventBooking.Application/Abstractions/IBookingRepository.cs","newPath":"src/EventBooking.Application/Abstractions/IBookingRepository.cs","beforeSha":"434acb2ea383249ec666af0267531b855b8ac7d704680b028e616cd16d34365b","afterSha":"d3a7422d0070ddc9f859a8f0914d50986177a5f778a38680b5dd2a9c8a3077f6","side":"after","part":1,"parts":1} -->

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

## before — src/EventBooking.Application/Abstractions/ICandidateBookingQueries.cs — 1/1

<!-- vocabulary-file: {"id":30,"oldPath":"src/EventBooking.Application/Abstractions/ICandidateBookingQueries.cs","newPath":"src/EventBooking.Application/Abstractions/IAttendeeBookingQueries.cs","beforeSha":"bb4df8e0eba1bfa3f788be27b826066597979da48323b45766958ba9e5aae9c6","afterSha":"337373e14ab382ae412793cebc94d136ce2aef96a9783b1e1e7700a415039c26","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Candidates;

namespace EventBooking.Application.Abstractions;

/// <summary>Loads a candidate's active bookings for the staff cancellation workflow.</summary>
public interface ICandidateBookingQueries
{
    /// <summary>Lists active bookings with slot windows, or null when the candidate does not exist.</summary>
    /// <param name="candidateId">The candidate identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Active booking summaries, or null for an unknown candidate.</returns>
    Task<IReadOnlyList<CandidateBookingSummary>?> ListActiveForCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken);
}
`````

## after — src/EventBooking.Application/Abstractions/IAttendeeBookingQueries.cs — 1/1

<!-- vocabulary-file: {"id":30,"oldPath":"src/EventBooking.Application/Abstractions/ICandidateBookingQueries.cs","newPath":"src/EventBooking.Application/Abstractions/IAttendeeBookingQueries.cs","beforeSha":"bb4df8e0eba1bfa3f788be27b826066597979da48323b45766958ba9e5aae9c6","afterSha":"337373e14ab382ae412793cebc94d136ce2aef96a9783b1e1e7700a415039c26","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Attendees;

namespace EventBooking.Application.Abstractions;

/// <summary>Loads a attendee's active bookings for the staff cancellation workflow.</summary>
public interface IAttendeeBookingQueries
{
    /// <summary>Lists active bookings with event windows, or null when the attendee does not exist.</summary>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Active booking summaries, or null for an unknown attendee.</returns>
    Task<IReadOnlyList<AttendeeBookingSummary>?> ListActiveForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken);
}
`````

## before — src/EventBooking.Application/Abstractions/ICandidateReadinessQueries.cs — 1/1

<!-- vocabulary-file: {"id":31,"oldPath":"src/EventBooking.Application/Abstractions/ICandidateReadinessQueries.cs","newPath":"src/EventBooking.Application/Abstractions/IAttendeeReadinessQueries.cs","beforeSha":"39acf50ea14356a92d538f7c462d3230ed177370bce5a0cadfe2471b50f429ca","afterSha":"fbfb22170b7af44f61fd1cb8e45707d3e689cc27cc43550b55005dc7d291c5cd","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Candidates;

namespace EventBooking.Application.Abstractions;

/// <summary>Loads the readiness journey projection for one candidate.</summary>
public interface ICandidateReadinessQueries
{
    /// <summary>Loads the full original/recovery journey, or null when the Candidate does not exist.</summary>
    /// <param name="candidateId">The candidate identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The snapshot, or null for an unknown candidate.</returns>
    Task<CandidateReadinessSnapshot?> GetSnapshotAsync(
        Guid candidateId,
        CancellationToken cancellationToken);
}
`````

## after — src/EventBooking.Application/Abstractions/IAttendeeReadinessQueries.cs — 1/1

<!-- vocabulary-file: {"id":31,"oldPath":"src/EventBooking.Application/Abstractions/ICandidateReadinessQueries.cs","newPath":"src/EventBooking.Application/Abstractions/IAttendeeReadinessQueries.cs","beforeSha":"39acf50ea14356a92d538f7c462d3230ed177370bce5a0cadfe2471b50f429ca","afterSha":"fbfb22170b7af44f61fd1cb8e45707d3e689cc27cc43550b55005dc7d291c5cd","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Attendees;

namespace EventBooking.Application.Abstractions;

/// <summary>Loads the readiness journey projection for one attendee.</summary>
public interface IAttendeeReadinessQueries
{
    /// <summary>Loads the full original/recovery journey, or null when the Attendee does not exist.</summary>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The snapshot, or null for an unknown attendee.</returns>
    Task<AttendeeReadinessSnapshot?> GetSnapshotAsync(
        Guid attendeeId,
        CancellationToken cancellationToken);
}
`````

## before — src/EventBooking.Application/Abstractions/ICandidateRepository.cs — 1/1

<!-- vocabulary-file: {"id":32,"oldPath":"src/EventBooking.Application/Abstractions/ICandidateRepository.cs","newPath":"src/EventBooking.Application/Abstractions/IAttendeeRepository.cs","beforeSha":"973b322787541add87719aa702d666bbcbcac243d8075f789a76c6269dec5cac","afterSha":"48a9158154d33fef7e1da1a2290c07cd6df65eab94e1a561a8abab65a58b7218","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Candidates;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines icandidate repository for the current use case.</summary>
public interface ICandidateRepository
{
    /// <summary>Provides get async within this contract.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Candidate?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Takes the candidate lifecycle write lock and loads the candidate's requirements. Invite
    /// issuance, booking confirmation, cancellation/rebooking, and deletion take this lock first
    /// inside their transactions so one candidate cannot transition through two lifecycles at once.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Candidate?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Provides get by email async within this contract.</summary>
    /// <param name="email">The email.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Candidate?> GetByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>All candidates, or only those in one status when a status is supplied.</summary>
    /// <param name="status">The status.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Candidate>> ListAsync(CandidateStatus? status, CancellationToken cancellationToken);

    /// <summary>Provides add within this contract.</summary>
    /// <param name="candidate">The candidate.</param>
    void Add(Candidate candidate);

    /// <summary>Provides remove within this contract.</summary>
    /// <param name="candidate">The candidate.</param>
    void Remove(Candidate candidate);
}
`````

## after — src/EventBooking.Application/Abstractions/IAttendeeRepository.cs — 1/1

<!-- vocabulary-file: {"id":32,"oldPath":"src/EventBooking.Application/Abstractions/ICandidateRepository.cs","newPath":"src/EventBooking.Application/Abstractions/IAttendeeRepository.cs","beforeSha":"973b322787541add87719aa702d666bbcbcac243d8075f789a76c6269dec5cac","afterSha":"48a9158154d33fef7e1da1a2290c07cd6df65eab94e1a561a8abab65a58b7218","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Attendees;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines iattendee repository for the current use case.</summary>
public interface IAttendeeRepository
{
    /// <summary>Provides get async within this contract.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Attendee?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Takes the attendee lifecycle write lock and loads the attendee's requirements. Invite
    /// issuance, booking confirmation, cancellation/rebooking, and deletion take this lock first
    /// inside their transactions so one attendee cannot transition through two lifecycles at once.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Attendee?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Provides get by email async within this contract.</summary>
    /// <param name="email">The email.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Attendee?> GetByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>All attendees, or only those in one status when a status is supplied.</summary>
    /// <param name="status">The status.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Attendee>> ListAsync(AttendeeStatus? status, CancellationToken cancellationToken);

    /// <summary>Provides add within this contract.</summary>
    /// <param name="attendee">The attendee.</param>
    void Add(Attendee attendee);

    /// <summary>Provides remove within this contract.</summary>
    /// <param name="attendee">The attendee.</param>
    void Remove(Attendee attendee);
}
`````

## before — src/EventBooking.Application/Abstractions/IClock.cs — 1/1

<!-- vocabulary-file: {"id":33,"oldPath":"src/EventBooking.Application/Abstractions/IClock.cs","newPath":"src/EventBooking.Application/Abstractions/IClock.cs","beforeSha":"05f28a5282d61a1bdfda421bc3eb148fdf18551c0aa8f673cceadd0f4d99484c","afterSha":"2445c064cbe9ca917c8000abaf5fb3186172b142d6423762fb8b4a94a215785c","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Application.Abstractions;

/// <summary>
/// The only source of "now" in the system. Production code never reads the ambient clock directly:
/// invite expiry, retry sweeps and slot eligibility all have to be steerable from a test.
/// </summary>
public interface IClock
{
    /// <summary>Provides utc now within this contract.</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>Gets the current instant converted to the configured head-office time zone.</summary>
    DateTimeOffset NowAtHeadOffice { get; }

    /// <summary>Today's date at head office. The system is single-site by design.</summary>
    DateOnly TodayAtHeadOffice { get; }

    /// <summary>Converts an instant to the calendar date at head office.</summary>
    /// <param name="instant">The instant.</param>
    DateOnly DateAtHeadOffice(DateTimeOffset instant);

    /// <summary>Converts the given instant to head-office local time, preserving time of day.</summary>
    /// <param name="instant">The instant to convert.</param>
    /// <returns>The same instant expressed with the head-office time-zone offset.</returns>
    DateTimeOffset InstantAtHeadOffice(DateTimeOffset instant);
}
`````

## after — src/EventBooking.Application/Abstractions/IClock.cs — 1/1

<!-- vocabulary-file: {"id":33,"oldPath":"src/EventBooking.Application/Abstractions/IClock.cs","newPath":"src/EventBooking.Application/Abstractions/IClock.cs","beforeSha":"05f28a5282d61a1bdfda421bc3eb148fdf18551c0aa8f673cceadd0f4d99484c","afterSha":"2445c064cbe9ca917c8000abaf5fb3186172b142d6423762fb8b4a94a215785c","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Application.Abstractions;

/// <summary>
/// The only source of "now" in the system. Production code never reads the ambient clock directly:
/// invite expiry, retry sweeps and event eligibility all have to be steerable from a test.
/// </summary>
public interface IClock
{
    /// <summary>Provides utc now within this contract.</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>Gets the current instant converted to the configured transitional-location time zone.</summary>
    DateTimeOffset NowAtTransitionalLocation { get; }

    /// <summary>Today's date at transitional location. The system is single-site by design.</summary>
    DateOnly TodayAtTransitionalLocation { get; }

    /// <summary>Converts an instant to the calendar date at transitional location.</summary>
    /// <param name="instant">The instant.</param>
    DateOnly DateAtTransitionalLocation(DateTimeOffset instant);

    /// <summary>Converts the given instant to transitional-location local time, preserving time of day.</summary>
    /// <param name="instant">The instant to convert.</param>
    /// <returns>The same instant expressed with the transitional-location time-zone offset.</returns>
    DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant);
}
`````

## before — src/EventBooking.Application/Abstractions/IConfirmedSlotRepository.cs — 1/1

<!-- vocabulary-file: {"id":34,"oldPath":"src/EventBooking.Application/Abstractions/IConfirmedSlotRepository.cs","newPath":"src/EventBooking.Application/Abstractions/IEventRepository.cs","beforeSha":"58aac80bfee7e20b205608c05e0074111fff87fc2c87fe5e36d313b465249df4","afterSha":"4e85b0c9f66d351f57887e816d095bb8b718159bfc1d595ebd8274b19573ef3a","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines iconfirmed slot repository for the current use case.</summary>
public interface IConfirmedSlotRepository
{
    /// <summary>Provides get async within this contract.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<ConfirmedSlot?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Takes the transactional write guard for a confirmed slot and returns its current state.
    /// Confirmation and every cancellation path that can change bookings on the slot must take
    /// this guard before reading booking or capacity state for that slot.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<ConfirmedSlot?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Active slots whose window falls on or after the given date, capacities loaded.</summary>
    /// <param name="onOrAfter">The on or after.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<ConfirmedSlot>> ListActiveAsync(DateOnly onOrAfter, CancellationToken cancellationToken);

    /// <summary>Every slot, cancelled ones included — for the coordinator's slots overview.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<ConfirmedSlot>> ListAllAsync(CancellationToken cancellationToken);

    /// <summary>Provides add within this contract.</summary>
    /// <param name="slot">The slot.</param>
    void Add(ConfirmedSlot slot);
}
`````

## after — src/EventBooking.Application/Abstractions/IEventRepository.cs — 1/1

<!-- vocabulary-file: {"id":34,"oldPath":"src/EventBooking.Application/Abstractions/IConfirmedSlotRepository.cs","newPath":"src/EventBooking.Application/Abstractions/IEventRepository.cs","beforeSha":"58aac80bfee7e20b205608c05e0074111fff87fc2c87fe5e36d313b465249df4","afterSha":"4e85b0c9f66d351f57887e816d095bb8b718159bfc1d595ebd8274b19573ef3a","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Events;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines ievent repository for the current use case.</summary>
public interface IEventRepository
{
    /// <summary>Provides get async within this contract.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Event?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Takes the transactional write guard for a event and returns its current state.
    /// Confirmation and every cancellation path that can change bookings on the event must take
    /// this guard before reading booking or capacity state for that eventItem.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Event?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Active events whose window falls on or after the given date, capacities loaded.</summary>
    /// <param name="onOrAfter">The on or after.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Event>> ListActiveAsync(DateOnly onOrAfter, CancellationToken cancellationToken);

    /// <summary>Every eventItem, cancelled ones included — for the coordinator's events overview.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Event>> ListAllAsync(CancellationToken cancellationToken);

    /// <summary>Provides add within this contract.</summary>
    /// <param name="eventItem">The eventItem.</param>
    void Add(Event eventItem);
}
`````

## before — src/EventBooking.Application/Abstractions/IDashboardQueries.cs — 1/1

<!-- vocabulary-file: {"id":35,"oldPath":"src/EventBooking.Application/Abstractions/IDashboardQueries.cs","newPath":"src/EventBooking.Application/Abstractions/IDashboardQueries.cs","beforeSha":"6a88d14fa816341cb5494270549ef458ead1c5b3df559ede30f0702dbb5989af","afterSha":"080d27f578714e8d608265825811f6b9e918dffce70d42f581f030d5f3dd7a54","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Abstractions;

/// <summary>The latest delivery projection for one candidate, including whether its context remains retryable.</summary>
/// <param name="CandidateId">The candidate whose latest delivery is projected.</param>
/// <param name="TemplateName">The server-owned template of the delivery.</param>
/// <param name="SentAt">The latest attempt or pending timestamp.</param>
/// <param name="Status">The durable delivery status.</param>
/// <param name="CanRetry">Whether current domain state still permits this delivery to be regenerated.</param>
public sealed record CandidateEmailStatusRow(
    Guid CandidateId,
    EmailTemplate TemplateName,
    DateTimeOffset SentAt,
    EmailStatus Status,
    bool CanRetry);

/// <summary>Defines awaiting availability row for the current use case.</summary>
/// <param name="CandidateId">The candidate id.</param>
/// <param name="Name">The name.</param>
/// <param name="Email">The email.</param>
/// <param name="RequiredCodes">The required codes.</param>
/// <param name="WaitingSince">The waiting since.</param>
/// <param name="DaysWaiting">The days waiting.</param>
public sealed record AwaitingAvailabilityRow(
    Guid CandidateId,
    string Name,
    string Email,
    IReadOnlyList<string> RequiredCodes,
    DateOnly WaitingSince,
    int DaysWaiting);

/// <summary>Defines no response row for the current use case.</summary>
/// <param name="CandidateId">The candidate id.</param>
/// <param name="Name">The name.</param>
/// <param name="Email">The email.</param>
/// <param name="RequiredCodes">The required codes.</param>
/// <param name="GaveUpOn">The gave up on.</param>
public sealed record NoResponseRow(
    Guid CandidateId,
    string Name,
    string Email,
    IReadOnlyList<string> RequiredCodes,
    DateOnly GaveUpOn);

/// <summary>Defines slot capacity row for the current use case.</summary>
/// <param name="Code">The code.</param>
/// <param name="TotalHeadcount">The total headcount.</param>
/// <param name="RemainingCapacity">The remaining capacity.</param>
public sealed record SlotCapacityRow(string Code, int TotalHeadcount, int RemainingCapacity);

/// <summary>Defines slot overview row for the current use case.</summary>
/// <param name="ConfirmedSlotId">The confirmed slot id.</param>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
/// <param name="EndTime">The end time.</param>
/// <param name="Capacities">The capacities.</param>
/// <param name="ActiveBookings">The active bookings.</param>
public sealed record SlotOverviewRow(
    Guid ConfirmedSlotId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyList<SlotCapacityRow> Capacities,
    int ActiveBookings);

/// <summary>
/// The dashboard read side. Implementations query and project directly, returning no entities and
/// offering no writes.
/// </summary>
public interface IDashboardQueries
{
    /// <summary>Provides awaiting availability async within this contract.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<AwaitingAvailabilityRow>> AwaitingAvailabilityAsync(
        CancellationToken cancellationToken);

    /// <summary>Provides no response async within this contract.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<NoResponseRow>> NoResponseAsync(CancellationToken cancellationToken);

    /// <summary>Provides slots overview async within this contract.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<SlotOverviewRow>> SlotsOverviewAsync(CancellationToken cancellationToken);

    /// <summary>The latest EmailLog row per candidate that has ever had one written.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<CandidateEmailStatusRow>> LatestEmailStatusAsync(CancellationToken cancellationToken);
}
`````

## after — src/EventBooking.Application/Abstractions/IDashboardQueries.cs — 1/1

<!-- vocabulary-file: {"id":35,"oldPath":"src/EventBooking.Application/Abstractions/IDashboardQueries.cs","newPath":"src/EventBooking.Application/Abstractions/IDashboardQueries.cs","beforeSha":"6a88d14fa816341cb5494270549ef458ead1c5b3df559ede30f0702dbb5989af","afterSha":"080d27f578714e8d608265825811f6b9e918dffce70d42f581f030d5f3dd7a54","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Abstractions;

/// <summary>The latest delivery projection for one attendee, including whether its context remains retryable.</summary>
/// <param name="AttendeeId">The attendee whose latest delivery is projected.</param>
/// <param name="TemplateName">The server-owned template of the delivery.</param>
/// <param name="SentAt">The latest attempt or pending timestamp.</param>
/// <param name="Status">The durable delivery status.</param>
/// <param name="CanRetry">Whether current domain state still permits this delivery to be regenerated.</param>
public sealed record AttendeeEmailStatusRow(
    Guid AttendeeId,
    EmailTemplate TemplateName,
    DateTimeOffset SentAt,
    EmailStatus Status,
    bool CanRetry);

/// <summary>Defines awaiting availability row for the current use case.</summary>
/// <param name="AttendeeId">The attendee id.</param>
/// <param name="Name">The name.</param>
/// <param name="Email">The email.</param>
/// <param name="RequiredCodes">The required codes.</param>
/// <param name="WaitingSince">The waiting since.</param>
/// <param name="DaysWaiting">The days waiting.</param>
public sealed record AwaitingAvailabilityRow(
    Guid AttendeeId,
    string Name,
    string Email,
    IReadOnlyList<string> RequiredCodes,
    DateOnly WaitingSince,
    int DaysWaiting);

/// <summary>Defines no response row for the current use case.</summary>
/// <param name="AttendeeId">The attendee id.</param>
/// <param name="Name">The name.</param>
/// <param name="Email">The email.</param>
/// <param name="RequiredCodes">The required codes.</param>
/// <param name="GaveUpOn">The gave up on.</param>
public sealed record NoResponseRow(
    Guid AttendeeId,
    string Name,
    string Email,
    IReadOnlyList<string> RequiredCodes,
    DateOnly GaveUpOn);

/// <summary>Defines event capacity row for the current use case.</summary>
/// <param name="Code">The code.</param>
/// <param name="TotalHeadcount">The total headcount.</param>
/// <param name="RemainingCapacity">The remaining capacity.</param>
public sealed record EventCapacityRow(string Code, int TotalHeadcount, int RemainingCapacity);

/// <summary>Defines event overview row for the current use case.</summary>
/// <param name="EventId">The event id.</param>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
/// <param name="EndTime">The end time.</param>
/// <param name="Capacities">The capacities.</param>
/// <param name="ActiveBookings">The active bookings.</param>
public sealed record EventOverviewRow(
    Guid EventId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyList<EventCapacityRow> Capacities,
    int ActiveBookings);

/// <summary>
/// The dashboard read side. Implementations query and project directly, returning no entities and
/// offering no writes.
/// </summary>
public interface IDashboardQueries
{
    /// <summary>Provides awaiting availability async within this contract.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<AwaitingAvailabilityRow>> AwaitingAvailabilityAsync(
        CancellationToken cancellationToken);

    /// <summary>Provides no response async within this contract.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<NoResponseRow>> NoResponseAsync(CancellationToken cancellationToken);

    /// <summary>Provides events overview async within this contract.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<EventOverviewRow>> EventsOverviewAsync(CancellationToken cancellationToken);

    /// <summary>The latest EmailLog row per attendee that has ever had one written.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<AttendeeEmailStatusRow>> LatestEmailStatusAsync(CancellationToken cancellationToken);
}
`````

## before — src/EventBooking.Application/Abstractions/IEmailDeliveryRepository.cs — 1/1

<!-- vocabulary-file: {"id":36,"oldPath":"src/EventBooking.Application/Abstractions/IEmailDeliveryRepository.cs","newPath":"src/EventBooking.Application/Abstractions/IEmailDeliveryRepository.cs","beforeSha":"3e62032d1d7e7b509a65cfd4f3e576e7f83510f644b9326a8a6f12265a7b79ec","afterSha":"11240a83e2b416635e1395e1f1a40c8ddf6f46193e31f02386bd3778c47129a1","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Abstractions;

/// <summary>Persistence port for the durable candidate-email delivery projection.</summary>
public interface IEmailDeliveryRepository
{
    /// <summary>Loads one delivery without taking a database lock.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<EmailLog?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads one delivery while holding its PostgreSQL row lock.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<EmailLog?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads the newest unresolved delivery, or latest terminal row, while holding its row lock.</summary>
    /// <param name="candidateId">The candidate id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<EmailLog?> LockLatestForCandidateAsync(Guid candidateId, CancellationToken cancellationToken);

    /// <summary>Loads the newest unresolved delivery, or latest terminal row, for one template.</summary>
    /// <param name="candidateId">The candidate id.</param>
    /// <param name="template">The template.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<EmailLog?> GetLatestForCandidateAsync(
        Guid candidateId,
        EmailTemplate template,
        CancellationToken cancellationToken);

    /// <summary>Stages a delivery row for the caller's current transaction.</summary>
    /// <param name="delivery">The delivery.</param>
    void Add(EmailLog delivery);
}
`````

## after — src/EventBooking.Application/Abstractions/IEmailDeliveryRepository.cs — 1/1

<!-- vocabulary-file: {"id":36,"oldPath":"src/EventBooking.Application/Abstractions/IEmailDeliveryRepository.cs","newPath":"src/EventBooking.Application/Abstractions/IEmailDeliveryRepository.cs","beforeSha":"3e62032d1d7e7b509a65cfd4f3e576e7f83510f644b9326a8a6f12265a7b79ec","afterSha":"11240a83e2b416635e1395e1f1a40c8ddf6f46193e31f02386bd3778c47129a1","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Abstractions;

/// <summary>Persistence port for the durable attendee-email delivery projection.</summary>
public interface IEmailDeliveryRepository
{
    /// <summary>Loads one delivery without taking a database lock.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<EmailLog?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads one delivery while holding its PostgreSQL row lock.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<EmailLog?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads the newest unresolved delivery, or latest terminal row, while holding its row lock.</summary>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<EmailLog?> LockLatestForAttendeeAsync(Guid attendeeId, CancellationToken cancellationToken);

    /// <summary>Loads the newest unresolved delivery, or latest terminal row, for one template.</summary>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="template">The template.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<EmailLog?> GetLatestForAttendeeAsync(
        Guid attendeeId,
        EmailTemplate template,
        CancellationToken cancellationToken);

    /// <summary>Stages a delivery row for the caller's current transaction.</summary>
    /// <param name="delivery">The delivery.</param>
    void Add(EmailLog delivery);
}
`````

## before — src/EventBooking.Application/Abstractions/IEmailSender.cs — 1/1

<!-- vocabulary-file: {"id":37,"oldPath":"src/EventBooking.Application/Abstractions/IEmailSender.cs","newPath":"src/EventBooking.Application/Abstractions/IEmailSender.cs","beforeSha":"a66df8d8872866df8420338bf0b77ee51fe09abd59e09842b791e6e2284dae59","afterSha":"1ca79870e99d4dd2c632c449801c4d2c5cc23d66f780231faa7b3f37c0b9d52f","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Abstractions;

/// <summary>
/// A fully rendered candidate email held in memory only for the provider call. The optional
/// delivery identifier correlates the provider attempt with its hash-only durable record.
/// </summary>
/// <param name="CandidateId">The candidate receiving the message.</param>
/// <param name="ToAddress">The candidate's email address.</param>
/// <param name="ToName">The candidate's display name.</param>
/// <param name="Template">The candidate-facing template.</param>
/// <param name="Subject">The rendered subject.</param>
/// <param name="TextBody">The rendered plain-text body, including any raw token only in memory.</param>
/// <param name="HtmlBody">The rendered HTML body, including any raw token only in memory.</param>
/// <param name="DeliveryId">The safe durable delivery identifier, when dispatched through the coordinator.</param>
public sealed record EmailMessage(
    Guid CandidateId,
    string ToAddress,
    string ToName,
    EmailTemplate Template,
    string Subject,
    string TextBody,
    string HtmlBody,
    Guid? DeliveryId = null);

/// <summary>Provider-facing port for one rendered candidate email.</summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends the message and returns false instead of throwing when the provider rejects it. A
    /// direct, uncoordinated call writes its own email-log row; a coordinated call carries a
    /// delivery identifier whose existing durable row is completed by the delivery service.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
`````

## after — src/EventBooking.Application/Abstractions/IEmailSender.cs — 1/1

<!-- vocabulary-file: {"id":37,"oldPath":"src/EventBooking.Application/Abstractions/IEmailSender.cs","newPath":"src/EventBooking.Application/Abstractions/IEmailSender.cs","beforeSha":"a66df8d8872866df8420338bf0b77ee51fe09abd59e09842b791e6e2284dae59","afterSha":"1ca79870e99d4dd2c632c449801c4d2c5cc23d66f780231faa7b3f37c0b9d52f","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Abstractions;

/// <summary>
/// A fully rendered attendee email held in memory only for the provider call. The optional
/// delivery identifier correlates the provider attempt with its hash-only durable record.
/// </summary>
/// <param name="AttendeeId">The attendee receiving the message.</param>
/// <param name="ToAddress">The attendee's email address.</param>
/// <param name="ToName">The attendee's display name.</param>
/// <param name="Template">The attendee-facing template.</param>
/// <param name="Subject">The rendered subject.</param>
/// <param name="TextBody">The rendered plain-text body, including any raw token only in memory.</param>
/// <param name="HtmlBody">The rendered HTML body, including any raw token only in memory.</param>
/// <param name="DeliveryId">The safe durable delivery identifier, when dispatched through the coordinator.</param>
public sealed record EmailMessage(
    Guid AttendeeId,
    string ToAddress,
    string ToName,
    EmailTemplate Template,
    string Subject,
    string TextBody,
    string HtmlBody,
    Guid? DeliveryId = null);

/// <summary>Provider-facing port for one rendered attendee email.</summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends the message and returns false instead of throwing when the provider rejects it. A
    /// direct, uncoordinated call writes its own email-log row; a coordinated call carries a
    /// delivery identifier whose existing durable row is completed by the delivery service.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
`````

## before — src/EventBooking.Application/Abstractions/IEmployeeGroupRepository.cs — 1/1

<!-- vocabulary-file: {"id":38,"oldPath":"src/EventBooking.Application/Abstractions/IEmployeeGroupRepository.cs","newPath":"src/EventBooking.Application/Abstractions/IAttendeeGroupRepository.cs","beforeSha":"731271ccd95e974af25bf6e4bf4277bbaaad2015d9e6403720f6d23fa7f40c1e","afterSha":"57aafe85f7f8140a6ef25a4bda776239ba0aab14ac5744fb1fa4e589f561513a","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.EmployeeGroups;

namespace EventBooking.Application.Abstractions;

/// <summary>Reads change-controlled Employee Group reference data with complete mappings.</summary>
public interface IEmployeeGroupRepository
{
    /// <summary>Gets a group by stable identifier, including inactive or inconsistent rows.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<EmployeeGroup?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Gets a group from a trimmed case-insensitive canonical-code input.</summary>
    /// <param name="code">The code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<EmployeeGroup?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>Lists active mapped groups ordered by display name.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<EmployeeGroup>> ListActiveAsync(CancellationToken cancellationToken);
}
`````

## after — src/EventBooking.Application/Abstractions/IAttendeeGroupRepository.cs — 1/1

<!-- vocabulary-file: {"id":38,"oldPath":"src/EventBooking.Application/Abstractions/IEmployeeGroupRepository.cs","newPath":"src/EventBooking.Application/Abstractions/IAttendeeGroupRepository.cs","beforeSha":"731271ccd95e974af25bf6e4bf4277bbaaad2015d9e6403720f6d23fa7f40c1e","afterSha":"57aafe85f7f8140a6ef25a4bda776239ba0aab14ac5744fb1fa4e589f561513a","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Application.Abstractions;

/// <summary>Reads change-controlled Attendee Group reference data with complete mappings.</summary>
public interface IAttendeeGroupRepository
{
    /// <summary>Gets a group by stable identifier, including inactive or inconsistent rows.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AttendeeGroup?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Gets a group from a trimmed case-insensitive canonical-code input.</summary>
    /// <param name="code">The code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AttendeeGroup?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>Lists active mapped groups ordered by display name.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<AttendeeGroup>> ListActiveAsync(CancellationToken cancellationToken);
}
`````

## before — src/EventBooking.Application/Abstractions/IInviteRepository.cs — 1/1

<!-- vocabulary-file: {"id":39,"oldPath":"src/EventBooking.Application/Abstractions/IInviteRepository.cs","newPath":"src/EventBooking.Application/Abstractions/IInviteRepository.cs","beforeSha":"fc0a988a319ce9fafa7da668c954067f5fd114a82e19e152ed35a37ba94da889","afterSha":"11cf32adcf3c2c881259190eb88fd172455196044ce6594fa2a9ecfea0a16be0","side":"before","part":1,"parts":1} -->

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
    /// offered slots. Candidate deletion and expiry processing use it after the candidate lock.
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
    /// invite remains usable or offers the selected slot.
    /// </summary>
    /// <param name="tokenHash">The token hash.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Invite?> LockByTokenHashForUpdateAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>
    /// Takes a row-level write lock on the candidate's current pending invite and loads its
    /// offered slots. Callers hold the candidate lifecycle lock before calling this method.
    /// </summary>
    /// <param name="candidateId">The candidate id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Invite?> LockPendingForCandidateAsync(Guid candidateId, CancellationToken cancellationToken);

    /// <summary>Provides get pending for candidate async within this contract.</summary>
    /// <param name="candidateId">The candidate id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Invite?> GetPendingForCandidateAsync(Guid candidateId, CancellationToken cancellationToken);

    /// <summary>Locks the Candidate's pending initial Invite after the Candidate lifecycle lock.</summary>
    /// <param name="candidateId">The candidate id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Invite?> LockPendingInitialForCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Takes row-level write locks on every pending Invite for the candidate, ordered by ID, and
    /// loads their offered slots. Callers hold the candidate lifecycle lock before calling this
    /// method; recovery issuance reads the set authoritatively exactly once.
    /// </summary>
    /// <param name="candidateId">The candidate id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Invite>> LockPendingListForCandidateAsync(
        Guid candidateId,
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

<!-- vocabulary-file: {"id":39,"oldPath":"src/EventBooking.Application/Abstractions/IInviteRepository.cs","newPath":"src/EventBooking.Application/Abstractions/IInviteRepository.cs","beforeSha":"fc0a988a319ce9fafa7da668c954067f5fd114a82e19e152ed35a37ba94da889","afterSha":"11cf32adcf3c2c881259190eb88fd172455196044ce6594fa2a9ecfea0a16be0","side":"after","part":1,"parts":1} -->

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

## before — src/EventBooking.Application/Abstractions/ISlotCapacityRepository.cs — 1/1

<!-- vocabulary-file: {"id":40,"oldPath":"src/EventBooking.Application/Abstractions/ISlotCapacityRepository.cs","newPath":"src/EventBooking.Application/Abstractions/IEventCapacityRepository.cs","beforeSha":"e57ab8b6e8cfc609846f443df56a33d8be336d5935ae3ae1cc6466260b3220f3","afterSha":"7eda22c77fdce1b680112fb02bad2d84bd4e355a2a3e3ebc14ce4e87e23da4d5","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines islot capacity repository for the current use case.</summary>
public interface ISlotCapacityRepository
{
    /// <summary>
    /// Takes a row-level write lock on the capacity rows for one slot and the given appointment
    /// types, and returns them. Must be called inside a transaction. Rows are locked in
    /// appointment-type order so two concurrent callers can never deadlock against each other.
    /// </summary>
    /// <param name="confirmedSlotId">The confirmed slot id.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<SlotCapacity>> LockForUpdateAsync(
        Guid confirmedSlotId,
        IReadOnlyCollection<Guid> appointmentTypeIds,
        CancellationToken cancellationToken);
}
`````

## after — src/EventBooking.Application/Abstractions/IEventCapacityRepository.cs — 1/1

<!-- vocabulary-file: {"id":40,"oldPath":"src/EventBooking.Application/Abstractions/ISlotCapacityRepository.cs","newPath":"src/EventBooking.Application/Abstractions/IEventCapacityRepository.cs","beforeSha":"e57ab8b6e8cfc609846f443df56a33d8be336d5935ae3ae1cc6466260b3220f3","afterSha":"7eda22c77fdce1b680112fb02bad2d84bd4e355a2a3e3ebc14ce4e87e23da4d5","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Events;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines ievent capacity repository for the current use case.</summary>
public interface IEventCapacityRepository
{
    /// <summary>
    /// Takes a row-level write lock on the capacity rows for one event and the given appointment
    /// types, and returns them. Must be called inside a transaction. Rows are locked in
    /// appointment-type order so two concurrent callers can never deadlock against each other.
    /// </summary>
    /// <param name="eventId">The event id.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<EventCapacity>> LockForUpdateAsync(
        Guid eventId,
        IReadOnlyCollection<Guid> appointmentTypeIds,
        CancellationToken cancellationToken);
}
`````

## before — src/EventBooking.Application/Abstractions/ISlotProposalRepository.cs — 1/1

<!-- vocabulary-file: {"id":41,"oldPath":"src/EventBooking.Application/Abstractions/ISlotProposalRepository.cs","newPath":"src/EventBooking.Application/Abstractions/IEventProposalRepository.cs","beforeSha":"de02c6956d0271e08b8675490b222957dee514f0fb434dde8ebe2e76113dc025","afterSha":"258279a03fcaf8afdc6c02e173446dce1e844e45204dda9562096253e86876b3","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines islot proposal repository for the current use case.</summary>
public interface ISlotProposalRepository
{
    /// <summary>Provides get async within this contract.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<SlotProposal?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Takes the row-level write lock for an existing proposal and loads its acceptances. Proposal
    /// lifecycle mutations must call this inside their unit-of-work transaction before observing
    /// status or changing an acceptance.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<SlotProposal?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Provides list open async within this contract.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<SlotProposal>> ListOpenAsync(CancellationToken cancellationToken);

    /// <summary>Provides add within this contract.</summary>
    /// <param name="proposal">The proposal.</param>
    void Add(SlotProposal proposal);
}
`````

## after — src/EventBooking.Application/Abstractions/IEventProposalRepository.cs — 1/1

<!-- vocabulary-file: {"id":41,"oldPath":"src/EventBooking.Application/Abstractions/ISlotProposalRepository.cs","newPath":"src/EventBooking.Application/Abstractions/IEventProposalRepository.cs","beforeSha":"de02c6956d0271e08b8675490b222957dee514f0fb434dde8ebe2e76113dc025","afterSha":"258279a03fcaf8afdc6c02e173446dce1e844e45204dda9562096253e86876b3","side":"after","part":1,"parts":1} -->

`````csharp
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
`````

## before — src/EventBooking.Application/Access/StaffAccessAuthorizer.cs — 1/1

<!-- vocabulary-file: {"id":42,"oldPath":"src/EventBooking.Application/Access/StaffAccessAuthorizer.cs","newPath":"src/EventBooking.Application/Access/StaffAccessAuthorizer.cs","beforeSha":"da274031e8a6e69e4d21dbb6dac39236a2eeb52a62b563395fd4c22cffb1409d","afterSha":"49ce24da2329dcc629c46b8e6903e08a15e408e9a7682059eedb047e6eede5de","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Access;

/// <summary>Defines staff access context for the current use case.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="Roles">The roles.</param>
/// <param name="AppointmentTypeId">The appointment type id.</param>
public sealed record StaffAccessContext(
    Guid StaffUserId,
    IReadOnlySet<Role> Roles,
    Guid? AppointmentTypeId);

/// <summary>Defines istaff access authorizer for the current use case.</summary>
public interface IStaffAccessAuthorizer
{
    /// <summary>Provides authorize async within this contract.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="capability">The capability.</param>
    /// <param name="requiredAppointmentTypeId">The required appointment type id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Result<StaffAccessContext>> AuthorizeAsync(
        Guid staffUserId,
        StaffCapability capability,
        Guid? requiredAppointmentTypeId,
        CancellationToken cancellationToken);
}

/// <summary>Defines staff access authorizer for the current use case.</summary>
/// <param name="profiles">The profiles.</param>
public sealed class StaffAccessAuthorizer(IStaffAccessProfileRepository profiles)
    : IStaffAccessAuthorizer
{
    private static readonly Error Denied = Error.Forbidden("This staff profile cannot perform this operation.");

    /// <summary>Defines authorize async for the current use case.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="capability">The capability.</param>
    /// <param name="requiredAppointmentTypeId">The required appointment type id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<StaffAccessContext>> AuthorizeAsync(
        Guid staffUserId,
        StaffCapability capability,
        Guid? requiredAppointmentTypeId,
        CancellationToken cancellationToken)
    {
        var profile = await profiles.GetAsync(staffUserId, cancellationToken);
        if (profile is null || !profile.IsValid() || !IsAllowed(profile, capability))
        {
            return Result<StaffAccessContext>.Failure(Denied);
        }

        // A Manager or AppointmentStaff profile with no appointment-type scope grants no
        // capabilities on its own: the null scope denies every capability unless another role
        // on the same profile grants it.
        if (StaffAccessProfile.NeedsScope(profile.Roles)
            && profile.AppointmentTypeId is null
            && !IsAllowed(profile.IsAdmin, profile.IsCoordinator, false, false, capability))
        {
            return Result<StaffAccessContext>.Failure(Denied);
        }

        var scopedCapability = capability is
            StaffCapability.ManageSlotNegotiation or
            StaffCapability.ViewSlotOperations or
            StaffCapability.ConductAppointments;

        if (scopedCapability
            && StaffAccessProfile.NeedsScope(profile.Roles)
            && profile.AppointmentTypeId is null)
        {
            return Result<StaffAccessContext>.Failure(Denied);
        }

        if (requiredAppointmentTypeId is not null
            && scopedCapability
            && profile.AppointmentTypeId != requiredAppointmentTypeId)
        {
            return Result<StaffAccessContext>.Failure(Denied);
        }

        return Result<StaffAccessContext>.Success(new StaffAccessContext(
            profile.StaffUserId,
            profile.Roles,
            profile.AppointmentTypeId));
    }

    private static bool IsAllowed(StaffAccessProfile profile, StaffCapability capability) =>
        IsAllowed(profile.IsAdmin, profile.IsCoordinator, profile.IsManager, profile.IsAppointmentStaff, capability);

    private static bool IsAllowed(bool isAdmin, bool isCoordinator, bool isManager, bool isAppointmentStaff, StaffCapability capability)
    {
        // Explicit candidate-data deny for Admin is retained even though valid profiles make Admin
        // exclusive. It fails closed if invalid data reaches this method in a future refactor.
        if (isAdmin && capability is
            StaffCapability.ManageCandidates or
            StaffCapability.ViewCandidateDashboards or
            StaffCapability.ViewCandidateAudit)
        {
            return false;
        }

        return capability switch
        {
            StaffCapability.ManageSettings => isAdmin,
            StaffCapability.ManageStaffAccess => isAdmin,
            StaffCapability.ImportConfirmedSlots => isAdmin || isCoordinator,
            StaffCapability.ManageCandidates => isCoordinator,
            StaffCapability.ViewCandidateDashboards => isCoordinator,
            StaffCapability.ViewCandidateAudit => isCoordinator,
            StaffCapability.ViewSlotAudit => isAdmin || isCoordinator,
            StaffCapability.ManageSlotNegotiation => isManager,
            StaffCapability.ViewSlotOperations =>
                isAdmin || isCoordinator || isManager || isAppointmentStaff,
            StaffCapability.CancelConfirmedSlot =>
                isAdmin || isCoordinator || isManager,
            StaffCapability.ConductAppointments => isManager || isAppointmentStaff,
            _ => false,
        };
    }
}
`````
