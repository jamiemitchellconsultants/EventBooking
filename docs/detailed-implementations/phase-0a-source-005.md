# 00a — Port source 5 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## src/EventBooking.Application/Abstractions/IAppointmentTypeRepository.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Abstractions/IAppointmentTypeRepository.cs","encoding":"utf8","sha256":"4b0425bbb13967e04660538c33439c119be6187e520aa5a6bd22ae92481d3c32","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines iappointment type repository for the current use case.</summary>
public interface IAppointmentTypeRepository
{
    /// <summary>Provides list async within this contract.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<AppointmentType>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Provides get async within this contract.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AppointmentType?> GetAsync(Guid id, CancellationToken cancellationToken);
}
`````

## src/EventBooking.Application/Abstractions/IAppointmentWorkspaceQueries.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Abstractions/IAppointmentWorkspaceQueries.cs","encoding":"utf8","sha256":"60f38618a6fc16508f1f51c6c5315c2286f6253765e051472b7da9cc6376b6c6","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Appointments;

namespace EventBooking.Application.Abstractions;

/// <summary>Projects only appointment-delivery data inside a trusted appointment-type scope.</summary>
public interface IAppointmentWorkspaceQueries
{
    /// <summary>Lists recent-past, current, and future active slots containing active bookings in trusted scope.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    /// <param name="onOrAfter">The on or after.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AppointmentWorkspaceSlotList> ListSlotsAsync(
        Guid appointmentTypeId,
        DateOnly onOrAfter,
        CancellationToken cancellationToken);

    /// <summary>Gets one active slot's minimum appointment rows inside trusted scope.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    /// <param name="confirmedSlotId">The confirmed slot id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AppointmentSlotDetail?> GetSlotAsync(
        Guid appointmentTypeId,
        Guid confirmedSlotId,
        CancellationToken cancellationToken);
}
`````

