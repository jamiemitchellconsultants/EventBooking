# 03d — Booking and cancellation over N capacity rows (Task 15)

[← Phase overview](phase-3-application.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This task follows Task 14. The ported confirm-booking, manage-cancel, coordinator-cancel-booking
and cancel-event handlers move under `src/EventBooking.Application/Bookings/` and `Events/`,
charge and release exactly the required types, and adopt the Task 10 ordered-lock helpers —
extending the lock ladder with the `Invite` and `Booking` levels the design names.

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
is charged through Task 7's charge method (all-or-nothing across required rows) and released
through its release method — both finally called here. Capacity is re-checked under lock at
booking time, so a stale option can never overbook; exhaustion on any required type changes
nothing and returns `capacity-exhausted`. CancelEvent reads the affected attendee identifiers
without locks, then takes each booking's locks in canonical order and re-validates under lock
(section 8, decision 2). The concurrency harness switches to the real ConfirmBooking handler.
No domain entity leaves the handler.

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
their type.

## Replayed confirmations (contradiction #2, settled with the user)

A replayed confirmation of an already-used invite is refused as a conflict naming the existing
booking — it does not create a second booking and does not return the booking as a success.
This settles contradiction #2 against the master plan's "second confirm returns the same
booking" test: write the conflict test instead, carrying the existing booking id in the
outcome.

## Global constraints

One transaction per command; canonical lock order including the two new levels; audit in the
transaction; no domain entity leaves the handler. Attendee cancel with a new time while a
recovery invite is pending supersedes it (FR-9.6). CancelEvent without confirm returns the
booking count only; with confirm it cancels every active booking in attendee-id order, reissues
from each original location set through the Task 14 issuer, stages
`EventCancelledRebookingNeeded` with the "replacement created" flag in its context, and reports
cancelled, reinvited and awaiting-availability counts.

## Review focus

STOP AND CHECK four things. Confirming for an attendee needing one type on an event listing
three decrements only that type and creates one appointment row. The replay test asserts a
conflict naming the existing booking id — not a returned booking, not a second row. The
CancelEvent test asserts attendee-id ordering of the cancellations and the replacement-created
flag in every staged context. And the harness re-runs Task 10's scenarios against the real
handler plus CancelEvent racing bookings on the same event, with no deadlock.

### Task 15: Booking and cancellation over N capacity rows

**Files:**

- Create: src/EventBooking.Application/Bookings/ConfirmBookingHandler.cs
- Create: src/EventBooking.Application/Bookings/CancelBookingByAttendeeHandler.cs
- Create: src/EventBooking.Application/Bookings/CancelBookingByCoordinatorHandler.cs
- Create: src/EventBooking.Application/Events/CancelEventHandler.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Locking/ (Invite and Booking levels)
- Delete: the ported booking and cancellation files they replace
- Modify: tests/EventBooking.Infrastructure.Tests/Concurrency/ (drive the real handler)
- Test: tests/EventBooking.Application.Tests/Bookings/ConfirmBookingHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Bookings/CancelBookingHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs

**Interfaces:**

These complete types define the changed public boundary and its domain behavior. Apply them
after the failing test, not before.

```csharp
namespace EventBooking.Application.Bookings;

// Anonymous pipeline: the book token carries purpose, booking id and
// version; the signature is checked before any read.
public sealed record ConfirmBookingCommand(
    string BookToken,
    Guid EventId);

public sealed record ConfirmBookingOutcome(
    Guid BookingId,
    string ManageToken); // regenerated deterministically from id + version

// A replay names the booking the first confirmation created. It is a
// refusal, not a success and not a second row (contradiction #2).
public sealed record AlreadyConfirmed(
    Guid ExistingBookingId);

// The four truthful attendee-cancel outcomes. reinvited carries the new
// invite id; reinvitePending names the already-pending recovery invite.
public sealed record CancelBookingByAttendeeCommand(
    string ManageToken,
    bool RequestNewTime);

public sealed record AttendeeCancelOutcome(
    string Outcome,     // reinvited | reinvitePending | noEligibleEvents | cancelled
    Guid? InviteId);

// Two-step coordinator cancel: without confirm it returns
// confirmation-required with the active-booking consequence; with confirm
// it cancels.
public sealed record CancelBookingByCoordinatorCommand(
    Guid StaffUserId,
    Guid AttendeeId,
    Guid BookingId,
    bool Confirm);
```

```csharp
namespace EventBooking.Application.Events;

// Two-step event cancel with the same shape: without confirm the booking
// count is returned and nothing changes; with confirm every active booking
// is cancelled in attendee-id order and replacements issued.
public sealed record CancelEventCommand(
    Guid StaffUserId,
    Guid EventId,
    bool Confirm);

public sealed record CancelEventOutcome(
    int CancelledCount,
    int ReinvitedCount,
    int AwaitingAvailabilityCount);
```

- [ ] **Step 1: Write the failing tests.** Create the three Application test files. Required
  cases, one test per rule:

  ```csharp
  // tests/EventBooking.Application.Tests/Bookings/ConfirmBookingHandlerTests.cs
  // (representative file — the other two follow the same shape)
  using EventBooking.Application.Bookings;
  using EventBooking.Application.Common;

  namespace EventBooking.Application.Tests.Bookings;

  public sealed class ConfirmBookingHandlerTests
  {
      // Confirming for an attendee needing IND on an event listing MED, FIT
      // and IND decrements only IND, creates one BookingAppointment, sets
      // the invite Used and the attendee Booked, audits BookingCreated and
      // one CapacityDecremented, and stages BookingConfirmation.
      [Fact]
      public async Task Confirm_charges_only_required_types_and_stages_confirmation()
      {
          var fixture = BookingFixture.Create()
              .WithEvent(["MED", "FIT", "IND"], headcount: 10)
              .WithAttendeeNeeding("IND");
          var command = new ConfirmBookingCommand(
              fixture.BookToken, fixture.EventId);

          var result = await fixture.ConfirmAsync(command, CancellationToken.None);

          Assert.True(result.IsSuccess);
          Assert.Equal(9, fixture.RemainingFor("IND"));
          Assert.Equal(10, fixture.RemainingFor("MED"));
          Assert.Single(fixture.BookedAppointments(result.Value.BookingId));
          Assert.Equal("Used", fixture.StoredInvite.Status);
          Assert.Equal("Booked", fixture.StoredAttendee.Status);
          Assert.Single(fixture.Outbox.PendingForBooking(
              result.Value.BookingId, "BookingConfirmation"));
      }

      // A replay is a conflict naming the existing booking — no second row.
      [Fact]
      public async Task Replay_returns_conflict_naming_existing_booking()
      {
          var fixture = BookingFixture.Create()
              .WithEvent(["IND"], headcount: 10)
              .WithAttendeeNeeding("IND");
          var command = new ConfirmBookingCommand(
              fixture.BookToken, fixture.EventId);
          var first = await fixture.ConfirmAsync(command, CancellationToken.None);

          var result = await fixture.ConfirmAsync(command, CancellationToken.None);

          Assert.Equal("already-confirmed", result.Error.Type);
          Assert.Equal(first.Value.BookingId, result.Error.ExistingBookingId);
          Assert.Single(fixture.StoredBookings);
      }

      // Exhaustion on any required type changes nothing.
      [Fact]
      public async Task Exhausted_required_type_changes_nothing()
      {
          var fixture = BookingFixture.Create()
              .WithEvent(["MED", "IND"], headcount: 10)
              .WithAttendeeNeeding(["MED", "IND"])
              .WithRemaining("IND", 0);
          var command = new ConfirmBookingCommand(
              fixture.BookToken, fixture.EventId);

          var result = await fixture.ConfirmAsync(command, CancellationToken.None);

          Assert.Equal("capacity-exhausted", result.Error.Type);
          Assert.Empty(fixture.StoredBookings);
          Assert.Equal(10, fixture.RemainingFor("MED"));
      }
  }
  ```

  The remaining suites cover: attendee cancel without a new time sets `NotYetInvited` and
  releases capacity; attendee cancel with a new time while a recovery invite is pending
  supersedes it (FR-9.6); CancelEvent without confirm returns the booking count and changes
  nothing; with confirm it cancels every active booking in attendee-id order, reissues from
  each original location set, stages `EventCancelledRebookingNeeded` with the replacement
  flag, and reports counts; a FIT Manager cancelling an event not listing FIT is forbidden;
  every cancel after the start instant returns `window-started`.

- [ ] **Step 2: Switch the concurrency harness** to the real ConfirmBooking handler and
  re-run Task 10's scenarios plus CancelEvent racing bookings on the same event. Expected:
  FAIL — the handlers do not exist yet.

- [ ] **Step 3: Run.** Expected: FAIL.

  ```bash
  dotnet test tests/EventBooking.Application.Tests --filter "FullyQualifiedName~Bookings"
  ```

- [ ] **Step 4: Implement.** Create the four handler files; extend the lock ladder; delete
  the ported files. Charge through the domain charge method under the ordered locks;
  release through the release method. Adopt the helpers in the booking path; re-validate
  under lock in the cancellation path.

- [ ] **Step 5: Run** the application and infrastructure suites. Expected: PASS.

  ```bash
  dotnet build EventBooking.sln -warnaserror && dotnet test EventBooking.sln
  ```

  Expect Application and Infrastructure counts to move with the new suites and the
  converted harness. A count that does not match after the change is a signal to read the
  diff, not to adjust the number.

- [ ] **Step 6: Commit and push.**

  ```bash
  test -z "$(git status --porcelain --ignored=no | grep -v '^??')"
  node --input-type=module <<'LINT_PLANS'
  import fs from 'node:fs';
  import {execFileSync} from 'node:child_process';
  const directory = 'docs/detailed-implementations';
  const files = fs.readdirSync(directory).filter(name => name.endsWith('.md')).map(name => directory + '/' + name);
  process.stdout.write(execFileSync('node', ['scripts/check-ontology-terms.mjs', '--also', ...files], {encoding:'utf8', maxBuffer:1e7}));
  LINT_PLANS
  git add docs/detailed-implementations/phase-3d-booking-cancellation.md docs/detailed-implementations/phase-3-application.md docs/detailed-implementations/HANDOVER.md
  git diff --cached --name-only
  git diff --cached
  test -n "$EXECUTOR_COAUTHOR"
  git commit -m "docs(plans): Task 15 booking and cancellation handlers

  Co-authored-by: $EXECUTOR_COAUTHOR"
  git push
  ```
