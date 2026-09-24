using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.ReferenceData;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Locations;
using EventBooking.Domain.Time;

namespace EventBooking.Application.Tests.ReferenceData;

public sealed class LocationHandlerTests
{
    private static readonly Guid Admin = Guid.Parse("a0000001-0000-0000-0000-000000000001");

    private readonly InMemoryLocationRepository _locations = new();
    private readonly InMemoryStaffAccessProfileRepository _profiles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();

    public LocationHandlerTests()
    {
        _profiles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));
    }

    private CreateLocationHandler Creator => new(_locations, _profiles, _unitOfWork, _audit, TestZones.Instance);
    private UpdateLocationHandler Updater => new(_locations, _profiles, _unitOfWork, _audit, TestZones.Instance, new MemoryBlocking());

    [Fact]
    public async Task Create_location_writes_LocationCreated_with_no_personal_data()
    {
        var result = await Creator.HandleAsync(
            new CreateLocationCommand(Admin, "london_hq", "London HQ", "1 High St", "Europe/London"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("LONDON_HQ", result.Value.Code);
        Assert.Equal(1, result.Value.Version);
        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AuditAction.LocationCreated, entry.Action);
        Assert.Equal(AuditEntityTypes.Location, entry.EntityType);
        Assert.Equal(ActorType.Staff, entry.ActorType);
        Assert.Equal(Admin.ToString(), entry.ActorId);
        Assert.Contains("LONDON_HQ", entry.Details);
        Assert.Contains("Europe/London", entry.Details);
        Assert.DoesNotContain("@", entry.Details);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_location_with_unknown_zone_is_refused()
    {
        var result = await Creator.HandleAsync(
            new CreateLocationCommand(Admin, "NOWHERE", "Nowhere", "2 Lost Rd", "Moon/Olympus"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Empty(_locations.Items);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task Create_location_with_duplicate_code_is_refused()
    {
        await Creator.HandleAsync(
            new CreateLocationCommand(Admin, "LONDON_HQ", "London HQ", "1 High St", "Europe/London"),
            CancellationToken.None);

        var result = await Creator.HandleAsync(
            new CreateLocationCommand(Admin, "london_hq", "Other", "9 Elsewhere", "Europe/London"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Single(_locations.Items);
    }

    [Fact]
    public async Task Zone_change_while_scheduled_returns_in_use_with_both_counts()
    {
        var created = await Creator.HandleAsync(
            new CreateLocationCommand(Admin, "LONDON_HQ", "London HQ", "1 High St", "Europe/London"),
            CancellationToken.None);
        var blocking = new MemoryBlocking { LocationUsageValue = new LocationUsage(1, 2) };
        var updater = new UpdateLocationHandler(_locations, _profiles, _unitOfWork, _audit, TestZones.Instance, blocking);

        var result = await updater.HandleAsync(
            new UpdateLocationCommand(Admin, created.Value.Id, null, null, "Asia/Tokyo", true, 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("in-use", result.Error.Code);
        Assert.Equal(1L, result.Error.Data!["openProposals"]);
        Assert.Equal(2L, result.Error.Data!["futureEvents"]);
        Assert.Equal("Europe/London", _locations.Items.Single().TimeZoneId);
        Assert.Single(_audit.Entries);
    }

    [Fact]
    public async Task Stale_version_returns_version_conflict_with_current_state()
    {
        var created = await Creator.HandleAsync(
            new CreateLocationCommand(Admin, "LONDON_HQ", "London HQ", "1 High St", "Europe/London"),
            CancellationToken.None);

        var result = await Updater.HandleAsync(
            new UpdateLocationCommand(Admin, created.Value.Id, "Renamed", null, null, true, 99),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("version-conflict", result.Error.Code);
        Assert.Equal(1L, result.Error.Data!["currentVersion"]);
        Assert.Equal("London HQ", _locations.Items.Single().Name);
    }

    [Fact]
    public async Task Non_admin_cannot_create_a_location()
    {
        var coordinator = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(coordinator, Role.Coordinator, null));

        var result = await Creator.HandleAsync(
            new CreateLocationCommand(coordinator, "PARIS", "Paris", "3 Rue", "Europe/Paris"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Empty(_locations.Items);
    }
}
