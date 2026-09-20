# 00a — Port source 70 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## tests/EventBooking.Domain.Tests/Slots/SlotCapacityHeadcountAdjustmentTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Domain.Tests/Slots/SlotCapacityHeadcountAdjustmentTests.cs","encoding":"utf8","sha256":"4da0600456a57374a015f496d362ca53ec1a0e06681b539e37e1bc2ba2331296","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Domain.Tests.Slots;

public class SlotCapacityHeadcountAdjustmentTests
{
    private static SlotCapacity CapacityOf(int totalHeadcount, int occupied = 0)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), totalHeadcount);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 20);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 20);

        var capacity = ConfirmedSlot
            .CreateFrom(Guid.NewGuid(), proposal)
            .CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);

        for (var index = 0; index < occupied; index++)
        {
            capacity.Decrement();
        }

        return capacity;
    }

    [Fact]
    public void IncreasingTheTotalIncreasesRemainingByTheSameDelta()
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 6);

        var changed = capacity.AdjustTotalHeadcount(12);

        Assert.True(changed);
        Assert.Equal(12, capacity.TotalHeadcount);
        Assert.Equal(6, capacity.RemainingCapacity);
        Assert.Equal(6, capacity.OccupiedCapacity);
    }

    [Fact]
    public void DecreasingTheTotalDecreasesRemainingByTheSameDelta()
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 6);

        var changed = capacity.AdjustTotalHeadcount(8);

        Assert.True(changed);
        Assert.Equal(8, capacity.TotalHeadcount);
        Assert.Equal(2, capacity.RemainingCapacity);
        Assert.Equal(6, capacity.OccupiedCapacity);
    }

    [Fact]
    public void TheTotalMayEqualOccupiedCapacity()
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 6);

        capacity.AdjustTotalHeadcount(6);

        Assert.Equal(6, capacity.TotalHeadcount);
        Assert.Equal(0, capacity.RemainingCapacity);
        Assert.False(capacity.HasSpare);
    }

    [Fact]
    public void ATotalBelowOccupiedCapacityIsRejectedWithoutMutation()
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 6);

        var exception = Assert.Throws<DomainException>(
            () => capacity.AdjustTotalHeadcount(5));

        Assert.Equal(
            "totalHeadcount cannot be lower than occupied capacity.",
            exception.Message);
        Assert.Equal(10, capacity.TotalHeadcount);
        Assert.Equal(4, capacity.RemainingCapacity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ATotalMustRemainPositive(int totalHeadcount)
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 2);

        var exception = Assert.Throws<DomainException>(
            () => capacity.AdjustTotalHeadcount(totalHeadcount));

        Assert.Equal("totalHeadcount must be greater than zero.", exception.Message);
        Assert.Equal(10, capacity.TotalHeadcount);
        Assert.Equal(8, capacity.RemainingCapacity);
    }

    [Fact]
    public void ResubmittingTheCurrentTotalReportsNoChange()
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 6);

        var changed = capacity.AdjustTotalHeadcount(10);

        Assert.False(changed);
        Assert.Equal(10, capacity.TotalHeadcount);
        Assert.Equal(4, capacity.RemainingCapacity);
    }
}
`````

## tests/EventBooking.Domain.Tests/Slots/SlotCapacityTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Domain.Tests/Slots/SlotCapacityTests.cs","encoding":"utf8","sha256":"8ab7a09145680c3b84f8c3b07cc1e9239fabe106f3bb30f18b506f412b537872","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Domain.Tests.Slots;

public class SlotCapacityTests
{
    private static SlotCapacity CapacityOf(int headcount)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), headcount);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), headcount);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), headcount);

        return ConfirmedSlot
            .CreateFrom(Guid.NewGuid(), proposal)
            .CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);
    }

    [Fact]
    public void DecrementReducesTheRemainingCountByOne()
    {
        var capacity = CapacityOf(3);

        capacity.Decrement();

        Assert.Equal(2, capacity.RemainingCapacity);
        Assert.Equal(3, capacity.TotalHeadcount);
        Assert.True(capacity.HasSpare);
    }

    [Fact]
    public void DecrementingToZeroLeavesNoSpare()
    {
        var capacity = CapacityOf(1);

        capacity.Decrement();

        Assert.Equal(0, capacity.RemainingCapacity);
        Assert.False(capacity.HasSpare);
    }

    [Fact]
    public void DecrementingPastZeroIsRejected()
    {
        var capacity = CapacityOf(1);
        capacity.Decrement();

        var ex = Assert.Throws<DomainException>(() => capacity.Decrement());
        Assert.Equal("No remaining capacity for this appointment type on this slot.", ex.Message);
        Assert.Equal(0, capacity.RemainingCapacity);
    }

    [Fact]
    public void IncrementGivesTheHeadcountBack()
    {
        var capacity = CapacityOf(2);
        capacity.Decrement();

        capacity.Increment();

        Assert.Equal(2, capacity.RemainingCapacity);
    }

    [Fact]
    public void IncrementingAboveTheAcceptedHeadcountIsRejected()
    {
        var capacity = CapacityOf(2);

        var ex = Assert.Throws<DomainException>(() => capacity.Increment());
        Assert.Equal("Remaining capacity cannot exceed the headcount the manager accepted.", ex.Message);
        Assert.Equal(2, capacity.RemainingCapacity);
    }

    [Fact]
    public void RemainingCapacityStaysWithinBoundsAcrossManyOperations()
    {
        var capacity = CapacityOf(5);

        for (var i = 0; i < 5; i++)
        {
            capacity.Decrement();
            Assert.InRange(capacity.RemainingCapacity, 0, 5);
        }

        for (var i = 0; i < 5; i++)
        {
            capacity.Increment();
            Assert.InRange(capacity.RemainingCapacity, 0, 5);
        }

        Assert.Equal(5, capacity.RemainingCapacity);
    }
}
`````

