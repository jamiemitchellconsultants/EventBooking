using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class StaffAccessProfilePersistenceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task ACombinedProfileRoundTripsWithItsVersionAndScope()
    {
        await fixture.ResetAsync();
        var id = Guid.NewGuid();

        await using (var write = fixture.NewContext())
        {
            write.StaffAccessProfiles.Add(StaffAccessProfile.Create(
                id,
                [Role.Coordinator, Role.Manager, Role.AppointmentStaff],
                AppointmentTypeIds.MedicalCheckUp));
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var profile = await new StaffAccessProfileRepository(read)
            .GetAsync(id, CancellationToken.None);

        Assert.NotNull(profile);
        Assert.True(new HashSet<Role>
        {
            Role.Manager,
            Role.Coordinator,
            Role.AppointmentStaff,
        }.SetEquals(profile!.Roles));
        Assert.Equal(AppointmentTypeIds.MedicalCheckUp, profile.AppointmentTypeId);
        Assert.Equal(1, profile.Version);
    }

    [Fact]
    public async Task TheDatabaseRejectsTwoManagersForOneAppointmentType()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        context.StaffAccessProfiles.AddRange(
            StaffAccessProfile.Create(
                Guid.NewGuid(), [Role.Manager], AppointmentTypeIds.UniformFitting),
            StaffAccessProfile.Create(
                Guid.NewGuid(), [Role.Manager], AppointmentTypeIds.UniformFitting));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task MultipleAppointmentStaffForOneTypeAreAllowed()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        context.StaffAccessProfiles.AddRange(
            StaffAccessProfile.Create(
                Guid.NewGuid(), [Role.AppointmentStaff], AppointmentTypeIds.UniformFitting),
            StaffAccessProfile.Create(
                Guid.NewGuid(), [Role.AppointmentStaff], AppointmentTypeIds.UniformFitting));
        await context.SaveChangesAsync();

        Assert.Equal(2, await context.StaffAccessProfiles.CountAsync());
    }

    [Fact]
    public async Task TheDatabaseAcceptsAScopedRoleWithNullScope()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        context.StaffAccessProfiles.Add(StaffAccessProfile.Create(
            Guid.NewGuid(), [Role.Manager], null));

        // No exception: the relaxed constraint accepts this shape.
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task TheDatabaseStillRejectsScopeWithoutAScopedRole()
    {
        await fixture.ResetAsync();
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO staff_access_profile "
            + "(staff_user_id, is_admin, is_coordinator, is_manager, is_appointment_staff, "
            + "appointment_type_id, version) "
            + "VALUES (@id, false, true, false, false, @type, 1)";
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("type", AppointmentTypeIds.DrugAndAlcoholTesting);

        await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
    }
}
