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

// FR-13.1's three tabs, each with its row count. The rows are the ported row types,
// which already carry what the requirement names: the waiting-since date and required
// codes on awaiting availability, the capacities and active bookings on an event. The
// count travels beside the rows so a tab badge does not depend on materialising them.
public sealed record AwaitingAvailabilityTab(
    int Count, IReadOnlyList<AwaitingAvailabilityRow> Rows);
public sealed record NoResponseTab(int Count, IReadOnlyList<NoResponseRow> Rows);
public sealed record EventsTab(int Count, IReadOnlyList<EventOverviewRow> Rows);

public sealed record DashboardsView(
    AwaitingAvailabilityTab AwaitingAvailability,
    NoResponseTab NoResponse,
    EventsTab Events,
    int FailedEmails,
    int PendingEmails);

// EventOverviewRow gains its location: FR-13.1 says each event row carries one, and the
// Events tab is filterable by it. The other two ported row types are unchanged.
public sealed record EventOverviewRow(
    Guid EventId, Guid LocationId, string LocationName, DateOnly Date,
    TimeOnly StartTime, TimeOnly EndTime,
    IReadOnlyList<EventCapacityRow> Capacities, int ActiveBookings);
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
          // Both out-of-bound events and the Tokyo one are gone, so the tab is empty and
          // its count agrees with its rows — the count is not computed separately.
          Assert.Empty(result.Value.Events.Rows);
          Assert.Equal(0, result.Value.Events.Count);
      }

      [Fact]
      public async Task An_event_inside_the_bounds_at_the_named_location_is_kept()
      {
          var queries = new MemoryDashboardQueries()
              .WithEventEnding(daysFromNow: 30);
          var handler = new GetDashboardsHandler(
              queries, new InMemoryStaffAccessProfileRepository(), new FakeClock(),
              DashboardTestZones.Instance);

          var result = await handler.HandleAsync(
              new GetDashboardsQuery(Guid.NewGuid(), queries.LondonLocationId),
              CancellationToken.None);

          var row = Assert.Single(result.Value.Events.Rows);
          Assert.Equal(queries.LondonLocationId, row.LocationId);
          Assert.Equal(1, result.Value.Events.Count);
      }

      [Fact]
      public async Task The_attendee_tabs_are_one_status_each_and_carry_their_counts()
      {
          var queries = new MemoryDashboardQueries()
              .WithAwaitingAvailability("Amy", waitingSince: -12)
              .WithAwaitingAvailability("Ben", waitingSince: -3)
              .WithNoResponse("Cat");
          var handler = new GetDashboardsHandler(
              queries, new InMemoryStaffAccessProfileRepository(), new FakeClock(),
              DashboardTestZones.Instance);

          var result = await handler.HandleAsync(
              new GetDashboardsQuery(Guid.NewGuid(), null), CancellationToken.None);

          Assert.Equal(2, result.Value.AwaitingAvailability.Count);
          Assert.Equal(2, result.Value.AwaitingAvailability.Rows.Count);
          // Longest wait first, and the days-waiting figure is derived from the stamp.
          Assert.Equal("Amy", result.Value.AwaitingAvailability.Rows[0].Name);
          Assert.Equal(12, result.Value.AwaitingAvailability.Rows[0].DaysWaiting);
          Assert.Equal(1, result.Value.NoResponse.Count);
          Assert.Equal("Cat", Assert.Single(result.Value.NoResponse.Rows).Name);
      }

      [Fact]
      public async Task A_location_filter_does_not_narrow_the_attendee_tabs()
      {
          // An attendee awaiting availability has no event and therefore no location.
          // Filtering them by one would need a relationship the model does not have.
          var queries = new MemoryDashboardQueries()
              .WithAwaitingAvailability("Amy", waitingSince: -1);
          var handler = new GetDashboardsHandler(
              queries, new InMemoryStaffAccessProfileRepository(), new FakeClock(),
              DashboardTestZones.Instance);

          var result = await handler.HandleAsync(
              new GetDashboardsQuery(Guid.NewGuid(), queries.LondonLocationId),
              CancellationToken.None);

          Assert.Equal(1, result.Value.AwaitingAvailability.Count);
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
              shape, query.LocationId, clock.UtcNow, zones, ct));
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
  applies the exact end-instant bound with the resolver, and returns FR-13.1's three tabs —
  Awaiting availability, No response and Events, each with its row count — plus FR-13.2's
  failed and pending email counts. The widening is `EventWindow.MaximumDurationMinutes`
  rather than a repeated 720, so a change to the longest allowed window cannot leave the
  pre-filter narrower than the rule it is widening for.

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
  // src/EventBooking.Application/Abstractions/IDashboardQueries.cs — add beside the ported
  // row methods, which stay for the screens that read one tab at a time.
  Task<DashboardsView> GetDashboardsAsync(
      CallerShape shape, Guid? locationId, DateTimeOffset now,
      IEventWindowZones zones, CancellationToken ct);
  ```

  ```csharp
  // src/EventBooking.Infrastructure/Persistence/Queries/DashboardQueries.cs (complete) —
  // the GetDashboardsAsync addition only; the ported row methods in this class are
  // untouched.
  /// <inheritdoc />
  public async Task<DashboardsView> GetDashboardsAsync(
      CallerShape shape, Guid? locationId, DateTimeOffset now,
      IEventWindowZones zones, CancellationToken ct)
  {
      ArgumentNullException.ThrowIfNull(shape);
      ArgumentNullException.ThrowIfNull(zones);

      // The read model refuses the Admin shape itself (FR-13: Coordinator, never Admin).
      // The handler checks for a Coordinator profile, but an Admin who also holds one must
      // still not read attendee rows, and that rule belongs where the rows are.
      if (shape.IsAdmin)
      {
          return new DashboardsView(
              new AwaitingAvailabilityTab(0, []), new NoResponseTab(0, []),
              new EventsTab(0, []), 0, 0);
      }

      var today = DateOnly.FromDateTime(now.UtcDateTime);

      // The two attendee tabs are one status each (FR-13.1). They carry no location:
      // an attendee awaiting availability has no event yet, so a location filter cannot
      // narrow them without inventing a relationship the model does not have. The filter
      // applies to the Events tab, which is the tab that has one.
      var awaiting = await AttendeeRowsAsync(
          AttendeeStatus.AwaitingAvailability, today, ct);
      var noResponse = await AttendeeRowsAsync(
          AttendeeStatus.NoResponseNeedsFollowUp, today, ct);

      // Events ending between 7 days ago and 60 days ahead. There is no stored end
      // instant — PostgreSQL cannot evaluate IANA rules deterministically (design 04) —
      // so SQL pre-filters start_utc to the widened window and the exact end bound is
      // applied here over the shortlist, with each event's location zone.
      var from = now.AddDays(-7);
      var to = now.AddDays(60);
      var widenedFrom = from.AddMinutes(-EventWindow.MaximumDurationMinutes);

      var shortlist = await context.Events
          .AsNoTracking()
          .Where(e => e.Status == EventStatus.Active)
          .Where(e => locationId == null || e.LocationId == locationId)
          .Where(e => EF.Property<DateTimeOffset>(e, EventStartInstants.PropertyName) >= widenedFrom
              && EF.Property<DateTimeOffset>(e, EventStartInstants.PropertyName) <= to)
          .Select(e => new
          {
              e.Id,
              e.LocationId,
              e.Window.Date,
              e.Window.StartTime,
              e.Window.DurationMinutes,
              Capacities = e.Capacities
                  .Select(c => new { c.AppointmentTypeId, c.TotalHeadcount, c.RemainingCapacity })
                  .ToList(),
              ActiveBookings = context.Bookings
                  .Count(b => b.EventId == e.Id && b.Status == BookingStatus.Active),
          })
          .ToListAsync(ct);

      var locations = await context.Locations
          .AsNoTracking()
          .Select(l => new { l.Id, l.Name, l.TimeZoneId })
          .ToDictionaryAsync(l => l.Id, ct);
      var typeCodes = await context.AppointmentTypes
          .AsNoTracking()
          .Select(t => new { t.Id, t.Code })
          .ToDictionaryAsync(t => t.Id, t => t.Code, ct);

      var events = new List<EventOverviewRow>();
      foreach (var row in shortlist)
      {
          if (!locations.TryGetValue(row.LocationId, out var location))
          {
              continue;
          }

          var endTime = row.StartTime.Add(TimeSpan.FromMinutes(row.DurationMinutes));
          var endInstant = zones.InstantOf(row.Date, endTime, location.TimeZoneId);
          if (endInstant < from || endInstant > to)
          {
              continue;
          }

          events.Add(new EventOverviewRow(
              row.Id,
              row.LocationId,
              location.Name,
              row.Date,
              row.StartTime,
              endTime,
              [.. row.Capacities
                  .Where(c => typeCodes.ContainsKey(c.AppointmentTypeId))
                  .Select(c => new EventCapacityRow(
                      typeCodes[c.AppointmentTypeId], c.TotalHeadcount, c.RemainingCapacity))
                  .OrderBy(c => c.Code)],
              row.ActiveBookings));
      }

      events = [.. events.OrderBy(e => e.Date).ThenBy(e => e.StartTime).ThenBy(e => e.EventId)];

      // FR-13.2: failed and pending counts come from the latest email per attendee, not
      // from every row ever written, or one attendee's long retry history would dominate.
      var latest = await context.EmailLogs
          .AsNoTracking()
          .GroupBy(e => e.AttendeeId)
          .Select(g => g
              .OrderByDescending(e => e.SentAt)
              .ThenByDescending(e => e.Id)
              .Select(e => e.Status)
              .First())
          .ToListAsync(ct);

      return new DashboardsView(
          new AwaitingAvailabilityTab(awaiting.Count, awaiting),
          new NoResponseTab(noResponse.Count, noResponse),
          new EventsTab(events.Count, events),
          latest.Count(status => status == EmailStatus.Failed),
          latest.Count(status => status == EmailStatus.Pending));
  }

  private async Task<IReadOnlyList<AwaitingAvailabilityRow>> AttendeeRowsAsync(
      AttendeeStatus status, DateOnly today, CancellationToken ct)
  {
      var rows = await context.Attendees
          .AsNoTracking()
          .Where(c => c.Status == status)
          .Select(c => new
          {
              c.Id,
              c.Name,
              c.Email,
              c.StatusChangedAt,
              Codes = c.Requirements
                  .Join(context.AppointmentTypes, r => r.AppointmentTypeId, t => t.Id,
                      (r, t) => t.Code)
                  .OrderBy(code => code)
                  .ToList(),
          })
          .OrderBy(c => c.StatusChangedAt)
          .ThenBy(c => c.Id)
          .ToListAsync(ct);

      return
      [
          .. rows.Select(c =>
          {
              var since = DateOnly.FromDateTime(c.StatusChangedAt.UtcDateTime);
              return new AwaitingAvailabilityRow(
                  c.Id, c.Name, c.Email, c.Codes, since, today.DayNumber - since.DayNumber);
          }),
      ];
  }
  ```

  The No response tab reuses the same projection and then maps to NoResponseRow, whose
  GaveUpOn is the same stamped date under a different name; keep one query rather than two
  that could drift. The ported row methods stay: the screens that read one tab at a time
  still call them, and Task 22's endpoint chooses.

  ```csharp
  // src/EventBooking.Application/Abstractions/IAttendeeListQueries.cs (complete)
  using EventBooking.Application.Attendees;
  using EventBooking.Application.ReadModels;

  namespace EventBooking.Application.Abstractions;

  /// <summary>The keyset-paginated attendee list. Refuses an Admin-shaped caller itself.</summary>
  public interface IAttendeeListQueries
  {
      Task<AttendeeListView> ListAttendeesAsync(
          CallerShape shape, string? cursor, int limit, string? status,
          Guid? attendeeGroupId, string? readiness, string? nameOrEmailPrefix,
          CancellationToken ct);
  }
  ```

  ```csharp
  // src/EventBooking.Infrastructure/Persistence/Queries/AttendeeListQueries.cs (complete)
  using System.Globalization;
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Attendees;
  using EventBooking.Application.ReadModels;
  using EventBooking.Domain.Attendees;
  using EventBooking.Domain.Notifications;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.EntityFrameworkCore.Storage;
  using Npgsql;
  using NpgsqlTypes;

  namespace EventBooking.Infrastructure.Persistence.Queries;

  /// <summary>
  /// One statement per page over the attendee set, keyset-ordered on (lower(name), id) to
  /// match the index the Task 20a migration adds. The column names are this schema's:
  /// `status` on both attendee and email_log is the enum stored through
  /// HasConversion&lt;int&gt;, so both are compared and read as integers.
  /// </summary>
  /// <param name="context">The read-only persistence context.</param>
  public sealed class AttendeeListQueries(EventBookingDbContext context) : IAttendeeListQueries
  {
      // The required type codes are projected only for a attendee still awaiting
      // availability: once invited, the invite's own snapshot is the authority, and showing
      // the live requirement set beside an invited row would contradict it.
      //
      // The latest delivery status is the newest outbox row for the attendee, or null when
      // none exists. LEFT JOIN LATERAL keeps that to one row per attendee rather than a
      // group-by over the whole log.
      private const string PageSql =
          """
          SELECT c.id,
                 c.name,
                 c.email,
                 c.status,
                 g.code AS group_code,
                 COALESCE(
                     CASE WHEN c.status = @awaiting THEN (
                         SELECT array_agg(t.code ORDER BY t.code)
                           FROM attendee_requirement r
                           JOIN appointment_type t ON t.id = r.appointment_type_id
                          WHERE r.attendee_id = c.id)
                     END,
                     '{}'::text[]) AS required_codes,
                 latest.status AS latest_delivery_status
            FROM attendee c
            JOIN attendee_group g ON g.id = c.attendee_group_id
            LEFT JOIN LATERAL (
                SELECT e.status
                  FROM email_log e
                 WHERE e.attendee_id = c.id
                 ORDER BY e.sent_at DESC NULLS LAST, e.id DESC
                 LIMIT 1) AS latest ON TRUE
           WHERE (@status IS NULL OR c.status = @status)
             AND (@groupId IS NULL OR c.attendee_group_id = @groupId)
             AND (@prefix IS NULL
                  OR lower(c.name) LIKE @prefix || '%'
                  OR lower(c.email) LIKE @prefix || '%')
             AND (@cursorName IS NULL
                  OR lower(c.name) > @cursorName
                  OR (lower(c.name) = @cursorName AND c.id > @cursorId))
           ORDER BY lower(c.name), c.id
           LIMIT @limit
          """;

      /// <inheritdoc />
      public async Task<AttendeeListView> ListAttendeesAsync(
          CallerShape shape, string? cursor, int limit, string? status,
          Guid? attendeeGroupId, string? readiness, string? nameOrEmailPrefix,
          CancellationToken ct)
      {
          ArgumentNullException.ThrowIfNull(shape);

          // The read model refuses the Admin shape itself. The handler checks for a
          // Coordinator profile, but an Admin who also holds one must still not read
          // attendee rows, and that rule belongs where the rows are.
          if (shape.IsAdmin)
          {
              return new AttendeeListView([], null);
          }

          // A filter naming a status this system does not have matches nothing. That is an
          // empty page, not an error.
          int? statusValue = null;
          if (status is not null)
          {
              if (!Enum.TryParse<AttendeeStatus>(status, ignoreCase: true, out var parsed))
              {
                  return new AttendeeListView([], null);
              }

              statusValue = (int)parsed;
          }

          string? cursorName = null;
          Guid? cursorId = null;
          if (AttendeeCursor.TryDecode(cursor, out var sortKey, out var decodedId))
          {
              cursorName = sortKey;
              cursorId = decodedId;
          }

          await context.Database.OpenConnectionAsync(ct);
          try
          {
              var connection = (NpgsqlConnection)context.Database.GetDbConnection();
              await using var command = new NpgsqlCommand(PageSql, connection);
              command.Transaction =
                  context.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction;

              Add(command, "status", NpgsqlDbType.Integer, statusValue);
              Add(command, "groupId", NpgsqlDbType.Uuid, attendeeGroupId);
              Add(command, "prefix", NpgsqlDbType.Text, nameOrEmailPrefix?.ToLowerInvariant());
              Add(command, "cursorName", NpgsqlDbType.Text, cursorName);
              Add(command, "cursorId", NpgsqlDbType.Uuid, cursorId);
              Add(command, "awaiting", NpgsqlDbType.Integer, (int)AttendeeStatus.AwaitingAvailability);
              command.Parameters.Add(new NpgsqlParameter("limit", limit + 1));

              var rows = new List<AttendeeListItem>(limit + 1);
              await using var reader = await command.ExecuteReaderAsync(ct);
              while (await reader.ReadAsync(ct))
              {
                  var id = reader.GetGuid(0);
                  var name = reader.GetString(1);
                  var attendeeStatus = (AttendeeStatus)reader.GetInt32(3);
                  var delivery = await reader.IsDBNullAsync(6, ct)
                      ? null
                      : ((EmailStatus)reader.GetInt32(6)).ToString();

                  rows.Add(new AttendeeListItem(
                      name,
                      reader.GetString(2),
                      attendeeStatus.ToString(),
                      reader.GetString(4),
                      AttendeeReadiness.Of(attendeeStatus, delivery),
                      reader.GetFieldValue<string[]>(5),
                      delivery,
                      AttendeeCursor.Encode(name.ToLowerInvariant(), id)));
              }

              // Readiness is a derived label, not a column, so it cannot be filtered in
              // SQL without materialising it there too. Filtering the page after the fact
              // would silently shorten pages, so it is applied here and the keyset still
              // advances by the last row actually read.
              if (readiness is not null)
              {
                  rows = [.. rows.Where(r => string.Equals(
                      r.Readiness, readiness, StringComparison.OrdinalIgnoreCase))];
              }

              if (rows.Count <= limit)
              {
                  return new AttendeeListView(rows, null);
              }

              var page = rows.Take(limit).ToList();
              return new AttendeeListView(page, page[^1].Cursor);
          }
          finally
          {
              await context.Database.CloseConnectionAsync();
          }
      }

      private static void Add(
          NpgsqlCommand command, string name, NpgsqlDbType type, object? value) =>
          command.Parameters.Add(
              new NpgsqlParameter(name, type) { Value = value ?? DBNull.Value });
  }
  ```

  AttendeeReadiness.Of is the ported readiness calculator, moved beside the query; keep its
  existing rule over the attendee status and the latest delivery state rather than restating
  it here.

  DashboardQueries.cs is **not** supplied. The description above fixes the Events-tab bound
  and the Admin refusal, but nothing in this document, its tests or the master plan says what
  the four counts mean per tab — whether the Events tab counts attendees holding a booking on
  an event in the window, the Attendees tab counts every attendee, and the Recovery tab
  counts attendees with an active recovery booking. Writing the SQL would settle that by
  implication. Settle it with the user, then write it the way the attendee list above is
  written. Its Infrastructure suite is in the same position.

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