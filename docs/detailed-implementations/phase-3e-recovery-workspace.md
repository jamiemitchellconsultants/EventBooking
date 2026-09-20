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
recovery requirement selector revalidated under lock (kept as-is — it is already pure and
general); ListWorkspaceEvents (location id optional) bounded to end instants between 7 days
ago and 14 days ahead, grouped by location, defaulting to the nearest current or next;
GetWorkspaceRoster (event id); SetAppointmentStatus (appointment id, target status, expected
version); DownloadRoster (event id) as CSV with formula-cell neutralisation; concluding
recovery bookings whose appointments are all terminal.

**Architecture:** One transaction per command; attendee lock first, then the recovery invite
where one exists. A second concurrent recovery is refused with `recovery-active`. The
workspace roster exposes only name, email, status, timestamps and version — never attendee,
booking or invite identifiers. Check-in is judged on the event's local date in the
location's zone (replacing the transitional-location date); `NoShow` only after the end
instant (replacing the four-hour window). Version conflicts on appointments return
`version-conflict` with the current state. No domain entity leaves the handler.

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
- Create: src/EventBooking.Application/Recovery/CancelRecoveryInviteHandler.cs (new file;
  the ported file below is deleted first, so there is exactly one at the end)
- Keep: src/EventBooking.Application/Invites/RecoveryRequirementSelector.cs (unchanged;
  already pure and general)
- Create: src/EventBooking.Application/Appointments/WorkspaceEventHandlers.cs
- Create: src/EventBooking.Application/Appointments/WorkspaceRosterHandlers.cs
- Create: src/EventBooking.Application/Appointments/RosterCsv.cs
- Create: src/EventBooking.Infrastructure/Persistence/Queries/WorkspaceQueries.cs
- Delete: src/EventBooking.Application/Invites/StartRecoveryHandler.cs
- Delete: src/EventBooking.Application/Invites/CancelRecoveryInviteHandler.cs (ported version;
  deleted before the new file of the same name is created)
- Modify: src/EventBooking.Application/Common/Error.cs (recovery-active factory)
- Test: tests/EventBooking.Application.Tests/Recovery/RecoveryFixture.cs
- Test: tests/EventBooking.Application.Tests/Recovery/RecoveryHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Appointments/WorkspaceHandlerTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/Queries/WorkspaceQueryTests.cs

**Interfaces:**

```csharp
namespace EventBooking.Application.Recovery;

public sealed record StartRecoveryCommand(Guid StaffUserId, Guid AttendeeId, IReadOnlyList<Guid> AdditionalLocationIds);
public sealed record StartRecoveryOutcome(Guid RecoveryInviteId, IReadOnlyList<Guid> LocationIds, IReadOnlyList<Guid> RecoverableTypeIds);
public sealed record CancelRecoveryInviteCommand(Guid StaffUserId, Guid RecoveryInviteId);
```

```csharp
namespace EventBooking.Application.Appointments;

public sealed record ListWorkspaceEventsQuery(Guid StaffUserId, Guid? LocationId);
public sealed record WorkspaceEventView(
    Guid EventId, Guid LocationId, string LocationName, DateOnly Date,
    TimeOnly StartTime, TimeOnly EndTime, string ZoneAbbreviation);
public sealed record WorkspaceRosterRow(
    string Name, string Email, string ScopeTypeCode, string AppointmentStatus,
    DateTimeOffset? CheckedInAt, long Version);
public sealed record SetAppointmentStatusCommand(
    Guid StaffUserId, Guid AppointmentId, string TargetStatus, long ExpectedVersion);
```

```csharp
// Error.cs: add beside the Task 12–15 factories.
public const string RecoveryActiveCode = "recovery-active";
public static Error RecoveryActive(string message) => new(RecoveryActiveCode, message);
```

