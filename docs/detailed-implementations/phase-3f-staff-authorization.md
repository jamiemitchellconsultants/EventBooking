# 03f — Provider-neutral OIDC with per-request role sync (Task 17)

[← Phase overview](phase-3-application.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This task follows Task 16. Authentication becomes provider-neutral OIDC against a configurable
authority, and every request syncs roles into the staff access profile before authorization —
through one pipeline shared by REST and MCP.

> Use superpowers:executing-plans. This task is hand-authored: complete code and complete tests
> are written straight into this document, with no prototype. Compile and test-drive them
> yourself. The test counts below are what you should expect to reach, not figures observed by
> the author — nothing here has been run.

**Goal:** Configuration keys `Auth__Authority`, `Auth__Audience`, `Auth__Claims__StaffId`
(default `staff_id`), `Auth__Claims__Name` (`name`), `Auth__Claims__Roles` (`roles`),
`Identity__StaffIdPattern` (default `^[A-Z0-9]{1,32}$`). One authorization pipeline evaluating
in design 06's order. The capability matrix as a single table in code matching the design
exactly, including `ManageReferenceData` for Admin and scoped `CancelEvent` for Manager.
SetStaffScope (staff user id, type id or null, expected version) returning the displaced
Manager's display name when any. GetMe returning display name, `StaffId`, roles, scope and
capabilities — or a no-role response that explains the problem and writes no `StaffIdentity`.

**Architecture:** The bearer handler validates against the configured authority and audience;
claim mapping reads the configured names. Role sync runs before authorization on the same
request: role drift updates the profile and audits `StaffRolesSynced` as `System`; a token
with no roles removes the profile; a sync removing the last Admin is refused and logged. A
Manager role with a null scope gets no capability at all (FR-10.7). `StaffIdentity` refreshes
at most every 15 minutes. No domain entity leaves the handler.

**Tech Stack:** .NET 10, xUnit, WebApplicationFactory.

**Spec:** [Master Task 17](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[security and authentication](../design/06-security-and-authentication.md),
[ontology](../ontology.md).

## Missing staff identity (contradiction #3, settled with the user)

A token with no `staff_id` — or one not matching the configured pattern — gets 403 everywhere
except `/api/me`, which explains the problem and writes no `StaffIdentity`. This follows the
security document against the API catalogue's 401; Task 21's conventions implement the same
rule at the endpoint layer.

## Global constraints

Custom claim names from configuration are honoured everywhere. The generated
authorization-matrix test enumerates every capability-by-role combination against the design
table — adding a capability without updating the table fails the build. Assigning the MED
Manager scope to Jo while Sam holds it clears Sam atomically and names Sam; clearing the only
MED Manager is allowed.

## Review focus

STOP AND CHECK four things. The no-identity 403 test asserts no `StaffIdentity` row is
written. The role-drift test asserts the sync audit lands as `System` before the same
request authorizes. The matrix test is generated from the design table, not hand-listed. And
the last-Admin removal is refused and logged rather than applied.

### Task 17: Staff identity, role sync and authorization

**Files:**

- Modify: src/EventBooking.Api.Auth/ (provider-neutral OIDC; claim names from configuration)
- Create: src/EventBooking.Application/Access/CallerAccessor.cs
- Create: src/EventBooking.Application/Access/StaffIdentityRecorder.cs
- Create: src/EventBooking.Application/Access/RoleSyncHandler.cs
- Create: src/EventBooking.Application/Access/CapabilityMatrix.cs
- Create: src/EventBooking.Application/Access/StaffScopeHandler.cs
- Test: tests/EventBooking.Api.Tests/Auth/OidcAuthTests.cs
- Test: tests/EventBooking.Application.Tests/Access/RoleSyncTests.cs
- Test: tests/EventBooking.Application.Tests/Access/AuthorizationMatrixTests.cs
- Test: tests/EventBooking.Application.Tests/Access/StaffScopeHandlerTests.cs

**Interfaces:**

These complete types define the changed public boundary and its domain behavior. Apply them
after the failing test, not before.

```csharp
namespace EventBooking.Application.Access;

// The single table. Rows mirror design 06 exactly; the generated matrix
// test fails if code and design disagree.
public sealed record CapabilityGrant(
    string Capability,     // ManageReferenceData, ManageSettings,
                           // ManageEventNegotiation, ManageAttendees,
                           // CancelEvent, ConductAppointments,
                           // ViewEventAudit, ViewAttendeeAudit
    string Role,           // Admin, Coordinator, Manager, AppointmentStaff
    bool NeedsScope);      // true for Manager-held capabilities

public static class CapabilityMatrix
{
    // Every grant in the design table, and no others.
    public static IReadOnlyList<CapabilityGrant> Grants { get; }
}

// Setting scope displaces atomically: assigning MED to Jo while Sam holds
// it clears Sam and returns Sam's display name. Null scope clears.
public sealed record SetStaffScopeCommand(
    Guid StaffUserId,       // the Admin performing the assignment
    Guid TargetStaffUserId,
    Guid? AppointmentTypeId, // null clears the scope
    long ExpectedVersion);

public sealed record SetStaffScopeOutcome(
    Guid TargetStaffUserId,
    Guid? AppointmentTypeId,
    string? DisplacedManagerDisplayName); // null when nobody was displaced

// GetMe answers who the caller is even when they may do nothing.
public sealed record StaffMeView(
    string? DisplayName,
    string? StaffId,
    IReadOnlyList<string> Roles,
    Guid? ScopeAppointmentTypeId,
    IReadOnlyList<string> Capabilities,
    string? Problem); // set only for the no-role response
```

- [ ] **Step 1: Write the failing tests.** Create the four test files. Required cases, one
  test per rule:

  ```csharp
  // tests/EventBooking.Application.Tests/Access/RoleSyncTests.cs
  // (representative file — the other three follow the same shape)
  using EventBooking.Application.Access;
  using EventBooking.Application.Common;

  namespace EventBooking.Application.Tests.Access;

  public sealed class RoleSyncTests
  {
      // A token with no staff_id gets 403 everywhere except /api/me, which
      // explains the problem and writes no StaffIdentity.
      [Fact]
      public async Task Missing_staff_id_is_forbidden_and_writes_no_identity()
      {
          var fixture = AccessFixture.Create().WithTokenWithoutStaffId();

          var result = await fixture.AuthorizeAsync("ManageAttendees");
          var me = await fixture.GetMeAsync();

          Assert.Equal("forbidden", result.Error.Type);
          Assert.True(me.Value.Problem!.Contains("staff_id"));
          Assert.Empty(fixture.StoredIdentities);
      }

      // Role drift updates the profile and audits StaffRolesSynced as
      // System before authorizing the same request.
      [Fact]
      public async Task Role_drift_syncs_then_authorizes_same_request()
      {
          var fixture = AccessFixture.Create()
              .WithStoredRoles(["Coordinator"])
              .WithTokenRoles(["Coordinator", "Admin"]);

          var result = await fixture.AuthorizeAsync("ManageReferenceData");

          Assert.True(result.IsSuccess);
          Assert.Contains(
              fixture.Audit.Entries,
              e => e.Action == "StaffRolesSynced" && e.ActorType == "System");
      }

      // A sync removing the last Admin is refused and logged.
      [Fact]
      public async Task Removing_last_admin_is_refused_and_logged()
      {
          var fixture = AccessFixture.Create().WithOnlyAdmin("sam");

          var result = await fixture.SyncAsync(["Coordinator"]);

          Assert.Equal("refused", result.Error.Type);
          Assert.True(fixture.Logs.Contain("last Admin"));
          Assert.True(fixture.IsAdmin("sam"));
      }
  }
  ```

  The remaining suites cover: a `staff_id` not matching the configured pattern behaves like
  a missing one; custom claim names from configuration are honoured; a token with no roles
  removes the profile; a Manager role with a null scope gets no capability; the generated
  matrix test enumerates every capability-by-role cell against the design table; assigning
  MED to Jo while Sam holds it clears Sam atomically and names Sam; clearing the only MED
  Manager is allowed; `StaffIdentity` refreshes at most every 15 minutes.

- [ ] **Step 2: Run.** Expected: FAIL — the Access handlers do not exist.

  ```bash
  dotnet test tests/EventBooking.Application.Tests --filter "FullyQualifiedName~Access"
  ```

- [ ] **Step 3: Implement.** Create the handler and matrix files; rework Api.Auth to
  provider-neutral OIDC with configured claim names. Evaluate in design 06's order; sync
  before authorizing on every request.

- [ ] **Step 4: Run.** Expected: PASS — the new suites plus the full solution.

  ```bash
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  ```

  Expect Application and API counts to rise. A count that does not match after the change is
  a signal to read the diff, not to adjust the number.

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
  git add docs/detailed-implementations/phase-3f-staff-authorization.md docs/detailed-implementations/phase-3-application.md docs/detailed-implementations/HANDOVER.md
  git diff --cached --name-only
  git diff --cached
  test -n "$EXECUTOR_COAUTHOR"
  git commit -m "docs(plans): Task 17 staff identity and authorization

  Co-authored-by: $EXECUTOR_COAUTHOR"
  git push
  ```
