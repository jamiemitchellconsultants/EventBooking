using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;
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
    /// outcomes restore every capacity row and keep the candidate lifecycle consistent.
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
            new ConfirmBookingCommand(race.RecoveryToken, race.RecoverySlotId), CancellationToken.None);
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
                    "candidate-locked",
                    "invite-locked",
                    "original-booking-locked",
                    "active-recovery-locked",
                    "slot-guard-locked",
                    "capacity-locked",
                ],
                confirmTrace);
            Assert.Equal(
                [
                    "booking-slot-located",
                    "transaction-begun",
                    "candidate-locked",
                    "pending-invites-locked",
                    "booking-locked",
                    "active-recovery-locked",
                    "slot-guard-locked",
                    "slot-guard-locked",
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
                ["transaction-begun", "candidate-locked", "invite-locked"],
                confirmTrace);
            Assert.Equal(
                [
                    "booking-slot-located",
                    "transaction-begun",
                    "candidate-locked",
                    "pending-invites-locked",
                    "booking-locked",
                    "active-recovery-locked",
                    "slot-guard-locked",
                    "capacity-locked",
                ],
                cancelTrace);
        }

        Assert.Equal(BookingStatus.Cancelled, bookings.Single(b => b.Id == race.OriginalId).Status);
        var candidate = await verify.Candidates.SingleAsync(c => c.Id == race.CandidateId);
        Assert.Equal(CandidateStatus.NotYetInvited, candidate.Status);

        foreach (var slotId in new[] { race.OriginalSlotId, race.RecoverySlotId })
        {
            var remaining = await verify.SlotCapacities
                .Where(c => c.ConfirmedSlotId == slotId
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
                new ConfirmBookingCommand(race.RecoveryToken, race.RecoverySlotId),
                CancellationToken.None);

        Assert.True(confirmResult.IsFailure);
        Assert.Equal("not_found", confirmResult.Error.Code);
        Assert.Equal(
            ["transaction-begun", "candidate-locked", "invite-locked"],
            confirmTrace);
        Assert.Equal(
            [
                "booking-slot-located",
                "transaction-begun",
                "candidate-locked",
                "pending-invites-locked",
                "booking-locked",
                "active-recovery-locked",
                "slot-guard-locked",
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
            await verify.SlotCapacities
                .Where(c => c.ConfirmedSlotId == race.RecoverySlotId
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
                new ConfirmBookingCommand(race.RecoveryToken, race.RecoverySlotId),
                CancellationToken.None);
        Assert.True(confirmResult.IsSuccess);

        var cancelResult = await BuildCancelHandler(cancelScope.ServiceProvider, cancelTrace)
            .HandleAsync(new CancelBookingCommand(race.ManageToken, false), CancellationToken.None);
        Assert.True(cancelResult.IsSuccess);

        Assert.Equal(
            [
                "transaction-begun",
                "candidate-locked",
                "invite-locked",
                "original-booking-locked",
                "active-recovery-locked",
                "slot-guard-locked",
                "capacity-locked",
            ],
            confirmTrace);
        Assert.Equal(
            [
                "booking-slot-located",
                "transaction-begun",
                "candidate-locked",
                "pending-invites-locked",
                "booking-locked",
                "active-recovery-locked",
                "slot-guard-locked",
                "slot-guard-locked",
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

        foreach (var slotId in new[] { race.OriginalSlotId, race.RecoverySlotId })
        {
            Assert.Equal(
                10,
                await verify.SlotCapacities
                    .Where(c => c.ConfirmedSlotId == slotId
                        && c.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting)
                    .Select(c => c.RemainingCapacity)
                    .SingleAsync());
        }
    }

    /// <summary>One booked candidate with a no-show and a pending recovery invite.</summary>
    private sealed record RecoveryRace(
        Guid OriginalSlotId,
        Guid RecoverySlotId,
        Guid CandidateId,
        Guid OriginalId,
        string ManageToken,
        string RecoveryToken);

    private async Task<RecoveryRace> GivenRecoveryRaceAsync(ConcurrencyHarness harness)
    {
        var originalSlotId = await harness.GivenSlotAsync(10, 6, 8);
        var recoverySlotId = await harness.GivenSlotAsync(10, 6, 8);

        var initialToken = await harness.GivenInvitedCandidateAsync(
            originalSlotId, AppointmentTypeIds.DrugAndAlcoholTesting);
        var confirmed = await harness.ConfirmAsync(initialToken, originalSlotId);
        Assert.True(confirmed.IsSuccess);
        var originalId = confirmed.Value.BookingId;

        Guid candidateId;
        await using (var context = fixture.NewContext())
        {
            var original = await context.Bookings.SingleAsync(b => b.Id == originalId);
            candidateId = original.CandidateId;
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
            var issued = tokens.Issue(recoveryId);
            recoveryToken = issued.Token;
            await using var context = fixture.NewContext();
            context.Invites.Add(Invite.CreateRecovery(
                recoveryId, candidateId, originalId, issued.TokenHash,
                DateTimeOffset.UtcNow.AddDays(4),
                [recoverySlotId, ..harness.FallbackSlotIds],
                [AppointmentTypeIds.DrugAndAlcoholTesting]));
            await context.SaveChangesAsync();
        }

        return new RecoveryRace(
            originalSlotId, recoverySlotId, candidateId, originalId,
            confirmed.Value.ManageToken, recoveryToken);
    }

    private static ConfirmBookingHandler BuildConfirmHandler(IServiceProvider services, List<string> trace) => new(
        new RecordingInviteRepository(services.GetRequiredService<IInviteRepository>(), trace),
        new RecordingCandidateRepository(services.GetRequiredService<ICandidateRepository>(), trace),
        new RecordingSlotRepository(services.GetRequiredService<IConfirmedSlotRepository>(), trace),
        new RecordingBookingRepository(services.GetRequiredService<IBookingRepository>(), trace),
        services.GetRequiredService<IBookingAppointmentRepository>(),
        new RecordingCapacityRepository(services.GetRequiredService<ISlotCapacityRepository>(), trace),
        new EligibleSlotFinder(
            services.GetRequiredService<IConfirmedSlotRepository>(),
            services.GetRequiredService<IClock>()),
        services.GetRequiredService<ITokenService>(),
        services.GetRequiredService<EmailDeliveryService>(),
        services.GetRequiredService<IAuditLogger>(),
        new RecordingUnitOfWork(services.GetRequiredService<IUnitOfWork>(), trace),
        services.GetRequiredService<IClock>(),
        services.GetRequiredService<CandidatePortalOptions>());

    private static CancelBookingHandler BuildCancelHandler(IServiceProvider services, List<string> trace)
    {
        var bookings = new RecordingBookingRepository(services.GetRequiredService<IBookingRepository>(), trace);
        var capacities = new RecordingCapacityRepository(services.GetRequiredService<ISlotCapacityRepository>(), trace);
        var audit = services.GetRequiredService<IAuditLogger>();
        var issuer = new InviteIssuer(
            new RecordingInviteRepository(services.GetRequiredService<IInviteRepository>(), trace),
            services.GetRequiredService<IEmployeeGroupRepository>(),
            new EligibleSlotFinder(
                services.GetRequiredService<IConfirmedSlotRepository>(),
                services.GetRequiredService<IClock>()),
            services.GetRequiredService<ISystemSettingsRepository>(),
            services.GetRequiredService<ITokenService>(),
            services.GetRequiredService<EmailDeliveryService>(),
            audit,
            services.GetRequiredService<IClock>(),
            services.GetRequiredService<CandidatePortalOptions>());

        return new CancelBookingHandler(
            bookings,
            new RecordingSlotRepository(services.GetRequiredService<IConfirmedSlotRepository>(), trace),
            new RecordingCandidateRepository(services.GetRequiredService<ICandidateRepository>(), trace),
            new RecordingInviteRepository(services.GetRequiredService<IInviteRepository>(), trace),
            new BookingCanceller(
                services.GetRequiredService<IBookingAppointmentRepository>(), capacities, audit),
            issuer,
            services.GetRequiredService<EmailDeliveryService>(),
            services.GetRequiredService<ITokenService>(),
            services.GetRequiredService<IClock>(),
            new RecordingUnitOfWork(services.GetRequiredService<IUnitOfWork>(), trace));
    }

    /// <summary>Records candidate lifecycle-lock acquisition around the real repository.</summary>
    private sealed class RecordingCandidateRepository(
        ICandidateRepository inner,
        List<string> trace) : ICandidateRepository
    {
        public Task<Candidate?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            inner.GetAsync(id, cancellationToken);

        public Task<Candidate?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
        {
            trace.Add("candidate-locked");
            return inner.LockForUpdateAsync(id, cancellationToken);
        }

        public Task<Candidate?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
            inner.GetByEmailAsync(email, cancellationToken);

        public Task<IReadOnlyList<Candidate>> ListAsync(
            CandidateStatus? status,
            CancellationToken cancellationToken) =>
            inner.ListAsync(status, cancellationToken);

        public void Add(Candidate candidate) => inner.Add(candidate);

        public void Remove(Candidate candidate) => inner.Remove(candidate);
    }

    /// <summary>Records invite-lock acquisition order around the real repository.</summary>
    private sealed class RecordingInviteRepository(
        IInviteRepository inner,
        List<string> trace) : IInviteRepository
    {
        public Task<Invite?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            inner.GetAsync(id, cancellationToken);

        public Task<Invite?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
            inner.LockForUpdateAsync(id, cancellationToken);

        public Task<Invite?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
            inner.GetByTokenHashAsync(tokenHash, cancellationToken);

        public Task<Invite?> LockByTokenHashForUpdateAsync(
            string tokenHash,
            CancellationToken cancellationToken)
        {
            trace.Add("invite-locked");
            return inner.LockByTokenHashForUpdateAsync(tokenHash, cancellationToken);
        }

        public Task<Invite?> LockPendingForCandidateAsync(
            Guid candidateId,
            CancellationToken cancellationToken) =>
            inner.LockPendingForCandidateAsync(candidateId, cancellationToken);

        public Task<Invite?> GetPendingForCandidateAsync(
            Guid candidateId,
            CancellationToken cancellationToken) =>
            inner.GetPendingForCandidateAsync(candidateId, cancellationToken);

        public Task<Invite?> LockPendingInitialForCandidateAsync(
            Guid candidateId,
            CancellationToken cancellationToken) =>
            inner.LockPendingInitialForCandidateAsync(candidateId, cancellationToken);

        public Task<IReadOnlyList<Invite>> LockPendingListForCandidateAsync(
            Guid candidateId,
            CancellationToken cancellationToken)
        {
            trace.Add("pending-invites-locked");
            return inner.LockPendingListForCandidateAsync(candidateId, cancellationToken);
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

        public Task<Booking?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
            inner.LockForUpdateAsync(id, cancellationToken);

        public Task<Booking?> GetByManageTokenHashAsync(
            string manageTokenHash,
            CancellationToken cancellationToken) =>
            inner.GetByManageTokenHashAsync(manageTokenHash, cancellationToken);

        public Task<Guid?> GetConfirmedSlotIdByManageTokenHashAsync(
            string manageTokenHash,
            CancellationToken cancellationToken)
        {
            trace.Add("booking-slot-located");
            return inner.GetConfirmedSlotIdByManageTokenHashAsync(manageTokenHash, cancellationToken);
        }

        public Task<Guid?> GetCandidateIdByManageTokenHashAsync(
            string manageTokenHash,
            CancellationToken cancellationToken) =>
            inner.GetCandidateIdByManageTokenHashAsync(manageTokenHash, cancellationToken);

        public Task<Booking?> LockByManageTokenHashForUpdateAsync(
            string manageTokenHash,
            CancellationToken cancellationToken)
        {
            trace.Add("booking-locked");
            return inner.LockByManageTokenHashForUpdateAsync(manageTokenHash, cancellationToken);
        }

        public Task<Booking?> LockByIdForCandidateAsync(
            Guid bookingId,
            Guid candidateId,
            CancellationToken cancellationToken)
        {
            trace.Add("booking-locked");
            return inner.LockByIdForCandidateAsync(bookingId, candidateId, cancellationToken);
        }

        public Task<Booking?> LockActiveForCandidateAsync(
            Guid candidateId,
            CancellationToken cancellationToken) =>
            inner.LockActiveForCandidateAsync(candidateId, cancellationToken);

        public Task<Booking?> GetActiveForCandidateAsync(
            Guid candidateId,
            CancellationToken cancellationToken) =>
            inner.GetActiveForCandidateAsync(candidateId, cancellationToken);

        public Task<Booking?> LockActiveOriginalForCandidateAsync(
            Guid candidateId,
            CancellationToken cancellationToken)
        {
            trace.Add("original-booking-locked");
            return inner.LockActiveOriginalForCandidateAsync(candidateId, cancellationToken);
        }

        public Task<IReadOnlyList<Guid>> ListActiveCandidateIdsForSlotAsync(
            Guid confirmedSlotId,
            CancellationToken cancellationToken) =>
            inner.ListActiveCandidateIdsForSlotAsync(confirmedSlotId, cancellationToken);

        public Task<IReadOnlyList<Booking>> ListActiveForSlotAsync(
            Guid confirmedSlotId,
            CancellationToken cancellationToken) =>
            inner.ListActiveForSlotAsync(confirmedSlotId, cancellationToken);

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

    /// <summary>Records slot-guard acquisition around the real repository.</summary>
    private sealed class RecordingSlotRepository(
        IConfirmedSlotRepository inner,
        List<string> trace) : IConfirmedSlotRepository
    {
        public Task<ConfirmedSlot?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            inner.GetAsync(id, cancellationToken);

        public Task<ConfirmedSlot?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
        {
            trace.Add("slot-guard-locked");
            return inner.LockForUpdateAsync(id, cancellationToken);
        }

        public Task<IReadOnlyList<ConfirmedSlot>> ListActiveAsync(
            DateOnly onOrAfter,
            CancellationToken cancellationToken) =>
            inner.ListActiveAsync(onOrAfter, cancellationToken);

        public Task<IReadOnlyList<ConfirmedSlot>> ListAllAsync(CancellationToken cancellationToken) =>
            inner.ListAllAsync(cancellationToken);

        public void Add(ConfirmedSlot slot) => inner.Add(slot);
    }

    /// <summary>Records capacity-lock acquisition around the real repository.</summary>
    private sealed class RecordingCapacityRepository(
        ISlotCapacityRepository inner,
        List<string> trace) : ISlotCapacityRepository
    {
        public Task<IReadOnlyList<SlotCapacity>> LockForUpdateAsync(
            Guid confirmedSlotId,
            IReadOnlyCollection<Guid> appointmentTypeIds,
            CancellationToken cancellationToken)
        {
            trace.Add("capacity-locked");
            return inner.LockForUpdateAsync(confirmedSlotId, appointmentTypeIds, cancellationToken);
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
