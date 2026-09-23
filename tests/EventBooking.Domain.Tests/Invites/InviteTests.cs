using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Tests.Invites;

public class InviteTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid EventA = Guid.Parse("50000001-0000-0000-0000-000000000001");
    private static readonly Guid EventB = Guid.Parse("50000002-0000-0000-0000-000000000002");
    private static readonly Guid EventC = Guid.Parse("50000003-0000-0000-0000-000000000003");
    private static readonly Guid EventD = Guid.Parse("50000004-0000-0000-0000-000000000004");

    private static Invite NewInvite(int retryCount = 0) =>
        Invite.CreateInitial(
            Guid.NewGuid(), Guid.NewGuid(), "hash-of-the-token", Now.AddDays(4),
            [EventA, EventB, EventC], [AppointmentTypeIds.DrugAndAlcoholTesting], retryCount);

    [Fact]
    public void ANewInviteIsPendingWithThreeOptions()
    {
        var invite = NewInvite();

        Assert.Equal(InviteStatus.Pending, invite.Status);
        Assert.Equal(3, invite.Options.Count);
        Assert.Equal(Invite.RequiredOptionCount, invite.Options.Count);
        Assert.Equal([EventA, EventB, EventC], invite.OfferedEventIds);
        Assert.Equal(0, invite.RetryCount);
        Assert.Equal("hash-of-the-token", invite.TokenHash);
    }

    [Fact]
    public void EveryOptionBelongsToTheInvite()
    {
        var invite = NewInvite();

        Assert.All(invite.Options, o => Assert.Equal(invite.Id, o.InviteId));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void AnInviteMustOfferExactlyThreeOptions(int optionCount)
    {
        var events = new[] { EventA, EventB, EventC, EventD }.Take(optionCount);

        var ex = Assert.Throws<DomainException>(
            () => Invite.CreateInitial(
                Guid.NewGuid(), Guid.NewGuid(), "hash", Now.AddDays(4),
                events, [AppointmentTypeIds.DrugAndAlcoholTesting], 0));
        Assert.Equal("An invite must offer exactly 3 event options.", ex.Message);
    }

    [Fact]
    public void TheSameEventCannotBeOfferedTwice()
    {
        var ex = Assert.Throws<DomainException>(
            () => Invite.CreateInitial(
                Guid.NewGuid(), Guid.NewGuid(), "hash", Now.AddDays(4),
                [EventA, EventA, EventB], [AppointmentTypeIds.DrugAndAlcoholTesting], 0));
        Assert.Equal("An invite cannot offer the same event twice.", ex.Message);
    }

    [Fact]
    public void AnInviteWithoutATokenHashIsRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => Invite.CreateInitial(
                Guid.NewGuid(), Guid.NewGuid(), "  ", Now.AddDays(4),
                [EventA, EventB, EventC], [AppointmentTypeIds.DrugAndAlcoholTesting], 0));
        Assert.Equal("tokenHash must not be blank.", ex.Message);
    }

    [Fact]
    public void ANegativeRetryCountIsRejected()
    {
        Assert.Throws<DomainException>(
            () => Invite.CreateInitial(
                Guid.NewGuid(), Guid.NewGuid(), "hash", Now.AddDays(4),
                [EventA, EventB, EventC], [AppointmentTypeIds.DrugAndAlcoholTesting], -1));
    }

    [Fact]
    public void APendingInviteIsUsableUntilItExpires()
    {
        var invite = NewInvite();

        Assert.True(invite.IsUsableAt(Now));
        Assert.True(invite.IsUsableAt(Now.AddDays(4).AddSeconds(-1)));
        Assert.False(invite.IsUsableAt(Now.AddDays(4)));
        Assert.False(invite.IsUsableAt(Now.AddDays(5)));
    }

    [Fact]
    public void AUsedInviteIsNeverUsableAgain()
    {
        var invite = NewInvite();
        invite.MarkUsed();

        Assert.Equal(InviteStatus.Used, invite.Status);
        Assert.False(invite.IsUsableAt(Now));
    }

    [Fact]
    public void OnlyAPendingInviteCanBeUsedExpiredOrSuperseded()
    {
        var used = NewInvite();
        used.MarkUsed();
        Assert.Throws<DomainException>(() => used.MarkExpired());
        Assert.Throws<DomainException>(() => used.MarkSuperseded());
        Assert.Throws<DomainException>(() => used.MarkUsed());

        var expired = NewInvite();
        expired.MarkExpired();
        Assert.Equal(InviteStatus.Expired, expired.Status);
        Assert.Throws<DomainException>(() => expired.MarkUsed());

        var superseded = NewInvite();
        superseded.MarkSuperseded();
        Assert.Equal(InviteStatus.Superseded, superseded.Status);
    }

    [Fact]
    public void AnOptionThatFilledUpIsDroppedAndAReplacementRestoresThree()
    {
        var invite = NewInvite();

        invite.RemoveOption(EventB);
        Assert.Equal(2, invite.Options.Count);
        Assert.False(invite.Offers(EventB));

        invite.AddOption(EventD);
        Assert.Equal(3, invite.Options.Count);
        Assert.True(invite.Offers(EventD));
    }

    [Fact]
    public void AFourthOptionIsRejected()
    {
        var invite = NewInvite();

        var ex = Assert.Throws<DomainException>(() => invite.AddOption(EventD));
        Assert.Equal("An invite cannot offer more than 3 event options.", ex.Message);
    }

    [Fact]
    public void AddingAnOptionAlreadyOfferedIsRejected()
    {
        var invite = NewInvite();
        invite.RemoveOption(EventB);

        var ex = Assert.Throws<DomainException>(() => invite.AddOption(EventA));
        Assert.Equal("An invite cannot offer the same event twice.", ex.Message);
    }

    [Fact]
    public void RemovingAnOptionThatWasNotOfferedIsRejected()
    {
        var invite = NewInvite();

        var ex = Assert.Throws<DomainException>(() => invite.RemoveOption(EventD));
        Assert.Equal("This invite does not offer that eventItem.", ex.Message);
    }

    [Fact]
    public void OptionsCanOnlyChangeWhileTheInviteIsPending()
    {
        var invite = NewInvite();
        invite.MarkUsed();

        Assert.Throws<DomainException>(() => invite.RemoveOption(EventA));
        Assert.Throws<DomainException>(() => invite.AddOption(EventD));
    }

    [Fact]
    public void TheRetryCountIsCarriedForwardByTheCaller()
    {
        var invite = NewInvite(retryCount: 2);

        Assert.Equal(2, invite.RetryCount);
    }
}
