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
