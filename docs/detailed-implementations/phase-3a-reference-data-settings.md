# 03a — Reference-data and settings handlers (Task 12)

[← Phase overview](phase-3-application.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This task is the first of Phase 3. It puts the Admin-managed reference data behind handlers:
creating, updating, deactivating and reactivating `Location`, `AppointmentType` and `AttendeeGroup`
rows, listing each for staff, replacing a group's requirement set with its member re-derivation,
and saving the singleton `SystemSettings` — including the newly editable `inviteOptionCount`.

> Use superpowers:executing-plans. This task is hand-authored: complete code and complete tests
> are written straight into this document, with no prototype. Compile and test-drive them
> yourself. The test counts below are what you should expect to reach, not figures observed by
> the author — nothing here has been run.

**Goal:** Every reference-data write runs in one transaction (a single save; no explicit
transaction is needed because no capacity rows are touched), calls the Task 5 domain methods,
writes exactly one audit entry in the transaction, and returns a result DTO carrying the
updated version. Writes demand `ManageReferenceData` (new capability, Admin-only, following
the `ManageSettings` row in the authorizer matrix); settings demand `ManageSettings`; reads
demand no capability and are open to any staff member.

**Architecture:** Blocking counts (open proposals, future active events, mapped groups, member
counts) come from the new IReferenceDataBlockingQueries port in
`src/EventBooking.Infrastructure/Persistence/Queries/ReferenceDataBlockingQueries.cs`,
implemented once against PostgreSQL with LINQ over the DbContext sets. Replacing a group's
requirement set re-derives every member through AssignAttendeeGroup, supersedes each
member's pending initial invite, and moves affected members to `NotYetInvited` — all in the
one save, with each member's attendee lock taken in ascending id order first via the new
LockByGroupForUpdateAsync. A member holding an active original booking blocks the change
instead (`requirements-locked`, FR-1.5). No domain entity leaves any handler.

**Tech Stack:** .NET 10, xUnit, EF Core, PostgreSQL Testcontainers.

**Spec:** [Master Task 12](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[domain model](../design/01-domain-model.md), [functional requirements](../design/02-functional-requirements.md),
[solution architecture](../design/04-solution-architecture.md), [ontology](../ontology.md).

## Boundary

Task 12 owns reference data and settings only. Attendee CRUD, import and boundary work stay
with Task 20, which is split into 20a/20b (contradiction #5, settled with the user before
Task 12 was written). Do not add attendee CRUD handlers here.

The master plan writes appointment types as MED, FIT and IND. The prototype carries the
predecessor's three seeded rows (DAT, MED, UNI) until Phase 3; this task's tests create MED,
FIT and IND as Admin-managed rows through the new handlers. The fixed-identifier guard
(EnsureKnown) is removed from `Attendee.AssignAttendeeGroup` so Admin-managed ids flow;
the constants and the seed rows themselves retire with the seed rework in Phase 6, not here.

## Global constraints

One save per command; attendee locks ascending where the group replacement takes them
(reference-data rows themselves take no row locks — optimistic concurrency via Version
guards them); audit in the transaction; no domain entity leaves the handler; exactly one
StaffCapability per write handler. A `Location` zone change still requires a known IANA zone
and refuses while open proposals or future events reference the row, carrying both counts
(`in-use`, FR-1.2). An `AppointmentType` deactivation refuses while open proposals list it,
future active events list it, or active groups map it, carrying all three counts (`in-use`,
FR-1.6). A stale ExpectedVersion returns `version-conflict` carrying the current version.
Audit details carry codes, zone transitions, counts and versions — never display names,
addresses, personal data, tokens or URLs. Settings outside range are refused per field; valid settings never touch existing invites —
the invite snapshots the three settings values at issue (contradiction #9; the ontology
carries `inviteExpiryDays`, `maxAutoRetryCount` and `inviteOptionCount` on the invite, and
Task 14's issuer writes them).

## Review focus

STOP AND CHECK four things. The group-requirement replacement is proved by the two-member
case with a pending initial invite on one of them — both members re-derived, the invite
superseded, both `NotYetInvited`, one save — and by the blocked case with an active original
booking, which changes nothing. The zone-change refusal carries both counts (1 open proposal,
2 future events), not a bare refusal. The settings test that matters is the negative one:
valid settings do not alter existing invites. And every write emits exactly one audit entry
with old and new values and no names or emails.

### Task 12: Reference-data and settings use cases

**Files:**

- Modify: src/EventBooking.Application/Common/Error.cs (Data map plus in-use,
  requirements-locked and version-conflict factories)
- Modify: src/EventBooking.Application/Access/StaffCapability.cs (ManageReferenceData)
- Modify: src/EventBooking.Application/Access/StaffAccessAuthorizer.cs (Admin-only row)
- Modify: src/EventBooking.Domain/Audit/AuditAction.cs (values 31–37)
- Modify: src/EventBooking.Domain/Audit/AuditEntityTypes.cs (four constants)
- Modify: src/EventBooking.Domain/Attendees/Attendee.cs (remove the EnsureKnown guard)
- Modify: src/EventBooking.Domain/Invites/Invite.cs (snapshot properties, optional Create
  parameters, remove the EnsureKnown guard)
- Create: src/EventBooking.Application/Abstractions/ILocationRepository.cs
- Modify: src/EventBooking.Application/Abstractions/IAppointmentTypeRepository.cs (Add, GetByCodeAsync)
- Modify: src/EventBooking.Application/Abstractions/IAttendeeGroupRepository.cs (Add, ListAsync)
- Modify: src/EventBooking.Application/Abstractions/IAttendeeRepository.cs (LockByGroupForUpdateAsync)
- Create: src/EventBooking.Application/ReferenceData/LocationHandlers.cs
- Create: src/EventBooking.Application/ReferenceData/AppointmentTypeHandlers.cs
- Create: src/EventBooking.Application/ReferenceData/AttendeeGroupHandlers.cs
- Create: src/EventBooking.Application/ReferenceData/IReferenceDataBlockingQueries.cs
- Modify: src/EventBooking.Application/Settings/AdminSettingsHandler.cs (editable option count,
  expected version, SystemSettingsChanged audit, versioned result)
- Modify: src/EventBooking.Api/Endpoints/AdminEndpoints.cs (project the new settings result)
- Modify: src/EventBooking.Mcp/Tools/AdminTools.cs (project the new settings result)
- Create: src/EventBooking.Infrastructure/Persistence/Queries/ReferenceDataBlockingQueries.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/<generated-timestamp>_InviteSettingsSnapshot.cs
  (plus its Designer; the timestamp prefix comes from generation — see Step 3)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/EventBookingDbContextModelSnapshot.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs (location
  repository, Add methods, group-member locks)
- Modify: tests/EventBooking.Application.Tests/Fakes/InMemoryRepositories.cs (in-memory location
  repository, Add and lookup methods, group-member locks, in-memory blocking queries)
- Test: tests/EventBooking.Application.Tests/ReferenceData/ReferenceDataTestDoubles.cs
  (TestZones, MemoryBlocking)
- Modify: tests/EventBooking.Application.Tests/Settings/AdminSettingsAccessProfileTests.cs
  (positional SettingsView and UpdateSettingsCommand constructions)

The three Modify entries above are complete below; everything else in each file is untouched.
- Test: tests/EventBooking.Application.Tests/ReferenceData/LocationHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/ReferenceData/AppointmentTypeHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/ReferenceData/AttendeeGroupHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Settings/SettingsHandlerTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/Queries/ReferenceDataBlockingQueryTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/Groups/ReplaceAttendeeGroupRequirementsTests.cs

**Interfaces:**

The handler DTOs. Handlers follow the AdminSettingsHandler shape: authorize, load, check the
version, call the domain method, audit, save, return the DTO.

```csharp
namespace EventBooking.Application.ReferenceData;

public sealed record CreateLocationCommand(Guid StaffUserId, string? Code, string? Name, string? Address, string? TimeZoneId);
public sealed record UpdateLocationCommand(Guid StaffUserId, Guid LocationId, string? Name, string? Address, string? TimeZoneId, long ExpectedVersion);
public sealed record SetLocationActiveCommand(Guid StaffUserId, Guid LocationId, bool IsActive, long ExpectedVersion);
public sealed record LocationResult(Guid Id, string Code, string Name, string Address, string TimeZoneId, bool IsActive, long Version);
public sealed record LocationListItem(Guid Id, string Code, string Name, bool IsActive);

public sealed record CreateAppointmentTypeCommand(Guid StaffUserId, string? Code, string? Name);
public sealed record UpdateAppointmentTypeCommand(Guid StaffUserId, Guid AppointmentTypeId, string? Name, long ExpectedVersion);
public sealed record SetAppointmentTypeActiveCommand(Guid StaffUserId, Guid AppointmentTypeId, bool IsActive, long ExpectedVersion);
public sealed record AppointmentTypeResult(Guid Id, string Code, string Name, bool IsActive, long Version, string? ManagerDisplayName);
public sealed record AppointmentTypeListItem(Guid Id, string Code, string Name, bool IsActive, string? ManagerDisplayName);

public sealed record CreateAttendeeGroupCommand(Guid StaffUserId, string? Code, string? Name, IReadOnlyList<Guid> AppointmentTypeIds);
public sealed record UpdateAttendeeGroupCommand(Guid StaffUserId, Guid AttendeeGroupId, string? Name, IReadOnlyList<Guid>? AppointmentTypeIds, long ExpectedVersion);
public sealed record SetAttendeeGroupActiveCommand(Guid StaffUserId, Guid AttendeeGroupId, bool IsActive, long ExpectedVersion);
public sealed record AttendeeGroupResult(Guid Id, string Code, string Name, bool IsActive, long Version, IReadOnlyList<Guid> RequirementTypeIds, int MemberCount);
public sealed record AttendeeGroupListItem(Guid Id, string Code, string Name, bool IsActive, IReadOnlyList<Guid> RequirementTypeIds, int MemberCount);
```

```csharp
namespace EventBooking.Application.ReferenceData;

// Blocking counts behind the in-use refusals. Counts are exact, not estimates.
// LockByGroupForUpdateAsync returns the group's members with each attendee
// lifecycle lock held, ordered by attendee id ascending.
public interface IReferenceDataBlockingQueries
{
    Task<LocationUsage> LocationUsageAsync(Guid locationId, CancellationToken ct);
    Task<AppointmentTypeUsage> AppointmentTypeUsageAsync(Guid typeId, CancellationToken ct);
    Task<int> AttendeeGroupMemberCountAsync(Guid groupId, CancellationToken ct);
    Task<int> AttendeeGroupBlockingMemberCountAsync(Guid groupId, CancellationToken ct);
}
```

```csharp
namespace EventBooking.Application.Settings;

// inviteOptionCount is newly editable here (1-5); the old pass-through of the
// stored value retires with this task.
public sealed record SaveSystemSettingsCommand(Guid StaffUserId, int InviteExpiryDays, int MaxAutoRetryCount, int InviteOptionCount, long ExpectedVersion);
public sealed record SystemSettingsResult(int InviteExpiryDays, int MaxAutoRetryCount, int InviteOptionCount, long Version);
```

**Shared changes (apply before the handlers):**

```csharp
// Error.cs: add an optional Data map and three factories. Existing two-argument
// constructions are unaffected.
public sealed record Error(string Code, string Message, IReadOnlyDictionary<string, long>? Data = null)
{
    public const string ReferenceDataInUseCode = "in-use";
    public static Error ReferenceDataInUse(string message, IReadOnlyDictionary<string, int> blocking) =>
        new(ReferenceDataInUseCode, message, blocking.ToDictionary(kv => kv.Key, kv => (long)kv.Value));

    public const string RequirementsLockedCode = "requirements-locked";
    public static Error RequirementsLocked(string message, int blockingMembers) =>
        new(RequirementsLockedCode, message, new Dictionary<string, long> { ["blockingMembers"] = blockingMembers });

    public const string VersionConflictCode = "version-conflict";
    public static Error VersionConflict(string message, long currentVersion) =>
        new(VersionConflictCode, message, new Dictionary<string, long> { ["currentVersion"] = currentVersion });
}
```

```csharp
// StaffCapability.cs: add ManageReferenceData. StaffAccessAuthorizer.cs: add the
// Admin-only row beside ManageSettings:
//     StaffCapability.ManageReferenceData => isAdmin,

// AuditAction.cs: append seven values. 16 and 18 stay reserved (retired actions).
// LocationCreated = 31, LocationUpdated = 32, AppointmentTypeCreated = 33,
// AppointmentTypeUpdated = 34, AttendeeGroupCreated = 35, AttendeeGroupUpdated = 36,
// SystemSettingsChanged = 37.

// AuditEntityTypes.cs: add
// public const string Location = "Location";
// public const string AppointmentType = "AppointmentType";
// public const string AttendeeGroup = "AttendeeGroup";
// public const string SystemSettings = "SystemSettings";

// Attendee.cs: delete the EnsureKnown loop in AssignAttendeeGroup (Admin-managed ids
// are validated against the active set by the group, not against the fixed set).
// AttendeeGroup.Define keeps its guard: it is the seed path until Phase 6.

// Invite.cs: add the Task 9 ontology snapshot as real state, defaulting to the values every
// existing row was issued under (settings were never editable before this task, so the
// defaults are the true historical values). The parameters are optional so the Task 14
// issuer — which passes explicit values — is the only caller that changes.
public int InviteExpiryDays { get; private set; }
public int MaxAutoRetryCount { get; private set; }
public int InviteOptionCount { get; private set; }
// Create gains: int inviteExpiryDays = 7, int maxAutoRetryCount = 2, int inviteOptionCount = 3,
// stored on the row. Reissue copies all three from the originating invite.
// Delete the EnsureKnown loop in Create for the same reason as AssignAttendeeGroup.
```

```csharp
// ILocationRepository.cs (new port, same shape as the group repository):
public interface ILocationRepository
{
    Task<Location?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<Location?> GetByCodeAsync(string code, CancellationToken cancellationToken);
    Task<IReadOnlyList<Location>> ListAsync(CancellationToken cancellationToken);
    void Add(Location location);
}

// IAppointmentTypeRepository.cs: add
// void Add(AppointmentType type);
// Task<AppointmentType?> GetByCodeAsync(string code, CancellationToken cancellationToken);

// IAttendeeGroupRepository.cs: add
// void Add(AttendeeGroup group);
// Task<IReadOnlyList<AttendeeGroup>> ListAsync(CancellationToken cancellationToken);

// IAttendeeRepository.cs: add
// Takes every member's lifecycle lock in ascending id order and returns the
// members ordered by id. The group-requirement replacement calls this first.
Task<IReadOnlyList<Attendee>> LockByGroupForUpdateAsync(Guid groupId, CancellationToken cancellationToken);
```

- [ ] **Step 1: Write the failing tests.** Create the seven test files below in full
  (four Application suites, shared doubles, two Infrastructure suites).
  Shared doubles live in `tests/EventBooking.Application.Tests/ReferenceData/ReferenceDataTestDoubles.cs`
  (new file): TestZones implements IEventWindowZones with IsKnownZone true for
  `Europe/London` and `Asia/Tokyo` (other members throw), and MemoryBlocking implements the
  blocking port over settable values defaulting to empty usage and zero counts. Admin callers hold
  `StaffAccessProfile.Create(adminId, Role.Admin, null)`.

  ```csharp
  // tests/EventBooking.Application.Tests/ReferenceData/LocationHandlerTests.cs
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Access;
  using EventBooking.Application.ReferenceData;
  using EventBooking.Application.Tests.Fakes;
  using EventBooking.Domain.Access;
  using EventBooking.Domain.Audit;
  using EventBooking.Domain.Time;

  namespace EventBooking.Application.Tests.ReferenceData;

  public sealed class LocationHandlerTests
  {
      private static readonly Guid Admin = Guid.Parse("a0000001-0000-0000-0000-000000000001");

      private readonly InMemoryLocationRepository _locations = new();
      private readonly InMemoryStaffAccessProfileRepository _profiles = new();
      private readonly FakeUnitOfWork _unitOfWork = new();
      private readonly RecordingAuditLogger _audit = new();

      public LocationHandlerTests()
      {
          _profiles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));
      }

      private CreateLocationHandler Creator => new(_locations, _profiles, _unitOfWork, _audit, TestZones.Instance);
      private UpdateLocationHandler Updater => new(_locations, _profiles, _unitOfWork, _audit, TestZones.Instance, new MemoryBlocking());
      private SetLocationActiveHandler Activer => new(_locations, _profiles, _unitOfWork, _audit, new MemoryBlocking());

      [Fact]
      public async Task Create_location_writes_LocationCreated_with_no_personal_data()
      {
          var result = await Creator.HandleAsync(
              new CreateLocationCommand(Admin, "london_hq", "London HQ", "1 High St", "Europe/London"),
              CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal("LONDON_HQ", result.Value.Code);
          Assert.Equal(1, result.Value.Version);
          var entry = Assert.Single(_audit.Entries);
          Assert.Equal(AuditAction.LocationCreated, entry.Action);
          Assert.Equal(AuditEntityTypes.Location, entry.EntityType);
          Assert.Equal(ActorType.Staff, entry.ActorType);
          Assert.Equal(Admin.ToString(), entry.ActorId);
          Assert.Contains("LONDON_HQ", entry.Details);
          Assert.Contains("Europe/London", entry.Details);
          Assert.DoesNotContain("@", entry.Details);
          Assert.Equal(1, _unitOfWork.SaveCount);
      }

      [Fact]
      public async Task Create_location_with_unknown_zone_is_refused()
      {
          var result = await Creator.HandleAsync(
              new CreateLocationCommand(Admin, "NOWHERE", "Nowhere", "2 Lost Rd", "Moon/Olympus"),
              CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("validation", result.Error.Code);
          Assert.Empty(_locations.Items);
          Assert.Empty(_audit.Entries);
      }

      [Fact]
      public async Task Create_location_with_duplicate_code_is_refused()
      {
          await Creator.HandleAsync(
              new CreateLocationCommand(Admin, "LONDON_HQ", "London HQ", "1 High St", "Europe/London"),
              CancellationToken.None);

          var result = await Creator.HandleAsync(
              new CreateLocationCommand(Admin, "london_hq", "Other", "9 Elsewhere", "Europe/London"),
              CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("conflict", result.Error.Code);
          Assert.Single(_locations.Items);
      }

      [Fact]
      public async Task Zone_change_while_scheduled_returns_in_use_with_both_counts()
      {
          var created = await Creator.HandleAsync(
              new CreateLocationCommand(Admin, "LONDON_HQ", "London HQ", "1 High St", "Europe/London"),
              CancellationToken.None);
          var blocking = new MemoryBlocking { LocationUsageValue = new LocationUsage(1, 2) };
          var updater = new UpdateLocationHandler(_locations, _profiles, _unitOfWork, _audit, TestZones.Instance, blocking);

          var result = await updater.HandleAsync(
              new UpdateLocationCommand(Admin, created.Value.Id, null, null, "Asia/Tokyo", 1),
              CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("in-use", result.Error.Code);
          Assert.Equal(1L, result.Error.Data!["openProposals"]);
          Assert.Equal(2L, result.Error.Data!["futureEvents"]);
          Assert.Equal("Europe/London", _locations.Items.Single().TimeZoneId);
          Assert.Single(_audit.Entries);
      }

      [Fact]
      public async Task Stale_version_returns_version_conflict_with_current_state()
      {
          var created = await Creator.HandleAsync(
              new CreateLocationCommand(Admin, "LONDON_HQ", "London HQ", "1 High St", "Europe/London"),
              CancellationToken.None);

          var result = await Updater.HandleAsync(
              new UpdateLocationCommand(Admin, created.Value.Id, "Renamed", null, null, 99),
              CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("version-conflict", result.Error.Code);
          Assert.Equal(1L, result.Error.Data!["currentVersion"]);
          Assert.Equal("London HQ", _locations.Items.Single().Name);
      }

      [Fact]
      public async Task Non_admin_cannot_create_a_location()
      {
          var coordinator = Guid.NewGuid();
          _profiles.Add(StaffAccessProfile.Create(coordinator, Role.Coordinator, null));

          var result = await Creator.HandleAsync(
              new CreateLocationCommand(coordinator, "PARIS", "Paris", "3 Rue", "Europe/Paris"),
              CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("forbidden", result.Error.Code);
          Assert.Empty(_locations.Items);
      }
  }
  ```

  ```csharp
  // tests/EventBooking.Application.Tests/ReferenceData/AppointmentTypeHandlerTests.cs
  using EventBooking.Application.Access;
  using EventBooking.Application.ReferenceData;
  using EventBooking.Application.Tests.Fakes;
  using EventBooking.Domain.Access;
  using EventBooking.Domain.Audit;

  namespace EventBooking.Application.Tests.ReferenceData;

  public sealed class AppointmentTypeHandlerTests
  {
      private static readonly Guid Admin = Guid.Parse("a0000001-0000-0000-0000-000000000001");

      private readonly InMemoryAppointmentTypeRepository _types = new();
      private readonly InMemoryStaffAccessProfileRepository _profiles = new();
      private readonly FakeUnitOfWork _unitOfWork = new();
      private readonly RecordingAuditLogger _audit = new();

      public AppointmentTypeHandlerTests()
      {
          _profiles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));
          _types.Items.Clear();
      }

      private CreateAppointmentTypeHandler Creator => new(_types, _profiles, _unitOfWork, _audit);
      private SetAppointmentTypeActiveHandler Activer => new(_types, _profiles, _unitOfWork, _audit, new MemoryBlocking());

      [Fact]
      public async Task Create_type_writes_AppointmentTypeCreated()
      {
          var result = await Creator.HandleAsync(
              new CreateAppointmentTypeCommand(Admin, "med", "Medical"),
              CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal("MED", result.Value.Code);
          var entry = Assert.Single(_audit.Entries);
          Assert.Equal(AuditAction.AppointmentTypeCreated, entry.Action);
          Assert.Equal(AuditEntityTypes.AppointmentType, entry.EntityType);
      }

      [Fact]
      public async Task Deactivate_type_mapped_by_active_group_returns_in_use_naming_group_count()
      {
          var created = await Creator.HandleAsync(
              new CreateAppointmentTypeCommand(Admin, "MED", "Medical"),
              CancellationToken.None);
          var blocking = new MemoryBlocking { TypeUsageValue = new AppointmentTypeUsage(0, 0, 2) };
          var activer = new SetAppointmentTypeActiveHandler(_types, _profiles, _unitOfWork, _audit, blocking);

          var result = await activer.HandleAsync(
              new SetAppointmentTypeActiveCommand(Admin, created.Value.Id, false, 1),
              CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("in-use", result.Error.Code);
          Assert.Equal(2L, result.Error.Data!["activeGroups"]);
          Assert.True(_types.Items.Single().IsActive);
      }

      [Fact]
      public async Task Manager_display_name_falls_back_to_staff_id()
      {
          var created = await Creator.HandleAsync(
              new CreateAppointmentTypeCommand(Admin, "MED", "Medical"),
              CancellationToken.None);
          var manager = Guid.NewGuid();
          _profiles.Add(StaffAccessProfile.Create(manager, Role.Manager, created.Value.Id));
          var identities = new InMemoryStaffIdentityRepository();
          await identities.UpsertAsync(manager, new StaffId("M100"), null, DateTimeOffset.UtcNow, CancellationToken.None);
          var lister = new ListAppointmentTypesHandler(_types, _profiles, identities);

          var result = await lister.HandleAsync(CancellationToken.None);

          Assert.True(result.IsSuccess);
          var item = Assert.Single(result.Value);
          Assert.Equal("M100", item.ManagerDisplayName);
      }
  }
  ```

  ```csharp
  // tests/EventBooking.Application.Tests/ReferenceData/AttendeeGroupHandlerTests.cs
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Access;
  using EventBooking.Application.ReferenceData;
  using EventBooking.Application.Tests.Fakes;
  using EventBooking.Domain.Access;
  using EventBooking.Domain.AppointmentTypes;
  using EventBooking.Domain.Attendees;
  using EventBooking.Domain.AttendeeGroups;
  using EventBooking.Domain.Audit;
  using EventBooking.Domain.Bookings;
  using EventBooking.Domain.Invites;

  namespace EventBooking.Application.Tests.ReferenceData;

  public sealed class AttendeeGroupHandlerTests
  {
      private static readonly Guid Admin = Guid.Parse("a0000001-0000-0000-0000-000000000001");
      private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

      private readonly InMemoryAttendeeGroupRepository _groups = new();
      private readonly InMemoryAppointmentTypeRepository _types = new();
      private readonly InMemoryAttendeeRepository _attendees = new();
      private readonly InMemoryInviteRepository _invites = new();
      private readonly InMemoryStaffAccessProfileRepository _profiles = new();
      private readonly FakeUnitOfWork _unitOfWork = new();
      private readonly RecordingAuditLogger _audit = new();
      private readonly FakeClock _clock = new(Now);
      private readonly MemoryBlocking _blocking = new();

      private Guid _med;
      private Guid _fit;
      private Guid _ind;

      public AttendeeGroupHandlerTests()
      {
          _profiles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));
          _types.Items.Clear();
          _med = AddType("MED", "Medical");
          _fit = AddType("FIT", "Fitness");
          _ind = AddType("IND", "Induction");
      }

      private Guid AddType(string code, string name)
      {
          var type = AppointmentType.Create(Guid.NewGuid(), code, name);
          _types.Items.Add(type);
          return type.Id;
      }

      private CreateAttendeeGroupHandler Creator => new(_groups, _types, _profiles, _unitOfWork, _audit);
      private UpdateAttendeeGroupHandler Updater => new(_groups, _types, _attendees, _invites, _profiles, _blocking, _unitOfWork, _audit, _clock);

      [Fact]
      public async Task Requirement_change_rederives_both_members_and_supersedes_the_pending_invite()
      {
          var group = (await Creator.HandleAsync(
              new CreateAttendeeGroupCommand(Admin, "NHS", "NHS staff", [_med, _fit]),
              CancellationToken.None)).Value;
          var stored = _groups.Items.Single();
          var first = Attendee.Create(Guid.NewGuid(), "Amy", "amy@example.invalid", stored, Now);
          var second = Attendee.Create(Guid.NewGuid(), "Bo", "bo@example.invalid", stored, Now);
          _attendees.Items.AddRange([first, second]);
          first.MarkInvited(Now);
          var optionEvents = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
          var pending = Invite.CreateInitial(Guid.NewGuid(), first.Id, Now.AddDays(7), [Guid.NewGuid()], optionEvents, [_med, _fit], 0);
          _invites.Items.Add(pending);
          _blocking.BlockingMembers = 0;

          var result = await Updater.HandleAsync(
              new UpdateAttendeeGroupCommand(Admin, group.Id, null, [_med, _ind], 1),
              CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal([_ind, _med].Order().ToList(), result.Value.RequirementTypeIds.Order().ToList());
          Assert.Equal([_ind, _med].Order().ToList(), first.RequiredAppointmentTypeIds.Order().ToList());
          Assert.Equal([_ind, _med].Order().ToList(), second.RequiredAppointmentTypeIds.Order().ToList());
          Assert.Equal(InviteStatus.Superseded, pending.Status);
          Assert.Equal(AttendeeStatus.NotYetInvited, first.Status);
          Assert.Equal(AttendeeStatus.NotYetInvited, second.Status);
          var entry = Assert.Single(_audit.Entries.Where(e => e.Action == AuditAction.AttendeeGroupUpdated));
          Assert.Contains("2 members", entry.Details);
          Assert.Equal(1, _unitOfWork.SaveCount);
      }

      [Fact]
      public async Task Requirement_change_with_active_booking_is_refused_and_changes_nothing()
      {
          var group = (await Creator.HandleAsync(
              new CreateAttendeeGroupCommand(Admin, "NHS", "NHS staff", [_med, _fit]),
              CancellationToken.None)).Value;
          var stored = _groups.Items.Single();
          var member = Attendee.Create(Guid.NewGuid(), "Amy", "amy@example.invalid", stored, Now);
          _attendees.Items.Add(member);
          member.MarkInvited(Now);
          var optionEvents = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
          var invite = Invite.CreateInitial(Guid.NewGuid(), member.Id, Now.AddDays(7), [Guid.NewGuid()], optionEvents, [_med, _fit], 0);
          _invites.Items.Add(invite);
          invite.MarkUsed();
          _ = Booking.Create(Guid.NewGuid(), invite, optionEvents[0], Now);
          _blocking.BlockingMembers = 1;

          var result = await Updater.HandleAsync(
              new UpdateAttendeeGroupCommand(Admin, group.Id, null, [_med, _ind], 1),
              CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("requirements-locked", result.Error.Code);
          Assert.Equal(1L, result.Error.Data!["blockingMembers"]);
          Assert.Equal([_fit, _med].Order().ToList(), member.RequiredAppointmentTypeIds.Order().ToList());
          Assert.Equal(AttendeeStatus.Invited, member.Status);
          Assert.Single(_audit.Entries);
      }

      [Fact]
      public async Task Deactivate_group_with_members_returns_in_use()
      {
          var group = (await Creator.HandleAsync(
              new CreateAttendeeGroupCommand(Admin, "NHS", "NHS staff", [_med]),
              CancellationToken.None)).Value;
          _blocking.MemberCount = 3;
          var activer = new SetAttendeeGroupActiveHandler(_groups, _profiles, _blocking, _unitOfWork, _audit);

          var result = await activer.HandleAsync(
              new SetAttendeeGroupActiveCommand(Admin, group.Id, false, 1),
              CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("in-use", result.Error.Code);
          Assert.Equal(3L, result.Error.Data!["members"]);
          Assert.True(_groups.Items.Single().IsActive);
      }
  }
  ```

  ```csharp
  // tests/EventBooking.Application.Tests/Settings/SettingsHandlerTests.cs
  using EventBooking.Application.Access;
  using EventBooking.Application.Settings;
  using EventBooking.Application.Tests.Fakes;
  using EventBooking.Domain.Access;
  using EventBooking.Domain.Audit;
  using EventBooking.Domain.Invites;

  namespace EventBooking.Application.Tests.Settings;

  public sealed class SettingsHandlerTests
  {
      private static readonly Guid Admin = Guid.Parse("a0000001-0000-0000-0000-000000000001");
      private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

      private readonly InMemorySystemSettingsRepository _settings = new();
      private readonly InMemoryStaffAccessProfileRepository _profiles = new();
      private readonly FakeUnitOfWork _unitOfWork = new();
      private readonly RecordingAuditLogger _audit = new();

      public SettingsHandlerTests() =>
          _profiles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));

      private SaveSystemSettingsHandler Saver => new(_settings, _profiles, _unitOfWork, _audit);

      [Fact]
      public async Task Valid_save_writes_SystemSettingsChanged_with_version()
      {
          var result = await Saver.HandleAsync(
              new SaveSystemSettingsCommand(Admin, 14, 3, 5, 1),
              CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal(new SystemSettingsResult(14, 3, 5, 2), result.Value);
          var entry = Assert.Single(_audit.Entries);
          Assert.Equal(AuditAction.SystemSettingsChanged, entry.Action);
          Assert.Equal(AuditEntityTypes.SystemSettings, entry.EntityType);
      }

      [Fact]
      public async Task Each_field_is_refused_outside_its_range()
      {
          foreach (var command in new[]
          {
              new SaveSystemSettingsCommand(Admin, 0, 2, 3, 1),
              new SaveSystemSettingsCommand(Admin, 61, 2, 3, 1),
              new SaveSystemSettingsCommand(Admin, 7, -1, 3, 1),
              new SaveSystemSettingsCommand(Admin, 7, 11, 3, 1),
              new SaveSystemSettingsCommand(Admin, 7, 2, 0, 1),
              new SaveSystemSettingsCommand(Admin, 7, 2, 6, 1),
          })
          {
              var result = await Saver.HandleAsync(command, CancellationToken.None);
              Assert.True(result.IsFailure);
              Assert.Equal("validation", result.Error.Code);
          }

          Assert.Equal(7, _settings.Settings.InviteExpiryDays);
          Assert.Empty(_audit.Entries);
      }

      [Fact]
      public async Task Valid_settings_do_not_alter_existing_invites()
      {
          var optionEvents = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
          var invite = Invite.CreateInitial(Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(7), [Guid.NewGuid()], optionEvents, [Guid.NewGuid()], 0);

          var result = await Saver.HandleAsync(
              new SaveSystemSettingsCommand(Admin, 30, 5, 1, 1),
              CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal(3, invite.InviteOptionCount);
          Assert.Equal(7, invite.InviteExpiryDays);
          Assert.Equal(2, invite.MaxAutoRetryCount);
      }

      [Fact]
      public async Task Stale_version_returns_version_conflict()
      {
          var result = await Saver.HandleAsync(
              new SaveSystemSettingsCommand(Admin, 14, 3, 3, 99),
              CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("version-conflict", result.Error.Code);
          Assert.Equal(1L, result.Error.Data!["currentVersion"]);
      }
  }
  ```

  ```csharp
  // tests/EventBooking.Infrastructure.Tests/Queries/ReferenceDataBlockingQueryTests.cs (complete)
  using EventBooking.Application.ReferenceData;
  using EventBooking.Domain.AppointmentTypes;
  using EventBooking.Domain.Attendees;
  using EventBooking.Domain.AttendeeGroups;
  using EventBooking.Domain.Bookings;
  using EventBooking.Domain.Events;
  using EventBooking.Domain.Invites;
  using EventBooking.Domain.Locations;
  using EventBooking.Infrastructure.Persistence.Queries;

  namespace EventBooking.Infrastructure.Tests.Queries;

  public sealed class ReferenceDataBlockingQueryTests : PostgresBlockingHarness
  {
      [Fact]
      public async Task Location_usage_counts_open_proposals_and_future_events()
      {
          var location = await SeedLocationAsync("LONDON_HQ", "Europe/London");
          await SeedOpenProposalAsync(location.Id);
          await SeedOpenProposalAsync(location.Id);
          await SeedFutureEventAsync(location.Id);
          var queries = new ReferenceDataBlockingQueries(Context);

          var usage = await queries.LocationUsageAsync(location.Id, CancellationToken.None);

          Assert.Equal(new LocationUsage(2, 1), usage);
      }

      [Fact]
      public async Task Type_usage_counts_proposals_events_and_mapped_groups()
      {
          var (type, location) = await SeedTypeWithProposalEventAndGroupAsync();

          var usage = await new ReferenceDataBlockingQueries(Context)
              .AppointmentTypeUsageAsync(type.Id, CancellationToken.None);

          Assert.Equal(new AppointmentTypeUsage(1, 1, 1), usage);
      }

      [Fact]
      public async Task Group_counts_distinguish_members_from_blocking_members()
      {
          var group = await SeedGroupWithTwoMembersAsync(blockActiveBooking: true);

          var queries = new ReferenceDataBlockingQueries(Context);

          Assert.Equal(2, await queries.AttendeeGroupMemberCountAsync(group.Id, CancellationToken.None));
          Assert.Equal(1, await queries.AttendeeGroupBlockingMemberCountAsync(group.Id, CancellationToken.None));
      }

      [Fact]
      public async Task Empty_references_count_zero()
      {
          var location = await SeedLocationAsync("EMPTY", "Europe/London");
          var queries = new ReferenceDataBlockingQueries(Context);

          Assert.Equal(LocationUsage.None, await queries.LocationUsageAsync(location.Id, CancellationToken.None));
      }
  }
  ```

  PostgresBlockingHarness extends the Task 9b fixture (container, migrated context as
  Context): seed helpers insert locations, proposals (Open), events (Active with future
  start instants), groups with mappings, attendees, a pending invite on one member and an
  active original booking on the other. The group helper returns the group after adding
  two members — one holding a pending initial invite, one holding an active original
  booking — which is exactly the blocking distinction the third test asserts.

  ```csharp
  // tests/EventBooking.Infrastructure.Tests/Groups/ReplaceAttendeeGroupRequirementsTests.cs (complete)
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Access;
  using EventBooking.Application.Common;
  using EventBooking.Application.ReferenceData;
  using EventBooking.Domain.Access;
  using EventBooking.Domain.Attendees;
  using EventBooking.Domain.Audit;
  using EventBooking.Domain.Invites;

  namespace EventBooking.Infrastructure.Tests.Groups;

  public sealed class ReplaceAttendeeGroupRequirementsTests : PostgresBlockingHarness
  {
      [Fact]
      public async Task Replacement_rederives_both_members_and_supersedes_in_one_save()
      {
          var admin = Guid.NewGuid();
          Profiles.Add(StaffAccessProfile.Create(admin, Role.Admin, null));
          var group = await SeedGroupWithTwoMembersAsync(blockActiveBooking: false);
          var added = await AddTypeAsync("IND", "Induction");
          var handler = new UpdateAttendeeGroupHandler(
              GroupRepository, TypeRepository, AttendeeRepository, InviteRepository,
              Profiles, Queries, UnitOfWork, Audit, Clock);

          var result = await handler.HandleAsync(new UpdateAttendeeGroupCommand(
              admin, group.Id, null, [MedId, added.Id], 1), CancellationToken.None);

          Assert.True(result.IsSuccess);
          var members = await MembersOfAsync(group.Id);
          Assert.All(members, m => Assert.Equal(
              [MedId, added.Id].Order().ToList(),
              m.RequiredAppointmentTypeIds.Order().ToList()));
          Assert.All(members, m => Assert.Equal(
              AttendeeStatus.NotYetInvited, m.Status));
          Assert.Equal(InviteStatus.Superseded,
              (await InviteForAsync(members[0].Id)).Status);
          Assert.Equal(1, SaveCount);
      }

      [Fact]
      public async Task Replacement_with_active_booking_is_refused_and_changes_nothing()
      {
          var admin = Guid.NewGuid();
          Profiles.Add(StaffAccessProfile.Create(admin, Role.Admin, null));
          var group = await SeedGroupWithTwoMembersAsync(blockActiveBooking: true);
          var added = await AddTypeAsync("IND", "Induction");
          var before = await GroupVersionAsync(group.Id);
          var handler = new UpdateAttendeeGroupHandler(
              GroupRepository, TypeRepository, AttendeeRepository, InviteRepository,
              Profiles, Queries, UnitOfWork, Audit, Clock);

          var result = await handler.HandleAsync(new UpdateAttendeeGroupCommand(
              admin, group.Id, null, [MedId, added.Id], 1), CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("requirements-locked", result.Error.Code);
          Assert.Equal(before, await GroupVersionAsync(group.Id));
      }
  }
  ```

  The harness exposes the real EF repositories (GroupRepository, TypeRepository,
  AttendeeRepository, InviteRepository), the real blocking queries (Queries), a
  recording audit (Audit), a fake clock (Clock), a fake unit of work counting saves
  (SaveCount), the in-memory authorizer profiles (Profiles), the MED type id
  (MedId), per-group member/invite readers (MembersOfAsync, InviteForAsync,
  GroupVersionAsync), and the type/group seed helpers used above. `SeedGroupWithTwoMembersAsync(false)`
  leaves both members booking-free; with `true` one member holds an active original
  booking. `SaveCount == 1` proves the re-derivation committed in one save.

- [ ] **Step 2: Run.** Expected: FAIL to compile — the handlers, the repository methods,
  the blocking port, the new Error factories and the new audit members do not exist.

  ```bash
  dotnet test tests/EventBooking.Application.Tests --filter "FullyQualifiedName~ReferenceData|FullyQualifiedName~Settings"
  ```

- [ ] **Step 3: Implement.** Add the production code below in full, then the fakes, then the
  migration. No placeholders: every file below is complete.

  ```csharp
  // src/EventBooking.Application/ReferenceData/LocationHandlers.cs
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Access;
  using EventBooking.Application.Common;
  using EventBooking.Domain.Audit;
  using EventBooking.Domain.Common;
  using EventBooking.Domain.Locations;
  using EventBooking.Domain.Time;

  namespace EventBooking.Application.ReferenceData;

  public sealed class CreateLocationHandler(
      ILocationRepository locations,
      IStaffAccessAuthorizer access,
      IUnitOfWork unitOfWork,
      IAuditLogger audit,
      IEventWindowZones zones)
  {
      public async Task<Result<LocationResult>> HandleAsync(CreateLocationCommand command, CancellationToken ct)
      {
          var authorized = await access.AuthorizeAsync(command.StaffUserId, StaffCapability.ManageReferenceData, null, ct);
          if (authorized.IsFailure) return Result<LocationResult>.Failure(authorized.Error);

          Location location;
          try
          {
              location = Location.Create(Guid.NewGuid(), command.Code, command.Name, command.Address, command.TimeZoneId, zones);
          }
          catch (DomainException ex)
          {
              return Result<LocationResult>.Failure(Error.Validation(ex.Message));
          }

          if (await locations.GetByCodeAsync(location.Code, ct) is not null)
              return Result<LocationResult>.Failure(Error.Conflict($"A location with code '{location.Code}' already exists."));

          locations.Add(location);
          audit.Record(AuditEntityTypes.Location, location.Id, AuditAction.LocationCreated,
              ActorType.Staff, command.StaffUserId.ToString(), $"code {location.Code}; zone {location.TimeZoneId}");
          await unitOfWork.SaveChangesAsync(ct);
          return Result<LocationResult>.Success(new LocationResult(
              location.Id, location.Code, location.Name, location.Address, location.TimeZoneId, location.IsActive, location.Version));
      }
  }

  public sealed class UpdateLocationHandler(
      ILocationRepository locations,
      IStaffAccessAuthorizer access,
      IUnitOfWork unitOfWork,
      IAuditLogger audit,
      IEventWindowZones zones,
      IReferenceDataBlockingQueries blocking)
  {
      public async Task<Result<LocationResult>> HandleAsync(UpdateLocationCommand command, CancellationToken ct)
      {
          var authorized = await access.AuthorizeAsync(command.StaffUserId, StaffCapability.ManageReferenceData, null, ct);
          if (authorized.IsFailure) return Result<LocationResult>.Failure(authorized.Error);

          var location = await locations.GetAsync(command.LocationId, ct);
          if (location is null) return Result<LocationResult>.Failure(Error.NotFound("No such location."));
          if (location.Version != command.ExpectedVersion)
              return Result<LocationResult>.Failure(Error.VersionConflict("The location changed under you.", location.Version));

          var changes = new List<string>();
          try
          {
              if (command.Name is not null) { location.Rename(command.Name); changes.Add("name"); }
              if (command.Address is not null) { location.ChangeAddress(command.Address); changes.Add("address"); }
              if (command.TimeZoneId is not null && command.TimeZoneId != location.TimeZoneId)
              {
                  var before = location.TimeZoneId;
                  var usage = await blocking.LocationUsageAsync(location.Id, ct);
                  location.ChangeTimeZone(command.TimeZoneId, zones, usage);
                  if (location.TimeZoneId != before) changes.Add($"timeZone {before} -> {location.TimeZoneId}");
              }
          }
          catch (ReferenceDataInUseException ex)
          {
              return Result<LocationResult>.Failure(Error.ReferenceDataInUse(ex.Message, ex.Blocking));
          }
          catch (DomainException ex)
          {
              return Result<LocationResult>.Failure(Error.Validation(ex.Message));
          }

          if (changes.Count == 0)
              return Result<LocationResult>.Success(ToResult(location));

          audit.Record(AuditEntityTypes.Location, location.Id, AuditAction.LocationUpdated,
              ActorType.Staff, command.StaffUserId.ToString(), string.Join("; ", changes));
          await unitOfWork.SaveChangesAsync(ct);
          return Result<LocationResult>.Success(ToResult(location));
      }

      private static LocationResult ToResult(Location location) => new(
          location.Id, location.Code, location.Name, location.Address, location.TimeZoneId, location.IsActive, location.Version);
  }

  public sealed class SetLocationActiveHandler(
      ILocationRepository locations,
      IStaffAccessAuthorizer access,
      IUnitOfWork unitOfWork,
      IAuditLogger audit,
      IReferenceDataBlockingQueries blocking)
  {
      public async Task<Result<LocationResult>> HandleAsync(SetLocationActiveCommand command, CancellationToken ct)
      {
          var authorized = await access.AuthorizeAsync(command.StaffUserId, StaffCapability.ManageReferenceData, null, ct);
          if (authorized.IsFailure) return Result<LocationResult>.Failure(authorized.Error);

          var location = await locations.GetAsync(command.LocationId, ct);
          if (location is null) return Result<LocationResult>.Failure(Error.NotFound("No such location."));
          if (location.Version != command.ExpectedVersion)
              return Result<LocationResult>.Failure(Error.VersionConflict("The location changed under you.", location.Version));

          var beforeVersion = location.Version;
          try
          {
              if (command.IsActive) location.Reactivate();
              else location.Deactivate(await blocking.LocationUsageAsync(location.Id, ct));
          }
          catch (ReferenceDataInUseException ex)
          {
              return Result<LocationResult>.Failure(Error.ReferenceDataInUse(ex.Message, ex.Blocking));
          }

          if (location.Version == beforeVersion)
              return Result<LocationResult>.Success(new LocationResult(
                  location.Id, location.Code, location.Name, location.Address, location.TimeZoneId, location.IsActive, location.Version));

          audit.Record(AuditEntityTypes.Location, location.Id, AuditAction.LocationUpdated,
              ActorType.Staff, command.StaffUserId.ToString(), $"isActive -> {location.IsActive}");
          await unitOfWork.SaveChangesAsync(ct);
          return Result<LocationResult>.Success(new LocationResult(
              location.Id, location.Code, location.Name, location.Address, location.TimeZoneId, location.IsActive, location.Version));
      }
  }

  public sealed class ListLocationsHandler(ILocationRepository locations)
  {
      public async Task<Result<IReadOnlyList<LocationListItem>>> HandleAsync(CancellationToken ct)
      {
          var rows = await locations.ListAsync(ct);
          return Result<IReadOnlyList<LocationListItem>>.Success(
              rows.OrderBy(l => l.Code, StringComparer.Ordinal)
                  .Select(l => new LocationListItem(l.Id, l.Code, l.Name, l.IsActive))
                  .ToList());
      }
  }
  ```

  ```csharp
  // src/EventBooking.Application/ReferenceData/AppointmentTypeHandlers.cs
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Access;
  using EventBooking.Application.Common;
  using EventBooking.Domain.AppointmentTypes;
  using EventBooking.Domain.Audit;
  using EventBooking.Domain.Common;

  namespace EventBooking.Application.ReferenceData;

  public sealed class CreateAppointmentTypeHandler(
      IAppointmentTypeRepository types,
      IStaffAccessAuthorizer access,
      IUnitOfWork unitOfWork,
      IAuditLogger audit)
  {
      public async Task<Result<AppointmentTypeResult>> HandleAsync(CreateAppointmentTypeCommand command, CancellationToken ct)
      {
          var authorized = await access.AuthorizeAsync(command.StaffUserId, StaffCapability.ManageReferenceData, null, ct);
          if (authorized.IsFailure) return Result<AppointmentTypeResult>.Failure(authorized.Error);

          AppointmentType type;
          try
          {
              type = AppointmentType.Create(Guid.NewGuid(), command.Code, command.Name);
          }
          catch (DomainException ex)
          {
              return Result<AppointmentTypeResult>.Failure(Error.Validation(ex.Message));
          }

          if (await types.GetByCodeAsync(type.Code, ct) is not null)
              return Result<AppointmentTypeResult>.Failure(Error.Conflict($"An appointment type with code '{type.Code}' already exists."));

          types.Add(type);
          audit.Record(AuditEntityTypes.AppointmentType, type.Id, AuditAction.AppointmentTypeCreated,
              ActorType.Staff, command.StaffUserId.ToString(), $"code {type.Code}");
          await unitOfWork.SaveChangesAsync(ct);
          return Result<AppointmentTypeResult>.Success(new AppointmentTypeResult(
              type.Id, type.Code, type.Name, type.IsActive, type.Version, null));
      }
  }

  public sealed class UpdateAppointmentTypeHandler(
      IAppointmentTypeRepository types,
      IStaffAccessAuthorizer access,
      IUnitOfWork unitOfWork,
      IAuditLogger audit)
  {
      public async Task<Result<AppointmentTypeResult>> HandleAsync(UpdateAppointmentTypeCommand command, CancellationToken ct)
      {
          var authorized = await access.AuthorizeAsync(command.StaffUserId, StaffCapability.ManageReferenceData, null, ct);
          if (authorized.IsFailure) return Result<AppointmentTypeResult>.Failure(authorized.Error);

          var type = await types.GetAsync(command.AppointmentTypeId, ct);
          if (type is null) return Result<AppointmentTypeResult>.Failure(Error.NotFound("No such appointment type."));
          if (type.Version != command.ExpectedVersion)
              return Result<AppointmentTypeResult>.Failure(Error.VersionConflict("The appointment type changed under you.", type.Version));

          if (command.Name is null) return Result<AppointmentTypeResult>.Success(ToResult(type));

          try
          {
              type.Rename(command.Name);
          }
          catch (DomainException ex)
          {
              return Result<AppointmentTypeResult>.Failure(Error.Validation(ex.Message));
          }

          audit.Record(AuditEntityTypes.AppointmentType, type.Id, AuditAction.AppointmentTypeUpdated,
              ActorType.Staff, command.StaffUserId.ToString(), "name");
          await unitOfWork.SaveChangesAsync(ct);
          return Result<AppointmentTypeResult>.Success(ToResult(type));
      }

      private static AppointmentTypeResult ToResult(AppointmentType type) => new(
          type.Id, type.Code, type.Name, type.IsActive, type.Version, null);
  }

  public sealed class SetAppointmentTypeActiveHandler(
      IAppointmentTypeRepository types,
      IStaffAccessAuthorizer access,
      IUnitOfWork unitOfWork,
      IAuditLogger audit,
      IReferenceDataBlockingQueries blocking)
  {
      public async Task<Result<AppointmentTypeResult>> HandleAsync(SetAppointmentTypeActiveCommand command, CancellationToken ct)
      {
          var authorized = await access.AuthorizeAsync(command.StaffUserId, StaffCapability.ManageReferenceData, null, ct);
          if (authorized.IsFailure) return Result<AppointmentTypeResult>.Failure(authorized.Error);

          var type = await types.GetAsync(command.AppointmentTypeId, ct);
          if (type is null) return Result<AppointmentTypeResult>.Failure(Error.NotFound("No such appointment type."));
          if (type.Version != command.ExpectedVersion)
              return Result<AppointmentTypeResult>.Failure(Error.VersionConflict("The appointment type changed under you.", type.Version));

          var beforeVersion = type.Version;
          try
          {
              if (command.IsActive) type.Reactivate();
              else type.Deactivate(await blocking.AppointmentTypeUsageAsync(type.Id, ct));
          }
          catch (ReferenceDataInUseException ex)
          {
              return Result<AppointmentTypeResult>.Failure(Error.ReferenceDataInUse(ex.Message, ex.Blocking));
          }

          if (type.Version == beforeVersion)
              return Result<AppointmentTypeResult>.Success(new AppointmentTypeResult(
                  type.Id, type.Code, type.Name, type.IsActive, type.Version, null));

          audit.Record(AuditEntityTypes.AppointmentType, type.Id, AuditAction.AppointmentTypeUpdated,
              ActorType.Staff, command.StaffUserId.ToString(), $"isActive -> {type.IsActive}");
          await unitOfWork.SaveChangesAsync(ct);
          return Result<AppointmentTypeResult>.Success(new AppointmentTypeResult(
              type.Id, type.Code, type.Name, type.IsActive, type.Version, null));
      }
  }

  // ManagerDisplayName falls back to StaffId when the identity carries no display
  // name — the same projection the settings handler already uses.
  public sealed class ListAppointmentTypesHandler(
      IAppointmentTypeRepository types,
      IStaffAccessProfileRepository profiles,
      IStaffIdentityRepository identities)
  {
      public async Task<Result<IReadOnlyList<AppointmentTypeListItem>>> HandleAsync(CancellationToken ct)
      {
          var rows = await types.ListAsync(ct);
          var managerByType = (await profiles.ListAsync(ct))
              .Where(p => p.IsManager && p.AppointmentTypeId is not null)
              .ToDictionary(p => p.AppointmentTypeId!.Value, p => p.StaffUserId);
          var identityByUserId = (await identities.ListAsync(ct)).ToDictionary(i => i.StaffUserId);

          return Result<IReadOnlyList<AppointmentTypeListItem>>.Success(
              rows.OrderBy(t => t.Code, StringComparer.Ordinal)
                  .Select(t =>
                  {
                      var managerUserId = managerByType.TryGetValue(t.Id, out var found) ? found : (Guid?)null;
                      var identity = managerUserId is null ? null : identityByUserId.GetValueOrDefault(managerUserId.Value);
                      return new AppointmentTypeListItem(t.Id, t.Code, t.Name, t.IsActive,
                          identity?.DisplayName ?? identity?.StaffId.Value);
                  })
                  .ToList());
      }
  }
  ```

  ```csharp
  // src/EventBooking.Application/ReferenceData/AttendeeGroupHandlers.cs (complete)
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Access;
  using EventBooking.Application.Common;
  using EventBooking.Domain.Attendees;
  using EventBooking.Domain.AttendeeGroups;
  using EventBooking.Domain.Audit;
  using EventBooking.Domain.Common;

  namespace EventBooking.Application.ReferenceData;

  public sealed class CreateAttendeeGroupHandler(
      IAttendeeGroupRepository groups,
      IAppointmentTypeRepository types,
      IStaffAccessAuthorizer access,
      IUnitOfWork unitOfWork,
      IAuditLogger audit,
      IReferenceDataBlockingQueries blocking)
  {
      public async Task<Result<AttendeeGroupResult>> HandleAsync(CreateAttendeeGroupCommand command, CancellationToken ct)
      {
          var authorized = await access.AuthorizeAsync(command.StaffUserId, StaffCapability.ManageReferenceData, null, ct);
          if (authorized.IsFailure) return Result<AttendeeGroupResult>.Failure(authorized.Error);

          var activeIds = (await types.ListAsync(ct)).Where(t => t.IsActive).Select(t => t.Id).ToList();

          AttendeeGroup group;
          try
          {
              group = AttendeeGroup.Create(Guid.NewGuid(), command.Code, command.Name, command.AppointmentTypeIds, activeIds);
          }
          catch (DomainException ex)
          {
              return Result<AttendeeGroupResult>.Failure(Error.Validation(ex.Message));
          }

          if (await groups.GetByCodeAsync(group.Code, ct) is not null)
              return Result<AttendeeGroupResult>.Failure(Error.Conflict($"An attendee group with code '{group.Code}' already exists."));

          groups.Add(group);
          audit.Record(AuditEntityTypes.AttendeeGroup, group.Id, AuditAction.AttendeeGroupCreated,
              ActorType.Staff, command.StaffUserId.ToString(), $"code {group.Code}");
          await unitOfWork.SaveChangesAsync(ct);
          return Result<AttendeeGroupResult>.Success(new AttendeeGroupResult(
              group.Id, group.Code, group.Name, group.IsActive, group.Version,
              group.RequiredAppointmentTypeIds, 0));
      }
  }

  public sealed class UpdateAttendeeGroupHandler(
      IAttendeeGroupRepository groups,
      IAppointmentTypeRepository types,
      IAttendeeRepository attendees,
      IInviteRepository invites,
      IStaffAccessAuthorizer access,
      IReferenceDataBlockingQueries blocking,
      IUnitOfWork unitOfWork,
      IAuditLogger audit,
      IClock clock)
  {
      public async Task<Result<AttendeeGroupResult>> HandleAsync(UpdateAttendeeGroupCommand command, CancellationToken ct)
      {
          var authorized = await access.AuthorizeAsync(command.StaffUserId, StaffCapability.ManageReferenceData, null, ct);
          if (authorized.IsFailure) return Result<AttendeeGroupResult>.Failure(authorized.Error);

          var group = await groups.GetAsync(command.AttendeeGroupId, ct);
          if (group is null) return Result<AttendeeGroupResult>.Failure(Error.NotFound("No such attendee group."));
          if (group.Version != command.ExpectedVersion)
              return Result<AttendeeGroupResult>.Failure(Error.VersionConflict("The attendee group changed under you.", group.Version));

          var changes = new List<string>();
          try
          {
              if (command.Name is not null) { group.Rename(command.Name); changes.Add("name"); }
              if (command.AppointmentTypeIds is not null)
              {
                  var activeIds = (await types.ListAsync(ct)).Where(t => t.IsActive).Select(t => t.Id).ToList();
                  var blockingMembers = await blocking.AttendeeGroupBlockingMemberCountAsync(group.Id, ct);
                  if (group.ReplaceRequirements(command.AppointmentTypeIds, activeIds, blockingMembers))
                  {
                      var rederived = await RederiveMembersAsync(group, ct);
                      changes.Add($"requirements; {rederived} members re-derived");
                  }
              }
          }
          catch (ReferenceDataInUseException ex) when (ex.Blocking.ContainsKey("blockingMembers"))
          {
              return Result<AttendeeGroupResult>.Failure(
                  Error.RequirementsLocked(ex.Message, ex.Blocking["blockingMembers"]));
          }
          catch (ReferenceDataInUseException ex)
          {
              return Result<AttendeeGroupResult>.Failure(Error.ReferenceDataInUse(ex.Message, ex.Blocking));
          }
          catch (DomainException ex)
          {
              return Result<AttendeeGroupResult>.Failure(Error.Validation(ex.Message));
          }

          if (changes.Count == 0)
              return Result<AttendeeGroupResult>.Success(await ToResultAsync(group, ct));

          var memberCount = await blocking.AttendeeGroupMemberCountAsync(group.Id, ct);
          audit.Record(AuditEntityTypes.AttendeeGroup, group.Id, AuditAction.AttendeeGroupUpdated,
              ActorType.Staff, command.StaffUserId.ToString(), $"{string.Join("; ", changes)}; {memberCount} members");
          await unitOfWork.SaveChangesAsync(ct);
          return Result<AttendeeGroupResult>.Success(new AttendeeGroupResult(
              group.Id, group.Code, group.Name, group.IsActive, group.Version,
              group.RequiredAppointmentTypeIds, memberCount));
      }

      // Every member's attendee lock is already held in ascending id order by
      // LockByGroupForUpdateAsync. Re-derive, supersede the pending initial invite,
      // and park members that are not already waiting. Booked members cannot reach
      // here: the blocking check above refused first. Returns the member count.
      private async Task<int> RederiveMembersAsync(AttendeeGroup group, CancellationToken ct)
      {
          var members = await attendees.LockByGroupForUpdateAsync(group.Id, ct);
          foreach (var member in members)
          {
              member.AssignAttendeeGroup(group);
              var pending = await invites.LockPendingInitialForAttendeeAsync(member.Id, ct);
              pending?.MarkSuperseded();
              if (member.Status != AttendeeStatus.NotYetInvited)
                  member.ResetToNotYetInvited(clock.UtcNow);
          }

          return members.Count;
      }

      private async Task<AttendeeGroupResult> ToResultAsync(AttendeeGroup group, CancellationToken ct) =>
          new(group.Id, group.Code, group.Name, group.IsActive, group.Version,
              group.RequiredAppointmentTypeIds, await blocking.AttendeeGroupMemberCountAsync(group.Id, ct));
  }

  public sealed class SetAttendeeGroupActiveHandler(
      IAttendeeGroupRepository groups,
      IStaffAccessAuthorizer access,
      IReferenceDataBlockingQueries blocking,
      IUnitOfWork unitOfWork,
      IAuditLogger audit)
  {
      public async Task<Result<AttendeeGroupResult>> HandleAsync(SetAttendeeGroupActiveCommand command, CancellationToken ct)
      {
          var authorized = await access.AuthorizeAsync(command.StaffUserId, StaffCapability.ManageReferenceData, null, ct);
          if (authorized.IsFailure) return Result<AttendeeGroupResult>.Failure(authorized.Error);

          var group = await groups.GetAsync(command.AttendeeGroupId, ct);
          if (group is null) return Result<AttendeeGroupResult>.Failure(Error.NotFound("No such attendee group."));
          if (group.Version != command.ExpectedVersion)
              return Result<AttendeeGroupResult>.Failure(Error.VersionConflict("The attendee group changed under you.", group.Version));

          var beforeVersion = group.Version;
          try
          {
              if (command.IsActive) group.Reactivate();
              else group.Deactivate(await blocking.AttendeeGroupMemberCountAsync(group.Id, ct));
          }
          catch (ReferenceDataInUseException ex)
          {
              return Result<AttendeeGroupResult>.Failure(Error.ReferenceDataInUse(ex.Message, ex.Blocking));
          }

          var memberCount = await blocking.AttendeeGroupMemberCountAsync(group.Id, ct);
          if (group.Version == beforeVersion)
              return Result<AttendeeGroupResult>.Success(new AttendeeGroupResult(
                  group.Id, group.Code, group.Name, group.IsActive, group.Version,
                  group.RequiredAppointmentTypeIds, memberCount));

          audit.Record(AuditEntityTypes.AttendeeGroup, group.Id, AuditAction.AttendeeGroupUpdated,
              ActorType.Staff, command.StaffUserId.ToString(), $"isActive -> {group.IsActive}");
          await unitOfWork.SaveChangesAsync(ct);
          return Result<AttendeeGroupResult>.Success(new AttendeeGroupResult(
              group.Id, group.Code, group.Name, group.IsActive, group.Version,
              group.RequiredAppointmentTypeIds, memberCount));
      }
  }

  public sealed class ListAttendeeGroupsHandler(
      IAttendeeGroupRepository groups,
      IReferenceDataBlockingQueries blocking)
  {
      public async Task<Result<IReadOnlyList<AttendeeGroupListItem>>> HandleAsync(CancellationToken ct)
      {
          var rows = await groups.ListAsync(ct);
          var items = new List<AttendeeGroupListItem>();
          foreach (var group in rows.OrderBy(g => g.Code, StringComparer.Ordinal))
          {
              items.Add(new AttendeeGroupListItem(group.Id, group.Code, group.Name, group.IsActive,
                  group.RequiredAppointmentTypeIds, await blocking.AttendeeGroupMemberCountAsync(group.Id, ct)));
          }

          return Result<IReadOnlyList<AttendeeGroupListItem>>.Success(items);
      }
  }
  ```

  ```csharp
  // src/EventBooking.Application/Settings/AdminSettingsHandler.cs: replace the command,
  // view and UpdateAsync with the version below. GetAsync is unchanged except that the
  // returned view gains InviteOptionCount and Version.
  public sealed record SettingsView(
      int InviteExpiryDays,
      int MaxAutoRetryCount,
      int InviteOptionCount,
      long Version,
      IReadOnlyList<AppointmentTypeView> AppointmentTypes);

  public sealed record SaveSystemSettingsCommand(
      Guid StaffUserId,
      int InviteExpiryDays,
      int MaxAutoRetryCount,
      int InviteOptionCount,
      long ExpectedVersion);

  public sealed record SystemSettingsResult(
      int InviteExpiryDays,
      int MaxAutoRetryCount,
      int InviteOptionCount,
      long Version);

  public sealed class SaveSystemSettingsHandler(
      ISystemSettingsRepository settings,
      IStaffAccessAuthorizer access,
      IUnitOfWork unitOfWork,
      IAuditLogger audit)
  {
      public async Task<Result<SystemSettingsResult>> HandleAsync(
          SaveSystemSettingsCommand command, CancellationToken ct)
      {
          var authorized = await access.AuthorizeAsync(
              command.StaffUserId, StaffCapability.ManageSettings, null, ct);
          if (authorized.IsFailure) return Result<SystemSettingsResult>.Failure(authorized.Error);

          var current = await settings.GetAsync(ct);
          if (current.Version != command.ExpectedVersion)
              return Result<SystemSettingsResult>.Failure(
                  Error.VersionConflict("The settings changed under you.", current.Version));

          var before = new SystemSettingsResult(
              current.InviteExpiryDays, current.MaxAutoRetryCount, current.InviteOptionCount, current.Version);
          try
          {
              current.Update(command.InviteExpiryDays, command.MaxAutoRetryCount, command.InviteOptionCount);
          }
          catch (DomainException ex)
          {
              return Result<SystemSettingsResult>.Failure(Error.Validation(ex.Message));
          }

          audit.Record(AuditEntityTypes.SystemSettings, Guid.Empty, AuditAction.SystemSettingsChanged,
              ActorType.Staff, command.StaffUserId.ToString(),
              $"expiryDays {before.InviteExpiryDays} -> {current.InviteExpiryDays}; "
              + $"retryCount {before.MaxAutoRetryCount} -> {current.MaxAutoRetryCount}; "
              + $"optionCount {before.InviteOptionCount} -> {current.InviteOptionCount}");
          await unitOfWork.SaveChangesAsync(ct);
          return Result<SystemSettingsResult>.Success(new SystemSettingsResult(
              current.InviteExpiryDays, current.MaxAutoRetryCount, current.InviteOptionCount, current.Version));
      }
  }
  ```

  Keep the existing `AdminSettingsHandler.GetAsync` (widening the view) and delete its
  UpdateAsync and UpdateSettingsCommand: the Api and Mcp call sites move to the new
  handler, reading ExpectedVersion from the GetAsync view they already load for the
  settings page. Update the two endpoint files to construct SaveSystemSettingsCommand
  and project `result.Value` (`inviteExpiryDays`, `maxAutoRetryCount`, `inviteOptionCount`,
  `version`); the failure mapping is unchanged.
  - `src/EventBooking.Api/Endpoints/AdminEndpoints.cs` and
    `src/EventBooking.Mcp/Tools/AdminTools.cs`, both complete below.

  ```csharp
  // src/EventBooking.Api/Endpoints/AdminEndpoints.cs — the settings save only. The request
  // gains the option count and the expected version; the GET, the group and every other
  // endpoint in the file are untouched, and the failure mapping stays ToResponse().
  public sealed record UpdateSettingsRequest(
      int InviteExpiryDays,
      int MaxAutoRetryCount,
      int InviteOptionCount,
      long ExpectedVersion);

  group.MapPut("/settings", async (
      UpdateSettingsRequest request,
      ICallerAccessor caller,
      AdminSettingsHandler handler,
      CancellationToken cancellationToken) =>
  {
      var result = await handler.SaveAsync(
          new SaveSystemSettingsCommand(
              caller.RequireStaffUserId(),
              request.InviteExpiryDays,
              request.MaxAutoRetryCount,
              request.InviteOptionCount,
              request.ExpectedVersion),
          cancellationToken);

      // The versioned result is projected, not swallowed: the settings page needs the new
      // version to send with its next save, and a caller that never sees it can only ever
      // collide on the second one.
      return result.IsSuccess
          ? Results.Ok(new
          {
              inviteExpiryDays = result.Value.InviteExpiryDays,
              maxAutoRetryCount = result.Value.MaxAutoRetryCount,
              inviteOptionCount = result.Value.InviteOptionCount,
              version = result.Value.Version,
          })
          : result.ToResponse();
  })
      .WithAgentMetadata("updateSettings")
      .Produces(200)
      .ProducesProblem(400)
      .ProducesProblem(403)
      .ProducesProblem(409);
  ```

  ```csharp
  // src/EventBooking.Mcp/Tools/AdminTools.cs — the update tool only. Same command, same
  // projection; the tool returns the values rather than a sentence, for the same reason.
  [McpServerTool(Name = "update_settings", Title = "Update settings", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
  [Description("Update the invite expiry window, the re-issue limit and the number of options per invite. Caller must be an admin; overwrites all three.")]
  public async Task<SystemSettingsResult> UpdateSettingsAsync(
      ICallerAccessor caller,
      AdminSettingsHandler handler,
      [Description("Invite expiry window in days.")] int inviteExpiryDays,
      [Description("Maximum number of times an unanswered invite is automatically re-issued.")] int maxAutoRetryCount,
      [Description("How many event options each invite offers, 1 to 5.")] int inviteOptionCount,
      [Description("The version last read, for optimistic concurrency.")] long expectedVersion,
      CancellationToken cancellationToken)
  {
      var result = await handler.SaveAsync(
          new SaveSystemSettingsCommand(
              caller.RequireStaffUserId(), inviteExpiryDays, maxAutoRetryCount,
              inviteOptionCount, expectedVersion),
          cancellationToken);
      result.ThrowIfFailure();
      return result.Value;
  }
  ```

  - `tests/EventBooking.Application.Tests/Settings/AdminSettingsAccessProfileTests.cs`:
    update positional SettingsView and UpdateSettingsCommand constructions for the new
    parameters.

  ```csharp
  // src/EventBooking.Infrastructure/Persistence/Queries/ReferenceDataBlockingQueries.cs
  using EventBooking.Application.ReferenceData;
  using EventBooking.Domain.AttendeeGroups;
  using EventBooking.Domain.Attendees;
  using EventBooking.Domain.Bookings;
  using EventBooking.Domain.Events;
  using EventBooking.Domain.Locations;
  using EventBooking.Domain.AppointmentTypes;
  using Microsoft.EntityFrameworkCore;

  namespace EventBooking.Infrastructure.Persistence.Queries;

  public sealed class ReferenceDataBlockingQueries(EventBookingDbContext context) : IReferenceDataBlockingQueries
  {
      public async Task<LocationUsage> LocationUsageAsync(Guid locationId, CancellationToken ct) =>
          new(
              await context.EventProposals.CountAsync(
                  p => p.Status == EventProposalStatus.Open && p.LocationId == locationId, ct),
              await context.Events.CountAsync(
                  e => e.Status == EventStatus.Active
                      && e.LocationId == locationId
                      && EF.Property<DateTimeOffset?>(e, "StartUtc") > DateTimeOffset.UtcNow, ct));

      // Joins go through the explicit join sets (configured in Task 9b), because the
      // listed-type and requirement collections are not EF-translatable projections.
      public async Task<AppointmentTypeUsage> AppointmentTypeUsageAsync(Guid typeId, CancellationToken ct)
      {
          var now = DateTimeOffset.UtcNow;
          var openProposals = await (
              from listed in context.Set<EventProposalAppointmentType>()
              join proposal in context.EventProposals on listed.ProposalId equals proposal.Id
              where listed.AppointmentTypeId == typeId && proposal.Status == EventProposalStatus.Open
              select proposal.Id).CountAsync(ct);
          var futureEvents = await (
              from capacity in context.EventCapacities
              join e in context.Events on capacity.EventId equals e.Id
              where capacity.AppointmentTypeId == typeId
                  && e.Status == EventStatus.Active
                  && EF.Property<DateTimeOffset?>(e, "StartUtc") > now
              select e.Id).Distinct().CountAsync(ct);
          var activeGroups = await (
              from requirement in context.Set<AttendeeGroupRequirement>()
              join g in context.AttendeeGroups on requirement.AttendeeGroupId equals g.Id
              where requirement.AppointmentTypeId == typeId && g.IsActive
              select g.Id).Distinct().CountAsync(ct);
          return new AppointmentTypeUsage(openProposals, futureEvents, activeGroups);
      }

      public Task<int> AttendeeGroupMemberCountAsync(Guid groupId, CancellationToken ct) =>
          context.Attendees.CountAsync(a => a.AttendeeGroupId == groupId, ct);

      public async Task<int> AttendeeGroupBlockingMemberCountAsync(Guid groupId, CancellationToken ct)
      {
          var memberIds = await context.Attendees
              .Where(a => a.AttendeeGroupId == groupId)
              .Select(a => a.Id)
              .ToListAsync(ct);
          return await context.Bookings.CountAsync(
              b => memberIds.Contains(b.AttendeeId)
                  && b.Status == BookingStatus.Active
                  && b.RecoveryOfBookingId == null, ct);
      }
  }
  ```

  Verify the join-entity property names (ProposalId, AppointmentTypeId,
  AttendeeGroupId) against `EventProposalAppointmentType.cs` and
  `AttendeeGroupRequirement.cs` before accepting the implementation; adjust the projection
  (not the counts) if they differ. `EF.Property<DateTimeOffset?>(e, "StartUtc")` reads the
  Task 11 shadow property by its `EventStartInstants.PropertyName`.

  Fakes (`tests/EventBooking.Application.Tests/Fakes/InMemoryRepositories.cs`): add
  InMemoryLocationRepository (Get/Add, code lookup trimming and upper-casing like the
  group fake, List ordered by code), Add plus GetByCodeAsync on the appointment-type
  fake, Add plus ListAsync on the group fake, and group-member locks on the
  attendee fake (filter by group, order by id). MemoryBlocking in
  `ReferenceDataTestDoubles.cs` implements the blocking port from settable values:

  ```csharp
  // tests/EventBooking.Application.Tests/ReferenceData/ReferenceDataTestDoubles.cs
  using EventBooking.Application.ReferenceData;
  using EventBooking.Domain.AppointmentTypes;
  using EventBooking.Domain.Locations;
  using EventBooking.Domain.Time;

  namespace EventBooking.Application.Tests.ReferenceData;

  public sealed class TestZones : IEventWindowZones
  {
      public static readonly TestZones Instance = new();
      public bool IsKnownZone(string timeZoneId) =>
          timeZoneId is "Europe/London" or "Asia/Tokyo";
      public LocalTimeValidity ValidityOf(DateOnly date, TimeOnly time, string timeZoneId) =>
          throw new NotImplementedException();
      public DateTimeOffset InstantOf(DateOnly date, TimeOnly time, string timeZoneId) =>
          throw new NotImplementedException();
      public DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId) =>
          throw new NotImplementedException();
      public string AbbreviationOf(DateTimeOffset instant, string timeZoneId) =>
          throw new NotImplementedException();
  }

  public sealed class MemoryBlocking : IReferenceDataBlockingQueries
  {
      public LocationUsage LocationUsageValue { get; set; } = LocationUsage.None;
      public AppointmentTypeUsage TypeUsageValue { get; set; } = AppointmentTypeUsage.None;
      public int MemberCount { get; set; }
      public int BlockingMembers { get; set; }

      public Task<LocationUsage> LocationUsageAsync(Guid locationId, CancellationToken ct) =>
          Task.FromResult(LocationUsageValue);
      public Task<AppointmentTypeUsage> AppointmentTypeUsageAsync(Guid typeId, CancellationToken ct) =>
          Task.FromResult(TypeUsageValue);
      public Task<int> AttendeeGroupMemberCountAsync(Guid groupId, CancellationToken ct) =>
          Task.FromResult(MemberCount);
      public Task<int> AttendeeGroupBlockingMemberCountAsync(Guid groupId, CancellationToken ct) =>
          Task.FromResult(BlockingMembers);
  }
  ```

  EF repositories (`Repositories.cs`): add the location repository (get by id, code lookup
  trimmed and upper-cased, full list, add), add plus code lookup on the
  appointment-type repository, add plus full list on the group repository, and
  group-member locks on the attendee repository (filter by group, order by id,
  `FOR UPDATE` through the Task 10 lock helper for the attendee level).

  Migration for the invite snapshot columns:

  ```bash
  dotnet ef migrations add InviteSettingsSnapshot --project src/EventBooking.Infrastructure \
    --startup-project src/EventBooking.Api
  ```

  Read the generated migration before accepting it. It must backfill the true historical
  values — every existing invite was issued while settings were uneditable defaults — then
  drop the column defaults so new rows must state their own:

  ```sql
  UPDATE invite SET invite_expiry_days = 7, max_auto_retry_count = 2, invite_option_count = 3;
  ALTER TABLE invite ALTER COLUMN invite_expiry_days DROP DEFAULT;
  ALTER TABLE invite ALTER COLUMN max_auto_retry_count DROP DEFAULT;
  ALTER TABLE invite ALTER COLUMN invite_option_count DROP DEFAULT;
  ```

  Verify the generated column names against `InviteConfiguration.cs` before accepting; adjust
  the SQL (not the values) if they differ.

- [ ] **Step 4: Run.** Expected: PASS — the six new suites plus the full solution.

  ```bash
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  ```

  Expect the Application count to rise (five new suites) and Infrastructure to rise (two new
  suites). The executor's counts will differ from any number quoted here: what matters is
  green with zero skipped. A count that does not match the executor's own before/after diff
  is a signal to read the diff, not to adjust the number.

  ```csharp
  // src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs — add this
  // class; the repositories already in the file are untouched. Same shape as the group
  // repository it sits beside, so the two read the same way.
  /// <summary>Persists Admin-managed locations.</summary>
  /// <param name="context">The context to read and write through.</param>
  public sealed class LocationRepository(EventBookingDbContext context) : ILocationRepository
  {
      /// <summary>Gets a location by identifier, including inactive rows.</summary>
      public Task<Location?> GetAsync(Guid id, CancellationToken cancellationToken) =>
          context.Locations.SingleOrDefaultAsync(l => l.Id == id, cancellationToken);

      /// <summary>Gets a location from a trimmed case-insensitive canonical-code input.</summary>
      public Task<Location?> GetByCodeAsync(string code, CancellationToken cancellationToken)
      {
          var normalized = code.Trim().ToUpperInvariant();
          return context.Locations.SingleOrDefaultAsync(
              l => l.Code == normalized, cancellationToken);
      }

      /// <summary>Lists every location, inactive included, ordered by display name.</summary>
      public async Task<IReadOnlyList<Location>> ListAsync(CancellationToken cancellationToken) =>
          await context.Locations.OrderBy(l => l.Name).ToListAsync(cancellationToken);

      public void Add(Location location) => context.Locations.Add(location);
  }
  ```

  ```csharp
  // tests/EventBooking.Application.Tests/Fakes/InMemoryRepositories.cs — add this class
  // beside the other fakes. The code comparison is ordinal against an already-uppercased
  // input, matching what the EF repository asks the database for.
  public sealed class InMemoryLocationRepository : ILocationRepository
  {
      public List<Location> Items { get; } = [];

      public Task<Location?> GetAsync(Guid id, CancellationToken cancellationToken) =>
          Task.FromResult(Items.SingleOrDefault(l => l.Id == id));

      public Task<Location?> GetByCodeAsync(string code, CancellationToken cancellationToken)
      {
          var normalized = code.Trim().ToUpperInvariant();
          return Task.FromResult(
              Items.SingleOrDefault(l => string.Equals(l.Code, normalized, StringComparison.Ordinal)));
      }

      public Task<IReadOnlyList<Location>> ListAsync(CancellationToken cancellationToken) =>
          Task.FromResult<IReadOnlyList<Location>>([.. Items.OrderBy(l => l.Name)]);

      public void Add(Location location) => Items.Add(location);
  }
  ```

  ```csharp
  // tests/EventBooking.Application.Tests/Settings/AdminSettingsAccessProfileTests.cs —
  // this suite constructs SettingsView and the update command positionally, so both gain
  // the option count and the expected version. Its assertions are unchanged: it covers who
  // may reach settings, not what settings hold.
  //
  // Every construction of the form
  //     new SettingsView(inviteExpiryDays, maxAutoRetryCount)
  // becomes
  //     new SettingsView(inviteExpiryDays, maxAutoRetryCount, inviteOptionCount, version)
  // and every
  //     new UpdateSettingsCommand(staffUserId, inviteExpiryDays, maxAutoRetryCount)
  // becomes
  //     new SaveSystemSettingsCommand(
  //         staffUserId, inviteExpiryDays, maxAutoRetryCount, inviteOptionCount, expectedVersion)
  // with inviteOptionCount 3 and expectedVersion 1, the seeded defaults, so no case in this
  // suite changes meaning.
  ```

- [ ] **Step 5: Commit and push** the executor's code — not the plan documents. Stage the
  source and test files this task created or modified, review the cached diff, and commit
  under the master plan's message:

  ```bash
  test -z "$(git status --porcelain --ignored=no | grep -v '^??')"
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  git add src/EventBooking.Application/Common/Error.cs src/EventBooking.Application/Access/StaffCapability.cs src/EventBooking.Application/Access/StaffAccessAuthorizer.cs src/EventBooking.Domain/Audit/AuditAction.cs src/EventBooking.Domain/Audit/AuditEntityTypes.cs src/EventBooking.Domain/Attendees/Attendee.cs src/EventBooking.Domain/Invites/Invite.cs src/EventBooking.Application/Abstractions/ILocationRepository.cs src/EventBooking.Application/Abstractions/IAppointmentTypeRepository.cs src/EventBooking.Application/Abstractions/IAttendeeGroupRepository.cs src/EventBooking.Application/Abstractions/IAttendeeRepository.cs src/EventBooking.Application/ReferenceData/ src/EventBooking.Application/Settings/AdminSettingsHandler.cs src/EventBooking.Api/Endpoints/AdminEndpoints.cs src/EventBooking.Mcp/Tools/AdminTools.cs src/EventBooking.Infrastructure/Persistence/Queries/ReferenceDataBlockingQueries.cs src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs tests/EventBooking.Application.Tests/Fakes/InMemoryRepositories.cs tests/EventBooking.Application.Tests/Settings/AdminSettingsAccessProfileTests.cs tests/EventBooking.Application.Tests/ReferenceData/ tests/EventBooking.Application.Tests/Settings/SettingsHandlerTests.cs tests/EventBooking.Infrastructure.Tests/Queries/ReferenceDataBlockingQueryTests.cs tests/EventBooking.Infrastructure.Tests/Groups/ReplaceAttendeeGroupRequirementsTests.cs
  git diff --cached --name-only
  git diff --cached
  test -n "$EXECUTOR_COAUTHOR"
  git commit -m "feat(app): reference-data and settings use cases

  Co-authored-by: $EXECUTOR_COAUTHOR"
  git push
  ```
