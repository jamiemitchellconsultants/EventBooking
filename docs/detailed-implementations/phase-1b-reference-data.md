# 01b — Admin-managed reference data (Task 5)

[← Phase overview](phase-1-domain.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This task follows Task 4 on the Phase 1 branch. It turns the predecessor's fixed reference data into Admin-managed aggregates: a new `Location`, an `AppointmentType` that can be created and retired, an `AttendeeGroup` whose mapping set can be replaced, and settings that carry the option count.

> Use superpowers:executing-plans. Complete changed types and exact before/after files are embedded
> in the numbered companion volumes; apply them with the script in Step 3, never by hand.

**Goal:** Reference data is created, renamed, deactivated and reactivated by an Admin, is never hard-deleted, and refuses any change that would move live scheduling.

**Architecture:** Aggregates decide; callers count. Each refusal carries the counts that blocked it, so a screen can name them. The application handlers that supply those counts arrive in Task 12; this task proves the rules in the domain.

**Tech Stack:** .NET 10, xUnit, EF Core, PostgreSQL Testcontainers, bUnit.

**Spec:** [Master Task 5](../superpowers/plans/2026-09-19-eventbooking-implementation.md), [domain model](../design/01-domain-model.md), [functional requirements](../design/02-functional-requirements.md), [ontology](../ontology.md).

## Global constraints

Stay on the Phase 1 branch. Do not wire new screens, endpoints or MCP tools in this task. Codes are immutable and canonical uppercase snake case. Nothing in reference data is ever deleted. Keep the inherited seeded rows working: they are retired in Phase 3, not here.

## Review focus

STOP AND CHECK: the migration's backfill values, not just its columns. Existing appointment types are active, every concurrency token starts at 1, and the option count starts at 3; EF's generated false-and-zero defaults would misdescribe rows that already exist, and the column defaults are dropped afterwards so new rows must state their own values. Check too that a requirement change which leaves the set identical is allowed even when members hold bookings, and that a real change with blocking members changes nothing at all.

### Task 5: Admin-managed reference data

**Files:**

- Modify: src/EventBooking.Application/Settings/AdminSettingsHandler.cs
- Modify: src/EventBooking.Domain/AppointmentTypes/AppointmentType.cs
- Create: src/EventBooking.Domain/AppointmentTypes/AppointmentTypeUsage.cs
- Modify: src/EventBooking.Domain/AttendeeGroups/AttendeeGroup.cs
- Modify: src/EventBooking.Domain/Common/DomainException.cs
- Create: src/EventBooking.Domain/Common/ReferenceDataCode.cs
- Create: src/EventBooking.Domain/Common/ReferenceDataInUseException.cs
- Create: src/EventBooking.Domain/Locations/Location.cs
- Create: src/EventBooking.Domain/Locations/LocationUsage.cs
- Modify: src/EventBooking.Domain/Settings/SystemSettings.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/AppointmentTypeConfiguration.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeGroupConfiguration.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/SystemSettingsConfiguration.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260920072159_ManagedReferenceData.Designer.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260920072159_ManagedReferenceData.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/EventBookingDbContextModelSnapshot.cs
- Test: tests/EventBooking.Domain.Tests/AppointmentTypes/ManagedAppointmentTypeTests.cs
- Test: tests/EventBooking.Domain.Tests/AttendeeGroups/ManagedAttendeeGroupTests.cs
- Test: tests/EventBooking.Domain.Tests/Locations/LocationTests.cs
- Test: tests/EventBooking.Domain.Tests/Settings/SystemSettingsBoundsTests.cs
- Modify: tests/EventBooking.Domain.Tests/Settings/SystemSettingsTests.cs (delete)
- Modify: tests/EventBooking.Infrastructure.Tests/RepositoryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/SchemaTests.cs

**Interfaces:**

These complete types define the changed public boundary and its domain behavior. Apply them with the after files at Step 3, not before the failing test.

```csharp
using EventBooking.Domain.Common;
using EventBooking.Domain.Time;

namespace EventBooking.Domain.Locations;

/// <summary>
/// An Admin-managed site where events happen, with its own address and IANA time zone. Every
/// window at the site is read in that zone, so the zone cannot move while anything is scheduled.
/// </summary>
public sealed class Location
{
    /// <summary>The longest code a location may have (design 08).</summary>
    public const int MaximumCodeLength = 50;

    /// <summary>The longest name a location may have (design 08).</summary>
    public const int MaximumNameLength = 100;

    /// <summary>The longest address a location may have (design 08).</summary>
    public const int MaximumAddressLength = 500;

    private Location()
    {
        Code = string.Empty;
        Name = string.Empty;
        Address = string.Empty;
        TimeZoneId = string.Empty;
    }

    /// <summary>The stable reference-data identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>The immutable canonical uppercase snake-case code.</summary>
    public string Code { get; private set; }

    /// <summary>The staff-facing display name.</summary>
    public string Name { get; private set; }

    /// <summary>The postal address shown to attendees.</summary>
    public string Address { get; private set; }

    /// <summary>The IANA zone every window at this location is read in.</summary>
    public string TimeZoneId { get; private set; }

    /// <summary>Whether new proposals may name this location.</summary>
    public bool IsActive { get; private set; }

    /// <summary>The optimistic-concurrency token.</summary>
    public long Version { get; private set; }

    /// <summary>Creates an active location.</summary>
    /// <param name="id">The new identifier.</param>
    /// <param name="code">The code, in any case.</param>
    /// <param name="name">The display name.</param>
    /// <param name="address">The postal address.</param>
    /// <param name="timeZoneId">The IANA zone identifier.</param>
    /// <param name="zones">The zone abstraction that says whether the zone exists.</param>
    public static Location Create(
        Guid id, string? code, string? name, string? address, string? timeZoneId, IEventWindowZones zones)
    {
        ArgumentNullException.ThrowIfNull(zones);
        Guard.Against(id == Guid.Empty, "id must not be empty.");

        var canonicalCode = ReferenceDataCode.Parse(code, MaximumCodeLength, "code");
        var displayName = Bounded(name, MaximumNameLength, "name");
        var postalAddress = Bounded(address, MaximumAddressLength, "address");
        var zone = KnownZone(timeZoneId, zones);

        return new Location
        {
            Id = id,
            Code = canonicalCode,
            Name = displayName,
            Address = postalAddress,
            TimeZoneId = zone,
            IsActive = true,
            Version = 1,
        };
    }

    /// <summary>Renames the location. Always allowed.</summary>
    /// <param name="name">The new display name.</param>
    public void Rename(string? name)
    {
        Name = Bounded(name, MaximumNameLength, "name");
        Version++;
    }

    /// <summary>Changes the postal address. Always allowed.</summary>
    /// <param name="address">The new address.</param>
    public void ChangeAddress(string? address)
    {
        Address = Bounded(address, MaximumAddressLength, "address");
        Version++;
    }

    /// <summary>
    /// Moves the location to another zone. Refused while anything is scheduled, because it would
    /// silently move the wall-clock time of every window already agreed (FR-1.2).
    /// </summary>
    /// <param name="timeZoneId">The new IANA zone identifier.</param>
    /// <param name="zones">The zone abstraction that says whether the zone exists.</param>
    /// <param name="usage">What the location currently carries.</param>
    public void ChangeTimeZone(string? timeZoneId, IEventWindowZones zones, LocationUsage usage)
    {
        ArgumentNullException.ThrowIfNull(zones);
        var zone = KnownZone(timeZoneId, zones);

        if (zone == TimeZoneId)
        {
            return;
        }

        if (usage.Any)
        {
            throw new ReferenceDataInUseException(
                "The time zone cannot change while the location has open proposals or future events.",
                usage.AsBlocking());
        }

        TimeZoneId = zone;
        Version++;
    }

    /// <summary>Deactivates the location, refusing while it is in live use (FR-1.6).</summary>
    /// <param name="usage">What the location currently carries.</param>
    public void Deactivate(LocationUsage usage)
    {
        if (usage.Any)
        {
            throw new ReferenceDataInUseException(
                "The location cannot be deactivated while it has open proposals or future events.",
                usage.AsBlocking());
        }

        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        Version++;
    }

    /// <summary>Returns the location to use.</summary>
    public void Reactivate()
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        Version++;
    }

    private static string Bounded(string? value, int maximumLength, string field)
    {
        var trimmed = Guard.NotBlank(value, field);
        Guard.Against(trimmed.Length > maximumLength, $"{field} must be at most {maximumLength} characters.");
        return trimmed;
    }

    private static string KnownZone(string? timeZoneId, IEventWindowZones zones)
    {
        var zone = Guard.NotBlank(timeZoneId, "timeZoneId");
        Guard.Against(!zones.IsKnownZone(zone), $"timeZoneId '{zone}' is not a known IANA time zone.");
        return zone;
    }
}
```

```csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.AppointmentTypes;

/// <summary>
/// One kind of appointment, run by at most one current Manager across every location. Admin-managed
/// reference data: created, renamed and deactivated, never deleted (FR-1.3, FR-1.6, FR-1.7).
/// </summary>
public sealed class AppointmentType
{
    /// <summary>The longest code an appointment type may have (design 08).</summary>
    public const int MaximumCodeLength = 50;

    /// <summary>The longest name an appointment type may have (design 08).</summary>
    public const int MaximumNameLength = 100;

    private AppointmentType()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    /// <summary>The stable reference-data identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>The immutable canonical uppercase snake-case code.</summary>
    public string Code { get; private set; }

    /// <summary>The staff-facing display name.</summary>
    public string Name { get; private set; }

    /// <summary>Whether new proposals and groups may name this type.</summary>
    public bool IsActive { get; private set; }

    /// <summary>The optimistic-concurrency token.</summary>
    public long Version { get; private set; }

    /// <summary>Creates an active appointment type.</summary>
    /// <param name="id">The new identifier.</param>
    /// <param name="code">The code, in any case.</param>
    /// <param name="name">The display name.</param>
    public static AppointmentType Create(Guid id, string? code, string? name)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        var canonicalCode = ReferenceDataCode.Parse(code, MaximumCodeLength, "code");
        var displayName = BoundedName(name);

        return new AppointmentType
        {
            Id = id,
            Code = canonicalCode,
            Name = displayName,
            IsActive = true,
            Version = 1,
        };
    }

    /// <summary>Renames the type. Always allowed; the code never changes.</summary>
    /// <param name="name">The new display name.</param>
    public void Rename(string? name)
    {
        Name = BoundedName(name);
        Version++;
    }

    /// <summary>Deactivates the type, refusing while it is in live use (FR-1.6).</summary>
    /// <param name="usage">What the type currently carries.</param>
    public void Deactivate(AppointmentTypeUsage usage)
    {
        if (usage.Any)
        {
            throw new ReferenceDataInUseException(
                "The appointment type cannot be deactivated while it is listed or mapped.",
                usage.AsBlocking());
        }

        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        Version++;
    }

    /// <summary>Returns the type to use.</summary>
    public void Reactivate()
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        Version++;
    }

    /// <summary>
    /// The predecessor's three seeded types, retained only until Phase 3 rewires seeding and the
    /// handlers onto Admin-managed types.
    /// </summary>
    public static IReadOnlyList<AppointmentType> CreateFixedSet() =>
        AppointmentTypeIds.All
            .Select(id => new AppointmentType
            {
                Id = id,
                Code = AppointmentTypeIds.CodeOf(id),
                Name = AppointmentTypeIds.NameOf(id),
                IsActive = true,
                Version = 1,
            })
            .ToList();

    private static string BoundedName(string? name)
    {
        var displayName = Guard.NotBlank(name, "name");
        Guard.Against(
            displayName.Length > MaximumNameLength,
            $"name must be at most {MaximumNameLength} characters.");
        return displayName;
    }
}
```

```csharp
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
```

**Context you need**

- FR-1.1 (quoted): the system shall let an Admin create a `Location` with `code`, `name`, `address` and `timeZoneId`, validating that “`code` is unique canonical uppercase snake case, at most 50 characters” and “`timeZoneId` is a valid IANA zone”.
- FR-1.2 (quoted): an Admin may edit a `Location`'s `name` and `address` at any time, and the system “shall refuse a `timeZoneId` change while the location hosts an `Open` `EventProposal` or a future `Active` `Event`, returning the count of each”.
- FR-1.3 and FR-1.4: an Admin creates and renames an `AppointmentType` (code at most 50, name at most 100) and an `AttendeeGroup`, and replaces the group's `AttendeeGroupRequirement` set, which “must contain at least one active, non-duplicated `AppointmentType`”.
- FR-1.5: a real requirement change is refused while any member holds an active original `Booking`, “unless the new set is identical to the old one”, and the refusal returns the number of blocking members.
- FR-1.6 and design 01 (Reference data): deactivation is refused while a `Location` hosts an open proposal or future event, while an `AppointmentType` is listed on either or mapped by an active group, and while an `AttendeeGroup` has members.
- FR-1.7 (quoted): “The system shall never hard-delete reference data. Codes shall be immutable after creation.”
- FR-1.8: every reference-data and settings write is optimistic-concurrency-checked against `version`.
- FR-1.9 and design 08: `inviteExpiryDays` is 1 to 60 with a default of 7, `maxAutoRetryCount` is 0 to 10 with a default of 2, and `inviteOptionCount` is 1 to 5 with a default of 3. The predecessor's four-day default is replaced.
- Design 01 (Reference data): codes “are accepted case-insensitively on input and used as stable identifiers in CSV files, MCP tools and email ordering”, so they are normalised to uppercase on the way in.
- Design 08 bounds the lengths: `Location` code 50, name 100, address 500; `AppointmentType` code 50, name 100; `AttendeeGroup` code 50, name 200.
- The ontology already defines `Location`, `AppointmentType` and `AttendeeGroup` with `isActive` and `version`, and `SystemSettings` with `inviteOptionCount`. This task implements those definitions and adds no concept.
- The `AttendeeGroup` mapping is validated against the set of active appointment types the caller supplies, because types are no longer a fixed list the domain can know by itself.

- [ ] **Step 1: Write the failing tests**

Create these complete files before applying production changes.

tests/EventBooking.Domain.Tests/Locations/LocationTests.cs

```csharp
using EventBooking.Domain.Common;
using EventBooking.Domain.Locations;
using EventBooking.Domain.Time;

namespace EventBooking.Domain.Tests.Locations;

/// <summary>
/// Task 5: a Location is Admin-managed reference data. Its code is immutable, its zone cannot move
/// under live events, and nothing is ever hard-deleted (design 01 — Reference data, FR-1.1 to FR-1.7).
/// </summary>
public class LocationTests
{
    private static readonly KnownZones Zones = new();

    private static Location Create(string code = "LONDON_HQ", string zone = "Europe/London") =>
        Location.Create(Guid.NewGuid(), code, "London HQ", "1 Example St, London", zone, Zones);

    [Fact]
    public void ACreatedLocationCarriesItsDetailsAndStartsActiveAtVersionOne()
    {
        var location = Create();

        Assert.Equal("LONDON_HQ", location.Code);
        Assert.Equal("London HQ", location.Name);
        Assert.Equal("1 Example St, London", location.Address);
        Assert.Equal("Europe/London", location.TimeZoneId);
        Assert.True(location.IsActive);
        Assert.Equal(1, location.Version);
    }

    [Theory]
    [InlineData("london_hq", "LONDON_HQ")]
    [InlineData("  dublin  ", "DUBLIN")]
    public void ACodeIsAcceptedCaseInsensitivelyAndStoredCanonically(string input, string stored)
    {
        Assert.Equal(stored, Create(input).Code);
    }

    [Theory]
    [InlineData("1LONDON")]
    [InlineData("LONDON-HQ")]
    [InlineData("LONDON HQ")]
    [InlineData("")]
    public void AnUncanonicalCodeIsRefused(string code)
    {
        Assert.Throws<DomainException>(() => Create(code));
    }

    [Fact]
    public void AnUnknownTimeZoneIsRefused()
    {
        Assert.Throws<DomainException>(() => Create(zone: "Mars/Olympus_Mons"));
    }

    [Fact]
    public void TheCodeCannotBeChangedAfterCreation()
    {
        Assert.Null(typeof(Location).GetProperty(nameof(Location.Code))!.SetMethod?.IsPublic == true ? "settable" : null);
    }

    [Fact]
    public void RenamingAndReaddressingAreAlwaysAllowedAndBumpTheVersion()
    {
        var location = Create();

        location.Rename("London head office");
        location.ChangeAddress("2 Sample Rd, London");

        Assert.Equal("London head office", location.Name);
        Assert.Equal("2 Sample Rd, London", location.Address);
        Assert.Equal(3, location.Version);
    }

    [Fact]
    public void TheZoneMayChangeWhileNothingIsScheduled()
    {
        var location = Create();

        location.ChangeTimeZone("Europe/Dublin", Zones, LocationUsage.None);

        Assert.Equal("Europe/Dublin", location.TimeZoneId);
    }

    [Fact]
    public void TheZoneCannotMoveUnderOpenProposalsOrFutureEvents()
    {
        var location = Create();

        var refusal = Assert.Throws<ReferenceDataInUseException>(
            () => location.ChangeTimeZone("Europe/Dublin", Zones, new LocationUsage(1, 2)));

        Assert.Equal("Europe/London", location.TimeZoneId);
        Assert.Equal(1, refusal.Blocking["openProposals"]);
        Assert.Equal(2, refusal.Blocking["futureEvents"]);
    }

    [Fact]
    public void DeactivationIsRefusedWhileTheLocationIsInUseAndAllowedOtherwise()
    {
        var location = Create();

        Assert.Throws<ReferenceDataInUseException>(() => location.Deactivate(new LocationUsage(0, 3)));
        Assert.True(location.IsActive);

        location.Deactivate(LocationUsage.None);
        Assert.False(location.IsActive);

        location.Reactivate();
        Assert.True(location.IsActive);
    }

    [Fact]
    public void NameAndAddressLengthsAreBounded()
    {
        var location = Create();

        Assert.Throws<DomainException>(() => location.Rename(new string('x', 101)));
        Assert.Throws<DomainException>(() => location.ChangeAddress(new string('x', 501)));
    }

    private sealed class KnownZones : IEventWindowZones
    {
        public bool IsKnownZone(string timeZoneId) =>
            timeZoneId is "Europe/London" or "Europe/Dublin" or "Asia/Tokyo";

        public LocalTimeValidity ValidityOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            LocalTimeValidity.Unique;

        public DateTimeOffset InstantOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            new(date.ToDateTime(time), TimeSpan.Zero);

        public DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId) =>
            DateOnly.FromDateTime(instant.UtcDateTime);

        public string AbbreviationOf(DateTimeOffset instant, string timeZoneId) => "BST";
    }
}
```

tests/EventBooking.Domain.Tests/AppointmentTypes/ManagedAppointmentTypeTests.cs

```csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Tests.AppointmentTypes;

/// <summary>
/// Task 5: appointment types become Admin-managed reference data rather than a fixed set
/// (FR-1.3, FR-1.6, FR-1.7; design 01 — Reference data).
/// </summary>
public class ManagedAppointmentTypeTests
{
    private static AppointmentType Create(string code = "MED", string name = "Medical check") =>
        AppointmentType.Create(Guid.NewGuid(), code, name);

    [Fact]
    public void ACreatedTypeIsActiveAtVersionOneWithACanonicalCode()
    {
        var type = Create("med");

        Assert.Equal("MED", type.Code);
        Assert.Equal("Medical check", type.Name);
        Assert.True(type.IsActive);
        Assert.Equal(1, type.Version);
    }

    [Theory]
    [InlineData("1MED")]
    [InlineData("MED-1")]
    [InlineData(" ")]
    public void AnUncanonicalCodeIsRefused(string code)
    {
        Assert.Throws<DomainException>(() => Create(code));
    }

    [Fact]
    public void RenamingBumpsTheVersionAndBoundsTheName()
    {
        var type = Create();

        type.Rename("Medical screening");

        Assert.Equal("Medical screening", type.Name);
        Assert.Equal(2, type.Version);
        Assert.Throws<DomainException>(() => type.Rename(new string('x', 101)));
    }

    [Fact]
    public void DeactivationNamesEveryBlockingUse()
    {
        var type = Create();

        var refusal = Assert.Throws<ReferenceDataInUseException>(
            () => type.Deactivate(new AppointmentTypeUsage(2, 1, 3)));

        Assert.True(type.IsActive);
        Assert.Equal(2, refusal.Blocking["openProposals"]);
        Assert.Equal(1, refusal.Blocking["futureEvents"]);
        Assert.Equal(3, refusal.Blocking["activeGroups"]);
    }

    [Fact]
    public void AnUnusedTypeDeactivatesAndReactivates()
    {
        var type = Create();

        type.Deactivate(AppointmentTypeUsage.None);
        Assert.False(type.IsActive);

        type.Reactivate();
        Assert.True(type.IsActive);
        Assert.Equal(3, type.Version);
    }
}
```

tests/EventBooking.Domain.Tests/AttendeeGroups/ManagedAttendeeGroupTests.cs

```csharp
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Tests.AttendeeGroups;

/// <summary>
/// Task 5: an attendee group's mapping set is Admin-managed, and a requirement change is refused
/// while any member holds an active original booking (FR-1.4 to FR-1.6; design 01 — Requirements).
/// </summary>
public class ManagedAttendeeGroupTests
{
    private static readonly Guid Medical = Guid.Parse("a0000001-0000-0000-0000-000000000001");
    private static readonly Guid Fitting = Guid.Parse("a0000002-0000-0000-0000-000000000002");
    private static readonly Guid Induction = Guid.Parse("a0000003-0000-0000-0000-000000000003");

    private static readonly IReadOnlyCollection<Guid> ActiveTypes = [Medical, Fitting, Induction];

    private static AttendeeGroup Group(params Guid[] typeIds) =>
        AttendeeGroup.Create(Guid.NewGuid(), "field_staff", "Field staff", typeIds, ActiveTypes);

    [Fact]
    public void ACreatedGroupIsActiveAtVersionOneWithACanonicalCode()
    {
        var group = Group(Medical, Induction);

        Assert.Equal("FIELD_STAFF", group.Code);
        Assert.True(group.IsActive);
        Assert.Equal(1, group.Version);
        Assert.Equal([Medical, Induction], group.RequiredAppointmentTypeIds.Order());
    }

    [Fact]
    public void AMappingMustNameAtLeastOneActiveTypeAndNoDuplicates()
    {
        Assert.Throws<DomainException>(() => Group());
        Assert.Throws<DomainException>(() => Group(Medical, Medical));
        Assert.Throws<DomainException>(() =>
            AttendeeGroup.Create(Guid.NewGuid(), "x_group", "X", [Guid.NewGuid()], ActiveTypes));
    }

    [Fact]
    public void ReplacingTheSetWithTheSameTypesInAnotherOrderChangesNothing()
    {
        var group = Group(Medical, Induction);

        var changed = group.ReplaceRequirements([Induction, Medical], ActiveTypes, blockingMembers: 4);

        Assert.False(changed);
        Assert.Equal(1, group.Version);
    }

    [Fact]
    public void ARealRequirementChangeIsRefusedWhileMembersHoldActiveBookings()
    {
        var group = Group(Medical);

        var refusal = Assert.Throws<ReferenceDataInUseException>(
            () => group.ReplaceRequirements([Medical, Fitting], ActiveTypes, blockingMembers: 2));

        Assert.Equal(2, refusal.Blocking["blockingMembers"]);
        Assert.Equal([Medical], group.RequiredAppointmentTypeIds);
        Assert.Equal(1, group.Version);
    }

    [Fact]
    public void ARealRequirementChangeWithNoBlockingMembersIsApplied()
    {
        var group = Group(Medical);

        var changed = group.ReplaceRequirements([Fitting, Induction], ActiveTypes, blockingMembers: 0);

        Assert.True(changed);
        Assert.Equal([Fitting, Induction], group.RequiredAppointmentTypeIds.Order());
        Assert.Equal(2, group.Version);
    }

    [Fact]
    public void DeactivationIsRefusedWhileTheGroupHasMembers()
    {
        var group = Group(Medical);

        var refusal = Assert.Throws<ReferenceDataInUseException>(() => group.Deactivate(memberCount: 7));
        Assert.Equal(7, refusal.Blocking["members"]);
        Assert.True(group.IsActive);

        group.Deactivate(memberCount: 0);
        Assert.False(group.IsActive);

        group.Reactivate();
        Assert.True(group.IsActive);
    }

    [Fact]
    public void RenamingBumpsTheVersion()
    {
        var group = Group(Medical);

        group.Rename("Field crew");

        Assert.Equal("Field crew", group.Name);
        Assert.Equal(2, group.Version);
    }
}
```

tests/EventBooking.Domain.Tests/Settings/SystemSettingsBoundsTests.cs

```csharp
using EventBooking.Domain.Common;
using EventBooking.Domain.Settings;

namespace EventBooking.Domain.Tests.Settings;

/// <summary>
/// Task 5: settings gain the option count, and every value takes the design's bounds
/// (FR-1.9; design 08 — boundary values).
/// </summary>
public class SystemSettingsBoundsTests
{
    [Fact]
    public void TheDefaultsMatchTheDesign()
    {
        var settings = SystemSettings.CreateDefault();

        Assert.Equal(7, settings.InviteExpiryDays);
        Assert.Equal(2, settings.MaxAutoRetryCount);
        Assert.Equal(3, settings.InviteOptionCount);
    }

    [Theory]
    [InlineData(1, 0, 1)]
    [InlineData(60, 10, 5)]
    public void ValuesInsideTheBoundsAreAccepted(int expiryDays, int retries, int options)
    {
        var settings = SystemSettings.CreateDefault();

        settings.Update(expiryDays, retries, options);

        Assert.Equal(expiryDays, settings.InviteExpiryDays);
        Assert.Equal(retries, settings.MaxAutoRetryCount);
        Assert.Equal(options, settings.InviteOptionCount);
    }

    [Theory]
    [InlineData(0, 2, 3)]
    [InlineData(61, 2, 3)]
    [InlineData(7, -1, 3)]
    [InlineData(7, 11, 3)]
    [InlineData(7, 2, 0)]
    [InlineData(7, 2, 6)]
    public void ValuesOutsideTheBoundsAreRefusedWithoutChangingAnything(int expiryDays, int retries, int options)
    {
        var settings = SystemSettings.CreateDefault();

        Assert.Throws<DomainException>(() => settings.Update(expiryDays, retries, options));

        Assert.Equal(7, settings.InviteExpiryDays);
        Assert.Equal(2, settings.MaxAutoRetryCount);
        Assert.Equal(3, settings.InviteOptionCount);
    }
}
```

- [ ] **Step 2: Verify the tests fail**

```bash
dotnet test tests/EventBooking.Domain.Tests --filter FullyQualifiedName~LocationTests
dotnet test tests/EventBooking.Domain.Tests --filter FullyQualifiedName~ManagedAppointmentTypeTests
dotnet test tests/EventBooking.Domain.Tests --filter FullyQualifiedName~ManagedAttendeeGroupTests
dotnet test tests/EventBooking.Domain.Tests --filter FullyQualifiedName~SystemSettingsBoundsTests
```

Expected: None of the four suites compiles: Location, the managed create and deactivate methods, the usage records, the in-use refusal and the three-argument settings update do not exist yet. A Docker startup failure is not the intended failure.

- [ ] **Step 3: Apply the exact implementation and regression edits**

The 5 phase-1b-edits-NNN.md files supply 23 complete before/after changes. The script validates every payload and current file before writing. It accepts an already-applied after state, refuses unrelated edits, and only deletes explicitly listed files whose before hash matches.

```bash
node --input-type=module <<'TASK_PAYLOAD'
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
const root=fs.realpathSync('.'), plan='docs/detailed-implementations';
const sha=s=>crypto.createHash('sha256').update(s).digest('hex');
const names=fs.readdirSync(plan).filter(n=>n.startsWith('phase-1b-edits-')&&n.endsWith('.md')).sort();
if(names.length!==5)throw Error('Incomplete edit volumes.');
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
if(entries.size!==23)throw Error('Incomplete operation set.');
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
dotnet ef migrations add ManagedReferenceData --project src/EventBooking.Infrastructure --startup-project src/EventBooking.Api
```

Review the complete migration in the edit volumes before running database-dependent tests.

- [ ] **Step 4: Verify the targeted tests pass**

```bash
dotnet test tests/EventBooking.Domain.Tests --filter FullyQualifiedName~LocationTests
dotnet test tests/EventBooking.Domain.Tests --filter FullyQualifiedName~ManagedAppointmentTypeTests
dotnet test tests/EventBooking.Domain.Tests --filter FullyQualifiedName~ManagedAttendeeGroupTests
dotnet test tests/EventBooking.Domain.Tests --filter FullyQualifiedName~SystemSettingsBoundsTests
```

Expected: all targeted cases pass, with zero skipped tests.

- [ ] **Step 5: Build and validate the complete solution**

```bash
dotnet build EventBooking.sln -warnaserror
dotnet test EventBooking.sln
```

Expected: zero warnings, zero errors and zero failed or skipped tests. The verified checkpoint contains 1467 tests: Domain 289, Application 422, Infrastructure 173, API 232, MCP 35, Web 241 and SeedData 75.

- [ ] **Step 6: Commit and push**

The ontology already carries `isActive` and `version` on each reference-data entity and `inviteOptionCount` on `SystemSettings`, so no ontology source change belongs to this task.

```bash
git add -- \
  'src/EventBooking.Application/Settings/AdminSettingsHandler.cs' \
  'src/EventBooking.Domain/AppointmentTypes/AppointmentType.cs' \
  'src/EventBooking.Domain/AppointmentTypes/AppointmentTypeUsage.cs' \
  'src/EventBooking.Domain/AttendeeGroups/AttendeeGroup.cs' \
  'src/EventBooking.Domain/Common/DomainException.cs' \
  'src/EventBooking.Domain/Common/ReferenceDataCode.cs' \
  'src/EventBooking.Domain/Common/ReferenceDataInUseException.cs' \
  'src/EventBooking.Domain/Locations/Location.cs' \
  'src/EventBooking.Domain/Locations/LocationUsage.cs' \
  'src/EventBooking.Domain/Settings/SystemSettings.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/AppointmentTypeConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeGroupConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/SystemSettingsConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920072159_ManagedReferenceData.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920072159_ManagedReferenceData.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/EventBookingDbContextModelSnapshot.cs' \
  'tests/EventBooking.Domain.Tests/AppointmentTypes/ManagedAppointmentTypeTests.cs' \
  'tests/EventBooking.Domain.Tests/AttendeeGroups/ManagedAttendeeGroupTests.cs' \
  'tests/EventBooking.Domain.Tests/Locations/LocationTests.cs' \
  'tests/EventBooking.Domain.Tests/Settings/SystemSettingsBoundsTests.cs' \
  'tests/EventBooking.Domain.Tests/Settings/SystemSettingsTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/RepositoryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/SchemaTests.cs'
git diff --cached --name-only
git diff --cached
node scripts/check-ontology-terms.mjs
: "${EXECUTOR_COAUTHOR:?Set the executing harness co-author identity}"
git commit -m "feat(domain): Admin-managed Location, AppointmentType and AttendeeGroup" -m "Co-authored-by: $EXECUTOR_COAUTHOR"
git push -u origin HEAD
```

Continue to Task 6 on the same branch.
