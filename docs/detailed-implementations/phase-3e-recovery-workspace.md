# 03e — Recovery and the appointment workspace (Task 16)

[← Phase overview](phase-3-application.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This task follows Task 15. The ported start-recovery, cancel-recovery, recovery requirement
selector and workspace handlers and queries move under `src/EventBooking.Application/Recovery/`
and `Appointments/`, generalised across locations and scoped to the caller's type.

> Use superpowers:executing-plans. This task is hand-authored: complete code and complete tests
> are written straight into this document, with no prototype. Compile and test-drive them
> yourself. The test counts below are what you should expect to reach, not figures observed by
> the author — nothing here has been run.

**Goal:** StartRecovery (attendee id, additional location ids) defaulting to the original
booking's location and snapshotting only recoverable types; CancelRecoveryInvite; the
recovery requirement selector revalidated under lock; ListWorkspaceEvents (location id
optional) bounded to end instants between 7 days ago and 14 days ahead, grouped by location,
defaulting to the nearest current or next; GetWorkspaceRoster (event id); SetAppointmentStatus
(appointment id, target status, expected version); DownloadRoster (event id) as CSV with
formula-cell neutralisation.

**Architecture:** One transaction per command; attendee lock first, then the recovery invite
where one exists. A second concurrent recovery is refused with `recovery-active`. Recovery
bookings conclude when all appointments are terminal (`RecoveryBookingConcluded`), picked up by
the Task 19 sweep. The workspace roster exposes only name, email, status, timestamps and
version — never attendee, booking or invite identifiers. Check-in is judged on the event's
local date in the location's zone; `NoShow` only after the end instant. No domain entity
leaves the handler.

**Tech Stack:** .NET 10, xUnit, EF Core, PostgreSQL Testcontainers.

**Spec:** [Master Task 16](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[domain model](../design/01-domain-model.md), [functional requirements](../design/02-functional-requirements.md),
[solution architecture](../design/04-solution-architecture.md), [ontology](../ontology.md).

## Capabilities (contradiction #4, settled with the user)

Recovery handlers demand `ManageAttendees` (Coordinator recovery work); the workspace
handlers and queries demand `ConductAppointments`, scoped to the caller's type across every
location. This settles contradiction #4 as a split: the master plan's single-capability
wording is not followed where recovery is concerned.

## Global constraints

One transaction per command; canonical lock order; audit in the transaction; no domain entity
leaves the handler. Correcting `NoShow` to `Expected` is refused while a later recovery is
pending. An AppointmentStaff profile for MED never sees FIT rows — assert on the serialised
JSON. Roster CSV neutralises cells starting with `=`, `+`, `-`, `@`, tab and carriage return.

## Review focus

STOP AND CHECK four things. Recovery defaults to the original booking's location and unions
additional ones without duplicates. The Dublin check-in case (clock at 23:30 UTC the day
before in summer — allowed, since it is 00:30 local) proves zone-judged dates. The roster JSON
contains no attendee, booking or invite identifiers. And the CSV neutralisation covers all six
prefixes.

### Task 16: Recovery and workspace across locations

**Files:**

- Create: src/EventBooking.Application/Recovery/StartRecoveryHandler.cs
- Create: src/EventBooking.Application/Recovery/CancelRecoveryInviteHandler.cs
- Create: src/EventBooking.Application/Recovery/RecoveryRequirementSelector.cs
- Create: src/EventBooking.Application/Appointments/WorkspaceHandlers.cs
- Create: src/EventBooking.Infrastructure/Persistence/Queries/WorkspaceQueries.cs
- Delete: the ported recovery and workspace files they replace
- Test: tests/EventBooking.Application.Tests/Recovery/RecoveryHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Appointments/WorkspaceHandlerTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/Queries/WorkspaceQueryTests.cs

**Interfaces:**

These complete types define the changed public boundary and its domain behavior. Apply them
after the failing test, not before.

```csharp
namespace EventBooking.Application.Recovery;

// Recovery is Coordinator work under ManageAttendees (contradiction #4).
// Additional locations union with the original booking's location;
// duplicates collapse. Only still-outstanding no-show types are
// snapshotted; a second concurrent recovery is refused with
// recovery-active.
public sealed record StartRecoveryCommand(
    Guid StaffUserId,
    Guid AttendeeId,
    IReadOnlyList<Guid> AdditionalLocationIds);

public sealed record StartRecoveryOutcome(
    Guid RecoveryInviteId,
    IReadOnlyList<Guid> LocationIds,
    IReadOnlyList<Guid> RecoverableTypeIds);

public sealed record CancelRecoveryInviteCommand(
    Guid StaffUserId,
    Guid RecoveryInviteId);
```

```csharp
namespace EventBooking.Application.Appointments;

// Workspace work is ConductAppointments, scoped to the caller's type across
// every location. Events list the caller's type; the window is bounded to
// end instants between 7 days ago and 14 days ahead, grouped by location,
// defaulting to the nearest current or next event.
public sealed record ListWorkspaceEventsQuery(
    Guid StaffUserId,
    Guid? LocationId); // null means every location

public sealed record WorkspaceEventView(
    Guid EventId,
    Guid LocationId,
    string LocationName,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string ZoneAbbreviation);

// The roster carries no identifiers but its own row version: name, email,
// status, timestamps and version only.
public sealed record WorkspaceRosterRow(
    string Name,
    string Email,
    string AppointmentStatus,
    DateTimeOffset? CheckedInAt,
    long Version);

public sealed record SetAppointmentStatusCommand(
    Guid StaffUserId,
    Guid AppointmentId,
    string TargetStatus, // Expected | CheckedIn | NoShow
    long ExpectedVersion);
```

- [ ] **Step 1: Write the failing tests.** Create the two Application test files. Required
  cases, one test per rule:

  ```csharp
  // tests/EventBooking.Application.Tests/Recovery/RecoveryHandlerTests.cs
  // (representative file — the workspace suite follows the same shape)
  using EventBooking.Application.Common;
  using EventBooking.Application.Recovery;

  namespace EventBooking.Application.Tests.Recovery;

  public sealed class RecoveryHandlerTests
  {
      // Recovery defaults to the original booking's location, unions extra
      // ones, and snapshots only recoverable types.
      [Fact]
      public async Task Start_recovery_defaults_to_booking_location_and_snapshots_recoverable()
      {
          var fixture = RecoveryFixture.Create()
              .WithBookingAtLocation("LONDON_HQ", ["MED", "FIT"])
              .WithNoShow("MED");
          var command = new StartRecoveryCommand(
              fixture.CoordinatorUserId, fixture.AttendeeId, [fixture.TokyoLocationId]);

          var result = await fixture.StartAsync(command, CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Contains(fixture.LondonLocationId, result.Value.LocationIds);
          Assert.Contains(fixture.TokyoLocationId, result.Value.LocationIds);
          Assert.Equal([fixture.MedTypeId], result.Value.RecoverableTypeIds);
      }

      // A second concurrent recovery is refused with recovery-active.
      [Fact]
      public async Task Second_recovery_while_active_is_refused()
      {
          var fixture = RecoveryFixture.Create().WithActiveRecovery();
          var command = new StartRecoveryCommand(
              fixture.CoordinatorUserId, fixture.AttendeeId, []);

          var result = await fixture.StartAsync(command, CancellationToken.None);

          Assert.Equal("recovery-active", result.Error.Type);
      }

      // Recovery demands ManageAttendees, not ConductAppointments.
      [Fact]
      public async Task Recovery_without_manage_attendees_is_forbidden()
      {
          var fixture = RecoveryFixture.Create().WithoutCapability("ManageAttendees");
          var command = new StartRecoveryCommand(
              fixture.CoordinatorUserId, fixture.AttendeeId, []);

          var result = await fixture.StartAsync(command, CancellationToken.None);

          Assert.Equal("forbidden", result.Error.Type);
      }
  }
  ```

  The remaining suites cover: a recovery booking with all appointments terminal concludes
  (`RecoveryBookingConcluded`); check-in allowed only on the event's local date (the Dublin
  23:30 UTC summer case); `NoShow` allowed only after the end instant; correcting `NoShow`
  to `Expected` refused while a later recovery is pending; the roster JSON contains no
  attendee, booking or invite ids; the roster CSV neutralises all six prefixes; a MED
  profile never sees FIT rows.

- [ ] **Step 2: Run.** Expected: FAIL — the Recovery and Appointments handlers do not exist.

  ```bash
  dotnet test tests/EventBooking.Application.Tests --filter "FullyQualifiedName~Recovery|FullyQualifiedName~Appointments"
  ```

- [ ] **Step 3: Implement.** Create the handler and query files; delete the ported files.
  Scope every workspace read to the caller's type; judge every time rule in the location's
  zone.

- [ ] **Step 4: Run.** Expected: PASS — the new suites plus the full solution.

  ```bash
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  ```

  Expect the Application count to rise (new suites) and Infrastructure to rise (workspace
  query tests). A count that does not match after the change is a signal to read the diff,
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
  git add docs/detailed-implementations/phase-3e-recovery-workspace.md docs/detailed-implementations/phase-3-application.md docs/detailed-implementations/HANDOVER.md
  git diff --cached --name-only
  git diff --cached
  test -n "$EXECUTOR_COAUTHOR"
  git commit -m "docs(plans): Task 16 recovery and workspace handlers

  Co-authored-by: $EXECUTOR_COAUTHOR"
  git push
  ```
