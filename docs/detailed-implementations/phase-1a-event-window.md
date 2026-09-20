# 01a — Variable-length windows in the location's zone (Task 4)

[← Phase overview](phase-1-domain.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This is the first task of Phase 1. It replaces the predecessor's fixed four-hour window with an explicit duration, and gives the domain a way to ask a real time-zone database when a window starts, ends and falls.

> Use superpowers:executing-plans. Complete changed types and exact before/after files are embedded
> in the numbered companion volumes; apply them with the script in Step 3, never by hand.

**Goal:** An `EventWindow` carries its own duration and is interpreted in a named IANA zone, with daylight-saving gaps and overlaps refused rather than resolved arbitrarily.

**Architecture:** The domain owns the questions (a zone abstraction it defines) and infrastructure owns the answers (a NodaTime adapter over the IANA database). No handler behaviour changes yet: every existing window states the four hours it always had.

**Tech Stack:** .NET 10, xUnit, EF Core, PostgreSQL Testcontainers, bUnit.

**Spec:** [Master Task 4](../superpowers/plans/2026-09-19-eventbooking-implementation.md), [domain model](../design/01-domain-model.md), [functional requirements](../design/02-functional-requirements.md), [ontology](../ontology.md).

## Global constraints

Work on the Phase 1 branch. Do not push to main or open the phase pull request in this task. The temporary single-zone clock stays until a `Location` exists to supply zones; do not remove it here. Keep canonical names, and do not weaken the capacity or locking rules while touching window code.

## Review focus

STOP AND CHECK: a window is refused when its start **or its end** falls in a gap or an overlap, and the adapter refuses to resolve such a local time rather than silently shifting it. The backfill migration writes 240 minutes, because every inherited row is a four-hour window; a zero would violate the domain's own 15-to-720-minute rule on the next read. Do not use London and Dublin to prove zone-dependent ordering or dates: they share an offset. Use a genuinely different zone such as `Asia/Tokyo`.

### Task 4: Variable-length windows in the location's zone

**Files:**

- Modify: Directory.Packages.props
- Modify: src/EventBooking.Application/Events/ProposeEventHandler.cs
- Modify: src/EventBooking.Domain/Events/EventWindow.cs
- Create: src/EventBooking.Domain/Time/IEventWindowZones.cs
- Modify: src/EventBooking.Infrastructure/DependencyInjection.cs
- Modify: src/EventBooking.Infrastructure/EventBooking.Infrastructure.csproj
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/EventProposalConfiguration.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260920071155_VariableEventWindowDuration.Designer.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260920071155_VariableEventWindowDuration.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/EventBookingDbContextModelSnapshot.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Queries/AttendeeBookingQueries.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Queries/DashboardQueries.cs
- Create: src/EventBooking.Infrastructure/Time/NodaTimeEventWindowZones.cs
- Modify: src/EventBooking.SeedData/DemoInvitationSeeder.cs
- Modify: src/EventBooking.SeedData/DemoSeeder.cs
- Modify: tests/EventBooking.Api.Tests/AppointmentWorkspaceEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/AttendeeBookingCancellationEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/AttendeeEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/BookingEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/DashboardEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/EventCapacityAdjustmentEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/EventEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/ManageBookingEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/RecentPastWorkspaceBoundaryTests.cs
- Modify: tests/EventBooking.Api.Tests/RecoveryInviteEndpointTests.cs
- Modify: tests/EventBooking.Application.Tests/Appointments/LateNoShowOutcomeTests.cs
- Modify: tests/EventBooking.Application.Tests/Appointments/RecentPastRecoveryEligibilityTests.cs
- Modify: tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Attendees/DeleteAttendeeHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/BookingSnapshotCancellationTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/CancelAttendeeBookingHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/CancelBookingHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/ConfirmBookingHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/InviteOptionReplacementTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/InviteSnapshotAuthorityTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/ViewInviteHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/AcceptProposalHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/AcceptProposalHeadcountRevisionTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/AdjustEventCapacityHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/EventCancellationConcurrencyTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/GetManagerEventBoardHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/ManagerEventBoardHeadcountRevisionTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/ProposeEventHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/WithdrawAcceptanceHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/WithdrawProposalHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs
- Modify: tests/EventBooking.Application.Tests/Invites/EligibleEventFinderTests.cs
- Modify: tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Invites/InviteIssuerTests.cs
- Modify: tests/EventBooking.Application.Tests/Invites/RecoveryInviteHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Invites/TriggerInviteHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Notifications/AttendeeEmailComposerTests.cs
- Modify: tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs
- Modify: tests/EventBooking.Domain.Tests/Events/EventCancellationTests.cs
- Modify: tests/EventBooking.Domain.Tests/Events/EventCapacityHeadcountAdjustmentTests.cs
- Modify: tests/EventBooking.Domain.Tests/Events/EventCapacityTests.cs
- Modify: tests/EventBooking.Domain.Tests/Events/EventProposalAcceptanceTests.cs
- Modify: tests/EventBooking.Domain.Tests/Events/EventProposalConfirmationTests.cs
- Modify: tests/EventBooking.Domain.Tests/Events/EventProposalTests.cs
- Modify: tests/EventBooking.Domain.Tests/Events/EventTests.cs
- Modify: tests/EventBooking.Domain.Tests/Events/EventWindowTests.cs
- Test: tests/EventBooking.Domain.Tests/Events/EventWindowZoneRuleTests.cs
- Modify: tests/EventBooking.Domain.Tests/Events/ProposalAcceptanceHeadcountRevisionTests.cs
- Test: tests/EventBooking.Domain.Tests/Events/VariableEventWindowTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/AttendeeBookingQueryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyHarness.cs
- Modify: tests/EventBooking.Infrastructure.Tests/ConcurrencyHarness.cs
- Modify: tests/EventBooking.Infrastructure.Tests/DashboardQueryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/EventCapacityRepositoryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/EventPersistenceTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/RepairCConcurrencyTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/RepositoryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/SchemaTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/Time/NodaTimeEventWindowZonesTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs
- Modify: tests/EventBooking.Mcp.Tests/McpScenarioSeeder.cs

**Interfaces:**

These complete types define the changed public boundary and its domain behavior. Apply them with the after files at Step 3, not before the failing test.

```csharp
namespace EventBooking.Domain.Time;

/// <summary>
/// Whether one local wall-clock time names exactly one instant in a zone. A daylight-saving jump
/// forward leaves a gap where it names none; a jump back leaves an overlap where it names two.
/// </summary>
public enum LocalTimeValidity
{
    /// <summary>The local time names exactly one instant.</summary>
    Unique,

    /// <summary>The local time falls in a daylight-saving gap and names no instant.</summary>
    Gap,

    /// <summary>The local time falls in a daylight-saving overlap and names two instants.</summary>
    Ambiguous,
}

/// <summary>Why an <c>EventWindow</c> cannot be interpreted in a given zone.</summary>
public enum EventWindowZoneProblem
{
    /// <summary>The window names one unambiguous span of time in that zone.</summary>
    None,

    /// <summary>The zone identifier is not one the host recognises.</summary>
    UnknownZone,

    /// <summary>The start falls in a daylight-saving gap or overlap.</summary>
    StartHasNoUniqueInstant,

    /// <summary>The end falls in a daylight-saving gap or overlap.</summary>
    EndHasNoUniqueInstant,
}

/// <summary>
/// The time-zone questions the domain has to ask to interpret an <c>EventWindow</c> at its
/// <c>Location</c>. The domain owns the questions because its rules depend on the answers; the
/// IANA database that answers them lives in infrastructure.
/// </summary>
public interface IEventWindowZones
{
    /// <summary>Whether the identifier names a zone this host can resolve.</summary>
    /// <param name="timeZoneId">The IANA time-zone identifier.</param>
    bool IsKnownZone(string timeZoneId);

    /// <summary>Whether a local date and time names exactly one instant in the zone.</summary>
    /// <param name="date">The local date.</param>
    /// <param name="time">The local time of day.</param>
    /// <param name="timeZoneId">The IANA time-zone identifier.</param>
    LocalTimeValidity ValidityOf(DateOnly date, TimeOnly time, string timeZoneId);

    /// <summary>Converts a local date and time in the zone to the instant it names.</summary>
    /// <param name="date">The local date.</param>
    /// <param name="time">The local time of day.</param>
    /// <param name="timeZoneId">The IANA time-zone identifier.</param>
    DateTimeOffset InstantOf(DateOnly date, TimeOnly time, string timeZoneId);

    /// <summary>The calendar date an instant falls on, in the zone.</summary>
    /// <param name="instant">The instant.</param>
    /// <param name="timeZoneId">The IANA time-zone identifier.</param>
    DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId);

    /// <summary>The zone abbreviation in force at an instant, for display.</summary>
    /// <param name="instant">The instant.</param>
    /// <param name="timeZoneId">The IANA time-zone identifier.</param>
    string AbbreviationOf(DateTimeOffset instant, string timeZoneId);
}
```

```csharp
using EventBooking.Domain.Common;
using EventBooking.Domain.Time;

namespace EventBooking.Domain.Events;

/// <summary>
/// The attendee-facing window: a local date, a local start time and an explicit duration, all read
/// in the <c>Location</c>'s zone. The end time is always derived, never stored.
/// </summary>
public sealed record EventWindow : IComparable<EventWindow>
{
    /// <summary>The smallest step a duration may take, and the granularity of every duration.</summary>
    public const int DurationStepMinutes = 15;

    /// <summary>The longest window the design allows (design 08 — boundary values).</summary>
    public const int MaximumDurationMinutes = 720;

    /// <summary>Creates a window, refusing any duration the design does not allow.</summary>
    /// <param name="date">The local date the window starts and ends on.</param>
    /// <param name="startTime">The local start time.</param>
    /// <param name="durationMinutes">The length in minutes: a multiple of 15, from 15 to 720.</param>
    public EventWindow(DateOnly date, TimeOnly startTime, int durationMinutes)
    {
        Guard.Against(
            durationMinutes < DurationStepMinutes || durationMinutes > MaximumDurationMinutes,
            $"durationMinutes must be between {DurationStepMinutes} and {MaximumDurationMinutes}.");
        Guard.Against(
            durationMinutes % DurationStepMinutes != 0,
            $"durationMinutes must be a multiple of {DurationStepMinutes}.");
        Guard.Against(
            startTime.ToTimeSpan() + TimeSpan.FromMinutes(durationMinutes) >= TimeSpan.FromHours(24),
            "The window must end on the local date it starts.");

        Date = date;
        StartTime = startTime;
        DurationMinutes = durationMinutes;
    }

    /// <summary>The local date the window starts and ends on.</summary>
    public DateOnly Date { get; }

    /// <summary>The local start time.</summary>
    public TimeOnly StartTime { get; }

    /// <summary>The length of the window in minutes.</summary>
    public int DurationMinutes { get; }

    /// <summary>The derived local end time, always on the same date.</summary>
    public TimeOnly EndTime => StartTime.Add(TimeSpan.FromMinutes(DurationMinutes));

    /// <summary>Whether the window's date falls after the given local date.</summary>
    /// <param name="today">The local date to compare against.</param>
    public bool StartsAfter(DateOnly today) => Date > today;

    /// <summary>The instant the window starts, in the given zone.</summary>
    /// <param name="zones">The zone abstraction.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    public DateTimeOffset StartInstant(IEventWindowZones zones, string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(zones);
        return zones.InstantOf(Date, StartTime, timeZoneId);
    }

    /// <summary>The instant the window ends, in the given zone.</summary>
    /// <param name="zones">The zone abstraction.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    public DateTimeOffset EndInstant(IEventWindowZones zones, string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(zones);
        return zones.InstantOf(Date, EndTime, timeZoneId);
    }

    /// <summary>Whether the window has started at the given instant.</summary>
    /// <param name="zones">The zone abstraction.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    /// <param name="now">The current instant.</param>
    public bool HasStarted(IEventWindowZones zones, string timeZoneId, DateTimeOffset now) =>
        now >= StartInstant(zones, timeZoneId);

    /// <summary>Whether the window has ended at the given instant.</summary>
    /// <param name="zones">The zone abstraction.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    /// <param name="now">The current instant.</param>
    public bool HasEnded(IEventWindowZones zones, string timeZoneId, DateTimeOffset now) =>
        now >= EndInstant(zones, timeZoneId);

    /// <summary>Whether the given instant falls on the window's own local date.</summary>
    /// <param name="zones">The zone abstraction.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    /// <param name="now">The current instant.</param>
    public bool IsOnEventDate(IEventWindowZones zones, string timeZoneId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(zones);
        return zones.LocalDateOf(now, timeZoneId) == Date;
    }

    /// <summary>
    /// Why this window cannot be interpreted in the zone, or None. A window whose start or end
    /// falls in a daylight-saving gap or overlap does not identify a unique instant, so it is
    /// refused when a proposal is made rather than resolved arbitrarily.
    /// </summary>
    /// <param name="zones">The zone abstraction.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    public EventWindowZoneProblem ProblemIn(IEventWindowZones zones, string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(zones);

        if (!zones.IsKnownZone(timeZoneId))
        {
            return EventWindowZoneProblem.UnknownZone;
        }

        if (zones.ValidityOf(Date, StartTime, timeZoneId) != LocalTimeValidity.Unique)
        {
            return EventWindowZoneProblem.StartHasNoUniqueInstant;
        }

        return zones.ValidityOf(Date, EndTime, timeZoneId) != LocalTimeValidity.Unique
            ? EventWindowZoneProblem.EndHasNoUniqueInstant
            : EventWindowZoneProblem.None;
    }

    /// <summary>Orders by local date, then local start time.</summary>
    /// <param name="other">The window to compare against.</param>
    public int CompareTo(EventWindow? other)
    {
        if (other is null)
        {
            return 1;
        }

        var byDate = Date.CompareTo(other.Date);
        return byDate != 0 ? byDate : StartTime.CompareTo(other.StartTime);
    }

    /// <summary>Renders the local window for logs and diagnostics.</summary>
    public override string ToString() =>
        $"{Date:yyyy-MM-dd} {StartTime:HH\\:mm}-{EndTime:HH\\:mm}";
}
```

**Context you need**

- Design 01 (Time): “An `EventWindow` is local wall-clock time at its `Location`. The start and end instants are computed from `Location.timeZoneId` whenever they are needed.”
- Design 01 (Time), quoted: “`durationMinutes` is a positive multiple of 15 and at most 720. The window must end on the local date it starts.”
- Design 01 (Time), quoted: “A window whose start or end falls in a daylight-saving gap, or in an ambiguous overlap, is rejected at proposal time. This removes the only case where local time does not identify a unique instant.”
- Design 01 gives the rule table: “Window has started” is now ≥ start instant; “window has ended” is now ≥ end instant; “on the event date” compares the local date of now, in the location's zone, with the window's date.
- Design 08 (boundary values): `EventWindow.durationMinutes` is 15–720, in multiples of 15, and it must not cross local midnight.
- Design 04 lists a time-zone resolver port that “converts `EventWindow` and `timeZoneId` to start and end instants, and detects daylight-saving gaps and overlaps”, using NodaTime.
- The ontology already defines `EventWindow` with `date`, `startTime` and `durationMinutes`, and puts `timeZoneId` on `Location`. This task implements those definitions; it adds no concept.
- Dependencies point inward, so the abstraction the domain calls is declared in the domain project and implemented in infrastructure. The application layer's port list refers to that same abstraction rather than declaring a second one.
- A `Location` does not exist yet: callers pass a zone identifier directly, and Task 5 introduces the `Location` that will supply it.
- Every existing window keeps the four-hour length it always had by stating 240 explicitly. Varying durations arrive with negotiation in Task 6.

- [ ] **Step 1: Write the failing tests**

Create these complete files before applying production changes.

tests/EventBooking.Domain.Tests/Events/VariableEventWindowTests.cs

```csharp
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

/// <summary>
/// Task 4: the fixed four-hour window becomes an explicit duration in 15-minute steps that must
/// end on the local date it starts (design 01 — Time; boundary values in design 08).
/// </summary>
public class VariableEventWindowTests
{
    private static EventWindow Window(int durationMinutes, int hour = 9, int minute = 0) =>
        new(new DateOnly(2026, 9, 14), new TimeOnly(hour, minute), durationMinutes);

    [Theory]
    [InlineData(15)]
    [InlineData(90)]
    [InlineData(240)]
    [InlineData(720)]
    public void AnyQuarterHourDurationUpToTwelveHoursIsAccepted(int durationMinutes)
    {
        Assert.Equal(durationMinutes, Window(durationMinutes).DurationMinutes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-15)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(725)]
    [InlineData(735)]
    public void ADurationOutsideTheQuarterHourRangeIsRefused(int durationMinutes)
    {
        Assert.Throws<DomainException>(() => Window(durationMinutes));
    }

    [Fact]
    public void TheEndTimeIsDerivedFromTheDuration()
    {
        Assert.Equal(new TimeOnly(11, 0), Window(90, 9, 30).EndTime);
        Assert.Equal(new TimeOnly(17, 0), Window(240, 13).EndTime);
    }

    [Fact]
    public void AWindowThatWouldCrossLocalMidnightIsRefused()
    {
        Assert.Throws<DomainException>(() => Window(90, 23));
        Assert.Throws<DomainException>(() => Window(90, 22, 30));
    }

    [Fact]
    public void AWindowThatEndsBeforeLocalMidnightIsAccepted()
    {
        Assert.Equal(new TimeOnly(23, 45), Window(90, 22, 15).EndTime);
    }

    [Fact]
    public void WindowsWithTheSameDateAndStartButDifferentDurationsDiffer()
    {
        Assert.NotEqual(Window(90), Window(120));
    }

    [Fact]
    public void OrderingStaysByDateThenStartTime()
    {
        var early = new EventWindow(new DateOnly(2026, 9, 14), new TimeOnly(9, 0), 480);
        var late = new EventWindow(new DateOnly(2026, 9, 14), new TimeOnly(9, 30), 15);

        Assert.True(early.CompareTo(late) < 0);
    }
}
```

tests/EventBooking.Domain.Tests/Events/EventWindowZoneRuleTests.cs

```csharp
using EventBooking.Domain.Events;
using EventBooking.Domain.Time;

namespace EventBooking.Domain.Tests.Events;

/// <summary>
/// Task 4: every "has it started / has it ended / is it today" rule is answered in the
/// <c>Location</c>'s zone (design 01 — Time). The domain asks an abstraction; NodaTime answers it
/// in Infrastructure.
/// </summary>
public class EventWindowZoneRuleTests
{
    // Tokyo is nine hours ahead of UTC with no daylight saving, so a UTC instant late on one day is
    // already the next local day: the rules cannot be satisfied by reading UTC.
    private const string Tokyo = "Asia/Tokyo";

    private static readonly EventWindow Window =
        new(new DateOnly(2026, 9, 14), new TimeOnly(9, 30), 90);

    private static readonly FixedOffsetZones Zones = new(TimeSpan.FromHours(9));

    private static DateTimeOffset Utc(int day, int hour, int minute = 0) =>
        new(new DateTime(2026, 9, day, hour, minute, 0, DateTimeKind.Utc));

    [Fact]
    public void TheStartAndEndInstantsComeFromTheZone()
    {
        Assert.Equal(Utc(14, 0, 30), Window.StartInstant(Zones, Tokyo));
        Assert.Equal(Utc(14, 2, 0), Window.EndInstant(Zones, Tokyo));
    }

    [Fact]
    public void TheWindowHasStartedFromItsStartInstantOnwards()
    {
        Assert.False(Window.HasStarted(Zones, Tokyo, Utc(14, 0, 29)));
        Assert.True(Window.HasStarted(Zones, Tokyo, Utc(14, 0, 30)));
        Assert.True(Window.HasStarted(Zones, Tokyo, Utc(14, 5)));
    }

    [Fact]
    public void TheWindowHasEndedFromItsEndInstantOnwards()
    {
        Assert.False(Window.HasEnded(Zones, Tokyo, Utc(14, 1, 59)));
        Assert.True(Window.HasEnded(Zones, Tokyo, Utc(14, 2)));
    }

    [Fact]
    public void TheEventDateIsTheLocalDateNotTheUtcDate()
    {
        // 23:30 UTC on the 13th is 08:30 on the 14th in Tokyo: the event's own date.
        Assert.True(Window.IsOnEventDate(Zones, Tokyo, Utc(13, 23, 30)));
        Assert.False(Window.IsOnEventDate(Zones, Tokyo, Utc(14, 15, 30)));
    }

    [Fact]
    public void AWindowWhoseStartOrEndHasNoUniqueInstantIsRejected()
    {
        var gapAtStart = new AwkwardZones(LocalTimeValidity.Gap, LocalTimeValidity.Unique);
        var ambiguousAtEnd = new AwkwardZones(LocalTimeValidity.Unique, LocalTimeValidity.Ambiguous);

        Assert.Equal(EventWindowZoneProblem.StartHasNoUniqueInstant, Window.ProblemIn(gapAtStart, Tokyo));
        Assert.Equal(EventWindowZoneProblem.EndHasNoUniqueInstant, Window.ProblemIn(ambiguousAtEnd, Tokyo));
        Assert.Equal(EventWindowZoneProblem.None, Window.ProblemIn(Zones, Tokyo));
    }

    [Fact]
    public void AnUnknownZoneIsItsOwnProblem()
    {
        Assert.Equal(EventWindowZoneProblem.UnknownZone, Window.ProblemIn(Zones, "Mars/Olympus_Mons"));
    }

    private sealed class FixedOffsetZones(TimeSpan offset) : IEventWindowZones
    {
        public bool IsKnownZone(string timeZoneId) => timeZoneId == Tokyo;

        public LocalTimeValidity ValidityOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            LocalTimeValidity.Unique;

        public DateTimeOffset InstantOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            new DateTimeOffset(date.ToDateTime(time), offset).ToUniversalTime();

        public DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId) =>
            DateOnly.FromDateTime(instant.ToOffset(offset).DateTime);

        public string AbbreviationOf(DateTimeOffset instant, string timeZoneId) => "JST";
    }

    private sealed class AwkwardZones(LocalTimeValidity start, LocalTimeValidity end) : IEventWindowZones
    {
        public bool IsKnownZone(string timeZoneId) => timeZoneId == Tokyo;

        public LocalTimeValidity ValidityOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            time == Window.StartTime ? start : end;

        public DateTimeOffset InstantOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            new(date.ToDateTime(time), TimeSpan.Zero);

        public DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId) =>
            DateOnly.FromDateTime(instant.UtcDateTime);

        public string AbbreviationOf(DateTimeOffset instant, string timeZoneId) => "JST";
    }
}
```

tests/EventBooking.Infrastructure.Tests/Time/NodaTimeEventWindowZonesTests.cs

```csharp
using EventBooking.Domain.Events;
using EventBooking.Domain.Time;
using EventBooking.Infrastructure.Time;

namespace EventBooking.Infrastructure.Tests.Time;

/// <summary>
/// Task 4: the IANA database answers the domain's zone questions. These cases use real daylight
/// saving transitions, so they fail if the provider or the resolution policy changes.
/// </summary>
public class NodaTimeEventWindowZonesTests
{
    private readonly NodaTimeEventWindowZones _zones = new();

    [Theory]
    [InlineData("Europe/London", true)]
    [InlineData("Europe/Dublin", true)]
    [InlineData("Asia/Tokyo", true)]
    [InlineData("Mars/Olympus_Mons", false)]
    [InlineData("", false)]
    public void OnlyRealZonesAreKnown(string timeZoneId, bool known)
    {
        Assert.Equal(known, _zones.IsKnownZone(timeZoneId));
    }

    [Fact]
    public void TheSpringForwardGapNamesNoInstant()
    {
        // British Summer Time begins at 01:00 on 29 March 2026: 01:30 never happens.
        Assert.Equal(LocalTimeValidity.Gap,
            _zones.ValidityOf(new DateOnly(2026, 3, 29), new TimeOnly(1, 30), "Europe/London"));
    }

    [Fact]
    public void TheAutumnOverlapNamesTwoInstants()
    {
        // Clocks go back at 02:00 on 25 October 2026: 01:30 happens twice.
        Assert.Equal(LocalTimeValidity.Ambiguous,
            _zones.ValidityOf(new DateOnly(2026, 10, 25), new TimeOnly(1, 30), "Europe/London"));
    }

    [Fact]
    public void AWindowEndingInTheGapIsRejectedEvenWhenItsStartIsFine()
    {
        var window = new EventWindow(new DateOnly(2026, 3, 29), new TimeOnly(0, 30), 60);

        Assert.Equal(EventWindowZoneProblem.EndHasNoUniqueInstant,
            window.ProblemIn(_zones, "Europe/London"));
    }

    [Fact]
    public void AnOrdinaryWindowResolvesToItsInstants()
    {
        var window = new EventWindow(new DateOnly(2026, 9, 14), new TimeOnly(9, 30), 90);

        Assert.Equal(EventWindowZoneProblem.None, window.ProblemIn(_zones, "Europe/London"));
        Assert.Equal(
            new DateTimeOffset(2026, 9, 14, 8, 30, 0, TimeSpan.Zero),
            window.StartInstant(_zones, "Europe/London").ToUniversalTime());
        Assert.Equal(
            new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.Zero),
            window.EndInstant(_zones, "Europe/London").ToUniversalTime());
    }

    [Fact]
    public void TheSameLocalTimeInTwoZonesIsTwoDifferentInstants()
    {
        // London and Dublin share an offset, so they cannot show this; Tokyo can.
        var london = _zones.InstantOf(new DateOnly(2026, 9, 14), new TimeOnly(9, 0), "Europe/London");
        var tokyo = _zones.InstantOf(new DateOnly(2026, 9, 14), new TimeOnly(9, 0), "Asia/Tokyo");

        Assert.Equal(TimeSpan.FromHours(8), london.ToUniversalTime() - tokyo.ToUniversalTime());
    }

    [Fact]
    public void TheLocalDateOfAnInstantFollowsItsZone()
    {
        // 16:00 UTC is still the 13th in London (17:00 BST) but already the 14th in Tokyo (01:00).
        var instant = new DateTimeOffset(2026, 9, 13, 16, 0, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 9, 13), _zones.LocalDateOf(instant, "Europe/London"));
        Assert.Equal(new DateOnly(2026, 9, 14), _zones.LocalDateOf(instant, "Asia/Tokyo"));
    }

    [Fact]
    public void TheAbbreviationFollowsTheSeason()
    {
        Assert.Equal("BST", _zones.AbbreviationOf(
            new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero), "Europe/London"));
        Assert.Equal("GMT", _zones.AbbreviationOf(
            new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero), "Europe/London"));
    }

    [Fact]
    public void ResolvingATimeThatNamesNoUniqueInstantIsRefused()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _zones.InstantOf(new DateOnly(2026, 3, 29), new TimeOnly(1, 30), "Europe/London"));
    }
}
```

- [ ] **Step 2: Verify the tests fail**

```bash
dotnet test tests/EventBooking.Domain.Tests --filter FullyQualifiedName~VariableEventWindowTests
dotnet test tests/EventBooking.Domain.Tests --filter FullyQualifiedName~EventWindowZoneRuleTests
dotnet test tests/EventBooking.Infrastructure.Tests --filter FullyQualifiedName~NodaTimeEventWindowZonesTests
```

Expected: The domain tests fail to compile because the three-argument constructor, the zone abstraction and the rule methods do not exist yet. The infrastructure test fails for the same reason, and then on the missing adapter. A Docker startup failure is not the intended failure.

- [ ] **Step 3: Apply the exact implementation and regression edits**

The 28 phase-1a-edits-NNN.md files supply 84 complete before/after changes. The script validates every payload and current file before writing. It accepts an already-applied after state, refuses unrelated edits, and only deletes explicitly listed files whose before hash matches.

```bash
node --input-type=module <<'TASK_PAYLOAD'
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
const root=fs.realpathSync('.'), plan='docs/detailed-implementations';
const sha=s=>crypto.createHash('sha256').update(s).digest('hex');
const names=fs.readdirSync(plan).filter(n=>n.startsWith('phase-1a-edits-')&&n.endsWith('.md')).sort();
if(names.length!==28)throw Error('Incomplete edit volumes.');
const entries=new Map();
for(const name of names){
 const text=fs.readFileSync(path.join(plan,name),'utf8');
 const pattern=/<!-- retirement-file: (.+) -->\n\n`{5}[^\n]*\n([\s\S]*?)\n`{5}/g;
 for(const match of text.matchAll(pattern)){
  const m=JSON.parse(match[1]);
  if(path.isAbsolute(m.file)||m.file.split('/').includes('..'))throw Error('Unsafe path.');
  const e=entries.get(m.id)??{...m,before:new Map(),after:new Map(),counts:{}};
  if(e.file!==m.file||e.beforeSha!==m.beforeSha||e.afterSha!==m.afterSha||e[m.side].has(m.part))throw Error('Conflicting metadata.');
  e[m.side].set(m.part,match[2]+'\n');e.counts[m.side]=m.parts;entries.set(m.id,e);
 }
}
if(entries.size!==84)throw Error('Incomplete operation set.');
const actions=[];
for(const e of entries.values()){
 for(const side of ['before','after']){
  if(e[side+'Sha']===null)continue;
  if(e[side].size!==e.counts[side])throw Error('Missing parts.');
  const parts=Array.from({length:e.counts[side]},(_,i)=>e[side].get(i+1));
  if(parts.some(p=>p===undefined))throw Error('Missing part number.');
  e[side+'Text']=parts.join('');
  if(sha(e[side+'Text'])!==e[side+'Sha'])throw Error('Payload checksum mismatch.');
 }
 const target=path.join(root,e.file);
 let parent=path.dirname(target);while(!fs.existsSync(parent))parent=path.dirname(parent);
 const resolved=fs.realpathSync(parent);
 if(resolved!==root&&!resolved.startsWith(root+path.sep))throw Error('Parent escapes checkout.');
 if(fs.existsSync(target)&&fs.lstatSync(target).isSymbolicLink())throw Error('Symlink target.');
 const actual=fs.existsSync(target)?sha(fs.readFileSync(target)):null;
 if(actual!==e.beforeSha&&actual!==e.afterSha)throw Error('Unrelated edit: '+e.file);
 actions.push({target,body:e.afterText,remove:e.afterSha===null});
}
for(const action of actions){
 if(action.remove){if(fs.existsSync(action.target))fs.unlinkSync(action.target);}
 else{fs.mkdirSync(path.dirname(action.target),{recursive:true});fs.writeFileSync(action.target,action.body);}
}
console.log('Applied '+actions.length+' verified file changes.');
TASK_PAYLOAD
```

The included migration, designer and model snapshot were generated with this exact command, and are already represented in the supplied after files. Do not generate a duplicate migration:

```bash
dotnet ef migrations add VariableEventWindowDuration --project src/EventBooking.Infrastructure --startup-project src/EventBooking.Api
```

Review the complete migration in the edit volumes before running database-dependent tests.

- [ ] **Step 4: Verify the targeted tests pass**

```bash
dotnet test tests/EventBooking.Domain.Tests --filter FullyQualifiedName~VariableEventWindowTests
dotnet test tests/EventBooking.Domain.Tests --filter FullyQualifiedName~EventWindowZoneRuleTests
dotnet test tests/EventBooking.Infrastructure.Tests --filter FullyQualifiedName~NodaTimeEventWindowZonesTests
```

Expected: all targeted cases pass, with zero skipped tests.

- [ ] **Step 5: Build and validate the complete solution**

```bash
dotnet build EventBooking.sln -warnaserror
dotnet test EventBooking.sln
```

Expected: zero warnings, zero errors and zero failed or skipped tests. The verified checkpoint contains 1436 tests: Domain 258, Application 422, Infrastructure 173, API 232, MCP 35, Web 241 and SeedData 75.

- [ ] **Step 6: Commit and push**

The ontology already defines `EventWindow` with `durationMinutes` and puts `timeZoneId` on `Location`, so no ontology source change belongs to this task. If your implementation needed a concept the ontology does not name, stop: add it to `docs/ontology.ttl`, run `node scripts/build-ontology.mjs`, and commit both files with the code.

```bash
git add -- \
  'Directory.Packages.props' \
  'src/EventBooking.Application/Events/ProposeEventHandler.cs' \
  'src/EventBooking.Domain/Events/EventWindow.cs' \
  'src/EventBooking.Domain/Time/IEventWindowZones.cs' \
  'src/EventBooking.Infrastructure/DependencyInjection.cs' \
  'src/EventBooking.Infrastructure/EventBooking.Infrastructure.csproj' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/EventProposalConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920071155_VariableEventWindowDuration.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920071155_VariableEventWindowDuration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/EventBookingDbContextModelSnapshot.cs' \
  'src/EventBooking.Infrastructure/Persistence/Queries/AttendeeBookingQueries.cs' \
  'src/EventBooking.Infrastructure/Persistence/Queries/DashboardQueries.cs' \
  'src/EventBooking.Infrastructure/Time/NodaTimeEventWindowZones.cs' \
  'src/EventBooking.SeedData/DemoInvitationSeeder.cs' \
  'src/EventBooking.SeedData/DemoSeeder.cs' \
  'tests/EventBooking.Api.Tests/AppointmentWorkspaceEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/AttendeeBookingCancellationEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/AttendeeEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/BookingEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/DashboardEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/EventCapacityAdjustmentEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/EventEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/ManageBookingEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/RecentPastWorkspaceBoundaryTests.cs' \
  'tests/EventBooking.Api.Tests/RecoveryInviteEndpointTests.cs' \
  'tests/EventBooking.Application.Tests/Appointments/LateNoShowOutcomeTests.cs' \
  'tests/EventBooking.Application.Tests/Appointments/RecentPastRecoveryEligibilityTests.cs' \
  'tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Attendees/DeleteAttendeeHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/BookingSnapshotCancellationTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/CancelAttendeeBookingHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/CancelBookingHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/ConfirmBookingHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/InviteOptionReplacementTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/InviteSnapshotAuthorityTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/ViewInviteHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/AcceptProposalHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/AcceptProposalHeadcountRevisionTests.cs' \
  'tests/EventBooking.Application.Tests/Events/AdjustEventCapacityHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/EventCancellationConcurrencyTests.cs' \
  'tests/EventBooking.Application.Tests/Events/GetManagerEventBoardHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/ManagerEventBoardHeadcountRevisionTests.cs' \
  'tests/EventBooking.Application.Tests/Events/ProposeEventHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/WithdrawAcceptanceHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/WithdrawProposalHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/EligibleEventFinderTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/InviteIssuerTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/RecoveryInviteHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/TriggerInviteHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Notifications/AttendeeEmailComposerTests.cs' \
  'tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/EventCancellationTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/EventCapacityHeadcountAdjustmentTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/EventCapacityTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/EventProposalAcceptanceTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/EventProposalConfirmationTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/EventProposalTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/EventTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/EventWindowTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/EventWindowZoneRuleTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/ProposalAcceptanceHeadcountRevisionTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/VariableEventWindowTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/AttendeeBookingQueryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyHarness.cs' \
  'tests/EventBooking.Infrastructure.Tests/ConcurrencyHarness.cs' \
  'tests/EventBooking.Infrastructure.Tests/DashboardQueryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/EventCapacityRepositoryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/EventPersistenceTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/RepairCConcurrencyTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/RepositoryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/SchemaTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/Time/NodaTimeEventWindowZonesTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs' \
  'tests/EventBooking.Mcp.Tests/McpScenarioSeeder.cs'
git diff --cached --name-only
git diff --cached
node scripts/check-ontology-terms.mjs
: "${EXECUTOR_COAUTHOR:?Set the executing harness co-author identity}"
git commit -m "feat(domain): variable-length EventWindow in the location's time zone" -m "Co-authored-by: $EXECUTOR_COAUTHOR"
git push -u origin HEAD
```

Continue to Task 5 on the same branch.
