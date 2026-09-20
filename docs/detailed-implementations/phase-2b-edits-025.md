# 02b — One fresh schema, and the roles that keep the audit trail append-only, edits 25 (Task 9b)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — src/EventBooking.SeedData/Program.cs — 1/1

<!-- retirement-file: {"id":59,"file":"src/EventBooking.SeedData/Program.cs","beforeSha":"cf99f5228d669b302a84eb5fce3861e0626e331190cefd5107fc5a4e320f7a6b","afterSha":"9cf94805ef8045f41d6fc0be8aa04f9342705cc8da6277834bebf19d68f7cee0","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Time;
using EventBooking.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var reseed = args.Contains("--reseed");
var skipSeed = args.Contains("--skip-seed");
var verbose = args.Contains("--verbose");
var reanchorIndex = args.ToList().IndexOf("--reanchor");
var reanchorRequested = reanchorIndex >= 0;
DateOnly? reanchorDate = null;
if (reanchorRequested
    && reanchorIndex + 1 < args.Length
    && DateOnly.TryParse(args[reanchorIndex + 1], out var parsedDate))
{
    reanchorDate = parsedDate;
}
var connectionString = args.FirstOrDefault(a => !a.StartsWith('-'))
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__EventBooking");

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project src/EventBooking.SeedData -- \"<postgres connection string>\" [--reseed] [--skip-seed] [--verbose]");
    Console.Error.WriteLine(
        "   or set the ConnectionStrings__EventBooking environment variable.");
    Console.Error.WriteLine(
        "   Pending migrations are always applied first, whichever mode runs.");
    Console.Error.WriteLine(
        "   --reseed wipes every domain table first, then seeds fresh.");
    Console.Error.WriteLine(
        "   --skip-seed applies migrations only and seeds nothing.");
    Console.Error.WriteLine(
        "   --verbose reports per-step progress; failures print the full exception.");
    Console.Error.WriteLine(
        "   --reanchor [yyyy-MM-dd] resolves every day offset against the given date");
    Console.Error.WriteLine(
        "   (default: today at transitional location) instead of the file anchor, without editing");
    Console.Error.WriteLine(
        "   demo-seed.json. Use it when the file anchor has gone stale and proposals");
    Console.Error.WriteLine(
        "   land on today or earlier.");
    Console.Error.WriteLine(
        "   Keycloak demo users are converged when Keycloak__BaseUrl, Keycloak__Realm,");
    Console.Error.WriteLine(
        "   Keycloak__AdminRealm, Keycloak__AdminUsername, Keycloak__AdminPassword, and");
    Console.Error.WriteLine(
        "   Keycloak__DemoPassword are set; otherwise only the database is seeded.");
    Console.Error.WriteLine(
        "   --reseed also deletes and recreates the Keycloak realm from the file named by");
    Console.Error.WriteLine(
        "   Keycloak__RealmExportPath, when Keycloak settings are configured.");
    Console.Error.WriteLine("   Normal seed/reseed sends five demo invitations through Mailpit SMTP.");
    Console.Error.WriteLine("   Local defaults: Portal__BaseUrl=http://localhost:5002, Email__Smtp__Host=localhost,");
    Console.Error.WriteLine("   Email__Smtp__Port=1025 and the local API's development token key.");
    Console.Error.WriteLine("   Non-local portals require explicit Tokens__SigningKey and Email__Smtp__Host.");
    Console.Error.WriteLine("   Match Tokens__SigningKey and Portal__BaseUrl to the running API.");
    Console.Error.WriteLine("   --skip-seed does not read email settings or send any messages.");
    return 2;
}

