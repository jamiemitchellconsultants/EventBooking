# 02d — The invite eligibility query, and the start instant it orders on, edits 16 (Task 11)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.Infrastructure.Tests/RecoveryConcurrencyTests.cs — 1/1

<!-- retirement-file: {"id":34,"file":"tests/EventBooking.Infrastructure.Tests/RecoveryConcurrencyTests.cs","beforeSha":"d4a98dcfe4c854d7bd41fe34edbfa960b07bd4aaab8e4182c1f3cce771e01375","afterSha":"4205f862f0db3f5064ea75171876ec1eca7b0bef6641872325e3931104a46661","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Infrastructure.Tests;

/// <summary>
/// Races a recovery confirmation against an original-booking cancellation on PostgreSQL.
/// Row locks serialize the pair; the test proves both serialized outcomes leave no orphan
/// Booking, no duplicated capacity movement, and the exact lifecycle lock trace per racer.
/// </summary>
[Collection("postgres")]
public sealed class RecoveryConcurrencyTests(PostgresFixture fixture)
{
    /// <summary>
    /// Whichever racer commits first wins: a confirmed recovery is then cancelled with the
    /// whole journey, while a cancelled original leaves the recovery invite superseded. Both
    /// outcomes restore every capacity row and keep the attendee lifecycle consistent.
    /// </summary>
    [Fact]
    public async Task RecoveryConfirmationRacingOriginalCancellationSerializes()
    {
        await using var harness = await ConcurrencyHarness.CreateAsync(fixture);
        var race = await GivenRecoveryRaceAsync(harness);
        using var confirmScope = harness.CreateScope();
        using var cancelScope = harness.CreateScope();
        var confirmTrace = new List<string>();
        var cancelTrace = new List<string>();
        var confirmHandler = BuildConfirmHandler(confirmScope.ServiceProvider, confirmTrace);
        var cancelHandler = BuildCancelHandler(cancelScope.ServiceProvider, cancelTrace);

        var confirmTask = confirmHandler.HandleAsync(
            new ConfirmBookingCommand(race.RecoveryToken, race.RecoveryEventId), CancellationToken.None);
        var cancelTask = cancelHandler.HandleAsync(
            new CancelBookingCommand(race.ManageToken, false), CancellationToken.None);
        await Task.WhenAll(confirmTask, cancelTask);

        var confirmResult = await confirmTask;
        var cancelResult = await cancelTask;
        Assert.True(cancelResult.IsSuccess);

        await using var verify = fixture.NewContext();
        var bookings = await verify.Bookings.AsNoTracking().ToListAsync();
        var invites = await verify.Invites.AsNoTracking().ToListAsync();
        var recoveryInvite = Assert.Single(invites, i => i.RecoveryOfBookingId == race.OriginalId);

        var ids = bookings.Select(b => b.Id).ToHashSet();
        Assert.All(
            bookings.Where(b => b.RecoveryOfBookingId.HasValue),
            b => Assert.Contains(b.RecoveryOfBookingId!.Value, ids));

        if (confirmResult.IsSuccess)
        {
            var recovery = Assert.Single(bookings, b => b.RecoveryOfBookingId == race.OriginalId);
            Assert.Equal(BookingStatus.Cancelled, recovery.Status);
            Assert.Equal(BookingStatus.Cancelled, bookings.Single(b => b.Id == race.OriginalId).Status);
            Assert.Equal(InviteStatus.Used, recoveryInvite.Status);
            Assert.Equal(
                [
                    "transaction-begun",
                    "attendee-locked",
                    "invite-locked",
                    "original-booking-locked",
                    "active-recovery-locked",
                    "event-guard-locked",
                    "capacity-locked",
                ],
                confirmTrace);
            Assert.Equal(
                [
                    "booking-event-located",
                    "transaction-begun",
                    "attendee-locked",
                    "pending-invites-locked",
                    "booking-locked",
                    "active-recovery-locked",
                    "event-guard-locked",
                    "event-guard-locked",
                    "capacity-locked",
                    "capacity-locked",
                ],
                cancelTrace);
        }
        else
        {
            Assert.Equal("not_found", confirmResult.Error.Code);
            Assert.DoesNotContain(bookings, b => b.RecoveryOfBookingId.HasValue);
            Assert.Equal(InviteStatus.Superseded, recoveryInvite.Status);
            Assert.Equal(
                ["transaction-begun", "attendee-locked", "invite-locked"],
                confirmTrace);
            Assert.Equal(
                [
                    "booking-event-located",
                    "transaction-begun",
                    "attendee-locked",
                    "pending-invites-locked",
                    "booking-locked",
                    "active-recovery-locked",
                    "event-guard-locked",
                    "capacity-locked",
                ],
                cancelTrace);
        }

        Assert.Equal(BookingStatus.Cancelled, bookings.Single(b => b.Id == race.OriginalId).Status);
        var attendee = await verify.Attendees.SingleAsync(c => c.Id == race.AttendeeId);
        Assert.Equal(AttendeeStatus.NotYetInvited, attendee.Status);

        foreach (var eventId in new[] { race.OriginalEventId, race.RecoveryEventId })
        {
            var remaining = await verify.EventCapacities
                .Where(c => c.EventId == eventId
                    && c.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting)
                .Select(c => c.RemainingCapacity)
                .SingleAsync();
            Assert.Equal(10, remaining);
        }
    }

    /// <summary>
    /// Pins the cancel-first branch: after the original and its pending recovery invite are
    /// gone, confirming the recovery link fails closed without touching capacity.
    /// </summary>
    [Fact]
    public async Task CancellingFirstLeavesRecoveryConfirmationStale()
    {
        await using var harness = await ConcurrencyHarness.CreateAsync(fixture);
        var race = await GivenRecoveryRaceAsync(harness);
        using var cancelScope = harness.CreateScope();
        using var confirmScope = harness.CreateScope();
        var cancelTrace = new List<string>();
        var confirmTrace = new List<string>();

        var cancelResult = await BuildCancelHandler(cancelScope.ServiceProvider, cancelTrace)
            .HandleAsync(new CancelBookingCommand(race.ManageToken, false), CancellationToken.None);
        Assert.True(cancelResult.IsSuccess);

        var confirmResult = await BuildConfirmHandler(confirmScope.ServiceProvider, confirmTrace)
            .HandleAsync(
                new ConfirmBookingCommand(race.RecoveryToken, race.RecoveryEventId),
                CancellationToken.None);

        Assert.True(confirmResult.IsFailure);
        Assert.Equal("not_found", confirmResult.Error.Code);
        Assert.Equal(
            ["transaction-begun", "attendee-locked", "invite-locked"],
            confirmTrace);
        Assert.Equal(
            [
                "booking-event-located",
                "transaction-begun",
                "attendee-locked",
                "pending-invites-locked",
                "booking-locked",
                "active-recovery-locked",
                "event-guard-locked",
                "capacity-locked",
            ],
            cancelTrace);

        await using var verify = fixture.NewContext();
        Assert.DoesNotContain(
            await verify.Bookings.AsNoTracking().ToListAsync(),
            b => b.RecoveryOfBookingId.HasValue);
        Assert.Equal(
            InviteStatus.Superseded,
            await verify.Invites
                .Where(i => i.RecoveryOfBookingId == race.OriginalId)
                .Select(i => i.Status)
                .SingleAsync());
        Assert.Equal(
            10,
            await verify.EventCapacities
                .Where(c => c.EventId == race.RecoveryEventId
                    && c.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting)
                .Select(c => c.RemainingCapacity)
                .SingleAsync());
    }

    /// <summary>
    /// Pins the confirm-first branch: the recovery commits, then cancelling the original
    /// voids the whole journey and restores every capacity row.
    /// </summary>
    [Fact]
    public async Task ConfirmingFirstThenCancellingVoidsTheWholeJourney()
    {
        await using var harness = await ConcurrencyHarness.CreateAsync(fixture);
        var race = await GivenRecoveryRaceAsync(harness);
        using var confirmScope = harness.CreateScope();
        using var cancelScope = harness.CreateScope();
        var confirmTrace = new List<string>();
        var cancelTrace = new List<string>();

        var confirmResult = await BuildConfirmHandler(confirmScope.ServiceProvider, confirmTrace)
            .HandleAsync(
                new ConfirmBookingCommand(race.RecoveryToken, race.RecoveryEventId),
                CancellationToken.None);
        Assert.True(confirmResult.IsSuccess);

        var cancelResult = await BuildCancelHandler(cancelScope.ServiceProvider, cancelTrace)
            .HandleAsync(new CancelBookingCommand(race.ManageToken, false), CancellationToken.None);
        Assert.True(cancelResult.IsSuccess);

        Assert.Equal(
            [
                "transaction-begun",
                "attendee-locked",
                "invite-locked",
                "original-booking-locked",
                "active-recovery-locked",
                "event-guard-locked",
                "capacity-locked",
            ],
            confirmTrace);
        Assert.Equal(
            [
                "booking-event-located",
                "transaction-begun",
                "attendee-locked",
                "pending-invites-locked",
                "booking-locked",
                "active-recovery-locked",
                "event-guard-locked",
                "event-guard-locked",
                "capacity-locked",
                "capacity-locked",
            ],
            cancelTrace);

        await using var verify = fixture.NewContext();
        var bookings = await verify.Bookings.AsNoTracking().ToListAsync();
        Assert.Equal(BookingStatus.Cancelled, bookings.Single(b => b.Id == race.OriginalId).Status);
        Assert.Equal(
            BookingStatus.Cancelled,
            Assert.Single(bookings, b => b.RecoveryOfBookingId == race.OriginalId).Status);
        Assert.Equal(
            InviteStatus.Used,
            await verify.Invites
                .Where(i => i.RecoveryOfBookingId == race.OriginalId)
                .Select(i => i.Status)
                .SingleAsync());

        foreach (var eventId in new[] { race.OriginalEventId, race.RecoveryEventId })
        {
            Assert.Equal(
                10,
                await verify.EventCapacities
                    .Where(c => c.EventId == eventId
                        && c.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting)
                    .Select(c => c.RemainingCapacity)
                    .SingleAsync());
        }
    }

    /// <summary>One booked attendee with a no-show and a pending recovery invite.</summary>
    private sealed record RecoveryRace(
        Guid OriginalEventId,
        Guid RecoveryEventId,
        Guid AttendeeId,
        Guid OriginalId,
        string ManageToken,
        string RecoveryToken);

    private async Task<RecoveryRace> GivenRecoveryRaceAsync(ConcurrencyHarness harness)
    {
        var originalEventId = await harness.GivenEventAsync(10, 6, 8);
        var recoveryEventId = await harness.GivenEventAsync(10, 6, 8);

        var initialToken = await harness.GivenInvitedAttendeeAsync(
            originalEventId, AppointmentTypeIds.DrugAndAlcoholTesting);
        var confirmed = await harness.ConfirmAsync(initialToken, originalEventId);
        Assert.True(confirmed.IsSuccess);
        var originalId = confirmed.Value.BookingId;

        Guid attendeeId;
        await using (var context = fixture.NewContext())
        {
            var original = await context.Bookings.SingleAsync(b => b.Id == originalId);
            attendeeId = original.AttendeeId;
            var missed = await context.BookingAppointments.SingleAsync(a => a.BookingId == originalId);
            missed.TransitionTo(
                BookingAppointmentStatus.NoShow, Guid.NewGuid(), DateTimeOffset.UtcNow, false, true);
            await context.SaveChangesAsync();
        }

        string recoveryToken;
        using (var setup = harness.CreateScope())
        {
            var tokens = setup.ServiceProvider.GetRequiredService<ITokenService>();
            var recoveryId = Guid.NewGuid();
            var issued = tokens.Issue(TokenPurpose.Book, recoveryId, Invite.InitialTokenVersion);
            recoveryToken = issued;
            await using var context = fixture.NewContext();
            context.Invites.Add(Invite.CreateRecovery(
                recoveryId,
                attendeeId,
                originalId,
                DateTimeOffset.UtcNow.AddDays(4),
                ProposalFixture.LocationId,
                null,
                [recoveryEventId, ..harness.FallbackEventIds],
                [AppointmentTypeIds.DrugAndAlcoholTesting]));
            await context.SaveChangesAsync();
        }

        return new RecoveryRace(
            originalEventId, recoveryEventId, attendeeId, originalId,
            confirmed.Value.ManageToken, recoveryToken);
    }

    private static ConfirmBookingHandler BuildConfirmHandler(IServiceProvider services, List<string> trace) => new(
        new RecordingInviteRepository(services.GetRequiredService<IInviteRepository>(), trace),
        new RecordingAttendeeRepository(services.GetRequiredService<IAttendeeRepository>(), trace),
        new RecordingEventRepository(services.GetRequiredService<IEventRepository>(), trace),
        new RecordingBookingRepository(services.GetRequiredService<IBookingRepository>(), trace),
        services.GetRequiredService<IBookingAppointmentRepository>(),
        new RecordingCapacityRepository(services.GetRequiredService<IEventCapacityRepository>(), trace),
        new EligibleEventFinder(
            services.GetRequiredService<IEventEligibilityQuery>(),
            services.GetRequiredService<IEventRepository>(),
            services.GetRequiredService<IClock>()),
        services.GetRequiredService<ITokenService>(),
        services.GetRequiredService<EmailDeliveryService>(),
        services.GetRequiredService<IAuditLogger>(),
        new RecordingUnitOfWork(services.GetRequiredService<IUnitOfWork>(), trace),
        services.GetRequiredService<IClock>(),
        services.GetRequiredService<AttendeePortalOptions>());

    private static CancelBookingHandler BuildCancelHandler(IServiceProvider services, List<string> trace)
    {
        var bookings = new RecordingBookingRepository(services.GetRequiredService<IBookingRepository>(), trace);
        var capacities = new RecordingCapacityRepository(services.GetRequiredService<IEventCapacityRepository>(), trace);
        var audit = services.GetRequiredService<IAuditLogger>();
        var issuer = new InviteIssuer(
            new RecordingInviteRepository(services.GetRequiredService<IInviteRepository>(), trace),
            services.GetRequiredService<IAttendeeGroupRepository>(),
            new EligibleEventFinder(
                services.GetRequiredService<IEventEligibilityQuery>(),
                services.GetRequiredService<IEventRepository>(),
                services.GetRequiredService<IClock>()),
            services.GetRequiredService<ISystemSettingsRepository>(),
            services.GetRequiredService<ITokenService>(),
            services.GetRequiredService<EmailDeliveryService>(),
            audit,
            services.GetRequiredService<IClock>(),
            services.GetRequiredService<AttendeePortalOptions>());

        return new CancelBookingHandler(
            bookings,
            new RecordingEventRepository(services.GetRequiredService<IEventRepository>(), trace),
            new RecordingAttendeeRepository(services.GetRequiredService<IAttendeeRepository>(), trace),
            new RecordingInviteRepository(services.GetRequiredService<IInviteRepository>(), trace),
            new BookingCanceller(
                services.GetRequiredService<IBookingAppointmentRepository>(), capacities, audit),
            issuer,
            services.GetRequiredService<EmailDeliveryService>(),
            services.GetRequiredService<ITokenService>(),
            services.GetRequiredService<IClock>(),
            new RecordingUnitOfWork(services.GetRequiredService<IUnitOfWork>(), trace));
    }

    /// <summary>Records attendee lifecycle-lock acquisition around the real repository.</summary>
    private sealed class RecordingAttendeeRepository(
        IAttendeeRepository inner,
        List<string> trace) : IAttendeeRepository
    {
        public Task<Attendee?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            inner.GetAsync(id, cancellationToken);

        public Task<Attendee?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
        {
            trace.Add("attendee-locked");
            return inner.LockForUpdateAsync(id, cancellationToken);
        }

        public Task<Attendee?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
            inner.GetByEmailAsync(email, cancellationToken);

        public Task<IReadOnlyList<Attendee>> ListAsync(
            AttendeeStatus? status,
            CancellationToken cancellationToken) =>
            inner.ListAsync(status, cancellationToken);

        public void Add(Attendee attendee) => inner.Add(attendee);

        public void Remove(Attendee attendee) => inner.Remove(attendee);
    }

    /// <summary>Records invite-lock acquisition order around the real repository.</summary>
    private sealed class RecordingInviteRepository(
        IInviteRepository inner,
        List<string> trace) : IInviteRepository
    {
        public Task<Invite?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            inner.GetAsync(id, cancellationToken);

        public Task<Invite?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
        {
            trace.Add("invite-locked");
            return inner.LockForUpdateAsync(id, cancellationToken);
        }

        public Task<Invite?> LockPendingForAttendeeAsync(
            Guid attendeeId,
            CancellationToken cancellationToken) =>
            inner.LockPendingForAttendeeAsync(attendeeId, cancellationToken);

        public Task<Invite?> GetPendingForAttendeeAsync(
            Guid attendeeId,
            CancellationToken cancellationToken) =>
            inner.GetPendingForAttendeeAsync(attendeeId, cancellationToken);

        public Task<Invite?> LockPendingInitialForAttendeeAsync(
            Guid attendeeId,
            CancellationToken cancellationToken) =>
            inner.LockPendingInitialForAttendeeAsync(attendeeId, cancellationToken);

        public Task<IReadOnlyList<Invite>> LockPendingListForAttendeeAsync(
            Guid attendeeId,
            CancellationToken cancellationToken)
        {
            trace.Add("pending-invites-locked");
            return inner.LockPendingListForAttendeeAsync(attendeeId, cancellationToken);
        }

        public Task<IReadOnlyList<Invite>> ListPendingExpiredAsync(
            DateTimeOffset asAt,
            CancellationToken cancellationToken) =>
            inner.ListPendingExpiredAsync(asAt, cancellationToken);

        public void Add(Invite invite) => inner.Add(invite);
    }

    /// <summary>Records booking-lock acquisition order around the real repository.</summary>
    private sealed class RecordingBookingRepository(
        IBookingRepository inner,
        List<string> trace) : IBookingRepository
    {
        public Task<Booking?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            inner.GetAsync(id, cancellationToken);

        public Task<Booking?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
        {
            trace.Add("booking-locked");
            return inner.LockForUpdateAsync(id, cancellationToken);
        }

        public Task<Guid?> GetEventIdAsync(Guid id, CancellationToken cancellationToken)
        {
            trace.Add("booking-event-located");
            return inner.GetEventIdAsync(id, cancellationToken);
        }

        public Task<Guid?> GetAttendeeIdAsync(Guid id, CancellationToken cancellationToken) =>
            inner.GetAttendeeIdAsync(id, cancellationToken);

        public Task<Booking?> LockByIdForAttendeeAsync(
            Guid bookingId,
            Guid attendeeId,
            CancellationToken cancellationToken)
        {
            trace.Add("booking-locked");
            return inner.LockByIdForAttendeeAsync(bookingId, attendeeId, cancellationToken);
        }

        public Task<Booking?> LockActiveForAttendeeAsync(
            Guid attendeeId,
            CancellationToken cancellationToken) =>
            inner.LockActiveForAttendeeAsync(attendeeId, cancellationToken);

        public Task<Booking?> GetActiveForAttendeeAsync(
            Guid attendeeId,
            CancellationToken cancellationToken) =>
            inner.GetActiveForAttendeeAsync(attendeeId, cancellationToken);

        public Task<Booking?> LockActiveOriginalForAttendeeAsync(
            Guid attendeeId,
            CancellationToken cancellationToken)
        {
            trace.Add("original-booking-locked");
            return inner.LockActiveOriginalForAttendeeAsync(attendeeId, cancellationToken);
        }

        public Task<IReadOnlyList<Guid>> ListActiveAttendeeIdsForEventAsync(
            Guid eventId,
            CancellationToken cancellationToken) =>
            inner.ListActiveAttendeeIdsForEventAsync(eventId, cancellationToken);

        public Task<IReadOnlyList<Booking>> ListActiveForEventAsync(
            Guid eventId,
            CancellationToken cancellationToken) =>
            inner.ListActiveForEventAsync(eventId, cancellationToken);

        public Task<IReadOnlyList<Booking>> ListJourneyAsync(
            Guid originalBookingId,
            CancellationToken cancellationToken) =>
            inner.ListJourneyAsync(originalBookingId, cancellationToken);

        public Task<Booking?> LockActiveRecoveryAsync(
            Guid originalBookingId,
            CancellationToken cancellationToken)
        {
            trace.Add("active-recovery-locked");
            return inner.LockActiveRecoveryAsync(originalBookingId, cancellationToken);
        }

        public void Add(Booking booking) => inner.Add(booking);
    }

    /// <summary>Records event-guard acquisition around the real repository.</summary>
    private sealed class RecordingEventRepository(
        IEventRepository inner,
        List<string> trace) : IEventRepository
    {
        public Task<Event?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            inner.GetAsync(id, cancellationToken);

        public Task<Event?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
        {
            trace.Add("event-guard-locked");
            return inner.LockForUpdateAsync(id, cancellationToken);
        }

        public Task<IReadOnlyList<Event>> ListActiveAsync(
            DateOnly onOrAfter,
            CancellationToken cancellationToken) =>
            inner.ListActiveAsync(onOrAfter, cancellationToken);

        public Task<IReadOnlyList<Event>> ListAllAsync(CancellationToken cancellationToken) =>
            inner.ListAllAsync(cancellationToken);

        public Task<IReadOnlyList<Event>> ListByIdsAsync(
            IReadOnlyCollection<Guid> ids,
            CancellationToken cancellationToken) =>
            inner.ListByIdsAsync(ids, cancellationToken);

        public Task AddAsync(Event eventItem, CancellationToken cancellationToken) =>
            inner.AddAsync(eventItem, cancellationToken);
    }

    /// <summary>Records capacity-lock acquisition around the real repository.</summary>
    private sealed class RecordingCapacityRepository(
        IEventCapacityRepository inner,
        List<string> trace) : IEventCapacityRepository
    {
        public Task<IReadOnlyList<EventCapacity>> LockForUpdateAsync(
            Guid eventId,
            IReadOnlyCollection<Guid> appointmentTypeIds,
            CancellationToken cancellationToken)
        {
            trace.Add("capacity-locked");
            return inner.LockForUpdateAsync(eventId, appointmentTypeIds, cancellationToken);
        }
    }

    /// <summary>Records transaction boundaries around the real unit of work.</summary>
    private sealed class RecordingUnitOfWork(IUnitOfWork inner, List<string> trace) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
            inner.SaveChangesAsync(cancellationToken);

        public Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken)
        {
            trace.Add("transaction-begun");
            return inner.BeginTransactionAsync(cancellationToken);
        }
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/SchemaTests.cs — 1/1

<!-- retirement-file: {"id":35,"file":"tests/EventBooking.Infrastructure.Tests/SchemaTests.cs","beforeSha":"549b746d8696a6c176578af08f6f0927b7bda91f5bc09b05538bc331c604b136","afterSha":"dc2c66ad9a039dcd62693b74b379cc7deeef12c4dec2ed9c0dde293cfdda38b6","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using EventBooking.Domain.Locations;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventBooking.Infrastructure.Tests;

/// <summary>
/// The schema is one migration against a real PostgreSQL 16. Every constraint here is one the
/// application must not be able to violate even with a direct connection.
/// </summary>
[Collection("postgres")]
public class SchemaTests(PostgresFixture fixture)
{
    private const string CapacityBounds = "ck_event_capacity_bounds";

    [Fact]
    public async Task TheSchemaIsOneMigrationThatAppliesToAnEmptyDatabase()
    {
        var databaseName = $"eventbooking_fresh_{Guid.NewGuid():N}";
        var connectionString = ConnectionTo(databaseName);

        try
        {
            await CreateDatabaseAsync(databaseName);
            await ApplyRolesScriptAsync(connectionString);

            await using var context = NewContext(connectionString);
            Assert.Equal(
                ["20260920120000_InitialSchema"],
                (await context.Database.GetPendingMigrationsAsync()).ToArray());

            await context.Database.MigrateAsync();

            Assert.Empty(await context.Database.GetPendingMigrationsAsync());
            var types = await context.AppointmentTypes.OrderBy(t => t.Code).ToListAsync();
            Assert.Equal(["DAT", "MED", "UNI"], types.Select(t => t.Code));
            Assert.Equal(1, (await context.SystemSettings.SingleAsync()).Id);
        }
        finally
        {
            await DropDatabaseAsync(databaseName);
        }
    }

    [Theory]
    [InlineData(5, -1)]
    [InlineData(5, 6)]
    [InlineData(0, 0)]
    public async Task TheDatabaseRefusesACapacityRowOutsideItsBounds(int total, int remaining)
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var eventId = await CreateEventWithoutCapacitiesAsync(context);

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO event_capacity
                    (event_id, appointment_type_id, total_headcount, remaining_capacity)
                VALUES ({0}, {1}, {2}, {3});
                """,
                [eventId, AppointmentTypeIds.DrugAndAlcoholTesting, total, remaining]));

        Assert.Contains(CapacityBounds, ex.ToString());
    }

    [Fact]
    public async Task TheDatabaseRefusesASecondEventForOneProposal()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var eventId = await CreateEventWithoutCapacitiesAsync(context);
        var proposalId = await context.Events.Where(e => e.Id == eventId)
            .Select(e => e.ProposalId).SingleAsync();

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO event
                    (id, proposal_id, location_id, status, date, start_time, duration_minutes, start_utc)
                SELECT {0}, proposal_id, location_id, status, date, start_time, duration_minutes, start_utc
                  FROM event WHERE id = {1};
                """,
                [Guid.NewGuid(), eventId]));

        Assert.Contains("proposal_id", ex.ToString());
    }

    [Fact]
    public async Task TheDatabaseRefusesASecondManagerForOneAppointmentType()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var typeId = AppointmentTypeIds.MedicalCheckUp;
        await InsertManagerProfileAsync(context, typeId);

        var ex = await Assert.ThrowsAnyAsync<Exception>(
            () => InsertManagerProfileAsync(context, typeId));

        Assert.Contains("ux_staff_access_profile_manager_appointment_type", ex.ToString());
    }

    [Fact]
    public async Task TheApplicationRoleMayAppendToTheAuditTrailButNeverRewriteIt()
    {
        await fixture.ResetAsync();

        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, "SET ROLE eventbooking_app;");

        await ExecuteAsync(
            connection,
            """
            INSERT INTO audit_log (id, entity_type, entity_id, action, actor_type, actor_id, timestamp)
            VALUES (gen_random_uuid(), 'Event', gen_random_uuid(), 0, 2, 'schema-test', now());
            """);
        Assert.Equal(1L, await ScalarAsync(connection, "SELECT count(*) FROM audit_log;"));

        var update = await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteAsync(connection, "UPDATE audit_log SET actor_id = 'rewritten';"));
        Assert.Equal("42501", update.SqlState);

        var delete = await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteAsync(connection, "DELETE FROM audit_log;"));
        Assert.Equal("42501", delete.SqlState);
    }

    [Fact]
    public async Task TheApplicationRoleKeepsFullDmlOnEveryOtherTable()
    {
        await fixture.ResetAsync();

        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, "SET ROLE eventbooking_app;");

        await ExecuteAsync(
            connection,
            """
            INSERT INTO location (id, code, name, address, time_zone_id, is_active, version)
            VALUES (gen_random_uuid(), 'ROLE', 'Role check', 'Somewhere', 'Europe/London', true, 1);
            UPDATE location SET name = 'Renamed' WHERE code = 'ROLE';
            DELETE FROM location WHERE code = 'ROLE';
            """);

        Assert.Equal(0L, await ScalarAsync(connection, "SELECT count(*) FROM location WHERE code = 'ROLE';"));
    }

    [Fact]
    public async Task AStaleVersionOnALocationIsARefusedWrite()
    {
        await fixture.ResetAsync();

        var location = Location.Create(
            Guid.NewGuid(), "STALE", "Stale check", "Somewhere", "Europe/London", new NodaTimeEventWindowZones());
        await using (var seed = fixture.NewContext())
        {
            seed.Add(location);
            await seed.SaveChangesAsync();
        }

        await using var first = fixture.NewContext();
        await using var second = fixture.NewContext();
        var readByFirst = await first.Set<Location>().SingleAsync(l => l.Id == location.Id);
        var readBySecond = await second.Set<Location>().SingleAsync(l => l.Id == location.Id);

        readByFirst.Rename("Renamed first");
        await first.SaveChangesAsync();

        readBySecond.Rename("Renamed second");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Theory]
    [InlineData("location", "code")]
    [InlineData("appointment_type", "code")]
    [InlineData("attendee_group", "code")]
    public async Task ACodeIsUniqueWithinItsReferenceTable(string table, string column)
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var indexes = await IndexDefinitionsAsync(context, table);

        Assert.Contains(
            indexes,
            definition => definition.Contains("UNIQUE", StringComparison.Ordinal)
                && definition.Contains($"({column})", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnAttendeeEmailIsUniqueIgnoringCase()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var indexes = await IndexDefinitionsAsync(context, "attendee");

        Assert.Contains(
            indexes,
            definition => definition.Contains("UNIQUE", StringComparison.Ordinal)
                && definition.Contains("lower(", StringComparison.Ordinal)
                && definition.Contains("email", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheEligibilityQueryHasItsCoveringIndex()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var indexes = await IndexDefinitionsAsync(context, "event");

        Assert.Contains(
            indexes,
            definition => definition.Contains("(status, location_id, start_utc)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ADeliveryAttemptCarriesItsClaimAndItsClaimCount()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var columns = await ColumnNamesAsync(context, "email_log");

        Assert.Contains("claimed_at", columns);
        Assert.Contains("claim_count", columns);
    }

    private static async Task InsertManagerProfileAsync(EventBookingDbContext context, Guid typeId) =>
        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO staff_access_profile
                (staff_user_id, is_admin, is_coordinator, is_manager, is_appointment_staff,
                 appointment_type_id, version)
            VALUES ({0}, false, false, true, false, {1}, 1);
            """,
            [Guid.NewGuid(), typeId]);

    private static async Task<IReadOnlyList<string>> IndexDefinitionsAsync(
        EventBookingDbContext context, string table)
    {
        var rows = new List<string>();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        await context.Database.OpenConnectionAsync();
        command.CommandText =
            $"SELECT indexdef FROM pg_indexes WHERE schemaname = 'public' AND tablename = '{table}';";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<string>> ColumnNamesAsync(
        EventBookingDbContext context, string table)
    {
        var rows = new List<string>();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        await context.Database.OpenConnectionAsync();
        command.CommandText =
            $"""
            SELECT column_name FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = '{table}';
            """;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    private async Task ApplyRolesScriptAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, DatabaseRoles.Script);
    }

    private static async Task<Guid> CreateEventWithoutCapacitiesAsync(EventBookingDbContext context)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 1);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 1);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 1);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        context.EventProposals.Add(proposal);
        context.Events.Add(eventItem);
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM event_capacity WHERE event_id = {eventItem.Id}");

        return eventItem.Id;
    }

    private string ConnectionTo(string databaseName) =>
        new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Database = databaseName,
            Pooling = false,
        }.ConnectionString;

    private static EventBookingDbContext NewContext(string connectionString) =>
        new(new DbContextOptionsBuilder<EventBookingDbContext>()
            .UseNpgsql(connectionString)
            .Options);

    private async Task CreateDatabaseAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
            {
                Database = "postgres",
                Pooling = false,
            }.ConnectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, $"CREATE DATABASE {databaseName};");
    }

    private async Task DropDatabaseAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
            {
                Database = "postgres",
                Pooling = false,
            }.ConnectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, $"DROP DATABASE IF EXISTS {databaseName};");
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/SchemaTests.cs — 1/1

<!-- retirement-file: {"id":35,"file":"tests/EventBooking.Infrastructure.Tests/SchemaTests.cs","beforeSha":"549b746d8696a6c176578af08f6f0927b7bda91f5bc09b05538bc331c604b136","afterSha":"dc2c66ad9a039dcd62693b74b379cc7deeef12c4dec2ed9c0dde293cfdda38b6","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using EventBooking.Domain.Locations;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventBooking.Infrastructure.Tests;

/// <summary>
/// The schema is one migration against a real PostgreSQL 16. Every constraint here is one the
/// application must not be able to violate even with a direct connection.
/// </summary>
[Collection("postgres")]
public class SchemaTests(PostgresFixture fixture)
{
    private const string CapacityBounds = "ck_event_capacity_bounds";

    [Fact]
    public async Task TheSchemaAppliesToAnEmptyDatabaseFromItsMigrations()
    {
        var databaseName = $"eventbooking_fresh_{Guid.NewGuid():N}";
        var connectionString = ConnectionTo(databaseName);

        try
        {
            await CreateDatabaseAsync(databaseName);
            await ApplyRolesScriptAsync(connectionString);

            await using var context = NewContext(connectionString);
            // One initial schema, and the migration that makes the derived start instant
            // required once Task 11's repository computes it. The initial migration is not
            // rewritten: it is committed, and the chain is what keeps it regenerable.
            Assert.Equal(
                ["20260920120000_InitialSchema", "20260920145721_RequireEventStartInstant"],
                (await context.Database.GetPendingMigrationsAsync()).ToArray());

            await context.Database.MigrateAsync();

            Assert.Empty(await context.Database.GetPendingMigrationsAsync());
            var types = await context.AppointmentTypes.OrderBy(t => t.Code).ToListAsync();
            Assert.Equal(["DAT", "MED", "UNI"], types.Select(t => t.Code));
            Assert.Equal(1, (await context.SystemSettings.SingleAsync()).Id);
        }
        finally
        {
            await DropDatabaseAsync(databaseName);
        }
    }

    [Theory]
    [InlineData(5, -1)]
    [InlineData(5, 6)]
    [InlineData(0, 0)]
    public async Task TheDatabaseRefusesACapacityRowOutsideItsBounds(int total, int remaining)
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var eventId = await CreateEventWithoutCapacitiesAsync(context);

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO event_capacity
                    (event_id, appointment_type_id, total_headcount, remaining_capacity)
                VALUES ({0}, {1}, {2}, {3});
                """,
                [eventId, AppointmentTypeIds.DrugAndAlcoholTesting, total, remaining]));

        Assert.Contains(CapacityBounds, ex.ToString());
    }

    [Fact]
    public async Task TheDatabaseRefusesASecondEventForOneProposal()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var eventId = await CreateEventWithoutCapacitiesAsync(context);
        var proposalId = await context.Events.Where(e => e.Id == eventId)
            .Select(e => e.ProposalId).SingleAsync();

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO event
                    (id, proposal_id, location_id, status, date, start_time, duration_minutes, start_utc)
                SELECT {0}, proposal_id, location_id, status, date, start_time, duration_minutes, start_utc
                  FROM event WHERE id = {1};
                """,
                [Guid.NewGuid(), eventId]));

        Assert.Contains("proposal_id", ex.ToString());
    }

    [Fact]
    public async Task TheDatabaseRefusesASecondManagerForOneAppointmentType()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var typeId = AppointmentTypeIds.MedicalCheckUp;
        await InsertManagerProfileAsync(context, typeId);

        var ex = await Assert.ThrowsAnyAsync<Exception>(
            () => InsertManagerProfileAsync(context, typeId));

        Assert.Contains("ux_staff_access_profile_manager_appointment_type", ex.ToString());
    }

    [Fact]
    public async Task TheApplicationRoleMayAppendToTheAuditTrailButNeverRewriteIt()
    {
        await fixture.ResetAsync();

        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, "SET ROLE eventbooking_app;");

        await ExecuteAsync(
            connection,
            """
            INSERT INTO audit_log (id, entity_type, entity_id, action, actor_type, actor_id, timestamp)
            VALUES (gen_random_uuid(), 'Event', gen_random_uuid(), 0, 2, 'schema-test', now());
            """);
        Assert.Equal(1L, await ScalarAsync(connection, "SELECT count(*) FROM audit_log;"));

        var update = await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteAsync(connection, "UPDATE audit_log SET actor_id = 'rewritten';"));
        Assert.Equal("42501", update.SqlState);

        var delete = await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteAsync(connection, "DELETE FROM audit_log;"));
        Assert.Equal("42501", delete.SqlState);
    }

    [Fact]
    public async Task TheApplicationRoleKeepsFullDmlOnEveryOtherTable()
    {
        await fixture.ResetAsync();

        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, "SET ROLE eventbooking_app;");

        await ExecuteAsync(
            connection,
            """
            INSERT INTO location (id, code, name, address, time_zone_id, is_active, version)
            VALUES (gen_random_uuid(), 'ROLE', 'Role check', 'Somewhere', 'Europe/London', true, 1);
            UPDATE location SET name = 'Renamed' WHERE code = 'ROLE';
            DELETE FROM location WHERE code = 'ROLE';
            """);

        Assert.Equal(0L, await ScalarAsync(connection, "SELECT count(*) FROM location WHERE code = 'ROLE';"));
    }

    [Fact]
    public async Task AStaleVersionOnALocationIsARefusedWrite()
    {
        await fixture.ResetAsync();

        var location = Location.Create(
            Guid.NewGuid(), "STALE", "Stale check", "Somewhere", "Europe/London", new NodaTimeEventWindowZones());
        await using (var seed = fixture.NewContext())
        {
            seed.Add(location);
            await seed.SaveChangesAsync();
        }

        await using var first = fixture.NewContext();
        await using var second = fixture.NewContext();
        var readByFirst = await first.Set<Location>().SingleAsync(l => l.Id == location.Id);
        var readBySecond = await second.Set<Location>().SingleAsync(l => l.Id == location.Id);

        readByFirst.Rename("Renamed first");
        await first.SaveChangesAsync();

        readBySecond.Rename("Renamed second");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Theory]
    [InlineData("location", "code")]
    [InlineData("appointment_type", "code")]
    [InlineData("attendee_group", "code")]
    public async Task ACodeIsUniqueWithinItsReferenceTable(string table, string column)
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var indexes = await IndexDefinitionsAsync(context, table);

        Assert.Contains(
            indexes,
            definition => definition.Contains("UNIQUE", StringComparison.Ordinal)
                && definition.Contains($"({column})", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnAttendeeEmailIsUniqueIgnoringCase()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var indexes = await IndexDefinitionsAsync(context, "attendee");

        Assert.Contains(
            indexes,
            definition => definition.Contains("UNIQUE", StringComparison.Ordinal)
                && definition.Contains("lower(", StringComparison.Ordinal)
                && definition.Contains("email", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheEligibilityQueryHasItsCoveringIndex()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var indexes = await IndexDefinitionsAsync(context, "event");

        Assert.Contains(
            indexes,
            definition => definition.Contains("(status, location_id, start_utc)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheDatabaseRefusesAnEventWithoutAStartInstant()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var eventId = await CreateEventWithoutCapacitiesAsync(context);

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE event SET start_utc = NULL WHERE id = {0};",
                [eventId]));

        Assert.Contains("start_utc", ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADeliveryAttemptCarriesItsClaimAndItsClaimCount()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var columns = await ColumnNamesAsync(context, "email_log");

        Assert.Contains("claimed_at", columns);
        Assert.Contains("claim_count", columns);
    }

    private static async Task InsertManagerProfileAsync(EventBookingDbContext context, Guid typeId) =>
        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO staff_access_profile
                (staff_user_id, is_admin, is_coordinator, is_manager, is_appointment_staff,
                 appointment_type_id, version)
            VALUES ({0}, false, false, true, false, {1}, 1);
            """,
            [Guid.NewGuid(), typeId]);

    private static async Task<IReadOnlyList<string>> IndexDefinitionsAsync(
        EventBookingDbContext context, string table)
    {
        var rows = new List<string>();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        await context.Database.OpenConnectionAsync();
        command.CommandText =
            $"SELECT indexdef FROM pg_indexes WHERE schemaname = 'public' AND tablename = '{table}';";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<string>> ColumnNamesAsync(
        EventBookingDbContext context, string table)
    {
        var rows = new List<string>();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        await context.Database.OpenConnectionAsync();
        command.CommandText =
            $"""
            SELECT column_name FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = '{table}';
            """;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    private async Task ApplyRolesScriptAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, DatabaseRoles.Script);
    }

    private static async Task<Guid> CreateEventWithoutCapacitiesAsync(EventBookingDbContext context)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 1);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 1);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 1);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        context.EventProposals.Add(proposal);
        context.Events.Add(eventItem);
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM event_capacity WHERE event_id = {eventItem.Id}");

        return eventItem.Id;
    }

    private string ConnectionTo(string databaseName) =>
        new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Database = databaseName,
            Pooling = false,
        }.ConnectionString;

    private static EventBookingDbContext NewContext(string connectionString) =>
        new(new DbContextOptionsBuilder<EventBookingDbContext>()
            .UseNpgsql(connectionString)
            .Options);

    private async Task CreateDatabaseAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
            {
                Database = "postgres",
                Pooling = false,
            }.ConnectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, $"CREATE DATABASE {databaseName};");
    }

    private async Task DropDatabaseAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
            {
                Database = "postgres",
                Pooling = false,
            }.ConnectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, $"DROP DATABASE IF EXISTS {databaseName};");
    }
}
`````
