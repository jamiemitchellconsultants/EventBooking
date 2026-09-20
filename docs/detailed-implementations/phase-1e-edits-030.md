# 01e — Location-restricted invites and closed attendee transitions, edits 30 (Task 8)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.Infrastructure.Tests/DashboardQueryTests.cs — 1/1

<!-- retirement-file: {"id":82,"file":"tests/EventBooking.Infrastructure.Tests/DashboardQueryTests.cs","beforeSha":"d0fd6eabd3d1caea450d76fe41ba8a87e8d01985d7476d6b2fb6f9c78b3a7320","afterSha":"5b075df7aca19076308a373a4ad04aa74d94ed7eb5b36a4eb9307da4b14cc2bf","side":"after","part":1,"parts":1} -->

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
                Guid.NewGuid(),
                "C. Diallo",
                "c.diallo@mail.com",
                awaitingGroup,
                clock.UtcNow);
            attendee.MarkAwaitingAvailability(clock.UtcNow);
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
                Guid.NewGuid(),
                "B. Chen",
                "b.chen@mail.com",
                uniformOnly,
                clock.UtcNow);
            write.Attendees.Add(attendee);
            await write.SaveChangesAsync();
            attendeeId = attendee.Id;
        }

        await using (var addedRead = NewContext(clock))
        {
            var added = await addedRead.Attendees.SingleAsync(c => c.Id == attendeeId);
            Assert.Equal(clock.UtcNow, added.StatusChangedAt);
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
                renamed.StatusChangedAt);
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);
        await using (var statusChange = NewContext(clock))
        {
            var attendee = await statusChange.Attendees.SingleAsync(c => c.Id == attendeeId);
            attendee.MarkAwaitingAvailability(clock.UtcNow);
            await statusChange.SaveChangesAsync();
        }

        await using var changedRead = NewContext(clock);
        var changed = await changedRead.Attendees.SingleAsync(c => c.Id == attendeeId);
        Assert.Equal(clock.UtcNow, changed.StatusChangedAt);
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
                Guid.NewGuid(),
                "S. Patel",
                "s.patel@mail.com",
                uniformOnly,
                clock.UtcNow);
            write.Attendees.Add(attendee);
            write.SaveChanges();
            attendeeId = attendee.Id;
        }

        await using (var addedRead = NewContext(clock))
        {
            var attendee = await addedRead.Attendees.SingleAsync(c => c.Id == attendeeId);
            Assert.Equal(clock.UtcNow, attendee.StatusChangedAt);
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
                attendee.StatusChangedAt);
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);
        await using (var statusChange = NewContext(clock))
        {
            var attendee = await statusChange.Attendees.SingleAsync(c => c.Id == attendeeId);
            attendee.MarkAwaitingAvailability(clock.UtcNow);
            statusChange.SaveChanges();
        }

        await using var changedRead = NewContext(clock);
        var changed = await changedRead.Attendees.SingleAsync(c => c.Id == attendeeId);
        Assert.Equal(clock.UtcNow, changed.StatusChangedAt);
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
                Guid.NewGuid(),
                "D. Reyes",
                "d.reyes@mail.com",
                groundOps,
                clock.UtcNow);
            attendee.MarkInvited(clock.UtcNow);
            attendee.MarkNoResponse(clock.UtcNow);
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
                Guid.NewGuid(),
                "E. Martin",
                "e.martin@mail.com",
                pilots,
                clock.UtcNow);
            attendee.MarkInvited(clock.UtcNow);
            var invite = Invite.CreateInitial(
                Guid.NewGuid(),
                attendee.Id,
                "invite-hash",
                clock.UtcNow.AddDays(4),
                [ProposalFixture.LocationId],
                events.Select(s => s.Id),
                attendee.RequiredAppointmentTypeIds,
                0);
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
            eventItem.CancelBeforeStart();
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
                attendeeId,
                "A. Novak",
                "a.novak@mail.com",
                pilots,
                clock.UtcNow);
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
                Guid.NewGuid(),
                "B. Chen",
                "b.chen@mail.com",
                uniformOnly,
                clock.UtcNow));
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
        cancelledEvent.CancelBeforeStart();
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
                actionableId,
                "Actionable",
                "actionable@mail.com",
                pilots,
                clock.UtcNow);
            actionableAttendee.MarkAwaitingAvailability(clock.UtcNow);
            var staleAttendee = Attendee.Create(
                staleId,
                "Stale",
                "stale@mail.com",
                pilots,
                clock.UtcNow);
            var cancelledInvite = Invite.CreateInitial(
                Guid.NewGuid(),
                actionableId,
                "cancelled-invite-hash",
                clock.UtcNow.AddDays(4),
                [ProposalFixture.LocationId],
                [cancelledEvent.Id, Guid.NewGuid(), Guid.NewGuid()],
                actionableAttendee.RequiredAppointmentTypeIds,
                0);
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
            .Options;

        return new EventBookingDbContext(options);
    }

    private static Event EventFor(DateOnly date, TimeOnly startTime)
    {
        var proposal = ProposalFixture.Create(Guid.NewGuid(), new EventWindow(date, startTime, 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        return Event.CreateFrom(Guid.NewGuid(), proposal);
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/InviteRequirementPersistenceTests.cs — 1/1

<!-- retirement-file: {"id":83,"file":"tests/EventBooking.Infrastructure.Tests/InviteRequirementPersistenceTests.cs","beforeSha":"c671baabe3fa508ac17920d13b38a847559acca08a2d5aaf99f40dc561e7d684","afterSha":"5792e45df22557dca992eb044e3f630f7d2b5b09be365839c8e009db7e4a6e70","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies Invite requirement snapshots round-trip with restrictive constraints.</summary>
[Collection("postgres")]
public sealed class InviteRequirementPersistenceTests(PostgresFixture fixture)
{
    /// <summary>An Invite reloads options, requirements, and recovery linkage together.</summary>
    [Fact]
    public async Task SnapshotRoundTrips()
    {
        await fixture.ResetAsync();
        var group = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        var attendee = Attendee.Create(Guid.NewGuid(), "Amara", "amara@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, "hash", DateTimeOffset.UtcNow.AddDays(1),
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()], attendee.RequiredAppointmentTypeIds, 0);

        await using (var write = fixture.NewContext())
        {
            write.Attendees.Add(attendee);
            write.Invites.Add(invite);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var actual = await read.Invites
            .Include(value => value.Options)
            .Include(value => value.Requirements)
            .SingleAsync(value => value.Id == invite.Id);
        Assert.Equal(3, actual.Options.Count);
        Assert.Equal(attendee.RequiredAppointmentTypeIds, actual.RequiredAppointmentTypeIds);
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/InviteRequirementPersistenceTests.cs — 1/1

<!-- retirement-file: {"id":83,"file":"tests/EventBooking.Infrastructure.Tests/InviteRequirementPersistenceTests.cs","beforeSha":"c671baabe3fa508ac17920d13b38a847559acca08a2d5aaf99f40dc561e7d684","afterSha":"5792e45df22557dca992eb044e3f630f7d2b5b09be365839c8e009db7e4a6e70","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies Invite requirement snapshots round-trip with restrictive constraints.</summary>
[Collection("postgres")]
public sealed class InviteRequirementPersistenceTests(PostgresFixture fixture)
{
    /// <summary>An Invite reloads options, requirements, and recovery linkage together.</summary>
    [Fact]
    public async Task SnapshotRoundTrips()
    {
        await fixture.ResetAsync();
        var group = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        var attendee = Attendee.Create(Guid.NewGuid(), "Amara", "amara@example.com", group, ProposalFixture.Now);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            "hash",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);

        await using (var write = fixture.NewContext())
        {
            write.Attendees.Add(attendee);
            write.Invites.Add(invite);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var actual = await read.Invites
            .Include(value => value.Options)
            .Include(value => value.Requirements)
            .SingleAsync(value => value.Id == invite.Id);
        Assert.Equal(3, actual.Options.Count);
        Assert.Equal(attendee.RequiredAppointmentTypeIds, actual.RequiredAppointmentTypeIds);
    }

    /// <summary>An Invite reloads the location set every later offer must be drawn from.</summary>
    [Fact]
    public async Task TheLocationSetRoundTrips()
    {
        await fixture.ResetAsync();
        var second = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var group = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Bo", "bo@example.com", group, ProposalFixture.Now);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            "hash-of-a-two-location-invite",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId, second],
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);

        await using (var write = fixture.NewContext())
        {
            write.Attendees.Add(attendee);
            write.Invites.Add(invite);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var actual = await read.Invites
            .Include(value => value.Locations)
            .SingleAsync(value => value.Id == invite.Id);

        Assert.Equal([ProposalFixture.LocationId, second], actual.LocationIds.Order());
        Assert.All(actual.Locations, location => Assert.Equal(invite.Id, location.InviteId));
    }

    /// <summary>The attendee's status stamp is the aggregate's own, and survives a round trip.</summary>
    [Fact]
    public async Task TheAttendeeStatusStampRoundTrips()
    {
        await fixture.ResetAsync();
        var stampedAt = new DateTimeOffset(2026, 9, 4, 11, 0, 0, TimeSpan.Zero);
        var group = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Cass", "cass@example.com", group, ProposalFixture.Now);
        attendee.MarkAwaitingAvailability(stampedAt);

        await using (var write = fixture.NewContext())
        {
            write.Attendees.Add(attendee);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var actual = await read.Attendees.SingleAsync(value => value.Id == attendee.Id);

        Assert.Equal(AttendeeStatus.AwaitingAvailability, actual.Status);
        Assert.Equal(stampedAt, actual.StatusChangedAt);
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/LoggingEmailSenderTests.cs — 1/1

<!-- retirement-file: {"id":84,"file":"tests/EventBooking.Infrastructure.Tests/LoggingEmailSenderTests.cs","beforeSha":"3b6d4dd100f78b59aebb62cd444cb15b8e374bfb767f8833a0abb1d5f5e9e9de","afterSha":"39f6da56e9bac6f7f9470bb2b0e50295e54682192a7017dbe341688f8b53725a","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Notifications;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class LoggingEmailSenderTests(PostgresFixture fixture)
{
    [Theory]
    [InlineData("not-an-email", "EventBooking")]
    [InlineData("sender@example.com", " ")]
    public void SenderOptionsRejectIncompleteValues(string fromAddress, string fromName)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new EmailOptions(fromAddress, fromName, EmailProvider.Smtp));

        if (!string.IsNullOrWhiteSpace(fromAddress))
        {
            Assert.DoesNotContain(fromAddress, exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SenderOptionsDoNotExposeTheirValuesWhenFormatted()
    {
        var options = new EmailOptions("sender@example.com", "EventBooking", EmailProvider.Smtp);

        var formatted = options.ToString();

        Assert.DoesNotContain(options.FromAddress, formatted, StringComparison.Ordinal);
        Assert.DoesNotContain(options.FromName, formatted, StringComparison.Ordinal);
    }

    private sealed class FakeTransport : IEmailTransport
    {
        public List<EmailMessage> Sent { get; } = [];

        public bool Throw { get; set; }

        public bool Cancel { get; set; }

        public Action? AfterSend { get; set; }

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            if (Cancel)
            {
                return Task.FromCanceled(cancellationToken);
            }

            if (Throw)
            {
                throw new InvalidOperationException("the provider rejected the message");
            }

            Sent.Add(message);
            AfterSend?.Invoke();
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;

        /// <summary>Gets the fixed instant; this clock treats UTC as transitional-location time.</summary>
        public DateTimeOffset NowAtTransitionalLocation => now;

        public DateOnly TodayAtTransitionalLocation => DateAtTransitionalLocation(now);

        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) => DateOnly.FromDateTime(instant.UtcDateTime);

        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => instant.ToUniversalTime();
    }

    [Fact]
    public async Task ASuccessfulSendIsLoggedAsSent()
    {
        var (sender, transport, attendeeId) = await Given();

        var sent = await sender.SendAsync(MessageFor(attendeeId), CancellationToken.None);

        Assert.True(sent);
        Assert.Single(transport.Sent);

        await using var context = fixture.NewContext();
        var log = await context.EmailLogs.SingleAsync();
        Assert.Equal(attendeeId, log.AttendeeId);
        Assert.Equal(EmailTemplate.AttendeeInvite, log.TemplateName);
        Assert.Equal(EmailStatus.Sent, log.Status);
    }

    [Fact]
    public async Task AFailedSendReturnsFalseAndIsLoggedAsFailed()
    {
        var (sender, transport, attendeeId) = await Given();
        transport.Throw = true;

        var sent = await sender.SendAsync(MessageFor(attendeeId), CancellationToken.None);

        Assert.False(sent);
        Assert.Empty(transport.Sent);

        await using var context = fixture.NewContext();
        var log = await context.EmailLogs.SingleAsync();
        Assert.Equal(EmailStatus.Failed, log.Status);
    }

    [Fact]
    public async Task ThePublishedTimeComesFromTheClock()
    {
        var now = new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
        var (sender, _, attendeeId) = await Given(now);

        await sender.SendAsync(MessageFor(attendeeId), CancellationToken.None);

        await using var context = fixture.NewContext();
        Assert.Equal(now, (await context.EmailLogs.SingleAsync()).SentAt);
    }

    [Fact]
    public async Task ACancelledTransportPropagatesCancellationWithoutWritingAnEmailLog()
    {
        var (sender, transport, attendeeId) = await Given();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        transport.Cancel = true;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sender.SendAsync(MessageFor(attendeeId), cancellation.Token));

        await using var context = fixture.NewContext();
        Assert.Empty(await context.EmailLogs.ToListAsync());
    }

    [Fact]
    public async Task ACancelledAuditSavePropagatesCancellationWithoutWritingAnEmailLog()
    {
        var (sender, transport, attendeeId) = await Given();
        using var cancellation = new CancellationTokenSource();
        transport.AfterSend = cancellation.Cancel;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sender.SendAsync(MessageFor(attendeeId), cancellation.Token));

        await using var context = fixture.NewContext();
        Assert.Empty(await context.EmailLogs.ToListAsync());
    }

    [Fact]
    public async Task AnAuditContextFailureIsWarnedAndDoesNotFailTheSend()
    {
        var (_, _, attendeeId) = await Given();
        var logger = new RecordingLogger<LoggingEmailSender>();
        var sender = new LoggingEmailSender(
            new FakeTransport(),
            new ThrowingContextFactory(),
            new FixedClock(DateTimeOffset.UtcNow),
            logger);

        var sent = await sender.SendAsync(MessageFor(attendeeId), CancellationToken.None);

        Assert.True(sent);
        Assert.Contains(LogLevel.Warning, logger.LogLevels);

        await using var context = fixture.NewContext();
        Assert.Empty(await context.EmailLogs.ToListAsync());
    }

    private async Task<(LoggingEmailSender Sender, FakeTransport Transport, Guid AttendeeId)> Given(
        DateTimeOffset? now = null)
    {
        await fixture.ResetAsync();

        var attendeeId = Guid.NewGuid();
        await using (var write = fixture.NewContext())
        {
            var pilots = write.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
            var attendee = Attendee.Create(
                attendeeId, "Amara Novak", "a.novak@mail.com", pilots);
            write.Attendees.Add(attendee);
            await write.SaveChangesAsync();
        }

        var transport = new FakeTransport();
        var sender = new LoggingEmailSender(
            transport,
            new TestContextFactory(fixture),
            new FixedClock(now ?? DateTimeOffset.UtcNow),
            NullLogger<LoggingEmailSender>.Instance);

        return (sender, transport, attendeeId);
    }

    private static EmailMessage MessageFor(Guid attendeeId) =>
        new(attendeeId, "a.novak@mail.com", "Amara Novak", EmailTemplate.AttendeeInvite,
            "Choose a time", "text", "<html></html>");

    private sealed class TestContextFactory(PostgresFixture fixture)
        : IDbContextFactory<EventBookingDbContext>
    {
        public EventBookingDbContext CreateDbContext() => fixture.NewContext();
    }

    private sealed class ThrowingContextFactory : IDbContextFactory<EventBookingDbContext>
    {
        public EventBookingDbContext CreateDbContext() =>
            throw new InvalidOperationException("the audit database is unavailable");
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogLevel> LogLevels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LogLevels.Add(logLevel);
        }
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/LoggingEmailSenderTests.cs — 1/1

<!-- retirement-file: {"id":84,"file":"tests/EventBooking.Infrastructure.Tests/LoggingEmailSenderTests.cs","beforeSha":"3b6d4dd100f78b59aebb62cd444cb15b8e374bfb767f8833a0abb1d5f5e9e9de","afterSha":"39f6da56e9bac6f7f9470bb2b0e50295e54682192a7017dbe341688f8b53725a","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Notifications;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class LoggingEmailSenderTests(PostgresFixture fixture)
{
    [Theory]
    [InlineData("not-an-email", "EventBooking")]
    [InlineData("sender@example.com", " ")]
    public void SenderOptionsRejectIncompleteValues(string fromAddress, string fromName)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new EmailOptions(fromAddress, fromName, EmailProvider.Smtp));

        if (!string.IsNullOrWhiteSpace(fromAddress))
        {
            Assert.DoesNotContain(fromAddress, exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SenderOptionsDoNotExposeTheirValuesWhenFormatted()
    {
        var options = new EmailOptions("sender@example.com", "EventBooking", EmailProvider.Smtp);

        var formatted = options.ToString();

        Assert.DoesNotContain(options.FromAddress, formatted, StringComparison.Ordinal);
        Assert.DoesNotContain(options.FromName, formatted, StringComparison.Ordinal);
    }

    private sealed class FakeTransport : IEmailTransport
    {
        public List<EmailMessage> Sent { get; } = [];

        public bool Throw { get; set; }

        public bool Cancel { get; set; }

        public Action? AfterSend { get; set; }

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            if (Cancel)
            {
                return Task.FromCanceled(cancellationToken);
            }

            if (Throw)
            {
                throw new InvalidOperationException("the provider rejected the message");
            }

            Sent.Add(message);
            AfterSend?.Invoke();
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;

        /// <summary>Gets the fixed instant; this clock treats UTC as transitional-location time.</summary>
        public DateTimeOffset NowAtTransitionalLocation => now;

        public DateOnly TodayAtTransitionalLocation => DateAtTransitionalLocation(now);

        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) => DateOnly.FromDateTime(instant.UtcDateTime);

        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => instant.ToUniversalTime();
    }

    [Fact]
    public async Task ASuccessfulSendIsLoggedAsSent()
    {
        var (sender, transport, attendeeId) = await Given();

        var sent = await sender.SendAsync(MessageFor(attendeeId), CancellationToken.None);

        Assert.True(sent);
        Assert.Single(transport.Sent);

        await using var context = fixture.NewContext();
        var log = await context.EmailLogs.SingleAsync();
        Assert.Equal(attendeeId, log.AttendeeId);
        Assert.Equal(EmailTemplate.AttendeeInvite, log.TemplateName);
        Assert.Equal(EmailStatus.Sent, log.Status);
    }

    [Fact]
    public async Task AFailedSendReturnsFalseAndIsLoggedAsFailed()
    {
        var (sender, transport, attendeeId) = await Given();
        transport.Throw = true;

        var sent = await sender.SendAsync(MessageFor(attendeeId), CancellationToken.None);

        Assert.False(sent);
        Assert.Empty(transport.Sent);

        await using var context = fixture.NewContext();
        var log = await context.EmailLogs.SingleAsync();
        Assert.Equal(EmailStatus.Failed, log.Status);
    }

    [Fact]
    public async Task ThePublishedTimeComesFromTheClock()
    {
        var now = new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
        var (sender, _, attendeeId) = await Given(now);

        await sender.SendAsync(MessageFor(attendeeId), CancellationToken.None);

        await using var context = fixture.NewContext();
        Assert.Equal(now, (await context.EmailLogs.SingleAsync()).SentAt);
    }

    [Fact]
    public async Task ACancelledTransportPropagatesCancellationWithoutWritingAnEmailLog()
    {
        var (sender, transport, attendeeId) = await Given();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        transport.Cancel = true;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sender.SendAsync(MessageFor(attendeeId), cancellation.Token));

        await using var context = fixture.NewContext();
        Assert.Empty(await context.EmailLogs.ToListAsync());
    }

    [Fact]
    public async Task ACancelledAuditSavePropagatesCancellationWithoutWritingAnEmailLog()
    {
        var (sender, transport, attendeeId) = await Given();
        using var cancellation = new CancellationTokenSource();
        transport.AfterSend = cancellation.Cancel;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sender.SendAsync(MessageFor(attendeeId), cancellation.Token));

        await using var context = fixture.NewContext();
        Assert.Empty(await context.EmailLogs.ToListAsync());
    }

    [Fact]
    public async Task AnAuditContextFailureIsWarnedAndDoesNotFailTheSend()
    {
        var (_, _, attendeeId) = await Given();
        var logger = new RecordingLogger<LoggingEmailSender>();
        var sender = new LoggingEmailSender(
            new FakeTransport(),
            new ThrowingContextFactory(),
            new FixedClock(DateTimeOffset.UtcNow),
            logger);

        var sent = await sender.SendAsync(MessageFor(attendeeId), CancellationToken.None);

        Assert.True(sent);
        Assert.Contains(LogLevel.Warning, logger.LogLevels);

        await using var context = fixture.NewContext();
        Assert.Empty(await context.EmailLogs.ToListAsync());
    }

    private async Task<(LoggingEmailSender Sender, FakeTransport Transport, Guid AttendeeId)> Given(
        DateTimeOffset? now = null)
    {
        await fixture.ResetAsync();

        var attendeeId = Guid.NewGuid();
        await using (var write = fixture.NewContext())
        {
            var pilots = write.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
            var attendee = Attendee.Create(
                attendeeId,
                "Amara Novak",
                "a.novak@mail.com",
                pilots,
                ProposalFixture.Now);
            write.Attendees.Add(attendee);
            await write.SaveChangesAsync();
        }

        var transport = new FakeTransport();
        var sender = new LoggingEmailSender(
            transport,
            new TestContextFactory(fixture),
            new FixedClock(now ?? DateTimeOffset.UtcNow),
            NullLogger<LoggingEmailSender>.Instance);

        return (sender, transport, attendeeId);
    }

    private static EmailMessage MessageFor(Guid attendeeId) =>
        new(attendeeId, "a.novak@mail.com", "Amara Novak", EmailTemplate.AttendeeInvite,
            "Choose a time", "text", "<html></html>");

    private sealed class TestContextFactory(PostgresFixture fixture)
        : IDbContextFactory<EventBookingDbContext>
    {
        public EventBookingDbContext CreateDbContext() => fixture.NewContext();
    }

    private sealed class ThrowingContextFactory : IDbContextFactory<EventBookingDbContext>
    {
        public EventBookingDbContext CreateDbContext() =>
            throw new InvalidOperationException("the audit database is unavailable");
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogLevel> LogLevels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LogLevels.Add(logLevel);
        }
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/RecoveryBookingPersistenceTests.cs — 1/1

<!-- retirement-file: {"id":85,"file":"tests/EventBooking.Infrastructure.Tests/RecoveryBookingPersistenceTests.cs","beforeSha":"368784ca17e7344126ede5b32096bea4685067ba28e47faccfd4a934113a8fe2","afterSha":"5594d185e30c3b1d0a78ed9902e6e8f2991db96a849e0c9bc2e794b3bca65325","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies recovery journey links, ordering, and active uniqueness backstops.</summary>
[Collection("postgres")]
public sealed class RecoveryBookingPersistenceTests(PostgresFixture fixture)
{
    /// <summary>Verifies an original and two concluded recoveries reload as one ordered journey.</summary>
    [Fact]
    public async Task JourneyLinksPersistAndReloadInCreationOrder()
    {
        await fixture.ResetAsync();
        var attendeeId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var initial = Invite.CreateInitial(
            Guid.NewGuid(), attendeeId, "initial", DateTimeOffset.UtcNow.AddDays(1),
            [eventId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        var original = Booking.Create(
            Guid.NewGuid(), initial, eventId, "manage-original", DateTimeOffset.UtcNow);
        var first = RecoveryFor(attendeeId, original, DateTimeOffset.UtcNow.AddHours(1));
        var second = RecoveryFor(attendeeId, original, DateTimeOffset.UtcNow.AddHours(2));
        first.Conclude();
        second.Conclude();

        await using (var write = fixture.NewContext())
        {
            write.Bookings.AddRange(original, first, second);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var journey = await new BookingRepository(read)
            .ListJourneyAsync(original.Id, CancellationToken.None);

        Assert.Equal([original.Id, first.Id, second.Id], journey.Select(b => b.Id));
        Assert.Null(journey[0].RecoveryOfBookingId);
        Assert.Equal(BookingStatus.Active, journey[0].Status);
        Assert.Equal([original.Id, original.Id], journey.Skip(1).Select(b => b.RecoveryOfBookingId));
        Assert.All(journey.Skip(1), b => Assert.Equal(BookingStatus.Concluded, b.Status));
    }

    /// <summary>Verifies a second active original for one attendee violates uniqueness.</summary>
    [Fact]
    public async Task SecondActiveOriginalViolatesUniqueness()
    {
        await fixture.ResetAsync();
        var attendeeId = Guid.NewGuid();
        var first = OriginalFor(attendeeId);
        var second = OriginalFor(attendeeId);

        await using var context = fixture.NewContext();
        context.Bookings.AddRange(first, second);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    /// <summary>Verifies a second active recovery for one root violates uniqueness.</summary>
    [Fact]
    public async Task SecondActiveRecoveryViolatesUniqueness()
    {
        await fixture.ResetAsync();
        var attendeeId = Guid.NewGuid();
        var original = OriginalFor(attendeeId);
        var first = RecoveryFor(attendeeId, original, DateTimeOffset.UtcNow.AddHours(1));
        var second = RecoveryFor(attendeeId, original, DateTimeOffset.UtcNow.AddHours(2));

        await using var context = fixture.NewContext();
        context.Bookings.AddRange(original, first, second);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static Booking OriginalFor(Guid attendeeId)
    {
        var eventId = Guid.NewGuid();
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendeeId, "initial", DateTimeOffset.UtcNow.AddDays(1),
            [eventId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        return Booking.Create(Guid.NewGuid(), invite, eventId, "manage", DateTimeOffset.UtcNow);
    }

    private static Booking RecoveryFor(Guid attendeeId, Booking original, DateTimeOffset createdAt)
    {
        var eventId = Guid.NewGuid();
        var invite = Invite.CreateRecovery(
            Guid.NewGuid(), attendeeId, original.Id, "recovery", DateTimeOffset.UtcNow.AddDays(2),
            [eventId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);
        return Booking.CreateRecovery(
            Guid.NewGuid(), invite, original, eventId, $"manage-recovery-{Guid.NewGuid():N}", createdAt);
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/RecoveryBookingPersistenceTests.cs — 1/1

<!-- retirement-file: {"id":85,"file":"tests/EventBooking.Infrastructure.Tests/RecoveryBookingPersistenceTests.cs","beforeSha":"368784ca17e7344126ede5b32096bea4685067ba28e47faccfd4a934113a8fe2","afterSha":"5594d185e30c3b1d0a78ed9902e6e8f2991db96a849e0c9bc2e794b3bca65325","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies recovery journey links, ordering, and active uniqueness backstops.</summary>
[Collection("postgres")]
public sealed class RecoveryBookingPersistenceTests(PostgresFixture fixture)
{
    /// <summary>Verifies an original and two concluded recoveries reload as one ordered journey.</summary>
    [Fact]
    public async Task JourneyLinksPersistAndReloadInCreationOrder()
    {
        await fixture.ResetAsync();
        var attendeeId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var initial = Invite.CreateInitial(
            Guid.NewGuid(),
            attendeeId,
            "initial",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp],
            0);
        var original = Booking.Create(
            Guid.NewGuid(), initial, eventId, "manage-original", DateTimeOffset.UtcNow);
        var first = RecoveryFor(attendeeId, original, DateTimeOffset.UtcNow.AddHours(1));
        var second = RecoveryFor(attendeeId, original, DateTimeOffset.UtcNow.AddHours(2));
        first.Conclude();
        second.Conclude();

        await using (var write = fixture.NewContext())
        {
            write.Bookings.AddRange(original, first, second);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var journey = await new BookingRepository(read)
            .ListJourneyAsync(original.Id, CancellationToken.None);

        Assert.Equal([original.Id, first.Id, second.Id], journey.Select(b => b.Id));
        Assert.Null(journey[0].RecoveryOfBookingId);
        Assert.Equal(BookingStatus.Active, journey[0].Status);
        Assert.Equal([original.Id, original.Id], journey.Skip(1).Select(b => b.RecoveryOfBookingId));
        Assert.All(journey.Skip(1), b => Assert.Equal(BookingStatus.Concluded, b.Status));
    }

    /// <summary>Verifies a second active original for one attendee violates uniqueness.</summary>
    [Fact]
    public async Task SecondActiveOriginalViolatesUniqueness()
    {
        await fixture.ResetAsync();
        var attendeeId = Guid.NewGuid();
        var first = OriginalFor(attendeeId);
        var second = OriginalFor(attendeeId);

        await using var context = fixture.NewContext();
        context.Bookings.AddRange(first, second);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    /// <summary>Verifies a second active recovery for one root violates uniqueness.</summary>
    [Fact]
    public async Task SecondActiveRecoveryViolatesUniqueness()
    {
        await fixture.ResetAsync();
        var attendeeId = Guid.NewGuid();
        var original = OriginalFor(attendeeId);
        var first = RecoveryFor(attendeeId, original, DateTimeOffset.UtcNow.AddHours(1));
        var second = RecoveryFor(attendeeId, original, DateTimeOffset.UtcNow.AddHours(2));

        await using var context = fixture.NewContext();
        context.Bookings.AddRange(original, first, second);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static Booking OriginalFor(Guid attendeeId)
    {
        var eventId = Guid.NewGuid();
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendeeId,
            "initial",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp],
            0);
        return Booking.Create(Guid.NewGuid(), invite, eventId, "manage", DateTimeOffset.UtcNow);
    }

    private static Booking RecoveryFor(Guid attendeeId, Booking original, DateTimeOffset createdAt)
    {
        var eventId = Guid.NewGuid();
        var invite = Invite.CreateRecovery(
            Guid.NewGuid(),
            attendeeId,
            original.Id,
            "recovery",
            DateTimeOffset.UtcNow.AddDays(2),
            ProposalFixture.LocationId,
            null,
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp]);
        return Booking.CreateRecovery(
            Guid.NewGuid(), invite, original, eventId, $"manage-recovery-{Guid.NewGuid():N}", createdAt);
    }
}
`````
