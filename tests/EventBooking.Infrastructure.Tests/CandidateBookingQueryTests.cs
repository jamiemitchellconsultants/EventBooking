using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies the staff active-booking listing, its ordering, and its derived window end.</summary>
[Collection("postgres")]
public sealed class CandidateBookingQueryTests(PostgresFixture fixture)
{
    [Fact]
    public async Task AnUnknownCandidateReturnsNull()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var rows = await new CandidateBookingQueries(context)
            .ListActiveForCandidateAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(rows);
    }

    [Fact]
    public async Task ACandidateWithNoActiveBookingReturnsAnEmptyList()
    {
        await fixture.ResetAsync();
        var candidateId = await SeedCandidateAsync();

        await using var context = fixture.NewContext();
        var rows = await new CandidateBookingQueries(context)
            .ListActiveForCandidateAsync(candidateId, CancellationToken.None);

        Assert.NotNull(rows);
        Assert.Empty(rows!);
    }

    [Fact]
    public async Task TheOriginalSortsFirstAndEachWindowEndIsDerived()
    {
        await fixture.ResetAsync();
        var candidateId = await SeedCandidateAsync();
        var originalSlot = await SeedSlotAsync(new DateOnly(2026, 9, 10), new TimeOnly(9, 0));
        var recoverySlot = await SeedSlotAsync(new DateOnly(2026, 9, 12), new TimeOnly(13, 0));

        var original = OriginalFor(candidateId, originalSlot);
        var recovery = RecoveryFor(candidateId, original, recoverySlot);
        await using (var write = fixture.NewContext())
        {
            write.Bookings.AddRange(original, recovery);
            await write.SaveChangesAsync();
        }

        await using var context = fixture.NewContext();
        var rows = await new CandidateBookingQueries(context)
            .ListActiveForCandidateAsync(candidateId, CancellationToken.None);

        Assert.NotNull(rows);
        Assert.Equal(2, rows!.Count);

        Assert.True(rows[0].IsOriginal);
        Assert.Equal(original.Id, rows[0].BookingId);
        Assert.Equal(new DateOnly(2026, 9, 10), rows[0].SlotDate);
        Assert.Equal(new TimeOnly(9, 0), rows[0].SlotStartTime);
        Assert.Equal(new TimeOnly(13, 0), rows[0].SlotEndTime);

        Assert.False(rows[1].IsOriginal);
        Assert.Equal(recovery.Id, rows[1].BookingId);
        Assert.Equal(new TimeOnly(13, 0), rows[1].SlotStartTime);
        Assert.Equal(new TimeOnly(17, 0), rows[1].SlotEndTime);
    }

    [Fact]
    public async Task ACancelledBookingIsNotListed()
    {
        await fixture.ResetAsync();
        var candidateId = await SeedCandidateAsync();
        var slotId = await SeedSlotAsync(new DateOnly(2026, 9, 10), new TimeOnly(9, 0));
        var booking = OriginalFor(candidateId, slotId);
        booking.Cancel();

        await using (var write = fixture.NewContext())
        {
            write.Bookings.Add(booking);
            await write.SaveChangesAsync();
        }

        await using var context = fixture.NewContext();
        var rows = await new CandidateBookingQueries(context)
            .ListActiveForCandidateAsync(candidateId, CancellationToken.None);

        Assert.NotNull(rows);
        Assert.Empty(rows!);
    }

    private async Task<Guid> SeedCandidateAsync()
    {
        await using var write = fixture.NewContext();
        var pilots = await write.EmployeeGroups
            .Include(g => g.Requirements)
            .SingleAsync(g => g.Id == EmployeeGroupIds.Pilots);
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", $"a.novak.{Guid.NewGuid():N}@mail.com", pilots);
        write.Candidates.Add(candidate);
        await write.SaveChangesAsync();
        return candidate.Id;
    }

    private async Task<Guid> SeedSlotAsync(DateOnly date, TimeOnly startTime)
    {
        var proposal = SlotProposal.Create(Guid.NewGuid(), new SlotWindow(date, startTime), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 10);
        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);

        await using var write = fixture.NewContext();
        write.SlotProposals.Add(proposal);
        write.ConfirmedSlots.Add(slot);
        await write.SaveChangesAsync();
        return slot.Id;
    }

    private static Booking OriginalFor(Guid candidateId, Guid slotId)
    {
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidateId, $"initial-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.AddDays(1),
            [slotId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        return Booking.Create(
            Guid.NewGuid(), invite, slotId, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
    }

    private static Booking RecoveryFor(Guid candidateId, Booking original, Guid slotId)
    {
        var invite = Invite.CreateRecovery(
            Guid.NewGuid(), candidateId, original.Id, $"recovery-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(2),
            [slotId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);
        return Booking.CreateRecovery(
            Guid.NewGuid(), invite, original, slotId, $"manage-recovery-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddHours(1));
    }
}
