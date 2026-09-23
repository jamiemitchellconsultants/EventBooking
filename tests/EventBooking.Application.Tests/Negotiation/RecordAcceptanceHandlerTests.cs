using EventBooking.Application.Common;
using EventBooking.Application.Negotiation;

namespace EventBooking.Application.Tests.Negotiation;

public sealed class RecordAcceptanceHandlerTests
{
    [Fact]
    public async Task Single_type_proposal_confirms_with_three_audit_entries()
    {
        var fixture = NegotiationFixture.Create().WithTypes("MED");
        var proposed = await fixture.ProposeAsync("MED", ["MED"], headcount: 6);
        Assert.Equal("Confirmed", proposed.Status);
        Assert.NotNull(proposed.EventId);

        // EventConfirmed is recorded against the event, not the proposal, so the
        // sequence is asserted over the whole log rather than one entity's entries.
        Assert.Equal(
            ["ProposalCreated", "AcceptanceRecorded", "EventConfirmed"],
            fixture.Audit.Entries.Select(e => e.Action.ToString()));
        Assert.Single(fixture.Events.Items);
        Assert.Single(fixture.Events.Items.Single().Capacities);
    }

    [Fact]
    public async Task Three_type_proposal_confirms_only_on_the_third_accept()
    {
        var fixture = NegotiationFixture.Create().WithTypes("MED", "FIT", "IND");
        var proposed = await fixture.ProposeAsync("MED", ["MED", "FIT", "IND"], headcount: 6);

        var second = await fixture.AcceptAsync("FIT", proposed.ProposalId, headcount: 4);
        Assert.True(second.IsSuccess, $"second failed: {second.Error?.Code} {second.Error?.Message}");
        Assert.Equal("Open", second.Value.Status);
        Assert.Null(second.Value.EventId);

        var third = await fixture.AcceptAsync("IND", proposed.ProposalId, headcount: 5);
        Assert.True(third.IsSuccess, $"third failed: {third.Error?.Code} {third.Error?.Message}");
        Assert.Equal("Confirmed", third.Value.Status);
        Assert.NotNull(third.Value.EventId);
        Assert.Equal(3, fixture.Events.Items.Single().Capacities.Count);
        Assert.Equal("Confirmed", fixture.Proposals.Items.Single().Status.ToString());
    }

    [Fact]
    public async Task Same_headcount_revision_reports_unchanged_and_writes_no_audit()
    {
        // Three types: the first FIT accept must leave the proposal open, or the revision
        // lands on a confirmed proposal and the unchanged path is unreachable.
        var fixture = NegotiationFixture.Create().WithTypes("MED", "FIT", "IND");
        var proposed = await fixture.ProposeAsync("MED", ["MED", "FIT", "IND"], headcount: 6);
        await fixture.AcceptAsync("FIT", proposed.ProposalId, headcount: 4);
        var before = fixture.Audit.Entries.Count;

        var result = await fixture.AcceptAsync("FIT", proposed.ProposalId, headcount: 4);

        Assert.True(result.IsSuccess, $"result failed: {result.Error?.Code} {result.Error?.Message}");
        Assert.False(result.Value.Changed);
        Assert.Equal(before, fixture.Audit.Entries.Count);
    }

    [Fact]
    public async Task Accept_on_withdrawn_proposal_returns_status_with_no_audit()
    {
        var fixture = NegotiationFixture.Create().WithTypes("MED", "FIT");
        var proposed = await fixture.ProposeAsync("MED", ["MED", "FIT"], headcount: 6);
        await fixture.WithdrawProposalAsync("MED", proposed.ProposalId);
        var before = fixture.Audit.Entries.Count;

        var result = await fixture.AcceptAsync("FIT", proposed.ProposalId, headcount: 4);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Contains("Withdrawn", result.Error.Message);
        Assert.Equal(before, fixture.Audit.Entries.Count);
    }

    [Fact]
    public async Task Null_scoped_manager_is_forbidden_before_any_domain_call()
    {
        var fixture = NegotiationFixture.Create().WithTypes("MED", "FIT").WithNullScopedManager();
        var proposed = await fixture.ProposeAsync("MED", ["MED", "FIT"], headcount: 6);

        var result = await fixture.AcceptAsync(null, proposed.ProposalId, headcount: 4);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }
}
