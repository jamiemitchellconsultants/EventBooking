# 03d — Booking and cancellation over N capacity rows (Task 15)

[← Phase overview](phase-3-application.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This task follows Task 14. The ported confirm-booking, manage-cancel, coordinator-cancel-booking
and cancel-event handlers move under `src/EventBooking.Application/Bookings/` and `Events/`,
charge and release exactly the required types through the Task 7 domain methods, and adopt the
Task 10 ordered-lock helpers — extending the lock ladder with the `Invite` and `Booking`
levels the design names.

> Use superpowers:executing-plans. This task is hand-authored: complete code and complete tests
> are written straight into this document, with no prototype. Compile and test-drive them
> yourself. The test counts below are what you should expect to reach, not figures observed by
> the author — nothing here has been run.

**Goal:** ConfirmBooking (book token, event id) creating the booking, one `BookingAppointment`
per required type, the invite `Used` and the attendee `Booked`, auditing `BookingCreated` and
one `CapacityDecremented` per charged row, staging `BookingConfirmation`; CancelBookingByAttendee
(manage token, request-new-time) with the four truthful outcomes; CancelBookingByCoordinator
(attendee id, booking id, confirm) as a two-step with `confirmation-required` and the
active-booking count; CancelEvent (event id, confirm) reissuing from each original location
set and reporting counts. Every cancel after the start instant returns `window-started`, judged
in the event's location zone.

**Architecture:** One transaction per command; locks in the canonical order through the Task 10
helpers, now extended with `Invite` and `Booking` levels between attendee and event. Capacity
is charged through ChargeRequiredTypes (all-or-nothing across required rows) and released
through ReleaseTypes — both finally called here, on the same tracked rows the capacity
repository locked. Capacity is re-checked under lock at booking time, so a stale option can
never overbook; exhaustion on any required type changes nothing and returns
`capacity-exhausted`. CancelEvent reads the affected attendee identifiers without locks, then
takes each booking's locks in canonical order and re-validates under lock (section 8,
decision 2). The concurrency harness switches to the real ConfirmBooking handler. No domain
entity leaves the handler.

**Tech Stack:** .NET 10, xUnit, EF Core, PostgreSQL Testcontainers.

