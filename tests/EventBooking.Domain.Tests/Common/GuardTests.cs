using EventBooking.Domain.Common;

namespace EventBooking.Domain.Tests.Common;

public class GuardTests
{
    [Fact]
    public void NotBlankTrimsAndReturnsTheValue()
    {
        Assert.Equal("Amara Novak", Guard.NotBlank("  Amara Novak  ", "name"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NotBlankRejectsMissingValues(string? value)
    {
        var ex = Assert.Throws<DomainException>(() => Guard.NotBlank(value, "name"));
        Assert.Equal("name must not be blank.", ex.Message);
    }

    [Fact]
    public void PositiveAcceptsOneAndRejectsZero()
    {
        Assert.Equal(1, Guard.Positive(1, "headcount"));
        var ex = Assert.Throws<DomainException>(() => Guard.Positive(0, "headcount"));
        Assert.Equal("headcount must be greater than zero.", ex.Message);
    }

    [Fact]
    public void NotNegativeAcceptsZeroAndRejectsMinusOne()
    {
        Assert.Equal(0, Guard.NotNegative(0, "maxAutoRetryCount"));
        Assert.Throws<DomainException>(() => Guard.NotNegative(-1, "maxAutoRetryCount"));
    }

    [Fact]
    public void AgainstThrowsOnlyWhenTheConditionHolds()
    {
        Guard.Against(false, "never thrown");
        var ex = Assert.Throws<DomainException>(() => Guard.Against(true, "boom"));
        Assert.Equal("boom", ex.Message);
    }
}
