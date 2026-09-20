# 00b — Vocabulary edits 94 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs — 1/1

<!-- vocabulary-file: {"id":325,"oldPath":"tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs","beforeSha":"ca4e25949ea11f0f7634c36c69bc0e64f1c403fa5a66c618edce083e8543cc63","afterSha":"3f3857913363380f32bbf23acbb16de38031c0f0a9b7e3a9dd6eb1d63a138ce6","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Appointments;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Slots;
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
            var pilots = seed.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.Pilots);
            var candidate = Candidate.Create(
                Guid.NewGuid(), "Amara Novak", "amara@example.com", pilots);
            var slot = ConfirmedSlot.CreateImported(
                Guid.NewGuid(),
                new SlotWindow(new DateOnly(2026, 9, 7), new TimeOnly(9, 0)),
                AppointmentTypeIds.All.ToDictionary(value => value, _ => 10));
            var invite = Invite.CreateInitial(
                Guid.NewGuid(), candidate.Id, "invite-token", now.AddDays(1),
                [slot.Id, Guid.NewGuid(), Guid.NewGuid()], candidate.RequiredAppointmentTypeIds, 0);
            var booking = Booking.Create(
                Guid.NewGuid(), invite, slot.Id, "manage-token", now.AddDays(-1));
            var appointment = BookingAppointment.Create(
                Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting);
            appointmentId = appointment.Id;
            seed.Candidates.Add(candidate);
            seed.ConfirmedSlots.Add(slot);
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
                new CandidateRepository(context),
                new InviteRepository(context),
                new ConfirmedSlotRepository(context),
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
        Guid candidateId;
        Guid appointmentId;

        var ids = await GivenCorrectionRaceAsync(now, coordinator, appointmentStaff);
        candidateId = ids.CandidateId;
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
            invite.CandidateId == candidateId
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
                new CandidateRepository(context),
                new InviteRepository(context),
                new ConfirmedSlotRepository(context),
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
            var slots = new ConfirmedSlotRepository(context);
            var deliveries = new EmailDeliveryService(
                new EmailDeliveryRepository(context), new SilentSender(), unitOfWork, clock,
                NullLogger<EmailDeliveryService>.Instance);
            var issuer = new InviteIssuer(
                new InviteRepository(context),
                new EmployeeGroupRepository(context),
                new EligibleSlotFinder(slots, clock),
                new SystemSettingsRepository(context),
                tokens,
                deliveries,
                audit,
                clock,
                new CandidatePortalOptions(
                    "https://booking.example.com", "HQ", "recruitment@example.com"));
            var handler = new StartRecoveryHandler(
                new CandidateRepository(context),
                new StaffAccessAuthorizer(new StaffAccessProfileRepository(context)),
                new InviteRepository(context),
                new BookingRepository(context),
                new BookingAppointmentRepository(context),
                issuer,
                new EligibleSlotFinder(slots, clock),
                deliveries,
                unitOfWork);
            return await handler.HandleAsync(
                new StartRecoveryCommand(coordinator, candidateId), CancellationToken.None);
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
        Guid candidateId;
        Guid appointmentId;

        var ids = await GivenCorrectionRaceAsync(now, coordinator, appointmentStaff);
        candidateId = ids.CandidateId;
        appointmentId = ids.AppointmentId;

        await using var issueContext = fixture.NewContext();
        var issueResult = await IssueAsync(issueContext, now, coordinator, candidateId);
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
            invite.CandidateId == candidateId
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
                new CandidateRepository(context),
                new InviteRepository(context),
                new ConfirmedSlotRepository(context),
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
            Guid candidate)
        {
            var clock = new FixedClock(at);
            var unitOfWork = new UnitOfWork(context);
            var audit = new EfAuditLogger(context, clock);
            var tokens = new HmacTokenService(
                new TokenOptions("a-correction-race-signing-key-long-enough"));
            var slots = new ConfirmedSlotRepository(context);
            var deliveries = new EmailDeliveryService(
                new EmailDeliveryRepository(context), new SilentSender(), unitOfWork, clock,
                NullLogger<EmailDeliveryService>.Instance);
            var issuer = new InviteIssuer(
                new InviteRepository(context),
                new EmployeeGroupRepository(context),
                new EligibleSlotFinder(slots, clock),
                new SystemSettingsRepository(context),
                tokens,
                deliveries,
                audit,
                clock,
                new CandidatePortalOptions(
                    "https://booking.example.com", "HQ", "recruitment@example.com"));
            var handler = new StartRecoveryHandler(
                new CandidateRepository(context),
                new StaffAccessAuthorizer(new StaffAccessProfileRepository(context)),
                new InviteRepository(context),
                new BookingRepository(context),
                new BookingAppointmentRepository(context),
                issuer,
                new EligibleSlotFinder(slots, clock),
                deliveries,
                unitOfWork);
            return await handler.HandleAsync(
                new StartRecoveryCommand(staff, candidate), CancellationToken.None);
        }
    }

    private async Task<(Guid CandidateId, Guid AppointmentId)> GivenCorrectionRaceAsync(
        DateTimeOffset now,
        Guid coordinator,
        Guid appointmentStaff)
    {
        Guid candidateId;
        Guid appointmentId;

        await using (var seed = fixture.NewContext())
        {
            seed.StaffAccessProfiles.Add(StaffAccessProfile.Create(
                coordinator, [Role.Coordinator], null));
            seed.StaffAccessProfiles.Add(StaffAccessProfile.Create(
                appointmentStaff, [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting));
            var group = EmployeeGroup.Define(
                Guid.NewGuid(), $"DAT_ONLY_{Guid.NewGuid():N}".ToUpperInvariant(), "DAT only", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting]);
            var candidate = Candidate.Create(
                Guid.NewGuid(), "Amara Novak", "amara@example.com", group);
            candidate.MarkInvited();
            candidate.MarkBooked();
            candidateId = candidate.Id;
            var pastSlot = ConfirmedSlot.CreateImported(
                Guid.NewGuid(),
                new SlotWindow(new DateOnly(2026, 9, 7), new TimeOnly(9, 0)),
                AppointmentTypeIds.All.ToDictionary(value => value, _ => 10));
            var invite = Invite.CreateInitial(
                Guid.NewGuid(), candidate.Id, "invite-token", now.AddDays(1),
                [pastSlot.Id, Guid.NewGuid(), Guid.NewGuid()],
                candidate.RequiredAppointmentTypeIds, 0);
            var booking = Booking.Create(
                Guid.NewGuid(), invite, pastSlot.Id, "manage-token", now.AddDays(-1));
            invite.MarkUsed();
            var appointment = BookingAppointment.Create(
                Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting);
            appointment.TransitionTo(
                BookingAppointmentStatus.NoShow, appointmentStaff, now, false, true);
            appointmentId = appointment.Id;
            seed.Candidates.Add(candidate);
            seed.ConfirmedSlots.Add(pastSlot);
            foreach (var day in new[] { 30, 31, 32 })
            {
                seed.ConfirmedSlots.Add(ConfirmedSlot.CreateImported(
                    Guid.NewGuid(),
                    new SlotWindow(DateOnly.FromDateTime(now.DateTime).AddDays(day), new TimeOnly(9, 0)),
                    AppointmentTypeIds.All.ToDictionary(value => value, _ => 10)));
            }

            seed.Bookings.Add(booking);
            seed.BookingAppointments.Add(appointment);
            seed.EmployeeGroups.Add(group);
            await seed.SaveChangesAsync();
        }

        return (candidateId, appointmentId);
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
        public DateTimeOffset NowAtHeadOffice => now;
        /// <inheritdoc/>
        public DateOnly TodayAtHeadOffice => DateOnly.FromDateTime(now.DateTime);
        /// <inheritdoc/>
        public DateOnly DateAtHeadOffice(DateTimeOffset instant) =>
            DateOnly.FromDateTime(instant.DateTime);

        public DateTimeOffset InstantAtHeadOffice(DateTimeOffset instant) => instant;
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs — 1/1

<!-- vocabulary-file: {"id":325,"oldPath":"tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs","beforeSha":"ca4e25949ea11f0f7634c36c69bc0e64f1c403fa5a66c618edce083e8543cc63","afterSha":"3f3857913363380f32bbf23acbb16de38031c0f0a9b7e3a9dd6eb1d63a138ce6","side":"after","part":1,"parts":1} -->

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
                Guid.NewGuid(), "Amara Novak", "amara@example.com", pilots);
            var eventItem = Event.CreateImported(
                Guid.NewGuid(),
                new EventWindow(new DateOnly(2026, 9, 7), new TimeOnly(9, 0)),
                AppointmentTypeIds.All.ToDictionary(value => value, _ => 10));
            var invite = Invite.CreateInitial(
                Guid.NewGuid(), attendee.Id, "invite-token", now.AddDays(1),
                [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()], attendee.RequiredAppointmentTypeIds, 0);
            var booking = Booking.Create(
                Guid.NewGuid(), invite, eventItem.Id, "manage-token", now.AddDays(-1));
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
                    "https://booking.example.com", "HQ", "recruitment@example.com"));
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
                    "https://booking.example.com", "HQ", "recruitment@example.com"));
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
                Guid.NewGuid(), "Amara Novak", "amara@example.com", group);
            attendee.MarkInvited();
            attendee.MarkBooked();
            attendeeId = attendee.Id;
            var pastEvent = Event.CreateImported(
                Guid.NewGuid(),
                new EventWindow(new DateOnly(2026, 9, 7), new TimeOnly(9, 0)),
                AppointmentTypeIds.All.ToDictionary(value => value, _ => 10));
            var invite = Invite.CreateInitial(
                Guid.NewGuid(), attendee.Id, "invite-token", now.AddDays(1),
                [pastEvent.Id, Guid.NewGuid(), Guid.NewGuid()],
                attendee.RequiredAppointmentTypeIds, 0);
            var booking = Booking.Create(
                Guid.NewGuid(), invite, pastEvent.Id, "manage-token", now.AddDays(-1));
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
                seed.Events.Add(Event.CreateImported(
                    Guid.NewGuid(),
                    new EventWindow(DateOnly.FromDateTime(now.DateTime).AddDays(day), new TimeOnly(9, 0)),
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

## before — tests/EventBooking.Infrastructure.Tests/BookingAppointmentPersistenceTests.cs — 1/1

<!-- vocabulary-file: {"id":326,"oldPath":"tests/EventBooking.Infrastructure.Tests/BookingAppointmentPersistenceTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/BookingAppointmentPersistenceTests.cs","beforeSha":"8ace6bbf9de6109d417a07baed3ce10c76c404d5d0c5112a593dde285c558f5e","afterSha":"83dc9eff6583c9089f959082c22897359b199773ac70d8f2ecf6f95d192d6653","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies booking-appointment relational state and active-booking migration backfill.</summary>
[Collection("postgres")]
public sealed class BookingAppointmentPersistenceTests(PostgresFixture fixture)
{
    /// <summary>The last migration before Release 2 closed legacy reconciliation.</summary>
    private const string ReleaseOneMigration = "20260909120000_AddRecoveryBookings";

    /// <summary>Verifies every ontology field round-trips through the EF mapping.</summary>
    [Fact]
    public async Task AppointmentRoundTripsWithOperationalStateAndVersion()
    {
        await fixture.ResetAsync();
        var booking = NewBooking(Guid.NewGuid(), Guid.NewGuid());
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting);
        var staff = Guid.NewGuid();
        var checkedInAt = new DateTimeOffset(2026, 9, 7, 8, 55, 0, TimeSpan.Zero);
        appointment.TransitionTo(
            BookingAppointmentStatus.CheckedIn, staff, checkedInAt, true, false);

        await using (var write = fixture.NewContext())
        {
            write.Bookings.Add(booking);
            write.BookingAppointments.Add(appointment);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var loaded = await read.BookingAppointments.AsNoTracking().SingleAsync();

        Assert.Equal(booking.Id, loaded.BookingId);
        Assert.Equal(AppointmentTypeIds.DrugAndAlcoholTesting, loaded.AppointmentTypeId);
        Assert.Equal(BookingAppointmentStatus.CheckedIn, loaded.Status);
        Assert.Equal(checkedInAt, loaded.CheckedInAt);
        Assert.Null(loaded.OutcomeAt);
        Assert.Equal(staff, loaded.LastChangedByStaffUserId);
        Assert.Equal(checkedInAt, loaded.LastChangedAt);
        Assert.Equal(2, loaded.Version);
    }

    /// <summary>Verifies one booking cannot acquire duplicate records for one appointment type.</summary>
    [Fact]
    public async Task BookingAndAppointmentTypePairIsUnique()
    {
        await fixture.ResetAsync();
        var booking = NewBooking(Guid.NewGuid(), Guid.NewGuid());

        await using var context = fixture.NewContext();
        context.Bookings.Add(booking);
        context.BookingAppointments.AddRange(
            BookingAppointment.Create(
                Guid.NewGuid(), booking.Id, AppointmentTypeIds.MedicalCheckUp),
            BookingAppointment.Create(
                Guid.NewGuid(), booking.Id, AppointmentTypeIds.MedicalCheckUp));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    /// <summary>Verifies locator and row lock always include the trusted appointment-type scope.</summary>
    [Fact]
    public async Task RepositoryCannotLocateOrLockAnotherAppointmentType()
    {
        await fixture.ResetAsync();
        var booking = NewBooking(Guid.NewGuid(), Guid.NewGuid());
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.UniformFitting);

        await using (var write = fixture.NewContext())
        {
            write.Bookings.Add(booking);
            write.BookingAppointments.Add(appointment);
            await write.SaveChangesAsync();
        }

        await using var context = fixture.NewContext();
        var repository = new BookingAppointmentRepository(context);

        Assert.Null(await repository.FindLocatorInScopeAsync(
            appointment.Id,
            AppointmentTypeIds.MedicalCheckUp,
            CancellationToken.None));

        await using var transaction = await context.Database.BeginTransactionAsync();
        Assert.Null(await repository.LockForUpdateAsync(
            appointment.Id,
            AppointmentTypeIds.MedicalCheckUp,
            CancellationToken.None));
    }

    /// <summary>Verifies migration creates Expected rows for active but not cancelled bookings.</summary>
    [Fact]
    public async Task MigrationBackfillsOnlyActiveBookingRequirementPairs()
    {
        var databaseName = $"eventbooking_appointment_backfill_{Guid.NewGuid():N}";
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Database = databaseName,
            Pooling = false,
        }.ConnectionString;

        try
        {
            await CreateDatabaseAsync(databaseName);
            await using (var discovery = NewContext(connectionString))
            {
                var migrations = discovery.Database.GetMigrations().ToList();
                var targetIndex = migrations.FindIndex(name =>
                    name.EndsWith("_AddBookingAppointments", StringComparison.Ordinal));
                Assert.True(targetIndex > 0, "AddBookingAppointments migration was not found.");
                await discovery.Database.MigrateAsync(migrations[targetIndex - 1]);
            }

            var activeCandidate = Guid.NewGuid();
            var cancelledCandidate = Guid.NewGuid();
            var activeBooking = Guid.NewGuid();
            var cancelledBooking = Guid.NewGuid();
            await using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    INSERT INTO candidate (id, name, email, status, status_changed_at)
                    VALUES
                      (@active_candidate, 'Active Candidate', 'active@example.com', 4, now()),
                      (@cancelled_candidate, 'Cancelled Candidate', 'cancelled@example.com', 4, now());
                    INSERT INTO candidate_requirement (candidate_id, appointment_type_id)
                    VALUES
                      (@active_candidate, @dat),
                      (@active_candidate, @med),
                      (@cancelled_candidate, @dat);
                    INSERT INTO booking
                      (id, candidate_id, confirmed_slot_id, invite_id, created_at, status, manage_token_hash)
                    VALUES
                      (@active_booking, @active_candidate, @slot, @invite_one, now(), 1, 'active-token'),
                      (@cancelled_booking, @cancelled_candidate, @slot, @invite_two, now(), 2, 'cancelled-token');
                    """;
                command.Parameters.AddWithValue("active_candidate", activeCandidate);
                command.Parameters.AddWithValue("cancelled_candidate", cancelledCandidate);
                command.Parameters.AddWithValue("active_booking", activeBooking);
                command.Parameters.AddWithValue("cancelled_booking", cancelledBooking);
                command.Parameters.AddWithValue("slot", Guid.NewGuid());
                command.Parameters.AddWithValue("invite_one", Guid.NewGuid());
                command.Parameters.AddWithValue("invite_two", Guid.NewGuid());
                command.Parameters.AddWithValue("dat", AppointmentTypeIds.DrugAndAlcoholTesting);
                command.Parameters.AddWithValue("med", AppointmentTypeIds.MedicalCheckUp);
                await command.ExecuteNonQueryAsync();
            }

            await using (var latest = NewContext(connectionString))
            {
                await latest.Database.MigrateAsync(ReleaseOneMigration);
            }

            await using var read = NewContext(connectionString);
            var rows = await read.BookingAppointments.AsNoTracking().ToListAsync();
            Assert.Equal(2, rows.Count);
            Assert.All(rows, row => Assert.Equal(activeBooking, row.BookingId));
            Assert.All(rows, row => Assert.Equal(BookingAppointmentStatus.Expected, row.Status));
            Assert.All(rows, row => Assert.Equal(1, row.Version));
        }
        finally
        {
            await DropDatabaseAsync(databaseName);
        }
    }

    /// <summary>Creates a booking for persistence tests.</summary>
    /// <param name="candidateId">The candidate identifier.</param>
    /// <param name="slotId">The confirmed-slot identifier.</param>
    /// <returns>A new active booking.</returns>
    private static Booking NewBooking(Guid candidateId, Guid slotId)
    {
        var invite = Domain.Invites.Invite.CreateInitial(
            Guid.NewGuid(),
            candidateId,
            "invite-token-hash",
            DateTimeOffset.UtcNow.AddDays(1),
            [slotId, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting],
            0);
        return Booking.Create(
            Guid.NewGuid(), invite, slotId, "manage-token-hash", DateTimeOffset.UtcNow);
    }

    /// <summary>Creates a context against the supplied connection string.</summary>
    /// <param name="connectionString">The Npgsql connection string.</param>
    /// <returns>A new database context.</returns>
    private static EventBookingDbContext NewContext(string connectionString) =>
        new(new DbContextOptionsBuilder<EventBookingDbContext>()
            .UseNpgsql(connectionString)
            .Options);

    /// <summary>Creates a scratch database for the backfill test.</summary>
    /// <param name="databaseName">The database name.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task CreateDatabaseAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
            {
                Database = "postgres",
                Pooling = false,
            }.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE {databaseName};";
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Drops the scratch database for the backfill test.</summary>
    /// <param name="databaseName">The database name.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task DropDatabaseAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
            {
                Database = "postgres",
                Pooling = false,
            }.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS {databaseName};";
        await command.ExecuteNonQueryAsync();
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/BookingAppointmentPersistenceTests.cs — 1/1

<!-- vocabulary-file: {"id":326,"oldPath":"tests/EventBooking.Infrastructure.Tests/BookingAppointmentPersistenceTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/BookingAppointmentPersistenceTests.cs","beforeSha":"8ace6bbf9de6109d417a07baed3ce10c76c404d5d0c5112a593dde285c558f5e","afterSha":"83dc9eff6583c9089f959082c22897359b199773ac70d8f2ecf6f95d192d6653","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies booking-appointment relational state and active-booking migration backfill.</summary>
[Collection("postgres")]
public sealed class BookingAppointmentPersistenceTests(PostgresFixture fixture)
{
    /// <summary>The last migration before Release 2 closed legacy reconciliation.</summary>
    private const string ReleaseOneMigration = "20260909120000_AddRecoveryBookings";

    /// <summary>Verifies every ontology field round-trips through the EF mapping.</summary>
    [Fact]
    public async Task AppointmentRoundTripsWithOperationalStateAndVersion()
    {
        await fixture.ResetAsync();
        var booking = NewBooking(Guid.NewGuid(), Guid.NewGuid());
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting);
        var staff = Guid.NewGuid();
        var checkedInAt = new DateTimeOffset(2026, 9, 7, 8, 55, 0, TimeSpan.Zero);
        appointment.TransitionTo(
            BookingAppointmentStatus.CheckedIn, staff, checkedInAt, true, false);

        await using (var write = fixture.NewContext())
        {
            write.Bookings.Add(booking);
            write.BookingAppointments.Add(appointment);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var loaded = await read.BookingAppointments.AsNoTracking().SingleAsync();

        Assert.Equal(booking.Id, loaded.BookingId);
        Assert.Equal(AppointmentTypeIds.DrugAndAlcoholTesting, loaded.AppointmentTypeId);
        Assert.Equal(BookingAppointmentStatus.CheckedIn, loaded.Status);
        Assert.Equal(checkedInAt, loaded.CheckedInAt);
        Assert.Null(loaded.OutcomeAt);
        Assert.Equal(staff, loaded.LastChangedByStaffUserId);
        Assert.Equal(checkedInAt, loaded.LastChangedAt);
        Assert.Equal(2, loaded.Version);
    }

    /// <summary>Verifies one booking cannot acquire duplicate records for one appointment type.</summary>
    [Fact]
    public async Task BookingAndAppointmentTypePairIsUnique()
    {
        await fixture.ResetAsync();
        var booking = NewBooking(Guid.NewGuid(), Guid.NewGuid());

        await using var context = fixture.NewContext();
        context.Bookings.Add(booking);
        context.BookingAppointments.AddRange(
            BookingAppointment.Create(
                Guid.NewGuid(), booking.Id, AppointmentTypeIds.MedicalCheckUp),
            BookingAppointment.Create(
                Guid.NewGuid(), booking.Id, AppointmentTypeIds.MedicalCheckUp));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    /// <summary>Verifies locator and row lock always include the trusted appointment-type scope.</summary>
    [Fact]
    public async Task RepositoryCannotLocateOrLockAnotherAppointmentType()
    {
        await fixture.ResetAsync();
        var booking = NewBooking(Guid.NewGuid(), Guid.NewGuid());
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.UniformFitting);

        await using (var write = fixture.NewContext())
        {
            write.Bookings.Add(booking);
            write.BookingAppointments.Add(appointment);
            await write.SaveChangesAsync();
        }

        await using var context = fixture.NewContext();
        var repository = new BookingAppointmentRepository(context);

        Assert.Null(await repository.FindLocatorInScopeAsync(
            appointment.Id,
            AppointmentTypeIds.MedicalCheckUp,
            CancellationToken.None));

        await using var transaction = await context.Database.BeginTransactionAsync();
        Assert.Null(await repository.LockForUpdateAsync(
            appointment.Id,
            AppointmentTypeIds.MedicalCheckUp,
            CancellationToken.None));
    }

    /// <summary>Verifies migration creates Expected rows for active but not cancelled bookings.</summary>
    [Fact]
    public async Task MigrationBackfillsOnlyActiveBookingRequirementPairs()
    {
        var databaseName = $"eventbooking_appointment_backfill_{Guid.NewGuid():N}";
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Database = databaseName,
            Pooling = false,
        }.ConnectionString;

        try
        {
            await CreateDatabaseAsync(databaseName);
            await using (var discovery = NewContext(connectionString))
            {
                var migrations = discovery.Database.GetMigrations().ToList();
                var targetIndex = migrations.FindIndex(name =>
                    name.EndsWith("_AddBookingAppointments", StringComparison.Ordinal));
                Assert.True(targetIndex > 0, "AddBookingAppointments migration was not found.");
                await discovery.Database.MigrateAsync(migrations[targetIndex - 1]);
            }

            var activeAttendee = Guid.NewGuid();
            var cancelledAttendee = Guid.NewGuid();
            var activeBooking = Guid.NewGuid();
            var cancelledBooking = Guid.NewGuid();
            await using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    INSERT INTO attendee (id, name, email, status, status_changed_at)
                    VALUES
                      (@active_attendee, 'Active Attendee', 'active@example.com', 4, now()),
                      (@cancelled_attendee, 'Cancelled Attendee', 'cancelled@example.com', 4, now());
                    INSERT INTO attendee_requirement (attendee_id, appointment_type_id)
                    VALUES
                      (@active_attendee, @dat),
                      (@active_attendee, @med),
                      (@cancelled_attendee, @dat);
                    INSERT INTO booking
                      (id, attendee_id, event_id, invite_id, created_at, status, manage_token_hash)
                    VALUES
                      (@active_booking, @active_attendee, @eventItem, @invite_one, now(), 1, 'active-token'),
                      (@cancelled_booking, @cancelled_attendee, @eventItem, @invite_two, now(), 2, 'cancelled-token');
                    """;
                command.Parameters.AddWithValue("active_attendee", activeAttendee);
                command.Parameters.AddWithValue("cancelled_attendee", cancelledAttendee);
                command.Parameters.AddWithValue("active_booking", activeBooking);
                command.Parameters.AddWithValue("cancelled_booking", cancelledBooking);
                command.Parameters.AddWithValue("eventItem", Guid.NewGuid());
                command.Parameters.AddWithValue("invite_one", Guid.NewGuid());
                command.Parameters.AddWithValue("invite_two", Guid.NewGuid());
                command.Parameters.AddWithValue("dat", AppointmentTypeIds.DrugAndAlcoholTesting);
                command.Parameters.AddWithValue("med", AppointmentTypeIds.MedicalCheckUp);
                await command.ExecuteNonQueryAsync();
            }

            await using (var latest = NewContext(connectionString))
            {
                await latest.Database.MigrateAsync(ReleaseOneMigration);
            }

            await using var read = NewContext(connectionString);
            var rows = await read.BookingAppointments.AsNoTracking().ToListAsync();
            Assert.Equal(2, rows.Count);
            Assert.All(rows, row => Assert.Equal(activeBooking, row.BookingId));
            Assert.All(rows, row => Assert.Equal(BookingAppointmentStatus.Expected, row.Status));
            Assert.All(rows, row => Assert.Equal(1, row.Version));
        }
        finally
        {
            await DropDatabaseAsync(databaseName);
        }
    }

    /// <summary>Creates a booking for persistence tests.</summary>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="eventId">The event identifier.</param>
    /// <returns>A new active booking.</returns>
    private static Booking NewBooking(Guid attendeeId, Guid eventId)
    {
        var invite = Domain.Invites.Invite.CreateInitial(
            Guid.NewGuid(),
            attendeeId,
            "invite-token-hash",
            DateTimeOffset.UtcNow.AddDays(1),
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting],
            0);
        return Booking.Create(
            Guid.NewGuid(), invite, eventId, "manage-token-hash", DateTimeOffset.UtcNow);
    }

    /// <summary>Creates a context against the supplied connection string.</summary>
    /// <param name="connectionString">The Npgsql connection string.</param>
    /// <returns>A new database context.</returns>
    private static EventBookingDbContext NewContext(string connectionString) =>
        new(new DbContextOptionsBuilder<EventBookingDbContext>()
            .UseNpgsql(connectionString)
            .Options);

    /// <summary>Creates a scratch database for the backfill test.</summary>
    /// <param name="databaseName">The database name.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task CreateDatabaseAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
            {
                Database = "postgres",
                Pooling = false,
            }.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE {databaseName};";
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Drops the scratch database for the backfill test.</summary>
    /// <param name="databaseName">The database name.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task DropDatabaseAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
            {
                Database = "postgres",
                Pooling = false,
            }.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS {databaseName};";
        await command.ExecuteNonQueryAsync();
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/CandidateBookingCancellationPersistenceTests.cs — 1/1

<!-- vocabulary-file: {"id":327,"oldPath":"tests/EventBooking.Infrastructure.Tests/CandidateBookingCancellationPersistenceTests.cs","newPath":"tests/EventBooking.Infrastructure.Tests/AttendeeBookingCancellationPersistenceTests.cs","beforeSha":"74073de631f622b4f878667d6f01af2c50fe545547d8a7391bb1342a95a1e18a","afterSha":"f4186ec3fe2b369eeb65759c4e2b4cfbe21d49ff75899d213857df84b8c4309a","side":"before","part":1,"parts":1} -->

`````csharp
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
`````
