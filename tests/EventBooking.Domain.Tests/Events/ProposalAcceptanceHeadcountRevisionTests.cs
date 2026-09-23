using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class ProposalAcceptanceHeadcountRevisionTests
{
    private static readonly Guid DrugAndAlcoholManager =
        Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid FormerDrugAndAlcoholManager =
        Guid.Parse("c0000011-0000-0000-0000-000000000011");
    private static readonly Guid MedicalManager =
        Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid UniformManager =
        Guid.Parse("c0000003-0000-0000-0000-000000000003");

    private static EventProposal NewProposal() =>
        EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            DrugAndAlcoholManager);

    [Fact]
    public void TheSameManagerRevisesTheExistingAcceptanceInPlace()
    {
        var proposal = NewProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        var original = Assert.Single(proposal.Acceptances);

        var changed = proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 12);

        Assert.True(changed);
        Assert.Same(original, Assert.Single(proposal.Acceptances));
        Assert.Equal(12, original.Headcount);
    }

    [Fact]
    public void ResubmittingTheCurrentHeadcountReportsNoChange()
    {
        var proposal = NewProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);

        var changed = proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);

        Assert.False(changed);
        Assert.Equal(10, Assert.Single(proposal.Acceptances).Headcount);
    }

    [Fact]
    public void AReplacementManagerRevisesTheFormerManagersAcceptance()
    {
        var proposal = NewProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, FormerDrugAndAlcoholManager, 10);

        var changed = proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 12);

        Assert.True(changed);
        Assert.Equal(12, Assert.Single(proposal.Acceptances).Headcount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void AnInvalidRevisionLeavesTheCurrentHeadcountUntouched(int headcount)
    {
        var proposal = NewProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);

        var ex = Assert.Throws<DomainException>(() => proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, headcount));

        Assert.Equal("headcount must be greater than zero.", ex.Message);
        Assert.Equal(10, Assert.Single(proposal.Acceptances).Headcount);
    }

    [Fact]
    public void AWithdrawnProposalCannotHaveAnAcceptanceRevised()
    {
        var proposal = NewProposal();
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        proposal.Withdraw(DrugAndAlcoholManager);

        var ex = Assert.Throws<DomainException>(() => proposal.Accept(
            AppointmentTypeIds.MedicalCheckUp, MedicalManager, 8));

        Assert.Equal("Only an open proposal can be accepted.", ex.Message);
        Assert.Equal(6, Assert.Single(proposal.Acceptances).Headcount);
    }

    [Fact]
    public void AConfirmedProposalCannotHaveAnAcceptanceRevised()
    {
        var proposal = NewProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, UniformManager, 8);
        Event.CreateFrom(Guid.NewGuid(), proposal);

        var ex = Assert.Throws<DomainException>(() => proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 12));

        Assert.Equal("Only an open proposal can be accepted.", ex.Message);
        Assert.Equal(
            10,
            proposal.Acceptances.Single(acceptance =>
                acceptance.AppointmentTypeId
                == AppointmentTypeIds.DrugAndAlcoholTesting).Headcount);
    }
}
