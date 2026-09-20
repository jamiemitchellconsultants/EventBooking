# 01a — Variable-length windows in the location's zone, edits 25 (Task 4)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — tests/EventBooking.Infrastructure.Tests/DashboardQueryTests.cs — 1/1

<!-- retirement-file: {"id":75,"file":"tests/EventBooking.Infrastructure.Tests/DashboardQueryTests.cs","beforeSha":"6ccf62b67c557f0f1d28f4c4e763a7cd062582642dae08c6971b60607bd92f15","afterSha":"4ceea52792343cae9f6bc0796bf9f53c751470c5c120a7706c4dd71ddd789143","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class DashboardQueryTests(PostgresFixture fixture)
{
    private sealed class MovableLondonClock(DateTimeOffset now) : IClock
    {
        private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

        public DateTimeOffset UtcNow { get; set; } = now;

        /// <summary>Gets the current instant converted to the London transitional-location time zone.</summary>
        public DateTimeOffset NowAtTransitionalLocation => TimeZoneInfo.ConvertTime(UtcNow, London);

        public DateOnly TodayAtTransitionalLocation => DateAtTransitionalLocation(UtcNow);

        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, London).DateTime);

        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) =>
            TimeZoneInfo.ConvertTime(instant, London);
    }

    [Fact]
    public async Task AwaitingAttendeesUseTransitionalLocationDatesAcrossTheUtcMidnightBoundary()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 2, 23, 30, 0, TimeSpan.Zero));

        await using (var write = NewContext(clock))
        {
            var awaitingGroup = AttendeeGroup.Define(
                Guid.NewGuid(), "DASH_DAT_MED", "Dashboard DAT MED", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp]);
            write.AttendeeGroups.Add(awaitingGroup);
            var attendee = Attendee.Create(
                Guid.NewGuid(), "C. Diallo", "c.diallo@mail.com", awaitingGroup);
            attendee.MarkAwaitingAvailability();
            write.Attendees.Add(attendee);
            await write.SaveChangesAsync();
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 3, 23, 30, 0, TimeSpan.Zero);

        await using var read = NewContext(clock);
        var row = Assert.Single(await new DashboardQueries(read, clock)
            .AwaitingAvailabilityAsync(CancellationToken.None));

        Assert.Equal("C. Diallo", row.Name);
        Assert.Equal(new[] { "DAT", "MED" }, row.RequiredCodes);
        Assert.Equal(new DateOnly(2026, 9, 3), row.WaitingSince);
        Assert.Equal(1, row.DaysWaiting);
    }

    [Fact]
    public async Task TheStatusStampIsWrittenOnAddAndMovesOnlyWhenStatusMoves()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero));

        Guid attendeeId;
        await using (var write = NewContext(clock))
        {
            var uniformOnly = AttendeeGroup.Define(
                Guid.NewGuid(), "DASH_UNI", "Dashboard UNI", true,
                [AppointmentTypeIds.UniformFitting]);
            write.AttendeeGroups.Add(uniformOnly);
            var attendee = Attendee.Create(
                Guid.NewGuid(), "B. Chen", "b.chen@mail.com", uniformOnly);
            write.Attendees.Add(attendee);
            await write.SaveChangesAsync();
            attendeeId = attendee.Id;
        }

        await using (var addedRead = NewContext(clock))
        {
            var added = await addedRead.Attendees.SingleAsync(c => c.Id == attendeeId);
            Assert.Equal(clock.UtcNow, addedRead.Entry(added)
                .Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty).CurrentValue);
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
        await using (var rename = NewContext(clock))
        {
            var attendee = await rename.Attendees.SingleAsync(c => c.Id == attendeeId);
            attendee.UpdateDetails("B. Chen-Smith", "b.chen@mail.com");
            await rename.SaveChangesAsync();
        }

        await using (var renamedRead = NewContext(clock))
        {
            var renamed = await renamedRead.Attendees.SingleAsync(c => c.Id == attendeeId);
            Assert.Equal(
                new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero),
                renamedRead.Entry(renamed)
                    .Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty).CurrentValue);
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);
        await using (var statusChange = NewContext(clock))
        {
            var attendee = await statusChange.Attendees.SingleAsync(c => c.Id == attendeeId);
            attendee.MarkAwaitingAvailability();
            await statusChange.SaveChangesAsync();
        }

        await using var changedRead = NewContext(clock);
        var changed = await changedRead.Attendees.SingleAsync(c => c.Id == attendeeId);
        Assert.Equal(clock.UtcNow, changedRead.Entry(changed)
            .Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty).CurrentValue);
    }

    [Fact]
    public async Task TheSynchronousSavePathStampsAddsAndStatusChangesButNotUnrelatedEdits()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero));

        Guid attendeeId;
        await using (var write = NewContext(clock))
        {
            var uniformOnly = AttendeeGroup.Define(
                Guid.NewGuid(), "DASH_UNI_TWO", "Dashboard UNI two", true,
                [AppointmentTypeIds.UniformFitting]);
            write.AttendeeGroups.Add(uniformOnly);
            var attendee = Attendee.Create(
                Guid.NewGuid(), "S. Patel", "s.patel@mail.com", uniformOnly);
            write.Attendees.Add(attendee);
            write.SaveChanges();
            attendeeId = attendee.Id;
        }

        await using (var addedRead = NewContext(clock))
        {
            var attendee = await addedRead.Attendees.SingleAsync(c => c.Id == attendeeId);
            Assert.Equal(clock.UtcNow, addedRead.Entry(attendee)
                .Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty).CurrentValue);
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
        await using (var rename = NewContext(clock))
        {
            var attendee = await rename.Attendees.SingleAsync(c => c.Id == attendeeId);
            attendee.UpdateDetails("S. Patel-Jones", "s.patel@mail.com");
            rename.SaveChanges();
        }

        await using (var renamedRead = NewContext(clock))
        {
            var attendee = await renamedRead.Attendees.SingleAsync(c => c.Id == attendeeId);
            Assert.Equal(
                new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero),
                renamedRead.Entry(attendee)
                    .Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty).CurrentValue);
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);
        await using (var statusChange = NewContext(clock))
        {
            var attendee = await statusChange.Attendees.SingleAsync(c => c.Id == attendeeId);
            attendee.MarkAwaitingAvailability();
            statusChange.SaveChanges();
        }

        await using var changedRead = NewContext(clock);
        var changed = await changedRead.Attendees.SingleAsync(c => c.Id == attendeeId);
        Assert.Equal(clock.UtcNow, changedRead.Entry(changed)
            .Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty).CurrentValue);
    }

    [Fact]
    public async Task TheFollowUpListUsesTheTransitionalLocationDayTheAutoRetryGaveUp()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 2, 23, 30, 0, TimeSpan.Zero));

        await using (var write = NewContext(clock))
        {
            var groundOps = write.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.GroundOperationsAgent);
            var attendee = Attendee.Create(
                Guid.NewGuid(), "D. Reyes", "d.reyes@mail.com", groundOps);
            attendee.MarkInvited();
            attendee.MarkNoResponse();
            write.Attendees.Add(attendee);
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        var row = Assert.Single(await new DashboardQueries(read, clock)
            .NoResponseAsync(CancellationToken.None));

        Assert.Equal("D. Reyes", row.Name);
        Assert.Equal(new DateOnly(2026, 9, 3), row.GaveUpOn);
    }

    [Fact]
    public async Task TheEventsOverviewShowsCapacityAndActiveBookingCount()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

        await using (var write = NewContext(clock))
        {
            var events = new[]
            {
                EventFor(new DateOnly(2026, 9, 21), new TimeOnly(9, 0)),
                EventFor(new DateOnly(2026, 9, 22), new TimeOnly(9, 0)),
                EventFor(new DateOnly(2026, 9, 23), new TimeOnly(9, 0)),
            };
            events[0].CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();

            var pilots = write.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
            var attendee = Attendee.Create(
                Guid.NewGuid(), "E. Martin", "e.martin@mail.com", pilots);
            attendee.MarkInvited();
            var invite = Invite.CreateInitial(
                Guid.NewGuid(), attendee.Id, "invite-hash", clock.UtcNow.AddDays(4),
                events.Select(s => s.Id), attendee.RequiredAppointmentTypeIds, 0);
            var booking = Booking.Create(
                Guid.NewGuid(), invite, events[0].Id, "booking-hash", clock.UtcNow);

            write.Events.AddRange(events);
            write.Attendees.Add(attendee);
            write.Invites.Add(invite);
            write.Bookings.Add(booking);
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        var rows = await new DashboardQueries(read, clock).EventsOverviewAsync(CancellationToken.None);
        var row = rows.Single(s => s.Date == new DateOnly(2026, 9, 21));

        Assert.Equal(new TimeOnly(13, 0), row.EndTime);
        Assert.Equal(1, row.ActiveBookings);
        Assert.Equal(new[] { "DAT", "MED", "UNI" }, row.Capacities.Select(c => c.Code));
        var drugAndAlcohol = row.Capacities.Single(c => c.Code == "DAT");
        Assert.Equal(10, drugAndAlcohol.TotalHeadcount);
        Assert.Equal(9, drugAndAlcohol.RemainingCapacity);
    }

    [Fact]
    public async Task ACancelledEventIsNotOnTheOverview()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

        await using (var write = NewContext(clock))
        {
            var eventItem = EventFor(new DateOnly(2026, 9, 21), new TimeOnly(9, 0));
            eventItem.Cancel();
            write.Events.Add(eventItem);
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        Assert.Empty(await new DashboardQueries(read, clock).EventsOverviewAsync(CancellationToken.None));
    }

    [Fact]
    public async Task OnlyTheLatestEmailLogRowPerAttendeeIsReturned()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
        var attendeeId = Guid.NewGuid();

        await using (var write = NewContext(clock))
        {
            var pilots = write.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
            var attendee = Attendee.Create(
                attendeeId, "A. Novak", "a.novak@mail.com", pilots);
            write.Attendees.Add(attendee);
            write.EmailLogs.Add(EmailLog.Record(
                Guid.NewGuid(), attendeeId, EmailTemplate.AttendeeInvite,
                new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero), EmailStatus.Resolved));
            write.EmailLogs.Add(EmailLog.Record(
                Guid.NewGuid(), attendeeId, EmailTemplate.AttendeeReinvite,
                new DateTimeOffset(2026, 9, 2, 9, 0, 0, TimeSpan.Zero), EmailStatus.Sent));
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        var row = Assert.Single(
            await new DashboardQueries(read, clock).LatestEmailStatusAsync(CancellationToken.None));

        Assert.Equal(attendeeId, row.AttendeeId);
        Assert.Equal(EmailTemplate.AttendeeReinvite, row.TemplateName);
        Assert.Equal(EmailStatus.Sent, row.Status);
    }

    [Fact]
    public async Task AAttendeeWithNoEmailLogRowsIsAbsent()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

        await using (var write = NewContext(clock))
        {
            var uniformOnly = AttendeeGroup.Define(
                Guid.NewGuid(), "DASH_UNI_THREE", "Dashboard UNI three", true,
                [AppointmentTypeIds.UniformFitting]);
            write.AttendeeGroups.Add(uniformOnly);
            write.Attendees.Add(Attendee.Create(
                Guid.NewGuid(), "B. Chen", "b.chen@mail.com", uniformOnly));
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        Assert.Empty(
            await new DashboardQueries(read, clock).LatestEmailStatusAsync(CancellationToken.None));
    }

    /// <summary>Retry visibility follows the latest delivery's current attendee and event context.</summary>
    [Fact]
    public async Task LatestEmailStatusMarksOnlyActionableDeliveryContextAsRetryable()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
        var actionableId = Guid.NewGuid();
        var staleId = Guid.NewGuid();
        var cancelledEvent = EventFor(new DateOnly(2026, 9, 21), new TimeOnly(9, 0));
        cancelledEvent.Cancel();
        // Production stages the cancellation notice with its booking identifier, so the
        // retryable delivery carries one; the stale delivery below omits it on purpose.
        var cancelledBookingId = Guid.NewGuid();
        var outstanding = EmailLog.RecordPending(
            Guid.NewGuid(),
            actionableId,
            EmailTemplate.EventCancelledRebookingNeeded,
            clock.UtcNow,
            bookingId: cancelledBookingId,
            eventId: cancelledEvent.Id);
        var laterSent = EmailLog.Record(
            Guid.NewGuid(),
            actionableId,
            EmailTemplate.AttendeeInvite,
            clock.UtcNow.AddMinutes(3),
            EmailStatus.Sent);

        await using (var write = NewContext(clock))
        {
            var pilots = write.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
            var actionableAttendee = Attendee.Create(
                actionableId, "Actionable", "actionable@mail.com", pilots);
            actionableAttendee.MarkAwaitingAvailability();
            var staleAttendee = Attendee.Create(
                staleId, "Stale", "stale@mail.com", pilots);
            var cancelledInvite = Invite.CreateInitial(
                Guid.NewGuid(), actionableId, "cancelled-invite-hash", clock.UtcNow.AddDays(4),
                [cancelledEvent.Id, Guid.NewGuid(), Guid.NewGuid()],
                actionableAttendee.RequiredAppointmentTypeIds, 0);
            var cancelledBooking = Booking.Create(
                cancelledBookingId, cancelledInvite, cancelledEvent.Id, "cancelled-booking-hash", clock.UtcNow);
            cancelledBooking.Cancel();
            write.Attendees.AddRange(actionableAttendee, staleAttendee);
            write.Events.Add(cancelledEvent);
            write.Invites.Add(cancelledInvite);
            write.Bookings.Add(cancelledBooking);
            write.EmailLogs.AddRange(
                outstanding,
                laterSent,
                EmailLog.RecordPending(
                    Guid.NewGuid(),
                    staleAttendee.Id,
                    EmailTemplate.EventCancelledRebookingNeeded,
                    clock.UtcNow,
                    eventId: cancelledEvent.Id));
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        var rows = await new DashboardQueries(read, clock)
            .LatestEmailStatusAsync(CancellationToken.None);

        var actionable = rows.Single(row => row.AttendeeId == actionableId);
        Assert.True(actionable.CanRetry);
        Assert.Equal(EmailTemplate.EventCancelledRebookingNeeded, actionable.TemplateName);
        Assert.Equal(EmailStatus.Pending, actionable.Status);
        Assert.False(rows.Single(row => row.AttendeeId == staleId).CanRetry);

        await using (var resolve = NewContext(clock))
        {
            var persisted = await resolve.EmailLogs.SingleAsync(delivery => delivery.Id == outstanding.Id);
            persisted.MarkResolved(clock.UtcNow.AddMinutes(2));
            await resolve.SaveChangesAsync();
        }

        await using var reread = NewContext(clock);
        var terminal = (await new DashboardQueries(reread, clock)
            .LatestEmailStatusAsync(CancellationToken.None))
            .Single(row => row.AttendeeId == actionableId);
        Assert.Equal(EmailTemplate.AttendeeInvite, terminal.TemplateName);
        Assert.Equal(EmailStatus.Sent, terminal.Status);
        Assert.False(terminal.CanRetry);
    }

    private EventBookingDbContext NewContext(IClock clock)
    {
        var options = new DbContextOptionsBuilder<EventBookingDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .AddInterceptors(new StatusStampingInterceptor(clock))
            .Options;

        return new EventBookingDbContext(options);
    }

    private static Event EventFor(DateOnly date, TimeOnly startTime)
    {
        var proposal = EventProposal.Create(Guid.NewGuid(), new EventWindow(date, startTime), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        return Event.CreateFrom(Guid.NewGuid(), proposal);
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/DashboardQueryTests.cs — 1/1

<!-- retirement-file: {"id":75,"file":"tests/EventBooking.Infrastructure.Tests/DashboardQueryTests.cs","beforeSha":"6ccf62b67c557f0f1d28f4c4e763a7cd062582642dae08c6971b60607bd92f15","afterSha":"4ceea52792343cae9f6bc0796bf9f53c751470c5c120a7706c4dd71ddd789143","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class DashboardQueryTests(PostgresFixture fixture)
{
    private sealed class MovableLondonClock(DateTimeOffset now) : IClock
    {
        private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

        public DateTimeOffset UtcNow { get; set; } = now;

        /// <summary>Gets the current instant converted to the London transitional-location time zone.</summary>
        public DateTimeOffset NowAtTransitionalLocation => TimeZoneInfo.ConvertTime(UtcNow, London);

        public DateOnly TodayAtTransitionalLocation => DateAtTransitionalLocation(UtcNow);

        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, London).DateTime);

        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) =>
            TimeZoneInfo.ConvertTime(instant, London);
    }

    [Fact]
    public async Task AwaitingAttendeesUseTransitionalLocationDatesAcrossTheUtcMidnightBoundary()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 2, 23, 30, 0, TimeSpan.Zero));

        await using (var write = NewContext(clock))
        {
            var awaitingGroup = AttendeeGroup.Define(
                Guid.NewGuid(), "DASH_DAT_MED", "Dashboard DAT MED", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp]);
            write.AttendeeGroups.Add(awaitingGroup);
            var attendee = Attendee.Create(
                Guid.NewGuid(), "C. Diallo", "c.diallo@mail.com", awaitingGroup);
            attendee.MarkAwaitingAvailability();
            write.Attendees.Add(attendee);
            await write.SaveChangesAsync();
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 3, 23, 30, 0, TimeSpan.Zero);

        await using var read = NewContext(clock);
        var row = Assert.Single(await new DashboardQueries(read, clock)
            .AwaitingAvailabilityAsync(CancellationToken.None));

        Assert.Equal("C. Diallo", row.Name);
        Assert.Equal(new[] { "DAT", "MED" }, row.RequiredCodes);
        Assert.Equal(new DateOnly(2026, 9, 3), row.WaitingSince);
        Assert.Equal(1, row.DaysWaiting);
    }

    [Fact]
    public async Task TheStatusStampIsWrittenOnAddAndMovesOnlyWhenStatusMoves()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero));

        Guid attendeeId;
        await using (var write = NewContext(clock))
        {
            var uniformOnly = AttendeeGroup.Define(
                Guid.NewGuid(), "DASH_UNI", "Dashboard UNI", true,
                [AppointmentTypeIds.UniformFitting]);
            write.AttendeeGroups.Add(uniformOnly);
            var attendee = Attendee.Create(
                Guid.NewGuid(), "B. Chen", "b.chen@mail.com", uniformOnly);
            write.Attendees.Add(attendee);
            await write.SaveChangesAsync();
            attendeeId = attendee.Id;
        }

        await using (var addedRead = NewContext(clock))
        {
            var added = await addedRead.Attendees.SingleAsync(c => c.Id == attendeeId);
            Assert.Equal(clock.UtcNow, addedRead.Entry(added)
                .Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty).CurrentValue);
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
        await using (var rename = NewContext(clock))
        {
            var attendee = await rename.Attendees.SingleAsync(c => c.Id == attendeeId);
            attendee.UpdateDetails("B. Chen-Smith", "b.chen@mail.com");
            await rename.SaveChangesAsync();
        }

        await using (var renamedRead = NewContext(clock))
        {
            var renamed = await renamedRead.Attendees.SingleAsync(c => c.Id == attendeeId);
            Assert.Equal(
                new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero),
                renamedRead.Entry(renamed)
                    .Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty).CurrentValue);
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);
        await using (var statusChange = NewContext(clock))
        {
            var attendee = await statusChange.Attendees.SingleAsync(c => c.Id == attendeeId);
            attendee.MarkAwaitingAvailability();
            await statusChange.SaveChangesAsync();
        }

        await using var changedRead = NewContext(clock);
        var changed = await changedRead.Attendees.SingleAsync(c => c.Id == attendeeId);
        Assert.Equal(clock.UtcNow, changedRead.Entry(changed)
            .Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty).CurrentValue);
    }

    [Fact]
    public async Task TheSynchronousSavePathStampsAddsAndStatusChangesButNotUnrelatedEdits()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero));

        Guid attendeeId;
        await using (var write = NewContext(clock))
        {
            var uniformOnly = AttendeeGroup.Define(
                Guid.NewGuid(), "DASH_UNI_TWO", "Dashboard UNI two", true,
                [AppointmentTypeIds.UniformFitting]);
            write.AttendeeGroups.Add(uniformOnly);
            var attendee = Attendee.Create(
                Guid.NewGuid(), "S. Patel", "s.patel@mail.com", uniformOnly);
            write.Attendees.Add(attendee);
            write.SaveChanges();
            attendeeId = attendee.Id;
        }

        await using (var addedRead = NewContext(clock))
        {
            var attendee = await addedRead.Attendees.SingleAsync(c => c.Id == attendeeId);
            Assert.Equal(clock.UtcNow, addedRead.Entry(attendee)
                .Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty).CurrentValue);
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
        await using (var rename = NewContext(clock))
        {
            var attendee = await rename.Attendees.SingleAsync(c => c.Id == attendeeId);
            attendee.UpdateDetails("S. Patel-Jones", "s.patel@mail.com");
            rename.SaveChanges();
        }

        await using (var renamedRead = NewContext(clock))
        {
            var attendee = await renamedRead.Attendees.SingleAsync(c => c.Id == attendeeId);
            Assert.Equal(
                new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero),
                renamedRead.Entry(attendee)
                    .Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty).CurrentValue);
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);
        await using (var statusChange = NewContext(clock))
        {
            var attendee = await statusChange.Attendees.SingleAsync(c => c.Id == attendeeId);
            attendee.MarkAwaitingAvailability();
            statusChange.SaveChanges();
        }

        await using var changedRead = NewContext(clock);
        var changed = await changedRead.Attendees.SingleAsync(c => c.Id == attendeeId);
        Assert.Equal(clock.UtcNow, changedRead.Entry(changed)
            .Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty).CurrentValue);
    }

    [Fact]
    public async Task TheFollowUpListUsesTheTransitionalLocationDayTheAutoRetryGaveUp()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 2, 23, 30, 0, TimeSpan.Zero));

        await using (var write = NewContext(clock))
        {
            var groundOps = write.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.GroundOperationsAgent);
            var attendee = Attendee.Create(
                Guid.NewGuid(), "D. Reyes", "d.reyes@mail.com", groundOps);
            attendee.MarkInvited();
            attendee.MarkNoResponse();
            write.Attendees.Add(attendee);
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        var row = Assert.Single(await new DashboardQueries(read, clock)
            .NoResponseAsync(CancellationToken.None));

        Assert.Equal("D. Reyes", row.Name);
        Assert.Equal(new DateOnly(2026, 9, 3), row.GaveUpOn);
    }

    [Fact]
    public async Task TheEventsOverviewShowsCapacityAndActiveBookingCount()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

        await using (var write = NewContext(clock))
        {
            var events = new[]
            {
                EventFor(new DateOnly(2026, 9, 21), new TimeOnly(9, 0)),
                EventFor(new DateOnly(2026, 9, 22), new TimeOnly(9, 0)),
                EventFor(new DateOnly(2026, 9, 23), new TimeOnly(9, 0)),
            };
            events[0].CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();

            var pilots = write.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
            var attendee = Attendee.Create(
                Guid.NewGuid(), "E. Martin", "e.martin@mail.com", pilots);
            attendee.MarkInvited();
            var invite = Invite.CreateInitial(
                Guid.NewGuid(), attendee.Id, "invite-hash", clock.UtcNow.AddDays(4),
                events.Select(s => s.Id), attendee.RequiredAppointmentTypeIds, 0);
            var booking = Booking.Create(
                Guid.NewGuid(), invite, events[0].Id, "booking-hash", clock.UtcNow);

            write.Events.AddRange(events);
            write.Attendees.Add(attendee);
            write.Invites.Add(invite);
            write.Bookings.Add(booking);
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        var rows = await new DashboardQueries(read, clock).EventsOverviewAsync(CancellationToken.None);
        var row = rows.Single(s => s.Date == new DateOnly(2026, 9, 21));

        Assert.Equal(new TimeOnly(13, 0), row.EndTime);
        Assert.Equal(1, row.ActiveBookings);
        Assert.Equal(new[] { "DAT", "MED", "UNI" }, row.Capacities.Select(c => c.Code));
        var drugAndAlcohol = row.Capacities.Single(c => c.Code == "DAT");
        Assert.Equal(10, drugAndAlcohol.TotalHeadcount);
        Assert.Equal(9, drugAndAlcohol.RemainingCapacity);
    }

    [Fact]
    public async Task ACancelledEventIsNotOnTheOverview()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

        await using (var write = NewContext(clock))
        {
            var eventItem = EventFor(new DateOnly(2026, 9, 21), new TimeOnly(9, 0));
            eventItem.Cancel();
            write.Events.Add(eventItem);
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        Assert.Empty(await new DashboardQueries(read, clock).EventsOverviewAsync(CancellationToken.None));
    }

    [Fact]
    public async Task OnlyTheLatestEmailLogRowPerAttendeeIsReturned()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
        var attendeeId = Guid.NewGuid();

        await using (var write = NewContext(clock))
        {
            var pilots = write.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
            var attendee = Attendee.Create(
                attendeeId, "A. Novak", "a.novak@mail.com", pilots);
            write.Attendees.Add(attendee);
            write.EmailLogs.Add(EmailLog.Record(
                Guid.NewGuid(), attendeeId, EmailTemplate.AttendeeInvite,
                new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero), EmailStatus.Resolved));
            write.EmailLogs.Add(EmailLog.Record(
                Guid.NewGuid(), attendeeId, EmailTemplate.AttendeeReinvite,
                new DateTimeOffset(2026, 9, 2, 9, 0, 0, TimeSpan.Zero), EmailStatus.Sent));
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        var row = Assert.Single(
            await new DashboardQueries(read, clock).LatestEmailStatusAsync(CancellationToken.None));

        Assert.Equal(attendeeId, row.AttendeeId);
        Assert.Equal(EmailTemplate.AttendeeReinvite, row.TemplateName);
        Assert.Equal(EmailStatus.Sent, row.Status);
    }

    [Fact]
    public async Task AAttendeeWithNoEmailLogRowsIsAbsent()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

        await using (var write = NewContext(clock))
        {
            var uniformOnly = AttendeeGroup.Define(
                Guid.NewGuid(), "DASH_UNI_THREE", "Dashboard UNI three", true,
                [AppointmentTypeIds.UniformFitting]);
            write.AttendeeGroups.Add(uniformOnly);
            write.Attendees.Add(Attendee.Create(
                Guid.NewGuid(), "B. Chen", "b.chen@mail.com", uniformOnly));
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        Assert.Empty(
            await new DashboardQueries(read, clock).LatestEmailStatusAsync(CancellationToken.None));
    }

    /// <summary>Retry visibility follows the latest delivery's current attendee and event context.</summary>
    [Fact]
    public async Task LatestEmailStatusMarksOnlyActionableDeliveryContextAsRetryable()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
        var actionableId = Guid.NewGuid();
        var staleId = Guid.NewGuid();
        var cancelledEvent = EventFor(new DateOnly(2026, 9, 21), new TimeOnly(9, 0));
        cancelledEvent.Cancel();
        // Production stages the cancellation notice with its booking identifier, so the
        // retryable delivery carries one; the stale delivery below omits it on purpose.
        var cancelledBookingId = Guid.NewGuid();
        var outstanding = EmailLog.RecordPending(
            Guid.NewGuid(),
            actionableId,
            EmailTemplate.EventCancelledRebookingNeeded,
            clock.UtcNow,
            bookingId: cancelledBookingId,
            eventId: cancelledEvent.Id);
        var laterSent = EmailLog.Record(
            Guid.NewGuid(),
            actionableId,
            EmailTemplate.AttendeeInvite,
            clock.UtcNow.AddMinutes(3),
            EmailStatus.Sent);

        await using (var write = NewContext(clock))
        {
            var pilots = write.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
            var actionableAttendee = Attendee.Create(
                actionableId, "Actionable", "actionable@mail.com", pilots);
            actionableAttendee.MarkAwaitingAvailability();
            var staleAttendee = Attendee.Create(
                staleId, "Stale", "stale@mail.com", pilots);
            var cancelledInvite = Invite.CreateInitial(
                Guid.NewGuid(), actionableId, "cancelled-invite-hash", clock.UtcNow.AddDays(4),
                [cancelledEvent.Id, Guid.NewGuid(), Guid.NewGuid()],
                actionableAttendee.RequiredAppointmentTypeIds, 0);
            var cancelledBooking = Booking.Create(
                cancelledBookingId, cancelledInvite, cancelledEvent.Id, "cancelled-booking-hash", clock.UtcNow);
            cancelledBooking.Cancel();
            write.Attendees.AddRange(actionableAttendee, staleAttendee);
            write.Events.Add(cancelledEvent);
            write.Invites.Add(cancelledInvite);
            write.Bookings.Add(cancelledBooking);
            write.EmailLogs.AddRange(
                outstanding,
                laterSent,
                EmailLog.RecordPending(
                    Guid.NewGuid(),
                    staleAttendee.Id,
                    EmailTemplate.EventCancelledRebookingNeeded,
                    clock.UtcNow,
                    eventId: cancelledEvent.Id));
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        var rows = await new DashboardQueries(read, clock)
            .LatestEmailStatusAsync(CancellationToken.None);

        var actionable = rows.Single(row => row.AttendeeId == actionableId);
        Assert.True(actionable.CanRetry);
        Assert.Equal(EmailTemplate.EventCancelledRebookingNeeded, actionable.TemplateName);
        Assert.Equal(EmailStatus.Pending, actionable.Status);
        Assert.False(rows.Single(row => row.AttendeeId == staleId).CanRetry);

        await using (var resolve = NewContext(clock))
        {
            var persisted = await resolve.EmailLogs.SingleAsync(delivery => delivery.Id == outstanding.Id);
            persisted.MarkResolved(clock.UtcNow.AddMinutes(2));
            await resolve.SaveChangesAsync();
        }

        await using var reread = NewContext(clock);
        var terminal = (await new DashboardQueries(reread, clock)
            .LatestEmailStatusAsync(CancellationToken.None))
            .Single(row => row.AttendeeId == actionableId);
        Assert.Equal(EmailTemplate.AttendeeInvite, terminal.TemplateName);
        Assert.Equal(EmailStatus.Sent, terminal.Status);
        Assert.False(terminal.CanRetry);
    }

    private EventBookingDbContext NewContext(IClock clock)
    {
        var options = new DbContextOptionsBuilder<EventBookingDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .AddInterceptors(new StatusStampingInterceptor(clock))
            .Options;

        return new EventBookingDbContext(options);
    }

    private static Event EventFor(DateOnly date, TimeOnly startTime)
    {
        var proposal = EventProposal.Create(Guid.NewGuid(), new EventWindow(date, startTime, 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        return Event.CreateFrom(Guid.NewGuid(), proposal);
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/EventCapacityRepositoryTests.cs — 1/1

<!-- retirement-file: {"id":76,"file":"tests/EventBooking.Infrastructure.Tests/EventCapacityRepositoryTests.cs","beforeSha":"fc9971aebdb3e6449910e1a349b3d23095e10cbf69ed1a90ed7943b9c829dc98","afterSha":"49e2e72f0f7b7b6418306b78829188348097d7252ff0778f7f9c16967184a63a","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class EventCapacityRepositoryTests(PostgresFixture fixture)
{
    [Fact]
    public async Task OnlyTheRequestedTypesAreReturnedAndTheyComeBackInOrder()
    {
        var eventId = await GivenAEvent();

        await using var context = fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var locked = await new EventCapacityRepository(context).LockForUpdateAsync(
            eventId,
            [AppointmentTypeIds.UniformFitting, AppointmentTypeIds.DrugAndAlcoholTesting],
            CancellationToken.None);

        Assert.Equal(2, locked.Count);
        Assert.Equal(
            locked.Select(c => c.AppointmentTypeId).OrderBy(id => id),
            locked.Select(c => c.AppointmentTypeId));
        Assert.DoesNotContain(AppointmentTypeIds.MedicalCheckUp, locked.Select(c => c.AppointmentTypeId));
    }

    [Fact]
    public async Task TheReturnedRowsAreTrackedSoDecrementsPersist()
    {
        var eventId = await GivenAEvent();

        await using (var context = fixture.NewContext())
        {
            await using var transaction = await context.Database.BeginTransactionAsync();

            var locked = await new EventCapacityRepository(context).LockForUpdateAsync(
                eventId, [AppointmentTypeIds.DrugAndAlcoholTesting], CancellationToken.None);

            locked.Single().Decrement();
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        await using var read = fixture.NewContext();
        var eventItem = await read.Events.Include(s => s.Capacities).SingleAsync(s => s.Id == eventId);
        Assert.Equal(9, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
    }

    [Fact]
    public async Task ASecondTransactionWaitsOnTheCapacityRowLockBeforeItCanComplete()
    {
        var eventId = await GivenAEvent();

        await using var first = fixture.NewContext();
        await using var firstTransaction = await first.Database.BeginTransactionAsync();
        await new EventCapacityRepository(first).LockForUpdateAsync(
            eventId, [AppointmentTypeIds.DrugAndAlcoholTesting], CancellationToken.None);

        var waitingBackend = NewBarrier();
        var secondLock = LockCapacityAsync(eventId, waitingBackend);
        var secondPid = await waitingBackend.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await WaitUntilBlockedOnDatabaseLockAsync(first, secondPid);
        await firstTransaction.CommitAsync();

        var locked = await secondLock.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(eventId, locked.EventId);
        Assert.Equal(AppointmentTypeIds.DrugAndAlcoholTesting, locked.AppointmentTypeId);
    }

    [Fact]
    public async Task AnUnknownEventReturnsNothingRatherThanThrowing()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var locked = await new EventCapacityRepository(context).LockForUpdateAsync(
            Guid.NewGuid(), AppointmentTypeIds.All, CancellationToken.None);

        Assert.Empty(locked);
    }

    private async Task<Guid> GivenAEvent()
    {
        await fixture.ResetAsync();

        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        await using var context = fixture.NewContext();
        context.EventProposals.Add(proposal);
        context.Events.Add(eventItem);
        await context.SaveChangesAsync();

        return eventItem.Id;
    }

    private async Task<EventCapacity> LockCapacityAsync(
        Guid eventId,
        TaskCompletionSource<int> waitingBackend)
    {
        await using var context = fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        waitingBackend.SetResult(await GetBackendPidAsync(context));

        var locked = await new EventCapacityRepository(context).LockForUpdateAsync(
            eventId, [AppointmentTypeIds.DrugAndAlcoholTesting], CancellationToken.None);
        await transaction.CommitAsync();

        return Assert.Single(locked);
    }

    private static TaskCompletionSource<int> NewBarrier() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task<int> GetBackendPidAsync(EventBookingDbContext context)
    {
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = (NpgsqlTransaction)context.Database.CurrentTransaction!
            .GetDbTransaction();
        command.CommandText = "SELECT pg_backend_pid();";
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static async Task WaitUntilBlockedOnDatabaseLockAsync(
        EventBookingDbContext lockOwner,
        int waitingBackendPid)
    {
        var connection = (NpgsqlConnection)lockOwner.Database.GetDbConnection();

        for (var attempt = 0; attempt < 200; attempt++)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (NpgsqlTransaction)lockOwner.Database.CurrentTransaction!
                .GetDbTransaction();
            command.CommandText =
                "SELECT wait_event_type FROM pg_stat_activity WHERE pid = @waiting_backend_pid;";
            command.Parameters.AddWithValue("waiting_backend_pid", waitingBackendPid);

            if (string.Equals(
                    await command.ExecuteScalarAsync() as string,
                    "Lock",
                    StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.Fail($"PostgreSQL backend {waitingBackendPid} never waited on the capacity row lock.");
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/EventCapacityRepositoryTests.cs — 1/1

<!-- retirement-file: {"id":76,"file":"tests/EventBooking.Infrastructure.Tests/EventCapacityRepositoryTests.cs","beforeSha":"fc9971aebdb3e6449910e1a349b3d23095e10cbf69ed1a90ed7943b9c829dc98","afterSha":"49e2e72f0f7b7b6418306b78829188348097d7252ff0778f7f9c16967184a63a","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class EventCapacityRepositoryTests(PostgresFixture fixture)
{
    [Fact]
    public async Task OnlyTheRequestedTypesAreReturnedAndTheyComeBackInOrder()
    {
        var eventId = await GivenAEvent();

        await using var context = fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var locked = await new EventCapacityRepository(context).LockForUpdateAsync(
            eventId,
            [AppointmentTypeIds.UniformFitting, AppointmentTypeIds.DrugAndAlcoholTesting],
            CancellationToken.None);

        Assert.Equal(2, locked.Count);
        Assert.Equal(
            locked.Select(c => c.AppointmentTypeId).OrderBy(id => id),
            locked.Select(c => c.AppointmentTypeId));
        Assert.DoesNotContain(AppointmentTypeIds.MedicalCheckUp, locked.Select(c => c.AppointmentTypeId));
    }

    [Fact]
    public async Task TheReturnedRowsAreTrackedSoDecrementsPersist()
    {
        var eventId = await GivenAEvent();

        await using (var context = fixture.NewContext())
        {
            await using var transaction = await context.Database.BeginTransactionAsync();

            var locked = await new EventCapacityRepository(context).LockForUpdateAsync(
                eventId, [AppointmentTypeIds.DrugAndAlcoholTesting], CancellationToken.None);

            locked.Single().Decrement();
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        await using var read = fixture.NewContext();
        var eventItem = await read.Events.Include(s => s.Capacities).SingleAsync(s => s.Id == eventId);
        Assert.Equal(9, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
    }

    [Fact]
    public async Task ASecondTransactionWaitsOnTheCapacityRowLockBeforeItCanComplete()
    {
        var eventId = await GivenAEvent();

        await using var first = fixture.NewContext();
        await using var firstTransaction = await first.Database.BeginTransactionAsync();
        await new EventCapacityRepository(first).LockForUpdateAsync(
            eventId, [AppointmentTypeIds.DrugAndAlcoholTesting], CancellationToken.None);

        var waitingBackend = NewBarrier();
        var secondLock = LockCapacityAsync(eventId, waitingBackend);
        var secondPid = await waitingBackend.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await WaitUntilBlockedOnDatabaseLockAsync(first, secondPid);
        await firstTransaction.CommitAsync();

        var locked = await secondLock.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(eventId, locked.EventId);
        Assert.Equal(AppointmentTypeIds.DrugAndAlcoholTesting, locked.AppointmentTypeId);
    }

    [Fact]
    public async Task AnUnknownEventReturnsNothingRatherThanThrowing()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var locked = await new EventCapacityRepository(context).LockForUpdateAsync(
            Guid.NewGuid(), AppointmentTypeIds.All, CancellationToken.None);

        Assert.Empty(locked);
    }

    private async Task<Guid> GivenAEvent()
    {
        await fixture.ResetAsync();

        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        await using var context = fixture.NewContext();
        context.EventProposals.Add(proposal);
        context.Events.Add(eventItem);
        await context.SaveChangesAsync();

        return eventItem.Id;
    }

    private async Task<EventCapacity> LockCapacityAsync(
        Guid eventId,
        TaskCompletionSource<int> waitingBackend)
    {
        await using var context = fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        waitingBackend.SetResult(await GetBackendPidAsync(context));

        var locked = await new EventCapacityRepository(context).LockForUpdateAsync(
            eventId, [AppointmentTypeIds.DrugAndAlcoholTesting], CancellationToken.None);
        await transaction.CommitAsync();

        return Assert.Single(locked);
    }

    private static TaskCompletionSource<int> NewBarrier() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task<int> GetBackendPidAsync(EventBookingDbContext context)
    {
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = (NpgsqlTransaction)context.Database.CurrentTransaction!
            .GetDbTransaction();
        command.CommandText = "SELECT pg_backend_pid();";
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static async Task WaitUntilBlockedOnDatabaseLockAsync(
        EventBookingDbContext lockOwner,
        int waitingBackendPid)
    {
        var connection = (NpgsqlConnection)lockOwner.Database.GetDbConnection();

        for (var attempt = 0; attempt < 200; attempt++)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (NpgsqlTransaction)lockOwner.Database.CurrentTransaction!
                .GetDbTransaction();
            command.CommandText =
                "SELECT wait_event_type FROM pg_stat_activity WHERE pid = @waiting_backend_pid;";
            command.Parameters.AddWithValue("waiting_backend_pid", waitingBackendPid);

            if (string.Equals(
                    await command.ExecuteScalarAsync() as string,
                    "Lock",
                    StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.Fail($"PostgreSQL backend {waitingBackendPid} never waited on the capacity row lock.");
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/EventPersistenceTests.cs — 1/1

<!-- retirement-file: {"id":77,"file":"tests/EventBooking.Infrastructure.Tests/EventPersistenceTests.cs","beforeSha":"a01bc5752336ce2018c64518a109ef5c68fb06c989f5868a60e100bf5a94cbe8","afterSha":"18e7f5e85758c2feb014f2cefc3b159c7a55d44e7d655a0baffe404b717baa57","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class EventPersistenceTests(PostgresFixture fixture)
{
    private static Dictionary<Guid, int> FullHeadcounts() => new()
    {
        [AppointmentTypeIds.DrugAndAlcoholTesting] = 10,
        [AppointmentTypeIds.MedicalCheckUp] = 6,
        [AppointmentTypeIds.UniformFitting] = 8,
    };

    [Fact]
    public async Task AnEventPersistsWithItsProposalId()
    {
        await using var context = fixture.NewContext();
        var window = new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0));
        var eventItem = EventFixture.Create(Guid.NewGuid(), window, FullHeadcounts());

        context.Events.Add(eventItem);
        await context.SaveChangesAsync();

        await using var reload = fixture.NewContext();
        var reloaded = await reload.Events
            .Include(s => s.Capacities)
            .SingleAsync(s => s.Id == eventItem.Id);

        Assert.Equal(eventItem.ProposalId, reloaded.ProposalId);
        Assert.NotEqual(Guid.Empty, reloaded.ProposalId);
        Assert.Equal(3, reloaded.Capacities.Count);

        await fixture.ResetAsync();
    }

    [Fact]
    public async Task TwoEventsHaveDistinctProposalIds()
    {
        await using var context = fixture.NewContext();
        var first = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 12), new TimeOnly(9, 0)), FullHeadcounts());
        var second = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 13), new TimeOnly(9, 0)), FullHeadcounts());

        context.Events.AddRange(first, second);

        Assert.NotEqual(Guid.Empty, first.ProposalId);
        Assert.NotEqual(Guid.Empty, second.ProposalId);
        Assert.NotEqual(first.ProposalId, second.ProposalId);
        await context.SaveChangesAsync();

        await fixture.ResetAsync();
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/EventPersistenceTests.cs — 1/1

<!-- retirement-file: {"id":77,"file":"tests/EventBooking.Infrastructure.Tests/EventPersistenceTests.cs","beforeSha":"a01bc5752336ce2018c64518a109ef5c68fb06c989f5868a60e100bf5a94cbe8","afterSha":"18e7f5e85758c2feb014f2cefc3b159c7a55d44e7d655a0baffe404b717baa57","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class EventPersistenceTests(PostgresFixture fixture)
{
    private static Dictionary<Guid, int> FullHeadcounts() => new()
    {
        [AppointmentTypeIds.DrugAndAlcoholTesting] = 10,
        [AppointmentTypeIds.MedicalCheckUp] = 6,
        [AppointmentTypeIds.UniformFitting] = 8,
    };

    [Fact]
    public async Task AnEventPersistsWithItsProposalId()
    {
        await using var context = fixture.NewContext();
        var window = new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240);
        var eventItem = EventFixture.Create(Guid.NewGuid(), window, FullHeadcounts());

        context.Events.Add(eventItem);
        await context.SaveChangesAsync();

        await using var reload = fixture.NewContext();
        var reloaded = await reload.Events
            .Include(s => s.Capacities)
            .SingleAsync(s => s.Id == eventItem.Id);

        Assert.Equal(eventItem.ProposalId, reloaded.ProposalId);
        Assert.NotEqual(Guid.Empty, reloaded.ProposalId);
        Assert.Equal(3, reloaded.Capacities.Count);

        await fixture.ResetAsync();
    }

    [Fact]
    public async Task TwoEventsHaveDistinctProposalIds()
    {
        await using var context = fixture.NewContext();
        var first = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 12), new TimeOnly(9, 0), 240), FullHeadcounts());
        var second = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 13), new TimeOnly(9, 0), 240), FullHeadcounts());

        context.Events.AddRange(first, second);

        Assert.NotEqual(Guid.Empty, first.ProposalId);
        Assert.NotEqual(Guid.Empty, second.ProposalId);
        Assert.NotEqual(first.ProposalId, second.ProposalId);
        await context.SaveChangesAsync();

        await fixture.ResetAsync();
    }
}
`````
