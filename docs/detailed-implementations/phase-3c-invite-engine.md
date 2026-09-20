# 03c — Location-restricted invite engine (Task 14)

[← Phase overview](phase-3-application.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This task follows Task 13. The ported trigger-invite and expire-invites logic becomes the
generalised invite engine: every path that creates an invite goes through one internal
issuer, options come from the Task 11 eligibility port in UTC order, and the invite carries
the location set the Coordinator selected plus the Task 12 settings snapshot.

> Use superpowers:executing-plans. This task is hand-authored: complete code and complete tests
> are written straight into this document, with no prototype. Compile and test-drive them
> yourself. The test counts below are what you should expect to reach, not figures observed by
> the author — nothing here has been run.

**Goal:** InviteAttendee (attendee id, location ids) returning `Invited` or
`AwaitingAvailability` (`insufficient-events`); CountEligibleEventsForAttendee (attendee id,
location ids) for the dialog; one internal issuer used by every path that creates an invite
(initial, re-invite, reissue after expiry, top-up replacement, cancellation replacement,
recovery); ExpireInvite as a sweep step run per item; TopUpInviteOptions called on view and
on confirm. The issuer always supersedes the journey's pending invite, snapshots the Task 12
settings values onto the new invite, and stages the right template (`AttendeeInvite` for an
initial invite, `AttendeeReinvite` for a reissue) as one pending outbox row in the
transaction. Parking an attendee for lack of options writes no audit entry — there is no
business entity created, following the ported issuer's precedent.

**Architecture:** One transaction per command; the attendee lock is taken first, then the
invite row where one exists. The issuer never saves and never sends — the caller owns the
unit of work and the outbox row commits with the business change; the Task 18 dispatcher
sends it. Expiry reissues with the same location set and `retryCount + 1` while retries
remain, else moves the attendee to `NoResponseNeedsFollowUp` with a reason in the audit. The
fixed three-option count retires: the option count comes from the snapshotted
InviteOptionCount, and `Invite.RequiredOptionCount` is deleted with its two validations.
The transitional-location restriction retires: the location set comes from the command. No
domain entity leaves the handler.

**Tech Stack:** .NET 10, xUnit, EF Core, PostgreSQL Testcontainers.

**Spec:** [Master Task 14](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[domain model](../design/01-domain-model.md), [functional requirements](../design/02-functional-requirements.md),
[solution architecture](../design/04-solution-architecture.md), [ontology](../ontology.md).

## Boundary

Task 14 owns the invite engine only. The ported immediate-send delivery path retires:
issuing stages a pending outbox row and the Task 18 dispatcher sends it. Recovery paths keep
their Task 8 factories; StartRecovery stays with Task 16 — this task's issuer only exposes
the recovery-issue operation it calls. Event-cancellation replacement (Task 15) calls the
same issuer; this task defines the operation it will call but does not implement
cancellation. EligibleEventFinder stays untouched for its five existing callers.

## Global constraints

One transaction per command; attendee lock first; audit in the transaction; no domain entity
leaves the handler; exactly one StaffCapability per handler (`ManageAttendees` for the
coordinator trigger and top-up, no capability for the System-actor sweep step). An unknown
or inactive location in the selection is refused and nothing is created. Expiry loads the
invite by id and never attaches a second copy, so it works while the same invite is already
tracked in the unit of work. Valid settings never alter existing invites — the issuer reads
settings only to snapshot them onto the new row.

## Review focus

STOP AND CHECK four things. The option count is the snapshotted settings value, not a
constant — inviting with 3 eligible events and option count 3 creates 3 options in UTC
order with `expiresAt` equal to now plus `inviteExpiryDays`. With 2 eligible events there is
no invite, the status is `AwaitingAvailability`, and the outcome is `insufficient-events`.
Expiry at the retry limit — and a failed reissue — both yield `NoResponseNeedsFollowUp`,
the latter with a reason in the audit. And top-up excludes events already offered and the
just-cancelled booking's event, audits `InviteOptionReplaced`, and sets
`NoResponseNeedsFollowUp` when still short.

### Task 14: Location-restricted invite engine

**Files:**

- Create: src/EventBooking.Application/Invites/InviteAttendeeHandler.cs
- Create: src/EventBooking.Application/Invites/CountEligibleEventsHandler.cs
- Create: src/EventBooking.Application/Invites/InviteIssuer.cs (new file; the ported file
  below is deleted first, so there is exactly one InviteIssuer.cs at the end)
- Create: src/EventBooking.Application/Invites/ExpireInviteHandler.cs
- Create: src/EventBooking.Application/Invites/TopUpInviteOptionsHandler.cs
- Create: src/EventBooking.Application/Invites/IInviteIssuer.cs
- Delete: src/EventBooking.Application/Invites/TriggerInviteHandler.cs
- Delete: src/EventBooking.Application/Invites/ExpireInvitesHandler.cs
- Delete: src/EventBooking.Application/Invites/InviteIssuer.cs (ported immediate-send version;
  deleted before the new file of the same name is created)
- Modify: src/EventBooking.Domain/Invites/Invite.cs (delete RequiredOptionCount and its two
  validations; option count travels on each call)
- Test: tests/EventBooking.Application.Tests/Invites/InviteFixture.cs (StubEligibility,
  fixture)
- Test: tests/EventBooking.Application.Tests/Invites/InviteAttendeeHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Invites/ExpireInviteHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Invites/TopUpInviteOptionsHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Invites/InviteIssuerTests.cs

**Interfaces:**

```csharp
namespace EventBooking.Application.Invites;

public sealed record InviteAttendeeCommand(Guid StaffUserId, Guid AttendeeId, IReadOnlyList<Guid> LocationIds);
public sealed record InviteAttendeeOutcome(Guid AttendeeId, string Status, Guid? InviteId);
public sealed record CountEligibleEventsQuery(Guid StaffUserId, Guid AttendeeId, IReadOnlyList<Guid> LocationIds);
public sealed record ExpireInviteCommand(Guid InviteId);
public sealed record TopUpInviteOptionsCommand(Guid StaffUserId, Guid InviteId, Guid FilledEventId, Guid? ExcludeEventId);

// The one place invites are created. Never saves, never sends: the caller owns the unit of
// work and commits the staged outbox row with the business change. Template and actor
// travel typed (EmailTemplate, ActorType).
public interface IInviteIssuer
{
    Task<Result<InviteIssueOutcome>> IssueInitialAsync(
        Attendee attendee, IReadOnlyList<Guid> locationIds, EmailTemplate template,
        ActorType actor, string? actorId, CancellationToken ct);
    Task<Result<InviteIssueOutcome>> IssueReissueAsync(
        Invite expired, IReadOnlyList<Guid> freshEventIds,
        ActorType actor, string? actorId, CancellationToken ct);
    Task<Result<InviteIssueOutcome>> IssueRecoveryAsync(
        Attendee attendee, Guid rootBookingId, IReadOnlyList<Guid> selectedTypeIds,
        IReadOnlyList<Guid> locationIds, IReadOnlyList<Guid> freshEventIds,
        ActorType actor, string? actorId, CancellationToken ct);
}

public sealed record InviteIssueOutcome(Guid InviteId, int OptionCount, DateTimeOffset ExpiresAt, int RetryCount);
```

- [ ] **Step 1: Write the failing tests.** Create the five test files below in full. The
  fixture seeds an attendee needing MED and FIT, two locations, settings with option count
  3, and a stub eligibility port returning preset event ids in order (no event hydration:
  the issuer needs ids only).

  ```csharp
  // tests/EventBooking.Application.Tests/Invites/InviteFixture.cs (complete)
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Access;
  using EventBooking.Application.Tests.Fakes;
  using EventBooking.Domain.Access;
  using EventBooking.Domain.AppointmentTypes;
  using EventBooking.Domain.Attendees;
  using EventBooking.Domain.AttendeeGroups;
  using EventBooking.Domain.Locations;
  using EventBooking.Domain.Time;

  namespace EventBooking.Application.Tests.Invites;

  public sealed class StubEligibility : IEventEligibilityQuery
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
          Task.FromResult(EligibleInOrder.Count);
  }

  public sealed class InviteFixture
  {
      public static readonly DateTimeOffset Now = new(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);

      public InMemoryAttendeeRepository Attendees = new();
      public InMemoryInviteRepository Invites = new();
      public InMemoryLocationRepository Locations = new();
      public InMemoryAppointmentTypeRepository Types = new();
      public InMemoryAttendeeGroupRepository Groups = new();
      public InMemorySystemSettingsRepository Settings = new();
      public InMemoryEmailDeliveryRepository Emails = new();
      public InMemoryStaffAccessProfileRepository Profiles = new();
      public FakeUnitOfWork UnitOfWork = new();
      public RecordingAuditLogger Audit = new();
      public FakeClock Clock = new(Now);
      public StubEligibility Eligibility = new();

      public Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
      public Guid AttendeeId;
      public Guid MedId;
      public Guid FitId;
      public List<Guid> LocationIds = [];

      public static InviteFixture Create(int optionCount = 3)
      {
          var fixture = new InviteFixture();
          fixture.Types.Items.Clear();
          var med = AppointmentType.Create(Guid.NewGuid(), "MED", "Medical");
          var fit = AppointmentType.Create(Guid.NewGuid(), "FIT", "Fitness");
          fixture.Types.Items.AddRange([med, fit]);
          fixture.MedId = med.Id;
          fixture.FitId = fit.Id;
          var group = AttendeeGroup.Create(Guid.NewGuid(), "NHS", "NHS staff", [med.Id, fit.Id], [med.Id, fit.Id]);
          fixture.Groups.Items.Add(group);
          foreach (var code in new[] { "LONDON_HQ", "TOKYO" })
          {
              var location = Location.Create(Guid.NewGuid(), code, code, "1 High St",
                  code == "LONDON_HQ" ? "Europe/London" : "Asia/Tokyo", TestZonesDouble.Instance);
              fixture.Locations.Items.Add(location);
              fixture.LocationIds.Add(location.Id);
          }

          var attendee = Attendee.Create(Guid.NewGuid(), "Amy", "amy@example.invalid", group, Now);
          fixture.Attendees.Items.Add(attendee);
          fixture.AttendeeId = attendee.Id;
          fixture.Profiles.Add(StaffAccessProfile.Create(fixture.Coordinator, Role.Coordinator, null));
          fixture.Settings.Settings.Update(7, 2, optionCount);
          return fixture;
      }

      public InviteFixture WithEligibleEvents(int count)
      {
          Eligibility.EligibleInOrder = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();
          return this;
      }

      public InviteFixture WithInactiveLocation()
      {
          Locations.Items[0].Deactivate(LocationUsage.None);
          return this;
      }
  }

  public sealed class TestZonesDouble : IEventWindowZones
  {
      public static readonly TestZonesDouble Instance = new();
      public bool IsKnownZone(string timeZoneId) =>
          timeZoneId is "Europe/London" or "Asia/Tokyo";
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

  `Settings.Update(7, 2, optionCount)`
  sets the snapshot source; Version becomes 2, which the tests never assert. The fixture
  needs `using EventBooking.Domain.Locations;` (LocationUsage), `using
  EventBooking.Domain.Time;` (the zones port) and `using EventBooking.Domain.AppointmentTypes;`.

  ```csharp
  // tests/EventBooking.Application.Tests/Invites/InviteAttendeeHandlerTests.cs (complete)
  using EventBooking.Application.Common;
  using EventBooking.Application.Invites;
  using EventBooking.Domain.Audit;
  using EventBooking.Domain.Notifications;

  namespace EventBooking.Application.Tests.Invites;

  public sealed class InviteAttendeeHandlerTests
  {
      private static InviteAttendeeHandler Handler(InviteFixture f) => new(
          f.Attendees, f.Invites, f.Locations, f.Eligibility, f.Settings, f.Emails,
          f.Profiles, f.UnitOfWork, f.Audit, f.Clock,
          new InviteIssuer(f.Invites, f.Settings, f.Emails, f.Audit, f.Clock, f.Eligibility));

      [Fact]
      public async Task Invite_with_full_options_creates_invite_and_stages_email()
      {
          var fixture = InviteFixture.Create(optionCount: 3).WithEligibleEvents(3);

          var result = await Handler(fixture).HandleAsync(
              new InviteAttendeeCommand(fixture.Coordinator, fixture.AttendeeId, fixture.LocationIds),
              CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal("Invited", result.Value.Status);
          var invite = fixture.Invites.Items.Single(i => i.Id == result.Value.InviteId);
          Assert.Equal(fixture.Eligibility.EligibleInOrder, invite.Options.Select(o => o.EventId).ToList());
          Assert.Equal(fixture.LocationIds.Order().ToList(), invite.LocationIds.Order().ToList());
          Assert.Equal(InviteFixture.Now.AddDays(7), invite.ExpiresAt);
          Assert.Equal(3, invite.InviteOptionCount);
          Assert.Equal("Invited", fixture.Attendees.Items.Single().Status.ToString());
          var delivery = Assert.Single(fixture.Emails.Items);
          Assert.Equal(EmailTemplate.AttendeeInvite, delivery.TemplateName);
          Assert.Equal(EmailStatus.Pending, delivery.Status);
          Assert.Equal(invite.Id, delivery.InviteId);
          var entry = Assert.Single(fixture.Audit.Entries);
          Assert.Equal(AuditAction.InviteCreated, entry.Action);
      }

      [Fact]
      public async Task Invite_short_of_options_parks_attendee_with_no_side_effects()
      {
          var fixture = InviteFixture.Create(optionCount: 3).WithEligibleEvents(2);

          var result = await Handler(fixture).HandleAsync(
              new InviteAttendeeCommand(fixture.Coordinator, fixture.AttendeeId, fixture.LocationIds),
              CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("insufficient-events", result.Error.Code);
          Assert.Equal("AwaitingAvailability", fixture.Attendees.Items.Single().Status.ToString());
          Assert.Empty(fixture.Invites.Items);
          Assert.Empty(fixture.Emails.Items);
          Assert.Empty(fixture.Audit.Entries);
      }

      [Fact]
      public async Task Inactive_location_refuses_with_nothing_created()
      {
          var fixture = InviteFixture.Create(optionCount: 3).WithEligibleEvents(3).WithInactiveLocation();

          var result = await Handler(fixture).HandleAsync(
              new InviteAttendeeCommand(fixture.Coordinator, fixture.AttendeeId, fixture.LocationIds),
              CancellationToken.None);

          Assert.True(result.IsFailure);
          Assert.Equal("validation", result.Error.Code);
          Assert.Empty(fixture.Invites.Items);
          Assert.Empty(fixture.Emails.Items);
      }

      [Fact]
      public async Task Reinvite_supersedes_the_pending_invite()
      {
          var fixture = InviteFixture.Create(optionCount: 3).WithEligibleEvents(3);
          var first = await Handler(fixture).HandleAsync(
              new InviteAttendeeCommand(fixture.Coordinator, fixture.AttendeeId, fixture.LocationIds),
              CancellationToken.None);
          fixture.Attendees.Items.Single().MarkNoResponse(InviteFixture.Now);

          var result = await Handler(fixture).HandleAsync(
              new InviteAttendeeCommand(fixture.Coordinator, fixture.AttendeeId, [fixture.LocationIds[1]]),
              CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal(Domain.Invites.InviteStatus.Superseded,
              fixture.Invites.Items.Single(i => i.Id == first.Value.InviteId).Status);
          Assert.Equal([fixture.LocationIds[1]],
              fixture.Invites.Items.Single(i => i.Id == result.Value.InviteId).LocationIds.ToList());
      }
  }
  ```

  ```csharp
  // tests/EventBooking.Application.Tests/Invites/ExpireInviteHandlerTests.cs (complete)
  using EventBooking.Application.Common;
  using EventBooking.Application.Invites;
  using EventBooking.Domain.Audit;
  using EventBooking.Domain.Invites;
  using EventBooking.Domain.Notifications;

  namespace EventBooking.Application.Tests.Invites;

  public sealed class ExpireInviteHandlerTests
  {
      private static ExpireInviteHandler Handler(InviteFixture f) => new(
          f.Invites, f.Attendees, f.Eligibility, f.Settings, f.Emails,
          f.UnitOfWork, f.Audit, f.Clock,
          new InviteIssuer(f.Invites, f.Settings, f.Emails, f.Audit, f.Clock, f.Eligibility));

      private static async Task<Guid> IssuedInviteAsync(InviteFixture fixture)
      {
          var trigger = new InviteAttendeeHandler(
              fixture.Attendees, fixture.Invites, fixture.Locations, fixture.Eligibility,
              fixture.Settings, fixture.Emails, fixture.Profiles, fixture.UnitOfWork,
              fixture.Audit, fixture.Clock,
              new InviteIssuer(fixture.Invites, fixture.Settings, fixture.Emails, fixture.Audit, fixture.Clock, fixture.Eligibility));
          fixture.WithEligibleEvents(3);
          var result = await trigger.HandleAsync(
              new InviteAttendeeCommand(fixture.Coordinator, fixture.AttendeeId, fixture.LocationIds),
              CancellationToken.None);
          Assert.True(result.IsSuccess);
          return result.Value.InviteId!.Value;
      }

      [Fact]
      public async Task Expiry_below_limit_reissues_with_same_locations_and_retry_plus_one()
      {
          var fixture = InviteFixture.Create(optionCount: 3);
          var inviteId = await IssuedInviteAsync(fixture);
          fixture.WithEligibleEvents(3);
          fixture.Clock.UtcNow = InviteFixture.Now.AddDays(8);

          var result = await Handler(fixture).HandleAsync(new ExpireInviteCommand(inviteId), CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal(InviteStatus.Expired, fixture.Invites.Items.Single(i => i.Id == inviteId).Status);
          var reissued = fixture.Invites.Items.Single(i => i.Id != inviteId);
          Assert.Equal(fixture.LocationIds.Order().ToList(), reissued.LocationIds.Order().ToList());
          Assert.Equal(1, reissued.RetryCount);
          var delivery = Assert.Single(fixture.Emails.Items, e => e.InviteId == reissued.Id);
          Assert.Equal(EmailTemplate.AttendeeReinvite, delivery.TemplateName);
          Assert.Contains(fixture.Audit.Entries, e => e.Action == AuditAction.InviteExpired);
      }

      [Fact]
      public async Task Expiry_at_limit_yields_no_response_follow_up()
      {
          var fixture = InviteFixture.Create(optionCount: 3);
          var inviteId = await IssuedInviteAsync(fixture);
          var invite = fixture.Invites.Items.Single(i => i.Id == inviteId);
          invite.MarkExpired();
          var second = Invite.Reissue(Guid.NewGuid(), invite, InviteFixture.Now.AddDays(7),
              fixture.Eligibility.EligibleInOrder.Take(3));
          fixture.Invites.Items.Add(second);
          fixture.Invites.Items.Remove(invite);
          second.MarkExpired();
          var third = Invite.Reissue(Guid.NewGuid(), second, InviteFixture.Now.AddDays(7),
              fixture.Eligibility.EligibleInOrder.Take(3));
          fixture.Invites.Items.Add(third);
          fixture.Invites.Items.Remove(second);
          fixture.Clock.UtcNow = InviteFixture.Now.AddDays(30);

          var result = await Handler(fixture).HandleAsync(new ExpireInviteCommand(third.Id), CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal("NoResponseNeedsFollowUp", fixture.Attendees.Items.Single().Status.ToString());
      }

      [Fact]
      public async Task Failed_reissue_yields_no_response_with_reason_in_audit()
      {
          var fixture = InviteFixture.Create(optionCount: 3);
          var inviteId = await IssuedInviteAsync(fixture);
          fixture.WithEligibleEvents(1);
          fixture.Clock.UtcNow = InviteFixture.Now.AddDays(8);

          var result = await Handler(fixture).HandleAsync(new ExpireInviteCommand(inviteId), CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal("NoResponseNeedsFollowUp", fixture.Attendees.Items.Single().Status.ToString());
          var entry = Assert.Single(fixture.Audit.Entries.Where(e => e.Action == AuditAction.InviteExpired));
          Assert.Contains("insufficient", entry.Details);
      }
  }
  ```

  The at-limit test walks three expiries because the default MaxAutoRetryCount is 2:
  retries 0 and 1 reissue, retry 2 does not. `Invite.Reissue` carries the location set,
  requirements and `retryCount + 1` itself.

  ```csharp
  // tests/EventBooking.Application.Tests/Invites/TopUpInviteOptionsHandlerTests.cs (complete)
  using EventBooking.Application.Common;
  using EventBooking.Application.Invites;
  using EventBooking.Domain.Audit;

  namespace EventBooking.Application.Tests.Invites;

  public sealed class TopUpInviteOptionsHandlerTests
  {
      [Fact]
      public async Task Top_up_replaces_filled_option_and_audits_replacement()
      {
          var fixture = InviteFixture.Create(optionCount: 3).WithEligibleEvents(4);
          var trigger = new InviteAttendeeHandler(
              fixture.Attendees, fixture.Invites, fixture.Locations, fixture.Eligibility,
              fixture.Settings, fixture.Emails, fixture.Profiles, fixture.UnitOfWork,
              fixture.Audit, fixture.Clock,
              new InviteIssuer(fixture.Invites, fixture.Settings, fixture.Emails, fixture.Audit, fixture.Clock, fixture.Eligibility));
          var issued = await trigger.HandleAsync(
              new InviteAttendeeCommand(fixture.Coordinator, fixture.AttendeeId, fixture.LocationIds),
              CancellationToken.None);
          var inviteId = issued.Value.InviteId!.Value;
          var filled = fixture.Eligibility.EligibleInOrder[0];
          var fresh = fixture.Eligibility.EligibleInOrder[3];
          var handler = new TopUpInviteOptionsHandler(
              fixture.Invites, fixture.Attendees, fixture.Eligibility, fixture.Profiles,
              fixture.UnitOfWork, fixture.Audit, fixture.Clock);

          var result = await handler.HandleAsync(
              new TopUpInviteOptionsCommand(fixture.Coordinator, inviteId, filled, ExcludeEventId: null),
              CancellationToken.None);

          Assert.True(result.IsSuccess);
          var options = fixture.Invites.Items.Single(i => i.Id == inviteId).Options.Select(o => o.EventId).ToList();
          Assert.DoesNotContain(filled, options);
          Assert.Contains(fresh, options);
          Assert.Contains(fixture.Audit.Entries, e => e.Action == AuditAction.InviteOptionReplaced);
      }

      [Fact]
      public async Task Top_up_with_nothing_eligible_sets_no_response_follow_up()
      {
          var fixture = InviteFixture.Create(optionCount: 3).WithEligibleEvents(3);
          var trigger = new InviteAttendeeHandler(
              fixture.Attendees, fixture.Invites, fixture.Locations, fixture.Eligibility,
              fixture.Settings, fixture.Emails, fixture.Profiles, fixture.UnitOfWork,
              fixture.Audit, fixture.Clock,
              new InviteIssuer(fixture.Invites, fixture.Settings, fixture.Emails, fixture.Audit, fixture.Clock, fixture.Eligibility));
          var issued = await trigger.HandleAsync(
              new InviteAttendeeCommand(fixture.Coordinator, fixture.AttendeeId, fixture.LocationIds),
              CancellationToken.None);
          fixture.WithEligibleEvents(0);
          var handler = new TopUpInviteOptionsHandler(
              fixture.Invites, fixture.Attendees, fixture.Eligibility, fixture.Profiles,
              fixture.UnitOfWork, fixture.Audit, fixture.Clock);

          var result = await handler.HandleAsync(
              new TopUpInviteOptionsCommand(
                  fixture.Coordinator, issued.Value.InviteId!.Value,
                  fixture.Eligibility.EligibleInOrder.FirstOrDefault(), ExcludeEventId: null),
              CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal("NoResponseNeedsFollowUp", fixture.Attendees.Items.Single().Status.ToString());
      }
  }
  ```

  The short test passes a filled id that is no longer offered; the handler removes it only
  when present and still tops up from whatever is eligible. With zero eligible events the
  attendee moves to `NoResponseNeedsFollowUp`.

  ```csharp
  // tests/EventBooking.Application.Tests/Invites/InviteIssuerTests.cs (complete)
  using EventBooking.Application.Invites;
  using EventBooking.Domain.Audit;
  using EventBooking.Domain.Notifications;

  namespace EventBooking.Application.Tests.Invites;

  public sealed class InviteIssuerTests
  {
      [Fact]
      public async Task Issuer_snapshots_settings_and_supersedes_pending()
      {
          var fixture = InviteFixture.Create(optionCount: 2).WithEligibleEvents(2);
          var issuer = new InviteIssuer(fixture.Invites, fixture.Settings, fixture.Emails, fixture.Audit, fixture.Clock, fixture.Eligibility);
          var attendee = fixture.Attendees.Items.Single();

          var first = await issuer.IssueInitialAsync(
              attendee, fixture.LocationIds, EmailTemplate.AttendeeInvite, ActorType.Staff,
              fixture.Coordinator.ToString(), CancellationToken.None);
          var second = await issuer.IssueInitialAsync(
              attendee, [fixture.LocationIds[0]], EmailTemplate.AttendeeInvite, ActorType.Staff,
              fixture.Coordinator.ToString(), CancellationToken.None);

          Assert.True(first.IsSuccess);
          Assert.True(second.IsSuccess);
          var firstInvite = fixture.Invites.Items.Single(i => i.Id == first.Value.InviteId);
          Assert.Equal(Domain.Invites.InviteStatus.Superseded, firstInvite.Status);
          var secondInvite = fixture.Invites.Items.Single(i => i.Id == second.Value.InviteId);
          Assert.Equal(2, secondInvite.InviteOptionCount);
          Assert.Equal(7, secondInvite.InviteExpiryDays);
          Assert.Equal(2, secondInvite.MaxAutoRetryCount);
          Assert.Equal(InviteFixture.Now.AddDays(7), secondInvite.ExpiresAt);
      }
  }
  ```

- [ ] **Step 2: Run.** Expected: FAIL to compile — the Invites handlers do not exist yet.

  ```bash
  dotnet test tests/EventBooking.Application.Tests --filter "FullyQualifiedName~Invites"
  ```

- [ ] **Step 3: Implement.** Add the shared error factory, then the issuer and the four
  handlers below in full. Delete the three ported files. Remove RequiredOptionCount and
  its two validations from the invite aggregate — the count travels on each creation call
  (Task 12's optional parameters stay as the signature; only the constant and the two
  guards go).

  ```csharp
  // Error.cs: add beside the Task 12 and 13 factories.
  public const string InsufficientEventsCode = "insufficient-events";
  public static Error InsufficientEvents(string message) => new(InsufficientEventsCode, message);
  ```

  ```csharp
  // src/EventBooking.Application/Invites/IInviteIssuer.cs — the contract from Interfaces above,
  // except the template and actor travel typed: EmailTemplate and ActorType, not strings.
  // src/EventBooking.Application/Invites/InviteIssuer.cs (complete)
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Common;
  using EventBooking.Domain.Attendees;
  using EventBooking.Domain.Audit;
  using EventBooking.Domain.Common;
  using EventBooking.Domain.Invites;
  using EventBooking.Domain.Notifications;

  namespace EventBooking.Application.Invites;

  public sealed class InviteIssuer(
      IInviteRepository invites,
      ISystemSettingsRepository settings,
      IEmailDeliveryRepository emails,
      IAuditLogger audit,
      IClock clock,
      IEventEligibilityQuery eligibility) : IInviteIssuer
  {
      public async Task<Result<InviteIssueOutcome>> IssueInitialAsync(
          Attendee attendee, IReadOnlyList<Guid> locationIds, EmailTemplate template,
          ActorType actor, string? actorId, CancellationToken ct)
      {
          var configuration = await settings.GetAsync(ct);
          var found = await eligibility.FindEligibleEventsAsync(
              attendee.RequiredAppointmentTypeIds, locationIds, [], configuration.InviteOptionCount,
              clock.UtcNow, ct);
          if (found.Count < configuration.InviteOptionCount)
              return Result<InviteIssueOutcome>.Failure(Error.InsufficientEvents(
                  $"Only {found.Count} eligible events for {configuration.InviteOptionCount} options."));

          var pending = await invites.LockPendingForAttendeeAsync(attendee.Id, ct);
          pending?.MarkSuperseded();

          var invite = Invite.CreateInitial(Guid.NewGuid(), attendee.Id,
              clock.UtcNow.AddDays(configuration.InviteExpiryDays), locationIds, found,
              attendee.RequiredAppointmentTypeIds, 0,
              configuration.InviteExpiryDays, configuration.MaxAutoRetryCount, configuration.InviteOptionCount);
          invites.Add(invite);
          attendee.MarkInvited(clock.UtcNow);
          audit.Record(AuditEntityTypes.Invite, invite.Id, AuditAction.InviteCreated,
              actor, actorId, "retry 0");
          emails.Add(EmailLog.RecordPending(Guid.NewGuid(), attendee.Id, template, clock.UtcNow, inviteId: invite.Id));
          return Result<InviteIssueOutcome>.Success(new InviteIssueOutcome(
              invite.Id, invite.Options.Count, invite.ExpiresAt, invite.RetryCount));
      }

      public async Task<Result<InviteIssueOutcome>> IssueReissueAsync(
          Invite expired, IReadOnlyList<Guid> freshEventIds,
          ActorType actor, string? actorId, CancellationToken ct)
      {
          var invite = Invite.Reissue(Guid.NewGuid(), expired,
              clock.UtcNow.AddDays(expired.InviteExpiryDays), freshEventIds);
          invites.Add(invite);
          emails.Add(EmailLog.RecordPending(Guid.NewGuid(), invite.AttendeeId,
              EmailTemplate.AttendeeReinvite, clock.UtcNow, inviteId: invite.Id));
          audit.Record(AuditEntityTypes.Invite, invite.Id, AuditAction.InviteCreated,
              actor, actorId, $"retry {invite.RetryCount}");
          return Result<InviteIssueOutcome>.Success(new InviteIssueOutcome(
              invite.Id, invite.Options.Count, invite.ExpiresAt, invite.RetryCount));
      }

      public async Task<Result<InviteIssueOutcome>> IssueRecoveryAsync(
          Attendee attendee, Guid rootBookingId, IReadOnlyList<Guid> selectedTypeIds,
          IReadOnlyList<Guid> locationIds, IReadOnlyList<Guid> freshEventIds,
          ActorType actor, string? actorId, CancellationToken ct)
      {
          var configuration = await settings.GetAsync(ct);
          if (freshEventIds.Count != configuration.InviteOptionCount)
              return Result<InviteIssueOutcome>.Failure(Error.InsufficientEvents(
                  "A recovery invite must offer exactly the configured option count."));

          var invite = Invite.CreateRecovery(Guid.NewGuid(), attendee.Id, rootBookingId,
              clock.UtcNow.AddDays(configuration.InviteExpiryDays),
              locationIds[0], locationIds.Skip(1).ToList(), freshEventIds, selectedTypeIds,
              configuration.InviteExpiryDays, configuration.MaxAutoRetryCount, configuration.InviteOptionCount);
          invites.Add(invite);
          emails.Add(EmailLog.RecordPending(Guid.NewGuid(), attendee.Id,
              EmailTemplate.AttendeeInvite, clock.UtcNow, inviteId: invite.Id));
          audit.Record(AuditEntityTypes.Invite, invite.Id, AuditAction.RecoveryInviteCreated,
              actor, actorId, $"root {rootBookingId}");
          return Result<InviteIssueOutcome>.Success(new InviteIssueOutcome(
              invite.Id, invite.Options.Count, invite.ExpiresAt, invite.RetryCount));
      }
  }
  ```

  IssueInitialAsync reports shortfall instead of parking: the caller decides (the
  trigger parks, expiry moves on).

  ```csharp
  // src/EventBooking.Application/Invites/InviteAttendeeHandler.cs (complete)
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Access;
  using EventBooking.Application.Common;
  using EventBooking.Domain.Access;
  using EventBooking.Domain.Attendees;
  using EventBooking.Domain.Audit;
  using EventBooking.Domain.Common;
  using EventBooking.Domain.Notifications;

  namespace EventBooking.Application.Invites;

  public sealed class InviteAttendeeHandler(
      IAttendeeRepository attendees,
      IInviteRepository invites,
      ILocationRepository locations,
      IEventEligibilityQuery eligibility,
      ISystemSettingsRepository settings,
      IEmailDeliveryRepository emails,
      IStaffAccessAuthorizer access,
      IUnitOfWork unitOfWork,
      IAuditLogger audit,
      IClock clock,
      IInviteIssuer issuer)
  {
      public async Task<Result<InviteAttendeeOutcome>> HandleAsync(
          InviteAttendeeCommand command, CancellationToken ct)
      {
          var authorized = await access.AuthorizeAsync(
              command.StaffUserId, StaffCapability.ManageAttendees, null, ct);
          if (authorized.IsFailure) return Result<InviteAttendeeOutcome>.Failure(authorized.Error);

          await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
          var attendee = await attendees.LockForUpdateAsync(command.AttendeeId, ct);
          if (attendee is null)
              return Result<InviteAttendeeOutcome>.Failure(Error.NotFound("No such attendee."));

          if (attendee.Status is not (AttendeeStatus.NotYetInvited
              or AttendeeStatus.AwaitingAvailability
              or AttendeeStatus.NoResponseNeedsFollowUp))
              return Result<InviteAttendeeOutcome>.Failure(
                  Error.Conflict($"The attendee is {attendee.Status} and cannot be invited."));

          var locationIds = command.LocationIds.Distinct().ToList();
          if (locationIds.Count is < 1 or > 50)
              return Result<InviteAttendeeOutcome>.Failure(
                  Error.Validation("Select between 1 and 50 locations."));
          foreach (var locationId in locationIds)
          {
              var location = await locations.GetAsync(locationId, ct);
              if (location is null || !location.IsActive)
                  return Result<InviteAttendeeOutcome>.Failure(
                      Error.Validation("Every selected location must exist and be active."));
          }

          if (attendee.RequiredAppointmentTypeIds.Count == 0)
              return Result<InviteAttendeeOutcome>.Failure(
                  Error.Validation("The attendee has no required appointment types."));

          var issued = await issuer.IssueInitialAsync(attendee, locationIds,
              EmailTemplate.AttendeeInvite, ActorType.Staff, command.StaffUserId.ToString(), ct);
          if (issued.IsFailure)
          {
              if (issued.Error.Code != Error.InsufficientEventsCode)
                  return Result<InviteAttendeeOutcome>.Failure(issued.Error);

              if (attendee.Status != AttendeeStatus.AwaitingAvailability)
              {
                  if (Attendee.IsLegalTransition(attendee.Status, AttendeeStatus.AwaitingAvailability))
                      attendee.MarkAwaitingAvailability(clock.UtcNow);
                  else
                      attendee.MarkNoResponse(clock.UtcNow);
              }

              await unitOfWork.SaveChangesAsync(ct);
              await transaction.CommitAsync(ct);
              return Result<InviteAttendeeOutcome>.Failure(issued.Error);
          }

          await unitOfWork.SaveChangesAsync(ct);
          await transaction.CommitAsync(ct);
          return Result<InviteAttendeeOutcome>.Success(new InviteAttendeeOutcome(
              attendee.Id, attendee.Status.ToString(), issued.Value.InviteId));
      }
  }
  ```

  The trigger depends on the issuer interface, not the concrete issuer; the tests construct
  the handler with the real InviteIssuer. The parked outcome returns the
  `insufficient-events` error (the status read is `AwaitingAvailability` either by
  transition or because it already held).

  ```csharp
  // src/EventBooking.Application/Invites/CountEligibleEventsHandler.cs (complete)
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Access;
  using EventBooking.Application.Common;
  using EventBooking.Domain.Access;
  using EventBooking.Domain.Attendees;

  namespace EventBooking.Application.Invites;

  public sealed class CountEligibleEventsHandler(
      IAttendeeRepository attendees,
      ILocationRepository locations,
      IEventEligibilityQuery eligibility,
      IStaffAccessAuthorizer access)
  {
      public async Task<Result<int>> HandleAsync(CountEligibleEventsQuery query, CancellationToken ct)
      {
          var authorized = await access.AuthorizeAsync(
              query.StaffUserId, StaffCapability.ManageAttendees, null, ct);
          if (authorized.IsFailure) return Result<int>.Failure(authorized.Error);

          var attendee = await attendees.GetAsync(query.AttendeeId, ct);
          if (attendee is null) return Result<int>.Failure(Error.NotFound("No such attendee."));

          var locationIds = query.LocationIds.Distinct().ToList();
          foreach (var locationId in locationIds)
          {
              var location = await locations.GetAsync(locationId, ct);
              if (location is null || !location.IsActive)
                  return Result<int>.Failure(
                      Error.Validation("Every selected location must exist and be active."));
          }

          return Result<int>.Success(await eligibility.CountEligibleEventsAsync(
              attendee.RequiredAppointmentTypeIds, locationIds, [], DateTimeOffset.UtcNow, ct));
      }
  }
  ```

  ```csharp
  // src/EventBooking.Application/Invites/ExpireInviteHandler.cs (complete)
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Common;
  using EventBooking.Domain.Attendees;
  using EventBooking.Domain.Audit;
  using EventBooking.Domain.Common;
  using EventBooking.Domain.Invites;

  namespace EventBooking.Application.Invites;

  // System actor: no staff capability demanded. Each item runs in its own transaction; the
  // Task 19 sweep calls this per item.
  public sealed class ExpireInviteHandler(
      IInviteRepository invites,
      IAttendeeRepository attendees,
      IEventEligibilityQuery eligibility,
      ISystemSettingsRepository settings,
      IEmailDeliveryRepository emails,
      IStaffAccessProfileRepository profiles,
      IUnitOfWork unitOfWork,
      IAuditLogger audit,
      IClock clock,
      IInviteIssuer issuer)
  {
      public async Task<Result> HandleAsync(ExpireInviteCommand command, CancellationToken ct)
      {
          await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
          var invite = await invites.LockForUpdateAsync(command.InviteId, ct);
          if (invite is null) return Result.Failure(Error.NotFound("No such invite."));
          if (invite.Status != InviteStatus.Pending || invite.IsUsableAt(clock.UtcNow))
          {
              await transaction.CommitAsync(ct);
              return Result.Success();
          }

          var attendee = await attendees.LockForUpdateAsync(invite.AttendeeId, ct);
          if (attendee is null) return Result.Failure(Error.NotFound("No such attendee."));

          invite.MarkExpired();
          var configuration = await settings.GetAsync(ct);

          if (invite.RetryCount < configuration.MaxAutoRetryCount)
          {
              var fresh = await eligibility.FindEligibleEventsAsync(
                  invite.RequiredAppointmentTypeIds, invite.LocationIds,
                  invite.Options.Select(o => o.EventId).ToList(),
                  configuration.InviteOptionCount, clock.UtcNow, ct);
              if (fresh.Count >= configuration.InviteOptionCount)
              {
                  var reissued = await issuer.IssueReissueAsync(
                      invite, fresh.Take(configuration.InviteOptionCount).ToList(),
                      ActorType.System, null, ct);
                  if (reissued.IsSuccess)
                  {
                      attendee.MarkInvited(clock.UtcNow);
                      audit.Record(AuditEntityTypes.Invite, invite.Id, AuditAction.InviteExpired,
                          ActorType.System, null, $"reissued {reissued.Value.InviteId}");
                      await unitOfWork.SaveChangesAsync(ct);
                      await transaction.CommitAsync(ct);
                      return Result.Success();
                  }
              }

              attendee.MarkNoResponse(clock.UtcNow);
              audit.Record(AuditEntityTypes.Invite, invite.Id, AuditAction.InviteExpired,
                  ActorType.System, null, "reissue failed: insufficient eligible events");
          }
          else
          {
              attendee.MarkNoResponse(clock.UtcNow);
              audit.Record(AuditEntityTypes.Invite, invite.Id, AuditAction.InviteExpired,
                  ActorType.System, null, $"retry limit {configuration.MaxAutoRetryCount} reached");
          }

          await unitOfWork.SaveChangesAsync(ct);
          await transaction.CommitAsync(ct);
          return Result.Success();
      }
  }
  ```

  The profiles constructor parameter is unused — remove it. The committed constructor is
  `(invites, attendees, eligibility, settings, emails, unitOfWork, audit, clock, issuer)`;
  update the test constructions to match (drop the `f.Profiles` argument).

  ```csharp
  // src/EventBooking.Application/Invites/TopUpInviteOptionsHandler.cs (complete)
  using EventBooking.Application.Abstractions;
  using EventBooking.Application.Access;
  using EventBooking.Application.Common;
  using EventBooking.Domain.Access;
  using EventBooking.Domain.Attendees;
  using EventBooking.Domain.Audit;
  using EventBooking.Domain.Common;
  using EventBooking.Domain.Invites;

  namespace EventBooking.Application.Invites;

  public sealed class TopUpInviteOptionsHandler(
      IInviteRepository invites,
      IAttendeeRepository attendees,
      IEventEligibilityQuery eligibility,
      IStaffAccessAuthorizer access,
      IUnitOfWork unitOfWork,
      IAuditLogger audit,
      IClock clock)
  {
      public async Task<Result> HandleAsync(TopUpInviteOptionsCommand command, CancellationToken ct)
      {
          var authorized = await access.AuthorizeAsync(
              command.StaffUserId, StaffCapability.ManageAttendees, null, ct);
          if (authorized.IsFailure) return Result.Failure(authorized.Error);

          await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
          var invite = await invites.LockForUpdateAsync(command.InviteId, ct);
          if (invite is null) return Result.Failure(Error.NotFound("No such invite."));
          if (invite.Status != InviteStatus.Pending)
              return Result.Failure(Error.Conflict($"The invite is {invite.Status} and cannot be topped up."));

          var attendee = await attendees.LockForUpdateAsync(invite.AttendeeId, ct);
          if (attendee is null) return Result.Failure(Error.NotFound("No such attendee."));

          if (invite.Offers(command.FilledEventId))
              invite.RemoveOption(command.FilledEventId);

          var exclude = invite.Options.Select(o => o.EventId).ToList();
          if (command.ExcludeEventId is { } excluded && !exclude.Contains(excluded))
              exclude.Add(excluded);

          var fresh = await eligibility.FindEligibleEventsAsync(
              invite.RequiredAppointmentTypeIds, invite.LocationIds, exclude, 1, clock.UtcNow, ct);
          if (fresh.Count == 0)
          {
              attendee.MarkNoResponse(clock.UtcNow);
              audit.Record(AuditEntityTypes.Invite, invite.Id, AuditAction.InviteExpired,
                  ActorType.Staff, command.StaffUserId.ToString(), "top-up found no eligible event");
              await unitOfWork.SaveChangesAsync(ct);
              await transaction.CommitAsync(ct);
              return Result.Success();
          }

          try
          {
              invite.AddOption(fresh[0]);
          }
          catch (DomainException ex)
          {
              await transaction.RollbackAsync(ct);
              return Result.Failure(Error.Validation(ex.Message));
          }

          audit.Record(AuditEntityTypes.Invite, invite.Id, AuditAction.InviteOptionReplaced,
              ActorType.Staff, command.StaffUserId.ToString(), $"option {fresh[0]}");
          await unitOfWork.SaveChangesAsync(ct);
          await transaction.CommitAsync(ct);
          return Result.Success();
      }
  }
  ```

  `Invite.RemoveOption` and AddOption enforce the count bounds themselves (Task 8); the
  handler maps a refusal to validation. The top-up audit carries the new event id, never a
  token or URL.

- [ ] **Step 4: Run.** Expected: PASS — the new suites plus the full solution.

  ```bash
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  ```

  Expect the Application count to rise (four new suites replacing the ported trigger and
  expiry suites). A count that does not match the executor's own before/after diff is a
  signal to read the diff, not to adjust the number.

- [ ] **Step 5: Delete nothing by hand beyond the three listed files**, then commit and push
  the executor's code — not the plan documents — under the master plan's message:

  ```bash
  test -z "$(git status --porcelain --ignored=no | grep -v '^??')"
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  git add src/EventBooking.Application/Common/Error.cs src/EventBooking.Application/Invites/ src/EventBooking.Domain/Invites/Invite.cs tests/EventBooking.Application.Tests/Invites/
  git diff --cached --name-only
  git diff --cached
  test -n "$EXECUTOR_COAUTHOR"
  git commit -m "feat(app): location-restricted invite engine

  Co-authored-by: $EXECUTOR_COAUTHOR"
  git push
  ```
