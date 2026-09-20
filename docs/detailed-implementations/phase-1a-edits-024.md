# 01a — Variable-length windows in the location's zone, edits 24 (Task 4)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs — 1/1

<!-- retirement-file: {"id":72,"file":"tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs","beforeSha":"50028bb606e158e5cbf269be0cdc101b6bb928ef0ab5db9e06fdb8ed26d963a3","afterSha":"f6477b154810e6828dcffc6714027501357288175fdb2d5c9d074a1e1053b23a","side":"after","part":1,"parts":1} -->

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
            var eventItem = EventFixture.Create(
                Guid.NewGuid(),
                new EventWindow(new DateOnly(2026, 9, 7), new TimeOnly(9, 0), 240),
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
                Guid.NewGuid(), "Amara Novak", "amara@example.com", group);
            attendee.MarkInvited();
            attendee.MarkBooked();
            attendeeId = attendee.Id;
            var pastEvent = EventFixture.Create(
                Guid.NewGuid(),
                new EventWindow(new DateOnly(2026, 9, 7), new TimeOnly(9, 0), 240),
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

## before — tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyHarness.cs — 1/1

<!-- retirement-file: {"id":73,"file":"tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyHarness.cs","beforeSha":"983bda091b89de0706a6bb4c87b46d4277389236459af15ec0489f0b0032c336","afterSha":"40fab7833e14acdfb7277ddfe21a18c4dfc5aae928254d25f204f3bad79524a1","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyHarness.cs — 1/1

<!-- retirement-file: {"id":73,"file":"tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyHarness.cs","beforeSha":"983bda091b89de0706a6bb4c87b46d4277389236459af15ec0489f0b0032c336","afterSha":"40fab7833e14acdfb7277ddfe21a18c4dfc5aae928254d25f204f3bad79524a1","side":"after","part":1,"parts":1} -->

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
            new EventWindow(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), new TimeOnly(9, 0), 240),
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

## before — tests/EventBooking.Infrastructure.Tests/ConcurrencyHarness.cs — 1/1

<!-- retirement-file: {"id":74,"file":"tests/EventBooking.Infrastructure.Tests/ConcurrencyHarness.cs","beforeSha":"cf7e37417f8556354d7089af487c58aa32907886426aa5861ef250e78bef2b8c","afterSha":"da291e1860c98ad6026f1e094848f3564d4a72adefe743e80d9a34389b8cdf79","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Common;
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
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
    private IReadOnlyList<Guid> _fallbackEventIds = [];
    private int _nextEventOffset;

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
    /// The two genuine high-capacity options offered beside every event under contention. They are
    /// deliberately kept alive so a losing invite remains valid while the proof runs.
    /// </summary>
    public IReadOnlyList<Guid> FallbackEventIds => _fallbackEventIds;

    public static async Task<ConcurrencyHarness> CreateAsync(PostgresFixture fixture)
    {
        await fixture.ResetAsync();

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddEventBookingInfrastructure(
            fixture.ConnectionString,
            new ClockOptions("Europe/London"),
            new TokenOptions("a-concurrency-test-signing-key-long-enough"));

        services.AddEventBookingApplication(
            new AttendeePortalOptions("https://booking.example.com", "recruitment@example.com"));

        // Nothing registers IEmailTransport above any more — AddEventBookingInfrastructure no
        // longer does that itself, and this harness never calls AddAwsEmailTransport or
        // AddLocalEmailTransport, since email delivery is not what this proof is testing.
        services.AddScoped<IEmailTransport, SilentTransport>();

        var harness = new ConcurrencyHarness(fixture, services.BuildServiceProvider());
        harness._fallbackEventIds =
        [
            await harness.GivenEventAsync(drugAndAlcohol: 100, medical: 100, uniform: 100),
            await harness.GivenEventAsync(drugAndAlcohol: 100, medical: 100, uniform: 100),
        ];

        return harness;
    }

    public async Task<Guid> GivenEventAsync(int drugAndAlcohol, int medical, int uniform)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(
                DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30 + Interlocked.Increment(ref _nextEventOffset)),
                new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), drugAndAlcohol);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), medical);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), uniform);

        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        await using var context = _fixture.NewContext();
        context.EventProposals.Add(proposal);
        context.Events.Add(eventItem);
        await context.SaveChangesAsync();

        return eventItem.Id;
    }

    /// <summary>Opens an independent scope with its own connection for one racer.</summary>
    public IServiceScope CreateScope() => _services.CreateScope();

    public async Task<string> GivenInvitedAttendeeAsync(Guid eventId, params Guid[] requiredTypeIds)
    {
        var tokens = _services.GetRequiredService<ITokenService>();

        var group = AttendeeGroup.Define(
            Guid.NewGuid(), $"HARNESS_{Guid.NewGuid():N}".ToUpperInvariant(), "Harness", true,
            requiredTypeIds);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Concurrent Attendee", $"{Guid.NewGuid():N}@mail.com", group);
        attendee.MarkInvited();

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(inviteId);

        // Every invite is real: the fallback events remain valid options if the contested one fills.
        var invite = Invite.CreateInitial(
            inviteId,
            attendee.Id,
            issued.TokenHash,
            DateTimeOffset.UtcNow.AddDays(4),
            [eventId, .. _fallbackEventIds],
            requiredTypeIds,
            0);

        await using var context = _fixture.NewContext();
        context.AttendeeGroups.Add(group);
        context.Attendees.Add(attendee);
        context.Invites.Add(invite);
        await context.SaveChangesAsync();

        return issued.Token;
    }

    public async Task<Result<ConfirmBookingOutcome>> ConfirmAsync(string token, Guid eventId)
    {
        // A scope per attempt: separate context, separate connection, separate transaction.
        await using var scope = _services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ConfirmBookingHandler>();

        return await handler.HandleAsync(
            new ConfirmBookingCommand(token, eventId), CancellationToken.None);
    }

    /// <summary>
    /// Starts every confirmation only after a real external transaction has acquired the target
    /// event's row lock. Every production handler consequently waits on PostgreSQL before the
    /// guard commits, proving that the work overlaps rather than being merely scheduled together.
    /// </summary>
    public async Task<ConfirmationBatch> ConfirmBatchAsync(
        IReadOnlyCollection<string> tokens,
        Guid eventId)
    {
        var tokenList = tokens.ToArray();
        if (tokenList.Length == 0)
        {
            throw new ArgumentException("At least one confirmation is required.", nameof(tokens));
        }

        await using var guardContext = _fixture.NewContext();
        await using var guardTransaction = await guardContext.Database.BeginTransactionAsync();
        var lockedEvent = await new EventRepository(guardContext)
            .LockForUpdateAsync(eventId, CancellationToken.None);
        if (lockedEvent is null)
        {
            throw new InvalidOperationException("The batch target event does not exist.");
        }

        var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readiness = tokenList
            .Select(_ => new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously))
            .ToArray();
        var attempts = tokenList
            .Select((token, index) => ConfirmAfterGateAsync(token, eventId, startGate.Task, readiness[index]))
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
    /// active events with spare capacity for the attendee's required appointment types.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> LiveOptionEventIdsAsync(string token)
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

        var optionIds = invite.OfferedEventIds;
        if (optionIds.Count != Invite.RequiredOptionCount || optionIds.Distinct().Count() != optionIds.Count)
        {
            throw new InvalidOperationException("A live invite must retain three distinct options.");
        }

        var attendee = await context.Attendees
            .Include(c => c.Requirements)
            .SingleAsync(c => c.Id == invite.AttendeeId);
        var events = await context.Events
            .Include(s => s.Capacities)
            .Where(s => optionIds.Contains(s.Id))
            .ToListAsync();

        if (events.Count != optionIds.Count
            || events.Any(eventItem => eventItem.Status != EventStatus.Active)
            || events.Any(eventItem => !eventItem.HasSpareCapacityForAll(attendee.RequiredAppointmentTypeIds)))
        {
            throw new InvalidOperationException("Every invite option must be a live eligible eventItem.");
        }

        return optionIds;
    }

    public async Task<int> RemainingCapacityAsync(Guid eventId, Guid appointmentTypeId)
    {
        await using var context = _fixture.NewContext();
        var eventItem = await context.Events
            .Include(s => s.Capacities)
            .SingleAsync(s => s.Id == eventId);

        return eventItem.CapacityFor(appointmentTypeId).RemainingCapacity;
    }

    public async Task<int> ActiveBookingCountAsync(Guid eventId)
    {
        await using var context = _fixture.NewContext();
        return await context.Bookings.CountAsync(
            b => b.EventId == eventId && b.Status == BookingStatus.Active);
    }

    private async Task<Result<ConfirmBookingOutcome>> ConfirmAfterGateAsync(
        string token,
        Guid eventId,
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
                new ConfirmBookingCommand(token, eventId), CancellationToken.None);
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

