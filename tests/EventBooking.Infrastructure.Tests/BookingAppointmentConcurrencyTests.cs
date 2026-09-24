using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Appointments;
using EventBooking.Application.Invites;
using EventBooking.Application.Recovery;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Audit;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies PostgreSQL serialises distinct booking-appointment status writers.</summary>
[Collection("postgres")]
public sealed class BookingAppointmentConcurrencyTests(PostgresFixture fixture)
{
    /// <summary>Verifies exactly one concurrent transition commits from the same expected version.</summary>
    [Fact]
    public async Task ConcurrentDistinctTransitionsCannotSilentlyOverwrite()
    {
        await fixture.ResetAsync();
        var now = new DateTimeOffset(2026, 9, 7, 14, 0, 0, TimeSpan.Zero);
        var staff = Guid.NewGuid();
        Guid appointmentId;

        await using (var seed = fixture.NewContext())
        {
            seed.StaffAccessProfiles.Add(StaffAccessProfile.Create(
                staff, [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting));
            var pilots = seed.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
            var attendee = Attendee.Create(
                Guid.NewGuid(),
                "Amara Novak",
                "amara@example.com",
                pilots,
                ProposalFixture.Now);
            var eventItem = EventFixture.Create(
                Guid.NewGuid(),
                new EventWindow(new DateOnly(2026, 9, 7), new TimeOnly(9, 0), 240),
                AppointmentTypeIds.All.ToDictionary(value => value, _ => 10));
            var invite = Invite.CreateInitial(
                Guid.NewGuid(),
                attendee.Id,
                now.AddDays(1),
                [ProposalFixture.LocationId],
                [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
                attendee.RequiredAppointmentTypeIds,
                0);
            var booking = Booking.Create(
                Guid.NewGuid(), invite, eventItem.Id, now.AddDays(-1));
            var appointment = BookingAppointment.Create(
                Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting);
            appointmentId = appointment.Id;
            seed.Attendees.Add(attendee);
            seed.Events.Add(eventItem);
            seed.Bookings.Add(booking);
            seed.BookingAppointments.Add(appointment);
            await seed.SaveChangesAsync();
        }

        var first = ExecuteAsync(BookingAppointmentStatus.CheckedIn);
        var second = ExecuteAsync(BookingAppointmentStatus.NoShow);
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, result => result.IsSuccess);
        Assert.Single(results,
            result => result.IsFailure
                && result.Error.Code == "appointment_version_conflict");

        await using var read = fixture.NewContext();
        var stored = await read.BookingAppointments.AsNoTracking().SingleAsync();
        Assert.Equal(2, stored.Version);
        Assert.Contains(
            stored.Status,
            new[] { BookingAppointmentStatus.CheckedIn, BookingAppointmentStatus.NoShow });
        Assert.Single(await read.AuditLogs.AsNoTracking()
            .Where(entry => entry.EntityType == "BookingAppointment")
            .ToListAsync());

        async Task<EventBooking.Application.Common.Result<BookingAppointmentUpdateView>>
            ExecuteAsync(BookingAppointmentStatus status)
        {
            await using var context = fixture.NewContext();
            var handler = new UpdateBookingAppointmentStatusHandler(
                new StaffAccessAuthorizer(new StaffAccessProfileRepository(context)),
                new BookingAppointmentRepository(context),
                new BookingRepository(context),
                new AttendeeRepository(context),
                new InviteRepository(context),
                new EventRepository(context),
                new RecoveryBookingOutcomeCoordinator(),
                new EfAuditLogger(context, new FixedClock(now)),
                new UnitOfWork(context),
                new FixedClock(now));
            return await handler.HandleAsync(
                new UpdateBookingAppointmentStatusCommand
                {
                    StaffUserId = staff,
                    BookingAppointmentId = appointmentId,
                    Status = status,
                    ExpectedVersion = 1,
                },
                CancellationToken.None);
        }
    }

    /// <summary>
    /// A no-show correction racing recovery issuance leaves exactly one winner: the loser
    /// sees recovery_state_changed, recovery_not_available, or the cancel-recovery-first
    /// conflict, and a pending recovery never coexists with corrected Expected state.
    /// </summary>
    [Fact]
    public async Task CorrectionRacingIssuanceLeavesExactlyOneWinner()
    {
        await fixture.ResetAsync();
        var now = new DateTimeOffset(2026, 9, 7, 14, 0, 0, TimeSpan.Zero);
        var coordinator = Guid.NewGuid();
        var appointmentStaff = Guid.NewGuid();
        Guid attendeeId;
        Guid appointmentId;

        var ids = await GivenCorrectionRaceAsync(now, coordinator, appointmentStaff);
        attendeeId = ids.AttendeeId;
        appointmentId = ids.AppointmentId;

        var correctTask = CorrectAsync();
        var issueTask = IssueAsync();
        await Task.WhenAll(correctTask, issueTask);

        var correctResult = await correctTask;
        var issueResult = await issueTask;
        Assert.True(correctResult.IsSuccess ^ issueResult.IsSuccess);

        await using var read = fixture.NewContext();
        var stored = await read.BookingAppointments.AsNoTracking().SingleAsync();
        var pendingRecovery = await read.Invites.AnyAsync(invite =>
            invite.AttendeeId == attendeeId
            && invite.Status == InviteStatus.Pending
            && invite.RecoveryOfBookingId != null);
        Assert.False(pendingRecovery && stored.Status == BookingAppointmentStatus.Expected);

        if (correctResult.IsSuccess)
        {
            Assert.True(issueResult.IsFailure);
            Assert.Contains(
                issueResult.Error.Code, new[] { "recovery_not_available", "recovery_state_changed" });
            Assert.Equal(BookingAppointmentStatus.Expected, stored.Status);
            Assert.Equal(3, stored.Version);
            Assert.False(pendingRecovery);
        }
        else
        {
            Assert.Equal("recovery-active", correctResult.Error.Code);
            Assert.Contains("Cancel the recovery first", correctResult.Error.Message);
            Assert.True(issueResult.IsSuccess);
            Assert.Equal(BookingAppointmentStatus.NoShow, stored.Status);
            Assert.True(pendingRecovery);
        }

        async Task<EventBooking.Application.Common.Result<BookingAppointmentUpdateView>> CorrectAsync()
        {
            await using var context = fixture.NewContext();
            var handler = new UpdateBookingAppointmentStatusHandler(
                new StaffAccessAuthorizer(new StaffAccessProfileRepository(context)),
                new BookingAppointmentRepository(context),
                new BookingRepository(context),
                new AttendeeRepository(context),
                new InviteRepository(context),
                new EventRepository(context),
                new RecoveryBookingOutcomeCoordinator(),
                new EfAuditLogger(context, new FixedClock(now)),
                new UnitOfWork(context),
                new FixedClock(now));
            return await handler.HandleAsync(
                new UpdateBookingAppointmentStatusCommand
                {
                    StaffUserId = appointmentStaff,
                    BookingAppointmentId = appointmentId,
                    Status = BookingAppointmentStatus.Expected,
                    ExpectedVersion = 2,
                },
                CancellationToken.None);
        }

        async Task<EventBooking.Application.Common.Result<StartRecoveryOutcome>> IssueAsync()
        {
            await using var context = fixture.NewContext();
            var clock = new FixedClock(now);
            var eligibility = new EventEligibilityQuery(context);
            var issuer = new InviteIssuer(
                new InviteRepository(context),
                new SystemSettingsRepository(context),
                new EmailDeliveryRepository(context),
                new EfAuditLogger(context, clock),
                clock,
                eligibility);
            var handler = new StartRecoveryHandler(
                new AttendeeRepository(context),
                new BookingRepository(context),
                new BookingAppointmentRepository(context),
                new InviteRepository(context),
                new LocationRepository(context),
                new SystemSettingsRepository(context),
                new StaffAccessAuthorizer(new StaffAccessProfileRepository(context)),
                new UnitOfWork(context),
                clock,
                eligibility,
                issuer);
            return await handler.HandleAsync(
                new StartRecoveryCommand(coordinator, attendeeId, []), CancellationToken.None);
        }
    }

    /// <summary>
    /// Pins the issuance-wins branch sequentially: a pending recovery invite makes the
    /// later no-show correction fail with the cancel-recovery-first conflict.
    /// </summary>
    [Fact]
    public async Task IssuingFirstMakesCorrectionConflict()
    {
        await fixture.ResetAsync();
        var now = new DateTimeOffset(2026, 9, 7, 14, 0, 0, TimeSpan.Zero);
        var coordinator = Guid.NewGuid();
        var appointmentStaff = Guid.NewGuid();
        Guid attendeeId;
        Guid appointmentId;

        var ids = await GivenCorrectionRaceAsync(now, coordinator, appointmentStaff);
        attendeeId = ids.AttendeeId;
        appointmentId = ids.AppointmentId;

        await using var issueContext = fixture.NewContext();
        var issueResult = await IssueAsync(issueContext, now, coordinator, attendeeId);
        Assert.True(issueResult.IsSuccess);

        await using var correctContext = fixture.NewContext();
        var correctResult = await CorrectAsync(
            correctContext, now, appointmentStaff, appointmentId);

        Assert.True(correctResult.IsFailure);
        Assert.Equal("recovery-active", correctResult.Error.Code);
        Assert.Contains("Cancel the recovery first", correctResult.Error.Message);

        await using var read = fixture.NewContext();
        var stored = await read.BookingAppointments.AsNoTracking().SingleAsync();
        Assert.Equal(BookingAppointmentStatus.NoShow, stored.Status);
        Assert.True(await read.Invites.AnyAsync(invite =>
            invite.AttendeeId == attendeeId
            && invite.Status == InviteStatus.Pending
            && invite.RecoveryOfBookingId != null));

        async Task<EventBooking.Application.Common.Result<BookingAppointmentUpdateView>> CorrectAsync(
            EventBookingDbContext context,
            DateTimeOffset at,
            Guid staff,
            Guid appointment)
        {
            var handler = new UpdateBookingAppointmentStatusHandler(
                new StaffAccessAuthorizer(new StaffAccessProfileRepository(context)),
                new BookingAppointmentRepository(context),
                new BookingRepository(context),
                new AttendeeRepository(context),
                new InviteRepository(context),
                new EventRepository(context),
                new RecoveryBookingOutcomeCoordinator(),
                new EfAuditLogger(context, new FixedClock(at)),
                new UnitOfWork(context),
                new FixedClock(at));
            return await handler.HandleAsync(
                new UpdateBookingAppointmentStatusCommand
                {
                    StaffUserId = staff,
                    BookingAppointmentId = appointment,
                    Status = BookingAppointmentStatus.Expected,
                    ExpectedVersion = 2,
                },
                CancellationToken.None);
        }

        async Task<EventBooking.Application.Common.Result<StartRecoveryOutcome>> IssueAsync(
            EventBookingDbContext context,
            DateTimeOffset at,
            Guid staff,
            Guid attendee)
        {
            var clock = new FixedClock(at);
            var eligibility = new EventEligibilityQuery(context);
            var issuer = new InviteIssuer(
                new InviteRepository(context),
                new SystemSettingsRepository(context),
                new EmailDeliveryRepository(context),
                new EfAuditLogger(context, clock),
                clock,
                eligibility);
            var handler = new StartRecoveryHandler(
                new AttendeeRepository(context),
                new BookingRepository(context),
                new BookingAppointmentRepository(context),
                new InviteRepository(context),
                new LocationRepository(context),
                new SystemSettingsRepository(context),
                new StaffAccessAuthorizer(new StaffAccessProfileRepository(context)),
                new UnitOfWork(context),
                clock,
                eligibility,
                issuer);
            return await handler.HandleAsync(
                new StartRecoveryCommand(staff, attendee, []), CancellationToken.None);
        }
    }

    private async Task<(Guid AttendeeId, Guid AppointmentId)> GivenCorrectionRaceAsync(
        DateTimeOffset now,
        Guid coordinator,
        Guid appointmentStaff)
    {
        Guid attendeeId;
        Guid appointmentId;

        await using (var seed = fixture.NewContext())
        {
            seed.StaffAccessProfiles.Add(StaffAccessProfile.Create(
                coordinator, [Role.Coordinator], null));
            seed.StaffAccessProfiles.Add(StaffAccessProfile.Create(
                appointmentStaff, [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting));
            var group = AttendeeGroup.Define(
                Guid.NewGuid(), $"DAT_ONLY_{Guid.NewGuid():N}".ToUpperInvariant(), "DAT only", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting]);
            var attendee = Attendee.Create(
                Guid.NewGuid(),
                "Amara Novak",
                "amara@example.com",
                group,
                ProposalFixture.Now);
            attendee.MarkInvited(ProposalFixture.Now);
            attendee.MarkBooked(ProposalFixture.Now);
            attendeeId = attendee.Id;
            var pastEvent = EventFixture.Create(
                Guid.NewGuid(),
                new EventWindow(new DateOnly(2026, 9, 7), new TimeOnly(9, 0), 240),
                AppointmentTypeIds.All.ToDictionary(value => value, _ => 10));
            var invite = Invite.CreateInitial(
                Guid.NewGuid(),
                attendee.Id,
                now.AddDays(1),
                [ProposalFixture.LocationId],
                [pastEvent.Id, Guid.NewGuid(), Guid.NewGuid()],
                attendee.RequiredAppointmentTypeIds,
                0);
            var booking = Booking.Create(
                Guid.NewGuid(), invite, pastEvent.Id, now.AddDays(-1));
            invite.MarkUsed();
            var appointment = BookingAppointment.Create(
                Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting);
            appointment.TransitionTo(
                BookingAppointmentStatus.NoShow, appointmentStaff, now, false, true);
            appointmentId = appointment.Id;
            seed.Attendees.Add(attendee);
            seed.Events.Add(pastEvent);
            foreach (var day in new[] { 30, 31, 32 })
            {
                seed.Events.Add(EventFixture.Create(
                    Guid.NewGuid(),
                    new EventWindow(DateOnly.FromDateTime(now.DateTime).AddDays(day), new TimeOnly(9, 0), 240),
                    AppointmentTypeIds.All.ToDictionary(value => value, _ => 10)));
            }

            seed.Invites.Add(invite);
            seed.Bookings.Add(booking);
            seed.BookingAppointments.Add(appointment);
            seed.AttendeeGroups.Add(group);
            await seed.SaveChangesAsync();
        }

        return (attendeeId, appointmentId);
    }

    private sealed class FixedClock(DateTimeOffset now)
        : EventBooking.Application.Abstractions.IClock
    {
        /// <inheritdoc/>
        public DateTimeOffset UtcNow => now;
        /// <inheritdoc/>
        public DateTimeOffset NowAtTransitionalLocation => now;
        /// <inheritdoc/>
        public DateOnly TodayAtTransitionalLocation => DateOnly.FromDateTime(now.DateTime);
        /// <inheritdoc/>
        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) =>
            DateOnly.FromDateTime(instant.DateTime);

        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => instant;
    }
}
