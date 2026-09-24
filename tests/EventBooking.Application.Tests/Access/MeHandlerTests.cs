using System.Reflection;
using EventBooking.Application.Access;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Tests.Access;

public sealed class MeHandlerTests
{
    [Fact]
    public async Task An_invalid_profile_is_reported_not_rendered()
    {
        var fixture = AccessFixture.Create();
        var staffUserId = Guid.NewGuid();
        var corrupt = (StaffAccessProfile)Activator.CreateInstance(
            typeof(StaffAccessProfile), nonPublic: true)!;
        typeof(StaffAccessProfile)
            .GetProperty(nameof(StaffAccessProfile.StaffUserId))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(corrupt, [staffUserId]);
        fixture.Profiles.Items.Add(corrupt);
        var token = new AccessToken { StaffUserId = staffUserId };
        Assert.False(corrupt.IsValid());

        var me = await new MeHandler(fixture.Profiles)
            .HandleAsync(token.StaffUserId, token.StaffIdValue, token.DisplayName,
                token.Roles.ToHashSet(), token.StaffIdPattern, CancellationToken.None);

        Assert.True(me.IsSuccess);
        Assert.Empty(me.Value.Capabilities);
        Assert.Contains("not valid", me.Value.Problem);
    }
}
