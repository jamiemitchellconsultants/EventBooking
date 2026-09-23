using EventBooking.Domain.Common;
using EventBooking.Domain.Locations;
using EventBooking.Domain.Time;

namespace EventBooking.Domain.Tests.Locations;

/// <summary>
/// Task 5: a Location is Admin-managed reference data. Its code is immutable, its zone cannot move
/// under live events, and nothing is ever hard-deleted (design 01 — Reference data, FR-1.1 to FR-1.7).
/// </summary>
public class LocationTests
{
    private static readonly KnownZones Zones = new();

    private static Location Create(string code = "LONDON_HQ", string zone = "Europe/London") =>
        Location.Create(Guid.NewGuid(), code, "London HQ", "1 Example St, London", zone, Zones);

    [Fact]
    public void ACreatedLocationCarriesItsDetailsAndStartsActiveAtVersionOne()
    {
        var location = Create();

        Assert.Equal("LONDON_HQ", location.Code);
        Assert.Equal("London HQ", location.Name);
        Assert.Equal("1 Example St, London", location.Address);
        Assert.Equal("Europe/London", location.TimeZoneId);
        Assert.True(location.IsActive);
        Assert.Equal(1, location.Version);
    }

    [Theory]
    [InlineData("london_hq", "LONDON_HQ")]
    [InlineData("  dublin  ", "DUBLIN")]
    public void ACodeIsAcceptedCaseInsensitivelyAndStoredCanonically(string input, string stored)
    {
        Assert.Equal(stored, Create(input).Code);
    }

    [Theory]
    [InlineData("1LONDON")]
    [InlineData("LONDON-HQ")]
    [InlineData("LONDON HQ")]
    [InlineData("")]
    public void AnUncanonicalCodeIsRefused(string code)
    {
        Assert.Throws<DomainException>(() => Create(code));
    }

    [Fact]
    public void AnUnknownTimeZoneIsRefused()
    {
        Assert.Throws<DomainException>(() => Create(zone: "Mars/Olympus_Mons"));
    }

    [Fact]
    public void TheCodeCannotBeChangedAfterCreation()
    {
        Assert.Null(typeof(Location).GetProperty(nameof(Location.Code))!.SetMethod?.IsPublic == true ? "settable" : null);
    }

    [Fact]
    public void RenamingAndReaddressingAreAlwaysAllowedAndBumpTheVersion()
    {
        var location = Create();

        location.Rename("London head office");
        location.ChangeAddress("2 Sample Rd, London");

        Assert.Equal("London head office", location.Name);
        Assert.Equal("2 Sample Rd, London", location.Address);
        Assert.Equal(3, location.Version);
    }

    [Fact]
    public void TheZoneMayChangeWhileNothingIsScheduled()
    {
        var location = Create();

        location.ChangeTimeZone("Europe/Dublin", Zones, LocationUsage.None);

        Assert.Equal("Europe/Dublin", location.TimeZoneId);
    }

    [Fact]
    public void TheZoneCannotMoveUnderOpenProposalsOrFutureEvents()
    {
        var location = Create();

        var refusal = Assert.Throws<ReferenceDataInUseException>(
            () => location.ChangeTimeZone("Europe/Dublin", Zones, new LocationUsage(1, 2)));

        Assert.Equal("Europe/London", location.TimeZoneId);
        Assert.Equal(1, refusal.Blocking["openProposals"]);
        Assert.Equal(2, refusal.Blocking["futureEvents"]);
    }

    [Fact]
    public void DeactivationIsRefusedWhileTheLocationIsInUseAndAllowedOtherwise()
    {
        var location = Create();

        Assert.Throws<ReferenceDataInUseException>(() => location.Deactivate(new LocationUsage(0, 3)));
        Assert.True(location.IsActive);

        location.Deactivate(LocationUsage.None);
        Assert.False(location.IsActive);

        location.Reactivate();
        Assert.True(location.IsActive);
    }

    [Fact]
    public void NameAndAddressLengthsAreBounded()
    {
        var location = Create();

        Assert.Throws<DomainException>(() => location.Rename(new string('x', 101)));
        Assert.Throws<DomainException>(() => location.ChangeAddress(new string('x', 501)));
    }

    private sealed class KnownZones : IEventWindowZones
    {
        public bool IsKnownZone(string timeZoneId) =>
            timeZoneId is "Europe/London" or "Europe/Dublin" or "Asia/Tokyo";

        public LocalTimeValidity ValidityOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            LocalTimeValidity.Unique;

        public DateTimeOffset InstantOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            new(date.ToDateTime(time), TimeSpan.Zero);

        public DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId) =>
            DateOnly.FromDateTime(instant.UtcDateTime);

        public string AbbreviationOf(DateTimeOffset instant, string timeZoneId) => "BST";
    }
}
