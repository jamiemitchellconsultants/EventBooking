# 01d — Capacity generalised to N rows, edits 2 (Task 7)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — src/EventBooking.Domain/Events/Event.cs — 1/1

<!-- retirement-file: {"id":3,"file":"src/EventBooking.Domain/Events/Event.cs","beforeSha":"02141017b7ff494efab3e36265b50eadf42a22282b49a3efcc4b7e5a2f5c2023","afterSha":"cc6ee56361e57c11a94f530ba83e67d9f083d353147d3409c48ff26d5bbdd3c6","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Time;

namespace EventBooking.Domain.Events;

/// <summary>Defines event for the current use case.</summary>
public sealed class Event
{
    private readonly List<EventCapacity> _capacities = [];

    private Event()
    {
        // Required by the persistence layer's constructor binding.
        Window = null!;
    }

    /// <summary>Defines id for the current use case.</summary>
    public Guid Id { get; private set; }

    /// <summary>Defines proposal id for the current use case.</summary>
    public Guid ProposalId { get; private set; }

    /// <summary>The location hosting the event, carried from its proposal.</summary>
    public Guid LocationId { get; private set; }

    /// <summary>Defines window for the current use case.</summary>
    public EventWindow Window { get; private set; }

    /// <summary>Defines status for the current use case.</summary>
    public EventStatus Status { get; private set; } = EventStatus.Active;

    /// <summary>Defines capacities for the current use case.</summary>
    public IReadOnlyList<EventCapacity> Capacities => _capacities;

    /// <summary>
    /// The only way a eventItem is created. Marks the proposal confirmed in the same call, so
    /// a proposal can never back a second eventItem.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="proposal">The proposal.</param>
    public static Event CreateFrom(Guid id, EventProposal proposal)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(proposal is null, "proposal must be supplied.");

        proposal!.MarkConfirmed();

        var eventItem = new Event
        {
            Id = id,
            ProposalId = proposal.Id,
            LocationId = proposal.LocationId,
            Window = proposal.Window,
            Status = EventStatus.Active,
        };

        foreach (var acceptance in proposal.Acceptances.OrderBy(a => a.AppointmentTypeId))
        {
            eventItem._capacities.Add(
                EventCapacity.Initialise(id, acceptance.AppointmentTypeId, acceptance.Headcount));
        }

        return eventItem;
    }

    /// <summary>
    /// Sorts capacity keys into the order every command must take their row locks in: ascending
    /// event id, then ascending appointment type id (FR-3.3). Pure, so a caller can sort keys it
    /// has not loaded yet, and duplicates collapse.
    /// </summary>
    /// <param name="keys">The keys to sort, in any order and with any duplicates.</param>
    public static IReadOnlyList<EventCapacityKey> CapacityLockOrder(IEnumerable<EventCapacityKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        return
        [
            .. keys
                .Distinct()
                .OrderBy(key => key.EventId)
                .ThenBy(key => key.AppointmentTypeId),
        ];
    }

    /// <summary>Defines capacity for for the current use case.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    public EventCapacity CapacityFor(Guid appointmentTypeId)
    {
        var capacity = _capacities.SingleOrDefault(c => c.AppointmentTypeId == appointmentTypeId);
        Guard.Against(capacity is null, $"This event has no capacity counter for {appointmentTypeId}.");

        return capacity!;
    }

    /// <summary>
    /// Charges one place against each named type, and only those: an event may list types the
    /// attendee does not require, and those rows are untouched (FR-3.4). All or nothing — every
    /// row is checked before any row moves, so a refusal leaves the event exactly as it was.
    /// </summary>
    /// <param name="appointmentTypeIds">The types the attendee requires; every one must be listed.</param>
    public void ChargeRequiredTypes(IEnumerable<Guid> appointmentTypeIds)
    {
        var rows = RowsFor(appointmentTypeIds);

        Guard.Against(
            Status != EventStatus.Active,
            "A cancelled event cannot be charged.");

        if (rows.Find(row => !row.HasSpare) is { } exhausted)
        {
            throw new DomainException(EventCapacity.ExhaustedMessage(exhausted.AppointmentTypeId));
        }

        foreach (var row in rows)
        {
            row.Decrement();
        }
    }

    /// <summary>
    /// Returns one place to each named type. A cancelled event still releases: cancelling voids
    /// its bookings and gives their capacity back in the same transaction.
    /// </summary>
    /// <param name="appointmentTypeIds">The types to release; every one must be listed.</param>
    public void ReleaseTypes(IEnumerable<Guid> appointmentTypeIds)
    {
        var rows = RowsFor(appointmentTypeIds);

        if (rows.Find(row => row.RemainingCapacity >= row.TotalHeadcount) is { } full)
        {
            throw new DomainException(
                $"Appointment type {full.AppointmentTypeId} has nothing charged to release on this event.");
        }

        foreach (var row in rows)
        {
            row.Increment();
        }
    }

    /// <summary>Defines has spare capacity for all for the current use case.</summary>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    public bool HasSpareCapacityForAll(IEnumerable<Guid> appointmentTypeIds) =>
        Status == EventStatus.Active
        && appointmentTypeIds.All(id => CapacityFor(id).HasSpare);

    /// <summary>
    /// Cancels the event. Refused once the window has started: the appointments have begun, so
    /// voiding the bookings and re-inviting would misdescribe what happened.
    /// </summary>
    /// <param name="zones">The zone abstraction.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    /// <param name="now">The current instant.</param>
    public void Cancel(IEventWindowZones zones, string timeZoneId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(zones);

        Guard.Against(Status == EventStatus.Cancelled, "This event has already been cancelled.");
        Guard.Against(
            Window.HasStarted(zones, timeZoneId, now),
            "An event whose window has started cannot be cancelled.");

        Status = EventStatus.Cancelled;
    }

    private List<EventCapacity> RowsFor(IEnumerable<Guid> appointmentTypeIds)
    {
        ArgumentNullException.ThrowIfNull(appointmentTypeIds);

        var ids = appointmentTypeIds.Distinct().Order().ToList();
        Guard.Against(ids.Count == 0, "At least one appointment type must be named.");

        // CapacityFor refuses a type this event does not list, which is the check that keeps an
        // attendee whose requirements have drifted from silently booking a partial event.
        return [.. ids.Select(CapacityFor)];
    }
}
`````

## before — src/EventBooking.Domain/Events/EventCapacity.cs — 1/1

<!-- retirement-file: {"id":4,"file":"src/EventBooking.Domain/Events/EventCapacity.cs","beforeSha":"b7dcfc6afac4991f17c5cc6549dd6d68d4f4289cb1d1b1b6486cd1e24aa54162","afterSha":"0bace9a29ae93e91edca1ea400403d2196c096ee22b93eeee8e050839342f2b4","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Events;

/// <summary>
/// Remaining bookable headcount for one appointment type on one eventItem. This row is the
/// single source of truth for booking eligibility, and the row the confirm transaction locks.
/// </summary>
public sealed class EventCapacity
{
    private EventCapacity()
    {
    }

    /// <summary>Defines event id for the current use case.</summary>
    public Guid EventId { get; private set; }

    /// <summary>Defines appointment type id for the current use case.</summary>
    public Guid AppointmentTypeId { get; private set; }

    /// <summary>Defines total headcount for the current use case.</summary>
    public int TotalHeadcount { get; private set; }

    /// <summary>Defines remaining capacity for the current use case.</summary>
    public int RemainingCapacity { get; private set; }

    /// <summary>Defines has spare for the current use case.</summary>
    public bool HasSpare => RemainingCapacity > 0;

    /// <summary>Defines occupied capacity for the current use case.</summary>
    public int OccupiedCapacity => TotalHeadcount - RemainingCapacity;

    internal static EventCapacity Initialise(Guid eventId, Guid appointmentTypeId, int totalHeadcount)
    {
        AppointmentTypeIdsGuard(appointmentTypeId);

        var total = Guard.Positive(totalHeadcount, "totalHeadcount");

        return new EventCapacity
        {
            EventId = eventId,
            AppointmentTypeId = appointmentTypeId,
            TotalHeadcount = total,
            RemainingCapacity = total,
        };
    }

    /// <summary>Defines decrement for the current use case.</summary>
    public void Decrement()
    {
        Guard.Against(
            RemainingCapacity <= 0,
            "No remaining capacity for this appointment type on this eventItem.");

        RemainingCapacity -= 1;
    }

    /// <summary>Defines increment for the current use case.</summary>
    public void Increment()
    {
        Guard.Against(
            RemainingCapacity >= TotalHeadcount,
            "Remaining capacity cannot exceed the headcount the manager accepted.");

        RemainingCapacity += 1;
    }

    /// <summary>Defines adjust total headcount for the current use case.</summary>
    /// <param name="totalHeadcount">The total headcount.</param>
    public bool AdjustTotalHeadcount(int totalHeadcount)
    {
        var next = Guard.Positive(totalHeadcount, "totalHeadcount");
        if (next == TotalHeadcount)
        {
            return false;
        }

        Guard.Against(
            next < OccupiedCapacity,
            "totalHeadcount cannot be lower than occupied capacity.");

        var delta = next - TotalHeadcount;
        TotalHeadcount = next;
        RemainingCapacity += delta;
        return true;
    }

    private static void AppointmentTypeIdsGuard(Guid appointmentTypeId) =>
        AppointmentTypes.AppointmentTypeIds.EnsureKnown(appointmentTypeId);
}
`````

