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

**Goal:** Every reference-data write runs in one transaction, takes no row locks below its own
aggregate (reference-data rows are guarded by optimistic concurrency, not by the ordered row
locks, which start at the attendee level), calls the Task 5 domain methods, writes exactly one
audit entry in the transaction, and returns a result DTO carrying the updated version. Reads are
open to any staff member; writes demand `ManageReferenceData`; settings demand `ManageSettings`.

**Architecture:** Handlers live in `src/EventBooking.Application/ReferenceData/` and the existing
`src/EventBooking.Application/Settings/` handler is extended. Blocking counts (open proposals,
future active events, mapped groups, member counts) come from read-model queries in
`src/EventBooking.Infrastructure/Persistence/Queries/`, implemented once against PostgreSQL and
faked in memory for Application tests. Replacing a group's requirement set re-derives every
member's `AttendeeRequirement` rows, supersedes each member's pending initial invite, and moves
affected members to `NotYetInvited` — all in the one transaction, with each member's attendee
lock taken in ascending id order first. A member holding an active original booking blocks the
change instead (`requirements-locked`, FR-1.5). No domain entity leaves any handler.

**Tech Stack:** .NET 10, xUnit, EF Core, PostgreSQL Testcontainers.

**Spec:** [Master Task 12](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[domain model](../design/01-domain-model.md), [functional requirements](../design/02-functional-requirements.md),
[solution architecture](../design/04-solution-architecture.md), [ontology](../ontology.md).

## Boundary

Task 12 owns reference data and settings only. Attendee CRUD, import and boundary work stay with
Task 20, which will probably need lettered splits (contradiction #5, settled with the user before
this document was written). Do not add attendee CRUD handlers here.

The master plan writes appointment types as MED, FIT and IND. The prototype carries the
predecessor's three seeded rows (DAT, MED, UNI) until Phase 3; this task's tests use the plan's
MED, FIT and IND codes as Admin-managed rows created through the new handlers, and state the
mapping where a seeded row is referenced. The fixed-identifier constants retire here: handlers
and seed data must not reference them after this task.

## Global constraints

One transaction per command; locks in the canonical order (attendee locks ascending where the
group replacement takes them — reference-data rows themselves take none); audit written in the
transaction; no domain entity leaves the handler; exactly one StaffCapability per handler.
`Location` zone changes still require a known IANA zone and refuse while open proposals or
future events reference the row, carrying both counts (`in-use`, FR-1.2). `AppointmentType`
deactivation refuses while open proposals list it, future active events list it, or active
groups map it, carrying the group count (`in-use`, FR-1.6). A stale `expectedVersion` returns
`version-conflict` with the current state (409). Settings outside range are refused per field;
valid settings never touch existing invites — the invite snapshots the three settings values at
issue (contradiction #9, settled with the user; the ontology carries `inviteExpiryDays`,
`maxAutoRetryCount` and `inviteOptionCount` on the invite, and Task 14's issuer writes them).

## Review focus

STOP AND CHECK four things. The group-requirement replacement is proved by the two-member case
with a pending initial invite on one of them — both members re-derived, the invite superseded,
both `NotYetInvited`, one transaction — and by the blocked case with an active original booking,
which changes nothing. The zone-change refusal carries both counts (1 open proposal, 2 future
events), not a bare refusal. The settings test that matters is the negative one: valid settings
do not alter existing invites. And every write emits exactly one audit entry with old and new
values and no names or emails.

### Task 12: Reference-data and settings use cases

**Files:**

- Create: src/EventBooking.Application/ReferenceData/LocationHandlers.cs
- Create: src/EventBooking.Application/ReferenceData/AppointmentTypeHandlers.cs
- Create: src/EventBooking.Application/ReferenceData/AttendeeGroupHandlers.cs
- Create: src/EventBooking.Application/ReferenceData/ReferenceDataQueries.cs
- Modify: src/EventBooking.Application/Settings/AdminSettingsHandler.cs
- Create: src/EventBooking.Infrastructure/Persistence/Queries/ReferenceDataBlockingQueries.cs
- Test: tests/EventBooking.Application.Tests/ReferenceData/LocationHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/ReferenceData/AppointmentTypeHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/ReferenceData/AttendeeGroupHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Settings/SettingsHandlerTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/Queries/ReferenceDataBlockingQueryTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/Groups/ReplaceAttendeeGroupRequirementsTests.cs

**Interfaces:**

These complete types define the changed public boundary and its domain behavior. Apply them
after the failing test, not before.

```csharp
namespace EventBooking.Application.ReferenceData;

// Location commands. Code is immutable after creation; renames and address
// changes are always allowed; zone changes and deactivation carry blocking
// counts. Every result carries the updated version.
public sealed record CreateLocationCommand(
    Guid StaffUserId,
    string Code,          // any case; canonicalised to upper snake case
    string Name,          // at most 100 chars
    string Address,       // at most 500 chars
    string TimeZoneId);   // must be a known IANA zone

public sealed record UpdateLocationCommand(
    Guid StaffUserId,
    Guid LocationId,
    string? Name,         // null means unchanged
    string? Address,      // null means unchanged
    string? TimeZoneId,   // null means unchanged; change refused with
                          // in-use (openProposals, futureEvents) while scheduled
    long ExpectedVersion);

public sealed record SetLocationActiveCommand(
    Guid StaffUserId,
    Guid LocationId,
    bool IsActive,        // false = deactivate (refused with in-use while
                          // scheduled); true = reactivate (always allowed)
    long ExpectedVersion);

public sealed record LocationResult(
    Guid Id,
    string Code,
    string Name,
    string Address,
    string TimeZoneId,
    bool IsActive,
    long Version);

// Appointment-type commands. Same version/conflict shape as locations;
// deactivation carries the blocking group/proposal/event counts.
public sealed record CreateAppointmentTypeCommand(
    Guid StaffUserId,
    string Code,          // any case; at most 50 chars
    string Name);         // at most 100 chars

public sealed record UpdateAppointmentTypeCommand(
    Guid StaffUserId,
    Guid AppointmentTypeId,
    string? Name,         // null means unchanged
    long ExpectedVersion);

public sealed record SetAppointmentTypeActiveCommand(
    Guid StaffUserId,
    Guid AppointmentTypeId,
    bool IsActive,        // false refused with in-use (openProposals,
                          // futureEvents, mappedGroups) while referenced
    long ExpectedVersion);

public sealed record AppointmentTypeResult(
    Guid Id,
    string Code,
    string Name,
    bool IsActive,
    long Version,
    string? ManagerDisplayName); // null when no Manager holds the type;
                                 // falls back to StaffId when the identity
                                 // carries no display name

// Attendee-group commands. Requirement replacement re-derives members (see
// below); deactivation refuses while members exist.
public sealed record CreateAttendeeGroupCommand(
    Guid StaffUserId,
    string Code,                       // any case; at most 50 chars
    string Name,                       // at most 200 chars
    IReadOnlyList<Guid> AppointmentTypeIds); // 1+, distinct, all active

public sealed record UpdateAttendeeGroupCommand(
    Guid StaffUserId,
    Guid AttendeeGroupId,
    string? Name,                      // null means unchanged
    IReadOnlyList<Guid>? AppointmentTypeIds,
        // null means unchanged; a changed set runs the replacement below
        // and is refused with requirements-locked (blockingMembers) while
        // any member holds an active original booking
    long ExpectedVersion);

public sealed record SetAttendeeGroupActiveCommand(
    Guid StaffUserId,
    Guid AttendeeGroupId,
    bool IsActive,        // false refused with in-use (members) while assigned
    long ExpectedVersion);

public sealed record AttendeeGroupResult(
    Guid Id,
    string Code,
    string Name,
    bool IsActive,
    long Version,
    IReadOnlyList<Guid> RequirementTypeIds, // in ascending id order
    int MemberCount);

// List queries. Open to any staff member; no capability demanded.
public sealed record LocationListItem(
    Guid Id, string Code, string Name, bool IsActive);

public sealed record AttendeeGroupListItem(
    Guid Id, string Code, string Name, bool IsActive,
    IReadOnlyList<Guid> RequirementTypeIds, int MemberCount);

// Blocking counts behind the in-use refusals. One row per reference row;
// counts are exact, not estimates.
public interface IReferenceDataBlockingQueries
{
    // Open proposals and future active events naming the location.
    Task<LocationUsage> LocationUsageAsync(Guid locationId, CancellationToken ct);
    // Open proposals listing the type, future active events listing it,
    // and active groups mapping it.
    Task<AppointmentTypeUsage> AppointmentTypeUsageAsync(Guid typeId, CancellationToken ct);
    // Attendees assigned to the group; and the subset holding an active
    // original booking (the requirements-locked blockers).
    Task<int> AttendeeGroupMemberCountAsync(Guid groupId, CancellationToken ct);
    Task<int> AttendeeGroupBlockingMemberCountAsync(Guid groupId, CancellationToken ct);
}
```

```csharp
namespace EventBooking.Application.Settings;

// Settings save. inviteOptionCount is newly editable here (1-5); the old
// pass-through of the stored value retires with this task.
public sealed record SaveSystemSettingsCommand(
    Guid StaffUserId,
    int InviteExpiryDays,   // 1-60
    int MaxAutoRetryCount,  // 0-10
    int InviteOptionCount,  // 1-5
    long ExpectedVersion);

public sealed record SystemSettingsResult(
    int InviteExpiryDays,
    int MaxAutoRetryCount,
    int InviteOptionCount,
    long Version);
```

- [ ] **Step 1: Write the failing test.** Create the six test files above. Each suite follows the
  handler pattern (fake ports, fake unit of work, recording audit logger): authorize first
  (writes demand `ManageReferenceData`, settings demand `ManageSettings`, lists demand nothing),
  open the unit of work, call the Task 5 domain method, write the audit entry, commit, return
  the DTO. Required cases, one test per rule:

  ```csharp
  // tests/EventBooking.Application.Tests/ReferenceData/LocationHandlerTests.cs
  // (representative file — the other five follow the same shape)
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Common;
  using EventBooking.Application.ReferenceData;
  using EventBooking.Domain.Locations;

  namespace EventBooking.Application.Tests.ReferenceData;

  public sealed class LocationHandlerTests
  {
      // Each create and update writes exactly one audit entry naming the
      // action (LocationCreated / LocationUpdated) with old and new values
      // and no names or emails. (Pattern case; repeat per handler.)
      [Fact]
      public async Task Create_location_writes_LocationCreated_with_no_personal_data()
      {
          var fixture = ReferenceDataFixture.Create();
          var command = new CreateLocationCommand(
              fixture.AdminUserId, "london_hq", "London HQ", "1 High St", "Europe/London");

          var result = await fixture.Locations.CreateAsync(command, CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal("LONDON_HQ", result.Value.Code);
          Assert.Equal(1, fixture.Audit.Entries.Count(e => e.Action == "LocationCreated"));
          Assert.DoesNotContain(fixture.Audit.Entries, e => e.ContainsPersonalData());
      }

      // A zone change with 1 open proposal and 2 future events returns
      // in-use with those counts and changes nothing.
      [Fact]
      public async Task Zone_change_while_scheduled_returns_in_use_with_both_counts()
      {
          var fixture = ReferenceDataFixture.Create()
              .WithLocationUsage(openProposals: 1, futureEvents: 2);
          var command = new UpdateLocationCommand(
              fixture.AdminUserId, fixture.LocationId,
              Name: null, Address: null, TimeZoneId: "Asia/Tokyo", ExpectedVersion: 1);

          var result = await fixture.Locations.UpdateAsync(command, CancellationToken.None);

          Assert.Equal("in-use", result.Error.Type);
          Assert.Equal(1, result.Error.Count("openProposals"));
          Assert.Equal(2, result.Error.Count("futureEvents"));
          Assert.Equal("Europe/London", fixture.StoredLocation.TimeZoneId);
      }

      // A stale expectedVersion returns version-conflict with the current state.
      [Fact]
      public async Task Stale_version_returns_current_state()
      {
          var fixture = ReferenceDataFixture.Create();
          var command = new UpdateLocationCommand(
              fixture.AdminUserId, fixture.LocationId,
              Name: "Renamed", Address: null, TimeZoneId: null, ExpectedVersion: 99);

          var result = await fixture.Locations.UpdateAsync(command, CancellationToken.None);

          Assert.Equal("version-conflict", result.Error.Type);
          Assert.Equal(fixture.StoredLocation.Version, result.Error.CurrentVersion);
      }
  }
  ```

  The remaining suites cover: deactivating a type mapped by an active group returns `in-use`
  naming the group count; a group requirement change with 2 members, one holding a pending
  initial invite, re-derives both members' requirements, supersedes the invite, moves both to
  `NotYetInvited`, all in one transaction; the same change with one member holding an active
  original booking is refused with count 1 and nothing changes; settings outside range are
  refused per field; valid settings do not alter existing invites (create an invite, save new
  settings, assert the invite's snapshotted `inviteExpiryDays`, `maxAutoRetryCount` and
  `inviteOptionCount` are unchanged). The Infrastructure suites assert the blocking-count
  queries against real PostgreSQL and the member re-derivation path end to end.

- [ ] **Step 2: Run.** Expected: FAIL — none of the handlers exist and the settings command has
  no `inviteOptionCount`.

  ```bash
  dotnet test tests/EventBooking.Application.Tests --filter "FullyQualifiedName~ReferenceData|FullyQualifiedName~Settings"
  ```

- [ ] **Step 3: Implement.** Create the three handler files and the queries interface; extend
  the settings handler with the new field, the expected-version check and the
  `SystemSettingsChanged` audit entry; implement the PostgreSQL blocking-count queries.
  ReplaceAttendeeGroupRequirements takes each member's attendee lock in ascending id order
  before changing anything. Retire the fixed-identifier constants and the settings
  pass-through; retire the transitional `inviteOptionCount`-stored-but-not-editable state.

- [ ] **Step 4: Run.** Expected: PASS — the new suites plus the full solution.

  ```bash
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  ```

  Expect the Application count to rise (new handler suites) and Infrastructure to rise (new
  query suites). A count that does not match after the change is a signal to read the diff,
  not to adjust the number.

- [ ] **Step 5: Commit and push.**

  ```bash
  test -z "$(git status --porcelain --ignored=no | grep -v '^??')"
  node --input-type=module <<'LINT_PLANS'
  import fs from 'node:fs';
  import {execFileSync} from 'node:child_process';
  const directory = 'docs/detailed-implementations';
  const files = fs.readdirSync(directory).filter(name => name.endsWith('.md')).map(name => directory + '/' + name);
  process.stdout.write(execFileSync('node', ['scripts/check-ontology-terms.mjs', '--also', ...files], {encoding:'utf8', maxBuffer:1e7}));
  LINT_PLANS
  git add docs/detailed-implementations/phase-3a-reference-data-settings.md docs/detailed-implementations/phase-3-application.md docs/detailed-implementations/README.md docs/detailed-implementations/HANDOVER.md docs/ontology.ttl docs/ontology.md
  git diff --cached --name-only
  git diff --cached
  test -n "$EXECUTOR_COAUTHOR"
  git commit -m "docs(plans): Task 12 reference-data and settings handlers

  Co-authored-by: $EXECUTOR_COAUTHOR"
  git push
  ```
