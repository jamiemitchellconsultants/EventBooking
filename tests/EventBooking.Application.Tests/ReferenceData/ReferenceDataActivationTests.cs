using EventBooking.Application.Common;
using EventBooking.Application.ReferenceData;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.Locations;

namespace EventBooking.Application.Tests.ReferenceData;

/// <summary>
/// Design 05's single PUT body carries isActive, so one update applies the name, the address,
/// the zone and the activation in one save — or refuses the whole thing. The doubles are Task
/// 12's, unchanged.
/// </summary>
public sealed class ReferenceDataActivationTests
{
    private static readonly Guid Admin = Guid.Parse("a0000001-0000-0000-0000-000000000001");

    private readonly InMemoryLocationRepository _locations = new();
    private readonly InMemoryStaffAccessProfileRepository _profiles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly MemoryBlocking _blocking = new();

    public ReferenceDataActivationTests() =>
        _profiles.Items.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));

    private UpdateLocationHandler Updater =>
        new(_locations, _profiles, _unitOfWork, _audit, TestZones.Instance, _blocking);

    private ListLocationsHandler Lister => new(_locations);

    [Fact]
    public async Task AnUpdateAppliesTheNameAndTheActivationInOneSave()
    {
        var location = await GivenLocationAsync("ACT_ONE", "Old name");

        var result = await Updater.HandleAsync(
            new UpdateLocationCommand(
                Admin, location.Id, "New name", "1 New Street", "Europe/London",
                IsActive: false, location.Version),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("New name", result.Value.Name);
        Assert.False(result.Value.IsActive);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    /// <summary>
    /// The refusal is the whole command, not the activation half of it. A PUT that renamed the
    /// row and then failed to deactivate it would leave the caller's version stale and their
    /// screen wrong.
    /// </summary>
    [Fact]
    public async Task DeactivatingWhileInUseRefusesTheWholeUpdate()
    {
        var location = await GivenLocationAsync("ACT_TWO", "Old name");
        _blocking.LocationUsageValue = new LocationUsage(1, 2);

        var result = await Updater.HandleAsync(
            new UpdateLocationCommand(
                Admin, location.Id, "New name", "1 New Street", "Europe/London",
                IsActive: false, location.Version),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.ReferenceDataInUseCode, result.Error.Code);
        Assert.Equal(1L, result.Error.Data!["openProposals"]);
        Assert.Equal(2L, result.Error.Data["futureEvents"]);
        var reread = await _locations.GetAsync(location.Id, CancellationToken.None);
        Assert.Equal("Old name", reread!.Name);
        Assert.True(reread.IsActive);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    /// <summary>
    /// An update that leaves the flag alone is not a deactivation, so a location with live
    /// scheduling can still be renamed. Without this case the folded activation would make
    /// every rename fail the moment anything was booked at the site.
    /// </summary>
    [Fact]
    public async Task AnUpdateThatDoesNotChangeActivationIsNotBlockedByUsage()
    {
        var location = await GivenLocationAsync("ACT_THREE", "Old name");
        _blocking.LocationUsageValue = new LocationUsage(5, 5);

        var result = await Updater.HandleAsync(
            new UpdateLocationCommand(
                Admin, location.Id, "New name", "1 New Street", "Europe/London",
                IsActive: true, location.Version),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("New name", result.Value.Name);
        Assert.True(result.Value.IsActive);
    }

    [Fact]
    public async Task TheListHidesInactiveRowsUnlessAsked()
    {
        var active = await GivenLocationAsync("ACT_LIVE", "Live");
        var retired = await GivenLocationAsync("ACT_DEAD", "Retired");
        await Updater.HandleAsync(
            new UpdateLocationCommand(
                Admin, retired.Id, "Retired", "1 Old Street", "Europe/London",
                IsActive: false, retired.Version),
            CancellationToken.None);

        var hidden = await Lister.HandleAsync(
            new ListLocationsQuery(IncludeInactive: false), CancellationToken.None);
        var shown = await Lister.HandleAsync(
            new ListLocationsQuery(IncludeInactive: true), CancellationToken.None);

        Assert.Equal([active.Id], hidden.Value.Select(x => x.Id));
        Assert.Equal(2, shown.Value.Count);
    }

    /// <summary>Codes order the page, so the flag cannot reorder what the caller already saw.</summary>
    [Fact]
    public async Task TheListStaysInCodeOrderEitherWay()
    {
        await GivenLocationAsync("ACT_ZULU", "Zulu");
        await GivenLocationAsync("ACT_ALPHA", "Alpha");

        var shown = await Lister.HandleAsync(
            new ListLocationsQuery(IncludeInactive: true), CancellationToken.None);

        Assert.Equal(
            shown.Value.Select(x => x.Code).Order(StringComparer.Ordinal),
            shown.Value.Select(x => x.Code));
    }

    private async Task<Location> GivenLocationAsync(string code, string name)
    {
        var created = await new CreateLocationHandler(
                _locations, _profiles, _unitOfWork, _audit, TestZones.Instance)
            .HandleAsync(
                new CreateLocationCommand(Admin, code, name, "1 Old Street", "Europe/London"),
                CancellationToken.None);
        _unitOfWork.Reset();
        return (await _locations.GetAsync(created.Value.Id, CancellationToken.None))!;
    }
}
