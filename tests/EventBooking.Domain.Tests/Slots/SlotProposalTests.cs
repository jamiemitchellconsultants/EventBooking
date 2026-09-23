using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Domain.Tests.Slots;

public class SlotProposalTests
{
    private static readonly Guid Creator = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly SlotWindow Window =
        new(new DateOnly(2026, 9, 10), new TimeOnly(9, 0));

    private static SlotProposal NewProposal() => SlotProposal.Create(Guid.NewGuid(), Window, Creator);

    [Fact]
    public void ANewProposalIsOpenWithNoAcceptances()
    {
        var proposal = NewProposal();

        Assert.Equal(SlotProposalStatus.Open, proposal.Status);
        Assert.Empty(proposal.Acceptances);
        Assert.Equal(Window, proposal.Window);
        Assert.Equal(Creator, proposal.CreatedByManagerUserId);
    }

    [Fact]
    public void AProposalWithoutACreatorIsRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => SlotProposal.Create(Guid.NewGuid(), Window, Guid.Empty));
        Assert.Equal("createdByManagerUserId must not be empty.", ex.Message);
    }

    [Fact]
    public void AProposalWithoutAnIdentifierIsRejected()
    {
        Assert.Throws<DomainException>(() => SlotProposal.Create(Guid.Empty, Window, Creator));
    }

    [Fact]
    public void TheCreatorCanWithdrawAnOpenProposal()
    {
        var proposal = NewProposal();

        proposal.Withdraw(Creator);

        Assert.Equal(SlotProposalStatus.Withdrawn, proposal.Status);
    }

    [Fact]
    public void AnotherManagerCanWithdrawAnOpenProposal()
    {
        var proposal = NewProposal();

        proposal.Withdraw(Guid.NewGuid());

        Assert.Equal(SlotProposalStatus.Withdrawn, proposal.Status);
    }

    [Fact]
    public void WithdrawingTwiceIsRejected()
    {
        var proposal = NewProposal();
        proposal.Withdraw(Creator);

        var ex = Assert.Throws<DomainException>(() => proposal.Withdraw(Creator));
        Assert.Equal("Only an open proposal can be withdrawn.", ex.Message);
    }
}
