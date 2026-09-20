# 00b — Vocabulary edits 93 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs — 1/1

<!-- vocabulary-file: {"id":322,"oldPath":"tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs","beforeSha":"1dde1dcb97d5a4cddffbf53b12e2abf59bbed4e3285b6e25c9962fe63c6ceb9d","afterSha":"1370b2e57d6adaa1e0b274aefb71a3656eac1dd42fa82ce67a92f4a023ff27a3","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies workspace projections are scoped and contain only operational data.</summary>
[Collection("postgres")]
public sealed class AppointmentWorkspaceQueryTests(PostgresFixture fixture)
{
    /// <summary>Verifies event counts retain recent-past rows and exclude older, cancelled, inactive-booking, and other-type rows.</summary>
    [Fact]
    public async Task EventListContainsOnlyRetainedActiveScopedAppointments()
    {
        await fixture.ResetAsync();
        await using (var write = fixture.NewContext())
        {
            var current = AddEvent(write, new DateOnly(2026, 9, 7), cancelled: false);
            var future = AddEvent(write, new DateOnly(2026, 9, 8), cancelled: false);
            var recentPast = AddEvent(write, new DateOnly(2026, 9, 6), cancelled: false);
            var tooOld = AddEvent(write, new DateOnly(2026, 8, 30), cancelled: false);
            var cancelled = AddEvent(write, new DateOnly(2026, 9, 9), cancelled: true);
            AddBooking(write, current, "Alex Morgan", "alex@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.CheckedIn, false);
            AddBooking(write, current, "Priya Shah", "priya@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, current, "Other Type", "other@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            AddBooking(write, future, "Future Attendee", "future@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, recentPast, "Past Attendee", "past@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, tooOld, "Too Old", "old@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, cancelled, "Cancelled Event", "event@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, current, "Cancelled Booking", "booking@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, true);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var result = await new AppointmentWorkspaceQueries(read, new TestClock()).ListEventsAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            new DateOnly(2026, 9, 7),
            CancellationToken.None);

        Assert.Equal("Drug & Alcohol Testing", result.AppointmentTypeName);
        Assert.Equal(3, result.Events.Count);
        Assert.Equal(new DateOnly(2026, 9, 6), result.Events[0].Date);
        Assert.Equal(new DateOnly(2026, 9, 7), result.Events[1].Date);
        Assert.Equal(1, result.Events[1].Counts.Expected);
        Assert.Equal(1, result.Events[1].Counts.CheckedIn);
        Assert.Equal(0, result.Events[1].Counts.Completed);
        Assert.Equal(0, result.Events[1].Counts.NoShow);
        Assert.Equal(new DateOnly(2026, 9, 8), result.Events[2].Date);
    }

    /// <summary>Verifies event detail returns only same-type active rows ordered for staff use.</summary>
    [Fact]
    public async Task SelectedEventReturnsOnlyMinimumScopedAttendeeRows()
    {
        await fixture.ResetAsync();
        Guid eventId;
        await using (var write = fixture.NewContext())
        {
            var eventItem = AddEvent(write, new DateOnly(2026, 9, 7), cancelled: false);
            eventId = eventItem.Id;
            AddBooking(write, eventItem, "Zara Young", "zara@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Completed, false);
            AddBooking(write, eventItem, "Alex Morgan", "alex@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, eventItem, "Medical Attendee", "medical@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var detail = await new AppointmentWorkspaceQueries(read, new TestClock()).GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            eventId,
            CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal("Drug & Alcohol Testing", detail!.AppointmentTypeName);
        Assert.Equal(eventId, detail.EventId);
        Assert.Collection(
            detail.Appointments,
            row =>
            {
                Assert.Equal("Alex Morgan", row.AttendeeName);
                Assert.Equal("alex@example.com", row.AttendeeEmail);
                Assert.Equal(BookingAppointmentStatus.Expected, row.Status);
            },
            row =>
            {
                Assert.Equal("Zara Young", row.AttendeeName);
                Assert.Equal(BookingAppointmentStatus.Completed, row.Status);
                Assert.NotNull(row.CheckedInAt);
                Assert.NotNull(row.OutcomeAt);
            });
    }

    /// <summary>Verifies a event that has no row in trusted scope is indistinguishable from missing.</summary>
    [Fact]
    public async Task SelectedEventOutsideTrustedScopeIsNotFound()
    {
        await fixture.ResetAsync();
        Guid eventId;
        await using (var write = fixture.NewContext())
        {
            var eventItem = AddEvent(write, new DateOnly(2026, 9, 7), cancelled: false);
            eventId = eventItem.Id;
            AddBooking(write, eventItem, "Medical Attendee", "medical@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        Assert.Null(await new AppointmentWorkspaceQueries(read, new TestClock()).GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            eventId,
            CancellationToken.None));
    }

    private static Event AddEvent(
        EventBookingDbContext context,
        DateOnly date,
        bool cancelled)
    {
        var eventItem = Event.CreateImported(
            Guid.NewGuid(),
            new EventWindow(date, new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 20));
        if (cancelled)
        {
            eventItem.Cancel();
        }

        context.Events.Add(eventItem);
        return eventItem;
    }

    private static void AddBooking(
        EventBookingDbContext context,
        Event eventItem,
        string name,
        string email,
        Guid appointmentTypeId,
        BookingAppointmentStatus status,
        bool cancelled)
    {
        var group = AttendeeGroup.Define(
            Guid.NewGuid(), $"WORKSPACE_{Guid.NewGuid():N}".ToUpperInvariant(), "Workspace", true,
            [appointmentTypeId]);
        context.AttendeeGroups.Add(group);
        var attendee = Attendee.Create(Guid.NewGuid(), name, email, group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            $"invite-{attendee.Id}",
            DateTimeOffset.UtcNow.AddDays(1),
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, $"manage-{attendee.Id}", DateTimeOffset.UtcNow);
        if (cancelled)
        {
            booking.Cancel();
        }

        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, appointmentTypeId);
        var staff = Guid.NewGuid();
        var checkIn = new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero);
        if (status is BookingAppointmentStatus.CheckedIn or BookingAppointmentStatus.Completed)
        {
            appointment.TransitionTo(
                BookingAppointmentStatus.CheckedIn, staff, checkIn, true, false);
        }
        if (status == BookingAppointmentStatus.Completed)
        {
            appointment.TransitionTo(
                BookingAppointmentStatus.Completed, staff, checkIn.AddHours(1), false, false);
        }
        if (status == BookingAppointmentStatus.NoShow)
        {
            appointment.TransitionTo(
                BookingAppointmentStatus.NoShow, staff, checkIn.AddHours(4), false, true);
        }

        context.Attendees.Add(attendee);
        context.Bookings.Add(booking);
        context.BookingAppointments.Add(appointment);
    }

    /// <summary>A fixed clock pinning transitional-location today to 2026-09-07 for workspace tests.</summary>
    private sealed class TestClock : IClock
    {
        /// <inheritdoc/>
        public DateTimeOffset UtcNow => new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

        /// <inheritdoc/>
        public DateTimeOffset NowAtTransitionalLocation => UtcNow;

        /// <inheritdoc/>
        public DateOnly TodayAtTransitionalLocation => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => instant;
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs — 1/1

<!-- vocabulary-file: {"id":323,"oldPath":"tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs","beforeSha":"bd1120df20fd67296e3f03063451c1f9a02d1b87e9ac17bea8742d6ae2aa6174","afterSha":"4e692ae9cb5760932b73451f8c93bcdf833baa5cdaa0cbf460df19224d56f5ad","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies the workspace retains recently past slots for late outcome recording.</summary>
[Collection("postgres")]
public sealed class AppointmentWorkspaceRecentPastTests(PostgresFixture fixture)
{
    /// <summary>Verifies the slot list includes 7-days-past slots and excludes 8-days-past slots.</summary>
    [Fact]
    public async Task SlotListRetainsSevenDaysPastAndExcludesOlderSlots()
    {
        await fixture.ResetAsync();
        await using (var write = fixture.NewContext())
        {
            var current = AddSlot(write, new DateOnly(2026, 9, 7), cancelled: false);
            var sevenDaysPast = AddSlot(write, new DateOnly(2026, 8, 31), cancelled: false);
            var eightDaysPast = AddSlot(write, new DateOnly(2026, 8, 30), cancelled: false);
            AddBooking(write, current, "Alex Morgan", "alex@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, sevenDaysPast, "Recent Past", "recent@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, eightDaysPast, "Too Old", "old@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var result = await new AppointmentWorkspaceQueries(read, new TestClock()).ListSlotsAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            new DateOnly(2026, 9, 7),
            CancellationToken.None);

        Assert.Equal(2, result.Slots.Count);
        Assert.Equal(new DateOnly(2026, 8, 31), result.Slots[0].Date);
        Assert.Equal(new DateOnly(2026, 9, 7), result.Slots[1].Date);
    }

    /// <summary>Verifies slot detail loads a recently past slot inside trusted scope.</summary>
    [Fact]
    public async Task SelectedSlotLoadsRecentlyPastSlotInScope()
    {
        await fixture.ResetAsync();
        Guid slotId;
        await using (var write = fixture.NewContext())
        {
            var slot = AddSlot(write, new DateOnly(2026, 9, 6), cancelled: false);
            slotId = slot.Id;
            AddBooking(write, slot, "Past Candidate", "past@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var detail = await new AppointmentWorkspaceQueries(read, new TestClock()).GetSlotAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            slotId,
            CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(slotId, detail!.ConfirmedSlotId);
        Assert.Equal(new DateOnly(2026, 9, 6), detail.Date);
        Assert.Single(detail.Appointments);
    }

    /// <summary>Verifies slot detail returns null for slots older than the allowance or outside scope.</summary>
    [Fact]
    public async Task SelectedSlotTooOldOrOutOfScopeIsNotFound()
    {
        await fixture.ResetAsync();
        Guid tooOldId;
        Guid wrongScopeId;
        await using (var write = fixture.NewContext())
        {
            var tooOld = AddSlot(write, new DateOnly(2026, 8, 30), cancelled: false);
            tooOldId = tooOld.Id;
            AddBooking(write, tooOld, "Too Old", "old@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            var wrongScope = AddSlot(write, new DateOnly(2026, 9, 6), cancelled: false);
            wrongScopeId = wrongScope.Id;
            AddBooking(write, wrongScope, "Medical Candidate", "medical@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var queries = new AppointmentWorkspaceQueries(read, new TestClock());
        Assert.Null(await queries.GetSlotAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, tooOldId, CancellationToken.None));
        Assert.Null(await queries.GetSlotAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, wrongScopeId, CancellationToken.None));
    }

    private static ConfirmedSlot AddSlot(
        EventBookingDbContext context,
        DateOnly date,
        bool cancelled)
    {
        var slot = ConfirmedSlot.CreateImported(
            Guid.NewGuid(),
            new SlotWindow(date, new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 20));
        if (cancelled)
        {
            slot.Cancel();
        }

        context.ConfirmedSlots.Add(slot);
        return slot;
    }

    private static void AddBooking(
        EventBookingDbContext context,
        ConfirmedSlot slot,
        string name,
        string email,
        Guid appointmentTypeId,
        BookingAppointmentStatus status,
        bool cancelled)
    {
        var group = EmployeeGroup.Define(
            Guid.NewGuid(), $"WORKSPACE_{Guid.NewGuid():N}".ToUpperInvariant(), "Workspace", true,
            [appointmentTypeId]);
        context.EmployeeGroups.Add(group);
        var candidate = Candidate.Create(Guid.NewGuid(), name, email, group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            candidate.Id,
            $"invite-{candidate.Id}",
            DateTimeOffset.UtcNow.AddDays(1),
            [slot.Id, Guid.NewGuid(), Guid.NewGuid()],
            candidate.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, slot.Id, $"manage-{candidate.Id}", DateTimeOffset.UtcNow);
        if (cancelled)
        {
            booking.Cancel();
        }

        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, appointmentTypeId);

        context.Candidates.Add(candidate);
        context.Bookings.Add(booking);
        context.BookingAppointments.Add(appointment);
    }

    /// <summary>A fixed clock pinning head-office today to 2026-09-07 for workspace tests.</summary>
    private sealed class TestClock : IClock
    {
        /// <inheritdoc/>
        public DateTimeOffset UtcNow => new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

        /// <inheritdoc/>
        public DateTimeOffset NowAtHeadOffice => UtcNow;

        /// <inheritdoc/>
        public DateOnly TodayAtHeadOffice => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateOnly DateAtHeadOffice(DateTimeOffset instant) => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateTimeOffset InstantAtHeadOffice(DateTimeOffset instant) => instant;
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs — 1/1

<!-- vocabulary-file: {"id":323,"oldPath":"tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs","beforeSha":"bd1120df20fd67296e3f03063451c1f9a02d1b87e9ac17bea8742d6ae2aa6174","afterSha":"4e692ae9cb5760932b73451f8c93bcdf833baa5cdaa0cbf460df19224d56f5ad","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies the workspace retains recently past events for late outcome recording.</summary>
[Collection("postgres")]
public sealed class AppointmentWorkspaceRecentPastTests(PostgresFixture fixture)
{
    /// <summary>Verifies the event list includes 7-days-past events and excludes 8-days-past events.</summary>
    [Fact]
    public async Task EventListRetainsSevenDaysPastAndExcludesOlderEvents()
    {
        await fixture.ResetAsync();
        await using (var write = fixture.NewContext())
        {
            var current = AddEvent(write, new DateOnly(2026, 9, 7), cancelled: false);
            var sevenDaysPast = AddEvent(write, new DateOnly(2026, 8, 31), cancelled: false);
            var eightDaysPast = AddEvent(write, new DateOnly(2026, 8, 30), cancelled: false);
            AddBooking(write, current, "Alex Morgan", "alex@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, sevenDaysPast, "Recent Past", "recent@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, eightDaysPast, "Too Old", "old@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var result = await new AppointmentWorkspaceQueries(read, new TestClock()).ListEventsAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            new DateOnly(2026, 9, 7),
            CancellationToken.None);

        Assert.Equal(2, result.Events.Count);
        Assert.Equal(new DateOnly(2026, 8, 31), result.Events[0].Date);
        Assert.Equal(new DateOnly(2026, 9, 7), result.Events[1].Date);
    }

    /// <summary>Verifies event detail loads a recently past event inside trusted scope.</summary>
    [Fact]
    public async Task SelectedEventLoadsRecentlyPastEventInScope()
    {
        await fixture.ResetAsync();
        Guid eventId;
        await using (var write = fixture.NewContext())
        {
            var eventItem = AddEvent(write, new DateOnly(2026, 9, 6), cancelled: false);
            eventId = eventItem.Id;
            AddBooking(write, eventItem, "Past Attendee", "past@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var detail = await new AppointmentWorkspaceQueries(read, new TestClock()).GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            eventId,
            CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(eventId, detail!.EventId);
        Assert.Equal(new DateOnly(2026, 9, 6), detail.Date);
        Assert.Single(detail.Appointments);
    }

    /// <summary>Verifies event detail returns null for events older than the allowance or outside scope.</summary>
    [Fact]
    public async Task SelectedEventTooOldOrOutOfScopeIsNotFound()
    {
        await fixture.ResetAsync();
        Guid tooOldId;
        Guid wrongScopeId;
        await using (var write = fixture.NewContext())
        {
            var tooOld = AddEvent(write, new DateOnly(2026, 8, 30), cancelled: false);
            tooOldId = tooOld.Id;
            AddBooking(write, tooOld, "Too Old", "old@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            var wrongScope = AddEvent(write, new DateOnly(2026, 9, 6), cancelled: false);
            wrongScopeId = wrongScope.Id;
            AddBooking(write, wrongScope, "Medical Attendee", "medical@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var queries = new AppointmentWorkspaceQueries(read, new TestClock());
        Assert.Null(await queries.GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, tooOldId, CancellationToken.None));
        Assert.Null(await queries.GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, wrongScopeId, CancellationToken.None));
    }

    private static Event AddEvent(
        EventBookingDbContext context,
        DateOnly date,
        bool cancelled)
    {
        var eventItem = Event.CreateImported(
            Guid.NewGuid(),
            new EventWindow(date, new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 20));
        if (cancelled)
        {
            eventItem.Cancel();
        }

        context.Events.Add(eventItem);
        return eventItem;
    }

    private static void AddBooking(
        EventBookingDbContext context,
        Event eventItem,
        string name,
        string email,
        Guid appointmentTypeId,
        BookingAppointmentStatus status,
        bool cancelled)
    {
        var group = AttendeeGroup.Define(
            Guid.NewGuid(), $"WORKSPACE_{Guid.NewGuid():N}".ToUpperInvariant(), "Workspace", true,
            [appointmentTypeId]);
        context.AttendeeGroups.Add(group);
        var attendee = Attendee.Create(Guid.NewGuid(), name, email, group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            $"invite-{attendee.Id}",
            DateTimeOffset.UtcNow.AddDays(1),
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, $"manage-{attendee.Id}", DateTimeOffset.UtcNow);
        if (cancelled)
        {
            booking.Cancel();
        }

        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, appointmentTypeId);

        context.Attendees.Add(attendee);
        context.Bookings.Add(booking);
        context.BookingAppointments.Add(appointment);
    }

    /// <summary>A fixed clock pinning transitional-location today to 2026-09-07 for workspace tests.</summary>
    private sealed class TestClock : IClock
    {
        /// <inheritdoc/>
        public DateTimeOffset UtcNow => new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

        /// <inheritdoc/>
        public DateTimeOffset NowAtTransitionalLocation => UtcNow;

        /// <inheritdoc/>
        public DateOnly TodayAtTransitionalLocation => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => instant;
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/AuditQueryTests.cs — 1/1

<!-- vocabulary-file: {"id":324,"oldPath":"tests/EventBooking.Infrastructure.Tests/AuditQueryTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/AuditQueryTests.cs","beforeSha":"7ec6ae65c9227bfeb29067b7eb8214af5429bb32721d46f799de507e230fa79f","afterSha":"86ef5a01fa0531b189cf34afc958ee3d3fd95250463de11d753fe1c87c337838","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class AuditQueryTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ASlotsHistoryComesBackNewestFirst()
    {
        await fixture.ResetAsync();
        var slotId = Guid.NewGuid();

        await using (var write = fixture.NewContext())
        {
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.ConfirmedSlot, slotId, AuditAction.SlotConfirmed,
                ActorType.Staff, "staff-1", Now, null));
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.ConfirmedSlot, slotId, AuditAction.SlotCancelled,
                ActorType.Staff, "staff-2", Now.AddHours(1), "6 bookings voided"));
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.ConfirmedSlot, Guid.NewGuid(), AuditAction.SlotConfirmed,
                ActorType.Staff, "staff-3", Now, null));
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var rows = await new AuditQueries(read).ForEntityAsync(
            AuditEntityTypes.ConfirmedSlot, slotId, CancellationToken.None);

        Assert.Equal(2, rows.Count);
        Assert.Equal("SlotCancelled", rows[0].Action);
        Assert.Equal("6 bookings voided", rows[0].Details);
        Assert.Equal("SlotConfirmed", rows[1].Action);
        Assert.Equal("Staff", rows[1].ActorType);
    }

    [Fact]
    public async Task ACandidatesHistoryGathersTheirInviteAndBookingEntries()
    {
        await fixture.ResetAsync();

        var candidateId = Guid.NewGuid();
        var inviteId = Guid.NewGuid();

        await using (var write = fixture.NewContext())
        {
            var pilots = write.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.Pilots);
            var candidate = Candidate.Create(
                candidateId, "Amara Novak", "a.novak@mail.com", pilots);
            write.Candidates.Add(candidate);
            write.Invites.Add(Invite.CreateInitial(
                inviteId, candidate.Id, "hash", Now.AddDays(4),
                [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
                candidate.RequiredAppointmentTypeIds, 0));

            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Invite, inviteId, AuditAction.InviteCreated,
                ActorType.System, null, Now, "retry 0"));
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Invite, Guid.NewGuid(), AuditAction.InviteCreated,
                ActorType.System, null, Now, "someone else"));

            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var rows = await new AuditQueries(read).ForCandidateAsync(candidateId, CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal("InviteCreated", row.Action);
        Assert.Equal("retry 0", row.Details);
        Assert.Equal("System", row.ActorType);
        Assert.Null(row.ActorId);
    }

    [Fact]
    public async Task ACandidateWithNoHistoryGetsAnEmptyList()
    {
        await fixture.ResetAsync();

        await using var read = fixture.NewContext();
        var rows = await new AuditQueries(read).ForCandidateAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task ForCandidateIncludesBookingAppointmentEvents()
    {
        await fixture.ResetAsync();
        var candidateId = Guid.NewGuid();
        var appointmentId = await SeedCandidateBookingAsync(candidateId);
        await AddAuditAsync(AuditEntityTypes.BookingAppointment, appointmentId, AuditAction.AppointmentCheckedIn);

        await using var context = fixture.NewContext();
        var rows = await new AuditQueries(context).ForCandidateAsync(candidateId, CancellationToken.None);

        Assert.Contains(rows, r => r.EntityId == appointmentId);
    }

    [Fact]
    public async Task ForCandidateIncludesEntriesRecordedAgainstTheCandidateItself()
    {
        await fixture.ResetAsync();
        var candidateId = Guid.NewGuid();
        await SeedCandidateBookingAsync(candidateId);
        await AddAuditAsync(AuditEntityTypes.Candidate, candidateId, AuditAction.EmployeeGroupAssigned, Now.AddHours(-1));
        await AddAuditAsync(AuditEntityTypes.Candidate, Guid.NewGuid(), AuditAction.EmployeeGroupChanged);
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, candidateId, AuditAction.SlotConfirmed);

        await using var context = fixture.NewContext();
        var rows = await new AuditQueries(context).ForCandidateAsync(candidateId, CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal(AuditEntityTypes.Candidate, row.EntityType);
        Assert.Equal(nameof(AuditAction.EmployeeGroupAssigned), row.Action);
    }

    [Fact]
    public async Task SearchOrdersNewestFirstWithStableCursor()
    {
        await fixture.ResetAsync();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, first, AuditAction.SlotConfirmed, Now.AddHours(-2));
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, second, AuditAction.SlotCancelled, Now.AddHours(-1));

        await using var context = fixture.NewContext();
        var queries = new AuditQueries(context);
        var page1 = await queries.SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, AuditEntityTypes.All, null, null, 1),
            CancellationToken.None);
        Assert.Single(page1.Rows);
        Assert.Equal(second, page1.Rows[0].EntityId);
        Assert.NotNull(page1.NextCursor);

        // A row newer than the cursor must not shift the following page.
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, Guid.NewGuid(), AuditAction.SlotConfirmed, Now);

        var page2 = await queries.SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, AuditEntityTypes.All, null, page1.NextCursor, 10),
            CancellationToken.None);
        Assert.Contains(page2.Rows, r => r.EntityId == first);
        Assert.DoesNotContain(page2.Rows, r => r.EntityId == second);
    }

    [Fact]
    public async Task SearchRestrictedToOperationalBucketNeverReturnsCandidateRows()
    {
        await fixture.ResetAsync();
        await AddAuditAsync(AuditEntityTypes.Booking, Guid.NewGuid(), AuditAction.BookingCreated);
        var slotId = Guid.NewGuid();
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, slotId, AuditAction.SlotConfirmed);

        await using var context = fixture.NewContext();
        IReadOnlyList<string> operational =
            [AuditEntityTypes.SlotProposal, AuditEntityTypes.ConfirmedSlot, AuditEntityTypes.StaffAccessProfile];
        var page = await new AuditQueries(context).SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, operational, null, null, 50),
            CancellationToken.None);

        Assert.All(page.Rows, r => Assert.Contains(r.EntityType, operational));
        Assert.Contains(page.Rows, r => r.EntityId == slotId);
    }

    [Fact]
    public async Task SearchWithNoAllowedEntityTypesReturnsAnEmptyPage()
    {
        await fixture.ResetAsync();
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, Guid.NewGuid(), AuditAction.SlotConfirmed);

        await using var context = fixture.NewContext();
        var page = await new AuditQueries(context).SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, [], null, null, 50),
            CancellationToken.None);

        Assert.Empty(page.Rows);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task IdentifierMatchesEntityIdAndActorId()
    {
        await fixture.ResetAsync();
        var entityId = Guid.NewGuid();
        const string actor = "staff-actor-7";
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, entityId, AuditAction.SlotConfirmed, actor: actor);

        await using var context = fixture.NewContext();
        var queries = new AuditQueries(context);
        var byEntity = await queries.SearchAsync(
            new AuditSearchFilter(null, null, null, null, entityId.ToString(), AuditEntityTypes.All, null, null, 50),
            CancellationToken.None);
        Assert.Contains(byEntity.Rows, r => r.EntityId == entityId);

        var byActor = await queries.SearchAsync(
            new AuditSearchFilter(null, null, null, null, actor, AuditEntityTypes.All, null, null, 50),
            CancellationToken.None);
        Assert.Contains(byActor.Rows, r => r.EntityId == entityId);
    }

    [Fact]
    public async Task SearchFiltersByTimestampRangeActionAndActorType()
    {
        await fixture.ResetAsync();
        var wanted = Guid.NewGuid();
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, wanted, AuditAction.SlotCancelled, Now);
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, Guid.NewGuid(), AuditAction.SlotConfirmed, Now);
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, Guid.NewGuid(), AuditAction.SlotCancelled, Now.AddDays(-30));

        await using var context = fixture.NewContext();
        var page = await new AuditQueries(context).SearchAsync(
            new AuditSearchFilter(
                Now.AddHours(-1), Now.AddHours(1), "Staff", nameof(AuditAction.SlotCancelled),
                null, AuditEntityTypes.All, null, null, 50),
            CancellationToken.None);

        var row = Assert.Single(page.Rows);
        Assert.Equal(wanted, row.EntityId);
    }

    [Fact]
    public async Task SearchTreatsAMalformedCursorAsAbsent()
    {
        await fixture.ResetAsync();
        var slotId = Guid.NewGuid();
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, slotId, AuditAction.SlotConfirmed);

        await using var context = fixture.NewContext();
        var page = await new AuditQueries(context).SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, AuditEntityTypes.All, null, "not-a-cursor", 50),
            CancellationToken.None);

        Assert.Contains(page.Rows, r => r.EntityId == slotId);
    }

    /// <summary>Seeds a candidate with an invite, booking, and booking appointment; returns the appointment id.</summary>
    private async Task<Guid> SeedCandidateBookingAsync(Guid candidateId)
    {
        await using var write = fixture.NewContext();
        var pilots = write.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.Pilots);
        var candidate = Candidate.Create(candidateId, "Amara Novak", "a.novak@mail.com", pilots);
        var slotId = Guid.NewGuid();
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidateId, "hash", Now.AddDays(4),
            [slotId, Guid.NewGuid(), Guid.NewGuid()],
            candidate.RequiredAppointmentTypeIds, 0);
        var booking = Booking.Create(Guid.NewGuid(), invite, slotId, "manage-token-hash", Now);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting);

        write.Candidates.Add(candidate);
        write.Invites.Add(invite);
        write.Bookings.Add(booking);
        write.BookingAppointments.Add(appointment);
        await write.SaveChangesAsync();

        return appointment.Id;
    }

    /// <summary>Appends one audit-log row and returns the audited entity id.</summary>
    private async Task<Guid> AddAuditAsync(
        string entityType,
        Guid entityId,
        AuditAction action,
        DateTimeOffset? timestamp = null,
        string? actor = "staff-1")
    {
        await using var write = fixture.NewContext();
        write.AuditLogs.Add(AuditLog.Record(
            Guid.NewGuid(), entityType, entityId, action,
            actor is null ? ActorType.System : ActorType.Staff, actor, timestamp ?? Now, null));
        await write.SaveChangesAsync();
        return entityId;
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/AuditQueryTests.cs — 1/1

<!-- vocabulary-file: {"id":324,"oldPath":"tests/EventBooking.Infrastructure.Tests/AuditQueryTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/AuditQueryTests.cs","beforeSha":"7ec6ae65c9227bfeb29067b7eb8214af5429bb32721d46f799de507e230fa79f","afterSha":"86ef5a01fa0531b189cf34afc958ee3d3fd95250463de11d753fe1c87c337838","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class AuditQueryTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AEventsHistoryComesBackNewestFirst()
    {
        await fixture.ResetAsync();
        var eventId = Guid.NewGuid();

        await using (var write = fixture.NewContext())
        {
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Event, eventId, AuditAction.EventConfirmed,
                ActorType.Staff, "staff-1", Now, null));
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Event, eventId, AuditAction.EventCancelled,
                ActorType.Staff, "staff-2", Now.AddHours(1), "6 bookings voided"));
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Event, Guid.NewGuid(), AuditAction.EventConfirmed,
                ActorType.Staff, "staff-3", Now, null));
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var rows = await new AuditQueries(read).ForEntityAsync(
            AuditEntityTypes.Event, eventId, CancellationToken.None);

        Assert.Equal(2, rows.Count);
        Assert.Equal("EventCancelled", rows[0].Action);
        Assert.Equal("6 bookings voided", rows[0].Details);
        Assert.Equal("EventConfirmed", rows[1].Action);
        Assert.Equal("Staff", rows[1].ActorType);
    }

    [Fact]
    public async Task AAttendeesHistoryGathersTheirInviteAndBookingEntries()
    {
        await fixture.ResetAsync();

        var attendeeId = Guid.NewGuid();
        var inviteId = Guid.NewGuid();

        await using (var write = fixture.NewContext())
        {
            var pilots = write.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
            var attendee = Attendee.Create(
                attendeeId, "Amara Novak", "a.novak@mail.com", pilots);
            write.Attendees.Add(attendee);
            write.Invites.Add(Invite.CreateInitial(
                inviteId, attendee.Id, "hash", Now.AddDays(4),
                [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
                attendee.RequiredAppointmentTypeIds, 0));

            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Invite, inviteId, AuditAction.InviteCreated,
                ActorType.System, null, Now, "retry 0"));
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Invite, Guid.NewGuid(), AuditAction.InviteCreated,
                ActorType.System, null, Now, "someone else"));

            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var rows = await new AuditQueries(read).ForAttendeeAsync(attendeeId, CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal("InviteCreated", row.Action);
        Assert.Equal("retry 0", row.Details);
        Assert.Equal("System", row.ActorType);
        Assert.Null(row.ActorId);
    }

    [Fact]
    public async Task AAttendeeWithNoHistoryGetsAnEmptyList()
    {
        await fixture.ResetAsync();

        await using var read = fixture.NewContext();
        var rows = await new AuditQueries(read).ForAttendeeAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task ForAttendeeIncludesBookingAppointmentEvents()
    {
        await fixture.ResetAsync();
        var attendeeId = Guid.NewGuid();
        var appointmentId = await SeedAttendeeBookingAsync(attendeeId);
        await AddAuditAsync(AuditEntityTypes.BookingAppointment, appointmentId, AuditAction.AppointmentCheckedIn);

        await using var context = fixture.NewContext();
        var rows = await new AuditQueries(context).ForAttendeeAsync(attendeeId, CancellationToken.None);

        Assert.Contains(rows, r => r.EntityId == appointmentId);
    }

    [Fact]
    public async Task ForAttendeeIncludesEntriesRecordedAgainstTheAttendeeItself()
    {
        await fixture.ResetAsync();
        var attendeeId = Guid.NewGuid();
        await SeedAttendeeBookingAsync(attendeeId);
        await AddAuditAsync(AuditEntityTypes.Attendee, attendeeId, AuditAction.AttendeeGroupAssigned, Now.AddHours(-1));
        await AddAuditAsync(AuditEntityTypes.Attendee, Guid.NewGuid(), AuditAction.AttendeeGroupReassigned);
        await AddAuditAsync(AuditEntityTypes.Event, attendeeId, AuditAction.EventConfirmed);

        await using var context = fixture.NewContext();
        var rows = await new AuditQueries(context).ForAttendeeAsync(attendeeId, CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal(AuditEntityTypes.Attendee, row.EntityType);
        Assert.Equal(nameof(AuditAction.AttendeeGroupAssigned), row.Action);
    }

    [Fact]
    public async Task SearchOrdersNewestFirstWithStableCursor()
    {
        await fixture.ResetAsync();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await AddAuditAsync(AuditEntityTypes.Event, first, AuditAction.EventConfirmed, Now.AddHours(-2));
        await AddAuditAsync(AuditEntityTypes.Event, second, AuditAction.EventCancelled, Now.AddHours(-1));

        await using var context = fixture.NewContext();
        var queries = new AuditQueries(context);
        var page1 = await queries.SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, AuditEntityTypes.All, null, null, 1),
            CancellationToken.None);
        Assert.Single(page1.Rows);
        Assert.Equal(second, page1.Rows[0].EntityId);
        Assert.NotNull(page1.NextCursor);

        // A row newer than the cursor must not shift the following page.
        await AddAuditAsync(AuditEntityTypes.Event, Guid.NewGuid(), AuditAction.EventConfirmed, Now);

        var page2 = await queries.SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, AuditEntityTypes.All, null, page1.NextCursor, 10),
            CancellationToken.None);
        Assert.Contains(page2.Rows, r => r.EntityId == first);
        Assert.DoesNotContain(page2.Rows, r => r.EntityId == second);
    }

    [Fact]
    public async Task SearchRestrictedToOperationalBucketNeverReturnsAttendeeRows()
    {
        await fixture.ResetAsync();
        await AddAuditAsync(AuditEntityTypes.Booking, Guid.NewGuid(), AuditAction.BookingCreated);
        var eventId = Guid.NewGuid();
        await AddAuditAsync(AuditEntityTypes.Event, eventId, AuditAction.EventConfirmed);

        await using var context = fixture.NewContext();
        IReadOnlyList<string> operational =
            [AuditEntityTypes.EventProposal, AuditEntityTypes.Event, AuditEntityTypes.StaffAccessProfile];
        var page = await new AuditQueries(context).SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, operational, null, null, 50),
            CancellationToken.None);

        Assert.All(page.Rows, r => Assert.Contains(r.EntityType, operational));
        Assert.Contains(page.Rows, r => r.EntityId == eventId);
    }

    [Fact]
    public async Task SearchWithNoAllowedEntityTypesReturnsAnEmptyPage()
    {
        await fixture.ResetAsync();
        await AddAuditAsync(AuditEntityTypes.Event, Guid.NewGuid(), AuditAction.EventConfirmed);

        await using var context = fixture.NewContext();
        var page = await new AuditQueries(context).SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, [], null, null, 50),
            CancellationToken.None);

        Assert.Empty(page.Rows);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task IdentifierMatchesEntityIdAndActorId()
    {
        await fixture.ResetAsync();
        var entityId = Guid.NewGuid();
        const string actor = "staff-actor-7";
        await AddAuditAsync(AuditEntityTypes.Event, entityId, AuditAction.EventConfirmed, actor: actor);

        await using var context = fixture.NewContext();
        var queries = new AuditQueries(context);
        var byEntity = await queries.SearchAsync(
            new AuditSearchFilter(null, null, null, null, entityId.ToString(), AuditEntityTypes.All, null, null, 50),
            CancellationToken.None);
        Assert.Contains(byEntity.Rows, r => r.EntityId == entityId);

        var byActor = await queries.SearchAsync(
            new AuditSearchFilter(null, null, null, null, actor, AuditEntityTypes.All, null, null, 50),
            CancellationToken.None);
        Assert.Contains(byActor.Rows, r => r.EntityId == entityId);
    }

    [Fact]
    public async Task SearchFiltersByTimestampRangeActionAndActorType()
    {
        await fixture.ResetAsync();
        var wanted = Guid.NewGuid();
        await AddAuditAsync(AuditEntityTypes.Event, wanted, AuditAction.EventCancelled, Now);
        await AddAuditAsync(AuditEntityTypes.Event, Guid.NewGuid(), AuditAction.EventConfirmed, Now);
        await AddAuditAsync(AuditEntityTypes.Event, Guid.NewGuid(), AuditAction.EventCancelled, Now.AddDays(-30));

        await using var context = fixture.NewContext();
        var page = await new AuditQueries(context).SearchAsync(
            new AuditSearchFilter(
                Now.AddHours(-1), Now.AddHours(1), "Staff", nameof(AuditAction.EventCancelled),
                null, AuditEntityTypes.All, null, null, 50),
            CancellationToken.None);

        var row = Assert.Single(page.Rows);
        Assert.Equal(wanted, row.EntityId);
    }

    [Fact]
    public async Task SearchTreatsAMalformedCursorAsAbsent()
    {
        await fixture.ResetAsync();
        var eventId = Guid.NewGuid();
        await AddAuditAsync(AuditEntityTypes.Event, eventId, AuditAction.EventConfirmed);

        await using var context = fixture.NewContext();
        var page = await new AuditQueries(context).SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, AuditEntityTypes.All, null, "not-a-cursor", 50),
            CancellationToken.None);

        Assert.Contains(page.Rows, r => r.EntityId == eventId);
    }

    /// <summary>Seeds a attendee with an invite, booking, and booking appointment; returns the appointment id.</summary>
    private async Task<Guid> SeedAttendeeBookingAsync(Guid attendeeId)
    {
        await using var write = fixture.NewContext();
        var pilots = write.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
        var attendee = Attendee.Create(attendeeId, "Amara Novak", "a.novak@mail.com", pilots);
        var eventId = Guid.NewGuid();
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendeeId, "hash", Now.AddDays(4),
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds, 0);
        var booking = Booking.Create(Guid.NewGuid(), invite, eventId, "manage-token-hash", Now);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting);

        write.Attendees.Add(attendee);
        write.Invites.Add(invite);
        write.Bookings.Add(booking);
        write.BookingAppointments.Add(appointment);
        await write.SaveChangesAsync();

        return appointment.Id;
    }

    /// <summary>Appends one audit-log row and returns the audited entity id.</summary>
    private async Task<Guid> AddAuditAsync(
        string entityType,
        Guid entityId,
        AuditAction action,
        DateTimeOffset? timestamp = null,
        string? actor = "staff-1")
    {
        await using var write = fixture.NewContext();
        write.AuditLogs.Add(AuditLog.Record(
            Guid.NewGuid(), entityType, entityId, action,
            actor is null ? ActorType.System : ActorType.Staff, actor, timestamp ?? Now, null));
        await write.SaveChangesAsync();
        return entityId;
    }
}
`````
