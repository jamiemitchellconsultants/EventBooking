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
