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
