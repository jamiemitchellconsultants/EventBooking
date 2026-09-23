using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventProposalAcceptanceTests
{
    private static readonly Guid DrugAndAlcohol = AppointmentTypeIds.DrugAndAlcoholTesting;
    private static readonly Guid Medical = AppointmentTypeIds.MedicalCheckUp;
    private static readonly Guid DrugAndAlcoholManager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid MedicalManager = Guid.Parse("c0000002-0000-0000-0000-000000000002");

    private static EventProposal NewProposal() =>
        ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            DrugAndAlcoholManager);

    [Fact]
    public void AcceptingRecordsTheManagerAndTheirHeadcount()
    {
        var proposal = NewProposal();

        proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, 10);

        var acceptance = Assert.Single(proposal.Acceptances);
        Assert.Equal(proposal.Id, acceptance.ProposalId);
        Assert.Equal(DrugAndAlcohol, acceptance.AppointmentTypeId);
        Assert.Equal(DrugAndAlcoholManager, acceptance.ManagerUserId);
        Assert.Equal(10, acceptance.Headcount);
        Assert.True(proposal.IsAcceptedBy(DrugAndAlcohol));
        Assert.False(proposal.IsAcceptedBy(Medical));
    }

    [Fact]
    public void TwoManagersAcceptIndependently()
    {
        var proposal = NewProposal();

        proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, 10);
        proposal.Accept(Medical, MedicalManager, 6);

        Assert.Equal(2, proposal.Acceptances.Count);
        Assert.Equal(6, proposal.Acceptances.Single(a => a.AppointmentTypeId == Medical).Headcount);
    }

    [Fact]
    public void AcceptingTwiceForTheSameAppointmentTypeRevisesTheHeadcount()
    {
        var proposal = NewProposal();
        proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, 10);

        proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, 12);

        var acceptance = Assert.Single(proposal.Acceptances);
        Assert.Equal(12, acceptance.Headcount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void AcceptingWithoutARealHeadcountIsRejected(int headcount)
    {
        var proposal = NewProposal();

        var ex = Assert.Throws<DomainException>(
            () => proposal.Accept(Medical, MedicalManager, headcount));
        Assert.Equal("headcount must be between 1 and 1000.", ex.Message);
        // The proposer's own acceptance is recorded at creation and is unaffected by a refusal.
        Assert.Single(proposal.Acceptances);
    }

    [Fact]
    public void AcceptingForAnUnknownAppointmentTypeIsRejected()
    {
        var proposal = NewProposal();

        Assert.Throws<DomainException>(
            () => proposal.Accept(Guid.NewGuid(), DrugAndAlcoholManager, 10));
    }

    [Fact]
    public void WithdrawingAnUnknownAppointmentTypeIsRejected()
    {
        var proposal = NewProposal();
        proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, 10);
        var unlistedAppointmentType = Guid.Parse("a0000004-0000-0000-0000-000000000004");

        var ex = Assert.Throws<DomainException>(
            () => proposal.WithdrawAcceptance(unlistedAppointmentType));

        Assert.Equal("This appointment type is not listed on the proposal.", ex.Message);
        Assert.Single(proposal.Acceptances);
    }

    [Fact]
    public void AWithdrawnProposalCannotBeAccepted()
    {
        var proposal = NewProposal();
        proposal.Withdraw(ProposalFixture.ProposerType);

        var ex = Assert.Throws<ProposalNotOpenException>(
            () => proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, 10));
        Assert.Equal(EventProposalStatus.Withdrawn, ex.CurrentStatus);
    }

    [Fact]
    public void AManagerCanWithdrawTheirOwnAcceptance()
    {
        var proposal = NewProposal();
        proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, 10);
        proposal.Accept(Medical, MedicalManager, 6);

        proposal.WithdrawAcceptance(Medical);

        Assert.Single(proposal.Acceptances);
        Assert.False(proposal.IsAcceptedBy(Medical));
    }

    [Fact]
    public void AReplacementManagerCanWithdrawTheFormerManagersAcceptance()
    {
        var proposal = NewProposal();
        proposal.Accept(Medical, MedicalManager, 6);

        proposal.WithdrawAcceptance(Medical);

        // The proposing type's own acceptance stays: only the proposal itself can take that back.
        Assert.Equal(ProposalFixture.ProposerType, Assert.Single(proposal.Acceptances).AppointmentTypeId);
    }

    [Fact]
    public void WithdrawingAnAcceptanceThatWasNeverGivenIsRejected()
    {
        var proposal = NewProposal();

        var ex = Assert.Throws<DomainException>(
            () => proposal.WithdrawAcceptance(Medical));
        Assert.Equal("This appointment type has not accepted the proposal.", ex.Message);
    }
}
