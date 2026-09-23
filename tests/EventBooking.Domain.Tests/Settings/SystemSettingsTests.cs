using EventBooking.Domain.Common;
using EventBooking.Domain.Settings;

namespace EventBooking.Domain.Tests.Settings;

public class SystemSettingsTests
{
    [Fact]
    public void TheDefaultsMatchTheAdminScreenWireframe()
    {
        var settings = SystemSettings.CreateDefault();

        Assert.Equal(1, settings.Id);
        Assert.Equal(4, settings.InviteExpiryDays);
        Assert.Equal(2, settings.MaxAutoRetryCount);
    }

    [Fact]
    public void UpdateStoresBothValues()
    {
        var settings = SystemSettings.CreateDefault();

        settings.Update(7, 0);

        Assert.Equal(7, settings.InviteExpiryDays);
        Assert.Equal(0, settings.MaxAutoRetryCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ExpiryMustBeAPositiveNumberOfDays(int days)
    {
        var settings = SystemSettings.CreateDefault();

        var ex = Assert.Throws<DomainException>(() => settings.Update(days, 2));
        Assert.Equal("inviteExpiryDays must be greater than zero.", ex.Message);
    }

    [Fact]
    public void RetryCountMayBeZeroButNotNegative()
    {
        var settings = SystemSettings.CreateDefault();

        settings.Update(4, 0);
        Assert.Equal(0, settings.MaxAutoRetryCount);

        var ex = Assert.Throws<DomainException>(() => settings.Update(4, -1));
        Assert.Equal("maxAutoRetryCount must not be negative.", ex.Message);
    }

    [Fact]
    public void ARejectedUpdateLeavesTheSettingsUnchanged()
    {
        var settings = SystemSettings.CreateDefault();

        Assert.Throws<DomainException>(() => settings.Update(0, 99));

        Assert.Equal(4, settings.InviteExpiryDays);
        Assert.Equal(2, settings.MaxAutoRetryCount);
    }
}
