using System.Security.Claims;
using EventBooking.Api.Auth;

namespace EventBooking.Api.Tests;

/// <summary>Verifies untrusted staff-number claims are parsed at the request boundary.</summary>
public sealed class CallerIdentityTests
{
    /// <summary>Verifies valid claim casing is normalized into the domain value.</summary>
    [Theory]
    [InlineData("u123456", "U123456")]
    [InlineData("N654321", "N654321")]
    public void ValidClaimIsParsedAndCanonicalised(string claim, string expected)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("staff_id", claim)], "test"));

        Assert.Equal(expected, HttpContextCallerAccessor.StaffIdOf(principal)!.Value);
    }

    /// <summary>Verifies absent and malformed claims are indistinguishable and non-throwing.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("invalid staff id")]
    public void MissingOrMalformedClaimReturnsNull(string? claim)
    {
        var claims = claim is null ? [] : new[] { new Claim("staff_id", claim) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        Assert.Null(HttpContextCallerAccessor.StaffIdOf(principal));
    }
}
