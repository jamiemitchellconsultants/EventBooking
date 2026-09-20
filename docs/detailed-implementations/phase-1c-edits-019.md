# 01c — Negotiation across any number of types, edits 19 (Task 6)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.Domain.Tests/Events/EventProposalAcceptanceTests.cs — 1/1

<!-- retirement-file: {"id":58,"file":"tests/EventBooking.Domain.Tests/Events/EventProposalAcceptanceTests.cs","beforeSha":"7605c056e10cba87a5a9ef6302acabbe392439271e435d506230cb0167034685","afterSha":"3b2ec599408653819c73f99ba2a4a84f9d5226426cab15f28169364bbee4be31","side":"after","part":1,"parts":1} -->

`````csharp
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
`````

## before — tests/EventBooking.Domain.Tests/Events/EventProposalConfirmationTests.cs — 1/1

<!-- retirement-file: {"id":59,"file":"tests/EventBooking.Domain.Tests/Events/EventProposalConfirmationTests.cs","beforeSha":"97f33489664bde9179549174c4f51384bf6f22d197dda35117c711adc6f2a947","afterSha":"71dc7065994d17ab66fab12d5d376baf16b3280a5f717e2aae1b7eeb4bcf3dcd","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventProposalConfirmationTests
{
    private static EventProposal NewProposal() =>
        EventProposal.Create(
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

        proposal.WithdrawAcceptance(AppointmentTypeIds.MedicalCheckUp, medicalManager);

        Assert.False(proposal.IsFullyAccepted);
    }

    [Fact]
    public void AWithdrawnProposalIsNeverFullyAccepted()
    {
        var creator = Guid.NewGuid();
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            creator);
        proposal.Withdraw(creator);

        Assert.False(proposal.IsFullyAccepted);
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Events/EventProposalConfirmationTests.cs — 1/1

<!-- retirement-file: {"id":59,"file":"tests/EventBooking.Domain.Tests/Events/EventProposalConfirmationTests.cs","beforeSha":"97f33489664bde9179549174c4f51384bf6f22d197dda35117c711adc6f2a947","afterSha":"71dc7065994d17ab66fab12d5d376baf16b3280a5f717e2aae1b7eeb4bcf3dcd","side":"after","part":1,"parts":1} -->

`````csharp
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
`````

## before — tests/EventBooking.Domain.Tests/Events/EventProposalTests.cs — 1/1

<!-- retirement-file: {"id":60,"file":"tests/EventBooking.Domain.Tests/Events/EventProposalTests.cs","beforeSha":"57c3a4ccb9a4bb646336ba3b2f234a4524c140535716263659278e3a9172ad04","afterSha":"70b32f96b94ac2ec67cd86f57c0fd775c6233d14f59c77eafa123ef81fcb6225","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventProposalTests
{
    private static readonly Guid Creator = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly EventWindow Window =
        new(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240);

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
`````

## after — tests/EventBooking.Domain.Tests/Events/EventProposalTests.cs — 1/1

<!-- retirement-file: {"id":60,"file":"tests/EventBooking.Domain.Tests/Events/EventProposalTests.cs","beforeSha":"57c3a4ccb9a4bb646336ba3b2f234a4524c140535716263659278e3a9172ad04","afterSha":"70b32f96b94ac2ec67cd86f57c0fd775c6233d14f59c77eafa123ef81fcb6225","side":"after","part":1,"parts":1} -->

`````csharp
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
`````

## before — tests/EventBooking.Domain.Tests/Events/EventTests.cs — 1/1

<!-- retirement-file: {"id":61,"file":"tests/EventBooking.Domain.Tests/Events/EventTests.cs","beforeSha":"c40a3c9e751f0dca6f074b00e053639beddb5a99a04a642aba0c060851404017","afterSha":"7eec3909963b095a86c9d02885d386ea370cea141c1fc111a8b1dce42c87d84c","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventTests
{
    private static EventProposal FullyAcceptedProposal(
        int drugAndAlcohol = 10, int medical = 6, int uniform = 8)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), drugAndAlcohol);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), medical);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), uniform);
        return proposal;
    }

    [Fact]
    public void ConfirmingCarriesTheWindowAndMarksTheProposalConfirmed()
    {
        var proposal = FullyAcceptedProposal();

        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        Assert.Equal(proposal.Id, eventItem.ProposalId);
        Assert.Equal(proposal.Window, eventItem.Window);
        Assert.Equal(EventStatus.Active, eventItem.Status);
        Assert.Equal(EventProposalStatus.Confirmed, proposal.Status);
    }

    [Fact]
    public void ConfirmingCreatesOneCapacityCounterPerAppointmentType()
    {
        var eventItem = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());

        Assert.Equal(3, eventItem.Capacities.Count);
        Assert.Equal(
            AppointmentTypeIds.All.OrderBy(id => id),
            eventItem.Capacities.Select(c => c.AppointmentTypeId).OrderBy(id => id));
    }

    [Fact]
    public void EachCounterStartsAtTheHeadcountItsManagerAccepted()
    {
        var eventItem = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal(10, 6, 8));

        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).TotalHeadcount);
        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(6, eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(8, eventItem.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
    }

    [Fact]
    public void EveryCounterBelongsToTheEventThatOwnsIt()
    {
        var id = Guid.NewGuid();

        var eventItem = Event.CreateFrom(id, FullyAcceptedProposal());

        Assert.All(eventItem.Capacities, c => Assert.Equal(id, c.EventId));
    }

    [Fact]
    public void APartlyAcceptedProposalCannotBeConfirmed()
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);

        var ex = Assert.Throws<DomainException>(() => Event.CreateFrom(Guid.NewGuid(), proposal));
        Assert.Equal("A proposal can only be confirmed once all 3 managers have accepted it.", ex.Message);
    }

    [Fact]
    public void AProposalCannotBeConfirmedTwice()
    {
        var proposal = FullyAcceptedProposal();
        Event.CreateFrom(Guid.NewGuid(), proposal);

        Assert.Throws<DomainException>(() => Event.CreateFrom(Guid.NewGuid(), proposal));
    }

    [Fact]
    public void CapacityForAnUnknownAppointmentTypeIsRejected()
    {
        var eventItem = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());

        Assert.Throws<DomainException>(() => eventItem.CapacityFor(Guid.NewGuid()));
    }

    [Fact]
    public void SpareCapacityIsCheckedAcrossEveryRequiredType()
    {
        var eventItem = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());

        Assert.True(eventItem.HasSpareCapacityForAll(
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
        Assert.True(eventItem.HasSpareCapacityForAll(AppointmentTypeIds.All));
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Events/EventTests.cs — 1/1

<!-- retirement-file: {"id":61,"file":"tests/EventBooking.Domain.Tests/Events/EventTests.cs","beforeSha":"c40a3c9e751f0dca6f074b00e053639beddb5a99a04a642aba0c060851404017","afterSha":"7eec3909963b095a86c9d02885d386ea370cea141c1fc111a8b1dce42c87d84c","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventTests
{
    private static EventProposal FullyAcceptedProposal(
        int drugAndAlcohol = 10, int medical = 6, int uniform = 8)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), drugAndAlcohol);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), medical);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), uniform);
        return proposal;
    }

    [Fact]
    public void ConfirmingCarriesTheWindowAndMarksTheProposalConfirmed()
    {
        var proposal = FullyAcceptedProposal();

        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        Assert.Equal(proposal.Id, eventItem.ProposalId);
        Assert.Equal(proposal.Window, eventItem.Window);
        Assert.Equal(EventStatus.Active, eventItem.Status);
        Assert.Equal(EventProposalStatus.Confirmed, proposal.Status);
    }

    [Fact]
    public void ConfirmingCreatesOneCapacityCounterPerAppointmentType()
    {
        var eventItem = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());

        Assert.Equal(3, eventItem.Capacities.Count);
        Assert.Equal(
            AppointmentTypeIds.All.OrderBy(id => id),
            eventItem.Capacities.Select(c => c.AppointmentTypeId).OrderBy(id => id));
    }

    [Fact]
    public void EachCounterStartsAtTheHeadcountItsManagerAccepted()
    {
        var eventItem = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal(10, 6, 8));

        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).TotalHeadcount);
        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(6, eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(8, eventItem.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
    }

    [Fact]
    public void EveryCounterBelongsToTheEventThatOwnsIt()
    {
        var id = Guid.NewGuid();

        var eventItem = Event.CreateFrom(id, FullyAcceptedProposal());

        Assert.All(eventItem.Capacities, c => Assert.Equal(id, c.EventId));
    }

    [Fact]
    public void APartlyAcceptedProposalCannotBeConfirmed()
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);

        var ex = Assert.Throws<DomainException>(() => Event.CreateFrom(Guid.NewGuid(), proposal));
        Assert.Equal(
            "A proposal is confirmed only once every listed appointment type has accepted it.",
            ex.Message);
    }

    [Fact]
    public void AProposalCannotBeConfirmedTwice()
    {
        var proposal = FullyAcceptedProposal();
        Event.CreateFrom(Guid.NewGuid(), proposal);

        Assert.Throws<DomainException>(() => Event.CreateFrom(Guid.NewGuid(), proposal));
    }

    [Fact]
    public void CapacityForAnUnknownAppointmentTypeIsRejected()
    {
        var eventItem = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());

        Assert.Throws<DomainException>(() => eventItem.CapacityFor(Guid.NewGuid()));
    }

    [Fact]
    public void SpareCapacityIsCheckedAcrossEveryRequiredType()
    {
        var eventItem = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());

        Assert.True(eventItem.HasSpareCapacityForAll(
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
        Assert.True(eventItem.HasSpareCapacityForAll(AppointmentTypeIds.All));
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Events/NTypeNegotiationTests.cs — 1/1

<!-- retirement-file: {"id":62,"file":"tests/EventBooking.Domain.Tests/Events/NTypeNegotiationTests.cs","beforeSha":null,"afterSha":"257086a253955c2173c0042e9381ce11353ec5adba5634a67f4bd5fd24dbd19e","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;
using EventBooking.Domain.Time;

namespace EventBooking.Domain.Tests.Events;

/// <summary>
/// Task 6: negotiation runs across any number of listed appointment types, and the proposal knows
/// which type proposed it (FR-2.1 to FR-2.12; design 01 — Negotiation).
/// </summary>
public class NTypeNegotiationTests
{
    private static readonly Guid Medical = Guid.Parse("a0000001-0000-0000-0000-000000000001");
    private static readonly Guid Fitting = Guid.Parse("a0000002-0000-0000-0000-000000000002");
    private static readonly Guid Induction = Guid.Parse("a0000003-0000-0000-0000-000000000003");
    private static readonly Guid Escort = Guid.Parse("a0000004-0000-0000-0000-000000000004");

    private static readonly Guid London = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Proposer = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid Successor = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private static readonly DateTimeOffset Now = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly EventWindow Window = new(new DateOnly(2026, 10, 14), new TimeOnly(9, 30), 90);
    private static readonly AlwaysUniqueZones Zones = new();

    private static ProposableAppointmentType Type(Guid id, string code, bool active = true, bool managed = true) =>
        new(id, code, active, managed);

    private static EventProposal Propose(
        IReadOnlyList<ProposableAppointmentType>? listed = null,
        Guid? proposerType = null,
        int headcount = 4,
        bool locationIsActive = true,
        EventWindow? window = null) =>
        EventProposal.Propose(
            Guid.NewGuid(),
            London,
            locationIsActive,
            "Europe/London",
            window ?? Window,
            Zones,
            Now,
            listed ?? [Type(Medical, "MED"), Type(Fitting, "FIT"), Type(Induction, "IND")],
            proposerType ?? Medical,
            Proposer,
            headcount);

    [Fact]
    public void AProposalOpensWithItsListedTypesAndTheProposersOwnAcceptance()
    {
        var proposal = Propose();

        Assert.Equal(EventProposalStatus.Open, proposal.Status);
        Assert.Equal([Medical, Fitting, Induction], proposal.ListedAppointmentTypeIds.Order());
        Assert.Equal(Medical, proposal.ProposerAppointmentTypeId);
        Assert.Equal(London, proposal.LocationId);
        Assert.Single(proposal.Acceptances);
        Assert.Equal(4, proposal.Acceptances.Single().Headcount);
        Assert.False(proposal.IsFullyAccepted);
    }

    [Fact]
    public void TheLastAcceptanceCompletesTheSetAndTheEventTakesEveryHeadcount()
    {
        var proposal = Propose();

        Assert.True(proposal.Accept(Fitting, Guid.NewGuid(), 6));
        Assert.False(proposal.IsFullyAccepted);

        Assert.True(proposal.Accept(Induction, Guid.NewGuid(), 5));
        Assert.True(proposal.IsFullyAccepted);

        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        Assert.Equal(EventProposalStatus.Confirmed, proposal.Status);
        Assert.Equal(London, eventItem.LocationId);
        Assert.Equal(3, eventItem.Capacities.Count);
        Assert.Equal(4, eventItem.CapacityFor(Medical).TotalHeadcount);
        Assert.Equal(6, eventItem.CapacityFor(Fitting).TotalHeadcount);
        Assert.Equal(5, eventItem.CapacityFor(Induction).TotalHeadcount);
    }

    [Fact]
    public void ASingleTypeProposalIsFullyAcceptedAsSoonAsItIsMade()
    {
        var proposal = Propose([Type(Medical, "MED")]);

        Assert.True(proposal.IsFullyAccepted);
        Assert.Single(Event.CreateFrom(Guid.NewGuid(), proposal).Capacities);
    }

    [Fact]
    public void RevisingAnAcceptanceReportsOnlyARealChange()
    {
        var proposal = Propose();
        proposal.Accept(Fitting, Guid.NewGuid(), 6);

        Assert.False(proposal.Accept(Fitting, Guid.NewGuid(), 6));
        Assert.True(proposal.Accept(Fitting, Guid.NewGuid(), 7));
    }

    [Fact]
    public void AcceptingForATypeTheProposalDoesNotListIsRefused()
    {
        var proposal = Propose();

        Assert.Throws<DomainException>(() => proposal.Accept(Escort, Guid.NewGuid(), 3));
    }

    [Fact]
    public void TheProposersOwnAcceptanceCannotBeWithdrawn()
    {
        var proposal = Propose();

        Assert.Throws<DomainException>(() => proposal.WithdrawAcceptance(Medical));
        Assert.Single(proposal.Acceptances);
    }

    [Fact]
    public void AnotherTypesAcceptanceCanBeWithdrawnAndRecordedAgain()
    {
        var proposal = Propose();
        proposal.Accept(Fitting, Guid.NewGuid(), 6);

        proposal.WithdrawAcceptance(Fitting);
        Assert.False(proposal.IsAcceptedBy(Fitting));

        Assert.True(proposal.Accept(Fitting, Guid.NewGuid(), 2));
    }

    [Fact]
    public void OnlyTheProposersTypeMayWithdrawTheWholeProposal()
    {
        var proposal = Propose();

        Assert.Throws<DomainException>(() => proposal.Withdraw(Fitting));
        Assert.Equal(EventProposalStatus.Open, proposal.Status);

        // A successor Manager of the proposing type inherits the proposal: the rule is judged on
        // the appointment type, never on the person who created it (FR-2.10).
        proposal.Withdraw(Medical);
        Assert.Equal(EventProposalStatus.Withdrawn, proposal.Status);
        Assert.NotEqual(Successor, proposal.CreatedByManagerUserId);
    }

    [Fact]
    public void TheSweepWithdrawsAnOpenProposalOnceItsWindowHasStarted()
    {
        var proposal = Propose();

        Assert.False(proposal.TryWithdrawStarted(Zones, "Europe/London", Now));
        Assert.Equal(EventProposalStatus.Open, proposal.Status);

        var afterStart = Window.StartInstant(Zones, "Europe/London");
        Assert.True(proposal.TryWithdrawStarted(Zones, "Europe/London", afterStart));
        Assert.Equal(EventProposalStatus.Withdrawn, proposal.Status);
    }

    [Fact]
    public void EveryChangeToAProposalThatIsNoLongerOpenReportsItsCurrentStatus()
    {
        var proposal = Propose();
        proposal.Withdraw(Medical);

        var accept = Assert.Throws<ProposalNotOpenException>(() => proposal.Accept(Fitting, Guid.NewGuid(), 2));
        var withdrawAcceptance = Assert.Throws<ProposalNotOpenException>(() => proposal.WithdrawAcceptance(Fitting));
        var withdraw = Assert.Throws<ProposalNotOpenException>(() => proposal.Withdraw(Medical));

        Assert.Equal(EventProposalStatus.Withdrawn, accept.CurrentStatus);
        Assert.Equal(EventProposalStatus.Withdrawn, withdrawAcceptance.CurrentStatus);
        Assert.Equal(EventProposalStatus.Withdrawn, withdraw.CurrentStatus);
    }

    [Fact]
    public void TheListedTypesCannotBeChangedAfterCreation()
    {
        Assert.DoesNotContain(
            typeof(EventProposal).GetMethods(),
            method => method.Name.Contains("Type", StringComparison.Ordinal)
                && method.Name.StartsWith("Add", StringComparison.Ordinal));
    }

    [Fact]
    public void AProposalReportsEveryValidationFailureAtOnce()
    {
        var failure = Assert.Throws<ProposalValidationException>(() => EventProposal.Propose(
            Guid.NewGuid(),
            London,
            locationIsActive: false,
            "Europe/London",
            new EventWindow(new DateOnly(2026, 8, 1), new TimeOnly(9, 0), 60),
            Zones,
            Now,
            [Type(Medical, "MED"), Type(Medical, "MED"), Type(Fitting, "FIT", active: false), Type(Induction, "IND", managed: false)],
            Escort,
            Proposer,
            headcount: 0));

        Assert.Contains("location-inactive", failure.Failures);
        Assert.Contains("window-not-in-future", failure.Failures);
        Assert.Contains("types-duplicated", failure.Failures);
        Assert.Contains("type-inactive: FIT", failure.Failures);
        Assert.Contains("type-without-manager: IND", failure.Failures);
        Assert.Contains("proposer-type-not-listed", failure.Failures);
        Assert.Contains("headcount-out-of-range", failure.Failures);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void AHeadcountOutsideOneToAThousandIsRefused(int headcount)
    {
        Assert.Throws<ProposalValidationException>(() => Propose(headcount: headcount));
    }

    [Fact]
    public void TheListIsBoundedAtTwentyTypes()
    {
        var twenty = Enumerable.Range(0, 20)
            .Select(index => Type(Guid.NewGuid(), $"T{index:00}"))
            .ToList();
        var proposal = EventProposal.Propose(
            Guid.NewGuid(), London, true, "Europe/London", Window, Zones, Now,
            twenty, twenty[0].Id, Proposer, 3);

        Assert.Equal(20, proposal.ListedAppointmentTypeIds.Count);

        var twentyOne = twenty.Append(Type(Guid.NewGuid(), "T20")).ToList();
        Assert.Throws<ProposalValidationException>(() => EventProposal.Propose(
            Guid.NewGuid(), London, true, "Europe/London", Window, Zones, Now,
            twentyOne, twentyOne[0].Id, Proposer, 3));
    }

    [Fact]
    public void AnEmptyListIsRefused()
    {
        Assert.Throws<ProposalValidationException>(() => Propose([]));
    }

    [Fact]
    public void AWindowWithNoUniqueInstantInTheLocationsZoneIsRefused()
    {
        var failure = Assert.Throws<ProposalValidationException>(() => EventProposal.Propose(
            Guid.NewGuid(), London, true, "Europe/London", Window, new GapZones(), Now,
            [Type(Medical, "MED")], Medical, Proposer, 3));

        Assert.Contains("window-has-no-unique-instant", failure.Failures);
    }

    private sealed class AlwaysUniqueZones : IEventWindowZones
    {
        public bool IsKnownZone(string timeZoneId) => true;

        public LocalTimeValidity ValidityOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            LocalTimeValidity.Unique;

        public DateTimeOffset InstantOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            new(date.ToDateTime(time), TimeSpan.Zero);

        public DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId) =>
            DateOnly.FromDateTime(instant.UtcDateTime);

        public string AbbreviationOf(DateTimeOffset instant, string timeZoneId) => "BST";
    }

    private sealed class GapZones : IEventWindowZones
    {
        public bool IsKnownZone(string timeZoneId) => true;

        public LocalTimeValidity ValidityOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            LocalTimeValidity.Gap;

        public DateTimeOffset InstantOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            new(date.ToDateTime(time), TimeSpan.Zero);

        public DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId) =>
            DateOnly.FromDateTime(instant.UtcDateTime);

        public string AbbreviationOf(DateTimeOffset instant, string timeZoneId) => "BST";
    }
}
`````

## before — tests/EventBooking.Domain.Tests/Events/ProposalAcceptanceHeadcountRevisionTests.cs — 1/1

<!-- retirement-file: {"id":63,"file":"tests/EventBooking.Domain.Tests/Events/ProposalAcceptanceHeadcountRevisionTests.cs","beforeSha":"7a7f8e3cfe473b1211fdd05d6c89395c29caaa33edf22c42a3e43c113cbabc8c","afterSha":"f03610dc850c10e82c4194a997f4f80fcfabf5b5778d9ffdae5b0e753a6ce810","side":"before","part":1,"parts":1} -->

`````csharp
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
`````

## after — tests/EventBooking.Domain.Tests/Events/ProposalAcceptanceHeadcountRevisionTests.cs — 1/1

<!-- retirement-file: {"id":63,"file":"tests/EventBooking.Domain.Tests/Events/ProposalAcceptanceHeadcountRevisionTests.cs","beforeSha":"7a7f8e3cfe473b1211fdd05d6c89395c29caaa33edf22c42a3e43c113cbabc8c","afterSha":"f03610dc850c10e82c4194a997f4f80fcfabf5b5778d9ffdae5b0e753a6ce810","side":"after","part":1,"parts":1} -->

`````csharp
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
        ProposalFixture.Create(
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

        Assert.Equal("headcount must be between 1 and 1000.", ex.Message);
        Assert.Equal(10, Assert.Single(proposal.Acceptances).Headcount);
    }

    [Fact]
    public void AWithdrawnProposalCannotHaveAnAcceptanceRevised()
    {
        var proposal = NewProposal();
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        proposal.Withdraw(ProposalFixture.ProposerType);

        var ex = Assert.Throws<ProposalNotOpenException>(() => proposal.Accept(
            AppointmentTypeIds.MedicalCheckUp, MedicalManager, 8));

        Assert.Equal(EventProposalStatus.Withdrawn, ex.CurrentStatus);
        Assert.Equal(
            6,
            proposal.Acceptances
                .Single(acceptance => acceptance.AppointmentTypeId == AppointmentTypeIds.MedicalCheckUp)
                .Headcount);
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

        var ex = Assert.Throws<ProposalNotOpenException>(() => proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 12));

        Assert.Equal(EventProposalStatus.Confirmed, ex.CurrentStatus);
        Assert.Equal(
            10,
            proposal.Acceptances.Single(acceptance =>
                acceptance.AppointmentTypeId
                == AppointmentTypeIds.DrugAndAlcoholTesting).Headcount);
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/AttendeeBookingQueryTests.cs — 1/1

<!-- retirement-file: {"id":64,"file":"tests/EventBooking.Infrastructure.Tests/AttendeeBookingQueryTests.cs","beforeSha":"198273aa3e56c1ad4d9eb24ed3f105429a076f1f0ec22454effc7ef62baa3fea","afterSha":"6503310c5392e4fed3fd10ba78671a1912b671733ddab7816f4720e1b3d2814b","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies the staff active-booking listing, its ordering, and its derived window end.</summary>
[Collection("postgres")]
public sealed class AttendeeBookingQueryTests(PostgresFixture fixture)
{
    [Fact]
    public async Task AnUnknownAttendeeReturnsNull()
    {
        await fixture.ResetAsync();

        await using var context = fixture.NewContext();
        var rows = await new AttendeeBookingQueries(context)
            .ListActiveForAttendeeAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(rows);
    }

    [Fact]
    public async Task AAttendeeWithNoActiveBookingReturnsAnEmptyList()
    {
        await fixture.ResetAsync();
        var attendeeId = await SeedAttendeeAsync();

        await using var context = fixture.NewContext();
        var rows = await new AttendeeBookingQueries(context)
            .ListActiveForAttendeeAsync(attendeeId, CancellationToken.None);

        Assert.NotNull(rows);
        Assert.Empty(rows!);
    }

    [Fact]
    public async Task TheOriginalSortsFirstAndEachWindowEndIsDerived()
    {
        await fixture.ResetAsync();
        var attendeeId = await SeedAttendeeAsync();
        var originalEvent = await SeedEventAsync(new DateOnly(2026, 9, 10), new TimeOnly(9, 0));
        var recoveryEvent = await SeedEventAsync(new DateOnly(2026, 9, 12), new TimeOnly(13, 0));

        var original = OriginalFor(attendeeId, originalEvent);
        var recovery = RecoveryFor(attendeeId, original, recoveryEvent);
        await using (var write = fixture.NewContext())
        {
            write.Bookings.AddRange(original, recovery);
            await write.SaveChangesAsync();
        }

        await using var context = fixture.NewContext();
        var rows = await new AttendeeBookingQueries(context)
            .ListActiveForAttendeeAsync(attendeeId, CancellationToken.None);

        Assert.NotNull(rows);
        Assert.Equal(2, rows!.Count);

        Assert.True(rows[0].IsOriginal);
        Assert.Equal(original.Id, rows[0].BookingId);
        Assert.Equal(new DateOnly(2026, 9, 10), rows[0].EventDate);
        Assert.Equal(new TimeOnly(9, 0), rows[0].EventStartTime);
        Assert.Equal(new TimeOnly(13, 0), rows[0].EventEndTime);

        Assert.False(rows[1].IsOriginal);
        Assert.Equal(recovery.Id, rows[1].BookingId);
        Assert.Equal(new TimeOnly(13, 0), rows[1].EventStartTime);
        Assert.Equal(new TimeOnly(17, 0), rows[1].EventEndTime);
    }

    [Fact]
    public async Task ACancelledBookingIsNotListed()
    {
        await fixture.ResetAsync();
        var attendeeId = await SeedAttendeeAsync();
        var eventId = await SeedEventAsync(new DateOnly(2026, 9, 10), new TimeOnly(9, 0));
        var booking = OriginalFor(attendeeId, eventId);
        booking.Cancel();

        await using (var write = fixture.NewContext())
        {
            write.Bookings.Add(booking);
            await write.SaveChangesAsync();
        }

        await using var context = fixture.NewContext();
        var rows = await new AttendeeBookingQueries(context)
            .ListActiveForAttendeeAsync(attendeeId, CancellationToken.None);

        Assert.NotNull(rows);
        Assert.Empty(rows!);
    }

    private async Task<Guid> SeedAttendeeAsync()
    {
        await using var write = fixture.NewContext();
        var pilots = await write.AttendeeGroups
            .Include(g => g.Requirements)
            .SingleAsync(g => g.Id == AttendeeGroupIds.Pilots);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", $"a.novak.{Guid.NewGuid():N}@mail.com", pilots);
        write.Attendees.Add(attendee);
        await write.SaveChangesAsync();
        return attendee.Id;
    }

    private async Task<Guid> SeedEventAsync(DateOnly date, TimeOnly startTime)
    {
        var proposal = EventProposal.Create(Guid.NewGuid(), new EventWindow(date, startTime, 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 10);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        await using var write = fixture.NewContext();
        write.EventProposals.Add(proposal);
        write.Events.Add(eventItem);
        await write.SaveChangesAsync();
        return eventItem.Id;
    }

    private static Booking OriginalFor(Guid attendeeId, Guid eventId)
    {
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendeeId, $"initial-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.AddDays(1),
            [eventId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        return Booking.Create(
            Guid.NewGuid(), invite, eventId, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
    }

    private static Booking RecoveryFor(Guid attendeeId, Booking original, Guid eventId)
    {
        var invite = Invite.CreateRecovery(
            Guid.NewGuid(), attendeeId, original.Id, $"recovery-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(2),
            [eventId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);
        return Booking.CreateRecovery(
            Guid.NewGuid(), invite, original, eventId, $"manage-recovery-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddHours(1));
    }
}
`````
