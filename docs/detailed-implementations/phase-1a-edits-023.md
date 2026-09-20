# 01a — Variable-length windows in the location's zone, edits 23 (Task 4)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs — 1/1

<!-- retirement-file: {"id":70,"file":"tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs","beforeSha":"916d9c214947e3f5dbdb7de2979d486665d6385d4d5ac9aaa7ecc3f5df22d023","afterSha":"c81d671ba4a285b9f7351bd55c91d227fc30dbe35c3d5e94e4de8d6d8e18e83b","side":"before","part":1,"parts":1} -->

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
        var eventItem = EventFixture.Create(
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

## after — tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs — 1/1

<!-- retirement-file: {"id":70,"file":"tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs","beforeSha":"916d9c214947e3f5dbdb7de2979d486665d6385d4d5ac9aaa7ecc3f5df22d023","afterSha":"c81d671ba4a285b9f7351bd55c91d227fc30dbe35c3d5e94e4de8d6d8e18e83b","side":"after","part":1,"parts":1} -->

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
        var eventItem = EventFixture.Create(
            Guid.NewGuid(),
            new EventWindow(date, new TimeOnly(9, 0), 240),
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

## before — tests/EventBooking.Infrastructure.Tests/AttendeeBookingQueryTests.cs — 1/1

<!-- retirement-file: {"id":71,"file":"tests/EventBooking.Infrastructure.Tests/AttendeeBookingQueryTests.cs","beforeSha":"00d311b15be456ac93a6687c40288d13b2c5fc53e855ef2699659b8200bc7c95","afterSha":"198273aa3e56c1ad4d9eb24ed3f105429a076f1f0ec22454effc7ef62baa3fea","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies the staff active-booking listing, its ordering, and its derived window end.</summary>
[Collection("postgres")]
public sealed class AttendeeBookingQueryTests(PostgresFixture fixture)
{
    [Fact]
    public async Task AnUnknownAttendeeReturnsNull()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var rows = await new AttendeeBookingQueries(context)
            .ListActiveForAttendeeAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(rows);
    }

    [Fact]
    public async Task AAttendeeWithNoActiveBookingReturnsAnEmptyList()
    {
        await fixture.ResetAsync();
        var attendeeId = await SeedAttendeeAsync();

        await using var context = fixture.NewContext();
        var rows = await new AttendeeBookingQueries(context)
            .ListActiveForAttendeeAsync(attendeeId, CancellationToken.None);

        Assert.NotNull(rows);
        Assert.Empty(rows!);
    }

    [Fact]
    public async Task TheOriginalSortsFirstAndEachWindowEndIsDerived()
    {
        await fixture.ResetAsync();
        var attendeeId = await SeedAttendeeAsync();
        var originalEvent = await SeedEventAsync(new DateOnly(2026, 9, 10), new TimeOnly(9, 0));
        var recoveryEvent = await SeedEventAsync(new DateOnly(2026, 9, 12), new TimeOnly(13, 0));

        var original = OriginalFor(attendeeId, originalEvent);
        var recovery = RecoveryFor(attendeeId, original, recoveryEvent);
        await using (var write = fixture.NewContext())
        {
            write.Bookings.AddRange(original, recovery);
            await write.SaveChangesAsync();
        }

        await using var context = fixture.NewContext();
        var rows = await new AttendeeBookingQueries(context)
            .ListActiveForAttendeeAsync(attendeeId, CancellationToken.None);

        Assert.NotNull(rows);
        Assert.Equal(2, rows!.Count);

        Assert.True(rows[0].IsOriginal);
        Assert.Equal(original.Id, rows[0].BookingId);
        Assert.Equal(new DateOnly(2026, 9, 10), rows[0].EventDate);
        Assert.Equal(new TimeOnly(9, 0), rows[0].EventStartTime);
        Assert.Equal(new TimeOnly(13, 0), rows[0].EventEndTime);

        Assert.False(rows[1].IsOriginal);
        Assert.Equal(recovery.Id, rows[1].BookingId);
        Assert.Equal(new TimeOnly(13, 0), rows[1].EventStartTime);
        Assert.Equal(new TimeOnly(17, 0), rows[1].EventEndTime);
    }

    [Fact]
    public async Task ACancelledBookingIsNotListed()
    {
        await fixture.ResetAsync();
        var attendeeId = await SeedAttendeeAsync();
        var eventId = await SeedEventAsync(new DateOnly(2026, 9, 10), new TimeOnly(9, 0));
        var booking = OriginalFor(attendeeId, eventId);
        booking.Cancel();

        await using (var write = fixture.NewContext())
        {
            write.Bookings.Add(booking);
            await write.SaveChangesAsync();
        }

        await using var context = fixture.NewContext();
        var rows = await new AttendeeBookingQueries(context)
            .ListActiveForAttendeeAsync(attendeeId, CancellationToken.None);

        Assert.NotNull(rows);
        Assert.Empty(rows!);
    }

    private async Task<Guid> SeedAttendeeAsync()
    {
        await using var write = fixture.NewContext();
        var pilots = await write.AttendeeGroups
            .Include(g => g.Requirements)
            .SingleAsync(g => g.Id == AttendeeGroupIds.Pilots);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", $"a.novak.{Guid.NewGuid():N}@mail.com", pilots);
        write.Attendees.Add(attendee);
        await write.SaveChangesAsync();
        return attendee.Id;
    }

    private async Task<Guid> SeedEventAsync(DateOnly date, TimeOnly startTime)
    {
        var proposal = EventProposal.Create(Guid.NewGuid(), new EventWindow(date, startTime), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 10);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        await using var write = fixture.NewContext();
        write.EventProposals.Add(proposal);
        write.Events.Add(eventItem);
        await write.SaveChangesAsync();
        return eventItem.Id;
    }

    private static Booking OriginalFor(Guid attendeeId, Guid eventId)
    {
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendeeId, $"initial-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.AddDays(1),
            [eventId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        return Booking.Create(
            Guid.NewGuid(), invite, eventId, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
    }

    private static Booking RecoveryFor(Guid attendeeId, Booking original, Guid eventId)
    {
        var invite = Invite.CreateRecovery(
            Guid.NewGuid(), attendeeId, original.Id, $"recovery-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(2),
            [eventId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);
        return Booking.CreateRecovery(
            Guid.NewGuid(), invite, original, eventId, $"manage-recovery-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddHours(1));
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/AttendeeBookingQueryTests.cs — 1/1

<!-- retirement-file: {"id":71,"file":"tests/EventBooking.Infrastructure.Tests/AttendeeBookingQueryTests.cs","beforeSha":"00d311b15be456ac93a6687c40288d13b2c5fc53e855ef2699659b8200bc7c95","afterSha":"198273aa3e56c1ad4d9eb24ed3f105429a076f1f0ec22454effc7ef62baa3fea","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies the staff active-booking listing, its ordering, and its derived window end.</summary>
[Collection("postgres")]
public sealed class AttendeeBookingQueryTests(PostgresFixture fixture)
{
    [Fact]
    public async Task AnUnknownAttendeeReturnsNull()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var rows = await new AttendeeBookingQueries(context)
            .ListActiveForAttendeeAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(rows);
    }

    [Fact]
    public async Task AAttendeeWithNoActiveBookingReturnsAnEmptyList()
    {
        await fixture.ResetAsync();
        var attendeeId = await SeedAttendeeAsync();

        await using var context = fixture.NewContext();
        var rows = await new AttendeeBookingQueries(context)
            .ListActiveForAttendeeAsync(attendeeId, CancellationToken.None);

        Assert.NotNull(rows);
        Assert.Empty(rows!);
    }

    [Fact]
    public async Task TheOriginalSortsFirstAndEachWindowEndIsDerived()
    {
        await fixture.ResetAsync();
        var attendeeId = await SeedAttendeeAsync();
        var originalEvent = await SeedEventAsync(new DateOnly(2026, 9, 10), new TimeOnly(9, 0));
        var recoveryEvent = await SeedEventAsync(new DateOnly(2026, 9, 12), new TimeOnly(13, 0));

        var original = OriginalFor(attendeeId, originalEvent);
        var recovery = RecoveryFor(attendeeId, original, recoveryEvent);
        await using (var write = fixture.NewContext())
        {
            write.Bookings.AddRange(original, recovery);
            await write.SaveChangesAsync();
        }

        await using var context = fixture.NewContext();
        var rows = await new AttendeeBookingQueries(context)
            .ListActiveForAttendeeAsync(attendeeId, CancellationToken.None);

        Assert.NotNull(rows);
        Assert.Equal(2, rows!.Count);

        Assert.True(rows[0].IsOriginal);
        Assert.Equal(original.Id, rows[0].BookingId);
        Assert.Equal(new DateOnly(2026, 9, 10), rows[0].EventDate);
        Assert.Equal(new TimeOnly(9, 0), rows[0].EventStartTime);
        Assert.Equal(new TimeOnly(13, 0), rows[0].EventEndTime);

        Assert.False(rows[1].IsOriginal);
        Assert.Equal(recovery.Id, rows[1].BookingId);
        Assert.Equal(new TimeOnly(13, 0), rows[1].EventStartTime);
        Assert.Equal(new TimeOnly(17, 0), rows[1].EventEndTime);
    }

    [Fact]
    public async Task ACancelledBookingIsNotListed()
    {
        await fixture.ResetAsync();
        var attendeeId = await SeedAttendeeAsync();
        var eventId = await SeedEventAsync(new DateOnly(2026, 9, 10), new TimeOnly(9, 0));
        var booking = OriginalFor(attendeeId, eventId);
        booking.Cancel();

        await using (var write = fixture.NewContext())
        {
            write.Bookings.Add(booking);
            await write.SaveChangesAsync();
        }

        await using var context = fixture.NewContext();
        var rows = await new AttendeeBookingQueries(context)
            .ListActiveForAttendeeAsync(attendeeId, CancellationToken.None);

        Assert.NotNull(rows);
        Assert.Empty(rows!);
    }

    private async Task<Guid> SeedAttendeeAsync()
    {
        await using var write = fixture.NewContext();
        var pilots = await write.AttendeeGroups
            .Include(g => g.Requirements)
            .SingleAsync(g => g.Id == AttendeeGroupIds.Pilots);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", $"a.novak.{Guid.NewGuid():N}@mail.com", pilots);
        write.Attendees.Add(attendee);
        await write.SaveChangesAsync();
        return attendee.Id;
    }

    private async Task<Guid> SeedEventAsync(DateOnly date, TimeOnly startTime)
    {
        var proposal = EventProposal.Create(Guid.NewGuid(), new EventWindow(date, startTime, 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 10);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        await using var write = fixture.NewContext();
        write.EventProposals.Add(proposal);
        write.Events.Add(eventItem);
        await write.SaveChangesAsync();
        return eventItem.Id;
    }

    private static Booking OriginalFor(Guid attendeeId, Guid eventId)
    {
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendeeId, $"initial-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.AddDays(1),
            [eventId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        return Booking.Create(
            Guid.NewGuid(), invite, eventId, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
    }

    private static Booking RecoveryFor(Guid attendeeId, Booking original, Guid eventId)
    {
        var invite = Invite.CreateRecovery(
            Guid.NewGuid(), attendeeId, original.Id, $"recovery-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(2),
            [eventId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);
        return Booking.CreateRecovery(
            Guid.NewGuid(), invite, original, eventId, $"manage-recovery-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddHours(1));
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs — 1/1

<!-- retirement-file: {"id":72,"file":"tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs","beforeSha":"50028bb606e158e5cbf269be0cdc101b6bb928ef0ab5db9e06fdb8ed26d963a3","afterSha":"f6477b154810e6828dcffc6714027501357288175fdb2d5c9d074a1e1053b23a","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Appointments;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Audit;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Repositories;
using EventBooking.Infrastructure.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies PostgreSQL serialises distinct booking-appointment status writers.</summary>
[Collection("postgres")]
public sealed class BookingAppointmentConcurrencyTests(PostgresFixture fixture)
{
    /// <summary>Verifies exactly one concurrent transition commits from the same expected version.</summary>
    [Fact]
    public async Task ConcurrentDistinctTransitionsCannotSilentlyOverwrite()
    {
        await fixture.ResetAsync();
        var now = new DateTimeOffset(2026, 9, 7, 14, 0, 0, TimeSpan.Zero);
        var staff = Guid.NewGuid();
        Guid appointmentId;

        await using (var seed = fixture.NewContext())
        {
            seed.StaffAccessProfiles.Add(StaffAccessProfile.Create(
                staff, [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting));
            var pilots = seed.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
            var attendee = Attendee.Create(
                Guid.NewGuid(), "Amara Novak", "amara@example.com", pilots);
            var eventItem = EventFixture.Create(
                Guid.NewGuid(),
                new EventWindow(new DateOnly(2026, 9, 7), new TimeOnly(9, 0)),
                AppointmentTypeIds.All.ToDictionary(value => value, _ => 10));
            var invite = Invite.CreateInitial(
                Guid.NewGuid(), attendee.Id, "invite-token", now.AddDays(1),
                [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()], attendee.RequiredAppointmentTypeIds, 0);
            var booking = Booking.Create(
                Guid.NewGuid(), invite, eventItem.Id, "manage-token", now.AddDays(-1));
            var appointment = BookingAppointment.Create(
                Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting);
            appointmentId = appointment.Id;
            seed.Attendees.Add(attendee);
            seed.Events.Add(eventItem);
            seed.Bookings.Add(booking);
            seed.BookingAppointments.Add(appointment);
            await seed.SaveChangesAsync();
        }

        var first = ExecuteAsync(BookingAppointmentStatus.CheckedIn);
        var second = ExecuteAsync(BookingAppointmentStatus.NoShow);
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, result => result.IsSuccess);
        Assert.Single(results,
            result => result.IsFailure
                && result.Error.Code == "appointment_version_conflict");

        await using var read = fixture.NewContext();
        var stored = await read.BookingAppointments.AsNoTracking().SingleAsync();
        Assert.Equal(2, stored.Version);
        Assert.Contains(
            stored.Status,
            new[] { BookingAppointmentStatus.CheckedIn, BookingAppointmentStatus.NoShow });
        Assert.Single(await read.AuditLogs.AsNoTracking()
            .Where(entry => entry.EntityType == "BookingAppointment")
            .ToListAsync());

        async Task<EventBooking.Application.Common.Result<BookingAppointmentUpdateView>>
            ExecuteAsync(BookingAppointmentStatus status)
        {
            await using var context = fixture.NewContext();
            var handler = new UpdateBookingAppointmentStatusHandler(
                new StaffAccessAuthorizer(new StaffAccessProfileRepository(context)),
                new BookingAppointmentRepository(context),
                new BookingRepository(context),
                new AttendeeRepository(context),
                new InviteRepository(context),
                new EventRepository(context),
                new RecoveryBookingOutcomeCoordinator(),
                new EfAuditLogger(context, new FixedClock(now)),
                new UnitOfWork(context),
                new FixedClock(now));
            return await handler.HandleAsync(
                new UpdateBookingAppointmentStatusCommand
                {
                    StaffUserId = staff,
                    BookingAppointmentId = appointmentId,
                    Status = status,
                    ExpectedVersion = 1,
                },
                CancellationToken.None);
        }
    }

    /// <summary>
    /// A no-show correction racing recovery issuance leaves exactly one winner: the loser
    /// sees recovery_state_changed, recovery_not_available, or the cancel-recovery-first
    /// conflict, and a pending recovery never coexists with corrected Expected state.
    /// </summary>
    [Fact]
    public async Task CorrectionRacingIssuanceLeavesExactlyOneWinner()
    {
        await fixture.ResetAsync();
        var now = new DateTimeOffset(2026, 9, 7, 14, 0, 0, TimeSpan.Zero);
        var coordinator = Guid.NewGuid();
        var appointmentStaff = Guid.NewGuid();
        Guid attendeeId;
        Guid appointmentId;

        var ids = await GivenCorrectionRaceAsync(now, coordinator, appointmentStaff);
        attendeeId = ids.AttendeeId;
        appointmentId = ids.AppointmentId;

        var correctTask = CorrectAsync();
        var issueTask = IssueAsync();
        await Task.WhenAll(correctTask, issueTask);

        var correctResult = await correctTask;
        var issueResult = await issueTask;
        Assert.True(correctResult.IsSuccess ^ issueResult.IsSuccess);

        await using var read = fixture.NewContext();
        var stored = await read.BookingAppointments.AsNoTracking().SingleAsync();
        var pendingRecovery = await read.Invites.AnyAsync(invite =>
            invite.AttendeeId == attendeeId
            && invite.Status == InviteStatus.Pending
            && invite.RecoveryOfBookingId != null);
        Assert.False(pendingRecovery && stored.Status == BookingAppointmentStatus.Expected);

        if (correctResult.IsSuccess)
        {
            Assert.True(issueResult.IsFailure);
            Assert.Contains(
                issueResult.Error.Code, new[] { "recovery_not_available", "recovery_state_changed" });
            Assert.Equal(BookingAppointmentStatus.Expected, stored.Status);
            Assert.Equal(3, stored.Version);
            Assert.False(pendingRecovery);
        }
        else
        {
            Assert.Equal("conflict", correctResult.Error.Code);
            Assert.Contains("Cancel the recovery first", correctResult.Error.Message);
            Assert.True(issueResult.IsSuccess);
            Assert.Equal(BookingAppointmentStatus.NoShow, stored.Status);
            Assert.True(pendingRecovery);
        }

        async Task<EventBooking.Application.Common.Result<BookingAppointmentUpdateView>> CorrectAsync()
        {
            await using var context = fixture.NewContext();
            var handler = new UpdateBookingAppointmentStatusHandler(
                new StaffAccessAuthorizer(new StaffAccessProfileRepository(context)),
                new BookingAppointmentRepository(context),
                new BookingRepository(context),
                new AttendeeRepository(context),
                new InviteRepository(context),
                new EventRepository(context),
                new RecoveryBookingOutcomeCoordinator(),
                new EfAuditLogger(context, new FixedClock(now)),
                new UnitOfWork(context),
                new FixedClock(now));
            return await handler.HandleAsync(
                new UpdateBookingAppointmentStatusCommand
                {
                    StaffUserId = appointmentStaff,
                    BookingAppointmentId = appointmentId,
                    Status = BookingAppointmentStatus.Expected,
                    ExpectedVersion = 2,
                },
                CancellationToken.None);
        }

        async Task<EventBooking.Application.Common.Result<StartRecoveryResult>> IssueAsync()
        {
            await using var context = fixture.NewContext();
            var clock = new FixedClock(now);
            var unitOfWork = new UnitOfWork(context);
            var audit = new EfAuditLogger(context, clock);
            var tokens = new HmacTokenService(
                new TokenOptions("a-correction-race-signing-key-long-enough"));
            var events = new EventRepository(context);
            var deliveries = new EmailDeliveryService(
                new EmailDeliveryRepository(context), new SilentSender(), unitOfWork, clock,
                NullLogger<EmailDeliveryService>.Instance);
            var issuer = new InviteIssuer(
                new InviteRepository(context),
                new AttendeeGroupRepository(context),
                new EligibleEventFinder(events, clock),
                new SystemSettingsRepository(context),
                tokens,
                deliveries,
                audit,
                clock,
                new AttendeePortalOptions(
                    "https://booking.example.com", "recruitment@example.com"));
            var handler = new StartRecoveryHandler(
                new AttendeeRepository(context),
                new StaffAccessAuthorizer(new StaffAccessProfileRepository(context)),
                new InviteRepository(context),
                new BookingRepository(context),
                new BookingAppointmentRepository(context),
                issuer,
                new EligibleEventFinder(events, clock),
                deliveries,
                unitOfWork);
            return await handler.HandleAsync(
                new StartRecoveryCommand(coordinator, attendeeId), CancellationToken.None);
        }
    }

    /// <summary>
    /// Pins the issuance-wins branch sequentially: a pending recovery invite makes the
    /// later no-show correction fail with the cancel-recovery-first conflict.
    /// </summary>
    [Fact]
    public async Task IssuingFirstMakesCorrectionConflict()
    {
        await fixture.ResetAsync();
        var now = new DateTimeOffset(2026, 9, 7, 14, 0, 0, TimeSpan.Zero);
        var coordinator = Guid.NewGuid();
        var appointmentStaff = Guid.NewGuid();
        Guid attendeeId;
        Guid appointmentId;

        var ids = await GivenCorrectionRaceAsync(now, coordinator, appointmentStaff);
        attendeeId = ids.AttendeeId;
        appointmentId = ids.AppointmentId;

        await using var issueContext = fixture.NewContext();
        var issueResult = await IssueAsync(issueContext, now, coordinator, attendeeId);
        Assert.True(issueResult.IsSuccess);

        await using var correctContext = fixture.NewContext();
        var correctResult = await CorrectAsync(
            correctContext, now, appointmentStaff, appointmentId);

        Assert.True(correctResult.IsFailure);
        Assert.Equal("conflict", correctResult.Error.Code);
        Assert.Contains("Cancel the recovery first", correctResult.Error.Message);

        await using var read = fixture.NewContext();
        var stored = await read.BookingAppointments.AsNoTracking().SingleAsync();
        Assert.Equal(BookingAppointmentStatus.NoShow, stored.Status);
        Assert.True(await read.Invites.AnyAsync(invite =>
            invite.AttendeeId == attendeeId
            && invite.Status == InviteStatus.Pending
            && invite.RecoveryOfBookingId != null));

        async Task<EventBooking.Application.Common.Result<BookingAppointmentUpdateView>> CorrectAsync(
            EventBookingDbContext context,
            DateTimeOffset at,
            Guid staff,
            Guid appointment)
        {
            var handler = new UpdateBookingAppointmentStatusHandler(
                new StaffAccessAuthorizer(new StaffAccessProfileRepository(context)),
                new BookingAppointmentRepository(context),
                new BookingRepository(context),
                new AttendeeRepository(context),
                new InviteRepository(context),
                new EventRepository(context),
                new RecoveryBookingOutcomeCoordinator(),
                new EfAuditLogger(context, new FixedClock(at)),
                new UnitOfWork(context),
                new FixedClock(at));
            return await handler.HandleAsync(
                new UpdateBookingAppointmentStatusCommand
                {
                    StaffUserId = staff,
                    BookingAppointmentId = appointment,
                    Status = BookingAppointmentStatus.Expected,
                    ExpectedVersion = 2,
                },
                CancellationToken.None);
        }

        async Task<EventBooking.Application.Common.Result<StartRecoveryResult>> IssueAsync(
            EventBookingDbContext context,
            DateTimeOffset at,
            Guid staff,
            Guid attendee)
        {
            var clock = new FixedClock(at);
            var unitOfWork = new UnitOfWork(context);
            var audit = new EfAuditLogger(context, clock);
            var tokens = new HmacTokenService(
                new TokenOptions("a-correction-race-signing-key-long-enough"));
            var events = new EventRepository(context);
            var deliveries = new EmailDeliveryService(
                new EmailDeliveryRepository(context), new SilentSender(), unitOfWork, clock,
                NullLogger<EmailDeliveryService>.Instance);
            var issuer = new InviteIssuer(
                new InviteRepository(context),
                new AttendeeGroupRepository(context),
                new EligibleEventFinder(events, clock),
                new SystemSettingsRepository(context),
                tokens,
                deliveries,
                audit,
                clock,
                new AttendeePortalOptions(
                    "https://booking.example.com", "recruitment@example.com"));
            var handler = new StartRecoveryHandler(
                new AttendeeRepository(context),
                new StaffAccessAuthorizer(new StaffAccessProfileRepository(context)),
                new InviteRepository(context),
                new BookingRepository(context),
                new BookingAppointmentRepository(context),
                issuer,
                new EligibleEventFinder(events, clock),
                deliveries,
                unitOfWork);
            return await handler.HandleAsync(
                new StartRecoveryCommand(staff, attendee), CancellationToken.None);
        }
    }

    private async Task<(Guid AttendeeId, Guid AppointmentId)> GivenCorrectionRaceAsync(
        DateTimeOffset now,
        Guid coordinator,
        Guid appointmentStaff)
    {
        Guid attendeeId;
        Guid appointmentId;

        await using (var seed = fixture.NewContext())
        {
            seed.StaffAccessProfiles.Add(StaffAccessProfile.Create(
                coordinator, [Role.Coordinator], null));
            seed.StaffAccessProfiles.Add(StaffAccessProfile.Create(
                appointmentStaff, [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting));
            var group = AttendeeGroup.Define(
                Guid.NewGuid(), $"DAT_ONLY_{Guid.NewGuid():N}".ToUpperInvariant(), "DAT only", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting]);
            var attendee = Attendee.Create(
                Guid.NewGuid(), "Amara Novak", "amara@example.com", group);
            attendee.MarkInvited();
            attendee.MarkBooked();
            attendeeId = attendee.Id;
            var pastEvent = EventFixture.Create(
                Guid.NewGuid(),
                new EventWindow(new DateOnly(2026, 9, 7), new TimeOnly(9, 0)),
                AppointmentTypeIds.All.ToDictionary(value => value, _ => 10));
            var invite = Invite.CreateInitial(
                Guid.NewGuid(), attendee.Id, "invite-token", now.AddDays(1),
                [pastEvent.Id, Guid.NewGuid(), Guid.NewGuid()],
                attendee.RequiredAppointmentTypeIds, 0);
            var booking = Booking.Create(
                Guid.NewGuid(), invite, pastEvent.Id, "manage-token", now.AddDays(-1));
            invite.MarkUsed();
            var appointment = BookingAppointment.Create(
                Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting);
            appointment.TransitionTo(
                BookingAppointmentStatus.NoShow, appointmentStaff, now, false, true);
            appointmentId = appointment.Id;
            seed.Attendees.Add(attendee);
            seed.Events.Add(pastEvent);
            foreach (var day in new[] { 30, 31, 32 })
            {
                seed.Events.Add(EventFixture.Create(
                    Guid.NewGuid(),
                    new EventWindow(DateOnly.FromDateTime(now.DateTime).AddDays(day), new TimeOnly(9, 0)),
                    AppointmentTypeIds.All.ToDictionary(value => value, _ => 10)));
            }

            seed.Bookings.Add(booking);
            seed.BookingAppointments.Add(appointment);
            seed.AttendeeGroups.Add(group);
            await seed.SaveChangesAsync();
        }

        return (attendeeId, appointmentId);
    }

    private sealed class SilentSender : IEmailSender
    {
        public Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class FixedClock(DateTimeOffset now)
        : EventBooking.Application.Abstractions.IClock
    {
        /// <inheritdoc/>
        public DateTimeOffset UtcNow => now;
        /// <inheritdoc/>
        public DateTimeOffset NowAtTransitionalLocation => now;
        /// <inheritdoc/>
        public DateOnly TodayAtTransitionalLocation => DateOnly.FromDateTime(now.DateTime);
        /// <inheritdoc/>
        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) =>
            DateOnly.FromDateTime(instant.DateTime);

        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => instant;
    }
}
`````