## src/EventBooking.Application/Abstractions/IAuditLogger.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Abstractions/IAuditLogger.cs","encoding":"utf8","sha256":"f4e794c6404d93f3ce83a4b682d9feeac81ea5bf3594a2cbb4e42f7dfe707cc0","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Audit;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines iaudit logger for the current use case.</summary>
public interface IAuditLogger
{
    /// <summary>
    /// Stages one audit row on the current unit of work. Synchronous and void by design: the entry
    /// commits with the change it describes, or not at all.
    /// </summary>
    /// <param name="entityType">The entity type.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="action">The action.</param>
    /// <param name="actorType">The actor type.</param>
    /// <param name="actorId">The actor id.</param>
    /// <param name="details">The details.</param>
    void Record(
        string entityType,
        Guid entityId,
        AuditAction action,
        ActorType actorType,
        string? actorId,
        string? details = null);
}
`````

## src/EventBooking.Application/Abstractions/IAuditQueries.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Abstractions/IAuditQueries.cs","encoding":"utf8","sha256":"6cd75710abe6bc99a89d0a60b3616ca3f08d3d02277beea87c5eb5d08cf704a1","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Application.Abstractions;

/// <summary>A single row of audit history for one state change recorded in the audit log.</summary>
/// <param name="Timestamp">When the audited state change was recorded.</param>
/// <param name="EntityType">The audited entity type the row describes.</param>
/// <param name="EntityId">The identifier of the audited entity instance.</param>
/// <param name="Action">The recorded audit action name.</param>
/// <param name="ActorType">Who caused the change: staff, candidate token, or system.</param>
/// <param name="ActorId">The actor identifier, or null for a system actor.</param>
/// <param name="Details">Fixed identifiers, codes, and statuses only; never personal data.</param>
public sealed record AuditHistoryRow(
    DateTimeOffset Timestamp,
    string EntityType,
    Guid EntityId,
    string Action,
    string ActorType,
    string? ActorId,
    string? Details);

/// <summary>Cross-cutting audit search criteria. AllowedEntityTypes is set by the handler, never by the caller.</summary>
/// <param name="From">Inclusive lower bound on the recorded timestamp, or null for no bound.</param>
/// <param name="To">Inclusive upper bound on the recorded timestamp, or null for no bound.</param>
/// <param name="ActorType">Actor type name to match, or null for any.</param>
/// <param name="Action">Audit action name to match, or null for any.</param>
/// <param name="Identifier">Free-text identifier matched exactly against entity id or actor id, or null.</param>
/// <param name="AllowedEntityTypes">Entity types the caller may see, computed from granted capabilities.</param>
/// <param name="EntityType">Optional single entity type requested within the allowed bucket.</param>
/// <param name="Cursor">Opaque keyset cursor for the next page, or null for the newest page.</param>
/// <param name="PageSize">Rows per page; clamped to 200 by the implementation.</param>
public sealed record AuditSearchFilter(
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? ActorType,
    string? Action,
    string? Identifier,
    IReadOnlyList<string> AllowedEntityTypes,
    string? EntityType,
    string? Cursor,
    int PageSize = 50);

/// <summary>One page of audit search results, newest first.</summary>
/// <param name="Rows">The result rows in newest-first order.</param>
/// <param name="NextCursor">Opaque cursor for the following page, or null when exhausted.</param>
public sealed record AuditSearchPage(
    IReadOnlyList<AuditHistoryRow> Rows,
    string? NextCursor);

/// <summary>Defines iaudit queries for the current use case.</summary>
public interface IAuditQueries
{
    /// <summary>Provides for entity async within this contract.</summary>
    /// <param name="entityType">The entity type.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<AuditHistoryRow>> ForEntityAsync(
        string entityType, Guid entityId, CancellationToken cancellationToken);

    /// <summary>
    /// Every entry recorded against the candidate record itself and against their invites, bookings,
    /// and booking appointments, newest first. The caller must already hold candidate-audit access.
    /// </summary>
    /// <param name="candidateId">The candidate id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<AuditHistoryRow>> ForCandidateAsync(
        Guid candidateId, CancellationToken cancellationToken);

    /// <summary>Cross-cutting newest-first keyset-paginated search over the audit log.</summary>
    /// <param name="filter">The filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AuditSearchPage> SearchAsync(AuditSearchFilter filter, CancellationToken cancellationToken);
}
`````

## src/EventBooking.Application/Abstractions/IBookingAppointmentRepository.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Abstractions/IBookingAppointmentRepository.cs","encoding":"utf8","sha256":"11bbf8506b3f04a2f169271d42a1512f11b292fe7ea4b14c44a969c1dc168f69","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Abstractions;

/// <summary>Identifies the lifecycle owners of one appointment without granting authority.</summary>
/// <param name="CandidateId">The candidate owning the parent Booking.</param>
/// <param name="OriginalBookingId">The journey-root Booking identifier.</param>
/// <param name="BookingId">The parent booking identifier.</param>
/// <param name="ConfirmedSlotId">The confirmed slot selected by that booking.</param>
/// <param name="AppointmentTypeId">The appointment type scoping the lookup.</param>
public sealed record BookingAppointmentLocator(
    Guid CandidateId,
    Guid OriginalBookingId,
    Guid BookingId,
    Guid ConfirmedSlotId,
    Guid AppointmentTypeId);

/// <summary>Persists and locks booking appointments without widening appointment-type scope.</summary>
public interface IBookingAppointmentRepository
{
    /// <summary>Adds one appointment to the current unit of work.</summary>
    /// <param name="appointment">The appointment to track.</param>
    void Add(BookingAppointment appointment);

    /// <summary>Finds immutable parent identifiers only when record and trusted type both match.</summary>
    /// <param name="id">The stable appointment-record identifier.</param>
    /// <param name="appointmentTypeId">The trusted appointment-type scope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The immutable parent identifiers, or null when out of scope.</returns>
    Task<BookingAppointmentLocator?> FindLocatorInScopeAsync(
        Guid id,
        Guid appointmentTypeId,
        CancellationToken cancellationToken);

