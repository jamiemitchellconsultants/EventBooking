using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Common;
using EventBooking.Domain.EmployeeGroups;

namespace EventBooking.Domain.Tests.Candidates;

public class CandidateStatusTests
{
    /// <summary>Builds a DAT-only group; lifecycle tests need a mapping, not an identity.</summary>
    private static EmployeeGroup DatOnly() =>
        EmployeeGroup.Define(
            Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);

    private static Candidate NewCandidate() =>
        Candidate.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", DatOnly());

    private static Candidate InvitedCandidate()
    {
        var candidate = NewCandidate();
        candidate.MarkInvited();
        return candidate;
    }

    [Fact]
    public void ANotYetInvitedCandidateCanBeInvited()
    {
        var candidate = NewCandidate();

        candidate.MarkInvited();

        Assert.Equal(CandidateStatus.Invited, candidate.Status);
    }

    [Fact]
    public void ACandidateWithNoEligibleSlotsBecomesAwaitingAvailability()
    {
        var candidate = NewCandidate();

        candidate.MarkAwaitingAvailability();

        Assert.Equal(CandidateStatus.AwaitingAvailability, candidate.Status);
    }

    [Fact]
    public void AnAwaitingCandidateCanBeInvitedOnceSlotsAppear()
    {
        var candidate = NewCandidate();
        candidate.MarkAwaitingAvailability();

        candidate.MarkInvited();

        Assert.Equal(CandidateStatus.Invited, candidate.Status);
    }

    [Fact]
    public void ReInvitingAnAlreadyInvitedCandidateIsAllowed()
    {
        var candidate = InvitedCandidate();

        candidate.MarkInvited();

        Assert.Equal(CandidateStatus.Invited, candidate.Status);
    }

    [Fact]
    public void OnlyAnInvitedCandidateCanBecomeBooked()
    {
        var candidate = InvitedCandidate();
        candidate.MarkBooked();
        Assert.Equal(CandidateStatus.Booked, candidate.Status);

        var notInvited = NewCandidate();
        var ex = Assert.Throws<DomainException>(() => notInvited.MarkBooked());
        Assert.Equal("A candidate cannot move from NotYetInvited to Booked.", ex.Message);
    }

    [Fact]
    public void OnlyAnInvitedCandidateCanRunOutOfRetries()
    {
        var candidate = InvitedCandidate();
        candidate.MarkNoResponse();
        Assert.Equal(CandidateStatus.NoResponseNeedsFollowUp, candidate.Status);

        var booked = InvitedCandidate();
        booked.MarkBooked();
        Assert.Throws<DomainException>(() => booked.MarkNoResponse());
    }

    [Fact]
    public void AFollowUpCandidateCanBeManuallyReInvited()
    {
        var candidate = InvitedCandidate();
        candidate.MarkNoResponse();

        candidate.MarkInvited();

        Assert.Equal(CandidateStatus.Invited, candidate.Status);
    }

    [Fact]
    public void CancellingABookingReturnsTheCandidateToNotYetInvited()
    {
        var candidate = InvitedCandidate();
        candidate.MarkBooked();

        candidate.ResetToNotYetInvited();

        Assert.Equal(CandidateStatus.NotYetInvited, candidate.Status);
    }

    [Fact]
    public void ANotYetInvitedCandidateCannotBeResetAgain()
    {
        var candidate = NewCandidate();

        var ex = Assert.Throws<DomainException>(() => candidate.ResetToNotYetInvited());
        Assert.Equal("A candidate cannot move from NotYetInvited to NotYetInvited.", ex.Message);
    }

}
