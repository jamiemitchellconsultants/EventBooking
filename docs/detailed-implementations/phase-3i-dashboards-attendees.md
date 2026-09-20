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
(`cursor`/`limit`, limit 1–200 default 50, `{ items, nextCursor }`). The cursor payload is
the sort key plus id, base64url-encoded; Task 21 wraps it with an HMAC, so Task 20 treats
cursors as opaque strings it produces and parses but never trusts for authorization. Every
attendee read model takes the caller shape and refuses an Admin-shaped caller itself — even
when the capability check is bypassed, the model refuses. Attendee rows never carry
identifiers of other aggregates. No domain entity leaves the handler.

**Tech Stack:** .NET 10, xUnit, EF Core, PostgreSQL Testcontainers.

**Spec:** [Master Task 20](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[functional requirements](../design/02-functional-requirements.md),
[solution architecture](../design/04-solution-architecture.md), [ontology](../ontology.md).

## Global constraints

Exactly one StaffCapability per handler; attendee reads demand an attendee-facing capability
an Admin does not hold. Keyset pagination returns non-overlapping pages under concurrent
inserts (never offset). The attendee list with 50,000 rows returns a page in under 300 ms
(on-demand performance test, same tagging as Task 11's budget test: `Trait("Category",
"Performance")`, still run in the full suite).

## Review focus

STOP AND CHECK three things. The Events-tab bound (7 days ago to 60 days ahead) is proved by
events just outside each edge disappearing. The Admin refusal lives in the read model, not
just the handler — the test bypasses the capability check and still gets refused. And the
50,000-row performance test seeds rows the filters mostly exclude, so the index matters
(Task 11's lesson: a table where every row qualifies proves nothing).

### Task 20a: Dashboards, attendee list and readiness

**Files:**

- Create: src/EventBooking.Application/Dashboards/DashboardHandlers.cs (new file; the
  ported file below is deleted first, so there is exactly one DashboardHandlers.cs only if
  the ported name differs — the ported file is GetDashboardsHandler.cs, deleted here)
- Delete: src/EventBooking.Application/Dashboards/GetDashboardsHandler.cs
- Delete: src/EventBooking.Application/Dashboards/GetEventOperationsHandler.cs (operations
  board superseded by the Task 16 workspace)
- Create: src/EventBooking.Application/Attendees/AttendeeListHandlers.cs
- Create: src/EventBooking.Application/Abstractions/IAttendeeListQueries.cs (list port)
- Modify: src/EventBooking.Application/Abstractions/IDashboardQueries.cs
  (GetDashboardsAsync on the caller shape)
- Delete: src/EventBooking.Application/Attendees/ListAttendeesHandler.cs (ported version;
  replaced by AttendeeListHandlers.cs — delete, do not edit)
- Create: src/EventBooking.Application/ReadModels/CallerShape.cs
- Create: src/EventBooking.Application/ReadModels/AttendeeCursor.cs (payload contract)
- Create: src/EventBooking.Infrastructure/Persistence/Queries/DashboardQueries.cs
- Create: src/EventBooking.Infrastructure/Persistence/Queries/AttendeeListQueries.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/<generated-timestamp>_AttendeeListIndex.cs
  (plus its Designer; the timestamp prefix comes from generation)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/EventBookingDbContextModelSnapshot.cs
- Test: tests/EventBooking.Application.Tests/Dashboards/DashboardHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Attendees/AttendeeListHandlerTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/Queries/DashboardQueryTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/Queries/AttendeeListQueryTests.cs

**Interfaces:**

```csharp
namespace EventBooking.Application.ReadModels;

// What every Task 20 read model needs to refuse the wrong caller by itself. Resolved once
// per request from the access profile; Admin-shaped means IsAdmin regardless of scope.
public sealed record CallerShape(Guid StaffUserId, bool IsAdmin, IReadOnlySet<string> Roles);

// Cursor payload contract (Task 21 adds the HMAC wrapper). Opaque to callers: the sort
// key plus the row id, base64url-encoded, joined by a dot.
public static class AttendeeCursor
{
    public static string Encode(string sortKey, Guid id);
    public static bool TryDecode(string? cursor, out string sortKey, out Guid id);
}
```

```csharp
namespace EventBooking.Application.Dashboards;

public sealed record GetDashboardsQuery(Guid StaffUserId, Guid? LocationId);
public sealed record DashboardTabCounts(
    int AwaitingAvailability, int Invited, int Booked, int NeedsFollowUp);
public sealed record DashboardsView(
    DashboardTabCounts Events, DashboardTabCounts Attendees, DashboardTabCounts Recovery,
    int FailedEmails, int PendingEmails);
```

```csharp
namespace EventBooking.Application.Attendees;

public sealed record ListAttendeesQuery(
    Guid StaffUserId, string? Cursor, int Limit, string? Status,
    Guid? AttendeeGroupId, string? Readiness, string? NameOrEmailPrefix);
public sealed record AttendeeListItem(
    string Name, string Email, string Status, string GroupCode, string Readiness,
    IReadOnlyList<string> RequiredTypeCodes, string? LatestDeliveryStatus, string Cursor);
public sealed record AttendeeListView(IReadOnlyList<AttendeeListItem> Items, string? NextCursor);
```

- [ ] **Step 1: Write the failing tests.** Create the four test files below in full. The
  Application suites drive the handlers with an in-memory query double honouring the same
  bounds; the Infrastructure suites assert the SQL against real PostgreSQL.

  ```csharp
  // tests/EventBooking.Application.Tests/Dashboards/DashboardHandlerTests.cs (complete)
  using EventBooking.Application.Common;
  using EventBooking.Application.Dashboards;
  using EventBooking.Application.ReadModels;
  using EventBooking.Application.Tests.Fakes;

  namespace EventBooking.Application.Tests.Dashboards;

  public sealed class DashboardHandlerTests
  {
      [Fact]
      public async Task Events_tab_is_bounded_and_location_filtered()
      {
          var queries = new MemoryDashboardQueries()
              .WithEventEnding(daysFromNow: -8)
              .WithEventEnding(daysFromNow: 61)
              .WithEventEnding(daysFromNow: 30, location: "TOKYO");
          var handler = new GetDashboardsHandler(
              queries, new InMemoryStaffAccessProfileRepository(), new FakeClock(),
              DashboardTestZones.Instance);

          var result = await handler.HandleAsync(
              new GetDashboardsQuery(Guid.NewGuid(), queries.LondonLocationId),
              CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal(0, result.Value.Events.AwaitingAvailability);
      }

      [Fact]
      public async Task Admin_is_refused_by_the_read_model_itself()
      {
          var queries = new OpenDashboardQueries();
          var handler = new GetDashboardsHandler(
              queries, new InMemoryStaffAccessProfileRepository(), new FakeClock(),
              DashboardTestZones.Instance);

          var result = await handler.HandleAsync(
              new GetDashboardsQuery(Guid.NewGuid(), null), CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("forbidden", result.Error.Code);
      }

      [Fact]
      public async Task Email_counts_travel_with_the_tabs()
      {
          var queries = new MemoryDashboardQueries().WithEmails(failed: 2, pending: 5);
          var handler = new GetDashboardsHandler(
              queries, new InMemoryStaffAccessProfileRepository(), new FakeClock(),
              DashboardTestZones.Instance);

          var result = await handler.HandleAsync(
              new GetDashboardsQuery(Guid.NewGuid(), null), CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal(2, result.Value.FailedEmails);
          Assert.Equal(5, result.Value.PendingEmails);
      }
  }
  ```

  MemoryDashboardQueries is a private sealed class in the same file implementing the
  dashboard query port from settable rows (events with end offsets and locations, email
  counts) and exposing LondonLocationId. DashboardTestZones is a private stub in the
  same file with IsKnownZone true for the test zones and InstantOf treating local
  time as UTC (ordering only — the bound tests use offsets, not zones).
  OpenDashboardQueries is a second double in the same file whose caller shape is
  Admin-shaped regardless of input. Write both doubles in full in the file. The committed
  handler constructor is `(queries, profiles, clock, zones)`.

  ```csharp
  // tests/EventBooking.Application.Tests/Attendees/AttendeeListHandlerTests.cs (complete)
  using EventBooking.Application.Attendees;
  using EventBooking.Application.Common;
  using EventBooking.Application.ReadModels;
  using EventBooking.Application.Tests.Fakes;

  namespace EventBooking.Application.Tests.Attendees;

  public sealed class AttendeeListHandlerTests
  {
      [Fact]
      public async Task Filters_combine_and_required_codes_show_on_awaiting_rows()
      {
          var queries = new MemoryAttendeeQueries()
              .WithAttendee("Amy", "amy@example.invalid", "AwaitingAvailability", "NHS", ["MED", "IND"])
              .WithAttendee("Bo", "bo@example.invalid", "Invited", "NHS", ["MED"]);
          var handler = new ListAttendeesHandler(
              queries, new InMemoryStaffAccessProfileRepository());

          var result = await handler.HandleAsync(new ListAttendeesQuery(
              Guid.NewGuid(), null, 50, "AwaitingAvailability", null, null, "a"),
              CancellationToken.None);

          Assert.True(result.IsSuccess);
          var item = Assert.Single(result.Value.Items);
          Assert.Equal("Amy", item.Name);
          Assert.Equal(["IND", "MED"], item.RequiredTypeCodes.Order().ToList());
          Assert.NotNull(item.Cursor);
      }

      [Fact]
      public async Task Pages_do_not_overlap_under_concurrent_inserts()
      {
          var queries = new MemoryAttendeeQueries().WithAttendees(30);
          var handler = new ListAttendeesHandler(
              queries, new InMemoryStaffAccessProfileRepository());

          var first = await handler.HandleAsync(
              new ListAttendeesQuery(Guid.NewGuid(), null, 10, null, null, null, null),
              CancellationToken.None);
          queries.WithAttendees(10);
          var second = await handler.HandleAsync(
              new ListAttendeesQuery(Guid.NewGuid(), first.Value.NextCursor, 10, null, null, null, null),
              CancellationToken.None);

          Assert.True(second.IsSuccess);
          Assert.Empty(first.Value.Items.Select(i => i.Cursor)
              .Intersect(second.Value.Items.Select(i => i.Cursor)));
      }

      [Fact]
      public async Task Admin_caller_is_refused_by_the_model()
      {
          var queries = new AdminShapedAttendeeQueries();
          var handler = new ListAttendeesHandler(
              queries, new InMemoryStaffAccessProfileRepository());

          var result = await handler.HandleAsync(
              new ListAttendeesQuery(Guid.NewGuid(), null, 50, null, null, null, null),
              CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("forbidden", result.Error.Code);
      }
  }
  ```

  MemoryAttendeeQueries is a private sealed class in the same file honouring every
  filter, keyset order (name, id) and cursor round-trip through the payload contract.
  AdminShapedAttendeeQueries always reports an Admin caller. The readiness per row comes
  from the ported readiness calculator over the attendee's status and delivery state; the
  latest delivery status is null when never invited.

  ```csharp
  // tests/EventBooking.Infrastructure.Tests/Queries/DashboardQueryTests.cs and
  // tests/EventBooking.Infrastructure.Tests/Queries/AttendeeListQueryTests.cs (complete,
  // real PostgreSQL 16): seed events just inside and outside each bound edge (7 days ago
  // / 60 days ahead, end instants), across two locations; assert the tab counts, the
  // location filter, combined attendee filters, cursor round-trip with concurrent
  // inserts, and the 50,000-row page under 300 ms (Trait Category Performance, seeded
  // mostly outside the filters so the index decides). Follow the Task 9b fixture pattern.
  ```

- [ ] **Step 2: Run.** Expected: FAIL to compile — the dashboard and attendee-list handlers
  do not exist.

  ```bash
  dotnet test tests/EventBooking.Application.Tests --filter "FullyQualifiedName~Dashboards|FullyQualifiedName~Attendees"
  ```

- [ ] **Step 3: Implement.** Add the production code below in full. No placeholders:
  every file below is complete.

  ```csharp
  // src/EventBooking.Application/ReadModels/CallerShape.cs (complete)
  namespace EventBooking.Application.ReadModels;

  public sealed record CallerShape(Guid StaffUserId, bool IsAdmin, IReadOnlySet<string> Roles);
  ```

  ```csharp
  // src/EventBooking.Application/ReadModels/AttendeeCursor.cs (complete)
  using System.Text;

  namespace EventBooking.Application.ReadModels;

  // Cursor payload contract (Task 21 adds the HMAC wrapper). Opaque to callers: the sort
  // key plus the row id, base64url-encoded, joined by a dot.
  public static class AttendeeCursor
  {
      public static string Encode(string sortKey, Guid id) =>
          Convert.ToBase64String(Encoding.UTF8.GetBytes($"{sortKey}.{id:N}"))
              .TrimEnd('=').Replace('+', '-').Replace('/', '_');

      public static bool TryDecode(string? cursor, out string sortKey, out Guid id)
      {
          sortKey = string.Empty;
          id = Guid.Empty;
          if (string.IsNullOrEmpty(cursor)) return false;
          try
          {
              var padded = cursor.Replace('-', '+').Replace('_', '/');
              padded += new string('=', (4 - padded.Length % 4) % 4);
              var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
              var dot = decoded.LastIndexOf('.');
              if (dot < 0 || !Guid.TryParseExact(decoded[(dot + 1)..], "N", out id)) return false;
              sortKey = decoded[..dot];
              return true;
          }
          catch (FormatException)
          {
              return false;
          }
      }
  }
  ```

  ```csharp
  // src/EventBooking.Application/Dashboards/DashboardHandlers.cs (complete)
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Access;
  using EventBooking.Application.Common;
  using EventBooking.Application.ReadModels;
  using EventBooking.Domain.Access;
  using EventBooking.Domain.Time;

  namespace EventBooking.Application.Dashboards;

  public sealed class GetDashboardsHandler(
      IDashboardQueries queries,
      IStaffAccessProfileRepository profiles,
      IClock clock,
      IEventWindowZones zones)
  {
      // Coordinator capability: dashboards are attendee-facing reads an Admin does not hold.
      // The query re-checks the Admin shape itself.
      public async Task<Result<DashboardsView>> HandleAsync(
          GetDashboardsQuery query, CancellationToken ct)
      {
          var profile = await profiles.GetAsync(query.StaffUserId, ct);
          if (profile is null || !profile.IsCoordinator)
              return Result<DashboardsView>.Failure(
                  Error.Forbidden("Dashboards need a Coordinator profile."));
          var shape = new CallerShape(
              profile.StaffUserId, profile.IsAdmin, RolesOf(profile));
          return Result<DashboardsView>.Success(await queries.GetDashboardsAsync(
              shape, query.LocationId, clock.UtcNow, clock, zones, ct));
      }

      private static HashSet<string> RolesOf(StaffAccessProfile profile)
      {
          var roles = new HashSet<string>();
          if (profile.IsManager) roles.Add("Manager");
          if (profile.IsCoordinator) roles.Add("Coordinator");
          if (profile.IsAdmin) roles.Add("Admin");
          if (profile.IsAppointmentStaff) roles.Add("AppointmentStaff");
          return roles;
      }
  }
  ```

  `IDashboardQueries.GetDashboardsAsync` is the port (add it beside the ported row methods
  the queries class already implements): it takes the caller shape and refuses
  Admin-shaped callers itself, pre-filters events by the widened `start_utc` window in SQL,
  applies the exact end-instant bound with the resolver, and returns the three tabs plus
  email counts. The 7-day/60-day bounds and the 720-minute widening live as constants on
  the query class.

  ```csharp
  // src/EventBooking.Application/Attendees/AttendeeListHandlers.cs (complete)
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Access;
  using EventBooking.Application.Common;
  using EventBooking.Application.ReadModels;
  using EventBooking.Domain.Access;

  namespace EventBooking.Application.Attendees;

  public sealed class ListAttendeesHandler(
      IAttendeeListQueries queries,
      IStaffAccessProfileRepository profiles)
  {
      public async Task<Result<AttendeeListView>> HandleAsync(
          ListAttendeesQuery query, CancellationToken ct)
      {
          if (query.Limit is < 1 or > 200)
              return Result<AttendeeListView>.Failure(
                  Error.Validation("Limit must be between 1 and 200."));
          if (query.Cursor is not null
              && !AttendeeCursor.TryDecode(query.Cursor, out _, out _))
              return Result<AttendeeListView>.Failure(Error.Validation("The cursor is invalid."));

          var profile = await profiles.GetAsync(query.StaffUserId, ct);
          if (profile is null || !profile.IsCoordinator)
              return Result<AttendeeListView>.Failure(
                  Error.Forbidden("The attendee list needs a Coordinator profile."));
          var shape = new CallerShape(
              profile.StaffUserId, profile.IsAdmin, new HashSet<string>());

          var page = await queries.ListAttendeesAsync(shape, query.Cursor, query.Limit,
              query.Status, query.AttendeeGroupId, query.Readiness, query.NameOrEmailPrefix, ct);
          return Result<AttendeeListView>.Success(page);
      }
  }
  ```

  The limit default (50) is applied by the endpoint (Task 22), which passes an explicit
  value here. `IAttendeeListQueries.ListAttendeesAsync` is the port: Admin-shaped callers
  refused before reading; filters combine; keyset order (name, id); required type codes
  projected only on awaiting-availability rows; latest delivery status from the newest
  outbox row or null; readiness from the ported calculator over status and delivery state.

  ```csharp
  // src/EventBooking.Infrastructure/Persistence/Queries/DashboardQueries.cs (complete):
  // implements IDashboardQueries over the DbContext. Events tab: start_utc in
  // [now - 7 days - 720 minutes, now + 60 days], exact end bound via EventStartInstants
  // plus duration-to-end arithmetic in .NET over the shortlist with locations loaded for
  // zones; counts by attendee pipeline status through the attendee set; email counts by
  // outbox status. Location filter applies to every tab. Caller shape refused up front
  // when Admin-shaped.
  // src/EventBooking.Infrastructure/Persistence/Queries/AttendeeListQueries.cs (complete):
  // implements IAttendeeListQueries. Single statement with optional predicates, keyset on
  // (lower(name), id) decoded through the payload contract (malformed cursor already
  // refused by the handler), limit + 1 fetch for the next cursor, index on
  // (lower(name), id) added by the Task 20a migration beside the query.
  ```

  The attendee-list index needs a migration: add it to the Task 20a change with
  `dotnet ef migrations add AttendeeListIndex`, verified against the configuration before
  accepting. The 50,000-row performance test proves the index decides.

- [ ] **Step 4: Run.** Expected: PASS — the new suites plus the full solution.

  ```bash
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  ```

  Expect Application and Infrastructure counts to rise. A count that does not match the
  executor's own before/after diff is a signal to read the diff, not to adjust the number.

- [ ] **Step 5: Commit and push** the executor's code — not the plan documents — under the
  master plan's message:

  ```bash
  test -z "$(git status --porcelain --ignored=no | grep -v '^??')"
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  git add src/EventBooking.Application/Dashboards/ src/EventBooking.Application/Attendees/AttendeeListHandlers.cs src/EventBooking.Application/Abstractions/IAttendeeListQueries.cs src/EventBooking.Application/Abstractions/IDashboardQueries.cs src/EventBooking.Application/ReadModels/ src/EventBooking.Infrastructure/Persistence/Queries/DashboardQueries.cs src/EventBooking.Infrastructure/Persistence/Queries/AttendeeListQueries.cs src/EventBooking.Infrastructure/Persistence/Migrations/ tests/EventBooking.Application.Tests/Dashboards/ tests/EventBooking.Application.Tests/Attendees/AttendeeListHandlerTests.cs tests/EventBooking.Infrastructure.Tests/Queries/DashboardQueryTests.cs tests/EventBooking.Infrastructure.Tests/Queries/AttendeeListQueryTests.cs
  git diff --cached --name-only
  git diff --cached
  test -n "$EXECUTOR_COAUTHOR"
  git commit -m "feat(app): bounded dashboards and attendee list

  Co-authored-by: $EXECUTOR_COAUTHOR"
  git push
  ```