- [ ] **Step 1: Write the failing tests.** Create the four test files below in full. The
  fixture builds an attendee with a confirmed booking carrying MED and FIT appointments,
  marks MED `NoShow`, and wires locations, profiles (Coordinator plus a MED
  AppointmentStaff member), clock and zones.

  ```csharp
  // tests/EventBooking.Application.Tests/Recovery/RecoveryFixture.cs (complete)
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Access;
  using EventBooking.Application.Tests.Fakes;
  using EventBooking.Domain.Access;
  using EventBooking.Domain.AppointmentTypes;
  using EventBooking.Domain.Attendees;
  using EventBooking.Domain.AttendeeGroups;
  using EventBooking.Domain.Bookings;
  using EventBooking.Domain.Events;
  using EventBooking.Domain.Invites;
  using EventBooking.Domain.Locations;
  using EventBooking.Domain.Time;

  namespace EventBooking.Application.Tests.Recovery;

  public sealed class RecoveryZones : IEventWindowZones
  {
      public static readonly RecoveryZones Instance = new();
      public bool IsKnownZone(string timeZoneId) =>
          timeZoneId is "Europe/London" or "Europe/Dublin" or "Asia/Tokyo";
      public LocalTimeValidity ValidityOf(DateOnly date, TimeOnly time, string timeZoneId) =>
          LocalTimeValidity.Unique;
      public DateTimeOffset InstantOf(DateOnly date, TimeOnly time, string timeZoneId) =>
          new(new DateTime(date.Year, date.Month, date.Day, time.Hour, time.Minute, 0, DateTimeKind.Unspecified),
              TimeSpan.Zero);
      public DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId) =>
          // Hard-coded summer offsets for the June test date, not a zone database: Dublin
          // observes IST (UTC+1) in June, Tokyo JST (UTC+9) year-round.
          timeZoneId switch
          {
              "Asia/Tokyo" => DateOnly.FromDateTime(instant.UtcDateTime.AddHours(9)),
              "Europe/Dublin" => DateOnly.FromDateTime(instant.UtcDateTime.AddHours(1)),
              _ => DateOnly.FromDateTime(instant.UtcDateTime),
          };
      public string AbbreviationOf(DateTimeOffset instant, string timeZoneId) => "T";
  }

  public sealed class StubRecoveryEligibility : IEventEligibilityQuery
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

  public sealed class RecoveryFixture
  {
      public static readonly DateTimeOffset Now = new(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);

      public InMemoryAttendeeRepository Attendees = new();
      public InMemoryInviteRepository Invites = new();
      public InMemoryBookingRepository Bookings = new();
      public InMemoryBookingAppointmentRepository Appointments = null!;
      public InMemoryEventRepository Events = new();
      public InMemoryEventCapacityRepository Capacities = null!;
      public InMemoryLocationRepository Locations = new();
      public InMemoryAppointmentTypeRepository Types = new();
      public InMemoryAttendeeGroupRepository Groups = new();
      public InMemorySystemSettingsRepository Settings = new();
      public InMemoryEmailDeliveryRepository Emails = new();
      public InMemoryStaffAccessProfileRepository Profiles = new();
      public FakeUnitOfWork UnitOfWork = new();
      public RecordingAuditLogger Audit = new();
      public FakeClock Clock = new(Now);
      public FakeTokenService Tokens = new();
      public StubRecoveryEligibility Eligibility = new();

      public Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
      public Guid MedStaff = Guid.Parse("d0000001-0000-0000-0000-000000000001");
      public Guid AttendeeId;
      public Guid BookingId;
      public Guid LondonId;
      public Guid TokyoId;
      public Guid DublinId;
      public Guid MedId;
      public Guid FitId;
      public bool ManageAttendees = true;

      public static RecoveryFixture Create()
      {
          var fixture = new RecoveryFixture();
          fixture.Appointments = new InMemoryBookingAppointmentRepository(fixture.Bookings);
          fixture.Capacities = new InMemoryEventCapacityRepository(fixture.Events);
          fixture.Types.Items.Clear();
          var med = AppointmentType.Create(Guid.NewGuid(), "MED", "Medical");
          var fit = AppointmentType.Create(Guid.NewGuid(), "FIT", "Fitness");
          fixture.Types.Items.AddRange([med, fit]);
          fixture.MedId = med.Id;
          fixture.FitId = fit.Id;
          var london = Location.Create(Guid.NewGuid(), "LONDON_HQ", "London HQ", "1 High St",
              "Europe/London", RecoveryZones.Instance);
          var tokyo = Location.Create(Guid.NewGuid(), "TOKYO", "Tokyo", "2 Shibuya",
              "Asia/Tokyo", RecoveryZones.Instance);
          var dublin = Location.Create(Guid.NewGuid(), "DUBLIN", "Dublin", "3 Grafton St",
              "Europe/Dublin", RecoveryZones.Instance);
          fixture.Locations.Items.AddRange([london, tokyo, dublin]);
          fixture.LondonId = london.Id;
          fixture.TokyoId = tokyo.Id;
          fixture.DublinId = dublin.Id;
          var group = AttendeeGroup.Create(Guid.NewGuid(), "NHS", "NHS staff",
              [med.Id, fit.Id], [med.Id, fit.Id]);
          fixture.Groups.Items.Add(group);
          var attendee = Attendee.Create(Guid.NewGuid(), "Amy", "amy@example.invalid", group, Now);
          fixture.Attendees.Items.Add(attendee);
          fixture.AttendeeId = attendee.Id;

          var proposal = EventProposal.Propose(Guid.NewGuid(), london.Id, true, "Europe/London",
              new EventWindow(new DateOnly(2026, 11, 4), new TimeOnly(9, 30), 90),
              RecoveryZones.Instance, Now,
              [new ProposableAppointmentType(med.Id, "MED", true, true),
               new ProposableAppointmentType(fit.Id, "FIT", true, true)],
              med.Id, fixture.Coordinator, 10);
          proposal.Accept(fit.Id, fixture.Coordinator, 10);
          var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
          fixture.Events.Items.Add(eventItem);

          var invite = Invite.CreateInitial(Guid.NewGuid(), attendee.Id, Now.AddDays(7),
              [london.Id], [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()], [med.Id, fit.Id], 0);
          fixture.Invites.Items.Add(invite);
          invite.MarkUsed();
          var booking = Booking.Create(Guid.NewGuid(), invite, eventItem.Id, Now);
          fixture.Bookings.Items.Add(booking);
          fixture.BookingId = booking.Id;
          foreach (var typeId in new[] { med.Id, fit.Id })
              fixture.Appointments.Items.Add(BookingAppointment.Create(Guid.NewGuid(), booking.Id, typeId));
          attendee.MarkBooked(Now);
          Apps(fixture).Single(a => a.AppointmentTypeId == med.Id)
              .TransitionTo(BookingAppointmentStatus.NoShow, fixture.Coordinator, Now, false, true);
          fixture.Profiles.Add(StaffAccessProfile.Create(fixture.Coordinator, Role.Coordinator, null));
          fixture.Profiles.Add(StaffAccessProfile.Create(fixture.MedStaff, Role.AppointmentStaff, med.Id));
          fixture.Settings.Settings.Update(7, 2, 1);
          return fixture;
      }

      public static List<BookingAppointment> Apps(RecoveryFixture fixture) =>
          fixture.Appointments.Items.Where(a => a.BookingId == fixture.BookingId).ToList();

      // A Dublin event 00:30–01:30 local on 15 June with its own booked appointment,
      // returned for zone-judged check-in tests.
      public Guid WithDublinBooking()
      {
          var dublin = Locations.Items.Single(l => l.Id == DublinId);
          var proposal = EventProposal.Propose(Guid.NewGuid(), dublin.Id, true, "Europe/Dublin",
              new EventWindow(new DateOnly(2026, 6, 15), new TimeOnly(0, 30), 60),
              RecoveryZones.Instance, Now,
              [new ProposableAppointmentType(MedId, "MED", true, true),
               new ProposableAppointmentType(FitId, "FIT", true, true)],
              MedId, Coordinator, 5);
          proposal.Accept(FitId, Coordinator, 5);
          var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
          Events.Items.Add(eventItem);
          var invite = Invite.CreateInitial(Guid.NewGuid(), AttendeeId, Now.AddDays(700),
              [dublin.Id], [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()], [MedId, FitId], 0);
          Invites.Items.Add(invite);
          invite.MarkUsed();
          var booking = Booking.Create(Guid.NewGuid(), invite, eventItem.Id, Now);
          Bookings.Items.Add(booking);
          var appointment = BookingAppointment.Create(Guid.NewGuid(), booking.Id, MedId);
          Appointments.Items.Add(appointment);
          return appointment.Id;
      }

      public void RemoveCoordinator() =>
          Profiles.Items.RemoveAll(p => p.StaffUserId == Coordinator);
  }
  ```

  Apps filters the shared appointments fake to this fixture's booking.
  RemoveCoordinator deletes the Coordinator profile so the authorizer denies.

  ```csharp
  // tests/EventBooking.Application.Tests/Recovery/RecoveryHandlerTests.cs (complete)
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Bookings;
  using EventBooking.Application.Common;
  using EventBooking.Application.Invites;
  using EventBooking.Application.Recovery;
  using EventBooking.Domain.Audit;
  using EventBooking.Domain.Bookings;
  using EventBooking.Domain.Invites;

  namespace EventBooking.Application.Tests.Recovery;

  public sealed class RecoveryHandlerTests
  {
      private static StartRecoveryHandler Starter(RecoveryFixture f) => new(
          f.Attendees, f.Bookings, f.Appointments, f.Invites, f.Locations, f.Settings,
          f.Emails, f.Profiles, f.UnitOfWork, f.Audit, f.Clock, RecoveryZones.Instance,
          f.Eligibility, new InviteIssuer(f.Invites, f.Settings, f.Emails, f.Audit, f.Clock, f.Eligibility));

      [Fact]
      public async Task Start_recovery_defaults_to_booking_location_and_snapshots_recoverable()
      {
          var fixture = RecoveryFixture.Create();

          var result = await Starter(fixture).HandleAsync(
              new StartRecoveryCommand(fixture.Coordinator, fixture.AttendeeId, [fixture.TokyoId]),
              CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Contains(fixture.LondonId, result.Value.LocationIds);
          Assert.Contains(fixture.TokyoId, result.Value.LocationIds);
          Assert.Equal([fixture.MedId], result.Value.RecoverableTypeIds);
      }

      [Fact]
      public async Task Second_recovery_while_active_is_refused()
      {
          var fixture = RecoveryFixture.Create();
          var first = await Starter(fixture).HandleAsync(
              new StartRecoveryCommand(fixture.Coordinator, fixture.AttendeeId, []),
              CancellationToken.None);
          Assert.True(first.IsSuccess);

          var result = await Starter(fixture).HandleAsync(
              new StartRecoveryCommand(fixture.Coordinator, fixture.AttendeeId, []),
              CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("recovery-active", result.Error.Code);
      }

      [Fact]
      public async Task Recovery_without_manage_attendees_is_forbidden()
      {
          var fixture = RecoveryFixture.Create();
          fixture.RemoveCoordinator();

          var result = await Starter(fixture).HandleAsync(
              new StartRecoveryCommand(fixture.Coordinator, fixture.AttendeeId, []),
              CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("forbidden", result.Error.Code);
      }

      [Fact]
      public async Task Recovery_booking_concludes_when_all_appointments_terminal()
      {
          var fixture = RecoveryFixture.Create();
          fixture.Eligibility.EligibleInOrder = [fixture.Events.Items.Single().Id];
          var started = await Starter(fixture).HandleAsync(
              new StartRecoveryCommand(fixture.Coordinator, fixture.AttendeeId, []),
              CancellationToken.None);
          Assert.True(started.IsSuccess);

          var recoveryInvite = fixture.Invites.Items.Single(i => i.Id == started.Value.RecoveryInviteId);
          var token = fixture.Tokens.Issue(TokenPurpose.Book, recoveryInvite.Id, recoveryInvite.TokenVersion);
          var confirmer = new ConfirmBookingHandler(
              fixture.Invites, fixture.Attendees, fixture.Events, fixture.Capacities,
              fixture.Bookings, fixture.Appointments, fixture.Locations, fixture.Tokens,
              fixture.Emails, fixture.UnitOfWork, fixture.Audit, fixture.Clock,
              RecoveryZones.Instance);
          var confirmed = await confirmer.HandleAsync(
              new ConfirmBookingCommand(token, recoveryInvite.Options[0].EventId),
              CancellationToken.None);
          Assert.True(confirmed.IsSuccess);

          foreach (var appointment in fixture.Appointments.Items.Where(a => a.BookingId == confirmed.Value.BookingId))
          {
              appointment.TransitionTo(BookingAppointmentStatus.CheckedIn, fixture.Coordinator,
                  RecoveryFixture.Now, true, false);
              appointment.TransitionTo(BookingAppointmentStatus.Completed, fixture.Coordinator,
                  RecoveryFixture.Now, true, false);
          }

          var result = await new ConcludeRecoveryHandler(
              fixture.Bookings, fixture.Appointments, fixture.UnitOfWork, fixture.Audit)
              .HandleAsync(confirmed.Value.BookingId, CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal(BookingStatus.Concluded,
              fixture.Bookings.Items.Single(b => b.Id == confirmed.Value.BookingId).Status);
          Assert.Contains(fixture.Audit.Entries, e => e.Action == AuditAction.RecoveryBookingConcluded);
      }
  }
  ```

  ```csharp
  // tests/EventBooking.Application.Tests/Appointments/WorkspaceHandlerTests.cs (complete)
  using System.Text.Json;
  using EventBooking.Application.Appointments;
  using EventBooking.Application.Common;
  using EventBooking.Application.Tests.Recovery;
  using EventBooking.Domain.Bookings;

  namespace EventBooking.Application.Tests.Appointments;

  public sealed class WorkspaceHandlerTests
  {
      private static SetAppointmentStatusHandler StatusHandler(RecoveryFixture f) =>
          new(f.Appointments, f.Bookings, f.Events, f.Locations, f.Profiles,
              f.UnitOfWork, f.Audit, f.Clock, RecoveryZones.Instance);

      [Fact]
      public async Task Dublin_checkin_before_utc_midnight_is_allowed_on_local_date()
      {
          var fixture = RecoveryFixture.Create();
          var appointmentId = fixture.WithDublinBooking();
          fixture.Clock.UtcNow = new DateTimeOffset(2026, 6, 14, 23, 30, 0, TimeSpan.Zero);
          var appointment = fixture.Appointments.Items.Single(a => a.Id == appointmentId);
          var status = new SetAppointmentStatusHandler(
              fixture.Appointments, fixture.Bookings, fixture.Events, fixture.Locations,
              fixture.Profiles, fixture.UnitOfWork, fixture.Audit, fixture.Clock,
              RecoveryZones.Instance);

          var result = await status.HandleAsync(new SetAppointmentStatusCommand(
              fixture.MedStaff, appointment.Id, "CheckedIn", appointment.Version),
              CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal(BookingAppointmentStatus.CheckedIn, appointment.Status);
      }

      [Fact]
      public async Task Noshow_before_end_is_refused_and_roster_hides_ids()
      {
          var fixture = RecoveryFixture.Create();
          var appointment = RecoveryFixture.Apps(fixture).First();
          var status = StatusHandler(fixture);

          var result = await status.HandleAsync(new SetAppointmentStatusCommand(
              fixture.MedStaff, appointment.Id, "NoShow", appointment.Version),
              CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("validation", result.Error.Code);

          var roster = await new GetWorkspaceRosterHandler(
                  fixture.Appointments, fixture.Attendees, fixture.Events, fixture.Profiles)
              .HandleAsync(fixture.Events.Items.Single().Id, fixture.MedStaff, CancellationToken.None);
          Assert.True(roster.IsSuccess);
          var json = JsonSerializer.Serialize(roster.Value);
          Assert.DoesNotContain(fixture.AttendeeId.ToString(), json);
          Assert.DoesNotContain(fixture.BookingId.ToString(), json);
      }

      [Fact]
      public async Task Med_profile_never_sees_fit_rows_and_csv_neutralises_formulas()
      {
          var fixture = RecoveryFixture.Create();
          var roster = await new GetWorkspaceRosterHandler(
                  fixture.Appointments, fixture.Attendees, fixture.Events, fixture.Profiles)
              .HandleAsync(fixture.Events.Items.Single().Id, fixture.MedStaff, CancellationToken.None);

          Assert.True(roster.IsSuccess);
          Assert.All(roster.Value, row => Assert.Equal("MED", row.ScopeTypeCode));

          var csv = RosterCsv.Render(
              [new WorkspaceRosterRow("=cmd", "+x", "MED", "Expected", null, 1)],
              ["Name", "Email", "Status", "CheckedInAt", "Version"]);
          Assert.Contains("'=cmd", csv);
          Assert.Contains("'+x", csv);
      }

      [Fact]
      public async Task Stale_appointment_version_returns_current_state()
      {
          var fixture = RecoveryFixture.Create();
          var appointment = RecoveryFixture.Apps(fixture).First();
          var status = StatusHandler(fixture);

          var result = await status.HandleAsync(new SetAppointmentStatusCommand(
              fixture.MedStaff, appointment.Id, "CheckedIn", 99), CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("version-conflict", result.Error.Code);
          Assert.Equal(1L, result.Error.Data!["currentVersion"]);
      }
  }
  ```

  The Dublin test's event is built by WithDublinBooking on the fixture: a Dublin event
  at 00:30 local on 15 June with its own booked appointment. At 23:30 UTC on 14 June the
  UTC date is the 14th but the Dublin local date is the 15th, so the zone-judged handler
  allows check-in where a UTC-judged one would refuse. The roster row carries the caller's
  scope type code (ScopeTypeCode) so the suite can assert MED-only visibility —
  it is the type code, not an identifier, and is safe to expose.

