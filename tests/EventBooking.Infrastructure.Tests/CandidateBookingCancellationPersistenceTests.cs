using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;
using EventBooking.Infrastructure.Persistence.Repositories;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies the staff cancellation lock is scoped to the booking's own candidate.</summary>
[Collection("postgres")]
public sealed class CandidateBookingCancellationPersistenceTests(PostgresFixture fixture)
{
    /// <summary>Verifies the lock returns the booking when the candidate owns it.</summary>
    [Fact]
    public async Task LockByIdForCandidateReturnsTheBookingForItsOwnCandidate()
    {
        await fixture.ResetAsync();
        var candidateId = Guid.NewGuid();
        var booking = OriginalFor(candidateId);
        await SeedAsync(booking);

        await using var context = fixture.NewContext();
        var locked = await new BookingRepository(context)
            .LockByIdForCandidateAsync(booking.Id, candidateId, CancellationToken.None);

        Assert.NotNull(locked);
        Assert.Equal(booking.Id, locked!.Id);
        Assert.Equal(candidateId, locked.CandidateId);
    }

    /// <summary>Verifies one candidate cannot lock another candidate's booking.</summary>
    [Fact]
    public async Task LockByIdForCandidateReturnsNullForAnotherCandidatesBooking()
    {
        await fixture.ResetAsync();
        var candidateId = Guid.NewGuid();
        var booking = OriginalFor(candidateId);
        await SeedAsync(booking, OriginalFor(Guid.NewGuid()));

        await using var context = fixture.NewContext();
        var locked = await new BookingRepository(context)
            .LockByIdForCandidateAsync(booking.Id, Guid.NewGuid(), CancellationToken.None);

        Assert.Null(locked);
    }

    /// <summary>Verifies an unknown booking identifier locks nothing.</summary>
    [Fact]
    public async Task LockByIdForCandidateReturnsNullForAnUnknownId()
    {
        await fixture.ResetAsync();
        var candidateId = Guid.NewGuid();
        await SeedAsync(OriginalFor(candidateId));

        await using var context = fixture.NewContext();
        var locked = await new BookingRepository(context)
            .LockByIdForCandidateAsync(Guid.NewGuid(), candidateId, CancellationToken.None);

        Assert.Null(locked);
    }

    /// <summary>Verifies a recovery booking is lockable by id like any other booking.</summary>
    [Fact]
    public async Task LockByIdForCandidateReturnsARecoveryBooking()
    {
        await fixture.ResetAsync();
        var candidateId = Guid.NewGuid();
        var original = OriginalFor(candidateId);
        var recovery = RecoveryFor(candidateId, original, DateTimeOffset.UtcNow.AddHours(1));
        await SeedAsync(original, recovery);

        await using var context = fixture.NewContext();
        var locked = await new BookingRepository(context)
            .LockByIdForCandidateAsync(recovery.Id, candidateId, CancellationToken.None);

        Assert.NotNull(locked);
        Assert.Equal(original.Id, locked!.RecoveryOfBookingId);
    }

    private async Task SeedAsync(params Booking[] bookings)
    {
        await using var write = fixture.NewContext();
        write.Bookings.AddRange(bookings);
        await write.SaveChangesAsync();
    }

    private static Booking OriginalFor(Guid candidateId)
    {
        var slotId = Guid.NewGuid();
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidateId, $"initial-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.AddDays(1),
            [slotId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        return Booking.Create(
            Guid.NewGuid(), invite, slotId, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
    }

    private static Booking RecoveryFor(Guid candidateId, Booking original, DateTimeOffset createdAt)
    {
        var slotId = Guid.NewGuid();
        var invite = Invite.CreateRecovery(
            Guid.NewGuid(), candidateId, original.Id, $"recovery-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(2),
            [slotId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);
        return Booking.CreateRecovery(
            Guid.NewGuid(), invite, original, slotId, $"manage-recovery-{Guid.NewGuid():N}", createdAt);
    }
}
