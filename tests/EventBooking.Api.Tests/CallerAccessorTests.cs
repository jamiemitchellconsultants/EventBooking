using System.Security.Claims;
using EventBooking.Api.Auth;
using EventBooking.Domain.Access;

namespace EventBooking.Api.Tests;

public class CallerAccessorTests
{
    private static readonly Guid ObjectId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void TheShortObjectIdentifierClaimIsRead()
    {
        var principal = PrincipalWith(HttpContextCallerAccessor.ShortObjectIdClaim, ObjectId.ToString());

        Assert.Equal(ObjectId, HttpContextCallerAccessor.StaffUserIdOf(principal));
    }

    [Fact]
    public void TheLongObjectIdentifierClaimIsRead()
    {
        var principal = PrincipalWith(HttpContextCallerAccessor.ObjectIdClaim, ObjectId.ToString());

        Assert.Equal(ObjectId, HttpContextCallerAccessor.StaffUserIdOf(principal));
    }

    [Fact]
    public void AnUnauthenticatedPrincipalHasNoStaffIdentity()
    {
        Assert.Null(HttpContextCallerAccessor.StaffUserIdOf(null));
        Assert.Null(HttpContextCallerAccessor.StaffUserIdOf(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    [Fact]
    public void AnUnauthenticatedIdentityObjectIdentifierIsIgnored()
    {
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(HttpContextCallerAccessor.ShortObjectIdClaim, ObjectId.ToString())]));

        Assert.Null(HttpContextCallerAccessor.StaffUserIdOf(principal));
    }

    [Fact]
    public void AnObjectIdentifierCannotBeReadFromAnIdentityOtherThanTheAuthenticatedIdentity()
    {
        var principal = new ClaimsPrincipal(
        [
            new ClaimsIdentity([new Claim(HttpContextCallerAccessor.ShortObjectIdClaim, ObjectId.ToString())]),
            new ClaimsIdentity(authenticationType: "test"),
        ]);

        Assert.Null(HttpContextCallerAccessor.StaffUserIdOf(principal));
    }

    [Fact]
    public void AClaimThatIsNotAnIdentifierIsIgnored()
    {
        var principal = PrincipalWith(HttpContextCallerAccessor.ShortObjectIdClaim, "not-a-guid");

        Assert.Null(HttpContextCallerAccessor.StaffUserIdOf(principal));
    }

    private static ClaimsPrincipal PrincipalWith(string type, string value) =>
        new(new ClaimsIdentity([new Claim(type, value)], "test"));

    [Fact]
    public void KnownRoleClaimValuesAreParsed()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("roles", "Coordinator"),
                new Claim("roles", "Manager"),
            ],
            "test"));

        var roles = HttpContextCallerAccessor.RolesOf(principal);

        Assert.Equal(new HashSet<Role> { Role.Coordinator, Role.Manager }, roles);
    }

    [Fact]
    public void AnUnrecognisedRoleClaimValueIsDroppedNotThrown()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("roles", "Coordinator"),
                new Claim("roles", "SuperUser"),
            ],
            "test"));

        var rejected = new List<string>();
        var roles = HttpContextCallerAccessor.RolesOf(principal, rejected.Add);

        Assert.Equal(new HashSet<Role> { Role.Coordinator }, roles);
        Assert.Equal(["SuperUser"], rejected);
    }

    [Fact]
    public void AnAbsentRolesClaimYieldsAnEmptySet()
    {
        var principal = PrincipalWith(HttpContextCallerAccessor.ShortObjectIdClaim, ObjectId.ToString());

        Assert.Empty(HttpContextCallerAccessor.RolesOf(principal));
        Assert.Empty(HttpContextCallerAccessor.RolesOf(null));
    }

    [Fact]
    public void GroupsClaimAloneDoesNotCreateAnApplicationRole()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("groups", "Manager")],
            "test"));

        Assert.Empty(HttpContextCallerAccessor.RolesOf(principal));
    }

    [Fact]
    public void RolesAreReadFromALaterAuthenticatedIdentityWhenTheFirstHasNoRolesClaim()
    {
        var principal = new ClaimsPrincipal(
        [
            new ClaimsIdentity(
                [new Claim(HttpContextCallerAccessor.ShortObjectIdClaim, ObjectId.ToString())], "test"),
            new ClaimsIdentity(
                [new Claim("roles", "Manager")], "test"),
        ]);

        var roles = HttpContextCallerAccessor.RolesOf(principal);

        Assert.Equal(new HashSet<Role> { Role.Manager }, roles);
    }

    [Fact]
    public void RoleNamesAreCaseSensitiveAndDuplicatesCollapse()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("roles", "Manager"),
                new Claim("roles", "Manager"),
                new Claim("roles", "manager"),
            ],
            "test"));
        var rejected = new List<string>();

        var roles = HttpContextCallerAccessor.RolesOf(principal, rejected.Add);

        Assert.Equal(new HashSet<Role> { Role.Manager }, roles);
        Assert.Equal(["manager"], rejected);
    }

    /// <summary>Verifies a present name claim is surfaced verbatim.</summary>
    [Fact]
    public void DisplayNameReturnsClaimValueWhenPresent()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("name", "Dana Datson"), new Claim("staff_id", "U000002")], "test"));

        Assert.Equal("Dana Datson", HttpContextCallerAccessor.DisplayNameOf(principal));
    }

    /// <summary>Verifies an absent name claim reads as no name rather than an empty string.</summary>
    [Fact]
    public void DisplayNameReturnsNullWhenClaimAbsent()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("staff_id", "U000002")], "test"));

        Assert.Null(HttpContextCallerAccessor.DisplayNameOf(principal));
    }

    /// <summary>Verifies an empty or whitespace name claim reads as no name.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DisplayNameReturnsNullWhenClaimBlank(string value)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("name", value)], "test"));

        Assert.Null(HttpContextCallerAccessor.DisplayNameOf(principal));
    }

    /// <summary>Verifies an unauthenticated identity contributes no name.</summary>
    [Fact]
    public void DisplayNameIgnoresUnauthenticatedIdentity()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("name", "Dana Datson")]));

        Assert.Null(HttpContextCallerAccessor.DisplayNameOf(principal));
    }
}
