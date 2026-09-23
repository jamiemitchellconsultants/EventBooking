using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventProposalTests
{
    private static readonly Guid Creator = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly EventWindow Window =
        new(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240);

    private static EventProposal NewProposal() => ProposalFixture.Create(Guid.NewGuid(), Window, Creator);

    [Fact]
    public void ANewProposalIsOpenAndCarriesOnlyTheProposersAcceptance()
    {
        var proposal = NewProposal();

        Assert.Equal(EventProposalStatus.Open, proposal.Status);
        Assert.Equal(ProposalFixture.ProposerType, Assert.Single(proposal.Acceptances).AppointmentTypeId);
        Assert.Equal(Window, proposal.Window);
        Assert.Equal(Creator, proposal.CreatedByManagerUserId);
    }

    [Fact]
    public void AProposalWithoutACreatorIsRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => ProposalFixture.Create(Guid.NewGuid(), Window, Guid.Empty));
        Assert.Equal("createdByManagerUserId must not be empty.", ex.Message);
    }

    [Fact]
    public void AProposalWithoutAnIdentifierIsRejected()
    {
        Assert.Throws<DomainException>(() => ProposalFixture.Create(Guid.Empty, Window, Creator));
    }

    [Fact]
    public void TheProposingTypeCanWithdrawAnOpenProposal()
    {
        var proposal = NewProposal();

        proposal.Withdraw(ProposalFixture.ProposerType);

        Assert.Equal(EventProposalStatus.Withdrawn, proposal.Status);
    }

    [Fact]
    public void AnotherListedTypeCannotWithdrawTheProposal()
    {
        var proposal = NewProposal();

        Assert.Throws<DomainException>(() => proposal.Withdraw(AppointmentTypeIds.MedicalCheckUp));

        Assert.Equal(EventProposalStatus.Open, proposal.Status);
    }

    [Fact]
    public void WithdrawingTwiceReportsTheCurrentStatus()
    {
        var proposal = NewProposal();
        proposal.Withdraw(ProposalFixture.ProposerType);

        var ex = Assert.Throws<ProposalNotOpenException>(
            () => proposal.Withdraw(ProposalFixture.ProposerType));
        Assert.Equal(EventProposalStatus.Withdrawn, ex.CurrentStatus);
    }
}
