using EventBooking.Domain.Access;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Tests.Access;

/// <summary>Verifies canonical enterprise staff-number validation and identity invariants.</summary>
public sealed class StaffIdTests
{
    /// <summary>Verifies accepted values are exposed in the canonical uppercase form.</summary>
    [Theory]
    [InlineData("U000000", "U000000")]
    [InlineData("N999999", "N999999")]
    [InlineData("u123456", "U123456")]
    [InlineData("n654321", "N654321")]
    public void ValidValuesAreCanonicalised(string input, string expected)
    {
        var staffId = new StaffId(input);

        Assert.Equal(expected, staffId.Value);
        Assert.Equal(expected, staffId.ToString());
    }

    /// <summary>Verifies malformed or absent values cannot enter the domain.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" U123456")]
    [InlineData("U123456 ")]
    [InlineData("X123456")]
    [InlineData("U12345")]
    [InlineData("U1234567")]
    [InlineData("U12345A")]
    public void InvalidValuesCannotBeConstructed(string? input)
    {
        Assert.Throws<DomainException>(() => new StaffId(input!));
        Assert.False(StaffId.TryParse(input, out var parsed));
        Assert.Null(parsed);
    }

    /// <summary>Verifies record equality observes canonical rather than input casing.</summary>
    [Fact]
    public void EqualityUsesTheCanonicalValue()
    {
        Assert.Equal(new StaffId("u123456"), new StaffId("U123456"));
    }

    /// <summary>Verifies the identity pair is immutable while its approximate observation advances.</summary>
    [Fact]
    public void StaffIdentityKeepsThePairAndRefreshesLastSeenOnly()
    {
        var userId = Guid.NewGuid();
        var firstSeen = DateTimeOffset.Parse("2026-09-08T09:00:00Z");
        var identity = StaffIdentity.Create(userId, new StaffId("N123456"), null, firstSeen);

        identity.MarkSeen(null, firstSeen.AddHours(1));

        Assert.Equal(userId, identity.StaffUserId);
        Assert.Equal(new StaffId("N123456"), identity.StaffId);
        Assert.Equal(firstSeen.AddHours(1), identity.LastSeenAt);
    }

    /// <summary>Verifies an identity observed without a name claim mirrors no display name.</summary>
    [Fact]
    public void CreateStoresNullDisplayName()
    {
        var identity = StaffIdentity.Create(
            Guid.NewGuid(), new StaffId("U000002"), null, DateTimeOffset.UtcNow);

        Assert.Null(identity.DisplayName);
    }

    /// <summary>Verifies an observed name is mirrored onto the identity.</summary>
    [Fact]
    public void CreateStoresNonNullDisplayName()
    {
        var identity = StaffIdentity.Create(
            Guid.NewGuid(), new StaffId("U000003"), "Dana Datson", DateTimeOffset.UtcNow);

        Assert.Equal("Dana Datson", identity.DisplayName);
    }

    /// <summary>Verifies a later token without a name clears the mirrored value.</summary>
    [Fact]
    public void MarkSeenOverwritesDisplayNameBackToNull()
    {
        var start = DateTimeOffset.UtcNow;
        var identity = StaffIdentity.Create(
            Guid.NewGuid(), new StaffId("U000004"), "Meddy Medson", start);

        identity.MarkSeen(null, start.AddMinutes(1));

        Assert.Null(identity.DisplayName);
    }

    /// <summary>Verifies a rename observed at the next refresh replaces the mirrored value.</summary>
    [Fact]
    public void MarkSeenUpdatesDisplayNameToNewValue()
    {
        var start = DateTimeOffset.UtcNow;
        var identity = StaffIdentity.Create(Guid.NewGuid(), new StaffId("U000005"), "Old Name", start);

        identity.MarkSeen("New Name", start.AddMinutes(1));

        Assert.Equal("New Name", identity.DisplayName);
        Assert.Equal(start.AddMinutes(1), identity.LastSeenAt);
    }

    /// <summary>Verifies the observation time still refuses to move backwards.</summary>
    [Fact]
    public void MarkSeenStillRejectsBackwardsTime()
    {
        var start = DateTimeOffset.UtcNow;
        var identity = StaffIdentity.Create(Guid.NewGuid(), new StaffId("U000006"), "Old Name", start);

        Assert.Throws<DomainException>(() => identity.MarkSeen("New Name", start.AddMinutes(-1)));
    }
}
