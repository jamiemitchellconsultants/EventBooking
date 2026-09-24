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

    /// <summary>Empty, duplicate, and oversized snapshots are rejected.</summary>
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
        { [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
            AppointmentTypeIds.UniformFitting, AppointmentTypeIds.DrugAndAlcoholTesting] },
    };

    /// <summary>
    /// Admin-managed ids flow: the fixed-set guard is gone, so a snapshot may name an
    /// appointment type the fixed three never contained. Existence is checked against the
    /// type repository by the handler, not by the domain.
    /// </summary>
    [Fact]
    public void AdminManagedIdsAreAccepted()
    {
        var managed = Guid.NewGuid();

        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            Options,
            [managed],
            0);

        Assert.Equal([managed], invite.RequiredAppointmentTypeIds);
    }
}
