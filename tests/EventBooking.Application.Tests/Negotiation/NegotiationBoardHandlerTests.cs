using System.Text.Json;
using EventBooking.Application.Negotiation;

namespace EventBooking.Application.Tests.Negotiation;

public sealed class NegotiationBoardHandlerTests
{
    [Fact]
    public async Task Board_hides_every_other_type()
    {
        var fixture = NegotiationFixture.Create().WithTypes("MED", "FIT", "IND", "ESC");
        await fixture.ProposeAsync("MED", ["MED", "FIT", "IND"], headcount: 6);
        await fixture.ProposeAsync("FIT", ["FIT", "ESC"], headcount: 2);
        var handler = new NegotiationBoardHandler(
            fixture.Proposals, fixture.Events, fixture.Locations, fixture.Profiles,
            fixture.Clock, ProposalFixture.Zones);

        var result = await handler.HandleAsync(
            new GetNegotiationBoardQuery(fixture.Managers["MED"]), CancellationToken.None);

        Assert.True(result.IsSuccess, $"result failed: {result.Error?.Code} {result.Error?.Message}");
        Assert.Single(result.Value.OpenProposals);
        var json = JsonSerializer.Serialize(result.Value);
        Assert.DoesNotContain("FIT", json);
        Assert.DoesNotContain("IND", json);
        Assert.DoesNotContain("ESC", json);
        Assert.Contains("6", json);
    }
}
