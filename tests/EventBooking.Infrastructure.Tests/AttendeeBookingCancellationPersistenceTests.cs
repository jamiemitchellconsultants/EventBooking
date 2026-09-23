using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;
using EventBooking.Infrastructure.Persistence.Repositories;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies the staff cancellation lock is scoped to the booking's own attendee.</summary>
[Collection("postgres")]
public sealed class AttendeeBookingCancellationPersistenceTests(PostgresFixture fixture)
{
    /// <summary>Verifies the lock returns the booking when the attendee owns it.</summary>
    [Fact]
    public async Task LockByIdForAttendeeReturnsTheBookingForItsOwnAttendee()
    {
        await fixture.ResetAsync();
        var attendeeId = Guid.NewGuid();
        var booking = OriginalFor(attendeeId);
        await SeedAsync(booking);

        await using var context = fixture.NewContext();
        var locked = await new BookingRepository(context)
            .LockByIdForAttendeeAsync(booking.Id, attendeeId, CancellationToken.None);

        Assert.NotNull(locked);
        Assert.Equal(booking.Id, locked!.Id);
        Assert.Equal(attendeeId, locked.AttendeeId);
    }

    /// <summary>Verifies one attendee cannot lock another attendee's booking.</summary>
    [Fact]
    public async Task LockByIdForAttendeeReturnsNullForAnotherAttendeesBooking()
    {
        await fixture.ResetAsync();
        var attendeeId = Guid.NewGuid();
        var booking = OriginalFor(attendeeId);
        await SeedAsync(booking, OriginalFor(Guid.NewGuid()));

        await using var context = fixture.NewContext();
        var locked = await new BookingRepository(context)
            .LockByIdForAttendeeAsync(booking.Id, Guid.NewGuid(), CancellationToken.None);

        Assert.Null(locked);
    }

    /// <summary>Verifies an unknown booking identifier locks nothing.</summary>
    [Fact]
    public async Task LockByIdForAttendeeReturnsNullForAnUnknownId()
    {
        await fixture.ResetAsync();
        var attendeeId = Guid.NewGuid();
        await SeedAsync(OriginalFor(attendeeId));

        await using var context = fixture.NewContext();
        var locked = await new BookingRepository(context)
            .LockByIdForAttendeeAsync(Guid.NewGuid(), attendeeId, CancellationToken.None);

        Assert.Null(locked);
    }

    /// <summary>Verifies a recovery booking is lockable by id like any other booking.</summary>
    [Fact]
    public async Task LockByIdForAttendeeReturnsARecoveryBooking()
    {
        await fixture.ResetAsync();
        var attendeeId = Guid.NewGuid();
        var original = OriginalFor(attendeeId);
        var recovery = RecoveryFor(attendeeId, original, DateTimeOffset.UtcNow.AddHours(1));
        await SeedAsync(original, recovery);

        await using var context = fixture.NewContext();
        var locked = await new BookingRepository(context)
            .LockByIdForAttendeeAsync(recovery.Id, attendeeId, CancellationToken.None);

        Assert.NotNull(locked);
        Assert.Equal(original.Id, locked!.RecoveryOfBookingId);
    }

    private async Task SeedAsync(params Booking[] bookings)
    {
        await using var write = fixture.NewContext();
        write.Bookings.AddRange(bookings);
        await write.SaveChangesAsync();
    }

    private static Booking OriginalFor(Guid attendeeId)
    {
        var eventId = Guid.NewGuid();
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendeeId, $"initial-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.AddDays(1),
            [eventId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        return Booking.Create(
            Guid.NewGuid(), invite, eventId, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
    }

    private static Booking RecoveryFor(Guid attendeeId, Booking original, DateTimeOffset createdAt)
    {
        var eventId = Guid.NewGuid();
        var invite = Invite.CreateRecovery(
            Guid.NewGuid(), attendeeId, original.Id, $"recovery-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(2),
            [eventId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);
        return Booking.CreateRecovery(
            Guid.NewGuid(), invite, original, eventId, $"manage-recovery-{Guid.NewGuid():N}", createdAt);
    }
}