    /// <summary>Locks and returns one appointment only when record and trusted type both match.</summary>
    /// <param name="id">The stable appointment-record identifier.</param>
    /// <param name="appointmentTypeId">The trusted appointment-type scope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The locked appointment, or null when out of scope.</returns>
    Task<BookingAppointment?> LockForUpdateAsync(
        Guid id,
        Guid appointmentTypeId,
        CancellationToken cancellationToken);

    /// <summary>Locks all appointments for one Booking ordered by stable ID.</summary>
    /// <param name="bookingId">The parent booking identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The locked appointments in stable ID order.</returns>
    Task<IReadOnlyList<BookingAppointment>> LockForBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken);

    /// <summary>Lists the immutable Appointment Type snapshot owned by one Booking.</summary>
    /// <param name="bookingId">The booking id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<BookingAppointment>> ListForBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists the immutable Appointment Type snapshots owned by a whole Booking journey in stable
    /// ID order, so recovery eligibility reads every attempt deterministically in one round trip.
    /// </summary>
    /// <param name="bookingIds">The booking ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<BookingAppointment>> ListForBookingsAsync(
        IReadOnlyCollection<Guid> bookingIds,
        CancellationToken cancellationToken);
}
`````

## src/EventBooking.Application/Abstractions/IBookingRepository.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Abstractions/IBookingRepository.cs","encoding":"utf8","sha256":"434acb2ea383249ec666af0267531b855b8ac7d704680b028e616cd16d34365b","parts":1,"part":1} -->

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

## src/EventBooking.Application/Abstractions/ICandidateBookingQueries.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Abstractions/ICandidateBookingQueries.cs","encoding":"utf8","sha256":"bb4df8e0eba1bfa3f788be27b826066597979da48323b45766958ba9e5aae9c6","parts":1,"part":1} -->

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

## src/EventBooking.Application/Abstractions/ICandidateReadinessQueries.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Abstractions/ICandidateReadinessQueries.cs","encoding":"utf8","sha256":"39acf50ea14356a92d538f7c462d3230ed177370bce5a0cadfe2471b50f429ca","parts":1,"part":1} -->

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

## src/EventBooking.Application/Abstractions/ICandidateRepository.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Abstractions/ICandidateRepository.cs","encoding":"utf8","sha256":"973b322787541add87719aa702d666bbcbcac243d8075f789a76c6269dec5cac","parts":1,"part":1} -->

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

## src/EventBooking.Application/Abstractions/IClock.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Abstractions/IClock.cs","encoding":"utf8","sha256":"05f28a5282d61a1bdfda421bc3eb148fdf18551c0aa8f673cceadd0f4d99484c","parts":1,"part":1} -->

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

## src/EventBooking.Application/Abstractions/IConfirmedSlotRepository.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Abstractions/IConfirmedSlotRepository.cs","encoding":"utf8","sha256":"58aac80bfee7e20b205608c05e0074111fff87fc2c87fe5e36d313b465249df4","parts":1,"part":1} -->

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

## src/EventBooking.Application/Abstractions/IDashboardQueries.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Abstractions/IDashboardQueries.cs","encoding":"utf8","sha256":"6a88d14fa816341cb5494270549ef458ead1c5b3df559ede30f0702dbb5989af","parts":1,"part":1} -->

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

## src/EventBooking.Application/Abstractions/IEmailDeliveryRepository.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Abstractions/IEmailDeliveryRepository.cs","encoding":"utf8","sha256":"3e62032d1d7e7b509a65cfd4f3e576e7f83510f644b9326a8a6f12265a7b79ec","parts":1,"part":1} -->

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

## src/EventBooking.Application/Abstractions/IEmailSender.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Abstractions/IEmailSender.cs","encoding":"utf8","sha256":"a66df8d8872866df8420338bf0b77ee51fe09abd59e09842b791e6e2284dae59","parts":1,"part":1} -->

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

## src/EventBooking.Application/Abstractions/IEmployeeGroupRepository.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Abstractions/IEmployeeGroupRepository.cs","encoding":"utf8","sha256":"731271ccd95e974af25bf6e4bf4277bbaaad2015d9e6403720f6d23fa7f40c1e","parts":1,"part":1} -->

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

## src/EventBooking.Application/Abstractions/IInviteRepository.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Abstractions/IInviteRepository.cs","encoding":"utf8","sha256":"fc0a988a319ce9fafa7da668c954067f5fd114a82e19e152ed35a37ba94da889","parts":1,"part":1} -->

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

## src/EventBooking.Application/Abstractions/ISlotCapacityRepository.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Abstractions/ISlotCapacityRepository.cs","encoding":"utf8","sha256":"e57ab8b6e8cfc609846f443df56a33d8be336d5935ae3ae1cc6466260b3220f3","parts":1,"part":1} -->

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

## src/EventBooking.Application/Abstractions/ISlotProposalRepository.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Abstractions/ISlotProposalRepository.cs","encoding":"utf8","sha256":"de02c6956d0271e08b8675490b222957dee514f0fb434dde8ebe2e76113dc025","parts":1,"part":1} -->

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

## src/EventBooking.Application/Abstractions/IStaffAccessProfileRepository.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Abstractions/IStaffAccessProfileRepository.cs","encoding":"utf8","sha256":"fe605c28e2d86a66f1366e7741b5857b95b070882fd5d8341ba95f3edd285e15","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Access;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines istaff access profile repository for the current use case.</summary>
public interface IStaffAccessProfileRepository
{
    /// <summary>Provides get async within this contract.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<StaffAccessProfile?> GetAsync(Guid staffUserId, CancellationToken cancellationToken);
    /// <summary>Provides list async within this contract.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<StaffAccessProfile>> ListAsync(CancellationToken cancellationToken);
    /// <summary>Provides lock all async within this contract.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<StaffAccessProfile>> LockAllAsync(CancellationToken cancellationToken);
    /// <summary>Provides add within this contract.</summary>
    /// <param name="profile">The profile.</param>
    void Add(StaffAccessProfile profile);
    /// <summary>Provides remove within this contract.</summary>
    /// <param name="profile">The profile.</param>
    void Remove(StaffAccessProfile profile);
}
`````

