# 00a — Port source 71 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs","encoding":"utf8","sha256":"ca4e25949ea11f0f7634c36c69bc0e64f1c403fa5a66c618edce083e8543cc63","parts":1,"part":1} -->

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

## tests/EventBooking.Infrastructure.Tests/BookingAppointmentPersistenceTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/BookingAppointmentPersistenceTests.cs","encoding":"utf8","sha256":"8ace6bbf9de6109d417a07baed3ce10c76c404d5d0c5112a593dde285c558f5e","parts":1,"part":1} -->

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

## tests/EventBooking.Infrastructure.Tests/CandidateBookingCancellationPersistenceTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/CandidateBookingCancellationPersistenceTests.cs","encoding":"utf8","sha256":"74073de631f622b4f878667d6f01af2c50fe545547d8a7391bb1342a95a1e18a","parts":1,"part":1} -->

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

## tests/EventBooking.Infrastructure.Tests/CandidateBookingQueryTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/CandidateBookingQueryTests.cs","encoding":"utf8","sha256":"2ef7942c48aa546f9e5c5bf14a3afbc43fbe65e673078192e4b4aaf9886e0ca1","parts":1,"part":1} -->

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

## tests/EventBooking.Infrastructure.Tests/CandidateReadinessQueryTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/CandidateReadinessQueryTests.cs","encoding":"utf8","sha256":"acaffee1a885fef3636a37298cb2e9df4de1145449aaaa84c763325efdf62a19","parts":1,"part":1} -->

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

## tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyHarness.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyHarness.cs","encoding":"utf8","sha256":"8310026b45005eef723383b6613c245d161fc799302b263e4f1673a1d8c4c62a","parts":1,"part":1} -->

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

## tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyTests.cs","encoding":"utf8","sha256":"d479b2762894ee9520402dc2b60bca0a4ddd750a7ca147277c09f0a157f5726a","parts":1,"part":1} -->

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