## after — src/EventBooking.Domain/Events/EventCapacity.cs — 1/1

<!-- retirement-file: {"id":4,"file":"src/EventBooking.Domain/Events/EventCapacity.cs","beforeSha":"b7dcfc6afac4991f17c5cc6549dd6d68d4f4289cb1d1b1b6486cd1e24aa54162","afterSha":"0bace9a29ae93e91edca1ea400403d2196c096ee22b93eeee8e050839342f2b4","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Events;

/// <summary>
/// Remaining bookable headcount for one appointment type on one event. This row is the single
/// source of truth for booking eligibility, and the row the confirm transaction locks.
/// </summary>
public sealed class EventCapacity
{
    /// <summary>The largest total headcount any one type may carry (design 08 — boundary values).</summary>
    public const int MaximumTotalHeadcount = 1000;

    private EventCapacity()
    {
    }

    /// <summary>Defines event id for the current use case.</summary>
    public Guid EventId { get; private set; }

    /// <summary>Defines appointment type id for the current use case.</summary>
    public Guid AppointmentTypeId { get; private set; }

    /// <summary>Defines total headcount for the current use case.</summary>
    public int TotalHeadcount { get; private set; }

    /// <summary>Defines remaining capacity for the current use case.</summary>
    public int RemainingCapacity { get; private set; }

    /// <summary>Defines has spare for the current use case.</summary>
    public bool HasSpare => RemainingCapacity > 0;

    /// <summary>Defines occupied capacity for the current use case.</summary>
    public int OccupiedCapacity => TotalHeadcount - RemainingCapacity;

    /// <summary>This row's key, for sorting a command's rows into lock order.</summary>
    public EventCapacityKey Key => new(EventId, AppointmentTypeId);

    internal static EventCapacity Initialise(Guid eventId, Guid appointmentTypeId, int totalHeadcount)
    {
        AppointmentTypeIdsGuard(appointmentTypeId);

        var total = Guard.Positive(totalHeadcount, "totalHeadcount");

        return new EventCapacity
        {
            EventId = eventId,
            AppointmentTypeId = appointmentTypeId,
            TotalHeadcount = total,
            RemainingCapacity = total,
        };
    }

    /// <summary>Charges one place against this row, refusing once the row is exhausted.</summary>
    public void Decrement()
    {
        Guard.Against(RemainingCapacity <= 0, ExhaustedMessage(AppointmentTypeId));

        RemainingCapacity -= 1;
    }

    /// <summary>Returns one charged place to this row, refusing to exceed the accepted total.</summary>
    public void Increment()
    {
        Guard.Against(
            RemainingCapacity >= TotalHeadcount,
            "Remaining capacity cannot exceed the headcount the manager accepted.");

        RemainingCapacity += 1;
    }

    /// <summary>
    /// Replaces the total this type's Manager accepted, applying the same delta to the remaining
    /// count (FR-3.5). A total below the type's active-booking count is refused rather than
    /// thrown, because FR-3.6 wants the Manager told the minimum and the current server values.
    /// </summary>
    /// <param name="totalHeadcount">The new total: positive, and at most <see cref="MaximumTotalHeadcount"/>.</param>
    /// <param name="activeBookingCount">Active bookings requiring this type on this event.</param>
    public CapacityAdjustment AdjustTotalHeadcount(int totalHeadcount, int activeBookingCount)
    {
        var next = Guard.Positive(totalHeadcount, "totalHeadcount");
        Guard.Against(
            next > MaximumTotalHeadcount,
            $"totalHeadcount must not exceed {MaximumTotalHeadcount}.");
        var active = Guard.NotNegative(activeBookingCount, "activeBookingCount");

        if (next == TotalHeadcount)
        {
            return new CapacityAdjustment(
                CapacityAdjustmentStatus.Unchanged, active, TotalHeadcount, RemainingCapacity);
        }

        // The caller's count and this row's own occupancy should agree. Taking the larger of the
        // two keeps 0 <= remainingCapacity <= totalHeadcount true even if they have drifted, and
        // reports a minimum the next attempt will actually be allowed to use.
        var minimum = Math.Max(active, OccupiedCapacity);
        if (next < minimum)
        {
            return new CapacityAdjustment(
                CapacityAdjustmentStatus.BelowActiveBookings, minimum, TotalHeadcount, RemainingCapacity);
        }

        var delta = next - TotalHeadcount;
        TotalHeadcount = next;
        RemainingCapacity += delta;

        return new CapacityAdjustment(
            CapacityAdjustmentStatus.Adjusted, minimum, TotalHeadcount, RemainingCapacity);
    }

    internal static string ExhaustedMessage(Guid appointmentTypeId) =>
        $"capacity-exhausted: appointment type {appointmentTypeId} has no remaining capacity "
        + "on this event.";

    private static void AppointmentTypeIdsGuard(Guid appointmentTypeId) =>
        AppointmentTypes.AppointmentTypeIds.EnsureKnown(appointmentTypeId);
}
`````

## after — src/EventBooking.Domain/Events/EventCapacityKey.cs — 1/1

<!-- retirement-file: {"id":5,"file":"src/EventBooking.Domain/Events/EventCapacityKey.cs","beforeSha":null,"afterSha":"97d22c161f9e3cdd93bad658368e06404b0393f86ce08bd0e09f7a897a7c0a0a","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.Events;

/// <summary>
/// The key of one <see cref="EventCapacity"/> row. A command that touches several rows sorts its
/// keys with <see cref="Event.CapacityLockOrder"/> before locking, so two commands whose type sets
/// overlap can never deadlock (FR-3.3; design 01 — lock ordering).
/// </summary>
/// <param name="EventId">The event whose capacity row this is.</param>
/// <param name="AppointmentTypeId">The appointment type the row counts.</param>
public readonly record struct EventCapacityKey(Guid EventId, Guid AppointmentTypeId);
`````

## before — tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs — 1/1

<!-- retirement-file: {"id":6,"file":"tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs","beforeSha":"ad50b6147c9f5fe2829327893c3809bc2a5f2c32210200220f3eb75901bfa008","afterSha":"8b4e0bfa4a5085a4d3dd1298e09e24adf8b83200cd195cee6afa33c094d815bb","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Appointments;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Appointments;

/// <summary>Verifies scoped, versioned, idempotent appointment status command behavior.</summary>
public sealed class UpdateBookingAppointmentStatusHandlerTests
{
    /// <summary>Verifies a real check-in commits one state and one safe audit entry.</summary>
    [Fact]
    public async Task CheckInUpdatesAndAuditsOnce()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingAppointmentStatus.CheckedIn, result.Value.Status);
        Assert.Equal(2, result.Value.Version);
        var entry = Assert.Single(scenario.Audit.Entries);
        Assert.Equal(AuditAction.AppointmentCheckedIn, entry.Action);
        Assert.DoesNotContain("Amara", entry.Details);
        Assert.DoesNotContain("amara@example.com", entry.Details);
        Assert.Equal(1, scenario.UnitOfWork.CommitCount);
    }

    /// <summary>
    /// Verifies mutation shares the attendee-first lifecycle lock order used by every
    /// journey transition: Attendee, pending Invites, original Booking, addressed Booking,
    /// Confirmed Event, then ordered Booking Appointments.
    /// </summary>
    [Fact]
    public async Task LocksAttendeeLifecycleBeforeEventAndAppointments()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [
                "transaction-begun",
                "attendee-locked",
                "pending-invites-locked",
                "booking-locked",
                "booking-locked",
                "event-guard-locked",
                "appointments-locked",
            ],
            scenario.Operations.Events.TakeLast(7));
    }

    /// <summary>Verifies retrying the committed state with the old version is a clean success.</summary>
    [Fact]
    public async Task SameStateRetryWithOldVersionCreatesNoSecondWriteOrAudit()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        var retry = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        Assert.True(retry.IsSuccess);
        Assert.Equal(2, retry.Value.Version);
        Assert.Single(scenario.Audit.Entries);
        Assert.Equal(1, scenario.UnitOfWork.SaveCount);
        Assert.Equal(1, scenario.UnitOfWork.CommitCount);
    }

    /// <summary>Verifies a different state carrying a stale version returns current safe state.</summary>
    [Fact]
    public async Task DistinctStaleTransitionConflictsWithoutMutation()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        var stale = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.Completed, 1),
            CancellationToken.None);

        Assert.True(stale.IsFailure);
        Assert.Equal("appointment_version_conflict", stale.Error.Code);
        Assert.Contains("version 2", stale.Error.Message);
        Assert.Equal(BookingAppointmentStatus.CheckedIn, scenario.Appointment.Status);
        Assert.Single(scenario.Audit.Entries);
    }

    /// <summary>Verifies no-show is rejected until the four-hour window has ended.</summary>
    [Fact]
    public async Task NoShowBeforeWindowEndIsRejected()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 12, 59, 0, TimeSpan.Zero));

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.NoShow, 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Empty(scenario.Audit.Entries);
    }

    /// <summary>Verifies check-in is rejected outside the event transitional-location date.</summary>
    [Fact]
    public async Task CheckInOnAnotherTransitionalLocationDateIsRejected()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 8, 9, 5, 0, TimeSpan.Zero));

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(BookingAppointmentStatus.Expected, scenario.Appointment.Status);
    }

    /// <summary>Verifies cancelled parent state blocks an otherwise valid transition.</summary>
    [Fact]
    public async Task CancelledBookingRejectsTheUpdate()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        scenario.Booking.Cancel();

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Empty(scenario.Audit.Entries);
    }

    /// <summary>Verifies a cancelled event blocks an otherwise valid transition.</summary>
    [Fact]
    public async Task CancelledEventRejectsTheUpdate()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        scenario.Event.Cancel();

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Empty(scenario.Audit.Entries);
    }

    /// <summary>Verifies terminal outcomes on the original Booking leave it Active.</summary>
    [Fact]
    public async Task OriginalTerminalOutcomesStayActive()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.Completed, 2),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Active, scenario.Booking.Status);
        Assert.DoesNotContain(scenario.Audit.Entries, e => e.Action == AuditAction.RecoveryBookingConcluded);
    }

    /// <summary>Verifies mixed recovery outcomes leave the recovery Booking Active.</summary>
    [Fact]
    public async Task MixedRecoveryOutcomesLeaveRecoveryActive()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        var (_, first) = AddRecovery(scenario, scenario.Booking.CreatedAt.AddHours(1));
        first.TransitionTo(
            BookingAppointmentStatus.CheckedIn, scenario.StaffUserId,
            new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero), true, false);
        first.TransitionTo(
            BookingAppointmentStatus.Completed, scenario.StaffUserId,
            new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero), false, false);
        var other = BookingAppointment.Create(
            Guid.NewGuid(), scenario.Bookings.Items.Single(b => !b.IsOriginal).Id,
            AppointmentTypeIds.DrugAndAlcoholTesting);
        scenario.Appointments.Add(other);

        var result = await scenario.Handler.HandleAsync(
            ForAppointment(
                scenario, other.Id, BookingAppointmentStatus.CheckedIn, other.Version),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            BookingStatus.Active,
            scenario.Bookings.Items.Single(b => !b.IsOriginal).Status);
        Assert.DoesNotContain(scenario.Audit.Entries, e => e.Action == AuditAction.RecoveryBookingConcluded);
    }

    /// <summary>Verifies the final terminal outcome concludes a recovery Booking.</summary>
    [Fact]
    public async Task FinalOutcomeConcludesRecovery()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 14, 0, 0, TimeSpan.Zero));
        var (_, appointment) = AddRecovery(scenario, scenario.Booking.CreatedAt.AddHours(1));

        var result = await scenario.Handler.HandleAsync(
            ForAppointment(
                scenario, appointment.Id, BookingAppointmentStatus.NoShow, appointment.Version),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            BookingStatus.Concluded,
            scenario.Bookings.Items.Single(b => !b.IsOriginal).Status);
        Assert.Equal(BookingStatus.Active, scenario.Booking.Status);
        Assert.True(scenario.Audit.Contains(AuditAction.RecoveryBookingConcluded));
        Assert.True(scenario.Audit.Contains(AuditAction.AppointmentMarkedNoShow));
    }

    /// <summary>Verifies correcting a concluded recovery reopens it with fixed statuses.</summary>
    [Fact]
    public async Task AllowedCorrectionReopensConcludedRecovery()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        var (recovery, appointment) = AddRecovery(scenario, scenario.Booking.CreatedAt.AddHours(1));
        appointment.TransitionTo(
            BookingAppointmentStatus.NoShow, scenario.StaffUserId,
            new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero), false, true);
        recovery.Conclude();

        var result = await scenario.Handler.HandleAsync(
            ForAppointment(
                scenario, appointment.Id, BookingAppointmentStatus.Expected, appointment.Version),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Active, recovery.Status);
        var entry = Assert.Single(
            scenario.Audit.Entries, e => e.Action == AuditAction.AppointmentStatusCorrected);
        Assert.Contains(";booking:Concluded->Active", entry.Details);
        Assert.DoesNotContain(scenario.Audit.Entries, e => e.Action == AuditAction.RecoveryBookingConcluded);
    }

    /// <summary>Verifies a correction behind a pending recovery invite is rejected.</summary>
    [Fact]
    public async Task GuardedCorrectionBehindPendingInviteRequiresCancellingRecoveryFirst()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        var (recovery, appointment) = AddRecovery(scenario, scenario.Booking.CreatedAt.AddHours(1));
        appointment.TransitionTo(
            BookingAppointmentStatus.NoShow, scenario.StaffUserId,
            new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero), false, true);
        scenario.Invites.Add(Invite.CreateRecovery(
            Guid.NewGuid(), scenario.Attendee.Id, scenario.Booking.Id, "pending-recovery",
            new DateTimeOffset(2026, 9, 9, 9, 0, 0, TimeSpan.Zero),
            [scenario.Event.Id, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting]));

        var result = await scenario.Handler.HandleAsync(
            ForAppointment(
                scenario, appointment.Id, BookingAppointmentStatus.Expected, appointment.Version),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Contains("Cancel the recovery first", result.Error.Message);
        Assert.Equal(BookingAppointmentStatus.NoShow, appointment.Status);
        Assert.Equal(BookingStatus.Active, recovery.Status);
    }

    /// <summary>Verifies a correction behind a later recovery Booking is rejected.</summary>
    [Fact]
    public async Task GuardedCorrectionBehindLaterBookingRequiresCancellingRecoveryFirst()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        scenario.Appointment.TransitionTo(
            BookingAppointmentStatus.NoShow, scenario.StaffUserId,
            new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero), false, true);
        AddRecovery(scenario, scenario.Booking.CreatedAt.AddHours(1));

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.Expected, scenario.Appointment.Version),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Contains("Cancel the recovery first", result.Error.Message);
        Assert.Equal(BookingAppointmentStatus.NoShow, scenario.Appointment.Status);
    }

    /// <summary>Verifies capability denial occurs before the scoped appointment locator.</summary>
    [Fact]
    public async Task AdminIsDeniedBeforeAppointmentDataAccess()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        var admin = Guid.NewGuid();
        scenario.Profiles.Add(StaffAccessProfile.Create(admin, [Role.Admin], null));

        var result = await scenario.Handler.HandleAsync(
            new UpdateBookingAppointmentStatusCommand
            {
                StaffUserId = admin,
                BookingAppointmentId = scenario.Appointment.Id,
                Status = BookingAppointmentStatus.CheckedIn,
                ExpectedVersion = 1,
            },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.DoesNotContain("appointment-located", scenario.Operations.Events);
    }

    /// <summary>Verifies another appointment type is hidden behind the stable not-found result.</summary>
    [Fact]
    public async Task CrossTypeRecordIsNotFound()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        var otherStaff = Guid.NewGuid();
        scenario.Profiles.Add(StaffAccessProfile.Create(
            otherStaff, [Role.Manager], AppointmentTypeIds.MedicalCheckUp));

        var result = await scenario.Handler.HandleAsync(
            new UpdateBookingAppointmentStatusCommand
            {
                StaffUserId = otherStaff,
                BookingAppointmentId = scenario.Appointment.Id,
                Status = BookingAppointmentStatus.CheckedIn,
                ExpectedVersion = 1,
            },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    private static UpdateBookingAppointmentStatusCommand Command(
        Scenario scenario,
        BookingAppointmentStatus status,
        long version) => ForAppointment(scenario, scenario.Appointment.Id, status, version);

    private static UpdateBookingAppointmentStatusCommand ForAppointment(
        Scenario scenario,
        Guid appointmentId,
        BookingAppointmentStatus status,
        long version) => new()
    {
        StaffUserId = scenario.StaffUserId,
        BookingAppointmentId = appointmentId,
        Status = status,
        ExpectedVersion = version,
    };

    private static (Booking Recovery, BookingAppointment Appointment) AddRecovery(
        Scenario scenario,
        DateTimeOffset recoveryCreatedAt)
    {
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(), scenario.Attendee.Id, scenario.Booking.Id, $"recovery-{Guid.NewGuid():N}",
            recoveryCreatedAt.AddDays(2),
            [scenario.Event.Id, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        scenario.Invites.Add(recoveryInvite);
        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, scenario.Booking, scenario.Event.Id,
            $"manage-{Guid.NewGuid():N}", recoveryCreatedAt);
        scenario.Bookings.Add(recovery);
        recoveryInvite.MarkUsed();
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.DrugAndAlcoholTesting);
        scenario.Appointments.Add(appointment);

        return (recovery, appointment);
    }

    /// <summary>Builds a DAT-only group; the scenario needs a mapping, not an identity.</summary>
    private static AttendeeGroup DatOnly() =>
        AttendeeGroup.Define(
            Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);

    private static Scenario GivenScenario(DateTimeOffset now)
    {
        var staff = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            staff, [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting));
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "amara@example.com", DatOnly());
        var operations = new TransactionOperationLog();
        var attendees = new InMemoryAttendeeRepository(operations);
        attendees.Add(attendee);
        var eventItem = EventFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 7), new TimeOnly(9, 0), 240),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 10));
        var events = new InMemoryEventRepository(operations);
        events.Add(eventItem);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, "invite-token", now.AddDays(1),
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()], attendee.RequiredAppointmentTypeIds, 0);
        var invites = new InMemoryInviteRepository(operations);
        invites.Add(invite);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, "manage-token", now.AddDays(-1));
        var bookings = new InMemoryBookingRepository(operations);
        bookings.Add(booking);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting);
        var appointments = new InMemoryBookingAppointmentRepository(bookings, operations);
        appointments.Add(appointment);
        var audit = new RecordingAuditLogger();
        var unitOfWork = new FakeUnitOfWork(operations);
        var clock = new FakeClock(now);
        var handler = new UpdateBookingAppointmentStatusHandler(
            new StaffAccessAuthorizer(profiles),
            appointments,
            bookings,
            attendees,
            invites,
            events,
            new RecoveryBookingOutcomeCoordinator(),
            audit,
            unitOfWork,
            clock);
        return new Scenario(
            staff, profiles, attendee, eventItem, appointment, booking,
            attendees, invites, bookings, appointments,
            audit, unitOfWork, operations, handler);
    }

    private sealed record Scenario(
        Guid StaffUserId,
        InMemoryStaffAccessProfileRepository Profiles,
        Attendee Attendee,
        Event Event,
        BookingAppointment Appointment,
        Booking Booking,
        InMemoryAttendeeRepository Attendees,
        InMemoryInviteRepository Invites,
        InMemoryBookingRepository Bookings,
        InMemoryBookingAppointmentRepository Appointments,
        RecordingAuditLogger Audit,
        FakeUnitOfWork UnitOfWork,
        TransactionOperationLog Operations,
        UpdateBookingAppointmentStatusHandler Handler);
}
`````

## after — tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs — 1/1

<!-- retirement-file: {"id":6,"file":"tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs","beforeSha":"ad50b6147c9f5fe2829327893c3809bc2a5f2c32210200220f3eb75901bfa008","afterSha":"8b4e0bfa4a5085a4d3dd1298e09e24adf8b83200cd195cee6afa33c094d815bb","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Appointments;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Appointments;

/// <summary>Verifies scoped, versioned, idempotent appointment status command behavior.</summary>
public sealed class UpdateBookingAppointmentStatusHandlerTests
{
    /// <summary>Verifies a real check-in commits one state and one safe audit entry.</summary>
    [Fact]
    public async Task CheckInUpdatesAndAuditsOnce()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingAppointmentStatus.CheckedIn, result.Value.Status);
        Assert.Equal(2, result.Value.Version);
        var entry = Assert.Single(scenario.Audit.Entries);
        Assert.Equal(AuditAction.AppointmentCheckedIn, entry.Action);
        Assert.DoesNotContain("Amara", entry.Details);
        Assert.DoesNotContain("amara@example.com", entry.Details);
        Assert.Equal(1, scenario.UnitOfWork.CommitCount);
    }

    /// <summary>
    /// Verifies mutation shares the attendee-first lifecycle lock order used by every
    /// journey transition: Attendee, pending Invites, original Booking, addressed Booking,
    /// Confirmed Event, then ordered Booking Appointments.
    /// </summary>
    [Fact]
    public async Task LocksAttendeeLifecycleBeforeEventAndAppointments()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [
                "transaction-begun",
                "attendee-locked",
                "pending-invites-locked",
                "booking-locked",
                "booking-locked",
                "event-guard-locked",
                "appointments-locked",
            ],
            scenario.Operations.Events.TakeLast(7));
    }

    /// <summary>Verifies retrying the committed state with the old version is a clean success.</summary>
    [Fact]
    public async Task SameStateRetryWithOldVersionCreatesNoSecondWriteOrAudit()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        var retry = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        Assert.True(retry.IsSuccess);
        Assert.Equal(2, retry.Value.Version);
        Assert.Single(scenario.Audit.Entries);
        Assert.Equal(1, scenario.UnitOfWork.SaveCount);
        Assert.Equal(1, scenario.UnitOfWork.CommitCount);
    }

    /// <summary>Verifies a different state carrying a stale version returns current safe state.</summary>
    [Fact]
    public async Task DistinctStaleTransitionConflictsWithoutMutation()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        var stale = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.Completed, 1),
            CancellationToken.None);

        Assert.True(stale.IsFailure);
        Assert.Equal("appointment_version_conflict", stale.Error.Code);
        Assert.Contains("version 2", stale.Error.Message);
        Assert.Equal(BookingAppointmentStatus.CheckedIn, scenario.Appointment.Status);
        Assert.Single(scenario.Audit.Entries);
    }

    /// <summary>Verifies no-show is rejected until the four-hour window has ended.</summary>
    [Fact]
    public async Task NoShowBeforeWindowEndIsRejected()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 12, 59, 0, TimeSpan.Zero));

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.NoShow, 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Empty(scenario.Audit.Entries);
    }

    /// <summary>Verifies check-in is rejected outside the event transitional-location date.</summary>
    [Fact]
    public async Task CheckInOnAnotherTransitionalLocationDateIsRejected()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 8, 9, 5, 0, TimeSpan.Zero));

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(BookingAppointmentStatus.Expected, scenario.Appointment.Status);
    }

    /// <summary>Verifies cancelled parent state blocks an otherwise valid transition.</summary>
    [Fact]
    public async Task CancelledBookingRejectsTheUpdate()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        scenario.Booking.Cancel();

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Empty(scenario.Audit.Entries);
    }

    /// <summary>Verifies a cancelled event blocks an otherwise valid transition.</summary>
    [Fact]
    public async Task CancelledEventRejectsTheUpdate()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        scenario.Event.CancelBeforeStart();

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Empty(scenario.Audit.Entries);
    }

    /// <summary>Verifies terminal outcomes on the original Booking leave it Active.</summary>
    [Fact]
    public async Task OriginalTerminalOutcomesStayActive()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.Completed, 2),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Active, scenario.Booking.Status);
        Assert.DoesNotContain(scenario.Audit.Entries, e => e.Action == AuditAction.RecoveryBookingConcluded);
    }

    /// <summary>Verifies mixed recovery outcomes leave the recovery Booking Active.</summary>
    [Fact]
    public async Task MixedRecoveryOutcomesLeaveRecoveryActive()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        var (_, first) = AddRecovery(scenario, scenario.Booking.CreatedAt.AddHours(1));
        first.TransitionTo(
            BookingAppointmentStatus.CheckedIn, scenario.StaffUserId,
            new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero), true, false);
        first.TransitionTo(
            BookingAppointmentStatus.Completed, scenario.StaffUserId,
            new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero), false, false);
        var other = BookingAppointment.Create(
            Guid.NewGuid(), scenario.Bookings.Items.Single(b => !b.IsOriginal).Id,
            AppointmentTypeIds.DrugAndAlcoholTesting);
        scenario.Appointments.Add(other);

        var result = await scenario.Handler.HandleAsync(
            ForAppointment(
                scenario, other.Id, BookingAppointmentStatus.CheckedIn, other.Version),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            BookingStatus.Active,
            scenario.Bookings.Items.Single(b => !b.IsOriginal).Status);
        Assert.DoesNotContain(scenario.Audit.Entries, e => e.Action == AuditAction.RecoveryBookingConcluded);
    }

    /// <summary>Verifies the final terminal outcome concludes a recovery Booking.</summary>
    [Fact]
    public async Task FinalOutcomeConcludesRecovery()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 14, 0, 0, TimeSpan.Zero));
        var (_, appointment) = AddRecovery(scenario, scenario.Booking.CreatedAt.AddHours(1));

        var result = await scenario.Handler.HandleAsync(
            ForAppointment(
                scenario, appointment.Id, BookingAppointmentStatus.NoShow, appointment.Version),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            BookingStatus.Concluded,
            scenario.Bookings.Items.Single(b => !b.IsOriginal).Status);
        Assert.Equal(BookingStatus.Active, scenario.Booking.Status);
        Assert.True(scenario.Audit.Contains(AuditAction.RecoveryBookingConcluded));
        Assert.True(scenario.Audit.Contains(AuditAction.AppointmentMarkedNoShow));
    }

    /// <summary>Verifies correcting a concluded recovery reopens it with fixed statuses.</summary>
    [Fact]
    public async Task AllowedCorrectionReopensConcludedRecovery()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        var (recovery, appointment) = AddRecovery(scenario, scenario.Booking.CreatedAt.AddHours(1));
        appointment.TransitionTo(
            BookingAppointmentStatus.NoShow, scenario.StaffUserId,
            new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero), false, true);
        recovery.Conclude();

        var result = await scenario.Handler.HandleAsync(
            ForAppointment(
                scenario, appointment.Id, BookingAppointmentStatus.Expected, appointment.Version),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Active, recovery.Status);
        var entry = Assert.Single(
            scenario.Audit.Entries, e => e.Action == AuditAction.AppointmentStatusCorrected);
        Assert.Contains(";booking:Concluded->Active", entry.Details);
        Assert.DoesNotContain(scenario.Audit.Entries, e => e.Action == AuditAction.RecoveryBookingConcluded);
    }

    /// <summary>Verifies a correction behind a pending recovery invite is rejected.</summary>
    [Fact]
    public async Task GuardedCorrectionBehindPendingInviteRequiresCancellingRecoveryFirst()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        var (recovery, appointment) = AddRecovery(scenario, scenario.Booking.CreatedAt.AddHours(1));
        appointment.TransitionTo(
            BookingAppointmentStatus.NoShow, scenario.StaffUserId,
            new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero), false, true);
        scenario.Invites.Add(Invite.CreateRecovery(
            Guid.NewGuid(), scenario.Attendee.Id, scenario.Booking.Id, "pending-recovery",
            new DateTimeOffset(2026, 9, 9, 9, 0, 0, TimeSpan.Zero),
            [scenario.Event.Id, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting]));

        var result = await scenario.Handler.HandleAsync(
            ForAppointment(
                scenario, appointment.Id, BookingAppointmentStatus.Expected, appointment.Version),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Contains("Cancel the recovery first", result.Error.Message);
        Assert.Equal(BookingAppointmentStatus.NoShow, appointment.Status);
        Assert.Equal(BookingStatus.Active, recovery.Status);
    }

    /// <summary>Verifies a correction behind a later recovery Booking is rejected.</summary>
    [Fact]
    public async Task GuardedCorrectionBehindLaterBookingRequiresCancellingRecoveryFirst()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        scenario.Appointment.TransitionTo(
            BookingAppointmentStatus.NoShow, scenario.StaffUserId,
            new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero), false, true);
        AddRecovery(scenario, scenario.Booking.CreatedAt.AddHours(1));

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.Expected, scenario.Appointment.Version),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Contains("Cancel the recovery first", result.Error.Message);
        Assert.Equal(BookingAppointmentStatus.NoShow, scenario.Appointment.Status);
    }

    /// <summary>Verifies capability denial occurs before the scoped appointment locator.</summary>
    [Fact]
    public async Task AdminIsDeniedBeforeAppointmentDataAccess()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        var admin = Guid.NewGuid();
        scenario.Profiles.Add(StaffAccessProfile.Create(admin, [Role.Admin], null));

        var result = await scenario.Handler.HandleAsync(
            new UpdateBookingAppointmentStatusCommand
            {
                StaffUserId = admin,
                BookingAppointmentId = scenario.Appointment.Id,
                Status = BookingAppointmentStatus.CheckedIn,
                ExpectedVersion = 1,
            },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.DoesNotContain("appointment-located", scenario.Operations.Events);
    }

    /// <summary>Verifies another appointment type is hidden behind the stable not-found result.</summary>
    [Fact]
    public async Task CrossTypeRecordIsNotFound()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero));
        var otherStaff = Guid.NewGuid();
        scenario.Profiles.Add(StaffAccessProfile.Create(
            otherStaff, [Role.Manager], AppointmentTypeIds.MedicalCheckUp));

        var result = await scenario.Handler.HandleAsync(
            new UpdateBookingAppointmentStatusCommand
            {
                StaffUserId = otherStaff,
                BookingAppointmentId = scenario.Appointment.Id,
                Status = BookingAppointmentStatus.CheckedIn,
                ExpectedVersion = 1,
            },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    private static UpdateBookingAppointmentStatusCommand Command(
        Scenario scenario,
        BookingAppointmentStatus status,
        long version) => ForAppointment(scenario, scenario.Appointment.Id, status, version);

    private static UpdateBookingAppointmentStatusCommand ForAppointment(
        Scenario scenario,
        Guid appointmentId,
        BookingAppointmentStatus status,
        long version) => new()
    {
        StaffUserId = scenario.StaffUserId,
        BookingAppointmentId = appointmentId,
        Status = status,
        ExpectedVersion = version,
    };

    private static (Booking Recovery, BookingAppointment Appointment) AddRecovery(
        Scenario scenario,
        DateTimeOffset recoveryCreatedAt)
    {
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(), scenario.Attendee.Id, scenario.Booking.Id, $"recovery-{Guid.NewGuid():N}",
            recoveryCreatedAt.AddDays(2),
            [scenario.Event.Id, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        scenario.Invites.Add(recoveryInvite);
        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, scenario.Booking, scenario.Event.Id,
            $"manage-{Guid.NewGuid():N}", recoveryCreatedAt);
        scenario.Bookings.Add(recovery);
        recoveryInvite.MarkUsed();
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.DrugAndAlcoholTesting);
        scenario.Appointments.Add(appointment);

        return (recovery, appointment);
    }

    /// <summary>Builds a DAT-only group; the scenario needs a mapping, not an identity.</summary>
    private static AttendeeGroup DatOnly() =>
        AttendeeGroup.Define(
            Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);

    private static Scenario GivenScenario(DateTimeOffset now)
    {
        var staff = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            staff, [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting));
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "amara@example.com", DatOnly());
        var operations = new TransactionOperationLog();
        var attendees = new InMemoryAttendeeRepository(operations);
        attendees.Add(attendee);
        var eventItem = EventFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 7), new TimeOnly(9, 0), 240),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 10));
        var events = new InMemoryEventRepository(operations);
        events.Add(eventItem);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, "invite-token", now.AddDays(1),
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()], attendee.RequiredAppointmentTypeIds, 0);
        var invites = new InMemoryInviteRepository(operations);
        invites.Add(invite);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, "manage-token", now.AddDays(-1));
        var bookings = new InMemoryBookingRepository(operations);
        bookings.Add(booking);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting);
        var appointments = new InMemoryBookingAppointmentRepository(bookings, operations);
        appointments.Add(appointment);
        var audit = new RecordingAuditLogger();
        var unitOfWork = new FakeUnitOfWork(operations);
        var clock = new FakeClock(now);
        var handler = new UpdateBookingAppointmentStatusHandler(
            new StaffAccessAuthorizer(profiles),
            appointments,
            bookings,
            attendees,
            invites,
            events,
            new RecoveryBookingOutcomeCoordinator(),
            audit,
            unitOfWork,
            clock);
        return new Scenario(
            staff, profiles, attendee, eventItem, appointment, booking,
            attendees, invites, bookings, appointments,
            audit, unitOfWork, operations, handler);
    }

    private sealed record Scenario(
        Guid StaffUserId,
        InMemoryStaffAccessProfileRepository Profiles,
        Attendee Attendee,
        Event Event,
        BookingAppointment Appointment,
        Booking Booking,
        InMemoryAttendeeRepository Attendees,
        InMemoryInviteRepository Invites,
        InMemoryBookingRepository Bookings,
        InMemoryBookingAppointmentRepository Appointments,
        RecordingAuditLogger Audit,
        FakeUnitOfWork UnitOfWork,
        TransactionOperationLog Operations,
        UpdateBookingAppointmentStatusHandler Handler);
}
`````