## src/EventBooking.Application/Abstractions/IStaffIdentityRepository.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Abstractions/IStaffIdentityRepository.cs","encoding":"utf8","sha256":"da92b1ed2c78656e569a591c15c5d5c378373bbbae6b03952b23a2eb9cb5f438","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Access;

namespace EventBooking.Application.Abstractions;

/// <summary>Stores identity-provider pairs learned from validated authenticated tokens.</summary>
public interface IStaffIdentityRepository
{
    /// <summary>Gets the identity addressed by the canonical enterprise staff number.</summary>
    /// <param name="staffId">The staff number to resolve.</param>
    /// <param name="cancellationToken">Stops the database query.</param>
    /// <returns>The matching identity, or null when it has not been observed.</returns>
    Task<StaffIdentity?> GetByStaffIdAsync(
        StaffId staffId,
        CancellationToken cancellationToken);

    /// <summary>Lists all observed identity pairs for staff-access projection.</summary>
    /// <param name="cancellationToken">Stops the database query.</param>
    /// <returns>The observed identities ordered by provider key.</returns>
    Task<IReadOnlyList<StaffIdentity>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Atomically creates or refreshes the mirror row for a provider identity.</summary>
    /// <param name="staffUserId">The provider-assigned identity key.</param>
    /// <param name="staffId">The enterprise staff number carried by its token.</param>
    /// <param name="displayName">
    /// The name carried by the token, or null when it carries none. Overwrites the stored value on
    /// every write, including back to null.
    /// </param>
    /// <param name="lastSeenAt">The approximate observation time.</param>
    /// <param name="cancellationToken">Stops the database command.</param>
    Task UpsertAsync(
        Guid staffUserId,
        StaffId staffId,
        string? displayName,
        DateTimeOffset lastSeenAt,
        CancellationToken cancellationToken);
}
`````

## src/EventBooking.Application/Abstractions/ISystemSettingsRepository.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Abstractions/ISystemSettingsRepository.cs","encoding":"utf8","sha256":"da59552672afe5e589ca90dced1710f390822d32f03fabf6cbdb83244d0eff0b","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Settings;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines isystem settings repository for the current use case.</summary>
public interface ISystemSettingsRepository
{
    /// <summary>Returns the single seeded settings row. Never null.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<SystemSettings> GetAsync(CancellationToken cancellationToken);
}
`````

