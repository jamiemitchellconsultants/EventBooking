using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Tests.Invites;

/// <summary>
/// Task 8: an invite is restricted to the locations the Coordinator chose, and every later offer
/// is drawn from that same set (FR-5.1, FR-5.2, FR-5.6, FR-5.9).
/// </summary>
public class InviteLocationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid London = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Dublin = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid Tokyo = Guid.Parse("10000000-0000-0000-0000-000000000003");

    private static readonly Guid EventA = Guid.Parse("50000001-0000-0000-0000-000000000001");
    private static readonly Guid EventB = Guid.Parse("50000002-0000-0000-0000-000000000002");
    private static readonly Guid EventC = Guid.Parse("50000003-0000-0000-0000-000000000003");
    private static readonly Guid EventD = Guid.Parse("50000004-0000-0000-0000-000000000004");
    private static readonly Guid EventE = Guid.Parse("50000005-0000-0000-0000-000000000005");
    private static readonly Guid EventF = Guid.Parse("50000006-0000-0000-0000-000000000006");

    private static Invite Initial(IEnumerable<Guid>? locationIds = null, int retryCount = 0) =>
        Invite.CreateInitial(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddDays(4),
            locationIds ?? [London, Dublin],
            [EventA, EventB, EventC],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp],
            retryCount);

    [Fact]
    public void AnInviteSnapshotsTheChosenLocations()
    {
        var invite = Initial();

        Assert.Equal([London, Dublin], invite.LocationIds.Order());
        Assert.All(invite.Locations, location => Assert.Equal(invite.Id, location.InviteId));
    }

    [Fact]
    public void AnInviteWithNoLocationsIsRefused()
    {
        var exception = Assert.Throws<DomainException>(() => Initial([]));

        Assert.Contains("at least one location", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnInviteWithMoreThanFiftyLocationsIsRefused()
    {
        var tooMany = Enumerable.Range(0, Invite.MaximumLocationCount + 1)
            .Select(_ => Guid.NewGuid())
            .ToList();

        var exception = Assert.Throws<DomainException>(() => Initial(tooMany));

        Assert.Contains(
            Invite.MaximumLocationCount.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FiftyLocationsIsAccepted()
    {
        var most = Enumerable.Range(0, Invite.MaximumLocationCount)
            .Select(_ => Guid.NewGuid())
            .ToList();

        Assert.Equal(Invite.MaximumLocationCount, Initial(most).Locations.Count);
    }

    [Fact]
    public void ARepeatedLocationIsRefused()
    {
        var exception = Assert.Throws<DomainException>(() => Initial([London, London]));

        Assert.Contains("same location twice", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AReissueCarriesTheLocationSetAndCountsTheRetry()
    {
        var original = Initial([London, Dublin, Tokyo], retryCount: 1);

        var reissued = Invite.Reissue(
            Guid.NewGuid(), original, Now.AddDays(11),
            [EventD, EventE, EventF]);

        Assert.Equal(original.LocationIds.Order(), reissued.LocationIds.Order());
        Assert.Equal(original.AttendeeId, reissued.AttendeeId);
        Assert.Equal(original.RequiredAppointmentTypeIds, reissued.RequiredAppointmentTypeIds);
        Assert.Equal(2, reissued.RetryCount);
        Assert.Equal(InviteStatus.Pending, reissued.Status);
        Assert.Equal([EventD, EventE, EventF], reissued.OfferedEventIds);
        Assert.All(reissued.Locations, location => Assert.Equal(reissued.Id, location.InviteId));
    }

    [Fact]
    public void AReissueOfARecoveryInviteStaysARecoveryInvite()
    {
        var bookingId = Guid.NewGuid();
        var original = Recovery(bookingId);

        var reissued = Invite.Reissue(
            Guid.NewGuid(), original, Now.AddDays(11),
            [EventD, EventE, EventF]);

        Assert.Equal(bookingId, reissued.RecoveryOfBookingId);
        Assert.Equal(1, reissued.RetryCount);
    }

    [Fact]
    public void ARecoveryInviteDefaultsToTheOriginalBookingsLocation()
    {
        var invite = Recovery(Guid.NewGuid());

        Assert.Equal([London], invite.LocationIds);
        Assert.Equal(0, invite.RetryCount);
    }

    [Fact]
    public void ARecoveryInviteUnionsAdditionalLocationsWithoutDuplicates()
    {
        var invite = Recovery(Guid.NewGuid(), [Dublin, London, Tokyo, Dublin]);

        Assert.Equal([London, Dublin, Tokyo], invite.LocationIds.Order());
    }

    [Fact]
    public void AnInviteSnapshotsMoreThanThreeRequirements()
    {
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddDays(4),
            [London],
            [EventA, EventB, EventC],
            AppointmentTypeIds.All,
            0);

        Assert.Equal(AppointmentTypeIds.All.Order(), invite.RequiredAppointmentTypeIds);
    }

    private static Invite Recovery(Guid bookingId, IEnumerable<Guid>? additionalLocationIds = null) =>
        Invite.CreateRecovery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            bookingId,
            Now.AddDays(4),
            London,
            additionalLocationIds,
            [EventA, EventB, EventC],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
}
