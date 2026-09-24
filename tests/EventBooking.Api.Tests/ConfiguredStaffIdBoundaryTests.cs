using System.Security.Claims;
using EventBooking.Api.Auth;
using EventBooking.Application.Access;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EventBooking.Api.Tests;

public sealed class ConfiguredStaffIdBoundaryTests
{
    [Theory]
    [InlineData(" a10023 ", "^[A-Z0-9]{1,32}$", "A10023")]
    [InlineData(" u123456 ", "^[UN][0-9]{6}$", "U123456")]
    [InlineData("A10023", "^[UN][0-9]{6}$", null)]
    public void Request_identity_uses_the_configured_policy(string input, string pattern, string? expected)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("staff_id", input)], "test")),
        };
        var accessor = new HttpContextCallerAccessor(new HttpContextAccessor { HttpContext = context },
            NullLogger<HttpContextCallerAccessor>.Instance,
            Options.Create(new AuthClaimOptions { StaffIdPattern = pattern }));
        Assert.Equal(expected, accessor.StaffId?.Value);
    }

    [Fact]
    public void Invalid_deployment_expression_fails_during_policy_construction()
    {
        Assert.ThrowsAny<ArgumentException>(() => new StaffIdPolicy("["));
    }
}
