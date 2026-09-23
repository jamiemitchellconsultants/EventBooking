using EventBooking.Application.Common;
using EventBooking.Application.Negotiation;

namespace EventBooking.Application.Tests.Negotiation;

public sealed class WithdrawHandlerTests
{
    [Fact]
    public async Task Withdraw_proposer_acceptance_is_refused()
    {
        var fixture = NegotiationFixture.Create().WithTypes("MED", "FIT");
        var proposed = await fixture.ProposeAsync("MED", ["MED", "FIT"], headcount: 6);
        var handler = new WithdrawAcceptanceHandler(
            fixture.Proposals, fixture.Profiles, fixture.UnitOfWork, fixture.Audit);

        var result = await handler.HandleAsync(
            new WithdrawAcceptanceCommand(fixture.Managers["MED"], proposed.ProposalId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Single(fixture.Proposals.Items.Single().Acceptances);
    }

    [Fact]
    public async Task Withdraw_then_reaccept_works()
    {
        // Three types: accepting on two would confirm the proposal, and a confirmed
        // proposal refuses the withdrawal this test exists to exercise.
        var fixture = NegotiationFixture.Create().WithTypes("MED", "FIT", "IND");
        var proposed = await fixture.ProposeAsync("MED", ["MED", "FIT", "IND"], headcount: 6);
        await fixture.AcceptAsync("FIT", proposed.ProposalId, headcount: 4);
        var handler = new WithdrawAcceptanceHandler(
            fixture.Proposals, fixture.Profiles, fixture.UnitOfWork, fixture.Audit);

        var withdrawn = await handler.HandleAsync(
            new WithdrawAcceptanceCommand(fixture.Managers["FIT"], proposed.ProposalId),
            CancellationToken.None);
        Assert.True(withdrawn.IsSuccess, $"withdrawn failed: {withdrawn.Error?.Code} {withdrawn.Error?.Message}");

        var reaccepted = await fixture.AcceptAsync("FIT", proposed.ProposalId, headcount: 3);
        Assert.True(reaccepted.IsSuccess, $"reaccepted failed: {reaccepted.Error?.Code} {reaccepted.Error?.Message}");
        Assert.True(reaccepted.Value.Changed);
        Assert.Equal("Open", reaccepted.Value.Status);
    }

    [Fact]
    public async Task Successor_manager_can_withdraw_the_proposal()
    {
        var fixture = NegotiationFixture.Create().WithTypes("MED", "FIT");
        var proposed = await fixture.ProposeAsync("MED", ["MED", "FIT"], headcount: 6);
        var successor = Guid.NewGuid();
        fixture.Profiles.Add(Domain.Access.StaffAccessProfile.Create(
            successor, Domain.Access.Role.Manager, fixture.TypeIds["MED"]));
        var handler = new WithdrawProposalHandler(
            fixture.Proposals, fixture.Profiles, fixture.UnitOfWork, fixture.Audit);

        var result = await handler.HandleAsync(
            new WithdrawProposalCommand(successor, proposed.ProposalId), CancellationToken.None);

        Assert.True(result.IsSuccess, $"result failed: {result.Error?.Code} {result.Error?.Message}");
        Assert.Equal("Withdrawn", fixture.Proposals.Items.Single().Status.ToString());
    }
}
