# 02d — The invite eligibility query, and the start instant it orders on, edits 14 (Task 11)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs — 1/1

<!-- retirement-file: {"id":29,"file":"tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs","beforeSha":"7803ac1c80080c2cceef8be22ec938c2734174633b8acbed01744afe0b516c1d","afterSha":"0130a0220297aca5bcb17ba20c5eed9c7f72a4581f2c48b6489b30867ef0ed6e","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Appointments;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Audit;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Repositories;
using EventBooking.Infrastructure.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

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
            Assert.Equal("conflict", correctResult.Error.Code);
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

        async Task<EventBooking.Application.Common.Result<StartRecoveryResult>> IssueAsync()
        {
            await using var context = fixture.NewContext();
            var clock = new FixedClock(now);
            var unitOfWork = new UnitOfWork(context);
            var audit = new EfAuditLogger(context, clock);
            var tokens = new HmacTokenService(
                new TokenOptions("a-correction-race-signing-key-long-enough"));
            var events = new EventRepository(context);
            var deliveries = new EmailDeliveryService(
                new EmailDeliveryRepository(context), new SilentSender(), unitOfWork, clock,
                NullLogger<EmailDeliveryService>.Instance);
            var issuer = new InviteIssuer(
                new InviteRepository(context),
                new AttendeeGroupRepository(context),
                new EligibleEventFinder(events, clock),
                new SystemSettingsRepository(context),
                tokens,
                deliveries,
                audit,
                clock,
                new AttendeePortalOptions(
                    "https://booking.example.com", "recruitment@example.com"));
            var handler = new StartRecoveryHandler(
                new AttendeeRepository(context),
                new StaffAccessAuthorizer(new StaffAccessProfileRepository(context)),
                new InviteRepository(context),
                new BookingRepository(context),
                new BookingAppointmentRepository(context),
                issuer,
                new EligibleEventFinder(events, clock),
                deliveries,
                unitOfWork);
            return await handler.HandleAsync(
                new StartRecoveryCommand(coordinator, attendeeId), CancellationToken.None);
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
        Assert.Equal("conflict", correctResult.Error.Code);
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

        async Task<EventBooking.Application.Common.Result<StartRecoveryResult>> IssueAsync(
            EventBookingDbContext context,
            DateTimeOffset at,
            Guid staff,
            Guid attendee)
        {
            var clock = new FixedClock(at);
            var unitOfWork = new UnitOfWork(context);
            var audit = new EfAuditLogger(context, clock);
            var tokens = new HmacTokenService(
                new TokenOptions("a-correction-race-signing-key-long-enough"));
            var events = new EventRepository(context);
            var deliveries = new EmailDeliveryService(
                new EmailDeliveryRepository(context), new SilentSender(), unitOfWork, clock,
                NullLogger<EmailDeliveryService>.Instance);
            var issuer = new InviteIssuer(
                new InviteRepository(context),
                new AttendeeGroupRepository(context),
                new EligibleEventFinder(events, clock),
                new SystemSettingsRepository(context),
                tokens,
                deliveries,
                audit,
                clock,
                new AttendeePortalOptions(
                    "https://booking.example.com", "recruitment@example.com"));
            var handler = new StartRecoveryHandler(
                new AttendeeRepository(context),
                new StaffAccessAuthorizer(new StaffAccessProfileRepository(context)),
                new InviteRepository(context),
                new BookingRepository(context),
                new BookingAppointmentRepository(context),
                issuer,
                new EligibleEventFinder(events, clock),
                deliveries,
                unitOfWork);
            return await handler.HandleAsync(
                new StartRecoveryCommand(staff, attendee), CancellationToken.None);
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

            seed.Bookings.Add(booking);
            seed.BookingAppointments.Add(appointment);
            seed.AttendeeGroups.Add(group);
            await seed.SaveChangesAsync();
        }

        return (attendeeId, appointmentId);
    }

    private sealed class SilentSender : IEmailSender
    {
        public Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
            Task.FromResult(true);
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
`````

## after — tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs — 1/1

<!-- retirement-file: {"id":29,"file":"tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs","beforeSha":"7803ac1c80080c2cceef8be22ec938c2734174633b8acbed01744afe0b516c1d","afterSha":"0130a0220297aca5bcb17ba20c5eed9c7f72a4581f2c48b6489b30867ef0ed6e","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Appointments;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Audit;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using EventBooking.Infrastructure.Persistence.Repositories;
using EventBooking.Infrastructure.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

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
            Assert.Equal("conflict", correctResult.Error.Code);
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

        async Task<EventBooking.Application.Common.Result<StartRecoveryResult>> IssueAsync()
        {
            await using var context = fixture.NewContext();
            var clock = new FixedClock(now);
            var unitOfWork = new UnitOfWork(context);
            var audit = new EfAuditLogger(context, clock);
            var tokens = new HmacTokenService(
                new TokenOptions("a-correction-race-signing-key-long-enough"));
            var events = new EventRepository(context);
            var deliveries = new EmailDeliveryService(
                new EmailDeliveryRepository(context), new SilentSender(), unitOfWork, clock,
                NullLogger<EmailDeliveryService>.Instance);
            var issuer = new InviteIssuer(
                new InviteRepository(context),
                new AttendeeGroupRepository(context),
                new EligibleEventFinder(new EventEligibilityQuery(context), events, clock),
                new SystemSettingsRepository(context),
                tokens,
                deliveries,
                audit,
                clock,
                new AttendeePortalOptions(
                    "https://booking.example.com", "recruitment@example.com"));
            var handler = new StartRecoveryHandler(
                new AttendeeRepository(context),
                new StaffAccessAuthorizer(new StaffAccessProfileRepository(context)),
                new InviteRepository(context),
                new BookingRepository(context),
                new BookingAppointmentRepository(context),
                issuer,
                new EligibleEventFinder(new EventEligibilityQuery(context), events, clock),
                deliveries,
                unitOfWork);
            return await handler.HandleAsync(
                new StartRecoveryCommand(coordinator, attendeeId), CancellationToken.None);
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
        Assert.Equal("conflict", correctResult.Error.Code);
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

        async Task<EventBooking.Application.Common.Result<StartRecoveryResult>> IssueAsync(
            EventBookingDbContext context,
            DateTimeOffset at,
            Guid staff,
            Guid attendee)
        {
            var clock = new FixedClock(at);
            var unitOfWork = new UnitOfWork(context);
            var audit = new EfAuditLogger(context, clock);
            var tokens = new HmacTokenService(
                new TokenOptions("a-correction-race-signing-key-long-enough"));
            var events = new EventRepository(context);
            var deliveries = new EmailDeliveryService(
                new EmailDeliveryRepository(context), new SilentSender(), unitOfWork, clock,
                NullLogger<EmailDeliveryService>.Instance);
            var issuer = new InviteIssuer(
                new InviteRepository(context),
                new AttendeeGroupRepository(context),
                new EligibleEventFinder(new EventEligibilityQuery(context), events, clock),
                new SystemSettingsRepository(context),
                tokens,
                deliveries,
                audit,
                clock,
                new AttendeePortalOptions(
                    "https://booking.example.com", "recruitment@example.com"));
            var handler = new StartRecoveryHandler(
                new AttendeeRepository(context),
                new StaffAccessAuthorizer(new StaffAccessProfileRepository(context)),
                new InviteRepository(context),
                new BookingRepository(context),
                new BookingAppointmentRepository(context),
                issuer,
                new EligibleEventFinder(new EventEligibilityQuery(context), events, clock),
                deliveries,
                unitOfWork);
            return await handler.HandleAsync(
                new StartRecoveryCommand(staff, attendee), CancellationToken.None);
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

            seed.Bookings.Add(booking);
            seed.BookingAppointments.Add(appointment);
            seed.AttendeeGroups.Add(group);
            await seed.SaveChangesAsync();
        }

        return (attendeeId, appointmentId);
    }

    private sealed class SilentSender : IEmailSender
    {
        public Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
            Task.FromResult(true);
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
`````

## before — tests/EventBooking.Infrastructure.Tests/EventPersistenceTests.cs — 1/1

<!-- retirement-file: {"id":30,"file":"tests/EventBooking.Infrastructure.Tests/EventPersistenceTests.cs","beforeSha":"18e7f5e85758c2feb014f2cefc3b159c7a55d44e7d655a0baffe404b717baa57","afterSha":"d507dca5c0b12959a45c3f177ad429316f2b5a401420037c28d64fc41d23d736","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class EventPersistenceTests(PostgresFixture fixture)
{
    private static Dictionary<Guid, int> FullHeadcounts() => new()
    {
        [AppointmentTypeIds.DrugAndAlcoholTesting] = 10,
        [AppointmentTypeIds.MedicalCheckUp] = 6,
        [AppointmentTypeIds.UniformFitting] = 8,
    };

    [Fact]
    public async Task AnEventPersistsWithItsProposalId()
    {
        await using var context = fixture.NewContext();
        var window = new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240);
        var eventItem = EventFixture.Create(Guid.NewGuid(), window, FullHeadcounts());

        context.Events.Add(eventItem);
        await context.SaveChangesAsync();

        await using var reload = fixture.NewContext();
        var reloaded = await reload.Events
            .Include(s => s.Capacities)
            .SingleAsync(s => s.Id == eventItem.Id);

        Assert.Equal(eventItem.ProposalId, reloaded.ProposalId);
        Assert.NotEqual(Guid.Empty, reloaded.ProposalId);
        Assert.Equal(3, reloaded.Capacities.Count);

        await fixture.ResetAsync();
    }

    [Fact]
    public async Task TwoEventsHaveDistinctProposalIds()
    {
        await using var context = fixture.NewContext();
        var first = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 12), new TimeOnly(9, 0), 240), FullHeadcounts());
        var second = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 13), new TimeOnly(9, 0), 240), FullHeadcounts());

        context.Events.AddRange(first, second);

        Assert.NotEqual(Guid.Empty, first.ProposalId);
        Assert.NotEqual(Guid.Empty, second.ProposalId);
        Assert.NotEqual(first.ProposalId, second.ProposalId);
        await context.SaveChangesAsync();

        await fixture.ResetAsync();
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/EventPersistenceTests.cs — 1/1

