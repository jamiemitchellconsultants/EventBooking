using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventProposalTests
{
    private static readonly Guid Creator = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly EventWindow Window =
        new(new DateOnly(2026, 9, 10), new TimeOnly(9, 0));

    private static EventProposal NewProposal() => EventProposal.Create(Guid.NewGuid(), Window, Creator);

    [Fact]
    public void ANewProposalIsOpenWithNoAcceptances()
    {
        var proposal = NewProposal();

        Assert.Equal(EventProposalStatus.Open, proposal.Status);
        Assert.Empty(proposal.Acceptances);
        Assert.Equal(Window, proposal.Window);
        Assert.Equal(Creator, proposal.CreatedByManagerUserId);
    }

    [Fact]
    public void AProposalWithoutACreatorIsRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => EventProposal.Create(Guid.NewGuid(), Window, Guid.Empty));
        Assert.Equal("createdByManagerUserId must not be empty.", ex.Message);
    }

    [Fact]
    public void AProposalWithoutAnIdentifierIsRejected()
    {
        Assert.Throws<DomainException>(() => EventProposal.Create(Guid.Empty, Window, Creator));
    }

    [Fact]
    public void TheCreatorCanWithdrawAnOpenProposal()
    {
        var proposal = NewProposal();

        proposal.Withdraw(Creator);

        Assert.Equal(EventProposalStatus.Withdrawn, proposal.Status);
    }

    [Fact]
    public void AnotherManagerCanWithdrawAnOpenProposal()
    {
        var proposal = NewProposal();

        proposal.Withdraw(Guid.NewGuid());

        Assert.Equal(EventProposalStatus.Withdrawn, proposal.Status);
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
