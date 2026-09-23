using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies the readiness journey projection across original and recovery bookings.</summary>
[Collection("postgres")]
public sealed class CandidateReadinessQueryTests(PostgresFixture fixture)
{
    /// <summary>Verifies one original no-show and one concluded recovery completion are projected.</summary>
    [Fact]
    public async Task SnapshotProjectsOriginalAndRecoveryAttempts()
    {
        await fixture.ResetAsync();
        var staff = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 9, 7, 9, 0, 0, TimeSpan.Zero);
        var slotId = Guid.NewGuid();
        var recoverySlotId = Guid.NewGuid();

        Guid candidateId;
        Guid originalId;
        await using (var write = fixture.NewContext())
        {
            var groundOps = write.EmployeeGroups
                .Include(g => g.Requirements)
                .Single(g => g.Id == EmployeeGroupIds.GroundOperationsAgent);
            var candidate = Candidate.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", groundOps);
            candidateId = candidate.Id;
            write.Candidates.Add(candidate);

            var initial = Invite.CreateInitial(
                Guid.NewGuid(), candidate.Id, "initial", now.AddDays(1),
                [slotId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
            var original = Booking.Create(Guid.NewGuid(), initial, slotId, "manage-original", now);
            originalId = original.Id;
            write.Bookings.Add(original);
            var originalAttempt = BookingAppointment.Create(
                Guid.NewGuid(), original.Id, AppointmentTypeIds.MedicalCheckUp);
            originalAttempt.TransitionTo(BookingAppointmentStatus.NoShow, staff, now, false, true);
            write.BookingAppointments.Add(originalAttempt);

            var recoveryInvite = Invite.CreateRecovery(
                Guid.NewGuid(), candidate.Id, original.Id, "recovery", now.AddDays(2),
                [recoverySlotId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);
            var recovery = Booking.CreateRecovery(
                Guid.NewGuid(), recoveryInvite, original, recoverySlotId, "manage-recovery", now.AddHours(1));
            recovery.Conclude();
            write.Bookings.Add(recovery);
            var recoveryAttempt = BookingAppointment.Create(
                Guid.NewGuid(), recovery.Id, AppointmentTypeIds.MedicalCheckUp);
            recoveryAttempt.TransitionTo(BookingAppointmentStatus.CheckedIn, staff, now, true, false);
            recoveryAttempt.TransitionTo(BookingAppointmentStatus.Completed, staff, now, true, false);
            write.BookingAppointments.Add(recoveryAttempt);

            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var snapshot = await new CandidateReadinessQueries(read)
            .GetSnapshotAsync(candidateId, CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Equal(candidateId, snapshot!.CandidateId);
        Assert.Equal(EmployeeGroupIds.GroundOperationsAgent, snapshot.EmployeeGroupId);
        Assert.Equal([AppointmentTypeIds.MedicalCheckUp], snapshot.CurrentRequirementTypeIds);
        Assert.Equal(originalId, snapshot.ActiveOriginalBookingId);
        Assert.Equal(2, snapshot.Attempts.Count);
        Assert.Contains(
            snapshot.Attempts,
            attempt => attempt.BookingId == originalId
                && attempt.Status == BookingAppointmentStatus.NoShow
                && attempt.BookingStatus == BookingStatus.Active);
        Assert.Contains(
            snapshot.Attempts,
            attempt => attempt.BookingId != originalId
                && attempt.Status == BookingAppointmentStatus.Completed
                && attempt.BookingStatus == BookingStatus.Concluded);
    }

    /// <summary>Verifies an unknown candidate projects no snapshot.</summary>
    [Fact]
    public async Task UnknownCandidateReturnsNull()
    {
        await fixture.ResetAsync();

        await using var read = fixture.NewContext();
        var snapshot = await new CandidateReadinessQueries(read)
            .GetSnapshotAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(snapshot);
    }
}
