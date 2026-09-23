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
