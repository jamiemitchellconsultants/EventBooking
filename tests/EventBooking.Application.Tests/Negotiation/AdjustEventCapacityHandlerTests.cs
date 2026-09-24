using EventBooking.Application.Common;
using EventBooking.Application.Negotiation;

namespace EventBooking.Application.Tests.Negotiation;

public sealed class AdjustEventCapacityHandlerTests
{
    [Fact]
    public async Task Adjust_other_type_capacity_is_forbidden()
    {
        var fixture = NegotiationFixture.Create().WithTypes("MED", "FIT");
        var proposed = await fixture.ProposeAsync("MED", ["MED"], headcount: 6);
        var handler = new AdjustEventCapacityHandler(
            fixture.Events, fixture.Capacities, fixture.Profiles, fixture.UnitOfWork,
            fixture.Audit, fixture.Types);

        var result = await handler.HandleAsync(new AdjustEventCapacityCommand(
            fixture.Managers["FIT"], proposed.EventId!.Value, 4), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Adjust_naming_another_type_in_the_route_is_refused_and_changes_nothing()
    {
        var fixture = NegotiationFixture.Create().WithTypes("MED", "FIT");
        var proposed = await fixture.ProposeAsync("MED", ["MED"], headcount: 6);
        var handler = new AdjustEventCapacityHandler(
            fixture.Events, fixture.Capacities, fixture.Profiles, fixture.UnitOfWork,
            fixture.Audit, fixture.Types);

        var result = await handler.HandleAsync(new AdjustEventCapacityCommand(
            fixture.Managers["MED"], proposed.EventId!.Value, 4,
            fixture.TypeIds["FIT"]), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(6, fixture.Events.Items
            .Single(e => e.Id == proposed.EventId.Value)
            .CapacityFor(fixture.TypeIds["MED"]).TotalHeadcount);
    }

    [Fact]
    public async Task Adjust_below_active_bookings_returns_minimum_and_current_values()
    {
        var fixture = NegotiationFixture.Create().WithTypes("MED");
        var proposed = await fixture.ProposeAsync("MED", ["MED"], headcount: 6);
        fixture.Occupy("MED", proposed.EventId!.Value, occupied: 5);
        var handler = new AdjustEventCapacityHandler(
            fixture.Events, fixture.Capacities, fixture.Profiles, fixture.UnitOfWork,
            fixture.Audit, fixture.Types);

        var result = await handler.HandleAsync(new AdjustEventCapacityCommand(
            fixture.Managers["MED"], proposed.EventId.Value, 4), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("capacity-below-bookings", result.Error.Code);
        Assert.Equal(5L, result.Error.Data!["minimum"]);
        Assert.Equal(6L, result.Error.Data!["currentTotal"]);
    }

    [Fact]
    public async Task Unchanged_adjustment_writes_no_audit()
    {
        var fixture = NegotiationFixture.Create().WithTypes("MED");
        var proposed = await fixture.ProposeAsync("MED", ["MED"], headcount: 6);
        var before = fixture.Audit.Entries.Count;
        var handler = new AdjustEventCapacityHandler(
            fixture.Events, fixture.Capacities, fixture.Profiles, fixture.UnitOfWork,
            fixture.Audit, fixture.Types);

        var result = await handler.HandleAsync(new AdjustEventCapacityCommand(
            fixture.Managers["MED"], proposed.EventId!.Value, 6), CancellationToken.None);

        Assert.True(result.IsSuccess, $"result failed: {result.Error?.Code} {result.Error?.Message}");
        Assert.False(result.Value.Changed);
        Assert.Equal(before, fixture.Audit.Entries.Count);
    }
}
