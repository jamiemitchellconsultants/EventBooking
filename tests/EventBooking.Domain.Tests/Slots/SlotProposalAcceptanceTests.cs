using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Domain.Tests.Slots;

public class SlotProposalAcceptanceTests
{
    private static readonly Guid DrugAndAlcohol = AppointmentTypeIds.DrugAndAlcoholTesting;
    private static readonly Guid Medical = AppointmentTypeIds.MedicalCheckUp;
    private static readonly Guid DrugAndAlcoholManager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid MedicalManager = Guid.Parse("c0000002-0000-0000-0000-000000000002");

    private static SlotProposal NewProposal() =>
        SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
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
            () => proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, headcount));
        Assert.Equal("headcount must be greater than zero.", ex.Message);
        Assert.Empty(proposal.Acceptances);
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
        var unknownAppointmentType = Guid.Parse("a0000004-0000-0000-0000-000000000004");

        var ex = Assert.Throws<DomainException>(
            () => proposal.WithdrawAcceptance(unknownAppointmentType, DrugAndAlcoholManager));

        Assert.Equal($"{unknownAppointmentType} is not one of the 3 appointment types.", ex.Message);
        Assert.Single(proposal.Acceptances);
    }

    [Fact]
    public void AWithdrawnProposalCannotBeAccepted()
    {
        var proposal = NewProposal();
        proposal.Withdraw(DrugAndAlcoholManager);

        var ex = Assert.Throws<DomainException>(
            () => proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, 10));
        Assert.Equal("Only an open proposal can be accepted.", ex.Message);
    }

    [Fact]
    public void AManagerCanWithdrawTheirOwnAcceptance()
    {
        var proposal = NewProposal();
        proposal.Accept(DrugAndAlcohol, DrugAndAlcoholManager, 10);
        proposal.Accept(Medical, MedicalManager, 6);

        proposal.WithdrawAcceptance(Medical, MedicalManager);

        Assert.Single(proposal.Acceptances);
        Assert.False(proposal.IsAcceptedBy(Medical));
    }

    [Fact]
    public void AReplacementManagerCanWithdrawTheFormerManagersAcceptance()
    {
        var proposal = NewProposal();
        proposal.Accept(Medical, MedicalManager, 6);

        proposal.WithdrawAcceptance(Medical, DrugAndAlcoholManager);

        Assert.Empty(proposal.Acceptances);
    }

    [Fact]
    public void WithdrawingAnAcceptanceThatWasNeverGivenIsRejected()
    {
        var proposal = NewProposal();

        var ex = Assert.Throws<DomainException>(
            () => proposal.WithdrawAcceptance(Medical, MedicalManager));
        Assert.Equal("This appointment type has not accepted the proposal.", ex.Message);
    }
}
