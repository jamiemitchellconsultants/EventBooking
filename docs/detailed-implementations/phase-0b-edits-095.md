# 00b — Vocabulary edits 95 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — tests/EventBooking.Infrastructure.Tests/AttendeeBookingCancellationPersistenceTests.cs — 1/1

<!-- vocabulary-file: {"id":327,"oldPath":"tests/EventBooking.Infrastructure.Tests/CandidateBookingCancellationPersistenceTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/AttendeeBookingCancellationPersistenceTests.cs","beforeSha":"74073de631f622b4f878667d6f01af2c50fe545547d8a7391bb1342a95a1e18a","afterSha":"f4186ec3fe2b369eeb65759c4e2b4cfbe21d49ff75899d213857df84b8c4309a","side":"after","part":1,"parts":1} -->

`````csharp
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
`````

## before — tests/EventBooking.Infrastructure.Tests/CandidateBookingQueryTests.cs — 1/1

<!-- vocabulary-file: {"id":328,"oldPath":"tests/EventBooking.Infrastructure.Tests/CandidateBookingQueryTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/AttendeeBookingQueryTests.cs","beforeSha":"2ef7942c48aa546f9e5c5bf14a3afbc43fbe65e673078192e4b4aaf9886e0ca1","afterSha":"00d311b15be456ac93a6687c40288d13b2c5fc53e855ef2699659b8200bc7c95","side":"before","part":1,"parts":1} -->

`````csharp
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
`````

## after — tests/EventBooking.Infrastructure.Tests/AttendeeBookingQueryTests.cs — 1/1

<!-- vocabulary-file: {"id":328,"oldPath":"tests/EventBooking.Infrastructure.Tests/CandidateBookingQueryTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/AttendeeBookingQueryTests.cs","beforeSha":"2ef7942c48aa546f9e5c5bf14a3afbc43fbe65e673078192e4b4aaf9886e0ca1","afterSha":"00d311b15be456ac93a6687c40288d13b2c5fc53e855ef2699659b8200bc7c95","side":"after","part":1,"parts":1} -->

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

## before — tests/EventBooking.Infrastructure.Tests/CandidateReadinessQueryTests.cs — 1/1

<!-- vocabulary-file: {"id":329,"oldPath":"tests/EventBooking.Infrastructure.Tests/CandidateReadinessQueryTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/AttendeeReadinessQueryTests.cs","beforeSha":"acaffee1a885fef3636a37298cb2e9df4de1145449aaaa84c763325efdf62a19","afterSha":"8d310c51ea5e9334ede4bb451c6022f9468461c4919d0bc8e191d55b42336cca","side":"before","part":1,"parts":1} -->

`````csharp
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
`````

## after — tests/EventBooking.Infrastructure.Tests/AttendeeReadinessQueryTests.cs — 1/1