<!-- retirement-file: {"id":30,"file":"tests/EventBooking.Infrastructure.Tests/EventPersistenceTests.cs","beforeSha":"18e7f5e85758c2feb014f2cefc3b159c7a55d44e7d655a0baffe404b717baa57","afterSha":"d507dca5c0b12959a45c3f177ad429316f2b5a401420037c28d64fc41d23d736","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence.Repositories;
using EventBooking.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class EventPersistenceTests(PostgresFixture fixture)
{
    private static Dictionary<Guid, int> FullHeadcounts() => new()
    {
        [AppointmentTypeIds.DrugAndAlcoholTesting] = 10,
        [AppointmentTypeIds.MedicalCheckUp] = 6,
        [AppointmentTypeIds.UniformFitting] = 8,
    };

    [Fact]
    public async Task AnEventPersistsWithItsProposalId()
    {
        await using var context = fixture.NewContext();
        var window = new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240);
        var eventItem = EventFixture.Create(Guid.NewGuid(), window, FullHeadcounts());

        context.Events.Add(eventItem);
        await context.SaveChangesAsync();

        await using var reload = fixture.NewContext();
        var reloaded = await reload.Events
            .Include(s => s.Capacities)
            .SingleAsync(s => s.Id == eventItem.Id);

        Assert.Equal(eventItem.ProposalId, reloaded.ProposalId);
        Assert.NotEqual(Guid.Empty, reloaded.ProposalId);
        Assert.Equal(3, reloaded.Capacities.Count);

        await fixture.ResetAsync();
    }

    [Fact]
    public async Task TwoEventsHaveDistinctProposalIds()
    {
        await using var context = fixture.NewContext();
        var first = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 12), new TimeOnly(9, 0), 240), FullHeadcounts());
        var second = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 13), new TimeOnly(9, 0), 240), FullHeadcounts());

        context.Events.AddRange(first, second);

        Assert.NotEqual(Guid.Empty, first.ProposalId);
        Assert.NotEqual(Guid.Empty, second.ProposalId);
        Assert.NotEqual(first.ProposalId, second.ProposalId);
        await context.SaveChangesAsync();

        await fixture.ResetAsync();
    }

    [Fact]
    public async Task TheRepositoryWritesTheStartInstantFromTheLocationsZone()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        // A summer date, so the transitional location is an hour ahead of UTC and a start instant
        // copied from the local time would be an hour late.
        var window = new EventWindow(new DateOnly(2026, 7, 15), new TimeOnly(9, 0), 240);
        var eventItem = EventFixture.Create(Guid.NewGuid(), window, FullHeadcounts());

        await new EventRepository(context, new NodaTimeEventWindowZones())
            .AddAsync(eventItem, CancellationToken.None);

        Assert.Equal(
            new DateTimeOffset(2026, 7, 15, 8, 0, 0, TimeSpan.Zero),
            context.Entry(eventItem).Property<DateTimeOffset?>("StartUtc").CurrentValue);

        await context.SaveChangesAsync();

        await fixture.ResetAsync();
    }

    [Fact]
    public async Task AnEventAddedStraightThroughTheContextStillGetsItsStartInstant()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var window = new EventWindow(new DateOnly(2026, 7, 15), new TimeOnly(9, 0), 240);
        var eventItem = EventFixture.Create(Guid.NewGuid(), window, FullHeadcounts());

        context.Events.Add(eventItem);
        await context.SaveChangesAsync();

        await using var reload = fixture.NewContext();
        var stored = await reload.Database
            .SqlQuery<DateTimeOffset>(
                $"SELECT start_utc AS \"Value\" FROM event WHERE id = {eventItem.Id}")
            .SingleAsync();

        Assert.Equal(new DateTimeOffset(2026, 7, 15, 8, 0, 0, TimeSpan.Zero), stored);

        await fixture.ResetAsync();
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/PostgresFixture.cs — 1/1

