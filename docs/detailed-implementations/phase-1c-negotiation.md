# 01c — Negotiation across any number of types (Task 6)

[← Phase overview](phase-1-domain.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This task follows Task 5 on the Phase 1 branch. The predecessor's three fixed Managers become a listed set of any size from 1 to 20, the proposing Manager commits at creation, and the proposal records which appointment type proposed it.

> Use superpowers:executing-plans. Complete changed types and exact before/after files are embedded
> in the numbered companion volumes; apply them with the script in Step 3, never by hand.

**Goal:** An `EventProposal` lists its own `AppointmentType`s, refuses a bad request with every failure at once, and becomes an `Event` exactly when the last listed type accepts.

**Architecture:** The aggregate owns the rules and the caller supplies the facts: whether the location is active, and for each listed type whether it is active and currently held by a Manager. Withdrawal is judged on the proposing appointment type, never on the person, so a successor Manager inherits it.

**Tech Stack:** .NET 10, xUnit, EF Core, PostgreSQL Testcontainers, bUnit.

**Spec:** [Master Task 6](../superpowers/plans/2026-09-19-eventbooking-implementation.md), [domain model](../design/01-domain-model.md), [functional requirements](../design/02-functional-requirements.md), [ontology](../ontology.md).

## Global constraints

Stay on the Phase 1 branch. Headcounts stay private to their own type: nothing here returns another type's number. Do not touch capacity arithmetic or lock ordering; Task 7 owns those. The listed set is fixed at creation — there is deliberately no method to add or remove a type afterwards.

## Review focus

STOP AND CHECK four things. The proposer's own acceptance is recorded by the create path, so a new proposal already has one acceptance and a single-type proposal is immediately complete. A refusal reports every failure together, not the first. A change to a proposal that is no longer `Open` is a conflict carrying the current status, not a validation error. And the migration refuses to run against existing proposals rather than inventing a proposing type for them: nothing in the predecessor's schema records one.

### Task 6: Negotiation across any number of types

**Files:**

- Modify: src/EventBooking.Application/Events/AcceptProposalHandler.cs
- Modify: src/EventBooking.Application/Events/ProposeEventHandler.cs
- Create: src/EventBooking.Application/Events/TransitionalLocation.cs
- Modify: src/EventBooking.Application/Events/WithdrawAcceptanceHandler.cs
- Modify: src/EventBooking.Application/Events/WithdrawProposalHandler.cs
- Modify: src/EventBooking.Domain/Events/Event.cs
- Modify: src/EventBooking.Domain/Events/EventProposal.cs
- Create: src/EventBooking.Domain/Events/EventProposalAppointmentType.cs
- Create: src/EventBooking.Domain/Events/ProposalNegotiation.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs
- Create: src/EventBooking.Infrastructure/Persistence/Configurations/EventProposalAppointmentTypeConfiguration.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/EventProposalConfiguration.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260920073816_ListedAppointmentTypes.Designer.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260920073816_ListedAppointmentTypes.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/EventBookingDbContextModelSnapshot.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs
- Modify: src/EventBooking.SeedData/DemoEventFactory.cs
- Modify: tests/EventBooking.Api.Tests/AttendeeEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/BookingEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/DashboardEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/EventBooking.Api.Tests.csproj
- Modify: tests/EventBooking.Api.Tests/EventCapacityAdjustmentEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/EventEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/Fixtures/EventFixture.cs
- Modify: tests/EventBooking.Api.Tests/ManageBookingEndpointTests.cs
- Modify: tests/EventBooking.Application.Tests/Attendees/DeleteAttendeeHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/CancelAttendeeBookingHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/CancelBookingHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/ConfirmBookingHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/InviteOptionReplacementTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/ViewInviteHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/EventBooking.Application.Tests.csproj
- Modify: tests/EventBooking.Application.Tests/Events/AcceptProposalHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/AcceptProposalHeadcountRevisionTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/AdjustEventCapacityHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/CombinedManagerAuthorizationTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/EventCancellationConcurrencyTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/GetManagerEventBoardHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/ManagerEventBoardHeadcountRevisionTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/ProposeEventHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/WithdrawAcceptanceHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/WithdrawProposalHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs
- Modify: tests/EventBooking.Application.Tests/Fixtures/EventFixture.cs
- Modify: tests/EventBooking.Application.Tests/Invites/EligibleEventFinderTests.cs
- Modify: tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Invites/InviteIssuerTests.cs
- Modify: tests/EventBooking.Application.Tests/Invites/RecoveryInviteHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Invites/TriggerInviteHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Notifications/AttendeeEmailComposerTests.cs
- Modify: tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs
- Modify: tests/EventBooking.Domain.Tests/EventBooking.Domain.Tests.csproj
- Modify: tests/EventBooking.Domain.Tests/Events/EventCancellationTests.cs
- Modify: tests/EventBooking.Domain.Tests/Events/EventCapacityHeadcountAdjustmentTests.cs
- Modify: tests/EventBooking.Domain.Tests/Events/EventCapacityTests.cs
- Modify: tests/EventBooking.Domain.Tests/Events/EventProposalAcceptanceTests.cs
- Modify: tests/EventBooking.Domain.Tests/Events/EventProposalConfirmationTests.cs
- Modify: tests/EventBooking.Domain.Tests/Events/EventProposalTests.cs
- Modify: tests/EventBooking.Domain.Tests/Events/EventTests.cs
- Test: tests/EventBooking.Domain.Tests/Events/NTypeNegotiationTests.cs
- Modify: tests/EventBooking.Domain.Tests/Events/ProposalAcceptanceHeadcountRevisionTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/AttendeeBookingQueryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyHarness.cs
- Modify: tests/EventBooking.Infrastructure.Tests/ConcurrencyHarness.cs
- Modify: tests/EventBooking.Infrastructure.Tests/DashboardQueryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/EventBooking.Infrastructure.Tests.csproj
- Modify: tests/EventBooking.Infrastructure.Tests/EventCapacityRepositoryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/Fixtures/EventFixture.cs
- Modify: tests/EventBooking.Infrastructure.Tests/RepairCConcurrencyTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/RepositoryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/SchemaTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs
- Modify: tests/EventBooking.Mcp.Tests/EventBooking.Mcp.Tests.csproj
- Modify: tests/EventBooking.Mcp.Tests/Fixtures/EventFixture.cs
- Modify: tests/EventBooking.SeedData.Tests/DemoInvitationHostTests.cs
- Modify: tests/EventBooking.SeedData.Tests/DemoInvitationSeederTests.cs
- Modify: tests/EventBooking.SeedData.Tests/EventBooking.SeedData.Tests.csproj
- Modify: tests/EventBooking.SeedData.Tests/ReanchorTests.cs
- Modify: tests/EventBooking.SeedData.Tests/ReseedTests.cs
- Modify: tests/EventBooking.Web.Tests/EventBooking.Web.Tests.csproj
- Test: tests/TestSupport/ProposalFixture.cs

**Interfaces:**

These complete types define the changed public boundary and its domain behavior. Apply them with the after files at Step 3, not before the failing test.

```csharp
using EventBooking.Domain.Common;
using EventBooking.Domain.Time;

namespace EventBooking.Domain.Events;

/// <summary>
/// A Manager's offer of an event: one window at one location, offering a fixed list of appointment
/// types. It becomes an event when every listed type's Manager has accepted with their own
/// headcount (design 01 — Negotiation).
/// </summary>
public sealed class EventProposal
{
    /// <summary>The most appointment types one proposal may list (design 08).</summary>
    public const int MaximumListedTypes = 20;

    /// <summary>The largest headcount a Manager may accept with (design 08).</summary>
    public const int MaximumHeadcount = 1000;

    private readonly List<ProposalAcceptance> _acceptances = [];
    private readonly List<EventProposalAppointmentType> _listedTypes = [];

    private EventProposal()
    {
        // Required by the persistence layer's constructor binding.
        Window = null!;
    }

    /// <summary>The proposal identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>The location that would host the event.</summary>
    public Guid LocationId { get; private set; }

    /// <summary>The proposed window, read in the location's zone.</summary>
    public EventWindow Window { get; private set; }

    /// <summary>Where the proposal stands.</summary>
    public EventProposalStatus Status { get; private set; } = EventProposalStatus.Open;

    /// <summary>The person who proposed it, kept for audit attribution only.</summary>
    public Guid CreatedByManagerUserId { get; private set; }

    /// <summary>
    /// The proposing Manager's own appointment type. Withdrawing the proposal, and withdrawing the
    /// proposer's acceptance, are judged against this type, so a successor Manager inherits both.
    /// </summary>
    public Guid ProposerAppointmentTypeId { get; private set; }

    /// <summary>The listed types, fixed at creation.</summary>
    public IReadOnlyList<EventProposalAppointmentType> ListedTypes => _listedTypes;

    /// <summary>The listed appointment type identifiers.</summary>
    public IReadOnlyList<Guid> ListedAppointmentTypeIds =>
        [.. _listedTypes.Select(listed => listed.AppointmentTypeId)];

    /// <summary>The acceptances recorded so far, one per accepted type.</summary>
    public IReadOnlyList<ProposalAcceptance> Acceptances => _acceptances;

    /// <summary>Whether every listed type has accepted.</summary>
    public bool IsFullyAccepted =>
        Status == EventProposalStatus.Open
        && _listedTypes.Count > 0
        && ListedAppointmentTypeIds.All(IsAcceptedBy);

    /// <summary>
    /// Makes a proposal, refusing it with every failure it has rather than the first (FR-2.1,
    /// FR-2.2). The proposer's own acceptance is recorded here, because a proposal nobody has
    /// committed to is noise.
    /// </summary>
    /// <param name="id">The new proposal identifier.</param>
    /// <param name="locationId">The location that would host the event.</param>
    /// <param name="locationIsActive">Whether that location is active.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    /// <param name="window">The proposed window, in local time at the location.</param>
    /// <param name="zones">The zone abstraction.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="listedTypes">The appointment types the event would offer.</param>
    /// <param name="proposerAppointmentTypeId">The proposing Manager's own type.</param>
    /// <param name="createdByManagerUserId">The proposing Manager, for audit attribution.</param>
    /// <param name="headcount">The proposer's own headcount.</param>
    public static EventProposal Propose(
        Guid id,
        Guid locationId,
        bool locationIsActive,
        string timeZoneId,
        EventWindow window,
        IEventWindowZones zones,
        DateTimeOffset now,
        IReadOnlyList<ProposableAppointmentType> listedTypes,
        Guid proposerAppointmentTypeId,
        Guid createdByManagerUserId,
        int headcount)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(listedTypes);
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(locationId == Guid.Empty, "locationId must not be empty.");
        Guard.Against(createdByManagerUserId == Guid.Empty, "createdByManagerUserId must not be empty.");

        var failures = Validate(
            locationIsActive, timeZoneId, window, zones, now, listedTypes, proposerAppointmentTypeId, headcount);
        if (failures.Count > 0)
        {
            throw new ProposalValidationException(failures);
        }

        var proposal = new EventProposal
        {
            Id = id,
            LocationId = locationId,
            Window = window,
            Status = EventProposalStatus.Open,
            CreatedByManagerUserId = createdByManagerUserId,
            ProposerAppointmentTypeId = proposerAppointmentTypeId,
        };

        foreach (var listed in listedTypes.OrderBy(type => type.Id))
        {
            proposal._listedTypes.Add(EventProposalAppointmentType.For(id, listed.Id));
        }

        proposal._acceptances.Add(
            ProposalAcceptance.Record(id, proposerAppointmentTypeId, createdByManagerUserId, headcount));

        return proposal;
    }

    /// <summary>
    /// Records or revises one listed type's acceptance (FR-2.4). Returns whether the headcount
    /// actually changed, so callers audit a real change and not a repeated save.
    /// </summary>
    /// <param name="appointmentTypeId">The accepting type, which must be listed.</param>
    /// <param name="managerUserId">The accepting Manager, for audit attribution.</param>
    /// <param name="headcount">How many attendees that team can take.</param>
    public bool Accept(Guid appointmentTypeId, Guid managerUserId, int headcount)
    {
        EnsureOpen();
        EnsureListed(appointmentTypeId);
        Guard.Against(
            headcount is < 1 or > MaximumHeadcount,
            $"headcount must be between 1 and {MaximumHeadcount}.");

        var existing = _acceptances.SingleOrDefault(
            acceptance => acceptance.AppointmentTypeId == appointmentTypeId);

        if (existing is null)
        {
            _acceptances.Add(ProposalAcceptance.Record(Id, appointmentTypeId, managerUserId, headcount));
            return true;
        }

        return existing.ChangeHeadcount(headcount);
    }

    /// <summary>
    /// Withdraws one listed type's acceptance. The proposer's own acceptance cannot be withdrawn:
    /// withdrawing the proposal is the way to take the whole offer back (FR-2.9).
    /// </summary>
    /// <param name="appointmentTypeId">The type withdrawing its acceptance.</param>
    public void WithdrawAcceptance(Guid appointmentTypeId)
    {
        EnsureOpen();
        EnsureListed(appointmentTypeId);
        Guard.Against(
            appointmentTypeId == ProposerAppointmentTypeId,
            "The proposing type's acceptance cannot be withdrawn; withdraw the proposal instead.");

        var acceptance = _acceptances.SingleOrDefault(a => a.AppointmentTypeId == appointmentTypeId);
        Guard.Against(acceptance is null, "This appointment type has not accepted the proposal.");

        _acceptances.Remove(acceptance!);
    }

    /// <summary>
    /// Withdraws the whole proposal. Only the proposing type may do this, whoever currently holds
    /// that type (FR-2.9, FR-2.10).
    /// </summary>
    /// <param name="actingAppointmentTypeId">The type the caller currently holds.</param>
    public void Withdraw(Guid actingAppointmentTypeId)
    {
        EnsureOpen();
        Guard.Against(
            actingAppointmentTypeId != ProposerAppointmentTypeId,
            "Only the proposing appointment type may withdraw the proposal.");

        Status = EventProposalStatus.Withdrawn;
    }

    /// <summary>
    /// Withdraws an open proposal whose window has started, as the sweep does (FR-2.12). Returns
    /// whether anything changed, so the sweep audits only real withdrawals.
    /// </summary>
    /// <param name="zones">The zone abstraction.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    /// <param name="now">The current instant.</param>
    public bool TryWithdrawStarted(IEventWindowZones zones, string timeZoneId, DateTimeOffset now)
    {
        if (Status != EventProposalStatus.Open || !Window.HasStarted(zones, timeZoneId, now))
        {
            return false;
        }

        Status = EventProposalStatus.Withdrawn;
        return true;
    }

    /// <summary>Whether the given type has accepted.</summary>
    /// <param name="appointmentTypeId">The appointment type.</param>
    public bool IsAcceptedBy(Guid appointmentTypeId) =>
        _acceptances.Any(a => a.AppointmentTypeId == appointmentTypeId);

    internal void MarkConfirmed()
    {
        Guard.Against(
            !IsFullyAccepted,
            "A proposal is confirmed only once every listed appointment type has accepted it.");
        Status = EventProposalStatus.Confirmed;
    }

    private static List<string> Validate(
        bool locationIsActive,
        string timeZoneId,
        EventWindow window,
        IEventWindowZones zones,
        DateTimeOffset now,
        IReadOnlyList<ProposableAppointmentType> listedTypes,
        Guid proposerAppointmentTypeId,
        int headcount)
    {
        var failures = new List<string>();

        if (!locationIsActive)
        {
            failures.Add("location-inactive");
        }

        var zoneProblem = window.ProblemIn(zones, timeZoneId);
        if (zoneProblem == EventWindowZoneProblem.UnknownZone)
        {
            failures.Add("unknown-zone");
        }
        else if (zoneProblem != EventWindowZoneProblem.None)
        {
            failures.Add("window-has-no-unique-instant");
        }
        else if (window.StartInstant(zones, timeZoneId) <= now)
        {
            failures.Add("window-not-in-future");
        }

        if (listedTypes.Count == 0)
        {
            failures.Add("types-empty");
        }

        if (listedTypes.Count > MaximumListedTypes)
        {
            failures.Add("types-too-many");
        }

        if (listedTypes.Select(type => type.Id).Distinct().Count() != listedTypes.Count)
        {
            failures.Add("types-duplicated");
        }

        AddNamed(failures, "type-inactive", listedTypes.Where(type => !type.IsActive));
        AddNamed(failures, "type-without-manager", listedTypes.Where(type => type.IsActive && !type.HasCurrentManager));

        if (listedTypes.All(type => type.Id != proposerAppointmentTypeId))
        {
            failures.Add("proposer-type-not-listed");
        }

        if (headcount is < 1 or > MaximumHeadcount)
        {
            failures.Add("headcount-out-of-range");
        }

        return failures;
    }

    private static void AddNamed(
        List<string> failures, string code, IEnumerable<ProposableAppointmentType> offending)
    {
        var codes = offending.Select(type => type.Code).Distinct().Order().ToList();
        if (codes.Count > 0)
        {
            failures.Add($"{code}: {string.Join(", ", codes)}");
        }
    }

    private void EnsureOpen()
    {
        if (Status != EventProposalStatus.Open)
        {
            throw new ProposalNotOpenException(Status);
        }
    }

    private void EnsureListed(Guid appointmentTypeId) =>
        Guard.Against(
            !ListedAppointmentTypeIds.Contains(appointmentTypeId),
            "This appointment type is not listed on the proposal.");
}
```

```csharp
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
```

**Context you need**

- FR-2.1 (quoted): when a Manager submits a proposal the system “shall create an `EventProposal` with status `Open`, one `EventProposalAppointmentType` per listed type, and the Manager's own `ProposalAcceptance`”, from `locationId`, `date`, `startTime` and `durationMinutes`, a list of `appointmentTypeId`s, and their own `headcount`.
- FR-2.2 (quoted): a proposal is rejected “naming each failure” when the `Location` is inactive, the `EventWindow` is invalid or not in the future, “the list is empty, has more than 20 entries, or contains duplicates”, a listed type is inactive or has no current Manager, the list omits the proposer's own type, or “`headcount` is not a positive integer of at most 1000”.
- FR-2.3: a list of exactly one type confirms the proposal in the same transaction.
- FR-2.4: a repeated submission from a listed type replaces the headcount in place, “writing an audit entry only if the value changed”.
- FR-2.6: when the accepted types equal the listed types the proposal is `Confirmed` and an `Active` `Event` is created with one `EventCapacity` per listed type, at that type's own headcount.
- FR-2.9: while a proposal is `Open`, a listed type's Manager may withdraw their own acceptance, “except the proposer's type”, and “the current Manager of the proposer's type may withdraw the whole proposal”.
- FR-2.10 (carried hardening): “Acceptances and proposals belong to the `AppointmentType`, not to the person”, so a successor acts on everything a predecessor left.
- FR-2.11: an acceptance, revision or withdrawal against a `Confirmed` or `Withdrawn` proposal returns a conflict with the current status and changes nothing.
- FR-2.12: the sweep withdraws every `Open` proposal whose window has started, audited with actor type `System`.
- Design 01 (Negotiation): the list is fixed at creation, because “changing the list after other Managers have accepted would make their headcounts refer to a different event”; every listed type must have a current Manager at creation, or the proposal could never confirm.
- The ontology defines `proposerAppointmentTypeId` on `EventProposal`, with the invariant that withdrawing the proposal and the proposer's acceptance are judged against that type rather than `createdByManagerUserId`.
- `EventProposalAppointmentType` is the ontology's name for one listed type, keyed by proposal and appointment type.

- [ ] **Step 1: Write the failing tests**

Create these complete files before applying production changes.

tests/EventBooking.Domain.Tests/Events/NTypeNegotiationTests.cs

```csharp
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;
using EventBooking.Domain.Time;

namespace EventBooking.Domain.Tests.Events;

/// <summary>
/// Task 6: negotiation runs across any number of listed appointment types, and the proposal knows
/// which type proposed it (FR-2.1 to FR-2.12; design 01 — Negotiation).
/// </summary>
public class NTypeNegotiationTests
{
    private static readonly Guid Medical = Guid.Parse("a0000001-0000-0000-0000-000000000001");
    private static readonly Guid Fitting = Guid.Parse("a0000002-0000-0000-0000-000000000002");
    private static readonly Guid Induction = Guid.Parse("a0000003-0000-0000-0000-000000000003");
    private static readonly Guid Escort = Guid.Parse("a0000004-0000-0000-0000-000000000004");

    private static readonly Guid London = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Proposer = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid Successor = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private static readonly DateTimeOffset Now = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly EventWindow Window = new(new DateOnly(2026, 10, 14), new TimeOnly(9, 30), 90);
    private static readonly AlwaysUniqueZones Zones = new();

    private static ProposableAppointmentType Type(Guid id, string code, bool active = true, bool managed = true) =>
        new(id, code, active, managed);

    private static EventProposal Propose(
        IReadOnlyList<ProposableAppointmentType>? listed = null,
        Guid? proposerType = null,
        int headcount = 4,
        bool locationIsActive = true,
        EventWindow? window = null) =>
        EventProposal.Propose(
            Guid.NewGuid(),
            London,
            locationIsActive,
            "Europe/London",
            window ?? Window,
            Zones,
            Now,
            listed ?? [Type(Medical, "MED"), Type(Fitting, "FIT"), Type(Induction, "IND")],
            proposerType ?? Medical,
            Proposer,
            headcount);

    [Fact]
    public void AProposalOpensWithItsListedTypesAndTheProposersOwnAcceptance()
    {
        var proposal = Propose();

        Assert.Equal(EventProposalStatus.Open, proposal.Status);
        Assert.Equal([Medical, Fitting, Induction], proposal.ListedAppointmentTypeIds.Order());
        Assert.Equal(Medical, proposal.ProposerAppointmentTypeId);
        Assert.Equal(London, proposal.LocationId);
        Assert.Single(proposal.Acceptances);
        Assert.Equal(4, proposal.Acceptances.Single().Headcount);
        Assert.False(proposal.IsFullyAccepted);
    }

    [Fact]
    public void TheLastAcceptanceCompletesTheSetAndTheEventTakesEveryHeadcount()
    {
        var proposal = Propose();

        Assert.True(proposal.Accept(Fitting, Guid.NewGuid(), 6));
        Assert.False(proposal.IsFullyAccepted);

        Assert.True(proposal.Accept(Induction, Guid.NewGuid(), 5));
        Assert.True(proposal.IsFullyAccepted);

        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        Assert.Equal(EventProposalStatus.Confirmed, proposal.Status);
        Assert.Equal(London, eventItem.LocationId);
        Assert.Equal(3, eventItem.Capacities.Count);
        Assert.Equal(4, eventItem.CapacityFor(Medical).TotalHeadcount);
        Assert.Equal(6, eventItem.CapacityFor(Fitting).TotalHeadcount);
        Assert.Equal(5, eventItem.CapacityFor(Induction).TotalHeadcount);
    }

    [Fact]
    public void ASingleTypeProposalIsFullyAcceptedAsSoonAsItIsMade()
    {
        var proposal = Propose([Type(Medical, "MED")]);

        Assert.True(proposal.IsFullyAccepted);
        Assert.Single(Event.CreateFrom(Guid.NewGuid(), proposal).Capacities);
    }

    [Fact]
    public void RevisingAnAcceptanceReportsOnlyARealChange()
    {
        var proposal = Propose();
        proposal.Accept(Fitting, Guid.NewGuid(), 6);

        Assert.False(proposal.Accept(Fitting, Guid.NewGuid(), 6));
        Assert.True(proposal.Accept(Fitting, Guid.NewGuid(), 7));
    }

    [Fact]
    public void AcceptingForATypeTheProposalDoesNotListIsRefused()
    {
        var proposal = Propose();

        Assert.Throws<DomainException>(() => proposal.Accept(Escort, Guid.NewGuid(), 3));
    }

    [Fact]
    public void TheProposersOwnAcceptanceCannotBeWithdrawn()
    {
        var proposal = Propose();

        Assert.Throws<DomainException>(() => proposal.WithdrawAcceptance(Medical));
        Assert.Single(proposal.Acceptances);
    }

    [Fact]
    public void AnotherTypesAcceptanceCanBeWithdrawnAndRecordedAgain()
    {
        var proposal = Propose();
        proposal.Accept(Fitting, Guid.NewGuid(), 6);

        proposal.WithdrawAcceptance(Fitting);
        Assert.False(proposal.IsAcceptedBy(Fitting));

        Assert.True(proposal.Accept(Fitting, Guid.NewGuid(), 2));
    }

    [Fact]
    public void OnlyTheProposersTypeMayWithdrawTheWholeProposal()
    {
        var proposal = Propose();

        Assert.Throws<DomainException>(() => proposal.Withdraw(Fitting));
        Assert.Equal(EventProposalStatus.Open, proposal.Status);

        // A successor Manager of the proposing type inherits the proposal: the rule is judged on
        // the appointment type, never on the person who created it (FR-2.10).
        proposal.Withdraw(Medical);
        Assert.Equal(EventProposalStatus.Withdrawn, proposal.Status);
        Assert.NotEqual(Successor, proposal.CreatedByManagerUserId);
    }

    [Fact]
    public void TheSweepWithdrawsAnOpenProposalOnceItsWindowHasStarted()
    {
        var proposal = Propose();

        Assert.False(proposal.TryWithdrawStarted(Zones, "Europe/London", Now));
        Assert.Equal(EventProposalStatus.Open, proposal.Status);

        var afterStart = Window.StartInstant(Zones, "Europe/London");
        Assert.True(proposal.TryWithdrawStarted(Zones, "Europe/London", afterStart));
        Assert.Equal(EventProposalStatus.Withdrawn, proposal.Status);
    }

    [Fact]
    public void EveryChangeToAProposalThatIsNoLongerOpenReportsItsCurrentStatus()
    {
        var proposal = Propose();
        proposal.Withdraw(Medical);

        var accept = Assert.Throws<ProposalNotOpenException>(() => proposal.Accept(Fitting, Guid.NewGuid(), 2));
        var withdrawAcceptance = Assert.Throws<ProposalNotOpenException>(() => proposal.WithdrawAcceptance(Fitting));
        var withdraw = Assert.Throws<ProposalNotOpenException>(() => proposal.Withdraw(Medical));

        Assert.Equal(EventProposalStatus.Withdrawn, accept.CurrentStatus);
        Assert.Equal(EventProposalStatus.Withdrawn, withdrawAcceptance.CurrentStatus);
        Assert.Equal(EventProposalStatus.Withdrawn, withdraw.CurrentStatus);
    }

    [Fact]
    public void TheListedTypesCannotBeChangedAfterCreation()
    {
        Assert.DoesNotContain(
            typeof(EventProposal).GetMethods(),
            method => method.Name.Contains("Type", StringComparison.Ordinal)
                && method.Name.StartsWith("Add", StringComparison.Ordinal));
    }

    [Fact]
    public void AProposalReportsEveryValidationFailureAtOnce()
    {
        var failure = Assert.Throws<ProposalValidationException>(() => EventProposal.Propose(
            Guid.NewGuid(),
            London,
            locationIsActive: false,
            "Europe/London",
            new EventWindow(new DateOnly(2026, 8, 1), new TimeOnly(9, 0), 60),
            Zones,
            Now,
            [Type(Medical, "MED"), Type(Medical, "MED"), Type(Fitting, "FIT", active: false), Type(Induction, "IND", managed: false)],
            Escort,
            Proposer,
            headcount: 0));

        Assert.Contains("location-inactive", failure.Failures);
        Assert.Contains("window-not-in-future", failure.Failures);
        Assert.Contains("types-duplicated", failure.Failures);
        Assert.Contains("type-inactive: FIT", failure.Failures);
        Assert.Contains("type-without-manager: IND", failure.Failures);
        Assert.Contains("proposer-type-not-listed", failure.Failures);
        Assert.Contains("headcount-out-of-range", failure.Failures);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void AHeadcountOutsideOneToAThousandIsRefused(int headcount)
    {
        Assert.Throws<ProposalValidationException>(() => Propose(headcount: headcount));
    }

    [Fact]
    public void TheListIsBoundedAtTwentyTypes()
    {
        var twenty = Enumerable.Range(0, 20)
            .Select(index => Type(Guid.NewGuid(), $"T{index:00}"))
            .ToList();
        var proposal = EventProposal.Propose(
            Guid.NewGuid(), London, true, "Europe/London", Window, Zones, Now,
            twenty, twenty[0].Id, Proposer, 3);

        Assert.Equal(20, proposal.ListedAppointmentTypeIds.Count);

        var twentyOne = twenty.Append(Type(Guid.NewGuid(), "T20")).ToList();
        Assert.Throws<ProposalValidationException>(() => EventProposal.Propose(
            Guid.NewGuid(), London, true, "Europe/London", Window, Zones, Now,
            twentyOne, twentyOne[0].Id, Proposer, 3));
    }

    [Fact]
    public void AnEmptyListIsRefused()
    {
        Assert.Throws<ProposalValidationException>(() => Propose([]));
    }

    [Fact]
    public void AWindowWithNoUniqueInstantInTheLocationsZoneIsRefused()
    {
        var failure = Assert.Throws<ProposalValidationException>(() => EventProposal.Propose(
            Guid.NewGuid(), London, true, "Europe/London", Window, new GapZones(), Now,
            [Type(Medical, "MED")], Medical, Proposer, 3));

        Assert.Contains("window-has-no-unique-instant", failure.Failures);
    }

    private sealed class AlwaysUniqueZones : IEventWindowZones
    {
        public bool IsKnownZone(string timeZoneId) => true;

        public LocalTimeValidity ValidityOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            LocalTimeValidity.Unique;

        public DateTimeOffset InstantOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            new(date.ToDateTime(time), TimeSpan.Zero);

        public DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId) =>
            DateOnly.FromDateTime(instant.UtcDateTime);

        public string AbbreviationOf(DateTimeOffset instant, string timeZoneId) => "BST";
    }

    private sealed class GapZones : IEventWindowZones
    {
        public bool IsKnownZone(string timeZoneId) => true;

        public LocalTimeValidity ValidityOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            LocalTimeValidity.Gap;

        public DateTimeOffset InstantOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            new(date.ToDateTime(time), TimeSpan.Zero);

        public DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId) =>
            DateOnly.FromDateTime(instant.UtcDateTime);

        public string AbbreviationOf(DateTimeOffset instant, string timeZoneId) => "BST";
    }
}
```

- [ ] **Step 2: Verify the tests fail**

```bash
dotnet test tests/EventBooking.Domain.Tests --filter FullyQualifiedName~NTypeNegotiationTests
```

Expected: The suite does not compile: the propose factory, the listed-type collection, the proposing type, the validation and not-open refusals, and the sweep withdrawal do not exist yet. A Docker startup failure is not the intended failure.

- [ ] **Step 3: Apply the exact implementation and regression edits**

The 25 phase-1c-edits-NNN.md files supply 84 complete before/after changes. The script validates every payload and current file before writing. It accepts an already-applied after state, refuses unrelated edits, and only deletes explicitly listed files whose before hash matches.

```bash
node --input-type=module <<'TASK_PAYLOAD'
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
const root=fs.realpathSync('.'), plan='docs/detailed-implementations';
const sha=s=>crypto.createHash('sha256').update(s).digest('hex');
const names=fs.readdirSync(plan).filter(n=>n.startsWith('phase-1c-edits-')&&n.endsWith('.md')).sort();
if(names.length!==25)throw Error('Incomplete edit volumes.');
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
if(entries.size!==84)throw Error('Incomplete operation set.');
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
dotnet ef migrations add ListedAppointmentTypes --project src/EventBooking.Infrastructure --startup-project src/EventBooking.Api
```

Review the complete migration in the edit volumes before running database-dependent tests.

- [ ] **Step 4: Verify the targeted tests pass**

```bash
dotnet test tests/EventBooking.Domain.Tests --filter FullyQualifiedName~NTypeNegotiationTests
```

Expected: all targeted cases pass, with zero skipped tests.

- [ ] **Step 5: Build and validate the complete solution**

```bash
dotnet build EventBooking.sln -warnaserror
dotnet test EventBooking.sln
```

Expected: zero warnings, zero errors and zero failed or skipped tests. The verified checkpoint contains 1484 tests: Domain 306, Application 422, Infrastructure 173, API 232, MCP 35, Web 241 and SeedData 75.

- [ ] **Step 6: Commit and push**

`proposerAppointmentTypeId` is already in `docs/ontology.ttl`, added with its invariant when this rule was settled. No further ontology change belongs to this task; if you find one, edit the source and regenerate before committing.

```bash
git add -- \
  'src/EventBooking.Application/Events/AcceptProposalHandler.cs' \
  'src/EventBooking.Application/Events/ProposeEventHandler.cs' \
  'src/EventBooking.Application/Events/TransitionalLocation.cs' \
  'src/EventBooking.Application/Events/WithdrawAcceptanceHandler.cs' \
  'src/EventBooking.Application/Events/WithdrawProposalHandler.cs' \
  'src/EventBooking.Domain/Events/Event.cs' \
  'src/EventBooking.Domain/Events/EventProposal.cs' \
  'src/EventBooking.Domain/Events/EventProposalAppointmentType.cs' \
  'src/EventBooking.Domain/Events/ProposalNegotiation.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/EventProposalAppointmentTypeConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/EventProposalConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920073816_ListedAppointmentTypes.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920073816_ListedAppointmentTypes.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/EventBookingDbContextModelSnapshot.cs' \
  'src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs' \
  'src/EventBooking.SeedData/DemoEventFactory.cs' \
  'tests/EventBooking.Api.Tests/AttendeeEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/BookingEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/DashboardEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/EventBooking.Api.Tests.csproj' \
  'tests/EventBooking.Api.Tests/EventCapacityAdjustmentEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/EventEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/Fixtures/EventFixture.cs' \
  'tests/EventBooking.Api.Tests/ManageBookingEndpointTests.cs' \
  'tests/EventBooking.Application.Tests/Attendees/DeleteAttendeeHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/CancelAttendeeBookingHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/CancelBookingHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/ConfirmBookingHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/InviteOptionReplacementTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/ViewInviteHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/EventBooking.Application.Tests.csproj' \
  'tests/EventBooking.Application.Tests/Events/AcceptProposalHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/AcceptProposalHeadcountRevisionTests.cs' \
  'tests/EventBooking.Application.Tests/Events/AdjustEventCapacityHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/CombinedManagerAuthorizationTests.cs' \
  'tests/EventBooking.Application.Tests/Events/EventCancellationConcurrencyTests.cs' \
  'tests/EventBooking.Application.Tests/Events/GetManagerEventBoardHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/ManagerEventBoardHeadcountRevisionTests.cs' \
  'tests/EventBooking.Application.Tests/Events/ProposeEventHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/WithdrawAcceptanceHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/WithdrawProposalHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs' \
  'tests/EventBooking.Application.Tests/Fixtures/EventFixture.cs' \
  'tests/EventBooking.Application.Tests/Invites/EligibleEventFinderTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/InviteIssuerTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/RecoveryInviteHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/TriggerInviteHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Notifications/AttendeeEmailComposerTests.cs' \
  'tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs' \
  'tests/EventBooking.Domain.Tests/EventBooking.Domain.Tests.csproj' \
  'tests/EventBooking.Domain.Tests/Events/EventCancellationTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/EventCapacityHeadcountAdjustmentTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/EventCapacityTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/EventProposalAcceptanceTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/EventProposalConfirmationTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/EventProposalTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/EventTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/NTypeNegotiationTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/ProposalAcceptanceHeadcountRevisionTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/AttendeeBookingQueryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyHarness.cs' \
  'tests/EventBooking.Infrastructure.Tests/ConcurrencyHarness.cs' \
  'tests/EventBooking.Infrastructure.Tests/DashboardQueryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/EventBooking.Infrastructure.Tests.csproj' \
  'tests/EventBooking.Infrastructure.Tests/EventCapacityRepositoryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/Fixtures/EventFixture.cs' \
  'tests/EventBooking.Infrastructure.Tests/RepairCConcurrencyTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/RepositoryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/SchemaTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs' \
  'tests/EventBooking.Mcp.Tests/EventBooking.Mcp.Tests.csproj' \
  'tests/EventBooking.Mcp.Tests/Fixtures/EventFixture.cs' \
  'tests/EventBooking.SeedData.Tests/DemoInvitationHostTests.cs' \
  'tests/EventBooking.SeedData.Tests/DemoInvitationSeederTests.cs' \
  'tests/EventBooking.SeedData.Tests/EventBooking.SeedData.Tests.csproj' \
  'tests/EventBooking.SeedData.Tests/ReanchorTests.cs' \
  'tests/EventBooking.SeedData.Tests/ReseedTests.cs' \
  'tests/EventBooking.Web.Tests/EventBooking.Web.Tests.csproj' \
  'tests/TestSupport/ProposalFixture.cs'
git diff --cached --name-only
git diff --cached
node scripts/check-ontology-terms.mjs
: "${EXECUTOR_COAUTHOR:?Set the executing harness co-author identity}"
git commit -m "feat(domain): negotiate an EventProposal across any number of types" -m "Co-authored-by: $EXECUTOR_COAUTHOR"
git push -u origin HEAD
```

Continue to Task 7 on the same branch.