- [ ] **Step 2: Run.** Expected: FAIL to compile — the Recovery and Appointments handlers do
  not exist.

  ```bash
  dotnet test tests/EventBooking.Application.Tests --filter "FullyQualifiedName~Recovery|FullyQualifiedName~Appointments"
  ```

- [ ] **Step 3: Implement.** Create the handler, CSV and query files below in full; delete
  the two ported files. Scope every workspace read to the caller's type; judge every time
  rule in the location's zone.

  ```csharp
  // src/EventBooking.Application/Recovery/StartRecoveryHandler.cs (complete)
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Access;
  using EventBooking.Application.Common;
  using EventBooking.Application.Invites;
  using EventBooking.Domain.Access;
  using EventBooking.Domain.Attendees;
  using EventBooking.Domain.Audit;
  using EventBooking.Domain.Bookings;
  using EventBooking.Domain.Common;
  using EventBooking.Domain.Invites;
  using EventBooking.Domain.Notifications;

  namespace EventBooking.Application.Recovery;

  public sealed class StartRecoveryHandler(
      IAttendeeRepository attendees,
      IBookingRepository bookings,
      IBookingAppointmentRepository appointments,
      IInviteRepository invites,
      ILocationRepository locations,
      ISystemSettingsRepository settings,
      IEmailDeliveryRepository emails,
      IStaffAccessAuthorizer access,
      IUnitOfWork unitOfWork,
      IAuditLogger audit,
      IClock clock,
      IEventWindowZones zones,
      IEventEligibilityQuery eligibility,
      IInviteIssuer issuer)
  {
      public async Task<Result<StartRecoveryOutcome>> HandleAsync(
          StartRecoveryCommand command, CancellationToken ct)
      {
          var authorized = await access.AuthorizeAsync(
              command.StaffUserId, StaffCapability.ManageAttendees, null, ct);
          if (authorized.IsFailure) return Result<StartRecoveryOutcome>.Failure(authorized.Error);

          await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
          var attendee = await attendees.LockForUpdateAsync(command.AttendeeId, ct);
          if (attendee is null)
              return Result<StartRecoveryOutcome>.Failure(Error.NotFound("No such attendee."));

          var activeOriginal = await bookings.LockActiveOriginalForAttendeeAsync(attendee.Id, ct);
          if (activeOriginal is null)
              return Result<StartRecoveryOutcome>.Failure(
                  Error.RecoveryNotAvailable("No active original booking can be recovered."));
          if (activeOriginal.AttendeeId != attendee.Id)
              return Result<StartRecoveryOutcome>.Failure(Error.NotFound("No such booking."));

          var pendingRecovery = await bookings.LockActiveRecoveryAsync(activeOriginal.Id, ct);
          if (pendingRecovery is not null)
              return Result<StartRecoveryOutcome>.Failure(
                  Error.RecoveryActive("A recovery is already active for this booking."));

          var journey = await bookings.ListJourneyAsync(activeOriginal.Id, ct);
          var attempts = new List<RecoveryAttempt>();
          foreach (var journeyBooking in journey)
          {
              var rows = await appointments.ListForBookingAsync(journeyBooking.Id, ct);
              attempts.AddRange(rows.Select(a => new RecoveryAttempt(
                  a.Id, a.AppointmentTypeId, a.Status, journeyBooking.CreatedAt)));
          }

          var pendingInvite = await invites.LockPendingForAttendeeAsync(attendee.Id, ct);
          var pendingTypes = pendingInvite?.RecoveryOfBookingId is not null
              ? pendingInvite.RequiredAppointmentTypeIds.ToList()
              : [];
          var recoverable = new RecoveryRequirementSelector().Select(
              attendee.RequiredAppointmentTypeIds, attempts, pendingTypes);
          if (recoverable.Count == 0)
              return Result<StartRecoveryOutcome>.Failure(
                  Error.RecoveryNotAvailable("Nothing remains to recover."));

          var configuration = await settings.GetAsync(ct);
          var originalInvite = await invites.GetAsync(activeOriginal.InviteId, ct);
          var locationIds = new List<Guid>();
          if (originalInvite is not null) locationIds.AddRange(originalInvite.LocationIds);
          foreach (var extra in command.AdditionalLocationIds.Distinct())
          {
              var location = await locations.GetAsync(extra, ct);
              if (location is null || !location.IsActive)
                  return Result<StartRecoveryOutcome>.Failure(
                      Error.Validation("Every additional location must exist and be active."));
              if (!locationIds.Contains(extra)) locationIds.Add(extra);
          }

          var fresh = await eligibility.FindEligibleEventsAsync(
              recoverable, locationIds, [], configuration.InviteOptionCount, clock.UtcNow, ct);
          if (fresh.Count < configuration.InviteOptionCount)
              return Result<StartRecoveryOutcome>.Failure(Error.InsufficientEvents(
                  $"Only {fresh.Count} eligible events for {configuration.InviteOptionCount} options."));

          var issued = await issuer.IssueRecoveryAsync(attendee, activeOriginal.Id, recoverable,
              locationIds, fresh, ActorType.Staff, command.StaffUserId.ToString(), ct);
          if (issued.IsFailure) return Result<StartRecoveryOutcome>.Failure(issued.Error);

          await unitOfWork.SaveChangesAsync(ct);
          await transaction.CommitAsync(ct);
          return Result<StartRecoveryOutcome>.Success(new StartRecoveryOutcome(
              issued.Value.InviteId, locationIds, recoverable));
      }
  }
  ```

  `Error.RecoveryNotAvailable` and `Error.RecoveryAlreadyPending` already exist on the
  shared type (verified in the prototype). The selector input reuses the Task 14 issuer
  only for issuing; selection stays pure. The original booking's location defaults the set
  even when the original invite row is gone; additional ids union without duplicates.

  ```csharp
  // src/EventBooking.Application/Recovery/CancelRecoveryInviteHandler.cs (complete):
  // authorize ManageAttendees; lock the invite by id; must be Pending with a
  // RecoveryOfBookingId (else validation "Only a recovery invite can be cancelled");
  // lock the attendee; invite.CancelRecovery(); audit RecoveryInviteCancelled as Staff;
  // save; commit.

  // src/EventBooking.Application/Recovery/ConcludeRecoveryHandler.cs (complete):
  // takes (IBookingRepository, IBookingAppointmentRepository, IUnitOfWork, IAuditLogger).
  // HandleAsync(bookingId): lock the booking; must be a non-original Active row (else
  // validation); load its appointments; all must be Completed or NoShow (else validation
  // "No appointments remain open"); booking.Conclude(); audit RecoveryBookingConcluded as
  // System; save; commit.
  ```

  ```csharp
  // src/EventBooking.Application/Appointments/WorkspaceEventHandlers.cs (complete)
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Access;
  using EventBooking.Application.Common;
  using EventBooking.Domain.Access;
  using EventBooking.Domain.Events;
  using EventBooking.Domain.Locations;

  namespace EventBooking.Application.Appointments;

  public sealed class ListWorkspaceEventsHandler(
      IEventRepository events,
      ILocationRepository locations,
      IStaffAccessAuthorizer access,
      IClock clock,
      IEventWindowZones zones)
  {
      public async Task<Result<IReadOnlyList<WorkspaceEventView>>> HandleAsync(
          ListWorkspaceEventsQuery query, CancellationToken ct)
      {
          var authorized = await access.AuthorizeAsync(
              query.StaffUserId, StaffCapability.ConductAppointments, null, ct);
          if (authorized.IsFailure) return Result<IReadOnlyList<WorkspaceEventView>>.Failure(authorized.Error);
          if (authorized.Value.AppointmentTypeId is not { } myType)
              return Result<IReadOnlyList<WorkspaceEventView>>.Failure(
                  Error.Forbidden("The workspace needs an assigned appointment type."));

          var zoneByLocation = (await locations.ListAsync(ct)).ToDictionary(l => l.Id);
          var now = clock.UtcNow;
          var from = now.AddDays(-7);
          var to = now.AddDays(14);

          var rows = (await events.ListActiveAsync(DateOnly.MinValue, ct))
              .Where(e => e.Status == EventStatus.Active
                  && e.Capacities.Any(c => c.AppointmentTypeId == myType)
                  && (query.LocationId is null || e.LocationId == query.LocationId)
                  && zoneByLocation.TryGetValue(e.LocationId, out var location)
                  && EventEnd(e, location.TimeZoneId, zones) >= from
                  && EventEnd(e, location.TimeZoneId, zones) <= to)
              .OrderBy(e => EventEnd(e, zoneByLocation[e.LocationId].TimeZoneId, zones))
              .ThenBy(e => e.Id)
              .Select(e => new WorkspaceEventView(e.Id, e.LocationId,
                  zoneByLocation[e.LocationId].Name,
                  e.Window.Date, e.Window.StartTime, e.Window.EndTime,
                  zones.AbbreviationOf(EventEnd(e, zoneByLocation[e.LocationId].TimeZoneId, zones),
                      zoneByLocation[e.LocationId].TimeZoneId)))
              .ToList();

          return Result<IReadOnlyList<WorkspaceEventView>>.Success(rows);
      }

      private static DateTimeOffset EventEnd(Event eventItem, string timeZoneId, IEventWindowZones zones) =>
          zones.InstantOf(eventItem.Window.Date, eventItem.Window.EndTime, timeZoneId);
  }
  ```

  The default event (nearest current or next) is the first row: the ordering by end
  instant then id puts it there without a special case. Grouping by location is a
  presentation concern over this ordered list.

  ```csharp
  // src/EventBooking.Application/Appointments/WorkspaceRosterHandlers.cs (complete):
  // GetWorkspaceRosterHandler(appointments, attendees, events, access):
  //   authorize ConductAppointments; scope type required (else forbidden). Load the event;
  //   refuse unless it lists the scope type. Load appointments for the event's bookings?
  //   The appointments port lists by booking, not by event: resolve booking ids through
  //   IBookingRepository.ListActiveForEventAsync plus journey rows? Keep to active
  //   bookings: rows = appointments.ListForBookingsAsync(activeBookingIds). Project one
  //   WorkspaceRosterRow per appointment with the attendee's name/email (loaded by id),
  //   the scope type code, status, CheckedInAt and Version. Never identifiers.
  // SetAppointmentStatusHandler(appointments, bookings, events, locations, access,
  //   unitOfWork, audit, clock, zones):
  //   authorize ConductAppointments; scope required. Lock the appointment; load its
  //   booking (read), event (read) and location (read). Parse the target status
  //   (unrecognised -> validation). Optimistic check: appointment.Version != expected ->
  //   version-conflict with current state. checkInAllowed = location local date of now ==
  //   window date; noShowAllowed = window ended in the location zone. Call TransitionTo;
  //   unchanged -> commit, return success without audit. Changed -> audit the appointment
  //   action matching the move (AppointmentCheckedIn / AppointmentMarkedNoShow /
  //   AppointmentCompleted / AppointmentStatusCorrected) as Staff; save; commit.
  //   Later-recovery guard: correcting NoShow -> Expected while a non-cancelled recovery
  //   booking exists for the root (LockActiveRecoveryAsync non-null) is refused with
  //   recovery-active. DownloadRosterHandler reuses the roster query and renders RosterCsv.
  ```

  ```csharp
  // src/EventBooking.Application/Appointments/RosterCsv.cs (complete)
  namespace EventBooking.Application.Appointments;

  public static class RosterCsv
  {
      // Neutralise formula cells: any cell starting with =, +, -, @, tab or carriage
      // return is prefixed with a single quote. Header included; CRLF line endings.
      public static string Render(IReadOnlyList<WorkspaceRosterRow> rows, IReadOnlyList<string> headers)
      {
          static string Cell(string value) =>
              value.Length > 0 && "=+-@\t\r".Contains(value[0]) ? "'" + value : value;
          var lines = new List<string> { string.Join(",", headers.Select(Cell)) };
          lines.AddRange(rows.Select(r => string.Join(",", new[]
          {
              Cell(r.Name), Cell(r.Email), Cell(r.AppointmentStatus),
              Cell(r.CheckedInAt?.ToString("o") ?? string.Empty), Cell(r.Version.ToString()),
          })));
          return string.Join("\r\n", lines) + "\r\n";
      }
  }
  ```

  `WorkspaceQueries.cs` (Infrastructure): the event-window read model behind the workspace
  list — parameterised SQL over events joined to locations with the same 7-day/14-day
  bounds, keyset-paginated; the handler above filters in memory only in the Application
  tests, while production resolves the scope through this query. Concretely the query
  exposes `ListWorkspaceEventIdsAsync(typeId, locationId, from, to, cursor, limit)`
  returning ordered ids the handler hydrates; the in-memory path is the fakes. The
  Infrastructure suite seeds events across locations and asserts bounds, scope and paging
  against real PostgreSQL.