<!-- retirement-file: {"id":31,"file":"tests/EventBooking.Infrastructure.Tests/PostgresFixture.cs","beforeSha":"86e22ad67c2c256d99e1f342bef68be65352d7f5901f1e2fd29d6c6355d0ed21","afterSha":"0633ec6dbfd623c41b6e319f2abd2d5e73565d011c7a918f827485b333d1b2a8","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace EventBooking.Infrastructure.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("eventbooking")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();
    private NpgsqlDataSource? _dataSource;

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _dataSource = NpgsqlDataSource.Create(ConnectionString);

        await using var context = NewContext();

        // The SeedData CLI does this before migrating, because the initial migration grants to
        // roles it does not create. The fixture has to do the same or the grants are skipped.
        await context.Database.ExecuteSqlRawAsync(DatabaseRoles.Script);
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    public EventBookingDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<EventBookingDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new EventBookingDbContext(options);
    }

    /// <summary>
    /// Empties every table and re-seeds the fixed rows. Faster and far less flaky than dropping
    /// and re-creating the database between tests.
    /// </summary>
    public async Task ResetAsync()
    {
        var dataSource = _dataSource
            ?? throw new InvalidOperationException("The PostgreSQL fixture has not been initialized.");
        await using var connection = await dataSource.OpenConnectionAsync();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                DO $$
                DECLARE statements CURSOR FOR
                    SELECT tablename FROM pg_tables
                    WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory'
                    -- Change-controlled Attendee Group rows stay seeded by the migration.
                    AND tablename NOT IN ('attendee_group', 'attendee_group_requirement');
                BEGIN
                    FOR statement IN statements LOOP
                        EXECUTE 'TRUNCATE TABLE ' || quote_ident(statement.tablename) || ' CASCADE;';
                    END LOOP;
                END $$;
                """;
            await command.ExecuteNonQueryAsync();
        }

        await using var context = NewContext();
        context.AppointmentTypes.AddRange(
            Domain.AppointmentTypes.AppointmentType.CreateFixedSet());
        context.SystemSettings.Add(Domain.Settings.SystemSettings.CreateDefault());
        await EnsureAttendeeGroupsSeededAsync(context);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Repairs change-controlled Attendee Group rows after truncation. Truncating
    /// appointment_type cascades into the mapping table, so migration-seeded mappings are
    /// re-inserted when missing. Production databases rely on the migration alone.
    /// </summary>
    private static async Task EnsureAttendeeGroupsSeededAsync(EventBookingDbContext context)
    {
        var fixedGroups = new[]
        {
            AttendeeGroup.Define(
                AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.UniformFitting]),
            AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]),
            AttendeeGroup.Define(
                AttendeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT", "Ground Operations Agent", true,
                [AppointmentTypeIds.MedicalCheckUp]),
            AttendeeGroup.Define(
                AttendeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
                [AppointmentTypeIds.MedicalCheckUp]),
            AttendeeGroup.Define(
                AttendeeGroupIds.GroundTransportServices, "GROUND_TRANSPORT_SERVICES", "Ground Transport Services", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.UniformFitting]),
        };

        foreach (var group in fixedGroups)
        {
            var existing = await context.AttendeeGroups
                .Include(persisted => persisted.Requirements)
                .SingleOrDefaultAsync(persisted => persisted.Id == group.Id);
            if (existing is null)
            {
                context.AttendeeGroups.Add(group);
                continue;
            }

            foreach (var requirement in group.Requirements)
            {
                if (existing.Requirements.All(persisted => persisted.AppointmentTypeId != requirement.AppointmentTypeId))
                {
                    context.Add(requirement);
                }
            }
        }
    }
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
`````

## after — tests/EventBooking.Infrastructure.Tests/PostgresFixture.cs — 1/1

<!-- retirement-file: {"id":31,"file":"tests/EventBooking.Infrastructure.Tests/PostgresFixture.cs","beforeSha":"86e22ad67c2c256d99e1f342bef68be65352d7f5901f1e2fd29d6c6355d0ed21","afterSha":"0633ec6dbfd623c41b6e319f2abd2d5e73565d011c7a918f827485b333d1b2a8","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Events;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Locations;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace EventBooking.Infrastructure.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("eventbooking")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();
    private NpgsqlDataSource? _dataSource;

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _dataSource = NpgsqlDataSource.Create(ConnectionString);

        await using var context = NewContext();

        // The SeedData CLI does this before migrating, because the initial migration grants to
        // roles it does not create. The fixture has to do the same or the grants are skipped.
        await context.Database.ExecuteSqlRawAsync(DatabaseRoles.Script);
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    public EventBookingDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<EventBookingDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new EventBookingDbContext(options);
    }

    /// <summary>
    /// Empties every table and re-seeds the fixed rows. Faster and far less flaky than dropping
    /// and re-creating the database between tests.
    /// </summary>
    public async Task ResetAsync()
    {
        var dataSource = _dataSource
            ?? throw new InvalidOperationException("The PostgreSQL fixture has not been initialized.");
        await using var connection = await dataSource.OpenConnectionAsync();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                DO $$
                DECLARE statements CURSOR FOR
                    SELECT tablename FROM pg_tables
                    WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory'
                    -- Change-controlled Attendee Group rows stay seeded by the migration.
                    AND tablename NOT IN ('attendee_group', 'attendee_group_requirement');
                BEGIN
                    FOR statement IN statements LOOP
                        EXECUTE 'TRUNCATE TABLE ' || quote_ident(statement.tablename) || ' CASCADE;';
                    END LOOP;
                END $$;
                """;
            await command.ExecuteNonQueryAsync();
        }

        await using var context = NewContext();
        context.AppointmentTypes.AddRange(
            Domain.AppointmentTypes.AppointmentType.CreateFixedSet());
        context.SystemSettings.Add(Domain.Settings.SystemSettings.CreateDefault());

        // The migration seeds the transitional location, and truncating takes it away with
        // everything else. Every event's derived start instant is computed from its location's
        // zone, so a reset that leaves the table empty makes the next event unwritable.
        context.Locations.Add(Location.Create(
            TransitionalLocation.Id,
            "TRANSITIONAL",
            "Transitional location",
            "Recorded against the transitional site until Phase 3.",
            TransitionalLocation.TimeZoneId,
            new NodaTimeEventWindowZones()));
        await EnsureAttendeeGroupsSeededAsync(context);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Repairs change-controlled Attendee Group rows after truncation. Truncating
    /// appointment_type cascades into the mapping table, so migration-seeded mappings are
    /// re-inserted when missing. Production databases rely on the migration alone.
    /// </summary>
    private static async Task EnsureAttendeeGroupsSeededAsync(EventBookingDbContext context)
    {
        var fixedGroups = new[]
        {
            AttendeeGroup.Define(
                AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.UniformFitting]),
            AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]),
            AttendeeGroup.Define(
                AttendeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT", "Ground Operations Agent", true,
                [AppointmentTypeIds.MedicalCheckUp]),
            AttendeeGroup.Define(
                AttendeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
                [AppointmentTypeIds.MedicalCheckUp]),
            AttendeeGroup.Define(
                AttendeeGroupIds.GroundTransportServices, "GROUND_TRANSPORT_SERVICES", "Ground Transport Services", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.UniformFitting]),
        };

        foreach (var group in fixedGroups)
        {
            var existing = await context.AttendeeGroups
                .Include(persisted => persisted.Requirements)
                .SingleOrDefaultAsync(persisted => persisted.Id == group.Id);
            if (existing is null)
            {
                context.AttendeeGroups.Add(group);
                continue;
            }

            foreach (var requirement in group.Requirements)
            {
                if (existing.Requirements.All(persisted => persisted.AppointmentTypeId != requirement.AppointmentTypeId))
                {
                    context.Add(requirement);
                }
            }
        }
    }
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
`````
