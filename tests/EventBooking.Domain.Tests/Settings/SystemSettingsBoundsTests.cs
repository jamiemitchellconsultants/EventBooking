using EventBooking.Domain.Common;
using EventBooking.Domain.Settings;

namespace EventBooking.Domain.Tests.Settings;

/// <summary>
/// Task 5: settings gain the option count, and every value takes the design's bounds
/// (FR-1.9; design 08 — boundary values).
/// </summary>
public class SystemSettingsBoundsTests
{
    [Fact]
    public void TheDefaultsMatchTheDesign()
    {
        var settings = SystemSettings.CreateDefault();

        Assert.Equal(7, settings.InviteExpiryDays);
        Assert.Equal(2, settings.MaxAutoRetryCount);
        Assert.Equal(3, settings.InviteOptionCount);
    }

    [Theory]
    [InlineData(1, 0, 1)]
    [InlineData(60, 10, 5)]
    public void ValuesInsideTheBoundsAreAccepted(int expiryDays, int retries, int options)
    {
        var settings = SystemSettings.CreateDefault();

        settings.Update(expiryDays, retries, options);

        Assert.Equal(expiryDays, settings.InviteExpiryDays);
        Assert.Equal(retries, settings.MaxAutoRetryCount);
        Assert.Equal(options, settings.InviteOptionCount);
    }

    [Theory]
    [InlineData(0, 2, 3)]
    [InlineData(61, 2, 3)]
    [InlineData(7, -1, 3)]
    [InlineData(7, 11, 3)]
    [InlineData(7, 2, 0)]
    [InlineData(7, 2, 6)]
    public void ValuesOutsideTheBoundsAreRefusedWithoutChangingAnything(int expiryDays, int retries, int options)
    {
        var settings = SystemSettings.CreateDefault();

        Assert.Throws<DomainException>(() => settings.Update(expiryDays, retries, options));

        Assert.Equal(7, settings.InviteExpiryDays);
        Assert.Equal(2, settings.MaxAutoRetryCount);
        Assert.Equal(3, settings.InviteOptionCount);
    }
}
