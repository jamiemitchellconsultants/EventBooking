using EventBooking.Application.Common;
using EventBooking.Application.Invites;

namespace EventBooking.Application.Tests.Invites;

public sealed class CountEligibleEventsHandlerTests
{
    private static CountEligibleEventsHandler Handler(InviteFixture f) => new(
        f.Attendees, f.Locations, f.Eligibility, f.Profiles);

    [Fact]
    public async Task Count_returns_the_eligible_total_for_the_dialog()
    {
        var fixture = InviteFixture.Create(optionCount: 3).WithEligibleEvents(5);

        var result = await Handler(fixture).HandleAsync(
            new CountEligibleEventsQuery(fixture.Coordinator, fixture.AttendeeId, fixture.LocationIds),
            CancellationToken.None);

        Assert.True(result.IsSuccess, $"result failed: {result.Error?.Code} {result.Error?.Message}");
        Assert.Equal(5, result.Value);
    }

    [Fact]
    public async Task Count_with_inactive_location_is_refused()
    {
        var fixture = InviteFixture.Create(optionCount: 3).WithEligibleEvents(5).WithInactiveLocation();

        var result = await Handler(fixture).HandleAsync(
            new CountEligibleEventsQuery(fixture.Coordinator, fixture.AttendeeId, fixture.LocationIds),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
    }
}