try
{
    var services = new ServiceCollection();
    services.AddLogging();
    if (skipSeed)
    {
        // The persistence interceptor still needs IClock, even for migration-only context creation.
        services.AddEventBookingPersistence(connectionString);
        services.AddSingleton(new ClockOptions("Europe/London"));
        services.AddSingleton<IClock, SystemClock>();
    }
    else
    {
        var email = DemoEmailOptions.From(Environment.GetEnvironmentVariable);
        services.AddEventBookingInfrastructure(connectionString, email.Clock, email.Tokens);
        services.AddEventBookingApplication(email.Portal,
            new EventBooking.Application.Access.StaffIdPolicy(Environment.GetEnvironmentVariable("Identity__StaffIdPattern")));
        services.AddLocalEmailTransport(email.Sender, email.Smtp);
        services.AddScoped<DemoSeeder>();
        services.AddScoped<DemoInvitationSeeder>();
    }
    using var provider = services.BuildServiceProvider();
    using var scope = provider.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
    if (verbose)
    {
        Console.WriteLine("[seed] Applying pending migrations...");
    }

    // Before migrating, not after: the initial migration grants to roles it does not create.
    await database.Database.ExecuteSqlRawAsync(DatabaseRoles.Script);
    if (verbose)
    {
        Console.WriteLine("[seed] Database roles applied.");
    }

    await database.Database.MigrateAsync();
    if (verbose)
    {
        Console.WriteLine("[seed] Migrations applied.");
    }

    if (skipSeed)
    {
        Console.WriteLine("Migrations applied. Skipping seed data (--skip-seed).");
        return 0;
    }

    var keycloakStep = new KeycloakSeedStep(
        Environment.GetEnvironmentVariable,
        static () => new HttpClient());
    if (reseed && verbose)
    {
        Console.WriteLine("[seed] Reseed requested: the Keycloak realm will be deleted and recreated first, if configured.");
    }

    var keycloakSummary = await keycloakStep.RunAsync(
        skipSeed, reseed, DemoSeedSpec.Staff(), CancellationToken.None);
    if (keycloakSummary is not null)
    {
        if (verbose)
        {
            Console.WriteLine(reseed
                ? "[seed] Keycloak realm reset and convergence complete."
                : "[seed] Keycloak convergence complete.");
        }

        Console.WriteLine(
            $"Keycloak seed complete: {keycloakSummary.RolesCreated} roles created, " +
            $"{keycloakSummary.MapperWrites} mapper writes, " +
            $"{keycloakSummary.UsersCreated} users created, " +
            $"{keycloakSummary.RoleMappingWrites} role-mapping writes.");
    }
    else if (verbose)
    {
        Console.WriteLine("[seed] Keycloak provider seed skipped.");
    }

    if (reanchorRequested)
    {
        reanchorDate ??= scope.ServiceProvider
            .GetRequiredService<IClock>()
            .TodayAtTransitionalLocation;
        DemoSeedSpec.OverrideAnchor(reanchorDate.Value);
        Console.WriteLine($"[seed] Reanchored to {reanchorDate:yyyy-MM-dd}.");
    }

    var seeder = scope.ServiceProvider.GetRequiredService<DemoSeeder>();
    if (verbose)
    {
        seeder.Progress = Console.Out;
    }

    var summary = reseed
        ? await seeder.ReseedAsync(CancellationToken.None)
        : await seeder.RunAsync(CancellationToken.None);

    var invitations = scope.ServiceProvider.GetRequiredService<DemoInvitationSeeder>();
    if (verbose) invitations.Progress = Console.Out;
    var invitationEmailsSent = await invitations.RunAsync(CancellationToken.None);

    Console.WriteLine(
        $"Migrations applied. " +
        $"{(reseed ? "Reseed complete (database was cleared): " : "Seed complete: ")}" +
        $"{summary.IdentitiesEnsured} identities, " +
        $"{summary.ProfilesEnsured} profiles, " +
        $"{summary.AgreedEventsImported} agreed events, " +
        $"{summary.ProposalsEnsured} proposals, " +
        $"{summary.AcceptancesApplied} acceptances, " +
        $"{summary.AttendeesCreated} attendees; " +
        $"{invitationEmailsSent} invitation emails sent or retried.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(verbose ? $"Seed failed: {ex}" : $"Seed failed: {ex.Message}");
    return 2;
}
`````

## before — tests/EventBooking.Infrastructure.Tests/AttendeeGroupRequiredMigrationTests.cs — 1/1

<!-- retirement-file: {"id":60,"file":"tests/EventBooking.Infrastructure.Tests/AttendeeGroupRequiredMigrationTests.cs","beforeSha":"39c683f118d9428cd2e923a49310edf17d0ec27696ec7a7467ed54bf74757d98","afterSha":null,"side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies Release 2 refuses incomplete reconciliation before changing nullability.</summary>
[Collection("postgres")]
public sealed class AttendeeGroupRequiredMigrationTests(PostgresFixture fixture)
{
    private const string ReleaseOneMigration = "20260909120000_AddRecoveryBookings";

    /// <summary>An unassigned row blocks migration; explicit matching assignment then succeeds.</summary>
    [Fact]
    public async Task GuardBlocksUnassignedAttendeeBeforeAlterColumn()
    {
        var databaseName = $"eventbooking_group_required_{Guid.NewGuid():N}";
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Database = databaseName,
            Pooling = false,
        }.ConnectionString;
        try
        {
            await ExecuteAdminAsync(fixture.ConnectionString, $"CREATE DATABASE \"{databaseName}\"");
            await using (var releaseOne = NewContext(connectionString))
            {
                await releaseOne.Database.MigrateAsync(ReleaseOneMigration);
            }
            var attendeeId = Guid.NewGuid();
            await ExecuteAsync(connectionString,
                """
                INSERT INTO attendee (id, name, email, status, status_changed_at, attendee_group_id)
                VALUES (@id, 'Legacy', 'legacy@example.com', @status, now(), NULL);
                """,
                ("id", attendeeId), ("status", (int)AttendeeStatus.NotYetInvited));

            await using (var blocked = NewContext(connectionString))
            {
                var error = await Assert.ThrowsAnyAsync<Exception>(() => blocked.Database.MigrateAsync());
                Assert.Contains("Attendee Group reconciliation", error.ToString());
            }
            Assert.True(await IsNullableAsync(connectionString));

            await ExecuteAsync(connectionString,
                """
                UPDATE attendee SET attendee_group_id = @group_id WHERE id = @id;
                INSERT INTO attendee_requirement (attendee_id, appointment_type_id)
                SELECT @id, appointment_type_id
                FROM attendee_group_requirement
                WHERE attendee_group_id = @group_id;
                """,
                ("id", attendeeId), ("group_id", AttendeeGroupIds.Pilots));
            await using (var reconciled = NewContext(connectionString))
            {
                await reconciled.Database.MigrateAsync();
            }

            Assert.False(await IsNullableAsync(connectionString));
        }
        finally
        {
            await ExecuteAdminAsync(
                fixture.ConnectionString,
                $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)");
        }
    }

    private static EventBookingDbContext NewContext(string connectionString) => new(
        new DbContextOptionsBuilder<EventBookingDbContext>().UseNpgsql(connectionString).Options);

    private static async Task ExecuteAdminAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteAsync(
        string connectionString,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> IsNullableAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT is_nullable = 'YES'
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'attendee'
              AND column_name = 'attendee_group_id';
            """, connection);
        return (bool)(await command.ExecuteScalarAsync())!;
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/BookingAppointmentPersistenceTests.cs — 1/1

<!-- retirement-file: {"id":61,"file":"tests/EventBooking.Infrastructure.Tests/BookingAppointmentPersistenceTests.cs","beforeSha":"8429e5499781b1f38957d4926c17e7a88ca8a5e40fc2fce978718f1aba5071e8","afterSha":"99c2097d17ef7cc2b624254d2d797b186117385550836d2e9eacfa6a533737d0","side":"before","part":1,"parts":1} -->

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
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting],
            0);
        return Booking.Create(
            Guid.NewGuid(), invite, eventId, DateTimeOffset.UtcNow);
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

<!-- retirement-file: {"id":61,"file":"tests/EventBooking.Infrastructure.Tests/BookingAppointmentPersistenceTests.cs","beforeSha":"8429e5499781b1f38957d4926c17e7a88ca8a5e40fc2fce978718f1aba5071e8","afterSha":"99c2097d17ef7cc2b624254d2d797b186117385550836d2e9eacfa6a533737d0","side":"after","part":1,"parts":1} -->

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

    /// <summary>Creates a booking for persistence tests.</summary>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="eventId">The event identifier.</param>
    /// <returns>A new active booking.</returns>
    private static Booking NewBooking(Guid attendeeId, Guid eventId)
    {
        var invite = Domain.Invites.Invite.CreateInitial(
            Guid.NewGuid(),
            attendeeId,
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting],
            0);
        return Booking.Create(
            Guid.NewGuid(), invite, eventId, DateTimeOffset.UtcNow);
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

## before — tests/EventBooking.Infrastructure.Tests/PostgresFixture.cs — 1/1

<!-- retirement-file: {"id":62,"file":"tests/EventBooking.Infrastructure.Tests/PostgresFixture.cs","beforeSha":"156c787ac039310a6daa5bfece000fbe5820319f9cfcb266e9c8c9161ebac257","afterSha":"86e22ad67c2c256d99e1f342bef68be65352d7f5901f1e2fd29d6c6355d0ed21","side":"before","part":1,"parts":1} -->

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

<!-- retirement-file: {"id":62,"file":"tests/EventBooking.Infrastructure.Tests/PostgresFixture.cs","beforeSha":"156c787ac039310a6daa5bfece000fbe5820319f9cfcb266e9c8c9161ebac257","afterSha":"86e22ad67c2c256d99e1f342bef68be65352d7f5901f1e2fd29d6c6355d0ed21","side":"after","part":1,"parts":1} -->

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

## before — tests/EventBooking.Infrastructure.Tests/SchemaTests.cs — 1/1

<!-- retirement-file: {"id":63,"file":"tests/EventBooking.Infrastructure.Tests/SchemaTests.cs","beforeSha":"a13870b242c63abae438fe324bee4bbac5ac58f5846268b6e07ef581df52ffc8","afterSha":"549b746d8696a6c176578af08f6f0927b7bda91f5bc09b05538bc331c604b136","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class SchemaTests(PostgresFixture fixture)
{
    private const string ReleaseOneMigration = "20260909120000_AddRecoveryBookings";

    [Fact]
    public async Task CleanDatabaseMigrationsSeedFixedRowsAndAddTheAttendeeStatusTimestamp()
    {
        var databaseName = $"eventbooking_seed_{Guid.NewGuid():N}";
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Database = databaseName,
            Pooling = false,
        }.ConnectionString;

        try
        {
            await CreateDatabaseAsync(fixture.ConnectionString, databaseName);

            await using (var context = new EventBookingDbContext(
                             new DbContextOptionsBuilder<EventBookingDbContext>()
                                 .UseNpgsql(connectionString)
                                 .Options))
            {
                await context.Database.MigrateAsync();

                var types = await context.AppointmentTypes.OrderBy(t => t.Code).ToListAsync();
                Assert.Collection(
                    types,
                    type =>
                    {
                        Assert.Equal(AppointmentTypeIds.DrugAndAlcoholTesting, type.Id);
                        Assert.Equal("DAT", type.Code);
                        Assert.Equal("Drug & Alcohol Testing", type.Name);
                    },
                    type =>
                    {
                        Assert.Equal(AppointmentTypeIds.MedicalCheckUp, type.Id);
                        Assert.Equal("MED", type.Code);
                        Assert.Equal("Medical Check-up", type.Name);
                    },
                    type =>
                    {
                        Assert.Equal(AppointmentTypeIds.UniformFitting, type.Id);
                        Assert.Equal("UNI", type.Code);
                        Assert.Equal("Uniform Fitting", type.Name);
                    });

                var settings = await context.SystemSettings.SingleAsync();
                Assert.Equal(1, settings.Id);
                Assert.Equal(7, settings.InviteExpiryDays);
                Assert.Equal(3, settings.InviteOptionCount);
                Assert.Equal(2, settings.MaxAutoRetryCount);
            }

            await using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT data_type
                    FROM information_schema.columns
                    WHERE table_schema = 'public'
                      AND table_name = 'attendee'
                      AND column_name = 'status_changed_at';
                    """;

                Assert.Equal("timestamp with time zone", await command.ExecuteScalarAsync());
            }
        }
        finally
        {
            await DropDatabaseAsync(fixture.ConnectionString, databaseName);
        }
    }

    [Fact]
    public async Task AttendeeStatusTimestampMigrationBackfillsExistingAttendeesWithoutLeavingADefault()
    {
        var databaseName = $"eventbooking_status_backfill_{Guid.NewGuid():N}";
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Database = databaseName,
            Pooling = false,
        }.ConnectionString;
        var attendeeId = Guid.NewGuid();

        try
        {
            await CreateDatabaseAsync(fixture.ConnectionString, databaseName);

            await using (var initialContext = NewContext(connectionString))
            {
                await initialContext.Database.MigrateAsync("20260905060413_InitialSchema");
            }

            await using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    INSERT INTO attendee (id, name, email, status)
                    VALUES (@attendee_id, 'Legacy Attendee', 'legacy.attendee@mail.com', @status);
                    INSERT INTO attendee_requirement (attendee_id, appointment_type_id)
                    VALUES (@attendee_id, @appointment_type_id);
                    """;
                command.Parameters.AddWithValue("attendee_id", attendeeId);
                command.Parameters.AddWithValue("status", (int)AttendeeStatus.NotYetInvited);
                command.Parameters.AddWithValue(
                    "appointment_type_id", AppointmentTypeIds.DrugAndAlcoholTesting);
                await command.ExecuteNonQueryAsync();
            }

            await using (var releaseOneContext = NewContext(connectionString))
            {
                await releaseOneContext.Database.MigrateAsync(ReleaseOneMigration);
            }

            await using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    UPDATE attendee SET attendee_group_id = @group_id WHERE id = @attendee_id;
                    INSERT INTO attendee_requirement (attendee_id, appointment_type_id)
                    VALUES (@attendee_id, @uniform_type_id);
                    """;
                command.Parameters.AddWithValue("attendee_id", attendeeId);
                command.Parameters.AddWithValue("group_id", AttendeeGroupIds.Pilots);
                command.Parameters.AddWithValue(
                    "uniform_type_id", AppointmentTypeIds.UniformFitting);
                await command.ExecuteNonQueryAsync();
            }

            var migrationStartedAt = DateTimeOffset.UtcNow;
            await using (var latestContext = NewContext(connectionString))
            {
                await latestContext.Database.MigrateAsync();
            }
            var migrationFinishedAt = DateTimeOffset.UtcNow;

            await using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT c.status_changed_at, col.column_default
                    FROM attendee AS c
                    CROSS JOIN information_schema.columns AS col
                    WHERE c.id = @attendee_id
                      AND col.table_schema = 'public'
                      AND col.table_name = 'attendee'
                      AND col.column_name = 'status_changed_at';
                    """;
                command.Parameters.AddWithValue("attendee_id", attendeeId);

                await using var reader = await command.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.False(reader.IsDBNull(0));
                var stampedAt = reader.GetFieldValue<DateTimeOffset>(0);
                Assert.InRange(stampedAt, migrationStartedAt.AddSeconds(-1), migrationFinishedAt.AddSeconds(1));
                Assert.True(stampedAt > DateTimeOffset.UnixEpoch);
                Assert.True(reader.IsDBNull(1));
            }
        }
        finally
        {
            await DropDatabaseAsync(fixture.ConnectionString, databaseName);
        }
    }

    [Fact]
    public async Task ResetAsyncReseedsTheFixedAppointmentTypes()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var types = await context.AppointmentTypes.OrderBy(t => t.Code).ToListAsync();

        Assert.Equal(3, types.Count);
        Assert.Equal(new[] { "DAT", "MED", "UNI" }, types.Select(t => t.Code));
        Assert.Equal(AppointmentTypeIds.DrugAndAlcoholTesting, types[0].Id);
    }

    [Fact]
    public async Task ResetAsyncReseedsTheDefaultSettingsRow()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var settings = await context.SystemSettings.SingleAsync();

        Assert.Equal(7, settings.InviteExpiryDays);
        Assert.Equal(3, settings.InviteOptionCount);
        Assert.Equal(2, settings.MaxAutoRetryCount);
    }

    [Fact]
    public async Task AProposalRoundTripsWithItsWindowAndAcceptances()
    {
        await fixture.ResetAsync();

        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);

        await using (var write = fixture.NewContext())
        {
            write.EventProposals.Add(proposal);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var loaded = await read.EventProposals
            .Include(p => p.Acceptances)
            .SingleAsync(p => p.Id == proposal.Id);

        Assert.Equal(new DateOnly(2026, 9, 10), loaded.Window.Date);
        Assert.Equal(new TimeOnly(9, 0), loaded.Window.StartTime);
        Assert.Equal(new TimeOnly(13, 0), loaded.Window.EndTime);
        Assert.Equal(2, loaded.Acceptances.Count);
        Assert.Equal(10, loaded.Acceptances.Single(
            a => a.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting).Headcount);
    }

    [Fact]
    public async Task AEventRoundTripsWithItsThreeCapacityRows()
    {
        await fixture.ResetAsync();

        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        await using (var write = fixture.NewContext())
        {
            write.EventProposals.Add(proposal);
            write.Events.Add(eventItem);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var loaded = await read.Events
            .Include(s => s.Capacities)
            .SingleAsync(s => s.Id == eventItem.Id);

        Assert.Equal(3, loaded.Capacities.Count);
        Assert.Equal(6, loaded.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
    }

    [Fact]
    public async Task TheDatabaseRefusesNegativeRemainingCapacity()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var eventId = await CreateEventWithoutCapacitiesAsync(context);

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO event_capacity
                    (event_id, appointment_type_id, total_headcount, remaining_capacity)
                VALUES ({0}, {1}, 5, -1);
                """,
                [eventId, AppointmentTypeIds.DrugAndAlcoholTesting]));

        Assert.Contains("ck_event_capacity_within_bounds", ex.ToString());
    }

    [Fact]
    public async Task TheDatabaseRefusesRemainingCapacityAboveTotalHeadcount()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var eventId = await CreateEventWithoutCapacitiesAsync(context);

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO event_capacity
                    (event_id, appointment_type_id, total_headcount, remaining_capacity)
                VALUES ({0}, {1}, 5, 6);
                """,
                [eventId, AppointmentTypeIds.DrugAndAlcoholTesting]));

        Assert.Contains("ck_event_capacity_within_bounds", ex.ToString());
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

    private static EventBookingDbContext NewContext(string connectionString) =>
        new(new DbContextOptionsBuilder<EventBookingDbContext>()
            .UseNpgsql(connectionString)
            .Options);

    private static async Task CreateDatabaseAsync(string connectionString, string databaseName)
    {
        await using var connection = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(connectionString)
            {
                Database = "postgres",
                Pooling = false,
            }.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE {databaseName};";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(string connectionString, string databaseName)
    {
        await using var connection = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(connectionString)
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
