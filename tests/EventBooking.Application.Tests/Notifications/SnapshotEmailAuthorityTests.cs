using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Notifications;

/// <summary>Verifies candidate emails name exactly their persisted requirement snapshot.</summary>
public sealed class SnapshotEmailAuthorityTests
{
    private static readonly Candidate Candidate = Candidate.Create(
        Guid.NewGuid(), "Amara", "amara@example.com",
        EmployeeGroup.Define(
            EmployeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting]));
    private static readonly ConfirmedSlot Slot = ConfirmedSlot.CreateImported(
        Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 10, 10), new TimeOnly(9, 0)),
        new Dictionary<Guid, int>
        {
            [AppointmentTypeIds.DrugAndAlcoholTesting] = 10,
            [AppointmentTypeIds.MedicalCheckUp] = 10,
            [AppointmentTypeIds.UniformFitting] = 10,
        });
    private static readonly CandidatePortalOptions Portal = new(
        "https://booking.example", "Head Office", "recruitment@example.com");

    /// <summary>An Invite names only a one-type snapshot and uses singular recovery copy.</summary>
    [Fact]
    public void RecoveryInviteUsesSnapshotAndSingularCopy()
    {
        var email = CandidateEmailComposer.Invite(
            Candidate,
            [AppointmentTypeIds.MedicalCheckUp],
            [Slot, Slot, Slot],
            "https://booking.example/book/token",
            isReinvite: false,
            isRecovery: true);

        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.DoesNotContain("Uniform Fitting", email.TextBody);
        Assert.Contains("missed appointment", email.TextBody, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Booking confirmation names a two-type Booking snapshot, not all Candidate types.</summary>
    [Fact]
    public void ConfirmationUsesBookingSnapshot()
    {
        var email = CandidateEmailComposer.BookingConfirmation(
            Candidate,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            Slot,
            "https://booking.example/manage/token",
            Portal);

        Assert.Contains("Drug & Alcohol Testing", email.TextBody);
        Assert.Contains("Uniform Fitting", email.TextBody);
        Assert.DoesNotContain("Medical Check-up", email.TextBody);
    }

    /// <summary>Cancellation names only the affected Booking snapshot.</summary>
    [Fact]
    public void CancellationUsesAffectedBookingSnapshot()
    {
        var email = CandidateEmailComposer.SlotCancelled(
            Candidate, [AppointmentTypeIds.MedicalCheckUp], Slot);

        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.DoesNotContain("Drug & Alcohol Testing", email.TextBody);
    }

    /// <summary>A three-type Invite names every snapshot type in code order.</summary>
    [Fact]
    public void ThreeTypeInviteNamesEverySnapshotType()
    {
        var email = CandidateEmailComposer.Invite(
            Candidate,
            [AppointmentTypeIds.UniformFitting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.DrugAndAlcoholTesting],
            [Slot, Slot, Slot],
            "https://booking.example/book/token",
            isReinvite: true,
            isRecovery: false);

        Assert.Contains("Drug & Alcohol Testing", email.TextBody);
        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.Contains("Uniform Fitting", email.TextBody);
        Assert.Contains("We have not heard back", email.TextBody);
    }

    /// <summary>A two-type cancellation names both snapshot types with plural copy.</summary>
    [Fact]
    public void TwoTypeCancellationUsesPluralCopy()
    {
        var email = CandidateEmailComposer.SlotCancelled(
            Candidate,
            [AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.DrugAndAlcoholTesting],
            Slot);

        Assert.Contains("Medical Check-up", email.TextBody);
        Assert.Contains("Drug & Alcohol Testing", email.TextBody);
        Assert.DoesNotContain("Uniform Fitting", email.TextBody);
        Assert.Contains("your appointments on", email.TextBody);
    }
}
