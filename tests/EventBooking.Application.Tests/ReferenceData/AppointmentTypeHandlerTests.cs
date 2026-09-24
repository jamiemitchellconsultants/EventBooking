using EventBooking.Application.Access;
using EventBooking.Application.ReferenceData;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;

namespace EventBooking.Application.Tests.ReferenceData;

public sealed class AppointmentTypeHandlerTests
{
    private static readonly Guid Admin = Guid.Parse("a0000001-0000-0000-0000-000000000001");

    private readonly InMemoryAppointmentTypeRepository _types = new();
    private readonly InMemoryStaffAccessProfileRepository _profiles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();

    public AppointmentTypeHandlerTests()
    {
        _profiles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));
        _types.Items.Clear();
    }

    private CreateAppointmentTypeHandler Creator => new(_types, _profiles, _unitOfWork, _audit);

    [Fact]
    public async Task Create_type_writes_AppointmentTypeCreated()
    {
        var result = await Creator.HandleAsync(
            new CreateAppointmentTypeCommand(Admin, "med", "Medical"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("MED", result.Value.Code);
        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AuditAction.AppointmentTypeCreated, entry.Action);
        Assert.Equal(AuditEntityTypes.AppointmentType, entry.EntityType);
    }

    [Fact]
    public async Task Deactivate_type_mapped_by_active_group_returns_in_use_naming_group_count()
    {
        var created = await Creator.HandleAsync(
            new CreateAppointmentTypeCommand(Admin, "MED", "Medical"),
            CancellationToken.None);
        var blocking = new MemoryBlocking { TypeUsageValue = new AppointmentTypeUsage(0, 0, 2) };
        var updater = new UpdateAppointmentTypeHandler(_types, _profiles, _unitOfWork, _audit, blocking);

        var result = await updater.HandleAsync(
            new UpdateAppointmentTypeCommand(Admin, created.Value.Id, null, false, 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("in-use", result.Error.Code);
        Assert.Equal(2L, result.Error.Data!["activeGroups"]);
        Assert.True(_types.Items.Single().IsActive);
    }

    [Fact]
    public async Task Manager_display_name_falls_back_to_staff_id()
    {
        var type = AppointmentType.Create(AppointmentTypeIds.MedicalCheckUp, "MED", "Medical Check-up");
        _types.Items.Add(type);
        var manager = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(manager, Role.Manager, type.Id));
        var identities = new InMemoryStaffIdentityRepository();
        await identities.UpsertAsync(manager, new StaffId("M100"), null, DateTimeOffset.UtcNow, CancellationToken.None);
        var lister = new ListAppointmentTypesHandler(_types, _profiles, identities);

        var result = await lister.HandleAsync(
            new ListAppointmentTypesQuery(IncludeInactive: false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value);
        Assert.Equal("M100", item.ManagerDisplayName);
    }
}
