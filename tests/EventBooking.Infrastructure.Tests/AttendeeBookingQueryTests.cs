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
            Guid.NewGuid(),
            "Amara Novak",
            $"a.novak.{Guid.NewGuid():N}@mail.com",
            pilots,
            ProposalFixture.Now);
        write.Attendees.Add(attendee);
        await write.SaveChangesAsync();
        return attendee.Id;
    }

    private async Task<Guid> SeedEventAsync(DateOnly date, TimeOnly startTime)
    {
        var proposal = ProposalFixture.Create(Guid.NewGuid(), new EventWindow(date, startTime, 240), Guid.NewGuid());
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
            Guid.NewGuid(),
            attendeeId,
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp],
            0);
        return Booking.Create(
            Guid.NewGuid(), invite, eventId, DateTimeOffset.UtcNow);
    }

    private static Booking RecoveryFor(Guid attendeeId, Booking original, Guid eventId)
    {
        var invite = Invite.CreateRecovery(
            Guid.NewGuid(),
            attendeeId,
            original.Id,
            DateTimeOffset.UtcNow.AddDays(2),
            ProposalFixture.LocationId,
            null,
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp]);
        return Booking.CreateRecovery(
            Guid.NewGuid(), invite, original, eventId,
            DateTimeOffset.UtcNow.AddHours(1));
    }
}