<!-- vocabulary-file: {"id":329,"oldPath":"tests/EventBooking.Infrastructure.Tests/CandidateReadinessQueryTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/AttendeeReadinessQueryTests.cs","beforeSha":"acaffee1a885fef3636a37298cb2e9df4de1145449aaaa84c763325efdf62a19","afterSha":"8d310c51ea5e9334ede4bb451c6022f9468461c4919d0bc8e191d55b42336cca","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies the readiness journey projection across original and recovery bookings.</summary>
[Collection("postgres")]
public sealed class AttendeeReadinessQueryTests(PostgresFixture fixture)
{
    /// <summary>Verifies one original no-show and one concluded recovery completion are projected.</summary>
    [Fact]
    public async Task SnapshotProjectsOriginalAndRecoveryAttempts()
    {
        await fixture.ResetAsync();
        var staff = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 9, 7, 9, 0, 0, TimeSpan.Zero);
        var eventId = Guid.NewGuid();
        var recoveryEventId = Guid.NewGuid();

        Guid attendeeId;
        Guid originalId;
        await using (var write = fixture.NewContext())
        {
            var groundOps = write.AttendeeGroups
                .Include(g => g.Requirements)
                .Single(g => g.Id == AttendeeGroupIds.GroundOperationsAgent);
            var attendee = Attendee.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", groundOps);
            attendeeId = attendee.Id;
            write.Attendees.Add(attendee);

            var initial = Invite.CreateInitial(
                Guid.NewGuid(), attendee.Id, "initial", now.AddDays(1),
                [eventId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
            var original = Booking.Create(Guid.NewGuid(), initial, eventId, "manage-original", now);
            originalId = original.Id;
            write.Bookings.Add(original);
            var originalAttempt = BookingAppointment.Create(
                Guid.NewGuid(), original.Id, AppointmentTypeIds.MedicalCheckUp);
            originalAttempt.TransitionTo(BookingAppointmentStatus.NoShow, staff, now, false, true);
            write.BookingAppointments.Add(originalAttempt);

            var recoveryInvite = Invite.CreateRecovery(
                Guid.NewGuid(), attendee.Id, original.Id, "recovery", now.AddDays(2),
                [recoveryEventId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);
            var recovery = Booking.CreateRecovery(
                Guid.NewGuid(), recoveryInvite, original, recoveryEventId, "manage-recovery", now.AddHours(1));
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
        var snapshot = await new AttendeeReadinessQueries(read)
            .GetSnapshotAsync(attendeeId, CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Equal(attendeeId, snapshot!.AttendeeId);
        Assert.Equal(AttendeeGroupIds.GroundOperationsAgent, snapshot.AttendeeGroupId);
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

    /// <summary>Verifies an unknown attendee projects no snapshot.</summary>
    [Fact]
    public async Task UnknownAttendeeReturnsNull()
    {
        await fixture.ResetAsync();

        await using var read = fixture.NewContext();
        var snapshot = await new AttendeeReadinessQueries(read)
            .GetSnapshotAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(snapshot);
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyHarness.cs — 1/1

<!-- vocabulary-file: {"id":330,"oldPath":"tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyHarness.cs","newPath":"tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyHarness.cs","beforeSha":"8310026b45005eef723383b6613c245d161fc799302b263e4f1673a1d8c4c62a","afterSha":"983bda091b89de0706a6bb4c87b46d4277389236459af15ec0489f0b0032c336","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Slots;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EventBooking.Infrastructure.Tests;

public sealed record CapacitySnapshot(int TotalHeadcount, int RemainingCapacity);

public sealed class CapacityAdjustmentConcurrencyHarness : IAsyncDisposable
{
    private readonly PostgresFixture _fixture;

    private CapacityAdjustmentConcurrencyHarness(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    public static async Task<CapacityAdjustmentConcurrencyHarness> CreateAsync(
        PostgresFixture fixture)
    {
        await fixture.ResetAsync();
        return new CapacityAdjustmentConcurrencyHarness(fixture);
    }

    public async Task<Guid> GivenSlotAsync(int totalHeadcount)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), totalHeadcount);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 20);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 20);
        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);

        await using var context = _fixture.NewContext();
        context.SlotProposals.Add(proposal);
        context.ConfirmedSlots.Add(slot);
        await context.SaveChangesAsync();
        return slot.Id;
    }

    public async Task<HeldCapacityChange> HoldAdjustmentAsync(
        Guid slotId,
        int totalHeadcount)
    {
        var context = _fixture.NewContext();
        var transaction = await context.Database.BeginTransactionAsync();
        var capacity = await LockAsync(context, slotId);
        capacity.AdjustTotalHeadcount(totalHeadcount);
        await context.SaveChangesAsync();
        return new HeldCapacityChange(context, transaction);
    }

    public async Task<HeldCapacityChange> HoldBookingAsync(Guid slotId)
    {
        var context = _fixture.NewContext();
        var transaction = await context.Database.BeginTransactionAsync();
        var capacity = await LockAsync(context, slotId);
        capacity.Decrement();
        await context.SaveChangesAsync();
        return new HeldCapacityChange(context, transaction);
    }

    public async Task BookAsync(Guid slotId)
    {
        await using var context = _fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var capacity = await LockAsync(context, slotId);
        capacity.Decrement();
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task AdjustAsync(Guid slotId, int totalHeadcount)
    {
        await using var context = _fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var capacity = await LockAsync(context, slotId);
        capacity.AdjustTotalHeadcount(totalHeadcount);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task<CapacitySnapshot> ReadAsync(Guid slotId)
    {
        await using var context = _fixture.NewContext();
        var capacity = await context.SlotCapacities.SingleAsync(
            item => item.ConfirmedSlotId == slotId
                && item.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting);
        return new CapacitySnapshot(capacity.TotalHeadcount, capacity.RemainingCapacity);
    }

    private static async Task<SlotCapacity> LockAsync(
        EventBookingDbContext context,
        Guid slotId)
    {
        var rows = await new SlotCapacityRepository(context).LockForUpdateAsync(
            slotId,
            [AppointmentTypeIds.DrugAndAlcoholTesting],
            CancellationToken.None);
        return Assert.Single(rows);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class HeldCapacityChange(
    EventBookingDbContext context,
    IDbContextTransaction transaction) : IAsyncDisposable
{
    private bool _committed;

    public async Task CommitAsync()
    {
        await transaction.CommitAsync();
        _committed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_committed)
        {
            await transaction.RollbackAsync();
        }

        await transaction.DisposeAsync();
        await context.DisposeAsync();
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyHarness.cs — 1/1

<!-- vocabulary-file: {"id":330,"oldPath":"tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyHarness.cs","newPath":"tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyHarness.cs","beforeSha":"8310026b45005eef723383b6613c245d161fc799302b263e4f1673a1d8c4c62a","afterSha":"983bda091b89de0706a6bb4c87b46d4277389236459af15ec0489f0b0032c336","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EventBooking.Infrastructure.Tests;

public sealed record CapacitySnapshot(int TotalHeadcount, int RemainingCapacity);

public sealed class CapacityAdjustmentConcurrencyHarness : IAsyncDisposable
{
    private readonly PostgresFixture _fixture;

    private CapacityAdjustmentConcurrencyHarness(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    public static async Task<CapacityAdjustmentConcurrencyHarness> CreateAsync(
        PostgresFixture fixture)
    {
        await fixture.ResetAsync();
        return new CapacityAdjustmentConcurrencyHarness(fixture);
    }

    public async Task<Guid> GivenEventAsync(int totalHeadcount)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), totalHeadcount);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 20);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 20);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        await using var context = _fixture.NewContext();
        context.EventProposals.Add(proposal);
        context.Events.Add(eventItem);
        await context.SaveChangesAsync();
        return eventItem.Id;
    }

    public async Task<HeldCapacityChange> HoldAdjustmentAsync(
        Guid eventId,
        int totalHeadcount)
    {
        var context = _fixture.NewContext();
        var transaction = await context.Database.BeginTransactionAsync();
        var capacity = await LockAsync(context, eventId);
        capacity.AdjustTotalHeadcount(totalHeadcount);
        await context.SaveChangesAsync();
        return new HeldCapacityChange(context, transaction);
    }

    public async Task<HeldCapacityChange> HoldBookingAsync(Guid eventId)
    {
        var context = _fixture.NewContext();
        var transaction = await context.Database.BeginTransactionAsync();
        var capacity = await LockAsync(context, eventId);
        capacity.Decrement();
        await context.SaveChangesAsync();
        return new HeldCapacityChange(context, transaction);
    }

    public async Task BookAsync(Guid eventId)
    {
        await using var context = _fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var capacity = await LockAsync(context, eventId);
        capacity.Decrement();
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task AdjustAsync(Guid eventId, int totalHeadcount)
    {
        await using var context = _fixture.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var capacity = await LockAsync(context, eventId);
        capacity.AdjustTotalHeadcount(totalHeadcount);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task<CapacitySnapshot> ReadAsync(Guid eventId)
    {
        await using var context = _fixture.NewContext();
        var capacity = await context.EventCapacities.SingleAsync(
            item => item.EventId == eventId
                && item.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting);
        return new CapacitySnapshot(capacity.TotalHeadcount, capacity.RemainingCapacity);
    }

    private static async Task<EventCapacity> LockAsync(
        EventBookingDbContext context,
        Guid eventId)
    {
        var rows = await new EventCapacityRepository(context).LockForUpdateAsync(
            eventId,
            [AppointmentTypeIds.DrugAndAlcoholTesting],
            CancellationToken.None);
        return Assert.Single(rows);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class HeldCapacityChange(
    EventBookingDbContext context,
    IDbContextTransaction transaction) : IAsyncDisposable
{
    private bool _committed;

    public async Task CommitAsync()
    {
        await transaction.CommitAsync();
        _committed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_committed)
        {
            await transaction.RollbackAsync();
        }

        await transaction.DisposeAsync();
        await context.DisposeAsync();
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyTests.cs — 1/1

<!-- vocabulary-file: {"id":331,"oldPath":"tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyTests.cs","beforeSha":"d479b2762894ee9520402dc2b60bca0a4ddd750a7ca147277c09f0a157f5726a","afterSha":"d09a4e6a5c96388c7cf26b4c41f156613a81abb98232c832fa43098c8a373bce","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class CapacityAdjustmentConcurrencyTests(PostgresFixture fixture)
{
    [Fact]
    public async Task BookingWaitsForAnAdjustmentAndUsesTheNewCapacity()
    {
        await using var harness =
            await CapacityAdjustmentConcurrencyHarness.CreateAsync(fixture);
        var slotId = await harness.GivenSlotAsync(totalHeadcount: 1);
        await using var held = await harness.HoldAdjustmentAsync(slotId, totalHeadcount: 2);

        var booking = harness.BookAsync(slotId);
        await AssertStillWaiting(booking);

        await held.CommitAsync();
        await booking.WaitAsync(TimeSpan.FromSeconds(5));

        var capacity = await harness.ReadAsync(slotId);
        Assert.Equal(2, capacity.TotalHeadcount);
        Assert.Equal(1, capacity.RemainingCapacity);
        Assert.InRange(capacity.RemainingCapacity, 0, capacity.TotalHeadcount);
    }

    [Fact]
    public async Task AdjustmentWaitsForABookingAndPreservesItsHold()
    {
        await using var harness =
            await CapacityAdjustmentConcurrencyHarness.CreateAsync(fixture);
        var slotId = await harness.GivenSlotAsync(totalHeadcount: 2);
        await using var held = await harness.HoldBookingAsync(slotId);

        var adjustment = harness.AdjustAsync(slotId, totalHeadcount: 1);
        await AssertStillWaiting(adjustment);

        await held.CommitAsync();
        await adjustment.WaitAsync(TimeSpan.FromSeconds(5));

        var capacity = await harness.ReadAsync(slotId);
        Assert.Equal(1, capacity.TotalHeadcount);
        Assert.Equal(0, capacity.RemainingCapacity);
        Assert.InRange(capacity.RemainingCapacity, 0, capacity.TotalHeadcount);
    }

    private static async Task AssertStillWaiting(Task operation)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(150));
        Assert.False(operation.IsCompleted);
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyTests.cs — 1/1

<!-- vocabulary-file: {"id":331,"oldPath":"tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyTests.cs","beforeSha":"d479b2762894ee9520402dc2b60bca0a4ddd750a7ca147277c09f0a157f5726a","afterSha":"d09a4e6a5c96388c7cf26b4c41f156613a81abb98232c832fa43098c8a373bce","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class CapacityAdjustmentConcurrencyTests(PostgresFixture fixture)
{
    [Fact]
    public async Task BookingWaitsForAnAdjustmentAndUsesTheNewCapacity()
    {
        await using var harness =
            await CapacityAdjustmentConcurrencyHarness.CreateAsync(fixture);
        var eventId = await harness.GivenEventAsync(totalHeadcount: 1);
        await using var held = await harness.HoldAdjustmentAsync(eventId, totalHeadcount: 2);

        var booking = harness.BookAsync(eventId);
        await AssertStillWaiting(booking);

        await held.CommitAsync();
        await booking.WaitAsync(TimeSpan.FromSeconds(5));

        var capacity = await harness.ReadAsync(eventId);
        Assert.Equal(2, capacity.TotalHeadcount);
        Assert.Equal(1, capacity.RemainingCapacity);
        Assert.InRange(capacity.RemainingCapacity, 0, capacity.TotalHeadcount);
    }

    [Fact]
    public async Task AdjustmentWaitsForABookingAndPreservesItsHold()
    {
        await using var harness =
            await CapacityAdjustmentConcurrencyHarness.CreateAsync(fixture);
        var eventId = await harness.GivenEventAsync(totalHeadcount: 2);
        await using var held = await harness.HoldBookingAsync(eventId);

        var adjustment = harness.AdjustAsync(eventId, totalHeadcount: 1);
        await AssertStillWaiting(adjustment);

        await held.CommitAsync();
        await adjustment.WaitAsync(TimeSpan.FromSeconds(5));

        var capacity = await harness.ReadAsync(eventId);
        Assert.Equal(1, capacity.TotalHeadcount);
        Assert.Equal(0, capacity.RemainingCapacity);
        Assert.InRange(capacity.RemainingCapacity, 0, capacity.TotalHeadcount);
    }

    private static async Task AssertStillWaiting(Task operation)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(150));
        Assert.False(operation.IsCompleted);
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/ConcurrencyHarness.cs — 1/1

<!-- vocabulary-file: {"id":332,"oldPath":"tests/EventBooking.Infrastructure.Tests/ConcurrencyHarness.cs","newPath":"tests/EventBooking.Infrastructure.Tests/ConcurrencyHarness.cs","beforeSha":"113d8ce953d39304aa1d62a154da47962e946fdd2a19d5afa2380d1283efdb05","afterSha":"3c9bd740071a16b6737c13720746957725aa9fa7562a2cecdfa10bcee323bfd3","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Common;
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Repositories;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NpgsqlTypes;

namespace EventBooking.Infrastructure.Tests;

/// <summary>
/// A real service provider over a real database, with the mail transport stubbed out. Every
/// confirmation runs in its own scope so that concurrent attempts use separate connections.
/// </summary>
public sealed class ConcurrencyHarness : IAsyncDisposable
{
    private static readonly TimeSpan ReadinessTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan CompletionTimeout = TimeSpan.FromSeconds(30);

    private readonly PostgresFixture _fixture;
    private readonly ServiceProvider _services;
    private IReadOnlyList<Guid> _fallbackSlotIds = [];
    private int _nextSlotOffset;

    private ConcurrencyHarness(PostgresFixture fixture, ServiceProvider services)
    {
        _fixture = fixture;
        _services = services;
    }

    /// <summary>Sends nothing. Email delivery is not what this task is testing.</summary>
    private sealed class SilentTransport : IEmailTransport
    {
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    /// <summary>
    /// The two genuine high-capacity options offered beside every slot under contention. They are
    /// deliberately kept alive so a losing invite remains valid while the proof runs.
    /// </summary>
    public IReadOnlyList<Guid> FallbackSlotIds => _fallbackSlotIds;

    public static async Task<ConcurrencyHarness> CreateAsync(PostgresFixture fixture)
    {
        await fixture.ResetAsync();

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddEventBookingInfrastructure(
            fixture.ConnectionString,
            new HeadOfficeOptions("Europe/London"),
            new TokenOptions("a-concurrency-test-signing-key-long-enough"));

        services.AddEventBookingApplication(
            new CandidatePortalOptions("https://booking.example.com", "HQ", "recruitment@example.com"));

        // Nothing registers IEmailTransport above any more — AddEventBookingInfrastructure no
        // longer does that itself, and this harness never calls AddAwsEmailTransport or
        // AddLocalEmailTransport, since email delivery is not what this proof is testing.
        services.AddScoped<IEmailTransport, SilentTransport>();

        var harness = new ConcurrencyHarness(fixture, services.BuildServiceProvider());
        harness._fallbackSlotIds =
        [
            await harness.GivenSlotAsync(drugAndAlcohol: 100, medical: 100, uniform: 100),
            await harness.GivenSlotAsync(drugAndAlcohol: 100, medical: 100, uniform: 100),
        ];

        return harness;
    }

    public async Task<Guid> GivenSlotAsync(int drugAndAlcohol, int medical, int uniform)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(
                DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30 + Interlocked.Increment(ref _nextSlotOffset)),
                new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), drugAndAlcohol);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), medical);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), uniform);

        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);

        await using var context = _fixture.NewContext();
        context.SlotProposals.Add(proposal);
        context.ConfirmedSlots.Add(slot);
        await context.SaveChangesAsync();

        return slot.Id;
    }

    /// <summary>Opens an independent scope with its own connection for one racer.</summary>
    public IServiceScope CreateScope() => _services.CreateScope();

    public async Task<string> GivenInvitedCandidateAsync(Guid slotId, params Guid[] requiredTypeIds)
    {
        var tokens = _services.GetRequiredService<ITokenService>();

        var group = EmployeeGroup.Define(
            Guid.NewGuid(), $"HARNESS_{Guid.NewGuid():N}".ToUpperInvariant(), "Harness", true,
            requiredTypeIds);
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Concurrent Candidate", $"{Guid.NewGuid():N}@mail.com", group);
        candidate.MarkInvited();

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(inviteId);

        // Every invite is real: the fallback slots remain valid options if the contested one fills.
        var invite = Invite.CreateInitial(
            inviteId,
            candidate.Id,
            issued.TokenHash,
            DateTimeOffset.UtcNow.AddDays(4),
            [slotId, .. _fallbackSlotIds],
            requiredTypeIds,
            0);

        await using var context = _fixture.NewContext();
        context.EmployeeGroups.Add(group);
        context.Candidates.Add(candidate);
        context.Invites.Add(invite);
        await context.SaveChangesAsync();

        return issued.Token;
    }

    public async Task<Result<ConfirmBookingOutcome>> ConfirmAsync(string token, Guid slotId)
    {
        // A scope per attempt: separate context, separate connection, separate transaction.
        await using var scope = _services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ConfirmBookingHandler>();

        return await handler.HandleAsync(
            new ConfirmBookingCommand(token, slotId), CancellationToken.None);
    }

    /// <summary>
    /// Starts every confirmation only after a real external transaction has acquired the target
    /// slot's row lock. Every production handler consequently waits on PostgreSQL before the
    /// guard commits, proving that the work overlaps rather than being merely scheduled together.
    /// </summary>
    public async Task<ConfirmationBatch> ConfirmBatchAsync(
        IReadOnlyCollection<string> tokens,
        Guid slotId)
    {
        var tokenList = tokens.ToArray();
        if (tokenList.Length == 0)
        {
            throw new ArgumentException("At least one confirmation is required.", nameof(tokens));
        }

        await using var guardContext = _fixture.NewContext();
        await using var guardTransaction = await guardContext.Database.BeginTransactionAsync();
        var lockedSlot = await new ConfirmedSlotRepository(guardContext)
            .LockForUpdateAsync(slotId, CancellationToken.None);
        if (lockedSlot is null)
        {
            throw new InvalidOperationException("The batch target slot does not exist.");
        }

        var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readiness = tokenList
            .Select(_ => new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously))
            .ToArray();
        var attempts = tokenList
            .Select((token, index) => ConfirmAfterGateAsync(token, slotId, startGate.Task, readiness[index]))
            .ToArray();

        var guardCommitted = false;
        try
        {
            var backendPids = await Task.WhenAll(readiness.Select(ready => ready.Task))
                .WaitAsync(ReadinessTimeout);
            if (backendPids.Distinct().Count() != tokenList.Length)
            {
                throw new InvalidOperationException("Each confirmation must hold its own PostgreSQL connection.");
            }

            startGate.SetResult();
            var blockedAttemptCount = await WaitUntilAllBlockedOnDatabaseLockAsync(
                guardContext,
                backendPids);

            await guardTransaction.CommitAsync();
            guardCommitted = true;
            var results = await Task.WhenAll(attempts).WaitAsync(CompletionTimeout);

            return new ConfirmationBatch(results, blockedAttemptCount);
        }
        catch
        {
            startGate.TrySetResult();
            if (!guardCommitted)
            {
                await guardTransaction.RollbackAsync();
            }

            await Task.WhenAll(attempts).WaitAsync(CompletionTimeout);
            throw;
        }
    }

    /// <summary>
    /// Reloads a pending invite by its raw token and verifies that all three option IDs identify
    /// active slots with spare capacity for the candidate's required appointment types.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> LiveOptionSlotIdsAsync(string token)
    {
        var tokenHash = _services.GetRequiredService<ITokenService>().Hash(token);

        await using var context = _fixture.NewContext();
        var invite = await context.Invites
            .Include(i => i.Options)
            .SingleAsync(i => i.TokenHash == tokenHash);
        if (invite.Status != InviteStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending invite can retain live options.");
        }

        var optionIds = invite.OfferedSlotIds;
        if (optionIds.Count != Invite.RequiredOptionCount || optionIds.Distinct().Count() != optionIds.Count)
        {
            throw new InvalidOperationException("A live invite must retain three distinct options.");
        }

        var candidate = await context.Candidates
            .Include(c => c.Requirements)
            .SingleAsync(c => c.Id == invite.CandidateId);
        var slots = await context.ConfirmedSlots
            .Include(s => s.Capacities)
            .Where(s => optionIds.Contains(s.Id))
            .ToListAsync();

        if (slots.Count != optionIds.Count
            || slots.Any(slot => slot.Status != ConfirmedSlotStatus.Active)
            || slots.Any(slot => !slot.HasSpareCapacityForAll(candidate.RequiredAppointmentTypeIds)))
        {
            throw new InvalidOperationException("Every invite option must be a live eligible slot.");
        }

        return optionIds;
    }

    public async Task<int> RemainingCapacityAsync(Guid slotId, Guid appointmentTypeId)
    {
        await using var context = _fixture.NewContext();
        var slot = await context.ConfirmedSlots
            .Include(s => s.Capacities)
            .SingleAsync(s => s.Id == slotId);

        return slot.CapacityFor(appointmentTypeId).RemainingCapacity;
    }

    public async Task<int> ActiveBookingCountAsync(Guid slotId)
    {
        await using var context = _fixture.NewContext();
        return await context.Bookings.CountAsync(
            b => b.ConfirmedSlotId == slotId && b.Status == BookingStatus.Active);
    }

    private async Task<Result<ConfirmBookingOutcome>> ConfirmAfterGateAsync(
        string token,
        Guid slotId,
        Task startGate,
        TaskCompletionSource<int> ready)
    {
        await using var scope = _services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        await context.Database.OpenConnectionAsync();

        try
        {
            ready.SetResult(await GetBackendPidAsync(context));
            await startGate;

            var handler = scope.ServiceProvider.GetRequiredService<ConfirmBookingHandler>();
            return await handler.HandleAsync(
                new ConfirmBookingCommand(token, slotId), CancellationToken.None);
        }
        catch (Exception exception)
        {
            ready.TrySetException(exception);
            throw;
        }
    }

    private static async Task<int> GetBackendPidAsync(EventBookingDbContext context)
    {
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_backend_pid();";
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<int> WaitUntilAllBlockedOnDatabaseLockAsync(
        EventBookingDbContext lockOwner,
        IReadOnlyCollection<int> backendPids)
    {
        var connection = (NpgsqlConnection)lockOwner.Database.GetDbConnection();

        for (var attempt = 0; attempt < 200; attempt++)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (NpgsqlTransaction)lockOwner.Database.CurrentTransaction!
                .GetDbTransaction();
            command.CommandText =
                "SELECT COUNT(*) FROM pg_stat_activity WHERE pid = ANY(@backend_pids) AND wait_event_type = 'Lock';";
            command.Parameters.AddWithValue(
                "backend_pids",
                NpgsqlDbType.Array | NpgsqlDbType.Integer,
                backendPids.ToArray());

            var blockedCount = Convert.ToInt32(await command.ExecuteScalarAsync());
            if (blockedCount == backendPids.Count)
            {
                return blockedCount;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException(
            $"Only a subset of the {backendPids.Count} confirmation connections waited on PostgreSQL's row lock.");
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();
}

public sealed record ConfirmationBatch(
    IReadOnlyList<Result<ConfirmBookingOutcome>> Results,
    int BlockedAttemptCount);
`````