**Spec:** [Master Task 15](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[domain model](../design/01-domain-model.md), [functional requirements](../design/02-functional-requirements.md),
[solution architecture](../design/04-solution-architecture.md), [ontology](../ontology.md).

## Boundary

Task 15 owns booking and all three cancellation paths. It inherits three debts: the booking
handler adopts the lock helpers, the lock ladder gains its `Invite` and `Booking` levels, and
Task 7's charge and release methods are finally called. Attendee-token routes keep their
anonymous pipeline (no StaffCapability); coordinator cancellation demands `ManageAttendees`
and event cancellation demands `CancelEvent`, scoped so a Manager acts only for events listing
their type. The ported view handlers (ViewBookingHandler, ViewInviteHandler) stay where
they are for Task 20's attendee pages.

## Replayed confirmations (contradiction #2, settled with the user)

A replayed confirmation of an already-used invite is refused as a conflict naming the existing
booking — it does not create a second booking and does not return the booking as a success.
This settles contradiction #2 against the master plan's "second confirm returns the same
booking" test: the `already-confirmed` refusal carries the existing booking id, and the
document's test asserts exactly that.

## The replacement flag

The master plan stages `EventCancelledRebookingNeeded` "with the replacement created flag in
its context". The outbox row carries context identifiers only (invite, booking, event) —
there is no flag column, and Task 18's composer derives the ending at send time from whether
a replacement invite exists. What Task 15 persists is the audit detail on each cancelled
booking (`replacement created` vs `no replacement`), alongside the staged row's booking and
event ids. The composer test in Task 18 proves both endings.

## Global constraints

One transaction per command; canonical lock order including the two new levels; audit in the
transaction; no domain entity leaves the handler. Attendee cancel with a new time while a
recovery invite is pending supersedes it (FR-9.6). CancelEvent without confirm returns the
booking count only; with confirm it cancels every active booking in attendee-id order,
reissues from each original location set through the Task 14 issuer, and reports cancelled,
reinvited and awaiting-availability counts.

## Review focus

STOP AND CHECK four things. Confirming for an attendee needing one type on an event listing
three decrements only that type and creates one appointment row. The replay test asserts a
conflict naming the existing booking id — not a returned booking, not a second row. The
CancelEvent test asserts attendee-id ordering of the cancellations and the replacement flag
in every audit detail. And the harness re-runs Task 10's scenarios against the real handler
plus CancelEvent racing bookings on the same event, with no deadlock.

### Task 15: Booking and cancellation over N capacity rows

**Files:**

- Modify: src/EventBooking.Application/Common/Error.cs (already-confirmed,
  capacity-exhausted, confirmation-required, window-started factories)
- Modify: src/EventBooking.Application/Abstractions/IBookingRepository.cs
  (GetByInviteIdAsync, CountActiveForAttendeeAsync)
- Create: src/EventBooking.Application/Bookings/ConfirmBookingHandler.cs
- Create: src/EventBooking.Application/Bookings/CancelBookingByAttendeeHandler.cs
- Create: src/EventBooking.Application/Bookings/CancelBookingByCoordinatorHandler.cs
- Create: src/EventBooking.Application/Events/CancelEventHandler.cs (new file; the ported
  file below is deleted first, so there is exactly one CancelEventHandler.cs at the end)
- Delete: src/EventBooking.Application/Bookings/ConfirmBookingHandler.cs (ported version;
  deleted before the new file of the same name is created)
- Delete: src/EventBooking.Application/Bookings/CancelAttendeeBookingHandler.cs
- Delete: src/EventBooking.Application/Bookings/CancelBookingHandler.cs
- Delete: src/EventBooking.Application/Events/CancelEventHandler.cs (ported version; deleted
  before the new file of the same name is created)
- Modify: src/EventBooking.Infrastructure/Persistence/Locking/ (Invite and Booking levels,
  adopted by the booking, invite and attendee repository lock methods)
- Modify: src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs (booking
  by-invite lookup, active-booking count)
- Modify: tests/EventBooking.Application.Tests/Fakes/InMemoryRepositories.cs (same two
  booking methods over the in-memory items)
- Modify: tests/EventBooking.Infrastructure.Tests/Concurrency/ (drive the real handler)
- Test: tests/EventBooking.Application.Tests/Bookings/BookingFixture.cs
- Test: tests/EventBooking.Application.Tests/Bookings/ConfirmBookingHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Bookings/CancelBookingHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs

**Interfaces:**

```csharp
namespace EventBooking.Application.Bookings;

public sealed record ConfirmBookingCommand(string BookToken, Guid EventId);
public sealed record ConfirmBookingOutcome(Guid BookingId, string ManageToken);

public sealed record CancelBookingByAttendeeCommand(string ManageToken, bool RequestNewTime);
public sealed record AttendeeCancelOutcome(string Outcome, Guid? InviteId);

public sealed record CancelBookingByCoordinatorCommand(
    Guid StaffUserId, Guid AttendeeId, Guid BookingId, bool Confirm);
public sealed record CoordinatorCancelOutcome(
    bool ConfirmationRequired, int ActiveBookingCount, Guid? CancelledBookingId);
```

```csharp
namespace EventBooking.Application.Events;

public sealed record CancelEventCommand(Guid StaffUserId, Guid EventId, bool Confirm);
public sealed record CancelEventOutcome(
    int CancelledCount, int ReinvitedCount, int AwaitingAvailabilityCount);
```

```csharp
// Error.cs: add beside the Task 12–14 factories.
public const string AlreadyConfirmedCode = "already-confirmed";
public static Error AlreadyConfirmed(string message, Guid existingBookingId) =>
    new(AlreadyConfirmedCode, message, new Dictionary<string, long>());
```

The booking id is a Guid, not a count, so it cannot travel in the numeric data map:
AlreadyConfirmed carries it in the message (`$"This invite already confirmed booking {id}."`)
and the outcome record below carries it typed. The test asserts both.

```csharp
public const string CapacityExhaustedCode = "capacity-exhausted";
public static Error CapacityExhausted(string message) => new(CapacityExhaustedCode, message);

public const string ConfirmationRequiredCode = "confirmation-required";
public static Error ConfirmationRequired(string message) => new(ConfirmationRequiredCode, message);

public const string WindowStartedCode = "window-started";
public static Error WindowStarted(string message) => new(WindowStartedCode, message);
```

```csharp
// IBookingRepository.cs: add
// Finds the booking created from one invite, for the replay refusal. Null when the
// invite was never confirmed.
Task<Booking?> GetByInviteIdAsync(Guid inviteId, CancellationToken cancellationToken);
// Counts the attendee's active bookings, for the coordinator-cancel consequence.
Task<int> CountActiveForAttendeeAsync(Guid attendeeId, CancellationToken cancellationToken);
```

```csharp
// src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs — add both to
// BookingRepository. Unlocked reads: the replay refusal re-checks under the booking lock,
// and the count is a consequence reported back to a Coordinator, not a gate.
/// <inheritdoc/>
public Task<Booking?> GetByInviteIdAsync(Guid inviteId, CancellationToken cancellationToken) =>
    context.Bookings
        .AsNoTracking()
        .SingleOrDefaultAsync(b => b.InviteId == inviteId, cancellationToken);

/// <inheritdoc/>
public Task<int> CountActiveForAttendeeAsync(Guid attendeeId, CancellationToken cancellationToken) =>
    context.Bookings
        .AsNoTracking()
        .CountAsync(
            b => b.AttendeeId == attendeeId && b.Status == BookingStatus.Active,
            cancellationToken);
```

```csharp
// tests/EventBooking.Application.Tests/Fakes/InMemoryRepositories.cs — the same two over
// InMemoryBookingRepository's Items, with the same semantics so a handler cannot pass
// against the fake and fail against SQL.
public Task<Booking?> GetByInviteIdAsync(Guid inviteId, CancellationToken cancellationToken) =>
    Task.FromResult(Items.SingleOrDefault(b => b.InviteId == inviteId));

public Task<int> CountActiveForAttendeeAsync(Guid attendeeId, CancellationToken cancellationToken) =>
    Task.FromResult(Items.Count(b => b.AttendeeId == attendeeId && b.Status == BookingStatus.Active));
```

- [ ] **Step 1: Write the failing tests.** Create the four test files below in full. The
  fixture mints real tokens through the fake token service, builds events through the
  Task 6/7 domain (proposal, acceptances, `Event.CreateFrom`), and issues invites through
  the Task 14 issuer with the stub eligibility returning the event ids.

  ```csharp
  // tests/EventBooking.Application.Tests/Bookings/BookingFixture.cs (complete)
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Access;
  using EventBooking.Application.Invites;
  using EventBooking.Application.Tests.Fakes;
  using EventBooking.Domain.Access;
  using EventBooking.Domain.AppointmentTypes;
  using EventBooking.Domain.Attendees;
  using EventBooking.Domain.AttendeeGroups;
  using EventBooking.Domain.Events;
  using EventBooking.Domain.Invites;
  using EventBooking.Domain.Locations;
  using EventBooking.Domain.Time;

  namespace EventBooking.Application.Tests.Bookings;

  public sealed class BookingFixture
  {
      public static readonly DateTimeOffset Now = new(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);

      public InMemoryAttendeeRepository Attendees = new();
      public InMemoryInviteRepository Invites = new();
      public InMemoryEventRepository Events = new();
      public InMemoryEventCapacityRepository Capacities = null!;
      public InMemoryBookingRepository Bookings = new();
      public InMemoryBookingAppointmentRepository Appointments = null!;
      public InMemoryLocationRepository Locations = new();
      public InMemoryAppointmentTypeRepository Types = new();
      public InMemoryAttendeeGroupRepository Groups = new();
      public InMemorySystemSettingsRepository Settings = new();
      public InMemoryEmailDeliveryRepository Emails = new();
      public InMemoryStaffAccessProfileRepository Profiles = new();
      public FakeTokenService Tokens = new();
      public FakeUnitOfWork UnitOfWork = new();
      public RecordingAuditLogger Audit = new();
      public FakeClock Clock = new(Now);
      public StubInviteEligibility Eligibility = new();

      public Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
      public Guid EventId;
      public Guid AttendeeId;
      public Dictionary<string, Guid> TypeIds = new();

      public static BookingFixture Create()
      {
          var fixture = new BookingFixture();
          fixture.Capacities = new InMemoryEventCapacityRepository(fixture.Events);
          fixture.Appointments = new InMemoryBookingAppointmentRepository(fixture.Bookings);
          fixture.Types.Items.Clear();
          foreach (var code in new[] { "MED", "FIT", "IND" })
          {
              var type = AppointmentType.Create(Guid.NewGuid(), code, code);
              fixture.Types.Items.Add(type);
              fixture.TypeIds[code] = type.Id;
          }

          var location = Location.Create(Guid.NewGuid(), "LONDON_HQ", "London HQ", "1 High St",
              "Europe/London", BookingTestZones.Instance);
          fixture.Locations.Items.Add(location);

          var group = AttendeeGroup.Create(Guid.NewGuid(), "NHS", "NHS staff",
              fixture.TypeIds.Values.ToList(), fixture.TypeIds.Values.ToList());
          fixture.Groups.Items.Add(group);
          var attendee = Attendee.Create(Guid.NewGuid(), "Amy", "amy@example.invalid", group, Now);
          fixture.Attendees.Items.Add(attendee);
          fixture.AttendeeId = attendee.Id;
          fixture.Profiles.Add(StaffAccessProfile.Create(fixture.Coordinator, Role.Coordinator, null));

          var proposal = EventProposal.Propose(Guid.NewGuid(), location.Id, true, "Europe/London",
              new EventWindow(new DateOnly(2026, 11, 4), new TimeOnly(9, 30), 90),
              BookingTestZones.Instance, Now,
              fixture.TypeIds.Values.Select(id => new ProposableAppointmentType(id, id.ToString(), true, true)).ToList(),
              fixture.TypeIds["MED"], fixture.Coordinator, 10);
          foreach (var code in new[] { "FIT", "IND" })
              proposal.Accept(fixture.TypeIds[code], fixture.Coordinator, 10);
          var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
          fixture.Events.Items.Add(eventItem);
          fixture.EventId = eventItem.Id;
          fixture.Eligibility.EligibleInOrder = [eventItem.Id];
          fixture.Settings.Settings.Update(7, 2, 1);
          return fixture;
      }

      public string BookTokenFor(Guid inviteId, int version) =>
          Tokens.Issue(TokenPurpose.Book, inviteId, version);

      public string ManageTokenFor(Guid bookingId, int version) =>
          Tokens.Issue(TokenPurpose.Manage, bookingId, version);

      private int _subGroups;

      // A second attendee needing exactly the named types, invited at the fixture event.
      // Returns the attendee id and the pending invite id.
      public (Guid AttendeeId, Guid InviteId) InviteAttendee(params string[] codes)
      {
          var ids = codes.Select(c => TypeIds[c]).ToList();
          var group = AttendeeGroup.Create(Guid.NewGuid(), $"SUB{_subGroups:D3}", "Sub group",
              ids, TypeIds.Values.ToList());
          Groups.Items.Add(group);
          var attendee = Attendee.Create(Guid.NewGuid(), "Sub", $"sub{_subGroups}@example.invalid", group, Now);
          _subGroups++;
          Attendees.Items.Add(attendee);
          var issuer = new InviteIssuer(Invites, Settings, Emails, Audit, Clock, Eligibility);
          var result = issuer.IssueInitialAsync(attendee, Locations.Items.Select(l => l.Id).ToList(),
              Domain.Notifications.EmailTemplate.AttendeeInvite, Domain.Audit.ActorType.Staff,
              Coordinator.ToString(), CancellationToken.None).GetAwaiter().GetResult();
          Assert.True(result.IsSuccess);
          return (attendee.Id, result.Value.InviteId);
      }

      public int RemainingFor(string code) =>
          Events.Items.Single().CapacityFor(TypeIds[code]).RemainingCapacity;

      public void Occupy(string code, int count)
      {
          var row = Events.Items.Single().CapacityFor(TypeIds[code]);
          for (var i = 0; i < count; i++) row.Decrement();
      }
  }

  public sealed class StubInviteEligibility : IEventEligibilityQuery
  {
      public IReadOnlyList<Guid> EligibleInOrder { get; set; } = [];
      public Task<IReadOnlyList<Guid>> FindEligibleEventsAsync(
          IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
          IReadOnlyCollection<Guid> locationIds,
          IReadOnlyCollection<Guid> excludeEventIds,
          int count, DateTimeOffset asOf, CancellationToken cancellationToken) =>
          Task.FromResult<IReadOnlyList<Guid>>(EligibleInOrder
              .Where(id => !excludeEventIds.Contains(id)).Take(count).ToList());
      public Task<int> CountEligibleEventsAsync(
          IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
          IReadOnlyCollection<Guid> locationIds,
          IReadOnlyCollection<Guid> excludeEventIds,
          DateTimeOffset asOf, CancellationToken cancellationToken) =>
          Task.FromResult(EligibleInOrder.Count(id => !excludeEventIds.Contains(id)));
  }

  public sealed class BookingTestZones : IEventWindowZones
  {
      public static readonly BookingTestZones Instance = new();
      public bool IsKnownZone(string timeZoneId) => timeZoneId is "Europe/London";
      public LocalTimeValidity ValidityOf(DateOnly date, TimeOnly time, string timeZoneId) =>
          throw new NotImplementedException();
      public DateTimeOffset InstantOf(DateOnly date, TimeOnly time, string timeZoneId) =>
          throw new NotImplementedException();
      public DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId) =>
          throw new NotImplementedException();
      public string AbbreviationOf(DateTimeOffset instant, string timeZoneId) =>
          throw new NotImplementedException();
  }
  ```

  BookingTestZones is a private stub in the same file (IsKnownZone true for
  `Europe/London`, other members throw). StubInviteEligibility is a private stub
  implementing the eligibility port from a settable ordered list, honouring exclusions.
  FakeTokenService is the existing fake. The proposal is confirmed by the three
  acceptances, so `Event.CreateFrom` marks it confirmed with three capacity rows of 10.

  ```csharp
  // tests/EventBooking.Application.Tests/Bookings/ConfirmBookingHandlerTests.cs (complete)
  using EventBooking.Application.Bookings;
  using EventBooking.Application.Common;
  using EventBooking.Domain.Audit;
  using EventBooking.Domain.Bookings;
  using EventBooking.Domain.Notifications;

  namespace EventBooking.Application.Tests.Bookings;

  public sealed class ConfirmBookingHandlerTests
  {
      private static ConfirmBookingHandler Handler(BookingFixture f) => new(
          f.Invites, f.Attendees, f.Events, f.Capacities, f.Bookings, f.Appointments,
          f.Locations, f.Tokens, f.Emails, f.UnitOfWork, f.Audit, f.Clock,
          BookingTestZones.Instance);

      [Fact]
      public async Task Confirm_charges_only_required_types_and_stages_confirmation()
      {
          var fixture = BookingFixture.Create();
          var (attendeeId, inviteId) = fixture.InviteAttendee("IND");
          var invite = fixture.Invites.Items.Single(i => i.Id == inviteId);

          var result = await Handler(fixture).HandleAsync(
              new ConfirmBookingCommand(fixture.BookTokenFor(inviteId, invite.TokenVersion), fixture.EventId),
              CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal(9, fixture.RemainingFor("IND"));
          Assert.Equal(10, fixture.RemainingFor("MED"));
          Assert.Equal(10, fixture.RemainingFor("FIT"));
          var booking = Assert.Single(fixture.Bookings.Items);
          Assert.Equal(attendeeId, booking.AttendeeId);
          Assert.Single(fixture.Appointments.Items);
          Assert.Equal(Domain.Invites.InviteStatus.Used, invite.Status);
          Assert.Equal("Booked", fixture.Attendees.Items.Single(a => a.Id == attendeeId).Status.ToString());
          var delivery = Assert.Single(fixture.Emails.Items);
          Assert.Equal(EmailTemplate.BookingConfirmation, delivery.TemplateName);
          Assert.Equal(booking.Id, delivery.BookingId);
          Assert.Equal(
              [AuditAction.BookingCreated, AuditAction.CapacityDecremented],
              fixture.Audit.Entries.Select(e => e.Action).ToList());
          Assert.Equal(
              fixture.ManageTokenFor(booking.Id, booking.ManageTokenVersion), result.Value.ManageToken);
      }

      [Fact]
      public async Task Replay_returns_conflict_naming_existing_booking_with_no_second_row()
      {
          var fixture = BookingFixture.Create();
          var (_, inviteId) = fixture.InviteAttendee("IND");
          var invite = fixture.Invites.Items.Single(i => i.Id == inviteId);
          var first = await Handler(fixture).HandleAsync(
              new ConfirmBookingCommand(fixture.BookTokenFor(inviteId, invite.TokenVersion), fixture.EventId),
              CancellationToken.None);
          Assert.True(first.IsSuccess);

          var result = await Handler(fixture).HandleAsync(
              new ConfirmBookingCommand(fixture.BookTokenFor(inviteId, invite.TokenVersion), fixture.EventId),
              CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("already-confirmed", result.Error.Code);
          Assert.Contains(first.Value.BookingId.ToString(), result.Error.Message);
          Assert.Single(fixture.Bookings.Items);
          Assert.Equal(9, fixture.RemainingFor("IND"));
      }

      [Fact]
      public async Task Exhausted_required_type_changes_nothing()
      {
          var fixture = BookingFixture.Create();
          fixture.Occupy("IND", 10);
          var (_, inviteId) = fixture.InviteAttendee("MED", "IND");
          var invite = fixture.Invites.Items.Single(i => i.Id == inviteId);

          var result = await Handler(fixture).HandleAsync(
              new ConfirmBookingCommand(fixture.BookTokenFor(inviteId, invite.TokenVersion), fixture.EventId),
              CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("capacity-exhausted", result.Error.Code);
          Assert.Empty(fixture.Bookings.Items);
          Assert.Equal(10, fixture.RemainingFor("MED"));
          Assert.Equal(0, fixture.RemainingFor("IND"));
      }

      [Fact]
      public async Task Tampered_token_is_refused()
      {
          var fixture = BookingFixture.Create();

          var result = await Handler(fixture).HandleAsync(
              new ConfirmBookingCommand("not-a-token", fixture.EventId), CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("validation", result.Error.Code);
          Assert.Empty(fixture.Bookings.Items);
      }
  }
  ```

  The fixture needs `Occupy(code, count)` (decrement the row through the domain method) —
  add it beside RemainingFor. `Booking.Appointments` are read from the appointments fake;
  InMemoryBookingAppointmentRepository exposes its items (follow the existing fake's
  member name; it wraps the bookings repository — assert through
  `fixture.Appointments` count of 1).

  ```csharp
  // tests/EventBooking.Application.Tests/Bookings/CancelBookingHandlerTests.cs (complete)
  using EventBooking.Application.Bookings;
  using EventBooking.Application.Common;
  using EventBooking.Application.Invites;
  using EventBooking.Domain.Audit;
  using EventBooking.Domain.Bookings;

  namespace EventBooking.Application.Tests.Bookings;

  public sealed class CancelBookingHandlerTests
  {
      private static async Task<Guid> ConfirmedBookingAsync(BookingFixture fixture, params string[] codes)
      {
          var (attendeeId, inviteId) = fixture.InviteAttendee(codes);
          var invite = fixture.Invites.Items.Single(i => i.Id == inviteId);
          var confirmer = new ConfirmBookingHandler(
              fixture.Invites, fixture.Attendees, fixture.Events, fixture.Capacities,
              fixture.Bookings, fixture.Appointments, fixture.Locations, fixture.Tokens,
              fixture.Emails, fixture.UnitOfWork, fixture.Audit, fixture.Clock,
              BookingTestZones.Instance);
          var result = await confirmer.HandleAsync(
              new ConfirmBookingCommand(fixture.BookTokenFor(inviteId, invite.TokenVersion), fixture.EventId),
              CancellationToken.None);
          Assert.True(result.IsSuccess);
          return result.Value.BookingId;
      }

      [Fact]
      public async Task Attendee_cancel_without_new_time_releases_and_parks()
      {
          var fixture = BookingFixture.Create();
          var bookingId = await ConfirmedBookingAsync(fixture, "IND");
          var booking = fixture.Bookings.Items.Single(b => b.Id == bookingId);
          var canceller = new CancelBookingByAttendeeHandler(
              fixture.Bookings, fixture.Attendees, fixture.Invites, fixture.Events,
              fixture.Capacities, fixture.Locations, fixture.Tokens,
              fixture.UnitOfWork, fixture.Audit, fixture.Clock, BookingTestZones.Instance,
              fixture.Eligibility, fixture.Settings,
              new InviteIssuer(fixture.Invites, fixture.Settings, fixture.Emails,
                  fixture.Audit, fixture.Clock, fixture.Eligibility));

          var result = await canceller.HandleAsync(
              new CancelBookingByAttendeeCommand(
                  fixture.ManageTokenFor(bookingId, booking.ManageTokenVersion), false),
              CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal("cancelled", result.Value.Outcome);
          Assert.Equal(BookingStatus.Cancelled, booking.Status);
          Assert.Equal(10, fixture.RemainingFor("IND"));
          Assert.Equal("NotYetInvited", fixture.Attendees.Items.Single(a => a.Id == booking.AttendeeId).Status.ToString());
      }

      [Fact]
      public async Task Attendee_cancel_with_new_time_supersedes_pending_recovery_and_rebooks()
      {
          var fixture = BookingFixture.Create();
          var bookingId = await ConfirmedBookingAsync(fixture, "IND");
          var booking = fixture.Bookings.Items.Single(b => b.Id == bookingId);
          var attendeeId = booking.AttendeeId;
          var starter = new StartRecoveryShim(fixture);
          var pendingRecoveryId = await starter.StartAsync(attendeeId, bookingId);
          var canceller = new CancelBookingByAttendeeHandler(
              fixture.Bookings, fixture.Attendees, fixture.Invites, fixture.Events,
              fixture.Capacities, fixture.Locations, fixture.Tokens,
              fixture.UnitOfWork, fixture.Audit, fixture.Clock, BookingTestZones.Instance,
              fixture.Eligibility, fixture.Settings,
              new InviteIssuer(fixture.Invites, fixture.Settings, fixture.Emails,
                  fixture.Audit, fixture.Clock, fixture.Eligibility));

          var result = await canceller.HandleAsync(
              new CancelBookingByAttendeeCommand(
                  fixture.ManageTokenFor(bookingId, booking.ManageTokenVersion), true),
              CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal("reinvited", result.Value.Outcome);
          Assert.NotEqual(pendingRecoveryId, result.Value.InviteId);
          Assert.Equal(Domain.Invites.InviteStatus.Superseded,
              fixture.Invites.Items.Single(i => i.Id == pendingRecoveryId).Status);
          Assert.Equal(BookingStatus.Cancelled, booking.Status);
      }

      [Fact]
      public async Task Attendee_cancel_with_new_time_but_no_options_reports_no_eligible_events()
      {
          var fixture = BookingFixture.Create();
          var bookingId = await ConfirmedBookingAsync(fixture, "IND");
          var booking = fixture.Bookings.Items.Single(b => b.Id == bookingId);
          fixture.Eligibility.EligibleInOrder = [];
          var canceller = new CancelBookingByAttendeeHandler(
              fixture.Bookings, fixture.Attendees, fixture.Invites, fixture.Events,
              fixture.Capacities, fixture.Locations, fixture.Tokens,
              fixture.UnitOfWork, fixture.Audit, fixture.Clock, BookingTestZones.Instance,
              fixture.Eligibility, fixture.Settings,
              new InviteIssuer(fixture.Invites, fixture.Settings, fixture.Emails,
                  fixture.Audit, fixture.Clock, fixture.Eligibility));

          var result = await canceller.HandleAsync(
              new CancelBookingByAttendeeCommand(
                  fixture.ManageTokenFor(bookingId, booking.ManageTokenVersion), true),
              CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal("noEligibleEvents", result.Value.Outcome);
          Assert.Equal(BookingStatus.Cancelled, booking.Status);
          Assert.Equal(10, fixture.RemainingFor("IND"));
          Assert.Equal("NotYetInvited", fixture.Attendees.Items.Single(a => a.Id == booking.AttendeeId).Status.ToString());
      }

      [Fact]
      public async Task Shortfall_with_pending_recovery_rolls_back_and_names_it()
      {
          var fixture = BookingFixture.Create();
          var bookingId = await ConfirmedBookingAsync(fixture, "IND");
          var booking = fixture.Bookings.Items.Single(b => b.Id == bookingId);
          var pendingRecoveryId = await new StartRecoveryShim(fixture).StartAsync(booking.AttendeeId, bookingId);
          fixture.Eligibility.EligibleInOrder = [];
          var canceller = new CancelBookingByAttendeeHandler(
              fixture.Bookings, fixture.Attendees, fixture.Invites, fixture.Events,
              fixture.Capacities, fixture.Locations, fixture.Tokens,
              fixture.UnitOfWork, fixture.Audit, fixture.Clock, BookingTestZones.Instance,
              fixture.Eligibility, fixture.Settings,
              new InviteIssuer(fixture.Invites, fixture.Settings, fixture.Emails,
                  fixture.Audit, fixture.Clock, fixture.Eligibility));

          var result = await canceller.HandleAsync(
              new CancelBookingByAttendeeCommand(
                  fixture.ManageTokenFor(bookingId, booking.ManageTokenVersion), true),
              CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal("reinvitePending", result.Value.Outcome);
          Assert.Equal(pendingRecoveryId, result.Value.InviteId);
          Assert.Equal(Domain.Invites.InviteStatus.Pending,
              fixture.Invites.Items.Single(i => i.Id == pendingRecoveryId).Status);
      }
  }

  // Private to CancelBookingHandlerTests.cs: issues a recovery invite through the Task 14
  // issuer's recovery operation (exactly the call Task 16's StartRecovery will own).
  public sealed class StartRecoveryShim(BookingFixture fixture)
  {
      public async Task<Guid> StartAsync(Guid attendeeId, Guid bookingId)
      {
          var attendee = fixture.Attendees.Items.Single(a => a.Id == attendeeId);
          var booking = fixture.Bookings.Items.Single(b => b.Id == bookingId);
          var invite = fixture.Invites.Items.Single(i => i.Id == booking.InviteId);
          var issuer = new InviteIssuer(fixture.Invites, fixture.Settings, fixture.Emails,
              fixture.Audit, fixture.Clock, fixture.Eligibility);
          var result = await issuer.IssueRecoveryAsync(attendee, bookingId,
              invite.RequiredAppointmentTypeIds, invite.LocationIds, [fixture.EventId],
              ActorType.Staff, fixture.Coordinator.ToString(), CancellationToken.None);
          Assert.True(result.IsSuccess);
          return result.Value.InviteId;
      }
  }

      [Fact]
      public async Task Coordinator_cancel_is_two_step_with_active_booking_count()
      {
          var fixture = BookingFixture.Create();
          var bookingId = await ConfirmedBookingAsync(fixture, "IND");
          var booking = fixture.Bookings.Items.Single(b => b.Id == bookingId);
          var handler = new CancelBookingByCoordinatorHandler(
              fixture.Bookings, fixture.Attendees, fixture.Invites, fixture.Events,
              fixture.Capacities, fixture.Locations, fixture.Profiles, fixture.UnitOfWork,
              fixture.Audit, fixture.Clock, BookingTestZones.Instance);

          var preview = await handler.HandleAsync(new CancelBookingByCoordinatorCommand(
              fixture.Coordinator, booking.AttendeeId, bookingId, false), CancellationToken.None);

          Assert.True(preview.IsSuccess);
          Assert.True(preview.Value.ConfirmationRequired);
          Assert.Equal(1, preview.Value.ActiveBookingCount);
          Assert.Equal(BookingStatus.Active, booking.Status);

          var confirmed = await handler.HandleAsync(new CancelBookingByCoordinatorCommand(
              fixture.Coordinator, booking.AttendeeId, bookingId, true), CancellationToken.None);

          Assert.True(confirmed.IsSuccess);
          Assert.False(confirmed.Value.ConfirmationRequired);
          Assert.Equal(bookingId, confirmed.Value.CancelledBookingId);
          Assert.Equal(BookingStatus.Cancelled, booking.Status);
      }
  }
  ```

  ```csharp
  // tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs (complete)
  using EventBooking.Application.Common;
  using EventBooking.Application.Events;
  using EventBooking.Application.Invites;
  using EventBooking.Application.Tests.Bookings;
  using EventBooking.Domain.Audit;
  using EventBooking.Domain.Bookings;
  using EventBooking.Domain.Notifications;

  namespace EventBooking.Application.Tests.Events;

  public sealed class CancelEventHandlerTests
  {
      private static CancelEventHandler Handler(BookingFixture f) => new(
          f.Events, f.Capacities, f.Bookings, f.Attendees, f.Invites, f.Locations,
          f.Emails, f.Profiles, f.UnitOfWork, f.Audit, f.Clock,
          BookingTestZones.Instance,
          new InviteIssuer(f.Invites, f.Settings, f.Emails, f.Audit, f.Clock, f.Eligibility),
          f.Eligibility, f.Settings);

      private static async Task<Guid> ConfirmedBookingAsync(BookingFixture fixture, params string[] codes)
      {
          var (attendeeId, inviteId) = fixture.InviteAttendee(codes);
          var invite = fixture.Invites.Items.Single(i => i.Id == inviteId);
          var confirmer = new ConfirmBookingHandler(
              fixture.Invites, fixture.Attendees, fixture.Events, fixture.Capacities,
              fixture.Bookings, fixture.Appointments, fixture.Locations, fixture.Tokens,
              fixture.Emails, fixture.UnitOfWork, fixture.Audit, fixture.Clock,
              BookingTestZones.Instance);
          var result = await confirmer.HandleAsync(
              new Bookings.ConfirmBookingCommand(fixture.BookTokenFor(inviteId, invite.TokenVersion), fixture.EventId),
              CancellationToken.None);
          Assert.True(result.IsSuccess);
          return result.Value.BookingId;
      }

      [Fact]
      public async Task Cancel_event_without_confirm_returns_counts_and_changes_nothing()
      {
          var fixture = BookingFixture.Create();
          await ConfirmedBookingAsync(fixture, "IND");
          await ConfirmedBookingAsync(fixture, "MED");

          var result = await Handler(fixture).HandleAsync(
              new CancelEventCommand(fixture.Coordinator, fixture.EventId, false),
              CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal(2, result.Value.CancelledCount);
          Assert.Equal(Domain.Events.EventStatus.Active, fixture.Events.Items.Single().Status);
          Assert.Equal(2, fixture.Bookings.Items.Count(b => b.Status == BookingStatus.Active));
      }

      [Fact]
      public async Task Cancel_event_with_confirm_cancels_in_attendee_order_with_replacements()
      {
          var fixture = BookingFixture.Create();
          var first = await ConfirmedBookingAsync(fixture, "IND");
          var second = await ConfirmedBookingAsync(fixture, "MED");

          var result = await Handler(fixture).HandleAsync(
              new CancelEventCommand(fixture.Coordinator, fixture.EventId, true),
              CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal(2, result.Value.CancelledCount);
          Assert.Equal(2, result.Value.ReinvitedCount);
          Assert.Equal(0, result.Value.AwaitingAvailabilityCount);
          Assert.Equal(Domain.Events.EventStatus.Cancelled, fixture.Events.Items.Single().Status);
          var orderedCancels = fixture.Audit.Entries
              .Where(e => e.Action == AuditAction.BookingCancelled)
              .Select(e => e.EntityId).ToList();
          var firstAttendee = fixture.Bookings.Items.Single(b => b.Id == first).AttendeeId;
          var secondAttendee = fixture.Bookings.Items.Single(b => b.Id == second).AttendeeId;
          var expectedFirst = firstAttendee.CompareTo(secondAttendee) < 0 ? first : second;
          Assert.Equal(expectedFirst, orderedCancels[0]);
          Assert.All(fixture.Audit.Entries.Where(e => e.Action == AuditAction.BookingCancelled),
              e => Assert.Contains("replacement created", e.Details));
          Assert.Equal(2, fixture.Emails.Items.Count(e =>
              e.TemplateName == EmailTemplate.EventCancelledRebookingNeeded));
      }

      [Fact]
      public async Task Manager_scoped_to_unlisted_type_cannot_cancel_event()
      {
          var fixture = BookingFixture.Create();
          var esc = Domain.AppointmentTypes.AppointmentType.Create(Guid.NewGuid(), "ESC", "Escalation");
          fixture.Types.Items.Add(esc);
          var escManager = Guid.NewGuid();
          fixture.Profiles.Add(Domain.Access.StaffAccessProfile.Create(
              escManager, Domain.Access.Role.Manager, esc.Id));

          var result = await Handler(fixture).HandleAsync(
              new CancelEventCommand(escManager, fixture.EventId, true), CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("forbidden", result.Error.Code);
          Assert.Equal(Domain.Events.EventStatus.Active, fixture.Events.Items.Single().Status);
      }
  }
  ```

  The coordinator in the fixture holds `Role.Coordinator`, which the ported matrix admits
  to `CancelEvent` alongside Admin and Manager; Managers are additionally scoped to events
  listing their type. The fixture event lists MED, FIT and IND, so the ESC-scoped Manager
  above is forbidden.

- [ ] **Step 2: Switch the concurrency harness** to the real ConfirmBookingHandler:
  re-run Task 10's three scenarios (40 parallel bookings over rotating subsets; two-event
  shuffled locks; capacity adjustment racing bookings) driving the handler with minted book
  tokens, plus a fourth: CancelEvent racing bookings on the same event never deadlocks and
  leaves capacity consistent (remaining equals total minus surviving active bookings).
  Expected: FAIL to compile — the handlers do not exist yet.