## tests/EventBooking.Domain.Tests/Slots/SlotProposalAcceptanceTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Domain.Tests/Slots/SlotProposalAcceptanceTests.cs","encoding":"utf8","sha256":"a9387ea736f977ca32badffb687ab2597b6182b50280e65a13c2c2a4a82f22ca","parts":1,"part":1} -->

`````csharp
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
`````

## tests/EventBooking.Domain.Tests/Slots/SlotProposalConfirmationTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Domain.Tests/Slots/SlotProposalConfirmationTests.cs","encoding":"utf8","sha256":"7033f197d59d87eb61f3050f5a89b9ade03196689f0af133b17f4f844c1e3ea9","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Domain.Tests.Slots;

public class SlotProposalConfirmationTests
{
    private static SlotProposal NewProposal() =>
        SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            Guid.NewGuid());

    private static SlotProposal AcceptedByAllThree(int drugAndAlcohol = 10, int medical = 6, int uniform = 8)
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
        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            creator);
        proposal.Withdraw(creator);

        Assert.False(proposal.IsFullyAccepted);
    }
}
`````

## tests/EventBooking.Domain.Tests/Slots/SlotProposalTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Domain.Tests/Slots/SlotProposalTests.cs","encoding":"utf8","sha256":"a14acf526e27eb61f2a06f7047192c302851fabca2ce3869ed1256a669313e6e","parts":1,"part":1} -->

`````csharp
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
`````

## tests/EventBooking.Domain.Tests/Slots/SlotWindowTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Domain.Tests/Slots/SlotWindowTests.cs","encoding":"utf8","sha256":"64fb7865e154f4308859e66b2593f3266107a8de31fbc501726a1fc55db6ce9a","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Domain.Tests.Slots;

public class SlotWindowTests
{
    private static SlotWindow Window(int day, int hour) =>
        new(new DateOnly(2026, 9, day), new TimeOnly(hour, 0));

    [Fact]
    public void EndTimeIsFourHoursAfterTheStart()
    {
        Assert.Equal(new TimeOnly(13, 0), Window(10, 9).EndTime);
        Assert.Equal(new TimeOnly(17, 0), Window(10, 13).EndTime);
    }

    [Fact]
    public void DurationIsAlwaysFourHours()
    {
        Assert.Equal(TimeSpan.FromHours(4), SlotWindow.Duration);
    }

    [Fact]
    public void TwoWindowsWithTheSameDateAndStartAreEqual()
    {
        Assert.Equal(Window(10, 9), Window(10, 9));
        Assert.NotEqual(Window(10, 9), Window(10, 13));
    }

    [Fact]
    public void WindowsSortByDateThenStartTime()
    {
        var unsorted = new List<SlotWindow> { Window(12, 9), Window(10, 13), Window(10, 9) };

        unsorted.Sort();

        Assert.Equal(new List<SlotWindow> { Window(10, 9), Window(10, 13), Window(12, 9) }, unsorted);
    }

    [Fact]
    public void AStartTimeThatWouldRunPastMidnightIsRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(21, 0)));
        Assert.Equal("startTime must leave room for the full 4-hour window on the same day.", ex.Message);
    }

    [Fact]
    public void StartsAfterComparesOnDateOnly()
    {
        Assert.True(Window(10, 9).StartsAfter(new DateOnly(2026, 9, 9)));
        Assert.False(Window(10, 9).StartsAfter(new DateOnly(2026, 9, 10)));
        Assert.False(Window(10, 9).StartsAfter(new DateOnly(2026, 9, 11)));
    }

    [Fact]
    public void ToStringRendersTheWindowForEmailAndAuditText()
    {
        Assert.Equal("2026-09-10 09:00-13:00", Window(10, 9).ToString());
    }
}
`````

## tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs","encoding":"utf8","sha256":"1dde1dcb97d5a4cddffbf53b12e2abf59bbed4e3285b6e25c9962fe63c6ceb9d","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies workspace projections are scoped and contain only operational data.</summary>
[Collection("postgres")]
public sealed class AppointmentWorkspaceQueryTests(PostgresFixture fixture)
{
    /// <summary>Verifies slot counts retain recent-past rows and exclude older, cancelled, inactive-booking, and other-type rows.</summary>
    [Fact]
    public async Task SlotListContainsOnlyRetainedActiveScopedAppointments()
    {
        await fixture.ResetAsync();
        await using (var write = fixture.NewContext())
        {
            var current = AddSlot(write, new DateOnly(2026, 9, 7), cancelled: false);
            var future = AddSlot(write, new DateOnly(2026, 9, 8), cancelled: false);
            var recentPast = AddSlot(write, new DateOnly(2026, 9, 6), cancelled: false);
            var tooOld = AddSlot(write, new DateOnly(2026, 8, 30), cancelled: false);
            var cancelled = AddSlot(write, new DateOnly(2026, 9, 9), cancelled: true);
            AddBooking(write, current, "Alex Morgan", "alex@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.CheckedIn, false);
            AddBooking(write, current, "Priya Shah", "priya@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, current, "Other Type", "other@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            AddBooking(write, future, "Future Candidate", "future@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, recentPast, "Past Candidate", "past@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, tooOld, "Too Old", "old@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, cancelled, "Cancelled Slot", "slot@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, current, "Cancelled Booking", "booking@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, true);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var result = await new AppointmentWorkspaceQueries(read, new TestClock()).ListSlotsAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            new DateOnly(2026, 9, 7),
            CancellationToken.None);

        Assert.Equal("Drug & Alcohol Testing", result.AppointmentTypeName);
        Assert.Equal(3, result.Slots.Count);
        Assert.Equal(new DateOnly(2026, 9, 6), result.Slots[0].Date);
        Assert.Equal(new DateOnly(2026, 9, 7), result.Slots[1].Date);
        Assert.Equal(1, result.Slots[1].Counts.Expected);
        Assert.Equal(1, result.Slots[1].Counts.CheckedIn);
        Assert.Equal(0, result.Slots[1].Counts.Completed);
        Assert.Equal(0, result.Slots[1].Counts.NoShow);
        Assert.Equal(new DateOnly(2026, 9, 8), result.Slots[2].Date);
    }

    /// <summary>Verifies slot detail returns only same-type active rows ordered for staff use.</summary>
    [Fact]
    public async Task SelectedSlotReturnsOnlyMinimumScopedCandidateRows()
    {
        await fixture.ResetAsync();
        Guid slotId;
        await using (var write = fixture.NewContext())
        {
            var slot = AddSlot(write, new DateOnly(2026, 9, 7), cancelled: false);
            slotId = slot.Id;
            AddBooking(write, slot, "Zara Young", "zara@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Completed, false);
            AddBooking(write, slot, "Alex Morgan", "alex@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, slot, "Medical Candidate", "medical@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var detail = await new AppointmentWorkspaceQueries(read, new TestClock()).GetSlotAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            slotId,
            CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal("Drug & Alcohol Testing", detail!.AppointmentTypeName);
        Assert.Equal(slotId, detail.ConfirmedSlotId);
        Assert.Collection(
            detail.Appointments,
            row =>
            {
                Assert.Equal("Alex Morgan", row.CandidateName);
                Assert.Equal("alex@example.com", row.CandidateEmail);
                Assert.Equal(BookingAppointmentStatus.Expected, row.Status);
            },
            row =>
            {
                Assert.Equal("Zara Young", row.CandidateName);
                Assert.Equal(BookingAppointmentStatus.Completed, row.Status);
                Assert.NotNull(row.CheckedInAt);
                Assert.NotNull(row.OutcomeAt);
            });
    }

    /// <summary>Verifies a slot that has no row in trusted scope is indistinguishable from missing.</summary>
    [Fact]
    public async Task SelectedSlotOutsideTrustedScopeIsNotFound()
    {
        await fixture.ResetAsync();
        Guid slotId;
        await using (var write = fixture.NewContext())
        {
            var slot = AddSlot(write, new DateOnly(2026, 9, 7), cancelled: false);
            slotId = slot.Id;
            AddBooking(write, slot, "Medical Candidate", "medical@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        Assert.Null(await new AppointmentWorkspaceQueries(read, new TestClock()).GetSlotAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            slotId,
            CancellationToken.None));
    }

    private static ConfirmedSlot AddSlot(
        EventBookingDbContext context,
        DateOnly date,
        bool cancelled)
    {
        var slot = ConfirmedSlot.CreateImported(
            Guid.NewGuid(),
            new SlotWindow(date, new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 20));
        if (cancelled)
        {
            slot.Cancel();
        }

        context.ConfirmedSlots.Add(slot);
        return slot;
    }

    private static void AddBooking(
        EventBookingDbContext context,
        ConfirmedSlot slot,
        string name,
        string email,
        Guid appointmentTypeId,
        BookingAppointmentStatus status,
        bool cancelled)
    {
        var group = EmployeeGroup.Define(
            Guid.NewGuid(), $"WORKSPACE_{Guid.NewGuid():N}".ToUpperInvariant(), "Workspace", true,
            [appointmentTypeId]);
        context.EmployeeGroups.Add(group);
        var candidate = Candidate.Create(Guid.NewGuid(), name, email, group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            candidate.Id,
            $"invite-{candidate.Id}",
            DateTimeOffset.UtcNow.AddDays(1),
            [slot.Id, Guid.NewGuid(), Guid.NewGuid()],
            candidate.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, slot.Id, $"manage-{candidate.Id}", DateTimeOffset.UtcNow);
        if (cancelled)
        {
            booking.Cancel();
        }

        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, appointmentTypeId);
        var staff = Guid.NewGuid();
        var checkIn = new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero);
        if (status is BookingAppointmentStatus.CheckedIn or BookingAppointmentStatus.Completed)
        {
            appointment.TransitionTo(
                BookingAppointmentStatus.CheckedIn, staff, checkIn, true, false);
        }
        if (status == BookingAppointmentStatus.Completed)
        {
            appointment.TransitionTo(
                BookingAppointmentStatus.Completed, staff, checkIn.AddHours(1), false, false);
        }
        if (status == BookingAppointmentStatus.NoShow)
        {
            appointment.TransitionTo(
                BookingAppointmentStatus.NoShow, staff, checkIn.AddHours(4), false, true);
        }

        context.Candidates.Add(candidate);
        context.Bookings.Add(booking);
        context.BookingAppointments.Add(appointment);
    }

    /// <summary>A fixed clock pinning head-office today to 2026-09-07 for workspace tests.</summary>
    private sealed class TestClock : IClock
    {
        /// <inheritdoc/>
        public DateTimeOffset UtcNow => new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

        /// <inheritdoc/>
        public DateTimeOffset NowAtHeadOffice => UtcNow;

        /// <inheritdoc/>
        public DateOnly TodayAtHeadOffice => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateOnly DateAtHeadOffice(DateTimeOffset instant) => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateTimeOffset InstantAtHeadOffice(DateTimeOffset instant) => instant;
    }
}
`````

## tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs","encoding":"utf8","sha256":"bd1120df20fd67296e3f03063451c1f9a02d1b87e9ac17bea8742d6ae2aa6174","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies the workspace retains recently past slots for late outcome recording.</summary>
[Collection("postgres")]
public sealed class AppointmentWorkspaceRecentPastTests(PostgresFixture fixture)
{
    /// <summary>Verifies the slot list includes 7-days-past slots and excludes 8-days-past slots.</summary>
    [Fact]
    public async Task SlotListRetainsSevenDaysPastAndExcludesOlderSlots()
    {
        await fixture.ResetAsync();
        await using (var write = fixture.NewContext())
        {
            var current = AddSlot(write, new DateOnly(2026, 9, 7), cancelled: false);
            var sevenDaysPast = AddSlot(write, new DateOnly(2026, 8, 31), cancelled: false);
            var eightDaysPast = AddSlot(write, new DateOnly(2026, 8, 30), cancelled: false);
            AddBooking(write, current, "Alex Morgan", "alex@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, sevenDaysPast, "Recent Past", "recent@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, eightDaysPast, "Too Old", "old@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var result = await new AppointmentWorkspaceQueries(read, new TestClock()).ListSlotsAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            new DateOnly(2026, 9, 7),
            CancellationToken.None);

        Assert.Equal(2, result.Slots.Count);
        Assert.Equal(new DateOnly(2026, 8, 31), result.Slots[0].Date);
        Assert.Equal(new DateOnly(2026, 9, 7), result.Slots[1].Date);
    }

    /// <summary>Verifies slot detail loads a recently past slot inside trusted scope.</summary>
    [Fact]
    public async Task SelectedSlotLoadsRecentlyPastSlotInScope()
    {
        await fixture.ResetAsync();
        Guid slotId;
        await using (var write = fixture.NewContext())
        {
            var slot = AddSlot(write, new DateOnly(2026, 9, 6), cancelled: false);
            slotId = slot.Id;
            AddBooking(write, slot, "Past Candidate", "past@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var detail = await new AppointmentWorkspaceQueries(read, new TestClock()).GetSlotAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            slotId,
            CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(slotId, detail!.ConfirmedSlotId);
        Assert.Equal(new DateOnly(2026, 9, 6), detail.Date);
        Assert.Single(detail.Appointments);
    }

    /// <summary>Verifies slot detail returns null for slots older than the allowance or outside scope.</summary>
    [Fact]
    public async Task SelectedSlotTooOldOrOutOfScopeIsNotFound()
    {
        await fixture.ResetAsync();
        Guid tooOldId;
        Guid wrongScopeId;
        await using (var write = fixture.NewContext())
        {
            var tooOld = AddSlot(write, new DateOnly(2026, 8, 30), cancelled: false);
            tooOldId = tooOld.Id;
            AddBooking(write, tooOld, "Too Old", "old@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            var wrongScope = AddSlot(write, new DateOnly(2026, 9, 6), cancelled: false);
            wrongScopeId = wrongScope.Id;
            AddBooking(write, wrongScope, "Medical Candidate", "medical@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var queries = new AppointmentWorkspaceQueries(read, new TestClock());
        Assert.Null(await queries.GetSlotAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, tooOldId, CancellationToken.None));
        Assert.Null(await queries.GetSlotAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, wrongScopeId, CancellationToken.None));
    }

    private static ConfirmedSlot AddSlot(
        EventBookingDbContext context,
        DateOnly date,
        bool cancelled)
    {
        var slot = ConfirmedSlot.CreateImported(
            Guid.NewGuid(),
            new SlotWindow(date, new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 20));
        if (cancelled)
        {
            slot.Cancel();
        }

        context.ConfirmedSlots.Add(slot);
        return slot;
    }

    private static void AddBooking(
        EventBookingDbContext context,
        ConfirmedSlot slot,
        string name,
        string email,
        Guid appointmentTypeId,
        BookingAppointmentStatus status,
        bool cancelled)
    {
        var group = EmployeeGroup.Define(
            Guid.NewGuid(), $"WORKSPACE_{Guid.NewGuid():N}".ToUpperInvariant(), "Workspace", true,
            [appointmentTypeId]);
        context.EmployeeGroups.Add(group);
        var candidate = Candidate.Create(Guid.NewGuid(), name, email, group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            candidate.Id,
            $"invite-{candidate.Id}",
            DateTimeOffset.UtcNow.AddDays(1),
            [slot.Id, Guid.NewGuid(), Guid.NewGuid()],
            candidate.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, slot.Id, $"manage-{candidate.Id}", DateTimeOffset.UtcNow);
        if (cancelled)
        {
            booking.Cancel();
        }

        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, appointmentTypeId);

        context.Candidates.Add(candidate);
        context.Bookings.Add(booking);
        context.BookingAppointments.Add(appointment);
    }

    /// <summary>A fixed clock pinning head-office today to 2026-09-07 for workspace tests.</summary>
    private sealed class TestClock : IClock
    {
        /// <inheritdoc/>
        public DateTimeOffset UtcNow => new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

        /// <inheritdoc/>
        public DateTimeOffset NowAtHeadOffice => UtcNow;

        /// <inheritdoc/>
        public DateOnly TodayAtHeadOffice => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateOnly DateAtHeadOffice(DateTimeOffset instant) => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateTimeOffset InstantAtHeadOffice(DateTimeOffset instant) => instant;
    }
}
`````

## tests/EventBooking.Infrastructure.Tests/AuditQueryTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/AuditQueryTests.cs","encoding":"utf8","sha256":"7ec6ae65c9227bfeb29067b7eb8214af5429bb32721d46f799de507e230fa79f","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class AuditQueryTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ASlotsHistoryComesBackNewestFirst()
    {
        await fixture.ResetAsync();
        var slotId = Guid.NewGuid();

        await using (var write = fixture.NewContext())
        {
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.ConfirmedSlot, slotId, AuditAction.SlotConfirmed,
                ActorType.Staff, "staff-1", Now, null));
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.ConfirmedSlot, slotId, AuditAction.SlotCancelled,
                ActorType.Staff, "staff-2", Now.AddHours(1), "6 bookings voided"));
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.ConfirmedSlot, Guid.NewGuid(), AuditAction.SlotConfirmed,
                ActorType.Staff, "staff-3", Now, null));
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var rows = await new AuditQueries(read).ForEntityAsync(
            AuditEntityTypes.ConfirmedSlot, slotId, CancellationToken.None);

        Assert.Equal(2, rows.Count);
        Assert.Equal("SlotCancelled", rows[0].Action);
        Assert.Equal("6 bookings voided", rows[0].Details);
        Assert.Equal("SlotConfirmed", rows[1].Action);
        Assert.Equal("Staff", rows[1].ActorType);
    }

    [Fact]
    public async Task ACandidatesHistoryGathersTheirInviteAndBookingEntries()
    {
        await fixture.ResetAsync();

        var candidateId = Guid.NewGuid();
        var inviteId = Guid.NewGuid();

        await using (var write = fixture.NewContext())
        {
            var pilots = write.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.Pilots);
            var candidate = Candidate.Create(
                candidateId, "Amara Novak", "a.novak@mail.com", pilots);
            write.Candidates.Add(candidate);
            write.Invites.Add(Invite.CreateInitial(
                inviteId, candidate.Id, "hash", Now.AddDays(4),
                [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
                candidate.RequiredAppointmentTypeIds, 0));

            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Invite, inviteId, AuditAction.InviteCreated,
                ActorType.System, null, Now, "retry 0"));
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Invite, Guid.NewGuid(), AuditAction.InviteCreated,
                ActorType.System, null, Now, "someone else"));

            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var rows = await new AuditQueries(read).ForCandidateAsync(candidateId, CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal("InviteCreated", row.Action);
        Assert.Equal("retry 0", row.Details);
        Assert.Equal("System", row.ActorType);
        Assert.Null(row.ActorId);
    }

    [Fact]
    public async Task ACandidateWithNoHistoryGetsAnEmptyList()
    {
        await fixture.ResetAsync();

        await using var read = fixture.NewContext();
        var rows = await new AuditQueries(read).ForCandidateAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task ForCandidateIncludesBookingAppointmentEvents()
    {
        await fixture.ResetAsync();
        var candidateId = Guid.NewGuid();
        var appointmentId = await SeedCandidateBookingAsync(candidateId);
        await AddAuditAsync(AuditEntityTypes.BookingAppointment, appointmentId, AuditAction.AppointmentCheckedIn);

        await using var context = fixture.NewContext();
        var rows = await new AuditQueries(context).ForCandidateAsync(candidateId, CancellationToken.None);

        Assert.Contains(rows, r => r.EntityId == appointmentId);
    }

    [Fact]
    public async Task ForCandidateIncludesEntriesRecordedAgainstTheCandidateItself()
    {
        await fixture.ResetAsync();
        var candidateId = Guid.NewGuid();
        await SeedCandidateBookingAsync(candidateId);
        await AddAuditAsync(AuditEntityTypes.Candidate, candidateId, AuditAction.EmployeeGroupAssigned, Now.AddHours(-1));
        await AddAuditAsync(AuditEntityTypes.Candidate, Guid.NewGuid(), AuditAction.EmployeeGroupChanged);
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, candidateId, AuditAction.SlotConfirmed);

        await using var context = fixture.NewContext();
        var rows = await new AuditQueries(context).ForCandidateAsync(candidateId, CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal(AuditEntityTypes.Candidate, row.EntityType);
        Assert.Equal(nameof(AuditAction.EmployeeGroupAssigned), row.Action);
    }

    [Fact]
    public async Task SearchOrdersNewestFirstWithStableCursor()
    {
        await fixture.ResetAsync();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, first, AuditAction.SlotConfirmed, Now.AddHours(-2));
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, second, AuditAction.SlotCancelled, Now.AddHours(-1));

        await using var context = fixture.NewContext();
        var queries = new AuditQueries(context);
        var page1 = await queries.SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, AuditEntityTypes.All, null, null, 1),
            CancellationToken.None);
        Assert.Single(page1.Rows);
        Assert.Equal(second, page1.Rows[0].EntityId);
        Assert.NotNull(page1.NextCursor);

        // A row newer than the cursor must not shift the following page.
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, Guid.NewGuid(), AuditAction.SlotConfirmed, Now);

        var page2 = await queries.SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, AuditEntityTypes.All, null, page1.NextCursor, 10),
            CancellationToken.None);
        Assert.Contains(page2.Rows, r => r.EntityId == first);
        Assert.DoesNotContain(page2.Rows, r => r.EntityId == second);
    }

    [Fact]
    public async Task SearchRestrictedToOperationalBucketNeverReturnsCandidateRows()
    {
        await fixture.ResetAsync();
        await AddAuditAsync(AuditEntityTypes.Booking, Guid.NewGuid(), AuditAction.BookingCreated);
        var slotId = Guid.NewGuid();
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, slotId, AuditAction.SlotConfirmed);

        await using var context = fixture.NewContext();
        IReadOnlyList<string> operational =
            [AuditEntityTypes.SlotProposal, AuditEntityTypes.ConfirmedSlot, AuditEntityTypes.StaffAccessProfile];
        var page = await new AuditQueries(context).SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, operational, null, null, 50),
            CancellationToken.None);

        Assert.All(page.Rows, r => Assert.Contains(r.EntityType, operational));
        Assert.Contains(page.Rows, r => r.EntityId == slotId);
    }

    [Fact]
    public async Task SearchWithNoAllowedEntityTypesReturnsAnEmptyPage()
    {
        await fixture.ResetAsync();
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, Guid.NewGuid(), AuditAction.SlotConfirmed);

        await using var context = fixture.NewContext();
        var page = await new AuditQueries(context).SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, [], null, null, 50),
            CancellationToken.None);

        Assert.Empty(page.Rows);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task IdentifierMatchesEntityIdAndActorId()
    {
        await fixture.ResetAsync();
        var entityId = Guid.NewGuid();
        const string actor = "staff-actor-7";
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, entityId, AuditAction.SlotConfirmed, actor: actor);

        await using var context = fixture.NewContext();
        var queries = new AuditQueries(context);
        var byEntity = await queries.SearchAsync(
            new AuditSearchFilter(null, null, null, null, entityId.ToString(), AuditEntityTypes.All, null, null, 50),
            CancellationToken.None);
        Assert.Contains(byEntity.Rows, r => r.EntityId == entityId);

        var byActor = await queries.SearchAsync(
            new AuditSearchFilter(null, null, null, null, actor, AuditEntityTypes.All, null, null, 50),
            CancellationToken.None);
        Assert.Contains(byActor.Rows, r => r.EntityId == entityId);
    }

    [Fact]
    public async Task SearchFiltersByTimestampRangeActionAndActorType()
    {
        await fixture.ResetAsync();
        var wanted = Guid.NewGuid();
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, wanted, AuditAction.SlotCancelled, Now);
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, Guid.NewGuid(), AuditAction.SlotConfirmed, Now);
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, Guid.NewGuid(), AuditAction.SlotCancelled, Now.AddDays(-30));

        await using var context = fixture.NewContext();
        var page = await new AuditQueries(context).SearchAsync(
            new AuditSearchFilter(
                Now.AddHours(-1), Now.AddHours(1), "Staff", nameof(AuditAction.SlotCancelled),
                null, AuditEntityTypes.All, null, null, 50),
            CancellationToken.None);

        var row = Assert.Single(page.Rows);
        Assert.Equal(wanted, row.EntityId);
    }

    [Fact]
    public async Task SearchTreatsAMalformedCursorAsAbsent()
    {
        await fixture.ResetAsync();
        var slotId = Guid.NewGuid();
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, slotId, AuditAction.SlotConfirmed);

        await using var context = fixture.NewContext();
        var page = await new AuditQueries(context).SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, AuditEntityTypes.All, null, "not-a-cursor", 50),
            CancellationToken.None);

        Assert.Contains(page.Rows, r => r.EntityId == slotId);
    }

    /// <summary>Seeds a candidate with an invite, booking, and booking appointment; returns the appointment id.</summary>
    private async Task<Guid> SeedCandidateBookingAsync(Guid candidateId)
    {
        await using var write = fixture.NewContext();
        var pilots = write.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.Pilots);
        var candidate = Candidate.Create(candidateId, "Amara Novak", "a.novak@mail.com", pilots);
        var slotId = Guid.NewGuid();
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidateId, "hash", Now.AddDays(4),
            [slotId, Guid.NewGuid(), Guid.NewGuid()],
            candidate.RequiredAppointmentTypeIds, 0);
        var booking = Booking.Create(Guid.NewGuid(), invite, slotId, "manage-token-hash", Now);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting);

        write.Candidates.Add(candidate);
        write.Invites.Add(invite);
        write.Bookings.Add(booking);
        write.BookingAppointments.Add(appointment);
        await write.SaveChangesAsync();

        return appointment.Id;
    }

    /// <summary>Appends one audit-log row and returns the audited entity id.</summary>
    private async Task<Guid> AddAuditAsync(
        string entityType,
        Guid entityId,
        AuditAction action,
        DateTimeOffset? timestamp = null,
        string? actor = "staff-1")
    {
        await using var write = fixture.NewContext();
        write.AuditLogs.Add(AuditLog.Record(
            Guid.NewGuid(), entityType, entityId, action,
            actor is null ? ActorType.System : ActorType.Staff, actor, timestamp ?? Now, null));
        await write.SaveChangesAsync();
        return entityId;
    }
}
`````
