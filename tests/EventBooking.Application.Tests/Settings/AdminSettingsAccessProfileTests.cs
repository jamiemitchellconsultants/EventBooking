using EventBooking.Application.Access;
using EventBooking.Application.Settings;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Tests.Settings;

public class AdminSettingsAccessProfileTests
{
    [Fact]
    public async Task SettingsDerivesEachManagerFromTheAccessProfile()
    {
        var admin = Guid.NewGuid();
        var manager = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(admin, [Role.Admin], null));
        profiles.Add(StaffAccessProfile.Create(
            manager, [Role.Manager], AppointmentTypeIds.MedicalCheckUp));
        var handler = new AdminSettingsHandler(
            new InMemorySystemSettingsRepository(),
            new InMemoryAppointmentTypeRepository(),
            profiles,
            new InMemoryStaffIdentityRepository(),
            new StaffAccessAuthorizer(profiles),
            new FakeUnitOfWork(),
            new RecordingAuditLogger());

        var result = await handler.GetAsync(admin, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            manager,
            result.Value.AppointmentTypes.Single(
                type => type.Id == AppointmentTypeIds.MedicalCheckUp).ManagerUserId);
    }

    /// <summary>Verifies an observed manager name reaches the settings view with its staff number.</summary>
    [Fact]
    public async Task GetPopulatesManagerDisplayNameWhenKnown()
    {
        var admin = Guid.NewGuid();
        var manager = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(admin, [Role.Admin], null));
        profiles.Add(StaffAccessProfile.Create(
            manager, [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting));
        var identities = new InMemoryStaffIdentityRepository();
        await identities.UpsertAsync(
            manager, new StaffId("U000002"), "Dana Datson",
            DateTimeOffset.Parse("2026-09-08T10:00:00Z"), CancellationToken.None);
        var handler = new AdminSettingsHandler(
            new InMemorySystemSettingsRepository(),
            new InMemoryAppointmentTypeRepository(),
            profiles,
            identities,
            new StaffAccessAuthorizer(profiles),
            new FakeUnitOfWork(),
            new RecordingAuditLogger());

        var result = await handler.GetAsync(admin, CancellationToken.None);

        var view = result.Value.AppointmentTypes.Single(
            type => type.Id == AppointmentTypeIds.DrugAndAlcoholTesting);
        Assert.Equal("U000002", view.ManagerStaffId);
        Assert.Equal("Dana Datson", view.ManagerDisplayName);
    }

    /// <summary>Verifies an identity without a name still surfaces its staff number.</summary>
    [Fact]
    public async Task GetLeavesManagerDisplayNameNullWhenUnknown()
    {
        var admin = Guid.NewGuid();
        var manager = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(admin, [Role.Admin], null));
        profiles.Add(StaffAccessProfile.Create(
            manager, [Role.Manager], AppointmentTypeIds.UniformFitting));
        var identities = new InMemoryStaffIdentityRepository();
        await identities.UpsertAsync(
            manager, new StaffId("U000003"), null,
            DateTimeOffset.Parse("2026-09-08T10:00:00Z"), CancellationToken.None);
        var handler = new AdminSettingsHandler(
            new InMemorySystemSettingsRepository(),
            new InMemoryAppointmentTypeRepository(),
            profiles,
            identities,
            new StaffAccessAuthorizer(profiles),
            new FakeUnitOfWork(),
            new RecordingAuditLogger());

        var result = await handler.GetAsync(admin, CancellationToken.None);

        var view = result.Value.AppointmentTypes.Single(
            type => type.Id == AppointmentTypeIds.UniformFitting);
        Assert.Equal("U000003", view.ManagerStaffId);
        Assert.Null(view.ManagerDisplayName);
    }
}
