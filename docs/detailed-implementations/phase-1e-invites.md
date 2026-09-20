# 01e — Location-restricted invites and closed attendee transitions (Task 8)

[← Phase overview](phase-1-domain.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This task follows Task 7 and closes Phase 1. An `Invite` is restricted to the `Location`s the Coordinator chose and every later offer is drawn from that same set; and the `Attendee` lifecycle becomes a closed table, so a move design 01 does not list is refused rather than quietly allowed.

> Use superpowers:executing-plans. Complete changed types and exact before/after files are embedded
> in the numbered companion volumes; apply them with the script in Step 3, never by hand.

**Goal:** An `Invite` snapshots 1 to 50 `InviteLocation`s that re-issues and recovery invites inherit, and an `Attendee` changes status only along the rows of design 01's table, stamping statusChangedAt on every accepted change.

**Architecture:** The legal moves live in the aggregate as a set, not as a per-method list of origins, so one table answers both “may this happen” and “what happened”. The set is public through a pure predicate, which is what lets the invite issuer tell FR-5.4's parked attendee from FR-5.7's failed re-issue without duplicating the rule. The status stamp moves from an infrastructure save-changes interceptor and a shadow property onto the aggregate, where the ontology puts it: the caller supplies the instant, so it is steerable from a test rather than read from an ambient clock.

**Tech Stack:** .NET 10, xUnit, EF Core, PostgreSQL Testcontainers, bUnit.

**Spec:** [Master Task 8](../superpowers/plans/2026-09-19-eventbooking-implementation.md), [domain model](../design/01-domain-model.md), [functional requirements](../design/02-functional-requirements.md), [ontology](../ontology.md).

## Global constraints

Stay on the Phase 1 branch. The book token stays a stored hash: design 06's versioned HMAC token replaces it in Task 9, together with the column, and splitting that across two tasks would rewrite the same 62 files twice. The option count stays the fixed three; `inviteOptionCount` becomes editable in Task 12 and the invite engine reads it in Task 14. Locations are still supplied as the transitional site by the application layer, because no command carries a Coordinator's selection until Task 12.

## Review focus

STOP AND CHECK four things. The table is the rule: the test enumerates all twenty-five ordered pairs and only the fourteen design 01 lists succeed, so adding a method later cannot widen it by accident. An invited attendee never drops back to `AwaitingAvailability` — a failed automatic re-issue ends at `NoResponseNeedsFollowUp`, per FR-5.7. A re-issue carries the originating invite's locations and requirements untouched, so no automatic path can widen what the Coordinator chose. And the recovery factory unions rather than refuses: repeating the original booking's location is not an error, because the caller does not have to know it is already there.

### Task 8: Invites with locations, and the closed attendee status table

**Files:**

- Modify: src/EventBooking.Application/Attendees/ImportAttendeesHandler.cs
- Modify: src/EventBooking.Application/Attendees/SaveAttendeeHandler.cs
- Modify: src/EventBooking.Application/Bookings/CancelAttendeeBookingHandler.cs
- Modify: src/EventBooking.Application/Bookings/CancelBookingHandler.cs
- Modify: src/EventBooking.Application/Bookings/ConfirmBookingHandler.cs
- Modify: src/EventBooking.Application/Bookings/ViewInviteHandler.cs
- Modify: src/EventBooking.Application/Events/CancelEventHandler.cs
- Modify: src/EventBooking.Application/Invites/ExpireInvitesHandler.cs
- Modify: src/EventBooking.Application/Invites/InviteIssuer.cs
- Modify: src/EventBooking.Domain/Attendees/Attendee.cs
- Modify: src/EventBooking.Domain/Invites/Invite.cs
- Create: src/EventBooking.Domain/Invites/InviteLocation.cs
- Modify: src/EventBooking.Infrastructure/DependencyInjection.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeConfiguration.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/InviteConfiguration.cs
- Create: src/EventBooking.Infrastructure/Persistence/Configurations/InviteLocationConfiguration.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260920095319_InviteLocations.Designer.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260920095319_InviteLocations.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/EventBookingDbContextModelSnapshot.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Queries/DashboardQueries.cs
- Modify: src/EventBooking.Infrastructure/Persistence/StatusStampingInterceptor.cs (delete)
- Modify: src/EventBooking.SeedData/DemoSeeder.cs
- Modify: tests/EventBooking.Api.Tests/AppointmentWorkspaceEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/AttendeeBookingCancellationEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/AttendeeEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/AuditEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/BookingEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/DashboardEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/EventEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/ManageBookingEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/RecentPastWorkspaceBoundaryTests.cs
- Modify: tests/EventBooking.Api.Tests/RecoveryInviteEndpointTests.cs
- Modify: tests/EventBooking.Application.Tests/Appointments/LateNoShowOutcomeTests.cs
- Modify: tests/EventBooking.Application.Tests/Appointments/RecentPastRecoveryEligibilityTests.cs
- Modify: tests/EventBooking.Application.Tests/Appointments/RecoveryBookingOutcomeCoordinatorTests.cs
- Modify: tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Attendees/ActiveBookingRequirementTests.cs
- Modify: tests/EventBooking.Application.Tests/Attendees/AttendeeAttendeeGroupFlowTests.cs
- Modify: tests/EventBooking.Application.Tests/Attendees/AttendeeGroupLifecycleTests.cs
- Modify: tests/EventBooking.Application.Tests/Attendees/DeleteAttendeeHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Attendees/ImportAttendeesHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Attendees/ListAttendeesHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Attendees/SaveAttendeeHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/BookingSnapshotCancellationTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/CancelAttendeeBookingHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/CancelBookingHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/ConfirmBookingHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/InviteOptionReplacementTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/InviteSnapshotAuthorityTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/RecoveryBookingLifecycleTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/ViewInviteHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/EventCancellationConcurrencyTests.cs
- Modify: tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs
- Modify: tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Invites/InviteIssuerTests.cs
- Modify: tests/EventBooking.Application.Tests/Invites/RecoveryInviteHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Invites/TriggerInviteHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Notifications/AttendeeEmailComposerTests.cs
- Modify: tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs
- Modify: tests/EventBooking.Domain.Tests/AttendeeGroups/AttendeeGroupAttendeeTests.cs
- Modify: tests/EventBooking.Domain.Tests/Attendees/AttendeeStatusTests.cs
- Test: tests/EventBooking.Domain.Tests/Attendees/AttendeeStatusTransitionTests.cs
- Modify: tests/EventBooking.Domain.Tests/Attendees/AttendeeTests.cs
- Modify: tests/EventBooking.Domain.Tests/Attendees/RequiredAttendeeGroupTests.cs
- Modify: tests/EventBooking.Domain.Tests/Bookings/BookingTests.cs
- Modify: tests/EventBooking.Domain.Tests/Bookings/RecoveryBookingTests.cs
- Test: tests/EventBooking.Domain.Tests/Invites/InviteLocationTests.cs
- Modify: tests/EventBooking.Domain.Tests/Invites/InviteRequirementSnapshotTests.cs
- Modify: tests/EventBooking.Domain.Tests/Invites/InviteTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/AttendeeBookingCancellationPersistenceTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/AttendeeBookingQueryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/AttendeeReadinessQueryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/AuditQueryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/BookingAppointmentPersistenceTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/ConcurrencyHarness.cs
- Modify: tests/EventBooking.Infrastructure.Tests/DashboardQueryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/InviteRequirementPersistenceTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/LoggingEmailSenderTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/RecoveryBookingPersistenceTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/RecoveryConcurrencyTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/RepairCConcurrencyTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/RepositoryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs
- Modify: tests/EventBooking.Mcp.Tests/McpScenarioSeeder.cs
- Modify: tests/TestSupport/ProposalFixture.cs

**Interfaces:**

These complete types define the changed public boundary and its domain behavior. Apply them with the after files at Step 3, not before the failing test.

```csharp
namespace EventBooking.Domain.Invites;

/// <summary>
/// One Location a Coordinator selected when issuing an Invite. Options, replacements and automatic
/// re-issues are drawn only from events at these locations.
/// </summary>
public sealed class InviteLocation
{
    private InviteLocation()
    {
    }

    /// <summary>Gets the owning Invite identifier.</summary>
    public Guid InviteId { get; private set; }

    /// <summary>Gets the selected Location identifier.</summary>
    public Guid LocationId { get; private set; }

    internal static InviteLocation For(Guid inviteId, Guid locationId) =>
        new() { InviteId = inviteId, LocationId = locationId };
}
```

```csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Invites;

/// <summary>An offer of event options carrying an immutable requirement snapshot.</summary>
public sealed class Invite
{
    /// <summary>Gets the number of event options every invite offers.</summary>
    public const int RequiredOptionCount = 3;

    /// <summary>The fewest locations an invite may be restricted to (design 08).</summary>
    public const int MinimumLocationCount = 1;

    /// <summary>The most locations an invite may be restricted to (design 08).</summary>
    public const int MaximumLocationCount = 50;

    private readonly List<InviteLocation> _locations = [];
    private readonly List<InviteOption> _options = [];
    private readonly List<InviteRequirement> _requirements = [];

    private Invite()
    {
        // Required by the persistence layer's constructor binding.
        TokenHash = string.Empty;
    }

    /// <summary>Gets the invite identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the invited attendee identifier.</summary>
    public Guid AttendeeId { get; private set; }

    /// <summary>Gets the original Booking recovered by this Invite, or null for an initial Invite.</summary>
    public Guid? RecoveryOfBookingId { get; private set; }

    /// <summary>The hash of the single-use token. The token itself is never stored.</summary>
    public string TokenHash { get; private set; }

    /// <summary>Gets when the invite stops being usable.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Gets the invite lifecycle state.</summary>
    public InviteStatus Status { get; private set; } = InviteStatus.Pending;

    /// <summary>Gets how many retries preceded this invite.</summary>
    public int RetryCount { get; private set; }

    /// <summary>Gets the locations every offer on this invite is drawn from.</summary>
    public IReadOnlyList<InviteLocation> Locations => _locations;

    /// <summary>Gets the selected location identifiers in stable order.</summary>
    public IReadOnlyList<Guid> LocationIds =>
        _locations.Select(l => l.LocationId).ToList();

    /// <summary>Gets the offered event options.</summary>
    public IReadOnlyList<InviteOption> Options => _options;

    /// <summary>Gets the offered event identifiers.</summary>
    public IReadOnlyList<Guid> OfferedEventIds => _options.Select(o => o.EventId).ToList();

    /// <summary>Gets the immutable requirement snapshot used by every downstream operation.</summary>
    public IReadOnlyList<InviteRequirement> Requirements => _requirements;

    /// <summary>Gets the snapshotted Appointment Type identifiers in stable order.</summary>
    public IReadOnlyList<Guid> RequiredAppointmentTypeIds =>
        _requirements.Select(r => r.AppointmentTypeId).Order().ToList();

    /// <summary>Replaces the persisted hash after issuing a fresh in-memory invite token.</summary>
    /// <param name="tokenHash">The token hash.</param>
    public void RotateTokenHash(string? tokenHash)
    {
        EnsurePending("Only a pending invite token can be rotated.");
        TokenHash = Guard.NotBlank(tokenHash, "tokenHash");
    }

    /// <summary>Creates an initial invite snapshotting every current derived requirement.</summary>
    /// <param name="id">The id.</param>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="tokenHash">The token hash.</param>
    /// <param name="expiresAt">The expires at.</param>
    /// <param name="locationIds">The locations the Coordinator selected; 1 to 50, no duplicates.</param>
    /// <param name="eventIds">The event ids.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    /// <param name="retryCount">The retry count.</param>
    public static Invite CreateInitial(
        Guid id,
        Guid attendeeId,
        string? tokenHash,
        DateTimeOffset expiresAt,
        IEnumerable<Guid> locationIds,
        IEnumerable<Guid> eventIds,
        IEnumerable<Guid> appointmentTypeIds,
        int retryCount) =>
        Create(
            id, attendeeId, null, tokenHash, expiresAt,
            DistinctLocations(locationIds), eventIds, appointmentTypeIds, retryCount);

    /// <summary>
    /// Issues the next invite of the same journey, on the same locations. Expiry re-issue, top-up
    /// and event cancellation all go through here, so none of them can quietly widen the set the
    /// Coordinator chose.
    /// </summary>
    /// <param name="id">The new invite's id.</param>
    /// <param name="originating">The invite being replaced.</param>
    /// <param name="tokenHash">The new token hash.</param>
    /// <param name="expiresAt">When the new invite stops being usable.</param>
    /// <param name="eventIds">The freshly chosen event options.</param>
    public static Invite Reissue(
        Guid id,
        Invite originating,
        string? tokenHash,
        DateTimeOffset expiresAt,
        IEnumerable<Guid> eventIds)
    {
        ArgumentNullException.ThrowIfNull(originating);

        return Create(
            id,
            originating.AttendeeId,
            originating.RecoveryOfBookingId,
            tokenHash,
            expiresAt,
            originating.LocationIds,
            eventIds,
            originating.RequiredAppointmentTypeIds,
            originating.RetryCount + 1);
    }

    /// <summary>Creates a recovery invite snapshotting only recoverable no-show types.</summary>
    /// <param name="id">The id.</param>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="recoveryOfBookingId">The recovery of booking id.</param>
    /// <param name="tokenHash">The token hash.</param>
    /// <param name="expiresAt">The expires at.</param>
    /// <param name="originalLocationId">The location of the booking being recovered.</param>
    /// <param name="additionalLocationIds">Further locations the Coordinator opened up, or null.</param>
    /// <param name="eventIds">The event ids.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    public static Invite CreateRecovery(
        Guid id,
        Guid attendeeId,
        Guid recoveryOfBookingId,
        string? tokenHash,
        DateTimeOffset expiresAt,
        Guid originalLocationId,
        IEnumerable<Guid>? additionalLocationIds,
        IEnumerable<Guid> eventIds,
        IEnumerable<Guid> appointmentTypeIds)
    {
        Guard.Against(recoveryOfBookingId == Guid.Empty, "recoveryOfBookingId must not be empty.");
        Guard.Against(originalLocationId == Guid.Empty, "originalLocationId must not be empty.");

        // The attendee already travelled to the original booking's location, so it is always
        // offered. Anything the Coordinator adds joins it; a repeat of it is not an error.
        var locations = new List<Guid> { originalLocationId };
        foreach (var locationId in additionalLocationIds ?? [])
        {
            if (!locations.Contains(locationId))
            {
                locations.Add(locationId);
            }
        }

        return Create(
            id, attendeeId, recoveryOfBookingId, tokenHash, expiresAt,
            Bounded(locations), eventIds, appointmentTypeIds, 0);
    }

    /// <summary>Determines whether the invite can still be used at the supplied instant.</summary>
    /// <param name="now">The now.</param>
    public bool IsUsableAt(DateTimeOffset now) =>
        Status == InviteStatus.Pending && now < ExpiresAt;

    /// <summary>Determines whether the invite offers the supplied eventItem.</summary>
    /// <param name="eventId">The event id.</param>
    public bool Offers(Guid eventId) =>
        _options.Any(o => o.EventId == eventId);

    /// <summary>Removes one offered event from a pending invite.</summary>
    /// <param name="eventId">The event id.</param>
    public void RemoveOption(Guid eventId)
    {
        EnsurePending("Only a pending invite's options can change.");

        var option = _options.SingleOrDefault(o => o.EventId == eventId);
        Guard.Against(option is null, "This invite does not offer that eventItem.");

        _options.Remove(option!);
    }

    /// <summary>Adds one offered event to a pending invite.</summary>
    /// <param name="eventId">The event id.</param>
    public void AddOption(Guid eventId)
    {
        EnsurePending("Only a pending invite's options can change.");
        Guard.Against(
            _options.Count >= RequiredOptionCount,
            $"An invite cannot offer more than {RequiredOptionCount} event options.");
        Guard.Against(Offers(eventId), "An invite cannot offer the same event twice.");

        _options.Add(InviteOption.For(Id, eventId));
    }

    /// <summary>Moves a pending invite to used.</summary>
    public void MarkUsed() => TransitionFromPendingTo(InviteStatus.Used);

    /// <summary>Moves a pending invite to expired.</summary>
    public void MarkExpired() => TransitionFromPendingTo(InviteStatus.Expired);

    /// <summary>Moves a pending invite to superseded.</summary>
    public void MarkSuperseded() => TransitionFromPendingTo(InviteStatus.Superseded);

    /// <summary>Cancels a pending recovery Invite without changing capacity.</summary>
    public void CancelRecovery()
    {
        Guard.Against(RecoveryOfBookingId is null, "Only a recovery invite can be cancelled.");
        EnsurePending("Only a pending recovery invite can be cancelled.");
        Status = InviteStatus.Cancelled;
    }

    private static Invite Create(
        Guid id,
        Guid attendeeId,
        Guid? recoveryOfBookingId,
        string? tokenHash,
        DateTimeOffset expiresAt,
        IReadOnlyList<Guid> locationIds,
        IEnumerable<Guid> eventIds,
        IEnumerable<Guid> appointmentTypeIds,
        int retryCount)
    {
        var invite = CreateCore(
            id, attendeeId, recoveryOfBookingId, tokenHash, expiresAt, locationIds, eventIds, retryCount);

        var snapshot = appointmentTypeIds.ToList();
        Guard.Against(snapshot.Count == 0, "An invite must snapshot at least one appointment type.");
        Guard.Against(
            snapshot.Distinct().Count() != snapshot.Count,
            "An invite cannot snapshot the same appointment type twice.");

        foreach (var appointmentTypeId in snapshot)
        {
            AppointmentTypeIds.EnsureKnown(appointmentTypeId);
        }

        foreach (var appointmentTypeId in snapshot.Order())
        {
            invite._requirements.Add(InviteRequirement.For(id, appointmentTypeId));
        }

        return invite;
    }

    private static Invite CreateCore(
        Guid id,
        Guid attendeeId,
        Guid? recoveryOfBookingId,
        string? tokenHash,
        DateTimeOffset expiresAt,
        IReadOnlyList<Guid> locationIds,
        IEnumerable<Guid> eventIds,
        int retryCount)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(attendeeId == Guid.Empty, "attendeeId must not be empty.");

        var offeredEventIds = eventIds.ToList();
        Guard.Against(
            offeredEventIds.Count != RequiredOptionCount,
            $"An invite must offer exactly {RequiredOptionCount} event options.");
        Guard.Against(
            offeredEventIds.Distinct().Count() != offeredEventIds.Count,
            "An invite cannot offer the same event twice.");

        var invite = new Invite
        {
            Id = id,
            AttendeeId = attendeeId,
            RecoveryOfBookingId = recoveryOfBookingId,
            TokenHash = Guard.NotBlank(tokenHash, "tokenHash"),
            ExpiresAt = expiresAt,
            Status = InviteStatus.Pending,
            RetryCount = Guard.NotNegative(retryCount, "retryCount"),
        };

        foreach (var locationId in locationIds)
        {
            invite._locations.Add(InviteLocation.For(id, locationId));
        }

        foreach (var eventId in eventIds)
        {
            invite._options.Add(InviteOption.For(id, eventId));
        }

        return invite;
    }

    private static IReadOnlyList<Guid> DistinctLocations(IEnumerable<Guid> locationIds)
    {
        ArgumentNullException.ThrowIfNull(locationIds);

        var chosen = locationIds.ToList();
        Guard.Against(
            chosen.Distinct().Count() != chosen.Count,
            "An invite cannot be restricted to the same location twice.");

        return Bounded(chosen);
    }

    private static IReadOnlyList<Guid> Bounded(IReadOnlyList<Guid> locationIds)
    {
        Guard.Against(
            locationIds.Count < MinimumLocationCount,
            "An invite must be restricted to at least one location.");
        Guard.Against(
            locationIds.Count > MaximumLocationCount,
            $"An invite cannot be restricted to more than {MaximumLocationCount} locations.");
        Guard.Against(
            locationIds.Any(locationId => locationId == Guid.Empty),
            "A location id must not be empty.");

        return locationIds;
    }

    private void TransitionFromPendingTo(InviteStatus target)
    {
        EnsurePending($"An invite that is already {Status} cannot become {target}.");
        Status = target;
    }

    private void EnsurePending(string message) =>
        Guard.Against(Status != InviteStatus.Pending, message);
}
```

```csharp
using System.Net.Mail;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Domain.Attendees;

/// <summary>A person invited to attend appointments, whose requirements derive from one attendee group.</summary>
public sealed class Attendee
{
    /// <summary>
    /// Every legal move, one entry per row of design 01's table. Anything absent is a defect
    /// (decision D15), so the set is the rule rather than a comment beside it.
    /// </summary>
    private static readonly HashSet<(AttendeeStatus From, AttendeeStatus To)> Legal =
    [
        (AttendeeStatus.NotYetInvited, AttendeeStatus.Invited),
        (AttendeeStatus.NotYetInvited, AttendeeStatus.AwaitingAvailability),
        (AttendeeStatus.AwaitingAvailability, AttendeeStatus.Invited),
        (AttendeeStatus.AwaitingAvailability, AttendeeStatus.NotYetInvited),
        (AttendeeStatus.Invited, AttendeeStatus.Invited),
        (AttendeeStatus.Invited, AttendeeStatus.Booked),
        (AttendeeStatus.Invited, AttendeeStatus.NoResponseNeedsFollowUp),
        (AttendeeStatus.Invited, AttendeeStatus.NotYetInvited),
        (AttendeeStatus.NoResponseNeedsFollowUp, AttendeeStatus.Invited),
        (AttendeeStatus.NoResponseNeedsFollowUp, AttendeeStatus.AwaitingAvailability),
        (AttendeeStatus.NoResponseNeedsFollowUp, AttendeeStatus.NotYetInvited),
        (AttendeeStatus.Booked, AttendeeStatus.Invited),
        (AttendeeStatus.Booked, AttendeeStatus.AwaitingAvailability),
        (AttendeeStatus.Booked, AttendeeStatus.NotYetInvited),
    ];

    private readonly List<AttendeeRequirement> _requirements = [];

    private Attendee()
    {
        // Required by the persistence layer's constructor binding.
        Name = string.Empty;
        Email = string.Empty;
    }

    /// <summary>Gets the attendee identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the attendee display name.</summary>
    public string Name { get; private set; }

    /// <summary>Gets the normalized attendee email address.</summary>
    public string Email { get; private set; }

    /// <summary>Gets the required assigned Attendee Group identifier.</summary>
    public Guid AttendeeGroupId { get; private set; }

    /// <summary>Gets where the attendee sits in the invite and booking lifecycle.</summary>
    public AttendeeStatus Status { get; private set; } = AttendeeStatus.NotYetInvited;

    /// <summary>Gets when the status was last written. Stamped on creation and on every change.</summary>
    public DateTimeOffset StatusChangedAt { get; private set; }

    /// <summary>Gets the materialized appointment types the attendee currently requires.</summary>
    public IReadOnlyList<AttendeeRequirement> Requirements => _requirements;

    /// <summary>Gets the identifiers of the appointment types the attendee currently requires.</summary>
    public IReadOnlyList<Guid> RequiredAppointmentTypeIds =>
        _requirements.Select(r => r.AppointmentTypeId).ToList();

    /// <summary>Creates a Attendee and derives every requirement from the active mapped group.</summary>
    /// <param name="id">The id.</param>
    /// <param name="name">The name.</param>
    /// <param name="email">The email.</param>
    /// <param name="attendeeGroup">The attendee group.</param>
    /// <param name="now">The instant the initial status is stamped with.</param>
    public static Attendee Create(
        Guid id, string? name, string? email, AttendeeGroup attendeeGroup, DateTimeOffset now)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");

        var attendee = new Attendee
        {
            Id = id,
            Name = Guard.NotBlank(name, "name"),
            Email = NormaliseEmail(email),
            Status = AttendeeStatus.NotYetInvited,
            StatusChangedAt = now,
        };

        attendee.AssignAttendeeGroup(attendeeGroup);

        return attendee;
    }

    /// <summary>Replaces the attendee name and email after validating both.</summary>
    /// <param name="name">The name.</param>
    /// <param name="email">The email.</param>
    public void UpdateDetails(string? name, string? email)
    {
        // Validate both before mutating either.
        var newName = Guard.NotBlank(name, "name");
        var newEmail = NormaliseEmail(email);

        Name = newName;
        Email = newEmail;
    }

    /// <summary>Assigns a group and derives its complete set; returns whether that set changed.</summary>
    /// <param name="attendeeGroup">The attendee group.</param>
    public bool AssignAttendeeGroup(AttendeeGroup attendeeGroup)
    {
        ArgumentNullException.ThrowIfNull(attendeeGroup);
        Guard.Against(!attendeeGroup.IsActive, "An inactive attendee group cannot be assignment authority.");

        var mapping = attendeeGroup.RequiredAppointmentTypeIds.ToList();

        Guard.Against(mapping.Count == 0, "An attendee group must map at least one appointment type.");

        foreach (var appointmentTypeId in mapping)
        {
            AppointmentTypeIds.EnsureKnown(appointmentTypeId);
        }

        AttendeeGroupId = attendeeGroup.Id;

        if (_requirements.Select(r => r.AppointmentTypeId).Order().SequenceEqual(mapping.Order()))
        {
            return false;
        }

        _requirements.Clear();
        foreach (var appointmentTypeId in mapping.Order())
        {
            _requirements.Add(AttendeeRequirement.For(Id, appointmentTypeId));
        }

        return true;
    }

    /// <summary>Whether design 01's table lists this move. Pure, so a caller can ask before acting.</summary>
    /// <param name="from">The current status.</param>
    /// <param name="to">The wanted status.</param>
    public static bool IsLegalTransition(AttendeeStatus from, AttendeeStatus to) =>
        Legal.Contains((from, to));

    /// <summary>Moves the attendee to Invited, from any status the table allows.</summary>
    /// <param name="now">The instant to stamp.</param>
    public void MarkInvited(DateTimeOffset now) => TransitionTo(AttendeeStatus.Invited, now);

    /// <summary>Moves the attendee to AwaitingAvailability, from any status the table allows.</summary>
    /// <param name="now">The instant to stamp.</param>
    public void MarkAwaitingAvailability(DateTimeOffset now) =>
        TransitionTo(AttendeeStatus.AwaitingAvailability, now);

    /// <summary>Moves an invited attendee to Booked.</summary>
    /// <param name="now">The instant to stamp.</param>
    public void MarkBooked(DateTimeOffset now) => TransitionTo(AttendeeStatus.Booked, now);

    /// <summary>Moves an invited attendee to NoResponseNeedsFollowUp.</summary>
    /// <param name="now">The instant to stamp.</param>
    public void MarkNoResponse(DateTimeOffset now) =>
        TransitionTo(AttendeeStatus.NoResponseNeedsFollowUp, now);

    /// <summary>Returns the attendee to NotYetInvited, which the table allows from anywhere.</summary>
    /// <param name="now">The instant to stamp.</param>
    public void ResetToNotYetInvited(DateTimeOffset now) =>
        TransitionTo(AttendeeStatus.NotYetInvited, now);

    /// <summary>Resets an unbooked Attendee after a derived requirement-set change.</summary>
    /// <param name="now">The instant to stamp.</param>
    public void ResetAfterRequirementChange(DateTimeOffset now)
    {
        if (Status is AttendeeStatus.NotYetInvited)
        {
            return;
        }

        // The table allows Booked to NotYetInvited, but only when the attendee themselves cancels.
        // A group reassignment must not silently discard a booking, so it is refused here.
        Guard.Against(
            Status == AttendeeStatus.Booked,
            $"A attendee cannot move from {Status} to {AttendeeStatus.NotYetInvited}.");

        TransitionTo(AttendeeStatus.NotYetInvited, now);
    }

    private void TransitionTo(AttendeeStatus target, DateTimeOffset now)
    {
        Guard.Against(
            !IsLegalTransition(Status, target),
            $"A attendee cannot move from {Status} to {target}.");

        Status = target;
        StatusChangedAt = now;
    }

    private static string NormaliseEmail(string? email)
    {
        var trimmed = email?.Trim() ?? string.Empty;

        var valid =
            trimmed.Length > 0
            && !trimmed.Any(char.IsWhiteSpace)
            && MailAddress.TryCreate(trimmed, out var parsed)
            && parsed!.Host.Contains('.');

        Guard.Against(!valid, "email is not a valid email address.");

        return trimmed.ToLowerInvariant();
    }
}
```

**Context you need**

- FR-5.1 (new): issuing an invite snapshots every requirement as `InviteRequirement`, the locations as `InviteLocation`, and `inviteOptionCount` eligible events as `InviteOption`s.
- FR-5.2 (new): an event is eligible only if it is `Active`, its window has not started, its `Location` is one of the invite's `InviteLocation`s, it lists every `InviteRequirement` type with capacity to spare, and it is not already offered.
- FR-5.4: fewer eligible events than the option count creates no invite and parks the attendee on `AwaitingAvailability`.
- FR-5.6 (carried hardening): an expired pending invite below the retry ceiling is re-issued “with the same `InviteLocation` set and `retryCount` + 1”.
- FR-5.7 (carried hardening): a failed automatic re-issue sets the attendee to `NoResponseNeedsFollowUp` and never leaves them `Invited` with no pending invite.
- FR-5.9: a Coordinator may re-invite an attendee in `AwaitingAvailability` or `NoResponseNeedsFollowUp` “at any time, choosing `Location`s again”.
- Design 01 (AttendeeStatus): the table lists every legal transition, and “any other transition is a defect (decision D15)”. Recovery invites and recovery bookings never change `AttendeeStatus`.
- Design 08 (boundary values): locations per invite are 1 to 50.
- The ontology already defines `InviteLocation` keyed by invite and location, states that `Invite` is restricted to one or more of them, and states that `Attendee` carries statusChangedAt, “stamped whenever `status` is written”.
- The predecessor stamped that column from an infrastructure save-changes interceptor through an EF shadow property. The column and its migration already exist and are unchanged; this task moves the value onto the aggregate and retires the interceptor.

- [ ] **Step 1: Write the failing tests**

Create these complete files before applying production changes.

tests/EventBooking.Domain.Tests/Attendees/AttendeeStatusTransitionTests.cs

```csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Tests.Attendees;

/// <summary>
/// Task 8: the attendee status table is closed. Every (from, to) pair is enumerated here, and only
/// the rows design 01 lists succeed; everything else is refused and leaves the attendee alone
/// (decision D15). Every accepted change stamps statusChangedAt.
/// </summary>
public class AttendeeStatusTransitionTests
{
    private static readonly DateTimeOffset Created = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = new(2026, 9, 2, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Every row of design 01's table, as (from, to) pairs.</summary>
    private static readonly HashSet<(AttendeeStatus From, AttendeeStatus To)> Legal =
    [
        (AttendeeStatus.NotYetInvited, AttendeeStatus.Invited),
        (AttendeeStatus.NotYetInvited, AttendeeStatus.AwaitingAvailability),
        (AttendeeStatus.AwaitingAvailability, AttendeeStatus.Invited),
        (AttendeeStatus.AwaitingAvailability, AttendeeStatus.NotYetInvited),
        (AttendeeStatus.Invited, AttendeeStatus.Invited),
        (AttendeeStatus.Invited, AttendeeStatus.Booked),
        (AttendeeStatus.Invited, AttendeeStatus.NoResponseNeedsFollowUp),
        (AttendeeStatus.Invited, AttendeeStatus.NotYetInvited),
        (AttendeeStatus.NoResponseNeedsFollowUp, AttendeeStatus.Invited),
        (AttendeeStatus.NoResponseNeedsFollowUp, AttendeeStatus.AwaitingAvailability),
        (AttendeeStatus.NoResponseNeedsFollowUp, AttendeeStatus.NotYetInvited),
        (AttendeeStatus.Booked, AttendeeStatus.Invited),
        (AttendeeStatus.Booked, AttendeeStatus.AwaitingAvailability),
        (AttendeeStatus.Booked, AttendeeStatus.NotYetInvited),
    ];

    public static TheoryData<AttendeeStatus, AttendeeStatus> EveryPair()
    {
        var data = new TheoryData<AttendeeStatus, AttendeeStatus>();
        foreach (var from in Enum.GetValues<AttendeeStatus>())
        {
            foreach (var to in Enum.GetValues<AttendeeStatus>())
            {
                data.Add(from, to);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryPair))]
    public void ExactlyTheListedTransitionsAreAccepted(AttendeeStatus from, AttendeeStatus to)
    {
        var attendee = AttendeeIn(from);
        var expected = Legal.Contains((from, to));

        Assert.Equal(expected, Attendee.IsLegalTransition(from, to));

        if (expected)
        {
            MoveTo(attendee, to, Later);

            Assert.Equal(to, attendee.Status);
            Assert.Equal(Later, attendee.StatusChangedAt);
            return;
        }

        var exception = Assert.Throws<DomainException>(() => MoveTo(attendee, to, Later));

        Assert.Equal($"A attendee cannot move from {from} to {to}.", exception.Message);
        Assert.Equal(from, attendee.Status);
        Assert.Equal(Created, attendee.StatusChangedAt);
    }

    [Fact]
    public void CreationStampsTheStatusChangeInstant()
    {
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", DatOnly(), Created);

        Assert.Equal(AttendeeStatus.NotYetInvited, attendee.Status);
        Assert.Equal(Created, attendee.StatusChangedAt);
    }

    [Fact]
    public void ARequirementChangeResetsAnUnbookedAttendeeButRefusesABookedOne()
    {
        var unbooked = AttendeeIn(AttendeeStatus.Invited);

        unbooked.ResetAfterRequirementChange(Later);

        Assert.Equal(AttendeeStatus.NotYetInvited, unbooked.Status);
        Assert.Equal(Later, unbooked.StatusChangedAt);

        var booked = AttendeeIn(AttendeeStatus.Booked);

        Assert.Throws<DomainException>(() => booked.ResetAfterRequirementChange(Later));
        Assert.Equal(AttendeeStatus.Booked, booked.Status);
    }

    [Fact]
    public void ARequirementChangeLeavesANeverInvitedAttendeeUntouched()
    {
        var attendee = AttendeeIn(AttendeeStatus.NotYetInvited);

        attendee.ResetAfterRequirementChange(Later);

        Assert.Equal(AttendeeStatus.NotYetInvited, attendee.Status);
        Assert.Equal(Created, attendee.StatusChangedAt);
    }

    private static AttendeeGroup DatOnly() =>
        AttendeeGroup.Define(
            Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);

    /// <summary>Walks an attendee to the wanted origin using only legal moves, stamped at creation.</summary>
    private static Attendee AttendeeIn(AttendeeStatus status)
    {
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", DatOnly(), Created);

        switch (status)
        {
            case AttendeeStatus.NotYetInvited:
                break;
            case AttendeeStatus.AwaitingAvailability:
                attendee.MarkAwaitingAvailability(Created);
                break;
            case AttendeeStatus.Invited:
                attendee.MarkInvited(Created);
                break;
            case AttendeeStatus.Booked:
                attendee.MarkInvited(Created);
                attendee.MarkBooked(Created);
                break;
            case AttendeeStatus.NoResponseNeedsFollowUp:
                attendee.MarkInvited(Created);
                attendee.MarkNoResponse(Created);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }

        return attendee;
    }

    private static void MoveTo(Attendee attendee, AttendeeStatus target, DateTimeOffset now)
    {
        switch (target)
        {
            case AttendeeStatus.NotYetInvited:
                attendee.ResetToNotYetInvited(now);
                break;
            case AttendeeStatus.AwaitingAvailability:
                attendee.MarkAwaitingAvailability(now);
                break;
            case AttendeeStatus.Invited:
                attendee.MarkInvited(now);
                break;
            case AttendeeStatus.Booked:
                attendee.MarkBooked(now);
                break;
            case AttendeeStatus.NoResponseNeedsFollowUp:
                attendee.MarkNoResponse(now);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(target));
        }
    }
}
```

tests/EventBooking.Domain.Tests/Invites/InviteLocationTests.cs

```csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Tests.Invites;

/// <summary>
/// Task 8: an invite is restricted to the locations the Coordinator chose, and every later offer
/// is drawn from that same set (FR-5.1, FR-5.2, FR-5.6, FR-5.9).
/// </summary>
public class InviteLocationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid London = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Dublin = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid Tokyo = Guid.Parse("10000000-0000-0000-0000-000000000003");

    private static readonly Guid EventA = Guid.Parse("50000001-0000-0000-0000-000000000001");
    private static readonly Guid EventB = Guid.Parse("50000002-0000-0000-0000-000000000002");
    private static readonly Guid EventC = Guid.Parse("50000003-0000-0000-0000-000000000003");
    private static readonly Guid EventD = Guid.Parse("50000004-0000-0000-0000-000000000004");
    private static readonly Guid EventE = Guid.Parse("50000005-0000-0000-0000-000000000005");
    private static readonly Guid EventF = Guid.Parse("50000006-0000-0000-0000-000000000006");

    private static Invite Initial(IEnumerable<Guid>? locationIds = null, int retryCount = 0) =>
        Invite.CreateInitial(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "hash-of-the-token",
            Now.AddDays(4),
            locationIds ?? [London, Dublin],
            [EventA, EventB, EventC],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp],
            retryCount);

    [Fact]
    public void AnInviteSnapshotsTheChosenLocations()
    {
        var invite = Initial();

        Assert.Equal([London, Dublin], invite.LocationIds.Order());
        Assert.All(invite.Locations, location => Assert.Equal(invite.Id, location.InviteId));
    }

    [Fact]
    public void AnInviteWithNoLocationsIsRefused()
    {
        var exception = Assert.Throws<DomainException>(() => Initial([]));

        Assert.Contains("at least one location", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnInviteWithMoreThanFiftyLocationsIsRefused()
    {
        var tooMany = Enumerable.Range(0, Invite.MaximumLocationCount + 1)
            .Select(_ => Guid.NewGuid())
            .ToList();

        var exception = Assert.Throws<DomainException>(() => Initial(tooMany));

        Assert.Contains(
            Invite.MaximumLocationCount.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FiftyLocationsIsAccepted()
    {
        var most = Enumerable.Range(0, Invite.MaximumLocationCount)
            .Select(_ => Guid.NewGuid())
            .ToList();

        Assert.Equal(Invite.MaximumLocationCount, Initial(most).Locations.Count);
    }

    [Fact]
    public void ARepeatedLocationIsRefused()
    {
        var exception = Assert.Throws<DomainException>(() => Initial([London, London]));

        Assert.Contains("same location twice", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AReissueCarriesTheLocationSetAndCountsTheRetry()
    {
        var original = Initial([London, Dublin, Tokyo], retryCount: 1);

        var reissued = Invite.Reissue(
            Guid.NewGuid(), original, "hash-of-the-next-token", Now.AddDays(11),
            [EventD, EventE, EventF]);

        Assert.Equal(original.LocationIds.Order(), reissued.LocationIds.Order());
        Assert.Equal(original.AttendeeId, reissued.AttendeeId);
        Assert.Equal(original.RequiredAppointmentTypeIds, reissued.RequiredAppointmentTypeIds);
        Assert.Equal(2, reissued.RetryCount);
        Assert.Equal(InviteStatus.Pending, reissued.Status);
        Assert.Equal([EventD, EventE, EventF], reissued.OfferedEventIds);
        Assert.All(reissued.Locations, location => Assert.Equal(reissued.Id, location.InviteId));
    }

    [Fact]
    public void AReissueOfARecoveryInviteStaysARecoveryInvite()
    {
        var bookingId = Guid.NewGuid();
        var original = Recovery(bookingId);

        var reissued = Invite.Reissue(
            Guid.NewGuid(), original, "hash-of-the-next-token", Now.AddDays(11),
            [EventD, EventE, EventF]);

        Assert.Equal(bookingId, reissued.RecoveryOfBookingId);
        Assert.Equal(1, reissued.RetryCount);
    }

    [Fact]
    public void ARecoveryInviteDefaultsToTheOriginalBookingsLocation()
    {
        var invite = Recovery(Guid.NewGuid());

        Assert.Equal([London], invite.LocationIds);
        Assert.Equal(0, invite.RetryCount);
    }

    [Fact]
    public void ARecoveryInviteUnionsAdditionalLocationsWithoutDuplicates()
    {
        var invite = Recovery(Guid.NewGuid(), [Dublin, London, Tokyo, Dublin]);

        Assert.Equal([London, Dublin, Tokyo], invite.LocationIds.Order());
    }

    [Fact]
    public void AnInviteSnapshotsMoreThanThreeRequirements()
    {
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "hash-of-the-token",
            Now.AddDays(4),
            [London],
            [EventA, EventB, EventC],
            AppointmentTypeIds.All,
            0);

        Assert.Equal(AppointmentTypeIds.All.Order(), invite.RequiredAppointmentTypeIds);
    }

    private static Invite Recovery(Guid bookingId, IEnumerable<Guid>? additionalLocationIds = null) =>
        Invite.CreateRecovery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            bookingId,
            "hash-of-the-token",
            Now.AddDays(4),
            London,
            additionalLocationIds,
            [EventA, EventB, EventC],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
}
```

- [ ] **Step 2: Verify the tests fail**

```bash
dotnet test tests/EventBooking.Domain.Tests --filter FullyQualifiedName~AttendeeStatusTransitionTests
dotnet test tests/EventBooking.Domain.Tests --filter FullyQualifiedName~InviteLocationTests
```

Expected: The suite does not compile: the location set and its two factories, the transition predicate, the statusChangedAt property and the instant every transition now takes do not exist yet. A Docker startup failure is not the intended failure.

- [ ] **Step 3: Apply the exact implementation and regression edits**

The 34 phase-1e-edits-NNN.md files supply 92 complete before/after changes. The script validates every payload and current file before writing. It accepts an already-applied after state, refuses unrelated edits, and only deletes explicitly listed files whose before hash matches.

```bash
node --input-type=module <<'TASK_PAYLOAD'
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
const root=fs.realpathSync('.'), plan='docs/detailed-implementations';
const sha=s=>crypto.createHash('sha256').update(s).digest('hex');
const names=fs.readdirSync(plan).filter(n=>n.startsWith('phase-1e-edits-')&&n.endsWith('.md')).sort();
if(names.length!==34)throw Error('Incomplete edit volumes.');
const entries=new Map();
for(const name of names){
 const text=fs.readFileSync(path.join(plan,name),'utf8');
 const pattern=/<!-- retirement-file: (.+) -->\n\n`{5}[^\n]*\n([\s\S]*?)\n`{5}/g;
 for(const match of text.matchAll(pattern)){
  const m=JSON.parse(match[1]);
  if(path.isAbsolute(m.file)||m.file.split('/').includes('..'))throw Error('Unsafe path.');
  const e=entries.get(m.id)??{...m,before:new Map(),after:new Map(),counts:{}};
  if(e.file!==m.file||e.beforeSha!==m.beforeSha||e.afterSha!==m.afterSha||e[m.side].has(m.part))throw Error('Conflicting metadata.');
  e[m.side].set(m.part,match[2]+'\n');e.counts[m.side]=m.parts;entries.set(m.id,e);
 }
}
if(entries.size!==92)throw Error('Incomplete operation set.');
const actions=[];
for(const e of entries.values()){
 for(const side of ['before','after']){
  if(e[side+'Sha']===null)continue;
  if(e[side].size!==e.counts[side])throw Error('Missing parts.');
  const parts=Array.from({length:e.counts[side]},(_,i)=>e[side].get(i+1));
  if(parts.some(p=>p===undefined))throw Error('Missing part number.');
  e[side+'Text']=parts.join('');
  if(sha(e[side+'Text'])!==e[side+'Sha'])throw Error('Payload checksum mismatch.');
 }
 const target=path.join(root,e.file);
 let parent=path.dirname(target);while(!fs.existsSync(parent))parent=path.dirname(parent);
 const resolved=fs.realpathSync(parent);
 if(resolved!==root&&!resolved.startsWith(root+path.sep))throw Error('Parent escapes checkout.');
 if(fs.existsSync(target)&&fs.lstatSync(target).isSymbolicLink())throw Error('Symlink target.');
 const actual=fs.existsSync(target)?sha(fs.readFileSync(target)):null;
 if(actual!==e.beforeSha&&actual!==e.afterSha)throw Error('Unrelated edit: '+e.file);
 actions.push({target,body:e.afterText,remove:e.afterSha===null});
}
for(const action of actions){
 if(action.remove){if(fs.existsSync(action.target))fs.unlinkSync(action.target);}
 else{fs.mkdirSync(path.dirname(action.target),{recursive:true});fs.writeFileSync(action.target,action.body);}
}
console.log('Applied '+actions.length+' verified file changes.');
TASK_PAYLOAD
```

The included migration, designer and model snapshot were generated with this exact command, and are already represented in the supplied after files. Do not generate a duplicate migration:

```bash
dotnet ef migrations add InviteLocations --project src/EventBooking.Infrastructure --startup-project src/EventBooking.Api
```

Review the complete migration in the edit volumes before running database-dependent tests.

- [ ] **Step 4: Verify the targeted tests pass**

```bash
dotnet test tests/EventBooking.Domain.Tests --filter FullyQualifiedName~AttendeeStatusTransitionTests
dotnet test tests/EventBooking.Domain.Tests --filter FullyQualifiedName~InviteLocationTests
```

Expected: all targeted cases pass, with zero skipped tests.

- [ ] **Step 5: Build and validate the complete solution**

```bash
dotnet build EventBooking.sln -warnaserror
dotnet test EventBooking.sln
```

Expected: zero warnings, zero errors and zero failed or skipped tests. The verified checkpoint contains 1540 tests: Domain 358, Application 424, Infrastructure 175, API 232, MCP 35, Web 241 and SeedData 75.

- [ ] **Step 6: Commit and push**

No ontology change belongs to this task. `docs/ontology.ttl` already defines `InviteLocation`, the one-or-more restriction on `Invite`, and statusChangedAt on `Attendee`; this task only puts them in code. If you find a concept that is genuinely missing, edit the source and regenerate before committing.

```bash
git add -- \
  'src/EventBooking.Application/Attendees/ImportAttendeesHandler.cs' \
  'src/EventBooking.Application/Attendees/SaveAttendeeHandler.cs' \
  'src/EventBooking.Application/Bookings/CancelAttendeeBookingHandler.cs' \
  'src/EventBooking.Application/Bookings/CancelBookingHandler.cs' \
  'src/EventBooking.Application/Bookings/ConfirmBookingHandler.cs' \
  'src/EventBooking.Application/Bookings/ViewInviteHandler.cs' \
  'src/EventBooking.Application/Events/CancelEventHandler.cs' \
  'src/EventBooking.Application/Invites/ExpireInvitesHandler.cs' \
  'src/EventBooking.Application/Invites/InviteIssuer.cs' \
  'src/EventBooking.Domain/Attendees/Attendee.cs' \
  'src/EventBooking.Domain/Invites/Invite.cs' \
  'src/EventBooking.Domain/Invites/InviteLocation.cs' \
  'src/EventBooking.Infrastructure/DependencyInjection.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/InviteConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/InviteLocationConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920095319_InviteLocations.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920095319_InviteLocations.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/EventBookingDbContextModelSnapshot.cs' \
  'src/EventBooking.Infrastructure/Persistence/Queries/DashboardQueries.cs' \
  'src/EventBooking.Infrastructure/Persistence/StatusStampingInterceptor.cs' \
  'src/EventBooking.SeedData/DemoSeeder.cs' \
  'tests/EventBooking.Api.Tests/AppointmentWorkspaceEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/AttendeeBookingCancellationEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/AttendeeEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/AuditEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/BookingEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/DashboardEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/EventEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/ManageBookingEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/RecentPastWorkspaceBoundaryTests.cs' \
  'tests/EventBooking.Api.Tests/RecoveryInviteEndpointTests.cs' \
  'tests/EventBooking.Application.Tests/Appointments/LateNoShowOutcomeTests.cs' \
  'tests/EventBooking.Application.Tests/Appointments/RecentPastRecoveryEligibilityTests.cs' \
  'tests/EventBooking.Application.Tests/Appointments/RecoveryBookingOutcomeCoordinatorTests.cs' \
  'tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Attendees/ActiveBookingRequirementTests.cs' \
  'tests/EventBooking.Application.Tests/Attendees/AttendeeAttendeeGroupFlowTests.cs' \
  'tests/EventBooking.Application.Tests/Attendees/AttendeeGroupLifecycleTests.cs' \
  'tests/EventBooking.Application.Tests/Attendees/DeleteAttendeeHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Attendees/ImportAttendeesHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Attendees/ListAttendeesHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Attendees/SaveAttendeeHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/BookingSnapshotCancellationTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/CancelAttendeeBookingHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/CancelBookingHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/ConfirmBookingHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/InviteOptionReplacementTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/InviteSnapshotAuthorityTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/RecoveryBookingLifecycleTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/ViewInviteHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/EventCancellationConcurrencyTests.cs' \
  'tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/InviteIssuerTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/RecoveryInviteHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/TriggerInviteHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Notifications/AttendeeEmailComposerTests.cs' \
  'tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs' \
  'tests/EventBooking.Domain.Tests/AttendeeGroups/AttendeeGroupAttendeeTests.cs' \
  'tests/EventBooking.Domain.Tests/Attendees/AttendeeStatusTests.cs' \
  'tests/EventBooking.Domain.Tests/Attendees/AttendeeStatusTransitionTests.cs' \
  'tests/EventBooking.Domain.Tests/Attendees/AttendeeTests.cs' \
  'tests/EventBooking.Domain.Tests/Attendees/RequiredAttendeeGroupTests.cs' \
  'tests/EventBooking.Domain.Tests/Bookings/BookingTests.cs' \
  'tests/EventBooking.Domain.Tests/Bookings/RecoveryBookingTests.cs' \
  'tests/EventBooking.Domain.Tests/Invites/InviteLocationTests.cs' \
  'tests/EventBooking.Domain.Tests/Invites/InviteRequirementSnapshotTests.cs' \
  'tests/EventBooking.Domain.Tests/Invites/InviteTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/AttendeeBookingCancellationPersistenceTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/AttendeeBookingQueryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/AttendeeReadinessQueryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/AuditQueryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/BookingAppointmentPersistenceTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/ConcurrencyHarness.cs' \
  'tests/EventBooking.Infrastructure.Tests/DashboardQueryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/InviteRequirementPersistenceTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/LoggingEmailSenderTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/RecoveryBookingPersistenceTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/RecoveryConcurrencyTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/RepairCConcurrencyTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/RepositoryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs' \
  'tests/EventBooking.Mcp.Tests/McpScenarioSeeder.cs' \
  'tests/TestSupport/ProposalFixture.cs'
git diff --cached --name-only
git diff --cached
node scripts/check-ontology-terms.mjs
: "${EXECUTOR_COAUTHOR:?Set the executing harness co-author identity}"
git commit -m "feat(domain): location-restricted invites and closed attendee transitions" -m "Co-authored-by: $EXECUTOR_COAUTHOR"
git push -u origin HEAD
```

Phase 1 is complete. Go to the phase overview's pull-request gate before starting Phase 2.
