using EventBooking.Domain.Access;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Tests.Access;

public sealed class ConfigurableStaffIdTests
{
    [Theory]
    [InlineData("A10023", "A10023")]
    [InlineData(" a10023 ", "A10023")]
    [InlineData("123", "123")]
    public void Default_format_accepts_and_canonicalises_organisation_neutral_identifiers(string input, string expected)
    {
        Assert.Equal(expected, new StaffId(input).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void Default_format_rejects_missing_or_overlong_identifiers(string input)
    {
        Assert.Throws<DomainException>(() => new StaffId(input));
    }

    [Fact]
    public void Deployment_format_restricts_identifiers_after_normalisation()
    {
        Assert.Equal("U123456", new StaffId(" u123456 ", "^[UN][0-9]{6}$").Value);
        Assert.Throws<DomainException>(() => new StaffId("A10023", "^[UN][0-9]{6}$"));
    }
}
