# 03i — Bounded dashboards, attendee list and readiness (Task 20a)

[← Phase overview](phase-3-application.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This task follows Task 19 and is the first half of the Task 20 split (contradiction #5,
settled with the user: attendee CRUD, import and boundary work stay with Task 20, which is
too large for one document). This half covers the dashboards, the attendee list and
readiness; the audit search, histories and the phase pull-request gate are Task 20b.

> Use superpowers:executing-plans. This task is hand-authored: complete code and complete tests
> are written straight into this document, with no prototype. Compile and test-drive them
> yourself. The test counts below are what you should expect to reach, not figures observed by
> the author — nothing here has been run.

**Goal:** GetDashboards (location id optional) — one query returning the three tabs with
counts (FR-13.1) and failed/pending email counts; ListAttendees with cursor, status, group,
readiness and name/email prefix filters, readiness and latest delivery status;
GetReadiness. The Events tab is bounded to end instants between 7 days ago and 60 days ahead
and filters by location; awaiting-availability rows show the required type codes.

**Architecture:** Read models are parameterised SQL projections, keyset-paginated
(`cursor`/`limit`, limit 1–200 default 50, `{ items, nextCursor }`). Every attendee read
model refuses an Admin-shaped caller — even when the capability check is bypassed in the
test, the model itself refuses. Attendee reads never emit personal data beyond name and email
on the row itself, and never identifiers of other aggregates. No domain entity leaves the
handler.

**Tech Stack:** .NET 10, xUnit, EF Core, PostgreSQL Testcontainers.

**Spec:** [Master Task 20](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[functional requirements](../design/02-functional-requirements.md),
[solution architecture](../design/04-solution-architecture.md), [ontology](../ontology.md).

## Global constraints

Exactly one StaffCapability per handler; attendee reads demand an attendee-facing capability
an Admin does not hold. Audit pagination returns non-overlapping pages under concurrent
inserts (keyset, not offset). The attendee list with 50,000 rows returns a page in under 300
ms (on-demand performance test, same tagging as Task 11's budget test).

## Review focus

STOP AND CHECK three things. The Events-tab bound (7 days ago to 60 days ahead) is proved by
events just outside each edge disappearing. The Admin refusal lives in the read model, not
just the handler — the test bypasses the capability check and still gets refused. And the
50,000-row performance test seeds rows the filters mostly exclude, so the index matters
(Task 11's lesson: a table where every row qualifies proves nothing).

### Task 20a: Dashboards, attendee list and readiness

**Files:**

- Create: src/EventBooking.Application/Dashboards/DashboardHandlers.cs
- Create: src/EventBooking.Application/Attendees/AttendeeListHandlers.cs
- Create: src/EventBooking.Infrastructure/Persistence/Queries/DashboardQueries.cs
- Create: src/EventBooking.Infrastructure/Persistence/Queries/AttendeeListQueries.cs
- Delete: the ported dashboard, attendee-list and readiness files they replace
- Test: tests/EventBooking.Application.Tests/Dashboards/DashboardHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Attendees/AttendeeListHandlerTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/Queries/DashboardQueryTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/Queries/AttendeeListQueryTests.cs

**Interfaces:**

These complete types define the changed public boundary and its domain behavior. Apply them
after the failing test, not before.

```csharp
namespace EventBooking.Application.Dashboards;

// One query, three tabs with counts plus the failed/pending email counts.
// The Events tab bounds end instants to 7 days ago through 60 days ahead.
public sealed record GetDashboardsQuery(
    Guid StaffUserId,
    Guid? LocationId); // null means every location

public sealed record DashboardTabCounts(
    int AwaitingAvailability,
    int Invited,
    int Booked,
    int NeedsFollowUp);

public sealed record DashboardsView(
    DashboardTabCounts Events,      // event-side pipeline counts
    DashboardTabCounts Attendees,   // attendee-side pipeline counts
    DashboardTabCounts Recovery,    // recovery pipeline counts
    int FailedEmails,
    int PendingEmails);
```

```csharp
namespace EventBooking.Application.Attendees;

// Cursor pagination only. Filters combine; readiness and latest delivery
// status are projected per row. Refuses an Admin-shaped caller in the read
// model itself (FR-12.3 buckets apply in Task 20b; the refusal lives here).
public sealed record ListAttendeesQuery(
    Guid StaffUserId,
    string? Cursor,
    int Limit,                     // 1-200, default 50
    string? Status,                // null means all
    Guid? AttendeeGroupId,         // null means all
    string? Readiness,             // null means all
    string? NameOrEmailPrefix);    // null means all

public sealed record AttendeeListItem(
    string Name,
    string Email,
    string Status,
    string GroupCode,
    string Readiness,
    IReadOnlyList<string> RequiredTypeCodes, // awaiting-availability rows
    string? LatestDeliveryStatus,            // null when never invited
    string Cursor);                          // opaque sort key + HMAC

public sealed record AttendeeListView(
    IReadOnlyList<AttendeeListItem> Items,
    string? NextCursor);           // null on the last page
```

- [ ] **Step 1: Write the failing tests.** Create the four test files. Required cases, one
  test per rule:

  ```csharp
  // tests/EventBooking.Application.Tests/Dashboards/DashboardHandlerTests.cs
  // (representative file — the other three follow the same shape)
  using EventBooking.Application.Common;
  using EventBooking.Application.Dashboards;

  namespace EventBooking.Application.Tests.Dashboards;

  public sealed class DashboardHandlerTests
  {
      // The Events tab excludes events ending 8 days ago and 61 days
      // ahead, and filters by location.
      [Fact]
      public async Task Events_tab_is_bounded_and_location_filtered()
      {
          var fixture = DashboardFixture.Create()
              .WithEventEnding(daysFromNow: -8)
              .WithEventEnding(daysFromNow: 61)
              .WithEventEnding(daysFromNow: 30, location: "TOKYO");
          var query = new GetDashboardsQuery(
              fixture.CoordinatorUserId, fixture.LondonLocationId);

          var result = await fixture.GetAsync(query, CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal(0, result.Value.Events.AwaitingAvailability);
      }

      // An Admin calling GetDashboards is refused by the read model even
      // when the capability check is bypassed in the test.
      [Fact]
      public async Task Admin_is_refused_by_the_read_model_itself()
      {
          var fixture = DashboardFixture.Create().BypassCapabilityCheck();
          var query = new GetDashboardsQuery(
              fixture.AdminUserId, LocationId: null);

          var result = await fixture.GetAsync(query, CancellationToken.None);

          Assert.Equal("forbidden", result.Error.Type);
      }
  }
  ```

  The remaining suites cover: awaiting-availability rows showing the required type codes;
  attendee-list filters combining (status, group, readiness, prefix); audit-free pagination
  returning non-overlapping pages under concurrent inserts; the 50,000-row page under 300
  ms (tagged `Category=Performance`, runs in the full suite per Task 11's rule).

- [ ] **Step 2: Run.** Expected: FAIL — the dashboard and attendee-list handlers do not
  exist.

  ```bash
  dotnet test tests/EventBooking.Application.Tests --filter "FullyQualifiedName~Dashboards|FullyQualifiedName~Attendees"
  ```

- [ ] **Step 3: Implement.** Create the handler and query files; delete the ported files.
  Bound the Events tab in SQL, not in memory. Refuse Admin-shaped callers in the read
  model. Paginate by keyset.

- [ ] **Step 4: Run.** Expected: PASS — the new suites plus the full solution.

  ```bash
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  ```

  Expect Application and Infrastructure counts to rise. A count that does not match after
  the change is a signal to read the diff, not to adjust the number.

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
  git add docs/detailed-implementations/phase-3i-dashboards-attendees.md docs/detailed-implementations/phase-3-application.md docs/detailed-implementations/HANDOVER.md
  git diff --cached --name-only
  git diff --cached
  test -n "$EXECUTOR_COAUTHOR"
  git commit -m "docs(plans): Task 20a dashboards and attendee list

  Co-authored-by: $EXECUTOR_COAUTHOR"
  git push
  ```
