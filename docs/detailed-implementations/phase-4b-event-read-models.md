# 04b — The read models the catalogue needs (Task 22a)

[← Phase overview](phase-4-api-and-mcp.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This task follows Task 21 and is the first half of master Task 22. It adds the Application read
side design 05 asks for and Phase 3 does not supply: the filtered `Event` list and the single
`Event` read, the cancellable-`Event` list, the filtered `EventProposal` list, `includeInactive`
on the three reference-data lists, activation folded into the reference-data update so one PUT
serves design 05's body, the appointment-type identifier on the capacity adjustment, and the
readiness capability correction. Task 22b then maps endpoints onto all of it without holding a
rule of its own.

> Use superpowers:executing-plans. This task is hand-authored: complete code and complete tests
> are written straight into this document, with no prototype. Compile and test-drive them
> yourself. The test counts below are what you should expect to reach, not figures observed by
> the author — nothing here has been run.

**Goal:** One keyset-paged `Event` query with design 05's four filters, scoped by what the caller
holds; one single-`Event` read filtered the same way; one cancellable-`Event` list bounded to
windows that have not started; one keyset-paged `EventProposal` list filtered by status and
`Location`; `includeInactive` on the location, appointment-type and attendee-group lists;
`isActive` folded into each reference-data update so design 05's single PUT body works;
the capacity-adjustment command carrying the appointment-type identifier the route names; and
attendee readiness demanding `ViewAttendeeDashboards`.

**Architecture:** The scope rule lives in the query, not only in the handler, the way Task 20b
put the audit bucket rule in its query: bypassing the capability check still filters. A Manager
sees their own type's capacity row and nothing else; an Admin or a Coordinator sees every listed
type. Paging is keyset on the derived start instant plus the row identifier — the column Task 11
computes and indexes — through the cursor payload contract Task 20a defined, generalised here so
the attendee list and the event list share one codec. No domain entity leaves a handler, and
every new query returns projections.

**Tech Stack:** .NET 10, xUnit, EF Core with Npgsql, Testcontainers, PostgreSQL 16.

**Spec:** [Master Task 22](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[API design](../design/05-api-design.md),
[functional requirements](../design/02-functional-requirements.md),
[solution architecture](../design/04-solution-architecture.md), [ontology](../ontology.md).

## Why this task exists

Master Task 22 scopes itself as wiring — "Consumes: every Phase 3 handler", and "endpoints only
translate HTTP to handler calls; no business rule lives in an endpoint". Seven of design 05's
endpoints have nothing to consume. Task 20a deletes the ported operations handler outright
("operations board superseded by the Task 16 workspace"), and Task 13's negotiation board takes a
staff identity and no filters, so `GET /api/events`, `GET /api/events/{id}` and
`GET /api/events/cancellable` have no handler at all. Filtering in the endpoint instead is the
in-memory shape Task 11 removed from the eligibility path at some 680 ms against 18 ms, so it is
not a neutral fallback. The user settled this as a lettered split (contradiction #11).

## Settlements this task implements

- **#12.** `GET /api/events` carries two capabilities in design 05, against design 04's "exactly
  one `StaffCapability` per handler". Task 20b's audit search already settled this shape: the
  handler demands neither and filters by what the caller holds. Here that is the scope, not a
  bucket — a Manager's view is their own type's.
- **#13.** Attendee readiness demands `ViewAttendeeDashboards`, per design 05, not the ported
  handler's `ManageAttendees`.
- **Activation folds into the update.** Design 05's PUT bodies carry `isActive`; Task 12 splits
  update from set-active into two commands and two handlers. One endpoint calling two handlers
  would be a rule in the endpoint, and two round trips would make a half-applied PUT possible.
  The update handlers take `isActive` and apply the activation change in the same save, through
  the same domain method, with the same in-use refusal. The three set-active handlers and their
  commands are deleted — nothing else calls them, and a handler with no endpoint would also have
  no MCP tool under Task 23's parity rule.

The route has an attendee identifier but not an email-log identifier, so the retry handler must
own the newest failed-delivery selection rather than asking an endpoint to invent one. This keeps
the retry rule transactional and makes both transports name the same operation.

```csharp
// src/EventBooking.Application/Notifications/IEmailDeliveryRepository.cs — add this member to
// the existing port. The infrastructure implementation orders by CreatedAt descending and locks
// the selected row with FOR UPDATE, returning null when this attendee has no failed delivery.
Task<EmailLog?> LockNewestFailedForAttendeeAsync(Guid attendeeId, CancellationToken ct);

// src/EventBooking.Application/Notifications/RetryEmailHandler.cs — replace the public command
// shape and complete the existing handler using the attendee-first locked lookup.
public sealed record RetryNewestEmailCommand(Guid StaffUserId, Guid AttendeeId);

public async Task<Result<RetryEmailOutcome>> HandleAsync(
    RetryNewestEmailCommand command, CancellationToken ct)
{
    var authorized = await access.AuthorizeAsync(
        command.StaffUserId, StaffCapability.ManageAttendees, null, ct);
    if (authorized.IsFailure) return Result<RetryEmailOutcome>.Failure(authorized.Error);

    await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
    var newest = await deliveries.LockNewestFailedForAttendeeAsync(command.AttendeeId, ct);
    if (newest is null) return Result<RetryEmailOutcome>.Failure(
        Error.NotFound("This attendee has no failed delivery to retry."));

    var replacement = EmailLog.RecordPending(Guid.NewGuid(), newest.AttendeeId,
        newest.TemplateName, clock.UtcNow, newest.InviteId, newest.BookingId, newest.EventId);
    deliveries.Add(replacement);
    newest.MarkResolved(clock.UtcNow);
    await unitOfWork.SaveChangesAsync(ct);
    await transaction.CommitAsync(ct);
    return Result<RetryEmailOutcome>.Success(new RetryEmailOutcome(replacement.Id));
}
```

The MCP list contract also exposes a real event status on the workspace rows. `WorkspaceEventView`
already comes from the `Event` aggregate, so the handler projects its existing status; it does
not invent a display-only state.

```csharp
// src/EventBooking.Application/Appointments/WorkspaceEventHandlers.cs — replace the existing view.
public sealed record WorkspaceEventView(
    Guid EventId, Guid LocationId, string LocationName, DateOnly Date,
    TimeOnly StartTime, TimeOnly EndTime, string ZoneAbbreviation, string Status);

// src/EventBooking.Application/Appointments/WorkspaceEventHandlers.cs — replace the projection.
.Select(e => new WorkspaceEventView(e.Id, e.LocationId,
    zoneByLocation[e.LocationId].Name,
    e.Window.Date, e.Window.StartTime, e.Window.EndTime,
    zones.AbbreviationOf(EventEnd(e, zoneByLocation[e.LocationId].TimeZoneId, zones),
        zoneByLocation[e.LocationId].TimeZoneId),
    e.Status.ToString()))
```

## Global constraints

Every new read demands exactly one `StaffCapability`, except the `Event` reads that settlement
#12 governs, which demand none and filter instead. Scope filtering is in the query as well as the
handler. Paging is keyset, never offset, and a page is ordered by the derived start instant then
the identifier so the order is total. No projection carries a name or an email address except the
attendee reads that already do. Reference-data reads stay open to any staff member, as Task 12
made them.

## Review focus

STOP AND CHECK four things. The scope test drives the query directly, with no capability check in
front of it, and asserts a Manager still sees one capacity row — a test that goes through the
handler proves only that the handler filtered. The paging test walks a set whose start instants
collide, because a keyset on a non-unique column is exactly where a page boundary loses or
repeats a row. The activation test asserts that a PUT turning `isActive` to false while the
`Location` hosts an open `EventProposal` changes neither the name nor the flag — one save, wholly
refused. And the capacity test asserts that a Manager naming another type's identifier in the
route is refused, rather than silently adjusting their own.

### Task 22a: The read models the endpoint catalogue needs

**Files:**

- Create: src/EventBooking.Application/ReadModels/KeysetCursor.cs
- Modify: src/EventBooking.Application/ReadModels/AttendeeCursor.cs (forwards to the shared codec)
- Create: src/EventBooking.Application/Events/EventReadModels.cs (queries, views and the port)
- Create: src/EventBooking.Application/Events/EventReadHandlers.cs (list, get, cancellable)
- Create: src/EventBooking.Application/Negotiation/ListEventProposalsHandler.cs
- Modify: src/EventBooking.Application/Negotiation/AdjustEventCapacityHandler.cs (the route's type)
- Modify: src/EventBooking.Application/ReferenceData/LocationHandlers.cs (includeInactive; isActive on update; the set-active handler deleted)
- Modify: src/EventBooking.Application/ReferenceData/AppointmentTypeHandlers.cs (the same three changes)
- Modify: src/EventBooking.Application/ReferenceData/AttendeeGroupHandlers.cs (the same three changes)
- Modify: src/EventBooking.Application/Attendees/GetAttendeeReadinessHandler.cs (ViewAttendeeDashboards)
- Modify: src/EventBooking.Application/Notifications/IEmailDeliveryRepository.cs (lock the newest failed delivery by attendee)
- Modify: src/EventBooking.Application/Notifications/RetryEmailHandler.cs (resolve the newest failed delivery from the attendee route)
- Modify: src/EventBooking.Application/Appointments/WorkspaceEventHandlers.cs (project the event status)
- Create: src/EventBooking.Infrastructure/Persistence/Queries/EventReadQueries.cs
- Modify: src/EventBooking.Infrastructure/DependencyInjection.cs (register the new port and handlers)
- Test: tests/EventBooking.Application.Tests/Events/EventReadHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/ReferenceData/ReferenceDataActivationTests.cs
- Modify: tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs (retry by attendee and cover no failed delivery)
- Modify: tests/EventBooking.Application.Tests/Appointments/WorkspaceHandlerTests.cs (assert the workspace row carries the event status)
- Test: tests/EventBooking.Application.Tests/Fakes/FakeUnitOfWork.cs (a Reset for the counters)
- Test: tests/EventBooking.Infrastructure.Tests/Queries/EventReadQueryTests.cs

Task 12's three reference-data suites construct the update handlers positionally and name the
set-active handlers; both change here, so their existing cases need their constructions updated
alongside. That is a mechanical edit across three files, not new coverage.

**Interfaces:**

```csharp
namespace EventBooking.Application.ReadModels;

// The one keyset codec. Task 20a defined this payload contract on the attendee list; the event
// and proposal lists need the same thing, so it moves here and AttendeeCursor forwards to it.
// Opaque to callers — Task 21's PageCursor adds the HMAC wrapper at the boundary.
public static class KeysetCursor
{
    public static string Encode(string sortKey, Guid id);
    public static bool TryDecode(string? cursor, out string sortKey, out Guid id);
}
```

```csharp
// src/EventBooking.Application/Events/EventReadModels.cs
namespace EventBooking.Application.Events;

// What an Event read is allowed to show. Resolved once per request from the access profile.
// AllTypes is true for a caller holding ViewEventOperations; AppointmentTypeId is the Manager's
// own scope. A caller with neither sees nothing, which is a 403 at the handler.
public sealed record EventScope(bool AllTypes, Guid? AppointmentTypeId);

// Design 05's four filters on GET /api/events, plus the page. From and To are local calendar
// dates at the Location, inclusive, because that is what a staff member filtering a calendar
// means; the query compares them against the stored date, not against the derived instant.
public sealed record ListEventsQuery(
    Guid StaffUserId, Guid? LocationId, DateOnly? From, DateOnly? To,
    Guid? AppointmentTypeId, string? Cursor, int Limit);

public sealed record GetEventQuery(Guid StaffUserId, Guid EventId);

// FR-7.2: the operations list is the events a cancellation could still reach, so the window must
// not have started. The bound is the derived start instant, not the date.
public sealed record ListCancellableEventsQuery(
    Guid StaffUserId, Guid? LocationId, DateOnly? From, DateOnly? To, string? Cursor, int Limit);

public sealed record EventCapacityView(
    Guid AppointmentTypeId, string Code, string Name, int TotalHeadcount, int RemainingCapacity);

public sealed record EventView(
    Guid EventId, Guid ProposalId, Guid LocationId, string LocationCode, string LocationName,
    string TimeZoneId, DateOnly Date, TimeOnly StartTime, int DurationMinutes, string Status,
    IReadOnlyList<EventCapacityView> Capacities, int ActiveBookings, string Cursor);

public sealed record EventListView(IReadOnlyList<EventView> Items, string? NextCursor);

// The read side. Implementations project directly and apply the scope themselves, so a caller
// that reached the query without a capability check still sees only its own type's capacity.
public interface IEventReadQueries
{
    Task<IReadOnlyList<EventView>> ListAsync(
        ListEventsQuery query, EventScope scope, DateTimeOffset now, bool notStartedOnly,
        CancellationToken ct);

    Task<EventView?> GetAsync(Guid eventId, EventScope scope, CancellationToken ct);
}
```

```csharp
namespace EventBooking.Application.Negotiation;

// Design 05: the caller's type's proposals (FR-2.13), filtered by status and Location. The
// status is an EventProposalStatus name; anything else is a field error rather than an empty
// page, because a caller who misspells Open should be told, not shown nothing.
public sealed record ListEventProposalsQuery(
    Guid StaffUserId, string? Status, Guid? LocationId, string? Cursor, int Limit);

public sealed record EventProposalListItem(
    Guid ProposalId, Guid LocationId, string LocationCode, string LocationName, string TimeZoneId,
    DateOnly Date, TimeOnly StartTime, int DurationMinutes, string Status,
    int ListedTypeCount, int AcceptedTypeCount, int? MyAcceptedHeadcount,
    bool AcceptedByMe, bool CreatedByMe, string Cursor);

public sealed record EventProposalListView(
    IReadOnlyList<EventProposalListItem> Items, string? NextCursor);
```

```csharp
// The three reference-data update commands gain isActive, and the three set-active commands and
// handlers are deleted. The list queries gain includeInactive. Everything else in Task 12's
// records is unchanged.
public sealed record UpdateLocationCommand(
    Guid StaffUserId, Guid LocationId, string? Name, string? Address, string? TimeZoneId,
    bool IsActive, long ExpectedVersion);
public sealed record ListLocationsQuery(bool IncludeInactive);

public sealed record UpdateAppointmentTypeCommand(
    Guid StaffUserId, Guid AppointmentTypeId, string? Name, bool IsActive, long ExpectedVersion);
public sealed record ListAppointmentTypesQuery(bool IncludeInactive);

public sealed record UpdateAttendeeGroupCommand(
    Guid StaffUserId, Guid AttendeeGroupId, string? Name, IReadOnlyList<Guid>? AppointmentTypeIds,
    bool IsActive, long ExpectedVersion);
public sealed record ListAttendeeGroupsQuery(bool IncludeInactive);

// The capacity route names the appointment type; the command now carries it, and the handler
// refuses a type that is not the caller's own rather than adjusting the caller's by mistake.
public sealed record AdjustEventCapacityCommand(
    Guid StaffUserId, Guid EventId, Guid AppointmentTypeId, int TotalHeadcount);
```

- [ ] **Step 1: Write the failing tests.** Three files. The Application suite drives the handlers
  with an in-memory query double; the scope rule itself is proved against real PostgreSQL,
  because a double that honours the rule proves only that the double honours it.

  ```csharp
  // tests/EventBooking.Application.Tests/Events/EventReadHandlerTests.cs (complete)
  using EventBooking.Application.Access;
  using EventBooking.Application.Common;
  using EventBooking.Application.Events;
  using EventBooking.Application.ReadModels;
  using EventBooking.Domain.Access;

  namespace EventBooking.Application.Tests.Events;

  /// <summary>The scope a caller resolves to, the page bounds, and the refusals.</summary>
  public sealed class EventReadHandlerTests
  {
      private static readonly Guid Manager = Guid.NewGuid();
      private static readonly Guid Coordinator = Guid.NewGuid();
      private static readonly Guid Nobody = Guid.NewGuid();
      private static readonly Guid MedicalType = Guid.NewGuid();

      [Fact]
      public async Task AManagerResolvesToTheirOwnTypesScope()
      {
          var queries = new RecordingQueries();
          var handler = new ListEventsHandler(new FakeAuthorizer(), queries, new FixedClock());

          var result = await handler.HandleAsync(Query(Manager), CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.False(queries.LastScope!.AllTypes);
          Assert.Equal(MedicalType, queries.LastScope.AppointmentTypeId);
      }

      [Fact]
      public async Task ACoordinatorResolvesToEveryType()
      {
          var queries = new RecordingQueries();
          var handler = new ListEventsHandler(new FakeAuthorizer(), queries, new FixedClock());

          var result = await handler.HandleAsync(Query(Coordinator), CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.True(queries.LastScope!.AllTypes);
          Assert.Null(queries.LastScope.AppointmentTypeId);
      }

      [Fact]
      public async Task ACallerWithNeitherCapabilityIsForbidden()
      {
          var handler = new ListEventsHandler(
              new FakeAuthorizer(), new RecordingQueries(), new FixedClock());

          var result = await handler.HandleAsync(Query(Nobody), CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("forbidden", result.Error.Code);
      }

      [Theory]
      [InlineData(0)]
      [InlineData(201)]
      public async Task ALimitOutsideTheRangeIsRefused(int limit)
      {
          var handler = new ListEventsHandler(
              new FakeAuthorizer(), new RecordingQueries(), new FixedClock());

          var result = await handler.HandleAsync(
              Query(Coordinator) with { Limit = limit }, CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("validation", result.Error.Code);
      }

      /// <summary>
      /// The query is asked for one row more than the page, and the extra row is what proves
      /// there is a next page. Asking for exactly the page size cannot distinguish a full last
      /// page from a full page with more behind it.
      /// </summary>
      [Fact]
      public async Task AFullPageWithMoreBehindItCarriesTheLastKeptRowsCursor()
      {
          var queries = new RecordingQueries { Rows = Rows(4) };
          var handler = new ListEventsHandler(new FakeAuthorizer(), queries, new FixedClock());

          var result = await handler.HandleAsync(
              Query(Coordinator) with { Limit = 3 }, CancellationToken.None);

          Assert.Equal(4, queries.LastLimit);
          Assert.Equal(3, result.Value.Items.Count);
          Assert.Equal(result.Value.Items[2].Cursor, result.Value.NextCursor);
      }

      [Fact]
      public async Task TheLastPageCarriesNoNextCursor()
      {
          var queries = new RecordingQueries { Rows = Rows(2) };
          var handler = new ListEventsHandler(new FakeAuthorizer(), queries, new FixedClock());

          var result = await handler.HandleAsync(
              Query(Coordinator) with { Limit = 3 }, CancellationToken.None);

          Assert.Equal(2, result.Value.Items.Count);
          Assert.Null(result.Value.NextCursor);
      }

      [Fact]
      public async Task TheCancellableListAsksForNotStartedEventsOnly()
      {
          var queries = new RecordingQueries();
          var handler = new ListCancellableEventsHandler(
              new FakeAuthorizer(), queries, new FixedClock());

          await handler.HandleAsync(
              new ListCancellableEventsQuery(Coordinator, null, null, null, null, 50),
              CancellationToken.None);

          Assert.True(queries.LastNotStartedOnly);
      }

      [Fact]
      public async Task AnEventOutsideTheCallersScopeReadsAsNotFound()
      {
          var handler = new GetEventHandler(
              new FakeAuthorizer(), new RecordingQueries { Rows = [] });

          var result = await handler.HandleAsync(
              new GetEventQuery(Manager, Guid.NewGuid()), CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("not_found", result.Error.Code);
      }

      private static ListEventsQuery Query(Guid staffUserId) =>
          new(staffUserId, null, null, null, null, null, 50);

      private static List<EventView> Rows(int count) =>
          [.. Enumerable.Range(0, count).Select(index => new EventView(
              Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "LON", "London", "Europe/London",
              new DateOnly(2026, 10, 14), new TimeOnly(9, 30), 90, "Active", [], 0,
              KeysetCursor.Encode($"2026-10-14T09:3{index}:00Z", Guid.NewGuid())))];

      private sealed class FixedClock : IClock
      {
          public DateTimeOffset UtcNow => new(2026, 9, 21, 9, 0, 0, TimeSpan.Zero);

          public DateTimeOffset NowAtTransitionalLocation => UtcNow;

          public DateOnly TodayAtTransitionalLocation => DateOnly.FromDateTime(UtcNow.UtcDateTime);

          public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) =>
              DateOnly.FromDateTime(instant.UtcDateTime);

          public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => instant;
      }

      /// <summary>One Manager scoped to medical, one Coordinator, and one caller with nothing.</summary>
      private sealed class FakeAuthorizer : IStaffAccessAuthorizer
      {
          public Task<Result<StaffAccessContext>> AuthorizeAsync(
              Guid staffUserId, StaffCapability capability, Guid? requiredAppointmentTypeId,
              CancellationToken cancellationToken)
          {
              var granted = (staffUserId, capability) switch
              {
                  _ when staffUserId == Coordinator &&
                      capability == StaffCapability.ViewEventOperations =>
                      new StaffAccessContext(staffUserId, Set(Role.Coordinator), null),
                  _ when staffUserId == Manager &&
                      capability == StaffCapability.ManageEventNegotiation =>
                      new StaffAccessContext(staffUserId, Set(Role.Manager), MedicalType),
                  _ => null,
              };

              return Task.FromResult(granted is null
                  ? Result<StaffAccessContext>.Failure(Error.Forbidden("No."))
                  : Result<StaffAccessContext>.Success(granted));
          }

          private static IReadOnlySet<Role> Set(Role role) => new HashSet<Role> { role };
      }

      private sealed class RecordingQueries : IEventReadQueries
      {
          public List<EventView> Rows { get; set; } = [];

          public EventScope? LastScope { get; private set; }

          public int LastLimit { get; private set; }

          public bool LastNotStartedOnly { get; private set; }

          public Task<IReadOnlyList<EventView>> ListAsync(
              ListEventsQuery query, EventScope scope, DateTimeOffset now, bool notStartedOnly,
              CancellationToken ct)
          {
              LastScope = scope;
              LastLimit = query.Limit;
              LastNotStartedOnly = notStartedOnly;
              return Task.FromResult<IReadOnlyList<EventView>>(Rows.Take(query.Limit).ToList());
          }

          public Task<EventView?> GetAsync(Guid eventId, EventScope scope, CancellationToken ct)
          {
              LastScope = scope;
              return Task.FromResult(Rows.FirstOrDefault());
          }
      }
  }
  ```

  ```csharp
  // tests/EventBooking.Application.Tests/ReferenceData/ReferenceDataActivationTests.cs (complete)
  using EventBooking.Application.Common;
  using EventBooking.Application.ReferenceData;
  using EventBooking.Application.Tests.Fakes;
  using EventBooking.Domain.Access;
  using EventBooking.Domain.Locations;

  namespace EventBooking.Application.Tests.ReferenceData;

  /// <summary>
  /// Design 05's single PUT body carries isActive, so one update applies the name, the address,
  /// the zone and the activation in one save — or refuses the whole thing. The doubles are Task
  /// 12's, unchanged.
  /// </summary>
  public sealed class ReferenceDataActivationTests
  {
      private static readonly Guid Admin = Guid.Parse("a0000001-0000-0000-0000-000000000001");

      private readonly InMemoryLocationRepository _locations = new();
      private readonly InMemoryStaffAccessProfileRepository _profiles = new();
      private readonly FakeUnitOfWork _unitOfWork = new();
      private readonly RecordingAuditLogger _audit = new();
      private readonly MemoryBlocking _blocking = new();

      public ReferenceDataActivationTests() =>
          _profiles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));

      private UpdateLocationHandler Updater =>
          new(_locations, _profiles, _unitOfWork, _audit, TestZones.Instance, _blocking);

      private ListLocationsHandler Lister => new(_locations);

      [Fact]
      public async Task AnUpdateAppliesTheNameAndTheActivationInOneSave()
      {
          var location = await GivenLocationAsync("ACT_ONE", "Old name");

          var result = await Updater.HandleAsync(
              new UpdateLocationCommand(
                  Admin, location.Id, "New name", "1 New Street", "Europe/London",
                  IsActive: false, location.Version),
              CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal("New name", result.Value.Name);
          Assert.False(result.Value.IsActive);
          Assert.Equal(1, _unitOfWork.SaveCount);
      }

      /// <summary>
      /// The refusal is the whole command, not the activation half of it. A PUT that renamed the
      /// row and then failed to deactivate it would leave the caller's version stale and their
      /// screen wrong.
      /// </summary>
      [Fact]
      public async Task DeactivatingWhileInUseRefusesTheWholeUpdate()
      {
          var location = await GivenLocationAsync("ACT_TWO", "Old name");
          _blocking.LocationUsageValue = new LocationUsage(1, 2);

          var result = await Updater.HandleAsync(
              new UpdateLocationCommand(
                  Admin, location.Id, "New name", "1 New Street", "Europe/London",
                  IsActive: false, location.Version),
              CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal(Error.ReferenceDataInUseCode, result.Error.Code);
          Assert.Equal(1L, result.Error.Data!["openProposals"]);
          Assert.Equal(2L, result.Error.Data["futureEvents"]);
          var reread = await _locations.GetAsync(location.Id, CancellationToken.None);
          Assert.Equal("Old name", reread!.Name);
          Assert.True(reread.IsActive);
          Assert.Equal(0, _unitOfWork.SaveCount);
      }

      /// <summary>
      /// An update that leaves the flag alone is not a deactivation, so a location with live
      /// scheduling can still be renamed. Without this case the folded activation would make
      /// every rename fail the moment anything was booked at the site.
      /// </summary>
      [Fact]
      public async Task AnUpdateThatDoesNotChangeActivationIsNotBlockedByUsage()
      {
          var location = await GivenLocationAsync("ACT_THREE", "Old name");
          _blocking.LocationUsageValue = new LocationUsage(5, 5);

          var result = await Updater.HandleAsync(
              new UpdateLocationCommand(
                  Admin, location.Id, "New name", "1 New Street", "Europe/London",
                  IsActive: true, location.Version),
              CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal("New name", result.Value.Name);
          Assert.True(result.Value.IsActive);
      }

      [Fact]
      public async Task TheListHidesInactiveRowsUnlessAsked()
      {
          var active = await GivenLocationAsync("ACT_LIVE", "Live");
          var retired = await GivenLocationAsync("ACT_DEAD", "Retired");
          await Updater.HandleAsync(
              new UpdateLocationCommand(
                  Admin, retired.Id, "Retired", "1 Old Street", "Europe/London",
                  IsActive: false, retired.Version),
              CancellationToken.None);

          var hidden = await Lister.HandleAsync(
              new ListLocationsQuery(IncludeInactive: false), CancellationToken.None);
          var shown = await Lister.HandleAsync(
              new ListLocationsQuery(IncludeInactive: true), CancellationToken.None);

          Assert.Equal([active.Id], hidden.Value.Select(x => x.Id));
          Assert.Equal(2, shown.Value.Count);
      }

      /// <summary>Codes order the page, so the flag cannot reorder what the caller already saw.</summary>
      [Fact]
      public async Task TheListStaysInCodeOrderEitherWay()
      {
          await GivenLocationAsync("ACT_ZULU", "Zulu");
          await GivenLocationAsync("ACT_ALPHA", "Alpha");

          var shown = await Lister.HandleAsync(
              new ListLocationsQuery(IncludeInactive: true), CancellationToken.None);

          Assert.Equal(
              shown.Value.Select(x => x.Code).Order(StringComparer.Ordinal),
              shown.Value.Select(x => x.Code));
      }

      private async Task<Location> GivenLocationAsync(string code, string name)
      {
          var created = await new CreateLocationHandler(
                  _locations, _profiles, _unitOfWork, _audit, TestZones.Instance)
              .HandleAsync(
                  new CreateLocationCommand(Admin, code, name, "1 Old Street", "Europe/London"),
                  CancellationToken.None);
          _unitOfWork.Reset();
          return (await _locations.GetAsync(created.Value.Id, CancellationToken.None))!;
      }
  }
  ```

  The fake unit of work gains a `Reset()` that zeroes its three counters, so a case can seed through a
  handler and still assert "exactly one save". It has no other caller and no other effect.
  The blocking and zone doubles are Task 12's, unchanged.

  ```csharp
  // tests/EventBooking.Infrastructure.Tests/Queries/EventReadQueryTests.cs (complete)
  using EventBooking.Application.Events;
  using EventBooking.Application.ReadModels;
  using EventBooking.Domain.AppointmentTypes;
  using EventBooking.Domain.Events;
  using EventBooking.Domain.Locations;
  using EventBooking.Infrastructure.Persistence;
  using EventBooking.Infrastructure.Persistence.Queries;
  using Microsoft.EntityFrameworkCore;

  namespace EventBooking.Infrastructure.Tests.Queries;

  /// <summary>
  /// The scope rule and the keyset walk, against real PostgreSQL. The scope cases drive the
  /// query with no capability check in front of it, because that is the only way to prove the
  /// filter is in the query rather than only in the handler.
  /// </summary>
  [Collection("postgres")]
  public sealed class EventReadQueryTests(PostgresFixture fixture)
  {
      private static readonly DateTimeOffset Now = new(2026, 9, 21, 9, 0, 0, TimeSpan.Zero);
      private static readonly EventScope EveryType = new(AllTypes: true, null);
      private static readonly EventScope MedicalOnly =
          new(AllTypes: false, AppointmentTypeIds.MedicalCheckUp);

      [Fact]
      public async Task AnAllTypesCallerSeesEveryCapacityRow()
      {
          await fixture.ResetAsync();
          var location = await GivenLocationAsync("QRY_ALL", "Europe/London");
          await GivenEventAsync(location, new DateOnly(2026, 10, 14), new TimeOnly(9, 30));

          var rows = await Query().ListAsync(
              Filter(), EveryType, Now, notStartedOnly: false, CancellationToken.None);

          Assert.Equal(3, Assert.Single(rows).Capacities.Count);
      }

      /// <summary>
      /// Driven straight at the query. A Manager sees their own type's headcount and no other
      /// type's, which is the whole of design 05's "a Manager sees their type's view".
      /// </summary>
      [Fact]
      public async Task AScopedCallerSeesOnlyTheirOwnTypesCapacity()
      {
          await fixture.ResetAsync();
          var location = await GivenLocationAsync("QRY_SCOPE", "Europe/London");
          await GivenEventAsync(location, new DateOnly(2026, 10, 14), new TimeOnly(9, 30));

          var rows = await Query().ListAsync(
              Filter(), MedicalOnly, Now, notStartedOnly: false, CancellationToken.None);

          var capacity = Assert.Single(Assert.Single(rows).Capacities);
          Assert.Equal(AppointmentTypeIds.MedicalCheckUp, capacity.AppointmentTypeId);
      }

      /// <summary>An event that does not list the caller's type is not theirs to see at all.</summary>
      [Fact]
      public async Task AnEventThatDoesNotListTheScopedTypeIsAbsent()
      {
          await fixture.ResetAsync();
          var location = await GivenLocationAsync("QRY_ABSENT", "Europe/London");
          await GivenEventAsync(
              location, new DateOnly(2026, 10, 14), new TimeOnly(9, 30),
              listed: [AppointmentTypeIds.DrugAndAlcoholTesting]);

          var rows = await Query().ListAsync(
              Filter(), MedicalOnly, Now, notStartedOnly: false, CancellationToken.None);

          Assert.Empty(rows);
      }

      [Fact]
      public async Task ASingleReadOutsideTheScopeIsNull()
      {
          await fixture.ResetAsync();
          var location = await GivenLocationAsync("QRY_ONE", "Europe/London");
          var eventId = await GivenEventAsync(
              location, new DateOnly(2026, 10, 14), new TimeOnly(9, 30),
              listed: [AppointmentTypeIds.DrugAndAlcoholTesting]);

          Assert.NotNull(await Query().GetAsync(eventId, EveryType, CancellationToken.None));
          Assert.Null(await Query().GetAsync(eventId, MedicalOnly, CancellationToken.None));
      }

      /// <summary>
      /// Five events sharing one start instant. A keyset on the instant alone would either
      /// repeat the boundary row or drop everything after it, so this is the case that proves
      /// the identifier is part of the key.
      /// </summary>
      [Fact]
      public async Task AWalkOverCollidingStartInstantsLosesAndRepeatsNothing()
      {
          await fixture.ResetAsync();
          var location = await GivenLocationAsync("QRY_TIE", "Europe/London");
          for (var index = 0; index < 5; index++)
          {
              await GivenEventAsync(location, new DateOnly(2026, 10, 14), new TimeOnly(9, 30));
          }

          var seen = new List<Guid>();
          string? cursor = null;
          for (var page = 0; page < 5; page++)
          {
              var rows = await Query().ListAsync(
                  Filter() with { Cursor = cursor, Limit = 2 },
                  EveryType, Now, notStartedOnly: false, CancellationToken.None);
              if (rows.Count == 0)
              {
                  break;
              }

              seen.AddRange(rows.Select(x => x.EventId));
              cursor = rows[^1].Cursor;
          }

          Assert.Equal(5, seen.Count);
          Assert.Equal(5, seen.Distinct().Count());
      }

      [Fact]
      public async Task TheLocationFilterNarrowsToOneSite()
      {
          await fixture.ResetAsync();
          var london = await GivenLocationAsync("QRY_LON", "Europe/London");
          var tokyo = await GivenLocationAsync("QRY_TOK", "Asia/Tokyo");
          await GivenEventAsync(london, new DateOnly(2026, 10, 14), new TimeOnly(9, 30));
          await GivenEventAsync(tokyo, new DateOnly(2026, 10, 14), new TimeOnly(9, 30));

          var rows = await Query().ListAsync(
              Filter() with { LocationId = tokyo }, EveryType, Now, notStartedOnly: false,
              CancellationToken.None);

          Assert.Equal("Asia/Tokyo", Assert.Single(rows).TimeZoneId);
      }

      /// <summary>
      /// London and Tokyo, not London and Dublin: contradiction #10. The two windows share a
      /// local date and time, and the ordering is by the derived instant, so Tokyo's must come
      /// first — an ordering that a shared offset could not demonstrate at all.
      /// </summary>
      [Fact]
      public async Task TheOrderIsByTheDerivedInstantRatherThanTheLocalTime()
      {
          await fixture.ResetAsync();
          var london = await GivenLocationAsync("QRY_ORD_LON", "Europe/London");
          var tokyo = await GivenLocationAsync("QRY_ORD_TOK", "Asia/Tokyo");
          await GivenEventAsync(london, new DateOnly(2026, 10, 14), new TimeOnly(9, 30));
          await GivenEventAsync(tokyo, new DateOnly(2026, 10, 14), new TimeOnly(9, 30));

          var rows = await Query().ListAsync(
              Filter(), EveryType, Now, notStartedOnly: false, CancellationToken.None);

          Assert.Equal(["Asia/Tokyo", "Europe/London"], rows.Select(x => x.TimeZoneId));
      }

      [Fact]
      public async Task TheDateRangeIsInclusiveAtBothEnds()
      {
          await fixture.ResetAsync();
          var location = await GivenLocationAsync("QRY_RANGE", "Europe/London");
          await GivenEventAsync(location, new DateOnly(2026, 10, 13), new TimeOnly(9, 30));
          await GivenEventAsync(location, new DateOnly(2026, 10, 14), new TimeOnly(9, 30));
          await GivenEventAsync(location, new DateOnly(2026, 10, 15), new TimeOnly(9, 30));

          var rows = await Query().ListAsync(
              Filter() with { From = new DateOnly(2026, 10, 13), To = new DateOnly(2026, 10, 14) },
              EveryType, Now, notStartedOnly: false, CancellationToken.None);

          Assert.Equal(2, rows.Count);
      }

      [Fact]
      public async Task TheAppointmentTypeFilterKeepsEventsListingThatType()
      {
          await fixture.ResetAsync();
          var location = await GivenLocationAsync("QRY_TYPE", "Europe/London");
          await GivenEventAsync(
              location, new DateOnly(2026, 10, 14), new TimeOnly(9, 30),
              listed: [AppointmentTypeIds.DrugAndAlcoholTesting]);
          await GivenEventAsync(location, new DateOnly(2026, 10, 15), new TimeOnly(9, 30));

          var rows = await Query().ListAsync(
              Filter() with { AppointmentTypeId = AppointmentTypeIds.MedicalCheckUp },
              EveryType, Now, notStartedOnly: false, CancellationToken.None);

          Assert.Equal(new DateOnly(2026, 10, 15), Assert.Single(rows).Date);
      }

      /// <summary>
      /// FR-7.2: the cancellable list is what a cancellation can still reach. A window that has
      /// started and a cancelled event are both out, and the bound is the derived instant.
      /// </summary>
      [Fact]
      public async Task TheCancellableListExcludesStartedAndCancelledEvents()
      {
          await fixture.ResetAsync();
          var location = await GivenLocationAsync("QRY_CANCELLABLE", "Europe/London");
          await GivenEventAsync(location, new DateOnly(2026, 9, 20), new TimeOnly(9, 30));
          var cancelled = await GivenEventAsync(
              location, new DateOnly(2026, 10, 14), new TimeOnly(9, 30));
          var live = await GivenEventAsync(
              location, new DateOnly(2026, 10, 15), new TimeOnly(9, 30));
          await CancelAsync(cancelled);

          var rows = await Query().ListAsync(
              Filter(), EveryType, Now, notStartedOnly: true, CancellationToken.None);

          Assert.Equal(live, Assert.Single(rows).EventId);
      }

      private static ListEventsQuery Filter() =>
          new(Guid.NewGuid(), null, null, null, null, null, 50);

      private EventReadQueries Query() => new(NewContext());

      private EventBookingDbContext NewContext() =>
          new(new DbContextOptionsBuilder<EventBookingDbContext>()
              .UseNpgsql(fixture.ConnectionString)
              .Options);

      private async Task<Guid> GivenLocationAsync(string code, string timeZoneId)
      {
          await using var context = NewContext();
          var location = Location.Create(
              Guid.NewGuid(), code, code, "1 Test Street", timeZoneId, ProposalFixture.Zones);
          context.Locations.Add(location);
          await context.SaveChangesAsync();
          return location.Id;
      }

      private async Task<Guid> GivenEventAsync(
          Guid locationId, DateOnly date, TimeOnly startTime, IReadOnlyList<Guid>? listed = null)
      {
          var types = listed ??
          [
              AppointmentTypeIds.DrugAndAlcoholTesting,
              AppointmentTypeIds.MedicalCheckUp,
              AppointmentTypeIds.UniformFitting,
          ];

          await using var context = NewContext();
          var proposal = ProposalFixture.Create(
              Guid.NewGuid(), new EventWindow(date, startTime, 240), Guid.NewGuid());
          foreach (var type in types)
          {
              proposal.Accept(type, Guid.NewGuid(), 10);
          }

          var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
          context.EventProposals.Add(proposal);
          context.Events.Add(eventItem);
          await context.SaveChangesAsync();

          // The fixture builds every proposal at its own transitional site, so the location is
          // set here rather than in the fixture: these cases are about filtering and ordering
          // across sites, which is exactly what the fixture's single site cannot show.
          await context.Database.ExecuteSqlRawAsync(
              "UPDATE event SET location_id = {0}, start_utc = NULL WHERE id = {1}",
              locationId, eventItem.Id);
          await context.Database.ExecuteSqlRawAsync(
              "UPDATE event_proposal SET location_id = {0} WHERE id = {1}",
              locationId, proposal.Id);

          // start_utc is derived from the location's zone, so it is recomputed here through the
          // same save-time backstop Task 11 installed rather than being written by hand.
          await using var restamp = NewContext();
          var reread = await restamp.Events.SingleAsync(e => e.Id == eventItem.Id);
          restamp.Entry(reread).State = EntityState.Modified;
          await restamp.SaveChangesAsync();
          return eventItem.Id;
      }

      private async Task CancelAsync(Guid eventId)
      {
          await using var context = NewContext();
          await context.Database.ExecuteSqlRawAsync(
              "UPDATE event SET status = {0} WHERE id = {1}", (int)EventStatus.Cancelled, eventId);
      }
  }
  ```

  The two raw statements are the only honest way to seed across sites: the shared proposal
  fixture belongs to one transitional `Location` by construction, and rebuilding it here would
  duplicate the very thing every other suite shares. The re-save afterwards is what recomputes
  `start_utc`, so the ordering cases are reading a derived instant rather than one the test
  invented — which is also what makes the London-and-Tokyo case meaningful.

- [ ] **Step 2: Run.** Expected: FAIL.

  The Application suite fails to compile against handlers and records that do not exist; the
  activation suite fails because the update command has no activation flag and the list handler takes
  no query; and the Infrastructure suite fails on the missing query type.

  ```bash
  dotnet test tests/EventBooking.Application.Tests --filter "FullyQualifiedName~Events|FullyQualifiedName~ReferenceDataActivation"
  dotnet test tests/EventBooking.Infrastructure.Tests --filter "FullyQualifiedName~EventReadQuery"
  ```

- [ ] **Step 3: Implement.** The cursor codec first, then the `Event` read side, then the
  reference-data and capacity changes.

  ```csharp
  // src/EventBooking.Application/ReadModels/KeysetCursor.cs (complete)
  using System.Buffers.Text;
  using System.Text;

  namespace EventBooking.Application.ReadModels;

  /// <summary>
  /// The keyset payload every paged read shares: the sort key and the row identifier, base64url
  /// encoded and joined by a dot. Task 20a defined this contract for the attendee list; the
  /// event and proposal lists need the same thing, so it lives here and the attendee codec
  /// forwards to it. It is not a security boundary — Task 21's PageCursor signs it at the API
  /// edge, which is where a tampered cursor is refused.
  /// </summary>
  public static class KeysetCursor
  {
      /// <summary>Encodes one row's position.</summary>
      /// <param name="sortKey">The sort key, already formatted for ordinal comparison.</param>
      /// <param name="id">The row identifier that breaks ties on the sort key.</param>
      /// <returns>The opaque payload.</returns>
      public static string Encode(string sortKey, Guid id)
      {
          ArgumentNullException.ThrowIfNull(sortKey);
          return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(sortKey)) + "." +
              Base64Url.EncodeToString(id.ToByteArray());
      }

      /// <summary>Decodes one row's position.</summary>
      /// <param name="cursor">The opaque payload.</param>
      /// <param name="sortKey">Receives the sort key.</param>
      /// <param name="id">Receives the row identifier.</param>
      /// <returns>Whether the payload decoded.</returns>
      public static bool TryDecode(string? cursor, out string sortKey, out Guid id)
      {
          sortKey = string.Empty;
          id = Guid.Empty;
          if (string.IsNullOrEmpty(cursor))
          {
              return false;
          }

          var separator = cursor.IndexOf('.', StringComparison.Ordinal);
          if (separator <= 0 || separator == cursor.Length - 1)
          {
              return false;
          }

          try
          {
              sortKey = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(cursor.AsSpan(0, separator)));
              var bytes = Base64Url.DecodeFromChars(cursor.AsSpan(separator + 1));
              if (bytes.Length != 16)
              {
                  return false;
              }

              id = new Guid(bytes);
              return true;
          }
          catch (FormatException)
          {
              sortKey = string.Empty;
              return false;
          }
      }
  }
  ```

  ```csharp
  // src/EventBooking.Application/ReadModels/AttendeeCursor.cs — the whole body becomes a
  // forwarder. Task 20a's callers are untouched; there is now one codec rather than two that
  // can drift.
  namespace EventBooking.Application.ReadModels;

  /// <summary>The attendee list's keyset cursor. One codec, shared with every other paged read.</summary>
  public static class AttendeeCursor
  {
      /// <summary>Encodes one attendee row's position.</summary>
      /// <param name="sortKey">The sort key.</param>
      /// <param name="id">The attendee identifier.</param>
      /// <returns>The opaque payload.</returns>
      public static string Encode(string sortKey, Guid id) => KeysetCursor.Encode(sortKey, id);

      /// <summary>Decodes one attendee row's position.</summary>
      /// <param name="cursor">The opaque payload.</param>
      /// <param name="sortKey">Receives the sort key.</param>
      /// <param name="id">Receives the attendee identifier.</param>
      /// <returns>Whether the payload decoded.</returns>
      public static bool TryDecode(string? cursor, out string sortKey, out Guid id) =>
          KeysetCursor.TryDecode(cursor, out sortKey, out id);
  }
  ```

  ```csharp
  // src/EventBooking.Application/Events/EventReadHandlers.cs (complete)
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Access;
  using EventBooking.Application.Common;

  namespace EventBooking.Application.Events;

  /// <summary>
  /// The scope both Event reads resolve. Design 05 gives GET /api/events two capabilities,
  /// which design 04's one-capability rule forbids a handler to demand; settlement #12 follows
  /// Task 20b's audit precedent and resolves what the caller holds instead. Operations wins over
  /// negotiation when a caller holds both, because seeing every type is the larger view.
  /// </summary>
  public static class EventScopeResolver
  {
      /// <summary>Resolves the caller's Event scope, or refuses.</summary>
      /// <param name="access">The authorizer.</param>
      /// <param name="staffUserId">The calling staff identity.</param>
      /// <param name="ct">The cancellation token.</param>
      /// <returns>The scope, or a forbidden failure.</returns>
      public static async Task<Result<EventScope>> ResolveAsync(
          IStaffAccessAuthorizer access, Guid staffUserId, CancellationToken ct)
      {
          ArgumentNullException.ThrowIfNull(access);

          var operations = await access.AuthorizeAsync(
              staffUserId, StaffCapability.ViewEventOperations, null, ct);
          if (operations.IsSuccess)
          {
              return Result<EventScope>.Success(new EventScope(AllTypes: true, null));
          }

          var negotiation = await access.AuthorizeAsync(
              staffUserId, StaffCapability.ManageEventNegotiation, null, ct);
          if (negotiation.IsSuccess && negotiation.Value.AppointmentTypeId is { } scope)
          {
              return Result<EventScope>.Success(new EventScope(AllTypes: false, scope));
          }

          return Result<EventScope>.Failure(
              Error.Forbidden("Reading events needs an event capability."));
      }
  }

  /// <summary>Lists events, filtered and scoped, one keyset page at a time.</summary>
  /// <param name="access">The authorizer.</param>
  /// <param name="queries">The read side.</param>
  /// <param name="clock">The clock.</param>
  public sealed class ListEventsHandler(
      IStaffAccessAuthorizer access, IEventReadQueries queries, IClock clock)
  {
      /// <summary>Returns one page of events.</summary>
      /// <param name="query">The filters and the page.</param>
      /// <param name="ct">The cancellation token.</param>
      /// <returns>The page.</returns>
      public async Task<Result<EventListView>> HandleAsync(
          ListEventsQuery query, CancellationToken ct)
      {
          ArgumentNullException.ThrowIfNull(query);
          if (query.Limit is < 1 or > 200)
          {
              return Result<EventListView>.Failure(
                  Error.Validation("Limit must be between 1 and 200."));
          }

          var scope = await EventScopeResolver.ResolveAsync(access, query.StaffUserId, ct);
          if (scope.IsFailure)
          {
              return Result<EventListView>.Failure(scope.Error);
          }

          var rows = await queries.ListAsync(
              query with { Limit = query.Limit + 1 }, scope.Value, clock.UtcNow,
              notStartedOnly: false, ct);
          return Result<EventListView>.Success(EventPage.From(rows, query.Limit));
      }
  }

  /// <summary>Lists the events a cancellation can still reach (FR-7.2).</summary>
  /// <param name="access">The authorizer.</param>
  /// <param name="queries">The read side.</param>
  /// <param name="clock">The clock.</param>
  public sealed class ListCancellableEventsHandler(
      IStaffAccessAuthorizer access, IEventReadQueries queries, IClock clock)
  {
      /// <summary>Returns one page of cancellable events.</summary>
      /// <param name="query">The filters and the page.</param>
      /// <param name="ct">The cancellation token.</param>
      /// <returns>The page.</returns>
      public async Task<Result<EventListView>> HandleAsync(
          ListCancellableEventsQuery query, CancellationToken ct)
      {
          ArgumentNullException.ThrowIfNull(query);
          if (query.Limit is < 1 or > 200)
          {
              return Result<EventListView>.Failure(
                  Error.Validation("Limit must be between 1 and 200."));
          }

          // Design 05 gives this route ViewEventOperations alone, but a Manager holding
          // CancelEvent for events listing their type has the same need; the resolver already
          // answers both, and the query narrows a Manager to their own capacity row regardless.
          var scope = await EventScopeResolver.ResolveAsync(access, query.StaffUserId, ct);
          if (scope.IsFailure)
          {
              return Result<EventListView>.Failure(scope.Error);
          }

          var rows = await queries.ListAsync(
              new ListEventsQuery(
                  query.StaffUserId, query.LocationId, query.From, query.To, null,
                  query.Cursor, query.Limit + 1),
              scope.Value, clock.UtcNow, notStartedOnly: true, ct);
          return Result<EventListView>.Success(EventPage.From(rows, query.Limit));
      }
  }

  /// <summary>Reads one event, filtered the same way the list is.</summary>
  /// <param name="access">The authorizer.</param>
  /// <param name="queries">The read side.</param>
  public sealed class GetEventHandler(IStaffAccessAuthorizer access, IEventReadQueries queries)
  {
      /// <summary>Returns one event, or not found.</summary>
      /// <param name="query">The event to read.</param>
      /// <param name="ct">The cancellation token.</param>
      /// <returns>The event.</returns>
      public async Task<Result<EventView>> HandleAsync(GetEventQuery query, CancellationToken ct)
      {
          ArgumentNullException.ThrowIfNull(query);

          var scope = await EventScopeResolver.ResolveAsync(access, query.StaffUserId, ct);
          if (scope.IsFailure)
          {
              return Result<EventView>.Failure(scope.Error);
          }

          var row = await queries.GetAsync(query.EventId, scope.Value, ct);

          // An event outside the caller's scope reads as absent rather than forbidden. A
          // Manager who can tell "not yours" from "no such event" can enumerate other types'
          // scheduling, which is the same reasoning behind the attendee token's one answer.
          return row is null
              ? Result<EventView>.Failure(Error.NotFound("No such event."))
              : Result<EventView>.Success(row);
      }
  }

  /// <summary>Turns an over-read of one extra row into a page and its next cursor.</summary>
  internal static class EventPage
  {
      public static EventListView From(IReadOnlyList<EventView> rows, int limit)
      {
          var kept = rows.Count > limit ? rows.Take(limit).ToList() : rows.ToList();
          var next = rows.Count > limit ? kept[^1].Cursor : null;
          return new EventListView(kept, next);
      }
  }
  ```

  The over-read of one row is what distinguishes a full last page from a full page with more
  behind it. A handler that asked for exactly the page size would have to guess, and the guess
  that a full page always has a successor produces one empty request at the end of every walk.

  ```csharp
  // src/EventBooking.Application/Negotiation/ListEventProposalsHandler.cs (complete)
  using EventBooking.Application.Access;
  using EventBooking.Application.Common;
  using EventBooking.Domain.Events;

  namespace EventBooking.Application.Negotiation;

  /// <summary>Lists the caller's type's proposals (FR-2.13), filtered and keyset-paged.</summary>
  /// <param name="access">The authorizer.</param>
  /// <param name="queries">The read side.</param>
  public sealed class ListEventProposalsHandler(
      IStaffAccessAuthorizer access, IEventProposalListQueries queries)
  {
      /// <summary>Returns one page of proposals.</summary>
      /// <param name="query">The filters and the page.</param>
      /// <param name="ct">The cancellation token.</param>
      /// <returns>The page.</returns>
      public async Task<Result<EventProposalListView>> HandleAsync(
          ListEventProposalsQuery query, CancellationToken ct)
      {
          ArgumentNullException.ThrowIfNull(query);
          if (query.Limit is < 1 or > 200)
          {
              return Result<EventProposalListView>.Failure(
                  Error.Validation("Limit must be between 1 and 200."));
          }

          // A misspelt status is a field error, not an empty page: a caller who asks for "open"
          // and is shown nothing will conclude there are no proposals.
          EventProposalStatus? status = null;
          if (query.Status is not null)
          {
              if (!Enum.TryParse<EventProposalStatus>(query.Status, ignoreCase: false, out var parsed)
                  || !Enum.IsDefined(parsed))
              {
                  return Result<EventProposalListView>.Failure(
                      Error.Validation($"'{query.Status}' is not a proposal status."));
              }

              status = parsed;
          }

          var authorized = await access.AuthorizeAsync(
              query.StaffUserId, StaffCapability.ManageEventNegotiation, null, ct);
          if (authorized.IsFailure)
          {
              return Result<EventProposalListView>.Failure(authorized.Error);
          }

          var actingType = authorized.Value.AppointmentTypeId!.Value;
          var rows = await queries.ListAsync(
              actingType, query.StaffUserId, status, query.LocationId, query.Cursor,
              query.Limit + 1, ct);

          var kept = rows.Count > query.Limit ? rows.Take(query.Limit).ToList() : rows.ToList();
          return Result<EventProposalListView>.Success(new EventProposalListView(
              kept, rows.Count > query.Limit ? kept[^1].Cursor : null));
      }
  }

  /// <summary>
  /// The proposal read side. The acting type is a parameter rather than a filter the caller can
  /// set: FR-2.13 is the caller's own type's board, and a Manager must not be able to page
  /// another type's negotiation by asking.
  /// </summary>
  public interface IEventProposalListQueries
  {
      /// <summary>Returns one keyset page of the acting type's proposals.</summary>
      /// <param name="actingAppointmentTypeId">The caller's own type.</param>
      /// <param name="staffUserId">The caller, for the accepted-by-me and created-by-me flags.</param>
      /// <param name="status">The status filter, or null for every status.</param>
      /// <param name="locationId">The location filter, or null for every site.</param>
      /// <param name="cursor">The keyset cursor, or null for the first page.</param>
      /// <param name="limit">The page size, over-read by one.</param>
      /// <param name="ct">The cancellation token.</param>
      /// <returns>The rows.</returns>
      Task<IReadOnlyList<EventProposalListItem>> ListAsync(
          Guid actingAppointmentTypeId, Guid staffUserId, EventProposalStatus? status,
          Guid? locationId, string? cursor, int limit, CancellationToken ct);
  }
  ```

  `AppointmentTypeId!.Value` is safe here and nowhere else: `ManageEventNegotiation` is a
  Manager-only, scoped capability, and the Task 17 null-scope gate refuses a scoped role with a
  null scope before the authorizer returns success. Task 13's handlers already rely on the same
  guarantee.

  **The reference-data changes.** All three files take the same three edits; the location file is
  written out in full and the other two follow it exactly, substituting their own aggregate,
  usage type and audit action.

  ```csharp
  // src/EventBooking.Application/ReferenceData/LocationHandlers.cs — the update handler's body
  // and the list handler, replacing Task 12's versions. SetLocationActiveCommand and
  // SetLocationActiveHandler are deleted from this file; nothing else calls them.
  public sealed class UpdateLocationHandler(
      ILocationRepository locations,
      IStaffAccessProfileRepository profiles,
      IUnitOfWork unitOfWork,
      IAuditLogger audit,
      IEventWindowZones zones,
      IReferenceDataBlockingQueries blocking)
  {
      /// <summary>Applies every field design 05's PUT carries, in one save.</summary>
      /// <param name="command">The update.</param>
      /// <param name="ct">The cancellation token.</param>
      /// <returns>The updated row.</returns>
      public async Task<Result<LocationResult>> HandleAsync(
          UpdateLocationCommand command, CancellationToken ct)
      {
          ArgumentNullException.ThrowIfNull(command);

          var authorized = await new StaffAccessAuthorizer(profiles).AuthorizeAsync(
              command.StaffUserId, StaffCapability.ManageReferenceData, null, ct);
          if (authorized.IsFailure)
          {
              return Result<LocationResult>.Failure(authorized.Error);
          }

          var location = await locations.GetAsync(command.LocationId, ct);
          if (location is null)
          {
              return Result<LocationResult>.Failure(Error.NotFound("No such location."));
          }

          if (location.Version != command.ExpectedVersion)
          {
              return Result<LocationResult>.Failure(Error.VersionConflict(
                  "This location has changed since you read it.", location.Version));
          }

          var wasActive = location.IsActive;
          var previousZone = location.TimeZoneId;
          try
          {
              location.Rename(command.Name);
              location.ChangeAddress(command.Address);
              if (!string.Equals(previousZone, command.TimeZoneId, StringComparison.Ordinal))
              {
                  location.ChangeTimeZone(
                      command.TimeZoneId, zones, await blocking.LocationUsageAsync(location.Id, ct));
              }

              // Activation is only asked about when it changes. A rename of a site with live
              // scheduling is always allowed, and asking the blocking query on every update
              // would turn one into a refusal.
              if (command.IsActive != wasActive)
              {
                  if (command.IsActive)
                  {
                      location.Reactivate();
                  }
                  else
                  {
                      location.Deactivate(await blocking.LocationUsageAsync(location.Id, ct));
                  }
              }
          }
          catch (ReferenceDataInUseException ex)
          {
              return Result<LocationResult>.Failure(
                  Error.ReferenceDataInUse(ex.Message, ex.Blocking));
          }
          catch (DomainException ex)
          {
              return Result<LocationResult>.Failure(Error.Validation(ex.Message));
          }

          audit.Record(
              AuditEntityTypes.Location, location.Id, AuditAction.LocationUpdated,
              ActorType.Staff, command.StaffUserId.ToString(),
              $"zone {previousZone} -> {location.TimeZoneId}; isActive {wasActive} -> {location.IsActive}");
          await unitOfWork.SaveChangesAsync(ct);

          return Result<LocationResult>.Success(new LocationResult(
              location.Id, location.Code, location.Name, location.Address, location.TimeZoneId,
              location.IsActive, location.Version));
      }
  }

  public sealed class ListLocationsHandler(ILocationRepository locations)
  {
      /// <summary>Lists locations in code order, hiding inactive rows unless asked.</summary>
      /// <param name="query">Whether to include inactive rows.</param>
      /// <param name="ct">The cancellation token.</param>
      /// <returns>The rows.</returns>
      public async Task<Result<IReadOnlyList<LocationListItem>>> HandleAsync(
          ListLocationsQuery query, CancellationToken ct)
      {
          ArgumentNullException.ThrowIfNull(query);
          var rows = await locations.ListAsync(ct);
          return Result<IReadOnlyList<LocationListItem>>.Success(
              rows.Where(l => query.IncludeInactive || l.IsActive)
                  .OrderBy(l => l.Code, StringComparer.Ordinal)
                  .Select(l => new LocationListItem(l.Id, l.Code, l.Name, l.IsActive))
                  .ToList());
      }
  }
  ```

  The refusal comes out of the aggregate, not out of a check before it: the domain already
  refuses a deactivation with live usage, and catching the exception is how Task 12's set-active
  handler rendered the same failure. Nothing is saved when it throws, because the save is after
  the whole block — which is what makes the refusal cover the rename too.

  The other two files take the same shape. Only their activation branch and their audit line
  differ, because each aggregate asks a different blocking question.

  ```csharp
  // src/EventBooking.Application/ReferenceData/AppointmentTypeHandlers.cs — the activation
  // branch and the audit line inside UpdateAppointmentTypeHandler.HandleAsync. The
  // authorization, the load, the version check, the rename and the save are the location
  // handler's, unchanged. SetAppointmentTypeActiveCommand and its handler are deleted.
  if (command.IsActive != wasActive)
  {
      if (command.IsActive)
      {
          type.Reactivate();
      }
      else
      {
          type.Deactivate(await blocking.AppointmentTypeUsageAsync(type.Id, ct));
      }
  }

  audit.Record(
      AuditEntityTypes.AppointmentType, type.Id, AuditAction.AppointmentTypeUpdated,
      ActorType.Staff, command.StaffUserId.ToString(),
      $"isActive {wasActive} -> {type.IsActive}");
  ```

  ```csharp
  // src/EventBooking.Application/ReferenceData/AttendeeGroupHandlers.cs — the same branch in
  // UpdateAttendeeGroupHandler.HandleAsync, placed AFTER Task 12's requirement replacement so
  // that a change refused as requirements-locked refuses the whole command, and asking a
  // member count on a group whose membership is about to change reads the settled number.
  // SetAttendeeGroupActiveCommand and its handler are deleted.
  if (command.AppointmentTypeIds is not null)
  {
      // Preserve Task 12's replacement sequence. It is deliberately inline: the domain method
      // supplies the refusal and the handler re-derives members only after a real replacement.
      var activeIds = (await types.ListAsync(ct)).Where(t => t.IsActive).Select(t => t.Id).ToList();
      var blockingMembers = await blocking.AttendeeGroupBlockingMemberCountAsync(group.Id, ct);
      if (group.ReplaceRequirements(command.AppointmentTypeIds, activeIds, blockingMembers))
      {
          var rederived = await RederiveMembersAsync(group, ct);
          changes.Add($"requirements; {rederived} members re-derived");
      }
  }

  if (command.IsActive != wasActive)
  {
      if (command.IsActive)
      {
          group.Reactivate();
      }
      else
      {
          group.Deactivate(await blocking.AttendeeGroupMemberCountAsync(group.Id, ct));
      }
  }

  audit.Record(
      AuditEntityTypes.AttendeeGroup, group.Id, AuditAction.AttendeeGroupUpdated,
      ActorType.Staff, command.StaffUserId.ToString(),
      $"isActive {wasActive} -> {group.IsActive}");
  ```

  The group's ordering is the one that matters. A group cannot be deactivated while it has
  members, and its requirement replacement can itself be refused; putting the activation branch
  second means a caller who sends both in one PUT gets the requirement refusal, which is the more
  specific of the two.

  ```csharp
  // src/EventBooking.Application/Negotiation/AdjustEventCapacityHandler.cs — the head of
  // HandleAsync, after the authorization and before Task 13's existing body. The route names a
  // type; a Manager naming somebody else's is refused rather than silently adjusting their own.
  var actingType = authorized.Value.AppointmentTypeId!.Value;
  if (command.AppointmentTypeId != actingType)
  {
      return Result<AdjustEventCapacityOutcome>.Failure(Error.Forbidden(
          "A Manager may only adjust their own appointment type's capacity."));
  }
  ```

  ```csharp
  // src/EventBooking.Application/Attendees/GetAttendeeReadinessHandler.cs — one capability
  // changes (settlement #13). Everything else in the handler is untouched.
  var authorized = await access.AuthorizeAsync(
      query.StaffUserId, StaffCapability.ViewAttendeeDashboards, null, cancellationToken);
  ```

  **The read side.**

  ```csharp
  // src/EventBooking.Infrastructure/Persistence/Queries/EventReadQueries.cs (complete)
  using EventBooking.Application.Events;
  using EventBooking.Application.Negotiation;
  using EventBooking.Application.ReadModels;
  using EventBooking.Domain.Bookings;
  using EventBooking.Domain.Events;
  using Microsoft.EntityFrameworkCore;

  namespace EventBooking.Infrastructure.Persistence.Queries;

  /// <summary>
  /// The Event read side. The scope is applied here and not only in the handler, so a caller
  /// that reached this query without a capability check still sees only their own type's
  /// capacity — the shape Task 20b settled for the audit buckets.
  /// </summary>
  /// <param name="context">The database context.</param>
  public sealed class EventReadQueries(EventBookingDbContext context) : IEventReadQueries
  {
      /// <summary>The sort key format: ordinal comparison must match instant comparison.</summary>
      private const string SortKeyFormat = "yyyy-MM-ddTHH:mm:ss.fffffffZ";

      /// <inheritdoc />
      public async Task<IReadOnlyList<EventView>> ListAsync(
          ListEventsQuery query, EventScope scope, DateTimeOffset now, bool notStartedOnly,
          CancellationToken ct)
      {
          ArgumentNullException.ThrowIfNull(query);
          ArgumentNullException.ThrowIfNull(scope);

          var events = Filtered(scope)
              .Where(e => query.LocationId == null || e.LocationId == query.LocationId)
              .Where(e => query.From == null || e.Window.Date >= query.From)
              .Where(e => query.To == null || e.Window.Date <= query.To)
              .Where(e => query.AppointmentTypeId == null ||
                  context.EventCapacities.Any(c =>
                      c.EventId == e.Id && c.AppointmentTypeId == query.AppointmentTypeId));

          if (notStartedOnly)
          {
              events = events.Where(e => e.Status == EventStatus.Active &&
                  EF.Property<DateTimeOffset>(e, "StartUtc") > now);
          }

          if (KeysetCursor.TryDecode(query.Cursor, out var sortKey, out var lastId) &&
              DateTimeOffset.TryParse(
                  sortKey, null, System.Globalization.DateTimeStyles.AdjustToUniversal,
                  out var lastStart))
          {
              // The identifier is part of the key, not decoration: several events can share one
              // derived instant, and a keyset on the instant alone would drop or repeat the
              // rows at that boundary.
              events = events.Where(e =>
                  EF.Property<DateTimeOffset>(e, "StartUtc") > lastStart ||
                  (EF.Property<DateTimeOffset>(e, "StartUtc") == lastStart && e.Id.CompareTo(lastId) > 0));
          }

          var page = await events
              .OrderBy(e => EF.Property<DateTimeOffset>(e, "StartUtc"))
              .ThenBy(e => e.Id)
              .Take(query.Limit)
              .Select(e => new
              {
                  Event = e,
                  ActiveBookings = context.Bookings.Count(b =>
                      b.EventId == e.Id && b.Status == BookingStatus.Active),
              })
              .ToListAsync(ct);

          return await ProjectAsync([.. page.Select(x => x.Event.Id)],
              page.ToDictionary(x => x.Event.Id, x => x.ActiveBookings), scope, ct);
      }

      /// <inheritdoc />
      public async Task<EventView?> GetAsync(Guid eventId, EventScope scope, CancellationToken ct)
      {
          ArgumentNullException.ThrowIfNull(scope);

          var found = await Filtered(scope)
              .Where(e => e.Id == eventId)
              .Select(e => new
              {
                  ActiveBookings = context.Bookings.Count(b =>
                      b.EventId == e.Id && b.Status == BookingStatus.Active),
              })
              .SingleOrDefaultAsync(ct);

          if (found is null)
          {
              return null;
          }

          var rows = await ProjectAsync(
              [eventId], new Dictionary<Guid, int> { [eventId] = found.ActiveBookings }, scope, ct);
          return rows.SingleOrDefault();
      }

      /// <summary>
      /// An event the caller may see at all. A scoped caller sees only events listing their own
      /// type; an all-types caller sees every event.
      /// </summary>
      private IQueryable<Event> Filtered(EventScope scope) =>
          context.Events.AsNoTracking().Where(e =>
              scope.AllTypes ||
              context.EventCapacities.Any(c =>
                  c.EventId == e.Id && c.AppointmentTypeId == scope.AppointmentTypeId));

      /// <summary>
      /// Joins the location and the capacity rows for a page of identifiers. Two round trips
      /// rather than one projection, because a capacity join inside the keyset query would
      /// multiply the rows the limit is counting.
      /// </summary>
      private async Task<IReadOnlyList<EventView>> ProjectAsync(
          IReadOnlyList<Guid> eventIds, IReadOnlyDictionary<Guid, int> activeBookings,
          EventScope scope, CancellationToken ct)
      {
          if (eventIds.Count == 0)
          {
              return [];
          }

          var rows = await (
              from e in context.Events.AsNoTracking()
              join l in context.Locations.AsNoTracking() on e.LocationId equals l.Id
              where eventIds.Contains(e.Id)
              orderby EF.Property<DateTimeOffset>(e, "StartUtc"), e.Id
              select new
              {
                  e.Id,
                  e.ProposalId,
                  e.LocationId,
                  LocationCode = l.Code,
                  LocationName = l.Name,
                  l.TimeZoneId,
                  e.Window.Date,
                  e.Window.StartTime,
                  e.Window.DurationMinutes,
                  e.Status,
                  StartUtc = EF.Property<DateTimeOffset>(e, "StartUtc"),
              }).ToListAsync(ct);

          var capacities = await (
              from c in context.EventCapacities.AsNoTracking()
              join t in context.AppointmentTypes.AsNoTracking()
                  on c.AppointmentTypeId equals t.Id
              where eventIds.Contains(c.EventId) &&
                  (scope.AllTypes || c.AppointmentTypeId == scope.AppointmentTypeId)
              orderby t.Code
              select new
              {
                  c.EventId,
                  c.AppointmentTypeId,
                  t.Code,
                  t.Name,
                  c.TotalHeadcount,
                  c.RemainingCapacity,
              }).ToListAsync(ct);

          var byEvent = capacities
              .GroupBy(c => c.EventId)
              .ToDictionary(
                  g => g.Key,
                  g => (IReadOnlyList<EventCapacityView>)[.. g.Select(c => new EventCapacityView(
                      c.AppointmentTypeId, c.Code, c.Name, c.TotalHeadcount, c.RemainingCapacity))]);

          return [.. rows.Select(r => new EventView(
              r.Id, r.ProposalId, r.LocationId, r.LocationCode, r.LocationName, r.TimeZoneId,
              r.Date, r.StartTime, r.DurationMinutes, r.Status.ToString(),
              byEvent.TryGetValue(r.Id, out var found) ? found : [],
              activeBookings.TryGetValue(r.Id, out var count) ? count : 0,
              KeysetCursor.Encode(
                  r.StartUtc!.Value.ToUniversalTime().ToString(SortKeyFormat,
                      System.Globalization.CultureInfo.InvariantCulture),
                  r.Id)))];
      }
  }
  ```

  The sort key is the derived start instant rendered to a fixed-width UTC string, so ordinal
  string comparison and instant comparison agree — which is what lets the cursor stay opaque
  text. `start_utc` is not null in the database from Task 11 onward even though its CLR type is
  nullable, which is why the projection dereferences it; a row that reached here unstamped is a
  Task 11 regression, not a case to handle.

  ```csharp
  // src/EventBooking.Infrastructure/Persistence/Queries/EventReadQueries.cs — the proposal
  // list, in the same file because it reads the same three tables.
  public sealed class EventProposalListQueries(EventBookingDbContext context)
      : IEventProposalListQueries
  {
      /// <inheritdoc />
      public async Task<IReadOnlyList<EventProposalListItem>> ListAsync(
          Guid actingAppointmentTypeId, Guid staffUserId, EventProposalStatus? status,
          Guid? locationId, string? cursor, int limit, CancellationToken ct)
      {
          var listed = context.EventProposals.AsNoTracking().Where(p =>
              p.ListedTypes.Any(t => t.AppointmentTypeId == actingAppointmentTypeId));

          if (status is not null)
          {
              listed = listed.Where(p => p.Status == status);
          }

          if (locationId is not null)
          {
              listed = listed.Where(p => p.LocationId == locationId);
          }

          if (KeysetCursor.TryDecode(cursor, out var sortKey, out var lastId) &&
              DateOnly.TryParse(sortKey[..10], out var lastDate))
          {
              listed = listed.Where(p =>
                  p.Window.Date > lastDate ||
                  (p.Window.Date == lastDate && p.Id.CompareTo(lastId) > 0));
          }

          var page = await (
              from p in listed
              join l in context.Locations.AsNoTracking() on p.LocationId equals l.Id
              orderby p.Window.Date, p.Id
              select new
              {
                  p.Id,
                  p.LocationId,
                  LocationCode = l.Code,
                  LocationName = l.Name,
                  l.TimeZoneId,
                  p.Window.Date,
                  p.Window.StartTime,
                  p.Window.DurationMinutes,
                  p.Status,
                  p.CreatedByManagerUserId,
                  ListedTypeCount = p.ListedTypes.Count,
                  AcceptedTypeCount = p.Acceptances.Count,
                  MyAcceptedHeadcount = p.Acceptances
                      .Where(a => a.AppointmentTypeId == actingAppointmentTypeId)
                      .Select(a => (int?)a.Headcount)
                      .FirstOrDefault(),
                  ProposerType = p.ProposerAppointmentTypeId,
              })
              .Take(limit)
              .ToListAsync(ct);

          // A proposal's window is ordered by its local date, not a derived instant: proposals
          // carry no start_utc, and FR-2.13's board is a Manager's own calendar rather than a
          // cross-site sequence.
          return [.. page.Select(p => new EventProposalListItem(
              p.Id, p.LocationId, p.LocationCode, p.LocationName, p.TimeZoneId,
              p.Date, p.StartTime, p.DurationMinutes, p.Status.ToString(),
              p.ListedTypeCount, p.AcceptedTypeCount, p.MyAcceptedHeadcount,
              p.MyAcceptedHeadcount is not null,
              p.ProposerType == actingAppointmentTypeId && p.CreatedByManagerUserId == staffUserId,
              KeysetCursor.Encode(p.Date.ToString("yyyy-MM-dd"), p.Id)))];
      }
  }
  ```

  The created-by-me flag is judged on the proposing type as well as the identity, because settlement #1
  makes the type the owner of a proposal: a Manager who inherited the scope from a predecessor
  sees the proposal as their type's, and the flag says whether they personally raised it.

  ```csharp
  // src/EventBooking.Infrastructure/DependencyInjection.cs — four registrations beside the
  // existing query ports and handlers.
  services.AddScoped<IEventReadQueries, Persistence.Queries.EventReadQueries>();
  services.AddScoped<IEventProposalListQueries, Persistence.Queries.EventProposalListQueries>();
  services.AddScoped<ListEventsHandler>();
  services.AddScoped<ListCancellableEventsHandler>();
  services.AddScoped<GetEventHandler>();
  services.AddScoped<ListEventProposalsHandler>();
  ```

- [ ] **Step 4: Run.** Expected: PASS — the three new suites plus the full solution.

  ```bash
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  ```

  No migration is needed. The keyset reads `start_utc` and `id`, which Task 11 already indexes as
  `ix_event_eligibility`, and the proposal list reads columns Task 9b's fresh schema created.

  **No figure here is observed.** This task is hand-authored and nothing in it has been built or
  run. Expect the Application and Infrastructure counts to rise by roughly twenty-five cases
  between them, and three of Task 12's existing cases to change shape rather than disappear as the
  set-active handlers go. The last measured checkpoint remains Task 11 at 1570.

- [ ] **Step 5: Commit and push** the executor's code — not the plan documents — under the
  master plan's message:

  ```bash
  test -z "$(git status --porcelain --ignored=no | grep -v '^??')"
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  git add src/EventBooking.Application/ src/EventBooking.Infrastructure/ tests/EventBooking.Application.Tests/ tests/EventBooking.Infrastructure.Tests/
  git diff --cached --name-only
  git diff --cached
  test -n "$EXECUTOR_COAUTHOR"
  git commit -m "feat(api): full EventBooking endpoint catalogue

  Co-authored-by: $EXECUTOR_COAUTHOR"
  git push
  ```

  The message is the master plan's, and Task 22b carries the same one. Two commits with one
  subject is what the Task 20a/20b split already does: the master plan's commit messages never
  change, and a lettered split is two commits of one task.