## src/EventBooking.Application/Abstractions/ITokenService.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Abstractions/ITokenService.cs","encoding":"utf8","sha256":"40d9f9a07f10867ed08a454a007547d690acb989b6aa0afefd5dd2d6bd5c0adf","parts":1,"part":1} -->

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

## src/EventBooking.Application/Abstractions/IUnitOfWork.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Abstractions/IUnitOfWork.cs","encoding":"utf8","sha256":"10bc1cb437e2a4c4c08636517592c9d3c9db75b30dab8778e49ae9382366c469","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Application.Abstractions;

/// <summary>Defines itransaction scope for the current use case.</summary>
public interface ITransactionScope : IAsyncDisposable
{
    /// <summary>Provides commit async within this contract.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task CommitAsync(CancellationToken cancellationToken);

    /// <summary>Provides rollback async within this contract.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task RollbackAsync(CancellationToken cancellationToken);
}

/// <summary>Defines iunit of work for the current use case.</summary>
public interface IUnitOfWork
{
    /// <summary>Provides save changes async within this contract.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Opens an explicit transaction. Only the use cases that touch capacity need one; everything
    /// else relies on the implicit transaction around a single save.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken);
}
`````

## src/EventBooking.Application/Access/MeHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Access/MeHandler.cs","encoding":"utf8","sha256":"4bf46edc61220d6c962905b68dffeeb428f675a36f5397a9e455a99ec8697456","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Access;

/// <summary>Describes the authenticated caller's staff number, roles, and shared scope.</summary>
/// <param name="StaffId">The staff id.</param>
/// <param name="Roles">The roles.</param>
/// <param name="AppointmentTypeId">The appointment type id.</param>
/// <param name="AppointmentTypeName">The appointment type name.</param>
public sealed record MeView(
    StaffId? StaffId,
    IReadOnlyList<Role> Roles,
    Guid? AppointmentTypeId,
    string? AppointmentTypeName);

/// <summary>Builds the current caller's view after synchronising provider-owned roles.</summary>
/// <param name="appointmentTypes">The appointment types.</param>
/// <param name="sync">The sync.</param>
public sealed class MeHandler(
    IAppointmentTypeRepository appointmentTypes,
    SyncStaffAccessProfileRolesHandler sync)
{
    /// <summary>Synchronises claimed roles, then returns roles and EventBooking-owned scope.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="staffId">The staff id.</param>
    /// <param name="claimedRoles">The claimed roles.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<MeView> GetAsync(
        Guid staffUserId,
        StaffId? staffId,
        IReadOnlySet<Role> claimedRoles,
        CancellationToken cancellationToken)
    {
        var profile = await sync.SyncAsync(staffUserId, claimedRoles, cancellationToken);
        if (profile is null || !profile.IsValid())
        {
            return new MeView(staffId, [], null, null);
        }

        string? appointmentTypeName = null;
        if (profile.AppointmentTypeId is not null)
        {
            var type = await appointmentTypes.GetAsync(
                profile.AppointmentTypeId.Value, cancellationToken);
            appointmentTypeName = type?.Name;
        }

        return new MeView(
            staffId,
            profile.Roles.OrderBy(role => role).ToList(),
            profile.AppointmentTypeId,
            appointmentTypeName);
    }
}
`````

## src/EventBooking.Application/Access/StaffAccessAuthorizer.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Access/StaffAccessAuthorizer.cs","encoding":"utf8","sha256":"da274031e8a6e69e4d21dbb6dac39236a2eeb52a62b563395fd4c22cffb1409d","parts":1,"part":1} -->

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
