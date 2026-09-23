using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventProposalConfirmationTests
{
    private static EventProposal NewProposal() =>
        ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            Guid.NewGuid());

    private static EventProposal AcceptedByAllThree(int drugAndAlcohol = 10, int medical = 6, int uniform = 8)
    {
        var proposal = NewProposal();
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), drugAndAlcohol);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), medical);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), uniform);
        return proposal;
    }

    [Fact]
    public void AProposalWithNoAcceptancesIsNotFullyAccepted()
    {
        Assert.False(NewProposal().IsFullyAccepted);
    }

    [Fact]
    public void TwoOfThreeAcceptancesIsNotEnough()
    {
        var proposal = NewProposal();
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);

        Assert.False(proposal.IsFullyAccepted);
    }

    [Fact]
    public void AllThreeAcceptancesMakeItFullyAccepted()
    {
        Assert.True(AcceptedByAllThree().IsFullyAccepted);
    }

    [Fact]
    public void WithdrawingOneAcceptanceUndoesFullAcceptance()
    {
        var proposal = NewProposal();
        var medicalManager = Guid.NewGuid();
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, medicalManager, 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        Assert.True(proposal.IsFullyAccepted);

        proposal.WithdrawAcceptance(AppointmentTypeIds.MedicalCheckUp);

        Assert.False(proposal.IsFullyAccepted);
    }

    [Fact]
    public void AWithdrawnProposalIsNeverFullyAccepted()
    {
        var creator = Guid.NewGuid();
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            creator);
        proposal.Withdraw(ProposalFixture.ProposerType);

        Assert.False(proposal.IsFullyAccepted);
    }
}
