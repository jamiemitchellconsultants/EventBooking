# 02a — Deterministic attendee links and the token version counter, edits 24 (Task 9a)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.Domain.Tests/Invites/InviteRequirementSnapshotTests.cs — 1/1

<!-- retirement-file: {"id":58,"file":"tests/EventBooking.Domain.Tests/Invites/InviteRequirementSnapshotTests.cs","beforeSha":"a99b3194121189784c72d0e5f72f24fcd378551ba41488bf954e9448d0806eaf","afterSha":"293f26421c1019c6cbcdbe43031200711fc37fd41aca9c67c04e9e6d5e4add69","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Tests.Invites;

/// <summary>Verifies initial and recovery Invites own immutable requirement snapshots.</summary>
public sealed class InviteRequirementSnapshotTests
{
    private static readonly Guid[] Options = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];

    /// <summary>An initial Invite snapshots distinct known requirements in stable order.</summary>
    [Fact]
    public void InitialInviteSnapshotsRequirements()
    {
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            Options,
            [AppointmentTypeIds.UniformFitting, AppointmentTypeIds.DrugAndAlcoholTesting],
            0);

        Assert.Null(invite.RecoveryOfBookingId);
        Assert.Equal(
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            invite.RequiredAppointmentTypeIds);
    }

    /// <summary>A recovery Invite retains its root Booking and can be explicitly cancelled.</summary>
    [Fact]
    public void RecoveryInviteLinksTheRootAndCancels()
    {
        var root = Guid.NewGuid();
        var invite = Invite.CreateRecovery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            root,
            DateTimeOffset.UtcNow.AddDays(1),
            ProposalFixture.LocationId,
            null,
            Options,
            [AppointmentTypeIds.MedicalCheckUp]);

        invite.CancelRecovery();

        Assert.Equal(root, invite.RecoveryOfBookingId);
        Assert.Equal(InviteStatus.Cancelled, invite.Status);
    }

    /// <summary>Empty, duplicate, unknown, and oversized snapshots are rejected.</summary>
    [Theory]
    [MemberData(nameof(InvalidSnapshots))]
    public void InvalidSnapshotsCannotBeCreated(Guid[] snapshot)
    {
        Assert.Throws<DomainException>(() => Invite.CreateInitial(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            Options,
            snapshot,
            0));
    }

    /// <summary>Provides every invalid snapshot shape.</summary>
    public static TheoryData<Guid[]> InvalidSnapshots => new()
    {
        { [] },
        { [AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.MedicalCheckUp] },
        { [Guid.NewGuid()] },
        { [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
            AppointmentTypeIds.UniformFitting, AppointmentTypeIds.DrugAndAlcoholTesting] },
    };
}
`````

## before — tests/EventBooking.Domain.Tests/Invites/InviteTests.cs — 1/1

<!-- retirement-file: {"id":59,"file":"tests/EventBooking.Domain.Tests/Invites/InviteTests.cs","beforeSha":"b4f0f391ce8d60895db982da5a9e8e1b90f08915969cebadae90f6ce338ada58","afterSha":"0e13049801e2f86fad8b9cf89e26b67417fa675ad098ac527379dcc9c4a906e1","side":"before","part":1,"parts":1} -->

`````csharp
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
            Guid.NewGuid(),
            Guid.NewGuid(),
            "hash-of-the-token",
            Now.AddDays(4),
            [ProposalFixture.LocationId],
            [EventA, EventB, EventC],
            [AppointmentTypeIds.DrugAndAlcoholTesting],
            retryCount);

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
                Guid.NewGuid(),
                Guid.NewGuid(),
                "hash",
                Now.AddDays(4),
                [ProposalFixture.LocationId],
                events,
                [AppointmentTypeIds.DrugAndAlcoholTesting],
                0));
        Assert.Equal("An invite must offer exactly 3 event options.", ex.Message);
    }

    [Fact]
    public void TheSameEventCannotBeOfferedTwice()
    {
        var ex = Assert.Throws<DomainException>(
            () => Invite.CreateInitial(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "hash",
                Now.AddDays(4),
                [ProposalFixture.LocationId],
                [EventA, EventA, EventB],
                [AppointmentTypeIds.DrugAndAlcoholTesting],
                0));
        Assert.Equal("An invite cannot offer the same event twice.", ex.Message);
    }

    [Fact]
    public void AnInviteWithoutATokenHashIsRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => Invite.CreateInitial(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "  ",
                Now.AddDays(4),
                [ProposalFixture.LocationId],
                [EventA, EventB, EventC],
                [AppointmentTypeIds.DrugAndAlcoholTesting],
                0));
        Assert.Equal("tokenHash must not be blank.", ex.Message);
    }

    [Fact]
    public void ANegativeRetryCountIsRejected()
    {
        Assert.Throws<DomainException>(
            () => Invite.CreateInitial(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "hash",
                Now.AddDays(4),
                [ProposalFixture.LocationId],
                [EventA, EventB, EventC],
                [AppointmentTypeIds.DrugAndAlcoholTesting],
                -1));
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
`````

## after — tests/EventBooking.Domain.Tests/Invites/InviteTests.cs — 1/1

<!-- retirement-file: {"id":59,"file":"tests/EventBooking.Domain.Tests/Invites/InviteTests.cs","beforeSha":"b4f0f391ce8d60895db982da5a9e8e1b90f08915969cebadae90f6ce338ada58","afterSha":"0e13049801e2f86fad8b9cf89e26b67417fa675ad098ac527379dcc9c4a906e1","side":"after","part":1,"parts":1} -->

`````csharp
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
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddDays(4),
            [ProposalFixture.LocationId],
            [EventA, EventB, EventC],
            [AppointmentTypeIds.DrugAndAlcoholTesting],
            retryCount);

    [Fact]
    public void ANewInviteIsPendingWithThreeOptions()
    {
        var invite = NewInvite();

        Assert.Equal(InviteStatus.Pending, invite.Status);
        Assert.Equal(3, invite.Options.Count);
        Assert.Equal(Invite.RequiredOptionCount, invite.Options.Count);
        Assert.Equal([EventA, EventB, EventC], invite.OfferedEventIds);
        Assert.Equal(0, invite.RetryCount);
        Assert.Equal(Invite.InitialTokenVersion, invite.TokenVersion);
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
                Guid.NewGuid(),
                Guid.NewGuid(),
                Now.AddDays(4),
                [ProposalFixture.LocationId],
                events,
                [AppointmentTypeIds.DrugAndAlcoholTesting],
                0));
        Assert.Equal("An invite must offer exactly 3 event options.", ex.Message);
    }

    [Fact]
    public void TheSameEventCannotBeOfferedTwice()
    {
        var ex = Assert.Throws<DomainException>(
            () => Invite.CreateInitial(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Now.AddDays(4),
                [ProposalFixture.LocationId],
                [EventA, EventA, EventB],
                [AppointmentTypeIds.DrugAndAlcoholTesting],
                0));
        Assert.Equal("An invite cannot offer the same event twice.", ex.Message);
    }

    [Fact]
    public void RotatingAPendingInvitesTokenMovesToTheNextVersion()
    {
        var invite = NewInvite();

        invite.RotateToken();

        Assert.Equal(Invite.InitialTokenVersion + 1, invite.TokenVersion);
    }

    [Fact]
    public void RotatingTheTokenOfAnInviteThatIsNoLongerPendingIsRejected()
    {
        var invite = NewInvite();
        invite.MarkUsed();

        var ex = Assert.Throws<DomainException>(invite.RotateToken);
        Assert.Equal("Only a pending invite token can be rotated.", ex.Message);
    }

    [Fact]
    public void ANegativeRetryCountIsRejected()
    {
        Assert.Throws<DomainException>(
            () => Invite.CreateInitial(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Now.AddDays(4),
                [ProposalFixture.LocationId],
                [EventA, EventB, EventC],
                [AppointmentTypeIds.DrugAndAlcoholTesting],
                -1));
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
`````

## before — tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs — 1/1

<!-- retirement-file: {"id":60,"file":"tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs","beforeSha":"270b0649b53e9d3d46b15763eacdb0a1e7ad6bcc2deb8e4c7501035102a9db0b","afterSha":"a26a674434fb8836a93f64285086f8649bfea92e76872f95296da287208acbe6","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies workspace projections are scoped and contain only operational data.</summary>
[Collection("postgres")]
public sealed class AppointmentWorkspaceQueryTests(PostgresFixture fixture)
{
    /// <summary>Verifies event counts retain recent-past rows and exclude older, cancelled, inactive-booking, and other-type rows.</summary>
    [Fact]
    public async Task EventListContainsOnlyRetainedActiveScopedAppointments()
    {
        await fixture.ResetAsync();
        await using (var write = fixture.NewContext())
        {
            var current = AddEvent(write, new DateOnly(2026, 9, 7), cancelled: false);
            var future = AddEvent(write, new DateOnly(2026, 9, 8), cancelled: false);
            var recentPast = AddEvent(write, new DateOnly(2026, 9, 6), cancelled: false);
            var tooOld = AddEvent(write, new DateOnly(2026, 8, 30), cancelled: false);
            var cancelled = AddEvent(write, new DateOnly(2026, 9, 9), cancelled: true);
            AddBooking(write, current, "Alex Morgan", "alex@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.CheckedIn, false);
            AddBooking(write, current, "Priya Shah", "priya@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, current, "Other Type", "other@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            AddBooking(write, future, "Future Attendee", "future@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, recentPast, "Past Attendee", "past@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, tooOld, "Too Old", "old@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, cancelled, "Cancelled Event", "event@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, current, "Cancelled Booking", "booking@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, true);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var result = await new AppointmentWorkspaceQueries(read, new TestClock()).ListEventsAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            new DateOnly(2026, 9, 7),
            CancellationToken.None);

        Assert.Equal("Drug & Alcohol Testing", result.AppointmentTypeName);
        Assert.Equal(3, result.Events.Count);
        Assert.Equal(new DateOnly(2026, 9, 6), result.Events[0].Date);
        Assert.Equal(new DateOnly(2026, 9, 7), result.Events[1].Date);
        Assert.Equal(1, result.Events[1].Counts.Expected);
        Assert.Equal(1, result.Events[1].Counts.CheckedIn);
        Assert.Equal(0, result.Events[1].Counts.Completed);
        Assert.Equal(0, result.Events[1].Counts.NoShow);
        Assert.Equal(new DateOnly(2026, 9, 8), result.Events[2].Date);
    }

    /// <summary>Verifies event detail returns only same-type active rows ordered for staff use.</summary>
    [Fact]
    public async Task SelectedEventReturnsOnlyMinimumScopedAttendeeRows()
    {
        await fixture.ResetAsync();
        Guid eventId;
        await using (var write = fixture.NewContext())
        {
            var eventItem = AddEvent(write, new DateOnly(2026, 9, 7), cancelled: false);
            eventId = eventItem.Id;
            AddBooking(write, eventItem, "Zara Young", "zara@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Completed, false);
            AddBooking(write, eventItem, "Alex Morgan", "alex@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, eventItem, "Medical Attendee", "medical@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var detail = await new AppointmentWorkspaceQueries(read, new TestClock()).GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            eventId,
            CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal("Drug & Alcohol Testing", detail!.AppointmentTypeName);
        Assert.Equal(eventId, detail.EventId);
        Assert.Collection(
            detail.Appointments,
            row =>
            {
                Assert.Equal("Alex Morgan", row.AttendeeName);
                Assert.Equal("alex@example.com", row.AttendeeEmail);
                Assert.Equal(BookingAppointmentStatus.Expected, row.Status);
            },
            row =>
            {
                Assert.Equal("Zara Young", row.AttendeeName);
                Assert.Equal(BookingAppointmentStatus.Completed, row.Status);
                Assert.NotNull(row.CheckedInAt);
                Assert.NotNull(row.OutcomeAt);
            });
    }

    /// <summary>Verifies a event that has no row in trusted scope is indistinguishable from missing.</summary>
    [Fact]
    public async Task SelectedEventOutsideTrustedScopeIsNotFound()
    {
        await fixture.ResetAsync();
        Guid eventId;
        await using (var write = fixture.NewContext())
        {
            var eventItem = AddEvent(write, new DateOnly(2026, 9, 7), cancelled: false);
            eventId = eventItem.Id;
            AddBooking(write, eventItem, "Medical Attendee", "medical@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        Assert.Null(await new AppointmentWorkspaceQueries(read, new TestClock()).GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            eventId,
            CancellationToken.None));
    }

    private static Event AddEvent(
        EventBookingDbContext context,
        DateOnly date,
        bool cancelled)
    {
        var eventItem = EventFixture.Create(
            Guid.NewGuid(),
            new EventWindow(date, new TimeOnly(9, 0), 240),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 20));
        if (cancelled)
        {
            eventItem.CancelBeforeStart();
        }

        context.Events.Add(eventItem);
        return eventItem;
    }

    private static void AddBooking(
        EventBookingDbContext context,
        Event eventItem,
        string name,
        string email,
        Guid appointmentTypeId,
        BookingAppointmentStatus status,
        bool cancelled)
    {
        var group = AttendeeGroup.Define(
            Guid.NewGuid(), $"WORKSPACE_{Guid.NewGuid():N}".ToUpperInvariant(), "Workspace", true,
            [appointmentTypeId]);
        context.AttendeeGroups.Add(group);
        var attendee = Attendee.Create(Guid.NewGuid(), name, email, group, ProposalFixture.Now);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            $"invite-{attendee.Id}",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, $"manage-{attendee.Id}", DateTimeOffset.UtcNow);
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

        context.Attendees.Add(attendee);
        context.Bookings.Add(booking);
        context.BookingAppointments.Add(appointment);
    }

    /// <summary>A fixed clock pinning transitional-location today to 2026-09-07 for workspace tests.</summary>
    private sealed class TestClock : IClock
    {
        /// <inheritdoc/>
        public DateTimeOffset UtcNow => new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

        /// <inheritdoc/>
        public DateTimeOffset NowAtTransitionalLocation => UtcNow;

        /// <inheritdoc/>
        public DateOnly TodayAtTransitionalLocation => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => instant;
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs — 1/1

<!-- retirement-file: {"id":60,"file":"tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs","beforeSha":"270b0649b53e9d3d46b15763eacdb0a1e7ad6bcc2deb8e4c7501035102a9db0b","afterSha":"a26a674434fb8836a93f64285086f8649bfea92e76872f95296da287208acbe6","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies workspace projections are scoped and contain only operational data.</summary>
[Collection("postgres")]
public sealed class AppointmentWorkspaceQueryTests(PostgresFixture fixture)
{
    /// <summary>Verifies event counts retain recent-past rows and exclude older, cancelled, inactive-booking, and other-type rows.</summary>
    [Fact]
    public async Task EventListContainsOnlyRetainedActiveScopedAppointments()
    {
        await fixture.ResetAsync();
        await using (var write = fixture.NewContext())
        {
            var current = AddEvent(write, new DateOnly(2026, 9, 7), cancelled: false);
            var future = AddEvent(write, new DateOnly(2026, 9, 8), cancelled: false);
            var recentPast = AddEvent(write, new DateOnly(2026, 9, 6), cancelled: false);
            var tooOld = AddEvent(write, new DateOnly(2026, 8, 30), cancelled: false);
            var cancelled = AddEvent(write, new DateOnly(2026, 9, 9), cancelled: true);
            AddBooking(write, current, "Alex Morgan", "alex@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.CheckedIn, false);
            AddBooking(write, current, "Priya Shah", "priya@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, current, "Other Type", "other@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            AddBooking(write, future, "Future Attendee", "future@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, recentPast, "Past Attendee", "past@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, tooOld, "Too Old", "old@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, cancelled, "Cancelled Event", "event@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, current, "Cancelled Booking", "booking@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, true);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var result = await new AppointmentWorkspaceQueries(read, new TestClock()).ListEventsAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            new DateOnly(2026, 9, 7),
            CancellationToken.None);

        Assert.Equal("Drug & Alcohol Testing", result.AppointmentTypeName);
        Assert.Equal(3, result.Events.Count);
        Assert.Equal(new DateOnly(2026, 9, 6), result.Events[0].Date);
        Assert.Equal(new DateOnly(2026, 9, 7), result.Events[1].Date);
        Assert.Equal(1, result.Events[1].Counts.Expected);
        Assert.Equal(1, result.Events[1].Counts.CheckedIn);
        Assert.Equal(0, result.Events[1].Counts.Completed);
        Assert.Equal(0, result.Events[1].Counts.NoShow);
        Assert.Equal(new DateOnly(2026, 9, 8), result.Events[2].Date);
    }

    /// <summary>Verifies event detail returns only same-type active rows ordered for staff use.</summary>
    [Fact]
    public async Task SelectedEventReturnsOnlyMinimumScopedAttendeeRows()
    {
        await fixture.ResetAsync();
        Guid eventId;
        await using (var write = fixture.NewContext())
        {
            var eventItem = AddEvent(write, new DateOnly(2026, 9, 7), cancelled: false);
            eventId = eventItem.Id;
            AddBooking(write, eventItem, "Zara Young", "zara@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Completed, false);
            AddBooking(write, eventItem, "Alex Morgan", "alex@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, eventItem, "Medical Attendee", "medical@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var detail = await new AppointmentWorkspaceQueries(read, new TestClock()).GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            eventId,
            CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal("Drug & Alcohol Testing", detail!.AppointmentTypeName);
        Assert.Equal(eventId, detail.EventId);
        Assert.Collection(
            detail.Appointments,
            row =>
            {
                Assert.Equal("Alex Morgan", row.AttendeeName);
                Assert.Equal("alex@example.com", row.AttendeeEmail);
                Assert.Equal(BookingAppointmentStatus.Expected, row.Status);
            },
            row =>
            {
                Assert.Equal("Zara Young", row.AttendeeName);
                Assert.Equal(BookingAppointmentStatus.Completed, row.Status);
                Assert.NotNull(row.CheckedInAt);
                Assert.NotNull(row.OutcomeAt);
            });
    }

    /// <summary>Verifies a event that has no row in trusted scope is indistinguishable from missing.</summary>
    [Fact]
    public async Task SelectedEventOutsideTrustedScopeIsNotFound()
    {
        await fixture.ResetAsync();
        Guid eventId;
        await using (var write = fixture.NewContext())
        {
            var eventItem = AddEvent(write, new DateOnly(2026, 9, 7), cancelled: false);
            eventId = eventItem.Id;
            AddBooking(write, eventItem, "Medical Attendee", "medical@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        Assert.Null(await new AppointmentWorkspaceQueries(read, new TestClock()).GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            eventId,
            CancellationToken.None));
    }

    private static Event AddEvent(
        EventBookingDbContext context,
        DateOnly date,
        bool cancelled)
    {
        var eventItem = EventFixture.Create(
            Guid.NewGuid(),
            new EventWindow(date, new TimeOnly(9, 0), 240),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 20));
        if (cancelled)
        {
            eventItem.CancelBeforeStart();
        }

        context.Events.Add(eventItem);
        return eventItem;
    }

    private static void AddBooking(
        EventBookingDbContext context,
        Event eventItem,
        string name,
        string email,
        Guid appointmentTypeId,
        BookingAppointmentStatus status,
        bool cancelled)
    {
        var group = AttendeeGroup.Define(
            Guid.NewGuid(), $"WORKSPACE_{Guid.NewGuid():N}".ToUpperInvariant(), "Workspace", true,
            [appointmentTypeId]);
        context.AttendeeGroups.Add(group);
        var attendee = Attendee.Create(Guid.NewGuid(), name, email, group, ProposalFixture.Now);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, DateTimeOffset.UtcNow);
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

        context.Attendees.Add(attendee);
        context.Bookings.Add(booking);
        context.BookingAppointments.Add(appointment);
    }

    /// <summary>A fixed clock pinning transitional-location today to 2026-09-07 for workspace tests.</summary>
    private sealed class TestClock : IClock
    {
        /// <inheritdoc/>
        public DateTimeOffset UtcNow => new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

        /// <inheritdoc/>
        public DateTimeOffset NowAtTransitionalLocation => UtcNow;

        /// <inheritdoc/>
        public DateOnly TodayAtTransitionalLocation => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => instant;
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs — 1/1

<!-- retirement-file: {"id":61,"file":"tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs","beforeSha":"ad9d261432e2a2d0cba588a2b996470dd89d079ae0450567712aabe3c67cb0f1","afterSha":"0156e1c30769e69ebf867585a530b60289eb7d502ededb622f9f9e7e3521b94e","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies the workspace retains recently past events for late outcome recording.</summary>
[Collection("postgres")]
public sealed class AppointmentWorkspaceRecentPastTests(PostgresFixture fixture)
{
    /// <summary>Verifies the event list includes 7-days-past events and excludes 8-days-past events.</summary>
    [Fact]
    public async Task EventListRetainsSevenDaysPastAndExcludesOlderEvents()
    {
        await fixture.ResetAsync();
        await using (var write = fixture.NewContext())
        {
            var current = AddEvent(write, new DateOnly(2026, 9, 7), cancelled: false);
            var sevenDaysPast = AddEvent(write, new DateOnly(2026, 8, 31), cancelled: false);
            var eightDaysPast = AddEvent(write, new DateOnly(2026, 8, 30), cancelled: false);
            AddBooking(write, current, "Alex Morgan", "alex@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, sevenDaysPast, "Recent Past", "recent@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, eightDaysPast, "Too Old", "old@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var result = await new AppointmentWorkspaceQueries(read, new TestClock()).ListEventsAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            new DateOnly(2026, 9, 7),
            CancellationToken.None);

        Assert.Equal(2, result.Events.Count);
        Assert.Equal(new DateOnly(2026, 8, 31), result.Events[0].Date);
        Assert.Equal(new DateOnly(2026, 9, 7), result.Events[1].Date);
    }

    /// <summary>Verifies event detail loads a recently past event inside trusted scope.</summary>
    [Fact]
    public async Task SelectedEventLoadsRecentlyPastEventInScope()
    {
        await fixture.ResetAsync();
        Guid eventId;
        await using (var write = fixture.NewContext())
        {
            var eventItem = AddEvent(write, new DateOnly(2026, 9, 6), cancelled: false);
            eventId = eventItem.Id;
            AddBooking(write, eventItem, "Past Attendee", "past@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var detail = await new AppointmentWorkspaceQueries(read, new TestClock()).GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            eventId,
            CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(eventId, detail!.EventId);
        Assert.Equal(new DateOnly(2026, 9, 6), detail.Date);
        Assert.Single(detail.Appointments);
    }

    /// <summary>Verifies event detail returns null for events older than the allowance or outside scope.</summary>
    [Fact]
    public async Task SelectedEventTooOldOrOutOfScopeIsNotFound()
    {
        await fixture.ResetAsync();
        Guid tooOldId;
        Guid wrongScopeId;
        await using (var write = fixture.NewContext())
        {
            var tooOld = AddEvent(write, new DateOnly(2026, 8, 30), cancelled: false);
            tooOldId = tooOld.Id;
            AddBooking(write, tooOld, "Too Old", "old@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            var wrongScope = AddEvent(write, new DateOnly(2026, 9, 6), cancelled: false);
            wrongScopeId = wrongScope.Id;
            AddBooking(write, wrongScope, "Medical Attendee", "medical@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var queries = new AppointmentWorkspaceQueries(read, new TestClock());
        Assert.Null(await queries.GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, tooOldId, CancellationToken.None));
        Assert.Null(await queries.GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, wrongScopeId, CancellationToken.None));
    }

    private static Event AddEvent(
        EventBookingDbContext context,
        DateOnly date,
        bool cancelled)
    {
        var eventItem = EventFixture.Create(
            Guid.NewGuid(),
            new EventWindow(date, new TimeOnly(9, 0), 240),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 20));
        if (cancelled)
        {
            eventItem.CancelBeforeStart();
        }

        context.Events.Add(eventItem);
        return eventItem;
    }

    private static void AddBooking(
        EventBookingDbContext context,
        Event eventItem,
        string name,
        string email,
        Guid appointmentTypeId,
        BookingAppointmentStatus status,
        bool cancelled)
    {
        var group = AttendeeGroup.Define(
            Guid.NewGuid(), $"WORKSPACE_{Guid.NewGuid():N}".ToUpperInvariant(), "Workspace", true,
            [appointmentTypeId]);
        context.AttendeeGroups.Add(group);
        var attendee = Attendee.Create(Guid.NewGuid(), name, email, group, ProposalFixture.Now);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            $"invite-{attendee.Id}",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, $"manage-{attendee.Id}", DateTimeOffset.UtcNow);
        if (cancelled)
        {
            booking.Cancel();
        }

        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, appointmentTypeId);

        context.Attendees.Add(attendee);
        context.Bookings.Add(booking);
        context.BookingAppointments.Add(appointment);
    }

    /// <summary>A fixed clock pinning transitional-location today to 2026-09-07 for workspace tests.</summary>
    private sealed class TestClock : IClock
    {
        /// <inheritdoc/>
        public DateTimeOffset UtcNow => new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

        /// <inheritdoc/>
        public DateTimeOffset NowAtTransitionalLocation => UtcNow;

        /// <inheritdoc/>
        public DateOnly TodayAtTransitionalLocation => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => instant;
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs — 1/1

<!-- retirement-file: {"id":61,"file":"tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs","beforeSha":"ad9d261432e2a2d0cba588a2b996470dd89d079ae0450567712aabe3c67cb0f1","afterSha":"0156e1c30769e69ebf867585a530b60289eb7d502ededb622f9f9e7e3521b94e","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies the workspace retains recently past events for late outcome recording.</summary>
[Collection("postgres")]
public sealed class AppointmentWorkspaceRecentPastTests(PostgresFixture fixture)
{
    /// <summary>Verifies the event list includes 7-days-past events and excludes 8-days-past events.</summary>
    [Fact]
    public async Task EventListRetainsSevenDaysPastAndExcludesOlderEvents()
    {
        await fixture.ResetAsync();
        await using (var write = fixture.NewContext())
        {
            var current = AddEvent(write, new DateOnly(2026, 9, 7), cancelled: false);
            var sevenDaysPast = AddEvent(write, new DateOnly(2026, 8, 31), cancelled: false);
            var eightDaysPast = AddEvent(write, new DateOnly(2026, 8, 30), cancelled: false);
            AddBooking(write, current, "Alex Morgan", "alex@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, sevenDaysPast, "Recent Past", "recent@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, eightDaysPast, "Too Old", "old@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var result = await new AppointmentWorkspaceQueries(read, new TestClock()).ListEventsAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            new DateOnly(2026, 9, 7),
            CancellationToken.None);

        Assert.Equal(2, result.Events.Count);
        Assert.Equal(new DateOnly(2026, 8, 31), result.Events[0].Date);
        Assert.Equal(new DateOnly(2026, 9, 7), result.Events[1].Date);
    }

    /// <summary>Verifies event detail loads a recently past event inside trusted scope.</summary>
    [Fact]
    public async Task SelectedEventLoadsRecentlyPastEventInScope()
    {
        await fixture.ResetAsync();
        Guid eventId;
        await using (var write = fixture.NewContext())
        {
            var eventItem = AddEvent(write, new DateOnly(2026, 9, 6), cancelled: false);
            eventId = eventItem.Id;
            AddBooking(write, eventItem, "Past Attendee", "past@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var detail = await new AppointmentWorkspaceQueries(read, new TestClock()).GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            eventId,
            CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(eventId, detail!.EventId);
        Assert.Equal(new DateOnly(2026, 9, 6), detail.Date);
        Assert.Single(detail.Appointments);
    }

    /// <summary>Verifies event detail returns null for events older than the allowance or outside scope.</summary>
    [Fact]
    public async Task SelectedEventTooOldOrOutOfScopeIsNotFound()
    {
        await fixture.ResetAsync();
        Guid tooOldId;
        Guid wrongScopeId;
        await using (var write = fixture.NewContext())
        {
            var tooOld = AddEvent(write, new DateOnly(2026, 8, 30), cancelled: false);
            tooOldId = tooOld.Id;
            AddBooking(write, tooOld, "Too Old", "old@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            var wrongScope = AddEvent(write, new DateOnly(2026, 9, 6), cancelled: false);
            wrongScopeId = wrongScope.Id;
            AddBooking(write, wrongScope, "Medical Attendee", "medical@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var queries = new AppointmentWorkspaceQueries(read, new TestClock());
        Assert.Null(await queries.GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, tooOldId, CancellationToken.None));
        Assert.Null(await queries.GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, wrongScopeId, CancellationToken.None));
    }

    private static Event AddEvent(
        EventBookingDbContext context,
        DateOnly date,
        bool cancelled)
    {
        var eventItem = EventFixture.Create(
            Guid.NewGuid(),
            new EventWindow(date, new TimeOnly(9, 0), 240),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 20));
        if (cancelled)
        {
            eventItem.CancelBeforeStart();
        }

        context.Events.Add(eventItem);
        return eventItem;
    }

    private static void AddBooking(
        EventBookingDbContext context,
        Event eventItem,
        string name,
        string email,
        Guid appointmentTypeId,
        BookingAppointmentStatus status,
        bool cancelled)
    {
        var group = AttendeeGroup.Define(
            Guid.NewGuid(), $"WORKSPACE_{Guid.NewGuid():N}".ToUpperInvariant(), "Workspace", true,
            [appointmentTypeId]);
        context.AttendeeGroups.Add(group);
        var attendee = Attendee.Create(Guid.NewGuid(), name, email, group, ProposalFixture.Now);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, DateTimeOffset.UtcNow);
        if (cancelled)
        {
            booking.Cancel();
        }

        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, appointmentTypeId);

        context.Attendees.Add(attendee);
        context.Bookings.Add(booking);
        context.BookingAppointments.Add(appointment);
    }

    /// <summary>A fixed clock pinning transitional-location today to 2026-09-07 for workspace tests.</summary>
    private sealed class TestClock : IClock
    {
        /// <inheritdoc/>
        public DateTimeOffset UtcNow => new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

        /// <inheritdoc/>
        public DateTimeOffset NowAtTransitionalLocation => UtcNow;

        /// <inheritdoc/>
        public DateOnly TodayAtTransitionalLocation => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => instant;
    }
}
`````