- [ ] **Step 4: Run.** Expected: PASS — the new suites plus the full solution.

  ```bash
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  ```

  Expect the Application count to rise (new suites) and Infrastructure to rise (workspace
  query tests). A count that does not match the executor's own before/after diff is a
  signal to read the diff, not to adjust the number.

- [ ] **Step 5: Commit and push** the executor's code — not the plan documents — under the
  master plan's message:

  ```bash
  test -z "$(git status --porcelain --ignored=no | grep -v '^??')"
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  git add src/EventBooking.Application/Common/Error.cs src/EventBooking.Application/Recovery/ src/EventBooking.Application/Appointments/ src/EventBooking.Application/Invites/StartRecoveryHandler.cs src/EventBooking.Application/Invites/CancelRecoveryInviteHandler.cs src/EventBooking.Infrastructure/Persistence/Queries/WorkspaceQueries.cs tests/EventBooking.Application.Tests/Recovery/ tests/EventBooking.Application.Tests/Appointments/ tests/EventBooking.Infrastructure.Tests/Queries/WorkspaceQueryTests.cs
  git diff --cached --name-only
  git diff --cached
  test -n "$EXECUTOR_COAUTHOR"
  git commit -m "feat(app): recovery and workspace across locations

  Co-authored-by: $EXECUTOR_COAUTHOR"
  git push
  ```
