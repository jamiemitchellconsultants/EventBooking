# 03f — Provider-neutral OIDC with per-request role sync (Task 17)

[← Phase overview](phase-3-application.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This task follows Task 16. Authentication becomes provider-neutral OIDC against a configurable
authority, claim mapping and the staff-number pattern all come from configuration, and every
request syncs roles into the staff access profile before authorization — through one pipeline
shared by REST and MCP.

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
request, reusing the ported sync handler (cheap unlocked read first, LockAllAsync only on
drift): role drift updates the profile and audits `StaffRolesSynced` as `System`; a token
with no roles removes the profile; a sync removing the last Admin is refused and logged. A
Manager role with a null scope gets no capability at all (FR-10.7). `StaffIdentity` refreshes
at most every 15 minutes through the existing recorder middleware. The fixed-set guard in
profile validation is removed alongside Task 12's removals. No domain entity leaves the
handler.

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

- Modify: src/EventBooking.Api.Auth/LocalAuthenticationExtensions.cs (provider-neutral
  section, claim names from configuration, HTTPS metadata from configuration)
- Create: src/EventBooking.Api/Auth/AuthClaimOptions.cs (claim names plus staff-number
  pattern, bound from Auth__Claims__* and Identity__StaffIdPattern with the defaults the
  OIDC tests assert)
- Modify: src/EventBooking.Api/Auth/HttpContextCallerAccessor.cs (claim names and staff-number
  pattern from the new options)
- Modify: src/EventBooking.Application/Access/StaffAccessAuthorizer.cs (matrix as a single
  table; add ManageReferenceData for Admin)
- Modify: src/EventBooking.Domain/Access/StaffAccessProfile.cs (remove the EnsureKnown guard)
- Create: src/EventBooking.Application/Access/StaffScopeHandler.cs
- Create: src/EventBooking.Application/Access/MeHandler.cs
- Test: tests/EventBooking.Api.Tests/Auth/OidcAuthTests.cs
- Test: tests/EventBooking.Application.Tests/Access/AccessFixture.cs
- Test: tests/EventBooking.Application.Tests/Access/RoleSyncTests.cs
- Test: tests/EventBooking.Application.Tests/Access/AuthorizationMatrixTests.cs
- Test: tests/EventBooking.Application.Tests/Access/StaffScopeHandlerTests.cs

**Interfaces:**

```csharp
namespace EventBooking.Application.Access;

// The single table. Rows mirror design 06 exactly; the generated matrix test fails if code
// and design disagree. NeedsScope marks the Manager-held capabilities the null-scope rule
// strips (FR-10.7).
public sealed record CapabilityGrant(string Capability, string Role, bool NeedsScope);

public static class CapabilityMatrix
{
    public static IReadOnlyList<CapabilityGrant> Grants { get; } = [...];
}
```

The table rows, in design order: (`ManageReferenceData`, Admin), (`ManageSettings`, Admin),
(`ManageStaffAccess`, Admin), (`ManageAttendees`, Coordinator), (`ViewAttendeeDashboards`,
Coordinator), (`ViewAttendeeAudit`, Coordinator), (`ViewEventAudit`, Admin),
(`ViewEventAudit`, Coordinator), (`ManageEventNegotiation`, Manager, scoped),
(`ViewEventOperations`, Admin), (`ViewEventOperations`, Coordinator),
(`ViewEventOperations`, Manager), (`ViewEventOperations`, AppointmentStaff),
(`CancelEvent`, Admin), (`CancelEvent`, Coordinator), (`CancelEvent`, Manager, scoped),
(`ConductAppointments`, Manager, scoped), (`ConductAppointments`, AppointmentStaff, scoped).
The authorizer answers from this table plus the two standing rules (Admin sees no
attendee-data capabilities even if a refactor mislabels a row; a scoped role with null
scope grants nothing unless another role on the same profile grants it).

```csharp
// Setting scope displaces atomically: assigning MED to Jo while Sam holds it clears Sam
// and returns Sam's display name. Null scope clears. Clearing the only MED Manager is
// allowed. The version check guards the target profile; displacement itself needs no
// version because at most one holder exists by construction.
public sealed record SetStaffScopeCommand(
    Guid StaffUserId, Guid TargetStaffUserId, Guid? AppointmentTypeId, long ExpectedVersion);
public sealed record SetStaffScopeOutcome(
    Guid TargetStaffUserId, Guid? AppointmentTypeId, string? DisplacedManagerDisplayName);

public sealed record StaffMeView(
    string? DisplayName, string? StaffId, IReadOnlyList<string> Roles,
    Guid? ScopeAppointmentTypeId, IReadOnlyList<string> Capabilities, string? Problem);
```

- [ ] **Step 1: Write the failing tests.** Create the five test files below in full. The
  fixture wraps the existing fakes (profiles implementing the authorizer, identities,
  audit, unit of work) plus a token double carrying configurable claims.

  ```csharp
  // tests/EventBooking.Application.Tests/Access/AccessFixture.cs (complete)
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Access;
  using EventBooking.Application.Tests.Fakes;
  using EventBooking.Domain.Access;

  namespace EventBooking.Application.Tests.Access;

  public sealed class AccessToken
  {
      public Guid StaffUserId { get; set; } = Guid.NewGuid();
      public string? StaffIdValue { get; set; } = "A10023";
      public string? DisplayName { get; set; } = "Sam";
      public List<Role> Roles { get; set; } = [Role.Coordinator];
      public string StaffIdClaim { get; set; } = "staff_id";
      public string StaffIdPattern { get; set; } = StaffId.DefaultPattern;
  }

  public sealed class AccessFixture
  {
      public InMemoryStaffAccessProfileRepository Profiles = new();
      public InMemoryStaffIdentityRepository Identities = new();
      public FakeUnitOfWork UnitOfWork = new();
      public RecordingAuditLogger Audit = new();
      public FakeClock Clock = new();
      public List<string> Logs = [];

      public static AccessFixture Create() => new();

      public AccessFixture WithStoredRoles(params Role[] roles)
      {
          Profiles.Items.Add(StaffAccessProfile.Create(Guid.NewGuid(), roles.ToList(), null));
          return this;
      }

      // Authorize through the real authorizer against the configured claim names,
      // syncing roles first exactly as the pipeline does.
      public async Task<Common.Result<StaffAccessContext>> AuthorizeAsync(
          AccessToken token, StaffCapability capability)
      {
          var syncer = new RoleSyncShim(this);
          await syncer.SyncAsync(token);
          var parsed = ParseStaffId(token);
          if (parsed is null)
              return Common.Result<StaffAccessContext>.Failure(
                  Common.Error.Forbidden("No usable staff_id."));
          var authorizer = new StaffAccessAuthorizer(Profiles);
          return await authorizer.AuthorizeAsync(
              token.StaffUserId, capability, null, CancellationToken.None);
      }

      public static StaffId? ParseStaffId(AccessToken token) =>
          token.StaffIdValue is null ? null
          : StaffId.TryParse(token.StaffIdValue, out var parsed, token.StaffIdPattern) ? parsed : null;
  }
  ```

  RoleSyncShim is a private sealed class in the same file driving the real ported
  SyncStaffAccessProfileRolesHandler with the token's roles and a null-logger, returning
  its profile. `StaffId.TryParse(value, out parsed, pattern)` and
  `StaffId.DefaultPattern` follow the Task 3 value object (verify the member names against
  `StaffId.cs` before accepting; adjust the call, not the rule).

  ```csharp
  // tests/EventBooking.Application.Tests/Access/RoleSyncTests.cs (complete)
  using EventBooking.Application.Access;
  using EventBooking.Domain.Access;

  namespace EventBooking.Application.Tests.Access;

  public sealed class RoleSyncTests
  {
      [Fact]
      public async Task Missing_staff_id_is_forbidden_and_writes_no_identity()
      {
          var fixture = AccessFixture.Create();
          var token = new AccessToken { StaffIdValue = null };

          var result = await fixture.AuthorizeAsync(token, StaffCapability.ManageAttendees);
          var me = await new MeHandler(
                  fixture.Profiles, fixture.Identities, fixture.UnitOfWork)
              .HandleAsync(token.StaffUserId, token.StaffIdValue, token.DisplayName,
                  token.Roles.ToHashSet(), token.StaffIdPattern, CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("forbidden", result.Error.Code);
          Assert.Contains("staff_id", me.Value.Problem);
          Assert.Empty(fixture.Identities.Items);
      }

      [Fact]
      public async Task Malformed_staff_id_behaves_like_missing()
      {
          var fixture = AccessFixture.Create();
          var token = new AccessToken { StaffIdValue = "not valid!!" };

          var result = await fixture.AuthorizeAsync(token, StaffCapability.ManageAttendees);

          Assert.True(result.IsFailure);
          Assert.Equal("forbidden", result.Error.Code);
          Assert.Empty(fixture.Identities.Items);
      }

      [Fact]
      public async Task Role_drift_syncs_then_authorizes_same_request()
      {
          var fixture = AccessFixture.Create();
          var user = Guid.NewGuid();
          fixture.Profiles.Items.Add(StaffAccessProfile.Create(user, Role.Coordinator, null));
          var token = new AccessToken
          {
              StaffUserId = user,
              Roles = [Role.Coordinator, Role.Admin],
          };

          var result = await fixture.AuthorizeAsync(token, StaffCapability.ManageReferenceData);

          Assert.True(result.IsSuccess);
          Assert.Contains(fixture.Audit.Entries,
              e => e.Action == Domain.Audit.AuditAction.StaffRolesSynced
                  && e.ActorType == Domain.Audit.ActorType.System);
      }

      [Fact]
      public async Task Removing_last_admin_is_refused_and_logged()
      {
          var fixture = AccessFixture.Create();
          var sam = Guid.NewGuid();
          fixture.Profiles.Items.Add(StaffAccessProfile.Create(sam, Role.Admin, null));
          var token = new AccessToken { StaffUserId = sam, Roles = [Role.Coordinator] };

          var result = await fixture.AuthorizeAsync(token, StaffCapability.ManageAttendees);

          Assert.True(result.IsFailure);
          Assert.True(fixture.Logs.Exists(l => l.Contains("last Admin")));
          Assert.True(fixture.Profiles.Items.Single(p => p.StaffUserId == sam).IsAdmin);
      }

      [Fact]
      public async Task Token_with_no_roles_removes_the_profile()
      {
          var fixture = AccessFixture.Create();
          var user = Guid.NewGuid();
          fixture.Profiles.Items.Add(StaffAccessProfile.Create(user, Role.Coordinator, null));
          var token = new AccessToken { StaffUserId = user, Roles = [] };

          var result = await fixture.AuthorizeAsync(token, StaffCapability.ManageAttendees);

          Assert.True(result.IsFailure);
          Assert.Empty(fixture.Profiles.Items);
      }

      [Fact]
      public async Task Null_scoped_manager_gets_no_capability()
      {
          var fixture = AccessFixture.Create();
          var user = Guid.NewGuid();
          fixture.Profiles.Items.Add(StaffAccessProfile.Create(user, Role.Manager, null));
          var token = new AccessToken { StaffUserId = user, Roles = [Role.Manager] };

          var result = await fixture.AuthorizeAsync(token, StaffCapability.ManageEventNegotiation);

          Assert.True(result.IsFailure);
          Assert.Equal("forbidden", result.Error.Code);
      }
  }
  ```

  `MeHandler.HandleAsync` takes the parsed token model (not an HttpContext): the Api
  endpoint adapts the caller accessor to it. The no-role response carries the problem
  explanation and writes no identity. RoleSyncShim appends the last-Admin refusal to
  `fixture.Logs`, mirroring the handler's logger call.

  ```csharp
  // tests/EventBooking.Application.Tests/Access/AuthorizationMatrixTests.cs (complete):
  // one Theory over every row of CapabilityMatrix.Grants asserting the authorizer
  // allows it, plus the negative cells the design implies.
  using EventBooking.Application.Access;
  using EventBooking.Domain.Access;

  namespace EventBooking.Application.Tests.Access;

  public sealed class AuthorizationMatrixTests
  {
      private static readonly Guid TypeId = Guid.Parse("b0000001-0000-0000-0000-000000000001");

      public static TheoryData<string, Role[]> Grants => new(
          CapabilityMatrix.Grants.Select(g =>
              new object[] { g.Capability, new[] { Enum.Parse<Role>(g.Role) } }));

      [Theory]
      [MemberData(nameof(Grants))]
      public async Task Every_table_row_authorizes(string capabilityName, Role[] roles)
      {
          var fixture = AccessFixture.Create();
          var user = Guid.NewGuid();
          var scope = roles.Any(NeedsScope) ? TypeId : (Guid?)null;
          fixture.Profiles.Items.Add(StaffAccessProfile.Create(user, roles.ToList(), scope));
          var token = new AccessToken { StaffUserId = user, Roles = roles.ToList() };
          var capability = Enum.Parse<StaffCapability>(capabilityName);

          var result = await fixture.AuthorizeAsync(token, capability);

          Assert.True(result.IsSuccess);
      }

      public static TheoryData<string, Role[]> Denials => new(
          CapabilityMatrix.AllCapabilities
              .SelectMany(capability => AllRoles()
                  .Where(role => !CapabilityMatrix.Grants.Any(g =>
                      g.Capability == capability && g.Role == role.ToString()))
                  .Select(role => new object[] { capability, new[] { role } })));

      [Theory]
      [MemberData(nameof(Denials))]
      public async Task Cells_outside_the_table_are_forbidden(string capabilityName, Role[] roles)
      {
          var fixture = AccessFixture.Create();
          var user = Guid.NewGuid();
          var scope = roles.Any(NeedsScope) ? TypeId : (Guid?)null;
          fixture.Profiles.Items.Add(StaffAccessProfile.Create(user, roles.ToList(), scope));
          var token = new AccessToken { StaffUserId = user, Roles = roles.ToList() };
          var capability = Enum.Parse<StaffCapability>(capabilityName);

          var result = await fixture.AuthorizeAsync(token, capability);

          Assert.True(result.IsFailure);
          Assert.Equal("forbidden", result.Error.Code);
      }

      private static bool NeedsScope(Role role) =>
          role is Role.Manager or Role.AppointmentStaff;

      private static IEnumerable<Role> AllRoles() =>
          Enum.GetValues<Role>().Where(r => r is Role.Admin or Role.Coordinator or Role.Manager or Role.AppointmentStaff);
  }
  ```

  `CapabilityMatrix.AllCapabilities` lists every `StaffCapability` name; the denial theory
  fails on any enum member the table does not cover. Manager and AppointmentStaff profiles
  are created with a scope so the denial cells test the table, not the null-scope rule
  (covered separately).

  ```csharp
  // tests/EventBooking.Application.Tests/Access/StaffScopeHandlerTests.cs (complete)
  using EventBooking.Application.Access;
  using EventBooking.Application.Common;
  using EventBooking.Application.Tests.Fakes;
  using EventBooking.Domain.Access;

  namespace EventBooking.Application.Tests.Access;

  public sealed class StaffScopeHandlerTests
  {
      private static readonly Guid MedType = Guid.Parse("b0000001-0000-0000-0000-000000000001");
      private static readonly Guid Admin = Guid.Parse("a0000001-0000-0000-0000-000000000001");

      private static (InMemoryStaffAccessProfileRepository Profiles,
          InMemoryStaffIdentityRepository Identities, FakeUnitOfWork UnitOfWork,
          RecordingAuditLogger Audit) Parts() => (new(), new(), new(), new());

      [Fact]
      public async Task Assigning_med_to_jo_clears_sam_and_names_him()
      {
          var (profiles, identities, unitOfWork, audit) = Parts();
          var sam = Guid.NewGuid();
          var jo = Guid.NewGuid();
          profiles.Items.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));
          profiles.Items.Add(StaffAccessProfile.Create(sam, Role.Manager, MedType));
          profiles.Items.Add(StaffAccessProfile.Create(jo, Role.Manager, null));
          await identities.UpsertAsync(sam, new StaffId("S100"), "Sam", DateTimeOffset.UtcNow,
              CancellationToken.None);
          var handler = new StaffScopeHandler(profiles, identities, unitOfWork, audit);

          var result = await handler.HandleAsync(
              new SetStaffScopeCommand(Admin, jo, MedType, 1), CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal("Sam", result.Value.DisplacedManagerDisplayName);
          Assert.DoesNotContain(profiles.Items, p => p.StaffUserId == sam);
          Assert.Equal(MedType, profiles.Items.Single(p => p.StaffUserId == jo).AppointmentTypeId);
          Assert.Contains(audit.Entries, e =>
              e.Action == Domain.Audit.AuditAction.StaffAccessChanged);
      }

      [Fact]
      public async Task Clearing_the_only_med_manager_is_allowed()
      {
          var (profiles, identities, unitOfWork, audit) = Parts();
          var sam = Guid.NewGuid();
          profiles.Items.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));
          profiles.Items.Add(StaffAccessProfile.Create(sam, Role.Manager, MedType));
          var handler = new StaffScopeHandler(profiles, identities, unitOfWork, audit);

          var result = await handler.HandleAsync(
              new SetStaffScopeCommand(Admin, sam, null, 1), CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Null(result.Value.DisplacedManagerDisplayName);
          Assert.Null(profiles.Items.Single(p => p.StaffUserId == sam).AppointmentTypeId);
      }

      [Fact]
      public async Task Stale_version_returns_current_state()
      {
          var (profiles, identities, unitOfWork, audit) = Parts();
          var jo = Guid.NewGuid();
          profiles.Items.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));
          profiles.Items.Add(StaffAccessProfile.Create(jo, Role.Manager, null));
          var handler = new StaffScopeHandler(profiles, identities, unitOfWork, audit);

          var result = await handler.HandleAsync(
              new SetStaffScopeCommand(Admin, jo, MedType, 99), CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("version-conflict", result.Error.Code);
          Assert.Equal(1L, result.Error.Data!["currentVersion"]);
      }
  }
  ```

  StaffScopeHandler (new file, complete): authorize the acting Admin (`ManageStaffAccess`,
  Admin-only per the matrix); load the target profile (NotFound when absent); version check
  against the target; LockAllAsync and find any *other* profile holding the requested type
  — clear it through RemoveManagerRole, dropping the profile row when it returns true
  (no roles remain); set the target scope through Replace (null clears); audit
  `StaffAccessChanged` as the acting Admin with the displaced user id in details; save;
  commit; return the displaced display name looked up from identities (falling back to the
  staff number, then null). Assigning a scope to a profile with no Manager role is refused
  as validation.

  MeHandler (new file, complete): `HandleAsync(Guid staffUserId, string? staffIdValue,
  string? displayName, IReadOnlySet<Role> roles, string staffIdPattern, CancellationToken)`
  plus profiles, identities and unit of work on the constructor. No usable staff number →
  the no-role view (Problem explains the missing `staff_id`, no identity row written, no
  profile lookup). Otherwise load the profile; no profile → no-role view naming the held
  roles; profile → full view with capabilities resolved through the authorizer's table for
  the profile's roles and scope. The Api endpoint adapts the caller accessor to these
  parameters.

  ```csharp
  // tests/EventBooking.Api.Tests/Auth/OidcAuthTests.cs (complete, WebApplicationFactory)
  using System.Net;
  using EventBooking.Domain.Access;

  namespace EventBooking.Api.Tests.Auth;

  [Collection("api")]
  public class OidcAuthTests(ApiFactory factory)
  {
      [Fact]
      public async Task Missing_staff_id_is_forbidden_except_me()
      {
          factory.SignedInAs = Guid.NewGuid();
          factory.StaffIdClaim = null;
          factory.RolesClaim = ["Coordinator"];
          var client = factory.CreateClient();

          var attendees = await client.GetAsync("/api/attendees");
          Assert.Equal(HttpStatusCode.Forbidden, attendees.StatusCode);

          var me = await client.GetAsync("/api/me");
          Assert.Equal(HttpStatusCode.OK, me.StatusCode);
          Assert.Contains("staff_id", await me.Content.ReadAsStringAsync());
      }

      [Fact]
      public async Task Malformed_staff_id_behaves_like_missing()
      {
          factory.SignedInAs = Guid.NewGuid();
          factory.StaffIdClaim = "not valid!!";
          factory.RolesClaim = ["Coordinator"];
          var client = factory.CreateClient();

          var attendees = await client.GetAsync("/api/attendees");
          Assert.Equal(HttpStatusCode.Forbidden, attendees.StatusCode);
      }

      [Fact]
      public async Task Claim_names_and_pattern_come_from_configuration()
      {
          var options = new AuthClaimOptions();
          Assert.Equal("staff_id", options.StaffIdClaim);
          Assert.Equal("name", options.NameClaim);
          Assert.Equal("roles", options.RolesClaim);
          Assert.Equal(StaffId.DefaultPattern, options.StaffIdPattern);

          var configured = new AuthClaimOptions
          {
              StaffIdClaim = "employee_no",
              NameClaim = "display_name",
              RolesClaim = "app_roles",
              StaffIdPattern = "^[UN][0-9]{6}$",
          };
          Assert.Equal("employee_no", configured.StaffIdClaim);
          Assert.True(StaffId.TryParse("U123456", out _, configured.StaffIdPattern));
          Assert.False(StaffId.TryParse("A10023", out _, configured.StaffIdPattern));
      }
  }
  ```

  AuthClaimOptions binds `Auth__Claims__StaffId/Name/Roles` plus `Identity__StaffIdPattern`
  with the defaults above; HttpContextCallerAccessor reads it instead of its constants.
  The factory's test scheme attaches the literal claims the configured names default to,
  so the HTTP tests exercise the default path end to end.

- [ ] **Step 2: Run.** Expected: FAIL to compile — the Access handlers do not exist.

  ```bash
  dotnet test tests/EventBooking.Application.Tests --filter "FullyQualifiedName~Access"
  ```

- [ ] **Step 3: Implement.** Create `StaffScopeHandler.cs`, `MeHandler.cs` and
  `AuthClaimOptions.cs` as specified above; restructure the authorizer around the
  capability table (behaviour identical, plus the `ManageReferenceData` row, plus an
  AllCapabilities member listing every `StaffCapability` name for the generated test);
  remove the EnsureKnown guard from profile validation; rework Api.Auth to provider-neutral
  OIDC (authority, audience and HTTPS metadata from configuration); keep MapInboundClaims
  disabled. The scope handler locks all profiles (LockAllAsync), displaces the current
  holder of the type if any, sets the target scope through Replace, audits
  `StaffAccessChanged` as the acting Admin, and returns the displaced display name.

- [ ] **Step 4: Run.** Expected: PASS — the new suites plus the full solution.

  ```bash
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  ```

  Expect Application and API counts to rise. A count that does not match the executor's
  own before/after diff is a signal to read the diff, not to adjust the number.

- [ ] **Step 5: Commit and push** the executor's code — not the plan documents — under the
  master plan's message:

  ```bash
  test -z "$(git status --porcelain --ignored=no | grep -v '^??')"
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  git add src/EventBooking.Api.Auth/ src/EventBooking.Api/Auth/ src/EventBooking.Application/Access/ src/EventBooking.Domain/Access/StaffAccessProfile.cs tests/EventBooking.Api.Tests/Auth/OidcAuthTests.cs tests/EventBooking.Application.Tests/Access/
  git diff --cached --name-only
  git diff --cached
  test -n "$EXECUTOR_COAUTHOR"
  git commit -m "feat(auth): provider-neutral OIDC with per-request role sync

  Co-authored-by: $EXECUTOR_COAUTHOR"
  git push
  ```