- [ ] **Step 3: Run.** Expected: FAIL to compile.

  ```bash
  dotnet test tests/EventBooking.Application.Tests --filter "FullyQualifiedName~Bookings|FullyQualifiedName~Events"
  ```

- [ ] **Step 4: Implement.** Create the four handler files below in full, add the two
  booking port methods (EF plus fakes), extend the lock ladder, and delete the four ported
  files. The lock helpers gain `Invite` and `Booking` levels between attendee and event;
  the attendee, invite, booking, event and capacity lock methods record their level through
  the existing tracker — no handler touches the tracker directly.

  ```csharp
  // src/EventBooking.Application/Bookings/ConfirmBookingHandler.cs (complete)
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Common;
  using EventBooking.Domain.Attendees;
  using EventBooking.Domain.Audit;
  using EventBooking.Domain.Bookings;
  using EventBooking.Domain.Common;
  using EventBooking.Domain.Invites;
  using EventBooking.Domain.Notifications;
  using EventBooking.Domain.Time;

  namespace EventBooking.Application.Bookings;

  // Anonymous pipeline: the book token carries purpose, invite id and version. Locks in
  // canonical order: attendee, invite, event, then the required capacity rows (ascending).
  // The attendee id is read without a lock first (port read), then every row is locked in
  // order and re-validated under lock. The charge runs on the same tracked rows the
  // capacity repository locked.
  public sealed class ConfirmBookingHandler(
      IInviteRepository invites,
      IAttendeeRepository attendees,
      IEventRepository events,
      IEventCapacityRepository capacities,
      IBookingRepository bookings,
      IBookingAppointmentRepository appointments,
      ILocationRepository locations,
      ITokenService tokens,
      IEmailDeliveryRepository emails,
      IUnitOfWork unitOfWork,
      IAuditLogger audit,
      IClock clock,
      IEventWindowZones zones)
  {
      public async Task<Result<ConfirmBookingOutcome>> HandleAsync(
          ConfirmBookingCommand command, CancellationToken ct)
      {
          if (!tokens.TryRead(command.BookToken, out var reference)
              || reference.Purpose != TokenPurpose.Book
              || reference.Version < Invite.InitialTokenVersion)
              return Result<ConfirmBookingOutcome>.Failure(
                  Error.Validation("This link cannot be used to confirm a booking."));

          await using var transaction = await unitOfWork.BeginTransactionAsync(ct);

          // Port read: the attendee id without a lock, so the locks below follow the
          // canonical order. Everything is re-validated after locking.
          var port = await invites.GetAsync(reference.EntityId, ct);
          if (port is null)
              return Result<ConfirmBookingOutcome>.Failure(Error.NotFound("No such invite."));

          var attendee = await attendees.LockForUpdateAsync(port.AttendeeId, ct);
          if (attendee is null)
              return Result<ConfirmBookingOutcome>.Failure(Error.NotFound("No such attendee."));

          var invite = await invites.LockForUpdateAsync(reference.EntityId, ct);
          if (invite is null)
              return Result<ConfirmBookingOutcome>.Failure(Error.NotFound("No such invite."));
          if (invite.AttendeeId != attendee.Id)
              return Result<ConfirmBookingOutcome>.Failure(
                  Error.Conflict("This link does not belong to this attendee."));
          if (invite.TokenVersion != reference.Version)
              return Result<ConfirmBookingOutcome>.Failure(
                  Error.Conflict("This link has been replaced."));
          if (invite.Status == InviteStatus.Used)
          {
              var existing = await bookings.GetByInviteIdAsync(invite.Id, ct);
              await transaction.RollbackAsync(ct);
              return Result<ConfirmBookingOutcome>.Failure(Error.AlreadyConfirmed(
                  $"This invite already confirmed booking {existing?.Id}.", existing?.Id ?? Guid.Empty));
          }

          if (invite.Status != InviteStatus.Pending)
              return Result<ConfirmBookingOutcome>.Failure(
                  Error.Conflict($"The invite is {invite.Status} and can no longer be used."));
          if (!invite.Offers(command.EventId))
              return Result<ConfirmBookingOutcome>.Failure(
                  Error.Validation("The chosen event is not one of this invite's options."));

          var eventItem = await events.LockForUpdateAsync(command.EventId, ct);
          if (eventItem is null)
              return Result<ConfirmBookingOutcome>.Failure(Error.NotFound("No such event."));
          var location = await locations.GetAsync(eventItem.LocationId, ct);
          if (location is not null && eventItem.Window.HasStarted(zones, location.TimeZoneId, clock.UtcNow))
              return Result<ConfirmBookingOutcome>.Failure(
                  Error.WindowStarted("This event has started and can no longer be booked."));

          var required = invite.RequiredAppointmentTypeIds;
          var locked = await capacities.LockForUpdateAsync(eventItem.Id, required, ct);
          if (locked.Count != required.Count)
              return Result<ConfirmBookingOutcome>.Failure(
                  Error.Validation("The event no longer lists every required appointment type."));

          try
          {
              eventItem.ChargeRequiredTypes(required);
          }
          catch (DomainException ex)
          {
              await transaction.RollbackAsync(ct);
              return Result<ConfirmBookingOutcome>.Failure(Error.CapacityExhausted(ex.Message));
          }

          var booking = Booking.Create(Guid.NewGuid(), invite, eventItem.Id, clock.UtcNow);
          bookings.Add(booking);
          foreach (var typeId in required)
              appointments.Add(BookingAppointment.Create(Guid.NewGuid(), booking.Id, typeId));
          invite.MarkUsed();
          if (attendee.Status != AttendeeStatus.Booked)
              attendee.MarkBooked(clock.UtcNow);

          audit.Record(AuditEntityTypes.Booking, booking.Id, AuditAction.BookingCreated,
              ActorType.AttendeeToken, invite.Id.ToString(), $"event {eventItem.Id}");
          foreach (var typeId in required)
              audit.Record(AuditEntityTypes.Event, eventItem.Id, AuditAction.CapacityDecremented,
                  ActorType.AttendeeToken, invite.Id.ToString(), $"type {typeId}");
          emails.Add(EmailLog.RecordPending(Guid.NewGuid(), attendee.Id,
              EmailTemplate.BookingConfirmation, clock.UtcNow, bookingId: booking.Id));

          await unitOfWork.SaveChangesAsync(ct);
          await transaction.CommitAsync(ct);
          return Result<ConfirmBookingOutcome>.Success(new ConfirmBookingOutcome(
              booking.Id, tokens.Issue(TokenPurpose.Manage, booking.Id, booking.ManageTokenVersion)));
      }
  }
  ```

  ```csharp
  // src/EventBooking.Application/Bookings/CancelBookingByAttendeeHandler.cs (complete)
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Common;
  using EventBooking.Application.Invites;
  using EventBooking.Domain.Attendees;
  using EventBooking.Domain.Audit;
  using EventBooking.Domain.Bookings;
  using EventBooking.Domain.Common;
  using EventBooking.Domain.Invites;
  using EventBooking.Domain.Notifications;
  using EventBooking.Domain.Time;

  namespace EventBooking.Application.Bookings;

  // Anonymous pipeline. The manage token names the booking; the attendee id is read without
  // a lock first so the locks follow the canonical order (attendee, booking, invite, event,
  // capacity). With a new time, eligibility is counted before anything mutates: shortfall
  // with a pending recovery reports reinvitePending and changes nothing; shortfall without
  // one cancels into noEligibleEvents; sufficiency supersedes any pending recovery and
  // issues fresh in the same transaction (FR-9.6).
  public sealed class CancelBookingByAttendeeHandler(
      IBookingRepository bookings,
      IAttendeeRepository attendees,
      IInviteRepository invites,
      IEventRepository events,
      IEventCapacityRepository capacities,
      ILocationRepository locations,
      ITokenService tokens,
      IUnitOfWork unitOfWork,
      IAuditLogger audit,
      IClock clock,
      IEventWindowZones zones,
      IEventEligibilityQuery eligibility,
      ISystemSettingsRepository settings,
      IInviteIssuer issuer)
  {
      public async Task<Result<AttendeeCancelOutcome>> HandleAsync(
          CancelBookingByAttendeeCommand command, CancellationToken ct)
      {
          if (!tokens.TryRead(command.ManageToken, out var reference)
              || reference.Purpose != TokenPurpose.Manage
              || reference.Version < Booking.InitialManageTokenVersion)
              return Result<AttendeeCancelOutcome>.Failure(
                  Error.Validation("This link cannot be used to manage a booking."));

          await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
          var portId = await bookings.GetAttendeeIdAsync(reference.EntityId, ct);
          if (portId is null)
              return Result<AttendeeCancelOutcome>.Failure(Error.NotFound("No such booking."));

          var attendee = await attendees.LockForUpdateAsync(portId.Value, ct);
          if (attendee is null)
              return Result<AttendeeCancelOutcome>.Failure(Error.NotFound("No such attendee."));

          var booking = await bookings.LockForUpdateAsync(reference.EntityId, ct);
          if (booking is null || booking.AttendeeId != attendee.Id)
              return Result<AttendeeCancelOutcome>.Failure(Error.NotFound("No such booking."));
          if (booking.ManageTokenVersion != reference.Version)
              return Result<AttendeeCancelOutcome>.Failure(
                  Error.Conflict("This link has been replaced."));
          if (booking.Status != BookingStatus.Active)
              return Result<AttendeeCancelOutcome>.Failure(
                  Error.Conflict($"The booking is {booking.Status} and cannot be cancelled."));

          var invite = await invites.LockForUpdateAsync(booking.InviteId, ct);
          var eventItem = await events.LockForUpdateAsync(booking.EventId, ct);
          if (eventItem is null)
              return Result<AttendeeCancelOutcome>.Failure(Error.NotFound("No such event."));
          var location = await locations.GetAsync(eventItem.LocationId, ct);
          if (location is not null && eventItem.Window.HasStarted(zones, location.TimeZoneId, clock.UtcNow))
              return Result<AttendeeCancelOutcome>.Failure(
                  Error.WindowStarted("This event has started and can no longer be cancelled."));

          var required = invite?.RequiredAppointmentTypeIds
              ?? attendee.RequiredAppointmentTypeIds;
          await capacities.LockForUpdateAsync(eventItem.Id, required, ct);

          if (!command.RequestNewTime)
          {
              booking.Cancel();
              eventItem.ReleaseTypes(required);
              attendee.ResetToNotYetInvited(clock.UtcNow);
              audit.Record(AuditEntityTypes.Booking, booking.Id, AuditAction.BookingCancelled,
                  ActorType.AttendeeToken, booking.Id.ToString(), "by attendee");
              await unitOfWork.SaveChangesAsync(ct);
              await transaction.CommitAsync(ct);
              return Result<AttendeeCancelOutcome>.Success(new AttendeeCancelOutcome("cancelled", null));
          }

          var configuration = await settings.GetAsync(ct);
          var originalLocations = invite?.LocationIds ?? [eventItem.LocationId];
          var eligible = await eligibility.CountEligibleEventsAsync(
              required, originalLocations, [eventItem.Id], clock.UtcNow, ct);
          var pendingRecovery = await invites.LockPendingForAttendeeAsync(attendee.Id, ct);
          var recoveryWasPending = pendingRecovery?.RecoveryOfBookingId is not null;

          if (eligible < configuration.InviteOptionCount)
          {
              if (recoveryWasPending)
                  return Result<AttendeeCancelOutcome>.Success(
                      new AttendeeCancelOutcome("reinvitePending", pendingRecovery!.Id));

              booking.Cancel();
              eventItem.ReleaseTypes(required);
              attendee.ResetToNotYetInvited(clock.UtcNow);
              audit.Record(AuditEntityTypes.Booking, booking.Id, AuditAction.BookingCancelled,
                  ActorType.AttendeeToken, booking.Id.ToString(), "by attendee; no eligible events");
              await unitOfWork.SaveChangesAsync(ct);
              await transaction.CommitAsync(ct);
              return Result<AttendeeCancelOutcome>.Success(new AttendeeCancelOutcome("noEligibleEvents", null));
          }

          booking.Cancel();
          eventItem.ReleaseTypes(required);
          pendingRecovery?.MarkSuperseded();
          var freshEvents = await eligibility.FindEligibleEventsAsync(
              required, originalLocations, [eventItem.Id], configuration.InviteOptionCount,
              clock.UtcNow, ct);
          var issued = await issuer.IssueRecoveryAsync(attendee, booking.Id, required,
              originalLocations, freshEvents, ActorType.AttendeeToken, booking.Id.ToString(), ct);
          if (issued.IsFailure)
          {
              await transaction.RollbackAsync(ct);
              return Result<AttendeeCancelOutcome>.Failure(issued.Error);
          }

          audit.Record(AuditEntityTypes.Booking, booking.Id, AuditAction.BookingCancelled,
              ActorType.AttendeeToken, booking.Id.ToString(), "by attendee; rebooked");
          await unitOfWork.SaveChangesAsync(ct);
          await transaction.CommitAsync(ct);
          return Result<AttendeeCancelOutcome>.Success(new AttendeeCancelOutcome("reinvited", issued.Value.InviteId));
      }
  }
  ```


  ```csharp
  // src/EventBooking.Application/Bookings/CancelBookingByCoordinatorHandler.cs (complete)
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Access;
  using EventBooking.Application.Common;
  using EventBooking.Domain.Access;
  using EventBooking.Domain.Attendees;
  using EventBooking.Domain.Audit;
  using EventBooking.Domain.Bookings;
  using EventBooking.Domain.Common;
  using EventBooking.Domain.Time;

  namespace EventBooking.Application.Bookings;

  public sealed class CancelBookingByCoordinatorHandler(
      IBookingRepository bookings,
      IAttendeeRepository attendees,
      IInviteRepository invites,
      IEventRepository events,
      IEventCapacityRepository capacities,
      ILocationRepository locations,
      IStaffAccessAuthorizer access,
      IUnitOfWork unitOfWork,
      IAuditLogger audit,
      IClock clock,
      IEventWindowZones zones)
  {
      public async Task<Result<CoordinatorCancelOutcome>> HandleAsync(
          CancelBookingByCoordinatorCommand command, CancellationToken ct)
      {
          var authorized = await access.AuthorizeAsync(
              command.StaffUserId, StaffCapability.ManageAttendees, null, ct);
          if (authorized.IsFailure) return Result<CoordinatorCancelOutcome>.Failure(authorized.Error);

          await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
          var attendee = await attendees.LockForUpdateAsync(command.AttendeeId, ct);
          if (attendee is null)
              return Result<CoordinatorCancelOutcome>.Failure(Error.NotFound("No such attendee."));

          var booking = await bookings.LockByIdForAttendeeAsync(command.BookingId, command.AttendeeId, ct);
          if (booking is null)
              return Result<CoordinatorCancelOutcome>.Failure(Error.NotFound("No such booking."));
          if (booking.Status != BookingStatus.Active)
              return Result<CoordinatorCancelOutcome>.Failure(
                  Error.Conflict($"The booking is {booking.Status} and cannot be cancelled."));

          if (!command.Confirm)
          {
              var activeCount = await bookings.CountActiveForAttendeeAsync(attendee.Id, ct);
              await transaction.CommitAsync(ct);
              return Result<CoordinatorCancelOutcome>.Success(
                  new CoordinatorCancelOutcome(true, activeCount, null));
          }

          var invite = await invites.LockForUpdateAsync(booking.InviteId, ct);
          var eventItem = await events.LockForUpdateAsync(booking.EventId, ct);
          if (eventItem is null)
              return Result<CoordinatorCancelOutcome>.Failure(Error.NotFound("No such event."));
          var location = await locations.GetAsync(eventItem.LocationId, ct);
          if (location is not null && eventItem.Window.HasStarted(zones, location.TimeZoneId, clock.UtcNow))
              return Result<CoordinatorCancelOutcome>.Failure(
                  Error.WindowStarted("This event has started and can no longer be cancelled."));

          var required = invite?.RequiredAppointmentTypeIds
              ?? attendee.RequiredAppointmentTypeIds;
          await capacities.LockForUpdateAsync(eventItem.Id, required, ct);

          booking.Cancel();
          eventItem.ReleaseTypes(required);
          attendee.ResetToNotYetInvited(clock.UtcNow);
          audit.Record(AuditEntityTypes.Booking, booking.Id, AuditAction.BookingCancelled,
              ActorType.Staff, command.StaffUserId.ToString(), "by coordinator");
          await unitOfWork.SaveChangesAsync(ct);
          await transaction.CommitAsync(ct);
          return Result<CoordinatorCancelOutcome>.Success(
              new CoordinatorCancelOutcome(false, 0, booking.Id));
      }
  }
  ```

  Without confirm the handler commits an empty transaction and returns
  `confirmation-required` through the outcome record (`ConfirmationRequired: true` with
  the active-booking count) — the `confirmation-required` error code is reserved for
  transports that surface the two-step as a refusal (Task 22 maps the outcome).

  ```csharp
  // src/EventBooking.Application/Events/CancelEventHandler.cs (complete)
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Access;
  using EventBooking.Application.Common;
  using EventBooking.Application.Invites;
  using EventBooking.Domain.Access;
  using EventBooking.Domain.Attendees;
  using EventBooking.Domain.Audit;
  using EventBooking.Domain.Bookings;
  using EventBooking.Domain.Common;
  using EventBooking.Domain.Events;
  using EventBooking.Domain.Notifications;
  using EventBooking.Domain.Time;

  namespace EventBooking.Application.Events;

  public sealed class CancelEventHandler(
      IEventRepository events,
      IEventCapacityRepository capacities,
      IBookingRepository bookings,
      IAttendeeRepository attendees,
      IInviteRepository invites,
      ILocationRepository locations,
      IEmailDeliveryRepository emails,
      IStaffAccessAuthorizer access,
      IUnitOfWork unitOfWork,
      IAuditLogger audit,
      IClock clock,
      IEventWindowZones zones,
      IInviteIssuer issuer,
      IEventEligibilityQuery eligibility,
      ISystemSettingsRepository settings)
  {
      public async Task<Result<CancelEventOutcome>> HandleAsync(
          CancelEventCommand command, CancellationToken ct)
      {
          var authorized = await access.AuthorizeAsync(
              command.StaffUserId, StaffCapability.CancelEvent, null, ct);
          if (authorized.IsFailure) return Result<CancelEventOutcome>.Failure(authorized.Error);

          await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
          var eventItem = await events.LockForUpdateAsync(command.EventId, ct);
          if (eventItem is null)
              return Result<CancelEventOutcome>.Failure(Error.NotFound("No such event."));

          if (authorized.Value.AppointmentTypeId is { } scopedType
              && !eventItem.Capacities.Any(c => c.AppointmentTypeId == scopedType))
              return Result<CancelEventOutcome>.Failure(
                  Error.Forbidden("This event does not list the manager's appointment type."));

          var active = await bookings.ListActiveForEventAsync(eventItem.Id, ct);
          if (!command.Confirm)
          {
              await transaction.CommitAsync(ct);
              return Result<CancelEventOutcome>.Success(new CancelEventOutcome(active.Count, 0, 0));
          }

          var location = await locations.GetAsync(eventItem.LocationId, ct);
          if (location is null)
              return Result<CancelEventOutcome>.Failure(Error.NotFound("No such location."));
          try
          {
              eventItem.Cancel(zones, location.TimeZoneId, clock.UtcNow);
          }
          catch (DomainException ex)
          {
              await transaction.RollbackAsync(ct);
              return Result<CancelEventOutcome>.Failure(Error.WindowStarted(ex.Message));
          }

          audit.Record(AuditEntityTypes.Event, eventItem.Id, AuditAction.EventCancelled,
              ActorType.Staff, command.StaffUserId.ToString(), $"{active.Count} bookings");

          var configuration = await settings.GetAsync(ct);
          var cancelled = 0;
          var reinvited = 0;
          var awaiting = 0;

          // Snapshot without locks, then each booking's locks in canonical order with
          // re-validation under lock (section 8, decision 2).
          foreach (var snapshot in active.OrderBy(b => b.AttendeeId).ThenBy(b => b.Id).ToList())
          {
              var attendee = await attendees.LockForUpdateAsync(snapshot.AttendeeId, ct);
              if (attendee is null) continue;
              var booking = await bookings.LockByIdForAttendeeAsync(snapshot.Id, attendee.Id, ct);
              if (booking is null || booking.Status != BookingStatus.Active) continue;

              var invite = await invites.LockForUpdateAsync(booking.InviteId, ct);
              var required = invite?.RequiredAppointmentTypeIds
                  ?? attendee.RequiredAppointmentTypeIds;
              await capacities.LockForUpdateAsync(eventItem.Id, required, ct);

              booking.Cancel();
              eventItem.ReleaseTypes(required);
              attendee.ResetToNotYetInvited(clock.UtcNow);
              cancelled++;

              var originalLocations = invite?.LocationIds ?? [eventItem.LocationId];
              var fresh = await eligibility.FindEligibleEventsAsync(
                  required, originalLocations, [eventItem.Id],
                  configuration.InviteOptionCount, clock.UtcNow, ct);
              if (fresh.Count >= configuration.InviteOptionCount)
              {
                  var issued = await issuer.IssueRecoveryAsync(attendee, booking.Id, required,
                      originalLocations, fresh.Take(configuration.InviteOptionCount).ToList(),
                      ActorType.Staff, command.StaffUserId.ToString(), ct);
                  if (issued.IsSuccess)
                  {
                      reinvited++;
                      emails.Add(EmailLog.RecordPending(Guid.NewGuid(), attendee.Id,
                          EmailTemplate.EventCancelledRebookingNeeded, clock.UtcNow,
                          bookingId: booking.Id, eventId: eventItem.Id));
                      audit.Record(AuditEntityTypes.Booking, booking.Id, AuditAction.BookingCancelled,
                          ActorType.Staff, command.StaffUserId.ToString(), "replacement created");
                      continue;
                  }
              }

              awaiting++;
              attendee.MarkAwaitingAvailability(clock.UtcNow);
              emails.Add(EmailLog.RecordPending(Guid.NewGuid(), attendee.Id,
                  EmailTemplate.EventCancelledRebookingNeeded, clock.UtcNow,
                  bookingId: booking.Id, eventId: eventItem.Id));
              audit.Record(AuditEntityTypes.Booking, booking.Id, AuditAction.BookingCancelled,
                  ActorType.Staff, command.StaffUserId.ToString(), "no replacement");
          }

          await unitOfWork.SaveChangesAsync(ct);
          await transaction.CommitAsync(ct);
          return Result<CancelEventOutcome>.Success(new CancelEventOutcome(cancelled, reinvited, awaiting));
      }
  }
  ```

  Two details are deliberate. ResetToNotYetInvited followed by MarkAwaitingAvailability
  in the no-replacement branch: the reset parks every cancelled attendee first, and only
  the branch without a replacement moves them on to awaiting — the table allows
  `NotYetInvited` to `AwaitingAvailability`, so the two-step transition is legal. The
  scoped-type check reads `authorized.Value.AppointmentTypeId`: Coordinators and Admins
  carry no scope and skip it; Managers are refused for events not listing their type. The
  preview branch (`!command.Confirm`) commits an empty transaction and returns counts only.

- [ ] **Step 5: Run** the application and infrastructure suites. Expected: PASS.

  ```bash
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  ```

  Expect Application and Infrastructure counts to move with the new suites and the
  converted harness. A count that does not match the executor's own before/after diff is a
  signal to read the diff, not to adjust the number.

- [ ] **Step 6: Commit and push** the executor's code — not the plan documents — under the
  master plan's message:

  ```bash
  test -z "$(git status --porcelain --ignored=no | grep -v '^??')"
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  git add src/EventBooking.Application/Common/Error.cs src/EventBooking.Application/Abstractions/IBookingRepository.cs src/EventBooking.Application/Bookings/ src/EventBooking.Application/Events/CancelEventHandler.cs src/EventBooking.Infrastructure/Persistence/Locking/ src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs tests/EventBooking.Application.Tests/Fakes/InMemoryRepositories.cs tests/EventBooking.Infrastructure.Tests/Concurrency/ tests/EventBooking.Application.Tests/Bookings/ tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs
  git diff --cached --name-only
  git diff --cached
  test -n "$EXECUTOR_COAUTHOR"
  git commit -m "feat(app): booking and cancellation over N capacity rows

  Co-authored-by: $EXECUTOR_COAUTHOR"
  git push
  ```