## after — tests/EventBooking.Infrastructure.Tests/ConcurrencyHarness.cs — 1/1

<!-- retirement-file: {"id":74,"file":"tests/EventBooking.Infrastructure.Tests/ConcurrencyHarness.cs","beforeSha":"cf7e37417f8556354d7089af487c58aa32907886426aa5861ef250e78bef2b8c","afterSha":"da291e1860c98ad6026f1e094848f3564d4a72adefe743e80d9a34389b8cdf79","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Common;
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
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
    private IReadOnlyList<Guid> _fallbackEventIds = [];
    private int _nextEventOffset;

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
    /// The two genuine high-capacity options offered beside every event under contention. They are
    /// deliberately kept alive so a losing invite remains valid while the proof runs.
    /// </summary>
    public IReadOnlyList<Guid> FallbackEventIds => _fallbackEventIds;

    public static async Task<ConcurrencyHarness> CreateAsync(PostgresFixture fixture)
    {
        await fixture.ResetAsync();

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddEventBookingInfrastructure(
            fixture.ConnectionString,
            new ClockOptions("Europe/London"),
            new TokenOptions("a-concurrency-test-signing-key-long-enough"));

        services.AddEventBookingApplication(
            new AttendeePortalOptions("https://booking.example.com", "recruitment@example.com"));

        // Nothing registers IEmailTransport above any more — AddEventBookingInfrastructure no
        // longer does that itself, and this harness never calls AddAwsEmailTransport or
        // AddLocalEmailTransport, since email delivery is not what this proof is testing.
        services.AddScoped<IEmailTransport, SilentTransport>();

        var harness = new ConcurrencyHarness(fixture, services.BuildServiceProvider());
        harness._fallbackEventIds =
        [
            await harness.GivenEventAsync(drugAndAlcohol: 100, medical: 100, uniform: 100),
            await harness.GivenEventAsync(drugAndAlcohol: 100, medical: 100, uniform: 100),
        ];

        return harness;
    }

    public async Task<Guid> GivenEventAsync(int drugAndAlcohol, int medical, int uniform)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(
                DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30 + Interlocked.Increment(ref _nextEventOffset)),
                new TimeOnly(9, 0), 240),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), drugAndAlcohol);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), medical);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), uniform);

        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        await using var context = _fixture.NewContext();
        context.EventProposals.Add(proposal);
        context.Events.Add(eventItem);
        await context.SaveChangesAsync();

        return eventItem.Id;
    }

    /// <summary>Opens an independent scope with its own connection for one racer.</summary>
    public IServiceScope CreateScope() => _services.CreateScope();

    public async Task<string> GivenInvitedAttendeeAsync(Guid eventId, params Guid[] requiredTypeIds)
    {
        var tokens = _services.GetRequiredService<ITokenService>();

        var group = AttendeeGroup.Define(
            Guid.NewGuid(), $"HARNESS_{Guid.NewGuid():N}".ToUpperInvariant(), "Harness", true,
            requiredTypeIds);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Concurrent Attendee", $"{Guid.NewGuid():N}@mail.com", group);
        attendee.MarkInvited();

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(inviteId);

        // Every invite is real: the fallback events remain valid options if the contested one fills.
        var invite = Invite.CreateInitial(
            inviteId,
            attendee.Id,
            issued.TokenHash,
            DateTimeOffset.UtcNow.AddDays(4),
            [eventId, .. _fallbackEventIds],
            requiredTypeIds,
            0);

        await using var context = _fixture.NewContext();
        context.AttendeeGroups.Add(group);
        context.Attendees.Add(attendee);
        context.Invites.Add(invite);
        await context.SaveChangesAsync();

        return issued.Token;
    }

    public async Task<Result<ConfirmBookingOutcome>> ConfirmAsync(string token, Guid eventId)
    {
        // A scope per attempt: separate context, separate connection, separate transaction.
        await using var scope = _services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ConfirmBookingHandler>();

        return await handler.HandleAsync(
            new ConfirmBookingCommand(token, eventId), CancellationToken.None);
    }

    /// <summary>
    /// Starts every confirmation only after a real external transaction has acquired the target
    /// event's row lock. Every production handler consequently waits on PostgreSQL before the
    /// guard commits, proving that the work overlaps rather than being merely scheduled together.
    /// </summary>
    public async Task<ConfirmationBatch> ConfirmBatchAsync(
        IReadOnlyCollection<string> tokens,
        Guid eventId)
    {
        var tokenList = tokens.ToArray();
        if (tokenList.Length == 0)
        {
            throw new ArgumentException("At least one confirmation is required.", nameof(tokens));
        }

        await using var guardContext = _fixture.NewContext();
        await using var guardTransaction = await guardContext.Database.BeginTransactionAsync();
        var lockedEvent = await new EventRepository(guardContext)
            .LockForUpdateAsync(eventId, CancellationToken.None);
        if (lockedEvent is null)
        {
            throw new InvalidOperationException("The batch target event does not exist.");
        }

        var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readiness = tokenList
            .Select(_ => new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously))
            .ToArray();
        var attempts = tokenList
            .Select((token, index) => ConfirmAfterGateAsync(token, eventId, startGate.Task, readiness[index]))
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
    /// active events with spare capacity for the attendee's required appointment types.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> LiveOptionEventIdsAsync(string token)
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

        var optionIds = invite.OfferedEventIds;
        if (optionIds.Count != Invite.RequiredOptionCount || optionIds.Distinct().Count() != optionIds.Count)
        {
            throw new InvalidOperationException("A live invite must retain three distinct options.");
        }

        var attendee = await context.Attendees
            .Include(c => c.Requirements)
            .SingleAsync(c => c.Id == invite.AttendeeId);
        var events = await context.Events
            .Include(s => s.Capacities)
            .Where(s => optionIds.Contains(s.Id))
            .ToListAsync();

        if (events.Count != optionIds.Count
            || events.Any(eventItem => eventItem.Status != EventStatus.Active)
            || events.Any(eventItem => !eventItem.HasSpareCapacityForAll(attendee.RequiredAppointmentTypeIds)))
        {
            throw new InvalidOperationException("Every invite option must be a live eligible eventItem.");
        }

        return optionIds;
    }

    public async Task<int> RemainingCapacityAsync(Guid eventId, Guid appointmentTypeId)
    {
        await using var context = _fixture.NewContext();
        var eventItem = await context.Events
            .Include(s => s.Capacities)
            .SingleAsync(s => s.Id == eventId);

        return eventItem.CapacityFor(appointmentTypeId).RemainingCapacity;
    }

    public async Task<int> ActiveBookingCountAsync(Guid eventId)
    {
        await using var context = _fixture.NewContext();
        return await context.Bookings.CountAsync(
            b => b.EventId == eventId && b.Status == BookingStatus.Active);
    }

    private async Task<Result<ConfirmBookingOutcome>> ConfirmAfterGateAsync(
        string token,
        Guid eventId,
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
                new ConfirmBookingCommand(token, eventId), CancellationToken.None);
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
