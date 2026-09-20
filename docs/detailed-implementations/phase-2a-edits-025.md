# 02a — Deterministic attendee links and the token version counter, edits 25 (Task 9a)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — tests/EventBooking.Infrastructure.Tests/AttendeeBookingCancellationPersistenceTests.cs — 1/1

<!-- retirement-file: {"id":62,"file":"tests/EventBooking.Infrastructure.Tests/AttendeeBookingCancellationPersistenceTests.cs","beforeSha":"47c3dd0ac87fb019dcd012d98e1a000dc92068c0c826e699f5780d49c82ed295","afterSha":"efeb77f2eb02e5e3a016118b1fee37e8f7969e1e27784ed8522828eb82ba1537","side":"before","part":1,"parts":1} -->

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
            Guid.NewGuid(),
            attendeeId,
            $"initial-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp],
            0);
        return Booking.Create(
            Guid.NewGuid(), invite, eventId, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
    }

    private static Booking RecoveryFor(Guid attendeeId, Booking original, DateTimeOffset createdAt)
    {
        var eventId = Guid.NewGuid();
        var invite = Invite.CreateRecovery(
            Guid.NewGuid(),
            attendeeId,
            original.Id,
            $"recovery-{Guid.NewGuid():N}",
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

## after — tests/EventBooking.Infrastructure.Tests/AttendeeBookingCancellationPersistenceTests.cs — 1/1

<!-- retirement-file: {"id":62,"file":"tests/EventBooking.Infrastructure.Tests/AttendeeBookingCancellationPersistenceTests.cs","beforeSha":"47c3dd0ac87fb019dcd012d98e1a000dc92068c0c826e699f5780d49c82ed295","afterSha":"efeb77f2eb02e5e3a016118b1fee37e8f7969e1e27784ed8522828eb82ba1537","side":"after","part":1,"parts":1} -->

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

    private static Booking RecoveryFor(Guid attendeeId, Booking original, DateTimeOffset createdAt)
    {
        var eventId = Guid.NewGuid();
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
            Guid.NewGuid(), invite, original, eventId, createdAt);
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/AttendeeBookingQueryTests.cs — 1/1

<!-- retirement-file: {"id":63,"file":"tests/EventBooking.Infrastructure.Tests/AttendeeBookingQueryTests.cs","beforeSha":"f4be42f1bd132e5cd143b57a31f1ba9e195084def2608f59c69d706717f21a9b","afterSha":"ed71bb9d3d3ec46101561d23284aeaf5e3872a35efdda9cfb772fef6ae85f5a1","side":"before","part":1,"parts":1} -->

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
            $"initial-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp],
            0);
        return Booking.Create(
            Guid.NewGuid(), invite, eventId, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
    }

    private static Booking RecoveryFor(Guid attendeeId, Booking original, Guid eventId)
    {
        var invite = Invite.CreateRecovery(
            Guid.NewGuid(),
            attendeeId,
            original.Id,
            $"recovery-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(2),
            ProposalFixture.LocationId,
            null,
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp]);
        return Booking.CreateRecovery(
            Guid.NewGuid(), invite, original, eventId, $"manage-recovery-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddHours(1));
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/AttendeeBookingQueryTests.cs — 1/1

<!-- retirement-file: {"id":63,"file":"tests/EventBooking.Infrastructure.Tests/AttendeeBookingQueryTests.cs","beforeSha":"f4be42f1bd132e5cd143b57a31f1ba9e195084def2608f59c69d706717f21a9b","afterSha":"ed71bb9d3d3ec46101561d23284aeaf5e3872a35efdda9cfb772fef6ae85f5a1","side":"after","part":1,"parts":1} -->

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
`````

## before — tests/EventBooking.Infrastructure.Tests/AttendeeReadinessQueryTests.cs — 1/1

<!-- retirement-file: {"id":64,"file":"tests/EventBooking.Infrastructure.Tests/AttendeeReadinessQueryTests.cs","beforeSha":"d09c37fb7fba23ce2a9d78c4684a830bdc6ec623eafb593ffa4409ca4f03702b","afterSha":"46664f43dccdc97ce9420d05087886cd2617148759572d7a202edae3ec85a193","side":"before","part":1,"parts":1} -->

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
            var attendee = Attendee.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", groundOps, ProposalFixture.Now);
            attendeeId = attendee.Id;
            write.Attendees.Add(attendee);

            var initial = Invite.CreateInitial(
                Guid.NewGuid(),
                attendee.Id,
                "initial",
                now.AddDays(1),
                [ProposalFixture.LocationId],
                [eventId, Guid.NewGuid(), Guid.NewGuid()],
                [AppointmentTypeIds.MedicalCheckUp],
                0);
            var original = Booking.Create(Guid.NewGuid(), initial, eventId, "manage-original", now);
            originalId = original.Id;
            write.Bookings.Add(original);
            var originalAttempt = BookingAppointment.Create(
                Guid.NewGuid(), original.Id, AppointmentTypeIds.MedicalCheckUp);
            originalAttempt.TransitionTo(BookingAppointmentStatus.NoShow, staff, now, false, true);
            write.BookingAppointments.Add(originalAttempt);

            var recoveryInvite = Invite.CreateRecovery(
                Guid.NewGuid(),
                attendee.Id,
                original.Id,
                "recovery",
                now.AddDays(2),
                ProposalFixture.LocationId,
                null,
                [recoveryEventId, Guid.NewGuid(), Guid.NewGuid()],
                [AppointmentTypeIds.MedicalCheckUp]);
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

## after — tests/EventBooking.Infrastructure.Tests/AttendeeReadinessQueryTests.cs — 1/1

<!-- retirement-file: {"id":64,"file":"tests/EventBooking.Infrastructure.Tests/AttendeeReadinessQueryTests.cs","beforeSha":"d09c37fb7fba23ce2a9d78c4684a830bdc6ec623eafb593ffa4409ca4f03702b","afterSha":"46664f43dccdc97ce9420d05087886cd2617148759572d7a202edae3ec85a193","side":"after","part":1,"parts":1} -->

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
            var attendee = Attendee.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", groundOps, ProposalFixture.Now);
            attendeeId = attendee.Id;
            write.Attendees.Add(attendee);

            var initial = Invite.CreateInitial(
                Guid.NewGuid(),
                attendee.Id,
                now.AddDays(1),
                [ProposalFixture.LocationId],
                [eventId, Guid.NewGuid(), Guid.NewGuid()],
                [AppointmentTypeIds.MedicalCheckUp],
                0);
            var original = Booking.Create(Guid.NewGuid(), initial, eventId, now);
            originalId = original.Id;
            write.Bookings.Add(original);
            var originalAttempt = BookingAppointment.Create(
                Guid.NewGuid(), original.Id, AppointmentTypeIds.MedicalCheckUp);
            originalAttempt.TransitionTo(BookingAppointmentStatus.NoShow, staff, now, false, true);
            write.BookingAppointments.Add(originalAttempt);

            var recoveryInvite = Invite.CreateRecovery(
                Guid.NewGuid(),
                attendee.Id,
                original.Id,
                now.AddDays(2),
                ProposalFixture.LocationId,
                null,
                [recoveryEventId, Guid.NewGuid(), Guid.NewGuid()],
                [AppointmentTypeIds.MedicalCheckUp]);
            var recovery = Booking.CreateRecovery(
                Guid.NewGuid(), recoveryInvite, original, recoveryEventId, now.AddHours(1));
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

## before — tests/EventBooking.Infrastructure.Tests/AuditQueryTests.cs — 1/1

<!-- retirement-file: {"id":65,"file":"tests/EventBooking.Infrastructure.Tests/AuditQueryTests.cs","beforeSha":"751e15a8a699cebd33494b9eeac79865c3a1f7ffb76251977591b744242c8efa","afterSha":"7b05ccdc250af241389a9af3b94bbf262974b02136d324e9fb1df4f285bb2710","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class AuditQueryTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AEventsHistoryComesBackNewestFirst()
    {
        await fixture.ResetAsync();
        var eventId = Guid.NewGuid();

        await using (var write = fixture.NewContext())
        {
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Event, eventId, AuditAction.EventConfirmed,
                ActorType.Staff, "staff-1", Now, null));
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Event, eventId, AuditAction.EventCancelled,
                ActorType.Staff, "staff-2", Now.AddHours(1), "6 bookings voided"));
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Event, Guid.NewGuid(), AuditAction.EventConfirmed,
                ActorType.Staff, "staff-3", Now, null));
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var rows = await new AuditQueries(read).ForEntityAsync(
            AuditEntityTypes.Event, eventId, CancellationToken.None);

        Assert.Equal(2, rows.Count);
        Assert.Equal("EventCancelled", rows[0].Action);
        Assert.Equal("6 bookings voided", rows[0].Details);
        Assert.Equal("EventConfirmed", rows[1].Action);
        Assert.Equal("Staff", rows[1].ActorType);
    }

    [Fact]
    public async Task AAttendeesHistoryGathersTheirInviteAndBookingEntries()
    {
        await fixture.ResetAsync();

        var attendeeId = Guid.NewGuid();
        var inviteId = Guid.NewGuid();

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
            write.Invites.Add(Invite.CreateInitial(
                inviteId,
                attendee.Id,
                "hash",
                Now.AddDays(4),
                [ProposalFixture.LocationId],
                [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
                attendee.RequiredAppointmentTypeIds,
                0));

            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Invite, inviteId, AuditAction.InviteCreated,
                ActorType.System, null, Now, "retry 0"));
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Invite, Guid.NewGuid(), AuditAction.InviteCreated,
                ActorType.System, null, Now, "someone else"));

            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var rows = await new AuditQueries(read).ForAttendeeAsync(attendeeId, CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal("InviteCreated", row.Action);
        Assert.Equal("retry 0", row.Details);
        Assert.Equal("System", row.ActorType);
        Assert.Null(row.ActorId);
    }

    [Fact]
    public async Task AAttendeeWithNoHistoryGetsAnEmptyList()
    {
        await fixture.ResetAsync();

        await using var read = fixture.NewContext();
        var rows = await new AuditQueries(read).ForAttendeeAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task ForAttendeeIncludesBookingAppointmentEvents()
    {
        await fixture.ResetAsync();
        var attendeeId = Guid.NewGuid();
        var appointmentId = await SeedAttendeeBookingAsync(attendeeId);
        await AddAuditAsync(AuditEntityTypes.BookingAppointment, appointmentId, AuditAction.AppointmentCheckedIn);

        await using var context = fixture.NewContext();
        var rows = await new AuditQueries(context).ForAttendeeAsync(attendeeId, CancellationToken.None);

        Assert.Contains(rows, r => r.EntityId == appointmentId);
    }

    [Fact]
    public async Task ForAttendeeIncludesEntriesRecordedAgainstTheAttendeeItself()
    {
        await fixture.ResetAsync();
        var attendeeId = Guid.NewGuid();
        await SeedAttendeeBookingAsync(attendeeId);
        await AddAuditAsync(AuditEntityTypes.Attendee, attendeeId, AuditAction.AttendeeGroupAssigned, Now.AddHours(-1));
        await AddAuditAsync(AuditEntityTypes.Attendee, Guid.NewGuid(), AuditAction.AttendeeGroupReassigned);
        await AddAuditAsync(AuditEntityTypes.Event, attendeeId, AuditAction.EventConfirmed);

        await using var context = fixture.NewContext();
        var rows = await new AuditQueries(context).ForAttendeeAsync(attendeeId, CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal(AuditEntityTypes.Attendee, row.EntityType);
        Assert.Equal(nameof(AuditAction.AttendeeGroupAssigned), row.Action);
    }

    [Fact]
    public async Task SearchOrdersNewestFirstWithStableCursor()
    {
        await fixture.ResetAsync();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await AddAuditAsync(AuditEntityTypes.Event, first, AuditAction.EventConfirmed, Now.AddHours(-2));
        await AddAuditAsync(AuditEntityTypes.Event, second, AuditAction.EventCancelled, Now.AddHours(-1));

        await using var context = fixture.NewContext();
        var queries = new AuditQueries(context);
        var page1 = await queries.SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, AuditEntityTypes.All, null, null, 1),
            CancellationToken.None);
        Assert.Single(page1.Rows);
        Assert.Equal(second, page1.Rows[0].EntityId);
        Assert.NotNull(page1.NextCursor);

        // A row newer than the cursor must not shift the following page.
        await AddAuditAsync(AuditEntityTypes.Event, Guid.NewGuid(), AuditAction.EventConfirmed, Now);

        var page2 = await queries.SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, AuditEntityTypes.All, null, page1.NextCursor, 10),
            CancellationToken.None);
        Assert.Contains(page2.Rows, r => r.EntityId == first);
        Assert.DoesNotContain(page2.Rows, r => r.EntityId == second);
    }

    [Fact]
    public async Task SearchRestrictedToOperationalBucketNeverReturnsAttendeeRows()
    {
        await fixture.ResetAsync();
        await AddAuditAsync(AuditEntityTypes.Booking, Guid.NewGuid(), AuditAction.BookingCreated);
        var eventId = Guid.NewGuid();
        await AddAuditAsync(AuditEntityTypes.Event, eventId, AuditAction.EventConfirmed);

        await using var context = fixture.NewContext();
        IReadOnlyList<string> operational =
            [AuditEntityTypes.EventProposal, AuditEntityTypes.Event, AuditEntityTypes.StaffAccessProfile];
        var page = await new AuditQueries(context).SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, operational, null, null, 50),
            CancellationToken.None);

        Assert.All(page.Rows, r => Assert.Contains(r.EntityType, operational));
        Assert.Contains(page.Rows, r => r.EntityId == eventId);
    }

    [Fact]
    public async Task SearchWithNoAllowedEntityTypesReturnsAnEmptyPage()
    {
        await fixture.ResetAsync();
        await AddAuditAsync(AuditEntityTypes.Event, Guid.NewGuid(), AuditAction.EventConfirmed);

        await using var context = fixture.NewContext();
        var page = await new AuditQueries(context).SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, [], null, null, 50),
            CancellationToken.None);

        Assert.Empty(page.Rows);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task IdentifierMatchesEntityIdAndActorId()
    {
        await fixture.ResetAsync();
        var entityId = Guid.NewGuid();
        const string actor = "staff-actor-7";
        await AddAuditAsync(AuditEntityTypes.Event, entityId, AuditAction.EventConfirmed, actor: actor);

        await using var context = fixture.NewContext();
        var queries = new AuditQueries(context);
        var byEntity = await queries.SearchAsync(
            new AuditSearchFilter(null, null, null, null, entityId.ToString(), AuditEntityTypes.All, null, null, 50),
            CancellationToken.None);
        Assert.Contains(byEntity.Rows, r => r.EntityId == entityId);

        var byActor = await queries.SearchAsync(
            new AuditSearchFilter(null, null, null, null, actor, AuditEntityTypes.All, null, null, 50),
            CancellationToken.None);
        Assert.Contains(byActor.Rows, r => r.EntityId == entityId);
    }

    [Fact]
    public async Task SearchFiltersByTimestampRangeActionAndActorType()
    {
        await fixture.ResetAsync();
        var wanted = Guid.NewGuid();
        await AddAuditAsync(AuditEntityTypes.Event, wanted, AuditAction.EventCancelled, Now);
        await AddAuditAsync(AuditEntityTypes.Event, Guid.NewGuid(), AuditAction.EventConfirmed, Now);
        await AddAuditAsync(AuditEntityTypes.Event, Guid.NewGuid(), AuditAction.EventCancelled, Now.AddDays(-30));

        await using var context = fixture.NewContext();
        var page = await new AuditQueries(context).SearchAsync(
            new AuditSearchFilter(
                Now.AddHours(-1), Now.AddHours(1), "Staff", nameof(AuditAction.EventCancelled),
                null, AuditEntityTypes.All, null, null, 50),
            CancellationToken.None);

        var row = Assert.Single(page.Rows);
        Assert.Equal(wanted, row.EntityId);
    }

    [Fact]
    public async Task SearchTreatsAMalformedCursorAsAbsent()
    {
        await fixture.ResetAsync();
        var eventId = Guid.NewGuid();
        await AddAuditAsync(AuditEntityTypes.Event, eventId, AuditAction.EventConfirmed);

        await using var context = fixture.NewContext();
        var page = await new AuditQueries(context).SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, AuditEntityTypes.All, null, "not-a-cursor", 50),
            CancellationToken.None);

        Assert.Contains(page.Rows, r => r.EntityId == eventId);
    }

    /// <summary>Seeds a attendee with an invite, booking, and booking appointment; returns the appointment id.</summary>
    private async Task<Guid> SeedAttendeeBookingAsync(Guid attendeeId)
    {
        await using var write = fixture.NewContext();
        var pilots = write.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
        var attendee = Attendee.Create(attendeeId, "Amara Novak", "a.novak@mail.com", pilots, ProposalFixture.Now);
        var eventId = Guid.NewGuid();
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendeeId,
            "hash",
            Now.AddDays(4),
            [ProposalFixture.LocationId],
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(Guid.NewGuid(), invite, eventId, "manage-token-hash", Now);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting);

        write.Attendees.Add(attendee);
        write.Invites.Add(invite);
        write.Bookings.Add(booking);
        write.BookingAppointments.Add(appointment);
        await write.SaveChangesAsync();

        return appointment.Id;
    }

    /// <summary>Appends one audit-log row and returns the audited entity id.</summary>
    private async Task<Guid> AddAuditAsync(
        string entityType,
        Guid entityId,
        AuditAction action,
        DateTimeOffset? timestamp = null,
        string? actor = "staff-1")
    {
        await using var write = fixture.NewContext();
        write.AuditLogs.Add(AuditLog.Record(
            Guid.NewGuid(), entityType, entityId, action,
            actor is null ? ActorType.System : ActorType.Staff, actor, timestamp ?? Now, null));
        await write.SaveChangesAsync();
        return entityId;
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/AuditQueryTests.cs — 1/1

<!-- retirement-file: {"id":65,"file":"tests/EventBooking.Infrastructure.Tests/AuditQueryTests.cs","beforeSha":"751e15a8a699cebd33494b9eeac79865c3a1f7ffb76251977591b744242c8efa","afterSha":"7b05ccdc250af241389a9af3b94bbf262974b02136d324e9fb1df4f285bb2710","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class AuditQueryTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AEventsHistoryComesBackNewestFirst()
    {
        await fixture.ResetAsync();
        var eventId = Guid.NewGuid();

        await using (var write = fixture.NewContext())
        {
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Event, eventId, AuditAction.EventConfirmed,
                ActorType.Staff, "staff-1", Now, null));
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Event, eventId, AuditAction.EventCancelled,
                ActorType.Staff, "staff-2", Now.AddHours(1), "6 bookings voided"));
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Event, Guid.NewGuid(), AuditAction.EventConfirmed,
                ActorType.Staff, "staff-3", Now, null));
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var rows = await new AuditQueries(read).ForEntityAsync(
            AuditEntityTypes.Event, eventId, CancellationToken.None);

        Assert.Equal(2, rows.Count);
        Assert.Equal("EventCancelled", rows[0].Action);
        Assert.Equal("6 bookings voided", rows[0].Details);
        Assert.Equal("EventConfirmed", rows[1].Action);
        Assert.Equal("Staff", rows[1].ActorType);
    }

    [Fact]
    public async Task AAttendeesHistoryGathersTheirInviteAndBookingEntries()
    {
        await fixture.ResetAsync();

        var attendeeId = Guid.NewGuid();
        var inviteId = Guid.NewGuid();

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
            write.Invites.Add(Invite.CreateInitial(
                inviteId,
                attendee.Id,
                Now.AddDays(4),
                [ProposalFixture.LocationId],
                [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
                attendee.RequiredAppointmentTypeIds,
                0));

            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Invite, inviteId, AuditAction.InviteCreated,
                ActorType.System, null, Now, "retry 0"));
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Invite, Guid.NewGuid(), AuditAction.InviteCreated,
                ActorType.System, null, Now, "someone else"));

            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var rows = await new AuditQueries(read).ForAttendeeAsync(attendeeId, CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal("InviteCreated", row.Action);
        Assert.Equal("retry 0", row.Details);
        Assert.Equal("System", row.ActorType);
        Assert.Null(row.ActorId);
    }

    [Fact]
    public async Task AAttendeeWithNoHistoryGetsAnEmptyList()
    {
        await fixture.ResetAsync();

        await using var read = fixture.NewContext();
        var rows = await new AuditQueries(read).ForAttendeeAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task ForAttendeeIncludesBookingAppointmentEvents()
    {
        await fixture.ResetAsync();
        var attendeeId = Guid.NewGuid();
        var appointmentId = await SeedAttendeeBookingAsync(attendeeId);
        await AddAuditAsync(AuditEntityTypes.BookingAppointment, appointmentId, AuditAction.AppointmentCheckedIn);

        await using var context = fixture.NewContext();
        var rows = await new AuditQueries(context).ForAttendeeAsync(attendeeId, CancellationToken.None);

        Assert.Contains(rows, r => r.EntityId == appointmentId);
    }

    [Fact]
    public async Task ForAttendeeIncludesEntriesRecordedAgainstTheAttendeeItself()
    {
        await fixture.ResetAsync();
        var attendeeId = Guid.NewGuid();
        await SeedAttendeeBookingAsync(attendeeId);
        await AddAuditAsync(AuditEntityTypes.Attendee, attendeeId, AuditAction.AttendeeGroupAssigned, Now.AddHours(-1));
        await AddAuditAsync(AuditEntityTypes.Attendee, Guid.NewGuid(), AuditAction.AttendeeGroupReassigned);
        await AddAuditAsync(AuditEntityTypes.Event, attendeeId, AuditAction.EventConfirmed);

        await using var context = fixture.NewContext();
        var rows = await new AuditQueries(context).ForAttendeeAsync(attendeeId, CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal(AuditEntityTypes.Attendee, row.EntityType);
        Assert.Equal(nameof(AuditAction.AttendeeGroupAssigned), row.Action);
    }

    [Fact]
    public async Task SearchOrdersNewestFirstWithStableCursor()
    {
        await fixture.ResetAsync();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await AddAuditAsync(AuditEntityTypes.Event, first, AuditAction.EventConfirmed, Now.AddHours(-2));
        await AddAuditAsync(AuditEntityTypes.Event, second, AuditAction.EventCancelled, Now.AddHours(-1));

        await using var context = fixture.NewContext();
        var queries = new AuditQueries(context);
        var page1 = await queries.SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, AuditEntityTypes.All, null, null, 1),
            CancellationToken.None);
        Assert.Single(page1.Rows);
        Assert.Equal(second, page1.Rows[0].EntityId);
        Assert.NotNull(page1.NextCursor);

        // A row newer than the cursor must not shift the following page.
        await AddAuditAsync(AuditEntityTypes.Event, Guid.NewGuid(), AuditAction.EventConfirmed, Now);

        var page2 = await queries.SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, AuditEntityTypes.All, null, page1.NextCursor, 10),
            CancellationToken.None);
        Assert.Contains(page2.Rows, r => r.EntityId == first);
        Assert.DoesNotContain(page2.Rows, r => r.EntityId == second);
    }

    [Fact]
    public async Task SearchRestrictedToOperationalBucketNeverReturnsAttendeeRows()
    {
        await fixture.ResetAsync();
        await AddAuditAsync(AuditEntityTypes.Booking, Guid.NewGuid(), AuditAction.BookingCreated);
        var eventId = Guid.NewGuid();
        await AddAuditAsync(AuditEntityTypes.Event, eventId, AuditAction.EventConfirmed);

        await using var context = fixture.NewContext();
        IReadOnlyList<string> operational =
            [AuditEntityTypes.EventProposal, AuditEntityTypes.Event, AuditEntityTypes.StaffAccessProfile];
        var page = await new AuditQueries(context).SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, operational, null, null, 50),
            CancellationToken.None);

        Assert.All(page.Rows, r => Assert.Contains(r.EntityType, operational));
        Assert.Contains(page.Rows, r => r.EntityId == eventId);
    }

    [Fact]
    public async Task SearchWithNoAllowedEntityTypesReturnsAnEmptyPage()
    {
        await fixture.ResetAsync();
        await AddAuditAsync(AuditEntityTypes.Event, Guid.NewGuid(), AuditAction.EventConfirmed);

        await using var context = fixture.NewContext();
        var page = await new AuditQueries(context).SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, [], null, null, 50),
            CancellationToken.None);

        Assert.Empty(page.Rows);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task IdentifierMatchesEntityIdAndActorId()
    {
        await fixture.ResetAsync();
        var entityId = Guid.NewGuid();
        const string actor = "staff-actor-7";
        await AddAuditAsync(AuditEntityTypes.Event, entityId, AuditAction.EventConfirmed, actor: actor);

        await using var context = fixture.NewContext();
        var queries = new AuditQueries(context);
        var byEntity = await queries.SearchAsync(
            new AuditSearchFilter(null, null, null, null, entityId.ToString(), AuditEntityTypes.All, null, null, 50),
            CancellationToken.None);
        Assert.Contains(byEntity.Rows, r => r.EntityId == entityId);

        var byActor = await queries.SearchAsync(
            new AuditSearchFilter(null, null, null, null, actor, AuditEntityTypes.All, null, null, 50),
            CancellationToken.None);
        Assert.Contains(byActor.Rows, r => r.EntityId == entityId);
    }

    [Fact]
    public async Task SearchFiltersByTimestampRangeActionAndActorType()
    {
        await fixture.ResetAsync();
        var wanted = Guid.NewGuid();
        await AddAuditAsync(AuditEntityTypes.Event, wanted, AuditAction.EventCancelled, Now);
        await AddAuditAsync(AuditEntityTypes.Event, Guid.NewGuid(), AuditAction.EventConfirmed, Now);
        await AddAuditAsync(AuditEntityTypes.Event, Guid.NewGuid(), AuditAction.EventCancelled, Now.AddDays(-30));

        await using var context = fixture.NewContext();
        var page = await new AuditQueries(context).SearchAsync(
            new AuditSearchFilter(
                Now.AddHours(-1), Now.AddHours(1), "Staff", nameof(AuditAction.EventCancelled),
                null, AuditEntityTypes.All, null, null, 50),
            CancellationToken.None);

        var row = Assert.Single(page.Rows);
        Assert.Equal(wanted, row.EntityId);
    }

    [Fact]
    public async Task SearchTreatsAMalformedCursorAsAbsent()
    {
        await fixture.ResetAsync();
        var eventId = Guid.NewGuid();
        await AddAuditAsync(AuditEntityTypes.Event, eventId, AuditAction.EventConfirmed);

        await using var context = fixture.NewContext();
        var page = await new AuditQueries(context).SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, AuditEntityTypes.All, null, "not-a-cursor", 50),
            CancellationToken.None);

        Assert.Contains(page.Rows, r => r.EntityId == eventId);
    }

    /// <summary>Seeds a attendee with an invite, booking, and booking appointment; returns the appointment id.</summary>
    private async Task<Guid> SeedAttendeeBookingAsync(Guid attendeeId)
    {
        await using var write = fixture.NewContext();
        var pilots = write.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
        var attendee = Attendee.Create(attendeeId, "Amara Novak", "a.novak@mail.com", pilots, ProposalFixture.Now);
        var eventId = Guid.NewGuid();
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendeeId,
            Now.AddDays(4),
            [ProposalFixture.LocationId],
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(Guid.NewGuid(), invite, eventId, Now);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting);

        write.Attendees.Add(attendee);
        write.Invites.Add(invite);
        write.Bookings.Add(booking);
        write.BookingAppointments.Add(appointment);
        await write.SaveChangesAsync();

        return appointment.Id;
    }

    /// <summary>Appends one audit-log row and returns the audited entity id.</summary>
    private async Task<Guid> AddAuditAsync(
        string entityType,
        Guid entityId,
        AuditAction action,
        DateTimeOffset? timestamp = null,
        string? actor = "staff-1")
    {
        await using var write = fixture.NewContext();
        write.AuditLogs.Add(AuditLog.Record(
            Guid.NewGuid(), entityType, entityId, action,
            actor is null ? ActorType.System : ActorType.Staff, actor, timestamp ?? Now, null));
        await write.SaveChangesAsync();
        return entityId;
    }
}
`````
