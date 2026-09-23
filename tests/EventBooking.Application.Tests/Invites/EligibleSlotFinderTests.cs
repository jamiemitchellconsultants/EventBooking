using EventBooking.Application.Invites;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Invites;

public class EligibleSlotFinderTests
{
    private static readonly Guid[] NeedsTwo =
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting];

    private readonly InMemoryConfirmedSlotRepository _slots = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    private EligibleSlotFinder Finder => new(_slots, _clock);

    [Fact]
    public async Task TheThreeEarliestQualifyingSlotsAreReturnedInOrder()
    {
        AddSlot(new DateOnly(2026, 9, 14));
        AddSlot(new DateOnly(2026, 9, 10));
        AddSlot(new DateOnly(2026, 9, 12));
        AddSlot(new DateOnly(2026, 9, 16));

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.Equal(
            new[] { new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 14) },
            result.Select(s => s.Window.Date));
    }

    [Fact]
    public async Task TwoWindowsOnOneDayAreOrderedByStartTime()
    {
        AddSlot(new DateOnly(2026, 9, 10), startHour: 13);
        AddSlot(new DateOnly(2026, 9, 10), startHour: 9);

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Equal(
            new[] { new TimeOnly(9, 0), new TimeOnly(13, 0) },
            result.Select(s => s.Window.StartTime));
    }

    [Fact]
    public async Task ASlotFullInOneRequiredTypeIsNotEligibleEvenIfTheOthersHaveRoom()
    {
        var slot = AddSlot(new DateOnly(2026, 9, 10), drugAndAlcoholHeadcount: 1);
        slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ASlotFullOnlyInATypeTheCandidateDoesNotNeedIsStillEligible()
    {
        var slot = AddSlot(new DateOnly(2026, 9, 10), medicalHeadcount: 1);
        slot.CapacityFor(AppointmentTypeIds.MedicalCheckUp).Decrement();

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Single(result);
    }

    [Fact]
    public async Task CancelledSlotsAreNeverEligible()
    {
        var slot = AddSlot(new DateOnly(2026, 9, 10));
        slot.Cancel();

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task TodaysAndPastWindowsAreNeverEligible()
    {
        AddSlot(new DateOnly(2026, 9, 3));
        AddSlot(new DateOnly(2026, 9, 1));

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ExcludedSlotsAreSkipped()
    {
        var first = AddSlot(new DateOnly(2026, 9, 10));
        AddSlot(new DateOnly(2026, 9, 12));

        var result = await Finder.FindAsync(NeedsTwo, 3, [first.Id], CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 9, 12), Assert.Single(result).Window.Date);
    }

    [Fact]
    public async Task FewerQualifyingSlotsThanAskedForReturnsWhatThereIs()
    {
        AddSlot(new DateOnly(2026, 9, 10));
        AddSlot(new DateOnly(2026, 9, 12));

        var result = await Finder.FindAsync(NeedsTwo, 3, [], CancellationToken.None);

        Assert.Equal(2, result.Count);
    }

    private ConfirmedSlot AddSlot(
        DateOnly date,
        int startHour = 9,
        int drugAndAlcoholHeadcount = 10,
        int medicalHeadcount = 6,
        int uniformHeadcount = 8)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(date, new TimeOnly(startHour, 0)), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), drugAndAlcoholHeadcount);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), medicalHeadcount);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), uniformHeadcount);

        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);
        _slots.Add(slot);
        return slot;
    }
}
